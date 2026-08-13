namespace Harness.Device.Tests;

/// <summary>
/// The exit-code reading. <b>Nothing here consults a State string</b>, and the tests that matter are
/// the ones asserting that a green-looking code is NOT read as a pass.
/// </summary>
public class DeviceExitCodeTests
{
    [Fact]
    public void compile_all_exit_14_is_NOT_a_pass_because_it_examined_nothing()
    {
        var reading = DeviceExitCodes.Interpret(DeviceStepKind.CompileAll, DeviceExitCodes.CliNothingExamined);

        Assert.Equal(StepVerdict.NotProven, reading.Verdict);
        Assert.Contains("Empty is not clean", reading.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void compile_all_exit_11_is_a_failure_because_something_was_never_examined()
    {
        var reading = DeviceExitCodes.Interpret(DeviceStepKind.CompileAll, DeviceExitCodes.CliCompileIncomplete);

        Assert.Equal(StepVerdict.Failed, reading.Verdict);
        Assert.Contains("still flagged inconsistent", reading.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void compile_all_exit_8_is_examined_and_wrong_which_is_a_DIFFERENT_thing_from_11()
    {
        var errors = DeviceExitCodes.Interpret(DeviceStepKind.CompileAll, DeviceExitCodes.CliCompileFailed);
        var inconsistent = DeviceExitCodes.Interpret(DeviceStepKind.CompileAll, DeviceExitCodes.CliCompileIncomplete);

        Assert.Equal(StepVerdict.Failed, errors.Verdict);
        Assert.NotEqual(errors.Reason, inconsistent.Reason);
    }

    [Fact]
    public void block_layout_exit_15_is_a_failure_and_says_why_it_looks_like_wiring()
    {
        var reading = DeviceExitCodes.Interpret(DeviceStepKind.LayoutExpect, DeviceExitCodes.CliLayoutMismatch);

        Assert.Equal(StepVerdict.Failed, reading.Verdict);
        Assert.Contains("ABSENT", reading.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void import_all_exit_13_is_a_failure_even_though_nothing_errored()
    {
        var reading = DeviceExitCodes.Interpret(DeviceStepKind.Import, DeviceExitCodes.CliImportIncomplete);

        Assert.Equal(StepVerdict.Failed, reading.Verdict);
        Assert.Contains("never attempted", reading.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    /// The most important single assertion in this file: <c>download-probe</c> exiting 0 means the
    /// call returned a result with no errors, and <b>says nothing about whether anything moved</b>.
    /// <c>DownloadResult.State</c> was <c>Success</c> on a live run that carried nothing.
    /// </summary>
    [Fact]
    public void download_exit_0_is_Ok_and_explicitly_disclaims_being_evidence_of_transfer()
    {
        var reading = DeviceExitCodes.Interpret(DeviceStepKind.Download, DeviceExitCodes.ProbeCompleted);

        Assert.Equal(StepVerdict.Ok, reading.Verdict);
        Assert.Contains("NOT EVIDENCE THAT ANYTHING WAS TRANSFERRED", reading.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void download_exit_10_is_NotProven_because_the_run_is_void()
    {
        var reading = DeviceExitCodes.Interpret(DeviceStepKind.Download, DeviceExitCodes.ProbeSelectionApplyFailed);

        Assert.Equal(StepVerdict.NotProven, reading.Verdict);
        Assert.Contains("VOID", reading.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void download_exit_7_is_a_deployment_failure_even_though_it_is_a_successful_experiment()
    {
        var reading = DeviceExitCodes.Interpret(DeviceStepKind.Download, DeviceExitCodes.ProbeAbortedByUnhandledConfiguration);

        Assert.Equal(StepVerdict.Failed, reading.Verdict);
    }

    /// <summary>
    /// 8 means "compile failed" on one binary and "completed with errors" on the other; 3 means
    /// "connect timeout" on one and "refused by path" on the other. The tables are separate on
    /// purpose, and this asserts they have not been merged.
    /// </summary>
    [Fact]
    public void The_same_number_reads_differently_on_the_two_binaries()
    {
        Assert.NotEqual(
            DeviceExitCodes.Interpret(DeviceStepKind.CompileAll, 8).Reason,
            DeviceExitCodes.Interpret(DeviceStepKind.Download, 8).Reason);

        Assert.NotEqual(
            DeviceExitCodes.Interpret(DeviceStepKind.Import, 3).Reason,
            DeviceExitCodes.Interpret(DeviceStepKind.Download, 3).Reason);
    }

    [Fact]
    public void A_binary_that_never_started_is_NotProven_rather_than_failed()
    {
        var reading = DeviceExitCodes.Interpret(DeviceStepKind.Import, ProcessResult.NotStarted("no such file"));

        Assert.Equal(StepVerdict.NotProven, reading.Verdict);
        Assert.Contains("never started", reading.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void A_command_that_timed_out_is_NotProven_and_warns_that_it_may_still_hold_the_project()
    {
        var timedOut = new ProcessResult(true, true, -1, "", "", "'openness-cli.exe' did not exit within 00:45:00. It may still hold the project");
        var reading = DeviceExitCodes.Interpret(DeviceStepKind.CompileAll, timedOut);

        Assert.Equal(StepVerdict.NotProven, reading.Verdict);
        Assert.Contains("did not exit", reading.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void An_import_connect_timeout_points_at_the_approval_cycle_rather_than_at_contention()
    {
        var reading = DeviceExitCodes.Interpret(DeviceStepKind.Import, DeviceExitCodes.CliConnectTimeout);

        Assert.Equal(StepVerdict.NotProven, reading.Verdict);
        Assert.Contains("NEEDS APPROVAL", reading.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Safety_is_a_failure_on_both_binaries_and_names_hard_rule_2()
    {
        Assert.Contains("Hard rule 2", DeviceExitCodes.Interpret(DeviceStepKind.Import, DeviceExitCodes.CliSafetyRefused).Reason, StringComparison.Ordinal);
        Assert.Contains("Hard rule 2", DeviceExitCodes.Interpret(DeviceStepKind.Download, DeviceExitCodes.ProbeSafetyRefused).Reason, StringComparison.Ordinal);
    }
}
