using Harness.Map;

namespace Harness.Map.Tests;

/// <summary>
/// 🔴 <b><see cref="LaneGenerator"/> — the production call site <see cref="SlotFcGenerator"/> and
/// <see cref="StimShellGenerator"/> did not have.</b>
///
/// <para>Both generators were reachable only from their own tests, so neither removed any work: standing a
/// conformance lane up still meant hand-authoring the slot FC and the whole of the stimulus head. These
/// tests are about the SEAM — what it generates, what it refuses, and, most of all, <b>what it says about
/// the things nobody declared</b>, because an undeclared object counted as generated would answer <i>how
/// much of this lane is still hand-built</i> with a number nothing established.</para>
///
/// <para><b>Every head here is INVENTED</b>, and <see cref="StimShellGeneratorTests"/>'s opening paragraph
/// applies unchanged: a green run of this file is not evidence that the shell reproduces a real head.</para>
/// </summary>
public class LaneGeneratorTests
{
    private static List<StimPhase> Phases() => new()
    {
        new("InHeadDisarm", "HeadDisarmEnd", "DisarmDwell",  StimPhaseKind.Disarm),
        new("InHeadReset",  "HeadResetEnd",  "ResetDwell",   StimPhaseKind.Reset),
        new("InHeadVerify", "HeadEnd",       "VerifyDwell",  StimPhaseKind.Verify),
        new("InScenario",   "ScenEnd",       "Stim.EndAt",   StimPhaseKind.Scenario),
        new("InTailDisarm", "TailDisarmEnd", "DisarmDwell",  StimPhaseKind.Disarm),
        new("InTailReset",  "TailResetEnd",  "ResetDwell",   StimPhaseKind.Reset),
        new("InTailVerify", "TailEnd",       "VerifyDwell",  StimPhaseKind.Verify),
    };

    private static StimHeadSpec HeadSpec(string head = "FB_WidgetStim") =>
        new(head, "iDB_WidgetUnderTest.Cmd.FaultReset", "T#5M", "T#200MS",
            Phases(),
            new[] { "iDB_WidgetUnderTest.Status.JamLatched" },
            new[] { "iDB_WidgetUnderTest.Status.StrokeComplete" },
            new[]
            {
                "Stim.ArrivedDirty", "Stim.InertAtStart", "Stim.RecoverFailed",
                "Stim.InertAtEnd", "Stim.CycleCounted", "Stim.ScenarioDone",
            });

    private static LaneDeclaration FullyDeclared(string slotId = "WIDGET") =>
        new(slotId,
            new SlotFcNaming("FC_HarnessWidgetSlot", 9010),
            new SlotCall("FB_WidgetStim", "iDB_WidgetStim", "Advance The Widget Stimulus Head"),
            new SlotCall("FB_WidgetUnderTest", "iDB_WidgetUnderTest", "Run The Widget Under Test"),
            HeadSpec());

    // ---------------------------------------------------------------------------------------------
    // WHAT IT GENERATES
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_FULL_DECLARATION_GENERATES_THE_SLOT_FC_AND_THE_SHELL()
    {
        var result = LaneGenerator.Generate(new[] { FullyDeclared() });

        Assert.False(result.Refused);

        var slot = Assert.Single(result.Slots);
        Assert.NotNull(slot.SlotFc);
        Assert.NotNull(slot.StimShell);

        // The FC is a DEPLOYABLE object; the shell is NOT one. Asserted together, because folding the
        // second into the first is the tempting simplification and it would put an unimportable file in
        // the list a lane deploys from.
        var fc = Assert.Single(result.Objects);
        Assert.Equal("FC_HarnessWidgetSlot", fc.Name);
        Assert.Equal(HarnessObjectKind.Block, fc.Kind);

        Assert.Single(result.Fragments);
    }

    [Fact]
    public void THE_HEAD_IS_CALLED_FIRST_AND_THE_ORDER_IS_NOT_A_PARAMETER_ANYWHERE()
    {
        // *** THE ONE THING IN THE FC THAT IS EASY TO GET WRONG. *** Reversing the two calls costs one
        // scan of latency on every transition: it does not fail a compile, does not fail an import, and
        // shows up as vectors timing out near their backstop for reasons nobody can see. The declaration
        // surface must not be able to express it either.
        var ir = Assert.Single(LaneGenerator.Generate(new[] { FullyDeclared() }).Objects).Ir;

        var head = ir.IndexOf("CALL FB_WidgetStim", StringComparison.Ordinal);
        var uut = ir.IndexOf("CALL FB_WidgetUnderTest", StringComparison.Ordinal);

        Assert.True(head >= 0 && uut >= 0);
        Assert.True(head < uut, "the stimulus head must be called before the block under test.");
    }

