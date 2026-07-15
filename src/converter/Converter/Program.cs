using System.Xml.Linq;
using Converter.Ir;
using Converter.Review;
using Converter.Sanitize;
using Converter.SimaticMl;

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

        if (args.Length < 2 || args[0] is not ("to-ir" or "to-xml"))
        {
            Console.Error.WriteLine("Usage: converter to-ir|to-xml <file> [<file> ...]");
            Console.Error.WriteLine("       converter to-xml <file> [<file> ...] --synthesize   # no real SIDECAR needed; mints a fresh one (plain COIL AND/OR/NOT chains only)");
            Console.Error.WriteLine("       converter sanitize <file> --map <mapping.json> --out <path>");
            Console.Error.WriteLine("       converter review <file> [<file> ...] [--ignore-errors] [--json]");
            return 1;
        }

        var mode = args[0];
        var rest = args[1..];
        var synthesize = rest.Contains("--synthesize");
        var files = rest.Where(a => a != "--synthesize").ToArray();

        if (synthesize && mode != "to-xml")
        {
            Console.Error.WriteLine("--synthesize is only valid with 'to-xml' — a real SimaticML export always has real sidecar data, so synthesis is meaningless for 'to-ir'.");
            return 1;
        }

        foreach (var file in files)
        {
            try
            {
                if (mode == "to-ir")
                {
                    ConvertToIr(file);
                }
                else
                {
                    ConvertToXml(file, synthesize);
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

    private static void ConvertToIr(string sourcePath)
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
        var irText = IrSerializer.SerializeBlock(irBlock, sidecars);

        var outPath = Path.ChangeExtension(sourcePath, ".ir");
        File.WriteAllText(outPath, irText);
        Console.WriteLine($"{sourcePath} -> {outPath}");
    }

    private static void ConvertToXml(string sourcePath, bool synthesize = false)
    {
        var irText = File.ReadAllText(sourcePath);

        if (irText.StartsWith("DB ", StringComparison.Ordinal))
        {
            var db = DbIrParser.ParseDb(irText);
            var dbXml = DbSourceWriter.Write(db);
            var dbOutPath = Path.ChangeExtension(sourcePath, ".xml");
            dbXml.Save(dbOutPath);
            Console.WriteLine($"{sourcePath} -> {dbOutPath}");
            return;
        }

        if (irText.StartsWith("TYPE ", StringComparison.Ordinal))
        {
            var type = TypeIrParser.ParseType(irText);
            var typeXml = PlcTypeSourceWriter.Write(type);
            var typeOutPath = Path.ChangeExtension(sourcePath, ".xml");
            typeXml.Save(typeOutPath);
            Console.WriteLine($"{sourcePath} -> {typeOutPath}");
            return;
        }

        if (irText.StartsWith("TAGTABLE ", StringComparison.Ordinal))
        {
            var tagTable = TagTableIrParser.ParseTagTable(irText);
            var tagTableXml = PlcTagTableSourceWriter.Write(tagTable);
            var tagTableOutPath = Path.ChangeExtension(sourcePath, ".xml");
            tagTableXml.Save(tagTableOutPath);
            Console.WriteLine($"{sourcePath} -> {tagTableOutPath}");
            return;
        }

        IrBlock block;
        IReadOnlyList<NetworkSidecar> sidecars;
        if (synthesize)
        {
            block = IrParser.ParseBlockWithoutSidecar(irText);
            sidecars = SidecarSynthesizer.SynthesizeBlock(block);
        }
        else
        {
            (block, sidecars) = IrParser.ParseBlock(irText);
        }

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
        var xml = BlockSourceWriter.Write(blockSource, flgNetworks, compileUnitUIds, networkTitles, networkComments);

        var outPath = Path.ChangeExtension(sourcePath, ".xml");
        xml.Save(outPath);
        Console.WriteLine($"{sourcePath} -> {outPath}");
    }
}
