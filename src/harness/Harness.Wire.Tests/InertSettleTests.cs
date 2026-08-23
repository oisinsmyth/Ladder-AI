using Harness.Map;
using Harness.Wire;

namespace Harness.Wire.Tests;

/// <summary>
/// 🔴 <b>Retrying the inert phase at index 0, instead of sleeping past a transient nobody measured.</b>
///
/// <para>Measured twice on the rig: the first wave after a download refuses <c>NotQuiescent</c> because
/// the model is still integrating. A fixed 15 s wait was tried and MEASURED TOO SHORT. The inert phase
/// already is the readiness test — two observations a scan apart — so the wave asks again, and the
/// attempts it needed become the measurement of the settling time.</para>
/// </summary>
public class InertSettleTests
{
    private static readonly BuildStamp Stamp = MirrorClientTests.Stamp;

    /// <summary>A fixture whose results keep moving until the Nth inert attempt, then hold still.</summary>
    private static (MirrorClient Client, RecordingTransport Wire) Settling(int movingForTransactions)
    {
        var map = MapAllocator.Allocate(new WaveSetRequest(
            MirrorGeometry.ForCpu1214C(256, 4000, declaredRegisters: (MirrorGeometry.Cpu1214CBitMemoryBytes - 4000) / 2),
            new[] { new SlotRequest("S0", 2, 2) })).Require();

        var wire = new RecordingTransport(map, Stamp);
        var reads = 0;

        wire.OnTransaction = t =>
        {
            var running = (t.StartBoolRegisters[0] & 1) != 0;

            // Each inert attempt takes two observations. Until the target attempt, R000 alternates —
            // the shape the rig actually showed, where R013 went 1->0 and R014 0->1 between the two.
            reads++;
            var stillMoving = reads < movingForTransactions;

            t.SetResult(0, 0, running ? (ushort)10 : (ushort)(stillMoving && reads % 2 == 0 ? 1 : 0));
            t.SetResult(0, 1, running ? (ushort)1 : (ushort)0);
        };

        return (new MirrorClient(map, wire, Stamp), wire);
    }

    private static WireVector Vector() => new(
        Values: new ushort[] { 1, 2 },
        Inert: new InertDeclaration(new Dictionary<int, ushort> { [0] = 0, [1] = 0 }),
        CompletionRegister: 1,
        CompletionValue: 1,
        Duration: new ScanBudget(1, 1));

    private static SlotTensor Tensor(int length) =>
        new(0, Enumerable.Range(0, length).Select(_ => Vector()).ToArray());

    // ---------------------------------------------------------------------------------------------

    /// <summary><b>Without a retry, a plant still moving at index 0 refuses.</b> The behaviour that stands.</summary>
    [Fact]
    public void With_NO_retry_a_moving_plant_refuses_at_the_first_index()
    {
        var (client, _) = Settling(movingForTransactions: 10_000);

        var wave = WaveRun.Run(client, RuntimeCompression.Uncompressed, new[] { Tensor(2) });

        Assert.Equal(SlotOutcome.NotInert, wave.For(0).Results[0].Outcome);
        Assert.Null(wave.SettleReport);
    }

    /// <summary>
    /// <b>With a retry, the same plant settles and the wave proceeds</b> — and the report says how many
    /// attempts it took, which is the measurement the fixed wait never produced.
    /// </summary>
    [Fact]
    public void With_a_retry_the_same_plant_settles_and_the_attempts_are_REPORTED()
    {
        var (client, _) = Settling(movingForTransactions: 14);
        var waits = new List<TimeSpan>();

        var wave = WaveRun.Run(client, RuntimeCompression.Uncompressed, new[] { Tensor(2) },
            inertSettle: InertSettle.Retry(6, TimeSpan.FromSeconds(5)), pauseFor: waits.Add);

        Assert.NotEqual(SlotOutcome.NotInert, wave.For(0).Results[0].Outcome);
        Assert.NotNull(wave.SettleReport);
        Assert.Contains("inert attempt(s)", wave.SettleReport);
        Assert.Contains("MEASURED rather than waited out", wave.SettleReport);

        // It waited between attempts, and stopped as soon as the plant was quiescent rather than
        // spending the whole budget.
        Assert.NotEmpty(waits);
        Assert.True(waits.Count < 5, $"it should stop retrying once inert holds, but waited {waits.Count} times.");
    }

    /// <summary>
    /// 🔴 <b>Exhausting the retries is NOT a pass, and the report says the transient explanation has run
    /// out.</b> A plant still moving after a minute of asking is not starting up.
    /// </summary>
    [Fact]
    public void A_plant_that_never_settles_still_refuses_and_the_report_stops_blaming_startup()
    {
        var (client, _) = Settling(movingForTransactions: 10_000);

        var wave = WaveRun.Run(client, RuntimeCompression.Uncompressed, new[] { Tensor(2) },
            inertSettle: InertSettle.Retry(3, TimeSpan.FromSeconds(1)), pauseFor: _ => { });

        Assert.Equal(SlotOutcome.NotInert, wave.For(0).Results[0].Outcome);
        Assert.Contains("STILL not quiescent", wave.SettleReport);
        Assert.Contains("no longer a startup transient", wave.SettleReport);
    }