    [Fact]
    public void THE_CALL_SITE_OBLIGATION_ARRIVES_RATHER_THAN_BEING_EMITTED_INTO_A_VARIABLE()
    {
        // 🔴 THE FAILURE THIS SEAM EXISTS DOWNSTREAM OF. A hand-written slot FC was deployed and called by
        // nothing: every vector timed out while the start echo reported "commanded, observed to run"
        // throughout, because both halves of that echo live in the copy layer, which IS called. The
        // generator emits its own obligation so the omission is a thing somebody DECLINED to do — and an
        // obligation the seam drops is exactly the silence it was built to remove.
        var result = LaneGenerator.Generate(new[] { FullyDeclared() });

        Assert.Contains(result.Obligations, o =>
            o.Contains("FC_HarnessWidgetSlot", StringComparison.Ordinal)
            && o.Contains("MUST be called from the cyclic OB", StringComparison.Ordinal));
    }

    [Fact]
    public void THE_SHELLS_OWN_OBLIGATIONS_ARRIVE_TOO_and_are_attributed_to_the_head()
    {
        var result = LaneGenerator.Generate(new[] { FullyDeclared() });

        // The generator states what it CANNOT do and a person must — chiefly the comments, the disarm
        // lever and the scenario timeline. A caveat only a reader of the design meets has already failed
        // the person it was written for.
        Assert.Contains(result.Obligations, o => o.StartsWith("FB_WidgetStim: ", StringComparison.Ordinal));
        Assert.Contains(result.Obligations, o => o.Contains("THE DISARM LEVER", StringComparison.Ordinal));

        // And the shell says, in the seam's own words, that it is not a whole block.
        Assert.Contains(result.Obligations, o =>
            o.Contains("IS NOT A COMPLETE BLOCK", StringComparison.Ordinal)
            && o.Contains("networks 1..18", StringComparison.Ordinal));
    }

