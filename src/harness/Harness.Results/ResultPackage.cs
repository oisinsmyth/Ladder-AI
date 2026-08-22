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

    /// <summary>
    /// 🔴 <b>OBSERVED REPEATEDLY, AND THE OBSERVATIONS DID NOT AGREE WITH EACH OTHER. Neither a pass, nor a
    /// failure, nor "nobody looked".</b>
    ///
    /// <para><b>It exists because the alternative was measured and it was a false accusation.</b> Until
    /// 2026-08-17 the harness kept exactly one observation per index — the poll that recognised completion
    /// — and a well-built stimulus model returns the block to inert BEFORE it raises its completion flag.
    /// So the single instant the harness looked at was, by construction, the one instant at which every
    /// commanded member is inert: an assertion expecting a commanded state to be TRUE read false and the package
    /// reported <c>Fail</c> against a block measured doing the right thing on the device.</para>
    ///
    /// <para><b>With the whole series retained, that case is no longer a disagreement — it is a signal that
    /// took both values while nothing declared which instant discharges the assertion.</b> Saying so is the
    /// honest answer, and it names two cheap repairs rather than sending someone to edit correct logic.
    /// <see cref="Disagreed"/> stays exactly as strong as it was: it now means the expected value was not
    /// present at ANY instant the harness looked inside the window.</para>
    /// </summary>
    Inconclusive,
}

/// <summary>Observed versus expected, per assertion — never a pass/fail for the test (DB-8).</summary>
public sealed record AssertionOutcome(string AssertionId, string Signal, string Expected, string Observed, AssertionState State)
{
    /// <summary>
    /// 🔴 <b>WHEN this was observed, out of how many observations, and whether the instant was inside the
    /// window the binding declared for it. Null means the outcome came from a path that does not record it.</b>
    ///
    /// <para><b>Nothing asked the "when" question before 2026-08-17, and the measured cost was a confident
    /// FAIL taken 25.2 seconds after the declared window closed</b>, with the model's own arm flag reading
    /// 0 in the very same register read. See <see cref="ObservationWindow"/>.</para>
    /// </summary>
    public ObservationWindow? Window { get; init; }

    /// <summary>
    /// Why this outcome is what it is, in a sentence a reader can act on. <b>Carried on the outcome rather
    /// than composed at the report</b>, because the reasons differ per assertion and a package-level
    /// sentence cannot name which signal it is about.
    /// </summary>
    public string? Detail { get; init; }

    /// <summary>
    /// Whether this row says anything about the BLOCK. <b>Only <see cref="AssertionState.Held"/> and
    /// <see cref="AssertionState.Disagreed"/> do</b> — an unread register and a self-contradicting series
    /// are both silence, of two different kinds.
    /// </summary>
    public bool SaysSomethingAboutTheBlock => State is AssertionState.Held or AssertionState.Disagreed;

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

    /// <summary>
    /// 🔴 <b>NOBODY LOOKED.</b> The experiment ran, but not one declared assertion was read — so this
    /// package is silent about the block for a reason that has nothing to do with the block, the timing,
    /// or the vector's admissibility.
    ///
    /// <para><b>It exists because the three verdicts it was previously spelled as each send a reader
    /// somewhere useless.</b> <see cref="TimedOut"/> says <i>the condition never occurred</i> — a claim
    /// about the plant, which points at durations and stimulus. <see cref="Unsettled"/> says <i>the value
    /// never became final</i> — a claim about settling. <see cref="Refused"/> says <i>the author broke a
    /// rule</i>. <b>All three describe an observation that was made; this one is the case where no
    /// observation was made at all</b>, and it points at the INSTRUMENT.</para>
    ///
    /// <para><b>MEASURED, 2026-08-17.</b> JOB9004's first live wave ran both vectors against the rig, polled
    /// 7,912 times, read the whole result band every poll, and returned <c>&lt;never read&gt;</c> for all
    /// four declared assertions. It rendered as <c>TimedOut</c> and <c>Unsettled</c>. The per-assertion
    /// rows were already honest (<c>saysSomethingAboutTheBlock: false</c>); <b>only the headline was
    /// not</b>, and the headline is what gets read.</para>
    ///
    /// <para><b>It sits BELOW liveness in the precedence, not above it.</b> A run whose stimulus was never
    /// confirmed, or whose inert phase never established, also observes nothing — and there
    /// <see cref="Stale"/> is the more informative answer, because it names WHY. This verdict is
    /// specifically <i>the experiment ran and the instrument did not read it.</i></para>
    /// </summary>
    NotObserved,

