using System.Globalization;
using System.Text.RegularExpressions;
using Harness.CmdInject;

namespace Harness.CmdInject.Loopback.Tests;

/// <summary>
/// L5 — <b>the printed frames, measured against what a real Modbus server received.</b>
///
/// <para>Everything below the client's own formatter is real here: NModbus encodes the PDU, a socket
/// carries it, NModbus decodes it, and a recording point source writes down the start address and the
/// values <i>as they arrived</i>. Until this existed, "the frames are exactly these bytes" was a claim
/// about our own string formatting, checked by reading it.</para>
///
/// <para><b>What this still does NOT prove.</b> It proves the client puts on the wire what it says it
/// will. It proves nothing about how a Siemens <c>MB_SERVER</c> applies those registers — one scan or
/// two, torn or whole — because the server here is a C# dictionary. That question belongs to the rig.</para>
///
/// <para>The listener is loopback-only on an ephemeral port; nothing here is reachable off this machine,
/// and no fixed port can collide with a real service.</para>
/// </summary>
public class LoopbackSendTests : IDisposable
{
    private readonly LoopbackFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void TheFramesThePrinterShows_AreTheRegistersTheServerReceives()
    {
        using var slave = new LoopbackSlave();
        _fixture.Present(slave.Registers, LoopbackFixture.Stamp, enable: true);

        // 1. The DRY run: prints the frames, opens nothing.
        var dry = new StringWriter();
        var dryExit = SendRun.Execute(_fixture.Send(armed: false, slave.Port), new SequenceLedger(), factory: null, dry);

        Assert.Equal(CmdInjectExit.DryRunNotArmed, dryExit);
        Assert.Empty(slave.Registers.Writes);
        Assert.Empty(slave.Registers.Reads);

        var printed = PrintedRegisters(dry.ToString());
        Assert.NotEmpty(printed);

        // 2. The ARMED run, over a real socket, with the production factory.
        var live = new StringWriter();
        var exit = SendRun.Execute(
            _fixture.Send(armed: true, slave.Port), new SequenceLedger(),
            new ModbusInjectionTransportFactory(), live, new InstantClock());

        // The server moves no acknowledgement count, so the honest ending is "not acknowledged" — this test
        // is about the bytes, not about the verdict.
        Assert.Equal(CmdInjectExit.NotAcknowledged, exit);

        // 3. THE MEASUREMENT. The first two writes the server received are the command, and every register
        //    value in them equals what the dry run printed.
        var commandWrites = slave.Registers.Writes.Take(2).ToList();
        var received = new Dictionary<int, ushort>();
        foreach (var (start, values) in commandWrites)
        {
            for (var i = 0; i < values.Length; i++)
                received[start + i] = values[i];
        }

        foreach (var (register, value) in printed)
        {
            Assert.True(received.ContainsKey(register),
                $"the dry run printed register {register}, and no write the server received covered it.");
            Assert.Equal(value, received[register]);
        }

        Assert.Equal(printed.Count, received.Count);
    }

    [Fact]
    public void TheCommandArrivesAsTwoWrites_OperandsFirst_SequenceAlone()
    {
        using var slave = new LoopbackSlave();
        _fixture.Present(slave.Registers, LoopbackFixture.Stamp, enable: true);

        SendRun.Execute(
            _fixture.Send(armed: true, slave.Port), new SequenceLedger(),
            new ModbusInjectionTransportFactory(), new StringWriter(), new InstantClock());

        // Measured on the wire, not in a recording double: two separate FC16s, operands first, and the one
        // that carries the sequence carries NOTHING else. A single write of the whole channel would land the
        // new sequence before its operands, executing a command against the previous one's values.
        var first = slave.Registers.Writes[0];
        var second = slave.Registers.Writes[1];

        Assert.Equal(LoopbackFixture.CodeRegister, first.Start);
        Assert.Equal(2, first.Values.Length);
        Assert.Equal(LoopbackFixture.SeqRegister, second.Start);
        Assert.Single(second.Values);
        Assert.Equal((ushort)1, second.Values[0]);
    }

