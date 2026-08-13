using System;
using System.Collections.Generic;
using System.Linq;

namespace Ladder.Wave.Tests
{
    /// <summary>
    /// 6.1 / DB-13 — wave-set admission: which slots may share a wave set.
    /// </summary>
    public sealed class WaveSetAdmissionTests
    {
        private const string CapProvenance = "K_max = floor(S x 23.33 / RTT_p99) per §12a, derived 2026-08-13";

        private static TestSlot Slot(string id, int width = 16, params string[] models) =>
            new TestSlot(id, "agent-" + id, new[] { "state/" + id }, "converter cross-check 2026-08-13", models, width);

        private static ConflictEdge Computed(string a, string b) =>
            new ConflictEdge(a, b, ConflictEdgeKind.OverlappingReachableState);

        private static ConflictEdge Blacklisted(string a, string b, string reason = "shares the vessel model") =>
            new ConflictEdge(a, b, ConflictEdgeKind.Blacklist, reason);

        // =============================================================================================
        // THE ENUM IS PINNED WHOLE — the X8 generalisation, applied deliberately
        // =============================================================================================

        [Fact]
        public void Every_conflict_edge_kind_is_in_exactly_one_bucket()
        {
            // *** THIS LANE HAS ALREADY HAD TWO PROPERTIES DEFINED BY NEGATION SILENTLY ACQUIRE A NEW
            // MEANING WHEN AN ENUM GREW *** — and the second was found by the test written for the
            // first. This enum will grow, so the partition is asserted over the WHOLE enum rather than
            // over the cases anybody thought of.
            var computed = new[] { ConflictEdgeKind.OverlappingReachableState, ConflictEdgeKind.SharedModelInstance };
            var manual = new[] { ConflictEdgeKind.Blacklist };
            var neither = new[] { ConflictEdgeKind.Unstated };

            Assert.Equal(
                Enum.GetValues(typeof(ConflictEdgeKind)).Cast<ConflictEdgeKind>().OrderBy(k => (int)k).ToArray(),
                computed.Concat(manual).Concat(neither).OrderBy(k => (int)k).ToArray());

            foreach (var kind in computed)
            {
                Assert.True(ConflictEdgeKinds.IsComputed(kind));
                Assert.False(ConflictEdgeKinds.IsManual(kind));
                Assert.True(ConflictEdgeKinds.IsStated(kind));
            }

            foreach (var kind in manual)
            {
                Assert.False(ConflictEdgeKinds.IsComputed(kind));
                Assert.True(ConflictEdgeKinds.IsManual(kind));
                Assert.True(ConflictEdgeKinds.IsStated(kind));
            }

            foreach (var kind in neither)
            {
                Assert.False(ConflictEdgeKinds.IsComputed(kind));
                Assert.False(ConflictEdgeKinds.IsManual(kind));
                Assert.False(ConflictEdgeKinds.IsStated(kind));
            }
        }

        [Fact]
        public void Exactly_one_edge_kind_is_manual_and_it_is_the_blacklist()
        {
            // If a second manual kind is ever added, D22's add-only argument has to be re-made for it —
            // this is what forces that conversation instead of letting it happen quietly.
            var manual = Enum.GetValues(typeof(ConflictEdgeKind))
                .Cast<ConflictEdgeKind>()
                .Where(ConflictEdgeKinds.IsManual)
                .ToArray();

            Assert.Equal(new[] { ConflictEdgeKind.Blacklist }, manual);
        }

        [Fact]
        public void The_zero_values_authorise_nothing()
        {
            Assert.Equal(ConflictEdgeKind.Unstated, default(ConflictEdgeKind));
            Assert.Equal(AdmissionPlanOutcome.NothingToAdmit, default(AdmissionPlanOutcome));
            Assert.Equal(ColouringDefect.None, default(ColouringDefect));
        }

        // =============================================================================================
        // EMPTY IS NOT CLEAN
        // =============================================================================================

