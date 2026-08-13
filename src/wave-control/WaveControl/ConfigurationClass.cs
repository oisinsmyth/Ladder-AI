namespace Ladder.Wave
{
    /// <summary>
    /// D32 caveat 3 — which class an unhandled download configuration falls into, and therefore
    /// whether the escalation ladder's step 6 (a disruptive full download) is progress or a wasted
    /// CPU stop.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE ZERO VALUE IS <see cref="Unknown"/>, AND THAT IS THE WHOLE SAFETY ARGUMENT OF THIS ENUM.
    /// A default-initialised field, a zeroed struct, a deserialisation that dropped the value, a
    /// <c>default(ConfigurationClass)</c> written by mistake — every one of them lands on the class
    /// that goes straight to a human. There is no path by which "nobody set this" reads as
    /// "escalating to a disruptive download is safe".
    /// </para>
    /// <para>
    /// This is deliberately the inverse mistake to the one R4a records against the Siemens enums:
    /// there, <c>StopModulesSelections.NoAction = 0</c> is safe while
    /// <c>DataBlockReinitializationSelections.StopPlcAndReinitialize = 0</c> is destructive, so a
    /// check written against one and reused for the other inverts silently. Here the zero value is
    /// pinned to the most conservative outcome and a test pins it there.
    /// </para>
    /// </remarks>
    public enum ConfigurationClass
    {
        /// <summary>
        /// CLASS C — unknown, or no <c>NoAction</c> and on neither list. The
        /// <c>SelectiveDeleteDownload</c> shape (<c>AcceptAll | DeleteSelected</c>) and every
        /// configuration nobody has seen yet. We cannot know what it entails and guessing is the
        /// thing D32 exists to forbid: straight to step 7, a human.
        /// <para>
        /// This is not the edge case. Each of the last three real downloads produced a configuration
        /// nobody predicted, so this is the branch most likely to be taken on the next unattended run.
        /// </para>
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// CLASS A — refused only because the mode is non-disruptive. Each entry is a consequence
        /// the chosen download option ALREADY entails, so a disruptive download can ANSWER it rather
        /// than hope it does not appear. The only class for which spending a CPU stop buys anything.
        /// </summary>
        RefusedOnlyBecauseNonDisruptive = 1,

        /// <summary>
        /// CLASS B — never permitted in any mode. These destroy BEYOND what the download option
        /// entails, so a disruptive download refuses them identically and aborts identically.
        /// Escalation buys nothing and costs a stop: straight to step 7, a human.
        /// </summary>
        NeverPermitted = 2,
    }

    /// <summary>
    /// Which rung of D32's ladder a verdict sends the caller to. There are only two outcomes once a
    /// configuration could not be attributed and excised: try the disruptive download, or stop and
    /// fetch a person.
    /// </summary>
    public enum LadderRung
    {
        /// <summary>
        /// Step 7 — TOTAL TEST ABORT (the session, not merely the wave) and wait for a human. The
        /// zero value, for the same reason <see cref="ConfigurationClass.Unknown"/> is.
        /// </summary>
        Step7TotalTestAbort = 0,

        /// <summary>
        /// Step 6 — a disruptive FULL download (D25/R8), never a differential. Reachable for Class A
        /// alone, and *** ONLY for an entry raised in the PRE delegate ***, where the abort leaves the
        /// CPU running and the project unchanged so nothing has yet been spent.
        /// </summary>
        Step6DisruptiveFullDownload = 1,

        /// <summary>
        /// Step 5 — EXCISE the attributable block from the wave set, route it to the deferred queue
        /// (D23) and re-download without it. An ordinary boundary; no disruption is spent. The only
        /// rung that makes progress cheaply.
        /// </summary>
        Step5ExciseAndRedownload = 2,

        /// <summary>
        /// *** ANSWER IT WITHIN THE CURRENT DOWNLOAD. *** For a Class A entry raised in the POST
        /// delegate, where "go to step 6" is CIRCULAR — the transfer has already happened and the CPU
        /// is already stopped, so fetching a fresh disruptive download to answer the configuration
        /// would be fetching the thing we are already inside. Refusing it leaves the CPU stopped.
        /// </summary>
        AnswerWithinTheCurrentDownload = 3,

        /// <summary>
        /// *** A DISRUPTIVE DOWNLOAD, AND IT MUST CARRY A START STEP. *** For an abort that landed
        /// AFTER the transfer: the CPU is stopped holding a complete program [M 2026-08-13], so the
        /// next download is not merely "the one that answers the configuration" — it is the one that
        /// raises <c>StartModules</c> in POST and answers it, returning the CPU to Running. Measured at
        /// 34 s with no owner present.
        /// </summary>
        RecoveryDownloadThatStartsTheCpu = 4,
    }

    /// <summary>
    /// Why a configuration landed in Class C. Every one of these goes to a human; the distinction
    /// exists for the log, because they call for different fixes.
    /// </summary>
    public enum UnknownReason
    {
        /// <summary>The configuration name appears on neither the allowance list nor the deny list.</summary>
        OnNeitherList = 0,

        /// <summary>
        /// The name is on the ALLOWANCE list but the raised configuration offered a selection no
        /// Class A entry claims an entailment for. The entailment argument is made per selection, so
        /// an unclaimed selection has no argument behind it — this is a KNOWN configuration in an
        /// UNRECOGNISED shape, and it is a stronger signal than a wholly new name.
        /// </summary>
        AllowedConfigurationInAnUnrecognisedShape = 1,

        /// <summary>
        /// The name is on the DENY list but none of that entry's denied selections was offered. The
        /// deny entry is keyed on a (configuration, selection) pair; a deny-listed configuration
        /// wearing a shape we have not seen is not evidence that it became safe.
        /// </summary>
        DeniedConfigurationInAnUnrecognisedShape = 2,

        /// <summary>The configuration carried no usable name at all. Cannot be looked up; cannot be trusted.</summary>
        NoConfigurationName = 3,
    }
}
