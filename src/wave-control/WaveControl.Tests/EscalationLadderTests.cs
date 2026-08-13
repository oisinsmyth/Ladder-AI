using System;
using System.Linq;

namespace Ladder.Wave.Tests
{
    /// <summary>
    /// 4.2 — D32's ladder, with the Class-A rung corrected against the 2026-08-13 measurement.
    /// </summary>
    public sealed class EscalationLadderTests
    {
        private static ConfigurationVerdict StopModules() =>
            ConfigurationClassifier.Classify("StopModules", "NoAction", "StopAll");

        private static ConfigurationVerdict StartModules() =>
            ConfigurationClassifier.Classify("StartModules", "NoAction", "StartModule");

        private static ConfigurationVerdict ClassB() =>
            ConfigurationClassifier.Classify("ResetModule", "NoAction", "DeleteAll");

        private static ConfigurationVerdict ClassC() =>
            ConfigurationClassifier.Classify("SelectiveDeleteDownload", "AcceptAll", "DeleteSelected");

        private static LadderAttempts Budget(int spent = 0, int max = 3, bool disruptiveAborted = false) =>
            new LadderAttempts(spent, max, disruptiveAborted);

        // =============================================================================================
        // THE MEASUREMENT, IN BOTH DIRECTIONS
        // =============================================================================================

        [Fact]
        public void A_PRE_delegate_throw_leaves_the_cpu_running_and_a_POST_one_leaves_it_stopped()
        {
            Assert.Equal(AbortAftermath.CpuLeftRunning, AbortAftermathTable.For(DelegateStage.PreTransfer));
            Assert.Equal(AbortAftermath.CpuLeftStoppedWithACompleteProgram, AbortAftermathTable.For(DelegateStage.PostTransfer));

            Assert.False(AbortAftermathTable.RequiresCpuStart(DelegateStage.PreTransfer));
            Assert.True(AbortAftermathTable.RequiresCpuStart(DelegateStage.PostTransfer));

            Assert.True(AbortAftermathTable.IsMeasured(DelegateStage.PreTransfer));
            Assert.True(AbortAftermathTable.IsMeasured(DelegateStage.PostTransfer));
        }

        [Fact]
        public void The_half_loaded_case_is_uncharacterised_and_is_the_zero_value()
        {
            // A6's feared failure mode was a HALF-LOADED CPU. What was measured after a POST throw is a
            // COMPLETE program that was not running. The genuinely half-loaded case needs a throw DURING
            // the transfer and there is no configuration raised there to hang one on.
            Assert.Equal(AbortAftermath.Uncharacterised, default(AbortAftermath));
            Assert.Equal(AbortAftermath.Uncharacterised, AbortAftermathTable.For(DelegateStage.Unknown));
            Assert.False(AbortAftermathTable.IsMeasured(DelegateStage.Unknown));
            Assert.Contains("NOT MEASURED", AbortAftermathTable.EvidenceFor(DelegateStage.Unknown), StringComparison.Ordinal);
            Assert.Equal(DelegateStage.Unknown, default(DelegateStage));
        }

        [Fact]
        public void Each_stage_carries_its_own_measured_evidence_and_they_are_not_the_same_string()
        {
            // The did-not-run trap named this week: a hardcoded value indistinguishable from the field
            // because every fixture used the same number.
            var pre = AbortAftermathTable.EvidenceFor(DelegateStage.PreTransfer);
            var post = AbortAftermathTable.EvidenceFor(DelegateStage.PostTransfer);

            Assert.NotEqual(pre, post);
            Assert.Contains("Running (8)", pre, StringComparison.Ordinal);
            Assert.Contains("NotRunning (4)", post, StringComparison.Ordinal);
            Assert.StartsWith("[M]", pre, StringComparison.Ordinal);
            Assert.StartsWith("[M]", post, StringComparison.Ordinal);
        }

        // =============================================================================================
        // THE CORRECTED CLASS A
        // =============================================================================================

        [Fact]
        public void A_PRE_delegate_class_A_abort_goes_to_step_6_and_needs_no_start_step()
        {
            var decision = EscalationLadder.Decide(StopModules(), AttributionOutcome.NoBlockIsResponsible, null, Budget());

            Assert.Equal(LadderRung.Step6DisruptiveFullDownload, decision.Rung);
            Assert.False(decision.RequiresCpuStart);
            Assert.True(decision.RestsOnAMeasuredAftermath);
            Assert.Equal(AbortAftermath.CpuLeftRunning, decision.Aftermath);
        }

