namespace Converter.Neighbours;

/// <summary>How a claim on the area declares itself.</summary>
public enum NeighbourKind
{
    /// <summary>A PLC tag with an absolute <c>%M</c> address in a tag table.</summary>
    Tag,

    /// <summary>A <c>P#M…</c> area-pointer literal in a program object's body.</summary>
    AreaPointer,
}

/// <summary>
/// The window the question is asked about, in <c>%M</c> BYTES.
///
/// <para><b>Bytes, not registers, and the conversion is done ONCE, here.</b> The harness thinks in
/// holding registers numbered from the area base; the IR declares <c>%M</c> byte addresses. That
/// conversion is the likeliest place to get this silently wrong — <c>ReservedRegion</c> says so in
/// its own docstring — so this producer emits byte spans throughout and the consumer converts them
/// through its own <c>MirrorGeometry.ReservingBytes</c>, which already rounds outward for the right
/// reason. Nothing here re-implements that.</para>
/// </summary>
/// <param name="BaseByte">First <c>%M</c> byte of the area.</param>
/// <param name="ByteLength">How many bytes it covers.</param>
/// <param name="Registers">
/// The register count the area was GIVEN as, when it was given that way — carried for the printed
/// description only, never as a second source of the width.
/// </param>
public sealed record MarkerArea(int BaseByte, int ByteLength, int? Registers)
{
    /// <summary>One past the last byte in the area.</summary>
    public int TopByteExclusive => BaseByte + ByteLength;

    public static MarkerArea OfRegisters(int baseByte, int registers)
    {
        Guard(baseByte, registers, nameof(registers));
        return new MarkerArea(baseByte, registers * 2, registers);
    }

    public static MarkerArea OfBytes(int baseByte, int bytes)
    {
        Guard(baseByte, bytes, nameof(bytes));
        return new MarkerArea(baseByte, bytes, null);
    }

    /// <summary>
    /// An area of nothing, or one below <c>%M0</c>, is a question about nothing. Answering it would
    /// print "0 neighbours" over a window that does not exist — a green with no subject.
    /// </summary>
    private static void Guard(int baseByte, int size, string sizeName)
    {
        if (baseByte < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(baseByte), baseByte,
                "The area base is a %M byte address and cannot be negative. A negative base is usually a register "
                + "number that was never converted to bytes.");
        }

        if (size <= 0)
        {
            throw new ArgumentOutOfRangeException(sizeName, size,
                "An area of no width has no occupants to find, so '0 neighbours' over it would be a green with no "
                + "subject. Give the width the program actually serves — `converter served-area` derives it.");
        }
    }

    public string Describe() => Registers is int r
        ? $"%M{BaseByte}..%M{TopByteExclusive - 1} (base {BaseByte}, {r} register(s))"
        : $"%M{BaseByte}..%M{TopByteExclusive - 1} (base {BaseByte}, {ByteLength} byte(s))";
}

/// <summary>
/// One run of <c>%M</c> bytes that something OTHER than the caller's map already claims, and
/// <b>the object that declares it</b>.
/// </summary>
/// <param name="Owner">
/// 🔴 <b>Not optional, and the reason is the whole defect.</b> "A refusal that cannot say WHOSE space
/// was hit sends the reader looking in the wrong place" — to the mirror, which is the one place the
/// problem is not (<c>ReservedRegion.cs:31-35</c>). Written in the words a reader would search the
/// project for.
/// </param>
/// <param name="Container">
/// The declaring object alone — a tag table's name, a block's name. Split out from
/// <see cref="Owner"/> so a consumer can exclude its OWN objects by name against a closed set it
/// already has, rather than by matching prose.
/// </param>
/// <param name="StartByte">First <c>%M</c> byte claimed.</param>
/// <param name="ByteLength">How many bytes, rounded OUTWARD from the declared width.</param>
public sealed record MarkerClaim(
    NeighbourKind Kind,
    string Owner,
    string Container,
    string Name,
    string Address,
    int StartByte,
    int ByteLength,
    string File,
    int Line)
{
    /// <summary>One past the last byte claimed.</summary>
    public int EndByteExclusive => StartByte + ByteLength;

    /// <summary>True when this claim and <paramref name="area"/> share at least one byte.</summary>
    public bool Overlaps(MarkerArea area) => StartByte < area.TopByteExclusive && area.BaseByte < EndByteExclusive;

    /// <summary>
    /// True when the claim covers the WHOLE area exactly — the area declaring itself rather than an
    /// occupant of it. Derived from the span, never from a name: the <c>MB_SERVER</c> call that
    /// serves the window states the window, and a producer that returned it as a neighbour would make
    /// every map refuse against its own area.
    /// </summary>
    public bool IsDeclarationOf(MarkerArea area) => StartByte == area.BaseByte && EndByteExclusive == area.TopByteExclusive;
}

