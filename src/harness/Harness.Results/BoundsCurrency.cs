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
    /// <b>The vector states no bound.</b> NOT a pass — and this is the state the whole check turns on.
    /// A vector that never says what number it was written against cannot be found stale by anything,
    /// which is exactly the silent survival AMB-19 describes. It is UNCHECKED, and unchecked fails
    /// closed.
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
public sealed record VectorBoundsCurrency(
    string VectorId,
    BoundsCurrencyState State,
    IReadOnlyList<BoundsDisagreement> Disagreements,
    IReadOnlyList<string> Agreed)
{
    /// <summary>True only for <see cref="BoundsCurrencyState.Current"/>. The other four are not passes.</summary>
    public bool Checked => State == BoundsCurrencyState.Current;

    /// <summary>
    /// True when the vector's own premise is out of date — <b>the states that must produce
    /// <see cref="ResultVerdict.Stale"/> and must never produce a Fail</b>.
    /// </summary>
    public bool PremiseOutOfDate => State is BoundsCurrencyState.Stale or BoundsCurrencyState.Unknown;

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

        _ => $"{VectorId}: unknown bounds-currency state.",
    };
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
/// </summary>
public static class BoundsCurrencyCheck
{
    /// <summary>Compare one vector's declared bounds against the enumeration's table.</summary>
    /// <param name="declared">
    /// What the vector says it was written against. <b>Null and empty are the same claim here</b> — the
    /// vector said nothing — and both yield <see cref="BoundsCurrencyState.NotDeclared"/>.
    /// </param>
    /// <param name="specified">
    /// The enumeration's current bounds table. Null or empty yields
    /// <see cref="BoundsCurrencyState.NoTable"/>, which is NOT a pass.
    /// </param>
    public static VectorBoundsCurrency Evaluate(
        string vectorId,
        IReadOnlyDictionary<string, string>? declared,
        IReadOnlyDictionary<string, string>? specified)
    {
        var id = string.IsNullOrWhiteSpace(vectorId) ? "<unnamed vector>" : vectorId;

        var none = Array.Empty<BoundsDisagreement>();
        var noNames = Array.Empty<string>();

        // The table is checked FIRST. With no table there is nothing to be current or stale against, and
        // reporting NotDeclared for a vector that did declare something would name the wrong repair.
        if (specified is null || specified.Count == 0)
            return new VectorBoundsCurrency(id, BoundsCurrencyState.NoTable, none, noNames);

        if (declared is null || declared.Count == 0)
            return new VectorBoundsCurrency(id, BoundsCurrencyState.NotDeclared, none, noNames);

        var table = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in specified)
            table[entry.Key.Trim()] = entry.Value;

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
