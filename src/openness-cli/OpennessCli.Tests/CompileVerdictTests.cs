using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using OpennessCli;
using OpennessCli.Cli;
using OpennessCli.Model;
using Xunit;

namespace OpennessCli.Tests;

/// <summary>
/// What `compile` decides, and on what. Two measured defects (2026-08-12) live here as regression
/// guards, both of which produced a WRONG-DIRECTION verdict on a real project:
///
/// 1. THE VERDICT USED TO KEY ON <c>State</c>. Every per-block compile against the live scratch
///    project exited 8 with `errors: 0`, because the project carries a permanent hardware warning
///    ("Inputs or outputs are used that do not exist in the configured hardware") and Compile()
///    returns a non-Success STATE for it on every block. A caller branching on the exit code read
///    every clean compile as a failure. `compile-all` already keyed on ErrorCount for exactly this
///    reason; `compile` did not.
///
/// 2. A CLEAN PER-BLOCK COMPILE DID NOT IMPLY EXPORTABLE. A compile reported "Block was successfully
///    compiled", errors 0, and the block was still flagged IsConsistent=false because a block it
///    referenced did not exist — after which TIA refused to export it. That is the converse of FI-52
///    (a DEVICE compile leaving OTHER blocks inconsistent): a PER-BLOCK compile leaving ITS OWN
///    block inconsistent.
///
/// Everything here runs against <see cref="FakeGateway"/>. No test in this file needs a live Portal
/// session, which is the point — a verdict is a decision about a result, and the results that expose
/// these two defects are ones a fake can hand over on demand.
/// </summary>
public class CompileVerdictTests
{
    // ---- defect 1: the verdict keys on errors, not on State ---------------------------------

