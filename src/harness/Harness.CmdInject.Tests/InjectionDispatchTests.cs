using Harness.CmdInject;

namespace Harness.CmdInject.Tests;

/// <summary>
/// The dispatch: the two transactions reach the transport <b>operands first, sequence alone second</b>,
/// and no single write covers both. Asserted against a recording transport, so ordering is an observed
/// fact rather than a claim about the code.
/// </summary>
public class InjectionDispatchTests
{
    private static readonly IReadOnlyDictionary<InjectionRole, string> Operands = new Dictionary<InjectionRole, string>
    {
        [InjectionRole.Code] = "5",
        [InjectionRole.Int1] = "-3",
        [InjectionRole.Int2] = "1000",
        [InjectionRole.Real1] = "1.5",
        [InjectionRole.Real2] = "-2.5",
    };

    [Fact]
    public void Apply_WritesOperandsThenSequenceAlone()
    {
        var channel = Fixtures.DefaultChannel();
        var frames = CommandFrameBuilder.Build(channel, new CommandRequest(Fixtures.ChannelName, Operands), sequence: 7).Frames!;
        var transport = new RecordingTransport();

        InjectionDispatch.Apply(transport, frames);

        Assert.Equal(2, transport.Writes.Count);

        // First: the operands (register 104..110). Second: the sequence, alone (register 103).
        Assert.Equal(104, transport.Writes[0].StartRegister);
        Assert.Equal(7, transport.Writes[0].Values.Count);

        Assert.Equal(103, transport.Writes[1].StartRegister);
        Assert.Single(transport.Writes[1].Values);
        Assert.Equal((ushort)7, transport.Writes[1].Values[0]);
    }

    [Fact]
    public void Apply_NeverWritesTheSequenceBeforeTheOperands()
    {
        var frames = CommandFrameBuilder.Build(Fixtures.DefaultChannel(), new CommandRequest(Fixtures.ChannelName, Operands), 7).Frames!;
        var transport = new RecordingTransport();

        InjectionDispatch.Apply(transport, frames);

        var sequenceRegister = frames.SequenceWrite.Target.Register;
        var operandWriteIndex = transport.Writes.FindIndex(w => w.StartRegister == frames.Operands.Target.Register);
        var sequenceWriteIndex = transport.Writes.FindIndex(w => w.Covers(sequenceRegister));

        Assert.True(operandWriteIndex < sequenceWriteIndex,
            "the sequence was written no later than the operands — a tear could then land a new sequence against old operands.");
    }

    [Fact]
    public void Apply_NoSingleWriteCoversBothTheSequenceAndAnOperand()
    {
        var frames = CommandFrameBuilder.Build(Fixtures.DefaultChannel(), new CommandRequest(Fixtures.ChannelName, Operands), 7).Frames!;
        var transport = new RecordingTransport();

        InjectionDispatch.Apply(transport, frames);

        var sequenceRegister = frames.SequenceWrite.Target.Register;
        foreach (var write in transport.Writes.Where(w => w.Covers(sequenceRegister)))
            Assert.Single(write.Values); // the write touching the sequence touches ONLY the sequence.
    }
}
