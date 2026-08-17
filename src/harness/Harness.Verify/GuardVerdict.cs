using Harness.Wire;

namespace Harness.Verify;

/// <summary>The verdict and the reasons behind it. Faults are carried, never collapsed into the code.</summary>
public sealed record GuardVerdict(VerifyExit Exit, IReadOnlyList<string> Faults)
{
    public bool GuardBehaved => Exit != VerifyExit.GuardDidNotBehave;
}

/// <summary>
/// 🔴 <b>Whether the DB-6 guard BEHAVED, decided as a pure function of the three attempts.</b>
///
/// <para><b>Why this is not inline in <see cref="VerifyRun"/>.</b> The interesting cases — the positive
/// control being ADMITTED, the acceptance control being REFUSED — are exactly the ones a live rig cannot
/// be made to produce, because producing them means breaking <c>MirrorClient</c>. Left inline, the only
/// way to exercise them would be to hand-edit the production guard and run the binary; and <i>a check
/// whose exercise requires editing the check will not be exercised.</i> As a function it takes fabricated
/// attempts, so both failure directions are ordinary tests.</para>
///
/// <para><b>Both directions are asserted, not just the alarming one.</b> A judge that returns
/// <see cref="VerifyExit.GuardDidNotBehave"/> for everything would satisfy every test that only looks for
/// the alarm — and a gate that fires outside its scope is noise, and noise gets switched off.</para>
/// </summary>
public static class GuardCheck
{
    /// <param name="a">The composition's own client — reported, and deliberately NOT a criterion.</param>
    /// <param name="b">The positive control: a deliberately wrong stamp. Must be refused, by name.</param>
    /// <param name="c">
    /// The acceptance control: the stamp the device itself published. Must be admitted. <b>Null means it
    /// could not be constructed at all</b> (a device publishing zero), which is a fault in its own right —
    /// a control that did not run is not a control that passed.
    /// </param>
    public static GuardVerdict Of(VersionReport report, ControlAttempt a, ControlAttempt b, ControlAttempt? c)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        var faults = new List<string>();

        if (b.Admitted)
        {
            faults.Add(
                $"THE POSITIVE CONTROL WAS ADMITTED. A client holding 16#{b.ExpectedStamp:X8} read the control region off a device "
                + $"publishing 16#{report.Observed:X8}, so MirrorClient.ReadControl's DB-6 comparison is not connected. Every address "
                + "that client holds is a guess, and nothing stopped it being used.");
        }
        else if (!b.NamesDb6)
        {
            faults.Add(
                "THE POSITIVE CONTROL WAS REFUSED WITHOUT NAMING DB-6. The guard fired, and its reader cannot tell which rule fired "
                + "or what to do about it. A refusal nobody can act on is the failure mode a fence decays through.");
        }

        if (c is not null && !c.Admitted)
        {
            faults.Add(
                $"THE ACCEPTANCE CONTROL WAS REFUSED. A client whose expected stamp is the value the device ITSELF published "
                + $"(16#{report.Observed:X8}) was turned away, so this guard refuses every case and its refusals are evidence about "
                + "nothing. A gate that fires outside its scope is noise, and noise gets switched off.");
        }

        // 🔴 *** A CONTRADICTION BETWEEN THE TWO READ PATHS IS ITS OWN FAULT. *** VersionCheck reads the
        // register through ReadControlUnverified and the client reads it through ReadControl; they are the
        // same FC03 over the same map, moments apart. If one says the stamps match and the other admits or
        // refuses the opposite way round, then one of the two paths is wrong and neither reading is usable
        // — which is not a statement about the device, and must not be reported as one.
        if (report.Confirmed && !a.Admitted)
        {
            faults.Add(
                $"VersionCheck CONFIRMED 16#{report.Expected:X8} and the client holding that same stamp was REFUSED. The two read "
                + "paths disagree about one register, so neither reading is usable. This is a fault in the harness, not in the device.");
        }

        if (!report.Confirmed && report.Outcome != VersionOutcome.Unsettled && a.Admitted)
        {
            faults.Add(
                $"VersionCheck reported {report.Outcome} (observed 16#{report.Observed:X8}, expected 16#{report.Expected:X8}) and the "
                + "client holding the expected stamp was ADMITTED anyway. The guard did not fire on a mismatch the version check had "
                + "just measured.");
        }

        // *** OBSERVED MISBEHAVIOUR OUTRANKS AN INCOMPLETE EXERCISE. *** Both can be true at once, and a
        // guard caught admitting a wrong stamp is the more serious and more actionable of the two.
        if (faults.Count > 0)
            return new GuardVerdict(VerifyExit.GuardDidNotBehave, faults);

        // *** NOT THE SAME AS MISBEHAVING, AND IT GETS ITS OWN CODE FOR THAT REASON. *** A zero register is
        // a DEVICE state; filing it under "the guard did not behave" would be a gate firing outside its
        // scope. What is true is that nothing here shows the guard ADMITTING, so nothing here separates a
        // working guard from one that refuses everything. Empty is not clean.
        if (c is null)
        {
            return new GuardVerdict(VerifyExit.NotEstablished, new[]
            {
                "THE ACCEPTANCE CONTROL COULD NOT BE RUN. The device published 16#00000000 and MirrorClient refuses to hold a zero "
                + "expected stamp - correctly, since unwritten bit memory reads as zero and such a client could not tell a running "
                + "program from an absent one. So the guard was NOT fully exercised: every refusal above is equally consistent with "
                + "a guard that refuses everything. That is a statement about what THIS RUN established, not about the guard.",
            });
        }

        return new GuardVerdict(
            report.Confirmed ? VerifyExit.Confirmed : VerifyExit.NotConfirmed,
            Array.Empty<string>());
    }
}
