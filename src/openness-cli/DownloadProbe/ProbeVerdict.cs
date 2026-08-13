namespace DownloadProbe;

/// <summary>
/// THE PRECEDENCE CHAIN, hoisted out of <c>ProbeSession</c> so it can be tested at all.
///
/// 🔴 **Why it exists (2026-08-13).** `--throw-from-post-delegate` was ARMED, the download raised
/// ZERO post configurations, the delegate was NEVER INVOKED, the download completed — <b>and the tool
/// exited 0</b>, where its own contract says <c>12 = armed and the delegate was never invoked</c>.
/// The lane caught it by reading the configuration list; the exit code said everything was fine.
///
/// The cause was **a missing arm, not a wrong one**: the completion path tested "did it fire" and had
/// no branch at all for "armed and never fired". And the reason no test caught *that* is that the
/// decision lived inline in a method which cannot run without Portal, a device and a download — so
/// the only assertable part was <c>ThrowInjection.Classify</c>, which was already correct. **A guard
/// whose non-firing has no test is the seventh gap of this shape found across the lanes**, and the
/// fix for the class is this: make the decision a value, decided by a pure function, that a test can
/// interrogate without a rig.
///
/// The order is the whole content, and each step outranks the one below it for a stated reason.
/// </summary>
internal static class ProbeVerdict
{
    /// <summary>
    /// The final exit code for a run that reached a download, or <c>null</c> to keep whatever the
    /// download itself earned.
    /// </summary>
    /// <param name="failedToApplyCount">
    /// Configurations the policy answered and the API refused. <b>Outranks everything</b>: the tool
    /// intended to answer them and could not, so the run proves nothing at all — a strictly stronger
    /// statement than "this particular experiment did not run".
    /// </param>
    /// <param name="injectionArmed">An injection flag was spelled exactly. Never true by default.</param>
    /// <param name="injectionFired">The throw actually happened.</param>
    internal static int? Decide(int failedToApplyCount, bool injectionArmed, bool injectionFired)
    {
        if (failedToApplyCount > 0)
        {
            return ProbeExitCodes.SelectionApplyFailed;
        }

        // EMPTY IS NOT CLEAN. An experiment that did not run is not a pass, and it is not a failure
        // of the download either — it is its own finding, and the one the contract already promised.
        if (injectionArmed && !injectionFired)
        {
            return ProbeExitCodes.InjectionNeverFired;
        }

        return null;
    }
}
