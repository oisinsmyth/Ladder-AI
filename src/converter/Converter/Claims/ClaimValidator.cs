using System.Text.RegularExpressions;
using Converter.CrossCheck;
using Converter.Preflight;
using Converter.Review;
using Converter.SimaticMl;

namespace Converter.Claims;

// The corpus half of a claim: a reservation is only meaningful if the thing being reserved is
// actually free. Checking claims against each other alone would happily reserve FB50, which
// FB_HopperBlockageMonitor already owns.
//
// Built once and shared by acquire/allocate/check — three separate corpus builds would be three
// chances to disagree with each other about the same project.
public sealed class ClaimCorpus
{
    private ClaimCorpus(ProjectIndex index, SignalInventory.SignalInventory inventory, ProjectUsageGraph graph)
    {
        Index = index;
        Inventory = inventory;
        Graph = graph;
    }

    public ProjectIndex Index { get; }

    public SignalInventory.SignalInventory Inventory { get; }

    public ProjectUsageGraph Graph { get; }

    public bool IsEmpty => !Index.IndexedAnything;

    public static ClaimCorpus Build(string projectDir) => new(
        ProjectIndex.Build(projectDir, Array.Empty<string>()),
        SignalInventory.SignalInventory.Build(projectDir),
        ProjectUsageGraph.Build(projectDir));
}

public static class ClaimValidator
{
    private static readonly Regex BlockNumberForm = new(@"^(FB|FC|OB|DB)(\d+)$", RegexOptions.Compiled);
    private static readonly Regex BlockNetworkForm = new(@"^(.+):(\d+)$", RegexOptions.Compiled);
    private static readonly Regex BitToken = new(@"^%X(\d+)$", RegexOptions.Compiled);

