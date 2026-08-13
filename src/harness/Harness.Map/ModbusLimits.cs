namespace Harness.Map;

/// <summary>
/// The two protocol limits the map is derived against, and the measured fact that decides how the
/// map should USE them.
///
/// <para><b>The limits.</b> FC03 (read holding registers) carries at most 125 registers per
/// transaction; FC16 (write multiple registers) at most 123. They are not the same number and the
/// difference is load-bearing here: a slot's RESULT region is read, so 125 bounds it; the start-bool
/// commit is written, so 123 bounds that.</para>
///
/// <para><b>The measurement, and it points the opposite way from the spec's original arithmetic.</b>
/// Phase 1.2 timed 1, 4, 8 and 16 registers per round trip and they all cost the same within noise —
/// median 71-78 ms, p99 136-173 ms, independent of width. <b>The marginal cost of a register is
/// indistinguishable from zero; what costs is the number of round trips.</b> D29 bounds tensor width
/// by poll bandwidth on the assumption that registers cost, and that assumption is measured false up
/// to these two ceilings. Re-deriving D29 is not this component's job — but not baking its premise in
/// IS, so nothing here ever narrows a region to save registers, and the cost this component reports is
/// counted in ROUND TRIPS (see <c>RegisterMap.PollRoundTrips</c>), never in registers.</para>
///
/// <para>Measured over the tunnel rather than a LAN, which is the deployment that matters, and the
/// figure would only get flatter on a LAN.</para>
/// </summary>
public static class ModbusLimits
{
    /// <summary>Maximum registers one FC03 (read holding registers) transaction may carry.</summary>
    public const int MaxReadRegisters = 125;

    /// <summary>Maximum registers one FC16 (write multiple registers) transaction may carry.</summary>
    public const int MaxWriteRegisters = 123;

    /// <summary>Transactions needed to write <paramref name="registers"/> registers.</summary>
    public static int WriteTransactions(int registers) =>
        registers <= 0 ? 0 : (registers + MaxWriteRegisters - 1) / MaxWriteRegisters;
}
