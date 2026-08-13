using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Ladder.Wave.Tests
{
    /// <summary>
    /// X-C's wave-in-progress marker AND D23's two queues, in ONE atomically-written file.
    /// </summary>
    /// <remarks>
    /// This file is the merge of what were <c>WaveMarkerStoreTests</c> and <c>WaveQueueStoreTests</c>.
    /// Every assertion from both survives — the merge was ruled a change of CONTAINER, not of
    /// semantics, and a test suite that lost half its cases while the container changed would be
    /// exactly the way to let a semantic change through unnoticed. The one deliberate difference is
    /// stated where it occurs: a completed wave no longer DELETES the file, it writes
    /// <c>wave=none</c>.
    /// </remarks>
    public sealed class CoordinatorStateStoreTests
    {
        private static readonly DateTimeOffset Started = new DateTimeOffset(2026, 8, 12, 21, 4, 5, TimeSpan.Zero);

        private static Func<string, string?> Unchanged => _ => ChangeSets.Hash;

        private static CoordinatorIdentity Identity(string host, int pid) =>
            new CoordinatorIdentity(host, pid, Guid.NewGuid());

        private static WaveMarker SampleMarker(CoordinatorIdentity? identity = null) =>
            new WaveMarker(
                waveId: "W-2026-08-12-003",
                slots: new[] { "S1", "S2", "S4" },
                programVersion: "9f3c1a7",
                startedUtc: Started,
                coordinator: identity ?? CoordinatorIdentity.Current);

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

        // =============================================================================================
        // THE WAVE HALF — every case from the former WaveMarkerStoreTests
        // =============================================================================================

        [Fact]
        public void A_wave_is_written_read_back_and_completed()
        {
            using (var temp = new TempDirectory())
            {
                var store = CoordinatorStateStore.InDirectory(temp.Path);

                Assert.Equal(CoordinatorStateFileState.NoStateFileFound, store.Read().State);
                Assert.Equal(WaveMarkerState.NoWaveInProgress, store.Read().Wave.State);

                store.BeginWave(SampleMarker(store.Identity), new WaveQueues());

                var found = store.Read();
                Assert.True(found.Wave.WaveWasInProgress);
                Assert.Equal(WaveMarkerState.WaveInProgress, found.Wave.State);
                Assert.NotNull(found.Wave.Marker);
                Assert.Equal("W-2026-08-12-003", found.Wave.Marker!.WaveId);
                Assert.Equal(new[] { "S1", "S2", "S4" }, found.Wave.Marker.Slots);
                Assert.Equal("9f3c1a7", found.Wave.Marker.ProgramVersion);
                Assert.Equal(Started, found.Wave.Marker.StartedUtc);
                Assert.True(found.Wave.WrittenByThisProcess);
                Assert.False(found.Wave.IsStale);

                store.CompleteWave(new WaveQueues());

                var afterwards = store.Read();
                Assert.False(afterwards.Wave.WaveWasInProgress);
                Assert.Equal(WaveMarkerState.NoWaveInProgress, afterwards.Wave.State);

                // *** THE ONE DELIBERATE DIFFERENCE FROM THE TWO-FILE ERA. *** Completing a wave used
                // to DELETE the marker file. Under the merge the file stays and says wave=none, because
                // the file also carries the queues — and because "no wave in flight" has to remain a
                // STATEMENT rather than becoming an absence.
                Assert.True(File.Exists(store.StatePath));
                Assert.Equal(CoordinatorStateFileState.Restored, afterwards.State);
            }
        }

        [Fact]
        public void The_state_survives_the_object_that_wrote_it_and_is_on_disk_before_the_call_returns()
        {
            using (var temp = new TempDirectory())
            {
                var path = temp.File(CoordinatorStateStore.DefaultFileName);
                new CoordinatorStateStore(path).BeginWave(SampleMarker(), new WaveQueues());

                // Read the bytes directly, the way a different process would: nothing about the state
                // depends on the store instance that wrote it still existing.
                var onDisk = File.ReadAllText(path, Encoding.UTF8);
                Assert.Contains("wave=in-progress", onDisk, StringComparison.Ordinal);
                Assert.Contains("wave-id=W-2026-08-12-003", onDisk, StringComparison.Ordinal);
                Assert.Contains("slot=S1", onDisk, StringComparison.Ordinal);
                Assert.Contains("program-version=9f3c1a7", onDisk, StringComparison.Ordinal);
                Assert.EndsWith("end\n", onDisk, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void A_wave_left_by_a_dead_coordinator_is_detected_as_stale_on_restart()
        {
            using (var temp = new TempDirectory())
            {
                var path = temp.File(CoordinatorStateStore.DefaultFileName);

                var beforeTheCrash = new CoordinatorStateStore(path, Identity("RIG-PC", 4120));
                beforeTheCrash.BeginWave(SampleMarker(beforeTheCrash.Identity), new WaveQueues());

                // ... the coordinator dies here, and a new process starts with a new identity.
                var afterTheRestart = new CoordinatorStateStore(path, Identity("RIG-PC", 4120));

                var found = afterTheRestart.Read().Wave;

                Assert.True(found.WaveWasInProgress);
                Assert.True(found.IsStale);
                Assert.False(found.WrittenByThisProcess);
                Assert.Equal("W-2026-08-12-003", found.Marker!.WaveId);
                Assert.Contains("INVALID", found.Describe(), StringComparison.Ordinal);
            }
        }

        [Fact]
        public void Finding_a_wave_is_reported_as_a_state_and_never_as_a_failure()
        {
            using (var temp = new TempDirectory())
            {
                var store = CoordinatorStateStore.InDirectory(temp.Path);
                store.BeginWave(SampleMarker(store.Identity), new WaveQueues());

                var found = store.Read().Wave;

                Assert.Equal(WaveMarkerState.WaveInProgress, found.State);
                Assert.StartsWith("A WAVE WAS IN PROGRESS", found.Describe(), StringComparison.Ordinal);
                var age = found.Age(Started.AddHours(2));
                Assert.NotNull(age);
                Assert.Equal(TimeSpan.FromHours(2), age!.Value);
            }
        }

        [Fact]
        public void Beginning_a_wave_over_one_already_in_progress_is_refused()
        {
            using (var temp = new TempDirectory())
            {
                var store = CoordinatorStateStore.InDirectory(temp.Path);
                store.BeginWave(SampleMarker(store.Identity), new WaveQueues());

                var ex = Assert.Throws<WaveAlreadyInProgressException>(
                    () => store.BeginWave(
                        new WaveMarker("W-004", new[] { "S9" }, "9f3c1a7", Started),
                        new WaveQueues()));

                Assert.True(ex.Status.WaveWasInProgress);
                Assert.Contains("DiscardInterruptedWave", ex.Message, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void An_interrupted_wave_is_discarded_explicitly_and_the_discard_is_logged()
        {
            using (var temp = new TempDirectory())
            {
                var path = temp.File(CoordinatorStateStore.DefaultFileName);
                var died = new CoordinatorStateStore(path, Identity("RIG-PC", 4120));
                died.BeginWave(SampleMarker(died.Identity), new WaveQueues());

                var restarted = new CoordinatorStateStore(path, Identity("RIG-PC", 5001));
                var found = restarted.Read();

                var line = restarted.DiscardInterruptedWave(found, "coordinator died mid-wave; forcing inert", new WaveQueues());

                Assert.Contains("DISCARDED INTERRUPTED WAVE", line, StringComparison.Ordinal);
                Assert.Contains("W-2026-08-12-003", line, StringComparison.Ordinal);
                Assert.Contains("queue was readable and is unaffected", line, StringComparison.Ordinal);
                Assert.False(restarted.Read().Wave.WaveWasInProgress);

                // And a new wave may now begin.
                restarted.BeginWave(new WaveMarker("W-2026-08-12-004", new[] { "S1" }, "9f3c1a7", Started), new WaveQueues());
                Assert.True(restarted.Read().Wave.WaveWasInProgress);
            }
        }

        [Fact]
        public void Beginning_a_wave_over_an_UNREADABLE_state_is_refused()
        {
            // A state we cannot read is not one we can overwrite: beginning a wave over it would erase
            // the only evidence that a previous one never completed, which is what X-C item 3 needs in
            // order to discard its results.
            using (var temp = new TempDirectory())
            {
                var store = CoordinatorStateStore.InDirectory(temp.Path);
                File.WriteAllText(store.StatePath, "format=2\nwave=none\n", new UTF8Encoding(false));

                Assert.Equal(CoordinatorStateFileState.Unreadable, store.Read().State);

                Assert.Throws<WaveAlreadyInProgressException>(
                    () => store.BeginWave(SampleMarker(store.Identity), new WaveQueues()));
            }
        }

        [Fact]
        public void Discarding_after_an_unreadable_state_records_that_the_QUEUE_was_voided_too()
        {
            // Under the merge an unreadable file voids both halves, and the discard log has to SAY so —
            // the agents holding that admitted work are the ones who have to re-submit it.
            using (var temp = new TempDirectory())
            {
                var store = CoordinatorStateStore.InDirectory(temp.Path);
                File.WriteAllText(store.StatePath, "format=2\nwave=none\n", new UTF8Encoding(false));

                var found = store.Read();
                var line = store.DiscardInterruptedWave(found, "state unreadable on restart", new WaveQueues());

                Assert.Contains("THE QUEUE WAS VOIDED TOO", line, StringComparison.Ordinal);
                Assert.Contains("re-submit", line, StringComparison.Ordinal);

                // And the discard leaves a clean, readable, EXPLICIT state behind.
                var afterwards = store.Read();
                Assert.Equal(CoordinatorStateFileState.Restored, afterwards.State);
                Assert.False(afterwards.Wave.WaveWasInProgress);
                Assert.Equal(0, afterwards.Count);
            }
        }

        [Fact]
        public void A_discard_cannot_be_logged_for_a_wave_that_was_never_in_progress()
        {
            using (var temp = new TempDirectory())
            {
                var store = CoordinatorStateStore.InDirectory(temp.Path);
                Assert.Throws<WaveMarkerException>(
                    () => store.DiscardInterruptedWave(store.Read(), "nothing actually happened", new WaveQueues()));
            }
        }

        [Fact]
        public void A_discard_must_carry_a_reason()
        {
            using (var temp = new TempDirectory())
            {
                var store = CoordinatorStateStore.InDirectory(temp.Path);
                store.BeginWave(SampleMarker(store.Identity), new WaveQueues());

                Assert.Throws<ArgumentException>(
                    () => store.DiscardInterruptedWave(store.Read(), "  ", new WaveQueues()));
            }
        }

        [Fact]
        public void Completing_a_wave_that_was_never_begun_is_surfaced_rather_than_swallowed()
        {
            using (var temp = new TempDirectory())
            {
                var store = CoordinatorStateStore.InDirectory(temp.Path);
                Assert.Throws<WaveMarkerException>(() => store.CompleteWave(new WaveQueues()));
            }
        }

        [Fact]
        public void No_wave_in_progress_is_deliberately_not_the_zero_value()
        {
            Assert.Equal(WaveMarkerState.WaveInProgressDetailsUnreadable, default(WaveMarkerState));
            Assert.NotEqual(WaveMarkerState.NoWaveInProgress, default(WaveMarkerState));
        }

        [Fact]
        public void A_missing_directory_reads_as_no_state_rather_than_throwing()
        {
            using (var temp = new TempDirectory())
            {
                var store = new CoordinatorStateStore(Path.Combine(temp.Path, "not-created-yet", "c.state"));

                Assert.Equal(CoordinatorStateFileState.NoStateFileFound, store.Read().State);
                Assert.False(store.Read().Wave.WaveWasInProgress);

                // ... and beginning a wave creates the directory rather than failing.
                store.BeginWave(new WaveMarker("W-1", new[] { "S1" }, "9f3c1a7", Started), new WaveQueues());
                Assert.True(store.Read().Wave.WaveWasInProgress);
            }
        }

        [Fact]
        public void A_marker_must_say_what_was_in_flight()
        {
            Assert.Throws<ArgumentException>(() => new WaveMarker("W1", new string[0], "v1", Started));
            Assert.Throws<ArgumentException>(() => new WaveMarker("W1", new[] { "S1", "S1" }, "v1", Started));
            Assert.Throws<ArgumentException>(() => new WaveMarker("  ", new[] { "S1" }, "v1", Started));
            Assert.Throws<ArgumentException>(() => new WaveMarker("W1", new[] { "S1" }, "  ", Started));
            Assert.Throws<ArgumentException>(() => new WaveMarker("W1", new[] { "S1" }, "v1", default(DateTimeOffset)));
        }

        [Fact]
        public void Awkward_values_survive_the_round_trip()
        {
            using (var temp = new TempDirectory())
            {
                var store = CoordinatorStateStore.InDirectory(temp.Path);
                var marker = new WaveMarker(
                    waveId: "W=1 \\ with an = and a backslash",
                    slots: new[] { "S1", "slot with spaces", "slot=with=equals" },
                    programVersion: "v1\\2",
                    startedUtc: Started,
                    coordinator: store.Identity);

                store.BeginWave(marker, new WaveQueues());
                var read = store.Read().Wave.Marker;

                Assert.NotNull(read);
                Assert.Equal(marker.WaveId, read!.WaveId);
                Assert.Equal(marker.Slots, read.Slots);
                Assert.Equal(marker.ProgramVersion, read.ProgramVersion);
                Assert.Equal(marker.Coordinator, read.Coordinator);
            }
        }

        [Fact]
        public void The_state_file_says_in_plain_words_what_finding_it_means()
        {
            // Whoever finds one of these is standing at the machine at 3am. The header is for them.
            var text = CoordinatorStateFormat.Serialize(SampleMarker(), new QueuedSubmission[0]);

            Assert.Contains("COORDINATOR STATE", text, StringComparison.Ordinal);
            Assert.Contains("INVALID", text, StringComparison.Ordinal);
            Assert.Contains("atomically", text, StringComparison.Ordinal);
        }

        // =============================================================================================
        // THE QUEUE HALF — every case from the former WaveQueueStoreTests
        // =============================================================================================

        [Fact]
        public void The_queues_survive_a_save_and_a_reload()
        {
            using (var dir = new TempDirectory())
            {
                var at = new DateTimeOffset(2026, 8, 13, 9, 30, 0, TimeSpan.Zero);
                var store = CoordinatorStateStore.InDirectory(dir.Path);
                store.Save(null, PopulatedQueues(at));

                var restored = store.Read();
                Assert.Equal(CoordinatorStateFileState.Restored, restored.State);
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
                var store = CoordinatorStateStore.InDirectory(dir.Path);
                store.Save(null, PopulatedQueues(at));

                var queues = QueueRehydrator.Rehydrate(store.Read(), Unchanged).Queues!;
                var excised = queues.DeferredQueue.Single(e => e.Submission.Id == "S-excised");

                Assert.Contains("EXCISED", excised.Reason, StringComparison.Ordinal);
                Assert.Contains("attributed to FB_Valve", excised.Reason, StringComparison.Ordinal);
                Assert.Equal(2, excised.WaveBoundariesWaited);
                Assert.Equal(at.AddSeconds(2), excised.EnqueuedUtc);
            }
        }

        [Fact]
        public void The_admission_evidence_travels_with_the_entry()
        {
            using (var dir = new TempDirectory())
            {
                var store = CoordinatorStateStore.InDirectory(dir.Path);
                store.Save(null, PopulatedQueues(DateTimeOffset.UtcNow));

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
                var store = CoordinatorStateStore.InDirectory(dir.Path);
                store.Save(null, PopulatedQueues(DateTimeOffset.UtcNow));

                var o = store.Read().Entries.Single(e => e.Submission.Id == "S-run").Submission.Objects.Single();

                Assert.Equal("FB_Motor", o.Name);
                Assert.Equal(ObjectKind.FunctionBlock, o.Kind);
                Assert.Equal(ChangeClass.Run, o.ChangeClass);
                Assert.Equal(new[] { "UDT_Motor" }, o.DependsOn.ToArray());
            }
        }

        [Fact]
        public void Empty_queues_and_no_wave_are_written_as_statements_never_by_deleting_the_file()
        {
            using (var dir = new TempDirectory())
            {
                var store = CoordinatorStateStore.InDirectory(dir.Path);
                store.Save(null, new WaveQueues());

                var restored = store.Read();

                Assert.Equal(CoordinatorStateFileState.Restored, restored.State);
                Assert.Equal(0, restored.Count);
                Assert.Equal(WaveMarkerState.NoWaveInProgress, restored.Wave.State);
                Assert.True(File.Exists(store.StatePath));
                Assert.Contains("wave=none", File.ReadAllText(store.StatePath), StringComparison.Ordinal);
            }
        }

        [Fact]
        public void An_absent_state_file_is_its_own_state_and_yields_no_queues()
        {
            using (var dir = new TempDirectory())
            {
                var store = CoordinatorStateStore.InDirectory(dir.Path);

                var restored = store.Read();
                Assert.Equal(CoordinatorStateFileState.NoStateFileFound, restored.State);

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
                var restored = CoordinatorStateStore.InDirectory(dir.Path).Read();

                Assert.Throws<ArgumentException>(() => CoordinatorStateStore.AcceptNoPersistedState(restored, "  "));

                var line = CoordinatorStateStore.AcceptNoPersistedState(restored, "first start of this coordinator on this rig");
                Assert.Contains("ACCEPTED ABSENT COORDINATOR STATE", line, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void An_absent_state_declaration_cannot_be_made_over_a_state_file_that_exists()
        {
            using (var dir = new TempDirectory())
            {
                var store = CoordinatorStateStore.InDirectory(dir.Path);
                store.Save(null, PopulatedQueues(DateTimeOffset.UtcNow));

                Assert.Throws<WaveMarkerException>(
                    () => CoordinatorStateStore.AcceptNoPersistedState(store.Read(), "pretending there was nothing"));
            }
        }

        [Fact]
        public void A_file_carrying_fewer_entries_than_it_declares_is_unreadable()
        {
            using (var dir = new TempDirectory())
            {
                var store = CoordinatorStateStore.InDirectory(dir.Path);
                store.Save(null, PopulatedQueues(DateTimeOffset.UtcNow));

                var text = File.ReadAllText(store.StatePath);
                var firstEntry = text.IndexOf("entry=", StringComparison.Ordinal);
                var secondEntry = text.IndexOf("entry=", firstEntry + 1, StringComparison.Ordinal);
                var truncated = text.Substring(0, secondEntry) + CoordinatorStateFormat.Sentinel + "\n";

                File.WriteAllText(store.StatePath, truncated, new UTF8Encoding(false));

                var restored = store.Read();

                Assert.Equal(CoordinatorStateFileState.Unreadable, restored.State);
                Assert.Contains("declares 3 entries and carries 1", restored.Problem, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void A_locked_state_file_is_unreadable_rather_than_absent()
        {
            using (var dir = new TempDirectory())
            {
                var store = CoordinatorStateStore.InDirectory(dir.Path);
                store.Save(null, PopulatedQueues(DateTimeOffset.UtcNow));

                using (new FileStream(store.StatePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    var restored = store.Read();

                    Assert.Equal(CoordinatorStateFileState.Unreadable, restored.State);
                    Assert.Contains("could not be opened", restored.Problem, StringComparison.Ordinal);
                }
            }
        }

        [Fact]
        public void Unreadable_is_the_zero_value_of_the_state_file_state()
        {
            Assert.Equal(CoordinatorStateFileState.Unreadable, default(CoordinatorStateFileState));
        }

        [Fact]
        public void An_entry_whose_content_moved_on_while_the_coordinator_was_dead_is_rejected()
        {
            using (var dir = new TempDirectory())
            {
                var store = CoordinatorStateStore.InDirectory(dir.Path);
                store.Save(null, PopulatedQueues(DateTimeOffset.UtcNow));

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
            using (var dir = new TempDirectory())
            {
                var store = CoordinatorStateStore.InDirectory(dir.Path);
                store.Save(null, PopulatedQueues(DateTimeOffset.UtcNow));

                var rehydrated = QueueRehydrator.Rehydrate(store.Read(), _ => null);

                Assert.Equal(3, rehydrated.Rejected.Count);
                Assert.Empty(rehydrated.Readmitted);
                Assert.Empty(rehydrated.Queues!.All);
            }
        }

        [Fact]
        public void A_restored_entry_whose_queue_no_longer_matches_its_class_is_rejected()
        {
            using (var dir = new TempDirectory())
            {
                var store = CoordinatorStateStore.InDirectory(dir.Path);
                var queues = new WaveQueues();
                queues.Enqueue(ChangeSets.AdmitAll(ChangeSets.Submission("S-stop", ChangeSets.Ob("OB_Cyclic"))));
                store.Save(null, queues);

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
            using (var dir = new TempDirectory())
            {
                var store = CoordinatorStateStore.InDirectory(dir.Path);
                store.Save(null, PopulatedQueues(DateTimeOffset.UtcNow));

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
            using (var dir = new TempDirectory())
            {
                var store = CoordinatorStateStore.InDirectory(dir.Path);
                store.Save(null, PopulatedQueues(DateTimeOffset.UtcNow));

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
            using (var dir = new TempDirectory())
            {
                var store = CoordinatorStateStore.InDirectory(dir.Path);
                store.Save(null, PopulatedQueues(DateTimeOffset.UtcNow));

                var lines = File.ReadAllLines(store.StatePath).Where(l => !l.StartsWith("excised=", StringComparison.Ordinal));
                File.WriteAllText(store.StatePath, string.Join("\n", lines) + "\n", new UTF8Encoding(false));

                var restored = store.Read();

                Assert.Equal(CoordinatorStateFileState.Unreadable, restored.State);
                Assert.Contains("does not state whether it was excised", restored.Problem, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void Rehydration_refuses_to_run_without_a_way_to_ask_the_current_hash()
        {
            using (var dir = new TempDirectory())
            {
                Assert.Throws<ArgumentNullException>(
                    () => QueueRehydrator.Rehydrate(CoordinatorStateStore.InDirectory(dir.Path).Read(), null!));
            }
        }

        [Fact]
        public void Restored_queues_keep_working_and_a_drain_still_needs_its_decision()
        {
            using (var dir = new TempDirectory())
            {
                var store = CoordinatorStateStore.InDirectory(dir.Path);
                store.Save(null, PopulatedQueues(DateTimeOffset.UtcNow));

                var queues = QueueRehydrator.Rehydrate(store.Read(), Unchanged).Queues!;

                var decision = DrainPolicy.Decide(queues, ChangeSets.Deployed());
                Assert.Equal(DrainOutcome.Drain, decision.Outcome);
                Assert.Equal(2, decision.DeferredContents.Count);

                var drained = queues.Drain(decision);
                Assert.Equal(2, drained.Count);

                store.Save(null, queues);
                Assert.Equal(1, store.Read().Count);
            }
        }

        [Fact]
        public void A_restored_entry_keeps_its_place_in_the_arrival_order_for_new_work()
        {
            using (var dir = new TempDirectory())
            {
                var store = CoordinatorStateStore.InDirectory(dir.Path);
                store.Save(null, PopulatedQueues(DateTimeOffset.UtcNow));

                var queues = QueueRehydrator.Rehydrate(store.Read(), Unchanged).Queues!;
                queues.Enqueue(ChangeSets.AdmitAll(ChangeSets.Submission("S-new", ChangeSets.Fb("FB_Pump"))));

                Assert.Equal(new[] { "S-run", "S-new" }, queues.RunQueue.Select(e => e.Submission.Id).ToArray());
            }
        }

        // =============================================================================================
        // WHAT THE MERGE ITSELF HAD TO GET RIGHT
        // =============================================================================================

        [Fact]
        public void One_write_carries_both_halves_so_the_two_disagreements_cannot_occur()
        {
            // The failure modes the merge closes: a queue written with the marker not cleared (a restart
            // voids results that were valid), and a marker cleared with the queue not written (an item
            // dequeued in memory but never persisted runs twice, or is lost). One write, both halves.
            using (var dir = new TempDirectory())
            {
                var store = CoordinatorStateStore.InDirectory(dir.Path);
                var queues = PopulatedQueues(DateTimeOffset.UtcNow);

                store.BeginWave(SampleMarker(store.Identity), queues);

                var midWave = store.Read();
                Assert.True(midWave.Wave.WaveWasInProgress);
                Assert.Equal(3, midWave.Count);

                queues.Complete("S-run");
                store.CompleteWave(queues);

                var afterwards = store.Read();
                Assert.False(afterwards.Wave.WaveWasInProgress);
                Assert.Equal(2, afterwards.Count);
                Assert.DoesNotContain(afterwards.Entries, e => e.Submission.Id == "S-run");
            }
        }

        [Fact]
        public void No_wave_in_flight_is_a_statement_the_file_makes_and_its_absence_is_unreadable()
        {
            // *** THE DISTINCTION MERGING IS MOST LIKELY TO LOSE. *** Reading a missing wave= line as
            // "none" would make a file that lost a line indistinguishable from a clean one — the same
            // trap DeployedProgram.From refuses an empty list to avoid.
            using (var dir = new TempDirectory())
            {
                var store = CoordinatorStateStore.InDirectory(dir.Path);
                store.Save(null, new WaveQueues());

                var withoutTheLine = File.ReadAllLines(store.StatePath)
                    .Where(l => !l.StartsWith("wave=", StringComparison.Ordinal));

                File.WriteAllText(store.StatePath, string.Join("\n", withoutTheLine) + "\n", new UTF8Encoding(false));

                var restored = store.Read();

                Assert.Equal(CoordinatorStateFileState.Unreadable, restored.State);
                Assert.True(restored.Wave.WaveWasInProgress);
                Assert.Contains("there is no 'wave=' line", restored.Problem, StringComparison.Ordinal);
            }
        }

        [Theory]
        [InlineData("wave=none", "wave=none\nwave-id=W1", "says 'wave=none' and also carries wave fields")]
        [InlineData("wave=none", "wave=maybe", "is neither 'none' nor 'in-progress'")]
        public void A_self_contradictory_or_unrecognised_wave_line_is_unreadable(string from, string to, string expected)
        {
            using (var dir = new TempDirectory())
            {
                var store = CoordinatorStateStore.InDirectory(dir.Path);
                store.Save(null, new WaveQueues());

                var text = File.ReadAllText(store.StatePath).Replace(from, to);
                File.WriteAllText(store.StatePath, text, new UTF8Encoding(false));

                var restored = store.Read();

                Assert.Equal(CoordinatorStateFileState.Unreadable, restored.State);
                Assert.Contains(expected, restored.Problem, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void A_wave_field_appearing_after_the_queue_entries_began_is_unreadable()
        {
            // Guessing which section a line belongs to is how a corrupt file reassembles into a
            // plausible-looking one.
            using (var dir = new TempDirectory())
            {
                var store = CoordinatorStateStore.InDirectory(dir.Path);
                store.Save(SampleMarker(store.Identity), PopulatedQueues(DateTimeOffset.UtcNow));

                var text = File.ReadAllText(store.StatePath);
                var tampered = text.Replace("end\n", "slot=S99\nend\n");
                File.WriteAllText(store.StatePath, tampered, new UTF8Encoding(false));

                var restored = store.Read();

                Assert.Equal(CoordinatorStateFileState.Unreadable, restored.State);
                Assert.Contains("after the queue entries began", restored.Problem, StringComparison.Ordinal);
            }
        }

        [Theory]
        [InlineData("")]
        [InlineData("# only a comment\n")]
        [InlineData("not a state file at all")]
        [InlineData("format=2\nwave=none\nentries=0\n")]                    // no sentinel
        [InlineData("format=3\nwave=none\nentries=0\nend\n")]               // a format this build does not read
        [InlineData("format=2\nwave=none\nentries=0\nsomething-new=1\nend\n")]
        [InlineData("format=2\nwave=none\nend\n")]                          // no declared entry count
        [InlineData("format=2\nwave=in-progress\nwave-id=W1\nentries=0\nend\n")]  // missing wave fields
        [InlineData("format=2\nwave=in-progress\nwave-id=W1\nstarted-utc=nonsense\nprogram-version=v\ncoordinator=H/1/00000000-0000-0000-0000-000000000001\nslot=S1\nentries=0\nend\n")]
        [InlineData("format=2\nwave=in-progress\nwave-id=W1\nstarted-utc=2026-08-12T21:04:05.0000000Z\nprogram-version=v\ncoordinator=not-a-token\nslot=S1\nentries=0\nend\n")]
        public void A_state_file_that_cannot_be_fully_understood_is_unreadable_and_voids_both_halves(string content)
        {
            using (var dir = new TempDirectory())
            {
                var store = CoordinatorStateStore.InDirectory(dir.Path);
                File.WriteAllText(store.StatePath, content, new UTF8Encoding(false));

                var restored = store.Read();

                Assert.Equal(CoordinatorStateFileState.Unreadable, restored.State);
                Assert.NotEqual(string.Empty, restored.Problem);

                // X-C's rule, and under the merge it now voids the QUEUE as well. Deliberate: a file
                // damaged in one region is not evidence about another region.
                Assert.True(restored.Wave.WaveWasInProgress);
                Assert.Equal(WaveMarkerState.WaveInProgressDetailsUnreadable, restored.Wave.State);
                Assert.False(restored.QueueIsUsable);
                Assert.Null(QueueRehydrator.Rehydrate(restored, Unchanged).Queues);
            }
        }

        [Fact]
        public void EVERY_truncation_of_the_merged_state_reads_as_a_wave_in_progress_with_an_unusable_queue()
        {
            // THE CRASH TEST, now over BOTH halves. There is no prefix of the file at which the answer
            // may become "no wave was in progress" or "here is a usable queue". This walks every one.
            using (var dir = new TempDirectory())
            {
                var store = CoordinatorStateStore.InDirectory(dir.Path);
                var complete = Encoding.UTF8.GetBytes(
                    CoordinatorStateFormat.Serialize(SampleMarker(), PopulatedQueues(Started).All));

                for (var length = 0; length < complete.Length; length++)
                {
                    var torn = new byte[length];
                    Array.Copy(complete, torn, length);
                    File.WriteAllBytes(store.StatePath, torn);

                    var restored = store.Read();

                    Assert.True(
                        restored.Wave.WaveWasInProgress,
                        "A state truncated to " + length + " of " + complete.Length +
                        " bytes read as 'no wave in progress'. A torn write must never read as a clean start.");

                    Assert.False(
                        restored.QueueIsUsable,
                        "A state truncated to " + length + " bytes offered a usable queue.");
                }
            }
        }

        [Fact]
        public void A_state_file_missing_only_its_FINAL_NEWLINE_is_unreadable()
        {
            // *** THE HOLE THE TRUNCATION WALK FOUND, PINNED AS ITS OWN CASE. *** A write torn at the
            // very last byte loses only the newline after "end". The line-trimming parse then sees
            // "end" as the last meaningful line and reads the file as COMPLETE — measured at 2,359 of
            // 2,360 bytes. Both two-file formats had this hole and neither could detect it: the marker's
            // own truncation walk asserted only "a wave was in progress", which is equally true of a
            // file that parsed perfectly.
            using (var dir = new TempDirectory())
            {
                var store = CoordinatorStateStore.InDirectory(dir.Path);
                store.Save(SampleMarker(store.Identity), PopulatedQueues(Started));

                var text = File.ReadAllText(store.StatePath);
                Assert.EndsWith("end\n", text, StringComparison.Ordinal);

                File.WriteAllText(store.StatePath, text.Substring(0, text.Length - 1), new UTF8Encoding(false));

                var restored = store.Read();

                Assert.Equal(CoordinatorStateFileState.Unreadable, restored.State);
                Assert.False(restored.QueueIsUsable);
                Assert.True(restored.Wave.WaveWasInProgress);
                Assert.Contains("does not end with a newline", restored.Problem, StringComparison.Ordinal);
            }
        }

        // --- ATOMICITY ------------------------------------------------------------------------------

        [Fact]
        public void A_save_leaves_no_temporary_file_behind()
        {
            using (var dir = new TempDirectory())
            {
                var store = CoordinatorStateStore.InDirectory(dir.Path);

                store.Save(null, new WaveQueues());
                store.Save(SampleMarker(store.Identity), PopulatedQueues(Started));
                store.Save(null, new WaveQueues());

                Assert.Empty(Directory.GetFiles(dir.Path, "*.tmp-*"));
                Assert.Single(Directory.GetFiles(dir.Path));
            }
        }

        [Fact]
        public void The_NEW_state_is_complete_elsewhere_while_the_destination_still_holds_the_PREVIOUS_whole_state()
        {
            // *** THE TEST THAT ACTUALLY DEMONSTRATES ATOMICITY, ADDED BECAUSE A MUTATION PROVED THE
            // OTHERS DO NOT. *** Replacing the whole temp-then-rename with a plain in-place write left
            // every other test in this file GREEN — the debris check and the failed-publish check both
            // pass under an in-place write, because a locked destination fails at OPEN and leaves the
            // old file untouched either way. Neither observed the ordering, which is the entire
            // guarantee: at the instant before publication the new state is ALREADY COMPLETE somewhere
            // else, and the destination is STILL the previous whole state. There is no moment at which
            // the destination is half of either.
            using (var dir = new TempDirectory())
            {
                var store = CoordinatorStateStore.InDirectory(dir.Path);
                store.Save(SampleMarker(store.Identity), PopulatedQueues(Started));

                var previous = File.ReadAllText(store.StatePath);
                string? newStateBeforePublication = null;
                string? destinationDuringTheSave = null;
                var seamFired = false;

                store.OnTemporaryWritten = temporaryPath =>
                {
                    seamFired = true;
                    newStateBeforePublication = File.ReadAllText(temporaryPath);
                    destinationDuringTheSave = File.ReadAllText(store.StatePath);
                };

                store.Save(null, new WaveQueues());

                Assert.True(seamFired, "The save never wrote a separate file before publishing it.");

                // The new state was already whole — terminator and all — before anything was published.
                Assert.EndsWith("end\n", newStateBeforePublication!, StringComparison.Ordinal);
                Assert.Contains("wave=none", newStateBeforePublication!, StringComparison.Ordinal);
                Assert.Contains("entries=0", newStateBeforePublication!, StringComparison.Ordinal);

                // And the destination was still byte-for-byte the previous whole state.
                Assert.Equal(previous, destinationDuringTheSave);

                // Afterwards, the destination is the new state and nothing else remains.
                Assert.Contains("entries=0", File.ReadAllText(store.StatePath), StringComparison.Ordinal);
                Assert.Empty(Directory.GetFiles(dir.Path, "*.tmp-*"));
            }
        }

        [Fact]
        public void A_failed_publish_leaves_the_PREVIOUS_whole_state_readable_and_no_debris()
        {
            // *** THE POINT OF THE TEMP-THEN-RENAME. *** A save that cannot complete must not damage
            // what is already there. Holding the destination open with FileShare.None makes the rename
            // fail the way an antivirus scanner or an indexer does on Windows.
            using (var dir = new TempDirectory())
            {
                var store = CoordinatorStateStore.InDirectory(dir.Path);
                store.Save(SampleMarker(store.Identity), PopulatedQueues(Started));

                var before = File.ReadAllText(store.StatePath);

                using (new FileStream(store.StatePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    Assert.Throws<WaveMarkerException>(() => store.Save(null, new WaveQueues()));
                }

                Assert.Equal(before, File.ReadAllText(store.StatePath));
                Assert.Empty(Directory.GetFiles(dir.Path, "*.tmp-*"));

                var restored = store.Read();
                Assert.Equal(CoordinatorStateFileState.Restored, restored.State);
                Assert.True(restored.Wave.WaveWasInProgress);
                Assert.Equal(3, restored.Count);
            }
        }

        [Fact]
        public void A_second_save_replaces_the_first_completely_rather_than_overlaying_it()
        {
            // A shorter new state must not leave a tail of the longer old one — which is what an
            // in-place write without truncation would do, and what a rename cannot do.
            using (var dir = new TempDirectory())
            {
                var store = CoordinatorStateStore.InDirectory(dir.Path);

                store.Save(SampleMarker(store.Identity), PopulatedQueues(Started));
                store.Save(null, new WaveQueues());

                var text = File.ReadAllText(store.StatePath);

                Assert.DoesNotContain("S-run", text, StringComparison.Ordinal);
                Assert.DoesNotContain("wave-id=", text, StringComparison.Ordinal);
                Assert.Contains("entries=0", text, StringComparison.Ordinal);
                Assert.Equal(CoordinatorStateFileState.Restored, store.Read().State);
            }
        }

        // --- MIGRATION ------------------------------------------------------------------------------

        [Theory]
        [InlineData(CoordinatorStateStore.LegacyMarkerFileName)]
        [InlineData(CoordinatorStateStore.LegacyQueueFileName)]
        public void Two_file_state_is_refused_and_never_half_read(string legacyName)
        {
            using (var dir = new TempDirectory())
            {
                var store = CoordinatorStateStore.InDirectory(dir.Path);
                File.WriteAllText(dir.File(legacyName), "format=1\nend\n", new UTF8Encoding(false));

                var restored = store.Read();

                Assert.Equal(CoordinatorStateFileState.LegacyTwoFileStatePresent, restored.State);
                Assert.Contains(legacyName, restored.Problem, StringComparison.Ordinal);
                Assert.Empty(restored.Entries);

                // Refusing is a state, not a pass: the wave reads as in-progress-unreadable, nothing is
                // rehydrated, and the absent-state declaration cannot be used to wave it through.
                Assert.True(restored.Wave.WaveWasInProgress);
                Assert.Null(QueueRehydrator.Rehydrate(restored, Unchanged).Queues);
                Assert.Throws<WaveMarkerException>(
                    () => CoordinatorStateStore.AcceptNoPersistedState(restored, "just carry on"));
            }
        }

        [Fact]
        public void Legacy_state_beside_a_valid_merged_file_is_still_a_refusal()
        {
            // The legacy check runs BEFORE the merged file is opened, so a directory holding both is
            // refused rather than silently preferring one.
            using (var dir = new TempDirectory())
            {
                var store = CoordinatorStateStore.InDirectory(dir.Path);
                store.Save(null, PopulatedQueues(Started));

                File.WriteAllText(dir.File(CoordinatorStateStore.LegacyQueueFileName), "anything", new UTF8Encoding(false));

                Assert.Equal(CoordinatorStateFileState.LegacyTwoFileStatePresent, store.Read().State);
            }
        }

        [Fact]
        public void Format_one_is_refused_by_name_rather_than_read_as_a_newer_file()
        {
            using (var dir = new TempDirectory())
            {
                var store = CoordinatorStateStore.InDirectory(dir.Path);
                File.WriteAllText(store.StatePath, "format=1\nwave=none\nentries=0\nend\n", new UTF8Encoding(false));

                var restored = store.Read();

                Assert.Equal(CoordinatorStateFileState.Unreadable, restored.State);
                Assert.Contains("two-file era", restored.Problem, StringComparison.Ordinal);
            }
        }
    }
}
