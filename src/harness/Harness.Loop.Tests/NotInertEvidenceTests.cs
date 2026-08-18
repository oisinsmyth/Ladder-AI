using Harness.Loop;
using Harness.Map;
using Harness.Results;
using Harness.Skeleton;
using Harness.Wire;

namespace Harness.Loop.Tests;

/// <summary>
/// 🔴 <b>WHAT A <c>NotInert</c> INDEX IS ALLOWED TO SAY ABOUT THE BLOCK — which is NOTHING.</b>
///
/// <para><b>The measurement these exist for.</b> JOB9004's vessel wave, 2026-08-18: three vectors on one
/// slot, indices 0 and 1 ran, index 2 came back <c>runOutcome: NotInert</c> with every expectation
/// <c>&lt;never read&gt;</c> — and the package said <i>"the slot was commanded and the echo says its
/// block never saw its start condition (X-E). The test did not happen."</i> That sentence accuses the
/// BLOCK. The same run's own final control frame read <c>StartBools = 0x0000</c>, so the commit provably
/// had not happened and nothing had been commanded at all.</para>
///
/// <para><b>The accusation was manufactured on our side of the wire.</b> <c>WaveRun</c> handed the
/// co-running log the index's PLANNED slot set on the path where <c>InertPhase.Commit</c> is never
/// called, and the inert phase had just cleared the echo a round trip earlier — so
/// <c>Commanded ∧ ¬Executed</c> was guaranteed. <c>StimulusCheck</c> was right all along and had the
/// correct outcome (<see cref="StimulusOutcome.NeverRan"/>) sitting one branch above; it was fed a
/// false input.</para>
///
/// <para><b>The cost of that is the whole reason it matters:</b> a day was spent looking for a start-bit
/// write race in the client that does not exist, on evidence the client had fabricated about itself.</para>
/// </summary>
public class NotInertEvidenceTests
{
    private const int ProgramBase = 3000;

    /// <summary>
    /// The same fixture block, bound with <b>one deliberately wrong declared resting value</b>: the count
    /// is declared to rest at 1 and network 1 drives it to 0 while the start command is off.
    ///
    /// <para>So the inert phase's FIRST check refuses at index 0, the wave stops, and the vector comes
    /// back <c>NotInert</c> — the shape measured on the rig, reproduced from a declaration rather than
    /// from a defect in the block, because that is what the rig case turned out to be too.</para>
    /// </summary>
    private static SlotBinding BindingWithAWrongDeclaredRest(string slotId = "S0")
    {
        var binding = TrivialBlock.Binding(slotId);

        return binding with
        {
            ResultSources = new[]
            {
                new MirroredSignal(TrivialBlock.CountTag, MirrorValueType.Int, SpecName: TrivialBlock.CountTag,
                    Rest: InertRest.At("1", "deliberately wrong: this block drives the count to 0 at rest")),
                binding.ResultSources[1],
            },
        };
    }

    private static (LoopResult Result, SimulatedGateway Gateway) RunWithARefusingInertDeclaration()
    {
        var request = LoopRunTests.Request() with { Bindings = new[] { BindingWithAWrongDeclaredRest() } };
        var gateway = new SimulatedGateway(LoopRunTests.Geometry());

        return (LoopRun.Execute(request, gateway), gateway);
    }

    // ---------------------------------------------------------------------------------------------
    // The fabricated accusation
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_NotInert_INDEX_REPORTS_NeverRan_AND_NOT_CommandedButDidNotRun()
    {
        var (result, _) = RunWithARefusingInertDeclaration();

        var package = Assert.Single(result.Packages);

        Assert.Equal(SlotOutcome.NotInert, package.RunOutcome);

        // The verdict is unchanged and correct either way — what changes is the REASON, and the reason is
        // what an agent acts on.
        Assert.Equal(ResultVerdict.Stale, package.Verdict);

        Assert.Equal(StimulusOutcome.NeverRan, package.Stimulus.Outcome);

        Assert.DoesNotContain("the echo says its block never saw its start condition",
            package.WhatToDoNext, StringComparison.Ordinal);

        Assert.DoesNotContain("Reading this as a failure would send an agent editing correct logic",
            package.WhatToDoNext, StringComparison.Ordinal);
    }

