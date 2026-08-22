using System.Diagnostics;
using DeviceGuard;
using Harness.MirrorRead;

namespace Harness.MirrorView;

/// <summary>
/// The polling loop: fence, connect, one FC03 over the whole declared area, publish.
///
/// <para><b>The fence sits above every line that can open a socket, on EVERY cycle.</b> The connection
/// factory is the only route to one, so "the fence refused and nothing connected" is asserted against a
/// counter in the tests rather than inferred from a status string.</para>
///
/// <para><b>One transaction per cycle, over the whole area.</b> Not per-tag reads: a screen assembled
/// from thirty-seven reads taken over a second is a picture of no single moment, and every row would
/// carry a different age while sharing one timestamp.</para>
///
/// <para><b>The session is reopened after any failure.</b> A half-dead socket that keeps answering
/// stale bytes is the failure this whole view exists to make visible, so the transport is discarded
/// rather than nursed.</para>
/// </summary>
public sealed class MirrorPoller : IDisposable
{
    private readonly MirrorViewOptions _options;
    private readonly IDeviceFence _fence;
    private readonly IRegisterSourceFactory _factory;
    private readonly Func<DateTimeOffset> _clock;
    private readonly MirrorState _state;

    private IRegisterSource? _session;
    private CancellationTokenSource? _stopping;
    private Task? _loop;

    public MirrorPoller(
        MirrorViewOptions options,
        MirrorState state,
        IDeviceFence fence,
        IRegisterSourceFactory factory,
        Func<DateTimeOffset> clock)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _fence = fence ?? throw new ArgumentNullException(nameof(fence));
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>Run one cycle and publish the result. Public because it is the whole unit under test.</summary>
    public PollAttempt PollOnce()
    {
        var attempt = Attempt();
        _state.Publish(attempt);
        return attempt;
    }

    private PollAttempt Attempt()
    {
        // ---- 1. THE FENCE. Above every line below that could reach a socket. ----
        GuardDecision decision;
        try
        {
            decision = _fence.Check(_options.Address);
        }
        catch (Exception ex)
        {
            Drop();
            return new PollAttempt(_clock(), PollOutcome.FenceFault,
                $"the device fence could not reach a decision ({ex.GetType().Name}: {ex.Message}). " +
                "Nothing examined the target, so this is a fault in the fence and NOT a verdict about the device.",
                Array.Empty<ushort>(), 0);
        }

        if (!decision.Allowed)
        {
            // A refusal closes any session already open. Leaving one alive would mean authorisation
            // could be withdrawn while the bytes kept arriving.
            Drop();
            return new PollAttempt(_clock(), PollOutcome.RefusedByFence,
                $"{decision.Reason}: {decision.Message} No connection was attempted.",
                Array.Empty<ushort>(), 0);
        }

        // ---- 2. CONNECT (only ever reached because the fence said ALLOWED) ----
        if (_session is null)
        {
            try
            {
                _session = _factory.Open(_options.Address, _options.Port, _options.UnitId);
            }
            catch (Exception ex)
            {
                _session = null;
                return new PollAttempt(_clock(), PollOutcome.ConnectFailed,
                    $"could not open {_options.Address}:{_options.Port} unit {_options.UnitId} — " +
                    $"{ex.GetType().Name}: {ex.Message}",
                    Array.Empty<ushort>(), 0);
            }
        }

        // ---- 3. THE WHOLE DECLARED AREA, IN AS MANY FC03s AS THE PROTOCOL NEEDS ----
        //
        // 🔴 This said "ONE FC03" and issued exactly that, over a width nothing bounded. FC03 carries at
        // most 125 registers and this file did not mention the limit; it worked only because the live run
        // declared 37. At the rig's real 576 the read is 4.6x the ceiling and comes back refused — which
        // the switch below renders as "the area is narrower than the map declares", i.e. it accuses the
        // area pointer of a fault that is in the request.
        var clock = Stopwatch.StartNew();
        var read = RegisterRead.PerformPaged(_session, 0, _options.DeclaredRegisters);
        clock.Stop();

        switch (read.Outcome)
        {
            case ReadOutcome.Ok:
                return new PollAttempt(_clock(), PollOutcome.Ok,
                    $"FC03(0, {_options.DeclaredRegisters}) answered in {read.ElapsedMs} ms.",
                    read.Values, read.ElapsedMs);

            case ReadOutcome.RefusedByServer:
                Drop();
                return new PollAttempt(_clock(), PollOutcome.RefusedByServer,
                    $"the server REFUSED FC03(0, {_options.DeclaredRegisters}) with Modbus exception code " +
                    $"{read.SlaveExceptionCode} ({RegisterRead.ExceptionCodeName(read.SlaveExceptionCode)}). " +
                    "It received the request and turned it down — the area is narrower than the map declares, " +
                    "or the server is not the one the map describes.",
                    Array.Empty<ushort>(), read.ElapsedMs);

            default:
                Drop();
                return new PollAttempt(_clock(), PollOutcome.TransportFailed,
                    $"no answer from the server: {read.Failure}. That is a failed measurement, not a refusal — " +
                    "nothing here is evidence about the device.",
                    Array.Empty<ushort>(), read.ElapsedMs);
        }
    }

    private void Drop()
    {
        try
        {
            _session?.Dispose();
        }
        catch (Exception)
        {
            // A transport that throws on the way out has nothing left to tell us.
        }

        _session = null;
    }

    /// <summary>Start the background loop. Returns immediately.</summary>
    public void Start()
    {
        if (_loop is not null) throw new InvalidOperationException("already started.");

        _stopping = new CancellationTokenSource();
        var token = _stopping.Token;

        _loop = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    PollOnce();
                }
                catch (Exception ex)
                {
                    // A crash is loud without being NAMED. The loop must not die silently, and the page
                    // must not keep showing the last good numbers as though a poller were still running.
                    _state.Publish(new PollAttempt(_clock(), PollOutcome.TransportFailed,
                        $"the poll loop threw ({ex.GetType().Name}: {ex.Message}). This is a defect in the " +
                        "viewer, not a reading of the device.", Array.Empty<ushort>(), 0));
                }

                try
                {
                    await Task.Delay(_options.PollIntervalMs, token).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    return;
                }
            }
        }, token);
    }

    public void Dispose()
    {
        _stopping?.Cancel();

        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(5));
        }
        catch (Exception)
        {
            // Shutting down; a loop that will not stop is not worth throwing over.
        }

        _stopping?.Dispose();
        Drop();
    }
}
