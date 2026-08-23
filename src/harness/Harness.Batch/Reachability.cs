using System.Text.RegularExpressions;

namespace Harness.Batch;

/// <summary>
/// What the union program says about which of its blocks are actually in the scan.
/// </summary>
/// <param name="Verified">
/// 🔴 <b>False means UNKNOWN, not "all reachable".</b> Reachability is only computable from a corpus
/// that contains an OB — an OB is called by the operating system and nothing else, so it is the only
/// root there is. A union with no OB cannot be judged, and saying so is the point.
/// </param>
public sealed record ReachabilityReport(
    bool Verified,
    IReadOnlyList<string> Roots,
    IReadOnlyList<string> Reached,
    IReadOnlyList<string> Unreachable,
    IReadOnlyList<string> Refusals,
    string Summary);

/// <summary>
/// 🔴 <b>THE CHECK THAT WOULD HAVE CAUGHT THE ORPHAN, AND THE REASON IT IS HERE RATHER THAN AT RUNTIME.</b>
///
/// <para><b>Measured 2026-08-22.</b> A lane's slot FC was in the controller and called by
/// nothing. Its stimulus model therefore never executed, every valve vector timed out, and it took an
/// IR read to find — three hours after the deploy that could have refused it.</para>
///
/// <para><b>The runtime could not have caught it, and this is the part worth understanding.</b> X-E's
/// start echo reported <i>"commanded, observed to run"</i> for that slot. Both halves of the echo live
/// in the COPY LAYER, which is called: the copy layer writes the block's start member and latches the
/// echo from that same member one scan later. The loop closes entirely inside the copy layer, so the
/// echo is structurally incapable of noticing that the block consuming it never runs. Nor does the load
/// manifest help — <c>Loaded</c> is honest and means the block is in the controller, which is not the
/// same as being in the scan.</para>
///
/// <para>So it is answered where it IS answerable: statically, over the union program, before anything
/// is deployed.</para>
///
/// <para>⚠️ <b>This is a coarse call graph and it is deliberately conservative.</b> It reads
/// <c>BLOCK &lt;kind&gt; &lt;name&gt;</c> headers and <c>CALL &lt;name&gt;</c> targets out of the IR. It
/// does NOT replace <c>converter cross-check</c>, which computes the real reference graph including
/// multi-instance placement and data-block references. Anything it cannot parse is treated as
/// UNKNOWN rather than unreachable, because a false accusation here refuses a good batch.</para>
/// </summary>
public static class Reachability
{
    private static readonly Regex BlockHeader = new(
        @"^\s*BLOCK\s+(?<kind>OB|FC|FB)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex CallTarget = new(
        @"\bCALL\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);

    /// <summary>
    /// The non-code objects that legitimately have no <c>BLOCK</c> header and are legitimately not scan
    /// participants. <b>Matched positively</b>, so that "this is a DB" and "this file declares nothing at
    /// all" stop being the same observation.
    /// </summary>
    private static readonly Regex NonCodeHeader = new(
        @"^\s*(DB|TYPE|TAGTABLE)\s+", RegexOptions.Multiline | RegexOptions.Compiled);

    public static ReachabilityReport Of(IReadOnlyList<string> programPaths)
    {
        ArgumentNullException.ThrowIfNull(programPaths);

        var kinds = new Dictionary<string, string>(StringComparer.Ordinal);
        var calls = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var unreadable = new List<string>();
        var unidentifiable = new List<string>();

        foreach (var file in programPaths.SelectMany(FilesUnder).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string text;
            try
            {
                text = File.ReadAllText(file);
            }
            catch (IOException)
            {
                unreadable.Add(Path.GetFileName(file));
                continue;
            }

            var header = BlockHeader.Match(text);
            if (!header.Success)
            {
                // 🔴 THE THIRD OUTCOME, AND IT USED TO BE INVISIBLE. This branch's comment said "a DB, a
                // UDT or a tag table" — true of the intended case, and NOT of the whole set. A truncated,
                // half-written or malformed CODE block lands here too: it drops out of `kinds`, out of the
                // `n of m` denominator, and out of every refusal, while the summary still reads
                // "reachability VERIFIED". FI-44 one level in — not a check that examined nothing, but a
                // check whose denominator quietly shrank.
                //
                // The legitimate kinds declare themselves, so they can be told apart rather than assumed.
                if (!NonCodeHeader.IsMatch(text))
                    unidentifiable.Add(Path.GetFileName(file));

                continue;
            }

            var name = header.Groups["name"].Value;
            kinds[name] = header.Groups["kind"].Value;
            calls[name] = CallTarget.Matches(text).Select(m => m.Groups["name"].Value).ToHashSet(StringComparer.Ordinal);
        }

        var roots = kinds.Where(k => k.Value == "OB").Select(k => k.Key).OrderBy(n => n, StringComparer.Ordinal).ToArray();

        if (roots.Length == 0)
        {
            // NOT a refusal. A lane legitimately supplies only its own blocks, and refusing that would
            // block the ordinary single-lane case. But it is said plainly, because this is precisely the
            // blind spot the orphan hid in, and an unstated "we could not check" reads as "we checked".
            return new ReachabilityReport(false, roots, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
                $"REACHABILITY NOT VERIFIED - the union program contains no OB, so nothing roots the call graph and "
                + $"whether each of its {kinds.Count} code block(s) is in the scan CANNOT BE DETERMINED HERE. "
                + "*** THIS IS NOT 'ALL REACHABLE'. *** An FC nothing calls is deployed, appears in the load manifest, "
                + "and its slot's start echo still reports 'observed to run' - measured. Supply the OB that calls the "
                + "slots if you want this checked.");
        }

        var reached = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>(roots);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!reached.Add(current))
                continue;

            foreach (var target in calls.TryGetValue(current, out var t) ? t : Enumerable.Empty<string>())
            {
                // Only blocks the union actually contains: a call to something outside it is not this
                // check's business, and treating it as a node would invent members of the graph.
                if (kinds.ContainsKey(target) && !reached.Contains(target))
                    queue.Enqueue(target);
            }
        }

