using Converter.Ir;
using Converter.SimaticMl;

namespace Converter.Digest;

// Builds a FileDigest per .ir file. Reuses the exact DB /TYPE /TAGTABLE prefix dispatch
// ReviewRunner already uses for .ir text, and TagReferences/AccessNode.FromDottedPath for tag
// enumeration and root extraction — deliberately no new traversal or path-splitting logic (a
// naive Split('.') here would reintroduce the Clock_0.5Hz literal-dot bug FromDottedPath fixed).
public static class DigestBuilder
{
    public static DigestReport DigestFiles(IReadOnlyList<string> paths, bool ignoreErrors)
    {
        var results = new List<FileDigest>();

        foreach (var path in paths)
        {
            try
            {
                results.Add(DigestFile(path));
            }
            catch (Exception ex) when (ex is SimaticMlFormatException or UnsupportedConstructException or NonReducibleNetworkException or IrFormatException)
            {
                if (!ignoreErrors)
                {
                    throw new DigestFileException($"{path}: {ex.GetType().Name}: {ex.Message}");
                }

                results.Add(new FileDigest(
                    path, "?", null, null, null, null,
                    Array.Empty<SectionDigest>(), Array.Empty<CallSiteDigest>(), Array.Empty<NetworkDigest>(), Array.Empty<string>(),
                    $"{ex.GetType().Name}: {ex.Message}"));
            }
        }

        return new DigestReport(results);
    }

    private static FileDigest DigestFile(string path)
    {
        var text = File.ReadAllText(path);

        if (text.StartsWith("DB ", StringComparison.Ordinal))
        {
            return DigestDb(path, DbIrParser.ParseDb(text));
        }

        if (text.StartsWith("TYPE ", StringComparison.Ordinal))
        {
            return DigestType(path, TypeIrParser.ParseType(text));
        }

        if (text.StartsWith("TAGTABLE ", StringComparison.Ordinal))
        {
            return DigestTagTable(path, TagTableIrParser.ParseTagTable(text));
        }

        // The same decision the parsers themselves enforce (ParseBlock requires a SIDECAR
        // section, ParseBlockWithoutSidecar rejects one) — chosen by the section's presence so
        // both exported and freshly hand-authored, not-yet-round-tripped IR digest cleanly.
        var block = HasSidecarSection(text)
            ? IrParser.ParseBlock(text).Block
            : IrParser.ParseBlockWithoutSidecar(text);
        return DigestBlock(path, block);
    }

    private static bool HasSidecarSection(string text) =>
        text.Replace("\r\n", "\n").Split('\n').Any(line => line == "SIDECAR");

    private static FileDigest DigestBlock(string path, IrBlock block)
    {
        var sections = new List<SectionDigest>();
        AddSection(sections, "INPUT", block.InputMembers);
        AddSection(sections, "OUTPUT", block.OutputMembers);
        AddSection(sections, "INOUT", block.InOutMembers);
        AddSection(sections, "STATIC", block.StaticMembers);
        AddSection(sections, "TEMP", block.TempMembers);
        AddSection(sections, "CONSTANT", block.ConstantMembers);

        var calls = block.Networks
            .SelectMany(n => n.Calls)
            .GroupBy(c => c.BlockName, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new CallSiteDigest(
                g.Key,
                g.Count(),
                g.Select(c => c.InstancePath).OfType<string>().Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal).ToList()))
            .ToList();

        var networks = block.Networks
            .Select(n => new NetworkDigest(n.Number, n.Title, SummarizeStatements(n), NetworkSignature.Compute(n)))
            .ToList();

        var tagRoots = block.Networks
            .SelectMany(TagReferences.AllTagPaths)
            .Select(RootComponent)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(r => r, StringComparer.Ordinal)
            .ToList();

