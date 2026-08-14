using System;
using System.IO;
using System.Linq;

namespace Ladder.Wave.Tests
{
    /// <summary>
    /// The shared wave store and the computed-edge derivation — the two things that made admission
    /// and colouring reachable by an agent rather than only by xUnit.
    /// </summary>
    public sealed class WaveStoreTests
    {
        private static readonly DateTimeOffset At = new DateTimeOffset(2026, 8, 14, 1, 0, 0, TimeSpan.Zero);

        private static TestSlot Slot(string id, string[]? reaches = null, string[]? models = null, string testsModel = "") =>
            new TestSlot(id, "agent-" + id, reaches ?? new[] { "state/" + id }, "converter cross-check", models, 16, testsModel);

        private static WaveStore StoreIn(TempDirectory dir) =>
            new WaveStore(dir.Path, allowWorktreeStore: true);

        // =============================================================================================
        // THE DEFECT THE DRIVER FOUND — and no unit test could have, before this one
        // =============================================================================================

        [Fact]
        public void A_slot_with_an_EMPTY_trailing_field_survives_the_round_trip()
        {
            // *** FOUND BY RUNNING THE CLI, NOT BY ASSERTING ABOUT OBJECTS. *** TestsModel is the LAST
            // field and is empty for every ordinary block slot, so the line ends in a tab. The parser
            // trimmed each line, which removed the field, and every store written by a non-model slot
            // was unreadable — "A slot line has 8 fields, not 9". The tests round-tripped through
            // objects; the defect lived strictly between the file and the parser.
            using (var dir = new TempDirectory())
            {
                var store = StoreIn(dir);
                store.Write(new[] { WaveStore.Stored(Slot("S1"), "A", At) });

                var read = store.Read();

                Assert.Single(read);
                Assert.Equal("S1", read[0].Slot.Id);
                Assert.Equal(string.Empty, read[0].Slot.TestsModel);
                Assert.False(read[0].Slot.IsModelSlot);
            }
        }

        [Fact]
        public void Every_field_of_a_slot_survives_the_round_trip()
        {
            using (var dir = new TempDirectory())
            {
                var store = StoreIn(dir);
                var slot = new TestSlot("S1", "agent-a", new[] { "x", "y" }, "cross-check 2026", new[] { "M_A" }, 42, "M_Vessel");

                store.Write(new[] { WaveStore.Stored(slot, "submitter", At) });
                var read = store.Read().Single();

                Assert.Equal("S1", read.Slot.Id);
                Assert.Equal("agent-a", read.Slot.Agent);
                Assert.Equal(new[] { "x", "y" }, read.Slot.ReachableState.ToArray());
                Assert.Equal("cross-check 2026", read.Slot.ReachableStateProvenance);
                Assert.Equal(new[] { "M_A" }, read.Slot.ModelInstances.ToArray());
                Assert.Equal(42, read.Slot.WidthRegisters);
                Assert.Equal("M_Vessel", read.Slot.TestsModel);
                Assert.Equal("submitter", read.SubmittedBy);
                Assert.Equal(At, read.SubmittedUtc);
            }
        }

        // =============================================================================================
        // THE STORE MUST BE SHARED
        // =============================================================================================

        [Fact]
        public void A_store_inside_a_git_worktree_is_refused()
        {
            // *** THE FAILURE --claims HAS NO DEFAULT TO AVOID. *** Two agents in two worktrees would
            // each get an empty store, each colour their slot alone, and each report a clean admission
            // — green multi-agent rows that never shared anything.
            using (var dir = new TempDirectory())
            {
                Directory.CreateDirectory(Path.Combine(dir.Path, ".git"));
                var inside = Path.Combine(dir.Path, "wave");

                var ex = Assert.Throws<WaveStoreException>(() => new WaveStore(inside));
                Assert.Contains("never shared anything", ex.Message, StringComparison.Ordinal);

                // ... and the named escape exists, for tests only.
                Assert.NotNull(new WaveStore(inside, allowWorktreeStore: true));
            }
        }

