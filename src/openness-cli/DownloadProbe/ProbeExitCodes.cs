namespace DownloadProbe;

/// <summary>
/// This binary's own exit-code table. Deliberately NOT shared with <c>openness-cli</c>'s
/// <c>ExitCodes</c>: the two programs answer different questions, and a reader who assumed 8 meant
/// "compile failed" here would be wrong. Codes 0-3 and 6 are kept aligned with the CLI's where the
/// meaning genuinely is the same, so muscle memory does not mislead.
///
/// The distinction this table exists for is <see cref="AbortedByUnhandledConfiguration"/> versus
/// everything else. An abort is a SUCCESSFUL EXPERIMENT — it means the fail-closed posture held and
/// the download did not proceed — and it must never be readable as "the tool failed to start".
/// </summary>
internal static class ProbeExitCodes
{
    /// <summary>The download ran to a <c>DownloadResult</c> and reported no errors.</summary>
    internal const int Completed = 0;

    /// <summary>Arguments were missing, unknown, or not one of the three option literals.</summary>
    internal const int UsageError = 1;

    /// <summary>Siemens.Engineering could not be located. Nothing was attempted.</summary>
    internal const int EnvironmentError = 2;

    /// <summary>
    /// The project path is not the scratch project. Refused before Portal was contacted — the whole
    /// point of the guard is that this code is reachable without a Portal session existing.
    /// </summary>
    internal const int RefusedByPath = 3;

    /// <summary>
    /// No <c>DownloadProvider</c> was obtainable anywhere in the device's tree. The run describes
    /// nothing; empty is not clean.
    /// </summary>
    internal const int NoProvider = 4;

    /// <summary>
    /// A provider exists but no unambiguous connection target could be taken from the project's own
    /// configuration — none configured, or several and no <c>--target</c> to choose between them.
    /// Refusing beats guessing which device to write to.
    /// </summary>
    internal const int NoDownloadTarget = 5;

    /// <summary>Safety content is present on the device. Hard rule 2; download is device-level.</summary>
    internal const int SafetyRefused = 6;

    /// <summary>
    /// *** A RESULT, NOT A FAILURE. *** The download aborted because a configuration was left
    /// unhandled — either its only selections were on the deny list, or there was no
    /// <c>NoAction</c> and no single obvious answer. The log names which configuration, and this
    /// code exists so a script can tell that apart from a tool that never got started.
    /// </summary>
    internal const int AbortedByUnhandledConfiguration = 7;

    /// <summary>
    /// The download ran to a <c>DownloadResult</c> that reports errors. Still a completed
    /// experiment — the configuration log is valid — but distinct from <see cref="Completed"/>.
    /// </summary>
    internal const int CompletedWithErrors = 8;

    /// <summary>Anything else, including a Portal connect/open failure. Logged in full.</summary>
    internal const int UnexpectedError = 9;

    /// <summary>
    /// *** THE OPPOSITE OF <see cref="AbortedByUnhandledConfiguration"/>, AND THE REASON IT EXISTS. ***
    ///
    /// The policy chose a permitted selection and the API REFUSED THE SET. The run is void: nothing
    /// was learned about which configurations a download raises, because the tool stopped answering
    /// after the first one it could not answer, and a later configuration could not have been raised
    /// even if it would have been.
    ///
    /// Until this code existed both outcomes exited 7 with a summary that read the same, so a broken
    /// tool and a working guard were indistinguishable to a script — and, on the run that prompted
    /// this, to a reader as well: the summary printed the policy's "a NoAction selection exists, so
    /// it is chosen" directly beneath an ANSWER line saying the apply had failed.
    ///
    /// It takes precedence over every other outcome, including a download that then completed:
    /// a completed download whose configurations were not all answered as intended is not a
    /// successful experiment, it is an unexplained one.
    /// </summary>
    internal const int SelectionApplyFailed = 10;

    /// <summary>
    /// *** A DELIBERATE FAILURE. EXPERIMENT 1.7 RAN. ***
    ///
    /// <c>--throw-from-pre-delegate</c>/<c>--throw-from-post-delegate</c> was given, the throw fired,
    /// and the run recorded what Openness did with it. Its own code so that <b>no script and no
    /// reader can ever mistake an injected failure for a real one</b> — which is the whole risk this
    /// feature carries. Never returned unless an injection flag was spelled exactly.
    /// </summary>
    internal const int InjectedThrowFired = 11;

    /// <summary>
    /// An injection was armed and <b>the delegate was never invoked</b>, so the throw never happened.
    /// Empty is not clean: this is emphatically not a pass, and it is not a failure of the download
    /// either — the experiment did not run. Distinct from <see cref="InjectedThrowFired"/> because
    /// the two answer opposite questions about whether anything was learned.
    /// </summary>
    internal const int InjectionNeverFired = 12;
}
