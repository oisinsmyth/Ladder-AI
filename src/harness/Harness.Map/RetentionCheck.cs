using System.Globalization;
using System.Text.RegularExpressions;

namespace Harness.Map;

/// <summary>One reason an object is refused. There is no warning severity — see <see cref="RetentionCheck"/>.</summary>
public sealed record RetentionFinding(string Object, string Detail);

/// <summary>
/// The outcome of the non-retentive assertion.
///
/// <para><see cref="Passed"/> requires that something was actually examined. A check that ran over
/// nothing and found nothing is not a pass, it is a check that did not happen — and it is the result
/// most likely to be believed, because every field reads as clean.</para>
/// </summary>
public sealed record RetentionVerdict(int ObjectsExamined, IReadOnlyList<RetentionFinding> Findings)
{
    public bool Passed => ObjectsExamined > 0 && Findings.Count == 0;

    public string Summary() => Passed
        ? $"NON-RETENTIVE: {ObjectsExamined} object(s) examined, 0 findings."
        : ObjectsExamined == 0
            ? "REFUSED: nothing examined. An empty object set is not a clean one."
            : $"REFUSED: {Findings.Count} finding(s) across {ObjectsExamined} object(s).";
}

/// <summary>
/// Build-plan item 0.1b — <b>every harness object is asserted non-retentive, from the IR, before any
/// device is involved.</b> Retain is the one hard memory restriction on this rig (A9 was scoped by
/// ruling to retain alone), and this is the check that enforces it.
///
/// <para><b>The trap the plan names, and why this refuses more than it strictly must.</b> Retain is
/// per-tag on an OPTIMIZED block but ALL-OR-NOTHING on a STANDARD-access one — and the harness
/// deliberately creates standard-access blocks, because classic S7comm cannot see an optimized block at
/// all (it is not an error; the block is simply absent, and it fails at the first data read rather than
/// at connect). So on the blocks this project actually generates, a single retentive member makes the
/// WHOLE object retentive. The finding says so in those terms rather than naming one member, because a
/// reader who fixes the member and believes the object is now partially retentive has the wrong model.</para>
///
/// <para><b>Three things are refused that are not, strictly, retention findings — each fails closed for
/// a stated reason:</b></para>
/// <list type="number">
/// <item>A DB with NO <c>MEMORYLAYOUT</c> line. Absence means "no opinion", never a default, so the
/// all-or-nothing rule above cannot be evaluated at all — and TIA will apply the S7-1200 default of
/// Optimized, which is invisible on the wire. Undetermined is not clean.</item>
/// <item>A DB declaring <c>MEMORYLAYOUT Optimized</c>. Same wire consequence, stated rather than
/// implied.</item>
/// <item>A mirror tag outside bit memory, or below the retentive window. For <c>%M</c> there is no
/// per-tag retain flag to inspect — a PLC tag table has no Remanence concept at all — so
/// <b>non-retentiveness of the mirror IS the address</b>, and checking the address is the only form
/// this assertion can take there.</item>
/// </list>
///
/// <para><b>No finding is a warning.</b> A check that detects something and only warns gets skimmed;
/// every finding here makes the verdict refuse.</para>
/// </summary>
public static class RetentionCheck
{
    private static readonly Regex RetainToken = new(@"(?<![A-Za-z0-9_])RETAIN(?![A-Za-z0-9_])", RegexOptions.Compiled);
    private static readonly Regex TagAddress = new(@"@\s*(?<addr>%\S+)", RegexOptions.Compiled);
    private static readonly Regex MemoryAddress =
        new(@"^%M(?<size>[BWDXL]?)(?<byte>\d+)(?:\.(?<bit>\d+))?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Run the assertion over every generated harness object.</summary>
    public static RetentionVerdict Check(IEnumerable<HarnessObject> objects, MirrorGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);

        var findings = new List<RetentionFinding>();
        var examined = 0;

        foreach (var refusal in geometry.Refusals)
            findings.Add(new RetentionFinding("<mirror geometry>", refusal));

        foreach (var obj in objects ?? Array.Empty<HarnessObject>())
        {
            examined++;
            CheckObject(obj, geometry, findings);
        }

        return new RetentionVerdict(examined, findings);
    }

    private static void CheckObject(HarnessObject obj, MirrorGeometry geometry, List<RetentionFinding> findings)
    {
        var name = string.IsNullOrWhiteSpace(obj.Name) ? "<unnamed>" : obj.Name;
        var lines = (obj.Ir ?? string.Empty).Replace("\r\n", "\n").Split('\n');

        var header = lines.FirstOrDefault(l => l.Trim().Length > 0)?.Trim() ?? string.Empty;
        var declared = DeclaredKind(header);

        if (declared is null)
        {
            // Unreadable is a refusal, never a skip. A harness object this check cannot classify is an
            // object it has not examined, whatever the count says.
            findings.Add(new RetentionFinding(name,
                $"could not classify the IR: first line reads '{Truncate(header)}'. An object this check cannot read is an object it has not checked."));
            return;
        }

        if (declared != obj.Kind)
        {
            findings.Add(new RetentionFinding(name,
                $"declared as {obj.Kind} but the IR begins '{Truncate(header)}', which is a {declared}."));
            return;
        }

        switch (obj.Kind)
        {
            case HarnessObjectKind.TagTable:
                CheckTagTable(name, lines, geometry, findings);
                break;

            case HarnessObjectKind.DataBlock:
                CheckMemoryLayout(name, lines, findings);
                CheckRetain(name, lines, findings, allOrNothing: true);
                break;

            default:
                // A code block's own interface members can carry RETAIN too (an FB's statics live in its
                // instance DB, and that is retain memory like any other).
                CheckRetain(name, lines, findings, allOrNothing: false);
                break;
        }
    }

