using System;

namespace Ladder.Wave
{
    /// <summary>
    /// D32 step 1 — the abort is OURS. Thrown from the download delegate so it is distinguishable from
    /// one TIA caused, the same separation as FailedToApply versus RefusedByPolicy.
    /// </summary>
    /// <remarks>
    /// <para>
    /// *** WHAT THIS EXCEPTION LEAVES BEHIND IS NOW MEASURED, IN BOTH DIRECTIONS [M 2026-08-13]. ***
    /// <see cref="Aftermath"/> carries it: thrown from PRE the CPU is left RUNNING with the project
    /// unchanged; thrown from POST the CPU is left STOPPED holding a COMPLETE program. It travels on
    /// the exception because the catch site is exactly where a caller decides what to do next, and
    /// deciding that without knowing which state the controller is in is the mistake D32's Class-A
    /// rung made.
    /// </para>
    /// </remarks>
    public class DownloadAbortedByPolicyException : Exception
    {
        /// <summary>Creates the abort.</summary>
        public DownloadAbortedByPolicyException(string message, RaisedConfiguration configuration, DelegateStage stage)
            : base(message)
        {
            Configuration = configuration;
            Stage = stage;
        }

        /// <summary>The configuration we refused to answer.</summary>
        public RaisedConfiguration Configuration { get; }

        /// <summary>Which delegate the throw came from.</summary>
        public DelegateStage Stage { get; }

        /// <summary>What the controller is in as a result [M 2026-08-13].</summary>
        public AbortAftermath Aftermath => AbortAftermathTable.For(Stage);

        /// <summary>TRUE when whatever comes next must carry a start step.</summary>
        public bool CpuIsStopped => Aftermath == AbortAftermath.CpuLeftStoppedWithACompleteProgram;
    }

    /// <summary>What the policy decided to do with one raised configuration.</summary>
    public sealed class ConfigurationResponse
    {
        private ConfigurationResponse(bool answer, string? selection, ConfigurationVerdict verdict, string reason)
        {
            ShouldAnswer = answer;
            Selection = selection;
            Verdict = verdict;
            Reason = reason;
        }

        /// <summary>TRUE to apply <see cref="Selection"/>; FALSE to THROW.</summary>
        public bool ShouldAnswer { get; }

        /// <summary>The selection to apply, when answering. Null when throwing.</summary>
        public string? Selection { get; }

        /// <summary>The classification behind the decision.</summary>
        public ConfigurationVerdict Verdict { get; }

        /// <summary>Why, for the log.</summary>
        public string Reason { get; }

        /// <summary>One line for an unattended run's log.</summary>
        public string ToLogLine() =>
            (ShouldAnswer ? "ANSWER " + Selection : "REFUSE (THROW)") + " :: " + Reason + " | " + Verdict.ToLogLine();

        /// <inheritdoc />
        public override string ToString() => ToLogLine();

        internal static ConfigurationResponse Answer(string selection, ConfigurationVerdict verdict, string reason) =>
            new ConfigurationResponse(true, selection, verdict, reason);

        internal static ConfigurationResponse Throw(ConfigurationVerdict verdict, string reason) =>
            new ConfigurationResponse(false, null, verdict, reason);
    }

    /// <summary>Which download this delegate is serving.</summary>
    public enum DownloadMode
    {
        /// <summary>
        /// Not stated. The zero value, and it REFUSES EVERYTHING — a delegate that does not know which
        /// download it is in must not answer a configuration, because the whole allowance argument is
        /// "the option you chose already entails this" and nobody said which option that is.
        /// </summary>
        Unstated = 0,

        /// <summary>
        /// The ordinary wave boundary. Non-disruptive: stopping the CPU mid-testing is forbidden, so
        /// EVERY configuration is refused, Class A included.
        /// </summary>
        WaveBoundaryNonDisruptive = 1,

        /// <summary>
        /// R8's sanctioned disruptive mode — a SEPARATE, EXPLICITLY DECLARED mode, never a flag on the
        /// wave-boundary path and never a fallback reached by retry, permitted ONLY while the deferred
        /// queue is being drained (D24).
        /// </summary>
        Disruptive = 2,
    }

