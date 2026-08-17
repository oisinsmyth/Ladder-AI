using Harness.Map;
using Harness.Wire;

namespace Harness.Results;

/// <summary>How one assertion came out. <c>NotObserved</c> is not a pass and is not a failure.</summary>
public enum AssertionState
{
    /// <summary>Observed, and it agreed with the expectation.</summary>
    Held,

    /// <summary>Observed, and it disagreed. This is the only state that says anything about the block.</summary>
    Disagreed,

    /// <summary>
    /// The signal was never read — outside the slot, or the run ended before it could be. <b>An unread
    /// register is not a passing one</b>, and modelling this as its own state is what stops it being
    /// counted with the ones that held.
    /// </summary>
    NotObserved,
}

/// <summary>Observed versus expected, per assertion — never a pass/fail for the test (DB-8).</summary>
public sealed record AssertionOutcome(string AssertionId, string Signal, string Expected, string Observed, AssertionState State)
{
    public static AssertionOutcome Compare(string assertionId, string signal, string expected, string? observed) =>
        observed is null
            ? new AssertionOutcome(assertionId, signal, expected, "<never read>", AssertionState.NotObserved)
            : new AssertionOutcome(assertionId, signal, expected, observed,
                string.Equals(expected, observed, StringComparison.Ordinal) ? AssertionState.Held : AssertionState.Disagreed);
}

/// <summary>
/// What makes this result trustworthy, and for how long (DB-2).
/// </summary>
/// <param name="ProgramVersion">The build stamp the copy layer published — what was RUNNING.</param>
/// <param name="MapHash">The map this client addressed. A mismatch means every address it held was a guess.</param>
/// <param name="Caveats">
/// Everything this result's validity rests on that has NOT been measured. Recorded on the result rather
/// than held elsewhere, because a caveat somebody has to go and look up is a caveat nobody reads.
/// </param>
public sealed record ValidityStamp(uint ProgramVersion, string MapHash, IReadOnlyList<string> Caveats);

/// <summary>The verdict vocabulary, and four of the six are not statements about the block.</summary>
public enum ResultVerdict
{
    /// <summary>Every assertion in this vector was observed as expected. <b>Not "the block is correct".</b></summary>
    Pass,

    /// <summary>An assertion was observed and disagreed. <b>Fix the block against the SPECIFICATION, not against the vector.</b></summary>
    Fail,

    /// <summary>The condition never occurred within the declared duration. <b>Not "the wrong thing happened".</b></summary>
    TimedOut,

    /// <summary>The value never met its settling condition, so nothing was legitimately read at all.</summary>
    Unsettled,

    /// <summary>The experiment never ran. <b>Says nothing whatsoever about the block.</b></summary>
    Stale,

    /// <summary>The vector was inadmissible. <b>Not a defect in the block.</b></summary>
    Refused,
}