        [Fact]
        public void A_POST_delegate_class_A_entry_is_answered_where_it_was_raised_not_sent_to_step_6()
        {
            // "Go to step 6" is CIRCULAR for StartModules: it is raised only because this download
            // already stopped the CPU, so fetching a disruptive download to answer it is fetching the
            // thing we are inside.
            var decision = EscalationLadder.Decide(StartModules(), AttributionOutcome.Attributed, null, Budget());

            Assert.Equal(LadderRung.AnswerWithinTheCurrentDownload, decision.Rung);
            Assert.NotEqual(LadderRung.Step6DisruptiveFullDownload, decision.Rung);
            Assert.True(decision.RequiresCpuStart);
            Assert.Contains("CIRCULAR", decision.Reason, StringComparison.Ordinal);
        }

        [Fact]
        public void The_policy_ANSWERS_a_POST_delegate_entry_even_on_a_non_disruptive_wave_boundary()
        {
            // The half of the correction that actually prevents the stopped CPU: refusing StartModules
            // does not defer a stop, it leaves one in place.
            var response = DownloadConfigurationPolicy.Decide(
                new RaisedConfiguration("StartModules", new[] { "NoAction", "StartModule" }),
                DownloadMode.WaveBoundaryNonDisruptive);

            Assert.True(response.ShouldAnswer);
            Assert.Equal("StartModule", response.Selection);
            Assert.Contains("ALREADY stopped the CPU", response.Reason, StringComparison.Ordinal);
        }

        [Fact]
        public void The_policy_REFUSES_a_PRE_delegate_class_A_entry_on_a_wave_boundary()
        {
            var response = DownloadConfigurationPolicy.Decide(
                new RaisedConfiguration("StopModules", new[] { "NoAction", "StopAll" }),
                DownloadMode.WaveBoundaryNonDisruptive);

            Assert.False(response.ShouldAnswer);
            Assert.Null(response.Selection);
        }

        [Fact]
        public void A_class_A_abort_that_lands_after_the_transfer_carries_a_start_step()
        {
            // Constructed rather than reached through StartModules, because the requirement is about ANY
            // abort landing post-transfer, not about that one entry.
            var postEntry = new ClassAEntry(
                configurationName: "SomeFuturePostConfiguration",
                answeredSelection: "ProceedAnyway",
                recognisedSelections: new[] { "NoAction", "ProceedAnyway" },
                entailedBy: DownloadOption.Software,
                entailment: "a well formed entailment for the Software download, stated at length",
                evidence: "[M] a hypothetical measured entry used to exercise the post-transfer rung",
                raisedIn: DelegateStage.PostTransfer);

            Assert.Equal(LadderRung.AnswerWithinTheCurrentDownload, postEntry.Rung);
            Assert.Equal(AbortAftermath.CpuLeftStoppedWithACompleteProgram, postEntry.Aftermath);
        }

        [Fact]
        public void A_class_A_entry_cannot_be_built_without_naming_the_delegate_that_raises_it()
        {
            // Class A is the only class that spends anything, and its rung is now derived from the
            // stage — so an entry that will not say which delegate raises it cannot be given a rung.
            var ex = Assert.Throws<ArgumentException>(() => new ClassAEntry(
                "Whatever", "Sel", new[] { "Sel" }, DownloadOption.Software,
                "a perfectly well written entailment for the Software download",
                "[M] somewhere", DelegateStage.Unknown));

            Assert.Equal("raisedIn", ex.ParamName);
        }

        [Fact]
        public void Every_live_allowance_entry_names_a_measured_stage()
        {
            Assert.NotEmpty(ConfigurationClassifier.AllowanceList);

            foreach (var entry in ConfigurationClassifier.AllowanceList)
            {
                Assert.NotEqual(DelegateStage.Unknown, entry.RaisedIn);
                Assert.True(AbortAftermathTable.IsMeasured(entry.RaisedIn), entry.ConfigurationName);
            }
        }

        // =============================================================================================
        // THROW-ON-UNHANDLED
        // =============================================================================================

        [Theory]
        [InlineData("ResetModule", "DeleteAll")]
        [InlineData("InitializeMemory", "AcceptAll")]
        [InlineData("SelectiveDeleteDownload", "DeleteSelected")]
        [InlineData("AConfigurationNobodyHasSeenYet", "ProceedAnyway")]
        public void Every_non_class_A_configuration_is_refused_in_EVERY_mode_disruptive_included(string name, string selection)
        {
            foreach (var mode in new[] { DownloadMode.WaveBoundaryNonDisruptive, DownloadMode.Disruptive })
            {
                var response = DownloadConfigurationPolicy.Decide(
                    new RaisedConfiguration(name, new[] { "NoAction", selection }),
                    mode);

                Assert.False(response.ShouldAnswer, name + " was answered in " + mode);
            }
        }

