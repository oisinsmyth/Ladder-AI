namespace Harness.MirrorRead;

/// <summary>What one run is pointed at, and how wide the area under test is DECLARED to be.</summary>
/// <param name="Address">Modbus TCP host. Also the address the device fence is asked about.</param>
/// <param name="Port">TCP port. 503 on this rig — 502 is refused there.</param>
/// <param name="UnitId">Modbus unit identifier.</param>
/// <param name="AllowlistPath">
/// Resolved allowlist path, or null. Null is a REFUSAL, never a pass: with no allowlist there is
/// nothing that could have authorised the target, and an unauthorised read is not made safe by being
/// a read.
/// </param>
/// <param name="DeclaredRegisters">
/// How many holding registers the area pointer declares — <c>MB_HOLD_REG ... WORD n</c> means
/// registers <c>0 .. n-1</c>. This is the CLAIM under test, supplied by the caller from the IR rather
/// than compiled in, so the tool cannot quietly agree with itself about what the right answer is.
/// </param>
/// <param name="BoundaryFrom">First register of the single-register sweep across the declared edge.</param>
/// <param name="BoundaryTo">Last register of that sweep. Must reach past <c>DeclaredRegisters - 1</c>.</param>
/// <param name="IntervalMs">
/// Gap between the two control reads. The scan counter is what proves the copy layer is executing
/// rather than sitting in memory, and a difference is only evidence if time passed between the reads.
/// </param>
/// <param name="ScanRetries">
/// 🔴 <b>How many EXTRA whole measurements to take when the scan counter has not moved — and nothing
/// else.</b>
///
/// <para><b>Why it exists.</b> A download stops the CPU, so a run placed straight after one meets a
/// counter that has not started again yet: exit 8, which is a liveness fact and not a width verdict.
/// The wave already answers this exact transient by ASKING AGAIN rather than sleeping a guessed
/// duration — the blind 15 s wait it replaced was measured to be too short — and it reports how many
/// attempts it needed, which finally measures the settling instead of estimating it. This is the same
/// mechanism so that one transient is not described by two policies.</para>
///
/// <para>🔴 <b>ONLY exit 8 is retried.</b> A narrower or wider area is a CONCLUSION, and re-rolling a
/// conclusion until it changes turns a check into a random number generator. Zero by default: a tool
/// run by hand should report a stall on its first reading, not after a minute of quietly asking again.</para>
/// </param>
/// <param name="ScanRetryIntervalMs">Gap between those attempts. Ignored when <paramref name="ScanRetries"/> is zero.</param>
public sealed record MirrorReadOptions(
    string Address,
    int Port,
    byte UnitId,
    string? AllowlistPath,
    int DeclaredRegisters,
    int BoundaryFrom,
    int BoundaryTo,
    int IntervalMs,
    int ScanRetries = 0,
    int ScanRetryIntervalMs = 5000)
{
    /// <summary>Highest register the declared area contains.</summary>
    public int LastDeclaredRegister => DeclaredRegisters - 1;

    /// <summary>First register OUTSIDE the declared area — the one that must be refused.</summary>
    public int FirstUndeclaredRegister => DeclaredRegisters;

    /// <summary>Everything wrong with these options, or empty.</summary>
    public IReadOnlyList<string> Refusals
    {
        get
        {
            var refusals = new List<string>();

            if (string.IsNullOrWhiteSpace(Address))
                refusals.Add("no --address given.");

            if (Port is < 1 or > 65535)
                refusals.Add($"--port {Port} is not a TCP port.");

            if (DeclaredRegisters < 1)
            {
                refusals.Add($"--declared-registers {DeclaredRegisters} declares an empty area. " +
                             "An area with no registers has no boundary to measure.");
            }

            if (BoundaryFrom < 0)
                refusals.Add($"--boundary-from {BoundaryFrom} is not a register address.");

            if (BoundaryTo < BoundaryFrom)
                refusals.Add($"--boundary-to {BoundaryTo} is below --boundary-from {BoundaryFrom}.");

            // The sweep exists to straddle the declared edge. A sweep entirely inside it can only ever
            // report success, and a sweep entirely outside it has no control — both would run, print,
            // and prove nothing, which is the failure mode this project keeps meeting.
            if (DeclaredRegisters >= 1 && BoundaryFrom > LastDeclaredRegister)
            {
                refusals.Add($"--boundary-from {BoundaryFrom} is already outside the declared area " +
                             $"(0..{LastDeclaredRegister}), so the sweep contains no register that MUST " +
                             "succeed. Without one, a refusal at the far end is not evidence about the " +
                             "boundary — it is equally what a server refusing everything looks like.");
            }

            if (DeclaredRegisters >= 1 && BoundaryTo < FirstUndeclaredRegister)
            {
                refusals.Add($"--boundary-to {BoundaryTo} never reaches register {FirstUndeclaredRegister}, " +
                             "the first one outside the declared area. Without it the run cannot tell an " +
                             "area of exactly the declared width from a wider one.");
            }

            if (IntervalMs < 0)
                refusals.Add($"--interval-ms {IntervalMs} is negative.");

            if (ScanRetries < 0)
                refusals.Add($"--scan-retry {ScanRetries} is negative. Zero means one attempt and no retry.");

            if (ScanRetryIntervalMs < 0)
                refusals.Add($"--scan-retry-interval-ms {ScanRetryIntervalMs} is negative.");

            return refusals;
        }
    }
}