/// <summary>
/// DB-8's result package — <b>what a wave hands back to the agent that wrote the block.</b>
///
/// <para><b>The one property everything here serves:</b> <i>a result must be unable to look conclusive
/// when it is not.</i> Phase 5's exit criterion is that "the result package tells the authoring agent
/// something it could act on", and a bare pass/fail cannot: an agent told only FAIL when the test never
/// started will go and edit correct logic.</para>
///
/// <para><b>So every not-conclusive state is its own thing, not an absent field.</b> An absent field
/// reads as "fine" to everyone who did not write it. <c>Stale</c>, <c>TimedOut</c>, <c>Unsettled</c> and
/// <c>Refused</c> are four different situations calling for four different actions, and
/// <see cref="StimulusOutcome"/> subdivides the first of them again — never-commanded and
/// commanded-but-did-not-run are not the same fact about a wave.</para>
///
/// <para><b>The verdict is COMPUTED, in a fixed precedence, and liveness comes before content.</b> A
/// package whose stimulus is unconfirmed can never read Pass, whatever its registers say — because
/// "every register agrees" is also what a mirror no write ever reached looks like.</para>
/// </summary>
public sealed record ResultPackage(
    string VectorId,
    int SlotIndex,
    int WaveIndex,
    Basis? Basis,
    Admissibility Admissibility,
    FidelityDeclaration? Fidelity,
    StimulusReport Stimulus,
    SettlingState Settling,
    SlotOutcome RunOutcome,
    IReadOnlyList<AssertionOutcome> Assertions,

    /// <summary>
    /// DB-8's co-running slice: <b>who actually ran alongside this vector, measured (X-E)</b>.
    ///
    /// <para>🔴 <b>NULL IS "NO SLICE WAS RECORDED FOR THIS INDEX", AND IT IS NOT AN EMPTY ONE.</b> An
    /// empty list is the positive measurement <i>"this vector ran alone"</i>; null is the absence of the
    /// measurement altogether. They were the same value until 2026-08-17 — the loop reached for
    /// <c>FirstOrDefault(...).CoRunners ?? Array.Empty&lt;int&gt;()</c>, so a missing log entry rendered
    /// as a confident <i>ran alone</i>. That is the wrong direction to be wrong in: a green obtained
    /// under unrecorded co-runners would read as one obtained in isolation.</para>
    /// </summary>
    IReadOnlyList<int>? CoRunners,
    ValidityStamp Stamp,
    // AMB-19. Null means the question was not asked for this result; the STATES inside it are the
    // answers, and four of the five are not passes. See BoundsCurrencyCheck.
    VectorBoundsCurrency? BoundsCurrency = null)
{
    /// <summary>
    /// The verdict, in the one precedence that keeps each state meaning what it says.
    ///
    /// <para><b>Admissibility, then LIVENESS, then the run, then settling, then content.</b> Content is
    /// last because it is the only one a frozen mirror can satisfy.</para>
    /// </summary>
    public ResultVerdict Verdict
    {
        get
        {
            // *** AMB-19, AND IT COMES BEFORE ADMISSIBILITY ON PURPOSE. *** A vector whose declared bound
            // no longer matches the specification is not testing what it says it tests, and that is prior
            // to whether it is well-formed: there is no useful sense in which a well-formed test of the
            // wrong number is admissible. It is STALE and not REFUSED because Refused reads as "the
            // author broke a rule" — this author broke none, the number moved underneath them. And it is
            // STALE and not FAIL because Fail reads as "the block disagreed with the specification",
            // which would send somebody to edit correct logic. Only NotDeclared/NoTable fall through, and
            // they do so because the submission gate refuses them first; if one ever reaches here, the
            // gate was bypassed and Caveats carries it.
            if (BoundsCurrency is { PremiseOutOfDate: true })
                return ResultVerdict.Stale;

            if (!Admissibility.Admissible)
                return ResultVerdict.Refused;

            if (!Stimulus.Confirmed)
                return ResultVerdict.Stale;

            if (RunOutcome == SlotOutcome.NotInert)
                return ResultVerdict.Stale;

            if (RunOutcome == SlotOutcome.TimedOut)
                return ResultVerdict.TimedOut;

            if (Settling != SettlingState.Settled)
                return ResultVerdict.Unsettled;

            // Empty is not clean: a package with nothing observed cannot pass, and neither can one whose
            // assertions were all left unread.
            if (Assertions.Count == 0 || Assertions.All(a => a.State == AssertionState.NotObserved))
                return ResultVerdict.Refused;

            if (Assertions.Any(a => a.State == AssertionState.NotObserved))
                return ResultVerdict.Unsettled;

            return Assertions.Any(a => a.State == AssertionState.Disagreed)
                ? ResultVerdict.Fail
                : ResultVerdict.Pass;
        }
    }

    /// <summary>
    /// Whether this result says anything about the BLOCK. Only <c>Pass</c> and <c>Fail</c> do.
    ///
    /// <para>Exposed as its own property so that a caller aggregating results cannot reach for
    /// <c>Verdict != Fail</c> and count four different kinds of nothing as successes.</para>
    /// </summary>
    public bool ConclusiveAboutTheBlock => Verdict is ResultVerdict.Pass or ResultVerdict.Fail;

    /// <summary>What the authoring agent should do next — different for every verdict, which is the point.</summary>
    public string WhatToDoNext => Verdict switch
    {
        ResultVerdict.Pass =>
            "These assertions held, under this model, at this fidelity. That is not 'the block is correct': read the fidelity declaration and the validity stamp before generalising, and note that a green does not mean the co-running slice was benign — only that nothing detected interference.",

        ResultVerdict.Fail =>
            "An assertion was observed and disagreed. FIX THE BLOCK AGAINST THE SPECIFICATION, NOT AGAINST THE VECTOR: the basis citation names the clause and the assertion, so a disagreement is traceable to a source rather than argued from the code. If you believe the vector is wrong, the disagreement is between two readings of the clause and that is what the citation exists to surface.",

        ResultVerdict.TimedOut =>
            "The condition never occurred within the declared duration. This is NOT 'the wrong thing happened' — nothing was observed to be wrong. Check the declared duration first, then whether the stimulus reaches the condition at all.",

        ResultVerdict.Unsettled =>
            "The value never met its settling condition, or an assertion was never read. NOTHING WAS LEGITIMATELY READ — this is not 'the value was wrong'. A completion flag is not a settling signal; declare what makes the value final.",

        // The two roads to Stale need two different next actions, so they are not collapsed into one
        // sentence. A frozen mirror is a RIG problem; an out-of-date bound is a VECTOR problem, and
        // telling somebody the experiment never ran when it was the premise that expired would send them
        // to the wrong place entirely.
        ResultVerdict.Stale when BoundsCurrency is { PremiseOutOfDate: true } =>
            "THE VECTOR'S PREMISE IS OUT OF DATE — IT WAS WRITTEN AGAINST A BOUND THE SPECIFICATION NO LONGER STATES (AMB-19). "
            + "*** THIS IS NOT A DEFECT IN THE BLOCK AND MUST NOT BE READ AS ONE: DO NOT EDIT THE BLOCK. *** No assertion ID moved, because no hashed assertion text contains a number — "
            + "which is exactly why nothing else here could have caught it. Re-read the vector against the current bounds table, then re-submit. "
            + BoundsCurrency.Detail,

        ResultVerdict.Stale =>
            "THE EXPERIMENT NEVER RAN. This result says nothing whatsoever about the block, and no part of it may be read as evidence. " + Stimulus.Detail,

        ResultVerdict.Refused =>
            "The vector was inadmissible, which is not a defect in the block: " + string.Join(" | ", Admissibility.Refusals.Select(r => $"{r.Reason}: {r.Detail}")),

        _ => throw new InvalidOperationException("unknown verdict."),
    };

    /// <summary>A one-line summary that names the verdict FIRST, so it cannot be skimmed past.</summary>
    public string Summary() =>
        $"{Verdict.ToString().ToUpperInvariant()} — vector {VectorId}, slot {SlotIndex} index {WaveIndex}: "
        + $"{Assertions.Count(a => a.State == AssertionState.Held)}/{Assertions.Count} assertion(s) held, "
        + $"stimulus {Stimulus.Outcome}, settling {Settling}"
        + CoRunningSummary
        + (Stamp.Caveats.Count > 0 ? $" [{Stamp.Caveats.Count} caveat(s)]" : string.Empty);

    /// <summary>
    /// The co-running slice in one clause, keeping the three cases apart: measured-alone, measured with
    /// names, and <b>not measured at all</b>.
    /// </summary>
    public string CoRunningSummary =>
        CoRunners is null ? ", CO-RUNNING NOT RECORDED for this index"
        : CoRunners.Count > 0 ? $", ran alongside slot(s) {string.Join(", ", CoRunners)}"
        : ", ran alone";
}

/// <summary>Whether the observed value was final when it was read.</summary>
public enum SettlingState
{
    /// <summary>The declared settling condition was met before the observation.</summary>
    Settled,

    /// <summary>It was not, so what was read may be mid-flight — the confidently-wrong case.</summary>
    NotSettled,

    /// <summary>Nothing established it either way. Not the same as settled, and never treated as it.</summary>
    NotEstablished,
}