    /// <summary>
    /// 🔴 <b>SOMEBODY LOOKED, REPEATEDLY, AND THE OBSERVATIONS DISAGREED WITH EACH OTHER.</b>
    ///
    /// <para>Distinct from all four of its neighbours, and each of them would send a reader somewhere
    /// useless. <see cref="NotObserved"/> says <i>nobody looked</i> — here the harness looked hundreds of
    /// times. <see cref="TimedOut"/> says <i>the condition never occurred</i> — here it occurred, and also
    /// did not, at different instants. <see cref="Unsettled"/> says <i>the value never became final</i> —
    /// an inert tail is perfectly final, which is why settling cannot catch this. And
    /// <see cref="Fail"/> is the answer this used to give: <b>a confident disagreement produced by looking
    /// only at the one instant a well-built model guarantees is inert.</b></para>
    ///
    /// <para><b>It points at the INSTRUMENT DECLARATION, not the block and not the plant.</b> The repair is
    /// in the binding or the expectation's mode, and it is named on <see cref="WhatToDoNext"/>.</para>
    /// </summary>
    Inconclusive,

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

    /// <summary>
    /// The settling verdict <b>and the registers it was taken over</b> — see <see cref="SettlingReport"/>.
    /// It was a bare <see cref="SettlingState"/> until 2026-08-20, so an UNSETTLED result named nothing at
    /// all and the reader had no way to tell a moving signal from a check that could not run.
    /// </summary>
    SettlingReport Settling,
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
    // answers, and five of the seven are not passes. See BoundsCurrencyCheck.
    VectorBoundsCurrency? BoundsCurrency = null,

