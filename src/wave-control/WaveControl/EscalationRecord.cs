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
            DateTimeOffset recordedUtc)
        {
            Verdict = verdict;
            Outcome = outcome;
            Detail = detail;
            RecordedUtc = recordedUtc;
        }

        /// <summary>The classification this record is the aftermath of.</summary>
        public ConfigurationVerdict Verdict { get; }

        /// <summary>Whether a disruptive download was spent, and how it went.</summary>
        public EscalationOutcome Outcome { get; }

        /// <summary>The reason, or the download's own account of itself.</summary>
        public string Detail { get; }

        /// <summary>When the record was made.</summary>
        public DateTimeOffset RecordedUtc { get; }

        /// <summary>True when a CPU stop was actually spent on this configuration.</summary>
        public bool DisruptiveDownloadAttempted => Outcome != EscalationOutcome.ReachedHumanWithoutAttemptingDownload;

        /// <summary>
        /// True when this record ends the test SESSION rather than the wave — either the ladder ran
        /// out of rungs, or a disruptive download aborted too (caveat 2).
        /// </summary>
        public bool IsTotalTestAbort => Outcome != EscalationOutcome.DisruptiveDownloadResolvedIt;

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
                recordedUtc ?? DateTimeOffset.UtcNow);
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
                recordedUtc ?? DateTimeOffset.UtcNow);
        }

        /// <summary>One line for an unattended run's log, carrying the verdict and the aftermath.</summary>
        public string ToLogLine()
        {
            var outcome = Outcome == EscalationOutcome.ReachedHumanWithoutAttemptingDownload
                ? "HUMAN, NO DOWNLOAD SPENT"
                : Outcome == EscalationOutcome.DisruptiveDownloadResolvedIt
                    ? "DISRUPTIVE DOWNLOAD SPENT, RESOLVED"
                    : "DISRUPTIVE DOWNLOAD SPENT, ABORTED TOO — TOTAL TEST ABORT, DO NOT RETRY";

            return RecordedUtc.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ") + " " +
                   outcome + " | " + Verdict.ToLogLine() + " | " + Detail;
        }

        /// <inheritdoc />
        public override string ToString() => ToLogLine();
    }
}
