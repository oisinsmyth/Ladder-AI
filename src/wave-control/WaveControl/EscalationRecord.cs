using System;

namespace Ladder.Wave
{
    /// <summary>
    /// What actually happened after a configuration was classified. D32: "THE CLASSIFICATION MUST BE
    /// EXPLICIT IN THE LOG — 'went to step 7 without attempting a download, because Class B' is a
    /// materially different record from 'attempted the full download and it aborted too'."
    /// </summary>
    public enum EscalationOutcome
    {
        /// <summary>
        /// Step 7 was reached WITHOUT a download being attempted. The zero value: if a caller records
        /// nothing, the record says no download was spent, which is the claim that cannot mislead a
        /// later reader into thinking the ladder was exhausted.
        /// </summary>
        ReachedHumanWithoutAttemptingDownload = 0,

        /// <summary>Step 6 was attempted and the disruptive download went through. The wave continues.</summary>
        DisruptiveDownloadResolvedIt = 1,

        /// <summary>
        /// Step 6 was attempted and the disruptive download ALSO aborted on a configuration. D32
        /// caveat 2: this is a TOTAL TEST ABORT and it waits for a human. *** NEVER RETRY A
        /// DISRUPTIVE DOWNLOAD THAT ABORTED ON A CONFIGURATION *** — retrying restarts the ladder
        /// against an unchanged situation and consumes the night.
        /// </summary>
        DisruptiveDownloadAbortedToo = 2,

        /// <summary>
        /// The configuration was ANSWERED where it was raised — the POST-delegate rung. No download was
        /// spent and none is owed: this is the ladder working, not escalating.
        /// </summary>
        AnsweredWithinTheCurrentDownload = 3,

        /// <summary>
        /// *** THE ABORT LEFT THE CPU STOPPED, HOLDING A COMPLETE PROGRAM, AND A RECOVERY DOWNLOAD IS
        /// OWED *** [M 2026-08-13]. Recordable since the A6 measurement; before it, an escalation could
        /// only say whether a download had been spent, and a human reading the log was told LESS than
        /// the tool knew.
        /// </summary>
        AbortLeftTheCpuStoppedAndARecoveryIsOwed = 4,

        /// <summary>
        /// A recovery download ran and the CPU read back RUNNING. Measured at 34 s with no owner: the
        /// download raises StartModules in POST — which happens only because the CPU was stopped — and
        /// answering it restores Running (8).
        /// </summary>
        RecoveryDownloadRestartedTheCpu = 5,

        /// <summary>
        /// A recovery download ran and the CPU is STILL not running. The owed recovery is NOT
        /// discharged, and this is the outcome that must never be mistaken for the one above.
        /// </summary>
        RecoveryDownloadDidNotRestartTheCpu = 6,
    }

    /// <summary>
    /// One line of the unattended run's account of an unhandled configuration: the verdict, and
    /// whether a CPU stop was spent on it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE FACTORIES ARE THE POLICY. There is no public constructor, and the two routes into this
    /// type enforce what D32's ladder permits rather than trusting a caller to remember it:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>
    ///     <see cref="AfterDisruptiveDownload"/> REFUSES a verdict that is not Class A. Spending a
    ///     disruptive download on Class B or C is the exact mistake caveat 3 exists to prevent, so it
    ///     cannot be recorded — and a caller that cannot record it has no reason to do it.
    ///   </description></item>
    ///   <item><description>
    ///     <see cref="ReachedHumanWithoutAttempt"/> requires an explicit reason when the verdict IS
    ///     Class A, because for Class A the ladder says step 6 exists. There are legitimate reasons to
    ///     skip it — R8 permits the disruptive mode ONLY while the deferred queue is being drained,
    ///     and the excision attempt count may already be spent — but the log must say which, or a
    ///     later reader cannot tell a policy decision from a bug.
    ///   </description></item>
    /// </list>
    /// </remarks>
    public sealed class EscalationRecord
    {
        private EscalationRecord(
            ConfigurationVerdict verdict,
            EscalationOutcome outcome,
            string detail,
            DateTimeOffset recordedUtc,
            AbortAftermath aftermath)
        {
            Verdict = verdict;
            Outcome = outcome;
            Detail = detail;
            RecordedUtc = recordedUtc;
            Aftermath = aftermath;
        }

        /// <summary>The classification this record is the aftermath of.</summary>
        public ConfigurationVerdict Verdict { get; }

        /// <summary>Whether a disruptive download was spent, and how it went.</summary>
        public EscalationOutcome Outcome { get; }

        /// <summary>The reason, or the download's own account of itself.</summary>
        public string Detail { get; }

