using Harness.CmdInject;
using Harness.MirrorView;

namespace Harness.CmdInject.Tests;

/// <summary>
/// Invented vocabulary and canonical shapes for the tests. <b>Not one job tag, register or code appears
/// here or anywhere in this suite</b> — the map is loaded at runtime from a binding, and these fixtures
/// stand in for it with names a plant never used (<c>UNIT_A</c>, <c>CH1_Seq</c>) and small register
/// numbers chosen for readability.
/// </summary>
internal static class Fixtures
{
    // Invented tag names. None of these is anyone's real signal.
    internal const string HeartbeatTag = "HB_Tick";
    internal const string EnableTag = "EN_Master";
    internal const string SafetyTag = "SAFE_Sim";

    internal const string ChannelName = "UNIT_A";
    internal const string SeqTag = "CH1_Seq";
    internal const string CodeTag = "CH1_Code";
    internal const string Int1Tag = "CH1_Int1";
    internal const string Int2Tag = "CH1_Int2";
    internal const string Real1Tag = "CH1_Real1";
    internal const string Real2Tag = "CH1_Real2";
    internal const string AckSeqTag = "CH1_AckSeq";
    internal const string AckCodeTag = "CH1_AckCode";
    internal const string AckResultTag = "CH1_AckResult";
    internal const string AckCountTag = "CH1_AckCount";

    internal static readonly BandDeclaration CommandBand = new(100, 88);   // 100..187
    internal static readonly BandDeclaration ObservationBand = new(200, 20); // 200..219

    /// <summary>A tag placed on the grid — address text is decorative, register + width are load-bearing.</summary>
    internal static MirrorTag Tag(string name, string type, int register, MirrorWidth width, int bit = 0) =>
        new(name, type, $"%M?{register}", register, width, bit, string.Empty, register);

    /// <summary>The canonical, all-valid tag set. A test mutates a copy for a negative case.</summary>
    internal static List<MirrorTag> DefaultTags() => new()
    {
        Tag(HeartbeatTag, "UInt", 100, MirrorWidth.Word),
        Tag(EnableTag,    "Bool", 101, MirrorWidth.Bit),
        Tag(SafetyTag,    "Bool", 102, MirrorWidth.Bit),

        Tag(SeqTag,   "UInt", 103, MirrorWidth.Word),
        Tag(CodeTag,  "Int",  104, MirrorWidth.Word),
        Tag(Int1Tag,  "Int",  105, MirrorWidth.Word),
        Tag(Int2Tag,  "Int",  106, MirrorWidth.Word),
        Tag(Real1Tag, "Real", 107, MirrorWidth.DoubleWord), // 107..108
        Tag(Real2Tag, "Real", 109, MirrorWidth.DoubleWord), // 109..110

        Tag(AckSeqTag,    "UInt", 200, MirrorWidth.Word),
        Tag(AckCodeTag,   "Int",  201, MirrorWidth.Word),
        Tag(AckResultTag, "Int",  202, MirrorWidth.Word),
        Tag(AckCountTag,  "UInt", 203, MirrorWidth.Word),
    };

    internal static MirrorMap MapWith(IEnumerable<MirrorTag> tags) =>
        new(0, 300, tags.ToList(), "fixture-tags", "fixture-area");

    internal static MirrorMap DefaultMap() => MapWith(DefaultTags());

    internal static IReadOnlyDictionary<InjectionRole, string> DefaultBandRoles() => new Dictionary<InjectionRole, string>
    {
        [InjectionRole.Heartbeat] = HeartbeatTag,
        [InjectionRole.Enable] = EnableTag,
        [InjectionRole.SafetyPermissive] = SafetyTag,
    };

    internal static IReadOnlyDictionary<InjectionRole, string> DefaultChannelRoles() => new Dictionary<InjectionRole, string>
    {
        [InjectionRole.Seq] = SeqTag,
        [InjectionRole.Code] = CodeTag,
        [InjectionRole.Int1] = Int1Tag,
        [InjectionRole.Int2] = Int2Tag,
        [InjectionRole.Real1] = Real1Tag,
        [InjectionRole.Real2] = Real2Tag,
        [InjectionRole.AckSeq] = AckSeqTag,
        [InjectionRole.AckCode] = AckCodeTag,
        [InjectionRole.AckResult] = AckResultTag,
        [InjectionRole.AckCount] = AckCountTag,
    };

    internal static InjectionBinding DefaultBinding() => new(
        CommandBand,
        new[] { ObservationBand },
        DefaultBandRoles(),
        new[] { new ChannelDeclaration(ChannelName, DefaultChannelRoles()) });

    /// <summary>The canonical resolved binding — resolution succeeds, for the tests that need one downstream.</summary>
    internal static ResolvedBinding DefaultResolved()
    {
        var result = BindingResolver.Resolve(DefaultBinding(), DefaultMap());
        Assert.True(result.Ok, "fixture invariant: the default binding must resolve. Refusals: " +
                               string.Join(" | ", result.Refusals));
        return result.Binding!;
    }

    internal static ResolvedChannel DefaultChannel() => DefaultResolved().Channel(ChannelName)!;
}