    [Fact]
    public void ThePollReachesTheStampAndTheAckInOneRead_WhenTheMapAllowsIt()
    {
        using var slave = new LoopbackSlave();
        _fixture.Present(slave.Registers, LoopbackFixture.Stamp, enable: true);

        SendRun.Execute(
            _fixture.Send(armed: true, slave.Port, pollAttempts: 3), new SequenceLedger(),
            new ModbusInjectionTransportFactory(), new StringWriter(), new InstantClock());

        // PollPlan says one FC03 from register zero reaches both the identity registers and the ack span on
        // this map. Here that is measured: three polls, three reads, each starting at register 0 and long
        // enough to cover the acknowledgement count.
        var polls = slave.Registers.Reads
            .Where(r => r.Start == 0 && r.Count > ControlRegisters.Count)
            .ToList();

        Assert.Equal(3, polls.Count);
        Assert.All(polls, poll => Assert.True(poll.Start + poll.Count - 1 >= LoopbackFixture.AckCountRegister));
    }

    [Fact]
    public void TheBandIsPutBack_AndTheEnableGoesDownFirst_OverARealSocket()
    {
        using var slave = new LoopbackSlave();
        _fixture.Present(slave.Registers, LoopbackFixture.Stamp, enable: true);
        slave.Registers[LoopbackFixture.Int1Register] = 0x0777; // something in the band worth putting back

        var enableTag = _fixture.Resolved().BandRoles[InjectionRole.Enable];

        SendRun.Execute(
            _fixture.Send(armed: true, slave.Port), new SequenceLedger(),
            new ModbusInjectionTransportFactory(), new StringWriter(), new InstantClock());

        // Four writes reached the server: operands, sequence, enable-down, band.
        Assert.Equal(4, slave.Registers.Writes.Count);

        var enableDrop = slave.Registers.Writes[2];
        Assert.Equal(enableTag.Register, enableDrop.Start);
        Assert.Single(enableDrop.Values);
        Assert.Equal(0, enableDrop.Values[0] & (1 << enableTag.BitInRegister));

        var bandRestore = slave.Registers.Writes[3];
        Assert.Equal(LoopbackFixture.CommandBandFirst, bandRestore.Start);
        Assert.Equal(LoopbackFixture.CommandBandCount, bandRestore.Values.Length);

        // And the band really is back as it was found — including the value that was in it before the run.
        Assert.Equal((ushort)0x0777, slave.Registers[LoopbackFixture.Int1Register]);
        Assert.Equal((ushort)0, slave.Registers[LoopbackFixture.SeqRegister]);
        Assert.Equal(0, slave.Registers[enableTag.Register] & (1 << enableTag.BitInRegister));
    }

    [Fact]
    public void AWrongStampOverARealSocket_WritesNothingAtAll()
    {
        using var slave = new LoopbackSlave();
        _fixture.Present(slave.Registers, new Harness.Map.BuildStamp(0x99998888), enable: true);

        var exit = SendRun.Execute(
            _fixture.Send(armed: true, slave.Port), new SequenceLedger(),
            new ModbusInjectionTransportFactory(), new StringWriter(), new InstantClock());

        Assert.Equal(CmdInjectExit.StampMismatch, exit);

        // The socket was opened and one read was made. Not one write was.
        Assert.NotEmpty(slave.Registers.Reads);
        Assert.Empty(slave.Registers.Writes);
    }

    [Fact]
    public void AClosedPort_IsAConnectFailure_NotADeviceThatRefused()
    {
        int port;
        using (var slave = new LoopbackSlave())
        {
            port = slave.Port;
        }

        var exit = SendRun.Execute(
            _fixture.Send(armed: true, port), new SequenceLedger(),
            new ModbusInjectionTransportFactory(), new StringWriter(), new InstantClock());

        Assert.Equal(CmdInjectExit.ConnectFailed, exit);
    }

    /// <summary>Parse the dry run's own byte dump back out of its output: <c>reg  NNN = 0xVVVV</c>.</summary>
    private static Dictionary<int, ushort> PrintedRegisters(string output)
    {
        var printed = new Dictionary<int, ushort>();

        foreach (Match match in Regex.Matches(output, @"reg\s+(\d+) = 0x([0-9A-F]{4})"))
        {
            printed[int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)] =
                ushort.Parse(match.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        return printed;
    }
}

/// <summary>A clock that does not wait, so a loopback poll costs no time.</summary>
internal sealed class InstantClock : IInjectionClock
{
    public void Wait(int milliseconds, CancellationToken cancel) { }
}
