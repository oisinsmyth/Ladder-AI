using Harness.Results;

namespace Harness.Results.Tests;

/// <summary>
/// <b>X-F's startup test class</b> — first-scan and power-up requirements, which the wave loop cannot test
/// at all and the disruptive boundary already can.
/// </summary>
public class StartupScheduleTests
{
    private static SubmissionVector Vector(string id = "V-STARTUP-1", InstrumentationMode mode = InstrumentationMode.Latched, bool anyExpectation = true) =>
        new(id, "S0", 0, new AgentIdentity("agent-b"),
            new Basis("REQ-002", "REQ-002:a1b2c3"),
            new Dictionary<string, string>(),
            "Demo_Start",
            anyExpectation
                ? new[] { new ObservabilityDeclaration("Demo_ValveClosed", SignalNature.Transient, mode, 0, "1") }
                : Array.Empty<ObservabilityDeclaration>(),
            AssertionForm.When,
            new SettlingDeclaration("latched at first scan", new[] { "Demo_ValveClosed" }, 1),
            MaxDurationScans: 5,
            CompletionValue: 1,
            Array.Empty<BlacklistEntry>(),
            CompressionFactor: 1,
            AssertedBehaviours: new[] { "valves closed at start-up" },
            CompletionSignal: "Demo_Done",
            Kills: "a block that leaves valves open on first scan");

    private static readonly DisruptiveBoundary Boundary = new("D25-wave-14", "the scheduled full download at wave 14");

    [Fact]
    public void A_latched_startup_test_RIDES_a_boundary_that_is_already_happening()
    {
        var plan = StartupSchedule.Schedule(new[] { Vector() }, Boundary, Array.Empty<SubmissionVector>());

        Assert.True(plan.IsScheduled);
        Assert.Empty(plan.Findings);
        Assert.Contains("no additional CPU stop", plan.Render(), StringComparison.Ordinal);
    }

    [Fact]
    public void With_NO_BOUNDARY_it_is_REFUSED_because_the_whole_argument_was_that_the_restart_already_happens()
    {
        var plan = StartupSchedule.Schedule(new[] { Vector() }, boundary: null, Array.Empty<SubmissionVector>());

        Assert.False(plan.IsScheduled);
        Assert.Equal(StartupRefusal.NoBoundaryToRideOn, plan.Findings.Single().Refusal);
    }

    [Fact]
    public void A_startup_test_PACKED_INTO_AN_ORDINARY_TENSOR_is_refused()
    {
        // The inert phase establishes a start STATE and cannot produce a first SCAN, and OB100 never runs
        // again once the CPU is in RUN. Run in a wave, this reports a confident result about a stimulus
        // that never occurred.
        var plan = StartupSchedule.Schedule(new[] { Vector() }, Boundary, new[] { Vector() });

        Assert.Equal(StartupRefusal.PackedIntoAnOrdinaryTensor, plan.Findings.Single().Refusal);
        Assert.False(plan.IsScheduled);
    }

    [Fact]
    public void A_SAMPLED_expectation_is_refused_because_THE_HARNESS_IS_DISCONNECTED_ACROSS_THE_DOWNLOAD()
    {
        var plan = StartupSchedule.Schedule(
            new[] { Vector(mode: InstrumentationMode.Sampled) }, Boundary, Array.Empty<SubmissionVector>());

        var finding = plan.Findings.Single();

        Assert.Equal(StartupRefusal.EvidenceNotLatched, finding.Refusal);
        Assert.Contains("nobody was present for", finding.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_STAMPED_expectation_is_refused_too_and_the_refusal_says_it_is_a_DECISION()
    {
        // A stamp would plausibly survive to the reconnect. X-F names latching and only latching, and
        // admitting a mode on an inference at the one boundary where nothing is watching is the permissive
        // direction — so this is the fail-closed reading, recorded as such rather than as an oversight.
        var plan = StartupSchedule.Schedule(
            new[] { Vector(mode: InstrumentationMode.Stamped) }, Boundary, Array.Empty<SubmissionVector>());

        var finding = plan.Findings.Single();

        Assert.Equal(StartupRefusal.EvidenceNotLatched, finding.Refusal);
        Assert.Contains("Refused deliberately, not by oversight", finding.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_startup_test_that_LATCHES_NOTHING_observes_nothing_and_the_restart_is_not_repeatable_on_demand()
    {
        var plan = StartupSchedule.Schedule(
            new[] { Vector(anyExpectation: false) }, Boundary, Array.Empty<SubmissionVector>());

        Assert.Equal(StartupRefusal.EvidenceNotLatched, plan.Findings.Single().Refusal);
    }

    [Fact]
    public void An_EMPTY_startup_set_must_not_read_like_a_successful_schedule()
    {
        var plan = StartupSchedule.Schedule(Array.Empty<SubmissionVector>(), Boundary, Array.Empty<SubmissionVector>());

        Assert.False(plan.IsScheduled);
        Assert.Equal(StartupRefusal.NothingScheduled, plan.Findings.Single().Refusal);
    }

    [Fact]
    public void There_is_no_TEST_CLASS_FIELD_a_vector_could_leave_blank()
    {
        // Which class a vector belongs to is expressed by WHICH ARGUMENT it is passed as, so there is no
        // blank class for something later to resolve permissively — and double-booking is caught by
        // comparison rather than by trusting a label.
        Assert.DoesNotContain(typeof(SubmissionVector).GetProperties(), p =>
            p.Name.Contains("Class", StringComparison.OrdinalIgnoreCase)
            || p.Name.Contains("TestKind", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void The_refusal_enum_has_an_UNUSABLE_zero_value()
    {
        Assert.Equal(StartupRefusal.Unstated, default(StartupRefusal));
    }
}