        /// <summary>When the record was made.</summary>
        public DateTimeOffset RecordedUtc { get; }

        /// <summary>What the abort left the controller in [M 2026-08-13].</summary>
        public AbortAftermath Aftermath { get; }

        /// <summary>TRUE when the CPU was left stopped, holding a complete program.</summary>
        public bool CpuIsStopped => Aftermath == AbortAftermath.CpuLeftStoppedWithACompleteProgram;

        /// <summary>
        /// *** TRUE WHILE A RECOVERY DOWNLOAD IS STILL OWED. *** The state an escalation could not
        /// express before A6 was measured: the CPU is stopped, the program is complete, and nothing has
        /// yet put it back in RUN. A record that owes a recovery and is never followed by one is a rig
        /// left stopped, and the log now says so rather than reporting "no download spent".
        /// </summary>
        public bool RecoveryOwed =>
            Outcome == EscalationOutcome.AbortLeftTheCpuStoppedAndARecoveryIsOwed ||
            Outcome == EscalationOutcome.RecoveryDownloadDidNotRestartTheCpu;

        /// <summary>True when a download was actually spent on this configuration.</summary>
        /// <remarks>
        /// *** CORRECTED 2026-08-13, THE SAME WAY AND FOR THE SAME REASON AS
        /// <see cref="IsTotalTestAbort"/>, AND FOUND BY ITS TEST. *** This read
        /// <c>Outcome != ReachedHumanWithoutAttemptingDownload</c> — a definition by negation that was
        /// correct while every other outcome DID spend a download, and silently wrong the moment
        /// outcomes were added that do not: answering a configuration in place spends nothing, and an
        /// abort that left the CPU stopped spent the download that was already running rather than a
        /// new one. *** A PROPERTY DEFINED AS "NOT THE ONE EXCEPTION" ACQUIRES A NEW MEANING EVERY TIME
        /// AN OUTCOME IS ADDED, WITHOUT ANYBODY EDITING IT. *** Enumerated instead, and pinned per
        /// outcome by a test.
        /// </remarks>
        public bool DisruptiveDownloadAttempted =>
            Outcome == EscalationOutcome.DisruptiveDownloadResolvedIt ||
            Outcome == EscalationOutcome.DisruptiveDownloadAbortedToo ||
            Outcome == EscalationOutcome.RecoveryDownloadRestartedTheCpu ||
            Outcome == EscalationOutcome.RecoveryDownloadDidNotRestartTheCpu;

        /// <summary>
        /// True when this record ends the test SESSION rather than the wave — either the ladder ran
        /// out of rungs, or a disruptive download aborted too (caveat 2).
        /// </summary>
        /// <remarks>
        /// *** CORRECTED 2026-08-13 WHEN THE NEW OUTCOMES LANDED. *** This read
        /// <c>Outcome != DisruptiveDownloadResolvedIt</c>, which was right while there were three
        /// outcomes and became wrong with seven: answering a configuration within the current download,
        /// and a recovery that restored RUN, are the ladder WORKING. Enumerating what ends a session is
        /// the fail-closed direction for a DIFFERENT reason than usual — a new outcome added later is
        /// not silently a total abort, so it has to be classified deliberately, and the test that pins
        /// every outcome's verdict is what forces that.
        /// </remarks>
        public bool IsTotalTestAbort =>
            Outcome == EscalationOutcome.ReachedHumanWithoutAttemptingDownload ||
            Outcome == EscalationOutcome.DisruptiveDownloadAbortedToo ||
            Outcome == EscalationOutcome.RecoveryDownloadDidNotRestartTheCpu;

        /// <summary>
        /// Step 6 was NOT attempted. For Class B and C that is the correct and automatic outcome; for
        /// Class A the caller must say why.
        /// </summary>
        /// <param name="verdict">The classification.</param>
        /// <param name="notAttemptedBecause">
        /// Required for a Class A verdict; ignored (and replaced by the class's own reason) for B and C.
        /// </param>
        public static EscalationRecord ReachedHumanWithoutAttempt(
            ConfigurationVerdict verdict,
            string? notAttemptedBecause = null,
            DateTimeOffset? recordedUtc = null)
        {
            if (verdict == null)
            {
                throw new ArgumentNullException(nameof(verdict));
            }

            string detail;
            if (verdict.Class == ConfigurationClass.RefusedOnlyBecauseNonDisruptive)
            {
                if (string.IsNullOrWhiteSpace(notAttemptedBecause))
                {
                    throw new ArgumentException(
                        "This is a Class A verdict, so D32 step 6 exists for it and a disruptive download " +
                        "would resolve it. Skipping step 6 is legitimate — R8 permits the disruptive mode " +
                        "only while the deferred queue is being drained, and the excision budget may be " +
                        "spent — but the reason must be recorded, or the log cannot tell a policy decision " +
                        "from a bug.",
                        nameof(notAttemptedBecause));
                }

                detail = "no download attempted: " + notAttemptedBecause!.Trim();
            }
            else
            {
                detail = "no download attempted, because Class " + verdict.ClassLetter + ": " + verdict.Reason;
                if (!string.IsNullOrWhiteSpace(notAttemptedBecause))
                {
                    detail += " (" + notAttemptedBecause!.Trim() + ")";
                }
            }

            return new EscalationRecord(
                verdict,
                EscalationOutcome.ReachedHumanWithoutAttemptingDownload,
                detail,
                recordedUtc ?? DateTimeOffset.UtcNow,
                verdict.Aftermath);
        }

