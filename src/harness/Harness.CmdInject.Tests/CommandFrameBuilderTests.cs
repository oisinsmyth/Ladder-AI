using Harness.CmdInject;
using Harness.MirrorView;

namespace Harness.CmdInject.Tests;

/// <summary>
/// The frame builder: the two-transaction split, the operand spans, the value refusals by name, and the
/// containment of every write inside the command band.
/// </summary>
public class CommandFrameBuilderTests
{
    private static readonly IReadOnlyDictionary<InjectionRole, string> FullOperands = new Dictionary<InjectionRole, string>
    {
        [InjectionRole.Code] = "5",
        [InjectionRole.Int1] = "-3",
        [InjectionRole.Int2] = "1000",
        [InjectionRole.Real1] = "1.5",
        [InjectionRole.Real2] = "-2.5",
    };

    private static CommandRequest Request(IReadOnlyDictionary<InjectionRole, string> operands) =>
        new(Fixtures.ChannelName, operands);

    // ---- a channel that declares only a sequence and a code, for the missing/orphan cases ----

    private static ResolvedChannel MinimalChannel()
    {
        var tags = new List<MirrorTag>
        {
            Fixtures.Tag(Fixtures.HeartbeatTag, "UInt", 100, MirrorWidth.Word),
            Fixtures.Tag(Fixtures.EnableTag, "Bool", 101, MirrorWidth.Bit),
            Fixtures.Tag(Fixtures.SeqTag, "UInt", 103, MirrorWidth.Word),
            Fixtures.Tag(Fixtures.CodeTag, "Int", 104, MirrorWidth.Word),
            Fixtures.Tag(Fixtures.AckSeqTag, "UInt", 200, MirrorWidth.Word),
            Fixtures.Tag(Fixtures.AckResultTag, "Int", 201, MirrorWidth.Word),
            Fixtures.Tag(Fixtures.AckCountTag, "UInt", 202, MirrorWidth.Word),
        };

        var binding = new InjectionBinding(
            Fixtures.CommandBand,
            new[] { Fixtures.ObservationBand },
            new Dictionary<InjectionRole, string>
            {
                [InjectionRole.Heartbeat] = Fixtures.HeartbeatTag,
                [InjectionRole.Enable] = Fixtures.EnableTag,
            },
            new[]
            {
                new ChannelDeclaration(Fixtures.ChannelName, new Dictionary<InjectionRole, string>
                {
                    [InjectionRole.Seq] = Fixtures.SeqTag,
                    [InjectionRole.Code] = Fixtures.CodeTag,
                    [InjectionRole.AckSeq] = Fixtures.AckSeqTag,
                    [InjectionRole.AckResult] = Fixtures.AckResultTag,
                    [InjectionRole.AckCount] = Fixtures.AckCountTag,
                }),
            });

        var resolved = BindingResolver.Resolve(binding, Fixtures.MapWith(tags));
        Assert.True(resolved.Ok, string.Join(" | ", resolved.Refusals));
        return resolved.Binding!.Channel(Fixtures.ChannelName)!;
    }

    // ---- frame spans -----------------------------------------------------------------------------

    [Fact]
    public void AFullCommand_HasTheExpectedSpansAndEncoding()
    {
        var channel = Fixtures.DefaultChannel();
        var result = CommandFrameBuilder.Build(channel, Request(FullOperands), sequence: 7);

        Assert.True(result.Ok, string.Join(" | ", result.Refusals));
        var frames = result.Frames!;

        // Operands: registers 104..110 (code, int1, int2, real1[2], real2[2]).
        Assert.Equal(InjectionRegion.Operands, frames.Operands.Target.Region);
        Assert.Equal(104, frames.Operands.Target.Register);
        Assert.Equal(7, frames.Operands.Target.Length);
        Assert.Equal(new ushort[] { 5, 0xFFFD, 1000, 0x3FC0, 0x0000, 0xC020, 0x0000 }, frames.Operands.Values.ToArray());

        // Sequence: register 103, ALONE.
        Assert.Equal(InjectionRegion.Sequence, frames.SequenceWrite.Target.Region);
        Assert.Equal(103, frames.SequenceWrite.Target.Register);
        Assert.Single(frames.SequenceWrite.Values);
        Assert.Equal((ushort)7, frames.SequenceWrite.Values[0]);
    }

