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
    /// <b>The enumeration supplied no bounds table</b>, so there was nothing to compare against. NOT a
    /// pass: the comparison did not happen.
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
    /// IT.</b> The only pass besides <see cref="Current"/>.
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
    /// The two passes: every declared bound matched, or the vector positively declared none <b>and the
    /// enumeration confirmed none was owed</b>. The other five are not passes.
    /// </summary>
    public bool Checked => State is BoundsCurrencyState.Current or BoundsCurrencyState.NoBoundsCited;

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
        or BoundsCurrencyState.BoundsOmitted;

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
    /// <see cref="BoundsCurrencyState.NoTable"/>, which is NOT a pass.
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
    public static VectorBoundsCurrency Evaluate(
        string vectorId,
        IReadOnlyDictionary<string, string>? declared,
        IReadOnlyDictionary<string, string>? specified,
        AssertionBoundsExpectation expected)
    {
        ArgumentNullException.ThrowIfNull(expected);

        var id = string.IsNullOrWhiteSpace(vectorId) ? "<unnamed vector>" : vectorId;

        var none = Array.Empty<BoundsDisagreement>();
        var noNames = Array.Empty<string>();

        // The table is checked FIRST. With no table there is nothing to be current or stale against, and
        // reporting NotDeclared for a vector that did declare something would name the wrong repair.
        //
        // *** IT ALSO COMES BEFORE THE EMPTY CLAIM, AND THAT IS EMPTY-IS-NOT-CLEAN. *** An enumeration
        // with no bounds table is not an enumeration whose assertions have no bounds: it is one nobody
        // supplied a table for. Letting `boundsUsed: {}` pass against it would make "declare nothing on
        // both sides" the cheapest route through this gate.
        if (specified is null || specified.Count == 0)
            return new VectorBoundsCurrency(id, BoundsCurrencyState.NoTable, none, noNames);

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