        [Fact]
        public void An_UNUSABLE_store_is_not_retryable_and_a_CONTENDED_one_is()
        {
            // *** THE DID-NOT-RUN CASE FOR THE NEW DISTINCTION. *** Without this, Retryable could be
            // hardcoded true and every test above would still pass — the exact "a flag that is
            // permanently true is indistinguishable from a constant" shape this lane keeps finding.
            using (var dir = new TempDirectory())
            {
                var store = StoreIn(dir);
                File.WriteAllText(store.SlotsPath, "format=1\nslots=2\nend\n");

                var unusable = Assert.Throws<WaveStoreException>(() => store.Read());
                Assert.False(unusable.Retryable);
                Assert.IsNotType<WaveStoreContendedException>(unusable);

                using (store.AcquireLease("A", TimeSpan.FromSeconds(5)))
                {
                    var contended = Assert.Throws<WaveStoreContendedException>(
                        () => store.AcquireLease("B", TimeSpan.FromMilliseconds(80)));

                    Assert.True(contended.Retryable);
                }
            }
        }

        [Fact]
        public void A_store_with_no_directory_is_refused_rather_than_defaulted()
        {
            Assert.Throws<WaveStoreException>(() => new WaveStore(null));
            Assert.Throws<WaveStoreException>(() => new WaveStore("   "));
        }

