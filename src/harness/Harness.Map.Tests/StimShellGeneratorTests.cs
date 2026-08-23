using Harness.Map;

namespace Harness.Map.Tests;

/// <summary>
/// 🔴 <b>THE DENOMINATOR OF THIS WHOLE FILE, STATED FIRST: every head here is INVENTED.</b>
///
/// <para>The generator was ported under a per-item data-boundary permission covering the MECHANISM ONLY.
/// The real head specs, the blocks they render, and the byte-for-byte verification that measured the port
/// (one head at 18 of 18, the other at 15 of 18, three cosmetic differences enumerated) all stayed in the
/// job folder and are re-run there. <b>So a green run of this file is NOT evidence that the generator
/// reproduces a real head.</b> It is evidence that the shell is internally consistent, that every refusal
/// fires on the input it is for, and that none of them fires on input it is not for.</para>
///
/// <para>A reader who forgets that will over-trust the suite, which is why it is written here, in the
/// plan, and in the commit rather than left to be inferred.</para>
/// </summary>
public class StimShellGeneratorTests
{
    /// <summary>
    /// The minimum shape: <c>disarm, reset, verify | scenario | disarm, reset, verify</c>. Every name is
    /// invented; the four fixed phase bits and two fixed boundaries are the shell's own contract, not
    /// anybody's vocabulary.
    /// </summary>
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

    private static StimHeadSpec Spec(
        IReadOnlyList<StimPhase>? phases = null,
        IReadOnlyList<string>? causes = null,
        IReadOnlyList<string>? cycleEdges = null,
        IReadOnlyList<string>? outcomeBits = null,
        string uutReset = "Widget.Cmd.FaultReset",
        string watchdog = "T#5M",
        string dwell = "T#200MS") =>
        new("WidgetHead", uutReset, watchdog, dwell,
            phases ?? Phases(),
            causes ?? new[] { "Widget.Status.JamLatched", "Widget.Status.OvertravelLatched" },
            cycleEdges ?? new[] { "Widget.Status.StrokeComplete" },
            outcomeBits ?? new[]
            {
                "Stim.ArrivedDirty", "Stim.InertAtStart", "Stim.RecoverFailed",
                "Stim.InertAtEnd", "Stim.CycleCounted", "Stim.ScenarioDone",
            });