        /// <summary>
        /// Step 6 WAS attempted. Class A only — see the remarks on this type.
        /// </summary>
        /// <param name="verdict">The classification. Must be Class A.</param>
        /// <param name="aborted">True if the disruptive download also aborted on a configuration.</param>
        /// <param name="detail">The download's own account of itself, for the log.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <paramref name="verdict"/> is not Class A. Escalating on Class B or C buys
        /// nothing and costs a CPU stop; it must not be recordable, so that it is not doable.
        /// </exception>
        public static EscalationRecord AfterDisruptiveDownload(
            ConfigurationVerdict verdict,
            bool aborted,
            string detail,
            DateTimeOffset? recordedUtc = null)
        {
            if (verdict == null)
            {
                throw new ArgumentNullException(nameof(verdict));
            }

            if (verdict.Class != ConfigurationClass.RefusedOnlyBecauseNonDisruptive)
            {
                throw new InvalidOperationException(
                    "A disruptive download must not be attempted for a Class " + verdict.ClassLetter +
                    " configuration. " + (verdict.Class == ConfigurationClass.NeverPermitted
                        ? "Class B is refused identically in every mode, so it aborts identically."
                        : "Class C is unknown, and a full download has a LARGER delta than a differential " +
                          "so it raises MORE configurations, never fewer.") +
                    " D32 caveat 3: go straight to step 7. Offending configuration: " + verdict.Configuration + ".");
            }

            var text = (detail ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                throw new ArgumentException(
                    "A disruptive download that has been spent must record what it did. This is the record " +
                    "that distinguishes 'attempted the full download and it aborted too' from 'went to the " +
                    "human without attempting one'.",
                    nameof(detail));
            }

            return new EscalationRecord(
                verdict,
                aborted ? EscalationOutcome.DisruptiveDownloadAbortedToo : EscalationOutcome.DisruptiveDownloadResolvedIt,
                text,
                recordedUtc ?? DateTimeOffset.UtcNow,
                verdict.Aftermath);
        }

        /// <summary>
        /// THE POST-DELEGATE RUNG. The configuration was answered where it was raised, because "go to
        /// step 6" is circular for it. No download spent, no recovery owed.
        /// </summary>
        public static EscalationRecord AnsweredInPlace(
            ConfigurationVerdict verdict,
            string detail,
            DateTimeOffset? recordedUtc = null)
        {
            if (verdict == null)
            {
                throw new ArgumentNullException(nameof(verdict));
            }

            if (verdict.NextRung != LadderRung.AnswerWithinTheCurrentDownload)
            {
                throw new InvalidOperationException(
                    "Only a configuration whose per-entry rung is AnswerWithinTheCurrentDownload may be " +
                    "recorded as answered in place. For anything else this logs a refusal as an answer, " +
                    "which is the difference between a CPU that is running and one that is not. " +
                    "Offending configuration: " + verdict.Configuration + ".");
            }

            return new EscalationRecord(
                verdict,
                EscalationOutcome.AnsweredWithinTheCurrentDownload,
                Required(detail, nameof(detail)),
                recordedUtc ?? DateTimeOffset.UtcNow,
                verdict.Aftermath);
        }

