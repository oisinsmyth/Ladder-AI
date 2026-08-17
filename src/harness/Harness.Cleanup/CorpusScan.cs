using System.Text.RegularExpressions;

namespace Harness.Cleanup;

/// <summary>What sort of thing an <c>.ir</c> file declares itself to be.</summary>
public enum IrObjectKind
{
    /// <summary>Unusable zero value: a file whose first line named nothing recognisable.</summary>
    Unstated = 0,

    /// <summary>An <c>FB</c>, <c>FC</c> or <c>OB</c> — a code block.</summary>
    Block,

    /// <summary>A global data block.</summary>
    GlobalDb,

    /// <summary>A data block carrying <c>INSTANCEOF</c>. DB-7's fastest-growing kind.</summary>
    InstanceDb,

    /// <summary>A PLC data type. <b>Has no block number</b>, which is why X-J cannot own it.</summary>
    Type,

    /// <summary>A tag table. <b>Has no block number</b> either.</summary>
    TagTable,
}

/// <summary>One object as the corpus declares it. Nothing here is inferred from a filename.</summary>
/// <param name="Name">The object's OWN declared name, not its basename — the two differ in this corpus
/// (<c>DefaultTagTable.ir</c> declares <c>Default tag table</c>), and pairing on the filename is the
/// defect <c>drift-check</c> was repaired for on 2026-08-14.</param>
/// <param name="Number">The block number, where the object has a number space at all.</param>
/// <param name="InstanceOf">The FB this instance DB instantiates, if any.</param>
public sealed record IrObject(
    string Name,
    IrObjectKind Kind,
    string? SubKind,
    int? Number,
    string? InstanceOf,
    string FileName)
{
    /// <summary>
    /// X-J's ownership test — <b>the only thing that decides what DB-7's cleanup OWNS</b>, and
    /// deliberately not the same question as whether anything still references it.
    ///
    /// <para>§16.10: <i>"harness objects become recognisable by number in every listing, which also
    /// tells DB-7's cleanup what it owns"</i>, and 2026-08-14 declared the range. Ownership by number
    /// is a CONVENTION about scope; eligibility stays graph-proven. Conflating them would be the
    /// count-based rule DB-7's second rule forbids, wearing a different name.</para>
    /// </summary>
    public bool IsHarnessOwned => Number is >= HarnessNumberFloor and <= HarnessNumberCeiling;

    /// <summary>
    /// <b>True when this object could never be owned by number in either direction</b> — it has no
    /// number space at all, so X-J's rule cannot reach it whatever it is named. Reported, never
    /// silently dropped: an absence that is invisible is the one that becomes permanent.
    /// </summary>
    public bool IsUnaddressableByNumber => Kind is IrObjectKind.Type or IrObjectKind.TagTable;

    public const int HarnessNumberFloor = 9000;
    public const int HarnessNumberCeiling = 9999;
}

/// <summary>Reads an IR corpus directory into <see cref="IrObject"/>s.</summary>
public static class CorpusScan
{
    private static readonly Regex NumberLine = new(@"^\s*NUMBER\s+(-?\d+)\s*$", RegexOptions.Multiline);
    private static readonly Regex InstanceOfLine = new(@"^\s*INSTANCEOF\s+(\S+)\s*$", RegexOptions.Multiline);

    /// <summary>
    /// The result of a scan, <b>with its denominator</b>. <see cref="Unreadable"/> is separate from
    /// <see cref="Objects"/> because a file that could not be parsed is not a file that contained
    /// nothing — <c>drift-check</c> exited 0 over an entire corpus of unparseable <c>.ir</c> until
    /// 2026-08-14 for exactly that conflation.
    /// </summary>
    public sealed record Result(
        IReadOnlyList<IrObject> Objects,
        IReadOnlyList<string> Unreadable,
        int FilesSeen);

    public static Result Read(string directory)
    {
        // TopDirectoryOnly matches every other corpus walk in this repo. It is also the likeliest real
        // mistake — a path one level above the files reads as an empty corpus — which is why FilesSeen
        // is reported on every run rather than only when it is interesting.
        var files = Directory.GetFiles(directory, "*.ir", SearchOption.TopDirectoryOnly)
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToArray();

        var objects = new List<IrObject>();
        var unreadable = new List<string>();

        foreach (var file in files)
        {
            string text;
            try
            {
                text = File.ReadAllText(file);
            }
            catch (IOException ex)
            {
                unreadable.Add($"{Path.GetFileName(file)}: {ex.Message}");
                continue;
            }

            var parsed = ParseHeader(text, Path.GetFileName(file));
            if (parsed is null)
            {
                unreadable.Add($"{Path.GetFileName(file)}: first line declares no BLOCK/DB/TYPE/TAGTABLE object");
                continue;
            }

            objects.Add(parsed);
        }

        return new Result(objects, unreadable, files.Length);
    }

    /// <summary>
    /// Reads the object's identity out of its own content.
    ///
    /// <para>Four header forms exist in this corpus and they are not uniform:
    /// <c>BLOCK FB Name</c>, <c>DB Name</c>, <c>TYPE Name</c>, <c>TAGTABLE Name With Spaces</c>. The
    /// tag-table form is why the name is taken as the REST OF THE LINE rather than as a token.</para>
    /// </summary>
    public static IrObject? ParseHeader(string text, string fileName)
    {
        var first = text.Split('\n').FirstOrDefault()?.Trim('﻿', '\r', ' ', '\t');
        if (string.IsNullOrWhiteSpace(first)) return null;

        var number = NumberLine.Match(text) is { Success: true } n ? int.Parse(n.Groups[1].Value) : (int?)null;
        var instanceOf = InstanceOfLine.Match(text) is { Success: true } i ? i.Groups[1].Value : null;

        if (first.StartsWith("BLOCK ", StringComparison.Ordinal))
        {
            var parts = first.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) return null;
            return new IrObject(parts[2].Trim(), IrObjectKind.Block, parts[1], number, instanceOf, fileName);
        }

        if (first.StartsWith("DB ", StringComparison.Ordinal))
        {
            var name = first[3..].Trim();
            var kind = instanceOf is null ? IrObjectKind.GlobalDb : IrObjectKind.InstanceDb;
            return new IrObject(name, kind, null, number, instanceOf, fileName);
        }

        if (first.StartsWith("TYPE ", StringComparison.Ordinal))
            return new IrObject(first[5..].Trim(), IrObjectKind.Type, null, number, instanceOf, fileName);

        if (first.StartsWith("TAGTABLE ", StringComparison.Ordinal))
            return new IrObject(first[9..].Trim(), IrObjectKind.TagTable, null, number, instanceOf, fileName);

        return null;
    }
}
