using Converter.Ir;
using Converter.Review;
using Converter.SimaticMl;

namespace Converter.Preflight;

// Composition, not new analysis: parses with the real parsers, converts with the real writers
// (FlgNetBuilder / SidecarSynthesizer / DbSourceWriter / …), folds in `converter review`'s own
// findings, and resolves every global tag root via TagReferences + the ProjectIndex — the
// pipeline's "exists, verified by grep" rule, mechanized. A target file that fails to parse is
// itself a pre-flight finding (that's exactly what pre-flight is for), so a batch never aborts.
public static class PreflightRunner
{
    public static PreflightReport Run(IReadOnlyList<string> paths, string projectDir)
    {
        var index = ProjectIndex.Build(projectDir, paths);

        // FI-60 (2026-08-08). Preflight's convert pass used to synthesize with NO callee registry
        // and NO tag types, so a block containing a WIRED CALL always reported
        //     "cannot synthesize the wired CALL to '<X>' - the callee's interface is not
        //      available ... or pass --project <ir-dir>"
        // EVEN ON A RUN WHERE --project WAS PASSED. The advice in the message was the one thing
        // that could not help, because preflight already had the project and simply never used it.
        //
        // It is a false positive, not a missed defect — `to-xml --project` converted the same files
        // cleanly and the import and compile both passed — which makes it the worse kind: it fires
        // for anyone who adds a parameterised FC call, on a check whose whole value is that a
        // finding means something.
        var callees = Program.BuildCalleeRegistry(paths, projectDir);
        var tagTypes = Program.BuildTagTypeRegistry(paths, projectDir);

        var files = paths.Select(path => PreflightFile(path, index, callees, tagTypes)).ToList();
        return new PreflightReport(files, index.Warnings);
    }

    private static FilePreflight PreflightFile(
        string path, ProjectIndex index, CalleeInterfaceRegistry callees, TagTypeRegistry tagTypes)
    {
        var findings = new List<PreflightFinding>();
        string? name = null;

        try
        {
            var text = File.ReadAllText(path);

            if (text.StartsWith("DB ", StringComparison.Ordinal))
            {
                var db = DbIrParser.ParseDb(text);
                name = db.Name;
                CheckConvert(findings, "DB", () => DbSourceWriter.Write(db));
                if (db.InstanceOfName is not null && !index.ResolvesAsBlock(db.InstanceOfName))
                {
                    findings.Add(new PreflightFinding("instanceof", $"INSTANCEOF '{db.InstanceOfName}' does not resolve to any block in the project or batch."));
                }
            }
            else if (text.StartsWith("TYPE ", StringComparison.Ordinal))
            {
                var type = TypeIrParser.ParseType(text);
                name = type.Name;
                CheckConvert(findings, "UDT", () => PlcTypeSourceWriter.Write(type));
            }
            else if (text.StartsWith("TAGTABLE ", StringComparison.Ordinal))
            {
                var table = TagTableIrParser.ParseTagTable(text);
                name = table.Name;
                CheckConvert(findings, "tag table", () => PlcTagTableSourceWriter.Write(table));
            }
            else
            {
                name = PreflightBlock(text, index, findings, callees, tagTypes);
            }
        }
        catch (Exception ex) when (ex is SimaticMlFormatException or UnsupportedConstructException or NonReducibleNetworkException or IrFormatException)
        {
            findings.Add(new PreflightFinding("parse", $"{ex.GetType().Name}: {ex.Message}"));
            return new FilePreflight(path, name, findings);
        }

        AppendReviewFindings(findings, path);
        return new FilePreflight(path, name, findings);
    }

