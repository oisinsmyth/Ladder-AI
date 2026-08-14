using Converter.Ir;
using Converter.Review;
using Converter.TagStatus;
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

        // The harness scope is built over the batch AND the project export together, so a tag's
        // referrers are looked for in the whole corpus rather than only in the files being flown.
        // Preflight is a filter before the compile gate; a generated harness object drawing 20+
        // naming findings here is exactly the noise that gets a filter switched off.
        var harnessScope = HarnessScope.Build(paths.Concat(Directory.EnumerateFiles(projectDir, "*.ir")));

        var files = paths.Select(path => PreflightFile(path, index, callees, tagTypes, harnessScope)).ToList();
        return new PreflightReport(files, index.Warnings);
    }

    private static FilePreflight PreflightFile(
        string path, ProjectIndex index, CalleeInterfaceRegistry callees, TagTypeRegistry tagTypes, HarnessScope harnessScope)
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

        AppendReviewFindings(findings, path, tagTypes, harnessScope);
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

        // literal-fit (2026-08-13): a literal too wide for the member or parameter it is written
        // into. `MOVE(IN := 70000) => <an Int member>` passed pre-flight CLEAN before this, and TIA
        // rejects it — see LiteralFitCheck for why the check lives here and not in `converter review`.
        findings.AddRange(LiteralFitCheck.Check(block, tagTypes, callees));

        // Locals: the block's own declared names resolve internally, everything else must
        // resolve in the project/batch index — the pipeline's `exists`/`proposed` line.
        var locals = new Dictionary<string, DbMember>(StringComparer.Ordinal);
        foreach (var members in new[] { block.InputMembers, block.OutputMembers, block.InOutMembers, block.StaticMembers, block.TempMembers, block.ConstantMembers })
        {
            foreach (var member in members ?? Array.Empty<DbMember>())
            {
                locals[member.Name] = member;
            }
        }

        var reportedRoots = new HashSet<string>(StringComparer.Ordinal);
        var reportedPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var network in block.Networks)
        {
            foreach (var tagPath in TagReferences.AllTagPaths(network))
            {
                var root = AccessNode.FromDottedPath(0, "GlobalVariable", tagPath).ComponentPath[0];
                var isLocal = locals.TryGetValue(root, out var localDeclaration);
                if (!isLocal && !index.ResolvesAsTagRoot(root))
                {
                    if (reportedRoots.Add(root))
                    {
                        findings.Add(new PreflightFinding("tag", $"network {network.Number}: tag root '{root}' (path '{tagPath}') does not resolve to a local declaration, project DB, tag-table entry, or batch file."));
                    }

                    continue;
                }

                // MEMBER level (2026-08-14). The root resolving was the WHOLE check until today, so a
                // block reading `DB_Settings.AlsoDoesNotExist`, `iDB_PusherControl.IO.NoSuchMember` and
                // `IO.NoSuchMember` reported CLEAN, exit 0 — while `tagstatus`, given the same three
                // paths and the same --project, refused two of them by name. Hard rule 3 says never
                // invent tags; `preflight` is the gate the coding workflow actually runs, and
                // `tagstatus` is the one somebody has to remember to run by hand on a name they
                // already suspect. TagStatusTests' own header asserted the two "never disagree".
                //
                // Same walk, same registry, same corpus as tagstatus — MemberPathResolver — so the
                // two cannot drift. NotEnumerable deliberately does NOT gate: a root whose member
                // namespace is genuinely unknowable (an IEC timer static, a UDT absent from the
                // export) is an honest "could not verify", and gating on it would manufacture the
                // opposite false accusation this check exists to remove.
                if (index.ResolvesAsTagRoot(tagPath))
                {
                    continue;   // whole-name-first: a literal-dot tag (Clock_0.5Hz) has no member part
                }

                var resolution = isLocal
                    ? MemberPathResolver.ResolveUnderLocal(tagPath, localDeclaration!, tagTypes)
                    : MemberPathResolver.Resolve(tagPath, tagTypes);

                var problem = resolution.Outcome switch
                {
                    MemberPathOutcome.MemberAbsent =>
                        $"member path '{tagPath}' does not resolve: {resolution.Detail}",
                    MemberPathOutcome.IndexOutOfRange =>
                        $"member path '{tagPath}' names an element outside the declared bounds: {resolution.Detail}",
                    _ => null,
                };

                if (problem is not null && reportedPaths.Add(tagPath))
                {
                    findings.Add(new PreflightFinding("tag", $"network {network.Number}: {problem}"));
                }
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

    // The tagTypes registry is passed through (2026-08-13). Preflight has ALWAYS built one — it
    // requires --project — and never gave it to the reviewer, so C-118/C-122/C-125 recorded
    // themselves unrunnable on every preflight this tool has ever done: three cross-file rules,
    // silently absent from the check that runs before every import. Same defect class as the
    // tag-table hole this change is about, one call site away from it.
    private static void AppendReviewFindings(List<PreflightFinding> findings, string path, TagTypeRegistry tagTypes, HarnessScope harnessScope)
    {
        var report = ReviewRunner.ReviewFiles(new[] { path }, ignoreErrors: true, tagTypes, harnessScope);
        foreach (var file in report.Files)
        {
            foreach (var finding in file.Findings)
            {
                var location = finding.NetworkNumber is int n ? $"network {n}: " : string.Empty;
                findings.Add(new PreflightFinding($"review:{finding.RuleId}", $"{location}[{finding.Severity}] {finding.Description}"));
            }

            // Reported, not dropped — and under a check name that says plainly it does not gate.
            // *** A SUPPRESSION NOBODY CAN SEE IS ONE STEP FROM A SUPPRESSION THAT HIDES ***, which
            // is the reason these are printed at all rather than filtered out upstream. The one-line
            // summary is emitted even at zero, for the same reason the report's own HARNESS-SCOPE
            // line is: a count of zero is a different fact from an absent line.
            // Announced when something was actually exempted, or when the file was classified as
            // harness at all. A plant file's verdict is NOT repeated per file here — pre-flight runs
            // over whole batches and a line per file would be pure noise — but the run-level count is
            // printed unconditionally including the zero (PreflightOutputFormatter), so "nothing was
            // exempted" and "the classifier never ran" are still distinguishable. The full per-file
            // verdict, plant ones included, is on `converter review`'s SCOPE line.
            if (file.Harness is { } verdict && (verdict.Class == HarnessClass.Harness || file.HarnessScopedFindings.Count > 0))
            {
                findings.Add(new PreflightFinding(
                    "review:harness-scope",
                    $"{verdict.Class}: {verdict.Basis}. {file.HarnessScopedFindings.Count} C-001/C-201 finding(s) reported without gating.",
                    Gates: false));
            }

            foreach (var finding in file.HarnessScopedFindings)
            {
                var location = finding.NetworkNumber is int n ? $"network {n}: " : string.Empty;
                findings.Add(new PreflightFinding(
                    "review:harness-scope",
                    $"{location}{finding.RuleId} [{finding.Severity}] {finding.Description}",
                    Gates: false));
            }
        }

        // A rule that was not judged is a pre-flight finding in its own right. Preflight is a filter
        // whose whole value is that passing it means something; "18 rules reported nothing because
        // nobody implemented them" must not be one of the ways it passes.
        foreach (var entry in ReviewOutcome.UncheckedRules(report))
        {
            findings.Add(new PreflightFinding($"review:{entry.RuleId}", $"[NOT CHECKED] {entry.RuleId} was not judged for this file - {entry.Reason}"));
        }
    }
}
