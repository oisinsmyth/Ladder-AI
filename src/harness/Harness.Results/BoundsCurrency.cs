namespace Harness.Results;

/// <summary>
/// Whether a vector is still testing the number the specification currently states — <b>AMB-19</b>.
///
/// <para><b>THE CHANNEL THIS CLOSES EXISTS ONLY IN COMBINATION, AND EVERY RULING THAT BUILT IT IS
/// INDIVIDUALLY CORRECT.</b> The assertion-ID scheme deliberately puts no numeric bound in any hashed
/// sentence: that is what has made five re-issues of the hopper enumeration cost zero re-hashes when an
/// output was renamed, and it is the same property that lets a clause reference a bounds TABLE rather
/// than a value. The enumeration then says, equally correctly, that <i>retuning the table re-hashes
/// nothing</i>. Put the two together and <b>a retune changes the truth conditions of every assertion
/// that refers to the table while moving ZERO assertion IDs</b> — on the hopper enumeration, 22 of
/// them.</para>
///
/// <para>*** SO A VECTOR WRITTEN AGAINST T#60S GOES ON PASSING AFTER THE BOUND BECOMES T#90S. *** No
/// citation dangles, because no ID moved. No <c>Stale</c> verdict fires, because staleness keys on
/// assertion IDs. The vector tests the wrong number and every mechanical check agrees it is fine — which
/// is the precise shape of failure this whole scheme exists to make impossible, arriving through the
/// one axis the hash was designed not to cover.</para>
///
/// <para><b>The fix is outside the register and costs one comparison:</b> a vector states which bound it
/// was written against, and that statement is compared against the table at submission time.</para>
///
/// <para>🔴 <b>A MISMATCH IS <see cref="ResultVerdict.Stale"/>, NEVER <see cref="ResultVerdict.Fail"/>,
/// AND THE DISTINCTION IS THE WHOLE POINT.</b> A Fail says the block disagreed with the specification.
/// Here the block may be perfectly correct and the vector simply predates a retune nobody told it about
/// — blaming the block for that is the identical defect to the missing-predicate case, where a vector's
/// own omission was reported as the block being wrong and an agent would have gone and edited correct
/// logic. The verdict must be reachable only by the vector's premise being out of date, and must never
/// be confusable with a real disagreement.</para>
/// </summary>
public enum BoundsCurrencyState
{
    /// <summary>
    /// <b>The enumeration supplied no bounds table AND NOBODY ACCOUNTED FOR THAT</b>, so there was
    /// nothing to compare against. NOT a pass: the comparison did not happen.
    ///
    /// <para>🔴 <b>SINCE FI-99 THIS IS SPECIFICALLY THE UNSIGNED EMPTINESS.</b> An empty table with a
    /// well-formed <c>boundsAbsence</c> claim behind it is <see cref="NoBoundsTableClaimed"/> instead —
    /// and a claim missing its author or its reason lands back here, because a claim with neither is not
    /// a claim.</para>
    /// </summary>
    NoTable,

    /// <summary>
    /// <b>NOBODY SAID ANYTHING ABOUT BOUNDS</b> — the field is absent (<c>null</c>). NOT a pass, and this
    /// is the state the whole check turns on. A vector that never says what number it was written against
    /// cannot be found stale by anything, which is exactly the silent survival AMB-19 describes. It is
    /// UNCHECKED, and unchecked fails closed.
    ///
    /// <para>🔴 <b>THIS IS NOT THE SAME CLAIM AS <c>boundsUsed: {}</c>, AND UNTIL 2026-08-17 IT WAS THE
    /// SAME STATE.</b> Silence and an empty map both landed here, so a vector citing an assertion that
    /// genuinely has no bound — a gating condition, an ordering, a state that mentions no time and no
    /// threshold — was refused, and <b>the only way to clear the refusal was to invent a bound</b>, which
    /// is precisely the fabrication this gate exists to prevent. <i>A gate that can only be satisfied by
    /// making something up is inverted.</i> The positive claim now has its own states below.</para>
    /// </summary>
    NotDeclared,

    /// <summary>Every bound the vector declared matches the table. The only state that is a pass.</summary>
    Current,

    /// <summary>
    /// A declared bound differs from the table's current value. <b>The vector was written against a
    /// number the specification no longer states.</b>
    /// </summary>
    Stale,

    /// <summary>
    /// A declared bound names nothing in the table. <b>Not a disagreement about a value — a disagreement
    /// about which bounds exist</b>, which is a different repair and is reported as its own thing rather
    /// than folded into Stale. Fails closed, and like Stale it says nothing about the block.
    /// </summary>
    Unknown,

    // -------------------------------------------------------------------------------------------------
    // THE POSITIVE CLAIM — `boundsUsed: {}` — AND ITS TWO WRONG ANSWERS. Added 2026-08-17.
    //
    // *** APPENDED, NEVER INSERTED. *** These belong beside NotDeclared by meaning and are at the end by
    // discipline: renumbering an enum is free right up until something has persisted an ordinal, and the
    // day it is not free nothing announces it. The grouping lives in the doc comments.
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// <b>THE VECTOR POSITIVELY CLAIMS IT WAS WRITTEN AGAINST NO BOUND, AND THE ENUMERATION CONFIRMS
    /// IT.</b> A pass, and one of three — see also <see cref="Current"/> and
    /// <see cref="NoBoundsTableClaimed"/>.
    ///
    /// <para><c>boundsUsed: {}</c> is a real and common claim: an assertion about a gating condition, an
    /// ordering, or a state that mentions no time and no threshold has no bound to cite. <b>It is
    /// VERIFIED and not accepted</b> — the enumeration must say that the cited assertion depends on no
    /// bound, and if it says otherwise the answer is <see cref="BoundsOmitted"/>. Taking the claim on
    /// trust would make this state a hole rather than a fix.</para>
    /// </summary>
    NoBoundsCited,