    [Fact]
    public void TheTwoTransactionsAreDisjointAndTheSequenceIsAlone()
    {
        var frames = CommandFrameBuilder.Build(Fixtures.DefaultChannel(), Request(FullOperands), 7).Frames!;

        // No write covers both the sequence and any operand register.
        Assert.False(frames.Operands.Covers(frames.SequenceWrite.Target.Register));
        Assert.Single(frames.SequenceWrite.Values);
        Assert.False(frames.SequenceWrite.Covers(frames.Operands.Target.Register));

        // Order: operands first, sequence second.
        Assert.Same(frames.Operands, frames.InOrder[0]);
        Assert.Same(frames.SequenceWrite, frames.InOrder[1]);
    }

    [Fact]
    public void EveryWrite_LandsInsideTheCommandBand()
    {
        var frames = CommandFrameBuilder.Build(Fixtures.DefaultChannel(), Request(FullOperands), 7).Frames!;
        var band = Fixtures.CommandBand;

        foreach (var transaction in frames.InOrder)
        {
            Assert.True(band.Contains(transaction.Target.Register), $"{transaction.Target} starts outside the command band.");
            Assert.True(band.Contains(transaction.Target.LastRegister), $"{transaction.Target} ends outside the command band.");
        }
    }

    [Fact]
    public void AMinimalChannel_EncodesOnlyItsCode()
    {
        var frames = CommandFrameBuilder.Build(MinimalChannel(), Request(new Dictionary<InjectionRole, string> { [InjectionRole.Code] = "42" }), 3).Frames!;

        Assert.Equal(104, frames.Operands.Target.Register);
        Assert.Equal(1, frames.Operands.Target.Length);
        Assert.Equal(new ushort[] { 42 }, frames.Operands.Values.ToArray());
        Assert.Equal(103, frames.SequenceWrite.Target.Register);
    }

    // ---- value refusals by name ------------------------------------------------------------------

    [Fact]
    public void AMissingOperand_IsRefusedByName()
    {
        var operands = FullOperands.Where(kv => kv.Key != InjectionRole.Int1).ToDictionary(kv => kv.Key, kv => kv.Value);
        var result = CommandFrameBuilder.Build(Fixtures.DefaultChannel(), Request(operands), 7);

        Assert.False(result.Ok);
        Assert.Contains(result.Refusals, r => r.Contains("declares operand role 'Int1' but the request supplies no value"));
    }

    [Fact]
    public void AnOrphanOperand_IsRefusedByName()
    {
        // The minimal channel declares no Int1; supplying one is a value written nowhere.
        var operands = new Dictionary<InjectionRole, string> { [InjectionRole.Code] = "1", [InjectionRole.Int1] = "9" };
        var result = CommandFrameBuilder.Build(MinimalChannel(), Request(operands), 3);

        Assert.False(result.Ok);
        Assert.Contains(result.Refusals, r => r.Contains("supplies a value for role 'Int1'") && r.Contains("does not declare"));
    }

    [Fact]
    public void AnIntegerOperandOutOfRange_IsRefusedByNameNotTruncated()
    {
        var operands = new Dictionary<InjectionRole, string>(FullOperands) { [InjectionRole.Code] = "99999" };
        var result = CommandFrameBuilder.Build(Fixtures.DefaultChannel(), Request(operands), 7);

        Assert.False(result.Ok);
        Assert.Contains(result.Refusals, r => r.Contains("UNIT_A.Code") && r.Contains("does not fit"));
    }

    [Fact]
    public void ANonNumericIntegerOperand_IsRefusedByName()
    {
        var operands = new Dictionary<InjectionRole, string>(FullOperands) { [InjectionRole.Int1] = "not-a-number" };
        var result = CommandFrameBuilder.Build(Fixtures.DefaultChannel(), Request(operands), 7);

        Assert.False(result.Ok);
        Assert.Contains(result.Refusals, r => r.Contains("UNIT_A.Int1") && r.Contains("not an integer"));
    }

    [Fact]
    public void ANonNumericRealOperand_IsRefusedByName()
    {
        var operands = new Dictionary<InjectionRole, string>(FullOperands) { [InjectionRole.Real1] = "xyz" };
        var result = CommandFrameBuilder.Build(Fixtures.DefaultChannel(), Request(operands), 7);

        Assert.False(result.Ok);
        Assert.Contains(result.Refusals, r => r.Contains("UNIT_A.Real1") && r.Contains("is not a number"));
    }
}
