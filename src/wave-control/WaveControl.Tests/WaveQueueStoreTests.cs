using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Ladder.Wave.Tests
{
    /// <summary>
    /// The deferred queue survives a coordinator's death — and an absent or unreadable state file
    /// never reads as an empty queue.
    /// </summary>
    public sealed class WaveQueueStoreTests
    {
        /// <summary>Every object keeps the hash it was admitted with — the "nothing changed" reload.</summary>
        private static Func<string, string?> Unchanged => _ => ChangeSets.Hash;

        private static WaveQueues PopulatedQueues(DateTimeOffset at)
        {
            var queues = new WaveQueues();

            queues.Enqueue(ChangeSets.AdmitAll(ChangeSets.Submission("S-run", ChangeSets.Fb("FB_Motor", ChangeSets.Hash, "UDT_Motor"))), at);
            queues.Enqueue(ChangeSets.AdmitAll(ChangeSets.Submission("S-stop", ChangeSets.Ob("OB_Cyclic"))), at.AddSeconds(1));
            queues.Enqueue(ChangeSets.AdmitAll(ChangeSets.Submission("S-excised", ChangeSets.Fb("FB_Valve"))), at.AddSeconds(2));

            queues.Excise("S-excised", "raised DataBlockReinitialization, attributed to FB_Valve by DB-1");
            queues.NoteWaveBoundary();
            queues.NoteWaveBoundary();

            return queues;
        }

        // --- THE ROUND TRIP --------------------------------------------------------------------------

        [Fact]
        public void The_queues_survive_a_save_and_a_reload()
        {
            using (var dir = new TempDirectory())
            {
                var at = new DateTimeOffset(2026, 8, 13, 9, 30, 0, TimeSpan.Zero);
                var store = WaveQueueStore.InDirectory(dir.Path);
                store.Save(PopulatedQueues(at));

                var restored = store.Read();
                Assert.Equal(QueueRestoreState.Restored, restored.State);
                Assert.Equal(3, restored.Count);

                var rehydrated = QueueRehydrator.Rehydrate(restored, Unchanged);

                Assert.True(rehydrated.Complete);
                Assert.NotNull(rehydrated.Queues);
                Assert.Equal(new[] { "S-run" }, rehydrated.Queues!.RunQueue.Select(e => e.Submission.Id).ToArray());
                Assert.Equal(new[] { "S-stop", "S-excised" }, rehydrated.Queues!.DeferredQueue.Select(e => e.Submission.Id).ToArray());
            }
        }

        [Fact]
        public void The_facts_a_readmission_cannot_recompute_are_preserved()
        {
            using (var dir = new TempDirectory())
            {
                var at = new DateTimeOffset(2026, 8, 13, 9, 30, 0, TimeSpan.Zero);
                var store = WaveQueueStore.InDirectory(dir.Path);
                store.Save(PopulatedQueues(at));

                var queues = QueueRehydrator.Rehydrate(store.Read(), Unchanged).Queues!;
                var excised = queues.DeferredQueue.Single(e => e.Submission.Id == "S-excised");

                // The excision record is D32 step 5's account of WHY that submission is where it is;
                // replacing it with a fresh admission summary would erase it.
                Assert.Contains("EXCISED", excised.Reason, StringComparison.Ordinal);
                Assert.Contains("attributed to FB_Valve", excised.Reason, StringComparison.Ordinal);

                // Starvation visibility survives too, or every restart resets the clock on a submission
                // that has been waiting.
                Assert.Equal(2, excised.WaveBoundariesWaited);
                Assert.Equal(at.AddSeconds(2), excised.EnqueuedUtc);
            }
        }

        [Fact]
        public void The_admission_evidence_travels_with_the_entry()
        {
            using (var dir = new TempDirectory())
            {
                var store = WaveQueueStore.InDirectory(dir.Path);
                store.Save(PopulatedQueues(DateTimeOffset.UtcNow));

                var entry = store.Read().Entries.Single(e => e.Submission.Id == "S-run");

                Assert.Single(entry.Evidence);
                Assert.Equal(ChangeSets.Hash, entry.Evidence[0].ArtifactHash);
                Assert.Equal(EvidenceOutcome.Passed, entry.Evidence[0].Preflight);
                Assert.Equal(EvidenceOutcome.Passed, entry.Evidence[0].CompiledCleanInIsolation);
            }
        }

        [Fact]
        public void An_object_keeps_its_kind_class_and_dependencies()
        {
            using (var dir = new TempDirectory())
            {
                var store = WaveQueueStore.InDirectory(dir.Path);
                store.Save(PopulatedQueues(DateTimeOffset.UtcNow));

                var o = store.Read().Entries.Single(e => e.Submission.Id == "S-run").Submission.Objects.Single();

                Assert.Equal("FB_Motor", o.Name);
                Assert.Equal(ObjectKind.FunctionBlock, o.Kind);
                Assert.Equal(ChangeClass.Run, o.ChangeClass);
                Assert.Equal(new[] { "UDT_Motor" }, o.DependsOn.ToArray());
            }
        }

        [Fact]
        public void Empty_queues_are_written_as_a_file_declaring_zero_entries_never_by_deleting_it()
        {
            // If an emptied queue deleted the file, "no file" would stop meaning "no coordinator ever
            // wrote one here" and start meaning both that and "the queues emptied" - which is exactly
            // the ambiguity this component exists to remove.
            using (var dir = new TempDirectory())
            {
                var store = WaveQueueStore.InDirectory(dir.Path);
                store.Save(new WaveQueues());

                var restored = store.Read();

                Assert.Equal(QueueRestoreState.Restored, restored.State);
                Assert.Equal(0, restored.Count);
                Assert.True(File.Exists(store.StatePath));
            }
        }

        // --- ABSENT IS NOT EMPTY ---------------------------------------------------------------------

        [Fact]
        public void An_absent_state_file_is_its_own_state_and_yields_no_queues()
        {
            using (var dir = new TempDirectory())
            {
                var store = WaveQueueStore.InDirectory(dir.Path);

                var restored = store.Read();
                Assert.Equal(QueueRestoreState.NoStateFileFound, restored.State);

                var rehydrated = QueueRehydrator.Rehydrate(restored, Unchanged);

                Assert.Null(rehydrated.Queues);
                Assert.False(rehydrated.Complete);
                Assert.Contains("NOTHING TO RESUME", rehydrated.Summary, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void Proceeding_from_an_absent_state_file_requires_an_explicit_reason()
        {
            using (var dir = new TempDirectory())
            {
                var restored = WaveQueueStore.InDirectory(dir.Path).Read();

                Assert.Throws<ArgumentException>(() => WaveQueueStore.AcceptNoPersistedState(restored, "  "));

                var line = WaveQueueStore.AcceptNoPersistedState(restored, "first start of this coordinator on this rig");
                Assert.Contains("ACCEPTED ABSENT QUEUE STATE", line, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void An_absent_state_declaration_cannot_be_made_over_a_state_file_that_exists()
        {
            using (var dir = new TempDirectory())
            {
                var store = WaveQueueStore.InDirectory(dir.Path);
                store.Save(PopulatedQueues(DateTimeOffset.UtcNow));

                Assert.Throws<WaveMarkerException>(
                    () => WaveQueueStore.AcceptNoPersistedState(store.Read(), "pretending there was nothing"));
            }
        }

        // --- UNREADABLE IS NOT EMPTY EITHER ----------------------------------------------------------

        [Theory]
        [InlineData("", "empty file")]
        [InlineData("# only a comment\n", "header only")]
        [InlineData("format=1\nentries=0\n", "no sentinel — a torn write")]
        [InlineData("format=2\nentries=0\nend\n", "a format this build does not read")]
        [InlineData("format=1\nentries=0\nsomething-new=1\nend\n", "a key this build does not know")]
        [InlineData("format=1\nend\n", "no declared entry count")]
        [InlineData("this is not key=value... wait, it is\n", "junk")]
        public void A_state_file_that_cannot_be_fully_understood_is_unreadable_not_empty(string content, string why)
        {
            using (var dir = new TempDirectory())
            {
                var store = WaveQueueStore.InDirectory(dir.Path);
                File.WriteAllText(store.StatePath, content, new UTF8Encoding(false));

                var restored = store.Read();

                Assert.Equal(QueueRestoreState.Unreadable, restored.State);
                Assert.NotEqual(string.Empty, restored.Problem);
                Assert.Null(QueueRehydrator.Rehydrate(restored, Unchanged).Queues);
                Assert.Contains("not a queue that is empty", restored.Describe() + " " + why, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void A_file_carrying_fewer_entries_than_it_declares_is_unreadable()
        {
            // *** THE CHECK THE SENTINEL CANNOT MAKE. *** A tear that removes whole entries and keeps
            // the terminator parses perfectly and yields A SHORTER QUEUE, which is admitted work that
            // silently never happens.
            using (var dir = new TempDirectory())
            {
                var store = WaveQueueStore.InDirectory(dir.Path);
                store.Save(PopulatedQueues(DateTimeOffset.UtcNow));

                var text = File.ReadAllText(store.StatePath);
                var firstEntry = text.IndexOf("entry=", StringComparison.Ordinal);
                var secondEntry = text.IndexOf("entry=", firstEntry + 1, StringComparison.Ordinal);
                var truncated = text.Substring(0, secondEntry) + WaveQueueFormat.Sentinel + "\n";

                File.WriteAllText(store.StatePath, truncated, new UTF8Encoding(false));

                var restored = store.Read();

                Assert.Equal(QueueRestoreState.Unreadable, restored.State);
                Assert.Contains("declares 3 entries and carries 1", restored.Problem, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void A_torn_write_that_loses_the_sentinel_is_unreadable()
        {
            using (var dir = new TempDirectory())
            {
                var store = WaveQueueStore.InDirectory(dir.Path);
                store.Save(PopulatedQueues(DateTimeOffset.UtcNow));

                var text = File.ReadAllText(store.StatePath);
                File.WriteAllText(store.StatePath, text.Substring(0, text.Length / 2), new UTF8Encoding(false));

                Assert.Equal(QueueRestoreState.Unreadable, store.Read().State);
            }
        }

        [Fact]
        public void A_locked_state_file_is_unreadable_rather_than_absent()
        {
            // File.Exists swallows every error and returns false, so a permissions or sharing problem
            // would otherwise land on the most benign-looking of the three states.
            using (var dir = new TempDirectory())
            {
                var store = WaveQueueStore.InDirectory(dir.Path);
                store.Save(PopulatedQueues(DateTimeOffset.UtcNow));

                using (new FileStream(store.StatePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    var restored = store.Read();

                    Assert.Equal(QueueRestoreState.Unreadable, restored.State);
                    Assert.Contains("could not be opened", restored.Problem, StringComparison.Ordinal);
                }
            }
        }

        [Fact]
        public void Unreadable_is_the_zero_value_of_the_restore_state()
        {
            Assert.Equal(QueueRestoreState.Unreadable, default(QueueRestoreState));
        }

        // --- THE RE-GATE -----------------------------------------------------------------------------

        [Fact]
        public void An_entry_whose_content_moved_on_while_the_coordinator_was_dead_is_rejected()
        {
            // It was admitted on evidence about a particular state of the content. Restoring it verbatim
            // would put work into a wave on evidence about something that no longer exists — and would
            // defeat the hash comparison by the simple act of restarting.
            using (var dir = new TempDirectory())
            {
                var store = WaveQueueStore.InDirectory(dir.Path);
                store.Save(PopulatedQueues(DateTimeOffset.UtcNow));

                var rehydrated = QueueRehydrator.Rehydrate(
                    store.Read(),
                    name => name == "FB_Motor" ? ChangeSets.OtherHash : ChangeSets.Hash);

                Assert.False(rehydrated.Complete);
                Assert.Single(rehydrated.Rejected);
                Assert.Equal("S-run", rehydrated.Rejected[0].Entry.Submission.Id);
                Assert.Contains(
                    rehydrated.Rejected[0].Decision.Findings,
                    f => f.Kind == AdmissionFindingKind.EvidenceStale);

                Assert.DoesNotContain(rehydrated.Queues!.All, e => e.Submission.Id == "S-run");
                Assert.Equal(2, rehydrated.Queues!.All.Count);
            }
        }

        [Fact]
        public void A_hash_that_cannot_be_determined_now_is_a_rejection_not_a_pass()
        {
            // Nothing falls back to the persisted hash: that would answer "has this changed?" with the
            // value being checked.
            using (var dir = new TempDirectory())
            {
                var store = WaveQueueStore.InDirectory(dir.Path);
                store.Save(PopulatedQueues(DateTimeOffset.UtcNow));

                var rehydrated = QueueRehydrator.Rehydrate(store.Read(), _ => null);

                Assert.Equal(3, rehydrated.Rejected.Count);
                Assert.Empty(rehydrated.Readmitted);
                Assert.Empty(rehydrated.Queues!.All);
            }
        }

        [Fact]
        public void A_restored_entry_whose_queue_no_longer_matches_its_class_is_rejected()
        {
            // A STOP-class change whose persisted queue said RunQueue would go through a wave boundary
            // and stop the CPU mid-testing. The queue is re-derived and cross-checked, never trusted.
            using (var dir = new TempDirectory())
            {
                var store = WaveQueueStore.InDirectory(dir.Path);
                var queues = new WaveQueues();
                queues.Enqueue(ChangeSets.AdmitAll(ChangeSets.Submission("S-stop", ChangeSets.Ob("OB_Cyclic"))));
                store.Save(queues);

                var tampered = File.ReadAllText(store.StatePath).Replace("queue=DeferredQueue", "queue=RunQueue");
                File.WriteAllText(store.StatePath, tampered, new UTF8Encoding(false));

                var rehydrated = QueueRehydrator.Rehydrate(store.Read(), Unchanged);

                Assert.Single(rehydrated.Rejected);
                Assert.Contains("routes to the DeferredQueue", rehydrated.Rejected[0].Detail, StringComparison.Ordinal);
                Assert.Empty(rehydrated.Queues!.All);
            }
        }

        [Fact]
        public void An_excised_entry_survives_although_its_change_class_says_run_queue()
        {
            // *** THE CASE THAT MAKES THE QUEUE CROSS-CHECK ASYMMETRIC. *** D32 step 5 excises a
            // RUN-class submission INTO the deferred queue, so re-deriving its queue from its change
            // classes correctly answers "run queue" for something that legitimately belongs in the
            // deferred one. An equality check would reject every excised entry on reload.
            using (var dir = new TempDirectory())
            {
                var store = WaveQueueStore.InDirectory(dir.Path);
                store.Save(PopulatedQueues(DateTimeOffset.UtcNow));

                var rehydrated = QueueRehydrator.Rehydrate(store.Read(), Unchanged);
                var excised = rehydrated.Queues!.DeferredQueue.Single(e => e.Submission.Id == "S-excised");

                Assert.True(excised.Excised);
                Assert.Equal(ChangeClass.Run, excised.Objects.Single().ChangeClass);
                Assert.Empty(rehydrated.Rejected);
            }
        }

        [Fact]
        public void A_deferred_entry_that_is_not_excised_and_routes_to_the_run_queue_is_rejected()
        {
            // The permission is for EXCISED entries only; without the flag, a RUN-class entry sitting in
            // the deferred queue is a state nothing in the design produces.
            using (var dir = new TempDirectory())
            {
                var store = WaveQueueStore.InDirectory(dir.Path);
                store.Save(PopulatedQueues(DateTimeOffset.UtcNow));

                var tampered = File.ReadAllText(store.StatePath).Replace("excised=true", "excised=false");
                File.WriteAllText(store.StatePath, tampered, new UTF8Encoding(false));

                var rehydrated = QueueRehydrator.Rehydrate(store.Read(), Unchanged);

                Assert.Single(rehydrated.Rejected);
                Assert.Equal("S-excised", rehydrated.Rejected[0].Entry.Submission.Id);
                Assert.Contains("only an EXCISED entry", rehydrated.Rejected[0].Detail, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void An_entry_that_does_not_say_whether_it_was_excised_is_unreadable()
        {
            // Not defaulted to false: "excised=false" and "the writer never said" are opposite claims
            // about whether a RUN-class entry belongs in the deferred queue.
            using (var dir = new TempDirectory())
            {
                var store = WaveQueueStore.InDirectory(dir.Path);
                store.Save(PopulatedQueues(DateTimeOffset.UtcNow));

                var lines = File.ReadAllLines(store.StatePath).Where(l => !l.StartsWith("excised=", StringComparison.Ordinal));
                File.WriteAllText(store.StatePath, string.Join("\n", lines) + "\n", new UTF8Encoding(false));

                var restored = store.Read();

                Assert.Equal(QueueRestoreState.Unreadable, restored.State);
                Assert.Contains("does not state whether it was excised", restored.Problem, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void Rehydration_refuses_to_run_without_a_way_to_ask_the_current_hash()
        {
            using (var dir = new TempDirectory())
            {
                Assert.Throws<ArgumentNullException>(
                    () => QueueRehydrator.Rehydrate(WaveQueueStore.InDirectory(dir.Path).Read(), null!));
            }
        }

        // --- THE RESTORED QUEUES ARE LIVE ------------------------------------------------------------

        [Fact]
        public void Restored_queues_keep_working_and_a_drain_still_needs_its_decision()
        {
            using (var dir = new TempDirectory())
            {
                var store = WaveQueueStore.InDirectory(dir.Path);
                store.Save(PopulatedQueues(DateTimeOffset.UtcNow));

                var queues = QueueRehydrator.Rehydrate(store.Read(), Unchanged).Queues!;

                // S-run depends on UDT_Motor, which is neither deployed nor queued, so nothing can
                // progress and the two deferred entries are due a drain.
                var decision = DrainPolicy.Decide(queues, ChangeSets.Deployed());
                Assert.Equal(DrainOutcome.Drain, decision.Outcome);
                Assert.Equal(2, decision.DeferredContents.Count);

                var drained = queues.Drain(decision);
                Assert.Equal(2, drained.Count);

                store.Save(queues);
                Assert.Equal(1, store.Read().Count);
            }
        }

        [Fact]
        public void A_restored_entry_keeps_its_place_in_the_arrival_order_for_new_work()
        {
            using (var dir = new TempDirectory())
            {
                var store = WaveQueueStore.InDirectory(dir.Path);
                store.Save(PopulatedQueues(DateTimeOffset.UtcNow));

                var queues = QueueRehydrator.Rehydrate(store.Read(), Unchanged).Queues!;
                queues.Enqueue(ChangeSets.AdmitAll(ChangeSets.Submission("S-new", ChangeSets.Fb("FB_Pump"))));

                Assert.Equal(new[] { "S-run", "S-new" }, queues.RunQueue.Select(e => e.Submission.Id).ToArray());
            }
        }
    }
}
