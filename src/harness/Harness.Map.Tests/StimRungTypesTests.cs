using Harness.Map;

namespace Harness.Map.Tests;

/// <summary>
/// 🔴 <b>THE ONE FAILURE A TYPE INFERENCE MUST NOT HAVE: typing what it understood and staying silent
/// about the rest.</b>
///
/// <para><see cref="IrMemberLine"/> holds the same discipline for the same reason — it is a second copy of
/// a grammar the converter owns, and <i>"every method here either recognises a line completely or
/// throws"</i>. <see cref="StimRungTypes"/> is a reader of a grammar <see cref="StimShellGenerator"/>
/// owns, one file over, and a shell that grows a rung shape this reader has not been taught must surface
/// as a REFUSAL naming the rung — never as a UDT member given a confident wrong type.</para>
///
/// <para>These tests drive <see cref="StimRungTypes.Infer"/> directly with hand-built networks, which is
/// the only way to exercise a rung the current shell cannot emit.</para>
/// </summary>
public class StimRungTypesTests
{
    private static IReadOnlyList<StimShellNetwork> One(params string[] rungs) =>
        new[] { new StimShellNetwork("S1", 1, "Under Test", rungs) };

    [Fact]
    public void A_COIL_TARGET_IS_BOOL_AND_SO_IS_EVERY_ATOM_OF_ITS_EXPRESSION()
    {
        var types = StimRungTypes.Infer(One("COIL Running := (Stim.Start OR Running) AND NOT Stim.ScenarioDone"))
            .ToDictionary(o => o.Name, o => o.Datatype, StringComparer.Ordinal);

        Assert.Equal("Bool", types["Running"]);
        Assert.Equal("Bool", types["Stim.Start"]);
        Assert.Equal("Bool", types["Stim.ScenarioDone"]);
    }

    /// <summary>A comparison unifies both sides; the Time seed can arrive from either one, at any distance.</summary>
    [Fact]
    public void TIME_PROPAGATES_ACROSS_COMPARISONS_AND_ARITHMETIC()
    {
        var types = StimRungTypes.Infer(One(
                "MOVE(EN := TRUE, IN := RunTimer.ET) => RunT",
                "SUB(EN := RunT >= HeadEnd, IN1 := RunT, IN2 := HeadEnd) => ScenT",
                "COIL Stim.Armed := ScenT >= Stim.ArmAt AND ScenT < Stim.ArmUntil"))
            .ToDictionary(o => o.Name, o => o.Datatype, StringComparer.Ordinal);

        Assert.Equal("Time", types["ScenT"]);
        Assert.Equal("Time", types["Stim.ArmAt"]);
        Assert.Equal("Time", types["Stim.ArmUntil"]);
        Assert.Equal("Bool", types["Stim.Armed"]);
    }

    /// <summary>
    /// 🔴 <b>Two operands compared against integer literals do NOT become the same type.</b> A single
    /// shared "integer" node would unify every such operand into one set, and a contradiction anywhere
    /// would then be reported against all of them.
    /// </summary>
    [Fact]
    public void INTEGER_LITERALS_DO_NOT_UNIFY_UNRELATED_OPERANDS()
    {
        var types = StimRungTypes.Infer(One(
                "MOVE(EN := TRUE, IN := 0) => Stim.Phase",
                "COIL Gate := Stim.ResetMode = 2"))
            .ToDictionary(o => o.Name, o => o, StringComparer.Ordinal);

        Assert.Equal(StimTypeClass.Integer, types["Stim.Phase"].Class);
        Assert.Equal(StimTypeClass.Integer, types["Stim.ResetMode"].Class);
        Assert.Null(types["Stim.Phase"].Datatype);
        Assert.Null(types["Stim.ResetMode"].Datatype);
    }

    [Fact]
    public void AN_UNRECOGNISED_INSTRUCTION_IS_REFUSED_NOT_SKIPPED()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            StimRungTypes.Infer(One("CTU(Counter, CU := Running, PV := 5) => Stim.Count")));

        Assert.Contains("does not fully understand", error.Message, StringComparison.Ordinal);
        Assert.Contains("`CTU`", error.Message, StringComparison.Ordinal);
        Assert.Contains("REFUSED RATHER THAN PARTIALLY READ", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AN_UNREADABLE_OPERAND_IS_REFUSED_NOT_SKIPPED()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            StimRungTypes.Infer(One("COIL Stim.Armed := %M100.0")));

        Assert.Contains("is not an operand this inference can read", error.Message, StringComparison.Ordinal);
    }

    /// <summary>Two seeds meeting in one set is a throw, not a winner.</summary>
    [Fact]
    public void RUNGS_THAT_CONTRADICT_EACH_OTHER_ARE_REFUSED()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            StimRungTypes.Infer(One(
                "COIL Stim.Armed := Running",
                "MOVE(EN := TRUE, IN := T#5S) => Stim.Armed")));

        Assert.Contains("CONTRADICT", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 <b>THE WHOLE OF THE SHELL GOES THROUGH IT.</b> Not a sample: every rung of every network the
    /// generator emits for a head that exercises each optional network. A rung the inference cannot read
    /// throws, so this passing IS the statement that the reader covers the emitter today.
    /// </summary>
    [Fact]
    public void EVERY_RUNG_THE_SHELL_EMITS_IS_UNDERSTOOD()
    {
        var shell = StimShellGenerator.Generate(new StimHeadSpec(
            "FB_WidgetJamStim", "Uut.FaultReset", "T#600S", "T#2S",
            new[]
            {
                new StimPhase("InHeadDisarm", "HeadDisarmEnd", "DisarmDwell", StimPhaseKind.Disarm),
                new StimPhase("InHeadReset", "HeadResetEnd", "ResetDwell", StimPhaseKind.Reset),
                new StimPhase("InHeadVerify", "HeadEnd", "VerifyDwell", StimPhaseKind.Verify),
                new StimPhase("InScenario", "ScenarioEnd", "Stim.RunFor", StimPhaseKind.Scenario),
                new StimPhase("InTailVerify", "TailEnd", "VerifyDwell", StimPhaseKind.Verify),
            },
            new[] { "Uut.JamAlarmLatched" },
            new[] { "Uut.WidgetPassed", "Uut.WidgetRejected" },
            new[]
            {
                "Stim.ArrivedDirty", "Stim.InertAtStart", "Stim.RecoverFailed",
                "Stim.InertAtEnd", "Stim.ScenarioDone", "Stim.CycleCounted",
            }));

        var types = StimRungTypes.Infer(shell.Networks);

        Assert.Equal(18, shell.Networks.Count);
        Assert.NotEmpty(types);

        // Nothing the shell references is left Unknown: every operand is reached by at least one seed.
        var unknown = types.Where(t => t.Class == StimTypeClass.Unknown).Select(t => t.Name).ToArray();
        Assert.True(unknown.Length == 0, "unconstrained operands: " + string.Join(", ", unknown));
    }
}
