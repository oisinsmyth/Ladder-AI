namespace Harness.Map;

/// <summary>
/// What a binding says about one published signal's RESTING value — the state D33's first check is
/// entitled to expect before a test starts.
/// </summary>
public enum InertRestKind
{
    /// <summary>The signal rests at a stated value, and inert is gated on it reading that value.</summary>
    Value,

    /// <summary>
    /// 🔴 <b>The signal has NO meaningful resting value, so inert is NOT gated on it — a positive claim,
    /// never an omission.</b>
    ///
    /// <para>The motivating case is a ONE-SCAN PULSE. Measured on the rig: a signal true for a single scan
    /// reads 1 in roughly one sample of five, so <i>any</i> single-sample expectation on it is a coin toss
    /// — and the coin toss gets blamed on the block under test. Excluding it is the correct engineering
    /// answer and it is also a real weakening of the gate, which is why it must be SAID, with a reason,
    /// and why it is counted separately in every report.</para>
    /// </summary>
    Excluded,
}

/// <summary>
/// 🔴 <b>ONE PUBLISHED SIGNAL'S DECLARED RESTING STATE — the thing D33's first check was documented as
/// requiring and never actually had.</b>
///
/// <para>*** THE DEFECT THIS TYPE CLOSES. *** <c>InertDeclaration</c>'s own summary states the contract —
/// <i>"D33's FIRST check, and it must be declared: a check with no expectation passes over anything"</i> —
/// and its only production caller built the declaration as
/// <c>Range(0, ResultRegistersNeeded).ToDictionary(i =&gt; i, _ =&gt; (ushort)0)</c>. <b>Every result
/// register was asserted to rest at zero, hardcoded.</b> That is not a declaration; it is an assumption
/// wearing one's clothes.</para>
///
/// <para><b>Measured consequence, on real hardware.</b> One slot passed its inert gate only because its
/// published signals happen to rest at zero. A second could not pass it at all, and every reason was a
/// legitimate resting value of a correctly-functioning program:</para>
/// <list type="bullet">
/// <item>a <b>sentinel</b> of <c>-1</c> meaning <i>no test has been performed</i>, where <c>0</c> is a
/// measured PASS verdict — <b>so asserting 0 asserts a pass as the resting state</b>, and the hardcoded
/// default therefore fails in the SILENT direction as well as the loud one: it accepts a stale result
/// from the previous index as an inert start state;</item>
/// <item>a commanded input resting at <b>whatever the pending index declares</b>, which no single constant
/// can be right for;</item>
/// <item>alarm bits that are <b>honestly true at rest</b> after a restart;</item>
/// <item>a <b>one-scan pulse</b>, which no single-sample expectation can be right about at all.</item>
/// </list>
///
/// <para><b>THE VALUE IS TEXT, AND THAT IS DELIBERATE.</b> It goes through <see cref="MirrorValueFit"/>
/// with the signal's own type and its own <see cref="MirroredSignal.Encoding"/> — exactly the path the
/// stimulus side uses — so a <c>Bool</c> resting <c>true</c>, an <c>Int</c> resting <c>-1</c>, a
/// <c>Time</c> spanning two registers and a symbolic value resolved through a declared table all work
/// without a second numeric path being written here. A second path is how two derivations of one rule come
/// to disagree, which this file has three recorded instances of.</para>
/// </summary>
/// <param name="Kind">Whether a value is claimed, or the signal is claimed to have no meaningful rest.</param>
/// <param name="Value">
/// The resting value as text, or null for <see cref="InertRestKind.Excluded"/>. <b>Signed</b> — a sentinel
/// of <c>-1</c> is a real case, and the register it lands in is unsigned on the wire.
/// </param>
/// <param name="Basis">
/// Why. <b>REQUIRED for <see cref="InertRestKind.Excluded"/> and optional for a declared value, and the
/// asymmetry is not laziness:</b> a declared value is falsifiable by the machine on every single run — the
/// device disagrees with it immediately and by name. <b>An exclusion is checked by nobody, ever</b>: it
/// removes a register from the gate and nothing downstream can notice it was wrong. What no machine will
/// check, a reader must, and a reader needs the reason.
/// </param>
public sealed record InertRest(InertRestKind Kind, string? Value, string Basis)
{
    /// <summary>The signal rests at <paramref name="value"/>. The basis is optional here — see the type's remarks.</summary>
    public static InertRest At(string value, string basis = "") =>
        new(InertRestKind.Value, value, (basis ?? string.Empty).Trim());

    /// <summary>
    /// The signal has no meaningful resting value and inert is not gated on it. <b>The reason is required</b>
    /// — an exclusion with no stated reason is indistinguishable from a signal somebody forgot.
    /// </summary>
    public static InertRest Excluded(string reason) =>
        string.IsNullOrWhiteSpace(reason)
            ? throw new ArgumentException(
                "an EXCLUDED resting state must state its reason. Excluding a signal removes a register from D33's first "
                + "check and nothing downstream can ever notice that it was wrong — so the claim is checked by a reader or "
                + "by nobody. An exclusion with no reason is indistinguishable from a signal somebody forgot to declare.",
                nameof(reason))
            : new InertRest(InertRestKind.Excluded, null, reason.Trim());

    /// <summary>True when this signal is claimed to have no meaningful resting value.</summary>
    public bool IsExcluded => Kind == InertRestKind.Excluded;

    /// <summary>
    /// Null when this declaration is well-formed, otherwise why it is not. <b>Checked at the consuming end
    /// as well as at the factories</b>, because a record can be constructed directly and a wire format can
    /// deserialize into one — and the factories are then not the only route in.
    /// </summary>
    public string? Malformed(string signal) => Kind switch
    {
        InertRestKind.Excluded when string.IsNullOrWhiteSpace(Basis) =>
            $"'{signal}' declares an EXCLUDED resting state with no reason. An exclusion removes a register from the inert "
            + "check and nothing downstream can notice it was wrong, so the reason is what a reader checks it by.",

        InertRestKind.Excluded when !string.IsNullOrWhiteSpace(Value) =>
            $"'{signal}' declares an EXCLUDED resting state AND a value '{Value}'. Those are two different claims: one says "
            + "the signal has no meaningful rest, the other says exactly what it is. Choosing between them would be this "
            + "code deciding what was meant.",

        InertRestKind.Value when string.IsNullOrWhiteSpace(Value) =>
            $"'{signal}' declares a resting VALUE and gives none. An absent value is not a zero one — zero is a value the "
            + "signal could legitimately rest at, and it is a measured PASS verdict on at least one real deliverable.",

        _ => null,
    };

    public override string ToString() =>
        IsExcluded ? $"EXCLUDED ({Basis})" : $"rests at {Value}" + (Basis.Length == 0 ? string.Empty : $" ({Basis})");
}
