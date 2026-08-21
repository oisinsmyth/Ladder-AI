using Harness.Wire;
using Xunit;

namespace Harness.Wire.Tests;

/// <summary>
/// 🔴 <b>THE SETTLING DWELL — AND EVERY ROAD THAT IS NOT A COMPARISON RETURNS <c>Taken: false</c> WITH
/// ITS REASON.</b>
///
/// <para>A sample that could not be taken must never arrive downstream looking like one that was taken
/// and found nothing. That distinction is the whole reason this type carries <c>Taken</c> separately
/// from <c>Unchanged</c> rather than returning a bare boolean.</para>
///
/// <para>These drive <c>SlotRun.Dwell</c> directly against a register file, because the refusal paths
/// are reachable from a running wave only by arranging a broken binding — and a refusal that is only
/// reachable through a defect is a refusal nobody tests.</para>
/// </summary>
public class SettlingSampleTests
{
    private static readonly SettlingRegister Count = new("Demo_Count", 0);
    private static readonly SettlingRegister Done = new("Demo_Done", 1);

    // ---------------------------------------------------------------------------------------------
    // the two real answers
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_BAND_THAT_HOLDS_STILL_IS_TAKEN_AND_UNCHANGED()
    {
        var (client, _) = Wired();

        var sample = SlotRun.Dwell(client, 0, Probe(3, Count, Done), new ushort[] { 0, 0 }, client.ReadControl().ScanCounter);

        Assert.NotNull(sample);
        Assert.True(sample!.Taken);
        Assert.True(sample.Unchanged);
        Assert.Contains("settled", sample.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.True(sample.ScansWaited >= 3, sample.Detail);
    }

    [Fact]
    public void AND_A_REGISTER_THAT_MOVES_IS_TAKEN_AND_NAMED()
    {
        var (client, wire) = Wired();

        // The band the vector closed on says 0; the program has since moved R000 to 7.
        wire.OnTransaction = t => t.SetResult(0, 0, 7);

        var sample = SlotRun.Dwell(client, 0, Probe(3, Count, Done), new ushort[] { 0, 0 }, client.ReadControl().ScanCounter);

        Assert.NotNull(sample);
        Assert.True(sample!.Taken);
        Assert.False(sample.Unchanged);

        // *** THE SIGNAL NAME, NOT ONLY THE REGISTER. *** A report that says only "R000 moved" makes
        // attributing a wave of unsettled verdicts a forensic exercise, which this project has done once.
        Assert.Contains("Demo_Count", sample.Detail, StringComparison.Ordinal);
        Assert.Contains("R000", sample.Detail, StringComparison.Ordinal);
        Assert.Contains("0 -> 7", sample.Detail, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // every road that is not a comparison
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void NO_PROBE_IS_NO_SAMPLE_rather_than_an_empty_one()
    {
        var (client, _) = Wired();

        // Null means nobody asked. The evaluator turns that into NotEstablished with that reason - it
        // must not arrive as a sample that was taken and found nothing.
        Assert.Null(SlotRun.Dwell(client, 0, null, new ushort[] { 0, 0 }, client.ReadControl().ScanCounter));
    }

    [Fact]
    public void A_ZERO_LENGTH_DWELL_IS_REFUSED_because_it_is_satisfied_by_anything()
    {
        var (client, _) = Wired();

        var sample = SlotRun.Dwell(client, 0, Probe(0, Count), new ushort[] { 0, 0 }, client.ReadControl().ScanCounter);

        Assert.False(sample!.Taken);
        Assert.False(sample.Unchanged);
        Assert.Contains("NOT settled", sample.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_PROBE_OVER_NO_REGISTER_IS_REFUSED_because_EMPTY_IS_NOT_CLEAN()
    {
        var (client, _) = Wired();

        var sample = SlotRun.Dwell(client, 0, Probe(3), new ushort[] { 0, 0 }, client.ReadControl().ScanCounter);

        Assert.False(sample!.Taken);
        Assert.Contains("EMPTY IS NOT CLEAN", sample.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_PROBE_REACHING_OUTSIDE_THE_BAND_IS_REFUSED_rather_than_silently_narrowed()
    {
        var (client, _) = Wired();

        var sample = SlotRun.Dwell(client, 0, Probe(3, new SettlingRegister("Demo_Ghost", 9)), new ushort[] { 0, 0 },
            client.ReadControl().ScanCounter);

        Assert.False(sample!.Taken);
        Assert.Contains("R009", sample.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_SCAN_COUNTER_THAT_NEVER_ADVANCES_IS_A_DWELL_THAT_NEVER_COMPLETED()
    {
        // *** A STOPPED PLC MUST NOT SETTLE. *** The band is genuinely unchanged the whole time - it is
        // unchanged because nothing is running - and reporting that as settled would be the purest form
        // of a check that passed because nothing happened.
        var (client, wire) = Wired();
        wire.ScansPerTransaction = 0;

        var sample = SlotRun.Dwell(client, 0, Probe(3, Count), new ushort[] { 0, 0 }, client.ReadControl().ScanCounter);

        Assert.False(sample!.Taken);
        Assert.False(sample.Unchanged);
        Assert.Contains("never completed", sample.Detail, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // fixtures
    // ---------------------------------------------------------------------------------------------

    private static SettlingProbe Probe(int scans, params SettlingRegister[] registers) => new(registers, scans);

    private static (MirrorClient Client, RecordingTransport Wire) Wired()
    {
        var map = MirrorClientTests.Map();
        var wire = new RecordingTransport(map, MirrorClientTests.Stamp);
        return (new MirrorClient(map, wire, MirrorClientTests.Stamp), wire);
    }
}
