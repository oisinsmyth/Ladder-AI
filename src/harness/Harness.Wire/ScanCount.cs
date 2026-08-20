using System.Globalization;

namespace Harness.Wire;

/// <summary>
/// A reading of the free-running scan counter, and <b>the only way to take a difference between two.</b>
///
/// <para>🔴 <b>WHY THIS IS A TYPE AND NOT A <c>long</c>.</b> The wrap WAS absorbed —
/// <c>S7Transport.ReadScanCounter</c> counts wraps, returns monotonically, and has a passing test.
/// <b>But S7 variable access is refused CPU-wide on this rig, so every data read goes over Modbus</b>,
/// and the live path (<c>MirrorClient.ReadControlUnverified</c>) decoded the register pair into a
/// <c>long</c> and absorbed nothing. At the <c>DInt</c> boundary <c>now - from</c> was
/// <b>−4 294 967 295</b>: <c>LoopRun.Settling</c> reported <c>NotEstablished</c>, <c>InertPhase</c>
/// reported <c>ScanCounterStalled</c> — both failing closed <i>with the wrong diagnosis</i> — and
/// <c>SlotRun</c>/<c>WaveRun</c> printed it as "after −4294967295 scan(s)" on a COMPLETED slot.</para>
///
/// <para>*** THE FINDING WAS NEVER THE WRAP. IT WAS A GUARD WRITTEN, TESTED, AND LIVING ON A PATH THAT
/// CANNOT RUN *** — found by asking WHICH TRANSPORT ACTUALLY CARRIES THIS. So the fix is not a second
/// wrap counter beside the first: it is making the wrong subtraction <b>unexpressible</b>. This struct
/// defines no <c>operator -</c> and no ordering operators, so <c>a - b</c> and <c>a &gt; b</c> do not
/// compile and every difference goes through <see cref="Since"/>. A future site cannot quietly
/// re-introduce the bug, which is the same shape as <c>MirrorWriteTarget</c> making a result-register
/// write unaddressable rather than refused.</para>
///
/// <para><b>The counter is read as UNSIGNED and the difference is modular</b>, which is the standard
/// tick-counter idiom: <c>unchecked(now - then)</c> in 32-bit unsigned arithmetic is correct ACROSS the
/// wrap with no state at all. The old code sign-extended the same 32 bits into a <c>long</c>, which is
/// what turned a one-scan advance into a nine-figure negative.</para>
///
/// <para>⚠️ <b>Urgency, stated so nobody rushes it:</b> a <c>DInt</c> at the measured 24.931 ms/scan
/// wraps after <b>~620 days</b> of continuous RUN (it read ~580 days while the scan constant was 23.33,
/// i.e. 7% low). This was worth fixing properly, not quickly.</para>
/// </summary>
public readonly record struct ScanCount
{
    public ScanCount(uint raw) => Raw = raw;

    /// <summary>The 32 bits as published, unsigned. <b>Never sign-extended.</b></summary>
    public uint Raw { get; }

    /// <summary>
    /// The largest forward advance that will be read AS an advance.
    ///
    /// <para>Half the span, which is the tick-comparison idiom's own bound: within it the modular
    /// difference is unambiguous, and beyond it a "huge advance" is far more likely a counter that went
    /// BACKWARDS — a CPU restart, a reload, or a different program. <b>Absorbing that as an advance would
    /// manufacture liveness</b>, which is the one direction this project never fails in.</para>
    /// </summary>
    public const long PlausibleAdvanceCeiling = int.MaxValue;

    /// <summary>
    /// Scans elapsed since <paramref name="earlier"/>. <b>Correct across the wrap, and always forward.</b>
    ///
    /// <para>Because the result is modular it can never be negative — so a counter that genuinely went
    /// backwards appears as a very large forward number, and <see cref="IsPlausibleAdvanceFrom"/> is what
    /// separates the two. Any site gating on liveness must ask both.</para>
    /// </summary>
    public long Since(ScanCount earlier) => unchecked(Raw - earlier.Raw);

    /// <summary>Whether the difference from <paramref name="earlier"/> can be read as an advance at all.</summary>
    public bool IsPlausibleAdvanceFrom(ScanCount earlier) => Since(earlier) <= PlausibleAdvanceCeiling;

    /// <summary>The two mirror registers carrying the counter, in the map's word order.</summary>
    public static ScanCount FromRegisters(ushort first, ushort second, RegisterWordOrder order) =>
        new(RegisterWords.To32(first, second, order));

    public override string ToString() => Raw.ToString(CultureInfo.InvariantCulture);
}