        [Fact]
        public void A_delegate_that_does_not_know_which_download_it_serves_answers_nothing()
        {
            // Every allowance rests on "the option you chose already entails this", and nobody has said
            // which option that is. DownloadMode.Unstated is the zero value.
            Assert.Equal(DownloadMode.Unstated, default(DownloadMode));

            foreach (var name in new[] { "StopModules", "StartModules", "DataBlockReinitialization" })
            {
                var response = DownloadConfigurationPolicy.Decide(
                    new RaisedConfiguration(name, new[] { "NoAction", "StopAll", "StartModule", "StopPlcAndReinitialize" }),
                    DownloadMode.Unstated);

                Assert.False(response.ShouldAnswer, name + " was answered by a delegate with no mode");
            }
        }

        [Fact]
        public void The_disruptive_mode_answers_a_class_A_configuration()
        {
            var response = DownloadConfigurationPolicy.Decide(
                new RaisedConfiguration("StopModules", new[] { "NoAction", "StopAll" }),
                DownloadMode.Disruptive);

            Assert.True(response.ShouldAnswer);
            Assert.Equal("StopAll", response.Selection);
        }

        [Fact]
        public void The_abort_carries_the_aftermath_because_the_catch_site_is_where_the_next_step_is_decided()
        {
            var response = DownloadConfigurationPolicy.Decide(
                new RaisedConfiguration("StopModules", new[] { "NoAction", "StopAll" }),
                DownloadMode.WaveBoundaryNonDisruptive);

            var pre = DownloadConfigurationPolicy.Abort(response, DelegateStage.PreTransfer);
            var post = DownloadConfigurationPolicy.Abort(response, DelegateStage.PostTransfer);

            Assert.Equal(AbortAftermath.CpuLeftRunning, pre.Aftermath);
            Assert.False(pre.CpuIsStopped);
            Assert.Equal(AbortAftermath.CpuLeftStoppedWithACompleteProgram, post.Aftermath);
            Assert.True(post.CpuIsStopped);
        }

        [Fact]
        public void An_abort_cannot_be_built_from_a_response_that_says_answer()
        {
            // Throwing away a selection the policy decided to apply is, for a POST entry, the difference
            // between a running CPU and a stopped one.
            var answering = DownloadConfigurationPolicy.Decide(
                new RaisedConfiguration("StartModules", new[] { "NoAction", "StartModule" }),
                DownloadMode.WaveBoundaryNonDisruptive);

            Assert.Throws<InvalidOperationException>(
                () => DownloadConfigurationPolicy.Abort(answering, DelegateStage.PostTransfer));
        }

        // =============================================================================================
        // THE EXCISION CLOSURE AND ITS THRESHOLD
        // =============================================================================================

        private static ChangedObject[] WaveSet() => new[]
        {
            ChangeSets.Udt("UDT_Motor"),
            ChangeSets.Fb("FB_Motor", ChangeSets.Hash, "UDT_Motor"),
            ChangeSets.InstanceDb("iDB_Motor", "FB_Motor"),
            ChangeSets.Fb("FB_Pump"),
            ChangeSets.Fb("FB_Valve"),
        };

        [Fact]
        public void Excising_a_block_takes_its_whole_dependency_group()
        {
            // A wave set that keeps an object whose dependency was excised is no longer closed (DB-4).
            var plan = ExcisionClosure.Plan("FB_Motor", WaveSet(), ChangeSets.Deployed(), 0.8);

            Assert.True(plan.Permitted);
            Assert.Equal(new[] { "FB_Motor", "iDB_Motor", "UDT_Motor" }, plan.Closure.ToArray());
            Assert.Equal(new[] { "FB_Pump", "FB_Valve" }, plan.Remaining.ToArray());
        }

        [Fact]
        public void An_independent_object_excises_alone()
        {
            var plan = ExcisionClosure.Plan("FB_Pump", WaveSet(), ChangeSets.Deployed(), 0.8);

            Assert.True(plan.Permitted);
            Assert.Equal(new[] { "FB_Pump" }, plan.Closure.ToArray());
        }

        [Fact]
        public void A_closure_over_the_threshold_is_refused()
        {
            // 3 of 5 is 0.6; a threshold of 0.5 refuses it.
            var plan = ExcisionClosure.Plan("FB_Motor", WaveSet(), ChangeSets.Deployed(), 0.5);

            Assert.False(plan.Permitted);
            Assert.Equal(ExcisionDefect.ClosureExceedsTheThreshold, plan.Defect);
            Assert.Contains("no measured value exists", plan.Detail, StringComparison.Ordinal);
        }

