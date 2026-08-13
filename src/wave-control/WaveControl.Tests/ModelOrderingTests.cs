using System;
using System.Collections.Generic;
using System.Linq;

namespace Ladder.Wave.Tests
{
    /// <summary>
    /// 6.5 / X-I — models are untested code that every result depends on, and the model-before-consumer
    /// ordering over wave sets.
    /// </summary>
    public sealed class ModelOrderingTests
    {
        private const string CapProvenance = "K_max per §12a, derived 2026-08-13";
        private const string ModelHash = "sha256:model-1";

        private static TestSlot Consumer(string id, params string[] models) =>
            new TestSlot(id, "agent-" + id, new[] { "state/" + id }, "converter cross-check", models, 16);

        private static TestSlot ModelSlot(string id, string model) =>
            new TestSlot(id, "model-tester", new[] { "state/" + id }, "converter cross-check", null, 16, model);

        private static ModelUnderTest Model(string name = "M_Vessel", string hash = ModelHash) =>
            new ModelUnderTest(name, hash);

        private static ModelTestResult Result(bool passed = true, string hash = ModelHash, string name = "M_Vessel") =>
            new ModelTestResult(name, passed, hash, "wave 2026-08-13-001");

        private const string RunLoop = "Harness.Loop 2026-08-13";

        private static StopOnFailedWaveSetGate Gate(string forVersion = RunLoop) =>
            StopOnFailedWaveSetGate.DeclaredFor(
                forVersion,
                "harness lane",
                "the loop refuses to start wave set n+1 after wave set n reported a failure; covered by " +
                "a test in Harness.Loop");

        private static WaveSetPlan Plan(IEnumerable<TestSlot> slots)
        {
            var list = slots.ToArray();
            return WaveSetAdmission.Admit(list, ModelOrdering.EdgesFor(list), 6, CapProvenance);
        }

        // =============================================================================================
        // THE ENUMS, PINNED WHOLE
        // =============================================================================================

        [Fact]
        public void Every_readiness_state_is_in_exactly_one_bucket()
        {
            var admits = new[] { ModelReadiness.Passed, ModelReadiness.TestedEarlierInThisPlan };
            var refuses = new[]
            {
                ModelReadiness.NotChecked,
                ModelReadiness.NoResultExists,
                ModelReadiness.ResultIsStale,
                ModelReadiness.ResultIsFailing,
            };

            Assert.Equal(
                Enum.GetValues(typeof(ModelReadiness)).Cast<ModelReadiness>().OrderBy(r => (int)r).ToArray(),
                admits.Concat(refuses).OrderBy(r => (int)r).ToArray());

            foreach (var state in admits)
            {
                Assert.True(ModelReadinessStates.Admits(state));
                Assert.False(ModelReadinessStates.Refuses(state));
            }

            foreach (var state in refuses)
            {
                Assert.False(ModelReadinessStates.Admits(state));
                Assert.True(ModelReadinessStates.Refuses(state));
            }

            // *** NOT CHECKED IS THE ZERO VALUE. *** A dropped or defaulted readiness never reads as
            // passed.
            Assert.Equal(ModelReadiness.NotChecked, default(ModelReadiness));
            Assert.False(ModelReadinessStates.Admits(default(ModelReadiness)));
        }

        [Fact]
        public void An_absence_of_evidence_is_distinguished_from_established_bad_news()
        {
            // Different fixes: run the check, versus fix the model or write vectors for it.
            Assert.True(ModelReadinessStates.IsAnAbsenceOfEvidence(ModelReadiness.NotChecked));
            Assert.True(ModelReadinessStates.IsAnAbsenceOfEvidence(ModelReadiness.NoResultExists));
            Assert.False(ModelReadinessStates.IsAnAbsenceOfEvidence(ModelReadiness.ResultIsStale));
            Assert.False(ModelReadinessStates.IsAnAbsenceOfEvidence(ModelReadiness.ResultIsFailing));
        }