        [Fact]
        public void An_empty_submission_set_is_NOTHING_TO_ADMIT_never_an_admission()
        {
            var plan = WaveSetAdmission.Admit(new TestSlot[0], null, 6, CapProvenance);

            Assert.Equal(AdmissionPlanOutcome.NothingToAdmit, plan.Outcome);
            Assert.False(plan.Usable);
            Assert.Equal(0, plan.SlotsExamined);
            Assert.Empty(plan.WaveSets);
            Assert.Contains("NOTHING TO ADMIT", plan.Describe(), StringComparison.Ordinal);
        }

        [Fact]
        public void A_missing_width_cap_is_UNCHECKED_rather_than_passed()
        {
            // D29's cap is a width limit on any wave set. Without it the colouring is not checked
            // against bandwidth, which is a different thing from satisfying it.
            var plan = WaveSetAdmission.Admit(new[] { Slot("S1") }, null, null, CapProvenance);

            Assert.Equal(AdmissionPlanOutcome.Refused, plan.Outcome);
            Assert.Contains(plan.Findings, f => f.Defect == ColouringDefect.WidthCapNotSupplied);
        }

        [Fact]
        public void A_cap_with_no_provenance_is_refused_because_the_constant_under_it_has_already_moved()
        {
            var plan = WaveSetAdmission.Admit(new[] { Slot("S1") }, null, 6, "   ");

            Assert.Equal(AdmissionPlanOutcome.Refused, plan.Outcome);
            Assert.Contains(plan.Findings, f => f.Defect == ColouringDefect.WidthCapNotSupplied);
        }

        [Fact]
        public void A_slot_whose_reachable_state_was_never_computed_is_refused()
        {
            // "Computed and found empty" and "nobody computed it" arrive as the same empty set and call
            // for opposite actions — the distinction DeployedProgram.From refuses an empty list to keep.
            var uncomputed = new TestSlot("S1", "agent-1", new string[0], null);

            var plan = WaveSetAdmission.Admit(new[] { uncomputed }, null, 6, CapProvenance);

            Assert.Equal(AdmissionPlanOutcome.Refused, plan.Outcome);
            Assert.Contains(plan.Findings, f => f.Defect == ColouringDefect.ReachableStateNotComputed);
        }

        [Fact]
        public void A_slot_with_a_computed_but_EMPTY_closure_is_admitted()
        {
            // The did-not-run case for the provenance guard: it must refuse the unprovenanced slot and
            // NOT an empty-but-computed one, or the check is just "reject empty sets".
            var computedEmpty = new TestSlot("S1", "agent-1", new string[0], "converter cross-check: touches no shared state");

            var plan = WaveSetAdmission.Admit(new[] { computedEmpty }, null, 6, CapProvenance);

            Assert.Equal(AdmissionPlanOutcome.Admitted, plan.Outcome);
        }

        // =============================================================================================
        // THE COLOURING
        // =============================================================================================

        [Fact]
        public void Conflict_free_slots_share_one_wave_set()
        {
            var plan = WaveSetAdmission.Admit(
                new[] { Slot("S1"), Slot("S2"), Slot("S3") },
                null,
                6,
                CapProvenance);

            Assert.Equal(AdmissionPlanOutcome.Admitted, plan.Outcome);
            Assert.Equal(1, plan.WaveCount);
            Assert.Equal(3, plan.WaveSets[0].SlotCount);
        }

        [Fact]
        public void Conflicting_slots_cost_an_extra_WAVE_not_a_longer_one()
        {
            // DB-13's actual currency: wave length is max tensor length across the slots, and nothing
            // here can change it.
            var plan = WaveSetAdmission.Admit(
                new[] { Slot("S1"), Slot("S2") },
                new[] { Computed("S1", "S2") },
                6,
                CapProvenance);

            Assert.Equal(2, plan.WaveCount);
            Assert.All(plan.WaveSets, w => Assert.Equal(1, w.SlotCount));
            Assert.Contains("costs an extra WAVE rather than a longer one", plan.Summary, StringComparison.Ordinal);
        }

        [Fact]
        public void The_width_cap_splits_a_conflict_free_set()
        {
            var slots = Enumerable.Range(1, 7).Select(i => Slot("S" + i)).ToArray();

            var plan = WaveSetAdmission.Admit(slots, null, 3, CapProvenance);

            Assert.Equal(AdmissionPlanOutcome.Admitted, plan.Outcome);
            Assert.Equal(3, plan.WaveCount);
            Assert.All(plan.WaveSets, w => Assert.True(w.SlotCount <= 3));
            Assert.Equal(7, plan.WaveSets.Sum(w => w.SlotCount));
        }

