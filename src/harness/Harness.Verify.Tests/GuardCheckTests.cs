using Harness.Wire;

namespace Harness.Verify.Tests;

/// <summary>
/// <see cref="GuardCheck"/> — the judge, exercised in BOTH directions.
///
/// <para>🔴 <b>Why this exists as a separate file.</b> The cases that matter most — the positive control
/// being ADMITTED, the acceptance control being REFUSED — are exactly the ones no rig can be made to
/// produce, because producing them means breaking <c>MirrorClient</c>. If the judgement lived inline in
/// <c>VerifyRun</c>, the only way to exercise them would be to hand-edit the production guard and run the
/// binary — and <i>a check whose exercise requires editing the check will not be exercised.</i> That is
/// how this project's most load-bearing IL walk decayed into a comment describing a manual run somebody
/// once did.</para>
///
/// <para><b>Every alarm is paired with its silence.</b> A judge that returned
/// <see cref="VerifyExit.GuardDidNotBehave"/> for everything would satisfy every test that only looks for
/// the alarm, and would be switched off by the first person it inconvenienced.</para>
/// </summary>
public class GuardCheckTests
{
    private const uint Composed = 0x33434A68;
    private const uint Observed = 0xF52ECEAD;
    private const uint Wrong = 0x11112222;

    private const string Db6Refusal =
        "the version register reads 16#F52ECEAD and this client's map was derived for 16#33434A68. A download landed on the "
        + "device that this client's mirror does not describe; every address it holds is now a guess (DB-6).";

    private static VersionReport Stale() =>
        new(VersionOutcome.Stale, Observed, Composed, 3, 1, "stale");

    private static VersionReport Confirmed() =>
        new(VersionOutcome.Confirmed, Composed, Composed, 3, 1, "confirmed");

    private static ControlAttempt Refused(string label, uint expected, string? message = null) =>
        new(label, expected, message ?? Db6Refusal, null);

    private static ControlAttempt Admitted(string label, uint expected) =>
        new(label, expected, null, Snapshot());

    private static ControlSnapshot Snapshot() =>
        new(Observed, new ScanCount(7), new ushort[] { 0 }, new ushort[] { 0 });

    // ---- the two healthy shapes -------------------------------------------------------------------

    /// <summary>The live shape: a stale device, a guard that refused the wrong stamp and admitted the device's own.</summary>
    [Fact]
    public void Stale_device_with_a_working_guard_is_NotConfirmed_with_no_faults()
    {
        var verdict = GuardCheck.Of(
            Stale(),
            Refused("A", Composed),
            Refused("B", Wrong),
            Admitted("C", Observed));

        Assert.Equal(VerifyExit.NotConfirmed, verdict.Exit);
        Assert.Empty(verdict.Faults);
    }

    /// <summary>The other healthy shape: the device carries the build, so A and C are one client's verdict.</summary>
    [Fact]
    public void Confirmed_device_with_a_working_guard_is_Confirmed_with_no_faults()
    {
        var verdict = GuardCheck.Of(
            Confirmed(),
            Admitted("A", Composed),
            Refused("B", Wrong),
            Admitted("C", Composed));

        Assert.Equal(VerifyExit.Confirmed, verdict.Exit);
        Assert.Empty(verdict.Faults);
    }

    // ---- the alarms -------------------------------------------------------------------------------

    /// <summary>
    /// *** THE ONE THAT MATTERS. *** A client holding a stamp the device is demonstrably not publishing was
    /// let through, so DB-6's client-side comparison is not connected. Nothing on a rig can produce this
    /// without breaking the guard first, which is precisely why it is a unit test.
    /// </summary>
    [Fact]
    public void A_positive_control_that_was_ADMITTED_is_GuardDidNotBehave()
    {
        var verdict = GuardCheck.Of(
            Stale(),
            Refused("A", Composed),
            Admitted("B", Wrong),
            Admitted("C", Observed));

        Assert.Equal(VerifyExit.GuardDidNotBehave, verdict.Exit);
        Assert.Contains(verdict.Faults, f => f.Contains("POSITIVE CONTROL WAS ADMITTED", StringComparison.Ordinal));
    }

