using System.Text.RegularExpressions;
using Converter.Ir;
using Converter.SimaticMl;

namespace Converter.Neighbours;

/// <summary>
/// 🔴 <b>THE NEIGHBOUR LIST, DERIVED FROM THE PROGRAM — not taken on somebody's word.</b>
///
/// <para><b>The defect, measured live on a running controller, 2026-08-23.</b> A generated mirror and
/// a hand-authored virtual panel both claimed registers 256–323 of one <c>%M</c> area. <b>53 panel
/// tags were overwritten bit for bit every scan</b>, among them the panel's master enable. Nothing
/// caught it, and the reason is not "a check was missing": the allocator bounds the mirror against
/// the DECLARED area and the register map proves the mirror's regions disjoint <i>from each other</i>.
/// Both ran. Both passed. <b>Both examined something real that was not the thing at risk.</b>
/// <c>ReservedRegion</c> closed the hole and its author wrote the limit into the binding document
/// where a reader meets it: <i>"Until something DERIVES the neighbour list from the deployed program,
/// this is a place to put the knowledge rather than a way to obtain it."</i> This obtains it.</para>
///
/// <para><b>Every claim carries the object that declares it.</b> Not decoration: "a refusal that
/// cannot say WHOSE space was hit sends the reader looking in the wrong place" — to the mirror, which
/// is the one place the problem is not.</para>
///
/// <para><b>A NEW VERB, deliberately not an extension of <c>cross-check</c>.</b> On the real corpus
/// <c>cross-check</c> emits 351 multi-writer facts and 250 dead-member facts, and a refusal-critical
/// fact does not go in that stream — most of its rows are a needle in a haystack of the tool's own
/// output. This one answers one question and prints its denominator.</para>
///
/// <para><b>Two sources, one shape.</b> Tag-table entries with absolute <c>%M</c> addresses, and every
/// <c>P#M…</c> area-pointer literal in any object's body. Both become a byte span plus an owner. The
/// byte→register conversion is the consumer's, through the <c>MirrorGeometry.ReservingBytes</c> it
/// already has — re-deriving it here would give the two halves of the check two chances to disagree
/// about the same arithmetic.</para>
///
/// <para>🔴 <b>WHAT THIS CANNOT POSSIBLY SEE.</b>
/// <list type="number">
/// <item><b>An occupant that reaches <c>%M</c> without DECLARING it</b> — an indirect access, a
/// pointer computed at runtime, an offset arrived at by arithmetic. The derivation is over
/// declarations in the IR, not over execution, and no amount of scanning changes that.</item>
/// <item>Anything outside the corpus it was handed.</item>
/// <item>Whether that corpus is the program on the controller. It reads files, never the CPU.</item>
/// </list>
/// A zero here is therefore "nothing DECLARED a claim in the corpus I read", and never "the area is
/// free".</para>
/// </summary>
public static class NeighbourRunner
{
    /// <summary>
    /// Every <c>P#…</c> area pointer in a line of IR, whatever statement holds it.
    ///
    /// <para><b>Text, after a successful parse has gated the file — and that is a choice, not a
    /// shortcut.</b> <c>IrNetwork</c> carries about twenty statement kinds and gains more; a
    /// structural walk that forgot one would drop a real claim and report a SHORT list as clean,
    /// which is exactly <c>Reachability.cs</c>'s silent <c>continue</c>, a shape this repo has already
    /// paid for. The parse still runs and still gates: a file that will not parse is NOT DERIVED. What
    /// the parse does not do is decide which shapes are worth looking inside.</para>
    /// </summary>
    private static readonly Regex PointerPattern = new(
        @"P#(?<addr>[A-Za-z0-9_.]+)[ \t]+(?<unit>[A-Za-z_][A-Za-z0-9_]*)[ \t]+(?<count>\d+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Any <c>P#</c> at all — so one the pattern above could not read is NAMED, not skipped past.</summary>
    private static readonly Regex AnyPointerPattern = new(@"P#", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static NeighbourReport Run(IReadOnlyList<string> paths, MarkerArea area)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(area);

        var files = new List<string>();
        var missing = new List<string>();

        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                files.AddRange(Directory.EnumerateFiles(path, "*.ir", SearchOption.TopDirectoryOnly));
            }
            else if (File.Exists(path))
            {
                files.Add(path);
            }
            else
            {
                // FI-44. A path naming nothing used to resolve to an empty sequence and vanish, leaving a
                // scan that examined less than it was asked to and said so nowhere.
                missing.Add(path);
            }
        }

