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
        log.Blank();
        log.Line("THIS TOOL PERFORMS A REAL DEVICE DOWNLOAD. It is not a dry run and has no dry-run mode.");
        log.Blank();

        log.Block(DownloadOptionChoices.ConsequenceWarning(arguments.Options));
        log.Blank();
        log.Block(DownloadOptionChoices.AbortPrediction(arguments.Options));
        log.Blank();

        log.Line("SELECTION POLICY (applied to every configuration the API raises):");
        log.Line("  1. If a 'NoAction' selection exists, choose it. Always.");
        log.Line("  2. Otherwise, if exactly one permitted selection is offered, take it AND SAY SO LOUDLY.");
        log.Line("  3. Otherwise leave the configuration unhandled and let the download abort.");
        log.Line("  Never chosen, whatever else is on offer:");
        foreach (var (configurationType, selection) in NoActionFirstPolicy.DeniedSelections)
        {
            log.Line($"      {configurationType} -> {selection}");
        }
    }

    private static void WriteFooter(ProbeLog log, ProbeOutcome outcome, string logPath)
    {
        log.Blank();
        log.Rule("VERDICT");
        log.Line(outcome.Verdict);
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