    /// <summary>
    /// <b>The vector claims no bound and NOTHING COULD CHECK THAT CLAIM</b> — the enumeration does not
    /// state which bounds the cited assertion depends on, or the vector cites no assertion at all. NOT a
    /// pass: a declaration is a transferred responsibility, not a verification.
    ///
    /// <para>🔴 <b>THE REPAIR IS TO THE ENUMERATION, NEVER TO THE VECTOR.</b> Adding a bound to the
    /// vector to clear this is the fabrication the whole gate exists to prevent. What is owed is the
    /// enumeration's per-assertion <c>bounds</c> relation — the third party saying which of its bounds
    /// each assertion depends on.</para>
    /// </summary>
    NoBoundsClaimUnverified,

    /// <summary>
    /// 🔴 <b>THE VECTOR CLAIMS NO BOUND AND THE ENUMERATION SAYS ITS CITED ASSERTION DEPENDS ON ONE.</b>
    /// A refusal, and a serious one: this is a vector written against a bound it did not look at.
    ///
    /// <para>It is <see cref="VectorBoundsCurrency.PremiseOutOfDate"/> — reported as Stale, never as a
    /// Fail — for the same reason every other state here is: <b>it says nothing whatsoever about the
    /// block</b>, and reporting it as a disagreement would send somebody to edit correct logic.</para>
    /// </summary>
    BoundsOmitted,

    // -------------------------------------------------------------------------------------------------
    // THE ENUMERATION'S OWN POSITIVE CLAIM — `boundsAbsence` — AND ITS TWO CONTRADICTIONS. FI-99, added
    // 2026-09-03.
    //
    // *** APPENDED, NEVER INSERTED, *** for the reason stated above the 2026-08-17 group: renumbering an
    // enum is free right up until something has persisted an ordinal, and the day it is not free nothing
    // announces it.
    //
    // The 2026-08-17 group above gave the VECTOR a way to say "no bound applies to me". This group gives
    // the ENUMERATION the same right about the whole subject, and for the identical reason: a purely
    // combinational block — an arbitration or a selection — has nothing to bound, so its enumeration's
    // `bounds:` table is empty AS AN ANSWER. Until FI-99 that was indistinguishable from nobody writing
    // one, so such a subject was permanently NOT CHECKED and the only escape was to invent a bound.
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// <b>THE ENUMERATION POSITIVELY CLAIMS THE SUBJECT HAS NO BOUNDS AT ALL, WITH AN AUTHOR AND A
    /// REASON, AND NOTHING IN THE SUBMISSION CONTRADICTS IT.</b> A CHECKED PASS — the third besides
    /// <see cref="Current"/> and <see cref="NoBoundsCited"/>.
    ///
    /// <para>🔴 <b>AND IT IS A DIFFERENT KIND OF PASS FROM THOSE TWO, WHICH IS WHY IT HAS ITS OWN STATE
    /// AND ITS OWN SENTENCE IN THE GATE.</b> <see cref="Current"/> rests on a COMPARISON and
    /// <see cref="NoBoundsCited"/> rests on a third party's per-assertion relation. This one rests on a
    /// RECORDED HUMAN CLAIM — <c>boundsAbsence: {claimed, by, because}</c> — that no comparison
    /// established and none could, because there is nothing to compare. Its whole defence is that it is
    /// <b>falsifiable</b>: an assertion that names a bound, or a vector that cites one, contradicts it,
    /// and the contradiction is a REFUSAL rather than a downgrade. A reader must be able to see that this
    /// green is somebody's signature and go and argue with them, so the finding names the claimant and
    /// quotes the reason.</para>
    /// </summary>
    NoBoundsTableClaimed,

    /// <summary>
    /// 🔴 <b>THE ENUMERATION CLAIMS THE SUBJECT HAS NO BOUNDS AND THE ENUMERATION'S OWN CONTENTS SAY
    /// OTHERWISE.</b> A refusal, and an <b>ENUMERATION</b> defect — repair the enumeration.
    ///
    /// <para>Two ways to reach it, both self-contradiction by the same document: the <c>bounds:</c> table
    /// is NOT empty while absence is claimed, or an assertion's <c>assertionBounds</c> entry names a
    /// bound. Either way the claimant said "nothing here has a bound" and their own file names one.</para>
    ///
    /// <para><b>It is NOT <see cref="VectorBoundsCurrency.PremiseOutOfDate"/>, deliberately.</b> That flag
    /// means <i>the VECTOR is wrong and the block is not accused</i>; here the vector is wrong about
    /// nothing at all, and dressing this in a Stale verdict headlined "re-read the vector" would send the
    /// reader to repair the one document that is innocent.</para>
    /// </summary>
    BoundsAbsenceContradictedByEnumeration,

    /// <summary>
    /// 🔴 <b>THE ENUMERATION CLAIMS THE SUBJECT HAS NO BOUNDS AND THIS VECTOR CITES ONE.</b> A refusal,
    /// and a <b>VECTOR</b> defect — a different repair from
    /// <see cref="BoundsAbsenceContradictedByEnumeration"/>, which is why it is a different state rather
    /// than a shared one with two message shapes.
    ///
    /// <para>The vector declared a non-empty <c>boundsUsed</c> against a subject whose bounds table is
    /// empty and claimed empty. Either the vector is citing a number nobody specified, or the absence
    /// claim is false — and the gate cannot tell which, so it names both parties and refuses.</para>
    ///
    /// <para>It IS <see cref="VectorBoundsCurrency.PremiseOutOfDate"/> — reported as Stale, never a Fail
    /// — for the reason every state here is: <b>it says nothing whatsoever about the block.</b></para>
    /// </summary>
    BoundsAbsenceContradictedByVector,
}