        files = files.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p, StringComparer.Ordinal).ToList();

        if (missing.Count > 0)
        {
            return NeighbourReport.Refuse(area, files.Count, 0, 0, 0,
                $"{missing.Count} scope path(s) name nothing on disk: {string.Join(", ", missing)}. A scope that "
                + "matched nothing is not a corpus with no occupant in it — the two produce the same empty list and "
                + "are different facts, so the question is refused rather than answered from the part that resolved.");
        }

        var scan = new Scan(area);

        foreach (var file in files)
        {
            scan.Read(file);
        }

        return scan.Report(files.Count);
    }

    /// <summary>The per-run accumulator. One instance per <see cref="Run"/>; never shared.</summary>
    private sealed class Scan(MarkerArea area)
    {
        private readonly List<MarkerClaim> _neighbours = [];
        private readonly List<MarkerClaim> _declarations = [];
        private readonly List<string> _refusals = [];
        private readonly List<string> _unparseable = [];
        private readonly HashSet<string> _seen = new(StringComparer.Ordinal);

        private int _tagTables;
        private int _blocks;
        private int _others;
        private int _belowBase;
        private int _aboveTop;
        private int _nonMarkerPointers;
        private int _addressOnly;

        public void Read(string file)
        {
            string text;
            try
            {
                text = File.ReadAllText(file);
            }
            catch (IOException e)
            {
                _unparseable.Add($"{file}: could not be read ({e.Message})");
                return;
            }

            var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            string container;
            PlcTagTableSource? tagTable;

            try
            {
                container = ParseForItsNameAndCount(text, out tagTable);
            }
            catch (Exception ex) when (ex is SimaticMlFormatException or UnsupportedConstructException
                                           or NonReducibleNetworkException or IrFormatException)
            {
                _unparseable.Add($"{file}: would not parse ({ex.GetType().Name}: {ex.Message})");
                return;
            }

            if (tagTable is not null)
            {
                ReadTags(file, lines, tagTable);
            }

            ReadPointers(file, lines, container);
        }

        /// <summary>
        /// Parses the object far enough to know its NAME and its kind, and to prove the file is IR at
        /// all. Prefix dispatch, the same one <c>ProjectUsageGraph</c> and <c>served-area</c> use, so
        /// "this is a DB" and "this file declares nothing readable" stay different observations —
        /// and, unlike <c>served-area</c>, a DB or a UDT is COUNTED and SCANNED rather than skipped:
        /// this verb's claim is about the whole corpus it was handed, not about its code objects.
        /// </summary>
        private string ParseForItsNameAndCount(string text, out PlcTagTableSource? tagTable)
        {
            tagTable = null;

            if (text.StartsWith("TAGTABLE ", StringComparison.Ordinal))
            {
                _tagTables++;
                tagTable = TagTableIrParser.ParseTagTable(text);
                return tagTable.Name;
            }

            if (text.StartsWith("DB ", StringComparison.Ordinal))
            {
                _others++;
                return DbIrParser.ParseDb(text).Name;
            }

            if (text.StartsWith("TYPE ", StringComparison.Ordinal))
            {
                _others++;
                return TypeIrParser.ParseType(text).Name;
            }

            var block = IrParser.HasSidecarSection(text)
                ? IrParser.ParseBlock(text).Block
                : IrParser.ParseBlockWithoutSidecar(text);
            _blocks++;
            return block.Name;
        }

        // -----------------------------------------------------------------------------------------
        // Tag-table entries with an absolute %M address.
        // -----------------------------------------------------------------------------------------

        private void ReadTags(string file, string[] lines, PlcTagTableSource table)
        {
            foreach (var tag in table.Tags)
            {
                if (!tag.LogicalAddress.StartsWith("%M", StringComparison.Ordinal))
                {
                    continue;
                }

                var line = LineOfTag(lines, tag.Name);

                if (!MarkerTagAddress.TryBound(tag.LogicalAddress, out var start, out var length, out var addressBits))
                {
                    // The span could not be measured. If the START is known and sits at or above the
                    // area top the claim CANNOT reach the area — occupancy runs upward — so excluding
                    // it is sound and refusing it would be noise. Anything else is refused: a claim
                    // seen and not measured, quietly dropped, makes a SHORT list look complete.
                    if (start >= 0 && start >= area.TopByteExclusive)
                    {
                        _aboveTop++;
                        continue;
                    }

                    _refusals.Add(
                        $"tag '{tag.Name}' in tag table '{table.Name}' ({file}:{line}) is addressed at "
                        + $"`{tag.LogicalAddress}`, a %M form whose width this verb cannot read, so the run of bytes it "
                        + "claims cannot be bounded. Refused rather than skipped: a claim that is seen and not measured, "
                        + "dropped quietly, is what turns a SHORT occupancy list into one that looks complete.");
                    continue;
                }

                if (TypeBits(tag.DataTypeName) is int declared)
                {
                    if (declared != addressBits)
                    {
                        _refusals.Add(
                            $"tag '{tag.Name}' in tag table '{table.Name}' ({file}:{line}) is declared `{tag.DataTypeName}` "
                            + $"({declared} bit(s)) at `{tag.LogicalAddress}` ({addressBits} bit(s)). The two homes of its "
                            + "width DISAGREE — the address is what the CPU addresses and the type is what a reader "
                            + "believes — so the bytes it occupies are not derivable and the wider of the two is not "
                            + "assumed.");
                        continue;
                    }
                }
                else
                {
                    // Bounded by the address alone. Counted, because a reader comparing this list against
                    // a panel drawing should know which rows rest on one source rather than two.
                    _addressOnly++;
                }

                Add(new MarkerClaim(
                    NeighbourKind.Tag,
                    $"tag table '{table.Name}' tag '{tag.Name}' ({tag.LogicalAddress})",
                    table.Name,
                    tag.Name,
                    tag.LogicalAddress,
                    start,
                    length,
                    file,
                    line));
            }
        }

        // -----------------------------------------------------------------------------------------
        // P#M… area pointers, wherever they sit.
        // -----------------------------------------------------------------------------------------

        private void ReadPointers(string file, string[] lines, string container)
        {
            var network = 0;

            for (var i = 0; i < lines.Length; i++)
            {
                var raw = lines[i];

                if (raw.TrimStart().StartsWith("NETWORK ", StringComparison.Ordinal)
                    && int.TryParse(
                        raw.TrimStart()["NETWORK ".Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(),
                        System.Globalization.NumberStyles.None,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var parsedNetwork))
                {
                    network = parsedNetwork;
                }

                // Quoted text is PROSE — a title, a block comment, a network comment. A pointer
                // described in words is not a pointer, and counting one would refuse a map that is
                // fine, which is the failure an over-broad derivation causes.
                var line = StripQuoted(raw);
                if (line.Length == 0)
                {
                    continue;
                }

                var matched = 0;
                foreach (Match match in PointerPattern.Matches(line))
                {
                    matched++;
                    ReadOnePointer(file, i + 1, container, network, match.Value);
                }

                if (AnyPointerPattern.Matches(line).Count > matched)
                {
                    _refusals.Add(
                        $"{file}:{i + 1} carries a `P#` this scan could not read as an area pointer: `{line.Trim()}`. "
                        + "Refused rather than passed over — an area pointer nobody could read is an occupancy nobody "
                        + "measured, and a list that quietly omits it reports a partial answer as a whole one.");
                }
            }
        }

        private void ReadOnePointer(string file, int line, string container, int network, string text)
        {
            if (!AreaPointerParser.TryParse(text, out var pointer, out var why))
            {
                _refusals.Add($"{file}:{line} carries `{text}`, and {why} An area pointer that cannot be read is an "
                    + "occupancy that cannot be bounded.");
                return;
            }

            if (!pointer.IsMarker)
            {
                // A different address space entirely. Not a claim on THIS area, and counted so that a
                // reader can see the scan looked at it rather than never reaching it.
                _nonMarkerPointers++;
                return;
            }

            if (pointer.ByteLength is not int length)
            {
                if (pointer.BaseByte >= area.TopByteExclusive)
                {
                    _aboveTop++;
                    return;
                }

                _refusals.Add(
                    $"{file}:{line} in '{container}' carries `{text}`, whose unit `{pointer.Unit}` this verb cannot "
                    + "convert to a width, so the run of bytes it claims cannot be bounded. Refused rather than "
                    + "approximated: widening the unit table needs a real export to ground it, and guessing here puts "
                    + "an unmeasured band in a list a refusal is computed from.");
                return;
            }

            var owner = network > 0
                ? $"'{container}' network {network} area pointer {text}"
                : $"'{container}' area pointer {text}";

            Add(new MarkerClaim(
                NeighbourKind.AreaPointer, owner, container, text, text, pointer.BaseByte, length, file, line));
        }

        // -----------------------------------------------------------------------------------------

        /// <summary>
        /// Files a claim against the area — inside, outside, or the area's own declaration — and
        /// counts every one of those three, because a claim that is merely dropped cannot be audited.
        /// </summary>
        private void Add(MarkerClaim claim)
        {
            // The same pointer appears twice in a block that carries a sidecar: once readable, once as
            // the constant backing it. One occupancy. Keyed on the span AND the text, so a DESYNCED
            // pair — the readable one every reader sees and the sidecar one `to-xml` rebuilds from —
            // stays two claims, which is the honest occupancy of a block in that state.
            var key = $"{claim.File}|{claim.Kind}|{claim.Name}|{claim.Address}|{claim.StartByte}|{claim.ByteLength}";
            if (!_seen.Add(key))
            {
                return;
            }

            if (claim.IsDeclarationOf(area))
            {
                _declarations.Add(claim);
                return;
            }

            if (!claim.Overlaps(area))
            {
                if (claim.EndByteExclusive <= area.BaseByte)
                {
                    _belowBase++;
                }
                else
                {
                    _aboveTop++;
                }

                return;
            }

            _neighbours.Add(claim);
        }

        public NeighbourReport Report(int files)
        {
            // 🔴 A FILE THAT DID NOT PARSE CAN HOLD THE VERY CLAIM THIS LIST IS MEANT TO CONTAIN, so no
            // list is emitted. Named, never a short denominator reported as clean.
            if (_unparseable.Count > 0)
            {
                return NeighbourReport.NotDerived(area, files, _tagTables, _blocks, _others,
                    $"{_unparseable.Count} of {files} file(s) would not parse, so the corpus is PARTIAL: "
                    + string.Join("; ", _unparseable)
                    + ". An unread file can declare a claim on this area, so NO list is emitted rather than a short one",
                    _unparseable);
            }

            if (_refusals.Count > 0)
            {
                return new NeighbourReport(false, area, [], [], files, _tagTables, _blocks, _others,
                    _belowBase, _aboveTop, _nonMarkerPointers, _addressOnly,
                    _refusals, _unparseable, string.Empty);
            }

            // EMPTY IS NOT CLEAN. Nothing examined is never a pass — "0 neighbours" over a corpus that
            // held no object at all is a claim about nothing, and it must not render like the earned
            // zero one line below it.
            if (_tagTables + _blocks + _others == 0)
            {
                return NeighbourReport.NotDerived(area, files, _tagTables, _blocks, _others,
                    "NOTHING WAS EXAMINED, so '0 neighbours' would be a claim about nothing rather than about this area");
            }

            return new NeighbourReport(
                true, area,
                _neighbours.OrderBy(n => n.StartByte).ThenBy(n => n.Owner, StringComparer.Ordinal).ToList(),
                _declarations,
                files, _tagTables, _blocks, _others,
                _belowBase, _aboveTop, _nonMarkerPointers, _addressOnly,
                _refusals, _unparseable, string.Empty);
        }

        /// <summary>Where a tag's own line sits, 1-based, so a refusal can be opened.</summary>
        private static int LineOfTag(string[] lines, string name)
        {
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("    " + name + " ", StringComparison.Ordinal))
                {
                    return i + 1;
                }
            }

            return 0;
        }

        /// <summary>
        /// Blanks the contents of every double-quoted run, leaving the rest of the line in place so
        /// column-free scanning still works. IR escapes an embedded quote as <c>\"</c>.
        /// </summary>
        private static string StripQuoted(string line)
        {
            if (!line.Contains('"', StringComparison.Ordinal))
            {
                return line;
            }

            var chars = line.ToCharArray();
            var inside = false;

            for (var i = 0; i < chars.Length; i++)
            {
                if (chars[i] == '\\' && inside && i + 1 < chars.Length)
                {
                    chars[i] = ' ';
                    chars[++i] = ' ';
                    continue;
                }

                if (chars[i] == '"')
                {
                    inside = !inside;
                    chars[i] = ' ';
                    continue;
                }

                if (inside)
                {
                    chars[i] = ' ';
                }
            }

            return new string(chars);
        }

        /// <summary>
        /// How wide an elementary type is, in bits, or null for one this verb has not grounded.
        ///
        /// <para>Used ONLY to cross-check the address, never to replace it: a PLC tag's occupancy is
        /// what its address addresses. Null means "bounded by the address alone", which is counted and
        /// printed rather than treated as agreement.</para>
        /// </summary>
        private static int? TypeBits(string typeName) => typeName.ToUpperInvariant() switch
        {
            "BOOL" => 1,
            "BYTE" or "SINT" or "USINT" or "CHAR" => 8,
            "WORD" or "INT" or "UINT" or "DATE" or "S5TIME" => 16,
            "DWORD" or "DINT" or "UDINT" or "REAL" or "TIME" or "TIME_OF_DAY" or "TOD" => 32,
            "LWORD" or "LINT" or "ULINT" or "LREAL" or "LTIME" => 64,
            _ => null,
        };
    }
}