    /// <summary>
    /// 🔴 <b>The retry applies to the FIRST index only.</b> A slot that is not quiescent at a later index
    /// has been disturbed by something the wave itself did — that is what D33 exists to catch, and
    /// retrying there would paper over it.
    /// </summary>
    [Fact]
    public void A_LATER_index_is_never_retried()
    {
        var map = MapAllocator.Allocate(new WaveSetRequest(
            MirrorGeometry.ForCpu1214C(256, 4000, declaredRegisters: 2096),
            new[] { new SlotRequest("S0", 2, 2) })).Require();

        var wire = new RecordingTransport(map, Stamp);
        var indexSeen = 0;

        wire.OnTransaction = t =>
        {
            var running = (t.StartBoolRegisters[0] & 1) != 0;
            if (running) indexSeen++;

            // Quiescent at first, then permanently restless once the wave has commanded once.
            t.SetResult(0, 0, running ? (ushort)10 : (ushort)(indexSeen > 0 ? (ushort)(indexSeen % 2) : (ushort)0));
            t.SetResult(0, 1, running ? (ushort)1 : (ushort)0);
        };

        var waits = new List<TimeSpan>();
        var wave = WaveRun.Run(new MirrorClient(map, wire, Stamp), RuntimeCompression.Uncompressed,
            new[] { Tensor(3) }, inertSettle: InertSettle.Retry(6, TimeSpan.FromSeconds(5)), pauseFor: waits.Add);

        // Index 0 was quiescent, so no retry was needed there; a later index refusing must not retry.
        Assert.Empty(waits);
        Assert.Null(wave.SettleReport);
    }

    [Fact]
    public void Retry_of_fewer_than_one_attempt_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => InertSettle.Retry(0, TimeSpan.FromSeconds(1)));
    }

    // ---------------------------------------------------------------------------------------------
    // THE SETTLING TIME AS A NUMBER. Everything above tests the prose, and the prose is written only
    // when the retry loop had to work — so the run that measures the transient at zero was reported
    // exactly like the run that never measured it.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>The defect, pinned: a plant quiescent on the FIRST attempt is a measurement, and the prose
    /// says nothing about it.</b> Both assertions matter together — <c>SettleReport</c> null is the OLD
    /// behaviour preserved deliberately, and <c>InertSettle</c> non-null is the whole point. Before this
    /// existed, the pair here was (null, nothing), identical to a run where no retry was ever licensed.
    /// </summary>
    [Fact]
    public void Licensed_and_quiescent_at_once_is_a_MEASUREMENT_even_though_the_prose_stays_silent()
    {
        var (client, _) = Settling(movingForTransactions: 0);
        var waits = new List<TimeSpan>();

        var wave = WaveRun.Run(client, RuntimeCompression.Uncompressed, new[] { Tensor(2) },
            inertSettle: InertSettle.Retry(6, TimeSpan.FromSeconds(5)), pauseFor: waits.Add);

        Assert.Empty(waits);
        Assert.Null(wave.SettleReport);

        Assert.NotNull(wave.InertSettle);
        Assert.Equal(1, wave.InertSettle!.Attempts);
        Assert.Equal(TimeSpan.Zero, wave.InertSettle.Waited);
        Assert.True(wave.InertSettle.Quiescent);
    }

    /// <summary>
    /// 🔴 <b>THE NEGATIVE CONTROL, AND IT IS THE REASON THE FIELD IS NULLABLE AT ALL.</b> Without a
    /// licence there is no measurement — and if this ever returned a zero-valued one instead of null,
    /// the type would be claiming the plant was quiescent when nobody asked it anything. That is the
    /// same substitution the whole change exists to undo, pointing the other way.
    /// </summary>
    [Fact]
    public void With_NO_retry_licensed_there_is_no_measurement_at_all()
    {
        var (client, _) = Settling(movingForTransactions: 0);

        var wave = WaveRun.Run(client, RuntimeCompression.Uncompressed, new[] { Tensor(2) });

        Assert.Null(wave.SettleReport);
        Assert.Null(wave.InertSettle);
    }

    /// <summary>
    /// A plant that takes more than one attempt reports the count AND the wall time waited, and the two
    /// agree with each other: the wait happens BETWEEN attempts, so N attempts waited N-1 intervals.
    /// </summary>
    [Fact]
    public void A_plant_that_needs_several_attempts_reports_the_count_and_the_time_waited()
    {
        var (client, _) = Settling(movingForTransactions: 14);
        var waits = new List<TimeSpan>();
        var interval = TimeSpan.FromSeconds(5);

        var wave = WaveRun.Run(client, RuntimeCompression.Uncompressed, new[] { Tensor(2) },
            inertSettle: InertSettle.Retry(6, interval), pauseFor: waits.Add);

        Assert.NotNull(wave.InertSettle);
        Assert.True(wave.InertSettle!.Quiescent);
        Assert.True(wave.InertSettle.Attempts > 1, "this fixture is chosen to need a retry.");

        // Derived from the attempt count, and cross-checked against what the runner ACTUALLY paused for
        // — the arithmetic and the behaviour have to agree or one of them is decoration.
        Assert.Equal(wave.InertSettle.Attempts - 1, waits.Count);
        Assert.Equal(interval * waits.Count, wave.InertSettle.Waited);
    }

    /// <summary>
    /// 🔴 <b>Exhausting the licence is a MEASUREMENT, not a missing one.</b> <c>Quiescent = false</c> says
    /// the transient outlasted the budget, which is a finding about the plant. Reporting it as an absent
    /// measurement would hide the most interesting result this field can carry.
    /// </summary>
    [Fact]
    public void A_plant_that_never_settles_still_produces_a_measurement()
    {
        var (client, _) = Settling(movingForTransactions: 10_000);

        var wave = WaveRun.Run(client, RuntimeCompression.Uncompressed, new[] { Tensor(2) },
            inertSettle: InertSettle.Retry(3, TimeSpan.FromSeconds(1)), pauseFor: _ => { });

        Assert.NotNull(wave.InertSettle);
        Assert.Equal(3, wave.InertSettle!.Attempts);
        Assert.False(wave.InertSettle.Quiescent);
        Assert.Contains("STILL not quiescent", wave.InertSettle.ToString());
    }
}