        [Fact]
        public void The_new_edge_kind_is_computed_and_ordered_and_the_enum_is_still_partitioned()
        {
            // X8 applied again after growing the enum, which is the whole point of pinning it whole.
            var computed = new[]
            {
                ConflictEdgeKind.OverlappingReachableState,
                ConflictEdgeKind.SharedModelInstance,
                ConflictEdgeKind.ModelUnderTestByAnotherSlot,
            };
            var manual = new[] { ConflictEdgeKind.Blacklist };
            var neither = new[] { ConflictEdgeKind.Unstated };

            Assert.Equal(
                Enum.GetValues(typeof(ConflictEdgeKind)).Cast<ConflictEdgeKind>().OrderBy(k => (int)k).ToArray(),
                computed.Concat(manual).Concat(neither).OrderBy(k => (int)k).ToArray());

            // *** EXACTLY ONE KIND CARRIES A DIRECTION. *** Every other edge is symmetric: "these two
            // cannot run together" says nothing about which runs first.
            var ordered = Enum.GetValues(typeof(ConflictEdgeKind))
                .Cast<ConflictEdgeKind>()
                .Where(ConflictEdgeKinds.IsOrdered)
                .ToArray();

            Assert.Equal(new[] { ConflictEdgeKind.ModelUnderTestByAnotherSlot }, ordered);
            Assert.True(ConflictEdgeKinds.IsComputed(ConflictEdgeKind.ModelUnderTestByAnotherSlot));
            Assert.False(ConflictEdgeKinds.IsManual(ConflictEdgeKind.ModelUnderTestByAnotherSlot));
        }

        [Fact]
        public void The_ordering_defect_zero_value_is_none()
        {
            Assert.Equal(OrderingDefect.None, default(OrderingDefect));
        }

        // =============================================================================================
        // AN UNTESTED MODEL IS NOT A PASSING MODEL
        // =============================================================================================

        [Fact]
        public void No_result_set_at_all_is_NOT_CHECKED_and_is_not_the_same_as_no_result()
        {
            // "Nobody asked" and "we asked and there is none" are different facts.
            var neverAsked = ModelReadinessCheck.Check(Model(), null);
            var asked = ModelReadinessCheck.Check(Model(), new ModelTestResult[0]);

            Assert.Equal(ModelReadiness.NotChecked, neverAsked.Readiness);
            Assert.Equal(ModelReadiness.NoResultExists, asked.Readiness);
            Assert.False(neverAsked.Admits);
            Assert.False(asked.Admits);
        }

        [Fact]
        public void A_result_for_different_content_is_STALE_and_not_absent()
        {
            // The STAMP is what makes this a comparison rather than a declaration.
            var verdict = ModelReadinessCheck.Check(Model(hash: "sha256:model-2"), new[] { Result() });

            Assert.Equal(ModelReadiness.ResultIsStale, verdict.Readiness);
            Assert.Contains("re-run its vectors rather than write new ones", verdict.Reason, StringComparison.Ordinal);
        }

        [Fact]
        public void A_model_with_no_current_hash_cannot_be_shown_current_by_any_result()
        {
            // Nothing falls back to trusting the result's own stamp — that would answer "has this
            // changed?" with the value being checked.
            var verdict = ModelReadinessCheck.Check(new ModelUnderTest("M_Vessel", null), new[] { Result() });

            Assert.Equal(ModelReadiness.ResultIsStale, verdict.Readiness);
            Assert.Contains("nothing could be compared", verdict.Reason, StringComparison.Ordinal);
        }

        [Fact]
        public void A_failing_result_against_current_content_refuses()
        {
            var verdict = ModelReadinessCheck.Check(Model(), new[] { Result(passed: false) });

            Assert.Equal(ModelReadiness.ResultIsFailing, verdict.Readiness);
            Assert.False(verdict.Admits);
        }

        [Fact]
        public void A_current_passing_result_admits()
        {
            // The converse, without which every refusal above would pass against a check that refused
            // everything.
            var verdict = ModelReadinessCheck.Check(Model(), new[] { Result() });

            Assert.Equal(ModelReadiness.Passed, verdict.Readiness);
            Assert.True(verdict.Admits);
        }

        // =============================================================================================
        // THE EDGES ARE COMPUTED FROM THE DEPENDENCY, NEVER DECLARED
        // =============================================================================================

        [Fact]
        public void A_model_dependency_derives_its_own_conflict_edge()
        {
            var slots = new[] { ModelSlot("S_model", "M_Vessel"), Consumer("S_block", "M_Vessel") };

            var edges = ModelOrdering.EdgesFor(slots);

            Assert.Single(edges);
            Assert.Equal(ConflictEdgeKind.ModelUnderTestByAnotherSlot, edges[0].Kind);
            Assert.NotEqual(string.Empty, edges[0].Reason);
        }

