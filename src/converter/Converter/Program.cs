using System.Xml.Linq;
using Converter.CrossCheck;
using Converter.Diff;
using Converter.Digest;
using Converter.DriftCheck;
using Converter.Ir;
using Converter.Preflight;
using Converter.ReuseScan;
using Converter.Review;
using Converter.Sanitize;
using Converter.SimaticMl;
using Converter.TagStatus;
using Converter.TargetScan;

namespace Converter;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length >= 1 && args[0] == "sanitize")
        {
            return RunSanitize(args[1..]);
        }

        if (args.Length >= 1 && args[0] == "review")
        {
            return RunReview(args[1..]);
        }

        if (args.Length >= 1 && args[0] == "digest")
        {
            return RunDigest(args[1..]);
        }

        if (args.Length >= 1 && args[0] == "preflight")
        {
            return RunPreflight(args[1..]);
        }

        if (args.Length >= 1 && args[0] == "tagstatus")
        {
            return RunTagStatus(args[1..]);
        }

        if (args.Length >= 1 && args[0] == "diff")
        {
            return RunDiff(args[1..]);
        }

        if (args.Length >= 1 && args[0] == "reuse-scan")
        {
            return RunReuseScan(args[1..]);
        }

        if (args.Length >= 1 && args[0] == "target-scan")
        {
            return RunTargetScan(args[1..]);
        }

        if (args.Length >= 1 && args[0] == "drift-check")
        {
            return RunDriftCheck(args[1..]);
        }

        if (args.Length >= 1 && args[0] == "cross-check")
        {
            return RunCrossCheck(args[1..]);
        }

        if (args.Length < 2 || args[0] is not ("to-ir" or "to-xml"))
        {
            Console.Error.WriteLine("Usage: converter to-ir|to-xml <file> [<file> ...] [--project <ir-dir>]");
            Console.Error.WriteLine("       converter to-xml   # derives the sidecar when the input has none (ADR-0005); uses a stored SIDECAR if present. --project supplies callee/tag types for derivation");
            Console.Error.WriteLine("       converter to-ir    # keeps the stored SIDECAR by default (safe); --no-sidecar omits it for a block already verified derivable (errors if unsynthesizable)");
            Console.Error.WriteLine("       converter to-xml <file> --synthesize   # force the derive path (errors if a SIDECAR is present)");
            Console.Error.WriteLine("       converter sanitize <file> --map <mapping.json> --out <path>");
            Console.Error.WriteLine("       converter review <file> [<file> ...] [--ignore-errors] [--json]");
            Console.Error.WriteLine("       converter digest <file> [<file> ...] [--ignore-errors] [--json]   # compact structural summary of .ir content (FI-15)");
            Console.Error.WriteLine("       converter preflight <file> [<file> ...] --project <ir-dir> [--json]   # static checks before any Portal round trip (FI-13; not the compile gate)");
            Console.Error.WriteLine("       converter tagstatus <name> [<name> ...] --project <ir-dir> [--json]   # classify tag names exists/proposed against the export (FI-24); exit 1 if any proposed");
            Console.Error.WriteLine("       converter diff <old.ir> <new.ir> [--only <network> ...] [--json]   # which networks changed, rest provably identical in IR (S7 invariance); with --only, exit 1 on any change outside the set");
            Console.Error.WriteLine("       converter reuse-scan --project <ir-dir> [--tag <tag> ...] [--kind <kind> ...] [--json]   # reuse-first: which blocks reference tag(s)/implement kind(s) (FI-29); exit 1 if any candidate found");
            Console.Error.WriteLine("       converter target-scan --requirements <register.md> --project <ir-dir> [--json]   # S6 new-block target gap-hunter: REQ x tag-status x as-built (FI-30); exit 1 if no clean candidate");
            Console.Error.WriteLine("       converter drift-check --project <ir-dir> --exports <simatic-ml-dir> [--json]   # detect ir<->simatic-ml export drift (FI-26); exit 1 if any block drifted");
            Console.Error.WriteLine("       converter cross-check --project <ir-dir> [--json]   # whole-project cross-block reference-graph FACTS the reviewer reasons over (FI-22); never verdicts; exit 0");
            return 1;
        }

        var mode = args[0];
        var rest = args[1..];
        var synthesize = rest.Contains("--synthesize");
        var noSidecar = rest.Contains("--no-sidecar");

        // --project <ir-dir> (optional, --synthesize only): supplies callee interfaces for wired-CALL
        // synthesis beyond the blocks in the batch itself.
        string? projectDir = null;
        var positional = new List<string>();
        for (var i = 0; i < rest.Length; i++)
        {
            if (rest[i] == "--synthesize" || rest[i] == "--no-sidecar")
            {
                continue;
            }

            if (rest[i] == "--project")
            {
                if (i + 1 >= rest.Length)
                {
                    Console.Error.WriteLine("Flag '--project' requires a value.");
                    return 1;
                }

                projectDir = rest[++i];
                continue;
            }

            positional.Add(rest[i]);
        }

        var files = positional.ToArray();

        if (synthesize && mode != "to-xml")
        {
            Console.Error.WriteLine("--synthesize is only valid with 'to-xml' — a real SimaticML export always has real sidecar data, so synthesis is meaningless for 'to-ir'.");
            return 1;
        }

        if (noSidecar && mode != "to-ir")
        {
            Console.Error.WriteLine("--no-sidecar is only valid with 'to-ir' — it omits the stored SIDECAR for a block already verified derivable.");
            return 1;
        }

        // Registries for synthesis. Built for both modes: `to-xml` derives the sidecar when the input
        // has none (ADR-0005), and `to-ir` uses the same synthesis to decide whether a block is
        // derivable (so it can omit the stored sidecar) — both need the callee interfaces (wired CALLs)
        // and tag/member types (typed boxes). A sidecar-carrying `to-xml` input ignores them.
        var callees = BuildCalleeRegistry(files, projectDir);
        var tagTypes = BuildTagTypeRegistry(files, projectDir);

        foreach (var file in files)
        {
            try
            {
                if (mode == "to-ir")
                {
                    ConvertToIr(file, noSidecar, callees, tagTypes);
                }
                else
                {
                    ConvertToXml(file, synthesize, callees, tagTypes);
                }
            }
            catch (Exception ex) when (ex is SimaticMlFormatException or UnsupportedConstructException or NonReducibleNetworkException or IrFormatException or UnsupportedSynthesisConstructException)
            {
                Console.Error.WriteLine($"{file}: {ex.GetType().Name}: {ex.Message}");
                return 1;
            }
        }

        return 0;
    }

    private static bool IsDbXml(XDocument document) =>
        document.Root?.Descendants().Any(e => e.Name.LocalName is "SW.Blocks.GlobalDB" or "SW.Blocks.InstanceDB") ?? false;

    private static bool IsTypeXml(XDocument document) =>
        document.Root?.Descendants().Any(e => e.Name.LocalName == "SW.Types.PlcStruct") ?? false;

    private static bool IsTagTableXml(XDocument document) =>
        document.Root?.Descendants().Any(e => e.Name.LocalName == "SW.Tags.PlcTagTable") ?? false;

    private static int RunSanitize(string[] args)
    {
        string? file = null;
        string? mapPath = null;
        string? outPath = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--map":
                    mapPath = RequireValue(args, ref i, "--map");
                    break;
                case "--out":
                    outPath = RequireValue(args, ref i, "--out");
                    break;
                default:
                    file ??= args[i];
                    break;
            }
        }

        if (file is null || mapPath is null || outPath is null)
        {
            Console.Error.WriteLine("Usage: converter sanitize <file> --map <mapping.json> --out <path>");
            return 1;
        }

        try
        {
            var document = XDocument.Load(file);
            var map = SanitizationMap.Load(mapPath);

            if (IsDbXml(document))
            {
                var db = DbSourceParser.Parse(document);
                var sanitizedDb = Sanitizer.ApplyToDb(db, map);
                var dbXml = DbSourceWriter.Write(sanitizedDb);
                dbXml.Save(outPath);
                Console.WriteLine($"{file} -> {outPath}");
                return 0;
            }

            if (IsTypeXml(document))
            {
                var type = PlcTypeSourceParser.Parse(document);
                var sanitizedType = Sanitizer.ApplyToType(type, map);
                var typeXml = PlcTypeSourceWriter.Write(sanitizedType);
                typeXml.Save(outPath);
                Console.WriteLine($"{file} -> {outPath}");
                return 0;
            }

            if (IsTagTableXml(document))
            {
                var tagTable = PlcTagTableSourceParser.Parse(document);
                var sanitizedTagTable = Sanitizer.ApplyToTagTable(tagTable, map);
                var tagTableXml = PlcTagTableSourceWriter.Write(sanitizedTagTable);
                tagTableXml.Save(outPath);
                Console.WriteLine($"{file} -> {outPath}");
                return 0;
            }

            var block = BlockSourceParser.Parse(document);
            var sanitized = Sanitizer.Apply(block, map);

            var flgNetworks = sanitized.CompileUnits.Select(u => u.Network).ToList();
            var compileUnitUIds = sanitized.CompileUnits.Select(u => u.UId).ToList();
            var networkTitles = sanitized.CompileUnits.Select(u => u.Title).ToList();
            var networkComments = sanitized.CompileUnits.Select(u => u.Comment).ToList();

            var xml = BlockSourceWriter.Write(sanitized, flgNetworks, compileUnitUIds, networkTitles, networkComments);
            xml.Save(outPath);
            Console.WriteLine($"{file} -> {outPath}");
            return 0;
        }
        catch (Exception ex) when (ex is SimaticMlFormatException or UnsupportedConstructException or SanitizationMapException)
        {
            Console.Error.WriteLine($"{file}: {ex.GetType().Name}: {ex.Message}");
            return 1;
        }
    }

    private static int RunReview(string[] args)
    {
        var files = new List<string>();
        var ignoreErrors = false;
        var json = false;

        foreach (var arg in args)
        {
            switch (arg)
            {
                case "--ignore-errors":
                    ignoreErrors = true;
                    break;
                case "--json":
                    json = true;
                    break;
                default:
                    files.Add(arg);
                    break;
            }
        }

        if (files.Count == 0)
        {
            Console.Error.WriteLine("Usage: converter review <file> [<file> ...] [--ignore-errors] [--json]");
            return 1;
        }

        ReviewReport report;
        try
        {
            report = ReviewRunner.ReviewFiles(files, ignoreErrors);
        }
        catch (ReviewFileException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        Console.WriteLine(json ? ReviewOutputFormatter.FormatJson(report) : ReviewOutputFormatter.FormatTable(report));

        var hasErrorFindings = report.Files.Any(f => f.Findings.Any(finding => finding.Severity == FindingSeverity.Error));
        var hasFileErrors = report.Files.Any(f => f.FileError is not null);
        return hasErrorFindings || hasFileErrors ? 1 : 0;
    }

    private static int RunDigest(string[] args)
    {
        var files = new List<string>();
        var ignoreErrors = false;
        var json = false;
        var fingerprint = false;

        foreach (var arg in args)
        {
            switch (arg)
            {
                case "--ignore-errors":
                    ignoreErrors = true;
                    break;
                case "--json":
                    json = true;
                    break;
                case "--fingerprint":
                    fingerprint = true;
                    break;
                default:
                    files.Add(arg);
                    break;
            }
        }

        if (files.Count == 0)
        {
            Console.Error.WriteLine("Usage: converter digest <file> [<file> ...] [--ignore-errors] [--json] [--fingerprint]");
            return 1;
        }

        DigestReport report;
        try
        {
            report = DigestBuilder.DigestFiles(files, ignoreErrors);
        }
        catch (DigestFileException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        Console.WriteLine(json ? DigestOutputFormatter.FormatJson(report) : DigestOutputFormatter.FormatText(report, fingerprint));

        return report.Files.Any(f => f.FileError is not null) ? 1 : 0;
    }

    private static int RunPreflight(string[] args)
    {
        var files = new List<string>();
        string? projectDir = null;
        var json = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--project":
                    projectDir = RequireValue(args, ref i, "--project");
                    break;
                case "--json":
                    json = true;
                    break;
                default:
                    files.Add(args[i]);
                    break;
            }
        }

        if (files.Count == 0 || projectDir is null)
        {
            Console.Error.WriteLine("Usage: converter preflight <file> [<file> ...] --project <ir-dir> [--json]");
            return 1;
        }

        if (!Directory.Exists(projectDir))
        {
            Console.Error.WriteLine($"--project directory not found: {projectDir}");
            return 1;
        }

        var report = PreflightRunner.Run(files, projectDir);
        Console.WriteLine(json ? PreflightOutputFormatter.FormatJson(report) : PreflightOutputFormatter.FormatText(report));

        return report.HasFindings ? 1 : 0;
    }

    private static int RunTagStatus(string[] args)
    {
        var names = new List<string>();
        string? projectDir = null;
        var json = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--project":
                    projectDir = RequireValue(args, ref i, "--project");
                    break;
                case "--json":
                    json = true;
                    break;
                default:
                    names.Add(args[i]);
                    break;
            }
        }

        if (names.Count == 0 || projectDir is null)
        {
            Console.Error.WriteLine("Usage: converter tagstatus <name> [<name> ...] --project <ir-dir> [--json]");
            return 1;
        }

        if (!Directory.Exists(projectDir))
        {
            Console.Error.WriteLine($"--project directory not found: {projectDir}");
            return 1;
        }

        var report = TagStatusRunner.Run(names, projectDir);
        Console.WriteLine(json ? TagStatusOutputFormatter.FormatJson(report) : TagStatusOutputFormatter.FormatText(report));

        return report.HasProposed ? 1 : 0;
    }

    private static int RunDiff(string[] args)
    {
        var paths = new List<string>();
        var onlyNetworks = new List<int>();
        var json = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--only":
                {
                    // Accept one or more network numbers after --only, space- and/or comma-separated
                    // (`--only 1 2`, `--only 1,2`), and --only may repeat. Consume following tokens while
                    // they parse as a network list; stop at the next flag or path. (Fixes the doc-vs-parser
                    // mismatch found in the gen-block-modify-fix validation, 2026-07-18 — the docs implied
                    // space-separated but the parser took only a single value.)
                    var consumedAny = false;
                    while (i + 1 < args.Length && TryParseNetworkList(args[i + 1], out var nets))
                    {
                        onlyNetworks.AddRange(nets);
                        i++;
                        consumedAny = true;
                    }

                    if (!consumedAny)
                    {
                        Console.Error.WriteLine("--only requires at least one network number (e.g. --only 1 2 or --only 1,2).");
                        return 1;
                    }

                    break;
                }
                case "--json":
                    json = true;
                    break;
                default:
                    paths.Add(args[i]);
                    break;
            }
        }

        if (paths.Count != 2)
        {
            Console.Error.WriteLine("Usage: converter diff <old.ir> <new.ir> [--only <network> ...] [--json]");
            return 1;
        }

        foreach (var path in paths)
        {
            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"File not found: {path}");
                return 1;
            }
        }

        DiffReport report;
        try
        {
            report = DiffRunner.Run(paths[0], paths[1], onlyNetworks);
        }
        catch (Exception ex) when (ex is IrFormatException or SimaticMlFormatException)
        {
            Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}");
            return 1;
        }

        Console.WriteLine(json ? DiffOutputFormatter.FormatJson(report) : DiffOutputFormatter.FormatText(report));

        // --only makes this an assertion: exit 1 if anything changed outside the declared set. With
        // no --only it's an informational report (exit 0) — a filter/inspection aid, never a gate on
        // its own.
        return report.HasInvarianceViolation ? 1 : 0;
    }

    private static int RunReuseScan(string[] args)
    {
        string? projectDir = null;
        var tags = new List<string>();
        var kinds = new List<string>();
        var json = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--project":
                    projectDir = RequireValue(args, ref i, "--project");
                    break;
                case "--tag":
                    var tag = RequireValue(args, ref i, "--tag");
                    if (tag is null)
                    {
                        return 1;
                    }

                    tags.Add(tag);
                    break;
                case "--kind":
                    var kind = RequireValue(args, ref i, "--kind");
                    if (kind is null)
                    {
                        return 1;
                    }

                    kinds.Add(kind);
                    break;
                case "--json":
                    json = true;
                    break;
                default:
                    Console.Error.WriteLine($"Unexpected argument: {args[i]}");
                    return 1;
            }
        }

        if (projectDir is null)
        {
            Console.Error.WriteLine("Usage: converter reuse-scan --project <ir-dir> [--tag <tag> ...] [--kind <kind> ...] [--json]");
            return 1;
        }

        if (!Directory.Exists(projectDir))
        {
            Console.Error.WriteLine($"--project directory not found: {projectDir}");
            return 1;
        }

        if (tags.Count == 0 && kinds.Count == 0)
        {
            Console.Error.WriteLine("reuse-scan needs at least one --tag or --kind to query for.");
            return 1;
        }

        var unknownKinds = kinds.Where(k => !ReuseScanRunner.KnownKinds.Contains(k)).ToList();
        if (unknownKinds.Count > 0)
        {
            Console.Error.WriteLine($"Unknown --kind value(s): {string.Join(", ", unknownKinds)}. Known kinds: {string.Join(", ", ReuseScanRunner.KnownKinds)}");
            return 1;
        }

        var report = ReuseScanRunner.Run(projectDir, tags, kinds);
        Console.WriteLine(json ? ReuseScanOutputFormatter.FormatJson(report) : ReuseScanOutputFormatter.FormatText(report));

        return report.HasMatches ? 1 : 0;
    }

    private static int RunTargetScan(string[] args)
    {
        string? requirementsPath = null;
        string? projectDir = null;
        var json = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--requirements":
                    requirementsPath = RequireValue(args, ref i, "--requirements");
                    break;
                case "--project":
                    projectDir = RequireValue(args, ref i, "--project");
                    break;
                case "--json":
                    json = true;
                    break;
                default:
                    Console.Error.WriteLine($"Unexpected argument: {args[i]}");
                    return 1;
            }
        }

        if (requirementsPath is null || projectDir is null)
        {
            Console.Error.WriteLine("Usage: converter target-scan --requirements <register.md> --project <ir-dir> [--json]");
            return 1;
        }

        if (!File.Exists(requirementsPath))
        {
            Console.Error.WriteLine($"--requirements file not found: {requirementsPath}");
            return 1;
        }

        if (!Directory.Exists(projectDir))
        {
            Console.Error.WriteLine($"--project directory not found: {projectDir}");
            return 1;
        }

        var report = TargetScanRunner.Run(requirementsPath, projectDir);
        Console.WriteLine(json ? TargetScanOutputFormatter.FormatJson(report) : TargetScanOutputFormatter.FormatText(report));

        return report.HasCandidates ? 0 : 1;
    }

    private static int RunCrossCheck(string[] args)
    {
        string? projectDir = null;
        var json = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--project":
                    projectDir = RequireValue(args, ref i, "--project");
                    break;
                case "--json":
                    json = true;
                    break;
                default:
                    Console.Error.WriteLine($"Unexpected argument: {args[i]}");
                    return 1;
            }
        }

        if (projectDir is null)
        {
            Console.Error.WriteLine("Usage: converter cross-check --project <ir-dir> [--json]");
            return 1;
        }

        if (!Directory.Exists(projectDir))
        {
            Console.Error.WriteLine($"--project directory not found: {projectDir}");
            return 1;
        }

        var report = CrossCheckRunner.Run(projectDir);
        Console.WriteLine(json ? CrossCheckOutputFormatter.FormatJson(report) : CrossCheckOutputFormatter.FormatText(report));

        // A facts provider, not a gate — always exit 0 (like the `converter review` dump the skills embed).
        return 0;
    }

    private static int RunDriftCheck(string[] args)
    {
        string? projectDir = null;
        string? exportsDir = null;
        var json = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--project":
                    projectDir = RequireValue(args, ref i, "--project");
                    break;
                case "--exports":
                    exportsDir = RequireValue(args, ref i, "--exports");
                    break;
                case "--json":
                    json = true;
                    break;
                default:
                    Console.Error.WriteLine($"Unexpected argument: {args[i]}");
                    return 1;
            }
        }

        if (projectDir is null || exportsDir is null)
        {
            Console.Error.WriteLine("Usage: converter drift-check --project <ir-dir> --exports <simatic-ml-dir> [--json]");
            return 1;
        }

        if (!Directory.Exists(projectDir))
        {
            Console.Error.WriteLine($"--project directory not found: {projectDir}");
            return 1;
        }

        if (!Directory.Exists(exportsDir))
        {
            Console.Error.WriteLine($"--exports directory not found: {exportsDir}");
            return 1;
        }

        var report = DriftCheckRunner.Run(projectDir, exportsDir);
        Console.WriteLine(json ? DriftCheckOutputFormatter.FormatJson(report) : DriftCheckOutputFormatter.FormatText(report));

        return report.HasDrift ? 1 : 0;
    }

    // Parses a single --only token: one network number or a comma-separated list ("1" or "1,2,3").
    // Returns false (so --only stops consuming) for anything not all-integer — the next flag or a path.
    private static bool TryParseNetworkList(string token, out List<int> networks)
    {
        networks = new List<int>();
        foreach (var part in token.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(part, out var n))
            {
                networks = new List<int>();
                return false;
            }

            networks.Add(n);
        }

        return networks.Count > 0;
    }

    private static string? RequireValue(string[] args, ref int i, string flag)
    {
        if (i + 1 >= args.Length)
        {
            Console.Error.WriteLine($"Flag '{flag}' requires a value.");
            return null;
        }

        i++;
        return args[i];
    }

    private static void ConvertToIr(
        string sourcePath, bool noSidecar, CalleeInterfaceRegistry callees, TagTypeRegistry tagTypes)
    {
        var document = XDocument.Load(sourcePath);

        if (IsDbXml(document))
        {
            var db = DbSourceParser.Parse(document);
            var dbIrText = DbIrSerializer.Serialize(db);
            var dbOutPath = Path.ChangeExtension(sourcePath, ".ir");
            File.WriteAllText(dbOutPath, dbIrText);
            Console.WriteLine($"{sourcePath} -> {dbOutPath}");
            return;
        }

        if (IsTypeXml(document))
        {
            var type = PlcTypeSourceParser.Parse(document);
            var typeIrText = TypeIrSerializer.Serialize(type);
            var typeOutPath = Path.ChangeExtension(sourcePath, ".ir");
            File.WriteAllText(typeOutPath, typeIrText);
            Console.WriteLine($"{sourcePath} -> {typeOutPath}");
            return;
        }

        if (IsTagTableXml(document))
        {
            var tagTable = PlcTagTableSourceParser.Parse(document);
            var tagTableIrText = TagTableIrSerializer.Serialize(tagTable);
            var tagTableOutPath = Path.ChangeExtension(sourcePath, ".ir");
            File.WriteAllText(tagTableOutPath, tagTableIrText);
            Console.WriteLine($"{sourcePath} -> {tagTableOutPath}");
            return;
        }

        var block = BlockSourceParser.Parse(document);

        var networks = new List<IrNetwork>();
        var sidecars = new List<NetworkSidecar>();

        foreach (var compileUnit in block.CompileUnits)
        {
            // Title drives the NETWORK line's own quoted label (S1 item 16, 2026-07-12) —
            // Comment is a genuinely separate field, carried through via `with` since
            // GraphReducer.Reduce doesn't need to know about it (it isn't consumed by reduction,
            // just threaded through to the output).
            var title = compileUnit.Title ?? string.Empty;
            var reduced = GraphReducer.Reduce(compileUnit.Network, networks.Count + 1, title, compileUnit.UId);
            networks.Add(reduced.Network with { Comment = compileUnit.Comment });
            sidecars.Add(reduced.Sidecar);
        }

        var irBlock = new IrBlock(
            block.RootUId, block.Kind, block.Name, block.Number, block.Language, block.Comment, networks, block.StaticMembers, block.TempMembers, block.Title,
            block.InputMembers, block.OutputMembers, block.InOutMembers, block.ConstantMembers, block.SecondaryType);

        // Derive-always (ADR-0005), corrected 2026-07-19: to-ir KEEPS the stored SIDECAR by default. A
        // real export can synthesise-but-diverge (e.g. array-index locals, Gap D — found in the
        // test-project001 FBs and MotorStarter), so "synthesis succeeds" is NOT proof the derived form
        // matches, and auto-omitting on that alone would silently corrupt such a block. Omitting is safe
        // only once the block is proven equivalent. `--no-sidecar` is the explicit opt-in — and, since the
        // ADR-0005 follow-on landed (2026-07-19), it VERIFIES that equivalence itself rather than trusting
        // the caller: it derives a sidecar from the readable form, rebuilds the SimaticML, and
        // Normalizer-compares it to the source export being converted, omitting the sidecar only if
        // semantically equivalent (else it errors and the block keeps its sidecar). Default (no flag) still
        // keeps the sidecar — flipping THAT to auto-omit-when-equivalent is a separate, deferred owner
        // decision.
        string irText;
        if (noSidecar)
        {
            irText = SynthesizeReadableVerified(irBlock, document, callees, tagTypes);
        }
        else
        {
            irText = IrSerializer.SerializeBlock(irBlock, sidecars);
        }

        var outPath = Path.ChangeExtension(sourcePath, ".ir");
        File.WriteAllText(outPath, irText);
        Console.WriteLine($"{sourcePath} -> {outPath}");
    }

    // The verified `--no-sidecar` path (ADR-0005 follow-on, 2026-07-19). Produces the readable-only IR for a
    // block only after PROVING it safe to drop the stored sidecar: derive a sidecar from the readable form
    // exactly as `to-xml` later will (serialize readable -> re-parse -> synthesize), rebuild the SimaticML,
    // and semantically compare it (the Normalizer) to `sourceDocument` — the very export being converted, so
    // the comparison is self-consistent and staleness-immune. Equivalent => the readable-only text is safe to
    // return. Not even synthesizable, or synthesizes-but-diverges => throw, and the caller keeps the stored
    // sidecar. This is the safe replacement for the old IsSynthesizable guard, which proved only that
    // synthesis didn't throw — necessary but, per ADR-0005's CriticalCaveat, NOT sufficient (a real block can
    // synthesise-but-diverge on gaps the reference corpus never exercised).
    internal static string SynthesizeReadableVerified(
        IrBlock block, XDocument sourceDocument, CalleeInterfaceRegistry? callees, TagTypeRegistry? tagTypes)
    {
        var readableText = IrSerializer.SerializeBlockReadable(block);
        var reparsed = IrParser.ParseBlockWithoutSidecar(readableText);

        IReadOnlyList<NetworkSidecar> synthSidecars;
        try
        {
            synthSidecars = SidecarSynthesizer.SynthesizeBlock(reparsed, callees, tagTypes);
        }
        catch (UnsupportedSynthesisConstructException ex)
        {
            // Genuinely unsynthesizable (an unsupported construct or an unresolvable operand type) — the old
            // IsSynthesizable failure mode. Reframe with the --no-sidecar context, preserving the reason.
            throw new UnsupportedSynthesisConstructException(
                $"{block.Name}: --no-sidecar requested but the block is not synthesizable — cannot omit the sidecar (it would not round-trip). {ex.Message}");
        }

        // Compare on documents that have BOTH been through an XML parse. The source export was loaded from
        // disk, but BuildBlockXml yields an in-memory tree whose node shape (text/whitespace nodes) differs
        // from a freshly-parsed one — and XNode.DeepEquals (inside the Normalizer) is sensitive to that, so
        // comparing the in-memory tree directly reports a spurious mismatch. Serialize-then-parse the synth
        // so both sides match the trusted offline parity comparison exactly (SynthesisParityRunner loads the
        // export and the synth both from file).
        var synthXml = XDocument.Parse(BuildBlockXml(reparsed, synthSidecars).ToString());
        if (!Normalizer.AreSemanticallyEquivalent(sourceDocument, synthXml))
        {
            throw new UnsupportedSynthesisConstructException(
                $"{block.Name}: --no-sidecar requested but the derived form is NOT semantically equivalent to the " +
                "source export — the block is not yet fully derivable, so its sidecar cannot be safely omitted (keep it). " +
                "See ADR-0005 / docs/notes/converter-synthesis-gaps.md.");
        }

        return readableText;
    }

    // Builds the callee-interface registry for wired-CALL synthesis from the batch's own block .ir files
    // (only "BLOCK …" files — DBs/UDTs/tag-tables have no callable interface) plus any --project export.
    // A file that won't parse is skipped here; a wired CALL that actually needs it then fails with a
    // clear error at synthesis time.
    internal static CalleeInterfaceRegistry BuildCalleeRegistry(IEnumerable<string> files, string? projectDir)
    {
        var texts = new List<string>();
        foreach (var file in files)
        {
            if (File.Exists(file))
            {
                texts.Add(File.ReadAllText(file));
            }
        }

        if (projectDir is not null && Directory.Exists(projectDir))
        {
            foreach (var file in Directory.EnumerateFiles(projectDir, "*.ir"))
            {
                texts.Add(File.ReadAllText(file));
            }
        }

        var blocks = new List<IrBlock>();
        foreach (var text in texts)
        {
            if (!text.StartsWith("BLOCK ", StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                blocks.Add(IrParser.HasSidecarSection(text)
                    ? IrParser.ParseBlock(text).Block
                    : IrParser.ParseBlockWithoutSidecar(text));
            }
            catch (IrFormatException)
            {
                // Unparseable — skip; a wired CALL that needs this callee will error clearly at synthesis.
            }
        }

        return CalleeInterfaceRegistry.FromBlocks(blocks);
    }

    // Builds the tag/member-type registry for typed box/compare synthesis from the batch's own .ir
    // files plus any --project export. TagTypeRegistry.FromFiles picks out the DB/UDT/tag-table files
    // (a block file contributes no operand types) and skips anything unparseable — an operand whose
    // type can't be resolved simply falls back to the builder's default rather than erroring here.
    internal static TagTypeRegistry BuildTagTypeRegistry(IEnumerable<string> files, string? projectDir)
    {
        var paths = new List<string>();
        foreach (var file in files)
        {
            if (File.Exists(file))
            {
                paths.Add(file);
            }
        }

        if (projectDir is not null && Directory.Exists(projectDir))
        {
            paths.AddRange(Directory.EnumerateFiles(projectDir, "*.ir"));
        }

        return TagTypeRegistry.FromFiles(paths);
    }

    private static void ConvertToXml(
        string sourcePath, bool synthesize = false,
        CalleeInterfaceRegistry? callees = null, TagTypeRegistry? tagTypes = null)
    {
        var irText = File.ReadAllText(sourcePath);
        var xml = BuildXmlFromIrText(irText, synthesize, callees, tagTypes);

        var outPath = Path.ChangeExtension(sourcePath, ".xml");
        xml.Save(outPath);
        Console.WriteLine($"{sourcePath} -> {outPath}");
    }

    // The full `.ir` text -> SimaticML XDocument dispatch, in-memory (no disk write). One code path for
    // every kind (DB / UDT / tag table / code block), shared by `to-xml` (which saves the result) and
    // `drift-check` (which Normalizer-compares it to a committed export). Block sidecar decision matches
    // ConvertToXml's original: `--synthesize` OR a sidecar-less input derives the sidecar (ADR-0005);
    // a stored SIDECAR is used as-is. Parse/synthesis exceptions propagate to the caller.
    internal static XDocument BuildXmlFromIrText(
        string irText, bool synthesize, CalleeInterfaceRegistry? callees, TagTypeRegistry? tagTypes)
    {
        if (irText.StartsWith("DB ", StringComparison.Ordinal))
        {
            return DbSourceWriter.Write(DbIrParser.ParseDb(irText));
        }

        if (irText.StartsWith("TYPE ", StringComparison.Ordinal))
        {
            return PlcTypeSourceWriter.Write(TypeIrParser.ParseType(irText));
        }

        if (irText.StartsWith("TAGTABLE ", StringComparison.Ordinal))
        {
            return PlcTagTableSourceWriter.Write(TagTableIrParser.ParseTagTable(irText));
        }

        IrBlock block;
        IReadOnlyList<NetworkSidecar> sidecars;
        if (synthesize || !IrParser.HasSidecarSection(irText))
        {
            block = IrParser.ParseBlockWithoutSidecar(irText);
            sidecars = SidecarSynthesizer.SynthesizeBlock(block, callees, tagTypes);
        }
        else
        {
            (block, sidecars) = IrParser.ParseBlock(irText);
        }

        return BuildBlockXml(block, sidecars);
    }

    // Rebuild the SimaticML XDocument for a block from its (stored or synthesized) sidecars — the shared
    // block->XML step used by both `to-xml` and the verified `--no-sidecar` equivalence check, so the two
    // paths produce byte-for-byte the same output from the same inputs.
    internal static XDocument BuildBlockXml(IrBlock block, IReadOnlyList<NetworkSidecar> sidecars)
    {
        var flgNetworks = new List<FlgNetwork>();
        var compileUnitUIds = new List<string>();
        var networkTitles = new List<string?>();
        var networkComments = new List<string?>();

        for (var i = 0; i < block.Networks.Count; i++)
        {
            var sidecar = sidecars[i];
            flgNetworks.Add(FlgNetBuilder.Build(block.Networks[i], sidecar));
            compileUnitUIds.Add(sidecar.CompileUnitUId);
            networkTitles.Add(string.IsNullOrEmpty(block.Networks[i].Title) ? null : block.Networks[i].Title);
            networkComments.Add(block.Networks[i].Comment);
        }

        var blockSource = new BlockSource(
            block.RootUId, block.Kind, block.Name, block.Number, block.Language, block.Comment, Array.Empty<CompileUnitSource>(), block.StaticMembers, block.TempMembers, block.Title,
            block.InputMembers, block.OutputMembers, block.InOutMembers, block.ConstantMembers, block.SecondaryType);
        return BlockSourceWriter.Write(blockSource, flgNetworks, compileUnitUIds, networkTitles, networkComments);
    }
}
