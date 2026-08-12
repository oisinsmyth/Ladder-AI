using System.Linq;

namespace Ladder.Download.Tests
{
    /// <summary>
    /// THE REGRESSION GUARD THAT MATTERS MOST, because its absence is invisible.
    /// </summary>
    /// <remarks>
    /// A parser that classifies only what it knows still returns a confident-looking answer when the
    /// vocabulary changes — it just quietly answers about a subset. Four distinct vocabularies
    /// appeared in the first runs and every new download option produced one nobody predicted, so
    /// "we have not seen this shape" is a routine event, not an exotic one. These tests assert the
    /// unknown SURVIVES to the report.
    /// </remarks>
    public sealed class UnrecognisedMessageTests
    {
        [Fact]
        public void An_unknown_message_shape_is_surfaced_not_dropped()
        {
            var feedback = Fixture.Parse(Fixture.UnrecognisedVocabulary);

            Assert.Equal(2, feedback.UnrecognisedMessageCount);
            Assert.Equal(
                new[]
                {
                    "The user program was rearranged in the work memory.",
                    "Alarm text libraries were transferred to the module.",
                },
                feedback.UnrecognisedMessages.Select(m => m.Text).ToArray());
        }

        [Fact]
        public void An_unknown_shape_appears_in_the_human_report()
        {
            var report = Fixture.Parse(Fixture.UnrecognisedVocabulary).ToReport();

            Assert.Contains("unrecognised   : 2 message(s)", report);
            Assert.Contains("The user program was rearranged in the work memory.", report);
        }

        [Fact]
        public void An_unknown_shape_is_an_anomaly_but_does_not_disturb_the_manifest()
        {
            var feedback = Fixture.Parse(Fixture.UnrecognisedVocabulary);

            // It is not counted as a transfer, and it does not suppress the one that happened.
            Assert.Equal(TransferVerdict.Transferred, feedback.Verdict);
            Assert.Equal(1, feedback.LoadedObjectCount);
            Assert.Contains(feedback.Anomalies, a => a.Contains("does not recognise"));
        }

        [Fact]
        public void Unrecognised_messages_keep_their_per_node_counts_and_position()
        {
            var feedback = Fixture.Parse(Fixture.UnrecognisedVocabulary);

            var warning = feedback.UnrecognisedMessages.Single(m => m.Node.WarningCount > 0);
            Assert.Equal("Warning", warning.Node.State);
            Assert.Equal(1, warning.Node.WarningCount);
            Assert.Equal(new[] { "PLC_1" }, warning.Path.ToArray());

            Assert.Equal(1, feedback.TotalNodeWarningCount);
            Assert.Single(feedback.MessagesWithWarnings);
        }

        [Fact]
        public void Every_message_in_the_tree_lands_in_exactly_one_bucket()
        {
            // Total accountability: the flat list is the whole tree, and the buckets partition it.
            // Without this, a shape could be recognised, discarded, and never counted anywhere.
            var feedback = Fixture.Parse(Fixture.HardwareThreeWordings);

            var bucketed =
                feedback.LoadedObjectMessageCount +
                feedback.NonObjectLoadCount +
                feedback.RunStateTransitions.Count +
                feedback.UpToDateSignals.Count +
                feedback.UnrecognisedMessageCount +
                feedback.AllMessages.Count(m => m.Kind == DownloadMessageKind.Container);

            Assert.Equal(feedback.AllMessages.Count, bucketed);
        }

        [Fact]
        public void A_leaf_that_looks_like_a_header_is_unrecognised_rather_than_assumed_structural()
        {
            // The Container rule needs BOTH children and no terminating period. A bare word with no
            // children is something new, not a group, and must say so.
            var summary = new DownloadResultSummary("Success", 0, 0, new[]
            {
                new DownloadMessageNode("Some new phase", "Success"),
            });

            var feedback = DownloadFeedbackParser.Parse(summary);

            Assert.Equal(1, feedback.UnrecognisedMessageCount);
            Assert.Equal(TransferVerdict.Undetermined, feedback.Verdict);
        }
    }
}
