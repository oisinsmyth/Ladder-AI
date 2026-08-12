using System;
using System.IO;
using System.Text;

namespace Ladder.Wave.Tests
{
    /// <summary>
    /// Spec §16.3 X-C item 2 — the persisted wave-in-progress marker, without which a restarted
    /// coordinator cannot tell "I died mid-wave" from "I am starting clean".
    /// </summary>
    public sealed class WaveMarkerStoreTests
    {
        private static readonly DateTimeOffset Started = new DateTimeOffset(2026, 8, 12, 21, 4, 5, TimeSpan.Zero);

        private static CoordinatorIdentity Identity(string host, int pid) =>
            new CoordinatorIdentity(host, pid, Guid.NewGuid());

        private static WaveMarker SampleMarker(CoordinatorIdentity? identity = null) =>
            new WaveMarker(
                waveId: "W-2026-08-12-003",
                slots: new[] { "S1", "S2", "S4" },
                programVersion: "9f3c1a7",
                startedUtc: Started,
                coordinator: identity ?? CoordinatorIdentity.Current);

        // --- THE HAPPY PATH ------------------------------------------------------------------------

        [Fact]
        public void A_marker_is_written_read_back_and_cleared()
        {
            using (var temp = new TempDirectory())
            {
                var store = WaveMarkerStore.InDirectory(temp.Path);

                Assert.Equal(WaveMarkerState.NoWaveInProgress, store.Read().State);

                store.BeginWave(SampleMarker(store.Identity));

                var found = store.Read();
                Assert.True(found.WaveWasInProgress);
                Assert.Equal(WaveMarkerState.WaveInProgress, found.State);
                Assert.NotNull(found.Marker);
                Assert.Equal("W-2026-08-12-003", found.Marker!.WaveId);
                Assert.Equal(new[] { "S1", "S2", "S4" }, found.Marker.Slots);
                Assert.Equal("9f3c1a7", found.Marker.ProgramVersion);
                Assert.Equal(Started, found.Marker.StartedUtc);
                Assert.True(found.WrittenByThisProcess);
                Assert.False(found.IsStale);

                store.CompleteWave();

                var afterwards = store.Read();
                Assert.False(afterwards.WaveWasInProgress);
                Assert.Equal(WaveMarkerState.NoWaveInProgress, afterwards.State);
                Assert.False(File.Exists(store.MarkerPath));
            }
        }

        [Fact]
        public void The_marker_survives_the_object_that_wrote_it_and_is_on_disk_before_the_call_returns()
        {
            using (var temp = new TempDirectory())
            {
                var path = temp.File(WaveMarkerStore.DefaultFileName);
                new WaveMarkerStore(path).BeginWave(SampleMarker());

                // Read the bytes directly, the way a different process would: nothing about the marker
                // depends on the store instance that wrote it still existing.
                var onDisk = File.ReadAllText(path, Encoding.UTF8);
                Assert.Contains("wave-id=W-2026-08-12-003", onDisk, StringComparison.Ordinal);
                Assert.Contains("slot=S1", onDisk, StringComparison.Ordinal);
                Assert.Contains("program-version=9f3c1a7", onDisk, StringComparison.Ordinal);
                Assert.EndsWith("end\n", onDisk, StringComparison.Ordinal);
            }
        }

        // --- THE CRASH SIGNAL ----------------------------------------------------------------------

        [Fact]
        public void A_marker_left_by_a_dead_coordinator_is_detected_as_stale_on_restart()
        {
            using (var temp = new TempDirectory())
            {
                var path = temp.File(WaveMarkerStore.DefaultFileName);

                var beforeTheCrash = new WaveMarkerStore(path, Identity("RIG-PC", 4120));
                beforeTheCrash.BeginWave(SampleMarker(beforeTheCrash.Identity));

                // ... the coordinator dies here, and a new process starts with a new identity.
                var afterTheRestart = new WaveMarkerStore(path, Identity("RIG-PC", 4120));

                var found = afterTheRestart.Read();

                Assert.True(found.WaveWasInProgress);
                Assert.True(found.IsStale);
                Assert.False(found.WrittenByThisProcess);
                Assert.Equal("W-2026-08-12-003", found.Marker!.WaveId);
                Assert.Contains("INVALID", found.Describe(), StringComparison.Ordinal);
            }
        }