        [Fact]
        public void The_threshold_has_no_default_and_an_impossible_one_is_refused()
        {
            // D32 calls step 5 an ordinary boundary, which is true for a small closure and false for one
            // removing most of the wave. Nobody has measured where that turns over, so this type will
            // not supply a number.
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ExcisionClosure.Plan("FB_Pump", WaveSet(), ChangeSets.Deployed(), 0.0));

            Assert.Throws<ArgumentOutOfRangeException>(
                () => ExcisionClosure.Plan("FB_Pump", WaveSet(), ChangeSets.Deployed(), 1.5));
        }

        [Fact]
        public void Excising_everything_leaves_nothing_to_re_download()
        {
            var chain = new[]
            {
                ChangeSets.Udt("UDT_A"),
                ChangeSets.Fb("FB_A", ChangeSets.Hash, "UDT_A"),
            };

            var plan = ExcisionClosure.Plan("FB_A", chain, ChangeSets.Deployed(), 1.0);

            Assert.False(plan.Permitted);
            Assert.Equal(ExcisionDefect.ClosureIsTheWholeWaveSet, plan.Defect);
        }

        [Fact]
        public void Excising_something_that_is_not_in_the_wave_set_is_refused_and_names_the_caveat_1_distinction()
        {
            var plan = ExcisionClosure.Plan("FB_Absent", WaveSet(), ChangeSets.Deployed(), 0.8);

            Assert.False(plan.Permitted);
            Assert.Equal(ExcisionDefect.NotInTheWaveSet, plan.Defect);
            Assert.Contains("the locator failed to find one", plan.Detail, StringComparison.Ordinal);
        }

        [Fact]
        public void Excising_nothing_is_refused_rather_than_repeating_the_download_that_just_aborted()
        {
            var plan = ExcisionClosure.Plan("   ", WaveSet(), ChangeSets.Deployed(), 0.8);

            Assert.False(plan.Permitted);
            Assert.Equal(ExcisionDefect.NothingNamed, plan.Defect);
        }

        // =============================================================================================
        // BOUNDED ATTEMPTS
        // =============================================================================================

        [Fact]
        public void An_attributable_configuration_within_budget_excises_and_re_downloads()
        {
            var plan = ExcisionClosure.Plan("FB_Pump", WaveSet(), ChangeSets.Deployed(), 0.8);

            var decision = EscalationLadder.Decide(StopModules(), AttributionOutcome.Attributed, plan, Budget(spent: 0, max: 3));

            Assert.Equal(LadderRung.Step5ExciseAndRedownload, decision.Rung);
            Assert.Contains("no disruption is spent", decision.Reason, StringComparison.Ordinal);
        }

        [Fact]
        public void A_spent_excision_budget_falls_through_to_step_6()
        {
            // Without a bound the ladder keeps removing blocks until the wave is empty and calls that
            // progress.
            var plan = ExcisionClosure.Plan("FB_Pump", WaveSet(), ChangeSets.Deployed(), 0.8);

            var decision = EscalationLadder.Decide(StopModules(), AttributionOutcome.Attributed, plan, Budget(spent: 3, max: 3));

            Assert.Equal(LadderRung.Step6DisruptiveFullDownload, decision.Rung);
            Assert.Contains("excision budget is spent", decision.Reason, StringComparison.Ordinal);
        }

        [Fact]
        public void A_refused_excision_plan_falls_through_rather_than_excising_anyway()
        {
            var overThreshold = ExcisionClosure.Plan("FB_Motor", WaveSet(), ChangeSets.Deployed(), 0.5);

            var decision = EscalationLadder.Decide(StopModules(), AttributionOutcome.Attributed, overThreshold, Budget());

            Assert.NotEqual(LadderRung.Step5ExciseAndRedownload, decision.Rung);
            Assert.Equal(LadderRung.Step6DisruptiveFullDownload, decision.Rung);
        }

        [Fact]
        public void An_attribution_with_no_plan_is_not_step_5()
        {
            var decision = EscalationLadder.Decide(StopModules(), AttributionOutcome.Attributed, null, Budget());

            Assert.Equal(LadderRung.Step7TotalTestAbort, decision.Rung);
        }

