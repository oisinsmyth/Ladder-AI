using System.Linq;

namespace Ladder.Download.Tests
{
    /// <summary>
    /// The two known defects, each guarded independently.
    /// </summary>
    /// <remarks>
    /// The probe this replaces committed BOTH at once — it inferred transfer from the absence of an
    /// up-to-date phrase AND read <c>state=Success</c> as evidence — so a single test covering
    /// "the verdict is right on the real logs" would pass with either one still in place. These
    /// separate the two readings and kill each on its own.
    /// </remarks>
    public sealed class VerdictTests
    {
        private static DownloadResultSummary Result(string state, params DownloadMessageNode[] messages) =>
            new DownloadResultSummary(state, 0, 0, new[] { new DownloadMessageNode("PLC_1", state, 0, 0, null, messages) });

        [Fact]
        public void Success_with_no_load_message_and_no_up_to_date_phrase_is_undetermined()
        {
            // DEFECT (a): the old verdict concluded TRANSFERRED here, purely because no phrase said
            // the target was up-to-date. Absence of a denial is not evidence of an act.
            var feedback = DownloadFeedbackParser.Parse(Result("Success"));

            Assert.Equal(TransferVerdict.Undetermined, feedback.Verdict);
            Assert.Equal(0, feedback.TransferredItemCount);
        }

        [Fact]
        public void Success_state_alone_never_produces_a_transferred_verdict()
        {
            // DEFECT (b): "state=Success, errors=0" was read out loud as proof of transfer. The
            // first run that ever returned Success transferred nothing.
            foreach (var state in new[] { "Success", "Information", "Warning", "Error", "SomethingNobodyHasSeen" })
            {
                var feedback = DownloadFeedbackParser.Parse(Result(state));
                Assert.NotEqual(TransferVerdict.Transferred, feedback.Verdict);
            }
        }

        [Fact]
        public void A_named_load_under_a_non_Success_state_still_counts_as_transferred()
        {
            // The mirror of the rule, and the reason the verdict is keyed on the manifest rather
            // than merely un-keyed from the state: positive evidence of a named load stands on its
            // own, whatever the state says. (Synthetic — no recorded run has this combination.)
            var summary = new DownloadResultSummary("Warning", 0, 3, new[]
            {
                new DownloadMessageNode("PLC_1", "Warning", 0, 3, null, new[]
                {
                    new DownloadMessageNode("'DB_Sample' was loaded successfully.", "Success"),
                }),
            });

            var feedback = DownloadFeedbackParser.Parse(summary);

            Assert.Equal(TransferVerdict.Transferred, feedback.Verdict);
            Assert.Equal(1, feedback.LoadedObjectCount);
            Assert.Equal(3, feedback.Result.WarningCount);
        }

        [Fact]
        public void No_result_at_all_is_undetermined_not_nothing_transferred()
        {
            var feedback = DownloadFeedbackParser.Parse(DownloadResultSummary.Absent);

            Assert.Equal(TransferVerdict.Undetermined, feedback.Verdict);
            Assert.False(feedback.ResultPresent);
        }

        [Fact]
        public void Null_result_is_treated_as_absent()
        {
            var feedback = DownloadFeedbackParser.Parse(null);

            Assert.Equal(TransferVerdict.Undetermined, feedback.Verdict);
            Assert.False(feedback.ResultPresent);
        }

        [Fact]
        public void A_present_but_empty_result_is_undetermined_and_flagged_as_an_anomaly()
        {
            var feedback = DownloadFeedbackParser.Parse(new DownloadResultSummary("Success", 0, 0, null));

            Assert.Equal(TransferVerdict.Undetermined, feedback.Verdict);
            Assert.Contains(feedback.Anomalies, a => a.Contains("NO messages"));
        }

        [Fact]
        public void Up_to_date_alongside_a_load_message_follows_the_load_and_reports_the_conflict()
        {
            // Never observed. If it ever happens, the count is the truth and the oddity is stated
            // rather than silently resolved.
            var summary = Result(
                "Success",
                new DownloadMessageNode("The software has not been loaded, because it is up-to-date.", "Information"),
                new DownloadMessageNode("'DB_Sample' was loaded successfully.", "Success"));

            var feedback = DownloadFeedbackParser.Parse(summary);

            Assert.Equal(TransferVerdict.Transferred, feedback.Verdict);
            Assert.True(feedback.UpToDateSignalPresent);
            Assert.Contains(feedback.Anomalies, a => a.Contains("CONFLICTING EVIDENCE"));
        }

        [Fact]
        public void A_repeated_object_name_is_reported_rather_than_silently_deduplicated()
        {
            var summary = Result(
                "Success",
                new DownloadMessageNode("'DB_Sample' was loaded successfully.", "Success"),
                new DownloadMessageNode("'DB_Sample' was loaded successfully.", "Success"));

            var feedback = DownloadFeedbackParser.Parse(summary);

            Assert.Equal(1, feedback.LoadedObjectCount);
            Assert.Equal(2, feedback.LoadedObjectMessageCount);
            Assert.Equal(new[] { "DB_Sample" }, feedback.DuplicateLoadedObjects.ToArray());
            Assert.Contains(feedback.Anomalies, a => a.Contains("REPEATED OBJECT NAME"));
        }

        [Fact]
        public void The_report_states_that_the_result_state_takes_no_part_in_the_verdict()
        {
            var report = DownloadFeedbackParser.Parse(Result("Success")).ToReport();

            Assert.Contains("TRANSFER VERDICT : UNDETERMINED", report);
            Assert.Contains("takes no part in the verdict", report);
        }
    }
}