    /// <summary>
    /// 🔴 <b>THE OBSERVABILITY FLOOR THIS RESULT'S EXPECTATIONS WERE JUDGED AGAINST, IN SCANS — and it was
    /// computed with the wrong number until 2026-08-18.</b>
    ///
    /// <para>The submission gate derived the floor from the MAP (<c>reads x RTT_p99 / scan</c>); the code
    /// that builds this package passed a hardcoded one read per cycle. On any wave set needing more than
    /// one read per poll cycle the package's floor was too small by that factor, <b>in the permissive
    /// direction</b> — a window the gate would refuse rendered <c>Supportable</c> here. A one-read wave
    /// set is unaffected, which is why it went unseen.</para>
    ///
    /// <para><b>It is on the package because a verdict that cannot say what it was measured against cannot
    /// be argued with</b>, and because the wrong number was being printed with no way for a reader to
    /// notice. Null means no observability report was supplied for this vector — which is a caveat, not a
    /// pass.</para>
    /// </summary>
    double? ObservabilityFloorScans = null)
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

            // 🔴 The link went away with this vector in flight. Stale for the same reason NotInert is —
            // "the experiment never ran, and this says nothing whatsoever about the block" — and above
            // NotObserved because it NAMES WHY nothing was read, which NotObserved cannot.
            //
            // *** Emphatically NOT TimedOut. *** A timeout is a claim about the plant: that a condition
            // did not occur inside the backstop. A dropped socket makes no such claim, and filing it as
            // one would send a reader to the block, the scenario clock and the compression factor, none
            // of which is implicated.
            if (RunOutcome == SlotOutcome.LinkLost)
                return ResultVerdict.Stale;

            // 🔴 *** NOBODY LOOKED COMES BEFORE THE CONDITION NEVER OCCURRED, AND THAT ORDER IS THE FIX.
            // *** This test used to sit three gates lower, so a run that read NOTHING was reported as
            // TimedOut whenever the completion register did not reach its value, and as Unsettled whenever
            // it did — two claims about the PLANT made by a package that had not observed the plant. And
            // when it was reached it answered `Refused`, whose text blames the vector author for a defect
            // in the harness. Measured on JOB9004's first live wave: four assertions, all `<never read>`, headline
            // TIMEDOUT and UNSETTLED.
            //
            // *** IT IS BELOW LIVENESS AND ADMISSIBILITY, DELIBERATELY. *** An experiment that never ran
            // also observes nothing, and `Stale` says WHY where this would only say that. Every earlier
            // branch names a cause; this one is what remains when the run happened and the reading did not.
            //
            // *** AND IT DOES NOT FIRE ON A PARTIAL READ. *** One assertion read and one not is still a
            // package that observed something, and it keeps falling through to Unsettled below — a gate
            // that fires outside its scope is noise, and noise gets switched off.
            if (Assertions.Count == 0 || Assertions.All(a => a.State == AssertionState.NotObserved))
                return ResultVerdict.NotObserved;

            if (RunOutcome == SlotOutcome.TimedOut)
                return ResultVerdict.TimedOut;

            // 🔴 *** ABOVE SETTLING, DELIBERATELY, AND THE ORDER IS THE WHOLE VALUE OF THE VERDICT. *** An
            // inert tail is PERFECTLY SETTLED — the measured package that reported FAIL against a correct
            // block had `Settled` — so settling can neither detect this nor be a more informative answer
            // for it. Below settling, this verdict would be masked in exactly the cases where the
            // observation instant is the thing in doubt, and the reader would be sent to declare a settling
            // condition that was never the problem.
            if (Assertions.Any(a => a.State == AssertionState.Inconclusive))
                return ResultVerdict.Inconclusive;

            if (!Settling.IsSettled)
                return ResultVerdict.Unsettled;

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

        // *** THE SETTLING DETAIL IS APPENDED, AND IT IS THE HALF THAT NAMES A REPAIR. *** The sentence
        // above is true of every road to Unsettled and actionable on none of them: it does not say whether
        // a declared signal MOVED (and which, and from what to what) or whether the check could not run at
        // all. Attributing that on the live wave of 2026-08-20 took a forensic pass, because the package
        // carried a bare enum.
        ResultVerdict.Unsettled =>
            "The value never met its settling condition, or an assertion was never read. NOTHING WAS LEGITIMATELY READ — this is not 'the value was wrong'. A completion flag is not a settling signal; declare what makes the value final. "
            + $"SETTLING: {Settling.Detail}",

        ResultVerdict.NotObserved when Assertions.Count == 0 =>
            "NOBODY LOOKED: this vector declares NO assertions at all, so the run could not have said anything about the block whatever it did. "
            + "*** THIS IS NOT A TIMEOUT AND NOT A SETTLING PROBLEM — do not go and check durations. *** Declare at least one expectation.",

        ResultVerdict.Inconclusive =>
            "SOMEBODY LOOKED, REPEATEDLY, AND THE OBSERVATIONS DISAGREED WITH EACH OTHER. "
            + "*** THIS IS NOT A FAILURE AND MUST NOT BE ACTIONED AGAINST THE BLOCK — DO NOT EDIT THE BLOCK. *** "
            + "The signal(s) below took the expected value at some observed instants and a different value at others, and nothing in "
            + "the submission or the binding declares which instant discharges the assertion. It is NOT a timeout (the condition did "
            + "occur), NOT unsettled (an inert tail is perfectly settled, which is why settling cannot catch this) and NOT 'nobody "
            + "looked'. GO TO THE INSTRUMENT DECLARATION: either declare `armedBy` on the signal in the binding, so the observation "
            + "window is published in-band and out-of-window frames stop counting, or declare the expectation `Latched`, so an "
            + "occurrence inside the window survives a model that returns the block to inert before it signals completion. "
            + string.Join(" | ", Assertions
                .Where(a => a.State == AssertionState.Inconclusive)
                .Select(a => $"'{a.Signal}': {a.Detail}")),

        ResultVerdict.NotObserved =>
            $"NOBODY LOOKED: the experiment RAN, and not one of the {Assertions.Count} declared assertion(s) was read — every one came back '<never read>'. "
            + "*** THIS SAYS NOTHING ABOUT THE BLOCK AND NOTHING ABOUT THE PLANT. *** It is not a TIMEOUT (the condition may well have occurred, "
            + "nobody was watching) and not UNSETTLED (nothing got as far as needing to settle). GO TO THE INSTRUMENT, NOT THE DURATIONS: check "
            + "that each signal named in `expectations` is a name the binding carries — the binding states the specification's name as `specName` "
            + "beside the block's own tag, and a citation matching neither resolves to no register at all. Unread signal(s): "
            + string.Join(", ", Assertions.Where(a => a.State == AssertionState.NotObserved).Select(a => $"'{a.Signal}'").Distinct(StringComparer.Ordinal))
            // *** AND THE ROWS THAT KNOW WHY, SAY WHY. *** An unjoined name, an absent latch and a window
            // that never opened are three different repairs in three different documents, and the generic
            // sentence above names only the first. A pointer that documents one case and steers the reader
            // away from the real gap is worse than no pointer, because it satisfies.
            + string.Concat(Assertions
                .Where(a => a.State == AssertionState.NotObserved && a.Detail is not null)
                .Select(a => $" | '{a.Signal}': {a.Detail}")),

        // The two roads to Stale need two different next actions, so they are not collapsed into one
        // sentence. A frozen mirror is a RIG problem; an out-of-date bound is a VECTOR problem, and
        // telling somebody the experiment never ran when it was the premise that expired would send them
        // to the wrong place entirely.
        // The headline is taken from the FINDING and not written here, because the three roads to a
        // premise refusal need three different repairs: "written against a bound the specification no
        // longer states" is simply false of a vector that declared no bound at all, and a headline that
        // misnames the repair sends the reader to the wrong document with full confidence.
        ResultVerdict.Stale when BoundsCurrency is { PremiseOutOfDate: true } =>
            BoundsCurrency.PremiseHeadline + " (AMB-19). "
            + "*** THIS IS NOT A DEFECT IN THE BLOCK AND MUST NOT BE READ AS ONE: DO NOT EDIT THE BLOCK. *** No assertion ID moved, because no hashed assertion text contains a number — "
            + "which is exactly why nothing else here could have caught it. "
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
        + $"stimulus {Stimulus.Outcome}, settling {Settling.State}"
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

