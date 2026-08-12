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
    internal static int Run(
        IReadOnlyList<string> args,
        TextWriter stdout,
        TextWriter stderr,
        Func<ProbeArguments, ProbeLog, ProbeOutcome> runSession)
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
        if (!ScratchProjectGuard.IsScratchProject(arguments.ProjectPath))
        {
            stderr.WriteLine(ScratchProjectGuard.DescribeRefusal(arguments.ProjectPath));
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
            WriteHeader(log, arguments, logPath);

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

    private static void WriteHeader(ProbeLog log, ProbeArguments arguments, string logPath)
    {
        log.Rule("download-probe");
        log.Line($"started (UTC)  : {DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)}");
        log.Line($"project        : {arguments.ProjectPath}");
        log.Line($"options        : {arguments.Options}");
        log.Line($"device filter  : {arguments.Device ?? "(none — one PLC device must resolve)"}");
        log.Line($"pc interface   : {arguments.PcInterface ?? "(none given — REQUIRED unless the project declares exactly one)"}");
        log.Line($"target filter  : {arguments.Target ?? "(none given — required only if the PC interface has several)"}");
        log.Line($"log file       : {logPath}");
        log.Line($"policy mode    : {arguments.PolicyMode}{(arguments.Disruptive ? "   *** --disruptive WAS GIVEN ***" : " (--disruptive was NOT given)")}");
        log.Blank();
        log.Line("THIS TOOL PERFORMS A REAL DEVICE DOWNLOAD. It is not a dry run and has no dry-run mode.");
        log.Blank();

        if (arguments.Disruptive)
        {
            log.Block(DisruptiveBanner);
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
