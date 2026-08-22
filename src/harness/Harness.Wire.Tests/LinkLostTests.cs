using Harness.Map;
using Harness.Wire;

namespace Harness.Wire.Tests;

/// <summary>
/// 🔴 <b>A dropped link used to destroy the whole wave and leave no record of it.</b>
///
/// <para>Found 2026-08-22 and unfixed until now: <c>LoopCli</c> calls <c>LoopRun.Execute</c> unguarded,
/// and the only <c>try</c> in <c>LoopRun</c> is the rig-release tidy-up. A <c>SocketException</c> from
/// inside the Modbus library therefore escaped <c>WaveRun</c>, escaped <c>Execute</c>, and reached the
/// top of <c>harness-run</c> — which never got as far as <c>Write(result)</c>. Every index that had
/// already completed went with it, and the only artifact was a stack trace in a terminal.</para>
///
/// <para><b>A batch multiplies what one lost run costs</b>, which is why this closed before the batching
/// work rather than after it.</para>
/// </summary>
public class LinkLostTests
{
    private static readonly BuildStamp Stamp = MirrorClientTests.Stamp;

    /// <summary>
    /// A transport that works normally and then stops answering, exactly as a dropped TCP connection
    /// does — the failure arrives mid-wave, not at connect.
    /// </summary>
    private sealed class DyingTransport : IRegisterTransport
    {
        private readonly RecordingTransport _inner;

        public DyingTransport(RecordingTransport inner, int failAfterTransactions)
        {
            _inner = inner;
            FailAfter = failAfterTransactions;
        }

        public int FailAfter { get; set; }

        public int Transactions { get; private set; }

        private void Tick()
        {
            Transactions++;
            if (Transactions > FailAfter)
            {
                // What NModbusTransport.LinkGuarded produces from a SocketException. Constructed here
                // rather than throwing a raw SocketException because the guard is the transport's job and
                // this test is about what the WAVE does with the result.
                throw new WireLinkLostException(
                    "the link to the device is gone (SocketException: A connection attempt failed because the connected party did not properly respond after a period of time).",
                    new System.Net.Sockets.SocketException(10060));
            }
        }

        public ushort[] ReadHoldingRegisters(int startRegister, int count)
        {
            Tick();
            return _inner.ReadHoldingRegisters(startRegister, count);
        }

        public void WriteHoldingRegisters(int startRegister, ushort[] values)
        {
            Tick();
            _inner.WriteHoldingRegisters(startRegister, values);
        }

        public void Dispose() => _inner.Dispose();
    }

    private static (RecordingTransport Recording, RegisterMap Map) Fixture(int slots = 1)
    {
        var map = MapAllocator.Allocate(new WaveSetRequest(
            MirrorGeometry.ForCpu1214C(256, 4000),
            Enumerable.Range(0, slots).Select(i => new SlotRequest($"S{i}", 2, 2)).ToArray())).Require();

        var wire = new RecordingTransport(map, Stamp);
        wire.OnTransaction = t =>
        {
            for (var i = 0; i < slots; i++)
            {
                var register = i / RegisterMap.SlotsPerStartRegister;
                var mask = (ushort)(1 << (i % RegisterMap.SlotsPerStartRegister));
                var running = (t.StartBoolRegisters[register] & mask) != 0;
                t.SetResult(i, 0, running ? (ushort)(10 + i) : (ushort)0);
                t.SetResult(i, 1, running ? (ushort)1 : (ushort)0);
            }
        };

        return (wire, map);
    }

    private static WireVector Vector() => new(
        Values: new ushort[] { 1, 2 },
        Inert: new InertDeclaration(new Dictionary<int, ushort> { [0] = 0, [1] = 0 }),
        CompletionRegister: 1,
        CompletionValue: 1,
        Duration: new ScanBudget(1, 1));

    private static SlotTensor Tensor(int slot, int length) =>
        new(slot, Enumerable.Range(0, length).Select(_ => Vector()).ToArray());

    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// <b>The whole point: the wave RETURNS.</b> Before this, the same situation produced an exception
    /// and no <see cref="WaveResult"/> at all.
    /// </summary>
    [Fact]
    public void A_link_lost_mid_wave_RETURNS_a_result_instead_of_throwing()
    {
        var (recording, map) = Fixture();

        // Enough transactions to get several indices done, then stop answering.
        var dying = new DyingTransport(recording, failAfterTransactions: 25);
        var client = new MirrorClient(map, dying, Stamp);

        var wave = WaveRun.Run(client, RuntimeCompression.Uncompressed, new[] { Tensor(0, 8) });

        Assert.False(wave.RanToCompletion);
        Assert.NotNull(wave.Interruption);
    }

    /// <summary>
    /// 🔴 <b>The indices that completed are still reported.</b> That is the difference between this and
    /// the old behaviour, and it is the only reason the fix is worth anything: a wave that got most of
    /// the way through has evidence in it.
    /// </summary>
    [Fact]
    public void Everything_observed_BEFORE_the_drop_survives()
    {
        var (recording, map) = Fixture();
        var dying = new DyingTransport(recording, failAfterTransactions: 25);
        var client = new MirrorClient(map, dying, Stamp);

        var wave = WaveRun.Run(client, RuntimeCompression.Uncompressed, new[] { Tensor(0, 8) });

        var results = wave.For(0).Results;
        var completed = results.Count(r => r.Outcome == SlotOutcome.Completed);

        Assert.True(completed > 0, "no index completed before the drop, so this fixture proves nothing about survival.");
        Assert.True(completed < 8, "every index completed, so the link never dropped and this test is not testing the drop.");

        // The interruption's own count agrees with what is actually in the package. A denominator that
        // disagrees with the results is worse than none.
        Assert.Equal(completed, wave.Interruption!.IndicesCompleted);
    }

