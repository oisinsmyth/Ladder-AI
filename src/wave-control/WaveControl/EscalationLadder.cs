using System;
using System.Collections.Generic;
using System.Linq;

namespace Ladder.Wave
{
    /// <summary>D32 step 4's answer, and *** THE LOG MUST DISTINGUISH THE MIDDLE TWO. ***</summary>
    /// <remarks>
    /// Caveat 1: <c>DataBlockReinitialization</c> and <c>StopModules</c> ARE attributable — DB-1's
    /// change classes map onto them. <c>UserManagementDownload</c> and <c>OverwriteSystemData</c> are
    /// PROJECT-LEVEL: no block entails them, so there is nothing to excise. *** "No block is
    /// responsible" and "the locator failed to find one" take the same branch and call for OPPOSITE
    /// fixes, and a log that conflates them will send a fix wave after a locator that was working
    /// correctly. ***
    /// </remarks>
    public enum AttributionOutcome
    {
        /// <summary>Attribution was not attempted. The zero value — it never reads as "nothing to excise".</summary>
        NotAttempted = 0,

        /// <summary>DB-1 named the block that entails the configuration.</summary>
        Attributed = 1,

        /// <summary>
        /// The configuration is PROJECT-LEVEL: no block entails it and there is nothing to excise. Not
        /// a locator bug — step 5 does not exist for it.
        /// </summary>
        NoBlockIsResponsible = 2,

        /// <summary>
        /// The locator ran and found nothing, on a configuration a block SHOULD entail. That is a
        /// defect in DB-1 or its baseline, and the fix is the opposite of the one above.
        /// </summary>
        LocatorFailed = 3,
    }

    /// <summary>What the ladder decided to do next, and whether the decision rests on a measurement.</summary>
    public sealed class LadderDecision
    {
        internal LadderDecision(
            LadderRung rung,
            ConfigurationVerdict verdict,
            AbortAftermath aftermath,
            bool requiresCpuStart,
            bool restsOnAMeasuredAftermath,
            string reason,
            string evidence)
        {
            Rung = rung;
            Verdict = verdict;
            Aftermath = aftermath;
            RequiresCpuStart = requiresCpuStart;
            RestsOnAMeasuredAftermath = restsOnAMeasuredAftermath;
            Reason = reason;
            Evidence = evidence;
        }

        /// <summary>Which rung.</summary>
        public LadderRung Rung { get; }

        /// <summary>The classification behind it.</summary>
        public ConfigurationVerdict Verdict { get; }

        /// <summary>What the abort left the controller in.</summary>
        public AbortAftermath Aftermath { get; }

        /// <summary>
        /// TRUE when the CPU is stopped and whatever comes next MUST carry a start step. This is the
        /// half of the correction D32's one-rung-per-class could not express.
        /// </summary>
        public bool RequiresCpuStart { get; }

        /// <summary>
        /// *** FALSE MEANS THIS RUNG IS UNPROVEN. *** It rests on an aftermath nobody has measured —
        /// the half-loaded case, which needs a throw DURING the transfer and has no configuration to
        /// hang one on. A caller must not treat an unproven rung as equivalent to a measured one.
        /// </summary>
        public bool RestsOnAMeasuredAftermath { get; }

        /// <summary>Why.</summary>
        public string Reason { get; }

        /// <summary>Where the claim comes from.</summary>
        public string Evidence { get; }

        /// <summary>TRUE when this ends the session rather than the wave.</summary>
        public bool IsTotalTestAbort => Rung == LadderRung.Step7TotalTestAbort;

        /// <summary>One line for an unattended run's log.</summary>
        public string ToLogLine() =>
            "LADDER -> " + Rung +
            (RequiresCpuStart ? " [CPU IS STOPPED — THE NEXT STEP MUST START IT]" : string.Empty) +
            (RestsOnAMeasuredAftermath ? string.Empty : " [UNPROVEN — rests on an unmeasured aftermath]") +
            " :: " + Reason + " | " + Evidence + " | " + Verdict.ToLogLine();

