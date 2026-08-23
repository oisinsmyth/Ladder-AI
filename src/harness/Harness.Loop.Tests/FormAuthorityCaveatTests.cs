using Harness.Loop;
using Harness.Results;

namespace Harness.Loop.Tests;

/// <summary>
/// 🔴 <b>THE <c>F-3-authority</c> CAVEAT WAS A CONSTANT, AND IT WENT ON SAYING THE HOLE WAS OPEN AFTER
/// THE GATE THAT CLOSES IT SHIPPED.</b>
///
/// <para><b>The measured problem.</b> <c>LoopRun.Caveats</c> is built from the REQUEST alone, before the
/// gate runs, and its F-3 entry asserted three things as facts: that nothing checks the declared form
/// against the enumeration, that the enumeration has no producer, and that an author citing a NEVER and
/// declaring WHEN takes the permissive path. Gate 3e — <c>assertion form authority</c> — makes all three
/// false whenever the enumeration carries forms, and it has been returning CHECKED on real submissions.
/// The caveat could not see that, because a caveat computed before a gate cannot report the gate.</para>
///
/// <para><b>Why that is worse than a stale comment.</b> A caveat travels on the RESULT PACKAGE — it is
/// what a reader is told the result does not establish. A standing, unconditional statement that an
/// enforcement is absent teaches every reader to discount an enforcement that is present, and it was
/// quoted onward as the reason the gap was still open.</para>
///
/// <para><b>The refusal side is the control.</b> A caveat that always said "closed" would be the same
/// defect pointing the other way, so the second test drives an enumeration carrying no forms and asserts
/// the hole is still reported open.</para>
/// </summary>
public class FormAuthorityCaveatTests
{
    private static LoopCaveat FormAuthority(LoopRequest request) =>
        Assert.Single(LoopRun.Generate(request, stopWhenInadmissible: false).Caveats,
            c => c.Id == "F-3-authority");

    /// <summary>
    /// The fixture's own vector, taken off the request rather than rebuilt.
    ///
    /// <para><b>Rebuilding it here would be a second fixture that agrees today</b> — it has to name the
    /// same clause, the same assertion hash and the same block tags as
    /// <c>LoopRunTests.Request</c>'s enumeration, and a copy that drifted would make these tests measure
    /// their own divergence instead of the caveat.</para>
    /// </summary>
    private static SubmissionVector Default() => LoopRunTests.Request().Vectors[0];

    /// <summary>
    /// <b>The enumeration states the form, gate 3e compares it, and the caveat says so.</b>
    /// </summary>
    [Fact]
    public void When_the_enumeration_states_the_form_the_caveat_reports_the_hole_CLOSED()
    {
        var caveat = FormAuthority(LoopRunTests.Request());

        Assert.Contains("CLOSED FOR THIS SUBMISSION", caveat.Detail, StringComparison.Ordinal);
        Assert.Contains("3e", caveat.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 <b>The sentence that was false on every package since gate 3e shipped.</b> It is asserted against
    /// by name so that re-introducing it as a constant fails here rather than in a result package.
    /// </summary>
    [Fact]
    public void And_it_no_longer_claims_the_enumeration_has_no_producer()
    {
        Assert.DoesNotContain("has no producer", FormAuthority(LoopRunTests.Request()).Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The control: a formless enumeration leaves the hole exactly as open as it was</b>, and the caveat
    /// must still say so. Gate 3e reports NOT CHECKED against the flat projection, which is not a pass.
    /// </summary>
    [Fact]
    public void A_formless_enumeration_still_reports_the_hole_OPEN()
    {
        var formless = AssertionEnumeration.Of(
            LoopRunTests.Request().Enumeration.Enumerations[0].Clauses,
            LoopRunTests.Request().Enumeration.Enumerations[0].Assertions,
            enumerator: "agent-c");

        var caveat = FormAuthority(LoopRunTests.Request(enumeration: formless));

        Assert.Contains("NOT BEEN COMPARED", caveat.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("CLOSED FOR THIS SUBMISSION", caveat.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>A vector declaring a form the enumeration contradicts is reported as the walk-around it is</b> —
    /// the caveat must not read "closed" on a submission gate 3e refused.
    /// </summary>
    [Fact]
    public void A_form_the_enumeration_contradicts_is_reported_as_REFUSED()
    {
        var caveat = FormAuthority(LoopRunTests.Request(vector: Default() with { Form = AssertionForm.Never }));

        Assert.Contains("REFUSED", caveat.Detail, StringComparison.Ordinal);
    }
}
