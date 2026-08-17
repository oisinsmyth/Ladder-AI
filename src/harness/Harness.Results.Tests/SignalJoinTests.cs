using Harness.Map;
using Harness.Results;

namespace Harness.Results.Tests;

/// <summary>
/// 🔴 <b>THE SECOND JOIN BETWEEN A SUBMISSION AND A BINDING — the SIGNAL names — which had no check at
/// all until 2026-08-17.</b>
///
/// <para>Written against the shape that was measured on the first live wave: the binding states the
/// specification's name as <c>specName</c> beside the block's own tag, and the vector cites the
/// specification's name.</para>
/// </summary>
public class SignalJoinTests
{
    private const string Tag = "iDB_Widget.IO.Fault";
    private const string SpecName = "SPEC.FaultAlarm";

    private static SlotBinding Binding(params MirroredSignal[] results) =>
        new("WGT", Array.Empty<MirroredSignal>(), "iDB_Model.Phase.Start", results)
        {
            Serves = new[] { "SLOT-VALVE-CLASS" },
        };

    private static MirroredSignal Result(string tag, string? spec = null) =>
        new(tag, MirrorValueType.Bool, SpecName: spec);

    private static SubmissionVector Vector(
        string id = "VJ-WGT-001",
        string slot = "SLOT-VALVE-CLASS",
        string? expectation = SpecName,
        string completion = "SPEC.ScenarioDone") =>
        new(id, slot, 0, new AgentIdentity("author"),
            new Basis("SPEC-3.3-W1", "SPEC-3.3-W1:beb55b"),
            new Dictionary<string, string>(StringComparer.Ordinal),
            "WGT_Start_Class",
            expectation is null
                ? Array.Empty<ObservabilityDeclaration>()
                : new[] { new ObservabilityDeclaration(expectation, SignalNature.PersistentState, InstrumentationMode.Latched, 10, "false") },
            AssertionForm.Never,
            new SettlingDeclaration("stub", Array.Empty<string>()),
            MaxDurationScans: 100,
            CompletionValue: 1,
            Array.Empty<BlacklistEntry>(),
            CompressionFactor: 1,
            AssertedBehaviours: new[] { "b" },
            CompletionSignal: completion,
            Kills: null);

    private static SignalJoinReport Check(SubmissionVector vector, SlotBinding binding) =>
        SignalJoin.Check(new[] { vector }, slot => binding.CitableSlotIds.Contains(slot, StringComparer.Ordinal) ? binding : null);

    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_VECTOR_CITING_THE_SPEC_NAME_JOINS_and_the_report_states_its_DENOMINATOR()
    {
        var report = Check(
            Vector(),
            Binding(Result(Tag, SpecName), Result("iDB_Model.Phase.ScenarioDone", "SPEC.ScenarioDone")));

        Assert.False(report.Any);

        // *** THE DENOMINATOR, ON THE CLEAN LINE. *** One expectation plus one completion signal. A clean
        // verdict that does not say how many citations it examined cannot be told from one that examined
        // none.
        Assert.Equal(2, report.Examined);
        Assert.Contains("2 cited signal(s)", report.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void AN_EXPECTATION_CITING_THE_TAG_INSTEAD_OF_THE_SPEC_NAME_IS_REFUSED_AND_BOTH_SIDES_ARE_NAMED()
    {
        // The converse mistake, and it must fail too: once the binding STATES a spec name, the tag is not
        // an alternative key. A lookup that tried both would silently accept two vocabularies for one
        // signal and make the `specName` declaration decorative.
        var report = Check(
            Vector(expectation: Tag),
            Binding(Result(Tag, SpecName), Result("iDB_Model.Phase.ScenarioDone", "SPEC.ScenarioDone")));

        Assert.True(report.Any);
        Assert.Equal(1, report.Signals);

        // The reader has to decide which of two documents is wrong, so the refusal names what was cited
        // AND what is carried.
        Assert.Contains(Tag, report.Detail, StringComparison.Ordinal);
        Assert.Contains($"'{SpecName}'", report.Detail, StringComparison.Ordinal);
        Assert.Contains("VJ-WGT-001", report.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_COMPLETION_SIGNAL_THAT_JOINS_TO_NOTHING_IS_REFUSED_AND_THE_REFUSAL_NAMES_THE_REGISTER_ZERO_HAZARD()
    {
        // *** THE MEASURED LIVE-WAVE CASE. *** `SPEC.Scenario_Done` matched neither the tag
        // (`iDB_Model.Phase.ScenarioDone`) nor the spec name (`SPEC.ScenarioDone`), and the loop
        // defaulted the completion register to 0. That register held the stimulus model's PHASE code, and
        // phase 1 equals the completion value 1, so a 41-second scenario was declared complete 8 scans in.
        var report = Check(
            Vector(completion: "SPEC.Scenario_Done"),
            Binding(Result(Tag, SpecName), Result("iDB_Model.Phase.ScenarioDone", "SPEC.ScenarioDone")));

        Assert.True(report.Any);
        Assert.Contains(report.Unjoined, u => u.Role == SignalRole.Completion && u.Signal == "SPEC.Scenario_Done");

        // The completion case gets its own sentence, because the remedy differs: an unjoined EXPECTATION
        // yields silence, an unjoined COMPLETION yields a confident early finish.
        Assert.Contains("register 0", report.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_BINDING_THAT_STATES_NO_SPEC_NAME_STILL_JOINS_ON_ITS_TAG()
    {
        // The unaffected case. JoinKey falls back to the tag where the binding never stated a spec name,
        // and a check that refused those would fire on every binding written the older way.
        var report = Check(
            Vector(expectation: Tag, completion: "iDB_Model.Phase.ScenarioDone"),
            Binding(Result(Tag), Result("iDB_Model.Phase.ScenarioDone")));

        Assert.False(report.Any);
        Assert.Equal(2, report.Examined);
    }

    [Fact]
    public void A_CARRIED_SIGNAL_THAT_NO_VECTOR_CITES_IS_NOT_A_FINDING()
    {
        // Deliberately one-sided, exactly as SlotJoin is. A binding publishes diagnostic registers on
        // purpose — the live binding carried twenty and its vectors cited four — and refusing the unused ones would
        // fire far outside this check's scope.
        var report = Check(
            Vector(),
            Binding(
                Result(Tag, SpecName),
                Result("iDB_Model.Phase.ScenarioDone", "SPEC.ScenarioDone"),
                Result("iDB_Widget.IO.Release", "SPEC.Release"),
                Result("iDB_Widget.IO.ExtFault", "SPEC.ExtFault")));

        Assert.False(report.Any);
    }

    [Fact]
    public void A_VECTOR_WHOSE_SLOT_IS_UNBOUND_IS_SKIPPED_because_that_is_SlotJoins_finding_and_not_this_ones()
    {
        // Reporting it twice under two names sends the reader hunting for a signal problem that does not
        // exist. SlotJoin runs first and refuses it by name.
        var report = SignalJoin.Check(new[] { Vector(slot: "SLOT-NOBODY-BOUND") }, _ => null);

        Assert.False(report.Any);

        // *** AND EMPTY IS NOT CLEAN. *** Nothing was examined, so the report says so rather than printing
        // the same line a real join produces.
        Assert.Equal(0, report.Examined);
        Assert.Contains("NOTHING EXAMINED", report.Detail, StringComparison.Ordinal);
        Assert.Contains("not a pass", report.Detail, StringComparison.Ordinal);
    }
}