        [Fact]
        public void A_disruptive_download_that_already_aborted_is_terminal_and_beats_everything_else()
        {
            // D32 caveat 2. NEVER RETRY: step 1 would fire again against an unchanged situation and the
            // ladder would restart, consuming the night.
            var plan = ExcisionClosure.Plan("FB_Pump", WaveSet(), ChangeSets.Deployed(), 0.8);

            var decision = EscalationLadder.Decide(
                StopModules(),
                AttributionOutcome.Attributed,
                plan,
                Budget(spent: 0, max: 3, disruptiveAborted: true));

            Assert.Equal(LadderRung.Step7TotalTestAbort, decision.Rung);
            Assert.True(decision.IsTotalTestAbort);
            Assert.Contains("NEVER RETRY", decision.Reason, StringComparison.Ordinal);
        }

        // =============================================================================================
        // CLASS B AND C NEVER SPEND A DOWNLOAD
        // =============================================================================================

        [Fact]
        public void Class_B_and_C_go_straight_to_a_human_without_attempting_a_download()
        {
            foreach (var verdict in new[] { ClassB(), ClassC() })
            {
                var decision = EscalationLadder.Decide(verdict, AttributionOutcome.Attributed, null, Budget());

                Assert.Equal(LadderRung.Step7TotalTestAbort, decision.Rung);
                Assert.Contains("WITHOUT attempting a download", decision.Reason, StringComparison.Ordinal);
            }
        }

        // =============================================================================================
        // ATTRIBUTION: THE TWO THAT MUST NOT CONFLATE
        // =============================================================================================

        [Fact]
        public void No_block_is_responsible_and_the_locator_failed_take_the_same_branch_with_different_reasons()
        {
            // Caveat 1: they call for OPPOSITE fixes, and a log that conflates them sends a fix wave
            // after a locator that was working correctly.
            var projectLevel = EscalationLadder.Decide(StopModules(), AttributionOutcome.NoBlockIsResponsible, null, Budget());
            var brokenLocator = EscalationLadder.Decide(StopModules(), AttributionOutcome.LocatorFailed, null, Budget());

            Assert.Equal(projectLevel.Rung, brokenLocator.Rung);
            Assert.NotEqual(projectLevel.Reason, brokenLocator.Reason);
            Assert.Contains("NOT a locator bug", projectLevel.Reason, StringComparison.Ordinal);
            Assert.Contains("defect in DB-1", brokenLocator.Reason, StringComparison.Ordinal);
        }

        [Fact]
        public void Attribution_not_attempted_is_recorded_as_such_and_is_the_zero_value()
        {
            Assert.Equal(AttributionOutcome.NotAttempted, default(AttributionOutcome));

            var decision = EscalationLadder.Decide(StopModules(), AttributionOutcome.NotAttempted, null, Budget());

            Assert.Contains("Attribution was not attempted", decision.Reason, StringComparison.Ordinal);
            Assert.DoesNotContain("NO BLOCK IS RESPONSIBLE", decision.Reason, StringComparison.Ordinal);
        }

        // =============================================================================================
        // THE LOG
        // =============================================================================================

        [Fact]
        public void The_log_line_says_when_the_cpu_is_stopped_and_when_a_rung_is_unproven()
        {
            var running = EscalationLadder.Decide(StopModules(), AttributionOutcome.NoBlockIsResponsible, null, Budget());
            var stopped = EscalationLadder.Decide(StartModules(), AttributionOutcome.NoBlockIsResponsible, null, Budget());

            Assert.DoesNotContain("CPU IS STOPPED", running.ToLogLine(), StringComparison.Ordinal);
            Assert.Contains("CPU IS STOPPED", stopped.ToLogLine(), StringComparison.Ordinal);

            // Nothing currently reaches an unproven rung, and that is itself worth asserting: every live
            // Class A entry names a measured stage.
            Assert.DoesNotContain("UNPROVEN", running.ToLogLine(), StringComparison.Ordinal);
            Assert.DoesNotContain("UNPROVEN", stopped.ToLogLine(), StringComparison.Ordinal);
        }

        [Fact]
        public void A_decision_whose_aftermath_was_never_measured_is_reported_UNPROVEN()
        {
            // The did-not-run case for the RestsOnAMeasuredAftermath flag: nothing in the live table can
            // produce it, so it is exercised through a Class A verdict built on an unmeasured stage.
            // Without this, the flag would be permanently true and indistinguishable from a constant.
            var decision = new LadderDecision(
                LadderRung.Step6DisruptiveFullDownload,
                StopModules(),
                AbortAftermath.Uncharacterised,
                requiresCpuStart: false,
                restsOnAMeasuredAftermath: false,
                reason: "a rung derived from an aftermath nobody measured",
                evidence: AbortAftermathTable.EvidenceFor(DelegateStage.Unknown));

            Assert.Contains("UNPROVEN", decision.ToLogLine(), StringComparison.Ordinal);
            Assert.False(decision.RestsOnAMeasuredAftermath);
        }
    }
}
