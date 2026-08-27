using Harness.Map;

namespace Harness.Map.Tests;

/// <summary>
/// 🔴 <b>THE STIMULUS UDT: WHAT THE RUNGS PROVE IS EMITTED, AND WHAT THEY DO NOT IS REFUSED.</b>
///
/// <para>The head below is wholly INVENTED — a widget rig with a jam detector — and it is invented on
/// purpose: <see cref="StimShellGenerator"/>'s own header records that the shell was ported under a
/// per-item data-boundary permission, MECHANISM ONLY, and that <b>a green committed suite is not evidence
/// that it reproduces a real head</b>. The same holds here. What these tests DO establish is that the
/// inference is exact about its own emitted text, and
/// <see cref="THE_DERIVED_TYPES_AGREE_WITH_THE_COMMITTED_STIMULUS_UDT"/> checks it against a
/// hand-authored type that has been through TIA.</para>
/// </summary>
public class StimUdtGeneratorTests
{
    private static StimHeadSpec Head() => new(
        HeadName: "FB_WidgetJamStim",
        UutReset: "Uut.FaultReset",
        Watchdog: "T#600S",
        Dwell: "T#2S",
        Phases: new[]
        {
            new StimPhase("InHeadDisarm", "HeadDisarmEnd", "DisarmDwell", StimPhaseKind.Disarm),
            new StimPhase("InHeadReset", "HeadResetEnd", "ResetDwell", StimPhaseKind.Reset),
            new StimPhase("InHeadVerify", "HeadEnd", "VerifyDwell", StimPhaseKind.Verify),

            // 🔴 A PHASE WHOSE DURATION IS A UDT MEMBER. The shell's own hand-written
            // `RequiredUdtMembers` list does not mention phase durations at all, so this member is one the
            // list cannot know about and the rungs cannot miss.
            new StimPhase("InScenario", "ScenarioEnd", "Stim.RunFor", StimPhaseKind.Scenario),

            new StimPhase("InTailDisarm", "TailDisarmEnd", "DisarmDwell", StimPhaseKind.Disarm),
            new StimPhase("InTailReset", "TailResetEnd", "ResetDwell", StimPhaseKind.Reset),
            new StimPhase("InTailVerify", "TailEnd", "VerifyDwell", StimPhaseKind.Verify),
        },
        Causes: new[] { "Uut.JamAlarmLatched" },
        CycleEdges: new[] { "Uut.WidgetPassed" },
        OutcomeBits: new[]
        {
            "Stim.ArrivedDirty", "Stim.InertAtStart", "Stim.RecoverFailed",
            "Stim.InertAtEnd", "Stim.ScenarioDone", "Stim.CycleCounted",
        });

    private static StimShellResult Shell() => StimShellGenerator.Generate(Head());

    private static StimUdtNaming Naming() => new(
        "UDT_WidgetJamStim",
        "Command and published state of the widget-jam stimulus model. Synthetic test material.");

    /// <summary>The two widths the rungs cannot fix, plus one member the shell never references.</summary>
    private static IReadOnlyList<StimUdtMember> DeclaredHalf() => new[]
    {
        new StimUdtMember("Phase", "Int"),
        new StimUdtMember("ResetMode", "Int"),
        new StimUdtMember("Jam", "Bool", Comment: "Out: commanded jam sensor state."),
    };

    // --- what the rungs prove -------------------------------------------------------------------------

    [Fact]
    public void THE_BOOLS_AND_THE_TIMES_ARE_DERIVED_FROM_THE_EMITTED_RUNGS()
    {
        var result = StimUdtGenerator.Generate(new StimUdtDeclaration(Naming(), DeclaredHalf()), Shell());
        var derived = result.Members.Where(m => m.Derived).ToDictionary(m => m.Name, m => m.Datatype);

        foreach (var boolMember in new[]
                 {
                     "Start", "ScenarioDone", "Armed",
                     "ArrivedDirty", "InertAtStart", "InertAtEnd", "RecoverFailed", "CycleCounted",
                 })
        {
            Assert.Equal("Bool", derived[boolMember]);
        }

        foreach (var timeMember in new[] { "ResetAt", "ArmAt", "ArmUntil" })
            Assert.Equal("Time", derived[timeMember]);
    }

    /// <summary>
    /// 🔴 <b>A UDT MEMBER USED AS A PHASE DURATION IS DERIVED <c>Time</c> — through THREE rungs across two
    /// networks, which is exactly what a type table gets wrong.</b> S5 moves it into the phase boundary,
    /// S6 compares that boundary against <c>RunT</c>, and S3 moves <c>RunT</c> from the run timer's
    /// <c>.ET</c>. Nothing declares it; the chain does.
    /// </summary>
    [Fact]
    public void A_PHASE_DURATION_MEMBER_IS_DERIVED_TIME_THROUGH_THE_BOUNDARY_CHAIN()
    {
        var result = StimUdtGenerator.Generate(new StimUdtDeclaration(Naming(), DeclaredHalf()), Shell());
        var runFor = result.Members.Single(m => m.Name == "RunFor");

        Assert.True(runFor.Derived);
        Assert.Equal("Time", runFor.Datatype);

        // And the shell's own hand-written requirement list does not know it exists.
        Assert.DoesNotContain("RunFor", Shell().RequiredUdtMembers);
    }

