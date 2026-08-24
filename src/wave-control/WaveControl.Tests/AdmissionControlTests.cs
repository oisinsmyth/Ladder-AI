using System;
using System.Collections.Generic;
using System.Linq;

namespace Ladder.Wave.Tests
{
    /// <summary>
    /// LOOP 2 (§1.2, R6) — a submission enters the test project only after passing preflight and
    /// compiling clean in isolation, and only if every change in it can be routed.
    /// </summary>
    public sealed class AdmissionControlTests
    {
        [Fact]
        public void A_submission_whose_objects_all_pass_is_admitted_into_the_run_queue()
        {
            var fb = ChangeSets.Fb("FB_Motor");
            var idb = ChangeSets.InstanceDb("iDB_Motor", "FB_Motor");
            var submission = ChangeSets.Submission("S1", fb, idb);

            var decision = AdmissionController.Admit(submission, ChangeSets.GoodEvidence(fb, idb));

            Assert.Equal(AdmissionOutcome.Admitted, decision.Outcome);
            Assert.Equal(DownloadQueue.RunQueue, decision.Queue);
            Assert.Empty(decision.Findings);
        }

        // --- NOTHING EXAMINED IS NOT A PASS ----------------------------------------------------------

        [Fact]
        public void An_empty_submission_is_reported_as_nothing_submitted_never_as_admitted()
        {
            // FI-44's shape: `compile-all` once claimed a pass having examined nothing.
            var decision = AdmissionController.Admit(new Submission("S-empty", "agent", new ChangedObject[0]), null);

            Assert.Equal(AdmissionOutcome.NothingSubmitted, decision.Outcome);
            Assert.False(decision.Admitted);
            Assert.Equal(DownloadQueue.Unassigned, decision.Queue);
            Assert.Contains("Nothing was examined", decision.Summary, StringComparison.Ordinal);
        }

        [Fact]
        public void Nothing_submitted_is_the_zero_value_of_the_outcome()
        {
            Assert.Equal(AdmissionOutcome.NothingSubmitted, default(AdmissionOutcome));
        }

        // --- THE EVIDENCE GATE -----------------------------------------------------------------------

        [Fact]
        public void An_object_with_no_evidence_at_all_is_refused()
        {
            var fb = ChangeSets.Fb("FB_Motor");

            var decision = AdmissionController.Admit(ChangeSets.Submission("S1", fb), null);

            Assert.Equal(AdmissionOutcome.Refused, decision.Outcome);
            Assert.Contains(decision.Findings, f => f.Kind == AdmissionFindingKind.NoEvidence);
        }

        [Theory]
        [InlineData(EvidenceOutcome.NotProvided, EvidenceOutcome.Passed, AdmissionFindingKind.CheckNotRun)]
        [InlineData(EvidenceOutcome.Passed, EvidenceOutcome.NotProvided, AdmissionFindingKind.CheckNotRun)]
        [InlineData(EvidenceOutcome.Failed, EvidenceOutcome.Passed, AdmissionFindingKind.CheckFailed)]
        [InlineData(EvidenceOutcome.Passed, EvidenceOutcome.Failed, AdmissionFindingKind.CheckFailed)]
        public void Each_half_of_the_gate_must_positively_pass(
            EvidenceOutcome preflight, EvidenceOutcome compile, AdmissionFindingKind expected)
        {
            var fb = ChangeSets.Fb("FB_Motor");
            var evidence = new[] { new AdmissionEvidence("FB_Motor", ChangeSets.Hash, preflight, compile, "fixture") };

            var decision = AdmissionController.Admit(ChangeSets.Submission("S1", fb), evidence);

            Assert.Equal(AdmissionOutcome.Refused, decision.Outcome);
            Assert.Contains(decision.Findings, f => f.Kind == expected);
        }

        [Fact]
        public void Evidence_about_different_content_is_refused_as_stale()
        {
            // A clean compile of the previous edit is not evidence about this one. Without the hash
            // comparison the gate is a formality any stale run satisfies.
            var fb = ChangeSets.Fb("FB_Motor", ChangeSets.Hash);
            var evidence = new[]
            {
                new AdmissionEvidence("FB_Motor", ChangeSets.OtherHash, EvidenceOutcome.Passed, EvidenceOutcome.Passed, "an earlier run"),
            };

            var decision = AdmissionController.Admit(ChangeSets.Submission("S1", fb), evidence);

            Assert.Equal(AdmissionOutcome.Refused, decision.Outcome);
            Assert.Contains(decision.Findings, f => f.Kind == AdmissionFindingKind.EvidenceStale);
        }