    // ---------------------------------------------------------------------------------------------
    // THE GOLDEN SHELL. Asserted whole rather than by fragments: the shell's value is that it is the
    // SAME on every head, and a test that checked a few substrings would not notice a network quietly
    // acquiring or losing a rung.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void The_invented_head_renders_the_whole_eighteen_network_shell()
    {
        var result = StimShellGenerator.Generate(Spec());

        var expected = string.Join("\n", new[]
        {
            "NETWORK 1 \"Index Latch\"",
            "  COIL Running := (Stim.Start OR Running) AND NOT Stim.ScenarioDone",
            "",
            "NETWORK 2 \"Index Timer\"",
            "  TON(RunTimer, IN := Running, PT := T#5M)",
            "",
            "NETWORK 3 \"Elapsed Index Time\"",
            "  MOVE(EN := TRUE, IN := RunTimer.ET) => RunT",
            "",
            "NETWORK 4 \"Cleardown Dwells\"",
            "  MOVE(EN := TRUE, IN := T#200MS) => DisarmDwell",
            "  MOVE(EN := TRUE, IN := T#200MS) => ResetDwell",
            "  MOVE(EN := TRUE, IN := T#200MS) => VerifyDwell",
            "",
            "NETWORK 5 \"Index Phase Boundaries\"",
            "  MOVE(EN := TRUE, IN := DisarmDwell) => HeadDisarmEnd",
            "  ADD(EN := TRUE, IN1 := HeadDisarmEnd, IN2 := ResetDwell) => HeadResetEnd",
            "  ADD(EN := TRUE, IN1 := HeadResetEnd, IN2 := VerifyDwell) => HeadEnd",
            "  ADD(EN := TRUE, IN1 := HeadEnd, IN2 := Stim.EndAt) => ScenEnd",
            "  ADD(EN := TRUE, IN1 := ScenEnd, IN2 := DisarmDwell) => TailDisarmEnd",
            "  ADD(EN := TRUE, IN1 := TailDisarmEnd, IN2 := ResetDwell) => TailResetEnd",
            "  ADD(EN := TRUE, IN1 := TailResetEnd, IN2 := VerifyDwell) => TailEnd",
            "  ADD(EN := TRUE, IN1 := Stim.ResetAt, IN2 := ResetDwell) => ResetPulseEnd",
            "",
            "NETWORK 6 \"Scenario Elapsed Time\"",
            "  MOVE(EN := RunT < HeadEnd, IN := T#0S) => ScenT",
            "  SUB(EN := RunT >= HeadEnd, IN1 := RunT, IN2 := HeadEnd) => ScenT",
            "",
            "NETWORK 7 \"Index Phases\"",
            "  COIL InHeadDisarm := Running AND RunT < HeadDisarmEnd",
            "  COIL InHeadReset := Running AND RunT >= HeadDisarmEnd AND RunT < HeadResetEnd",
            "  COIL InHeadVerify := Running AND RunT >= HeadResetEnd AND RunT < HeadEnd",
            "  COIL InScenario := Running AND RunT >= HeadEnd AND RunT < ScenEnd",
            "  COIL InTailDisarm := Running AND RunT >= ScenEnd AND RunT < TailDisarmEnd",
            "  COIL InTailReset := Running AND RunT >= TailDisarmEnd AND RunT < TailResetEnd",
            "  COIL InTailVerify := Running AND RunT >= TailResetEnd AND RunT < TailEnd",
            "",
            "NETWORK 8 \"Reset Windows\"",
            "  COIL ResetPulse := InHeadReset OR InTailReset",
            "  COIL ScenarioReset := Stim.ResetMode = 2 OR Stim.ResetMode = 1 AND ScenT >= Stim.ResetAt AND ScenT < ResetPulseEnd",
            "",
            "NETWORK 9 \"Drive The Fault Reset\"",
            "  COIL Widget.Cmd.FaultReset := ResetPulse OR InScenario AND ScenarioReset",
            "",
            "NETWORK 10 \"Latched Cause Standing\"",
            "  COIL CauseStanding := Widget.Status.JamLatched OR Widget.Status.OvertravelLatched",
            "",
            "NETWORK 11 \"Arrival Inert Check\"",
            "  SCOIL Stim.ArrivedDirty := InHeadDisarm AND CauseStanding",
            "",
            "NETWORK 12 \"Head Cleardown Outcome\"",
            "  SCOIL Stim.InertAtStart := InHeadVerify AND NOT CauseStanding",
            "  RCOIL Stim.InertAtStart := InHeadVerify AND CauseStanding",
            "",
            "NETWORK 13 \"Recovery Outcome\"",
            "  SCOIL Stim.RecoverFailed := InTailVerify AND CauseStanding",
            "  SCOIL Stim.InertAtEnd := InTailVerify AND NOT CauseStanding",
            "  RCOIL Stim.InertAtEnd := InTailVerify AND CauseStanding",
            "",
            "NETWORK 14 \"Operation Counted\"",
            "  SCOIL Stim.CycleCounted := Widget.Status.StrokeComplete AND Running",
            "",
            "NETWORK 15 \"Observation Window\"",
            "  COIL Stim.Armed := InScenario AND ScenT >= Stim.ArmAt AND ScenT < Stim.ArmUntil",
            "",
            "NETWORK 16 \"Index Complete\"",
            "  SCOIL Stim.ScenarioDone := Running AND RunT >= TailEnd",
            "",
            "NETWORK 17 \"Re-Arm For The Next Index\"",
            "  RCOIL Stim.ArrivedDirty := NOT Stim.Start AND NOT Running",
            "  RCOIL Stim.InertAtStart := NOT Stim.Start AND NOT Running",
            "  RCOIL Stim.RecoverFailed := NOT Stim.Start AND NOT Running",
            "  RCOIL Stim.InertAtEnd := NOT Stim.Start AND NOT Running",
            "  RCOIL Stim.CycleCounted := NOT Stim.Start AND NOT Running",
            "  RCOIL Stim.ScenarioDone := NOT Stim.Start AND NOT Running",
            "",
            "NETWORK 18 \"Published Phase\"",
            "  MOVE(EN := TRUE, IN := 0) => Stim.Phase",
            "  MOVE(EN := InHeadDisarm, IN := 1) => Stim.Phase",
            "  MOVE(EN := InHeadReset, IN := 2) => Stim.Phase",
            "  MOVE(EN := InHeadVerify, IN := 3) => Stim.Phase",
            "  MOVE(EN := InScenario, IN := 4) => Stim.Phase",
            "  MOVE(EN := InTailDisarm, IN := 5) => Stim.Phase",
            "  MOVE(EN := InTailReset, IN := 6) => Stim.Phase",
            "  MOVE(EN := InTailVerify, IN := 7) => Stim.Phase",
            "",
        });

        Assert.Equal(expected, result.Ir);
        Assert.Equal(18, result.Networks.Count);
    }