    private static void CheckRetain(string name, IReadOnlyList<string> lines, List<RetentionFinding> findings, bool allOrNothing)
    {
        foreach (var line in lines)
        {
            var text = StripComments(line);
            if (!RetainToken.IsMatch(text))
                continue;

            findings.Add(new RetentionFinding(name, allOrNothing
                ? $"RETAIN on '{Truncate(text.Trim())}'. On a STANDARD-access block retain is all-or-nothing, so this does not make one member retentive — it makes the WHOLE object retentive, and retain is the one hard memory restriction on this rig."
                : $"RETAIN on '{Truncate(text.Trim())}'. Harness objects are non-retentive without exception."));
        }
    }

    private static void CheckMemoryLayout(string name, IReadOnlyList<string> lines, List<RetentionFinding> findings)
    {
        var layout = lines
            .Select(l => l.Trim())
            .FirstOrDefault(l => l.StartsWith("MEMORYLAYOUT ", StringComparison.Ordinal))
            ?["MEMORYLAYOUT ".Length..].Trim();

        if (layout is null)
        {
            findings.Add(new RetentionFinding(name,
                "no MEMORYLAYOUT line. Absence means 'no opinion', never a default — TIA applies the S7-1200 default of Optimized, which classic S7comm cannot see at all, and the all-or-nothing retain rule cannot be evaluated without knowing the layout."));
            return;
        }

        if (!string.Equals(layout, "Standard", StringComparison.Ordinal))
        {
            findings.Add(new RetentionFinding(name,
                $"MEMORYLAYOUT {layout}. Harness data blocks must be Standard: an optimized block is not merely awkward on the wire, it is ABSENT — the read fails at the first data access rather than at connect."));
        }
    }

    private static void CheckTagTable(string name, IReadOnlyList<string> lines, MirrorGeometry geometry, List<RetentionFinding> findings)
    {
        var tags = 0;

        foreach (var line in lines)
        {
            var match = TagAddress.Match(StripComments(line));
            if (!match.Success)
                continue;

            tags++;
            var address = match.Groups["addr"].Value;
            var parsed = MemoryAddress.Match(address);

            if (!parsed.Success)
            {
                findings.Add(new RetentionFinding(name,
                    $"tag at '{address}' is not a bit-memory address. Harness mirror tags live in %M and nowhere else — a mirror in a DB is one re-import away from being invisible on the wire."));
                continue;
            }

            var byteAddress = int.Parse(parsed.Groups["byte"].Value, CultureInfo.InvariantCulture);
            var size = SizeOf(parsed.Groups["size"].Value);

            if (!geometry.IsNonRetentiveAddress(byteAddress))
            {
                findings.Add(new RetentionFinding(name,
                    $"tag at '{address}' is inside the retentive M window (MB0..MB{geometry.RetentiveBytes - 1}). Retentive M starts at MB0 and runs contiguously upward, and there is no per-tag retain flag on a PLC tag to override it — the address IS the assertion."));
            }

            if (byteAddress + size > geometry.TotalBytes)
            {
                findings.Add(new RetentionFinding(name,
                    $"tag at '{address}' runs past the CPU's {geometry.TotalBytes} bytes of bit memory."));
            }
        }

        if (tags == 0)
        {
            findings.Add(new RetentionFinding(name,
                "tag table declares no tags. An empty mirror is not a clean mirror — nothing in it was checked."));
        }
    }

    private static int SizeOf(string sizeToken) => sizeToken.ToUpperInvariant() switch
    {
        "W" => 2,
        "D" => 4,
        "L" => 8,
        _ => 1,
    };

    private static HarnessObjectKind? DeclaredKind(string header) =>
        header.StartsWith("TAGTABLE ", StringComparison.Ordinal) ? HarnessObjectKind.TagTable
        : header.StartsWith("DB ", StringComparison.Ordinal) ? HarnessObjectKind.DataBlock
        : header.StartsWith("BLOCK ", StringComparison.Ordinal) ? HarnessObjectKind.Block
        : header.StartsWith("TYPE ", StringComparison.Ordinal) ? HarnessObjectKind.Block
        : null;

    /// <summary>
    /// Removes the quoted tail of a line before the RETAIN scan, so a member whose COMMENT prose
    /// contains the word cannot be mistaken for a retentive member.
    /// </summary>
    private static string StripComments(string line)
    {
        var quote = line.IndexOf('"');
        return quote < 0 ? line : line[..quote];
    }

    private static string Truncate(string text) => text.Length <= 90 ? text : text[..87] + "...";
}
