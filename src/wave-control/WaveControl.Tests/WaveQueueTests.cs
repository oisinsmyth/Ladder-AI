using System;
using System.Linq;

namespace Ladder.Wave.Tests
{
    /// <summary>D23's two queues, and the operations that move something between them.</summary>
    public sealed class WaveQueueTests
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
        public void Run_class_submissions_and_stop_class_submissions_land_in_different_queues()
        {
            var run = ChangeSets.AdmitAll(ChangeSets.Submission("S-run", ChangeSets.Fb("FB_Motor")));
            var stop = ChangeSets.AdmitAll(ChangeSets.Submission("S-stop", ChangeSets.Ob("OB_Cyclic")));

            var queues = QueuesWith(run, stop);

            Assert.Equal(new[] { "S-run" }, queues.RunQueue.Select(e => e.Submission.Id).ToArray());
            Assert.Equal(new[] { "S-stop" }, queues.DeferredQueue.Select(e => e.Submission.Id).ToArray());
        }

        [Fact]
        public void A_submission_that_was_not_admitted_cannot_be_queued()
        {
            // Queuing anything else would put work into a wave that never passed loop 2, which is the
            // single thing admission control exists to prevent.
            var refused = AdmissionController.Admit(ChangeSets.Submission("S1", ChangeSets.Fb("FB_Motor")), null);
            var nothing = AdmissionController.Admit(new Submission("S2", "agent", new ChangedObject[0]), null);

            Assert.Throws<InvalidOperationException>(() => new WaveQueues().Enqueue(refused));
            Assert.Throws<InvalidOperationException>(() => new WaveQueues().Enqueue(nothing));
        }

        [Fact]
        public void The_same_submission_id_cannot_be_queued_twice()
        {
            var decision = ChangeSets.AdmitAll(ChangeSets.Submission("S1", ChangeSets.Fb("FB_Motor")));
            var again = ChangeSets.AdmitAll(ChangeSets.Submission("S1", ChangeSets.Fb("FB_Valve")));

            var queues = QueuesWith(decision);

            Assert.Throws<InvalidOperationException>(() => queues.Enqueue(again));
        }

        [Fact]
        public void Excising_moves_a_submission_to_the_deferred_queue_with_its_reason_recorded()
        {
            // D32 step 5: excise the attributable block from the wave set, route it to the deferred
            // queue, re-download without it.
            var queues = QueuesWith(ChangeSets.AdmitAll(ChangeSets.Submission("S1", ChangeSets.Fb("FB_Motor"))));

            var moved = queues.Excise("S1", "raised DataBlockReinitialization, attributed to FB_Motor by DB-1");

            Assert.Empty(queues.RunQueue);
            Assert.Equal(DownloadQueue.DeferredQueue, moved.Queue);
            Assert.Contains("EXCISED", moved.Reason, StringComparison.Ordinal);
            Assert.Contains("attributed to FB_Motor", moved.Reason, StringComparison.Ordinal);
        }

        [Fact]
        public void An_excision_must_record_why()
        {
            // D32 caveat 1 turns on exactly this distinction: "no block is responsible" and "the locator
            // failed to find one" take the same branch and call for opposite fixes.
            var queues = QueuesWith(ChangeSets.AdmitAll(ChangeSets.Submission("S1", ChangeSets.Fb("FB_Motor"))));

            Assert.Throws<ArgumentException>(() => queues.Excise("S1", "   "));
            Assert.Single(queues.RunQueue);
        }

        [Fact]
        public void Excising_or_completing_something_that_is_not_there_is_an_error_not_a_silent_success()
        {
            var queues = new WaveQueues();

            Assert.Throws<InvalidOperationException>(() => queues.Excise("S-absent", "a reason"));
            Assert.Throws<InvalidOperationException>(() => queues.Complete("S-absent"));
        }

        [Fact]
        public void A_wave_boundary_ages_the_deferred_queue_and_nothing_acts_on_the_count()
        {
            // Visibility only: D24's condition alone permits a deferred submission to wait indefinitely
            // while RUN-class work keeps arriving. §1.4 accepts the delay; it says nothing about an
            // unbounded one, and choosing a threshold here would be adding a policy the spec has not.
            var queues = QueuesWith(
                ChangeSets.AdmitAll(ChangeSets.Submission("S-run", ChangeSets.Fb("FB_Motor"))),
                ChangeSets.AdmitAll(ChangeSets.Submission("S-stop", ChangeSets.Ob("OB_Cyclic"))));

            queues.NoteWaveBoundary();
            queues.NoteWaveBoundary();
            queues.NoteWaveBoundary();

            Assert.Equal(3, queues.DeferredQueue.Single().WaveBoundariesWaited);
            Assert.Equal(0, queues.RunQueue.Single().WaveBoundariesWaited);
        }

        // --- THE DRAIN GATE --------------------------------------------------------------------------

        [Fact]
        public void Draining_requires_a_decision_that_says_drain()
        {
            // R8: the disruptive mode must be "a SEPARATE, EXPLICITLY DECLARED DISRUPTIVE MODE — never a
            // flag on the wave-boundary path, and never a fallback reached by retry", permitted "ONLY
            // when the deferred queue is being drained".
            var queues = QueuesWith(
                ChangeSets.AdmitAll(ChangeSets.Submission("S-run", ChangeSets.Fb("FB_Motor"))),
                ChangeSets.AdmitAll(ChangeSets.Submission("S-stop", ChangeSets.Ob("OB_Cyclic"))));

            var doNotDrain = DrainPolicy.Decide(queues, ChangeSets.Deployed());
            Assert.Equal(DrainOutcome.DoNotDrain, doNotDrain.Outcome);

            Assert.Throws<InvalidOperationException>(() => queues.Drain(doNotDrain));
            Assert.Single(queues.DeferredQueue);
        }

        [Fact]
        public void A_drain_empties_the_deferred_queue_and_returns_what_was_in_it()
        {
            var queues = QueuesWith(ChangeSets.AdmitAll(ChangeSets.Submission("S-stop", ChangeSets.Ob("OB_Cyclic"))));

            var decision = DrainPolicy.Decide(queues, ChangeSets.Deployed());
            Assert.Equal(DrainOutcome.Drain, decision.Outcome);

            var drained = queues.Drain(decision);

            Assert.Equal(new[] { "S-stop" }, drained.Select(d => d.Submission.Id).ToArray());
            Assert.Empty(queues.DeferredQueue);
        }

        [Fact]
        public void Draining_an_empty_queue_is_refused_rather_than_reported_as_a_successful_drain()
        {
            // A drain that moved nothing and reported success would spend a CPU stop for no change.
            var queues = QueuesWith(ChangeSets.AdmitAll(ChangeSets.Submission("S-stop", ChangeSets.Ob("OB_Cyclic"))));
            var decision = DrainPolicy.Decide(queues, ChangeSets.Deployed());
            queues.Drain(decision);

            Assert.Throws<InvalidOperationException>(() => queues.Drain(decision));
        }
    }
}