/// <summary>
/// 🔴 <b>THE ENUMERATION'S POSITIVE CLAIM THAT ITS SUBJECT HAS NO BOUNDS TO TABULATE — FI-99, and the
/// only thing that may open the <see cref="BoundsCurrencyState.NoTable"/> guard.</b>
///
/// <para><b>Measured 2026-09-03</b>, on a submission that was otherwise complete: 31 gates run, 0
/// refused, NOT ADMISSIBLE on a single NOT CHECKED. A third-party enumeration of a block with 8 clauses
/// and 17 assertions, every one of them carrying <c>assertion_bounds: []</c>, and prose in the file
/// stating the emptiness as a claim rather than a silence. Both vectors declared <c>boundsUsed: {}</c>,
/// agreeing with it. <b>The claim had already been made, in the right file, by the right party — and
/// there was no field to put it in</b>, so the schema dropped it and the gate read a silence.</para>
///
/// <para><b>A block that is purely combinational — an arbitration, a selection — has nothing to bound,
/// and was permanently inadmissible.</b> The only workaround was to INVENT a bound, which is the
/// fabrication the surrounding rules exist to prevent. <i>A gate whose only escape is a lie is worse
/// than the gap it guards.</i></para>
///
/// <para>🔴 <b>ALL THREE FIELDS ARE REQUIRED AND A CLAIM MISSING ONE IS NOT A CLAIM.</b> An unsigned or
/// unreasoned assertion of a negative is exactly the silence this type exists to distinguish itself from
/// — so <see cref="Of"/> does not throw and does not half-accept: it returns a <see cref="Rejected"/>
/// claim, which <see cref="IsClaimed"/> reports as false and which therefore leaves the guard shut at
/// <see cref="BoundsCurrencyState.NoTable"/>, exactly as before FI-99. The rejection REASON is carried so
/// the author is told why their claim did not count, rather than watching it vanish.</para>
///
/// <para>Naming the claimant follows the precedent already set by gate 4b (the fidelity list's declarer)
/// and gate 5c (the map's author): <b>name an authority, compare it, and report NOT CHECKED when there
/// is none.</b> An unattributed claim is indistinguishable from one written by the party it exculpates.</para>
/// </summary>
public sealed record BoundsAbsenceClaim
{
    private static readonly IReadOnlyList<string> NoAssertions = Array.Empty<string>();

    private BoundsAbsenceClaim(
        bool claimed, string by, string because, string rejectedBecause, IReadOnlyList<string> contradictingAssertions)
    {
        IsClaimed = claimed;
        By = by;
        Because = because;
        RejectedBecause = rejectedBecause;
        ContradictingAssertions = contradictingAssertions;
    }

    /// <summary>
    /// <b>Nobody claimed anything.</b> The overwhelmingly common case and the default everywhere: an
    /// enumeration written before FI-99 carries this, and against it every path behaves exactly as it did
    /// before FI-99 existed.
    /// </summary>
    public static readonly BoundsAbsenceClaim None =
        new(false, string.Empty, string.Empty, string.Empty, NoAssertions);

    /// <summary>True only for a WELL-FORMED claim — <c>claimed</c>, an author, and a reason.</summary>
    public bool IsClaimed { get; }

    /// <summary>Who claims the subject has no bounds. Non-empty exactly when <see cref="IsClaimed"/>.</summary>
    public string By { get; }

    /// <summary>Why. Quoted verbatim in the finding, because a pass resting on a claim must be arguable.</summary>
    public string Because { get; }

    /// <summary>
    /// Why an attempted claim was not counted as one. Empty for <see cref="None"/> and for a good claim.
    /// <b>Reported</b> — a rejected claim that vanished silently would be the same defect FI-99 fixes.
    /// </summary>
    public string RejectedBecause { get; }

    /// <summary>
    /// The enumeration's own assertions whose <c>assertionBounds</c> entry names a bound — <b>the
    /// falsifying evidence from the claimant's own file.</b> Empty when nothing contradicts, or when
    /// nothing was claimed.
    /// </summary>
    public IReadOnlyList<string> ContradictingAssertions { get; }

    /// <summary>True when a well-formed claim is contradicted by the enumeration that made it.</summary>
    public bool IsContradictedByEnumeration => IsClaimed && ContradictingAssertions.Count > 0;

    /// <summary>True when somebody wrote <c>claimed: true</c> and it was not counted.</summary>
    public bool WasRejected => RejectedBecause.Length > 0;

    /// <summary>
    /// A claim that does not count. <b>Not an exception</b>: a malformed claim is a real thing an author
    /// wrote and the correct handling is to say so and fall back to the closed guard, not to abort the
    /// whole submission read.
    /// </summary>
    public static BoundsAbsenceClaim Rejected(string rejectedBecause)
    {
        if (string.IsNullOrWhiteSpace(rejectedBecause))
            throw new ArgumentException("A rejected claim must say WHY it was rejected.", nameof(rejectedBecause));

        return new BoundsAbsenceClaim(false, string.Empty, string.Empty, rejectedBecause.Trim(), NoAssertions);
    }

    /// <summary>
    /// Build the claim from what the document said. <paramref name="claimed"/> false is
    /// <see cref="None"/>; true with a missing author or a missing reason is <see cref="Rejected"/>.
    /// </summary>
    /// <param name="contradictingAssertions">
    /// Assertion IDs from the SAME enumeration whose bounds relation names a bound. Supplied by the
    /// enumeration rather than computed here, because this type has no view of the assertions — see
    /// <c>AssertionEnumeration.ResolvedBoundsAbsence</c>.
    /// </param>
    public static BoundsAbsenceClaim Of(
        bool claimed, string? by, string? because, IReadOnlyList<string>? contradictingAssertions = null)
    {
        if (!claimed)
            return None;

        var hasBy = !string.IsNullOrWhiteSpace(by);
        var hasBecause = !string.IsNullOrWhiteSpace(because);

        if (!hasBy || !hasBecause)
        {
            var missing = !hasBy && !hasBecause ? "names neither WHO claims it nor WHY"
                : !hasBy ? "names no `by` — nobody claims it"
                : "gives no `because` — no reason is recorded";

            return Rejected(
                $"`boundsAbsence.claimed` is true but the claim {missing}. *** A CLAIM WITH NO AUTHOR OR NO REASON IS NOT A CLAIM, *** "
                + "and it is treated as though it were absent: an unsigned, unreasoned assertion of a negative is precisely the silence "
                + "this field exists to be distinguishable from. Supply BOTH `by` and `because`, or drop the field.");
        }

        return new BoundsAbsenceClaim(
            true, by!.Trim(), because!.Trim(), string.Empty,
            contradictingAssertions is null
                ? NoAssertions
                : contradictingAssertions.OrderBy(a => a, StringComparer.Ordinal).ToArray());
    }
}