    // Bit width by declared type. Deliberately a closed map with no default: guessing 16 for an
    // unrecognised type would let an agent reserve %X20 of a Byte and be told it was free. FI-44's
    // rule applied to a narrower question — an answer computed from an assumption is not an answer.
    private static readonly Dictionary<string, int> BitWidths = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Byte"] = 8,
        ["SInt"] = 8,
        ["USInt"] = 8,
        ["Word"] = 16,
        ["Int"] = 16,
        ["UInt"] = 16,
        ["DWord"] = 32,
        ["DInt"] = 32,
        ["UDInt"] = 32,
    };

    // Returns null when the value is usable; otherwise the outcome explaining why it is not.
    public static ClaimOutcome? Reject(ClaimCorpus corpus, ClaimKind kind, string value)
    {
        if (corpus.IsEmpty)
        {
            return Fail(ClaimResult.NothingExamined,
                "the project directory indexed no blocks, DBs, types or tags — refusing to report a " +
                "resource free against an empty corpus (FI-44: empty is not clean)");
        }

        return kind switch
        {
            ClaimKind.BlockNumber => RejectBlockNumber(corpus, value),
            ClaimKind.AlarmBit => RejectAlarmBit(corpus, value),
            ClaimKind.DbMember => RejectDbMember(corpus, value),
            ClaimKind.BlockNetwork => RejectBlockNetwork(corpus, value),
            ClaimKind.Tag => RejectTag(corpus, value),
            ClaimKind.BlockEdit => RejectBlockEdit(corpus, value),
            _ => Fail(ClaimResult.Invalid, $"unknown kind '{kind}'"),
        };
    }

    /// <summary>
    /// Splits a block-number value into its space and number, or null when it is not one. Exposed so
    /// the band annotation reads the number the SAME way the validator does — two parsers of one
    /// format is how they come to disagree about an edge case nobody tested.
    /// </summary>
    public static (string? Space, int Number) BlockNumberParts(string value)
    {
        var match = BlockNumberForm.Match(value);
        return match.Success
            ? (match.Groups[1].Value, int.Parse(match.Groups[2].Value))
            : (null, 0);
    }

    private static ClaimOutcome? RejectBlockNumber(ClaimCorpus corpus, string value)
    {
        var match = BlockNumberForm.Match(value);
        if (!match.Success)
        {
            return Fail(ClaimResult.Invalid, $"'{value}' is not a block number — expected e.g. FB51, FC7, DB20");
        }

        var blockKind = match.Groups[1].Value;
        var number = int.Parse(match.Groups[2].Value);
        var owner = corpus.Index.NumberOwner(blockKind, number);

        return owner is null
            ? null
            : Fail(ClaimResult.AlreadyUsedInCorpus, $"{value} is already {owner} in this project");
    }

    private static ClaimOutcome? RejectAlarmBit(ClaimCorpus corpus, string value)
    {
        if (!Rules.IsSliceAccessTag(value))
        {
            return Fail(ClaimResult.Invalid,
                $"'{value}' is not a bit-slice path — expected e.g. DB_Alarms.ShredderAlarm0.%X9");
        }

        var word = Rules.SliceWordPath(value);
        var bit = Rules.SliceBitToken(value);

        var bitMatch = BitToken.Match(bit);
        if (!bitMatch.Success)
        {
            return Fail(ClaimResult.Invalid, $"'{bit}' is not a bit slice — expected %Xn");
        }

        var leaf = corpus.Inventory.Leaves.FirstOrDefault(l => string.Equals(l.Path, word, StringComparison.Ordinal));
        if (leaf is null)
        {
            return Fail(ClaimResult.Invalid, $"alarm word '{word}' does not resolve in this project");
        }

        if (!BitWidths.TryGetValue(leaf.Type, out var width))
        {
            return Fail(ClaimResult.Invalid,
                $"alarm word '{word}' has type '{leaf.Type}', whose bit width this tool does not know — " +
                "refusing to guess how many bits it has");
        }

        var index = int.Parse(bitMatch.Groups[1].Value);
        if (index >= width)
        {
            return Fail(ClaimResult.Invalid, $"{bit} is out of range for '{word}' ({leaf.Type}, {width} bits)");
        }

        // "Already used" means already DRIVEN. A bit that is only read is not owned by anyone, and
        // refusing it would make half the alarm words unclaimable.
        if (corpus.Graph.Usages.TryGetValue(value, out var usage) && usage.Writers.Count > 0)
        {
            var writer = usage.Writers[0];
            return Fail(ClaimResult.AlreadyUsedInCorpus,
                $"{value} is already written by {writer.Block} network {writer.Network}");
        }

        return null;
    }

    private static ClaimOutcome? RejectDbMember(ClaimCorpus corpus, string value)
    {
        var lastDot = TagPath.LastIndexOfSeparator(value);
        if (lastDot <= 0 || lastDot == value.Length - 1)
        {
            return Fail(ClaimResult.Invalid, $"'{value}' is not a DB member path — expected e.g. DB_Settings.NewMember");
        }

        var root = AccessNode.FromDottedPath(0, "GlobalVariable", value).ComponentPath[0];
        if (!corpus.Index.ResolvesAsTagRoot(root))
        {
            return Fail(ClaimResult.Invalid, $"'{root}' does not resolve as a DB or tag root in this project");
        }

        if (corpus.Inventory.Leaves.Any(l => string.Equals(l.Path, value, StringComparison.Ordinal)))
        {
            return Fail(ClaimResult.AlreadyUsedInCorpus, $"{value} already exists in this project");
        }

        return null;
    }

    private static ClaimOutcome? RejectBlockNetwork(ClaimCorpus corpus, string value)
    {
        var match = BlockNetworkForm.Match(value);
        if (!match.Success)
        {
            return Fail(ClaimResult.Invalid, $"'{value}' is not a block network — expected e.g. FC_ControlMain:8");
        }

        var block = match.Groups[1].Value;
        var network = int.Parse(match.Groups[2].Value);

        if (!corpus.Index.ResolvesAsBlock(block))
        {
            return Fail(ClaimResult.Invalid, $"block '{block}' does not exist in this project");
        }

        return corpus.Index.NetworksOf(block).Contains(network)
            ? Fail(ClaimResult.AlreadyUsedInCorpus, $"{block} already has a network {network}")
            : null;
    }

    private static ClaimOutcome? RejectTag(ClaimCorpus corpus, string value) =>
        corpus.Index.ResolvesAsTagRoot(value)
            ? Fail(ClaimResult.AlreadyUsedInCorpus, $"tag '{value}' already exists — nothing to reserve")
            : null;

    /// <summary>
    /// 🔴 2026-08-14. *** THE ENTIRE DB AND UDT SURFACE OF EVERY PROJECT WAS UNRESERVABLE. ***
    ///
    /// <para>Measured with a positive control in the same run — five FB/OB edit claims succeeded
    /// while <c>iDB_HxBoolEcho</c>, <c>DB_Settings</c> and <c>UDT_HopperBlockageIO</c> were all
    /// refused <i>"does not exist in this project"</i>, about objects that demonstrably exist.</para>
    ///
    /// <para><b>The cause, established before anything was changed: it was ONE LOOKUP</b> — not a
    /// missing index and not a resolution path that cannot see them. <see cref="ProjectIndex"/>
    /// already indexes DBs and types in their own sets and already exposes them; this method asked
    /// only <c>ResolvesAsBlock</c>, which is FB/FC/OB. The message was literally true and completely
    /// misleading: <c>iDB_HxBoolEcho</c> is not a <i>block</i>, it is a DB, and the kind is spelled
    /// <c>block-edit</c>.</para>
    ///
    /// <para><b>Widened rather than given a new kind, and that is the load-bearing choice.</b> Two
    /// kinds with the same EXCLUSIVE semantics over overlapping resources would let agent A take
    /// <c>block-edit FB_X</c> and agent B take <c>object-edit FB_X</c> — *** BOTH GRANTED, THE
    /// REGISTRY FORKED IN THE KIND DIMENSION *** , which is the failure just closed in the path
    /// dimension wearing different clothes. One token, one namespace, one conflict domain. The token
    /// stays <c>block-edit</c> because it is a published contract; what it MEANS is any existing named
    /// object a change is written to.</para>
    ///
    /// <para><c>db-member</c> does NOT cover this, and its name is why that had to be checked rather
    /// than assumed: it is <see cref="ClaimSemantics.Allocation"/> and REFUSES a member that already
    /// exists, so it reserves the ADDITION of a new member. Editing an existing DB is the opposite
    /// question.</para>
    /// </summary>
    private static ClaimOutcome? RejectBlockEdit(ClaimCorpus corpus, string value) =>
        corpus.Index.ResolvesAsBlock(value)
        || corpus.Index.ResolvesAsDb(value)
        || corpus.Index.ResolvesAsType(value)
            ? null
            : Fail(ClaimResult.NotInCorpus,
                $"'{value}' does not exist in this project as a block, a DB or a PLC data type — an exclusive edit claim " +
                "names an object that EXISTS (a tag is addressable but is not an editable object, and is deliberately not accepted here)");

    // The allocation candidates for a kind, in the order they should be tried. Lowest-first so
    // numbering stays dense and predictable rather than drifting upward with every race.
    public static IEnumerable<string> Candidates(ClaimCorpus corpus, ClaimKind kind, string? type, int floor, string? within, out string? error)
    {
        error = null;

        switch (kind)
        {
            case ClaimKind.BlockNumber:
                if (type is null || !BlockNumberForm.IsMatch(type + "0"))
                {
                    error = "--allocate on block-number needs --type FB|FC|OB|DB";
                    return Array.Empty<string>();
                }

                return NumbersFrom(type, Math.Max(floor, 1), out error);

            case ClaimKind.BlockNetwork:
                if (within is null)
                {
                    error = "--allocate on block-network needs --in <BlockName>";
                    return Array.Empty<string>();
                }

                if (!corpus.Index.ResolvesAsBlock(within))
                {
                    error = $"block '{within}' does not exist in this project";
                    return Array.Empty<string>();
                }

                var networks = corpus.Index.NetworksOf(within);
                var next = networks.Count == 0 ? 1 : networks.Max() + 1;
                return Enumerable.Range(next, 64).Select(n => $"{within}:{n}");

            case ClaimKind.AlarmBit:
                if (within is null)
                {
                    error = "--allocate on alarm-bit needs --in <AlarmWordPath>";
                    return Array.Empty<string>();
                }

                var leaf = corpus.Inventory.Leaves.FirstOrDefault(l => string.Equals(l.Path, within, StringComparison.Ordinal));
                if (leaf is null)
                {
                    error = $"alarm word '{within}' does not resolve in this project";
                    return Array.Empty<string>();
                }

                if (!BitWidths.TryGetValue(leaf.Type, out var width))
                {
                    error = $"alarm word '{within}' has type '{leaf.Type}', whose bit width this tool does not know";
                    return Array.Empty<string>();
                }

                return Enumerable.Range(0, width).Select(b => $"{within}.%X{b}");

            default:
                error = $"--allocate is not defined for kind '{ClaimKinds.ToToken(kind)}' — pass --value";
                return Array.Empty<string>();
        }
    }

    // How far past the floor a deliverable search will look. Bounded rather than unbounded: a runaway
    // that walks to int.MaxValue looking for a free number is a hang, and 512 candidates is far beyond
    // any real project's block count.
    private const int SearchWidth = 512;

    /// <summary>
    /// 🔴 X-J's ENFORCING HALF (2026-08-14). Block-number candidates, with the reserved band applied.
    ///
    /// <para>Before this, `--allocate` had NO KNOWLEDGE of the band and would hand out 9000–9999 to a
    /// deliverable without comment — the spec's treatment says the range is reserved <b>and the claim
    /// tool refuses allocations inside it</b>, and only the first half existed.</para>
    ///
    /// <para>Two modes, and which one applies is decided by the FLOOR — a property of the range asked
    /// for, checkable against the declaration, never a boolean about who is asking:</para>
    /// <list type="bullet">
    /// <item><b>Deliverable search</b> (the default, and any floor outside the band): the band is
    /// REMOVED from the candidate set. The walk jumps from the number before the band to the number
    /// after it, so *** NO PLAIN `--allocate` CAN EVER RETURN A BAND NUMBER *** — it is not a
    /// preference the search could exhaust its way past.</item>
    /// <item><b>Band search</b> (a floor inside the band, fed by
    /// <see cref="Ladder.Wave.HarnessNumberRange.AllocationFloor"/>): candidates are CONFINED to the
    /// band and stop at its last number. *** WALKING PAST THE END INTO UNRESERVED SPACE IS THE FAILURE
    /// MODE *** — it hands a harness generator a deliverable number while every check stays green.
    /// Exhaustion is a REFUSAL naming the band, not a quiet step outside it.</item>
    /// </list>
    ///
    /// <para>A space the band does not cover (OB, per the carve-out) is unaffected in both directions:
    /// there is no band in OB's number space, so nothing is skipped and nothing is confined. Applying
    /// one would emit a false finding on <c>OB80</c>, the very first harness object X-J's own treatment
    /// names, and <i>the first false finding is what gets a check switched off</i>.</para>
    /// </summary>
    private static IEnumerable<string> NumbersFrom(string type, int floor, out string? error)
    {
        error = null;
        var band = ReservedBand.Declared;

        if (ReservedBand.IsBandAllocation(type, floor))
        {
            // Confined. `Count` is computed from the band's own last number so it cannot run past it —
            // there is deliberately no width constant here that could drift wider than the band.
            var count = band.LastNumber - floor + 1;
            return Enumerable.Range(floor, count).Select(n => $"{type}{n}");
        }

        // The band is SKIPPED, not merely started past. Written as an explicit walk rather than an
        // arithmetic shift because the shift is wrong at both ends: it has to leave a floor BELOW the
        // band alone until the walk reaches it, and leave a floor ABOVE the band alone entirely, and
        // a `n < First ? n : n + Capacity` displaces the second case into numbers nobody asked for.
        var candidates = new List<string>(SearchWidth);
        var number = floor;
        while (candidates.Count < SearchWidth)
        {
            if (band.CoversSpace(type) && band.ContainsNumber(number))
            {
                number = band.LastNumber + 1;
                continue;
            }

            candidates.Add($"{type}{number}");
            number++;
        }

        return candidates;
    }

    private static ClaimOutcome Fail(ClaimResult result, string reason) =>
        new(result, null, null, reason);
}