        var unreachable = kinds.Keys
            .Where(n => !reached.Contains(n))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        var refusals = new List<string>();
        if (unreachable.Length > 0)
        {
            refusals.Add(
                $"{unreachable.Length} code block(s) in the union program are NOT REACHABLE from any OB: "
                + string.Join(", ", unreachable)
                + $". Roots examined: {string.Join(", ", roots)}. *** A BLOCK NOTHING CALLS IS DEPLOYED AND NEVER RUNS. *** "
                + "Measured 2026-08-22: a slot FC in exactly this state put its whole lane's vectors into TIMED-OUT, and "
                + "nothing at runtime could report it - the load manifest says Loaded (true, and not the same as in the "
                + "scan) and the slot's start echo says 'observed to run' (the echo is written and latched inside the copy "
                + "layer, so it closes without the block). Add the call, or leave the block out of the program set.");
        }

        if (unreadable.Count > 0)
        {
            refusals.Add(
                $"{unreadable.Count} file(s) in the union program could not be read, so the call graph is incomplete and "
                + "its verdict would be over an unstated denominator: " + string.Join(", ", unreadable));
        }

        // 🔴 A file that declared NOTHING is a hole in the denominator, and it gates. It is not a DB, a
        // UDT or a tag table — those declare themselves and are matched positively above — so it is a
        // file this walk could not classify at all. Counting it as "not a scan participant" is how a
        // truncated code block disappears from a check that then reports VERIFIED over a short corpus.
        if (unidentifiable.Count > 0)
        {
            refusals.Add(
                $"{unidentifiable.Count} file(s) in the union program declare NO object this walk can identify — not a "
                + "BLOCK, not a DB, not a TYPE, not a TAGTABLE: " + string.Join(", ", unidentifiable)
                + ". They are absent from the denominator below, so the reachability verdict would be over a corpus "
                + "SMALLER than the one supplied, and a truncated code block is exactly what hides here.");
        }

        return new ReachabilityReport(
            true, roots, reached.OrderBy(n => n, StringComparer.Ordinal).ToArray(), unreachable, refusals,
            $"reachability VERIFIED from {roots.Length} OB(s): {reached.Count} of {kinds.Count} code block(s) are in the scan."
            + (unidentifiable.Count > 0 ? $" ⚠️ {unidentifiable.Count} supplied file(s) are NOT in that denominator." : string.Empty)
            + " (Derived from a text walk, not from the converter's own call graph — the weaker of the two.)");
    }

    private static IEnumerable<string> FilesUnder(string path) => ProgramFiles.Under(new[] { path });
}
