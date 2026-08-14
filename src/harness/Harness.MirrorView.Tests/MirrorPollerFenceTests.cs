using Harness.MirrorRead;

namespace Harness.MirrorView.Tests;

/// <summary>
/// The fence, and the three ways a poll can fail to produce a reading.
///
/// <para>🔴 <b>EVERY REFUSAL TEST ASSERTS <c>Opens == 0</c>, NOT AN OUTCOME NAME.</b> The connection
/// factory is the only route to a socket, so the counter is the observable consequence — and it is what
/// survives somebody moving the fence below the connect, which a status string would not.</para>
///
/// <para><b>Both directions are tested.</b> A gate that refuses everything is as broken as one that
/// refuses nothing, and the second is the failure that gets the gate deleted. The allowing control below
/// is as deliberate as the refusals.</para>
/// </summary>
public class MirrorPollerFenceTests
{
    private static (MirrorPoller Poller, MirrorState State, RecordingFactory Factory) Rig(
        IDeviceFence fence, IRegisterSource source, TestClock? clock = null)
    {
        var state = new MirrorState();
        var factory = new RecordingFactory(() => source);
        var poller = new MirrorPoller(Fixtures.Options(), state, fence, factory, (clock ?? new TestClock(Fixtures.T0)).Read);
        return (poller, state, factory);
    }

    // ---- the fence refuses -------------------------------------------------------------------------

    [Fact]
    public void AFenceThatRefuses_OpensNoSocketAtAll()
    {
        var source = new ScriptedSource { Standing = ScriptedSource.Area(Fixtures.Registers) };
        var (poller, _, factory) = Rig(new RefusingFence(), source);
        using var _p = poller;

        poller.PollOnce();

        Assert.Equal(0, factory.Opens);
        Assert.Equal(0, source.Reads);
    }