    /// <summary>A guard that fires anonymously is one nobody can act on. It fired, and that is not enough.</summary>
    [Fact]
    public void A_refusal_that_does_not_name_DB6_is_GuardDidNotBehave()
    {
        var verdict = GuardCheck.Of(
            Stale(),
            Refused("A", Composed),
            Refused("B", Wrong, "something went wrong"),
            Admitted("C", Observed));

        Assert.Equal(VerifyExit.GuardDidNotBehave, verdict.Exit);
        Assert.Contains(verdict.Faults, f => f.Contains("WITHOUT NAMING DB-6", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>A guard that refuses everything is evidence about nothing.</b> This is the failure direction that
    /// gets a check switched off, and it is asserted as deliberately as its opposite.
    /// </summary>
    [Fact]
    public void An_acceptance_control_that_was_REFUSED_is_GuardDidNotBehave()
    {
        var verdict = GuardCheck.Of(
            Stale(),
            Refused("A", Composed),
            Refused("B", Wrong),
            Refused("C", Observed));

        Assert.Equal(VerifyExit.GuardDidNotBehave, verdict.Exit);
        Assert.Contains(verdict.Faults, f => f.Contains("ACCEPTANCE CONTROL WAS REFUSED", StringComparison.Ordinal));
    }

    /// <summary>
    /// The two read paths — <c>ReadControlUnverified</c> under <c>VersionCheck</c> and <c>ReadControl</c>
    /// under the client — read the same register moments apart. If they disagree, neither reading is
    /// usable, and that is a harness fault rather than a device one.
    /// </summary>
    [Fact]
    public void A_confirmed_version_with_a_REFUSED_client_A_is_GuardDidNotBehave()
    {
        var verdict = GuardCheck.Of(
            Confirmed(),
            Refused("A", Composed),
            Refused("B", Wrong),
            Admitted("C", Composed));

        Assert.Equal(VerifyExit.GuardDidNotBehave, verdict.Exit);
        Assert.Contains(verdict.Faults, f => f.Contains("two read paths disagree", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The converse disagreement: the version check measured a mismatch and the client let it through anyway.</summary>
    [Fact]
    public void A_stale_version_with_an_ADMITTED_client_A_is_GuardDidNotBehave()
    {
        var verdict = GuardCheck.Of(
            Stale(),
            Admitted("A", Composed),
            Refused("B", Wrong),
            Admitted("C", Observed));

        Assert.Equal(VerifyExit.GuardDidNotBehave, verdict.Exit);
        Assert.Contains(verdict.Faults, f => f.Contains("ADMITTED anyway", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>An UNSETTLED register is exempt from the A-admitted rule, and that exemption is tested.</b> A
    /// flapping value can legitimately equal the expected stamp at the instant control A reads it, so
    /// accusing the guard there would be a gate firing on a case outside its scope.
    /// </summary>
    [Fact]
    public void An_unsettled_version_with_an_ADMITTED_client_A_is_NOT_an_accusation()
    {
        var verdict = GuardCheck.Of(
            new VersionReport(VersionOutcome.Unsettled, Observed, Composed, 60, 0, "unsettled"),
            Admitted("A", Composed),
            Refused("B", Wrong),
            Admitted("C", Observed));

        Assert.Equal(VerifyExit.NotConfirmed, verdict.Exit);
        Assert.Empty(verdict.Faults);
    }

    // ---- the incomplete exercise, which is NOT an accusation --------------------------------------

    [Fact]
    public void A_missing_acceptance_control_is_NotEstablished_and_names_why()
    {
        var verdict = GuardCheck.Of(
            new VersionReport(VersionOutcome.Absent, 0, Composed, 3, 1, "absent"),
            Refused("A", Composed),
            Refused("B", Wrong),
            c: null);

        Assert.Equal(VerifyExit.NotEstablished, verdict.Exit);
        Assert.Contains(verdict.Faults, f => f.Contains("COULD NOT BE RUN", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>Observed misbehaviour outranks an incomplete exercise.</b> Both can be true at once, and a guard
    /// caught admitting a wrong stamp is the more serious and more actionable of the two.
    /// </summary>
    [Fact]
    public void An_admitted_positive_control_outranks_a_missing_acceptance_control()
    {
        var verdict = GuardCheck.Of(
            new VersionReport(VersionOutcome.Absent, 0, Composed, 3, 1, "absent"),
            Refused("A", Composed),
            Admitted("B", Wrong),
            c: null);

        Assert.Equal(VerifyExit.GuardDidNotBehave, verdict.Exit);
    }

    // ---- ControlAttempt itself --------------------------------------------------------------------

    [Fact]
    public void An_attempt_with_no_refusal_is_admitted_and_one_with_a_refusal_is_not()
    {
        Assert.True(Admitted("x", 1).Admitted);
        Assert.False(Refused("x", 1).Admitted);
    }

    [Fact]
    public void NamesDb6_is_false_when_the_message_does_not_carry_the_rule_id()
    {
        Assert.True(Refused("x", 1).NamesDb6);
        Assert.False(Refused("x", 1, "no rule named here").NamesDb6);
        Assert.False(Admitted("x", 1).NamesDb6);
    }
}