        [Fact]
        public void Every_slot_lands_in_exactly_one_wave_set()
        {
            var slots = Enumerable.Range(1, 9).Select(i => Slot("S" + i)).ToArray();
            var edges = new[] { Computed("S1", "S2"), Computed("S2", "S3"), Blacklisted("S4", "S5") };

            var plan = WaveSetAdmission.Admit(slots, edges, 4, CapProvenance);

            var placed = plan.WaveSets.SelectMany(w => w.Slots.Select(s => s.Id)).ToArray();

            Assert.Equal(9, placed.Length);
            Assert.Equal(9, placed.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }

        [Fact]
        public void The_same_submission_set_produces_the_same_plan()
        {
            // Two runs that produce different wave sets cannot be compared.
            var slots = Enumerable.Range(1, 8).Select(i => Slot("S" + i)).ToArray();
            var edges = new[] { Computed("S1", "S4"), Computed("S2", "S6") };

            var first = WaveSetAdmission.Admit(slots, edges, 3, CapProvenance);
            var second = WaveSetAdmission.Admit(slots.Reverse().ToArray(), edges.Reverse().ToArray(), 3, CapProvenance);

            Assert.Equal(
                first.WaveSets.Select(w => string.Join(",", w.Slots.Select(s => s.Id).ToArray())).ToArray(),
                second.WaveSets.Select(w => string.Join(",", w.Slots.Select(s => s.Id).ToArray())).ToArray());
        }

        [Fact]
        public void A_blacklist_edge_ADDS_an_exclusion_and_the_computed_edges_survive_it()
        {
            // D22: the blacklist may only ADD. There is no argument here that removes an edge, so the
            // rule is structural — this pins that the union is what gets applied.
            var slots = new[] { Slot("S1"), Slot("S2"), Slot("S3") };

            // *** ASSERT THE INVARIANT, NOT A WAVE COUNT. *** A count is a property of the colouring
            // algorithm, so pinning one would fail on a better colourer while passing on one that
            // ignored the blacklist. The invariant is that EVERY edge in the union separates its ends.
            var noEdges = WaveSetAdmission.Admit(slots, null, 6, CapProvenance);
            Assert.True(ShareAWaveSet(noEdges, "S1", "S2"), "conflict-free slots should share a wave set");

            // The blacklist edge alone is load-bearing: the same pair must now be separated.
            var blacklistOnly = WaveSetAdmission.Admit(slots, new[] { Blacklisted("S1", "S2") }, 6, CapProvenance);
            Assert.False(ShareAWaveSet(blacklistOnly, "S1", "S2"));

            // And it ADDS rather than replaces: with a computed edge present, BOTH are honoured.
            var both = WaveSetAdmission.Admit(
                slots,
                new[] { Computed("S1", "S2"), Blacklisted("S2", "S3") },
                6,
                CapProvenance);

            Assert.False(ShareAWaveSet(both, "S1", "S2"));
            Assert.False(ShareAWaveSet(both, "S2", "S3"));
        }

        private static bool ShareAWaveSet(WaveSetPlan plan, string first, string second) =>
            plan.WaveSets.Any(w =>
                w.Slots.Any(s => string.Equals(s.Id, first, StringComparison.OrdinalIgnoreCase)) &&
                w.Slots.Any(s => string.Equals(s.Id, second, StringComparison.OrdinalIgnoreCase)));

        // =============================================================================================
        // INPUT REFUSALS
        // =============================================================================================

        [Fact]
        public void An_edge_whose_kind_is_unstated_is_refused()
        {
            var plan = WaveSetAdmission.Admit(
                new[] { Slot("S1"), Slot("S2") },
                new[] { new ConflictEdge("S1", "S2", ConflictEdgeKind.Unstated) },
                6,
                CapProvenance);

            Assert.Contains(plan.Findings, f => f.Defect == ColouringDefect.EdgeKindNotStated);
        }

        [Fact]
        public void A_blacklist_entry_without_a_reason_is_refused()
        {
            // §2.5's named failure mode is defensive over-blacklisting: concurrency collapses toward
            // serial and nobody notices, because it still WORKS.
            var plan = WaveSetAdmission.Admit(
                new[] { Slot("S1"), Slot("S2") },
                new[] { new ConflictEdge("S1", "S2", ConflictEdgeKind.Blacklist, "  ") },
                6,
                CapProvenance);

            Assert.Contains(plan.Findings, f => f.Defect == ColouringDefect.BlacklistEntryWithoutAReason);
        }

        [Fact]
        public void A_computed_edge_needs_no_reason_because_nobody_chose_it()
        {
            // The did-not-run case for the reason guard: it must bite on the manual kind ONLY.
            var plan = WaveSetAdmission.Admit(
                new[] { Slot("S1"), Slot("S2") },
                new[] { Computed("S1", "S2") },
                6,
                CapProvenance);

            Assert.Equal(AdmissionPlanOutcome.Admitted, plan.Outcome);
        }

        [Theory]
        [InlineData("S1", "S99")]
        [InlineData("S99", "S1")]
        [InlineData("S1", "S1")]
        public void An_edge_that_does_not_connect_two_admitted_slots_is_refused(string first, string second)
        {
            // Silently dropping it would make the colouring look LESS constrained than it is.
            var plan = WaveSetAdmission.Admit(
                new[] { Slot("S1"), Slot("S2") },
                new[] { new ConflictEdge(first, second, ConflictEdgeKind.OverlappingReachableState) },
                6,
                CapProvenance);

            Assert.Contains(plan.Findings, f => f.Defect == ColouringDefect.EdgeDoesNotConnectTwoAdmittedSlots);
        }

        [Fact]
        public void An_unidentified_or_duplicated_slot_is_refused()
        {
            var unnamed = WaveSetAdmission.Admit(
                new[] { new TestSlot("  ", "a", new[] { "x" }, "cross-check") },
                null,
                6,
                CapProvenance);

            Assert.Contains(unnamed.Findings, f => f.Defect == ColouringDefect.SlotNotIdentified);

            var duplicated = WaveSetAdmission.Admit(new[] { Slot("S1"), Slot("s1") }, null, 6, CapProvenance);

            Assert.Contains(duplicated.Findings, f => f.Defect == ColouringDefect.DuplicateSlotId);
        }

        // =============================================================================================
        // THE INDEPENDENT RE-VERIFICATION — and it had no did-not-run case until a mutation said so
        // =============================================================================================

        [Fact]
        public void The_verifier_catches_a_colouring_that_puts_conflicting_slots_together()
        {
            // *** THIS TEST EXISTS BECAUSE DELETING THE RE-VERIFICATION LEFT THE SUITE GREEN. *** The
            // colourer never produces a bad colouring, so nothing could reach the check — and a guard
            // that cannot be shown to fire is indistinguishable from a constant. Pointing it at a
            // deliberately wrong colouring is the only evidence that it checks anything.
            var bad = new[] { WaveSetAdmission.WaveSetOf(0, new[] { Slot("S1"), Slot("S2") }) };

            var findings = WaveSetAdmission.Verify(bad, new[] { Computed("S1", "S2") }, 6);

            Assert.Contains(findings, f => f.Defect == ColouringDefect.SlotCouldNotBePlaced);
        }

        [Fact]
        public void The_verifier_catches_a_wave_set_over_the_cap()
        {
            var overCap = new[] { WaveSetAdmission.WaveSetOf(0, new[] { Slot("S1"), Slot("S2"), Slot("S3") }) };

            var findings = WaveSetAdmission.Verify(overCap, null, 2);

            Assert.Contains(findings, f => f.Defect == ColouringDefect.SlotCouldNotBePlaced);
        }

        [Fact]
        public void The_verifier_catches_a_slot_placed_in_two_wave_sets()
        {
            // Colour classes PARTITION the slots; running one twice double-counts its bandwidth.
            var duplicated = new[]
            {
                WaveSetAdmission.WaveSetOf(0, new[] { Slot("S1") }),
                WaveSetAdmission.WaveSetOf(1, new[] { Slot("S1") }),
            };

            var findings = WaveSetAdmission.Verify(duplicated, null, 6);

            Assert.Contains(findings, f => f.Defect == ColouringDefect.DuplicateSlotId);
        }

        [Fact]
        public void The_verifier_catches_an_edge_the_colouring_never_placed()
        {
            var partial = new[] { WaveSetAdmission.WaveSetOf(0, new[] { Slot("S1") }) };

            var findings = WaveSetAdmission.Verify(partial, new[] { Computed("S1", "S2") }, 6);

            Assert.Contains(findings, f => f.Defect == ColouringDefect.EdgeDoesNotConnectTwoAdmittedSlots);
        }

        [Fact]
        public void The_verifier_passes_a_correct_colouring()
        {
            // The converse, without which every assertion above would pass against a verifier that
            // simply reported everything.
            var good = new[]
            {
                WaveSetAdmission.WaveSetOf(0, new[] { Slot("S1"), Slot("S3") }),
                WaveSetAdmission.WaveSetOf(1, new[] { Slot("S2") }),
            };

            Assert.Empty(WaveSetAdmission.Verify(good, new[] { Computed("S1", "S2") }, 6));
        }

        // =============================================================================================
        // WHAT THE PLAN REPORTS BACK
        // =============================================================================================

        [Fact]
        public void The_plan_reports_what_a_submission_cost()
        {
            // DB-13: an agent can see that a submission adding one test and one wave set is visibly
            // more expensive than one adding five slots into an existing set.
            var plan = WaveSetAdmission.Admit(
                new[] { Slot("S1"), Slot("S2"), Slot("S3") },
                new[] { Computed("S1", "S2") },
                6,
                CapProvenance);

            var described = plan.Describe();

            Assert.Contains("2 wave set(s)", described, StringComparison.Ordinal);
            Assert.Contains("3 slot(s) coloured", described, StringComparison.Ordinal);
            Assert.Contains("WAVE SET 0", described, StringComparison.Ordinal);
        }

        [Fact]
        public void Differing_slot_widths_within_a_wave_set_are_REPORTED_and_not_acted_on()
        {
            // *** F-6 IS UNRULED AND IS DELIBERATELY NOT IMPLEMENTED. *** Under F-1 one wide slot in a
            // fixed-size wave set collapses R for every slot in it, so whether admission should GROUP BY
            // SLOT SIZE is a live question — F-2 and F-6 are to be ruled together. Making the spread
            // visible is what this component may do; grouping on it would be pre-empting the ruling.
            var plan = WaveSetAdmission.Admit(
                new[] { Slot("S1", width: 4), Slot("S2", width: 123) },
                null,
                6,
                CapProvenance);

            Assert.Equal(1, plan.WaveCount);
            Assert.Equal(123, plan.WaveSets[0].WidestSlotRegisters);
            Assert.Equal(4, plan.WaveSets[0].NarrowestSlotRegisters);
            Assert.Contains("GROUP BY SLOT SIZE is F-6/F-2 and is UNRULED", plan.Describe(), StringComparison.Ordinal);
        }

        [Fact]
        public void A_uniform_wave_set_carries_no_F6_note()
        {
            // The did-not-run case for the note: it must appear only when widths actually differ, or it
            // becomes noise and stops being read.
            var plan = WaveSetAdmission.Admit(
                new[] { Slot("S1", width: 16), Slot("S2", width: 16) },
                null,
                6,
                CapProvenance);

            Assert.DoesNotContain("UNRULED", plan.Describe(), StringComparison.Ordinal);
        }

        [Fact]
        public void No_timing_constant_is_chosen_here_and_the_cap_arrives_from_outside()
        {
            // RTT_p99 moved 173 -> 201 while this component was written. Anything holding a baked-in
            // K_max would now be over-admitting; this consumes a cap instead, so a re-measurement
            // changes one caller rather than this file.
            var narrow = WaveSetAdmission.Admit(new[] { Slot("S1"), Slot("S2") }, null, 1, CapProvenance);
            var wide = WaveSetAdmission.Admit(new[] { Slot("S1"), Slot("S2") }, null, 2, CapProvenance);

            Assert.Equal(2, narrow.WaveCount);
            Assert.Equal(1, wide.WaveCount);
        }
    }
}
