using System;

namespace Ladder.Wave.Tests
{
    /// <summary>
    /// D32: "'went to step 7 without attempting a download, because Class B' is a materially
    /// different record from 'attempted the full download and it aborted too'." These are the tests
    /// that the two records stay different, and that the second one cannot be written for a class
    /// where the download must never be spent.
    /// </summary>
    public sealed class EscalationRecordTests
    {
        private static ConfigurationVerdict ClassA() =>
            ConfigurationClassifier.Classify("StopModules", "NoAction", "StopAll");

        private static ConfigurationVerdict ClassB() =>
            ConfigurationClassifier.Classify("InitializeMemory", "NoAction", "AcceptAll");

        private static ConfigurationVerdict ClassC() =>
            ConfigurationClassifier.Classify("SelectiveDeleteDownload", "AcceptAll", "DeleteSelected");

        [Fact]
        public void A_class_B_record_says_no_download_was_spent_and_names_the_class()
        {
            var record = EscalationRecord.ReachedHumanWithoutAttempt(ClassB());

            Assert.Equal(EscalationOutcome.ReachedHumanWithoutAttemptingDownload, record.Outcome);
            Assert.False(record.DisruptiveDownloadAttempted);
            Assert.True(record.IsTotalTestAbort);
            Assert.Contains("no download attempted, because Class B", record.Detail, StringComparison.Ordinal);
            Assert.Contains("HUMAN, NO DOWNLOAD SPENT", record.ToLogLine(), StringComparison.Ordinal);
        }

        [Fact]
        public void An_attempted_and_aborted_download_is_a_different_record_from_never_attempting_one()
        {
            var notAttempted = EscalationRecord.ReachedHumanWithoutAttempt(ClassC());
            var attemptedAndAborted = EscalationRecord.AfterDisruptiveDownload(
                ClassA(), aborted: true, detail: "the disruptive Software download raised OverwriteSystemData and aborted");

            Assert.NotEqual(notAttempted.Outcome, attemptedAndAborted.Outcome);
            Assert.False(notAttempted.DisruptiveDownloadAttempted);
            Assert.True(attemptedAndAborted.DisruptiveDownloadAttempted);

            // Caveat 2: an aborted disruptive download is a total test abort and is never retried.
            Assert.True(attemptedAndAborted.IsTotalTestAbort);
            Assert.Contains("DO NOT RETRY", attemptedAndAborted.ToLogLine(), StringComparison.Ordinal);
        }

        [Fact]
        public void A_resolved_disruptive_download_is_not_a_total_test_abort()
        {
            var record = EscalationRecord.AfterDisruptiveDownload(
                ClassA(), aborted: false, detail: "PLC_1 stopped., 99 objects loaded, PLC_1 started.");

            Assert.Equal(EscalationOutcome.DisruptiveDownloadResolvedIt, record.Outcome);
            Assert.False(record.IsTotalTestAbort);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void A_disruptive_download_cannot_be_recorded_for_class_B(bool aborted)
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => EscalationRecord.AfterDisruptiveDownload(ClassB(), aborted, "whatever happened"));

            Assert.Contains("Class B", ex.Message, StringComparison.Ordinal);
            Assert.Contains("straight to step 7", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void A_disruptive_download_cannot_be_recorded_for_class_C()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => EscalationRecord.AfterDisruptiveDownload(ClassC(), aborted: true, detail: "whatever happened"));

            Assert.Contains("Class C", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Skipping_step_6_on_a_class_A_verdict_must_be_justified()
        {
            // Legitimate reasons exist (R8 permits the disruptive mode only while draining the deferred
            // queue; the excision budget may be spent) but the log must say which, or a later reader
            // cannot tell a policy decision from a bug.
            var ex = Assert.Throws<ArgumentException>(() => EscalationRecord.ReachedHumanWithoutAttempt(ClassA()));
            Assert.Equal("notAttemptedBecause", ex.ParamName);

            var justified = EscalationRecord.ReachedHumanWithoutAttempt(
                ClassA(), "the deferred queue is not being drained, so R8's disruptive mode is not open");

            Assert.False(justified.DisruptiveDownloadAttempted);
            Assert.Contains("R8's disruptive mode is not open", justified.Detail, StringComparison.Ordinal);
        }

        [Fact]
        public void A_spent_download_must_record_what_it_did()
        {
            var ex = Assert.Throws<ArgumentException>(
                () => EscalationRecord.AfterDisruptiveDownload(ClassA(), aborted: true, detail: "   "));

            Assert.Equal("detail", ex.ParamName);
        }

        [Fact]
        public void The_log_line_carries_the_timestamp_the_outcome_and_the_whole_verdict()
        {
            var at = new DateTimeOffset(2026, 8, 12, 23, 15, 0, TimeSpan.Zero);
            var line = EscalationRecord.ReachedHumanWithoutAttempt(ClassB(), recordedUtc: at).ToLogLine();

            Assert.StartsWith("2026-08-12T23:15:00", line, StringComparison.Ordinal);
            Assert.Contains("CONFIG-CLASS B", line, StringComparison.Ordinal);
            Assert.Contains("InitializeMemory", line, StringComparison.Ordinal);
        }
    }
}