/// <summary>
/// 🔴 <b>THE SETTLING VERDICT <i>AND WHAT IT WAS TAKEN OVER</i> — because a bare enum could not say which
/// register moved, and attributing one cost a forensic pass over a whole live wave.</b>
///
/// <para><b>The shape is <c>InertReport</c>'s, deliberately.</b> That check collects every disagreeing
/// register into a set and NAMES them — <c>"slot 0 R003 moved 5 -> 7"</c> — while settling returned a bare
/// boolean, so a run in which sixteen of sixteen assertions held and every vector came back UNSETTLED said
/// nothing at all about WHY. The two checks compare registers across a scan gap for the same reason and
/// there was never a case for one of them reporting and the other not.</para>
///
/// <para><b>The detail is present on the PASS as well as the failure</b>, and it carries the denominator:
/// which signals were examined, at which registers, over how many scans. A settling check that examined
/// nothing is the shape this project has been bitten by repeatedly, and <see cref="SettlingState.Settled"/>
/// over zero registers must not be able to look like <see cref="SettlingState.Settled"/> over three.</para>
/// </summary>
/// <param name="State">The verdict. Never <see cref="SettlingState.Settled"/> unless registers were actually compared.</param>
/// <param name="Detail">What was compared, or what stopped it being compared. Never blank.</param>
public sealed record SettlingReport(SettlingState State, string Detail)
{
    /// <summary>Nothing established it either way — with the reason, which is the half a bare enum lost.</summary>
    public static SettlingReport NotEstablished(string detail) => new(SettlingState.NotEstablished, detail);

    /// <summary>The declared signals held still across the declared scans. <paramref name="detail"/> carries the denominator.</summary>
    public static SettlingReport Settled(string detail) => new(SettlingState.Settled, detail);

    /// <summary>A declared signal moved. <paramref name="detail"/> names which register, and from what to what.</summary>
    public static SettlingReport NotSettled(string detail) => new(SettlingState.NotSettled, detail);

    /// <summary>True only for <see cref="SettlingState.Settled"/>. Exposed so no caller reaches for <c>!= NotSettled</c>.</summary>
    public bool IsSettled => State == SettlingState.Settled;

    public override string ToString() => $"{State}: {Detail}";
}