/// <summary>
/// A PLC tag's absolute <c>%M</c> address — <c>%M1.2</c>, <c>%MB10</c>, <c>%MW100</c>, <c>%MD200</c>.
///
/// <para><b>The span comes from the ADDRESS, not from the declared type</b>, because the address
/// letter is what the CPU addresses: <c>%MW100</c> is two bytes whatever the tag calls itself. The
/// type is used only as a second opinion, and a disagreement between the two is refused rather than
/// resolved (see <c>NeighbourRunner</c>).</para>
/// </summary>
internal static class MarkerTagAddress
{
    /// <summary>
    /// Bounds <paramref name="address"/> to a byte span.
    ///
    /// <para>On failure <paramref name="startByte"/> is still set when the digits could be read, and
    /// <c>-1</c> when they could not — the caller uses that to tell "unbounded but provably above the
    /// area" (excludable) from "unbounded, and it could be anywhere" (refusable).</para>
    /// </summary>
    /// <param name="addressBits">The width the ADDRESS states, in bits — 1, 8, 16 or 32.</param>
    internal static bool TryBound(string address, out int startByte, out int byteLength, out int addressBits)
    {
        startByte = -1;
        byteLength = 0;
        addressBits = 0;

        if (!address.StartsWith("%M", StringComparison.Ordinal) || address.Length < 3)
        {
            return false;
        }

        var rest = address[2..];

        if (char.IsAsciiDigit(rest[0]))
        {
            // The bit form, `%M<byte>.<bit>`. A bare `%M100` is not an address a tag table writes, and
            // reading it as a byte would be an invention.
            var dot = rest.IndexOf('.', StringComparison.Ordinal);
            if (dot < 0 || !TryByte(rest[..dot], out startByte) || !TryByte(rest[(dot + 1)..], out var bit) || bit > 7)
            {
                startByte = TryByte(dot < 0 ? rest : rest[..dot], out var partial) ? partial : -1;
                return false;
            }

            byteLength = 1;
            addressBits = 1;
            return true;
        }

        var width = rest[0] switch
        {
            'B' => 1,
            'W' => 2,
            'D' => 4,
            _ => 0,
        };

        if (!TryByte(rest[1..], out startByte))
        {
            startByte = -1;
        }

        if (width == 0 || startByte < 0)
        {
            return false;
        }

        byteLength = width;
        addressBits = width * 8;
        return true;
    }

    private static bool TryByte(string text, out int value) => int.TryParse(
        text,
        System.Globalization.NumberStyles.None,
        System.Globalization.CultureInfo.InvariantCulture,
        out value);
}