    private static string PreflightBlock(
        string text, ProjectIndex index, List<PreflightFinding> findings,
        CalleeInterfaceRegistry callees, TagTypeRegistry tagTypes)
    {
        IrBlock block;
        IReadOnlyList<NetworkSidecar>? sidecars;

        if (ProjectIndex.HasSidecarSection(text))
        {
            (block, sidecars) = IrParser.ParseBlock(text);
        }
        else
        {
            block = IrParser.ParseBlockWithoutSidecar(text);
            try
            {
                sidecars = SidecarSynthesizer.SynthesizeBlock(block, callees, tagTypes);
            }
            catch (UnsupportedSynthesisConstructException ex)
            {
                // Real limitation of the sidecar-less path, worth knowing *before* attempting
                // `to-xml --synthesize` — but tag/call resolution below still runs.
                findings.Add(new PreflightFinding("convert", $"not synthesizable without a real sidecar: {ex.Message}"));
                sidecars = null;
            }
        }

        if (sidecars is not null)
        {
            for (var i = 0; i < block.Networks.Count; i++)
            {
                var network = block.Networks[i];
                try
                {
                    var flgNet = FlgNetBuilder.Build(network, sidecars[i]);

                    // FI-27: the built FlgNet serialized to XML must carry its instruction <Parts> in
                    // wire-graph flow order (else a live TIA import rejects it). Validated here, offline,
                    // because the Normalizer masks raw Part order from every equivalence oracle.
                    var emitted = FlowOrderCheck.ReadEmittedPartUIds(FlgNetWriter.Write(flgNet));
                    var flowFinding = FlowOrderCheck.Validate(network.Number, flgNet, emitted);
                    if (flowFinding is not null)
                    {
                        findings.Add(flowFinding);
                    }
                }
                catch (Exception ex) when (ex is SimaticMlFormatException or UnsupportedConstructException or NonReducibleNetworkException or IrFormatException)
                {
                    findings.Add(new PreflightFinding("convert", $"network {network.Number}: {ex.GetType().Name}: {ex.Message}"));
                }
            }
        }

        // Locals: the block's own declared names resolve internally, everything else must
        // resolve in the project/batch index — the pipeline's `exists`/`proposed` line.
        var locals = new HashSet<string>(StringComparer.Ordinal);
        foreach (var members in new[] { block.InputMembers, block.OutputMembers, block.InOutMembers, block.StaticMembers, block.TempMembers, block.ConstantMembers })
        {
            foreach (var member in members ?? Array.Empty<DbMember>())
            {
                locals.Add(member.Name);
            }
        }

        var reportedRoots = new HashSet<string>(StringComparer.Ordinal);
        foreach (var network in block.Networks)
        {
            foreach (var tagPath in TagReferences.AllTagPaths(network))
            {
                var root = AccessNode.FromDottedPath(0, "GlobalVariable", tagPath).ComponentPath[0];
                if (locals.Contains(root) || index.ResolvesAsTagRoot(root) || !reportedRoots.Add(root))
                {
                    continue;
                }

                findings.Add(new PreflightFinding("tag", $"network {network.Number}: tag root '{root}' (path '{tagPath}') does not resolve to a local declaration, project DB, tag-table entry, or batch file."));
            }

            foreach (var call in network.Calls)
            {
                if (!index.ResolvesAsBlock(call.BlockName))
                {
                    findings.Add(new PreflightFinding("call", $"network {network.Number}: CALL target '{call.BlockName}' does not resolve to any block in the project or batch."));
                }
            }
        }

        return block.Name;
    }

    private static void CheckConvert(List<PreflightFinding> findings, string label, Action write)
    {
        try
        {
            write();
        }
        catch (Exception ex) when (ex is SimaticMlFormatException or UnsupportedConstructException or IrFormatException)
        {
            findings.Add(new PreflightFinding("convert", $"{label} does not convert: {ex.GetType().Name}: {ex.Message}"));
        }
    }

    private static void AppendReviewFindings(List<PreflightFinding> findings, string path)
    {
        var report = ReviewRunner.ReviewFiles(new[] { path }, ignoreErrors: true);
        foreach (var file in report.Files)
        {
            foreach (var finding in file.Findings)
            {
                var location = finding.NetworkNumber is int n ? $"network {n}: " : string.Empty;
                findings.Add(new PreflightFinding($"review:{finding.RuleId}", $"{location}[{finding.Severity}] {finding.Description}"));
            }
        }
    }
}