    // --- what they do not ------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>THE CENTRAL REFUSAL. An integer literal in a rung proves an integer and not its width.</b>
    /// Both members are named in ONE refusal, and it says what the rungs did establish so the reader can
    /// see the working rather than a verdict.
    /// </summary>
    [Fact]
    public void A_WIDTH_THE_RUNGS_DO_NOT_FIX_IS_REFUSED_AND_NOT_GUESSED()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            StimUdtGenerator.Generate(new StimUdtDeclaration(Naming()), Shell()));

        Assert.Contains("'Phase'", error.Message, StringComparison.Ordinal);
        Assert.Contains("'ResetMode'", error.Message, StringComparison.Ordinal);
        Assert.Contains("an INTEGER of unstated width", error.Message, StringComparison.Ordinal);
        Assert.Contains("`Int` is not a safer guess", error.Message, StringComparison.Ordinal);

        // Not the first one and then the next: both, in one message.
        Assert.Contains("2 member(s)", error.Message, StringComparison.Ordinal);
    }

    /// <summary>A declared type that CONTRADICTS the rungs is refused, not honoured.</summary>
    [Fact]
    public void A_DECLARED_TYPE_THAT_CONTRADICTS_THE_RUNGS_IS_REFUSED()
    {
        var declaration = new StimUdtDeclaration(
            Naming(), DeclaredHalf().Append(new StimUdtMember("Armed", "Int")).ToArray());

        var error = Assert.Throws<ArgumentException>(() => StimUdtGenerator.Generate(declaration, Shell()));

        Assert.Contains("declares 'Armed : Int'", error.Message, StringComparison.Ordinal);
        Assert.Contains("make it Bool", error.Message, StringComparison.Ordinal);
    }

    /// <summary>A member the shell never references and that carries no declared type has nothing behind it.</summary>
    [Fact]
    public void A_MEMBER_THE_SHELL_NEVER_REFERENCES_NEEDS_A_DECLARED_TYPE()
    {
        var declaration = new StimUdtDeclaration(
            Naming(), DeclaredHalf().Append(new StimUdtMember("Conveyor")).ToArray());

        var error = Assert.Throws<ArgumentException>(() => StimUdtGenerator.Generate(declaration, Shell()));

        Assert.Contains("'Conveyor' with no datatype", error.Message, StringComparison.Ordinal);
        Assert.Contains("authored members carry authored types", error.Message, StringComparison.Ordinal);
    }

    /// <summary>A start value is never originated, and the report says which way it went.</summary>
    [Fact]
    public void NO_START_VALUE_IS_ORIGINATED_AND_THE_ABSENCE_IS_REPORTED()
    {
        var result = StimUdtGenerator.Generate(new StimUdtDeclaration(Naming(), DeclaredHalf()), Shell());

        Assert.DoesNotContain(" = ", result.Ir, StringComparison.Ordinal);
        Assert.Contains(result.Obligations, o => o.Contains("NO START VALUE WAS DECLARED", StringComparison.Ordinal));

        var withPreset = StimUdtGenerator.Generate(
            new StimUdtDeclaration(Naming(), DeclaredHalf().Append(new StimUdtMember("ArmAt", StartValue: "T#5S")).ToArray()),
            Shell());

        Assert.Contains("    ArmAt : Time = T#5S\n", withPreset.Ir, StringComparison.Ordinal);
        Assert.Contains(withPreset.Obligations, o => o.Contains("CARRY A DECLARED START VALUE", StringComparison.Ordinal));
    }

    // --- the two lists that could disagree, and now cannot silently ------------------------------------

    /// <summary>
    /// 🔴 <b>THE SHELL'S HAND-WRITTEN `RequiredUdtMembers` AND ITS OWN RUNGS DISAGREE, IN BOTH
    /// DIRECTIONS — and until this generator existed nothing said so.</b>
    ///
    /// <para><c>EndAt</c> is in that array and no emitted rung mentions it. <c>RunFor</c> is referenced by
    /// S5 and is not in the array. The array is a literal beside the networks; this derivation is read out
    /// of the emitted text.</para>
    /// </summary>
    [Fact]
    public void THE_SHELLS_OWN_REQUIREMENT_LIST_IS_RECONCILED_AGAINST_THE_RUNGS()
    {
        var result = StimUdtGenerator.Generate(new StimUdtDeclaration(Naming(), DeclaredHalf()), Shell());

        Assert.Contains(result.Obligations, o =>
            o.Contains("NO EMITTED RUNG REFERENCES", StringComparison.Ordinal) && o.Contains("EndAt", StringComparison.Ordinal));

        Assert.Contains(result.Obligations, o =>
            o.Contains("ABSENT FROM THAT LIST", StringComparison.Ordinal) && o.Contains("RunFor", StringComparison.Ordinal));

        Assert.DoesNotContain("EndAt", result.Ir);
    }

    /// <summary>
    /// 🔴 <b>THE INFERENCE, AGAINST A HAND-AUTHORED TYPE THAT HAS BEEN THROUGH TIA.</b>
    ///
    /// <para><c>ir/test-project001/UDT_HopperBlockageStim.ir</c> was typed by a person for a head of a
    /// different design, so only its MECHANISM members overlap with what the shell emits. On every one of
    /// those the derivation agrees — and on <c>ResetMode</c> it does not agree, it REFUSES, and the
    /// committed file's answer (<c>Int</c>) is exactly the width choice this component declines to
    /// make.</para>
    /// </summary>
    [Fact]
    public void THE_DERIVED_TYPES_AGREE_WITH_THE_COMMITTED_STIMULUS_UDT()
    {
        var committed = File.ReadAllLines(CommsFbAgainstTheCommittedCorpusTests.CorpusPath("UDT_HopperBlockageStim.ir"))
            .Select(l => l.Trim())
            .Where(l => l.Contains(" : ", StringComparison.Ordinal))
            .Select(l => l.Split(" : ", 2))
            .ToDictionary(p => p[0], p => p[1].Split(' ')[0], StringComparer.Ordinal);

        var inferred = StimRungTypes.Infer(Shell().Networks)
            .Where(o => StimRungTypes.StimMemberName(o.Name) is not null)
            .ToDictionary(o => StimRungTypes.StimMemberName(o.Name)!, o => o, StringComparer.Ordinal);

        var overlapping = committed.Keys.Where(inferred.ContainsKey).ToArray();
        Assert.True(overlapping.Length >= 6, $"expected the mechanism members to overlap; found {overlapping.Length}.");

        foreach (var member in overlapping)
        {
            var derived = inferred[member];

            if (derived.Datatype is null)
            {
                // The rungs prove only an integer. The committed file states a width; this component
                // refuses to. That is the disagreement, and it is the intended one.
                Assert.Equal(StimTypeClass.Integer, derived.Class);
                Assert.Equal("Int", committed[member]);
                continue;
            }

            Assert.Equal(committed[member], derived.Datatype);
        }
    }

    // --- the emitted text ------------------------------------------------------------------------------

    [Fact]
    public void THE_EMITTED_TYPE_IS_A_TYPE_IR_DOCUMENT()
    {
        var result = StimUdtGenerator.Generate(new StimUdtDeclaration(Naming(), DeclaredHalf()), Shell());
        var lines = result.Ir.Split('\n');

        Assert.Equal("TYPE UDT_WidgetJamStim", lines[0]);
        Assert.Equal("  ROOTID 0", lines[1]);
        Assert.StartsWith("  COMMENT \"", lines[2]);
        Assert.Equal("  MEMBERS", lines[3]);
        Assert.All(lines.Skip(4).Where(l => l.Length > 0), l => Assert.StartsWith("    ", l));

        // Derived members first, in first-reference order; the declared-only member last.
        Assert.Equal("Start", result.Members[0].Name);
        Assert.Equal("Jam", result.Members[^1].Name);
        Assert.Contains("    Jam : Bool COMMENT \"Out: commanded jam sensor state.\"\n", result.Ir, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 <b>THE SECOND AUTHORITY: the emitted type is a document the real converter accepts.</b> The
    /// component's own tests compare the generator against what its author expected; this one does not.
    /// </summary>
    [Fact]
    public void THE_GENERATED_TYPE_CONVERTS_TO_SIMATICML()
    {
        var work = Directory.CreateTempSubdirectory("stimudt-roundtrip-");
        try
        {
            var result = StimUdtGenerator.Generate(new StimUdtDeclaration(Naming(), DeclaredHalf()), Shell());
            var path = Path.Combine(work.FullName, result.TypeName + ".ir");
            File.WriteAllText(path, result.Ir);

            var (exit, output) = ConverterProcess.Run("to-xml", path);

            Assert.True(exit == 0, $"converter to-xml refused the generated UDT (exit {exit}): {output}");
            Assert.True(File.Exists(Path.ChangeExtension(path, ".xml")),
                "converter to-xml reported success and wrote no XML.");
        }
        finally
        {
            work.Delete(recursive: true);
        }
    }

    /// <summary>Members with no declared comment are named, because the type is not finished without them.</summary>
    [Fact]
    public void UNCOMMENTED_MEMBERS_ARE_NAMED_RATHER_THAN_GIVEN_A_WRITTEN_ONE()
    {
        var result = StimUdtGenerator.Generate(new StimUdtDeclaration(Naming(), DeclaredHalf()), Shell());

        var owed = Assert.Single(result.Obligations.Where(o => o.StartsWith("WRITE ", StringComparison.Ordinal)));
        Assert.Contains("Start", owed, StringComparison.Ordinal);
        Assert.DoesNotContain("Jam,", owed, StringComparison.Ordinal);
        Assert.Contains("NOT equivalent to a hand-authored one", owed, StringComparison.Ordinal);
    }
}
