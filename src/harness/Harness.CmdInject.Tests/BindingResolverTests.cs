using Harness.CmdInject;
using Harness.MirrorView;

namespace Harness.CmdInject.Tests;

/// <summary>
/// The resolver, refusal by refusal. <b>Each negative case asserts the SENTENCE, not merely that it
/// refused</b> — a resolver that refuses for the wrong reason is a resolver that will one day accept for
/// the wrong reason.
/// </summary>
public class BindingResolverTests
{
    private static ResolveResult Resolve(MirrorMap map, InjectionBinding binding) =>
        BindingResolver.Resolve(binding, map);

    private static List<MirrorTag> TagsReplacing(string name, MirrorTag replacement)
    {
        var tags = Fixtures.DefaultTags();
        tags.RemoveAll(t => t.Name == name);
        tags.Add(replacement);
        return tags;
    }

    [Fact]
    public void TheDefaultBinding_Resolves()
    {
        var result = Resolve(Fixtures.DefaultMap(), Fixtures.DefaultBinding());

        Assert.True(result.Ok, string.Join(" | ", result.Refusals));
        var channel = result.Binding!.Channel(Fixtures.ChannelName)!;
        Assert.Equal(103, channel.SequenceRegister);
        Assert.Equal(104, channel.OperandFirstRegister);
        Assert.Equal(7, channel.OperandRegisterCount); // code+int1+int2 (3) + real1(2) + real2(2)
    }

    [Fact]
    public void ATagTheMapDoesNotCarry_IsRefusedByName()
    {
        var tags = Fixtures.DefaultTags();
        tags.RemoveAll(t => t.Name == Fixtures.SeqTag); // binding still names CH1_Seq; map no longer has it.

        var result = Resolve(Fixtures.MapWith(tags), Fixtures.DefaultBinding());

        Assert.False(result.Ok);
        Assert.Contains(result.Refusals, r => r.Contains("the map does not carry tag 'CH1_Seq'"));
    }

    [Fact]
    public void ATagWhoseTypeIsNotWhatTheRoleRequires_IsRefusedByName()
    {
        // CH1_Seq must be a UInt; give the map an Int of the same width.
        var tags = TagsReplacing(Fixtures.SeqTag, Fixtures.Tag(Fixtures.SeqTag, "Int", 103, MirrorWidth.Word));

        var result = Resolve(Fixtures.MapWith(tags), Fixtures.DefaultBinding());

        Assert.False(result.Ok);
        Assert.Contains(result.Refusals, r =>
            r.Contains("requires a UInt (Word)") && r.Contains("declares it Int"));
    }

    [Fact]
    public void ACommandMemberOutsideTheCommandBand_IsRefusedByName()
    {
        // Move the code to a register still on the grid but outside the command band (100..187).
        var tags = TagsReplacing(Fixtures.CodeTag, Fixtures.Tag(Fixtures.CodeTag, "Int", 190, MirrorWidth.Word));

        var result = Resolve(Fixtures.MapWith(tags), Fixtures.DefaultBinding());

        Assert.False(result.Ok);
        Assert.Contains(result.Refusals, r =>
            r.Contains("CH1_Code") && r.Contains("outside the declared command band"));
    }

    [Fact]
    public void AnAckMemberOutsideEveryObservationBand_IsRefusedByName()
    {
        // Move the ack count outside the observation band (200..219) but still on the grid.
        var tags = TagsReplacing(Fixtures.AckCountTag, Fixtures.Tag(Fixtures.AckCountTag, "UInt", 250, MirrorWidth.Word));

        var result = Resolve(Fixtures.MapWith(tags), Fixtures.DefaultBinding());

        Assert.False(result.Ok);
        Assert.Contains(result.Refusals, r =>
            r.Contains("CH1_AckCount") && r.Contains("outside every declared observation band"));
    }

    [Fact]
    public void TwoRolesSharingARegister_IsRefusedByName()
    {
        // Put the enable on the same register as the sequence.
        var tags = TagsReplacing(Fixtures.EnableTag, Fixtures.Tag(Fixtures.EnableTag, "Bool", 103, MirrorWidth.Bit));

        var result = Resolve(Fixtures.MapWith(tags), Fixtures.DefaultBinding());

        Assert.False(result.Ok);
        Assert.Contains(result.Refusals, r =>
            r.Contains("register 103 is claimed by more than one role"));
    }

    [Fact]
    public void NonContiguousCommandMembers_IsRefusedByName()
    {
        // Leave a gap: move int2 from 106 to 108, so 106 and 107(real1 low) are followed by a jump. Actually
        // real1 occupies 107..108, so putting int2 at 112 makes 110 -> 112 a gap.
        var tags = TagsReplacing(Fixtures.Int2Tag, Fixtures.Tag(Fixtures.Int2Tag, "Int", 112, MirrorWidth.Word));

        var result = Resolve(Fixtures.MapWith(tags), Fixtures.DefaultBinding());

        Assert.False(result.Ok);
        Assert.Contains(result.Refusals, r =>
            r.Contains(Fixtures.ChannelName) && r.Contains("non-contiguous command members"));
    }

    [Fact]
    public void ASequenceNotAtTheLowAddress_IsRefusedByName()
    {
        // Swap the sequence up to 111 (above real2), so it is contiguous but no longer the lowest register.
        var tags = TagsReplacing(Fixtures.SeqTag, Fixtures.Tag(Fixtures.SeqTag, "UInt", 111, MirrorWidth.Word));

        var result = Resolve(Fixtures.MapWith(tags), Fixtures.DefaultBinding());

        Assert.False(result.Ok);
        Assert.Contains(result.Refusals, r =>
            r.Contains("sequence must sit at the LOW address"));
    }

    [Fact]
    public void ADeclaredBandThatDisagreesWithTheUnion_IsRefusedByName()
    {
        // Declare the command band starting one register below where anything lands.
        var binding = Fixtures.DefaultBinding() with { CommandBand = new BandDeclaration(99, 89) };

        var result = Resolve(Fixtures.DefaultMap(), binding);

        Assert.False(result.Ok);
        Assert.Contains(result.Refusals, r =>
            r.Contains("disagrees with what resolved into it") && r.Contains("begins at register 99"));
    }

    [Fact]
    public void AnObservationBandNothingLandsIn_IsRefusedByName()
    {
        // A second observation band that no ack member uses.
        var binding = Fixtures.DefaultBinding() with
        {
            ObservationBands = new[] { Fixtures.ObservationBand, new BandDeclaration(250, 10) },
        };

        var result = Resolve(Fixtures.DefaultMap(), binding);

        Assert.False(result.Ok);
        Assert.Contains(result.Refusals, r =>
            r.Contains("has NO role resolve into it"));
    }
}
