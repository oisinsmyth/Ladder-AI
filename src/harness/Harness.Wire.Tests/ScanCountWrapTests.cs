using Harness.Map;
using Harness.Wire;

namespace Harness.Wire.Tests;

/// <summary>
/// The scan counter across its wrap, <b>ON THE MODBUS PATH</b>.
///
/// <para>🔴 <b>THE DID-NOT-RUN TEST.</b> The wrap was already absorbed on <c>S7Transport</c>, with a
/// passing test — and <b>S7 variable access is refused CPU-wide on this rig, so every data read goes
/// over Modbus</b>. That test proves nothing about the live route. Everything here drives the boundary
/// through <see cref="MirrorClient"/> and the register pair, which is the path that actually runs.</para>
///
/// <para>*** THE FINDING WAS A GUARD WRITTEN, TESTED, AND LIVING ON A PATH THAT CANNOT EXECUTE *** —
/// found by asking WHICH TRANSPORT ACTUALLY CARRIES THIS.</para>
/// </summary>
public class ScanCountWrapTests
{
    private const uint LastBeforeWrap = uint.MaxValue;
    private const uint DIntBoundary = 0x8000_0000;      // int.MaxValue + 1 — where the OLD sign-extension broke
    private const uint LastPositiveDInt = 0x7FFF_FFFF;  // int.MaxValue

    // ---- THE ARITHMETIC ---------------------------------------------------------------------------

    /// <summary>
    /// *** THE EXACT NUMBER FROM THE FINDING. *** Sign-extending the same 32 bits into a <c>long</c> made
    /// a ONE-SCAN advance across the <c>DInt</c> boundary read as <b>−4 294 967 295</b>.
    /// </summary>
    [Fact]
    public void One_scan_across_the_DInt_boundary_is_ONE_and_not_MINUS_FOUR_BILLION()
    {
        var before = new ScanCount(LastPositiveDInt);
        var after = new ScanCount(DIntBoundary);

        Assert.Equal(1, after.Since(before));

        // What the old decode produced, kept as the thing being ruled out rather than described.
        Assert.NotEqual(-4_294_967_295L, after.Since(before));
        Assert.Equal(-4_294_967_295L, unchecked((int)DIntBoundary) - (long)unchecked((int)LastPositiveDInt));
    }

    [Fact]
    public void One_scan_across_the_UNSIGNED_wrap_is_also_ONE()
    {
        Assert.Equal(1, new ScanCount(0).Since(new ScanCount(LastBeforeWrap)));
    }

    [Theory]
    [InlineData(0u, 0u, 0L)]
    [InlineData(10u, 4u, 6L)]
    [InlineData(LastPositiveDInt, 0u, 2_147_483_647L)]
    [InlineData(DIntBoundary + 5, LastPositiveDInt, 6L)]
    [InlineData(3u, LastBeforeWrap, 4L)]
    public void The_difference_is_modular_and_therefore_never_negative(uint now, uint then, long expected)
    {
        Assert.Equal(expected, new ScanCount(now).Since(new ScanCount(then)));
    }

    /// <summary>
    /// A modular difference can never be negative, so a counter that went BACKWARDS reads as a very large
    /// FORWARD number — which would satisfy any liveness threshold. That is the dangerous direction, and
    /// it is why <see cref="ScanCount.IsPlausibleAdvanceFrom"/> exists beside <see cref="ScanCount.Since"/>.
    /// </summary>
    [Fact]
    public void A_counter_that_went_BACKWARDS_is_not_a_plausible_advance()
    {
        var earlier = new ScanCount(1_000_000);
        var restarted = new ScanCount(5);

        Assert.True(restarted.Since(earlier) > 0, "the modular difference is positive, which is exactly the trap.");
        Assert.False(restarted.IsPlausibleAdvanceFrom(earlier));

        // And the ordinary case still is one.
        Assert.True(new ScanCount(1_000_006).IsPlausibleAdvanceFrom(earlier));
    }

    /// <summary>
    /// *** THE WRONG SUBTRACTION DOES NOT COMPILE. *** Pinned by reflection, because the whole point of
    /// making it a type is that a future site cannot quietly re-introduce <c>now - from</c> — and no
    /// behavioural test would notice an operator being added.
    /// </summary>
    [Fact]
    public void ScanCount_defines_NO_subtraction_and_NO_ordering_operators()
    {
        var names = typeof(ScanCount)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Select(m => m.Name)
            .ToArray();

        foreach (var forbidden in new[] { "op_Subtraction", "op_Addition", "op_LessThan", "op_GreaterThan", "op_LessThanOrEqual", "op_GreaterThanOrEqual", "op_Implicit", "op_Explicit" })
        {
            Assert.False(names.Contains(forbidden),
                $"ScanCount defines {forbidden}. The wrong difference has to be UNEXPRESSIBLE, not merely discouraged — a conversion or an operator puts `now - from` back one keystroke away, and that subtraction is the defect.");
        }
    }

