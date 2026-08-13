namespace Harness.Wire;

/// <summary>
/// The three transport settings this design is not free to leave at their defaults, and why.
///
/// <para><b>1. RETRIES = 0, AND NModbus DEFAULTS IT TO 3.</b> §12a derivation 4 names the one thing
/// that must not be built: <i>a retry that is indistinguishable from a lost request</i>. If the client
/// retries after a timeout the original request may still land — on a write that is a duplicated vector
/// application, and <b>on the start-bool commit it is a SECOND T=0</b>, which silently moves the time
/// origin every stamp in the test is measured from. The spec requires the commit to be idempotent OR
/// non-retried, "decided at build time". <b>It is decided here, and it is non-retried</b>: idempotence
/// is an argument about level-driven coils, not a measurement, and the whole point of the 2,216 ms
/// outlier is that the case is live rather than theoretical.</para>
///
/// <para><b>2. TIMEOUTS AT 3,000 ms.</b> <c>RTT_max x ~1.35</c>. A per-request timeout under 2,216 ms
/// converts a measured, ordinary tail event into a transport failure at ~1 in 2,000 requests — an
/// intermittent unattributable red. A generous timeout costs no throughput: it only ever elapses on a
/// request that has already failed.</para>
///
/// <para><b>3. WAIT-TO-RETRY IS MOOT AND STILL STATED.</b> With retries off it can never elapse. It is
/// set anyway so that anyone who raises <see cref="Retries"/> in future has to look at this record and
/// read the paragraph above rather than discovering a default.</para>
/// </summary>
public sealed record ModbusPolicy(int Retries, int ReadTimeoutMs, int WriteTimeoutMs, int WaitToRetryMs)
{
    /// <summary>The policy every harness client uses. There is deliberately no second one.</summary>
    public static ModbusPolicy Default { get; } = new(
        Retries: 0,
        ReadTimeoutMs: WireTiming.PerRequestTimeoutMs,
        WriteTimeoutMs: WireTiming.PerRequestTimeoutMs,
        WaitToRetryMs: 0);

    /// <summary>Everything wrong with this policy, or empty.</summary>
    public IReadOnlyList<string> Refusals
    {
        get
        {
            var refusals = new List<string>();

            if (Retries != 0)
                refusals.Add($"retries is {Retries}, not 0. A retry after a timeout may duplicate a request that still landed — on the start-bool commit that is a SECOND T=0, which moves the origin every stamp is measured from (spec section 12a, derivation 4).");

            if (ReadTimeoutMs < WireTiming.RttMaxObservedMs)
                refusals.Add($"read timeout {ReadTimeoutMs} ms is below the {WireTiming.RttMaxObservedMs} ms round trip measured on this rig, so an ordinary tail event becomes a transport failure at ~1 request in 2,000.");

            if (WriteTimeoutMs < WireTiming.RttMaxObservedMs)
                refusals.Add($"write timeout {WriteTimeoutMs} ms is below the {WireTiming.RttMaxObservedMs} ms round trip measured on this rig, so an ordinary tail event becomes a transport failure at ~1 request in 2,000.");

            return refusals;
        }
    }
}
