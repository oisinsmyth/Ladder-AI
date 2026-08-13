namespace Harness.Device;

/// <summary>How one step's exit code was read, and why.</summary>
public sealed record StepReading(StepVerdict Verdict, string Reason);

/// <summary>
/// The exit-code tables of the binaries this gateway invokes, transcribed from
/// <c>OpennessCli.ExitCodes</c> and <c>DownloadProbe.ProbeExitCodes</c>.
///
/// <para><b>Two separate tables, deliberately not merged.</b> The probe's own file says so in as many
/// words: 8 means <i>compile failed</i> in one and <i>completed with errors</i> in the other, and a
/// reader who assumed they were the same would draw the opposite conclusion. So the interpretation is
/// keyed on the step KIND as well as the number.</para>
///
/// <para><b>Nothing here reads a State string.</b> A healthy project carrying standing hardware
/// warnings returns <c>Warning, errors=0</c>, and a check written against <c>State == Success</c>
/// marks it unhealthy forever. The exit codes already encode the errors-not-state distinction —
/// <c>compile</c> stopped failing on a warnings-only state on 2026-08-12 — and this table is
/// deliberately the only thing consulted.</para>
/// </summary>
public static class DeviceExitCodes
{
    // ---- openness-cli (OpennessCli.ExitCodes) ----------------------------------------------------
    public const int CliSuccess = 0;
    public const int CliUsageError = 1;
    public const int CliEnvironmentError = 2;
    public const int CliConnectTimeout = 3;
    public const int CliProjectOpenTimeout = 4;
    public const int CliUnexpectedError = 5;
    public const int CliSafetyRefused = 6;
    public const int CliCommandError = 7;
    public const int CliCompileFailed = 8;
    public const int CliSanityCheckFailed = 9;
    public const int CliNotConfirmed = 10;
    public const int CliCompileIncomplete = 11;
    public const int CliExportIncomplete = 12;
    public const int CliImportIncomplete = 13;
    public const int CliNothingExamined = 14;
    public const int CliLayoutMismatch = 15;

    // ---- download-probe (DownloadProbe.ProbeExitCodes) -------------------------------------------
    public const int ProbeCompleted = 0;
    public const int ProbeUsageError = 1;
    public const int ProbeEnvironmentError = 2;
    public const int ProbeRefusedByPath = 3;
    public const int ProbeNoProvider = 4;
    public const int ProbeNoDownloadTarget = 5;
    public const int ProbeSafetyRefused = 6;
    public const int ProbeAbortedByUnhandledConfiguration = 7;
    public const int ProbeCompletedWithErrors = 8;
    public const int ProbeUnexpectedError = 9;
    public const int ProbeSelectionApplyFailed = 10;
    public const int ProbeInjectedThrowFired = 11;
    public const int ProbeInjectionNeverFired = 12;

    /// <summary>Read one step's result. A process that never started or timed out is never Ok.</summary>
    public static StepReading Interpret(DeviceStepKind kind, ProcessResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!result.Started)
            return new StepReading(StepVerdict.NotProven, "the binary was never started, so this step says nothing: " + result.Detail);

        if (result.TimedOut)
            return new StepReading(StepVerdict.NotProven, "the command did not exit within its timeout, so its answer is unknown: " + result.Detail);

