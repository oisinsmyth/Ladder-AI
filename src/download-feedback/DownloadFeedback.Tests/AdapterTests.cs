using System.Collections.Generic;
using System.Linq;

namespace Ladder.Download.Tests
{
    /// <summary>
    /// The live seam: a stand-in for the Siemens <c>DownloadResultMessage</c> shape, adapted the way
    /// <c>openness-cli</c> will adapt the real one.
    /// </summary>
    public sealed class AdapterTests
    {
        private sealed class FakeMessage
        {
            public string Message = string.Empty;
            public string State = "Success";
            public uint ErrorCount;
            public uint WarningCount;
            public List<FakeMessage> Messages = new List<FakeMessage>();
        }

        private static DownloadResultSummary Adapt(IEnumerable<FakeMessage> roots) =>
            DownloadResultAdapter.Adapt<FakeMessage>(
                "Success", 0, 0, roots,
                m => m.Message,
                m => m.State,
                m => (int)m.ErrorCount,
                m => (int)m.WarningCount,
                m => null,
                m => m.Messages);

        [Fact]
        public void An_adapted_object_graph_produces_the_same_verdict_as_the_recorded_log()
        {
            var roots = new[]
            {
                new FakeMessage
                {
                    Message = "PLC_1",
                    Messages =
                    {
                        new FakeMessage { Message = "PLC_1 stopped." },
                        new FakeMessage { Message = "'DB_Sample' was loaded successfully." },
                        new FakeMessage { Message = "'FB_Example' was loaded successfully.", WarningCount = 2 },
                        new FakeMessage { Message = "PLC_1 started.", ErrorCount = 1, State = "Error" },
                    },
                },
            };

            var feedback = DownloadFeedbackParser.Parse(Adapt(roots));

            Assert.Equal(TransferVerdict.Transferred, feedback.Verdict);
            Assert.Equal(new[] { "DB_Sample", "FB_Example" }, feedback.LoadedObjects.ToArray());
            Assert.Equal(
                new[] { RunStateEvent.Stopped, RunStateEvent.Started },
                feedback.RunStateTransitions.Select(t => t.Transition).ToArray());

            // Per-node counts survive the projection, with their nesting (§9c item 5).
            Assert.Equal(1, feedback.TotalNodeErrorCount);
            Assert.Equal(2, feedback.TotalNodeWarningCount);
            Assert.Equal("PLC_1 started.", Assert.Single(feedback.MessagesWithErrors).Text);
        }

        [Fact]
        public void The_optional_projections_may_all_be_omitted()
        {
            var summary = DownloadResultAdapter.Adapt<FakeMessage>(
                null, 0, 0,
                new[] { new FakeMessage { Message = "'DB_Sample' was loaded successfully." } },
                m => m.Message);

            var feedback = DownloadFeedbackParser.Parse(summary);

            Assert.Equal(TransferVerdict.Transferred, feedback.Verdict);
            Assert.Equal(1, feedback.LoadedObjectCount);
        }

        [Fact]
        public void A_cyclic_graph_truncates_and_says_so_rather_than_overflowing_the_stack()
        {
            // A stack overflow is not catchable, so it would take the coordinator with it. The
            // truncation marker is deliberately an unrecognised shape: it shows up in the report.
            var loop = new FakeMessage { Message = "PLC_1" };
            loop.Messages.Add(loop);

            var feedback = DownloadFeedbackParser.Parse(Adapt(new[] { loop }));

            Assert.Contains(
                feedback.UnrecognisedMessages,
                m => m.Text.Contains("truncated"));
        }
    }
}
