using System;
using System.Linq;

namespace Ladder.Wave.Tests
{
    /// <summary>
    /// What an escalation record can say now that A6 is measured — and could not before.
    /// </summary>
    public sealed class EscalationRecordAftermathTests
    {
        private static readonly DateTimeOffset At = new DateTimeOffset(2026, 8, 13, 15, 0, 0, TimeSpan.Zero);

        private static ConfigurationVerdict PreEntry() =>
            ConfigurationClassifier.Classify("StopModules", "NoAction", "StopAll");

        private static ConfigurationVerdict PostEntry() =>
            ConfigurationClassifier.Classify("StartModules", "NoAction", "StartModule");

        private static ConfigurationVerdict ClassB() =>
            ConfigurationClassifier.Classify("ResetModule", "NoAction", "DeleteAll");

        // =============================================================================================
        // THE RECORD A6 MADE POSSIBLE
        // =============================================================================================

        [Fact]
        public void An_abort_that_stopped_the_cpu_records_the_owed_recovery()
        {
            // Before the measurement an escalation could only say whether a download had been spent. A
            // human reading the log was told LESS than the tool knew.
            var record = EscalationRecord.AfterAbortThatStoppedTheCpu(
                PostEntry(),
                "policy refused StartModules on a wave boundary",
                At);

            Assert.Equal(EscalationOutcome.AbortLeftTheCpuStoppedAndARecoveryIsOwed, record.Outcome);
            Assert.True(record.CpuIsStopped);
            Assert.True(record.RecoveryOwed);
            Assert.Equal(AbortAftermath.CpuLeftStoppedWithACompleteProgram, record.Aftermath);

            var line = record.ToLogLine();
            Assert.Contains("CPU STOPPED WITH A COMPLETE PROGRAM", line, StringComparison.Ordinal);
            Assert.Contains("RECOVERY OWED", line, StringComparison.Ordinal);
            Assert.Contains("34 s", line, StringComparison.Ordinal);
        }