/// <summary>
/// <b>What the ENUMERATION says the cited assertion's bounds are</b> — the outside authority that makes
/// <c>boundsUsed: {}</c> a checkable claim rather than an accepted one.
///
/// <para><b>ABSENT IS NOT EMPTY, and that distinction is the entire type.</b> An empty
/// <see cref="Bounds"/> set is the positive statement <i>"this assertion depends on no bound"</i>; a null
/// one is <i>"nobody computed that"</i>. It is the same discipline as <c>reachable-state</c>'s
/// <c>reachableState: []</c> versus an omitted key, and as <c>drift-check</c>'s <c>--complete</c>: the
/// convention already exists here and is reused rather than re-invented.</para>
///
/// <para><b>There is no public constructor</b> — only <see cref="Stated"/> and <see cref="NotStated"/> —
/// so a caller cannot produce a stated-but-null expectation, and a caller who has nothing must say why
/// they have nothing. The reason is printed in the finding, where the reader of RESULTS meets it.</para>
/// </summary>
public sealed record AssertionBoundsExpectation
{
    private AssertionBoundsExpectation(string? assertionId, IReadOnlySet<string>? bounds, string absenceReason)
    {
        AssertionId = assertionId;
        Bounds = bounds;
        AbsenceReason = absenceReason;
    }

    /// <summary>The assertion this expectation is about, or null when nothing could be looked up.</summary>
    public string? AssertionId { get; }

    /// <summary>
    /// The bound names the enumeration says this assertion depends on. <b>Empty is the positive claim
    /// "none"; null is "the enumeration does not say".</b>
    /// </summary>
    public IReadOnlySet<string>? Bounds { get; }

    /// <summary>Why there is nothing to compare against, when there is nothing. Empty when <see cref="IsStated"/>.</summary>
    public string AbsenceReason { get; }

    /// <summary>True when the enumeration answered the question at all — <b>including answering "none"</b>.</summary>
    public bool IsStated => Bounds is not null;

    /// <summary>The enumeration states that <paramref name="assertionId"/> depends on exactly these bounds — possibly none.</summary>
    public static AssertionBoundsExpectation Stated(string assertionId, IReadOnlySet<string> bounds)
    {
        ArgumentNullException.ThrowIfNull(bounds);
        return new AssertionBoundsExpectation(assertionId, bounds, string.Empty);
    }

    /// <summary>
    /// Nothing states which bounds the cited assertion depends on. <paramref name="reason"/> is required
    /// and is printed in the finding — <b>"could not be checked" without "because" is a dead end for
    /// whoever has to repair it.</b>
    /// </summary>
    public static AssertionBoundsExpectation NotStated(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("An unverifiable expectation must say WHY it is unverifiable.", nameof(reason));

        return new AssertionBoundsExpectation(null, null, reason.Trim());
    }
}

/// <summary>
/// One bound where the vector and the table do not agree.
/// </summary>
/// <param name="Bound">The bound's name, as the vector spelled it.</param>
/// <param name="DeclaredValue">What the vector says it was written against.</param>
/// <param name="SpecifiedValue">
/// What the table currently says — <b>null when the table has no such bound at all</b>, which is the
/// <see cref="BoundsCurrencyState.Unknown"/> case and is not the same fact as a differing value.
/// </param>
public sealed record BoundsDisagreement(string Bound, string DeclaredValue, string? SpecifiedValue)
{
    public override string ToString() =>
        SpecifiedValue is null
            ? $"{Bound}: the vector was written against '{DeclaredValue}' and the enumeration's table has no such bound"
            : $"{Bound}: the vector was written against '{DeclaredValue}', the enumeration's table now specifies '{SpecifiedValue}'";
}