        [Fact]
        public void An_object_with_no_hash_cannot_be_admitted_even_on_passing_evidence()
        {
            var fb = ChangeSets.Fb("FB_Motor", hash: null);
            var evidence = new[]
            {
                new AdmissionEvidence("FB_Motor", ChangeSets.Hash, EvidenceOutcome.Passed, EvidenceOutcome.Passed, "fixture"),
            };

            var decision = AdmissionController.Admit(ChangeSets.Submission("S1", fb), evidence);

            Assert.Equal(AdmissionOutcome.Refused, decision.Outcome);
            Assert.Contains(decision.Findings, f => f.Kind == AdmissionFindingKind.EvidenceStale);
        }

        [Fact]
        public void Evidence_for_a_different_object_does_not_satisfy_this_one()
        {
            var fb = ChangeSets.Fb("FB_Motor");
            var evidence = ChangeSets.GoodEvidence(ChangeSets.Fb("FB_Valve"));

            var decision = AdmissionController.Admit(ChangeSets.Submission("S1", fb), evidence);

            Assert.Equal(AdmissionOutcome.Refused, decision.Outcome);
            Assert.Contains(decision.Findings, f => f.Kind == AdmissionFindingKind.NoEvidence && f.ObjectName == "FB_Motor");
        }

        // --- ATOMICITY (DB-9) ------------------------------------------------------------------------

        [Fact]
        public void One_bad_object_refuses_the_whole_submission()
        {
            var good = ChangeSets.Fb("FB_Motor");
            var bad = ChangeSets.Fb("FB_Broken");
            var evidence = ChangeSets.GoodEvidence(good)
                .Concat(new[] { new AdmissionEvidence("FB_Broken", ChangeSets.Hash, EvidenceOutcome.Passed, EvidenceOutcome.Failed, "fixture") });

            var decision = AdmissionController.Admit(ChangeSets.Submission("S1", good, bad), evidence);

            Assert.Equal(AdmissionOutcome.Refused, decision.Outcome);
            Assert.Equal(DownloadQueue.Unassigned, decision.Queue);
        }

        [Fact]
        public void Every_finding_is_reported_not_only_the_first()
        {
            // An agent that fixes one defect per round trip costs a wave per defect.
            var a = ChangeSets.Fb("FB_A");
            var b = ChangeSets.Fb("FB_B");
            var c = ChangeSets.Fb("FB_C");

            var decision = AdmissionController.Admit(ChangeSets.Submission("S1", a, b, c), null);

            Assert.Equal(AdmissionOutcome.Refused, decision.Outcome);
            Assert.Equal(3, decision.Findings.Count(f => f.Kind == AdmissionFindingKind.NoEvidence));
        }

        [Fact]
        public void A_duplicate_object_name_within_a_submission_is_refused()
        {
            var a = ChangeSets.Fb("FB_A");
            var again = ChangeSets.Fb("fb_a");

            var decision = AdmissionController.Admit(ChangeSets.Submission("S1", a, again), ChangeSets.GoodEvidence(a, again));

            Assert.Equal(AdmissionOutcome.Refused, decision.Outcome);
            Assert.Contains(decision.Findings, f => f.Kind == AdmissionFindingKind.DuplicateObjectName);
        }

        [Fact]
        public void An_unidentified_submission_is_refused()
        {
            var fb = ChangeSets.Fb("FB_Motor");

            var decision = AdmissionController.Admit(new Submission(null, null, new[] { fb }), ChangeSets.GoodEvidence(fb));

            Assert.Equal(AdmissionOutcome.Refused, decision.Outcome);
            Assert.Equal(2, decision.Findings.Count(f => f.Kind == AdmissionFindingKind.SubmissionNotIdentified));
        }

        [Fact]
        public void An_unroutable_change_refuses_the_submission()
        {
            var fb = new ChangedObject("FB_Mystery", ObjectKind.FunctionBlock, ChangeClass.Unknown, null, ChangeSets.Hash);

            var decision = AdmissionController.Admit(ChangeSets.Submission("S1", fb), ChangeSets.GoodEvidence(fb));

            Assert.Equal(AdmissionOutcome.Refused, decision.Outcome);
            Assert.Contains(decision.Findings, f => f.Kind == AdmissionFindingKind.Unroutable);
        }

        // --- THE READING THIS COMPONENT TAKES --------------------------------------------------------

