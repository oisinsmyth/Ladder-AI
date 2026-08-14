using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using OpennessCli;
using OpennessCli.Cli;
using OpennessCli.Model;
using OpennessCli.Openness;
using Xunit;

namespace OpennessCli.Tests;

/// <summary>
/// Three defects measured on 2026-08-14 by hammering the READ-ONLY Portal surface of `openness-cli`
/// against the live scratch project, with Portal held by one lane. All three are reporting defects:
/// nothing computed the wrong answer, and every one of them HANDED a caller the wrong answer.
///
/// <list type="number">
/// <item><b><c>compile-all --json</c> said <c>"clean": true</c> about a run that examined nothing.</b>
///   The text output of the same run said <c>NOTHING EXAMINED … proves nothing about the project</c>.</item>
/// <item><b><c>compile --json</c> did not emit JSON.</b> A prose paragraph followed the object on
///   stdout, so the output parsed as nothing at all.</item>
/// <item><b>A <c>--device</c> that matched nothing was reported as a missing BLOCK.</b>
///   <c>export-all --device NoSuchDevice</c> named 43 present objects as absent from the project.</item>
/// </list>
///
/// Each test below is paired with the mutation that turns it red, named in its own doc comment —
/// because a regression guard nobody can run backwards is a claim, not a check.
/// </summary>
public class ReadOnlySurfaceDefectTests
{
    // ---- 1. "clean" must not be true of a run that examined nothing -------------------------

    /// <summary>
    /// MEASURED: <c>compile-all GenProject1 --json</c> on an all-consistent project returned
    /// <c>{"clean": true, "compiled": 0, "entries": []}</c> and exit 14.
    ///
    /// <para>MUTATION: restore <c>IsClean =&gt; WithErrorsCount == 0 &amp;&amp; StillInconsistentCount == 0</c>
    /// (drop the <c>!NothingExamined</c> term) and this goes red.</para>
    /// </summary>
    [Fact]
    public void CompileAllJson_NothingExamined_IsNotReportedAsClean()
    {
        var result = new CompileAllResult(Array.Empty<CompileAllEntry>(), Passes: 0);

        using var doc = JsonDocument.Parse(OutputFormatter.FormatCompileAllJson(result));
        var root = doc.RootElement;

        Assert.False(root.GetProperty("clean").GetBoolean());
        Assert.True(root.GetProperty("nothingExamined").GetBoolean());
        Assert.Equal(0, root.GetProperty("compiled").GetInt32());
    }

    /// <summary>
    /// The converse, and it is the half that stops the fix from being "make clean always false".
    /// A run that DID examine items and found nothing wrong must still report clean.
    ///
    /// <para>MUTATION: <c>IsClean =&gt; false</c> goes red here and green on the test above — which is
    /// why both exist.</para>
    /// </summary>
    [Fact]
    public void CompileAllJson_ExaminedAndFoundNothingWrong_IsStillClean()
    {
        var result = new CompileAllResult(
            new[]
            {
                new CompileAllEntry("FB_X", "Block", CompileState.Warning, ErrorCount: 0, WarningCount: 1, StillInconsistent: false, Detail: null),
            },
            Passes: 1);

        using var doc = JsonDocument.Parse(OutputFormatter.FormatCompileAllJson(result));

        Assert.True(doc.RootElement.GetProperty("clean").GetBoolean());
        Assert.False(doc.RootElement.GetProperty("nothingExamined").GetBoolean());
    }

    /// <summary>
    /// <c>clean: false</c> now covers two different failures, so the JSON has to keep them apart.
    /// Without <c>nothingExamined</c> a consumer could not tell "examined 43 and one was wrong" from
    /// "examined none" — the exact collapse <see cref="CompileAllResult"/>'s own comment refuses.
    /// </summary>
    [Fact]
    public void CompileAllJson_ExaminedAndFoundErrors_IsNotCleanButWasExamined()
    {
        var result = new CompileAllResult(
            new[]
            {
                new CompileAllEntry("FB_X", "Block", CompileState.Error, ErrorCount: 3, WarningCount: 0, StillInconsistent: false, Detail: null),
            },
            Passes: 1);

        using var doc = JsonDocument.Parse(OutputFormatter.FormatCompileAllJson(result));

        Assert.False(doc.RootElement.GetProperty("clean").GetBoolean());
        Assert.False(doc.RootElement.GetProperty("nothingExamined").GetBoolean());
    }

    /// <summary>
    /// The text surface never had this defect and must not acquire one from the fix: an empty run
    /// still prints NOTHING EXAMINED and still does not print the CLEAN line.
    /// </summary>
    [Fact]
    public void CompileAllTable_NothingExamined_StillSaysSoAndDoesNotSayClean()
    {
        var text = OutputFormatter.FormatCompileAllTable(new CompileAllResult(Array.Empty<CompileAllEntry>(), Passes: 0));

        Assert.Contains("NOTHING EXAMINED", text, StringComparison.Ordinal);
        Assert.DoesNotContain("CLEAN: every item compiled", text, StringComparison.Ordinal);
    }