    /// <summary>
    /// THE regression guard. A result with <c>State = Warning</c> and <c>ErrorCount = 0</c> must
    /// exit 0. Without this test the defect returns the first time someone "tidies" the verdict
    /// logic back into the obvious-looking `State != Success` check.
    /// </summary>
    [Fact]
    public void PerBlock_WarningStateWithNoErrors_ExitsSuccess()
    {
        var gateway = new FakeGateway
        {
            BlockCompileResult = new CompileResult(
                CompileState.Warning,
                ErrorCount: 0,
                WarningCount: 1,
                Messages: new[]
                {
                    new CompileMessage(
                        CompileState.Warning,
                        "Inputs or outputs are used that do not exist in the configured hardware",
                        "PLC_1"),
                },
                ConsistentAfterCompile: true),
        };

        var (exitCode, _, _) = CaptureConsole(() =>
            Program.RunCompile(gateway, BlockOptions(), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.Success, exitCode);
    }

    /// <summary>
    /// The same pass says out loud that it was not a CLEAN compile. Swallowing the warnings would
    /// trade one wrong reading for another: "compiled with warnings" and "compiled clean" are
    /// different facts, and only the exit code was ever wrong.
    /// </summary>
    [Fact]
    public void PerBlock_WarningStateWithNoErrors_StillReportsTheStateAndTheWarnings()
    {
        var gateway = new FakeGateway
        {
            BlockCompileResult = new CompileResult(
                CompileState.Warning,
                ErrorCount: 0,
                WarningCount: 1,
                Messages: new[] { new CompileMessage(CompileState.Warning, "hardware warning", "PLC_1") },
                ConsistentAfterCompile: true),
        };

        var (exitCode, stdout, _) = CaptureConsole(() =>
            Program.RunCompile(gateway, BlockOptions(), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("STATE: Warning", stdout);
        Assert.Contains("WARNINGS: 1", stdout);
        Assert.Contains("hardware warning", stdout);
        Assert.Contains("PASSED WITH WARNINGS", stdout);
    }

    [Theory]
    [InlineData(CompileState.Information)]
    [InlineData(CompileState.Warning)]
    public void PerType_NonSuccessStateWithNoErrors_ExitsSuccess(CompileState state)
    {
        var gateway = new FakeGateway
        {
            TypeCompileResult = new CompileResult(
                state, ErrorCount: 0, WarningCount: 3,
                Messages: Array.Empty<CompileMessage>(),
                ConsistentAfterCompile: true),
        };

        var (exitCode, _, _) = CaptureConsole(() =>
            Program.RunCompile(gateway, TypeOptions(), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.Success, exitCode);
    }

    /// <summary>Errors still fail, whatever <c>State</c> says — including a state of Success.</summary>
    [Fact]
    public void PerBlock_ErrorsFail_EvenWhenTheStateClaimsSuccess()
    {
        var gateway = new FakeGateway
        {
            BlockCompileResult = new CompileResult(
                CompileState.Success,
                ErrorCount: 2,
                WarningCount: 0,
                Messages: Array.Empty<CompileMessage>(),
                ConsistentAfterCompile: true),
        };

        var (exitCode, _, _) = CaptureConsole(() =>
            Program.RunCompile(gateway, BlockOptions(), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.CompileFailed, exitCode);
    }

    /// <summary>
    /// Fail-closed on the two counts disagreeing. The formatter already warns that the compiler's own
    /// aggregates are unreliable when they contradict the message tree; a verdict trusting only the
    /// aggregate would be talked out of failing by the count that happened to read zero.
    /// </summary>
    [Fact]
    public void PerBlock_ErrorMessagesWithAZeroAggregate_StillFail()
    {
        var gateway = new FakeGateway
        {
            BlockCompileResult = new CompileResult(
                CompileState.Warning,
                ErrorCount: 0,
                WarningCount: 0,
                Messages: new[] { new CompileMessage(CompileState.Error, "Tag 'Foo' does not exist", "FB_X/Network 3") },
                ConsistentAfterCompile: true),
        };

        var (exitCode, _, _) = CaptureConsole(() =>
            Program.RunCompile(gateway, BlockOptions(), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.CompileFailed, exitCode);
    }

    /// <summary>
    /// The whole-device path gets the same verdict rule. It is the same `Compile()` and the same
    /// project-wide hardware warning, so a device compile with no errors is a pass too — and the
    /// FI-52 backstop below it is what decides whether the pass means anything.
    /// </summary>
    [Fact]
    public void WholeDevice_WarningStateWithNoErrorsAndEveryBlockConsistent_ExitsSuccess()
    {
        var gateway = new FakeGateway
        {
            StationCompileResult = new CompileResult(
                CompileState.Warning, ErrorCount: 0, WarningCount: 1,
                Messages: Array.Empty<CompileMessage>()),
            BlocksForEnumeration = new[] { Block("FB_A", consistent: true), Block("FB_B", consistent: true) },
        };

        var (exitCode, _, _) = CaptureConsole(() =>
            Program.RunCompile(gateway, DeviceOptions(), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.Success, exitCode);
    }

    /// <summary>FI-52 is untouched by the verdict change: a clean device compile over an inconsistent block is still 11.</summary>
    [Fact]
    public void WholeDevice_NoErrorsButABlockLeftInconsistent_IsStillCompileIncomplete()
    {
        var gateway = new FakeGateway
        {
            StationCompileResult = new CompileResult(
                CompileState.Warning, ErrorCount: 0, WarningCount: 1,
                Messages: Array.Empty<CompileMessage>()),
            BlocksForEnumeration = new[] { Block("FB_A", consistent: true), Block("FB_B", consistent: false) },
        };

        var (exitCode, _, stderr) = CaptureConsole(() =>
            Program.RunCompile(gateway, DeviceOptions(), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.CompileIncomplete, exitCode);
        Assert.Contains("FB_B", stderr);
    }

    // ---- defect 2: the post-compile consistency read-back ------------------------------------

    /// <summary>
    /// The measured case. Errors 0, and the block cannot be exported. A clean-looking exit here is
    /// the failure being fixed: the caller proceeds to `export` and gets a refusal it cannot explain.
    /// </summary>
    [Fact]
    public void PerBlock_CleanCompileThatLeavesTheBlockInconsistent_ExitsCompileIncomplete()
    {
        var gateway = new FakeGateway
        {
            BlockCompileResult = new CompileResult(
                CompileState.Success,
                ErrorCount: 0,
                WarningCount: 0,
                Messages: new[] { new CompileMessage(CompileState.Success, "Block was successfully compiled", "FB_X") },
                ConsistentAfterCompile: false),
        };

        var (exitCode, stdout, stderr) = CaptureConsole(() =>
            Program.RunCompile(gateway, BlockOptions(), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.CompileIncomplete, exitCode);
        Assert.NotEqual(ExitCodes.Success, exitCode);
        Assert.Contains("CONSISTENT: NO", stdout);
        Assert.Contains("COMPILE INCOMPLETE", stderr);
        Assert.Contains("cannot be exported", stderr);
    }

    [Fact]
    public void PerType_CleanCompileThatLeavesTheTypeInconsistent_ExitsCompileIncomplete()
    {
        var gateway = new FakeGateway
        {
            TypeCompileResult = new CompileResult(
                CompileState.Success, ErrorCount: 0, WarningCount: 0,
                Messages: Array.Empty<CompileMessage>(),
                ConsistentAfterCompile: false),
        };

        var (exitCode, _, stderr) = CaptureConsole(() =>
            Program.RunCompile(gateway, TypeOptions(), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.CompileIncomplete, exitCode);
        Assert.Contains("UDT_Rack", stderr);
    }

    /// <summary>
    /// An unanswered read-back is not a pass (FI-44: empty is not clean). The compile ran; the one
    /// question that would have made it a gate went unanswered, so the exit code says so.
    /// </summary>
    [Fact]
    public void PerBlock_CleanCompileWhoseConsistencyCouldNotBeRead_ExitsCompileIncomplete()
    {
        var gateway = new FakeGateway
        {
            BlockCompileResult = new CompileResult(
                CompileState.Success, ErrorCount: 0, WarningCount: 0,
                Messages: Array.Empty<CompileMessage>(),
                ConsistentAfterCompile: null),
        };

        var (exitCode, _, stderr) = CaptureConsole(() =>
            Program.RunCompile(gateway, BlockOptions(), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.CompileIncomplete, exitCode);
        Assert.Contains("could not", stderr, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A consistent block after a clean compile is the only shape that passes.</summary>
    [Fact]
    public void PerBlock_CleanCompileThatLeavesTheBlockConsistent_ExitsSuccessAndSaysSo()
    {
        var gateway = new FakeGateway
        {
            BlockCompileResult = new CompileResult(
                CompileState.Success, ErrorCount: 0, WarningCount: 0,
                Messages: Array.Empty<CompileMessage>(),
                ConsistentAfterCompile: true),
        };

        var (exitCode, stdout, _) = CaptureConsole(() =>
            Program.RunCompile(gateway, BlockOptions(), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("CONSISTENT: yes", stdout);
    }

    /// <summary>
    /// Errors outrank the consistency read-back: an item that failed to compile IS the finding, and
    /// reporting it as 11 ("nothing errored, something went unverified") would be a lie about which.
    /// </summary>
    [Fact]
    public void PerBlock_ErrorsAndInconsistency_ReportsTheErrors()
    {
        var gateway = new FakeGateway
        {
            BlockCompileResult = new CompileResult(
                CompileState.Error, ErrorCount: 3, WarningCount: 0,
                Messages: Array.Empty<CompileMessage>(),
                ConsistentAfterCompile: false),
        };

        var (exitCode, _, _) = CaptureConsole(() =>
            Program.RunCompile(gateway, BlockOptions(), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.CompileFailed, exitCode);
    }

    /// <summary>
    /// The whole-device path asks no per-item question — there is no single item to ask about — so a
    /// null read-back there must NOT be read as "unverified". FI-52's block enumeration is that
    /// path's own check, and it is a different one.
    /// </summary>
    [Fact]
    public void WholeDevice_NullReadBack_IsNotTreatedAsUnverified()
    {
        var gateway = new FakeGateway
        {
            StationCompileResult = new CompileResult(
                CompileState.Success, ErrorCount: 0, WarningCount: 0,
                Messages: Array.Empty<CompileMessage>(),
                ConsistentAfterCompile: null),
            BlocksForEnumeration = new[] { Block("FB_A", consistent: true) },
        };

        var (exitCode, _, _) = CaptureConsole(() =>
            Program.RunCompile(gateway, DeviceOptions(), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.Success, exitCode);
    }

    // ---- output shape -------------------------------------------------------------------------

    /// <summary>
    /// The JSON field is emitted even when null, so a consumer can tell "this build does not read
    /// consistency back" from "this compile had no single item to ask about". Same reasoning as
    /// download-plan's `granularity`.
    /// </summary>
    [Fact]
    public void Json_CarriesTheConsistencyReadBack_IncludingWhenNull()
    {
        var withReadBack = OutputFormatter.FormatCompileJson(new CompileResult(
            CompileState.Success, 0, 0, Array.Empty<CompileMessage>(), ConsistentAfterCompile: false));
        using (var doc = JsonDocument.Parse(withReadBack))
        {
            Assert.False(doc.RootElement.GetProperty("consistentAfterCompile").GetBoolean());
        }

        var deviceLevel = OutputFormatter.FormatCompileJson(new CompileResult(
            CompileState.Success, 0, 0, Array.Empty<CompileMessage>()));
        using (var doc = JsonDocument.Parse(deviceLevel))
        {
            Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("consistentAfterCompile").ValueKind);
        }
    }

    [Fact]
    public void Table_OmitsTheConsistencyLine_WhenTheQuestionWasNotAsked()
    {
        var text = OutputFormatter.FormatCompileTable(new CompileResult(
            CompileState.Success, 0, 0, Array.Empty<CompileMessage>()));

        Assert.DoesNotContain("CONSISTENT:", text);
    }

    // ---- helpers ------------------------------------------------------------------------------

    private static BlockInfo Block(string name, bool consistent) =>
        new(name, BlockType.FB, 1, "LAD", IsSafety: false, "PLC_1/Program blocks", IsConsistent: consistent);

    /// <summary>
    /// A bare `compile` — no scope flag. Since 2026-08-13 that dispatches to the STATION scope
    /// (hardware + program), not the DeviceItem one (hardware only), so the fakes above configure
    /// <c>StationCompileResult</c>. The verdict rules these tests pin are unchanged by that; what
    /// changed is which compile the whole-device path actually runs.
    /// </summary>
    private static CompileCommandOptions DeviceOptions() => new(
        ProjectIdentifier: "C:\\proj\\My.ap20",
        Device: null,
        Block: null,
        Type: null,
        Json: false,
        TiaInstallOverride: null,
        TimeoutConnectSeconds: ArgumentParser.DefaultTimeoutConnectSeconds,
        TimeoutOpenSeconds: ArgumentParser.DefaultTimeoutOpenSeconds);

    private static CompileCommandOptions BlockOptions() => DeviceOptions() with { Block = "FB_X" };

    private static CompileCommandOptions TypeOptions() => DeviceOptions() with { Type = "UDT_Rack" };

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