        return Interpret(kind, result.ExitCode);
    }

    /// <summary>Read one step's exit code.</summary>
    public static StepReading Interpret(DeviceStepKind kind, int exitCode) => kind switch
    {
        DeviceStepKind.Convert => Convert(exitCode),
        DeviceStepKind.Import => Import(exitCode),
        DeviceStepKind.LayoutSet or DeviceStepKind.LayoutExpect => Layout(kind, exitCode),
        DeviceStepKind.CompileAll => CompileAll(exitCode),
        DeviceStepKind.SanityCheck => SanityCheck(exitCode),
        DeviceStepKind.Download => Download(exitCode),
        _ => new StepReading(StepVerdict.NotProven, $"no exit-code table exists for step kind {kind}."),
    };

    private static StepReading Convert(int code) => code == 0
        ? new StepReading(StepVerdict.Ok, "every IR file converted to SimaticML.")
        : new StepReading(StepVerdict.Failed,
            $"converter exited {code}. Nothing was imported. The likeliest cause is FI-71's fail-closed refusal — an unresolved member type — which is a REAL defect that TIA would otherwise reject at compile after a full round trip.");

    private static StepReading Import(int code) => code switch
    {
        CliSuccess => new StepReading(StepVerdict.Ok, "every file supplied is now in the project. It is NOT compiled: everything imported is flagged inconsistent."),
        CliImportIncomplete => new StepReading(StepVerdict.Failed,
            "exit 13 = ImportIncomplete. The project does NOT contain everything supplied, INCLUDING possibly a file never attempted. A project missing a block opens, lists, and can pass a device compile."),
        CliSafetyRefused => new StepReading(StepVerdict.Failed, "exit 6 = safety content was encountered. Hard rule 2: stop and report."),
        CliConnectTimeout => new StepReading(StepVerdict.NotProven,
            "exit 3 = the Portal connect timed out. On a freshly rebuilt binary read this as NEEDS APPROVAL, not as contention: TIA whitelists by (Path, FileHash) and an unapproved build's attach hangs until the timeout."),
        CliProjectOpenTimeout => new StepReading(StepVerdict.NotProven, "exit 4 = the project did not open within the timeout. TIA project open is slow; nothing here says the project is bad."),
        _ => new StepReading(StepVerdict.Failed, $"import-all exited {code}. Nothing further was attempted."),
    };

    private static StepReading Layout(DeviceStepKind kind, int code) => code switch
    {
        CliSuccess => new StepReading(StepVerdict.Ok,
            kind == DeviceStepKind.LayoutSet
                ? "the block was set to Standard and the layout was READ BACK and matched."
                : "the block reads back Standard."),
        CliLayoutMismatch => new StepReading(StepVerdict.Failed,
            "exit 15 = the block's memory layout is NOT Standard. An optimized block is not an error on classic S7comm — it is simply ABSENT, and the read fails at the first DATA access rather than at connect, which presents as a wiring problem."),
        _ => new StepReading(StepVerdict.Failed, $"block-layout exited {code}."),
    };

    private static StepReading CompileAll(int code) => code switch
    {
        CliSuccess => new StepReading(StepVerdict.Ok, "every inconsistent type and block compiled with no errors."),
        CliCompileFailed => new StepReading(StepVerdict.Failed, "exit 8 = at least one item compiled WITH ERRORS. Examined and wrong."),
        CliCompileIncomplete => new StepReading(StepVerdict.Failed,
            "exit 11 = something is still flagged inconsistent. Not examined at all — which is the count that looks like a pass."),
        CliNothingExamined => new StepReading(StepVerdict.NotProven,
            "exit 14 = NOTHING EXAMINED. The work set was empty, so the run compiled zero items and its silence is not evidence about the project. "
            + "An item that compiled WITH ERRORS is still flagged CONSISTENT and errors do not survive the process, so the run right after a failed one is exactly the one that examines nothing. Empty is not clean."),
        CliConnectTimeout => new StepReading(StepVerdict.NotProven, "exit 3 = the Portal connect timed out; the gate did not run."),
        _ => new StepReading(StepVerdict.Failed, $"compile-all exited {code}."),
    };

    private static StepReading SanityCheck(int code) => code switch
    {
        CliSuccess => new StepReading(StepVerdict.Ok, "BLOCKS and TYPES both report INCONSISTENT: 0, and compile health is clean."),
        CliSanityCheckFailed => new StepReading(StepVerdict.Failed, "exit 9 = the project is not healthy. Read both the BLOCKS: line and the TYPES: line — a freshly imported UDT used to be invisible here (FI-62)."),
        CliConnectTimeout => new StepReading(StepVerdict.NotProven, "exit 3 = the Portal connect timed out; the check did not run."),
        _ => new StepReading(StepVerdict.Failed, $"sanity-check exited {code}."),
    };

    private static StepReading Download(int code) => code switch
    {
        ProbeCompleted => new StepReading(StepVerdict.Ok,
            "the download ran to a DownloadResult reporting no errors. *** THIS IS NOT EVIDENCE THAT ANYTHING WAS TRANSFERRED *** — State was Success on a live run that carried nothing. The load manifest decides that, and it is read separately."),
        ProbeCompletedWithErrors => new StepReading(StepVerdict.Failed,
            "exit 8 = the download ran to a DownloadResult that reports errors. A completed experiment, and not a deployment."),
        ProbeAbortedByUnhandledConfiguration => new StepReading(StepVerdict.Failed,
            "exit 7 = the download ABORTED because a configuration was left unhandled — its only selections were on the deny list, or there was no obvious answer. "
            + "For the probe that is a successful experiment; for a deployment it means nothing was transferred. The log names which configuration."),
        ProbeSelectionApplyFailed => new StepReading(StepVerdict.NotProven,
            "exit 10 = a permitted selection was chosen and the API REFUSED THE SET. The run is VOID: the tool stopped answering after the first configuration it could not apply, so nothing was learned and the device state is unknown."),
        ProbeRefusedByPath => new StepReading(StepVerdict.Failed,
            "exit 3 = the project is not the scratch project. Portal was NOT contacted. If this fires, the gateway's own fence is looser than the probe's and that is the defect to fix."),
        ProbeSafetyRefused => new StepReading(StepVerdict.Failed,
            "exit 6 = safety content is present on the device. Download is device-level, so no download of an F-capable PLC excludes its safety program. Hard rule 2: stop and report."),
        ProbeNoProvider => new StepReading(StepVerdict.NotProven, "exit 4 = no DownloadProvider was obtainable. The run describes nothing; empty is not clean."),
        ProbeNoDownloadTarget => new StepReading(StepVerdict.Failed,
            "exit 5 = no unambiguous connection target. Either none is configured, or several are and --target chose between none of them."),
        ProbeInjectedThrowFired => new StepReading(StepVerdict.NotProven,
            "exit 11 = a DELIBERATE injected throw fired. This gateway never passes an injection flag, so reaching this means something else invoked the probe."),
        ProbeInjectionNeverFired => new StepReading(StepVerdict.NotProven, "exit 12 = an injection was armed and never fired. Nothing was learned."),
        ProbeUsageError => new StepReading(StepVerdict.Failed,
            "exit 1 = the probe rejected its own arguments. A flag this gateway emits is not one that binary accepts — check the argument vector against DownloadProbe/ProbeArguments.cs, not against any README."),
        _ => new StepReading(StepVerdict.Failed, $"download-probe exited {code}."),
    };
}
