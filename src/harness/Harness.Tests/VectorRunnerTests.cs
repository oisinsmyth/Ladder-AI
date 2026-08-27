namespace Harness.Tests;

/// <summary>
/// The runner's job is to produce results that mean something. Most of these tests are about the
/// ways a test suite lies: a vector that passed because nothing was observed, a wait that was a
/// sleep rather than a scan count, a green summary hiding vectors that never ran.
/// </summary>
public class VectorRunnerTests
{
    private static TestVector Vector(
        string id = "V-01",
        Observability obs = Observability.PersistentState,
        TargetEnvironment env = TargetEnvironment.Both,
        params VectorStep[] steps) =>
        new(id, "a vector", "Rev 10 §2.4", obs, env, Kills: "something plausible", Steps: steps);

    private static VectorStep Step(int wait = 0, TagWrite[]? writes = null, Expectation[]? expect = null) =>
        new(Note: null, Stimulus: writes, WaitScans: wait, Expect: expect);

    // ---------------------------------------------------------------- the happy path

    [Fact]
    public void Passes_when_every_assertion_holds()
    {
        var t = new FakeTransport();
        t.Set("IO.StateID", "110");

        var v = Vector(steps: Step(expect: new[] { new Expectation("IO.StateID", "110") }));

        var r = new VectorRunner(t, TargetEnvironment.Both).Run(v);

        Assert.Equal(VectorOutcome.Passed, r.Outcome);
        Assert.Empty(r.Failures);
    }

    [Fact]
    public void Fails_and_reports_the_actual_value()
    {
        var t = new FakeTransport();
        t.Set("IO.T0", "0.0");

        var v = Vector(steps: Step(expect: new[] { new Expectation("IO.T0", "512.4") }));

        var r = new VectorRunner(t, TargetEnvironment.Both).Run(v);

        Assert.Equal(VectorOutcome.Failed, r.Outcome);
        var f = Assert.Single(r.Failures);
        Assert.Equal("512.4", f.Expected);
        Assert.Equal("0.0", f.Actual);
    }

    [Fact]
    public void Applies_stimulus_before_asserting()
    {
        var t = new FakeTransport();
        var v = Vector(steps: Step(
            writes: new[] { new TagWrite("DB_GaugeInterface", "Raw_Value", "3400.0") },
            expect: new[] { new Expectation("Raw_Value", "3400.0") }));

        var r = new VectorRunner(t, TargetEnvironment.Both).Run(v);

        Assert.Equal(VectorOutcome.Passed, r.Outcome);
        Assert.Equal("DB_GaugeInterface", Assert.Single(t.Writes).Area);
    }

    // ---------------------------------------------------------------- tolerance

    [Fact]
    public void Honours_a_tolerance_on_real_values()
    {
        var t = new FakeTransport();
        t.Set("MC", "0.450004");

        var v = Vector(steps: Step(expect: new[] { new Expectation("MC", "0.45", Tolerance: 0.0001) }));

        Assert.Equal(VectorOutcome.Passed, new VectorRunner(t, TargetEnvironment.Both).Run(v).Outcome);
    }

    [Fact]
    public void A_tolerance_on_non_numeric_values_fails_rather_than_silently_passing()
    {
        var t = new FakeTransport();
        t.Set("State", "Held");

        var v = Vector(steps: Step(expect: new[] { new Expectation("State", "Running", Tolerance: 0.5) }));

        var r = new VectorRunner(t, TargetEnvironment.Both).Run(v);
        Assert.Equal(VectorOutcome.Failed, r.Outcome);
    }

    // ---------------------------------------------------------------- observability gating

    /// <summary>
    /// The whole point. A coincidence vector against a transport with no event scan-stamps must not
    /// pass — it must say it could not be evaluated.
    /// </summary>
    [Fact]
    public void A_coincidence_vector_is_not_observable_without_event_scan_stamps()
    {
        var t = new FakeTransport(TransportCapabilities.ScanCounter | TransportCapabilities.Write);

        var v = Vector(obs: Observability.Coincidence,
            steps: Step(expect: new[] { new Expectation("ScanStamp_ValveOpen", "412") }));

        var r = new VectorRunner(t, TargetEnvironment.Both).Run(v);

        Assert.Equal(VectorOutcome.NotObservable, r.Outcome);
        Assert.Contains("NOT a pass", r.Message);
    }

    [Fact]
    public void A_transient_vector_is_not_observable_without_latching()
    {
        var t = new FakeTransport(TransportCapabilities.ScanCounter | TransportCapabilities.Write);
        var v = Vector(obs: Observability.Transient, steps: Step());

        Assert.Equal(VectorOutcome.NotObservable, new VectorRunner(t, TargetEnvironment.Both).Run(v).Outcome);
    }

    [Fact]
    public void A_scan_wait_is_not_observable_without_a_scan_counter()
    {
        var t = new FakeTransport(TransportCapabilities.Write);
        var v = Vector(steps: Step(wait: 1));

        Assert.Equal(VectorOutcome.NotObservable, new VectorRunner(t, TargetEnvironment.Both).Run(v).Outcome);
    }

    [Fact]
    public void A_writing_vector_is_not_observable_on_a_read_only_transport()
    {
        var t = new FakeTransport(TransportCapabilities.ScanCounter);
        var v = Vector(steps: Step(writes: new[] { new TagWrite("DB_PanelCmd", "Code", "25") }));

        Assert.Equal(VectorOutcome.NotObservable, new VectorRunner(t, TargetEnvironment.Both).Run(v).Outcome);
    }