        [Fact]
        public void A_model_nobody_tests_in_this_plan_derives_no_edge()
        {
            // The did-not-run case for the derivation: it must fire on a dependency that EXISTS in the
            // plan, not on every mention of a model.
            var edges = ModelOrdering.EdgesFor(new[] { Consumer("S_block", "M_Vessel") });

            Assert.Empty(edges);
        }

        [Fact]
        public void A_model_slot_that_also_uses_its_own_model_derives_no_self_edge()
        {
            var selfish = new TestSlot("S_model", "a", new[] { "x" }, "cross-check", new[] { "M_Vessel" }, 16, "M_Vessel");

            Assert.Empty(ModelOrdering.EdgesFor(new[] { selfish }));
        }

        [Fact]
        public void The_derived_edge_separates_the_model_from_its_consumer()
        {
            var slots = new[] { ModelSlot("S_model", "M_Vessel"), Consumer("S_block", "M_Vessel") };

            var plan = Plan(slots);

            Assert.Equal(AdmissionPlanOutcome.Admitted, plan.Outcome);
            Assert.Equal(2, plan.WaveCount);
        }

        // =============================================================================================
        // THE ORDERING
        // =============================================================================================

        [Fact]
        public void The_model_slot_runs_before_the_slots_depending_on_it()
        {
            var slots = new[] { ModelSlot("S_model", "M_Vessel"), Consumer("S_block", "M_Vessel") };

            var ordering = ModelOrdering.Order(Plan(slots), new[] { Model() }, new[] { Result() });

            Assert.True(ordering.Ordered);
            Assert.Equal(2, ordering.OrderedWaveSets.Count);
            Assert.Contains(ordering.OrderedWaveSets[0].Slots, s => s.Id == "S_model");
            Assert.Contains(ordering.OrderedWaveSets[1].Slots, s => s.Id == "S_block");
        }

        [Fact]
        public void The_order_is_produced_even_when_the_colouring_numbered_them_the_other_way()
        {
            // Ordering is COMPUTED from the dependency, so it must not depend on the colour numbers the
            // colourer happened to assign. "A_consumer" sorts before "Z_model", so greedy gives the
            // consumer the lower colour — and the run order must still put the model first.
            var slots = new[] { Consumer("A_consumer", "M_Vessel"), ModelSlot("Z_model", "M_Vessel") };

            var ordering = ModelOrdering.Order(Plan(slots), new[] { Model() }, new[] { Result() });

            Assert.True(ordering.Ordered);
            Assert.Contains(ordering.OrderedWaveSets[0].Slots, s => s.Id == "Z_model");
            Assert.Contains(ordering.OrderedWaveSets[1].Slots, s => s.Id == "A_consumer");
        }

        [Fact]
        public void A_consumer_whose_model_is_not_ready_is_refused()
        {
            var slots = new[] { Consumer("S_block", "M_Vessel") };

            var ordering = ModelOrdering.Order(Plan(slots), new[] { Model() }, new ModelTestResult[0]);

            Assert.False(ordering.Ordered);
            Assert.Contains(ordering.Findings, f => f.Defect == OrderingDefect.ConsumerDependsOnAModelThatIsNotReady);
            Assert.Contains(ordering.Readiness, r => r.Readiness == ModelReadiness.NoResultExists);
        }

        [Fact]
        public void A_model_that_was_never_supplied_could_not_even_be_asked_about()
        {
            var ordering = ModelOrdering.Order(Plan(new[] { Consumer("S_block", "M_Vessel") }), null, new[] { Result() });

            Assert.False(ordering.Ordered);
            Assert.Contains(ordering.Findings, f => f.Defect == OrderingDefect.ModelNotSupplied);
        }

        [Fact]
        public void Two_slots_testing_the_same_model_is_refused()
        {
            var slots = new[]
            {
                ModelSlot("S_model_a", "M_Vessel"),
                ModelSlot("S_model_b", "M_Vessel"),
                Consumer("S_block", "M_Vessel"),
            };

            var ordering = ModelOrdering.Order(Plan(slots), new[] { Model() }, new[] { Result() });

            Assert.False(ordering.Ordered);
            Assert.Contains(ordering.Findings, f => f.Defect == OrderingDefect.TwoSlotsTestTheSameModel);
        }