    /// <summary>
    /// The skeleton ids are stable across heads — "network 12 is the head cleardown outcome" has to be
    /// true of all of them, which is what makes a shared reader possible at all.
    /// </summary>
    [Fact]
    public void Skeleton_ids_are_contiguous_and_in_order()
    {
        var result = StimShellGenerator.Generate(Spec());

        Assert.Equal(Enumerable.Range(1, 18), result.Networks.Select(n => n.Number));
        Assert.Equal("S12", result.Networks.Single(n => n.Number == 12).Id);
        Assert.Equal("Head Cleardown Outcome", result.Networks.Single(n => n.Number == 12).Title);
    }

    /// <summary>
    /// 🔴 <b>The spec's shape propagates without a branch anywhere.</b> A head with a settle phase between
    /// two resets differs from one without on ONE line of its spec, and that difference reaches four
    /// networks consistently. This is the test that the port is an extraction rather than a forcing.
    /// </summary>
    [Fact]
    public void An_extra_phase_propagates_to_four_networks_with_no_special_case()
    {
        var phases = Phases();
        phases.Insert(2, new StimPhase("InHeadSettle", "HeadSettleEnd", "Stim.SettleDwell", StimPhaseKind.Settle));

        var result = StimShellGenerator.Generate(Spec(phases));
        string Rungs(string id) => string.Join("\n", result.Networks.Single(n => n.Id == id).Rungs);

        Assert.Contains("HeadSettleEnd", Rungs("S5"));                       // boundaries
        Assert.Contains("COIL InHeadSettle :=", Rungs("S7"));                // membership
        Assert.Contains("MOVE(EN := InHeadSettle, IN := 3) => Stim.Phase", Rungs("S18"));   // published code
        Assert.DoesNotContain("InHeadSettle", Rungs("S8"));                  // not a reset phase
        Assert.Equal(18, result.Networks.Count);                             // still 18 networks
    }

    /// <summary>
    /// A head that counts nothing OMITS S14 rather than emitting it empty, and the numbering stays
    /// contiguous — so the shell is 17 networks and every id after S14 shifts down by one.
    /// </summary>
    [Fact]
    public void A_head_with_no_counting_edges_omits_S14_entirely_and_stays_contiguous()
    {
        var result = StimShellGenerator.Generate(Spec(
            cycleEdges: Array.Empty<string>(),
            outcomeBits: new[]
            {
                "Stim.ArrivedDirty", "Stim.InertAtStart", "Stim.RecoverFailed",
                "Stim.InertAtEnd", "Stim.ScenarioDone",
            }));

        Assert.Equal(17, result.Networks.Count);
        Assert.DoesNotContain(result.Networks, n => n.Id == "S14");
        Assert.Equal(Enumerable.Range(1, 17), result.Networks.Select(n => n.Number));
        Assert.DoesNotContain("CycleCounted", result.Ir);

        // And the author is told, because a silently absent publication is one vectors would assert on.
        Assert.Contains(result.Obligations, o => o.Contains("NO COUNTING EDGES", StringComparison.Ordinal));
    }

    // --- refusals: each one in BOTH directions ----------------------------------------------------