    // ---- THROUGH THE LIVE PATH: registers -> MirrorClient -> ControlSnapshot ------------------------

    private static RegisterMap Map()
    {
        var result = MapAllocator.Allocate(new WaveSetRequest(
            MirrorGeometry.ForCpu1214C(0, 1000, declaredRegisters: (MirrorGeometry.Cpu1214CBitMemoryBytes - 1000) / 2),
            new[] { new SlotRequest("S0", 2, 2) }));

        Assert.True(result.Allocated, string.Join(" | ", result.Refusals));
        return result.Map!;
    }

    /// <summary>A transport that publishes one chosen counter value into the real mirror geometry.</summary>
    private sealed class CounterAt : IRegisterTransport
    {
        private readonly RegisterMap _map;
        private readonly BuildStamp _stamp;
        public uint Value;

        public CounterAt(RegisterMap map, BuildStamp stamp, uint value)
        {
            _map = map;
            _stamp = stamp;
            Value = value;
        }

        public ushort[] ReadHoldingRegisters(int startRegister, int count)
        {
            var registers = new ushort[count];

            void Put(int register, uint value)
            {
                var words = RegisterWords.From32(value, RegisterWordOrder.HighWordFirst);
                if (register - startRegister >= 0 && register - startRegister + 1 < count)
                {
                    registers[register - startRegister] = words[0];
                    registers[register - startRegister + 1] = words[1];
                }
            }

            Put(_map.Version.Register, _stamp.Value);
            Put(_map.ScanCounter.Register, Value);
            return registers;
        }

        public void WriteHoldingRegisters(int startRegister, ushort[] values) { }

        public void Dispose() { }
    }

    /// <summary>
    /// The register pair is decoded UNSIGNED, so a counter past <c>int.MaxValue</c> arrives as itself
    /// rather than as a negative. This is the decode the live route performs.
    /// </summary>
    [Fact]
    public void The_MODBUS_decode_reads_the_counter_UNSIGNED()
    {
        var map = Map();
        var stamp = new BuildStamp(0x1234ABCD);
        var wire = new CounterAt(map, stamp, DIntBoundary);
        var client = new MirrorClient(map, wire, stamp);

        Assert.Equal(new ScanCount(DIntBoundary), client.ReadControl().ScanCounter);
    }

    /// <summary>
    /// *** THE WHOLE DEFECT, END TO END ON THE PATH THAT RUNS. *** Two successive control reads one scan
    /// apart across the boundary, through <c>MirrorClient</c>, and the elapsed count is 1.
    /// </summary>
    [Fact]
    public void TWO_CONTROL_READS_ACROSS_THE_BOUNDARY_ARE_ONE_SCAN_APART()
    {
        var map = Map();
        var stamp = new BuildStamp(0x1234ABCD);
        var wire = new CounterAt(map, stamp, LastPositiveDInt);
        var client = new MirrorClient(map, wire, stamp);

        var before = client.ReadControl().ScanCounter;
        wire.Value = DIntBoundary;
        var after = client.ReadControl().ScanCounter;

        Assert.Equal(1, after.Since(before));
        Assert.True(after.IsPlausibleAdvanceFrom(before));
    }

    /// <summary>
    /// The consequence at the three sites named in the finding, in the form they express it: a
    /// COMPLETED slot's elapsed count. It used to print "after −4294967295 scan(s)".
    /// </summary>
    [Fact]
    public void A_SLOT_RESULT_SPANNING_THE_BOUNDARY_REPORTS_A_SANE_ELAPSED_COUNT()
    {
        var result = new SlotRunResult(
            SlotOutcome.Completed, new ushort[] { 10 },
            new ScanCount(LastPositiveDInt - 3), new ScanCount(DIntBoundary + 4),
            PollRounds: 2, RoundTrips: 4,
            new InertReport(InertOutcome.Established, new ScanCount(LastPositiveDInt - 4), Array.Empty<ushort>(), Array.Empty<ushort>(), "stub"),
            "stub",
            // About the WRAP, not about observation. See ObservationSeries.OfSingleFrame.
            ObservationSeries.OfSingleFrame(new ushort[] { 10 }, new ScanCount(DIntBoundary + 4)));

        Assert.Equal(8, result.ElapsedScans);
    }
}