        return new FileDigest(path, block.Kind, block.Name, block.Number, null, block.Title, sections, calls, networks, tagRoots, null);
    }

    private static FileDigest DigestDb(string path, DbSource db)
    {
        var kind = db.InstanceOfName is null ? "GlobalDB" : "InstanceDB";
        var sections = new List<SectionDigest>();
        AddSection(sections, "INPUT", db.InputMembers);
        AddSection(sections, "OUTPUT", db.OutputMembers);
        AddSection(sections, "INOUT", db.InOutMembers);
        AddSection(sections, "MEMBERS", db.Members);

        return new FileDigest(
            path, kind, db.Name, db.Number, db.InstanceOfName, null, sections,
            Array.Empty<CallSiteDigest>(), Array.Empty<NetworkDigest>(), Array.Empty<string>(), null);
    }

    private static FileDigest DigestType(string path, PlcTypeSource type)
    {
        var sections = new List<SectionDigest>();
        AddSection(sections, "MEMBERS", type.Members);

        return new FileDigest(
            path, "UDT", type.Name, null, null, null, sections,
            Array.Empty<CallSiteDigest>(), Array.Empty<NetworkDigest>(), Array.Empty<string>(), null);
    }

    private static FileDigest DigestTagTable(string path, PlcTagTableSource table)
    {
        var tagLines = table.Tags
            .Select(t => $"{t.Name} : {t.DataTypeName} @ {t.LogicalAddress}")
            .ToList();
        var sections = new List<SectionDigest> { new("TAGS", tagLines) };

        return new FileDigest(
            path, "TAGTABLE", table.Name, null, null, null, sections,
            Array.Empty<CallSiteDigest>(), Array.Empty<NetworkDigest>(), Array.Empty<string>(), null);
    }

    private static void AddSection(List<SectionDigest> sections, string name, IReadOnlyList<DbMember>? members)
    {
        if (members is null || members.Count == 0)
        {
            return;
        }

        sections.Add(new SectionDigest(name, members.Select(FormatMember).ToList()));
    }

    private static string FormatMember(DbMember member) =>
        member.NestedMembers is { Count: > 0 } nested
            ? $"{member.Name} : {member.Datatype} ({nested.Count} nested)"
            : $"{member.Name} : {member.Datatype}";

    // First path component via the proven inverse of DottedPath — atomic literal-dot names
    // (Clock_0.5Hz and friends) stay whole. Scope is irrelevant to root extraction; UId 0 is a
    // placeholder (FromDottedPath does no registration, it just splits correctly).
    private static string RootComponent(string tagPath) =>
        AccessNode.FromDottedPath(0, "GlobalVariable", tagPath).ComponentPath[0];

    private static string SummarizeStatements(IrNetwork network)
    {
        var parts = new List<string>();
        Append(parts, "coil", network.Assignments.Count);
        Append(parts, "timer", network.Timers.Count);
        Append(parts, "move", network.Moves.Count);
        Append(parts, "wand", network.WordAnds.Count);
        Append(parts, "call", network.Calls.Count);
        Append(parts, "arith", network.Muls.Count);
        Append(parts, "convert", network.Converts.Count);
        Append(parts, "swap", network.Swaps.Count);
        Append(parts, "abs", network.AbsStatements.Count);
        Append(parts, "limit", network.Limits.Count);
        Append(parts, "tsub", network.TSubs.Count);
        Append(parts, "tconv", network.TConvs.Count);
        Append(parts, "calc", network.Calcs.Count);
        Append(parts, "moveblk", network.MoveBlkVariants.Count);
        Append(parts, "wait", network.Waits.Count);
        Append(parts, "fillblk", network.FillBlockIs.Count);
        Append(parts, "mb-master", network.ModbusMasters.Count);
        Append(parts, "mb-commload", network.ModbusCommLoads.Count);
        foreach (var group in network.FixedShapes.GroupBy(f => f.Instruction, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            Append(parts, group.Key.ToLowerInvariant(), group.Count());
        }

        return parts.Count == 0 ? "-" : string.Join(", ", parts);
    }

    private static void Append(List<string> parts, string label, int count)
    {
        if (count > 0)
        {
            parts.Add($"{label}:{count}");
        }
    }
}