    [Fact]
    public void A_FRAGMENT_IS_WRITTEN_UNDER_A_SUBDIRECTORY_AND_NOT_AS_A_DOT_IR_BESIDE_THE_BLOCKS()
    {
        // Every loader that turns an emit directory back into a --program list globs `*.ir`
        // NON-RECURSIVELY. A fragment beside the deployables would be picked up as a block.
        var fragment = Assert.Single(LaneGenerator.Generate(new[] { FullyDeclared() }).Fragments);

        Assert.Equal("stim-shell", StimShellFragment.Subdirectory);
        Assert.Equal("FB_WidgetStim.networks.ir", fragment.FileName);

        // The requirements companion is DERIVED from the shell, so it cannot disagree with the rungs.
        Assert.Contains("UDT MEMBERS (14)", fragment.Requirements(), StringComparison.Ordinal);
        Assert.Contains("STATICS (25)", fragment.Requirements(), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // 🔴 THE NEGATIVE — ABSENT MEANS NOT GENERATED, AND IT IS SAID OUT LOUD
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_SLOT_THAT_DECLARES_NOTHING_GENERATES_NOTHING_AND_IS_REPORTED_AS_AUTHORED()
    {
        // *** THE CASE EVERY EXISTING LANE IS IN. *** It must keep working, and it must not be
        // indistinguishable from a lane whose test side is generated — which is what the origin field
        // exists to answer.
        var result = LaneGenerator.Generate(new[] { new LaneDeclaration("WIDGET") });

        Assert.False(result.Refused);
        Assert.Empty(result.Objects);
        Assert.Empty(result.Fragments);
        Assert.Empty(result.Obligations);

        var slot = Assert.Single(result.Slots);
        Assert.Null(slot.SlotFc);
        Assert.Null(slot.StimShell);

        // TWO lines, not one: the slot FC and the head are separately declarable and separately absent.
        Assert.Equal(2, slot.NotDeclared.Count);
        Assert.All(slot.NotDeclared, line => Assert.Contains("AUTHORED", line, StringComparison.Ordinal));
        Assert.Contains(slot.NotDeclared, l => l.Contains("no slot FC was declared", StringComparison.Ordinal));
        Assert.Contains(slot.NotDeclared, l => l.Contains("no stimulus head spec was declared", StringComparison.Ordinal));
    }

    [Fact]
    public void THE_TWO_HALVES_ARE_INDEPENDENT_so_a_lane_may_generate_its_FC_and_author_its_head()
    {
        var result = LaneGenerator.Generate(new[] { FullyDeclared() with { StimHead = null } });

        Assert.False(result.Refused);
        Assert.Single(result.Objects);
        Assert.Empty(result.Fragments);

        var absent = Assert.Single(Assert.Single(result.Slots).NotDeclared);
        Assert.Contains("stimulus head", absent, StringComparison.Ordinal);
    }

    [Fact]
    public void NO_DECLARATIONS_AT_ALL_IS_NOTHING_rather_than_a_refusal()
    {
        Assert.False(LaneGenerator.Generate(null).Refused);
        Assert.False(LaneGenerator.Generate(Array.Empty<LaneDeclaration>()).Refused);
        Assert.Empty(LaneGenerator.Generate(null).Objects);
    }

    [Fact]
    public void THE_SUMMARY_CARRIES_BOTH_NUMBERS_because_a_bare_count_of_generated_objects_is_not_a_denominator()
    {
        var summary = LaneGenerator.Generate(new[]
        {
            FullyDeclared(),
            new LaneDeclaration("SPARE"),
        }).Summary();

        Assert.Contains("1 slot FC(s) and 1 stimulus shell(s) GENERATED", summary, StringComparison.Ordinal);
        Assert.Contains("2 object(s) NOT generated", summary, StringComparison.Ordinal);
        Assert.Contains("AUTHORED", summary, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // 🔴 EVERY UNCERTAINTY IS A REFUSAL, NOT A GUESS
    // ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("slotFc")]
    [InlineData("head")]
    [InlineData("uut")]
    public void A_PARTIAL_SLOT_FC_DECLARATION_IS_REFUSED_AND_THE_MISSING_HALF_IS_NAMED(string dropped)
    {
        var declaration = dropped switch
        {
            "slotFc" => FullyDeclared() with { SlotFc = null },
            "head" => FullyDeclared() with { StimulusHead = null },
            _ => FullyDeclared() with { BlockUnderTest = null },
        };

        var result = LaneGenerator.Generate(new[] { declaration });

        Assert.True(result.Refused);
        Assert.Contains(result.Refusals, r => r.Contains("only in part", StringComparison.Ordinal));
        Assert.Contains(result.Refusals, r => r.Contains("WIDGET", StringComparison.Ordinal));
    }

    [Fact]
    public void A_REFUSAL_ANYWHERE_YIELDS_NOTHING_ANYWHERE_including_the_slots_that_generated_cleanly()
    {
        // *** A LANE MISSING ITS SLOT FC IS THE ORPHAN. *** Handing back the objects that worked invites a
        // caller to deploy two thirds of a lane, and the third that is missing is the one whose absence
        // the start echo cannot detect.
        var result = LaneGenerator.Generate(new[]
        {
            FullyDeclared("GOOD"),
            FullyDeclared("BAD") with { BlockUnderTest = null },
        });

        Assert.True(result.Refused);
        Assert.Empty(result.Objects);
        Assert.Empty(result.Fragments);
        Assert.Empty(result.Slots);
        Assert.Empty(result.Obligations);
    }

    [Fact]
    public void TWO_SLOTS_EMITTING_ONE_BLOCK_NAME_IS_REFUSED_naming_both()
    {
        // TIA matches an import by NAME, so the second would replace the first and one lane would run the
        // other's calls — with both files present on disk and nothing saying so.
        var result = LaneGenerator.Generate(new[]
        {
            FullyDeclared("ONE"),
            FullyDeclared("TWO") with { SlotFc = new SlotFcNaming("FC_HarnessWidgetSlot", 9011) },
        });

        Assert.True(result.Refused);
        var refusal = Assert.Single(result.Refusals);
        Assert.Contains("ONE", refusal, StringComparison.Ordinal);
        Assert.Contains("TWO", refusal, StringComparison.Ordinal);
        Assert.Contains("FC_HarnessWidgetSlot", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void A_GENERATORS_OWN_REFUSAL_IS_CARRIED_THROUGH_WITH_THE_SLOT_NAMED()
    {
        // Block number zero: hard rule 3 forbids inventing one. The generator's message is the authority
        // and is reproduced verbatim; what the seam adds is WHICH SLOT, which the generator cannot know.
        var result = LaneGenerator.Generate(new[]
        {
            FullyDeclared() with { SlotFc = new SlotFcNaming("FC_HarnessWidgetSlot", 0) },
        });

        Assert.True(result.Refused);
        var refusal = Assert.Single(result.Refusals);
        Assert.Contains("slot 'WIDGET'", refusal, StringComparison.Ordinal);
        Assert.Contains("block number is required", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void A_HEAD_SPEC_THE_SHELL_GENERATOR_REFUSES_IS_A_LANE_REFUSAL_and_names_the_slot()
    {
        // No reset phase: the reset pulse would have no terms, so the head could never clear the block
        // under test and every cleardown outcome would report whatever was already standing.
        var noReset = HeadSpec() with
        {
            Phases = Phases().Select(p => p with { Kind = p.Kind == StimPhaseKind.Reset ? StimPhaseKind.Settle : p.Kind }).ToArray(),
        };

        var result = LaneGenerator.Generate(new[] { FullyDeclared() with { StimHead = noReset } });

        Assert.True(result.Refused);
        Assert.Contains(result.Refusals, r =>
            r.Contains("slot 'WIDGET'", StringComparison.Ordinal)
            && r.Contains("no phase is of kind Reset", StringComparison.Ordinal));
    }
}