/// <summary>One vector's bounds-currency finding.</summary>
/// <param name="Agreed">The bounds that matched, named — so a pass says WHICH numbers it checked rather than merely that it checked.</param>
/// <param name="CitedAssertion">
/// The assertion whose bounds the enumeration was consulted about, when it was. <b>Carried so that a
/// <see cref="BoundsCurrencyState.NoBoundsCited"/> pass names what it verified against</b> — "verified"
/// with no authority named is the shape of a claim taken on trust.
/// </param>
public sealed record VectorBoundsCurrency(
    string VectorId,
    BoundsCurrencyState State,
    IReadOnlyList<BoundsDisagreement> Disagreements,
    IReadOnlyList<string> Agreed,
    string? CitedAssertion = null)
{
    /// <summary>
    /// The THREE passes: every declared bound matched; the vector positively declared none <b>and the
    /// enumeration confirmed none was owed</b>; or <b>the enumeration positively claimed the subject has
    /// no bounds at all, with an author and a reason, uncontradicted</b> (FI-99). The other seven states
    /// are not passes.
    /// </summary>
    public bool Checked => State is BoundsCurrencyState.Current
        or BoundsCurrencyState.NoBoundsCited
        or BoundsCurrencyState.NoBoundsTableClaimed;

    /// <summary>
    /// 🔴 <b>WHO CLAIMED THE SUBJECT HAS NO BOUNDS, AND WHY — carried so a
    /// <see cref="BoundsCurrencyState.NoBoundsTableClaimed"/> pass can be CHALLENGED.</b>
    ///
    /// <para>That state is the only pass in this type that rests on a recorded claim rather than on a
    /// comparison. A green whose basis is somebody's signature must print the signature, or it reads as
    /// an ordinary green and nobody ever goes and checks it.</para>
    /// </summary>
    public BoundsAbsenceClaim Absence { get; init; } = BoundsAbsenceClaim.None;

    /// <summary>
    /// The two FI-99 contradictions. <b>They REFUSE, and they are not
    /// <see cref="PremiseOutOfDate"/> uniformly</b> — the enumeration-side one is not the vector's fault
    /// at all, and marking it as an out-of-date premise would headline the wrong repair.
    /// </summary>
    public bool ContradictsAnAbsenceClaim => State is BoundsCurrencyState.BoundsAbsenceContradictedByEnumeration
        or BoundsCurrencyState.BoundsAbsenceContradictedByVector;

    /// <summary>
    /// <b>Every state that must REFUSE the submission</b>, as opposed to leaving it NOT CHECKED. Gate 3i
    /// keys on this rather than on <see cref="PremiseOutOfDate"/>: those were the same set until FI-99,
    /// and an enumeration contradicting its own absence claim refuses without the vector's premise being
    /// wrong about anything.
    /// </summary>
    public bool Refused => PremiseOutOfDate || ContradictsAnAbsenceClaim;

    /// <summary>
    /// True when the vector's own premise about the bounds is wrong — <b>the states that must produce
    /// <see cref="ResultVerdict.Stale"/> and must never produce a Fail</b>.
    ///
    /// <para><see cref="BoundsCurrencyState.BoundsOmitted"/> is here with Stale and Unknown because the
    /// three share the only property this flag is asked about: <b>the vector is wrong and the block is
    /// not accused of anything.</b> They differ in the repair, which <see cref="PremiseHeadline"/> and
    /// <see cref="Detail"/> carry.</para>
    /// </summary>
    public bool PremiseOutOfDate => State is BoundsCurrencyState.Stale
        or BoundsCurrencyState.Unknown
        or BoundsCurrencyState.BoundsOmitted
        or BoundsCurrencyState.BoundsAbsenceContradictedByVector;

    /// <summary>
    /// The one-line reason a <see cref="PremiseOutOfDate"/> finding produces a Stale verdict, <b>stated
    /// per state</b>. "Written against a number the specification no longer states" is FALSE of
    /// <see cref="BoundsCurrencyState.BoundsOmitted"/> — that vector was written against no number at all
    /// — and a headline that is wrong about which repair is owed sends the reader to the wrong document.
    /// </summary>
    public string PremiseHeadline => State switch
    {
        BoundsCurrencyState.Stale =>
            "THE VECTOR'S PREMISE IS OUT OF DATE — IT WAS WRITTEN AGAINST A BOUND THE SPECIFICATION NO LONGER STATES. Re-read the vector against the current bounds table, then re-submit.",

        BoundsCurrencyState.Unknown =>
            "THE VECTOR NAMES A BOUND THE SPECIFICATION'S TABLE DOES NOT CONTAIN, so its premise resolves to nothing specified. Repair the reference — in the vector or in the table — then re-submit.",

        BoundsCurrencyState.BoundsOmitted =>
            "THE VECTOR CLAIMS IT WAS WRITTEN AGAINST NO BOUND AND THE ENUMERATION SAYS ITS CITED ASSERTION DEPENDS ON ONE — it was written against a bound nobody looked at. Re-read the assertion against the bounds table and declare what it used.",

        BoundsCurrencyState.BoundsAbsenceContradictedByVector =>
            $"THE VECTOR CITES A BOUND AGAINST A SUBJECT '{Absence.By}' HAS CLAIMED HAS NONE (FI-99) — the enumeration's `boundsAbsence` says this subject tabulates no bound at all, and this vector's `boundsUsed` names one. Either the citation is wrong or the absence claim is; settle it with the claimant, then re-submit.",

        // Stated rather than left to the default, so nothing falls through this switch silently. It is
        // not a premise headline at all — this state is deliberately NOT PremiseOutOfDate, so nothing
        // reaches here in normal operation — and if a caller does ask, the answer must not accuse the
        // vector of anything.
        BoundsCurrencyState.BoundsAbsenceContradictedByEnumeration =>
            "THIS VECTOR'S PREMISE IS NOT AT FAULT (FI-99) — the ENUMERATION contradicts its own `boundsAbsence` claim, and the repair is to the enumeration. Do not re-read this vector on the strength of it.",

        _ => "THE VECTOR'S PREMISE ABOUT THE SPECIFIED BOUNDS WAS NOT ESTABLISHED.",
    };

    public string Detail => State switch
    {
        BoundsCurrencyState.NoTable =>
            $"{VectorId}: the enumeration supplied no bounds table, so the bound this vector was written against was compared against nothing. NOT CHECKED — an absent table is not a matching one.",

        BoundsCurrencyState.NotDeclared =>
            $"{VectorId}: the vector states no bound. NOT CHECKED, and this is the AMB-19 hole itself — a vector that never records which number it was written against can never be found stale, so it survives a retune silently and every other check agrees it is fine.",

        BoundsCurrencyState.Current =>
            $"{VectorId}: written against the values the enumeration currently specifies ({string.Join("; ", Agreed)}).",

        BoundsCurrencyState.Stale =>
            $"{VectorId}: *** STALE, NOT FAILED. *** " + string.Join(" | ", Disagreements.Select(d => d.ToString()))
            + ". The bound was retuned and this vector was not re-read against the new number, so it is testing something the specification no longer says. THIS IS NOT A DEFECT IN THE BLOCK and must not be reported as one — re-read the vector against the current table, then re-submit.",

        BoundsCurrencyState.Unknown =>
            $"{VectorId}: the vector names a bound the enumeration's table does not contain — " + string.Join(" | ", Disagreements.Select(d => d.ToString()))
            + ". Either the bound was renamed or removed, or the vector is naming something that was never specified. Both are repairs to the VECTOR or the TABLE, never to the block.",

        BoundsCurrencyState.NoBoundsCited =>
            $"{VectorId}: the vector positively states it was written against NO bound (`boundsUsed` present and empty), and the enumeration confirms that assertion '{CitedAssertion}' depends on none. CHECKED — "
            + "the claim was VERIFIED against the enumeration, not taken from the vector. An assertion about a gating condition, an ordering or a state names no time and no threshold, so it has no bound to cite, "
            + "and refusing that would leave inventing one as the only way to pass.",

        BoundsCurrencyState.NoBoundsClaimUnverified =>
            $"{VectorId}: the vector positively states it was written against NO bound, and NOTHING COULD VERIFY THAT CLAIM — {UnverifiableBecause}. NOT CHECKED: a declaration is a transferred responsibility, not a verification, "
            + "and taking this one on trust would let a vector opt out of AMB-19 by declaring nothing rather than by declaring silence. "
            + "*** THE REPAIR IS TO THE ENUMERATION, NEVER TO THE VECTOR: *** state which of the enumeration's bounds each assertion depends on (`enumeration.assertionBounds`). "
            + "DO NOT add a bound to the vector to clear this — a gate that can only be satisfied by making something up is inverted, which is the defect this state exists to avoid re-creating.",

        BoundsCurrencyState.BoundsOmitted =>
            $"{VectorId}: *** THE VECTOR CLAIMS IT WAS WRITTEN AGAINST NO BOUND, AND THE ENUMERATION SAYS ASSERTION '{CitedAssertion}' DEPENDS ON {string.Join(", ", Disagreements.Select(d => d.Bound))}. *** "
            + "This is a vector written against a bound it did not look at — the strong form of the AMB-19 hole, not the silent one: it does not merely fail to record a number, it asserts that no number applies where the specification says one does. "
            + string.Join(" | ", Disagreements.Select(d => d.ToString()))
            + ". THE BLOCK IS NOT ACCUSED OF ANYTHING HERE. Re-read the cited assertion against the bounds table and declare the value it was written against.",

        // FI-99. *** THIS DETAIL MUST NOT READ LIKE AN ORDINARY GREEN. *** It is the one pass in this
        // type that rests on a recorded human claim rather than on a comparison, so it prints the
        // claimant and quotes the reason: a reader has to be able to see WHOSE signature the pass is and
        // go and argue with them. The falsifiability is stated too, because "we accepted a claim" and "we
        // accepted a claim that nothing in this submission contradicts" are different strengths.
        BoundsCurrencyState.NoBoundsTableClaimed =>
            $"{VectorId}: the enumeration supplies NO bounds table AND POSITIVELY CLAIMS IT HAS NONE TO SUPPLY — claimed by '{Absence.By}', because: \"{Absence.Because}\". "
            + "CHECKED — *** BUT ON A RECORDED CLAIM, NOT ON A COMPARISON. *** There is nothing to compare here and there never could be; what makes this a pass rather than a hole is that the claim is FALSIFIABLE and was not falsified: "
            + "no assertion in this enumeration names a bound, and no vector cites one. Either would have REFUSED this submission. "
            + "*** IF YOU DOUBT IT, THE PERSON TO ASK IS NAMED ABOVE *** — a purely combinational subject (an arbitration, a selection) genuinely has no timing bound, no tolerance, no delay, no timeout and no settling time, "
            + "and refusing that would leave INVENTING one as the only route to admissibility, which is the fabrication this whole gate exists to prevent.",

        BoundsCurrencyState.BoundsAbsenceContradictedByEnumeration =>
            $"{VectorId}: *** THE ENUMERATION CONTRADICTS ITS OWN ABSENCE CLAIM. *** '{Absence.By}' claims this subject has no bounds (\"{Absence.Because}\"), and the same enumeration names {string.Join(", ", Absence.ContradictingAssertions)}. "
            + "REFUSED, and *** THIS IS AN ENUMERATION DEFECT — THE REPAIR IS TO THE ENUMERATION AND NEVER TO THE VECTOR OR THE BLOCK. *** "
            + "Either the subject does have bounds, in which case drop `boundsAbsence` and supply the `bounds:` table, or it does not, in which case those assertions' `assertionBounds` entries are wrong. "
            + "The gate cannot pick, and picking for it in the passing direction is how a claim becomes a hole.",

        BoundsCurrencyState.BoundsAbsenceContradictedByVector =>
            $"{VectorId}: *** THIS VECTOR CITES A BOUND AGAINST A SUBJECT CLAIMED TO HAVE NONE. *** '{Absence.By}' claims this subject tabulates no bound (\"{Absence.Because}\"), and this vector's `boundsUsed` names "
            + string.Join(", ", Disagreements.Select(d => $"'{d.Bound}' = '{d.DeclaredValue}'"))
            + ". REFUSED, and *** THIS IS A VECTOR DEFECT — a different repair from an enumeration that contradicts itself. *** "
            + "The vector was written against a number no table in this submission specifies, so nothing can ever find it stale. Either withdraw the citation (declare `boundsUsed: {}`) or take the absence claim up with its author. "
            + "THE BLOCK IS NOT ACCUSED OF ANYTHING HERE.",

        _ => $"{VectorId}: unknown bounds-currency state.",
    };

    /// <summary>
    /// Why the claim could not be verified, for <see cref="BoundsCurrencyState.NoBoundsClaimUnverified"/>.
    /// Carried through the finding so the reader of a RESULT meets the reason, not only the reader of the
    /// gate that produced it.
    /// </summary>
    public string UnverifiableBecause { get; init; } = "no reason was recorded";
}