        /// <summary>
        /// *** THE RECORD A6 MADE POSSIBLE: the abort landed after the transfer, so the CPU is STOPPED
        /// holding a COMPLETE program and a recovery download is OWED. ***
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The verdict's abort does not leave the CPU stopped. Recording an owed recovery for a
        /// controller that is still running would send somebody to restart a rig that never stopped,
        /// and would make the flag unreadable for the cases that need it.
        /// </exception>
        public static EscalationRecord AfterAbortThatStoppedTheCpu(
            ConfigurationVerdict verdict,
            string detail,
            DateTimeOffset? recordedUtc = null)
        {
            if (verdict == null)
            {
                throw new ArgumentNullException(nameof(verdict));
            }

            if (!verdict.AbortLeavesTheCpuStopped)
            {
                throw new InvalidOperationException(
                    "This configuration's abort does not leave the CPU stopped — it is raised in the " +
                    verdict.RaisedIn + " delegate, and a throw there leaves the CPU RUNNING with the " +
                    "project bit-for-bit unchanged [M 2026-08-13].");
            }

            return new EscalationRecord(
                verdict,
                EscalationOutcome.AbortLeftTheCpuStoppedAndARecoveryIsOwed,
                Required(detail, nameof(detail)) + " " + AbortAftermathTable.RecoveryEvidence,
                recordedUtc ?? DateTimeOffset.UtcNow,
                verdict.Aftermath);
        }

        /// <summary>
        /// The recovery ran. <paramref name="cpuIsRunning"/> is the READ-BACK, never the request.
        /// </summary>
        /// <param name="owed">The record that owed the recovery.</param>
        /// <param name="cpuIsRunning">
        /// What the controller reports NOW. *** X-K's rule: key on RUN, never on STOP *** — Sharp7 maps
        /// 8 to Run and EVERYTHING ELSE to Stop, so a Run reading is precise while a Stop reading is
        /// "4, or anything unrecognised". This parameter is therefore "is it running", not "did it stop".
        /// </param>
        /// <param name="detail">What the recovery download did.</param>
        public static EscalationRecord AfterRecoveryDownload(
            EscalationRecord owed,
            bool cpuIsRunning,
            string detail,
            DateTimeOffset? recordedUtc = null)
        {
            if (owed == null)
            {
                throw new ArgumentNullException(nameof(owed));
            }

            if (!owed.RecoveryOwed)
            {
                throw new InvalidOperationException(
                    "No recovery was owed by the record supplied (" + owed.Outcome + "). Logging a " +
                    "recovery for a CPU that was never stopped puts a restart in the log that never " +
                    "happened, and makes the owed-recovery flag unreadable for the cases that need it.");
            }

            return new EscalationRecord(
                owed.Verdict,
                cpuIsRunning
                    ? EscalationOutcome.RecoveryDownloadRestartedTheCpu
                    : EscalationOutcome.RecoveryDownloadDidNotRestartTheCpu,
                Required(detail, nameof(detail)),
                recordedUtc ?? DateTimeOffset.UtcNow,
                cpuIsRunning ? AbortAftermath.CpuLeftRunning : AbortAftermath.CpuLeftStoppedWithACompleteProgram);
        }

        /// <summary>One line for an unattended run's log, carrying the verdict and the aftermath.</summary>
        public string ToLogLine()
        {
            string outcome;
            switch (Outcome)
            {
                case EscalationOutcome.DisruptiveDownloadResolvedIt:
                    outcome = "DISRUPTIVE DOWNLOAD SPENT, RESOLVED";
                    break;
                case EscalationOutcome.DisruptiveDownloadAbortedToo:
                    outcome = "DISRUPTIVE DOWNLOAD SPENT, ABORTED TOO — TOTAL TEST ABORT, DO NOT RETRY";
                    break;
                case EscalationOutcome.AnsweredWithinTheCurrentDownload:
                    outcome = "ANSWERED IN PLACE (POST rung), NO DOWNLOAD SPENT";
                    break;
                case EscalationOutcome.AbortLeftTheCpuStoppedAndARecoveryIsOwed:
                    outcome = "*** CPU STOPPED WITH A COMPLETE PROGRAM — RECOVERY DOWNLOAD OWED ***";
                    break;
                case EscalationOutcome.RecoveryDownloadRestartedTheCpu:
                    outcome = "RECOVERY DOWNLOAD RAN, CPU READ BACK RUNNING";
                    break;
                case EscalationOutcome.RecoveryDownloadDidNotRestartTheCpu:
                    outcome = "*** RECOVERY DOWNLOAD RAN AND THE CPU IS STILL NOT RUNNING ***";
                    break;
                default:
                    outcome = "HUMAN, NO DOWNLOAD SPENT";
                    break;
            }

            return RecordedUtc.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ") + " " +
                   outcome + (RecoveryOwed ? " [RECOVERY OWED]" : "") +
                   " | " + Verdict.ToLogLine() + " | " + Detail;
        }

        private static string Required(string value, string parameterName)
        {
            var text = (value ?? "").Trim();
            if (text.Length == 0)
            {
                throw new ArgumentException(
                    "An escalation record must say what happened. This is the account a human reads " +
                    "instead of the rig.",
                    parameterName);
            }

            return text;
        }

        /// <inheritdoc />
        public override string ToString() => ToLogLine();
    }
}