/// <summary>
/// What a corpus says lives inside one <c>%M</c> area — or, when it cannot say, exactly why not.
/// </summary>
/// <param name="Derived">
/// 🔴 <b>True only when a real corpus was read whole.</b> An empty list under <c>Derived == true</c>
/// is the EARNED zero and a positive claim; under false it means nothing, and the JSON withholds the
/// list entirely so a lenient consumer cannot read one as the other.
/// </param>
public sealed record NeighbourReport(
    bool Derived,
    MarkerArea Area,
    IReadOnlyList<MarkerClaim> Neighbours,
    IReadOnlyList<MarkerClaim> AreaDeclarations,
    int FilesScanned,
    int TagTablesScanned,
    int BlocksScanned,
    int OtherObjectsScanned,
    int ExcludedBelowBase,
    int ExcludedAtOrAboveTop,
    int ExcludedNonMarkerPointers,
    int BoundedByAddressAlone,
    IReadOnlyList<string> Refusals,
    IReadOnlyList<string> Unparseable,
    string NotDerivedReason)
{
    public bool Refused => Refusals.Count > 0;

    public int ObjectsScanned => TagTablesScanned + BlocksScanned + OtherObjectsScanned;

    /// <summary>
    /// <b>The line printed on every run, whatever the outcome</b> — the shape <c>drift-check</c>'s
    /// <c>COMPARED:</c>, <c>preflight</c>'s <c>RESOLVED AGAINST:</c> and <c>served-area</c>'s
    /// <c>NOT DERIVED — n block(s) …</c> already set. Three outcomes that must never render alike:
    /// a real corpus with no occupant, a corpus that was empty, and a corpus one of whose files would
    /// not parse.
    /// </summary>
    public string Denominator
    {
        get
        {
            var scanned = $"{TagTablesScanned} tag table(s) + {BlocksScanned} block(s) + "
                + $"{OtherObjectsScanned} other object(s) in {FilesScanned} file(s)";

            if (Refused)
            {
                return $"REFUSED — {scanned} scanned; {Refusals.Count} refusal(s) below. No neighbour list was "
                    + "derived, so any declared reservation stands UNCORROBORATED and any undeclared occupant "
                    + "stands unseen.";
            }

            if (!Derived)
            {
                return $"NOT DERIVED — {scanned} scanned; {NotDerivedReason}; area {Area.Describe()}";
            }

            return $"neighbours: {Neighbours.Count} region(s) derived from {scanned}; "
                + $"{Unparseable.Count} file(s) unparseable; area {Area.Describe()}";
        }
    }

    /// <summary>
    /// <b>What was seen and deliberately left out, printed on every run.</b> An over-broad derivation
    /// refuses maps that are fine, so the claims outside the area are counted rather than merely
    /// dropped: a reader who expected a neighbour and got none can tell "not there" from "excluded".
    /// </summary>
    public string Exclusions =>
        $"excluded: {ExcludedBelowBase} %M claim(s) below base {Area.BaseByte}, "
        + $"{ExcludedAtOrAboveTop} at or above %M{Area.TopByteExclusive}; "
        + $"{ExcludedNonMarkerPointers} area pointer(s) outside marker memory; "
        + $"{AreaDeclarations.Count} declaration(s) of the area itself. "
        + $"{BoundedByAddressAlone} claim(s) bounded by their address alone (a data type this verb does not know).";

    public static NeighbourReport NotDerived(
        MarkerArea area, int files, int tagTables, int blocks, int others,
        string reason, IReadOnlyList<string>? unparseable = null) =>
        new(false, area, Array.Empty<MarkerClaim>(), Array.Empty<MarkerClaim>(),
            files, tagTables, blocks, others, 0, 0, 0, 0,
            Array.Empty<string>(), unparseable ?? Array.Empty<string>(), reason);

    public static NeighbourReport Refuse(
        MarkerArea area, int files, int tagTables, int blocks, int others, params string[] refusals) =>
        new(false, area, Array.Empty<MarkerClaim>(), Array.Empty<MarkerClaim>(),
            files, tagTables, blocks, others, 0, 0, 0, 0,
            refusals, Array.Empty<string>(), string.Empty);
}
