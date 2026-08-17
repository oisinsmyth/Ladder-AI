namespace Harness.Verify;

/// <summary>
/// What the run established. <b>Read the report, not the code</b> — but a caller that only has the code
/// must not be misled, and in particular must never confuse <i>"the device is running a different
/// build"</i> with <i>"the guard that detects that has stopped working"</i>.
/// </summary>
public enum VerifyExit
{
    /// <summary>
    /// The version register CONFIRMED the composed build, and every DB-6 control behaved. The device is
    /// running the program these documents describe.
    /// </summary>
    Confirmed = 0,

    /// <summary>
    /// 🔴 <b>The version register did NOT confirm — and the DB-6 guard behaved correctly throughout.</b>
    ///
    /// <para><b>This is a statement about the DEVICE, not about the guard.</b> <c>Stale</c>, <c>Absent</c>,
    /// <c>WordOrderSuspect</c> and <c>Unsettled</c> all land here: the run examined everything it set out
    /// to examine and the answer was that the device is not carrying this build. It is a successful
    /// exercise of DB-6, reporting the refusal DB-6 exists to produce.</para>
    /// </summary>
    NotConfirmed = 1,

    /// <summary>Usage, or an input that could not be read. <b>Nothing was examined.</b></summary>
    NothingExamined = 2,

    /// <summary>The device fence refused the target, or no allowlist was configured. <b>No socket was opened.</b></summary>
    Refused = 3,

    /// <summary>
    /// <b>Nothing was read.</b> The transport would not open, a read threw, or a control could not be
    /// constructed at all. <i>Empty is not clean:</i> an unreachable device is not a verified one, and a
    /// control that did not run is not a control that passed.
    /// </summary>
    NothingRead = 4,

    /// <summary>
    /// The submission, binding and program could not be COMPOSED into a map and a stamp. There is nothing
    /// for the device to be checked against, so no socket was opened.
    /// </summary>
    NotComposed = 5,

    /// <summary>
    /// 🔴🔴 <b>THE GUARD DID NOT BEHAVE. This is a finding about THE HARNESS, not about the device.</b>
    ///
    /// <para>Either the POSITIVE CONTROL — a client holding a deliberately wrong stamp — was ADMITTED,
    /// which means DB-6's client-side guard is not connected; or the ACCEPTANCE CONTROL — a client
    /// holding the stamp actually read off the device this run — was REFUSED, which means the guard
    /// refuses everything and is therefore evidence about nothing.</para>
    ///
    /// <para><b>It outranks every other outcome</b>, including <see cref="NotConfirmed"/>: a refusal
    /// produced by a guard that refuses everything is not a refusal, and reporting it as one is how a
    /// broken fence keeps publishing.</para>
    /// </summary>
    GuardDidNotBehave = 6,

    /// <summary>
    /// <b>The guard was not fully exercised — and that is NOT the same as it misbehaving.</b>
    ///
    /// <para>The live case: the device publishes <c>16#00000000</c>, so the ACCEPTANCE CONTROL cannot be
    /// constructed at all (<c>MirrorClient</c> refuses a zero expected stamp, correctly). Every refusal the
    /// run observed is then equally consistent with a guard that refuses everything, so the run establishes
    /// nothing about the guard — while establishing plenty about the device.</para>
    ///
    /// <para><b>Its own code rather than <see cref="GuardDidNotBehave"/>, deliberately.</b> A zero register
    /// is a DEVICE state, not a harness fault, and filing it under "the guard did not behave" would be a
    /// gate firing outside its scope — which is noise, and noise gets switched off. <i>Conservative is not
    /// the same as correct.</i></para>
    /// </summary>
    NotEstablished = 7,
}
