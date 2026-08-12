using System.Linq;

namespace Ladder.Download.Tests
{
    /// <summary>
    /// The measured facts, pinned. Every number here was observed on a real download against a real
    /// S7-1200 and is recorded in the spec (§9a/§9b); if one of these changes, either TIA changed or
    /// this parser broke, and both are worth a red test.
    /// </summary>
    public sealed class RecordedRunTests
    {
        [Fact]
        public void Differential_download_loaded_exactly_one_object()
        {
            var feedback = Fixture.Parse(Fixture.DifferentialOneObject);

            Assert.Equal(TransferVerdict.Transferred, feedback.Verdict);
            Assert.Equal(1, feedback.LoadedObjectCount);
            Assert.Equal(new[] { "DB_Data01" }, feedback.LoadedObjects.ToArray());
            Assert.Equal(1, feedback.TransferredItemCount);
            Assert.Empty(feedback.NonObjectLoads);
            Assert.Empty(feedback.UnrecognisedMessages);
        }

        [Fact]
        public void Differential_download_disclosed_no_run_state_and_that_is_not_a_reassurance()
        {
            var feedback = Fixture.Parse(Fixture.DifferentialOneObject);

            // The measured run carried no stop and no start message. The spec's own audit flags
            // reading that as "the CPU never stopped" — inference from absence. So the parser
            // reports UNDISCLOSED and offers no run state at all, rather than a cheerful default.
            Assert.False(feedback.RunStateDisclosed);
            Assert.Empty(feedback.RunStateTransitions);
            Assert.Null(feedback.FinalRunStateEvent);
        }

        [Fact]
        public void Full_download_loaded_ninety_nine_objects()
        {
            var feedback = Fixture.Parse(Fixture.FullNinetyNineObjects);

            Assert.Equal(TransferVerdict.Transferred, feedback.Verdict);
            Assert.Equal(99, feedback.LoadedObjectCount);

            // 99 = 66 blocks + 33 PLC data types, which is exactly what `download-plan` predicted.
            // That agreement is the whole point of reporting a COUNT rather than a boolean: it is
            // what makes the change tracker falsifiable on every download.
            Assert.Equal(33, feedback.LoadedObjects.Count(n => n.StartsWith("UDT_")));
            Assert.Equal(66, feedback.LoadedObjects.Count(n => !n.StartsWith("UDT_")));

            Assert.Empty(feedback.DuplicateLoadedObjects);
            Assert.Equal(99, feedback.LoadedObjectMessageCount);
            Assert.Empty(feedback.UnrecognisedMessages);
        }

        [Fact]
        public void Full_download_stopped_then_started_the_cpu_in_that_order()
        {
            var feedback = Fixture.Parse(Fixture.FullNinetyNineObjects);

            Assert.True(feedback.RunStateDisclosed);
            Assert.Equal(
                new[] { RunStateEvent.Stopped, RunStateEvent.Started },
                feedback.RunStateTransitions.Select(t => t.Transition).ToArray());
            Assert.Equal(RunStateEvent.Started, feedback.FinalRunStateEvent);

            // Order is by position in the tree, and the stop genuinely preceded every load.
            var stop = feedback.RunStateTransitions[0];
            var firstLoad = feedback.AllMessages.First(m => m.Kind == DownloadMessageKind.ObjectLoad);
            Assert.True(stop.Order < firstLoad.Order);
        }

        [Fact]
        public void Hardware_download_reported_three_non_object_loads_in_three_different_wordings()
        {
            var feedback = Fixture.Parse(Fixture.HardwareThreeWordings);

            Assert.Equal(TransferVerdict.Transferred, feedback.Verdict);

            // Zero program objects — a parser keyed only on the quoted-name form would have called
            // this "nothing transferred" while hardware, connections and routing all went down.
            Assert.Equal(0, feedback.LoadedObjectCount);
            Assert.Equal(3, feedback.NonObjectLoadCount);
            Assert.Equal(3, feedback.TransferredItemCount);

            Assert.Equal(
                new[] { "Hardware configuration", "Connection configuration", "Routing configuration" },
                feedback.NonObjectLoadSubjects.ToArray());

            // Verbatim, because the wording varies and the variation is the finding: two of the
            // three say "loaded", the middle one says "downloaded".
            Assert.Equal(
                new[]
                {
                    "Hardware configuration was loaded successfully.",
                    "Connection configuration was downloaded successfully.",
                    "Routing configuration was loaded successfully.",
                },
                feedback.NonObjectLoads.Select(m => m.Text).ToArray());

            Assert.Empty(feedback.UnrecognisedMessages);
        }