    [Fact]
    public void Coincidence_runs_when_the_program_provides_scan_stamps()
    {
        var t = new FakeTransport(TransportCapabilities.ScanCounter
                                  | TransportCapabilities.Write
                                  | TransportCapabilities.EventScanStamps);
        t.Set("ScanStamp_ValveOpen", "412");
        t.Set("ScanStamp_TimerStart", "412");

        var v = Vector(obs: Observability.Coincidence, steps: Step(expect: new[]
        {
            new Expectation("ScanStamp_ValveOpen", "412"),
            new Expectation("ScanStamp_TimerStart", "412"),
        }));

        Assert.Equal(VectorOutcome.Passed, new VectorRunner(t, TargetEnvironment.Both).Run(v).Outcome);
    }

    // ---------------------------------------------------------------- scan waiting

    [Fact]
    public void Waits_by_observing_the_scan_counter()
    {
        var t = new FakeTransport { AutoAdvancePerRead = 1 };
        var v = Vector(steps: Step(wait: 5, expect: new[] { new Expectation("X", "") }));

        var r = new VectorRunner(t, TargetEnvironment.Both).Run(v);

        Assert.Equal(VectorOutcome.Passed, r.Outcome);
        Assert.True(Assert.Single(r.StepResults).ScansWaited >= 5);
    }

    [Fact]
    public void A_stalled_scan_counter_errors_rather_than_hanging_or_passing()
    {
        var t = new FakeTransport { AutoAdvancePerRead = 0 };
        var v = Vector(steps: Step(wait: 3));

        var r = new VectorRunner(t, TargetEnvironment.Both, maxPollsPerScanWait: 20).Run(v);

        Assert.Equal(VectorOutcome.Errored, r.Outcome);
        Assert.Contains("did not advance", r.Message);
    }

    // ---------------------------------------------------------------- environment gating

    [Fact]
    public void Skips_a_hardware_vector_on_a_simulator_run()
    {
        var v = Vector(env: TargetEnvironment.Hardware, steps: Step());
        var r = new VectorRunner(new FakeTransport(), TargetEnvironment.Simulator).Run(v);

        Assert.Equal(VectorOutcome.Skipped, r.Outcome);
    }

    [Fact]
    public void Runs_a_both_vector_anywhere()
    {
        var v = Vector(env: TargetEnvironment.Both, steps: Step());
        Assert.Equal(VectorOutcome.Passed,
            new VectorRunner(new FakeTransport(), TargetEnvironment.Hardware).Run(v).Outcome);
    }

    // ---------------------------------------------------------------- the report

    [Fact]
    public void A_run_containing_an_unobservable_vector_is_not_green()
    {
        var t = new FakeTransport(TransportCapabilities.ScanCounter | TransportCapabilities.Write);
        t.Set("A", "1");

        var report = new VectorRunner(t, TargetEnvironment.Both).RunAll(new[]
        {
            Vector("V-01", steps: Step(expect: new[] { new Expectation("A", "1") })),
            Vector("V-02", obs: Observability.Coincidence, steps: Step()),
        });

        Assert.Equal(1, report.Passed);
        Assert.Equal(1, report.NotObservable);
        Assert.False(report.IsGreen);
        Assert.Contains("NOT OBSERVABLE", report.Summary());
    }

    [Fact]
    public void Environment_skips_do_not_spoil_green_but_are_reported()
    {
        var t = new FakeTransport();
        t.Set("A", "1");

        var report = new VectorRunner(t, TargetEnvironment.Simulator).RunAll(new[]
        {
            Vector("V-01", steps: Step(expect: new[] { new Expectation("A", "1") })),
            Vector("V-02", env: TargetEnvironment.Hardware, steps: Step()),
        });

        Assert.True(report.IsGreen);
        Assert.Equal(1, report.Skipped);
        Assert.Contains("skipped", report.Summary());
    }

    [Fact]
    public void An_empty_run_is_not_green()
    {
        var report = new VectorRunner(new FakeTransport(), TargetEnvironment.Both)
            .RunAll(Array.Empty<TestVector>());

        Assert.False(report.IsGreen);
    }

    // ---------------------------------------------------------------- scope derivation

    [Fact]
    public void Declared_areas_are_derived_from_the_vectors_themselves()
    {
        var vectors = new[]
        {
            Vector("V-01", steps: Step(writes: new[] { new TagWrite("DB_PanelCmd", "Code", "1") })),
            Vector("V-02", steps: Step(writes: new[]
            {
                new TagWrite("DB_PanelCmd", "Code", "25"),
                new TagWrite("DB_GaugeInterface", "Quality", "0"),
            })),
        };

        var areas = VectorSet.DeclaredAreas(vectors);

        // Sorted, NOT first-seen: the vectors write DB_PanelCmd first.
        Assert.Equal(new[] { "DB_GaugeInterface", "DB_PanelCmd" }, areas);
    }

    [Fact]
    public void A_read_only_vector_declares_no_areas()
    {
        var v = Vector(steps: Step(expect: new[] { new Expectation("A", "1") }));
        Assert.True(v.IsReadOnly);
        Assert.Empty(v.AreasWritten);
    }
}