    [Fact]
    public void THE_WAVE_LOG_AGREES_WITH_THE_WIRE_no_slot_is_recorded_as_commanded_at_a_refused_index()
    {
        // The claim under test is about the DEVICE: no start bool was ever raised. The log has to say the
        // same thing the mirror does, because everything downstream reads the log.
        var (result, _) = RunWithARefusingInertDeclaration();

        var index = Assert.Single(result.Wave!.Log.Indices);

        Assert.Empty(index.Commanded);
        Assert.Equal(new[] { 0 }, index.PlannedSlots);
        Assert.Equal(CoRunningOutcome.NotCommitted, index.Outcome);
    }

    // ---------------------------------------------------------------------------------------------
    // The reason the refusal happened, which reached nobody at all
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void THE_INERT_PHASES_OWN_REASON_REACHES_THE_HEADLINE_because_it_reached_nothing_before()
    {
        // A NotInert package names no register and no check. `InertReport` names both, and it was on
        // `SlotRunResult.Inert` being rendered by nothing — so the three candidate causes (a wrong
        // declaration, a signal that is not quiescent by construction, a program that did not reset) were
        // indistinguishable from the artifact.
        var (result, _) = RunWithARefusingInertDeclaration();

        Assert.Contains("INERT REFUSED AT 1 INDEX(ES)", result.Detail, StringComparison.Ordinal);
        Assert.Contains(nameof(InertOutcome.StartConditionsWrong), result.Detail, StringComparison.Ordinal);

        // Named to the register, which is the whole value of carrying it.
        Assert.Contains("R000 reads 0, declared 1", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_RUN_WITH_NO_REFUSAL_SAYS_NOTHING_ABOUT_INERT_REFUSALS()
    {
        // A line that appears on every run is a line nobody reads. The healthy path must stay silent, or
        // the loud one stops being loud.
        var result = LoopRun.Execute(LoopRunTests.Request(), new SimulatedGateway(LoopRunTests.Geometry()));

        Assert.Equal(LoopOutcome.Ran, result.Outcome);
        Assert.DoesNotContain("INERT REFUSED", result.Detail, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // 🔴 THE RIG IS LEFT INERT, NOT RUNNING
    //
    // Measured on JOB9004, 2026-08-18: the final control frame of two COMPLETE vessel waves reads
    // StartBools = 0x0001 and StartEcho = 0x0001. Nothing in `WaveRun` lowers them at the end — the
    // reset level is written by the NEXT index's inert phase, and after the last index there is no next
    // index — so the block under test was left commanded, with nobody observing it, until somebody ran
    // another wave.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void THE_RUN_LOWERS_EVERY_START_BOOL_WHEN_IT_FINISHES_so_no_block_is_left_commanded()
    {
        var gateway = new SimulatedGateway(LoopRunTests.Geometry());
        var result = LoopRun.Execute(LoopRunTests.Request(), gateway);

        Assert.Equal(LoopOutcome.Ran, result.Outcome);

        var map = MapAllocator.Allocate(new WaveSetRequest(
            LoopRunTests.Geometry(), new[] { new SlotRequest("S0", 2, 2) })).Require();

        var startBoolByte = LoopRunTests.Geometry().ByteAddressOf(map.StartBools.Register);

        Assert.Equal(0, gateway.Plc.Memory[startBoolByte]);
        Assert.Equal(0, gateway.Plc.Memory[startBoolByte + 1]);

        // And the block under test actually lets go: one more scan of the copy layer carries the lowered
        // mirror bit into the block's own start condition, and network 1 puts the count back to zero.
        gateway.Plc.Run(3);

        Assert.Equal(0, gateway.Plc.Memory[ProgramBase] & 0b1);
    }

    [Fact]
    public void THE_RELEASE_IS_REPORTED_because_a_release_that_did_not_happen_must_be_visible()
    {
        var result = LoopRun.Execute(LoopRunTests.Request(), new SimulatedGateway(LoopRunTests.Geometry()));

        Assert.Contains("The rig was released", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void THE_RELEASE_HAPPENS_AFTER_PACKAGING_because_SETTLING_re_reads_a_still_driven_block()
    {
        // The ordering is the reason the release is in the loop rather than in the wave. `Package`
        // evaluates settling by re-reading the slot's results some scans after completion and comparing
        // them against what completion recorded — which is only meaningful while the block is still
        // driven. Release first and every healthy last index reports NotSettled instead.
        var result = LoopRun.Execute(LoopRunTests.Request(), new SimulatedGateway(LoopRunTests.Geometry()));

        var package = Assert.Single(result.Packages);

        Assert.Equal(SettlingState.Settled, package.Settling);
        Assert.Equal(ResultVerdict.Pass, package.Verdict);
    }
}