/// <summary>
/// The comparison itself — <b>AMB-19's whole fix, and it is one set difference over two dictionaries.</b>
///
/// <para><b>THE COMPARISON RULE IS DELIBERATELY ASYMMETRIC: lax where laxity fails CLOSED, strict where
/// laxity would fail OPEN.</b> A bound's NAME is compared case-insensitively, because getting a name
/// wrong yields <see cref="BoundsCurrencyState.Unknown"/> — a refusal — so a lenient match can only ever
/// turn a refusal into a real comparison. A bound's VALUE is compared with case preserved and ordinally,
/// because a lenient match there would turn a real difference into a PASS, which is the one direction
/// this check may never move in.</para>
///
/// <para><b>Values are normalised by <see cref="AssertionId.Normalise"/> and by nothing else</b> — trim,
/// collapse internal whitespace, strip one trailing <c>.</c> or <c>;</c>. That is the house rule already,
/// reused rather than re-invented, and it is deliberately NOT a duration parser: <c>T#60S</c> and
/// <c>T#1M</c> are the same interval and this reports them as different. <b>Teaching it to equate them
/// would mean teaching it to equate things, which is how a comparator starts passing.</b> The report
/// prints both strings, so the cost of the strictness is a human reading two values, and the cost of the
/// leniency would be a silent green.</para>
///
/// <para><b>What it CANNOT tell apart, stated rather than hidden:</b> a genuine retune and a
/// mis-transcribed table entry produce the same finding. Both are STALE, both need a person, and neither
/// is the block's fault — so the verdict is right in both cases even though the diagnosis is not
/// determined. What it must never do is resolve that ambiguity by guessing in the passing direction.</para>
///
/// <para>🔴 <b>AND SINCE 2026-08-17 IT IS TWO COMPARISONS, NOT ONE.</b> Beside the value comparison sits
/// a set comparison for the vector that declares <c>boundsUsed: {}</c> — the positive claim <i>"this
/// assertion has no bound to cite"</i>, which is a real and common thing and was previously
/// indistinguishable from silence. That vector was refused, <b>and the only way to clear the refusal was
/// to invent a bound</b> — the exact fabrication this gate exists to prevent. <i>A gate satisfiable only
/// by making something up is inverted.</i> The claim is now CHECKED against the ENUMERATION's own
/// per-assertion relation: confirmed is a pass, contradicted is a refusal, and unverifiable is NOT
/// CHECKED. <b>Taking the claim from the vector would have made the state a hole rather than a fix.</b></para>
/// </summary>
public static class BoundsCurrencyCheck
{
    /// <summary>Compare one vector's declared bounds against the enumeration's table.</summary>
    /// <param name="declared">
    /// What the vector says it was written against. 🔴 <b>NULL AND EMPTY ARE DIFFERENT CLAIMS AND ARE
    /// NOT INTERCHANGEABLE HERE</b> (they were until 2026-08-17): null is <i>"nobody said anything about
    /// bounds"</i> and stays <see cref="BoundsCurrencyState.NotDeclared"/>; empty is the positive claim
    /// <i>"this vector was written against no bound at all"</i>, which is checked against
    /// <paramref name="expected"/> and can come out as any of
    /// <see cref="BoundsCurrencyState.NoBoundsCited"/>,
    /// <see cref="BoundsCurrencyState.NoBoundsClaimUnverified"/> or
    /// <see cref="BoundsCurrencyState.BoundsOmitted"/>.
    /// </param>
    /// <param name="specified">
    /// The enumeration's current bounds table. Null or empty yields
    /// <see cref="BoundsCurrencyState.NoTable"/>, which is NOT a pass — <b>unless</b>
    /// <paramref name="absence"/> carries a well-formed, uncontradicted claim that there is nothing to
    /// tabulate (FI-99), which is the only thing that opens that guard.
    /// </param>
    /// <param name="expected">
    /// <b>What the ENUMERATION says the cited assertion's bounds are.</b> Required, and deliberately not
    /// defaulted: this is the outside authority that makes the empty claim checkable, and an optional
    /// parameter is an invitation — six call sites once simply never passed one. Callers with nothing to
    /// supply must say so by name, via <see cref="AssertionBoundsExpectation.NotStated"/>, which fails
    /// closed and prints the reason.
    ///
    /// <para><b>It is consulted ONLY on the empty claim.</b> A vector that declares one bound where the
    /// enumeration says its assertion depends on two is NOT caught here — that is a partial-citation
    /// check, a different gate, and building it into this one would have it fire on every vector in the
    /// live corpus, none of whose enumerations state the relation at all. Recorded at the site rather
    /// than left to be rediscovered.</para>
    /// </param>
    /// <param name="absence">
    /// 🔴 <b>THE ENUMERATION'S OWN CLAIM THAT ITS SUBJECT HAS NO BOUNDS TO TABULATE — FI-99, and the only
    /// key that opens the <see cref="BoundsCurrencyState.NoTable"/> guard below.</b>
    ///
    /// <para><b>Optional, and the omitted value is the STRICT one</b> — unlike <paramref name="expected"/>,
    /// whose default was left out because omitting it failed OPEN. <see cref="BoundsAbsenceClaim.None"/>
    /// reproduces pre-FI-99 behaviour exactly: a caller who never learns this parameter exists gets the
    /// closed guard, not a pass. There is no direction in which forgetting it can admit something.</para>
    /// </param>
    public static VectorBoundsCurrency Evaluate(
        string vectorId,
        IReadOnlyDictionary<string, string>? declared,
        IReadOnlyDictionary<string, string>? specified,
        AssertionBoundsExpectation expected,
        BoundsAbsenceClaim? absence = null)
    {
        ArgumentNullException.ThrowIfNull(expected);

        var id = string.IsNullOrWhiteSpace(vectorId) ? "<unnamed vector>" : vectorId;
        var claim = absence ?? BoundsAbsenceClaim.None;

        var none = Array.Empty<BoundsDisagreement>();
        var noNames = Array.Empty<string>();

        // *** FALSIFIABILITY, LIMB 1 OF 3, AND IT IS CHECKED BEFORE ANYTHING ELSE BECAUSE IT IS THE
        // FLATTEST CONTRADICTION AVAILABLE: *** a claim that the subject tabulates NO bound, sitting on
        // top of a table that tabulates some. The claim is not "ignored when a table exists" — ignoring it
        // would leave a false statement standing unchallenged in the document, which is how the next
        // reader comes to believe it. It REFUSES, and it refuses as an ENUMERATION defect, because both
        // halves of the contradiction were written by the enumeration.
        if (claim.IsClaimed && specified is not null && specified.Count > 0)
        {
            var tabulated = specified.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();

            return new VectorBoundsCurrency(id, BoundsCurrencyState.BoundsAbsenceContradictedByEnumeration, none, noNames)
            {
                Absence = BoundsAbsenceClaim.Of(true, claim.By, claim.Because,
                    claim.ContradictingAssertions.Concat(tabulated.Select(b => $"the `bounds` table entry '{b}'")).ToArray()),
            };
        }

        // The table is checked FIRST. With no table there is nothing to be current or stale against, and
        // reporting NotDeclared for a vector that did declare something would name the wrong repair.
        //
        // *** IT ALSO COMES BEFORE THE EMPTY CLAIM, AND THAT IS EMPTY-IS-NOT-CLEAN. *** An enumeration
        // with no bounds table is not an enumeration whose assertions have no bounds: it is one nobody
        // supplied a table for. Letting `boundsUsed: {}` pass against it would make "declare nothing on
        // both sides" the cheapest route through this gate.
        //
        // 🔴 *** THAT REASONING IS STILL RIGHT, AND SINCE FI-99 IT IS ALSO THE REASON THE GUARD SURVIVES
        // AT ALL. *** What it could not tell apart was "nobody wrote a table" from "there is nothing to
        // put in one", and for a purely combinational subject — an arbitration, a selection — the second
        // is the truth. Measured 2026-09-03: an otherwise complete submission, 31 gates run, 0 refused,
        // NOT ADMISSIBLE on this one NOT CHECKED, with the enumerator's positive claim written in prose
        // no gate reads. The only escape available was to invent a bound, and A GATE WHOSE ONLY ESCAPE IS
        // A LIE IS WORSE THAN THE GAP IT GUARDS.
        //
        // So the empty case splits, and the split is a KEY rather than a widening:
        //   - no claim, or a claim missing its author or its reason  -> NoTable. UNCHANGED. Still shut.
        //   - a claim with an author and a reason, uncontradicted    -> NoBoundsTableClaimed, a pass.
        //   - a claim contradicted by this submission                -> a REFUSAL naming which side.
        // `{}` on both sides with nobody signing for it remains exactly as expensive as it was.
        if (specified is null || specified.Count == 0)
        {
            if (!claim.IsClaimed)
                return new VectorBoundsCurrency(id, BoundsCurrencyState.NoTable, none, noNames) { Absence = claim };

            // FALSIFIABILITY, LIMB 2: the claimant's own assertions name a bound. The enumeration said
            // "nothing here has one" and its own per-assertion relation says otherwise. An ENUMERATION
            // defect, and it outranks the vector-side check because a self-contradictory authority cannot
            // adjudicate the vector.
            if (claim.IsContradictedByEnumeration)
            {
                return new VectorBoundsCurrency(id, BoundsCurrencyState.BoundsAbsenceContradictedByEnumeration, none, noNames)
                {
                    Absence = claim,
                };
            }

            // FALSIFIABILITY, LIMB 3: a vector cites a bound against a subject claimed to have none. A
            // VECTOR defect — a different repair, hence a different state and a different sentence.
            if (declared is { Count: > 0 })
            {
                var cited = declared
                    .OrderBy(e => e.Key, StringComparer.Ordinal)
                    .Select(e => new BoundsDisagreement(e.Key.Trim(), AssertionId.Normalise(e.Value), null))
                    .ToArray();

                return new VectorBoundsCurrency(id, BoundsCurrencyState.BoundsAbsenceContradictedByVector, cited, noNames)
                {
                    Absence = claim,
                };
            }

            // *** SILENCE IS STILL SILENCE, EVEN UNDER A GOOD ABSENCE CLAIM. *** A vector that omits
            // `boundsUsed` said nothing, and FI-99 gave the ENUMERATION a voice, not the vector. Reading
            // an absent key as agreement with somebody else's claim is the absent-versus-empty collapse
            // this file has now been repaired for twice; declaring `boundsUsed: {}` is a one-line, honest
            // ask and it is what the measured case already did on both its vectors.
            if (declared is null)
                return new VectorBoundsCurrency(id, BoundsCurrencyState.NotDeclared, none, noNames) { Absence = claim };

            return new VectorBoundsCurrency(id, BoundsCurrencyState.NoBoundsTableClaimed, none, noNames, expected.AssertionId)
            {
                Absence = claim,
            };
        }

        if (declared is null)
            return new VectorBoundsCurrency(id, BoundsCurrencyState.NotDeclared, none, noNames);

        // Built BEFORE the empty-claim branch so both paths resolve a bound name by exactly the same
        // rule. Two lookups with two comparers is how a name that matches in one report fails in another.
        var table = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in specified)
            table[entry.Key.Trim()] = entry.Value;