    /// <summary>
    /// <b>The in-flight vector is <see cref="SlotOutcome.LinkLost"/>, not <see cref="SlotOutcome.TimedOut"/>.</b>
    ///
    /// <para>A timeout is a claim about the PLANT — that a condition did not occur within the backstop —
    /// and this file's own note says a spurious one "is worse than a spurious FAILED, because it is
    /// believed". A dropped socket says nothing about the plant, and calling it a timeout would send a
    /// reader to the block, the scenario clock and the compression factor, none of which is at fault.</para>
    /// </summary>
    [Fact]
    public void The_in_flight_vector_is_LINK_LOST_and_not_a_timeout()
    {
        var (recording, map) = Fixture();
        var dying = new DyingTransport(recording, failAfterTransactions: 25);
        var client = new MirrorClient(map, dying, Stamp);

        var wave = WaveRun.Run(client, RuntimeCompression.Uncompressed, new[] { Tensor(0, 8) });

        var last = wave.For(0).Results[^1];
        Assert.Equal(SlotOutcome.LinkLost, last.Outcome);
        Assert.DoesNotContain(wave.For(0).Results, r => r.Outcome == SlotOutcome.TimedOut);
        Assert.Contains("NOTHING WAS LEARNED ABOUT THIS VECTOR", last.Detail);
    }

    /// <summary>
    /// The package must say how much of the wave happened. A short package that does not state its own
    /// denominator is one a reader explains to themselves.
    /// </summary>
    [Fact]
    public void The_interruption_states_the_index_it_died_on_and_what_was_never_attempted()
    {
        var (recording, map) = Fixture();
        var dying = new DyingTransport(recording, failAfterTransactions: 25);
        var client = new MirrorClient(map, dying, Stamp);

        var wave = WaveRun.Run(client, RuntimeCompression.Uncompressed, new[] { Tensor(0, 8) });

        var interruption = wave.Interruption!;
        Assert.Equal(8, interruption.AtIndex + interruption.IndicesNeverAttempted + 1);
        Assert.Contains("THE WAVE DID NOT FINISH", interruption.ToString());
        Assert.Contains("never attempted", interruption.ToString());
    }

    /// <summary>
    /// Every slot in flight is accounted for, not just the first. A multi-slot wave that reported one
    /// LinkLost and left the others silent would read as though the rest had simply not been scheduled.
    /// </summary>
    [Fact]
    public void EVERY_active_slot_gets_an_outcome_when_the_link_dies()
    {
        var (recording, map) = Fixture(slots: 3);
        var dying = new DyingTransport(recording, failAfterTransactions: 30);
        var client = new MirrorClient(map, dying, Stamp);

        var wave = WaveRun.Run(client, RuntimeCompression.Uncompressed,
            new[] { Tensor(0, 6), Tensor(1, 6), Tensor(2, 6) });

        Assert.Equal(3, wave.Distributions.Count);
        Assert.All(wave.Distributions, d =>
            Assert.Equal(SlotOutcome.LinkLost, d.Results[^1].Outcome));
    }

    /// <summary>
    /// <b>The negative control.</b> A wave that is never interrupted must report
    /// <see cref="WaveResult.RanToCompletion"/> — otherwise "it did not finish" would be true of every
    /// run and would stop meaning anything.
    /// </summary>
    [Fact]
    public void A_wave_that_is_NOT_interrupted_says_so()
    {
        var (recording, map) = Fixture();
        var client = new MirrorClient(map, recording, Stamp);

        var wave = WaveRun.Run(client, RuntimeCompression.Uncompressed, new[] { Tensor(0, 4) });

        Assert.True(wave.RanToCompletion);
        Assert.Null(wave.Interruption);
        Assert.DoesNotContain(wave.For(0).Results, r => r.Outcome == SlotOutcome.LinkLost);
    }

    /// <summary>
    /// A link that dies on the very first transaction still produces a result rather than an exception —
    /// the degenerate case, where nothing at all was observed and the package has to say exactly that.
    /// </summary>
    [Fact]
    public void A_link_that_never_worked_produces_a_package_saying_nothing_was_observed()
    {
        var (recording, map) = Fixture();

        // 3 transactions gets the client past its build-stamp check and into the wave.
        var dying = new DyingTransport(recording, failAfterTransactions: 3);
        var client = new MirrorClient(map, dying, Stamp);

        var wave = WaveRun.Run(client, RuntimeCompression.Uncompressed, new[] { Tensor(0, 4) });

        Assert.False(wave.RanToCompletion);
        Assert.Equal(0, wave.Interruption!.IndicesCompleted);
        Assert.All(wave.For(0).Results, r => Assert.Equal(SlotOutcome.LinkLost, r.Outcome));
    }
}
