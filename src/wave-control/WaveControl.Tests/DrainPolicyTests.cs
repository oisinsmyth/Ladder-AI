using System;
using System.Linq;

namespace Ladder.Wave.Tests
{
    /// <summary>D24 — the deferred queue drains when NO TEST CAN MAKE PROGRESS.</summary>
    public sealed class DrainPolicyTests
    {
        private static WaveQueues QueuesWith(params AdmissionDecision[] decisions)
        {
            var queues = new WaveQueues();
            foreach (var decision in decisions)
            {
                queues.Enqueue(decision);
            }

            return queues;
        }

        [Fact]
        public void Nothing_deferred_and_tests_progressing_is_its_own_outcome()
        {
            var queues = QueuesWith(ChangeSets.AdmitAll(ChangeSets.Submission("S-run", ChangeSets.Fb("FB_Motor"))));

            var decision = DrainPolicy.Decide(queues, ChangeSets.Deployed());

            Assert.Equal(DrainOutcome.NothingToDrain, decision.Outcome);
            Assert.False(decision.DisruptiveModeAuthorised);
        }

        [Fact]
        public void Something_deferred_but_a_test_can_still_run_does_not_drain_and_names_the_witness()
        {
            // Declining to drain is a CLAIM that a test can progress. A claim with no witness is an
            // absence of findings reported as a result.
            var queues = QueuesWith(
                ChangeSets.AdmitAll(ChangeSets.Submission("S-run", ChangeSets.Fb("FB_Motor"))),
                ChangeSets.AdmitAll(ChangeSets.Submission("S-stop", ChangeSets.Ob("OB_Cyclic"))));

            var decision = DrainPolicy.Decide(queues, ChangeSets.Deployed());

            Assert.Equal(DrainOutcome.DoNotDrain, decision.Outcome);
            Assert.NotNull(decision.Witness);
            Assert.Equal("S-run", decision.Witness!.Submission.Id);
            Assert.False(decision.DisruptiveModeAuthorised);
        }

        [Fact]
        public void A_do_not_drain_decision_cannot_be_built_without_a_witness()
        {
            Assert.Throws<InvalidOperationException>(
                () => DrainDecision.DoNotDrain(null!, new ProgressBlock[0], new QueuedSubmission[0], 1, "should be impossible"));
        }

        [Fact]
        public void No_run_queue_at_all_plus_a_deferred_queue_drains()
        {
            var queues = QueuesWith(ChangeSets.AdmitAll(ChangeSets.Submission("S-stop", ChangeSets.Ob("OB_Cyclic"))));

            var decision = DrainPolicy.Decide(queues, ChangeSets.Deployed());

            Assert.Equal(DrainOutcome.Drain, decision.Outcome);
            Assert.True(decision.DisruptiveModeAuthorised);
            Assert.Null(decision.Witness);
        }

        [Fact]
        public void A_run_submission_blocked_on_a_deferred_object_cannot_make_progress()
        {
            // D24's stated reason for choosing this condition: "the deferred queue holds other
            // dependencies too, so in practice the two conditions converge". That sentence is the
            // implementation — a submission is blocked when something it DEPENDS ON is deferred.
            var deferredUdt = new ChangedObject("UDT_Shared", ObjectKind.DataType, ChangeClass.Stop, null, ChangeSets.Hash);
            var blocked = ChangeSets.Fb("FB_Motor", ChangeSets.Hash, "UDT_Shared");

            var queues = QueuesWith(
                ChangeSets.AdmitAll(ChangeSets.Submission("S-run", blocked)),
                ChangeSets.AdmitAll(ChangeSets.Submission("S-stop", deferredUdt)));

            var decision = DrainPolicy.Decide(queues, ChangeSets.Deployed());

            Assert.Equal(DrainOutcome.Drain, decision.Outcome);
            Assert.Contains(decision.Blocks, b => b.Kind == ProgressBlockKind.BlockedOnADeferredObject && b.DependencyName == "UDT_Shared");
        }

        [Fact]
        public void A_dependency_that_is_in_no_queue_at_all_is_a_different_block_from_a_deferred_one()
        {
            // A drain will not fix this one, and conflating the two would send a fix at the queue when
            // the fault is in the change set or the baseline. Same distinction D32 caveat 1 insists on.
            var blocked = ChangeSets.Fb("FB_Motor", ChangeSets.Hash, "UDT_Absent");
            var queues = QueuesWith(
                ChangeSets.AdmitAll(ChangeSets.Submission("S-run", blocked)),
                ChangeSets.AdmitAll(ChangeSets.Submission("S-stop", ChangeSets.Ob("OB_Cyclic"))));

            var decision = DrainPolicy.Decide(queues, ChangeSets.Deployed());

            Assert.Equal(DrainOutcome.Drain, decision.Outcome);
            Assert.Contains(decision.Blocks, b => b.Kind == ProgressBlockKind.DependencyPresentNowhere && b.DependencyName == "UDT_Absent");
            Assert.DoesNotContain(decision.Blocks, b => b.Kind == ProgressBlockKind.BlockedOnADeferredObject);
        }

