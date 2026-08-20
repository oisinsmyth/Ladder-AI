using Harness.CmdInject;

namespace Harness.CmdInject.Tests;

/// <summary>
/// The <c>send</c> run, driven with a RECORDING factory.
///
/// <para>🔴 <b>OPENS == 0 IS THE OBSERVABLE FACT THAT THE FENCE HELD.</b> A test asserting an exit code
/// proves the run ended a particular way; it does not prove no socket was opened, because a disconnected
/// gate can produce the same code by accident. Every refusal and every dry run asserts the counter is
/// zero, and the one authorised case asserts it is one — so the counter, not the exit code, carries the
/// claim.</para>
/// </summary>
public class SendRunTests : IDisposable
{
    private readonly FixtureFiles _files = new();
    private readonly string _allowlistDir =
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "cmd-inject-send-" + Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose()
    {
        _files.Dispose();
        try { Directory.Delete(_allowlistDir, recursive: true); }
        catch (IOException) { /* leftover temp dir is not a failure */ }
    }

    private const string Rig = "10.10.10.10";
    private const uint Stamp = 0xF52ECEAD;

    private string Allowlist(string name, bool writeEligible = true) =>
        WriteFile(name, $$"""
          {
            "entries": [
              { "address": "{{Rig}}", "kind": "test-rig",
                "writeEligible": {{(writeEligible ? "true" : "false")}},
                "outputsIsolated": true, "isolationAssertedBy": "a-tester" }
            ]
          }
          """);

