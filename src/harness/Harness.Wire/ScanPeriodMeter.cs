using Harness.Map;

namespace Harness.Wire;

/// <summary>
/// 🔴 <b>What this run's program actually scanned at — measured, stamped, and NOT substituted for the
/// compiled constant.</b>
/// </summary>
/// <param name="MillisecondsPerScan">Wall time divided by scans advanced, over the whole window.</param>
/// <param name="Scans">
/// The denominator. <b>Printed always</b> — a rate over three scans and a rate over twelve thousand are
/// not the same claim, and only this number distinguishes them.
/// </param>
/// <param name="Window">Wall time the sample spans.</param>
/// <param name="BuildStamp">
/// 🔴 <b>WHICH PROGRAM THIS BELONGS TO, travelling inseparably with the number.</b> The scan period is a
/// property of the PROGRAM, not the controller, and this repository has twice recorded one program's
/// figure as a rig fact — once wrong by an order of magnitude. A measurement without its subject is the
/// next instance of that, pre-packaged.
/// </param>
/// <param name="Precondition">
/// What the system was doing while this was taken. A number is only as good as the condition it was taken
/// in: a stationarity claim over 113 frames and 252 seconds was once beautifully stable and wrong, because
/// the sweep measured residue rather than rest. <b>More N cannot fix a measurement taken in the wrong
/// condition</b>, so the condition travels with it.
/// </param>
public sealed record ScanPeriodMeasurement(
    double MillisecondsPerScan,
    long Scans,
    TimeSpan Window,
    uint BuildStamp,
    string Precondition)
{
    /// <summary>How far this run's program is from the compiled constant, as a signed fraction.</summary>
    public double DeltaFraction => (MillisecondsPerScan - WireTiming.ScanPeriodMs) / WireTiming.ScanPeriodMs;

    /// <summary>
    /// Whether the gap is big enough to say out loud. <b>5% is chosen, not derived</b> — the one recorded
    /// error that mattered was 7% low and reached every budget in the harness, so the threshold sits below
    /// it deliberately.
    /// </summary>
    public bool DisagreesWithConstant => Math.Abs(DeltaFraction) > 0.05;

    public override string ToString() =>
        $"scan period: {MillisecondsPerScan:0.000} ms over {Scans} scan(s) / {Window.TotalSeconds:0.#} s, "
        + $"build stamp 16#{BuildStamp:X8} (compiled constant {WireTiming.ScanPeriodMs:0.000} ms, "
        + $"delta {DeltaFraction * 100:+0.0;-0.0;0.0}%){(DisagreesWithConstant ? " *** DISAGREES ***" : string.Empty)}";
}

/// <summary>
/// 🔴 <b>Measures the scan period from traffic the wave already pays for.</b>
///
/// <para><c>WireTiming.ScanPeriodMs</c> is a compiled constant — <b>24.931 ms, one program's number</b>,
/// measured 2026-08-18 — and thirty-odd call sites convert scans to milliseconds with it: the timeout
/// backstop, the timer floor, the observability floor, every scan budget. Its own doc comment says
/// <i>"IT IS A PROPERTY OF THE PROGRAM, NOT OF THE CONTROLLER… re-measure whenever the program changes
/// materially"</i>. <b>That is a prose warning where §3.1 asks for a computed check, and it is enforced by
/// nothing.</b></para>
///
/// <para>🔴 <b>PHASE 4 IS WHAT MAKES IT BITE.</b> Its whole outcome is that a lane becomes something the
/// tool MAKES — and a new lane is a new program, with a different scan period, inheriting one program's
/// constant silently. It is a closed check in the strict sense: the backstop examines something real (the
/// vector's declared scans) and converts it with a number belonging to a different program. Never empty,
/// never silent, and healthiest-looking exactly when it is wrong.</para>
///
/// <para><b>Nothing new is polled.</b> Every control read already carries the mirror's own scan counter,
/// and the client already has an injectable clock. Two readings and a subtraction give the rate; the
/// measurement is a byproduct of traffic the wave was going to pay for anyway.</para>
///
/// <para>⚠️ <b>IT REPORTS. IT DOES NOT REPLACE THE CONSTANT, AND THAT IS DELIBERATE.</b> A measured value
/// silently overriding the constant would be a WORSE failure than the constant, because it would look
/// measured while being just as capable of having been taken in the wrong condition. Replacing it stays a
/// human act — now an informed one, because a record exists.</para>
/// </summary>
public sealed class ScanPeriodMeter
{
    /// <summary>
    /// Below this the sample is refused. <b>One sample is not a rate</b>, and a handful of scans across a
    /// few hundred milliseconds is dominated by where in the cycle each read landed.
    /// </summary>
    public const long MinimumScans = 20;