        [Fact]
        public void A_circular_model_dependency_has_NO_run_order_and_says_so()
        {
            // Not a bad order — an unorderable set. Two models whose slots depend on each other.
            var slots = new[]
            {
                new TestSlot("S_model_a", "t", new[] { "a" }, "cross-check", new[] { "M_B" }, 16, "M_A"),
                new TestSlot("S_model_b", "t", new[] { "b" }, "cross-check", new[] { "M_A" }, 16, "M_B"),
            };

            var ordering = ModelOrdering.Order(
                Plan(slots),
                new[] { Model("M_A", "h-a"), Model("M_B", "h-b") },
                new[] { Result(true, "h-a", "M_A"), Result(true, "h-b", "M_B") });

            Assert.False(ordering.Ordered);
            Assert.Contains(ordering.Findings, f => f.Defect == OrderingDefect.CircularModelDependency);
            Assert.Contains("NO RUN ORDER", ordering.Describe(), StringComparison.Ordinal);
        }

        [Fact]
        public void An_unadmitted_colouring_is_not_ordered()
        {
            var refused = WaveSetAdmission.Admit(new[] { Consumer("S1") }, null, null, CapProvenance);

            var ordering = ModelOrdering.Order(refused, new[] { Model() }, new[] { Result() });

            Assert.False(ordering.Ordered);
            Assert.Empty(ordering.OrderedWaveSets);
        }

        [Fact]
        public void A_model_and_its_consumer_sharing_a_wave_set_cannot_be_fixed_by_reordering()
        {
            // Admitted WITHOUT the derived edges, so the colouring never separated them. The finding
            // must say that reordering is not the fix.
            var slots = new[] { ModelSlot("S_model", "M_Vessel"), Consumer("S_block", "M_Vessel") };
            var unseparated = WaveSetAdmission.Admit(slots, null, 6, CapProvenance);

            Assert.Equal(1, unseparated.WaveCount);

            var ordering = ModelOrdering.Order(unseparated, new[] { Model() }, new[] { Result() });

            Assert.False(ordering.Ordered);
            Assert.Contains(ordering.Findings, f => f.Defect == OrderingDefect.ModelAndConsumerShareAWaveSet);
            Assert.Contains("REORDERING CANNOT FIX THIS", ordering.Findings[0].Detail, StringComparison.Ordinal);
        }

        // =============================================================================================
        // THE VERIFIER — BUILT REACHABLE FROM THE START, PER Y7b
        // =============================================================================================

        [Fact]
        public void The_verifier_catches_a_consumer_ordered_BEFORE_its_model()
        {
            // *** Y7b'S QUESTION ASKED IN ADVANCE. *** The orderer never emits an inverted order, so a
            // check buried inside it could be reached by no input at all. Pointed at an order the
            // producer would never generate, it has to actually check something.
            var model = ModelSlot("S_model", "M_Vessel");
            var consumer = Consumer("S_block", "M_Vessel");

            var inverted = new[]
            {
                WaveSetAdmission.WaveSetOf(0, new[] { consumer }),
                WaveSetAdmission.WaveSetOf(1, new[] { model }),
            };

            var findings = ModelOrdering.Verify(inverted, new[] { model, consumer });

            Assert.Contains(findings, f => f.Defect == OrderingDefect.ConsumerRunsBeforeItsModel);
        }

        [Fact]
        public void The_verifier_catches_a_model_and_consumer_in_one_wave_set()
        {
            var model = ModelSlot("S_model", "M_Vessel");
            var consumer = Consumer("S_block", "M_Vessel");

            var together = new[] { WaveSetAdmission.WaveSetOf(0, new[] { model, consumer }) };

            var findings = ModelOrdering.Verify(together, new[] { model, consumer });

            Assert.Contains(findings, f => f.Defect == OrderingDefect.ModelAndConsumerShareAWaveSet);
        }

        [Fact]
        public void The_verifier_passes_a_correct_order()
        {
            // The converse, without which the two above would pass against a verifier that reported
            // everything.
            var model = ModelSlot("S_model", "M_Vessel");
            var consumer = Consumer("S_block", "M_Vessel");

            var correct = new[]
            {
                WaveSetAdmission.WaveSetOf(0, new[] { model }),
                WaveSetAdmission.WaveSetOf(1, new[] { consumer }),
            };

            Assert.Empty(ModelOrdering.Verify(correct, new[] { model, consumer }));
        }

