using System;
using System.IO;
using System.Text.Json;
using OpennessCli;
using OpennessCli.Cli;
using OpennessCli.Model;
using Xunit;

namespace OpennessCli.Tests;

/// <summary>
/// What `hmi-compile` decides, and on what.
///
/// It carried the SAME defect `compile` did until 2026-08-12: the verdict keyed on
/// <c>CompileState</c>, so a result with warnings and zero errors exited 8 (CompileFailed). On the PLC
/// side that was MEASURED — a project carrying a permanent hardware warning returned a non-Success
/// state on every otherwise-clean compile, and a caller branching on the exit code read every success
/// as a failure. `hmi-compile` was left alone at the time because no HMI project had demonstrated it;
/// leaving a known defect because it has not bitten yet is how it bites later. Both paths now key on
/// <see cref="Program.EffectiveErrorCount"/> — the same fail-closed
/// <c>max(ErrorCount, Error messages in the tree)</c>, one implementation, not two.
///
/// WHAT DELIBERATELY DOES **NOT** TRANSFER FROM `compile`: the post-compile consistency read-back.
/// `compile` re-resolves its block/type and reads <c>IsConsistent</c> back, exiting 11 when the item
/// is still inconsistent. There is no HMI equivalent to read — confirmed against the V20 API surface
/// (2026-08-12): <c>IsConsistent</c> exists on exactly four types, all PLC-side (<c>PlcBlock</c>,
/// <c>PlcType</c>, <c>PlcForceTable</c>, <c>PlcWatchTable</c>), and on NOTHING in
/// <c>Siemens.Engineering.Hmi.*</c> or <c>Siemens.Engineering.HmiUnified.*</c>. So `hmi-compile`
/// success is genuinely WEAKER than `compile` success, and the tests at the bottom of this file assert
/// the ABSENCE of a consistency field rather than its presence — an always-null field reported as
/// though it were a check is the failure mode being guarded against.
///
/// Everything here runs against <see cref="FakeGateway"/>; no test in this file needs a live Portal
/// session.
/// </summary>
public class HmiCompileVerdictTests
{
    // ---- the verdict keys on errors, not on State ---------------------------------------------