    // ---- 2. --json must emit JSON ----------------------------------------------------------

    /// <summary>
    /// MEASURED: <c>compile GenProject1 --block DB_Input --json</c> emitted the object and then
    /// <c>PASSED WITH WARNINGS: …</c> on stdout. <c>ConvertFrom-Json</c> failed with
    /// <i>"Invalid JSON primitive: ASSED WITH WARNINGS"</i>.
    ///
    /// <para>This is the project's ordinary case, not an edge: the scratch project carries a
    /// permanent hardware warning, so EVERY per-block compile takes the non-Success branch.</para>
    ///
    /// <para>MUTATION: change <c>WriteAdvisory</c>'s sink to <c>Console.Out</c> unconditionally and
    /// this goes red.</para>
    /// </summary>
    [Fact]
    public void CompileJson_WarningState_StdOutIsParseableJson()
    {
        var gateway = WarningStateGateway();

        var (exitCode, stdout, stderr) = CaptureConsole(() =>
            Program.RunCompile(gateway, JsonBlockOptions(), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.Success, exitCode);

        // The whole of stdout, not a prefix of it — a defect that appends is invisible to any
        // check that parses only as far as the first complete value.
        using var doc = JsonDocument.Parse(stdout);
        Assert.Equal("Warning", doc.RootElement.GetProperty("state").GetString());

        // Routed, not suppressed: the advice is the part that says a non-Success state is a pass.
        Assert.Contains("PASSED WITH WARNINGS", stderr, StringComparison.Ordinal);
    }

    /// <summary>
    /// Without <c>--json</c> nothing moves: the advisory stays on stdout, where every human reader
    /// and every existing test expects it.
    /// </summary>
    [Fact]
    public void CompileTable_WarningState_AdvisoryStaysOnStdOut()
    {
        var (_, stdout, _) = CaptureConsole(() =>
            Program.RunCompile(WarningStateGateway(), BlockOptions(), timeoutOpenSeconds: 1));

        Assert.Contains("PASSED WITH WARNINGS", stdout, StringComparison.Ordinal);
    }

    // ---- 3. a device filter that matches nothing is a DEVICE failure -----------------------

    /// <summary>
    /// MEASURED: <c>export --block DB_Input --device NoSuchDevice</c> answered <i>"No block named
    /// 'DB_Input' found in the project."</i> while the identical command without the flag exported
    /// that block successfully.
    ///
    /// <para>MUTATION: drop the <c>matches.Count &gt; 0</c> guard's throw and this goes red.</para>
    /// </summary>
    [Fact]
    public void NarrowToDevice_FilterEmptiesANonEmptySet_NamesTheDevice()
    {
        var matches = new List<(string Name, string Path)> { ("DB_Input", "S7-1200 station_1/PLC1") };

        var ex = Assert.Throws<DeviceNotFoundException>(() =>
            OpennessGateway.NarrowToDevice(matches, "NoSuchDevice", m => m.Path));

        Assert.Contains("NoSuchDevice", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The distinction the fix turns on, and the half that keeps it honest: when the set was ALREADY
    /// empty, the object really is absent and the caller's own not-found exception must still be the
    /// one that fires. Without this, the fix would relabel every missing object as a missing device.
    /// </summary>
    [Fact]
    public void NarrowToDevice_SetWasAlreadyEmpty_DoesNotBlameTheDevice()
    {
        var narrowed = OpennessGateway.NarrowToDevice(
            new List<(string Name, string Path)>(), "NoSuchDevice", m => m.Path);

        Assert.Empty(narrowed);
    }

    /// <summary>A filter that matches still narrows — the positive control.</summary>
    [Fact]
    public void NarrowToDevice_FilterMatches_KeepsOnlyTheMatchingEntries()
    {
        var matches = new List<(string Name, string Path)>
        {
            ("DB_Input", "S7-1200 station_1/PLC1"),
            ("DB_Input", "S7-1500 station_2/PLC2"),
        };

        var narrowed = OpennessGateway.NarrowToDevice(matches, "station_2", m => m.Path);

        Assert.Single(narrowed);
        Assert.Equal("S7-1500 station_2/PLC2", narrowed[0].Path);
    }

    /// <summary>No filter is not a filter that matched nothing.</summary>
    [Fact]
    public void NarrowToDevice_NoFilter_ReturnsEverything()
    {
        var matches = new List<(string Name, string Path)> { ("DB_Input", "S7-1200 station_1/PLC1") };

        Assert.Single(OpennessGateway.NarrowToDevice(matches, null, m => m.Path));
    }

    // ---- 4. sanity-check must not pass a project it never examined -------------------------

    /// <summary>
    /// <c>IsHealthy</c>'s last term is <c>DeviceCompiles.All(…)</c>, and <c>All</c> over an empty
    /// sequence is TRUE — so a run that found no PLC device returned <c>OVERALL: HEALTHY</c> and
    /// exit 0 while having compiled nothing. Hard rule 4 names this command as THE gate.
    ///
    /// <para>MUTATION: drop the <c>!NothingExamined</c> term from <c>IsHealthy</c> and this goes
    /// red.</para>
    /// </summary>
    [Fact]
    public void SanityCheck_NoPlcDeviceFound_IsNotHealthy()
    {
        var result = EmptyDeviceSanityResult();

        Assert.True(result.NothingExamined);
        Assert.False(result.IsHealthy);
    }

    /// <summary>
    /// And it must SAY which of the two it is. "ISSUES FOUND" with an empty findings list sends a
    /// reader hunting for a problem that does not exist; the real answer is that there was no
    /// subject.
    /// </summary>
    [Fact]
    public void SanityCheck_NoPlcDeviceFound_SaysNothingExaminedRatherThanIssuesFound()
    {
        var text = OutputFormatter.FormatSanityCheckTable(EmptyDeviceSanityResult());

        Assert.Contains("OVERALL: NOTHING EXAMINED", text, StringComparison.Ordinal);
        Assert.DoesNotContain("OVERALL: HEALTHY", text, StringComparison.Ordinal);
        Assert.DoesNotContain("OVERALL: ISSUES FOUND", text, StringComparison.Ordinal);
        Assert.Contains("no PLC device was found", text, StringComparison.Ordinal);
    }

    /// <summary>The JSON keeps the two apart too — `healthy: false` alone cannot.</summary>
    [Fact]
    public void SanityCheckJson_NoPlcDeviceFound_CarriesNothingExamined()
    {
        using var doc = JsonDocument.Parse(OutputFormatter.FormatSanityCheckJson(EmptyDeviceSanityResult()));

        Assert.False(doc.RootElement.GetProperty("healthy").GetBoolean());
        Assert.True(doc.RootElement.GetProperty("nothingExamined").GetBoolean());
    }

    /// <summary>
    /// THE ANTI-OVER-FIRING CONTROL, and the reason the guard is scoped to devices and not to blocks.
    /// A real device carrying a permanent hardware warning, zero blocks and zero types is
    /// <b>examined and healthy</b>. A gate that refused this would be firing outside its scope, and a
    /// gate that fires on ordinary submissions gets switched off by someone who is right to.
    /// </summary>
    [Fact]
    public void SanityCheck_DeviceWithNoBlocksButAWarning_IsStillHealthyAndExamined()
    {
        var result = new SanityCheckResult(
            TotalBlocks: 0,
            InconsistentBlocks: Array.Empty<BlockConsistencyIssue>(),
            DeviceCompiles: new[]
            {
                new DeviceCompileSummary(
                    "S7-1200 station_1/PLC1",
                    new CompileResult(
                        CompileState.Warning,
                        ErrorCount: 0,
                        WarningCount: 1,
                        Messages: new[] { new CompileMessage(CompileState.Warning, "hardware warning", "PLC_1") },
                        ConsistentAfterCompile: null),
                    "station (hardware + program)"),
            },
            TotalTypes: 0,
            InconsistentTypes: Array.Empty<TypeConsistencyIssue>(),
            DuplicateNumbers: Array.Empty<DuplicateBlockNumber>());

        Assert.False(result.NothingExamined);
        Assert.True(result.IsHealthy);
        Assert.Contains("OVERALL: HEALTHY", OutputFormatter.FormatSanityCheckTable(result), StringComparison.Ordinal);
    }

    private static SanityCheckResult EmptyDeviceSanityResult() => new(
        TotalBlocks: 0,
        InconsistentBlocks: Array.Empty<BlockConsistencyIssue>(),
        DeviceCompiles: Array.Empty<DeviceCompileSummary>(),
        TotalTypes: 0,
        InconsistentTypes: Array.Empty<TypeConsistencyIssue>(),
        DuplicateNumbers: Array.Empty<DuplicateBlockNumber>());

    // ---- helpers ---------------------------------------------------------------------------

    private static FakeGateway WarningStateGateway() => new()
    {
        BlockCompileResult = new CompileResult(
            CompileState.Warning,
            ErrorCount: 0,
            WarningCount: 1,
            Messages: new[]
            {
                new CompileMessage(
                    CompileState.Warning,
                    "Inputs or outputs are used that do not exist in the configured hardware.",
                    "PLC_1"),
            },
            ConsistentAfterCompile: true),
    };

    private static CompileCommandOptions BlockOptions() => new(
        ProjectIdentifier: "C:\\proj\\My.ap20",
        Device: null,
        Block: "FB_X",
        Type: null,
        Json: false,
        TiaInstallOverride: null,
        TimeoutConnectSeconds: ArgumentParser.DefaultTimeoutConnectSeconds,
        TimeoutOpenSeconds: ArgumentParser.DefaultTimeoutOpenSeconds);

    private static CompileCommandOptions JsonBlockOptions() => BlockOptions() with { Json = true };

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
            return (action(), stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}