        [Fact]
        public void An_owed_recovery_cannot_be_recorded_for_an_abort_that_left_the_cpu_running()
        {
            // Recording one here would send somebody to restart a rig that never stopped, and would make
            // the flag unreadable for the cases that need it.
            var ex = Assert.Throws<InvalidOperationException>(
                () => EscalationRecord.AfterAbortThatStoppedTheCpu(PreEntry(), "a PRE-delegate refusal", At));

            Assert.Contains("leaves the CPU RUNNING", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void A_recovery_that_restored_RUN_discharges_the_debt()
        {
            var owed = EscalationRecord.AfterAbortThatStoppedTheCpu(PostEntry(), "refused in POST", At);

            var recovered = EscalationRecord.AfterRecoveryDownload(
                owed,
                cpuIsRunning: true,
                detail: "disruptive download raised StartModules in POST, answered, CPU read back Running (8)",
                recordedUtc: At.AddSeconds(34));

            Assert.Equal(EscalationOutcome.RecoveryDownloadRestartedTheCpu, recovered.Outcome);
            Assert.False(recovered.RecoveryOwed);
            Assert.False(recovered.CpuIsStopped);
            Assert.False(recovered.IsTotalTestAbort);
        }

        [Fact]
        public void A_recovery_that_did_NOT_restore_RUN_still_owes_one_and_is_a_total_abort()
        {
            // The outcome that must never be mistaken for the one above.
            var owed = EscalationRecord.AfterAbortThatStoppedTheCpu(PostEntry(), "refused in POST", At);

            var failed = EscalationRecord.AfterRecoveryDownload(
                owed,
                cpuIsRunning: false,
                detail: "download ran; PlcGetStatus did not read back 8",
                recordedUtc: At.AddSeconds(40));

            Assert.Equal(EscalationOutcome.RecoveryDownloadDidNotRestartTheCpu, failed.Outcome);
            Assert.True(failed.RecoveryOwed);
            Assert.True(failed.IsTotalTestAbort);
            Assert.Contains("STILL NOT RUNNING", failed.ToLogLine(), StringComparison.Ordinal);
        }

        [Fact]
        public void A_recovery_cannot_be_logged_for_a_record_that_never_owed_one()
        {
            var neverStopped = EscalationRecord.ReachedHumanWithoutAttempt(ClassB(), null, At);

            Assert.Throws<InvalidOperationException>(
                () => EscalationRecord.AfterRecoveryDownload(neverStopped, true, "a restart that never happened", At));
        }

        [Fact]
        public void A_failed_recovery_may_be_followed_by_another_because_the_debt_is_still_owed()
        {
            var owed = EscalationRecord.AfterAbortThatStoppedTheCpu(PostEntry(), "refused in POST", At);
            var failed = EscalationRecord.AfterRecoveryDownload(owed, false, "did not come back", At.AddSeconds(40));

            var second = EscalationRecord.AfterRecoveryDownload(failed, true, "second attempt read back Running (8)", At.AddSeconds(80));

            Assert.Equal(EscalationOutcome.RecoveryDownloadRestartedTheCpu, second.Outcome);
        }

        // =============================================================================================
        // THE POST RUNG
        // =============================================================================================

        [Fact]
        public void Answering_in_place_is_recordable_and_spends_nothing()
        {
            var record = EscalationRecord.AnsweredInPlace(PostEntry(), "answered StartModule where it was raised", At);

            Assert.Equal(EscalationOutcome.AnsweredWithinTheCurrentDownload, record.Outcome);
            Assert.False(record.DisruptiveDownloadAttempted);
            Assert.False(record.RecoveryOwed);
            Assert.False(record.IsTotalTestAbort);
            Assert.Contains("ANSWERED IN PLACE", record.ToLogLine(), StringComparison.Ordinal);
        }

        [Fact]
        public void Answering_in_place_is_refused_for_a_configuration_whose_rung_is_not_that()
        {
            // For anything else this logs a refusal as an answer — the difference between a CPU that is
            // running and one that is not.
            Assert.Throws<InvalidOperationException>(
                () => EscalationRecord.AnsweredInPlace(PreEntry(), "answered it anyway", At));

            Assert.Throws<InvalidOperationException>(
                () => EscalationRecord.AnsweredInPlace(ClassB(), "answered it anyway", At));
        }

        // =============================================================================================
        // THE CORRECTED TOTAL-ABORT VERDICT
        // =============================================================================================

        [Fact]
        public void Every_outcome_has_a_deliberate_total_abort_verdict()
        {
            // IsTotalTestAbort used to read `Outcome != DisruptiveDownloadResolvedIt`, which was right
            // with three outcomes and wrong with seven. This pins each one, so a NEW outcome added later
            // cannot slip in as either answer without somebody choosing.
            var expected = new (EscalationOutcome outcome, bool totalAbort)[]
            {
                (EscalationOutcome.ReachedHumanWithoutAttemptingDownload, true),
                (EscalationOutcome.DisruptiveDownloadResolvedIt, false),
                (EscalationOutcome.DisruptiveDownloadAbortedToo, true),
                (EscalationOutcome.AnsweredWithinTheCurrentDownload, false),
                (EscalationOutcome.AbortLeftTheCpuStoppedAndARecoveryIsOwed, false),
                (EscalationOutcome.RecoveryDownloadRestartedTheCpu, false),
                (EscalationOutcome.RecoveryDownloadDidNotRestartTheCpu, true),
            };

            Assert.Equal(
                Enum.GetValues(typeof(EscalationOutcome)).Cast<EscalationOutcome>().OrderBy(o => (int)o).ToArray(),
                expected.Select(e => e.outcome).ToArray());
        }

        [Fact]
        public void Every_outcome_has_a_deliberate_download_spent_verdict_too()
        {
            // *** THE SECOND PROPERTY DEFINED BY NEGATION, FOUND BY THE TEST ABOVE'S SIBLING. ***
            // DisruptiveDownloadAttempted read `Outcome != ReachedHumanWithoutAttemptingDownload`, which
            // silently acquired a new meaning as soon as outcomes were added that spend no download.
            var spendsADownload = new[]
            {
                EscalationOutcome.DisruptiveDownloadResolvedIt,
                EscalationOutcome.DisruptiveDownloadAbortedToo,
                EscalationOutcome.RecoveryDownloadRestartedTheCpu,
                EscalationOutcome.RecoveryDownloadDidNotRestartTheCpu,
            };

            var spendsNothing = new[]
            {
                EscalationOutcome.ReachedHumanWithoutAttemptingDownload,
                EscalationOutcome.AnsweredWithinTheCurrentDownload,
                EscalationOutcome.AbortLeftTheCpuStoppedAndARecoveryIsOwed,
            };

            // Every outcome is in exactly one of the two lists — so a new one cannot be added without
            // being classified here.
            Assert.Equal(
                Enum.GetValues(typeof(EscalationOutcome)).Cast<EscalationOutcome>().OrderBy(o => (int)o).ToArray(),
                spendsADownload.Concat(spendsNothing).OrderBy(o => (int)o).ToArray());

            var owed = EscalationRecord.AfterAbortThatStoppedTheCpu(PostEntry(), "refused in POST", At);
            var answered = EscalationRecord.AnsweredInPlace(PostEntry(), "answered in place", At);
            var recovered = EscalationRecord.AfterRecoveryDownload(owed, true, "back to Running (8)", At);

            Assert.False(owed.DisruptiveDownloadAttempted);
            Assert.False(answered.DisruptiveDownloadAttempted);
            Assert.True(recovered.DisruptiveDownloadAttempted);
        }

        [Fact]
        public void An_owed_recovery_is_not_a_total_abort_because_the_rig_is_recoverable_without_a_human()
        {
            // 34 s, no owner needed. Calling it a session abort would fetch a person for something the
            // coordinator can fix.
            var owed = EscalationRecord.AfterAbortThatStoppedTheCpu(PostEntry(), "refused in POST", At);

            Assert.False(owed.IsTotalTestAbort);
            Assert.True(owed.RecoveryOwed);
        }

        [Fact]
        public void The_existing_factories_still_carry_the_aftermath_and_the_old_verdicts_are_unchanged()
        {
            // The did-not-run case for the aftermath field on the paths that predate it.
            var human = EscalationRecord.ReachedHumanWithoutAttempt(ClassB(), null, At);
            var resolved = EscalationRecord.AfterDisruptiveDownload(PreEntry(), false, "went through", At);
            var abortedToo = EscalationRecord.AfterDisruptiveDownload(PreEntry(), true, "aborted again", At);

            Assert.True(human.IsTotalTestAbort);
            Assert.False(resolved.IsTotalTestAbort);
            Assert.True(abortedToo.IsTotalTestAbort);

            Assert.False(human.RecoveryOwed);
            Assert.False(resolved.RecoveryOwed);
            Assert.Equal(AbortAftermath.CpuLeftRunning, resolved.Aftermath);
        }

        [Fact]
        public void A_record_must_say_what_happened()
        {
            Assert.Throws<ArgumentException>(() => EscalationRecord.AnsweredInPlace(PostEntry(), "   ", At));
            Assert.Throws<ArgumentException>(() => EscalationRecord.AfterAbortThatStoppedTheCpu(PostEntry(), "  ", At));
        }
    }
}