    /// <summary>
    /// R4 / D32 step 1 — *** NEVER WRITE A DOWNLOAD DELEGATE THAT ACCEPTS EVERYTHING. *** This decides,
    /// per raised configuration, whether to apply a selection or to abort by throwing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The guard is "REFUSE TO ANSWER AT ALL", which makes Openness abort with
    /// <c>Download configuration '&lt;type&gt;' was unhandled</c>. R4 records that the original
    /// wording — "answer NoAction and let it abort" — was WRONG BY MEASUREMENT: <c>NoAction</c> CANNOT
    /// BE APPLIED, on <c>StopModules</c> and <c>DataBlockReinitialization</c> alike, through the
    /// property setter and the underlying <c>SetAttribute</c>, even though <c>GetAttributeInfos()</c>
    /// reports it ReadWrite. Still fail-closed, through a different door.
    /// </para>
    /// <para>
    /// *** AND R4a's HAZARD IS WHY NOTHING HERE INFERS SAFETY FROM A DEFAULT: *** the zero values of
    /// the Siemens enums differ IN DIRECTION — <c>StopModulesSelections.NoAction = 0</c> is safe while
    /// <c>DataBlockReinitializationSelections.StopPlcAndReinitialize = 0</c> is DESTRUCTIVE. So the
    /// only selections this ever applies are ones a <see cref="ClassAEntry"/> names explicitly, and
    /// <see cref="DownloadMode.Unstated"/> answers nothing at all.
    /// </para>
    /// <para>
    /// This type takes no dependency on Openness and performs no download. It decides; the caller
    /// throws <see cref="DownloadAbortedByPolicyException"/> from inside the real delegate.
    /// </para>
    /// </remarks>
    public static class DownloadConfigurationPolicy
    {
        /// <summary>Decide what to do with one raised configuration.</summary>
        /// <param name="configuration">What was raised.</param>
        /// <param name="mode">Which download this is.</param>
        /// <param name="stage">Which delegate raised it, when the caller knows.</param>
        public static ConfigurationResponse Decide(
            RaisedConfiguration configuration,
            DownloadMode mode,
            DelegateStage stage = DelegateStage.Unknown)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            var verdict = ConfigurationClassifier.Classify(configuration);

            if (mode == DownloadMode.Unstated)
            {
                return ConfigurationResponse.Throw(
                    verdict,
                    "The delegate does not know which download it is serving. Every allowance rests on " +
                    "'the option you chose already entails this', and nobody has said which option that " +
                    "is — so nothing may be answered.");
            }

            if (verdict.Class != ConfigurationClass.RefusedOnlyBecauseNonDisruptive)
            {
                return ConfigurationResponse.Throw(
                    verdict,
                    "Class " + verdict.ClassLetter + " is refused in EVERY mode, disruptive included. " +
                    verdict.Reason);
            }

            // --- CLASS A. -------------------------------------------------------------------------
            var entry = FindEntry(configuration);

            if (mode == DownloadMode.Disruptive)
            {
                return ConfigurationResponse.Answer(
                    verdict.AnsweredSelection!,
                    verdict,
                    "R8's sanctioned disruptive mode: this is the boundary the stop was budgeted for, and " +
                    "the option already entails the consequence.");
            }

            // *** THE CORRECTION. A POST-DELEGATE CLASS A ENTRY IS ANSWERED EVEN IN THE WAVE LOOP. ***
            // Refusing it does not defer a stop — the stop has already happened, because StartModules is
            // raised ONLY when the download actually stopped the CPU. Throwing here would leave the CPU
            // stopped holding a complete program [M 2026-08-13] and send the ladder to fetch a
            // disruptive download it is already inside.
            if (entry != null && entry.RaisedIn == DelegateStage.PostTransfer)
            {
                return ConfigurationResponse.Answer(
                    entry.AnsweredSelection,
                    verdict,
                    "Raised in the POST delegate, which happens only because this download ALREADY stopped " +
                    "the CPU. Refusing it would not prevent a stop — it would leave the CPU stopped with a " +
                    "complete program [M 2026-08-13] and call for a disruptive download we are already " +
                    "inside. It is answered within the CURRENT download, which is the per-entry rung.");
            }

            return ConfigurationResponse.Throw(
                verdict,
                "Class A, raised in the PRE delegate on a non-disruptive wave boundary: stopping the CPU " +
                "mid-testing is forbidden, and the abort leaves the CPU RUNNING with the project unchanged " +
                "[M 2026-08-13], so nothing is spent by refusing.");
        }

        /// <summary>Builds the abort a caller throws from inside the real delegate.</summary>
        public static DownloadAbortedByPolicyException Abort(ConfigurationResponse response, DelegateStage stage)
        {
            if (response == null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            if (response.ShouldAnswer)
            {
                throw new InvalidOperationException(
                    "This response says ANSWER, not refuse. Building an abort from it would throw away a " +
                    "selection the policy decided to apply — and for a POST-delegate entry that is the " +
                    "difference between a running CPU and a stopped one.");
            }

            return new DownloadAbortedByPolicyException(
                "Download configuration '" + response.Verdict.Configuration.Name + "' was refused by policy. " +
                response.Reason + " Aftermath: " + AbortAftermathTable.EvidenceFor(stage),
                response.Verdict.Configuration,
                stage);
        }

        private static ClassAEntry? FindEntry(RaisedConfiguration configuration)
        {
            foreach (var entry in ConfigurationClassifier.AllowanceList)
            {
                if (ClassAEntry.NameComparer.Equals(entry.ConfigurationName, configuration.Name))
                {
                    return entry;
                }
            }

            return null;
        }
    }
}
