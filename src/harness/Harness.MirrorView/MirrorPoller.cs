using System.Diagnostics;
using DeviceGuard;
using Harness.MirrorRead;

namespace Harness.MirrorView;

/// <summary>
/// The polling loop: fence, connect, the whole declared area in as many FC03s as the protocol needs,
/// publish.
///
/// <para><b>The fence sits above every line that can open a socket, on EVERY cycle.</b> The connection
/// factory is the only route to one, so "the fence refused and nothing connected" is asserted against a
/// counter in the tests rather than inferred from a status string.</para>
///
/// <para><b>The WHOLE AREA per cycle, in the fewest transactions that can carry it.</b> Not per-tag
/// reads: a screen assembled from thirty-seven separate reads is a picture of no single moment, and every
/// row would carry a different age while sharing one timestamp. 🔴 <b>Above 125 registers that ideal is
/// unreachable and this file used to pretend otherwise</b> — FC03 carries no more, so a 300-register area
/// is three transactions and a 1024-register one is nine. The reads are therefore <b>stamped per page</b>
/// and every row states the instant ITS OWN page returned; what the design refuses is a read per TAG,
/// which would be thirty-seven moments where the protocol only forces three.</para>
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
        // the switch below rendered as "the area is narrower than the map declares", i.e. it accused the
        // area pointer of a fault that is in the request. The read has been paged since ae11c3c; what
        // follows is what the paging left behind in the LABELS — the same residue 0c3b43a cleared out of
        // harness-mirror-read.
        //
        // ⚠️ `TransactionsPerPoll` is READ, not recomputed. It is derived in MirrorViewOptions because the
        // view model needs the same number to decide whether "every row shares one instant" is still a
        // true sentence, and a second derivation here would let the label and the page disagree about what
        // was issued.
        var pages = _options.TransactionsPerPoll;
        var pageSize = MirrorViewOptions.PageSize;

        // *** THE PAGE INSTANTS COME FROM THE TRANSACTIONS THEMSELVES, NOT FROM A PLANNED LAYOUT. ***
        // Wrapping the session is what makes the provenance a record of what happened rather than a second
        // copy of the paging arithmetic — if PerformPaged ever splits differently, this follows it.
        var timed = new PageTimingSource(_session, _clock);

        var clock = Stopwatch.StartNew();
        var read = RegisterRead.PerformPaged(timed, 0, _options.DeclaredRegisters, pageSize);
        clock.Stop();

        switch (read.Outcome)
        {
            case ReadOutcome.Ok:
                // THE LABEL NAMES THE TRANSACTIONS THAT WERE ACTUALLY ISSUED — three-way, the same shape
                // as MirrorReadRun.cs:225-230. A single page keeps its old words to the character, because
                // there the whole area really was one request and nothing about that sentence was wrong.
                return new PollAttempt(_clock(), PollOutcome.Ok,
                    pages == 1
                        ? $"FC03(0, {_options.DeclaredRegisters}) answered in {read.ElapsedMs} ms."
                        : $"{timed.Transactions} x FC03 -> 0..{_options.LastDeclaredRegister} answered in " +
                          $"{read.ElapsedMs} ms total. The declared area is wider than the {pageSize} " +
                          "registers FC03 carries, so this reading is a REASSEMBLY of separate moments on a " +
                          "live mirror and not one snapshot — each row states the instant its own page " +
                          "returned, and two rows from different pages were never read together.",
                    read.Values, read.ElapsedMs,
                    RegisterObservedUtc: pages == 1 ? null : timed.MaskOver(_options.DeclaredRegisters),
                    Transactions: timed.Transactions);

            case ReadOutcome.RefusedByServer:
                Drop();
                return new PollAttempt(_clock(), PollOutcome.RefusedByServer,
                    $"the server REFUSED {Where(read, pages, pageSize)} with Modbus exception code " +
                    $"{read.SlaveExceptionCode} ({RegisterRead.ExceptionCodeName(read.SlaveExceptionCode)}). " +
                    "It received the request and turned it down — " + Narrowing(read, pages),
                    Array.Empty<ushort>(), read.ElapsedMs, Transactions: timed.Transactions);

            default:
                Drop();
                return new PollAttempt(_clock(), PollOutcome.TransportFailed,
                    $"no answer to {Where(read, pages, pageSize)}: {read.Failure}. That is a failed " +
                    "measurement, not a refusal — nothing here is evidence about the device.",
                    Array.Empty<ushort>(), read.ElapsedMs, Transactions: timed.Transactions);
        }
    }

    /// <summary>
    /// 🔴 <b>THE TRANSACTION THAT FAILED, NAMED AS ITSELF.</b>
    ///
    /// <para><c>PerformPaged</c> returns the FAILING PAGE'S own <c>Start</c> and <c>Count</c>, and says why
    /// in its own words: reporting the whole span "would claim a boundary at an address that was never
    /// asked for". This viewer threw that away and reasserted <c>FC03(0, DeclaredRegisters)</c> — so a
    /// refusal at register 1000 of a 1,024-register area was printed as a refusal of a 1,024-register read
    /// from 0, a request the protocol cannot carry and this program never sent.</para>
    ///
    /// <para>Read from the record's structured <c>Start</c> and <c>Count</c> rather than from the failure
    /// text, which <c>DeviceFactsFrom</c> may truncate away.</para>
    /// </summary>
    private string Where(RegisterRead read, int pages, int pageSize) =>
        pages == 1
            ? $"FC03(0, {_options.DeclaredRegisters})"
            : $"FC03({read.Start}, {read.Count}), page {read.Start / pageSize + 1} of {pages} covering " +
              $"registers {read.Start}..{read.Start + read.Count - 1}";

    /// <summary>
    /// ⚠️ <b>THE INFERENCE ABOUT THE DEVICE, AND WHAT IT IS ENTITLED TO REST ON.</b> "The area is narrower
    /// than the map declares" was drawn from a page boundary the reader was never told about: on a
    /// 1,024-register area that sentence appeared under a label claiming the whole span had been asked for.
    ///
    /// <para>Paging is fail-fast, so a refusal at page <i>k</i> means every earlier page ANSWERED — a
    /// measured lower bound on the width, which the old text discarded in favour of an unquantified
    /// "narrower". The first page refusing has no earlier page to cite and must not invent one. And in
    /// every multi-page case the registers past the failing page were never asked about at all, so nothing
    /// here is a statement about them.</para>
    /// </summary>
    private string Narrowing(RegisterRead read, int pages)
    {
        if (pages == 1)
        {
            return "the area is narrower than the map declares, or the server is not the one the map " +
                   "describes.";
        }

        if (read.Start == 0)
        {
            return "no page answered before it, so nothing here bounds the area from below — either it is " +
                   "narrower than this first page, or the server is not the one the map describes. " +
                   $"Registers {read.Count}..{_options.LastDeclaredRegister} were never asked about: the " +
                   "read stops at the first page that fails.";
        }

        return $"registers 0..{read.Start - 1} answered before it, so the area is at least {read.Start} " +
               "register(s) wide and ends inside this page — or the server is not the one the map " +
               $"describes. Registers {read.Start + read.Count}..{_options.LastDeclaredRegister} were never " +
               "asked about: the read stops at the first page that fails, so no part of this is a " +
               "statement about them.";
    }

    /// <summary>
    /// 🔴 <b>WHEN EACH FC03 CAME BACK, RECORDED AT THE TRANSACTION RATHER THAN COMPUTED FROM A PLAN.</b>
    ///
    /// <para>The whole point of the change this class belongs to is that the viewer stopped describing
    /// requests it had not issued. Deriving the page instants from a second copy of the paging arithmetic
    /// would reintroduce exactly that — a table of moments for the transactions this file EXPECTED, which
    /// goes on looking plausible after the split changes underneath it. This wraps the session instead, so
    /// every instant is stamped on a read that really happened.</para>
    ///
    /// <para>⚠️ <b><see cref="Dispose"/> does NOT dispose the session.</b> The poller owns the socket and
    /// reuses it across polls; a decorator that closed it would turn one session into one per cycle. The
    /// paged read never disposes its source, so this is belt and braces rather than the live path.</para>
    /// </summary>
    private sealed class PageTimingSource : IRegisterSource
    {
        private readonly IRegisterSource _inner;
        private readonly Func<DateTimeOffset> _clock;
        private readonly List<(int Start, int Count, DateTimeOffset At)> _pages = new();

        internal PageTimingSource(IRegisterSource inner, Func<DateTimeOffset> clock)
        {
            _inner = inner;
            _clock = clock;
        }

        /// <summary>
        /// FC03s ISSUED this cycle, whatever became of them. <b>The denominator</b> — counted before the
        /// call, so a page that timed out is still a transaction this program sent.
        /// </summary>
        internal int Transactions { get; private set; }

        public ushort[] Read(int startRegister, int count)
        {
            Transactions++;
            var values = _inner.Read(startRegister, count);

            // Stamped AFTER the answer arrived: a row's age is measured from when its value was KNOWN,
            // not from when it was asked for.
            _pages.Add((startRegister, count, _clock()));
            return values;
        }

        /// <summary>
        /// One instant per register, from the page that carried it. A register no page answered stays null
        /// and the page prints it as NOT READ — never as the zero underneath.
        /// </summary>
        internal DateTimeOffset?[] MaskOver(int registers)
        {
            var mask = new DateTimeOffset?[Math.Max(0, registers)];

            foreach (var (start, count, at) in _pages)
            {
                for (var i = Math.Max(0, start); i < start + count && i < mask.Length; i++) mask[i] = at;
            }

            return mask;
        }

        public void Dispose() { }
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