        [Fact]
        public void Finding_a_marker_is_reported_as_a_state_and_never_as_a_failure()
        {
            using (var temp = new TempDirectory())
            {
                var store = WaveMarkerStore.InDirectory(temp.Path);
                store.BeginWave(SampleMarker(store.Identity));

                // Read never throws for a present marker; that is the whole API contract of the
                // startup call. Finding one is the expected signal after a coordinator death.
                var found = store.Read();

                Assert.Equal(WaveMarkerState.WaveInProgress, found.State);
                Assert.StartsWith("A WAVE WAS IN PROGRESS", found.Describe(), StringComparison.Ordinal);
                var age = found.Age(Started.AddHours(2));
                Assert.NotNull(age);
                Assert.Equal(TimeSpan.FromHours(2), age!.Value);
            }
        }

        [Fact]
        public void Beginning_a_wave_over_an_existing_marker_is_refused()
        {
            using (var temp = new TempDirectory())
            {
                var store = WaveMarkerStore.InDirectory(temp.Path);
                store.BeginWave(SampleMarker(store.Identity));

                var ex = Assert.Throws<WaveAlreadyInProgressException>(
                    () => store.BeginWave("W-004", new[] { "S9" }, "9f3c1a7"));

                Assert.True(ex.Status.WaveWasInProgress);
                Assert.Contains("DiscardInterruptedWave", ex.Message, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void An_interrupted_wave_is_discarded_explicitly_and_the_discard_is_logged()
        {
            using (var temp = new TempDirectory())
            {
                var path = temp.File(WaveMarkerStore.DefaultFileName);
                var died = new WaveMarkerStore(path, Identity("RIG-PC", 4120));
                died.BeginWave(SampleMarker(died.Identity));

                var restarted = new WaveMarkerStore(path, Identity("RIG-PC", 5001));
                var found = restarted.Read();

                var line = restarted.DiscardInterruptedWave(found, "coordinator died mid-wave; forcing inert");

                Assert.Contains("DISCARDED INTERRUPTED WAVE", line, StringComparison.Ordinal);
                Assert.Contains("W-2026-08-12-003", line, StringComparison.Ordinal);
                Assert.False(restarted.Read().WaveWasInProgress);

                // And a new wave may now begin.
                restarted.BeginWave("W-2026-08-12-004", new[] { "S1" }, "9f3c1a7");
                Assert.True(restarted.Read().WaveWasInProgress);
            }
        }

        [Fact]
        public void A_discard_cannot_be_logged_for_a_wave_that_was_never_in_progress()
        {
            using (var temp = new TempDirectory())
            {
                var store = WaveMarkerStore.InDirectory(temp.Path);
                Assert.Throws<WaveMarkerException>(
                    () => store.DiscardInterruptedWave(store.Read(), "nothing actually happened"));
            }
        }

        [Fact]
        public void A_discard_must_carry_a_reason()
        {
            using (var temp = new TempDirectory())
            {
                var store = WaveMarkerStore.InDirectory(temp.Path);
                store.BeginWave(SampleMarker(store.Identity));

                Assert.Throws<ArgumentException>(() => store.DiscardInterruptedWave(store.Read(), "  "));
            }
        }

        [Fact]
        public void Completing_a_wave_that_was_never_begun_is_surfaced_rather_than_swallowed()
        {
            using (var temp = new TempDirectory())
            {
                var store = WaveMarkerStore.InDirectory(temp.Path);
                Assert.Throws<WaveMarkerException>(() => store.CompleteWave());
            }
        }

        // --- EMPTY IS NOT CLEAN (FI-44) ------------------------------------------------------------

        [Fact]
        public void An_empty_marker_file_reads_as_a_wave_in_progress_not_as_absent()
        {
            using (var temp = new TempDirectory())
            {
                var path = temp.File(WaveMarkerStore.DefaultFileName);
                File.WriteAllBytes(path, new byte[0]);

                var found = new WaveMarkerStore(path).Read();

                Assert.True(found.WaveWasInProgress);
                Assert.Equal(WaveMarkerState.WaveInProgressDetailsUnreadable, found.State);
                Assert.Null(found.Marker);
                Assert.True(found.IsStale);
                Assert.Contains("UNREADABLE", found.Describe(), StringComparison.Ordinal);
            }
        }

        [Fact]
        public void EVERY_truncation_of_a_marker_reads_as_a_wave_in_progress()
        {
            // THE CRASH TEST. A process killed between creating the file and flushing its payload
            // leaves SOME prefix of the intended bytes; there is no prefix at which the answer may
            // become "no wave was in progress". This walks every one of them.
            using (var temp = new TempDirectory())
            {
                var path = temp.File(WaveMarkerStore.DefaultFileName);
                var complete = Encoding.UTF8.GetBytes(WaveMarkerFormat.Serialize(SampleMarker()));

                for (var length = 0; length < complete.Length; length++)
                {
                    var torn = new byte[length];
                    Array.Copy(complete, torn, length);
                    File.WriteAllBytes(path, torn);

                    var found = new WaveMarkerStore(path).Read();

                    Assert.True(
                        found.WaveWasInProgress,
                        "A marker truncated to " + length + " of " + complete.Length +
                        " bytes read as 'no wave in progress'. A torn write must never read as a clean start.");
                }
            }
        }

        [Theory]
        [InlineData("not a marker at all")]
        [InlineData("format=1\nwave-id=W1\nend\n")]                                   // missing fields
        [InlineData("format=2\nwave-id=W1\nstarted-utc=2026-08-12T21:04:05.0000000Z\nprogram-version=v\ncoordinator=H/1/00000000-0000-0000-0000-000000000001\nslot=S1\nend\n")]
        [InlineData("format=1\nwave-id=W1\nstarted-utc=nonsense\nprogram-version=v\ncoordinator=H/1/00000000-0000-0000-0000-000000000001\nslot=S1\nend\n")]
        [InlineData("format=1\nwave-id=W1\nstarted-utc=2026-08-12T21:04:05.0000000Z\nprogram-version=v\ncoordinator=not-a-token\nslot=S1\nend\n")]
        [InlineData("format=1\nwave-id=W1\nstarted-utc=2026-08-12T21:04:05.0000000Z\nprogram-version=v\ncoordinator=H/1/00000000-0000-0000-0000-000000000001\nslot=S1\nsomething-new=x\nend\n")]
        [InlineData("# only a comment\n")]
        public void A_corrupt_marker_reads_as_a_wave_in_progress(string content)
        {
            using (var temp = new TempDirectory())
            {
                var path = temp.File(WaveMarkerStore.DefaultFileName);
                File.WriteAllText(path, content, new UTF8Encoding(false));

                var found = new WaveMarkerStore(path).Read();

                Assert.True(found.WaveWasInProgress);
                Assert.Equal(WaveMarkerState.WaveInProgressDetailsUnreadable, found.State);
                Assert.False(string.IsNullOrWhiteSpace(found.Problem));
            }
        }

        [Fact]
        public void A_marker_that_cannot_be_opened_reads_as_a_wave_in_progress()
        {
            using (var temp = new TempDirectory())
            {
                var path = temp.File(WaveMarkerStore.DefaultFileName);
                File.WriteAllText(path, "whatever");

                using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    var found = new WaveMarkerStore(path).Read();

                    Assert.True(found.WaveWasInProgress);
                    Assert.Equal(WaveMarkerState.WaveInProgressDetailsUnreadable, found.State);
                }
            }
        }

        [Fact]
        public void No_wave_in_progress_is_deliberately_not_the_zero_value()
        {
            // The only state a caller must never arrive at by accident is the one that says the rig is
            // fresh. A defaulted state says a wave was in flight.
            Assert.Equal(WaveMarkerState.WaveInProgressDetailsUnreadable, default(WaveMarkerState));
            Assert.NotEqual(WaveMarkerState.NoWaveInProgress, default(WaveMarkerState));
        }

        [Fact]
        public void A_missing_directory_reads_as_no_wave_rather_than_throwing()
        {
            using (var temp = new TempDirectory())
            {
                var store = new WaveMarkerStore(Path.Combine(temp.Path, "not-created-yet", "m.marker"));

                Assert.False(store.Read().WaveWasInProgress);

                // ... and beginning a wave creates the directory rather than failing.
                store.BeginWave("W-1", new[] { "S1" }, "9f3c1a7");
                Assert.True(store.Read().WaveWasInProgress);
            }
        }

        // --- THE PAYLOAD ---------------------------------------------------------------------------

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
                var store = WaveMarkerStore.InDirectory(temp.Path);
                var marker = new WaveMarker(
                    waveId: "W=1 \\ with an = and a backslash",
                    slots: new[] { "S1", "slot with spaces", "slot=with=equals" },
                    programVersion: "v1\\2",
                    startedUtc: Started,
                    coordinator: store.Identity);

                store.BeginWave(marker);
                var read = store.Read().Marker;

                Assert.NotNull(read);
                Assert.Equal(marker.WaveId, read!.WaveId);
                Assert.Equal(marker.Slots, read.Slots);
                Assert.Equal(marker.ProgramVersion, read.ProgramVersion);
                Assert.Equal(marker.Coordinator, read.Coordinator);
            }
        }

        [Fact]
        public void The_marker_file_says_in_plain_words_what_finding_it_means()
        {
            // Whoever finds one of these is standing at the machine at 3am. The header is for them.
            var text = WaveMarkerFormat.Serialize(SampleMarker());

            Assert.Contains("wave-in-progress marker", text, StringComparison.Ordinal);
            Assert.Contains("INVALID", text, StringComparison.Ordinal);
        }
    }
}