        // --- the positive claim: "this assertion has no bound to cite" -------------------------------
        if (declared.Count == 0)
        {
            if (!expected.IsStated)
            {
                return new VectorBoundsCurrency(id, BoundsCurrencyState.NoBoundsClaimUnverified, none, noNames)
                {
                    UnverifiableBecause = expected.AbsenceReason,
                };
            }

            var owed = expected.Bounds!
                .OrderBy(b => b, StringComparer.Ordinal)
                .ToArray();

            if (owed.Length == 0)
                return new VectorBoundsCurrency(id, BoundsCurrencyState.NoBoundsCited, none, noNames, expected.AssertionId);

            // The vector says "no number applies" and the enumeration names the numbers that do. The
            // declared value is the claim itself, so it is spelled out rather than left blank: a
            // disagreement row reading `<none declared>` against `T#60S` is legible without the prose.
            var omitted = owed
                .Select(b => new BoundsDisagreement(b, "<none declared>",
                    table.TryGetValue(b.Trim(), out var value) ? AssertionId.Normalise(value) : null))
                .ToArray();

            return new VectorBoundsCurrency(id, BoundsCurrencyState.BoundsOmitted, omitted, noNames, expected.AssertionId);
        }

        var disagreements = new List<BoundsDisagreement>();
        var agreed = new List<string>();
        var unknown = false;

        foreach (var entry in declared.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            var name = entry.Key.Trim();
            var used = AssertionId.Normalise(entry.Value);

            if (!table.TryGetValue(name, out var raw))
            {
                unknown = true;
                disagreements.Add(new BoundsDisagreement(name, used, null));
                continue;
            }

            var current = AssertionId.Normalise(raw);

            if (string.Equals(used, current, StringComparison.Ordinal))
                agreed.Add($"{name} = {current}");
            else
                disagreements.Add(new BoundsDisagreement(name, used, current));
        }

        // Unknown outranks Stale: a bound nobody specified is a broken reference, and repairing the
        // reference has to happen before any question about its value can even be asked.
        var state = unknown ? BoundsCurrencyState.Unknown
            : disagreements.Count > 0 ? BoundsCurrencyState.Stale
            : BoundsCurrencyState.Current;

        return new VectorBoundsCurrency(id, state, disagreements, agreed);
    }
}