    [Fact]
    public void AFenceThatRefuses_IsReportedAsARefusal_NotAsANetworkFault()
    {
        var (poller, state, _) = Rig(new RefusingFence(), new ScriptedSource());
        using var _p = poller;

        poller.PollOnce();
        var model = MirrorView.Build(Fixtures.Map(), Fixtures.Options(), state, Fixtures.T0);

        Assert.Equal(MirrorStatus.Refused, model.Status);
        Assert.False(model.ValuesAreCurrent);
        Assert.Contains("no socket was opened", model.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not a network fault", model.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// *** THE MUTATION THAT MATTERS MOST: A FENCE THAT STARTS REFUSING MID-RUN MUST TAKE THE PAGE
    /// RED, AND DROP THE SESSION. *** Authorisation checked once at startup is authorisation that
    /// cannot be withdrawn while the bytes keep arriving.
    /// </summary>
    [Fact]
    public void AFenceThatRefusesAfterAGoodPoll_TakesThePageOffLive_AndClosesTheSession()
    {
        var source = new ScriptedSource { Standing = ScriptedSource.Area(Fixtures.Registers) };
        var state = new MirrorState();
        var fence = new SwitchableFence { Allow = true };
        var factory = new RecordingFactory(() => source);
        using var poller = new MirrorPoller(Fixtures.Options(), state, fence, factory, () => Fixtures.T0);

        poller.PollOnce();
        Assert.Equal(MirrorStatus.Live, MirrorView.Build(Fixtures.Map(), Fixtures.Options(), state, Fixtures.T0).Status);

        fence.Allow = false;
        poller.PollOnce();

        var model = MirrorView.Build(Fixtures.Map(), Fixtures.Options(), state, Fixtures.T0);
        Assert.Equal(MirrorStatus.Refused, model.Status);
        Assert.True(source.Disposed, "the session was left open after the fence withdrew authorisation.");
        Assert.Equal(1, factory.Opens);
    }

    // ---- the fence faults --------------------------------------------------------------------------

    /// <summary>
    /// A fault in the fence is not a refusal and not a reading. Nothing examined the target, and the
    /// page must say that rather than picking whichever of the two it resembles.
    /// </summary>
    [Fact]
    public void AFenceThatThrows_IsItsOwnState_AndStillOpensNoSocket()
    {
        var (poller, state, factory) = Rig(new ThrowingFence(), new ScriptedSource());
        using var _p = poller;

        poller.PollOnce();
        var model = MirrorView.Build(Fixtures.Map(), Fixtures.Options(), state, Fixtures.T0);

        Assert.Equal(0, factory.Opens);
        Assert.Equal(MirrorStatus.FenceFault, model.Status);
        Assert.Contains("neither a refusal nor a reading", model.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    // ---- the fence allows: the control, and the three wire outcomes --------------------------------

    /// <summary>
    /// *** THE ANTI-OVER-FIRE CONTROL. *** An allowed target must reach the socket and go green. A gate
    /// that refused every ordinary run would be removed within a week, by someone who was right to.
    /// </summary>
    [Fact]
    public void AnAllowedTarget_ConnectsAndReads()
    {
        var source = new ScriptedSource { Standing = ScriptedSource.Area(Fixtures.Registers) };
        var (poller, state, factory) = Rig(new AllowingFence(), source);
        using var _p = poller;

        var attempt = poller.PollOnce();

        Assert.Equal(1, factory.Opens);
        Assert.Equal(1, source.Reads);
        Assert.Equal(PollOutcome.Ok, attempt.Outcome);
        Assert.Equal(MirrorStatus.Live,
            MirrorView.Build(Fixtures.Map(), Fixtures.Options(), state, Fixtures.T0).Status);
    }

    /// <summary>One session serves many polls — a reconnect per poll would be a second, uncalibrated cost.</summary>
    [Fact]
    public void ASucceedingSession_IsReusedAcrossPolls()
    {
        var source = new ScriptedSource { Standing = ScriptedSource.Area(Fixtures.Registers) };
        var (poller, _, factory) = Rig(new AllowingFence(), source);
        using var _p = poller;

        poller.PollOnce();
        poller.PollOnce();
        poller.PollOnce();

        Assert.Equal(1, factory.Opens);
        Assert.Equal(3, source.Reads);
    }

    /// <summary>
    /// *** A SILENCE IS NOT A REFUSAL. *** A timeout is a failed measurement; it says nothing about the
    /// device's address space, and it must not be reported in the same words as a server that answered.
    /// </summary>
    [Fact]
    public void ATimeout_IsTransportFailed_NotARefusal()
    {
        var (poller, state, _) = Rig(new AllowingFence(), new SilentSource());
        using var _p = poller;

        var attempt = poller.PollOnce();

        Assert.Equal(PollOutcome.TransportFailed, attempt.Outcome);
        Assert.Contains("not a refusal", attempt.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(MirrorStatus.Failing, MirrorView.Build(Fixtures.Map(), Fixtures.Options(), state, Fixtures.T0).Status);
    }

    /// <summary>A Modbus exception response is the server ANSWERING — a different fact from silence.</summary>
    [Fact]
    public void AModbusExceptionResponse_IsRefusedByServer_AndNotConfusedWithTheFence()
    {
        var (poller, state, factory) = Rig(new AllowingFence(), new ServerRefusingSource());
        using var _p = poller;

        var attempt = poller.PollOnce();
        var model = MirrorView.Build(Fixtures.Map(), Fixtures.Options(), state, Fixtures.T0);

        Assert.Equal(1, factory.Opens);
        Assert.Equal(PollOutcome.RefusedByServer, attempt.Outcome);
        Assert.Equal(MirrorStatus.Failing, model.Status);
        Assert.NotEqual(MirrorStatus.Refused, model.Status);
    }

    /// <summary>A failed read discards the transport: a half-dead socket answering stale bytes is the enemy.</summary>
    [Fact]
    public void AFailedRead_DropsTheSession_SoTheNextPollReconnects()
    {
        var sources = new Queue<IRegisterSource>();
        sources.Enqueue(new SilentSource());
        sources.Enqueue(new ScriptedSource { Standing = ScriptedSource.Area(Fixtures.Registers) });

        var factory = new RecordingFactory(() => sources.Dequeue());
        using var poller = new MirrorPoller(Fixtures.Options(), new MirrorState(), new AllowingFence(), factory, () => Fixtures.T0);

        poller.PollOnce();
        poller.PollOnce();

        Assert.Equal(2, factory.Opens);
    }

    [Fact]
    public void AConnectThatFails_IsItsOwnOutcome_AndIsRetried()
    {
        var attempts = 0;
        var factory = new RecordingFactory(() =>
        {
            attempts++;
            throw new IOException("no route to host.");
        });

        using var poller = new MirrorPoller(Fixtures.Options(), new MirrorState(), new AllowingFence(), factory, () => Fixtures.T0);

        Assert.Equal(PollOutcome.ConnectFailed, poller.PollOnce().Outcome);
        Assert.Equal(PollOutcome.ConnectFailed, poller.PollOnce().Outcome);
        Assert.Equal(2, attempts);
    }

    private sealed class SwitchableFence : IDeviceFence
    {
        public bool Allow { get; set; }

        public DeviceGuard.GuardDecision Check(string address) =>
            Allow ? new AllowingFence().Check(address) : new RefusingFence().Check(address);
    }
}
