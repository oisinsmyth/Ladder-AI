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
        var lastDot = value.LastIndexOf('.');
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

    private static ClaimOutcome? RejectBlockEdit(ClaimCorpus corpus, string value) =>
        corpus.Index.ResolvesAsBlock(value)
            ? null
            : Fail(ClaimResult.NotInCorpus, $"block '{value}' does not exist in this project");

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

                return NumbersFrom(corpus, type, Math.Max(floor, 1));

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

    // Bounded rather than unbounded: a runaway that walks to int.MaxValue looking for a free number is
    // a hang, and 512 candidates past the floor is far beyond any real project's block count.
    private static IEnumerable<string> NumbersFrom(ClaimCorpus corpus, string type, int floor) =>
        Enumerable.Range(floor, 512).Select(n => $"{type}{n}");

    private static ClaimOutcome Fail(ClaimResult result, string reason) =>
        new(result, null, null, reason);
}