    /// <summary>Without a reset phase the head can never clear the block, and every verify reports stale.</summary>
    [Fact]
    public void A_phase_list_with_no_RESET_is_refused()
    {
        var phases = Phases().Select(p => p.Kind == StimPhaseKind.Reset ? p with { Kind = StimPhaseKind.Disarm } : p).ToList();

        var error = Assert.Throws<ArgumentException>(() => StimShellGenerator.Generate(Spec(phases)));
        Assert.Contains("could never clear", error.Message);

        // The same list with one kind put back renders.
        phases[1] = phases[1] with { Kind = StimPhaseKind.Reset };
        Assert.NotEmpty(StimShellGenerator.Generate(Spec(phases)).Ir);
    }

    /// <summary>
    /// 🔴 <b>The most damaging authoring error available, and it was previously prevented by a sentence in
    /// a README.</b> A bit the shell SETS and S17 never clears reads as set on every index after the first,
    /// and nothing in a green run says so. Computed from what the shell actually sets, so it cannot drift.
    /// </summary>
    [Fact]
    public void An_outcome_bit_the_shell_sets_but_never_clears_is_refused()
    {
        var missing = new[]
        {
            "Stim.ArrivedDirty", "Stim.InertAtStart", "Stim.RecoverFailed",
            "Stim.CycleCounted", "Stim.ScenarioDone",   // Stim.InertAtEnd omitted
        };

        var error = Assert.Throws<ArgumentException>(() => StimShellGenerator.Generate(Spec(outcomeBits: missing)));
        Assert.Contains("Stim.InertAtEnd", error.Message);
        Assert.Contains("never clear", error.Message);

        // Put it back and the same spec renders — the refusal is about that one bit, not about the list.
        Assert.NotEmpty(StimShellGenerator.Generate(Spec(outcomeBits: missing.Append("Stim.InertAtEnd").ToArray())).Ir);
    }

    /// <summary>A head that publishes nothing cannot be observed: every vector would read the same thing.</summary>
    [Fact]
    public void An_empty_outcome_bit_list_is_refused()
    {
        var error = Assert.Throws<ArgumentException>(
            () => StimShellGenerator.Generate(Spec(outcomeBits: Array.Empty<string>())));

        Assert.Contains("cannot be observed", error.Message);
    }

    /// <summary>
    /// 🔴 <b>An empty CAUSE list is ALLOWED and REPORTED.</b> A block that latches nothing is legitimate;
    /// an unfinished list looks identical, and only the author can say which. Refusing would be wrong and
    /// staying silent would be worse — it reads as a clean bill of health, in green, forever.
    /// </summary>
    [Fact]
    public void An_empty_cause_list_renders_and_is_REPORTED_rather_than_refused()
    {
        var result = StimShellGenerator.Generate(Spec(causes: Array.Empty<string>()));

        Assert.Contains("COIL CauseStanding := FALSE", result.Ir);
        Assert.Contains(result.Obligations, o => o.Contains("NO CAUSES WERE DECLARED", StringComparison.Ordinal));
        Assert.Contains(result.Obligations, o => o.Contains("in green, forever", StringComparison.Ordinal));
    }

    /// <summary>
    /// 🔴 <b>The fixed names the shell writes into its own rungs must be declared.</b> S6 reads
    /// <c>HeadEnd</c>, S16 reads <c>TailEnd</c>, and six networks read the four fixed phase bits — so a
    /// head that names its phases otherwise emits IR pointing at statics that do not exist.
    /// </summary>
    [Theory]
    [InlineData("InScenario")]
    [InlineData("InHeadDisarm")]
    [InlineData("InHeadVerify")]
    [InlineData("InTailVerify")]
    public void A_spec_missing_a_fixed_PHASE_BIT_is_refused_naming_it(string bit)
    {
        var phases = Phases().Select(p => p.Bit == bit ? p with { Bit = "Renamed" } : p).ToList();

        var error = Assert.Throws<ArgumentException>(() => StimShellGenerator.Generate(Spec(phases)));
        Assert.Contains(bit, error.Message);
    }

    [Theory]
    [InlineData("HeadEnd")]
    [InlineData("TailEnd")]
    public void A_spec_missing_a_fixed_BOUNDARY_is_refused_naming_it(string boundary)
    {
        var phases = Phases().Select(p => p.End == boundary ? p with { End = "Renamed" } : p).ToList();

        var error = Assert.Throws<ArgumentException>(() => StimShellGenerator.Generate(Spec(phases)));
        Assert.Contains(boundary, error.Message);
    }