        [Fact]
        public void An_absent_store_reads_as_empty_but_a_CORRUPT_one_refuses()
        {
            using (var dir = new TempDirectory())
            {
                var store = StoreIn(dir);

                // No submission has happened yet — legitimately empty, because a submission that was
                // never written was never admitted.
                Assert.Empty(store.Read());

                File.WriteAllText(store.SlotsPath, "format=1\nslots=2\nend\n");
                var ex = Assert.Throws<WaveStoreException>(() => store.Read());
                Assert.Contains("silently never happens", ex.Message, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void A_store_missing_its_terminator_refuses()
        {
            using (var dir = new TempDirectory())
            {
                var store = StoreIn(dir);
                store.Write(new[] { WaveStore.Stored(Slot("S1"), "A", At) });

                var text = File.ReadAllText(store.SlotsPath);
                File.WriteAllText(store.SlotsPath, text.Substring(0, text.Length - 1));

                Assert.Throws<WaveStoreException>(() => store.Read());
            }
        }

        // =============================================================================================
        // THE LEASE IS OBSERVABLE
        // =============================================================================================

        [Fact]
        public void A_lease_reports_what_it_cost_even_when_uncontended()
        {
            using (var dir = new TempDirectory())
            using (var lease = StoreIn(dir).AcquireLease("A", TimeSpan.FromSeconds(5)))
            {
                Assert.Equal("A", lease.Agent);
                Assert.Equal(1, lease.Attempts);
                Assert.False(lease.Contended);
            }
        }

        [Fact]
        public void A_second_holder_is_refused_while_the_first_holds_it()
        {
            // The campaign is testing whether concurrent submission SERIALISES. A lock that worked
            // silently would let a serialised run be reported as a parallel one.
            using (var dir = new TempDirectory())
            {
                var store = StoreIn(dir);

                using (store.AcquireLease("A", TimeSpan.FromSeconds(5)))
                {
                    // *** RULED 2026-08-14: CONTENTION IS ITS OWN EXCEPTION AND ITS OWN EXIT CODE. ***
                    // A lease timeout used to raise a plain WaveStoreException, which the driver
                    // reported as exit 2 — "nothing was decided and retrying will not help". Retrying
                    // WOULD help here, so a harness keying on exit 2 gave up where it should back off.
                    var ex = Assert.Throws<WaveStoreContendedException>(
                        () => store.AcquireLease("B", TimeSpan.FromMilliseconds(120)));

                    // The PROPERTY is the contract; the wording is decoration. Asserting on the word
                    // alone would pass against a message that said it and a type that did not mean it.
                    Assert.True(ex.Retryable);
                    Assert.Contains("BACK OFF AND RETRY", ex.Message, StringComparison.Ordinal);
                    Assert.Contains("THIS IS THE MECHANISM WORKING", ex.Message, StringComparison.Ordinal);
                }

                // ... and it is released, so the next agent gets it.
                using (var after = store.AcquireLease("B", TimeSpan.FromSeconds(5)))
                {
                    Assert.Equal("B", after.Agent);
                }
            }
        }

        // =============================================================================================
        // THE COMPUTED EDGE PRODUCERS — two kinds had none
        // =============================================================================================

        [Fact]
        public void Overlapping_reachable_state_derives_its_own_edge()
        {
            // *** D9 SAYS INDEPENDENCE IS COMPUTED, NOT DECLARED — AND UNTIL THIS PRODUCER EXISTED,
            // NOTHING IN THIS COMPONENT COMPUTED IT. *** TestSlot carried the sets and nothing read them.
            var edges = SlotConflictDerivation.OverlappingReachableState(new[]
            {
                Slot("S1", new[] { "DB_Recipe.Setpoint", "FB_A" }),
                Slot("S2", new[] { "DB_Recipe.Setpoint" }),
                Slot("S3", new[] { "FB_C" }),
            });

            Assert.Single(edges);
            Assert.Equal(ConflictEdgeKind.OverlappingReachableState, edges[0].Kind);
            Assert.Contains("DB_Recipe.Setpoint", edges[0].Reason, StringComparison.Ordinal);
        }

        [Fact]
        public void Shared_model_instances_derive_their_own_edge()
        {
            var edges = SlotConflictDerivation.SharedModelInstances(new[]
            {
                Slot("S1", new[] { "a" }, new[] { "M_Vessel" }),
                Slot("S2", new[] { "b" }, new[] { "M_Vessel" }),
                Slot("S3", new[] { "c" }, new[] { "M_Other" }),
            });

            Assert.Single(edges);
            Assert.Equal(ConflictEdgeKind.SharedModelInstance, edges[0].Kind);
            Assert.Contains("DOMINANT source", edges[0].Reason, StringComparison.Ordinal);
        }

        [Fact]
        public void Disjoint_slots_derive_no_edges_at_all()
        {
            // The did-not-run case for both producers: without it they could pair everything and every
            // assertion above would still pass.
            var slots = new[] { Slot("S1"), Slot("S2"), Slot("S3") };

            Assert.Empty(SlotConflictDerivation.AllComputedEdges(slots));
        }

        [Fact]
        public void All_three_COMPUTED_kinds_now_have_a_producer_and_the_blacklist_correctly_does_not()
        {
            // The audit answer, pinned: three kinds are computed and all three are derived here; the
            // fourth is authored by a person (§2.5/D22) and must never have a producer.
            var slots = new[]
            {
                Slot("S1", new[] { "shared" }, new[] { "M_Vessel" }),
                Slot("S2", new[] { "shared" }, new[] { "M_Vessel" }),
                new TestSlot("S_model", "t", new[] { "m" }, "cross-check", null, 16, "M_Vessel"),
            };

            var kinds = SlotConflictDerivation.AllComputedEdges(slots).Select(e => e.Kind).Distinct().ToArray();

            Assert.Contains(ConflictEdgeKind.OverlappingReachableState, kinds);
            Assert.Contains(ConflictEdgeKind.SharedModelInstance, kinds);
            Assert.Contains(ConflictEdgeKind.ModelUnderTestByAnotherSlot, kinds);
            Assert.DoesNotContain(ConflictEdgeKind.Blacklist, kinds);

            // Every kind the derivation emits is a COMPUTED one — an authored kind arriving from here
            // would mean the component had invented an author's judgement.
            Assert.All(kinds, k => Assert.True(ConflictEdgeKinds.IsComputed(k)));
        }

        // =============================================================================================
        // THE TRAP THE CAMPAIGN MUST NOT FALL INTO
        // =============================================================================================

        [Fact]
        public void N_slots_sharing_one_block_are_ONE_slot_and_the_colouring_says_so()
        {
            // *** A CAMPAIGN GENERATING N SLOTS AGAINST ONE BLOCK AND REPORTING "N CONCURRENT" WOULD BE
            // MEASURING ITS OWN GENERATOR. *** Four slots that all reach FB_Shared colour into FOUR wave
            // sets of one.
            var slots = Enumerable.Range(1, 4)
                .Select(i => Slot("S" + i, new[] { "FB_Shared" }))
                .ToArray();

            var plan = WaveSetAdmission.Admit(slots, SlotConflictDerivation.AllComputedEdges(slots), 6, "test");

            Assert.Equal(AdmissionPlanOutcome.Admitted, plan.Outcome);
            Assert.Equal(4, plan.WaveCount);
            Assert.All(plan.WaveSets, w => Assert.Equal(1, w.SlotCount));
        }

        [Fact]
        public void Genuinely_disjoint_slots_DO_co_run()
        {
            // The converse, and the reason the artificial corpus exists: without it the test above
            // would pass against a colourer that separated everything.
            var slots = Enumerable.Range(9010, 4)
                .Select(i => Slot("S_" + i, new[] { "FB" + i }))
                .ToArray();

            var plan = WaveSetAdmission.Admit(slots, SlotConflictDerivation.AllComputedEdges(slots), 6, "test");

            Assert.Equal(1, plan.WaveCount);
            Assert.Equal(4, plan.WaveSets[0].SlotCount);
        }
    }
}