    /// <summary>Same reasoning on the other axis: a rate needs a window, not two adjacent instants.</summary>
    public static readonly TimeSpan MinimumWindow = TimeSpan.FromSeconds(2);

    private readonly uint _buildStamp;
    private readonly string _precondition;

    private ScanCount _firstCount;
    private DateTimeOffset _firstAt;
    private ScanCount _lastCount;
    private DateTimeOffset _lastAt;
    private bool _started;

    public ScanPeriodMeter(uint buildStamp, string precondition)
    {
        _buildStamp = buildStamp;
        _precondition = string.IsNullOrWhiteSpace(precondition)
            ? throw new ArgumentException("a measurement without its condition is the failure this field exists to prevent.", nameof(precondition))
            : precondition;
    }

    /// <summary>
    /// Offer one observation. <b>Non-advancing and implausible samples are ignored, not recorded</b>: a
    /// static counter means the program is not scanning, and folding that into a rate would silently
    /// inflate the period rather than reporting a stopped PLC.
    /// </summary>
    public void Observe(ScanCount count, DateTimeOffset at)
    {
        if (!_started)
        {
            _firstCount = _lastCount = count;
            _firstAt = _lastAt = at;
            _started = true;
            return;
        }

        // Wrap-safe by ScanCount.Since; a wrap is a legitimate advance and an implausible jump is not.
        if (!count.IsPlausibleAdvanceFrom(_lastCount) || at < _lastAt)
            return;

        _lastCount = count;
        _lastAt = at;
    }

    /// <summary>
    /// The measurement, or <b>null meaning NOT MEASURED — never a value that happens to be wrong.</b>
    /// A window too short or too few scans is not a weak rate, it is not a rate. Empty is not clean.
    /// </summary>
    public ScanPeriodMeasurement? Result()
    {
        if (!_started)
            return null;

        var scans = _lastCount.Since(_firstCount);
        var window = _lastAt - _firstAt;

        if (scans < MinimumScans || window < MinimumWindow)
            return null;

        return new ScanPeriodMeasurement(
            window.TotalMilliseconds / scans, scans, window, _buildStamp, _precondition);
    }

    /// <summary>
    /// Why there is no measurement, when there is none. <b>A caller must be able to tell "the plant was
    /// not scanning" from "the window was too short" from "nobody asked"</b>, and a bare null tells them
    /// none of the three.
    /// </summary>
    public string NotMeasuredBecause()
    {
        if (!_started)
            return "NOT MEASURED: no control read was observed, so the scan counter was never sampled.";

        var scans = _lastCount.Since(_firstCount);
        var window = _lastAt - _firstAt;

        if (scans < MinimumScans)
        {
            return $"NOT MEASURED: the counter advanced {scans} scan(s), below the {MinimumScans} needed for a rate. "
                 + (scans == 0
                     ? "It did not advance at all — that is a STOPPED or STALLED program, not a slow one, and it is "
                       + "reported here rather than folded into a period."
                     : "A handful of scans is dominated by where in the cycle each read landed.");
        }

        return $"NOT MEASURED: the sample spans {window.TotalSeconds:0.#} s, below the "
             + $"{MinimumWindow.TotalSeconds:0.#} s needed. Two adjacent instants are not a rate.";
    }
}
