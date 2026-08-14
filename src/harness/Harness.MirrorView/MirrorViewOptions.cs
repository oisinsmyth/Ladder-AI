namespace Harness.MirrorView;

/// <summary>
/// What one viewer is pointed at, and how long a reading may go on being called current.
/// </summary>
/// <param name="Address">Modbus TCP host — and the address the device fence is asked about.</param>
/// <param name="Port">TCP port. 503 on this rig; 502 is refused there.</param>
/// <param name="UnitId">Modbus unit identifier.</param>
/// <param name="AllowlistPath">
/// Resolved allowlist path, or null. <b>Null is a REFUSAL, never a pass</b> — with no allowlist there is
/// nothing that could have authorised the target, and an unauthorised read is not made safe by being a
/// read.
/// </param>
/// <param name="DeclaredRegisters">
/// Width of the mirrored area, from the <c>MB_HOLD_REG</c> pointer in the IR. Supplied by the map
/// loader, never compiled in.
/// </param>
/// <param name="PollIntervalMs">Gap between polls.</param>
/// <param name="StaleAfterMs">
/// How old the newest successful reading may be before the page declares itself STALE.
///
/// <para>Defaulted to three poll intervals rather than to a wall-clock constant: what makes a reading
/// stale is <b>missed polls</b>, and a fixed threshold means the same number is generous at a 200 ms
/// interval and impossible at a 10 s one.</para>
/// </param>
/// <param name="HttpPort">Loopback TCP port the page is served on.</param>
public sealed record MirrorViewOptions(
    string Address,
    int Port,
    byte UnitId,
    string? AllowlistPath,
    int DeclaredRegisters,
    int PollIntervalMs,
    int StaleAfterMs,
    int HttpPort)
{
    /// <summary>The default staleness threshold for a given interval: three missed polls.</summary>
    public static int DefaultStaleAfterMs(int pollIntervalMs) => Math.Max(1000, pollIntervalMs * 3);

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

            if (HttpPort is < 1 or > 65535)
                refusals.Add($"--http-port {HttpPort} is not a TCP port.");

            if (DeclaredRegisters < 1)
                refusals.Add($"the map declares {DeclaredRegisters} register(s) — there is nothing to show.");

            if (PollIntervalMs < 50)
            {
                refusals.Add($"--poll-ms {PollIntervalMs} is below 50 ms. The measured round trip to this rig " +
                             "is 63-106 ms, so a shorter interval cannot produce a reading per poll and would " +
                             "report staleness that is entirely the viewer's own.");
            }

            if (StaleAfterMs <= PollIntervalMs)
            {
                refusals.Add($"--stale-after-ms {StaleAfterMs} is at or below the {PollIntervalMs} ms poll " +
                             "interval, so a perfectly healthy reading would be declared stale before the next " +
                             "poll could possibly replace it. A gate that fires on working input is noise, and " +
                             "noise gets switched off.");
            }

            return refusals;
        }
    }
}