    private string WriteFile(string name, string content)
    {
        var path = Path.Combine(_allowlistDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static readonly IReadOnlyDictionary<InjectionRole, string> Operands = new Dictionary<InjectionRole, string>
    {
        [InjectionRole.Code] = "5",
        [InjectionRole.Int1] = "-3",
        [InjectionRole.Int2] = "1000",
        [InjectionRole.Real1] = "1.5",
        [InjectionRole.Real2] = "-2.5",
    };

    private SendOptions Options(bool armed, string? target, string? allowlist, uint? stamp) =>
        new(_files.TagsPath, _files.AreaPath, _files.BindingPath, Fixtures.ChannelName, Operands,
            armed, target, allowlist, stamp, Port: 503, UnitId: 1);

    // ---- dry run opens nothing -------------------------------------------------------------------

    [Fact]
    public void ADryRun_PrintsFramesAndOpensNothing()
    {
        var factory = new RecordingFactory();
        var output = new StringWriter();

        var exit = SendRun.Execute(Options(armed: false, Rig, Allowlist("ok.json"), Stamp), new SequenceLedger(), factory, output);

        Assert.Equal(CmdInjectExit.DryRunNotArmed, exit);
        Assert.Equal(0, factory.Opens);
        Assert.Contains("NOT READ", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("bytes", output.ToString(), StringComparison.Ordinal);

        // The arming plan is part of the plan a dry run prints. It is the only description of the writes a
        // real run makes BEFORE the command, and leaving it out would make the dry run describe the smaller
        // half of what --arm would do.
        Assert.Contains("arming", output.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("scan", output.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    // ---- every refusal opens nothing -------------------------------------------------------------

    [Fact]
    public void AnUnresolvableBinding_IsRefusedAndOpensNothing()
    {
        // A binding that names a channel role no map tag backs.
        using var broken = new FixtureFiles(FixtureFiles.DefaultBindingJson().Replace(Fixtures.SeqTag, "NOPE_Missing"));
        var factory = new RecordingFactory();
        var options = new SendOptions(broken.TagsPath, broken.AreaPath, broken.BindingPath, Fixtures.ChannelName,
            Operands, Armed: true, Rig, Allowlist("ok.json"), Stamp, 503, 1);

        var exit = SendRun.Execute(options, new SequenceLedger(), factory, new StringWriter());

        Assert.Equal(CmdInjectExit.MapRefused, exit);
        Assert.Equal(0, factory.Opens);
    }

    [Fact]
    public void ArmedButNotWriteEligible_IsRefusedAndOpensNothing()
    {
        var factory = new RecordingFactory();

        var exit = SendRun.Execute(Options(armed: true, Rig, Allowlist("nowrite.json", writeEligible: false), Stamp),
            new SequenceLedger(), factory, new StringWriter());

        Assert.Equal(CmdInjectExit.NotWriteEligible, exit);
        Assert.Equal(0, factory.Opens);
    }

    [Fact]
    public void ArmedWithNoAllowlist_IsAUsageErrorAndOpensNothing()
    {
        var factory = new RecordingFactory();

        var exit = SendRun.Execute(Options(armed: true, Rig, allowlist: null, Stamp), new SequenceLedger(), factory, new StringWriter());

        Assert.Equal(CmdInjectExit.Usage, exit);
        Assert.Equal(0, factory.Opens);
    }

    [Fact]
    public void ArmedWithNoExpectedStamp_IsRefusedAndOpensNothing()
    {
        var factory = new RecordingFactory();

        var exit = SendRun.Execute(Options(armed: true, Rig, Allowlist("ok.json"), stamp: null), new SequenceLedger(), factory, new StringWriter());

        Assert.Equal(CmdInjectExit.NoExpectedBuildStamp, exit);
        Assert.Equal(0, factory.Opens);
    }

    // ---- a caller supplying no factory reaches no device -----------------------------------------

    [Fact]
    public void ArmedAndAllowedButNoFactory_IsNoTransportAndContactsNothing()
    {
        var exit = SendRun.Execute(Options(armed: true, Rig, Allowlist("ok.json"), Stamp), new SequenceLedger(), factory: null, new StringWriter());

        Assert.Equal(CmdInjectExit.NoTransportInThisBuild, exit);
    }

    // ---- the one authorised path DOES open, exactly once -----------------------------------------

    /// <summary>A transport presenting itself the way a running harness program does.</summary>
    private static RecordingTransport Running(bool enable = true)
    {
        var transport = new RecordingTransport();
        transport.PublishStamp(new Harness.Map.BuildStamp(Stamp));
        transport.PublishScan(700);

        if (enable)
        {
            var tag = Fixtures.DefaultResolved().BandRoles[InjectionRole.Enable];
            transport.Poke(tag.Register, (ushort)(1 << tag.BitInRegister));
        }

        return transport;
    }

    [Fact]
    public void ArmedAndAllowedWithAFactory_OpensExactlyOnceAndAppliesInOrder()
    {
        var transport = Running();
        var factory = new RecordingFactory(transport);

        var exit = SendRun.Execute(
            Options(armed: true, Rig, Allowlist("ok.json"), Stamp) with { PollAttempts = 2, PollIntervalMs = 0 },
            new SequenceLedger(), factory, new StringWriter(), new InstantClock());

        // The device never acknowledges (a recording store moves no count), which is an honest ending — not
        // an error, and not a success.
        Assert.Equal(CmdInjectExit.NotAcknowledged, exit);
        Assert.Equal(1, factory.Opens);
        Assert.Equal(Rig, factory.LastHost);
        Assert.Equal(503, factory.LastPort);

        // SIX writes, in one fixed order: the two heartbeat stamps that arm the block, the command's two
        // transactions, and the two the restore makes. The arming comes FIRST and it is part of the run's
        // shape, not a detail — a command written before it would be one the block declines to process.
        var heartbeat = Fixtures.DefaultResolved().BandRoles[InjectionRole.Heartbeat].Register;

        Assert.Equal(6, transport.Writes.Count);

        Assert.Equal(heartbeat, transport.Writes[0].StartRegister);
        Assert.Single(transport.Writes[0].Values);
        Assert.Equal(heartbeat, transport.Writes[1].StartRegister);
        Assert.Single(transport.Writes[1].Values);
        Assert.NotEqual(transport.Writes[0].Values[0], transport.Writes[1].Values[0]);

        // Then the command reached the transport operands-first, sequence-alone.
        Assert.Equal(104, transport.Writes[2].StartRegister);
        Assert.Equal(103, transport.Writes[3].StartRegister);
        Assert.Single(transport.Writes[3].Values);

        // And the band was put back afterwards: the enable alone, then the whole band.
        Assert.Single(transport.Writes[4].Values);
        Assert.Equal(Fixtures.CommandBand.FirstRegister, transport.Writes[5].StartRegister);
        Assert.Equal(Fixtures.CommandBand.RegisterCount, transport.Writes[5].Values.Count);
    }

    [Fact]
    public void TheArmingHappensAfterTheEnableIsRaised_AndTheRestorePutsTheHeartbeatBack()
    {
        // The order is load-bearing: the heartbeat reaches the block only while the enable is up, so a stamp
        // made before the raise would land in memory and count for nothing.
        var transport = Running(enable: false);
        var factory = new RecordingFactory(transport);
        var binding = Fixtures.DefaultResolved();
        var enable = binding.BandRoles[InjectionRole.Enable].Register;
        var heartbeat = binding.BandRoles[InjectionRole.Heartbeat].Register;

        SendRun.Execute(
            Options(armed: true, Rig, Allowlist("ok.json"), Stamp) with { RaiseEnable = true, PollAttempts = 1, PollIntervalMs = 0 },
            new SequenceLedger(), factory, new StringWriter(), new InstantClock());

        var enableRaise = transport.Writes.FindIndex(w => w.StartRegister == enable && w.Values.Count == 1);
        var firstStamp = transport.Writes.FindIndex(w => w.StartRegister == heartbeat && w.Values.Count == 1);

        Assert.True(enableRaise >= 0 && firstStamp >= 0, "both the enable raise and the heartbeat stamp must have happened.");
        Assert.True(enableRaise < firstStamp, "the enable was raised no earlier than the first stamp, so the stamp reached nothing.");

        // And the heartbeat register goes back to what the session found there, like every other register in
        // the band. What that does to the block's own arming state is not something this tool claims to know.
        Assert.Equal((ushort)0, transport.Peek(heartbeat));
    }

    [Fact]
    public void AStampedRunOverAStalledScanCounter_RefusesBeforeWritingACommand()
    {
        // A CPU that is not scanning samples nothing, so a second stamp would be issued hopefully. The run
        // refuses there rather than sending a command whose "not acknowledged" would be unreadable.
        var transport = Running();
        transport.ScanAdvancesOnRead = false;
        var factory = new RecordingFactory(transport);

        var output = new StringWriter();
        var exit = SendRun.Execute(
            Options(armed: true, Rig, Allowlist("ok.json"), Stamp) with
            {
                PollAttempts = 1,
                PollIntervalMs = 0,
                Heartbeat = new HeartbeatPlan(HeartbeatPlan.ModelledChanges, ScanWaitAttempts: 3),
            },
            new SequenceLedger(), factory, output, new InstantClock());

        Assert.Equal(CmdInjectExit.Aborted, exit);
        Assert.Contains("ScanStalled", output.ToString(), StringComparison.Ordinal);

        // The command never went out: no write to the sequence register.
        Assert.DoesNotContain(transport.Writes, w => w.StartRegister == Fixtures.DefaultChannel().SequenceRegister && w.Values.Count == 1);
    }

    [Fact]
    public void ArmedAgainstTheWrongBuild_WritesNothingAndSaysWhy()
    {
        var transport = Running();
        transport.PublishStamp(new Harness.Map.BuildStamp(0x00C0FFEE));
        var factory = new RecordingFactory(transport);
        var output = new StringWriter();

        var exit = SendRun.Execute(
            Options(armed: true, Rig, Allowlist("ok.json"), Stamp), new SequenceLedger(), factory, output, new InstantClock());

        Assert.Equal(CmdInjectExit.StampMismatch, exit);
        Assert.Equal(1, factory.Opens);
        Assert.Empty(transport.Writes);
        Assert.Contains("EVERY address it would write to is a guess", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ArmedWithTheEnableClear_RefusesBeforeWritingAnything()
    {
        var transport = Running(enable: false);
        var factory = new RecordingFactory(transport);

        var exit = SendRun.Execute(
            Options(armed: true, Rig, Allowlist("ok.json"), Stamp), new SequenceLedger(), factory, new StringWriter(), new InstantClock());

        Assert.Equal(CmdInjectExit.EnableClear, exit);
        Assert.Empty(transport.Writes);
    }

    [Fact]
    public void ArmedWithTheEnableClearAndRaiseEnable_RaisesItAndPutsItBackDown()
    {
        var transport = Running(enable: false);
        var factory = new RecordingFactory(transport);
        var enableTag = Fixtures.DefaultResolved().BandRoles[InjectionRole.Enable];

        var exit = SendRun.Execute(
            Options(armed: true, Rig, Allowlist("ok.json"), Stamp) with { RaiseEnable = true, PollAttempts = 1, PollIntervalMs = 0 },
            new SequenceLedger(), factory, new StringWriter(), new InstantClock());

        Assert.Equal(CmdInjectExit.NotAcknowledged, exit);
        Assert.Equal(enableTag.Register, transport.Writes[0].StartRegister);
        Assert.Equal((ushort)(1 << enableTag.BitInRegister), transport.Writes[0].Values[0]);
        Assert.Equal(0, transport.Peek(enableTag.Register) & (1 << enableTag.BitInRegister));
    }

    [Fact]
    public void AFailedRestore_OutranksAnOtherwiseCleanRun()
    {
        var transport = Running();
        var factory = new RecordingFactory(transport);

        // The restore's own verifying re-read fails. A restore that cannot be VERIFIED is not a restore with
        // a caveat — the band may be holding values nobody chose, and this run cannot say it is not. The
        // command itself is irrelevant here: a run that cannot put the band back is a failed run whatever
        // else it achieved.
        transport.FailReads = (start, _) =>
            start == Fixtures.CommandBand.FirstRegister && transport.Writes.Count > 2
                ? new IOException("the verifying read did not come back")
                : null;

        var exit = SendRun.Execute(
            Options(armed: true, Rig, Allowlist("ok.json"), Stamp) with { PollAttempts = 1, PollIntervalMs = 0 },
            new SequenceLedger(), factory, new StringWriter(), new InstantClock());

        Assert.Equal(CmdInjectExit.RestoreFailed, exit);
    }

    [Fact]
    public void AConnectThatNeverHappened_IsNotADeviceThatRefused()
    {
        var factory = new RecordingFactory(new RecordingTransport()) { OpenThrows = new IOException("no route to host") };

        var exit = SendRun.Execute(
            Options(armed: true, Rig, Allowlist("ok.json"), Stamp), new SequenceLedger(), factory, new StringWriter(), new InstantClock());

        Assert.Equal(CmdInjectExit.ConnectFailed, exit);
    }
}