        [Fact]
        public void A_dependency_satisfied_by_another_queued_submission_is_not_a_block()
        {
            var udt = ChangeSets.Udt("UDT_Shared");
            var user = ChangeSets.Fb("FB_Motor", ChangeSets.Hash, "UDT_Shared");

            var queues = QueuesWith(
                ChangeSets.AdmitAll(ChangeSets.Submission("S-a", udt)),
                ChangeSets.AdmitAll(ChangeSets.Submission("S-b", user)));

            var decision = DrainPolicy.Decide(queues, ChangeSets.Deployed());

            Assert.Equal(DrainOutcome.NothingToDrain, decision.Outcome);
            Assert.Empty(decision.Blocks);
        }

        [Fact]
        public void A_dependency_already_on_the_device_is_not_a_block()
        {
            var queues = QueuesWith(
                ChangeSets.AdmitAll(ChangeSets.Submission("S-run", ChangeSets.Fb("FB_Motor", ChangeSets.Hash, "UDT_Shared"))),
                ChangeSets.AdmitAll(ChangeSets.Submission("S-stop", ChangeSets.Ob("OB_Cyclic"))));

            var decision = DrainPolicy.Decide(queues, ChangeSets.Deployed("UDT_Shared"));

            Assert.Equal(DrainOutcome.DoNotDrain, decision.Outcome);
            Assert.Empty(decision.Blocks);
        }

        // --- THE STALL, WHICH IS THE OUTCOME MOST EASILY MISTAKEN FOR HEALTH -------------------------

        [Fact]
        public void An_idle_coordinator_is_reported_as_stalled_not_as_a_healthy_quiet_period()
        {
            var decision = DrainPolicy.Decide(new WaveQueues(), ChangeSets.Deployed());

            Assert.Equal(DrainOutcome.StalledWithNothingToDrain, decision.Outcome);
            Assert.False(decision.DisruptiveModeAuthorised);
            Assert.Contains("STALLED", decision.ToLogLine(), StringComparison.Ordinal);
        }

        [Fact]
        public void Blocked_work_with_an_empty_deferred_queue_is_a_stall_not_a_drain()
        {
            // Both have an empty deferred queue and only one of them is healthy, so they must not share
            // a verdict — a drain here would spend a CPU stop and change nothing.
            var queues = QueuesWith(
                ChangeSets.AdmitAll(ChangeSets.Submission("S-run", ChangeSets.Fb("FB_Motor", ChangeSets.Hash, "UDT_Absent"))));

            var decision = DrainPolicy.Decide(queues, ChangeSets.Deployed());

            Assert.Equal(DrainOutcome.StalledWithNothingToDrain, decision.Outcome);
            Assert.False(decision.DisruptiveModeAuthorised);
            Assert.Contains(decision.Blocks, b => b.Kind == ProgressBlockKind.DependencyPresentNowhere);
        }

        // --- THE GUARDS AROUND THE DECISION OBJECT ---------------------------------------------------

        [Fact]
        public void A_drain_decision_cannot_be_built_over_an_empty_deferred_queue()
        {
            Assert.Throws<InvalidOperationException>(
                () => DrainDecision.Drain(new ProgressBlock[0], new QueuedSubmission[0], 0, "should be impossible"));
        }

        [Fact]
        public void Not_evaluated_is_the_zero_value_of_the_outcome()
        {
            Assert.Equal(DrainOutcome.NotEvaluated, default(DrainOutcome));
            Assert.Equal(ProgressBlockKind.None, default(ProgressBlockKind));
        }

        [Fact]
        public void The_policy_refuses_to_run_without_a_baseline()
        {
            Assert.Throws<ArgumentNullException>(() => DrainPolicy.Decide(new WaveQueues(), null!));
        }

        [Fact]
        public void The_drain_log_line_records_what_was_in_the_queue_and_why()
        {
            // R8: the disruptive boundary must be "logged as a deliberate CPU stop, with what was in the
            // queue and why".
            var queues = QueuesWith(ChangeSets.AdmitAll(ChangeSets.Submission("S-stop", ChangeSets.Ob("OB_Cyclic"))));
            queues.NoteWaveBoundary();

            var line = DrainPolicy.Decide(queues, ChangeSets.Deployed()).ToLogLine();

            Assert.Contains("DELIBERATE CPU STOP", line, StringComparison.Ordinal);
            Assert.Contains("S-stop", line, StringComparison.Ordinal);
            Assert.Contains("waited 1", line, StringComparison.Ordinal);
        }
    }
}
