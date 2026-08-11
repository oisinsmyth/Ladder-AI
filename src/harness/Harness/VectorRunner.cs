using System.Globalization;

namespace Harness;

/// <summary>
/// Executes declarative vectors against an <see cref="ITransport"/> and reports pass/fail.
///
/// Two things make this more than a generic test loop, and both exist to stop a green result that
/// means nothing:
///
/// <para><b>1. It refuses what it cannot observe.</b> Before running a vector it checks that the
/// transport (and therefore the program under test) can actually support the vector's observability
/// class. A coincidence assertion with no event scan-stamps, a transient with no latching, a scan
/// wait with no scan counter — each is reported <see cref="VectorOutcome.NotObservable"/> and makes
/// the run not-green. The alternative is a vector that passes because nothing was looked at.</para>
///
/// <para><b>2. It waits by observing scans, not by sleeping.</b> An S7-1200 cannot be stepped, and
/// the host is not a real-time system, so a wall-clock delay is not a scan count. The runner reads
/// the program's free-running scan counter and waits for it to advance — a closed loop rather than
/// a guess.</para>
/// </summary>
public sealed class VectorRunner
{
    private readonly ITransport _transport;
    private readonly TargetEnvironment _environment;
    private readonly int _maxPollsPerScanWait;

    /// <param name="environment">Where this run is happening. Vectors scoped elsewhere are skipped.</param>
    /// <param name="maxPollsPerScanWait">
    /// Bound on how many scan-counter reads to make while waiting, so a stalled PLC ends the run
    /// rather than hanging it. Errored, never silently passed.
    /// </param>
    public VectorRunner(ITransport transport, TargetEnvironment environment, int maxPollsPerScanWait = 10_000)
    {
        _transport = transport;
        _environment = environment;
        _maxPollsPerScanWait = maxPollsPerScanWait;
    }

    public RunReport RunAll(IEnumerable<TestVector> vectors) =>
        new((vectors ?? Array.Empty<TestVector>()).Select(Run).ToArray());

    public RunResult Run(TestVector vector)
    {
        if (!IsForThisEnvironment(vector))
            return new RunResult(vector, VectorOutcome.Skipped,
                $"scoped to {vector.Environment}, this run is {_environment}.");

        var missing = MissingCapabilities(vector);
        if (missing != TransportCapabilities.None)
            return new RunResult(vector, VectorOutcome.NotObservable,
                $"cannot be evaluated here: needs {Describe(missing)}, which this transport does not " +
                $"provide. {WhyItMatters(vector.Observability)} This is NOT a pass.");

        var steps = new List<StepResult>();

        try
        {
            for (var i = 0; i < vector.Sequence.Count; i++)
            {
                var step = vector.Sequence[i];

                foreach (var w in step.Writes)
                    _transport.Write(w.Area, w.Tag, w.Value);

                var waited = WaitScans(step.WaitScans);

                var assertions = step.Assertions
                    .Select(e => Evaluate(e))
                    .ToArray();

                steps.Add(new StepResult(i, step.Note, waited, assertions));
            }
        }
        catch (TimeoutException tex)
        {
            return new RunResult(vector, VectorOutcome.Errored, tex.Message, steps);
        }
        catch (Exception ex)
        {
            return new RunResult(vector, VectorOutcome.Errored,
                $"{ex.GetType().Name}: {ex.Message}", steps);
        }

        var failures = steps.SelectMany(s => s.Assertions).Count(a => !a.Held);

        return failures == 0
            ? new RunResult(vector, VectorOutcome.Passed, "all assertions held.", steps)
            : new RunResult(vector, VectorOutcome.Failed,
                $"{failures} assertion(s) did not hold.", steps);
    }

    private bool IsForThisEnvironment(TestVector v) =>
        v.Environment == TargetEnvironment.Both
        || _environment == TargetEnvironment.Both
        || v.Environment == _environment;

    /// <summary>What this vector needs that the transport does not have.</summary>
    private TransportCapabilities MissingCapabilities(TestVector v)
    {
        var needed = TransportCapabilities.None;

        if (!v.IsReadOnly)
            needed |= TransportCapabilities.Write;

        if (v.Sequence.Any(s => s.WaitScans > 0))
            needed |= TransportCapabilities.ScanCounter;

        needed |= v.Observability switch
        {
            Observability.Coincidence => TransportCapabilities.EventScanStamps,
            Observability.Transient => TransportCapabilities.LatchedTransients,
            _ => TransportCapabilities.None,
        };

        return needed & ~_transport.Capabilities;
    }

    private long WaitScans(int scans)
    {
        if (scans <= 0) return 0;

        var start = _transport.ReadScanCounter();
        for (var poll = 0; poll < _maxPollsPerScanWait; poll++)
        {
            var elapsed = _transport.ReadScanCounter() - start;
            if (elapsed >= scans) return elapsed;
        }

        throw new TimeoutException(
            $"waited {_maxPollsPerScanWait} polls for {scans} scan(s) and the scan counter did not " +
            "advance far enough — the PLC may be stopped, or the counter is not running.");
    }

    private AssertionResult Evaluate(Expectation e)
    {
        var actual = _transport.Read(e.Tag);

        if (e.Tolerance is { } tol)
        {
            var okExpected = double.TryParse(e.Expected, NumberStyles.Float, CultureInfo.InvariantCulture, out var exp);
            var okActual = double.TryParse(actual, NumberStyles.Float, CultureInfo.InvariantCulture, out var act);

            if (!okExpected || !okActual)
                return new AssertionResult(e.Tag, e.Expected, actual, false,
                    "a tolerance was given but the values are not both numeric.");

            var delta = Math.Abs(exp - act);
            return new AssertionResult(e.Tag, e.Expected, actual, delta <= tol,
                $"|{exp} - {act}| = {delta}, tolerance {tol}");
        }

        var held = string.Equals(e.Expected?.Trim(), actual?.Trim(), StringComparison.OrdinalIgnoreCase);
        return new AssertionResult(e.Tag, e.Expected ?? string.Empty, actual ?? string.Empty, held);
    }

    private static string Describe(TransportCapabilities c) =>
        string.Join(" + ", Enum.GetValues<TransportCapabilities>()
            .Where(f => f != TransportCapabilities.None && c.HasFlag(f))
            .Select(f => f.ToString()));

    private static string WhyItMatters(Observability o) => o switch
    {
        Observability.Coincidence =>
            "A same-scan coincidence cannot be seen by sampling at any rate; the program must record " +
            "the scan number of each event so the assertion becomes a comparison of two integers.",
        Observability.Transient =>
            "A one-scan pulse is far below the observation floor of a polled transport; the program " +
            "must latch it so it survives to be read.",
        _ =>
            "A step that waits N scans needs a free-running scan counter in the program — a wall-clock " +
            "delay on a non-real-time host is not a scan count.",
    };
}
