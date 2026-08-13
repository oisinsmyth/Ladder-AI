using System;

namespace Ladder.Wave
{
    /// <summary>
    /// Whether the run loop's stop-on-failed-wave-set gate is established for the run loop actually in
    /// use. *** THE ZERO VALUE IS "NOT DECLARED", AND IT REFUSES. ***
    /// </summary>
    public enum RunLoopGateState
    {
        /// <summary>
        /// *** NOBODY DECLARED IT. *** The zero value, so a dropped or defaulted gate never permits
        /// X-I reading (b). A capability that depends on a component nobody has built is a capability
        /// that does not exist yet.
        /// </summary>
        NotDeclared = 0,

        /// <summary>
        /// A declaration exists but was made for a DIFFERENT run loop than the one in use. Kept apart
        /// from <see cref="NotDeclared"/> deliberately: one calls for making the declaration, the other
        /// for re-establishing it against the loop that will actually run — and a stale declaration is
        /// the more dangerous of the two, because somebody did the work once and it reads as done.
        /// </summary>
        DeclaredForADifferentRunLoop = 1,

        /// <summary>The gate is declared for the run loop in use.</summary>
        Established = 2,
    }

    /// <summary>Facts about gate states, pinned by a test over the whole enum.</summary>
    public static class RunLoopGateStates
    {
        /// <summary>TRUE only for <see cref="RunLoopGateState.Established"/>.</summary>
        public static bool Establishes(RunLoopGateState state) => state == RunLoopGateState.Established;

        /// <summary>TRUE for every state that refuses X-I reading (b).</summary>
        public static bool Refuses(RunLoopGateState state) => !Establishes(state);
    }

    /// <summary>
    /// A declaration that the run loop STOPS rather than continuing to the next wave set after one
    /// fails — the gate X-I reading (b) depends on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// *** WHY THIS EXISTS, AND IT IS THE OWNER'S RULING OF 2026-08-13. *** X-I rule 2 supports two
    /// readings: (a) a passing result must already exist before a consumer is admitted, and (b) a model
    /// and its consumer may be submitted together with the ordering enforcing precedence. **(b) IS
    /// PERMITTED, BUT ONLY ONCE THE STOP-ON-FAILED-WAVE-SET GATE DEMONSTRABLY EXISTS. UNTIL THEN (b)
    /// REFUSES.**
    /// </para>
    /// <list type="bullet">
    ///   <item><description>
    ///     (a) alone is sound but costs a round trip per model, and a model submitted with its consumer
    ///     is the natural authoring flow — a rule that forbids it permanently gets worked around rather
    ///     than followed.
    ///   </description></item>
    ///   <item><description>
    ///     *** (b) WITHOUT THE GATE IS UNSOUND IN THE WORST WAY: *** the consumer's results would be
    ///     produced and BELIEVED after its model failed. Not a missing check — a WRONG ANSWER THAT
    ///     LOOKS LIKE A RESULT.
    ///   </description></item>
    ///   <item><description>
    ///     So the gate is not an optimisation, it is what makes (b) mean anything, and (b) fails closed
    ///     until it is real.
    ///   </description></item>
    /// </list>
    /// <para>
    /// *** WHOSE REQUIREMENT IT IS, NAMED: THE RUN LOOP'S, NOT ADMISSION'S. *** Admission can only
    /// refuse to admit; it cannot stop a run already under way. Nothing here builds the gate, and
    /// nothing here can observe it.
    /// </para>
    /// <para>
    /// *** THE ESTABLISHMENT IS A STAMP, NOT A BOOLEAN — THE FOURTH TIME THAT DISTINCTION HAS MATTERED
    /// IN THIS COMPONENT. *** A bare "yes, the loop stops" is a claim with nothing to compare it
    /// against. A declaration carries THE RUN LOOP VERSION IT WAS ESTABLISHED FOR, and is checked
    /// against the version actually in use — so "nobody declared it" and "declared for a run loop that
    /// is no longer the one running" come out as two facts rather than one missing tick, exactly as
    /// <see cref="ModelTestResult"/>'s hash does for a model.
    /// </para>
    /// <para>
    /// 🔴 *** AND THE HONEST LIMIT, STATED RATHER THAN IMPLIED: THE POSITIVE CASE IS ONLY AS GOOD AS
    /// WHOEVER SUPPLIES THE DECLARATION. *** This component cannot execute the run loop, cannot observe
    /// it stopping, and has no independent evidence that the behaviour exists. What it CAN do is make
    /// the absence a refusal, require the claim to name a version, an author and its evidence, and
    /// detect the version drifting. It cannot make a false declaration true. A declaration is a
    /// transferred responsibility, not a verification.
    /// </para>
    /// </remarks>
    public sealed class StopOnFailedWaveSetGate
    {
        /// <summary>
        /// The floor on <see cref="Evidence"/>. Its job is to stop "yes", "ok" and "done" satisfying a
        /// field whose whole purpose is to make somebody say HOW they know — the same lint-shaped
        /// argument <see cref="ClassAEntry.MinimumEntailmentLength"/> makes.
        /// </summary>
        public const int MinimumEvidenceLength = 24;

