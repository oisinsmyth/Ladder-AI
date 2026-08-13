using System.Globalization;
using System.Text.RegularExpressions;

namespace Harness.Map;

/// <summary>One reason an object is refused. There is no warning severity — see <see cref="RetentionCheck"/>.</summary>
public sealed record RetentionFinding(string Object, string Detail);

/// <summary>
/// The outcome of the non-retentive assertion.
///
/// <para><b>0.1b is TWO rules, and each has its own denominator.</b> The ATTRIBUTE rule (no
/// <c>RETAIN</c>, and a data block that states its layout) is counted by
/// <see cref="ObjectsExamined"/>. The ADDRESS rule — the one that covers the mirror, where retention
/// is not an attribute at all — is counted by <see cref="AddressesExamined"/>, and it is a SEPARATE
/// count for a reason: an object set carrying no absolute address at all satisfies the attribute rule
/// completely while the address rule never runs. Every field then reads as clean, which is the result
/// most likely to be believed.</para>
///
/// <para><see cref="Passed"/> therefore requires BOTH counts to be non-zero. There is no flag that
/// relaxes it: a harness object set that declares no <c>%M</c> address has no mirror, and a
/// retention check over a program with no mirror has not checked the thing 0.1b exists for.</para>
/// </summary>
public sealed record RetentionVerdict(int ObjectsExamined, int AddressesExamined, IReadOnlyList<RetentionFinding> Findings)
{
    public bool Passed => ObjectsExamined > 0 && AddressesExamined > 0 && Findings.Count == 0;

    public string Summary() => Passed
        ? $"NON-RETENTIVE: {ObjectsExamined} object(s) and {AddressesExamined} address(es) examined, 0 findings."
        : ObjectsExamined == 0
            ? "REFUSED: nothing examined. An empty object set is not a clean one."
            : AddressesExamined == 0 && Findings.Count == 0
                ? $"REFUSED: {ObjectsExamined} object(s) examined but NO ADDRESS was. The attribute rule ran and the address rule did not, so the mirror — the object 0.1b exists for — was never checked."
                : $"REFUSED: {Findings.Count} finding(s) across {ObjectsExamined} object(s) and {AddressesExamined} address(es).";
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
/// <para><b>THE SECOND RULE, AND THE PLAN'S FRAMING DOES NOT COVER IT.</b> Build-plan item 0.1b is
/// written as though retention were always an ATTRIBUTE — per-tag on optimized blocks, all-or-nothing
/// on standard ones. <b>A <c>%M</c> tag has no retain attribute in either direction</b>:
/// <c>ir/SPEC.md</c>'s TAGTABLE grammar has no <c>Remanence</c> concept at all, and <c>%M</c>
/// retention is set by the CPU's retentive-M range, which is contiguous from MB0. So for the mirror —
/// the largest harness object and the one 0.1b exists for — <b>non-retentiveness IS THE ADDRESS</b>,
/// and a checker implementing only the attribute rule passes a mirror sitting on MB0.</para>
///
/// <para><b>AND THE ADDRESS RULE MUST NOT BE SATISFIABLE VACUOUSLY.</b> Three ways it was, each
/// closed here and each negative-tested:</para>
/// <list type="number">
/// <item><b>No address anywhere in the set.</b> The attribute rule ran over every object, found
/// nothing, and the verdict passed — with the address rule never executed.
/// <see cref="RetentionVerdict.AddressesExamined"/> is now a separate denominator and zero refuses.</item>
/// <item><b>A declared tag line carrying no parseable address was skipped.</b> Every non-blank line
/// after <c>TAGS</c> is a DECLARATION and must yield an address; one that does not is a finding, not
/// a line the scan moves past.</item>
/// <item><b>Absolute addresses outside a tag table were never looked at.</b> The rule ran on tag
/// tables alone, so a <c>%M0.0</c> written straight into a rung was invisible to it. Every object is
/// now scanned.</item>
/// </list>
///
/// <para><b>Three things are refused that are not, strictly, retention findings — each fails closed for
/// a stated reason:</b></para>
/// <list type="number">
/// <item>A DB with NO <c>MEMORYLAYOUT</c> line. Absence means "no opinion", never a default, so the
/// all-or-nothing rule above cannot be evaluated at all — and TIA will apply the S7-1200 default of
/// Optimized, which is invisible on the wire. Undetermined is not clean.</item>
/// <item>A DB declaring <c>MEMORYLAYOUT Optimized</c>. Same wire consequence, stated rather than
/// implied.</item>
/// <item>A mirror tag outside bit memory, or below the retentive window.</item>
/// </list>
///
/// <para><b>What this still cannot check, and it is the layout half.</b> A re-import silently reverts a
/// Standard DB to Optimized (the exported <c>.xml</c> carries no <c>MemoryLayout</c> element, so the
/// import states no opinion and TIA applies the default), and <c>drift-check</c> is structurally blind
/// to it. So the IR can say <c>Standard</c>, every check here can pass, and the device can be
/// Optimized. <b>The layout branch needs a device-side leg —</b> <c>openness-cli block-layout --set
/// Standard --yes</c> after EVERY import, then <c>--expect Standard</c> as the gate. Necessary here,
/// not sufficient.</para>
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

    /// <summary>Any absolute PLC address, wherever it appears — not only after an <c>@</c> in a tag table.</summary>
    private static readonly Regex AbsoluteAddress =
        new(@"(?<![A-Za-z0-9_])%[A-Za-z]{0,2}\d+(?:\.\d+)?", RegexOptions.Compiled);