    /// <summary>
    /// THE regression guard. <c>State = Warning</c> with <c>ErrorCount = 0</c> must exit 0. Without
    /// this test the defect returns the first time someone "tidies" the verdict back into the
    /// obvious-looking `State != Success` check.
    /// </summary>
    [Fact]
    public void WarningStateWithNoErrors_ExitsSuccess()
    {
        var gateway = new FakeGateway
        {
            HmiCompileResult = new CompileResult(
                CompileState.Warning,
                ErrorCount: 0,
                WarningCount: 1,
                Messages: new[]
                {
                    new CompileMessage(CompileState.Warning, "A screen item is outside the visible area", "HMI_1/Screen_1"),
                }),
        };

        var (exitCode, _, _) = CaptureConsole(() =>
            Program.RunHmiCompile(gateway, HmiOptions(), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.Success, exitCode);
    }

    /// <summary>
    /// The same pass says out loud that it was not a CLEAN compile. Swallowing the warnings would
    /// trade one wrong reading for another: "compiled with warnings" and "compiled clean" are
    /// different facts, and only the exit code was ever wrong.
    /// </summary>
    [Fact]
    public void WarningStateWithNoErrors_StillReportsTheStateAndTheWarnings()
    {
        var gateway = new FakeGateway
        {
            HmiCompileResult = new CompileResult(
                CompileState.Warning,
                ErrorCount: 0,
                WarningCount: 1,
                Messages: new[] { new CompileMessage(CompileState.Warning, "screen item warning", "HMI_1/Screen_1") }),
        };

        var (exitCode, stdout, _) = CaptureConsole(() =>
            Program.RunHmiCompile(gateway, HmiOptions(), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("STATE: Warning", stdout);
        Assert.Contains("WARNINGS: 1", stdout);
        Assert.Contains("screen item warning", stdout);
        Assert.Contains("PASSED WITH WARNINGS", stdout);
    }

    [Theory]
    [InlineData(CompileState.Information)]
    [InlineData(CompileState.Warning)]
    public void NonSuccessStateWithNoErrors_ExitsSuccess(CompileState state)
    {
        var gateway = new FakeGateway
        {
            HmiCompileResult = new CompileResult(
                state, ErrorCount: 0, WarningCount: 3, Messages: Array.Empty<CompileMessage>()),
        };

        var (exitCode, _, _) = CaptureConsole(() =>
            Program.RunHmiCompile(gateway, HmiOptions(), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.Success, exitCode);
    }

    /// <summary>Errors still fail, whatever <c>State</c> says — including a state of Success.</summary>
    [Fact]
    public void ErrorsFail_EvenWhenTheStateClaimsSuccess()
    {
        var gateway = new FakeGateway
        {
            HmiCompileResult = new CompileResult(
                CompileState.Success, ErrorCount: 2, WarningCount: 0, Messages: Array.Empty<CompileMessage>()),
        };

        var (exitCode, _, _) = CaptureConsole(() =>
            Program.RunHmiCompile(gateway, HmiOptions(), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.CompileFailed, exitCode);
    }

    /// <summary>
    /// Fail-closed on the two counts disagreeing, identically to `compile` — this is the same
    /// <see cref="Program.EffectiveErrorCount"/>, not a second copy of the rule. The formatter already
    /// warns that the compiler's aggregates are unreliable when they contradict the message tree; a
    /// verdict trusting only the aggregate would be talked out of failing by the count that read zero.
    /// </summary>
    [Fact]
    public void ErrorMessagesWithAZeroAggregate_StillFail()
    {
        var gateway = new FakeGateway
        {
            HmiCompileResult = new CompileResult(
                CompileState.Warning,
                ErrorCount: 0,
                WarningCount: 0,
                Messages: new[] { new CompileMessage(CompileState.Error, "Tag 'Foo' is not resolvable", "HMI_1/Screen_1") }),
        };

        var (exitCode, _, _) = CaptureConsole(() =>
            Program.RunHmiCompile(gateway, HmiOptions(), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.CompileFailed, exitCode);
    }

    /// <summary>The unambiguous case: nothing wrong, nothing to caveat, exit 0 with no warnings banner.</summary>
    [Fact]
    public void CleanCompile_ExitsSuccessWithoutTheWarningsBanner()
    {
        var gateway = new FakeGateway
        {
            HmiCompileResult = new CompileResult(
                CompileState.Success, ErrorCount: 0, WarningCount: 0, Messages: Array.Empty<CompileMessage>()),
        };

        var (exitCode, stdout, _) = CaptureConsole(() =>
            Program.RunHmiCompile(gateway, HmiOptions(), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.DoesNotContain("PASSED WITH WARNINGS", stdout);
    }

    /// <summary>The device filter reaches the gateway — `--device` is how an ambiguous project is resolved.</summary>
    [Fact]
    public void DeviceFilter_IsPassedThrough()
    {
        var gateway = new FakeGateway
        {
            HmiCompileResult = new CompileResult(
                CompileState.Success, ErrorCount: 0, WarningCount: 0, Messages: Array.Empty<CompileMessage>()),
        };

        CaptureConsole(() =>
            Program.RunHmiCompile(gateway, HmiOptions() with { Device = "HMI_1" }, timeoutOpenSeconds: 1));

        Assert.Equal(1, gateway.CompileHmiCalls);
        Assert.Equal("HMI_1", gateway.LastHmiCompileDeviceFilter);
    }

    // ---- the asymmetry with `compile`, asserted as an ABSENCE ---------------------------------

    /// <summary>
    /// No consistency read-back exists for HMI, so none is reported. Asserting the absence is the
    /// point: the tempting "symmetry" fix is a `consistentAfterCompile` field that is always null,
    /// which reads to a consumer as a check that ran and found nothing wrong. There is no such check.
    /// </summary>
    [Fact]
    public void HmiResult_ReportsNoConsistencyLine_BecauseThereIsNothingToRead()
    {
        var gateway = new FakeGateway
        {
            HmiCompileResult = new CompileResult(
                CompileState.Success, ErrorCount: 0, WarningCount: 0, Messages: Array.Empty<CompileMessage>()),
        };

        var (exitCode, stdout, stderr) = CaptureConsole(() =>
            Program.RunHmiCompile(gateway, HmiOptions(), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.DoesNotContain("CONSISTENT:", stdout);
        Assert.Equal(string.Empty, stderr);
    }

    /// <summary>
    /// And in JSON the field is null — "the question was not asked", the same value a whole-device PLC
    /// compile carries. A `false` here would be a claim about an inconsistency nobody can observe.
    /// </summary>
    [Fact]
    public void HmiJson_CarriesANullConsistencyField()
    {
        var gateway = new FakeGateway
        {
            HmiCompileResult = new CompileResult(
                CompileState.Success, ErrorCount: 0, WarningCount: 0, Messages: Array.Empty<CompileMessage>()),
        };

        var (_, stdout, _) = CaptureConsole(() =>
            Program.RunHmiCompile(gateway, HmiOptions() with { Json = true }, timeoutOpenSeconds: 1));

        using var doc = JsonDocument.Parse(stdout);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("consistentAfterCompile").ValueKind);
    }

    /// <summary>
    /// A warnings pass says why it cannot be strengthened. `compile` can fall back on the consistency
    /// read-back when the state is unhelpful; here the compiler's own word is all there is, and a
    /// reader comparing the two commands' output should be told that rather than left to assume.
    /// </summary>
    [Fact]
    public void WarningsPass_SaysThereIsNoConsistencyReadBackToFallBackOn()
    {
        var gateway = new FakeGateway
        {
            HmiCompileResult = new CompileResult(
                CompileState.Warning, ErrorCount: 0, WarningCount: 2, Messages: Array.Empty<CompileMessage>()),
        };

        var (_, stdout, _) = CaptureConsole(() =>
            Program.RunHmiCompile(gateway, HmiOptions(), timeoutOpenSeconds: 1));

        Assert.Contains("IsConsistent", stdout);
        Assert.Contains("PLC-only", stdout);
    }

    // ---- helpers ------------------------------------------------------------------------------

    private static CompileCommandOptions HmiOptions() => new(
        ProjectIdentifier: "C:\\proj\\My.ap20",
        Device: null,
        Block: null,
        Type: null,
        Json: false,
        TiaInstallOverride: null,
        TimeoutConnectSeconds: ArgumentParser.DefaultTimeoutConnectSeconds,
        TimeoutOpenSeconds: ArgumentParser.DefaultTimeoutOpenSeconds);

    private static (int ExitCode, string StdOut, string StdErr) CaptureConsole(Func<int> action)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            var exitCode = action();
            return (exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}