        private StopOnFailedWaveSetGate(string runLoopVersion, string establishedBy, string evidence)
        {
            RunLoopVersion = runLoopVersion;
            EstablishedBy = establishedBy;
            Evidence = evidence;
        }

        /// <summary>Which run loop the gate was established for.</summary>
        public string RunLoopVersion { get; }

        /// <summary>Who established it. A declaration nobody made is not one.</summary>
        public string EstablishedBy { get; }

        /// <summary>How they know — a test, a measured run, a code path.</summary>
        public string Evidence { get; }

        /// <summary>
        /// Declare the gate for one run loop version.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Any of the three is missing, or the evidence is too short to be evidence. This exception IS
        /// the guard; do not soften it. The claim being made is that a component OUTSIDE this one
        /// behaves a particular way, and it is the only thing standing between reading (b) and a wrong
        /// answer that looks like a result.
        /// </exception>
        public static StopOnFailedWaveSetGate DeclaredFor(string? runLoopVersion, string? establishedBy, string? evidence)
        {
            var version = Required(runLoopVersion, nameof(runLoopVersion),
                "The declaration must name WHICH run loop it was established for, or it cannot be " +
                "checked against the one actually running.");

            var author = Required(establishedBy, nameof(establishedBy),
                "The declaration must say who made it. A claim about another component's behaviour with " +
                "nobody behind it is not a claim.");

            var how = (evidence ?? string.Empty).Trim();
            if (how.Length < MinimumEvidenceLength)
            {
                throw new ArgumentException(
                    "The declaration must say HOW the gate is known to exist, in at least " +
                    MinimumEvidenceLength + " characters. This is the only thing standing between X-I " +
                    "reading (b) and a consumer's results being produced and believed after its model " +
                    "failed — a wrong answer that looks like a result.",
                    nameof(evidence));
            }

            return new StopOnFailedWaveSetGate(version, author, how);
        }

        /// <summary>
        /// Compare the declaration against the run loop actually in use. *** THIS IS THE COMPARISON
        /// THAT MAKES IT A FACT RATHER THAN AN ASSERTION. ***
        /// </summary>
        /// <param name="runLoopVersionInUse">The run loop that will execute this plan.</param>
        public RunLoopGateState CheckAgainst(string? runLoopVersionInUse)
        {
            var inUse = (runLoopVersionInUse ?? string.Empty).Trim();

            if (inUse.Length == 0)
            {
                // Nobody said which loop will run, so the declaration cannot be shown to be about it.
                // Treated as undeclared rather than as a match: an unchecked stamp is not a stamp.
                return RunLoopGateState.NotDeclared;
            }

            return string.Equals(RunLoopVersion, inUse, StringComparison.OrdinalIgnoreCase)
                ? RunLoopGateState.Established
                : RunLoopGateState.DeclaredForADifferentRunLoop;
        }

        /// <inheritdoc />
        public override string ToString() =>
            "stop-on-failed-wave-set gate for run loop '" + RunLoopVersion + "', established by " +
            EstablishedBy + " (" + Evidence + ")";

        private static string Required(string? value, string parameterName, string why)
        {
            var trimmed = (value ?? string.Empty).Trim();
            if (trimmed.Length == 0)
            {
                throw new ArgumentException(why, parameterName);
            }

            return trimmed;
        }
    }
}
