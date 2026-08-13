using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DownloadProbe;

/// <summary>
/// `download-probe` — a single-purpose instrumented download.
///
/// IT EXISTS TO ANSWER ONE QUESTION: does the Openness configuration
/// <c>DataBlockReinitialization</c> get RAISED when a download restructures a STANDARD-access data
/// block? Its own message text scopes it to "different memory reserves in the online block and
/// offline block", and a standard-access DB has no reserve in either copy — so it may never fire,
/// and the API-level fail-closed guard the harness design depends on may not cover the case it was
/// chosen for. The observation is entirely PC-side: the answer is visible in the delegate, with no
/// need to read a single value out of the controller.
///
/// It is a SEPARATE BINARY from `openness-cli` deliberately. `openness-cli` provably cannot download
/// — a test walks the compiled IL of every method in that assembly and asserts no call to
/// <c>DownloadProvider.Download</c> exists — and that property is worth keeping. This is the "unsafe
/// half" that was deliberately not built, built on purpose, once, in one place, where it can be read
/// in full.
///
/// TWO THINGS A REVIEWER SHOULD CHECK IN ONE READ:
///   1. <see cref="ScratchProjectGuard"/> runs BEFORE anything else — before the log file is even
///      created, and long before Portal is contacted. A project that is not the scratch copy exits
///      <see cref="ProbeExitCodes.RefusedByPath"/> having touched nothing.
///   2. <see cref="NoActionFirstPolicy"/> is the only thing that can decide a selection, and
///      <see cref="NoActionFirstPolicy.DeniedSelections"/> is a readable list of what it will never
///      choose. There is no flag, argument or environment variable that empties that list.
///   3. <c>--disruptive</c> SHRINKS that list to <see cref="NoActionFirstPolicy.DisruptiveDeniedSelections"/>
///      — two entries, named — and adds exactly <see cref="NoActionFirstPolicy.DisruptiveAllowances"/>
///      — three entries, named. Both lists are data, printed in the banner before Portal is
///      contacted. The flag is a bare literal in one <c>switch</c> arm: there is no default, no
///      fallback, no environment variable and no retry path that can reach it.
///
/// AND ONE THING THE OUTPUT MUST ANSWER, because a green result did not: WAS ANYTHING ACTUALLY
/// TRANSFERRED? <c>DownloadResult.State</c> was <c>Success</c> on a live run that carried nothing,
/// so <see cref="TransferVerdicts"/> classifies the message tree and the answer goes in the verdict
/// line itself.
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args) =>
        Run(args, Console.Out, Console.Error, ProbeSession.Execute);

    /// <summary>
    /// The whole program, with the Portal half injected so that everything before it — which is
    /// everything that decides whether Portal is contacted at all — is testable without Portal.
    /// </summary>
    /// <param name="fenceOrigin">
    /// Where the allowlist search starts — the directory this binary was built into, in every real
    /// run. Injected ONLY so the fence's own tests can stand up a repository with a known allowlist;
    /// <see cref="Main"/> never passes it and there is no argument, flag or environment variable that
    /// reaches it. Omitting it selects the REAL discovery, so the default is the safe one: a caller
    /// that forgets this parameter gets the fence, not a hole.
    /// </param>
    internal static int Run(
        IReadOnlyList<string> args,
        TextWriter stdout,
        TextWriter stderr,
        Func<ProbeArguments, ProbeLog, ProbeOutcome> runSession,
        string? fenceOrigin = null)
    {
        var parsed = ProbeArgumentParser.Parse(args);
        if (parsed is ProbeParseResult.Failure failure)
        {
            stderr.WriteLine(failure.Message);
            return ProbeExitCodes.UsageError;
        }

        var arguments = ((ProbeParseResult.Success)parsed).Arguments;

        // FIRST, and before any side effect at all — not even a log file is created. The guard's
        // value is that it is reachable with no Portal session and no artifacts in existence.
        var fence = ScratchProjectGuard.Evaluate(
            arguments.ProjectPath, fenceOrigin ?? AppDomain.CurrentDomain.BaseDirectory);
        if (!fence.Permitted)
        {
            stderr.WriteLine(fence.Text);
            return ProbeExitCodes.RefusedByPath;
        }

        // With --json, stdout carries the machine-readable report ALONE, so the verbatim log goes to
        // stderr instead. Without it, the log is the output. Either way the file gets everything.
        var logConsole = arguments.Json ? stderr : stdout;
        var logPath = Path.Combine(arguments.LogDirectory, ProbeLog.BuildFileName(arguments.Options, DateTime.UtcNow));

        ProbeLog log;
        try
        {
            log = new ProbeLog(logConsole, logPath);
        }
        catch (Exception ex)
        {
            // The file IS the artifact. A run whose log could not be opened would produce its answer
            // in scrollback only, which is not evidence — so this is a refusal, not a downgrade.
            stderr.WriteLine($"Could not open the log file '{logPath}': {ex.GetType().Name}: {ex.Message}");
            stderr.WriteLine("The log is this tool's product, so the run is refused rather than run un-logged.");
            return ProbeExitCodes.EnvironmentError;
        }

        ProbeOutcome outcome;
        using (log)
        {
            WriteHeader(log, arguments, logPath, fence);

            try
            {
                outcome = runSession(arguments, log);
            }
            catch (Exception ex)
            {
                log.Blank();
                log.Rule("UNHANDLED EXCEPTION");

                // The chain first, ToString() after. ToString() does contain the inner exceptions,
                // but as one wrapped blob in which the reader has to find the "--->"; the structured
                // rendering names each level, its HResult and where it was thrown.
                ExceptionReport.Write(log, "every exception in the chain:", ex);
                log.Blank();
                log.Verbatim("full ToString() : ", ex.ToString());
                outcome = new ProbeOutcome(ProbeExitCodes.UnexpectedError, ExceptionReport.Summarise(ex))
                {
                    ExceptionText = ex.ToString(),
                };
            }

            WriteFooter(log, outcome, logPath);
        }

        if (arguments.Json)
        {
            stdout.WriteLine(BuildJson(arguments, outcome, logPath, log.Lines));
        }

        return outcome.ExitCode;
    }

    private static void WriteHeader(ProbeLog log, ProbeArguments arguments, string logPath, GuardDecision fence)
    {
        log.Rule("download-probe");
        log.Line($"started (UTC)  : {DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)}");
        log.Line($"project        : {arguments.ProjectPath}");

        // WHICH ENTRY VOUCHED FOR THIS PROJECT, in the artifact. A fence that only speaks when it
        // refuses leaves a permitted run unable to say what permitted it.
        log.Block(fence.Lines);
        log.Line($"options        : {arguments.Options}");
        log.Line($"device filter  : {arguments.Device ?? "(none — one PLC device must resolve)"}");
        log.Line($"pc interface   : {arguments.PcInterface ?? "(none given — REQUIRED unless the project declares exactly one)"}");
        log.Line($"target filter  : {arguments.Target ?? "(none given — required only if the PC interface has several)"}");
        log.Line($"log file       : {logPath}");
        log.Line($"policy mode    : {arguments.PolicyMode}{(arguments.Disruptive ? "   *** --disruptive WAS GIVEN ***" : " (--disruptive was NOT given)")}");
        log.Line($"throw injection: {(arguments.InjectThrow is null ? "(none — no delegate will throw)" : $"*** ARMED: {arguments.InjectThrow.ToString()!.ToUpperInvariant()} DELEGATE WILL THROW ON PURPOSE (experiment 1.7) ***")}");
        log.Blank();
        log.Line("THIS TOOL PERFORMS A REAL DEVICE DOWNLOAD. It is not a dry run and has no dry-run mode.");
        log.Blank();

        if (arguments.Disruptive)
        {
            log.Block(DisruptiveBanner);
            log.Blank();
        }

        // Printed BEFORE Portal is contacted, so a log that was going to break a download says so at
        // the top rather than in the wreckage. Same reasoning as the disruptive banner above it.
        if (arguments.InjectThrow is DelegatePhase phase)
        {
            log.Block(InjectionBanner(phase));
            log.Blank();
        }

        log.Block(DownloadOptionChoices.ConsequenceWarning(arguments.Options));
        log.Blank();
        log.Block(DownloadOptionChoices.AbortPrediction(arguments.Options, arguments.PolicyMode));
        log.Blank();

        log.Line("SELECTION POLICY (applied to every configuration the API raises):");
        if (arguments.Disruptive)
        {
            log.Line("  0. *** DISRUPTIVE ONLY *** If the (configuration, selection) pair is on the allowance");
            log.Line("     list below, choose it — AHEAD of NoAction, which is the whole point.");
        }

        log.Line("  1. If a 'NoAction' selection exists, choose it. Always.");
        log.Line("  2. Otherwise, if exactly one permitted selection is offered, take it AND SAY SO LOUDLY.");
        log.Line("  3. Otherwise leave the configuration unhandled and let the download abort.");

        if (arguments.Disruptive)
        {
            log.Line("  PERMITTED ONLY BECAUSE --disruptive WAS GIVEN (the normal policy denies every one):");
            foreach (var (configurationType, selection) in NoActionFirstPolicy.DisruptiveAllowances)
            {
                log.Line($"      {configurationType} -> {selection}");
            }
        }

        log.Line("  Never chosen, whatever else is on offer:");
        foreach (var (configurationType, selection) in NoActionFirstPolicy.DeniedSelectionsFor(arguments.PolicyMode))
        {
            log.Line($"      {configurationType} -> {selection}");
        }

        if (arguments.Disruptive)
        {
            log.Line("      ^^ SHRUNK, NOT EMPTIED. These two destroy BEYOND what the chosen download option");
            log.Line("         entails, so they stay denied here and the download aborts if either is raised.");
        }
    }

    /// <summary>
    /// EXPERIMENT 1.7's banner. Phase-specific, because the two throws have very different blast
    /// radii and a shared warning would understate one of them.
    ///
    /// The thing it must accomplish: <b>a log from an injected run must be unmistakable for a defect
    /// report.</b> Somebody reads these weeks later, and "download failed" is the most believable
    /// sentence in the file.
    /// </summary>
    private static IReadOnlyList<string> InjectionBanner(DelegatePhase phase)
    {
        var lines = new List<string>
        {
            "################################################################################",
            "###  *** EXPERIMENT 1.7: THIS RUN WILL BREAK ITS OWN DOWNLOAD ON PURPOSE. *** ###",
            "###                                                                          ###",
            "###  A DeliberateProbeInjectionException will be thrown out of the            ###",
            $"###  {phase.ToString().ToUpperInvariant(),-4} download-configuration delegate, on its FIRST invocation,      ###",
            "###  immediately after that configuration has been recorded and answered.     ###",
            "###                                                                          ###",
            "###  ANY FAILURE THIS RUN REPORTS IS CAUSED BY THIS TOOL. It is not evidence  ###",
            "###  about the project, the program, or the controller. The log says so again ###",
            "###  at the point of the throw and again in the verdict.                      ###",
            "###                                                                          ###",
            "###  WHAT IS BEING MEASURED: whether Openness propagates, wraps or SWALLOWS   ###",
            "###  an exception raised inside its own callback. A swallow would mean a      ###",
            "###  callback cannot refuse a download by failing.                            ###",
            "###                                                                          ###",
        };

        if (phase == DelegatePhase.Pre)
        {
            lines.Add("###  PRE fires BEFORE ANYTHING TRANSFERS — the least destructive real case.   ###");
            lines.Add("###  Rehearse it with --to-folder first: that path reaches this same delegate ###");
            lines.Add("###  with nothing on the wire and no CPU to strand.                           ###");
        }
        else
        {
            lines.Add("###  *** POST FIRES AFTER THE TRANSFER, AND StartModules IS RAISED IN THE    ###");
            lines.Add("###      POST DELEGATE — AFTER THE DOWNLOAD HAS ALREADY STOPPED THE MODULES. ###");
            lines.Add("###      A THROW HERE CAN LEAVE THE CPU STOPPED WITH NO ROUTE TO START IT    ###");
            lines.Add("###      FROM INSIDE THIS DOWNLOAD. There is no rehearsal for this: the      ###");
            lines.Add("###      folder overload has no post delegate. BE AT THE MACHINE. ***        ###");
        }

        lines.Add("################################################################################");
        return lines;
    }

    /// <summary>
    /// The banner. Printed before Portal is contacted, and it states the three things a reader has to
    /// know before this run happens: that the CPU will be stopped, exactly what is permitted that
    /// normally is not, and exactly what is still refused.
    /// </summary>
    private static readonly IReadOnlyList<string> DisruptiveBanner = new[]
    {
        "################################################################################",
        "###  *** --disruptive: THIS RUN IS ALLOWED TO STOP THE CPU. ***              ###",
        "###                                                                          ###",
        "###  Every previous run of this tool succeeded BY REFUSING. This one can only ###",
        "###  succeed by NOT refusing. It answers the selections that stop the modules ###",
        "###  and reinitialise data blocks, so the download can actually complete.     ###",
        "###                                                                          ###",
        "###  PERMITTED HERE, AND DENIED BY THE NORMAL POLICY:                         ###",
        "###      " + NoActionFirstPolicy.DisruptiveAllowanceSummary,
        "###      (each is a consequence the chosen --options value ALREADY entails)   ###",
        "###                                                                          ###",
        "###  *** STILL DENIED, EVEN HERE — THE HALF THAT MATTERS: ***                 ###",
        "###      " + NoActionFirstPolicy.DisruptiveDenySummary,
        "###      (these destroy BEYOND what the option entails. If either is raised   ###",
        "###       it is left unhandled and the download aborts, exactly as before.)   ###",
        "###                                                                          ###",
        "###  EXPECT THE CPU TO END UP STOPPED. There may be no automated way back.    ###",
        "###  BE AT THE MACHINE.                                                       ###",
        "################################################################################",
    };

    private static void WriteFooter(ProbeLog log, ProbeOutcome outcome, string logPath)
    {
        log.Blank();
        log.Rule("VERDICT");
        log.Line(outcome.Verdict);

        // Repeated at the very end even though the session already logged them. This is the last
        // thing on the screen, and "the CPU may be stopped" is not a fact to leave scrolled off.
        if (outcome.ClosingLines.Count > 0)
        {
            log.Blank();
            log.Block(outcome.ClosingLines);
            log.Blank();
        }

        log.Line($"exit code      : {outcome.ExitCode}{DescribeExitCode(outcome.ExitCode)}");
        log.Line($"finished (UTC) : {DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)}");
        log.Line($"log file       : {logPath}");
    }

    /// <summary>
    /// The two outcomes that must never read alike, spelled out on the exit-code line itself — the
    /// one line someone skims. 7 is the experiment succeeding; 10 is the tool failing.
    /// </summary>
    private static string DescribeExitCode(int exitCode) => exitCode switch
    {
        ProbeExitCodes.AbortedByUnhandledConfiguration =>
            "  (ABORTED BY A CONFIGURATION REFUSED ON PURPOSE — A RESULT, NOT A FAILURE)",
        ProbeExitCodes.SelectionApplyFailed =>
            "  (*** A SELECTION COULD NOT BE APPLIED — THE TOOL FAILED, THIS RUN PROVES NOTHING ***)",
        _ => string.Empty,
    };

    private static string BuildJson(ProbeArguments arguments, ProbeOutcome outcome, string logPath, IReadOnlyList<string> logLines)
    {
        var payload = new Dictionary<string, object?>
        {
            ["tool"] = "download-probe",
            ["project"] = arguments.ProjectPath,
            ["options"] = arguments.Options.ToString(),
            ["disruptive"] = arguments.Disruptive,
            ["policyMode"] = arguments.PolicyMode.ToString(),
            ["disruptiveAllowances"] = arguments.Disruptive
                ? NoActionFirstPolicy.DisruptiveAllowances.Select(a => $"{a.ConfigurationType} -> {a.Selection}").ToList()
                : new List<string>(),
            ["deniedSelections"] = NoActionFirstPolicy.DeniedSelectionsFor(arguments.PolicyMode)
                .Select(d => $"{d.ConfigurationType} -> {d.Selection}").ToList(),

            // The experiment's actual answer, as three separate keys so a consumer cannot read
            // "undetermined" as "nothing transferred": softwareLoaded is TRI-STATE and null means
            // exactly that nobody knows.
            ["transferVerdict"] = outcome.Transfer?.Kind.ToString(),
            ["softwareLoaded"] = outcome.Transfer?.SoftwareLoaded,
            ["transferHeadline"] = outcome.Transfer?.Headline,
            ["transferEvidence"] = outcome.Transfer?.Evidence,
            ["upToDatePhraseMatched"] = outcome.Transfer?.MatchedPhrase,
            ["cpuRunStateAdvisory"] = outcome.ClosingLines,
            ["pcInterface"] = arguments.PcInterface,
            ["targetInterface"] = arguments.Target,
            ["devicePath"] = outcome.DevicePath,
            ["providerFound"] = outcome.ProviderFound,
            ["downloadTarget"] = outcome.DownloadTarget,
            ["exitCode"] = outcome.ExitCode,
            ["verdict"] = outcome.Verdict,
            ["abortedByUnhandledConfiguration"] = outcome.ExitCode == ProbeExitCodes.AbortedByUnhandledConfiguration,

            // Separate keys, never one merged list: a consumer that could not tell a deliberate
            // refusal from a failed apply would draw the opposite conclusion from the same run.
            ["selectionApplyFailed"] = outcome.ExitCode == ProbeExitCodes.SelectionApplyFailed,
            ["unhandledConfigurations"] = outcome.UnhandledConfigurations,
            ["failedToApplyConfigurations"] = outcome.FailedToApplyConfigurations,
            // *** THE LOAD MANIFEST, FIRST-CLASS. *** See BuildLoadManifest: this is what `Loaded`
            // keys on, and until 2026-08-13 the only way to reach it from another process was to
            // parse the `log` array below.
            ["loadManifest"] = BuildLoadManifest(outcome),

            ["resultState"] = outcome.ResultState,
            ["resultErrorCount"] = outcome.ResultErrorCount,
            ["resultWarningCount"] = outcome.ResultWarningCount,
            ["preDownloadConfigurations"] = outcome.PreDownloadConfigurations.Select(Describe).ToList(),
            ["postDownloadConfigurations"] = outcome.PostDownloadConfigurations.Select(Describe).ToList(),
            ["exception"] = outcome.ExceptionText,
            ["logFile"] = logPath,

            // The log is embedded, not merely referenced. This tool's product is the verbatim
            // configuration text; a JSON report that only pointed at a file would be a summary of
            // the one thing that must never be summarised.
            ["log"] = logLines,
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// *** THE LOAD MANIFEST AS DATA, NOT AS PROSE. ***
    ///
    /// The manifest is the ONLY positive evidence that a download carried anything —
    /// <c>DownloadResult.State</c> was <c>Success</c> on a live run that transferred nothing — so it
    /// is the single most load-bearing value this binary produces. Its consumer is a SEPARATE
    /// PROCESS (the harness deployment gateway targets net8.0 and must never link
    /// <c>Siemens.Engineering</c>, or every harness build joins the (Path,FileHash) approval cycle),
    /// and until this existed that process recovered the manifest by RE-PARSING THE RENDERED LOG
    /// embedded below. Anything the renderer dropped was invisible to it.
    ///
    /// THE KEYS ARE <c>Ladder.Download.DownloadFeedback</c>'s OWN PROPERTY NAMES, camel-cased, and
    /// deliberately not a shape invented here: both existing consumers already speak that vocabulary,
    /// so nothing has to change to accept this.
    ///
    /// *** NULL, NEVER AN EMPTY LIST, WHEN THERE IS NO DEVICE MANIFEST. *** "Nothing was examined" and
    /// "a manifest naming nothing" are opposite findings — the second says TIA loaded nothing, the
    /// first says nobody knows — and an empty array would render them identically. Empty is not clean
    /// (FI-44).
    ///
    /// *** THREE STATES, AND THE THIRD WAS MISSING UNTIL 2026-08-13. *** <c>available</c> answers one
    /// question only: <b>is there a manifest of what reached a CONTROLLER?</b>
    ///
    /// <list type="table">
    /// <item><term><c>target: "controller"</c>, <c>available: true</c></term><description>a device
    /// download; the object list is evidence of transfer.</description></item>
    /// <item><term><c>target: "none"</c>, <c>available: false</c>, <c>resultPresent: false</c></term>
    /// <description>an abort or a throw. No <c>DownloadResult</c> exists; nobody looked.</description></item>
    /// <item><term><c>target: "folder"</c>, <c>available: false</c>, <c>resultPresent: TRUE</c></term>
    /// <description>*** THE ONE THAT WAS WRONG. *** A folder run.</description></item>
    /// </list>
    ///
    /// 🔴 <b>THIS COMMENT USED TO SAY A FOLDER RUN PRODUCES NO <c>DownloadResult</c>. IT PRODUCES ONE,
    /// AND THAT FALSEHOOD IS WHY THE DEFECT WAS INVISIBLE.</b> Measured on the first live rehearsal
    /// (2026-08-13, `GenProject1`, no wire, no CPU stop): the folder run returned a result whose
    /// message tree named <b>27 objects</b>, so this method took its <c>available: true</c> branch and
    /// reported <c>verdict: Transferred</c> — while <c>transferVerdict</c> in the same document said
    /// <c>Undetermined</c>. *** THOSE ARE EXACTLY A GATEWAY'S `Loaded` CONDITIONS, FOR A RUN THAT
    /// CONTACTED NO CONTROLLER. ***
    ///
    /// <b>Why <c>available: false</c> rather than a fourth truthy state.</b> A manifest genuinely
    /// exists for a folder run and it describes a real image, so throwing it away would be its own
    /// dishonesty — it is kept, in full, under <c>image</c>. But <c>available</c> is not "did we
    /// derive anything", it is "is there a DEVICE manifest", and for a folder run there is not.
    /// Emitting <c>true</c> with nulled device fields was considered and rejected on measurement: the
    /// existing consumer treats a missing <c>loadedObjects</c> array as "no first-class manifest" and
    /// FALLS BACK TO SCRAPING THE EMBEDDED LOG — which names the same 27 objects. The honest-looking
    /// shape would have re-created the false positive one layer down. <c>resultPresent</c> keeps the
    /// folder case distinguishable from an abort, and <c>target</c> names it outright.
    /// </summary>
    private static Dictionary<string, object?> BuildLoadManifest(ProbeOutcome outcome)
    {
        if (outcome.Destination == DownloadDestination.Folder && outcome.Feedback is { } image)
        {
            return FolderImageManifest(outcome, image);
        }

        if (outcome.Feedback is not { } feedback)
        {
            return new Dictionary<string, object?>
            {
                ["available"] = false,
                ["target"] = outcome.Destination switch
                {
                    DownloadDestination.Folder => "folder",
                    DownloadDestination.Controller => "controller",
                    _ => "none",
                },
                ["describesDeviceTransfer"] = false,
                ["source"] = "none",
                ["resultPresent"] = false,
                ["verdict"] = Ladder.Download.TransferVerdict.Undetermined.ToString(),
                ["verdictReason"] =
                    "No DownloadResult was produced, so no manifest was derived: the download aborted or threw. "
                    + "NOTHING WAS EXAMINED — which is not the same as nothing having been loaded, and must not "
                    + "be read as it.",
                ["image"] = null,
                ["loadedObjects"] = null,
                ["loadedObjectCount"] = null,
                ["loadedObjectMessageCount"] = null,
                ["duplicateLoadedObjects"] = null,
                ["nonObjectLoadSubjects"] = null,
                ["nonObjectLoadCount"] = null,
                ["transferredItemCount"] = null,
                ["runStateDisclosed"] = null,
                ["runStateTransitions"] = null,
                ["finalRunStateEvent"] = null,
                ["upToDateSignalPresent"] = null,
                ["unrecognisedMessages"] = null,
                ["unrecognisedMessageCount"] = null,
                ["anomalies"] = null,
            };
        }

        return new Dictionary<string, object?>
        {
            ["available"] = true,
            ["target"] = "controller",
            ["describesDeviceTransfer"] = true,
            ["image"] = null,

            // Which authority produced this, as RECORDED BY WHOEVER BUILT IT — never assumed here.
            // The adapter reads the live Openness objects; the log reader re-derives from a rendering
            // and is for forensics only. A consumer that cannot tell them apart cannot tell a
            // first-hand answer from a second-hand one.
            ["source"] = outcome.FeedbackSource ?? "unknown",
            ["resultPresent"] = feedback.ResultPresent,
            ["verdict"] = feedback.Verdict.ToString(),
            ["verdictReason"] = feedback.VerdictReason,

            ["loadedObjects"] = feedback.LoadedObjects,
            ["loadedObjectCount"] = feedback.LoadedObjectCount,
            ["loadedObjectMessageCount"] = feedback.LoadedObjectMessageCount,
            ["duplicateLoadedObjects"] = feedback.DuplicateLoadedObjects,

            ["nonObjectLoadSubjects"] = feedback.NonObjectLoadSubjects,
            ["nonObjectLoadCount"] = feedback.NonObjectLoadCount,
            ["transferredItemCount"] = feedback.TransferredItemCount,

            // An empty transition list means the download SAID NOTHING about run state. It does not
            // mean the CPU kept running, and `runStateDisclosed` is carried so a consumer cannot
            // read the absence as the reassurance.
            ["runStateDisclosed"] = feedback.RunStateDisclosed,
            ["runStateTransitions"] = feedback.RunStateTransitions
                .Select(t => new Dictionary<string, object?>
                {
                    ["transition"] = t.Transition.ToString(),
                    ["device"] = t.Device,
                    ["order"] = t.Order,
                    ["text"] = t.Text,
                })
                .ToList(),
            ["finalRunStateEvent"] = feedback.FinalRunStateEvent?.ToString(),

            ["upToDateSignalPresent"] = feedback.UpToDateSignalPresent,

            // Carried in full, never dropped: every download option measured so far produced a
            // vocabulary nobody predicted, and this list being non-empty is the signal that the
            // parser needs extending — which it can only be if it is reported.
            ["unrecognisedMessages"] = feedback.UnrecognisedMessages.Select(m => m.Text).ToList(),
            ["unrecognisedMessageCount"] = feedback.UnrecognisedMessageCount,

            ["anomalies"] = feedback.Anomalies,
        };
    }

    /// <summary>
    /// *** A FOLDER RUN'S REPORT: EVERY DEVICE-FACING FIELD NULL, EVERY IMAGE FACT KEPT. ***
    ///
    /// The image really was written and the objects really are named — discarding them would trade
    /// one dishonesty for another. So they move to <c>image</c>, where no key is a word a
    /// "was it loaded?" check would ever key on, and where <c>parserVerdict</c> is labelled as what
    /// the manifest parser said ABOUT THE IMAGE rather than about any controller.
    /// </summary>
    private static Dictionary<string, object?> FolderImageManifest(
        ProbeOutcome outcome, Ladder.Download.DownloadFeedback image) => new()
    {
        // FALSE, and it is the load-bearing word: there is no manifest of what reached a controller,
        // because no controller was contacted. See BuildLoadManifest's remarks for why this is not
        // `true` with nulled fields — the existing consumer would fall back to scraping the log.
        ["available"] = false,
        ["target"] = "folder",
        ["describesDeviceTransfer"] = false,

        // TRUE — and this is what separates a folder run from an abort, which is also `available:
        // false`. A result exists and was read in full; it simply says nothing about a device.
        ["resultPresent"] = image.ResultPresent,
        ["source"] = outcome.FeedbackSource ?? "unknown",

        ["verdict"] = Ladder.Download.TransferVerdict.Undetermined.ToString(),
        ["verdictReason"] =
            "*** NOTHING REACHED ANY CONTROLLER — THIS RUN WROTE AN IMAGE TO A FOLDER. *** "
            + "Download(DirectoryInfo, delegate) takes no connection, so no device was contacted and none was "
            + "even selected; that is true by construction and does not depend on reading a message. The "
            + $"{image.LoadedObjectCount} object(s) the result names describe THE IMAGE and are reported under "
            + "`image` below — no count of them is evidence that a controller holds anything. Reported as "
            + "UNDETERMINED rather than as a negative about a device, because no device was in play at all.",

        ["image"] = new Dictionary<string, object?>
        {
            ["folder"] = outcome.DownloadTarget,
            ["objects"] = image.LoadedObjects,
            ["objectCount"] = image.LoadedObjectCount,
            ["objectMessageCount"] = image.LoadedObjectMessageCount,
            ["duplicateObjects"] = image.DuplicateLoadedObjects,
            ["nonObjectSubjects"] = image.NonObjectLoadSubjects,
            ["nonObjectCount"] = image.NonObjectLoadCount,
            ["itemCount"] = image.TransferredItemCount,

            // What Ladder.Download said about these messages, kept verbatim and clearly labelled.
            // It says "Transferred" because objects are named — which is correct about the IMAGE and
            // says nothing about a device. Hiding it would be hiding the thing that used to mislead.
            ["parserVerdict"] = image.Verdict.ToString(),
            ["parserVerdictReason"] = image.VerdictReason,

            ["unrecognisedMessages"] = image.UnrecognisedMessages.Select(m => m.Text).ToList(),
            ["unrecognisedMessageCount"] = image.UnrecognisedMessageCount,
            ["anomalies"] = image.Anomalies,
        },

        // Every device-facing field is NULL. A folder run has no answer to any of these, and a null
        // is the only rendering that cannot be mistaken for one.
        ["loadedObjects"] = null,
        ["loadedObjectCount"] = null,
        ["loadedObjectMessageCount"] = null,
        ["duplicateLoadedObjects"] = null,
        ["nonObjectLoadSubjects"] = null,
        ["nonObjectLoadCount"] = null,
        ["transferredItemCount"] = null,
        ["runStateDisclosed"] = null,
        ["runStateTransitions"] = null,
        ["finalRunStateEvent"] = null,
        ["upToDateSignalPresent"] = null,
        ["unrecognisedMessages"] = null,
        ["unrecognisedMessageCount"] = null,
        ["anomalies"] = null,
    };

    private static Dictionary<string, object?> Describe(RecordedConfiguration record) => new()
    {
        ["phase"] = record.Phase,
        ["ordinal"] = record.Ordinal,
        ["type"] = record.TypeName,
        ["message"] = record.Message,
        ["availableSelections"] = record.AvailableSelections,
        ["chosenSelection"] = record.ChosenSelection,
        ["attemptedSelection"] = record.AttemptedSelection,
        ["decision"] = record.Decision.ToString(),
        ["outcome"] = record.Outcome.ToString(),
        ["reason"] = record.Reason,
        ["whyUnanswered"] = record.WasLeftUnhandled ? record.WhyUnanswered : null,
        ["appliedByRoute"] = record.AppliedByRoute,
        ["failure"] = record.FailureSummary,
    };
}