    /// <summary>
    /// 🔴 <b>S9 is the only writer of the block's fault reset — by construction, not by convention.</b>
    /// Naming it anywhere else in the spec means another emitted network drives it too.
    /// </summary>
    [Fact]
    public void A_uut_reset_that_is_also_shell_state_is_refused()
    {
        var error = Assert.Throws<ArgumentException>(
            () => StimShellGenerator.Generate(Spec(uutReset: "Stim.ArrivedDirty")));

        Assert.Contains("S9 is its only writer", error.Message);
    }

    /// <summary>A bare name resolves against the head's OWN statics, which compiles and observes the wrong thing.</summary>
    [Theory]
    [InlineData("JamLatched")]
    [InlineData("Widget.")]
    [InlineData("")]
    public void A_cause_that_is_not_a_full_member_path_is_refused(string cause)
    {
        var error = Assert.Throws<ArgumentException>(() => StimShellGenerator.Generate(Spec(causes: new[] { cause })));
        Assert.Contains("full member path", error.Message);
    }

    [Fact]
    public void A_cycle_edge_that_is_not_a_full_member_path_is_refused()
    {
        Assert.Throws<ArgumentException>(() => StimShellGenerator.Generate(Spec(cycleEdges: new[] { "StrokeComplete" })));
    }

    /// <summary>Two phases sharing a membership bit merge two phases silently — every later network conflates them.</summary>
    [Fact]
    public void Two_phases_sharing_a_membership_bit_are_refused()
    {
        var phases = Phases();
        phases[5] = phases[5] with { Bit = "InHeadReset" };

        var error = Assert.Throws<ArgumentException>(() => StimShellGenerator.Generate(Spec(phases)));
        Assert.Contains("share the membership bit", error.Message);
    }

    /// <summary>A literal duration means the index can only be re-timed by regenerating the block.</summary>
    [Fact]
    public void A_phase_duration_given_as_a_literal_is_refused()
    {
        var phases = Phases();
        phases[0] = phases[0] with { Duration = "T#500MS" };

        var error = Assert.Throws<ArgumentException>(() => StimShellGenerator.Generate(Spec(phases)));
        Assert.Contains("not a literal", error.Message);
    }

    [Theory]
    [InlineData("5M")]
    [InlineData("")]
    public void A_watchdog_that_is_not_a_Time_literal_is_refused(string watchdog)
    {
        Assert.Throws<ArgumentException>(() => StimShellGenerator.Generate(Spec(watchdog: watchdog)));
    }

    /// <summary>
    /// 🔴 <b>THE NEGATIVE CONTROL for fifteen refusal tests.</b> A generator that threw on everything would
    /// pass all of them. The ordinary head must render, and must owe the author the things only they can do.
    /// </summary>
    [Fact]
    public void An_ordinary_head_renders_and_states_what_the_author_still_owes()
    {
        var result = StimShellGenerator.Generate(Spec());

        Assert.Equal(18, result.Networks.Count);
        Assert.Contains(result.Obligations, o => o.Contains("WRITE THE COMMENTS", StringComparison.Ordinal));
        Assert.Contains(result.Obligations, o => o.Contains("THE DISARM LEVER", StringComparison.Ordinal));
        Assert.Contains(result.Obligations, o => o.Contains("STALENESS QUESTION", StringComparison.Ordinal));

        // The statics and UDT members it references but does not create.
        Assert.Contains("CauseStanding", result.RequiredStatics);
        Assert.Contains("InHeadDisarm", result.RequiredStatics);
        Assert.Contains("CycleCounted", result.RequiredUdtMembers);
    }

    /// <summary>Without counting edges the UDT no longer requires the member that only S14 publishes.</summary>
    [Fact]
    public void CycleCounted_is_only_required_when_something_counts()
    {
        var result = StimShellGenerator.Generate(Spec(
            cycleEdges: Array.Empty<string>(),
            outcomeBits: new[]
            {
                "Stim.ArrivedDirty", "Stim.InertAtStart", "Stim.RecoverFailed",
                "Stim.InertAtEnd", "Stim.ScenarioDone",
            }));

        Assert.DoesNotContain("CycleCounted", result.RequiredUdtMembers);
    }
}