        [Fact]
        public void The_verifier_ignores_a_model_that_is_not_in_the_order_at_all()
        {
            // A consumer whose model is tested in an earlier SUBMISSION has no model slot here, and that
            // is not an ordering violation — readiness handles it.
            var consumer = Consumer("S_block", "M_Vessel");

            Assert.Empty(ModelOrdering.Verify(
                new[] { WaveSetAdmission.WaveSetOf(0, new[] { consumer }) },
                new[] { consumer }));
        }

        // =============================================================================================
        // THE AMBIGUITY, RAISED RATHER THAN PICKED
        // =============================================================================================

        [Fact]
        public void Reading_b_is_REFUSED_when_the_stop_on_failed_wave_set_gate_is_not_established()
        {
            // *** OWNER'S RULING 2026-08-13, AND THIS TEST REPLACES ONE THAT ENCODED THE PRE-RULING
            // BEHAVIOUR. *** It used to assert that a consumer relying on an in-plan model was ORDERED
            // and merely flagged. (b) is now permitted only once the gate demonstrably exists: without
            // it the consumer's results would be produced and BELIEVED after its model failed — a wrong
            // answer that looks like a result, not a missing check.
            var slots = new[] { ModelSlot("S_model", "M_Vessel"), Consumer("S_block", "M_Vessel") };

            var ordering = ModelOrdering.Order(Plan(slots), new[] { Model() }, new ModelTestResult[0]);

            Assert.False(ordering.Ordered);
            Assert.True(ordering.RestsOnABetweenWaveSetGate);
            Assert.Equal(RunLoopGateState.NotDeclared, ordering.GateState);
            Assert.Contains(ordering.Findings, f => f.Defect == OrderingDefect.ReadingBRefusedBecauseTheGateIsNotEstablished);
            Assert.Contains(ordering.Readiness, r => r.Readiness == ModelReadiness.TestedEarlierInThisPlan);
        }

        [Fact]
        public void Reading_b_is_ADMITTED_once_the_gate_is_established()
        {
            // *** THE DID-NOT-RUN CASE FOR THE REFUSAL ITSELF. *** Without this the refusal could be
            // vacuously always-on and no test would notice.
            var slots = new[] { ModelSlot("S_model", "M_Vessel"), Consumer("S_block", "M_Vessel") };

            var ordering = ModelOrdering.Order(
                Plan(slots), new[] { Model() }, new ModelTestResult[0], Gate(), RunLoop);

            Assert.True(ordering.Ordered);
            Assert.True(ordering.RestsOnABetweenWaveSetGate);
            Assert.Equal(RunLoopGateState.Established, ordering.GateState);
            Assert.Contains(ordering.OrderedWaveSets[0].Slots, x => x.Id == "S_model");
            Assert.Contains("only as good as whoever supplied it", ordering.Describe(), StringComparison.Ordinal);
        }

        [Fact]
        public void A_gate_declared_for_a_DIFFERENT_run_loop_is_stale_and_still_refuses()
        {
            // Two facts, not one: "nobody declared it" and "somebody did the work once and the loop has
            // moved on". The second is the more dangerous, because it reads as done.
            var slots = new[] { ModelSlot("S_model", "M_Vessel"), Consumer("S_block", "M_Vessel") };

            var ordering = ModelOrdering.Order(
                Plan(slots), new[] { Model() }, new ModelTestResult[0], Gate("Harness.Loop 2026-01-01"), RunLoop);

            Assert.False(ordering.Ordered);
            Assert.Equal(RunLoopGateState.DeclaredForADifferentRunLoop, ordering.GateState);
        }

        [Fact]
        public void A_gate_with_no_run_loop_in_use_to_compare_against_is_not_established()
        {
            // An unchecked stamp is not a stamp.
            var slots = new[] { ModelSlot("S_model", "M_Vessel"), Consumer("S_block", "M_Vessel") };

            var ordering = ModelOrdering.Order(Plan(slots), new[] { Model() }, new ModelTestResult[0], Gate(), null);

            Assert.False(ordering.Ordered);
            Assert.Equal(RunLoopGateState.NotDeclared, ordering.GateState);
        }