    /// <summary>Run the assertion over every generated harness object.</summary>
    public static RetentionVerdict Check(IEnumerable<HarnessObject> objects, MirrorGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);

        var findings = new List<RetentionFinding>();
        var examined = 0;
        var addresses = 0;

        foreach (var refusal in geometry.Refusals)
            findings.Add(new RetentionFinding("<mirror geometry>", refusal));

        foreach (var obj in objects ?? Array.Empty<HarnessObject>())
        {
            examined++;
            addresses += CheckObject(obj, geometry, findings);
        }

        return new RetentionVerdict(examined, addresses, findings);
    }

    /// <summary>Checks one object and returns how many ADDRESSES it subjected to the address rule.</summary>
    private static int CheckObject(HarnessObject obj, MirrorGeometry geometry, List<RetentionFinding> findings)
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
            return 0;
        }

        if (declared != obj.Kind)
        {
            findings.Add(new RetentionFinding(name,
                $"declared as {obj.Kind} but the IR begins '{Truncate(header)}', which is a {declared}."));
            return 0;
        }

        switch (obj.Kind)
        {
            case HarnessObjectKind.TagTable:
                return CheckTagTable(name, lines, geometry, findings);

            case HarnessObjectKind.DataBlock:
                CheckMemoryLayout(name, lines, findings);
                CheckRetain(name, lines, findings, allOrNothing: true);
                return CheckInlineAddresses(name, lines, geometry, findings);

            default:
                // A code block's own interface members can carry RETAIN too (an FB's statics live in its
                // instance DB, and that is retain memory like any other).
                CheckRetain(name, lines, findings, allOrNothing: false);
                return CheckInlineAddresses(name, lines, geometry, findings);
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

    /// <summary>
    /// The address rule over a tag table, and the counting is the load-bearing part.
    ///
    /// <para><b>Every non-blank line after <c>TAGS</c> is a DECLARATION, and must yield an address.</b>
    /// The earlier form scanned for an <c>@ %addr</c> and moved past anything that did not match — so a
    /// table of six tags, five of them malformed, examined ONE address and reported a clean pass. A line
    /// this check cannot read is a line it has not checked, and that is a finding.</para>
    /// </summary>
    private static int CheckTagTable(string name, IReadOnlyList<string> lines, MirrorGeometry geometry, List<RetentionFinding> findings)
    {
        var declarations = 0;
        var addresses = 0;
        var inTags = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (!inTags)
            {
                inTags = string.Equals(trimmed, "TAGS", StringComparison.Ordinal);
                continue;
            }

            if (trimmed.Length == 0)
                continue;

            declarations++;

            var match = TagAddress.Match(StripComments(line));
            if (!match.Success)
            {
                findings.Add(new RetentionFinding(name,
                    $"tag declaration '{Truncate(trimmed)}' carries no '@ <address>'. For %M there is no retain attribute to fall back on — the address IS the assertion — so a declaration whose address cannot be read is one this check has NOT made."));
                continue;
            }

            addresses++;
            CheckAddress(name, match.Groups["addr"].Value, "tag at", geometry, findings);
        }

        if (declarations == 0)
        {
            findings.Add(new RetentionFinding(name,
                "tag table declares no tags. An empty mirror is not a clean mirror — nothing in it was checked."));
        }

        return addresses;
    }

    /// <summary>
    /// The address rule over an object that is not a tag table.
    ///
    /// <para>An absolute address written straight into a rung or a DB member is subject to exactly the
    /// same rule as one declared in a tag table, and was invisible to this check until it scanned for
    /// them. Symbolic harness objects contain none, so this normally examines nothing and adds
    /// nothing — the point is that it CANNOT be bypassed by writing the address somewhere else.</para>
    /// </summary>
    private static int CheckInlineAddresses(string name, IReadOnlyList<string> lines, MirrorGeometry geometry, List<RetentionFinding> findings)
    {
        var addresses = 0;

        foreach (var line in lines)
        {
            foreach (Match match in AbsoluteAddress.Matches(StripComments(line)))
            {
                addresses++;
                CheckAddress(name, match.Value, "absolute address", geometry, findings);
            }
        }

        return addresses;
    }

    private static void CheckAddress(string name, string address, string what, MirrorGeometry geometry, List<RetentionFinding> findings)
    {
        var parsed = MemoryAddress.Match(address);

        if (!parsed.Success)
        {
            findings.Add(new RetentionFinding(name,
                $"{what} '{address}' is not a bit-memory address. Harness mirror tags live in %M and nowhere else — a mirror in a DB is one re-import away from being invisible on the wire."));
            return;
        }

        var byteAddress = int.Parse(parsed.Groups["byte"].Value, CultureInfo.InvariantCulture);
        var size = SizeOf(parsed.Groups["size"].Value);

        if (!geometry.IsNonRetentiveAddress(byteAddress))
        {
            findings.Add(new RetentionFinding(name,
                $"{what} '{address}' is inside the retentive M window (MB0..MB{geometry.RetentiveBytes - 1}). Retentive M starts at MB0 and runs contiguously upward, and there is no per-tag retain flag on a PLC tag to override it — the address IS the assertion."));
        }

        if (byteAddress + size > geometry.TotalBytes)
        {
            findings.Add(new RetentionFinding(name,
                $"{what} '{address}' runs past the CPU's {geometry.TotalBytes} bytes of bit memory."));
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