        [Fact]
        public void Hardware_download_group_header_is_not_mistaken_for_a_load()
        {
            var feedback = Fixture.Parse(Fixture.HardwareThreeWordings);

            // "Hardware configuration" appears TWICE with two different meanings: once as a bare
            // group node that CONTAINS the loads, once as the sentence that IS one. A substring
            // match on the words counts the container as a transfer.
            var header = feedback.AllMessages.Single(m => m.Text == "Hardware configuration");
            Assert.Equal(DownloadMessageKind.Container, header.Kind);

            // And the nesting is preserved: the real content sits one level deeper here than it
            // does on a software download.
            var load = feedback.AllMessages.Single(m => m.Text == "Hardware configuration was loaded successfully.");
            Assert.Equal(DownloadMessageKind.NonObjectLoad, load.Kind);
            Assert.Equal(new[] { "PLC_1", "Hardware configuration" }, load.Path.ToArray());
            Assert.Equal(2, load.Depth);
        }

        [Fact]
        public void Hardware_download_stopped_then_started_the_cpu()
        {
            var feedback = Fixture.Parse(Fixture.HardwareThreeWordings);

            Assert.Equal(
                new[] { RunStateEvent.Stopped, RunStateEvent.Started },
                feedback.RunStateTransitions.Select(t => t.Transition).ToArray());
        }

        [Fact]
        public void Up_to_date_run_transferred_nothing_while_the_result_said_Success()
        {
            var feedback = Fixture.Parse(Fixture.UpToDateNothingTransferred);

            // The result's own state is Success. Nothing moved.
            Assert.Equal("Success", feedback.Result.State);
            Assert.Equal(0, feedback.Result.ErrorCount);

            Assert.Equal(TransferVerdict.NothingTransferred, feedback.Verdict);
            Assert.Equal(0, feedback.TransferredItemCount);
            Assert.True(feedback.UpToDateSignalPresent);
            Assert.Empty(feedback.UnrecognisedMessages);
        }

        [Fact]
        public void Up_to_date_run_carried_Information_state_nodes_not_Success_ones()
        {
            var feedback = Fixture.Parse(Fixture.UpToDateNothingTransferred);

            // Per-node state differs from the result-level state on this run — which is one more
            // reason no verdict may be read off either.
            Assert.Equal(2, feedback.MessageCountsByState["Information"]);
            Assert.False(feedback.MessageCountsByState.ContainsKey("Success"));
        }

        [Fact]
        public void Aborted_run_with_no_result_is_undetermined_never_a_negative()
        {
            var feedback = Fixture.Parse(Fixture.AbortedNoDownloadResult);

            Assert.False(feedback.ResultPresent);
            Assert.Equal(TransferVerdict.Undetermined, feedback.Verdict);

            // The distinction that matters: this is NOT the up-to-date run. Nothing is known here,
            // where there something positive was known.
            Assert.NotEqual(TransferVerdict.NothingTransferred, feedback.Verdict);
            Assert.False(feedback.UpToDateSignalPresent);
        }

        [Fact]
        public void Every_recorded_run_that_produced_a_result_is_fully_classified()
        {
            // The standing check: across every recorded vocabulary, nothing falls through. When a
            // new download option adds a shape, this is what turns amber.
            foreach (var name in new[]
                     {
                         Fixture.DifferentialOneObject,
                         Fixture.FullNinetyNineObjects,
                         Fixture.HardwareThreeWordings,
                         Fixture.UpToDateNothingTransferred,
                     })
            {
                var feedback = Fixture.Parse(name);
                Assert.True(
                    feedback.UnrecognisedMessageCount == 0,
                    name + " produced unrecognised message(s): " +
                    string.Join(" | ", feedback.UnrecognisedMessages.Select(m => m.Text)));
            }
        }
    }
}
