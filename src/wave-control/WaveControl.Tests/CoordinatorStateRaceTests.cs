using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ladder.Wave.Tests
{
    /// <summary>
    /// <b>NB-D — the demonstration the atomicity argument never had.</b>
    ///
    /// <para><c>CoordinatorStateStore</c>'s central claim is that <i>"A READER NEVER SEES A HALF-NEW
    /// FILE"</i>, and until now that claim was <b>argued from Win32 rename semantics and demonstrated by
    /// nothing</b>. The register's own words: <i>"it needs a reader racing a save."</i> This is that
    /// reader.</para>
    ///
    /// <para>*** WHY IT IS WORTH THE THREADS. *** This exact region has already produced a real loss.
    /// <c>File.Replace</c> is not atomic against process death, and a reader landing in the window where
    /// the destination did not exist read <c>FileNotFoundException</c> as <b>an empty store</b> — correct
    /// exactly once, before anybody has ever submitted, and catastrophic every time after; eight
    /// committed wave sets vanished while the surviving submission reported success. *A review that
    /// establishes the WRITER is atomic says nothing about how the READER interprets the writer's
    /// failure.* The writer here was reviewed. The reader racing it was not.</para>
    ///
    /// <para>*** EVERY TEST BELOW REPORTS WHETHER IT ENTERED THE CONDITION UNDER TEST. *** A
    /// non-recurrence is not a demonstration: a race test that never raced is evidence about nothing,
    /// and banking it as confirmation is how a guard comes to be believed on the strength of runs that
    /// never reached it. So each asserts a DENOMINATOR — reads actually performed — and, where the point
    /// is crossing a publish, that <b>more than one distinct state version was observed</b>. A run that
    /// saw only one version never crossed a rename and must not pass.</para>
    /// </summary>
    public sealed class CoordinatorStateRaceTests
    {
        private static readonly DateTimeOffset Started = new DateTimeOffset(2026, 8, 14, 16, 0, 0, TimeSpan.Zero);

        private static WaveQueues QueuesOf(int count, DateTimeOffset at)
        {
            var queues = new WaveQueues();
            for (var i = 0; i < count; i++)
            {
                queues.Enqueue(
                    ChangeSets.AdmitAll(ChangeSets.Submission("S-" + i, ChangeSets.Fb("FB_" + i, ChangeSets.Hash))),
                    at.AddSeconds(i));
            }

            return queues;
        }

        private static WaveMarker MarkerOf(int generation) =>
            new WaveMarker(
                waveId: "W-GEN-" + generation.ToString("D4"),
                slots: new[] { "S1" },
                programVersion: "v" + generation,
                startedUtc: Started,
                coordinator: CoordinatorIdentity.Current);

        /// <summary>
        /// *** THE CENTRAL CLAIM, EXERCISED. *** One writer publishing repeatedly, four readers reading
        /// as fast as they can. Every read must land on a WHOLE state — the old one or the new one, never
        /// a torn one, never an absent one, never an unreadable one.
        ///
        /// <para>The reader is deliberately given no retry and no tolerance. A retry loop here would be a
        /// test that measures how well the TEST recovers from the defect it is looking for.</para>
        /// </summary>
        [Fact]
        public async Task A_reader_racing_a_save_never_observes_a_half_new_file()
        {
            using (var temp = new TempDirectory())
            {
                var store = CoordinatorStateStore.InDirectory(temp.Path);

                // The first save exists so the race is over File.Replace (destination present), which is
                // the path that produced the real loss. The File.Move first-save path is covered below,
                // separately, because they are different Win32 calls with different failure windows.
                store.Save(MarkerOf(0), QueuesOf(1, Started));

                var observed = new ConcurrentBag<string>();
                var bad = new ConcurrentBag<string>();
                var reads = 0;
                var stop = new CancellationTokenSource(TimeSpan.FromSeconds(3));

                var readers = Enumerable.Range(0, 4).Select(_ => Task.Factory.StartNew(() =>
                {
                    var reader = CoordinatorStateStore.InDirectory(temp.Path);
                    while (!stop.IsCancellationRequested)
                    {
                        CoordinatorStateResult result;
                        try
                        {
                            result = reader.Read();
                        }
                        catch (Exception ex)
                        {
                            // A THROW is its own finding and is not the same as an unreadable verdict.
                            // A harness cannot tell an unhandled exception from a refusal, so naming it
                            // is what makes the difference reportable.
                            bad.Add("THREW " + ex.GetType().Name + ": " + ex.Message);
                            continue;
                        }

                        Interlocked.Increment(ref reads);

                        if (result.State != CoordinatorStateFileState.Restored)
                        {
                            bad.Add("STATE " + result.State + " — a save was in flight, which is not a "
                                + "reason for the state to be anything but Restored");
                            continue;
                        }

                        // Wholeness, not merely readability: the marker and the entry count must AGREE
                        // with each other. A file assembled from two different generations would parse.
                        var wave = result.Wave.Marker;
                        if (wave is null)
                        {
                            bad.Add("a Restored read carried no wave although every save wrote one");
                            continue;
                        }

                        var generation = int.Parse(wave.WaveId.Substring("W-GEN-".Length));
                        if (result.Entries.Count != generation + 1)
                        {
                            bad.Add("TORN: wave " + wave.WaveId + " (generation " + generation + ") beside "
                                + result.Entries.Count + " queue entries — the two halves came from "
                                + "different saves, which is the state one file was chosen to make impossible");
                            continue;
                        }

                        observed.Add(wave.WaveId);
                    }
                }, TaskCreationOptions.LongRunning)).ToArray();

                var generations = 0;
                var writer = Task.Factory.StartNew(() =>
                {
                    while (!stop.IsCancellationRequested)
                    {
                        generations++;
                        store.Save(MarkerOf(generations), QueuesOf(generations + 1, Started));
                    }
                }, TaskCreationOptions.LongRunning);

                await Task.WhenAll(readers.Append(writer).ToArray());

                Assert.Empty(bad);

                // THE DENOMINATORS. Without these, a run in which the readers never started, or the
                // writer published once, passes exactly as loudly as a run that raced hard.
                Assert.True(reads > 100, "only " + reads + " read(s) completed — a race test that barely read is evidence about nothing");
                Assert.True(generations > 1, "only " + generations + " save(s) were published — nothing was raced");
                Assert.True(
                    observed.Distinct().Count() > 1,
                    "the readers saw only " + observed.Distinct().Count() + " distinct state version(s) across "
                    + reads + " read(s) and " + generations + " save(s) — SO NO READ EVER CROSSED A PUBLISH, "
                    + "and this run did not enter the condition under test");
            }
        }

        /// <summary>
        /// The FIRST save takes <see cref="File.Move"/>, not <see cref="File.Replace"/> — a different
        /// Win32 call with a different window, and the one where <i>absent</i> is genuinely ambiguous.
        ///
        /// <para>*** AN ABSENCE THAT IS CORRECT EXACTLY ONCE IS A BUG FOR THE REST OF TIME. *** Before
        /// the first publish, "no state file" truly means <i>nobody ever wrote one here</i>. A reader
        /// racing that first publish must see EITHER that or a whole state — and never anything else,
        /// because after this moment those two readings stop being interchangeable.</para>
        /// </summary>
        [Fact]
        public async Task A_reader_racing_the_very_first_save_sees_either_nothing_or_everything()
        {
            for (var attempt = 0; attempt < 40; attempt++)
            {
                using (var temp = new TempDirectory())
                {
                    var store = CoordinatorStateStore.InDirectory(temp.Path);
                    var reader = CoordinatorStateStore.InDirectory(temp.Path);

                    var seen = new List<CoordinatorStateFileState>();
                    var entriesWhenRestored = new List<int>();
                    var gate = new ManualResetEventSlim(false);

                    var readerTask = Task.Factory.StartNew(() =>
                    {
                        gate.Wait();
                        for (var i = 0; i < 400; i++)
                        {
                            var result = reader.Read();
                            seen.Add(result.State);
                            if (result.State == CoordinatorStateFileState.Restored)
                            {
                                entriesWhenRestored.Add(result.Entries.Count);
                            }
                        }
                    }, TaskCreationOptions.LongRunning);

                    var writerTask = Task.Factory.StartNew(() =>
                    {
                        gate.Set();
                        store.Save(MarkerOf(3), QueuesOf(4, Started));
                    }, TaskCreationOptions.LongRunning);

                    await Task.WhenAll(readerTask, writerTask);

                    // Exactly two readings are legitimate here and no third is.
                    var illegal = seen.Where(s =>
                        s != CoordinatorStateFileState.NoStateFileFound &&
                        s != CoordinatorStateFileState.Restored).ToList();

                    Assert.Empty(illegal);

                    // And a Restored read must be COMPLETE — never the four-entry queue half-arrived.
                    Assert.All(entriesWhenRestored, count => Assert.Equal(4, count));
                }
            }
        }

        /// <summary>
        /// *** THE UNAFFECTED CASE, TESTED AS DELIBERATELY AS THE REFUSED ONE. *** A store nobody has
        /// ever published must still read as <see cref="CoordinatorStateFileState.NoStateFileFound"/> —
        /// that is the ONE state <c>DeclareNoStateFile</c> permits a coordinator to proceed from, so a
        /// qualification that refused every absence would make a first run impossible. *A gate that
        /// refuses every ordinary case is removed within a week, by someone who is right to.*
        /// </summary>
        [Fact]
        public void A_store_that_was_never_published_still_reads_as_absent()
        {
            using (var temp = new TempDirectory())
            {
                var store = CoordinatorStateStore.InDirectory(temp.Path);

                Assert.False(File.Exists(store.InitialisedPath));
                Assert.Equal(CoordinatorStateFileState.NoStateFileFound, store.Read().State);

                // And the read is PROMPT: an absence with no marker must not spend the contention budget
                // waiting for a publish that was never started. Every coordinator start pays this.
                var began = DateTime.UtcNow;
                store.Read();
                Assert.True(
                    DateTime.UtcNow - began < TimeSpan.FromMilliseconds(150),
                    "an unpublished store waited for a rename that is not happening — the retry budget "
                    + "must not be spent on the ordinary first-run path");
            }
        }

        /// <summary>
        /// *** AND THE CASE THAT WAS SILENTLY CATASTROPHIC. *** Once the store HAS been published, a
        /// missing state file is not an empty store — it is a publish that did not complete. Reading it
        /// as absent hands the coordinator the one state it may proceed from, discarding a wave marker
        /// and a whole admitted queue.
        /// </summary>
        [Fact]
        public void A_published_store_whose_state_file_vanished_refuses_rather_than_reading_as_absent()
        {
            using (var temp = new TempDirectory())
            {
                var store = CoordinatorStateStore.InDirectory(temp.Path);
                store.Save(MarkerOf(7), QueuesOf(3, Started));

                Assert.True(File.Exists(store.InitialisedPath));
                File.Delete(store.StatePath);

                var result = store.Read();

                Assert.NotEqual(CoordinatorStateFileState.NoStateFileFound, result.State);
                Assert.Equal(CoordinatorStateFileState.Unreadable, result.State);
                Assert.Contains("HAS been published", result.Wave.Problem);
                Assert.Contains(".tmp-*", result.Wave.Problem);

                // The refusal must not be usable as a queue: Unreadable voids both halves deliberately.
                Assert.False(result.QueueIsUsable);
            }
        }

        /// <summary>
        /// *** THE TEMPORARY FILES ARE IN THE SAME DIRECTORY AS THE STATE, WHICH IS THE POINT — AND IT
        /// MEANS A READER SHARES A DIRECTORY WITH THEM. *** A reader that globbed, or that mistook a
        /// `.tmp-*` for the state, would be reading a file mid-write. This pins that the store reads
        /// exactly one path and that the temporaries do not accumulate under it.
        /// </summary>
        [Fact]
        public void The_temporaries_live_beside_the_state_and_do_not_survive_a_successful_save()
        {
            using (var temp = new TempDirectory())
            {
                var store = CoordinatorStateStore.InDirectory(temp.Path);

                for (var i = 0; i < 25; i++)
                {
                    store.Save(MarkerOf(i), QueuesOf(i + 1, Started));
                }

                var files = Directory.GetFiles(temp.Path).Select(Path.GetFileName).ToList();

                Assert.Contains(Path.GetFileName(store.StatePath), files);
                Assert.DoesNotContain(files, f => f!.Contains(".tmp-"));

                // The denominator: 25 saves really did happen, so "no leftovers" is a statement about
                // 25 publishes and not about a directory nothing ever wrote to.
                Assert.Equal(25, store.Read().Entries.Count);
            }
        }

        /// <summary>
        /// *** THE RESIDUAL, ASSERTED SO IT CANNOT BE QUIETLY UPGRADED INTO A GUARANTEE. *** .NET offers
        /// no portable directory flush on Windows, so the rename's durability across a POWER CUT is not
        /// forced — after a power loss you may find the PREVIOUS state although <c>Save</c> returned.
        ///
        /// <para>Nothing above tests that, and nothing CAN from a process. This test exists to say so at
        /// a place a reader of the tests will meet it — <i>a caveat in a report decays; a caveat that is
        /// a test cannot</i> — and it asserts the direction the residual fails in, which is the half
        /// anyone reasoning about recovery actually needs: <b>a lost save leaves a coherent OLDER state,
        /// never an incoherent new one.</b></para>
        /// </summary>
        [Fact]
        public void The_power_cut_residual_is_unforced_and_its_direction_is_the_safe_one()
        {
            using (var temp = new TempDirectory())
            {
                var store = CoordinatorStateStore.InDirectory(temp.Path);
                store.Save(MarkerOf(1), QueuesOf(2, Started));

                // Simulate the ONE thing a power cut can leave that a process death cannot be made to:
                // a fully written temporary whose rename never landed. Whatever the cause, the reader
                // must still see the previous WHOLE state and must not be tempted by the temporary.
                var orphan = store.StatePath + ".tmp-" + Guid.NewGuid().ToString("N");
                File.WriteAllText(orphan, File.ReadAllText(store.StatePath));

                var result = store.Read();

                Assert.Equal(CoordinatorStateFileState.Restored, result.State);
                Assert.Equal("W-GEN-0001", result.Wave.Marker!.WaveId);
                Assert.Equal(2, result.Entries.Count);

                // *** AND THE ORPHAN IS LEFT ALONE. *** It is not cleanup's business here: on 2026-08-14
                // an error message advised that the newest `.tmp-*` is a complete store, and restoring
                // one recovered 17 slots. A store that deletes them removes that recovery path.
                Assert.True(File.Exists(orphan),
                    "the orphaned temporary was removed — it is the documented manual recovery source, "
                    + "and deleting it takes away the only thing a human has after a lost rename");
            }
        }
    }
}