        /// <inheritdoc />
        public override string ToString() => ToLogLine();
    }

    /// <summary>How many of each attempt have been spent on this configuration already.</summary>
    public sealed class LadderAttempts
    {
        /// <param name="excisionAttemptsSoFar">How many excise-and-redownload rounds have been spent.</param>
        /// <param name="maxExcisionAttempts">
        /// The cap. REQUIRED — no default. Excision is the cheap rung, so without a bound the ladder
        /// can keep removing blocks until the wave is empty and call that progress.
        /// </param>
        /// <param name="disruptiveDownloadAlreadyAborted">
        /// D32 caveat 2: *** NEVER RETRY A DISRUPTIVE DOWNLOAD THAT ABORTED ON A CONFIGURATION. ***
        /// </param>
        public LadderAttempts(int excisionAttemptsSoFar, int maxExcisionAttempts, bool disruptiveDownloadAlreadyAborted = false)
        {
            if (maxExcisionAttempts < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExcisionAttempts));
            }

            ExcisionAttemptsSoFar = Math.Max(0, excisionAttemptsSoFar);
            MaxExcisionAttempts = maxExcisionAttempts;
            DisruptiveDownloadAlreadyAborted = disruptiveDownloadAlreadyAborted;
        }

        /// <summary>Excision rounds spent.</summary>
        public int ExcisionAttemptsSoFar { get; }

        /// <summary>The cap on them.</summary>
        public int MaxExcisionAttempts { get; }

        /// <summary>Whether a disruptive download has already aborted on a configuration.</summary>
        public bool DisruptiveDownloadAlreadyAborted { get; }

        /// <summary>TRUE when another excise-and-redownload round is still permitted.</summary>
        public bool ExcisionBudgetRemains => ExcisionAttemptsSoFar < MaxExcisionAttempts;
    }

    /// <summary>
    /// D32's escalation ladder, with the Class-A rung CORRECTED against the 2026-08-13 measurement.
    /// </summary>
    /// <remarks>
    /// <para>
    /// *** THE CORRECTION, IN ONE PARAGRAPH. *** D32 gives one rung per CLASS, and the Class-A rung —
    /// "spend a disruptive full download, it ANSWERS the configuration rather than hoping it does not
    /// appear" — is sound only if the abort left the CPU untouched. Measured: after a PRE-delegate
    /// throw it did (CPU <c>Running (8)</c>, project unchanged). After a POST-delegate throw it did
    /// NOT — the CPU was left <c>NotRunning (4)</c> holding a COMPLETE program. So the rung is TRUE for
    /// PRE and FALSE for POST, and *** the ladder needs a PER-ENTRY rung, not one rung per class. ***
    /// </para>
    /// <para>
    /// The two POST consequences are different and both are handled:
    /// </para>
    /// <list type="number">
    ///   <item><description>
    ///     For <c>StartModules</c> — the one POST entry — "go to step 6" is CIRCULAR: it is raised only
    ///     because this download already stopped the CPU, so fetching a disruptive download to answer it
    ///     is fetching the thing we are inside. Its rung is
    ///     <see cref="LadderRung.AnswerWithinTheCurrentDownload"/>, and
    ///     <see cref="DownloadConfigurationPolicy"/> answers it even on a wave boundary.
    ///   </description></item>
    ///   <item><description>
    ///     For any abort that nonetheless landed AFTER the transfer, the next download must CARRY A
    ///     START STEP — <see cref="LadderRung.RecoveryDownloadThatStartsTheCpu"/>. Measured: a normal
    ///     disruptive download raises <c>StartModules</c> in POST, answering it returns the CPU to
    ///     <c>Running (8)</c>, 34 s, no owner.
    ///   </description></item>
    /// </list>
    /// <para>
    /// *** WHAT REMAINS UNPROVEN, AND IT IS NAMED RATHER THAN PAPERED OVER. *** The genuinely
    /// half-loaded controller — a throw DURING the transfer — has never been observed, and there is no
    /// configuration raised during transfer to hang one on, so nothing in this design can reach it.
    /// Every rung here is derived from a MEASURED aftermath, and
    /// <see cref="LadderDecision.RestsOnAMeasuredAftermath"/> is the flag that says so. A decision
    /// carrying <c>false</c> is unproven and must be read as such.
    /// </para>
    /// </remarks>
    public static class EscalationLadder
    {
        /// <summary>Decide the next rung after a policy abort.</summary>
        /// <param name="verdict">The classification of the configuration that was refused.</param>
        /// <param name="attribution">D32 step 4's answer.</param>
        /// <param name="excision">
        /// The excision plan, when <paramref name="attribution"/> is
        /// <see cref="AttributionOutcome.Attributed"/>. A refused plan means step 5 cannot make progress.
        /// </param>
        /// <param name="attempts">What has already been spent.</param>
        public static LadderDecision Decide(
            ConfigurationVerdict verdict,
            AttributionOutcome attribution,
            ExcisionPlan? excision,
            LadderAttempts attempts)
        {
            if (verdict == null)
            {
                throw new ArgumentNullException(nameof(verdict));
            }

            if (attempts == null)
            {
                throw new ArgumentNullException(nameof(attempts));
            }

            var stage = verdict.RaisedIn;
            var aftermath = AbortAftermathTable.For(stage);
            var requiresStart = AbortAftermathTable.RequiresCpuStart(stage);
            var measured = AbortAftermathTable.IsMeasured(stage);
            var evidence = AbortAftermathTable.EvidenceFor(stage);

            // --- CAVEAT 2 FIRST. A disruptive download that aborted is terminal, whatever else is true.
            if (attempts.DisruptiveDownloadAlreadyAborted)
            {
                return new LadderDecision(
                    LadderRung.Step7TotalTestAbort, verdict, aftermath, requiresStart, measured,
                    "A disruptive download has ALREADY aborted on a configuration. D32 caveat 2: that is a " +
                    "TOTAL TEST ABORT — the whole session, not merely the wave — and it waits for a human. " +
                    "NEVER RETRY A DISRUPTIVE DOWNLOAD THAT ABORTED ON A CONFIGURATION: step 1 would fire " +
                    "again against an unchanged situation and the ladder would restart, consuming the night.",
                    evidence);
            }

            // --- CLASS B AND C: STRAIGHT TO A HUMAN, NO DOWNLOAD SPENT. ------------------------------
            if (verdict.Class != ConfigurationClass.RefusedOnlyBecauseNonDisruptive)
            {
                return new LadderDecision(
                    LadderRung.Step7TotalTestAbort, verdict, aftermath, requiresStart, measured,
                    "Class " + verdict.ClassLetter + ": step 6 is futile. " +
                    (verdict.Class == ConfigurationClass.NeverPermitted
                        ? "A disruptive download refuses it identically and aborts identically, so spending " +
                          "one buys nothing and costs a stop."
                        : "We cannot know what it entails, and a full download has a LARGER delta than a " +
                          "differential so it raises MORE configurations, never fewer.") +
                    " Went to step 7 WITHOUT attempting a download.",
                    evidence);
            }

            // --- THE PER-ENTRY RUNG, FOR A POST-TRANSFER CLASS A ENTRY. ------------------------------
            if (verdict.NextRung == LadderRung.AnswerWithinTheCurrentDownload)
            {
                return new LadderDecision(
                    LadderRung.AnswerWithinTheCurrentDownload, verdict, aftermath, requiresStart, measured,
                    "Raised in the POST delegate. 'Go to step 6' is CIRCULAR for this entry: the transfer " +
                    "has already happened and the CPU is already stopped, so a fresh disruptive download " +
                    "would be the thing we are inside. Refusing it does not defer a stop — it leaves the " +
                    "CPU stopped holding a complete program. Answer it HERE.",
                    evidence);
            }

            // --- STEP 5: EXCISE AND RE-DOWNLOAD. The only rung that makes progress cheaply. ----------
            if (attribution == AttributionOutcome.Attributed)
            {
                if (excision == null)
                {
                    return new LadderDecision(
                        LadderRung.Step7TotalTestAbort, verdict, aftermath, requiresStart, measured,
                        "The configuration was attributed to a block but no excision plan was supplied, so " +
                        "there is nothing to act on. An attribution without a plan is not step 5.",
                        evidence);
                }

                if (!attempts.ExcisionBudgetRemains)
                {
                    return new LadderDecision(
                        NextRungAfterExcisionIsExhausted(requiresStart), verdict, aftermath, requiresStart, measured,
                        "Attributable, but the excision budget is spent (" + attempts.ExcisionAttemptsSoFar +
                        " of " + attempts.MaxExcisionAttempts + "). Excision is the cheap rung, and without a " +
                        "bound the ladder keeps removing blocks until the wave is empty and calls that " +
                        "progress." + StartStepNote(requiresStart),
                        evidence);
                }

                if (!excision.Permitted)
                {
                    return new LadderDecision(
                        NextRungAfterExcisionIsExhausted(requiresStart), verdict, aftermath, requiresStart, measured,
                        "Attributable, but the excision cannot proceed: " + excision.Detail +
                        StartStepNote(requiresStart),
                        evidence);
                }

                return new LadderDecision(
                    LadderRung.Step5ExciseAndRedownload, verdict, aftermath, requiresStart, measured,
                    "Attributable to '" + excision.Requested + "'. Excise its whole dependency closure (" +
                    excision.Closure.Count + " object(s)), route it to the deferred queue (D23) and " +
                    "re-download the remaining " + excision.Remaining.Count + ". An ordinary boundary; no " +
                    "disruption is spent." + StartStepNote(requiresStart),
                    evidence);
            }

            // --- STEP 6, OR ITS CORRECTED FORM. -----------------------------------------------------
            var notAttributableBecause = attribution == AttributionOutcome.NoBlockIsResponsible
                ? "NO BLOCK IS RESPONSIBLE — the configuration is project-level, so there is nothing to " +
                  "excise and step 6 is the only rung that exists for it. This is NOT a locator bug."
                : attribution == AttributionOutcome.LocatorFailed
                    ? "THE LOCATOR FAILED to find a responsible block on a configuration one should entail. " +
                      "That is a defect in DB-1 or its baseline, and it calls for the OPPOSITE fix to 'no " +
                      "block is responsible' — the two take this same branch and must never be conflated."
                    : "Attribution was not attempted, so step 5 cannot be reached. Recorded as such rather " +
                      "than as 'nothing to excise', which is a different claim.";

            return new LadderDecision(
                requiresStart ? LadderRung.RecoveryDownloadThatStartsTheCpu : LadderRung.Step6DisruptiveFullDownload,
                verdict, aftermath, requiresStart, measured,
                notAttributableBecause + " " +
                (requiresStart
                    ? "AND THE ABORT LANDED AFTER THE TRANSFER: the CPU is stopped holding a complete " +
                      "program, so the next download is not merely the one that answers the configuration " +
                      "— it MUST also start the CPU. " + AbortAftermathTable.RecoveryEvidence
                    : "The abort left the CPU RUNNING and the project unchanged, so nothing has been spent " +
                      "and a disruptive full download ANSWERS the configuration rather than hoping it does " +
                      "not appear."),
                evidence);
        }

        private static LadderRung NextRungAfterExcisionIsExhausted(bool requiresStart) =>
            requiresStart ? LadderRung.RecoveryDownloadThatStartsTheCpu : LadderRung.Step6DisruptiveFullDownload;

        private static string StartStepNote(bool requiresStart) =>
            requiresStart
                ? " *** AND THE CPU IS STOPPED: whatever download comes next must carry a start step, " +
                  "because this abort landed after the transfer. ***"
                : string.Empty;
    }
}