        [Fact]
        public void A_plan_that_does_not_use_reading_b_is_unaffected_by_the_gate()
        {
            // *** THE GATE IS NOT A BLANKET REQUIREMENT. *** Making it one would refuse every ordinary
            // plan for a dependency it does not have — and a check that refuses everything gets removed.
            var ordering = ModelOrdering.Order(
                Plan(new[] { Consumer("S_block", "M_Vessel") }), new[] { Model() }, new[] { Result() });

            Assert.True(ordering.Ordered);
            Assert.False(ordering.RestsOnABetweenWaveSetGate);
            Assert.DoesNotContain(ordering.Findings, f => f.Defect == OrderingDefect.ReadingBRefusedBecauseTheGateIsNotEstablished);
        }

        [Fact]
        public void A_gate_declaration_must_name_a_loop_an_author_and_how_it_is_known()
        {
            // It is the only thing standing between reading (b) and a wrong answer that looks like a
            // result, so a declaration nobody had to justify is not one.
            Assert.Throws<ArgumentException>(() => StopOnFailedWaveSetGate.DeclaredFor("  ", "a", "a long enough evidence string"));
            Assert.Throws<ArgumentException>(() => StopOnFailedWaveSetGate.DeclaredFor("v", "  ", "a long enough evidence string"));
            Assert.Throws<ArgumentException>(() => StopOnFailedWaveSetGate.DeclaredFor("v", "a", "yes"));
        }

        [Fact]
        public void Every_run_loop_gate_state_is_in_exactly_one_bucket()
        {
            var establishes = new[] { RunLoopGateState.Established };
            var refuses = new[] { RunLoopGateState.NotDeclared, RunLoopGateState.DeclaredForADifferentRunLoop };

            Assert.Equal(
                Enum.GetValues(typeof(RunLoopGateState)).Cast<RunLoopGateState>().OrderBy(g => (int)g).ToArray(),
                establishes.Concat(refuses).OrderBy(g => (int)g).ToArray());

            Assert.Equal(RunLoopGateState.NotDeclared, default(RunLoopGateState));
            Assert.False(RunLoopGateStates.Establishes(default(RunLoopGateState)));

            foreach (var state in refuses)
            {
                Assert.True(RunLoopGateStates.Refuses(state));
            }
        }

        [Fact]
        public void No_ordering_defect_is_merely_informational()
        {
            // A finding that is reported and does not gate is the "a warning is not a gate" shape.
            foreach (var defect in Enum.GetValues(typeof(OrderingDefect)).Cast<OrderingDefect>())
            {
                Assert.Equal(defect != OrderingDefect.None, OrderingDefects.RefusesThePlan(defect));
            }
        }

        [Fact]
        public void A_consumer_resting_on_a_PRIOR_passing_result_does_not_raise_the_flag()
        {
            // The did-not-run case for the flag: it must mark the ambiguous route ONLY, or it becomes
            // noise on every plan and stops being read.
            var ordering = ModelOrdering.Order(
                Plan(new[] { Consumer("S_block", "M_Vessel") }),
                new[] { Model() },
                new[] { Result() });

            Assert.True(ordering.Ordered);
            Assert.False(ordering.RestsOnABetweenWaveSetGate);
            Assert.DoesNotContain("RESTS ON A BETWEEN-WAVE-SET GATE", ordering.Describe(), StringComparison.Ordinal);
        }

        [Fact]
        public void The_plan_repeats_X_Is_own_residual_rather_than_letting_a_green_overstate_itself()
        {
            // Passing its own tests makes a model faithful to its SPECIFICATION, not to the plant.
            var ordering = ModelOrdering.Order(
                Plan(new[] { Consumer("S_block", "M_Vessel") }),
                new[] { Model() },
                new[] { Result() });

            Assert.Contains("faithful to its SPECIFICATION, NOT TO THE PLANT", ordering.Summary, StringComparison.Ordinal);
            Assert.Contains("PHYSICS NOBODY THOUGHT TO INCLUDE IS NOT", ordering.Summary, StringComparison.Ordinal);

            // *** AND IT REACHES A READER OF RESULTS, NOT ONLY A LANE REPORT. *** Every plan emits it,
            // ordered or refused, so a caller printing only the outcome cannot drop it.
            Assert.Contains(ModelOrderingPlan.FidelityResidual, ordering.Describe(), StringComparison.Ordinal);

            var refused = ModelOrdering.Order(Plan(new[] { Consumer("S_x", "M_Absent") }), null, null);
            Assert.False(refused.Ordered);
            Assert.Contains(ModelOrderingPlan.FidelityResidual, refused.Describe(), StringComparison.Ordinal);
        }
    }
}