        [Fact]
        public void A_submission_with_any_stop_class_object_is_held_as_a_unit_for_the_drain()
        {
            // §1.4 routes CHANGES; DB-9 admits SUBMISSIONS whole. Where one submission's objects route
            // both ways, the whole submission takes the later queue: splitting it would leave the test
            // project holding half a test set, and a wave in between could test against it.
            var fb = ChangeSets.Fb("FB_Motor");
            var ob = ChangeSets.Ob("OB_Cyclic");
            var submission = ChangeSets.Submission("S1", fb, ob);

            var decision = AdmissionController.Admit(submission, ChangeSets.GoodEvidence(fb, ob));

            Assert.Equal(AdmissionOutcome.Admitted, decision.Outcome);
            Assert.Equal(DownloadQueue.DeferredQueue, decision.Queue);
            Assert.True(decision.DeferredAsAUnit);
            Assert.Equal(new[] { "OB_Cyclic" }, decision.StopClassObjects.Select(o => o.Name).ToArray());
        }

        [Fact]
        public void A_refused_submission_still_carries_its_routing_for_the_log()
        {
            var fb = ChangeSets.Fb("FB_Motor");

            var decision = AdmissionController.Admit(ChangeSets.Submission("S1", fb), null);

            Assert.Single(decision.Routing);
            Assert.Contains("FB_Motor", decision.Describe(), StringComparison.Ordinal);
        }

        // --- WHAT ADMISSION DOES NOT CHECK: WHO IS ASKING --------------------------------------------

        /// <summary>
        /// 🔴 <b>THIS TEST PASSES TRIVIALLY, AND THE PASS IS THE FINDING — do not read it as evidence of
        /// an identity check, because it is the record that there is none.</b>
        ///
        /// <para>Two DIFFERENT agents submit changes to THE SAME object into one wave, and both are
        /// admitted, both into the run queue, with no finding between them. <see cref="AdmissionController"/>
        /// looks at <c>submission.Agent</c> exactly once, to ask whether it is EMPTY (results are
        /// distributed per agent, §2.4, so an unnamed submitter has nobody to distribute to). It never
        /// compares one submission's agent against another's, and it holds no cross-submission state in
        /// which it could: <c>Admit</c> is a pure function of one submission plus its evidence.</para>
        ///
        /// <para><b>That is a division of labour, not a hole here.</b> Preventing two agents from editing
        /// one object is the CLAIM REGISTRY's job (<c>converter claim --kind block-edit</c>, FI-65) and
        /// happens long before a submission exists; admission's question is whether THIS content passed
        /// preflight and compiled clean. The reason to write it down in a test is that the two components
        /// are read together, and "wave-control admits per submission" invites the assumption that
        /// something in this path would notice a collision. Nothing in this path would.</para>
        ///
        /// <para>So: if a collision between two agents is ever to be refused at admission, this test is
        /// the one that must change, and its changing is the visible sign that a policy was added. Until
        /// then it documents the boundary — and a green here says nothing whatever about whether either
        /// agent had claimed the object.</para>
        /// </summary>
        [Fact]
        public void Two_different_agents_changing_one_object_are_both_admitted_because_admission_is_agent_blind()
        {
            var mine = ChangeSets.Fb("FB_Motor");
            var theirs = ChangeSets.Fb("FB_Motor");
            var first = new Submission("S-alpha", "agent-alpha", new[] { mine });
            var second = new Submission("S-beta", "agent-beta", new[] { theirs });

            // The denominator: the situation this test is about was actually set up. Two submissions
            // carrying the same object name and two genuinely different agent strings — without both,
            // the greens below would be about nothing.
            Assert.NotEqual(first.Agent, second.Agent);
            Assert.Equal(first.Objects[0].Name, second.Objects[0].Name);

            var decisionA = AdmissionController.Admit(first, ChangeSets.GoodEvidence(mine));
            var decisionB = AdmissionController.Admit(second, ChangeSets.GoodEvidence(theirs));

            Assert.Equal(AdmissionOutcome.Admitted, decisionA.Outcome);
            Assert.Equal(AdmissionOutcome.Admitted, decisionB.Outcome);
            Assert.Equal(DownloadQueue.RunQueue, decisionA.Queue);
            Assert.Equal(DownloadQueue.RunQueue, decisionB.Queue);

            // Not "no identity finding" — NO finding at all. A single finding of any kind would mean
            // something in this path had an opinion about the pair, and the boundary this test records
            // would be somewhere other than where it says it is.
            Assert.Empty(decisionA.Findings);
            Assert.Empty(decisionB.Findings);
        }
    }
}
