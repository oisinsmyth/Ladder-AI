using System;
using System.Collections.Generic;
using System.IO;
using OpennessCli;
using OpennessCli.Cli;
using OpennessCli.Model;
using OpennessCli.Openness;
using Xunit;

namespace OpennessCli.Tests;

/// <summary>
/// `block-layout` — reading, asserting and setting a block's optimized/standard block access.
///
/// Everything here runs against <see cref="FakeGateway"/> rather than Portal. That is the only way
/// this command's central property can be checked at all: a `--set` must FAIL when the value read
/// back after saving is not the value requested, and the whole point of that failure mode is that
/// nothing else in the toolchain can see it. The converter emits no MemoryLayout, `Normalizer`
/// ignores the attribute, and no compile or drift-check reports on it — so a silent no-op here
/// produces a green pipeline and a block a classic-S7comm reader cannot see.
/// </summary>
public class BlockLayoutTests
{
    // ---- argument parsing ------------------------------------------------------------------

    [Fact]
    public void Parse_ReadOnly_IsTheDefault()
    {
        var result = ArgumentParser.Parse(new[] { "block-layout", "C:\\proj\\My.ap20", "--block", "DB_Marker" });

        var success = Assert.IsType<ParseResult.BlockLayoutSuccess>(result);
        Assert.Equal("DB_Marker", success.Options.BlockName);
        Assert.Null(success.Options.Set);
        Assert.Null(success.Options.Expect);
        Assert.False(success.Options.Confirm);
    }

    [Fact]
    public void Parse_MissingBlock_Fails()
    {
        var result = ArgumentParser.Parse(new[] { "block-layout", "C:\\proj\\My.ap20" });
        var failure = Assert.IsType<ParseResult.Failure>(result);
        Assert.Contains("--block", failure.Message);
    }

    [Fact]
    public void Parse_MissingProject_Fails()
    {
        var result = ArgumentParser.Parse(new[] { "block-layout", "--block", "DB_Marker" });
        Assert.IsType<ParseResult.Failure>(result);
    }

    [Theory]
    [InlineData("Standard", MemoryLayoutKind.Standard)]
    [InlineData("standard", MemoryLayoutKind.Standard)]
    [InlineData("Optimized", MemoryLayoutKind.Optimized)]
    [InlineData("OPTIMIZED", MemoryLayoutKind.Optimized)]
    public void Parse_SetValue_IsCaseInsensitive(string raw, MemoryLayoutKind expected)
    {
        var result = ArgumentParser.Parse(new[]
        {
            "block-layout", "C:\\proj\\My.ap20", "--block", "DB_Marker", "--set", raw, "--yes",
        });

        var success = Assert.IsType<ParseResult.BlockLayoutSuccess>(result);
        Assert.Equal(expected, success.Options.Set);
    }

    /// <summary>
    /// An unrecognised value is a HARD ERROR naming both valid values — never a silent default.
    /// "0"/"1" are in here on purpose: <c>Enum.TryParse</c> accepts numeric strings, so the obvious
    /// implementation would resolve a typo to a layout nobody named.
    /// </summary>
    [Theory]
    [InlineData("Optimised")]
    [InlineData("standard-access")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("")]
    public void Parse_UnrecognisedSetValue_FailsNamingBothValidValues(string bad)
    {
        var result = ArgumentParser.Parse(new[]
        {
            "block-layout", "C:\\proj\\My.ap20", "--block", "DB_Marker", "--set", bad, "--yes",
        });

        var failure = Assert.IsType<ParseResult.Failure>(result);
        Assert.Contains("Standard", failure.Message);
        Assert.Contains("Optimized", failure.Message);
    }

    [Fact]
    public void Parse_UnrecognisedExpectValue_Fails()
    {
        var result = ArgumentParser.Parse(new[]
        {
            "block-layout", "C:\\proj\\My.ap20", "--block", "DB_Marker", "--expect", "Optimised",
        });

        var failure = Assert.IsType<ParseResult.Failure>(result);
        Assert.Contains("--expect", failure.Message);
    }

    [Fact]
    public void Parse_SetAndExpectTogether_Fails()
    {
        var result = ArgumentParser.Parse(new[]
        {
            "block-layout", "C:\\proj\\My.ap20", "--block", "DB_Marker",
            "--set", "Standard", "--expect", "Standard", "--yes",
        });

        var failure = Assert.IsType<ParseResult.Failure>(result);
        Assert.Contains("mutually exclusive", failure.Message);
    }

    [Fact]
    public void Parse_SetWithoutYes_ParsesButIsNotConfirmed()
    {
        var result = ArgumentParser.Parse(new[]
        {
            "block-layout", "C:\\proj\\My.ap20", "--block", "DB_Marker", "--set", "Standard",
        });

        var success = Assert.IsType<ParseResult.BlockLayoutSuccess>(result);
        Assert.Equal(MemoryLayoutKind.Standard, success.Options.Set);
        Assert.False(success.Options.Confirm);
    }

    /// <summary>
    /// The unconfirmed set is refused from the arguments alone — <see cref="Program.Main"/> checks
    /// it before <c>Connect</c>, so no Portal session exists when it is answered. This asserts the
    /// exit code and, more importantly, that the refusal states the consequence rather than just
    /// declining.
    /// </summary>
    [Fact]
    public void SetWithoutYes_RefusesWithoutContactingPortal_AndStatesTheConsequence()
    {
        var parsed = Assert.IsType<ParseResult.BlockLayoutSuccess>(ArgumentParser.Parse(new[]
        {
            "block-layout", "C:\\proj\\My.ap20", "--block", "DB_Marker", "--set", "Standard",
        }));

        var (exitCode, _, stderr) = CaptureConsole(() => Program.RefuseUnconfirmedBlockLayout(parsed.Options));

        Assert.Equal(ExitCodes.NotConfirmed, exitCode);
        Assert.Contains("DESTROYS ITS RETAINED DATA", stderr);
        Assert.Contains("--yes", stderr);
        Assert.Contains("Portal was not contacted", stderr);
        Assert.Contains("UNVERIFIED", stderr);
    }

    // ---- read path -------------------------------------------------------------------------

    [Fact]
    public void Read_ReportsTheLayout_AndWritesNothing()
    {
        var gateway = new FakeGateway { Current = MemoryLayoutKind.Optimized };

        var (exitCode, stdout, _) = CaptureConsole(() =>
            Program.RunBlockLayout(gateway, ReadOptions(), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("LAYOUT: Optimized", stdout);
        Assert.Equal(0, gateway.SetCalls);
        Assert.Equal(0, gateway.SaveCalls);
    }

    [Fact]
    public void Read_Json_CarriesTheLayoutAndNoRequest()
    {
        var gateway = new FakeGateway { Current = MemoryLayoutKind.Standard };

        var (exitCode, stdout, _) = CaptureConsole(() =>
            Program.RunBlockLayout(gateway, ReadOptions() with { Json = true }, timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("\"layout\": \"Standard\"", stdout);
        Assert.Contains("\"isSet\": false", stdout);
        Assert.Contains("\"requestedLayout\": null", stdout);
        Assert.Equal(0, gateway.SetCalls);
    }

    [Fact]
    public void Expect_Matching_Passes()
    {
        var gateway = new FakeGateway { Current = MemoryLayoutKind.Standard };

        var (exitCode, _, _) = CaptureConsole(() => Program.RunBlockLayout(
            gateway, ReadOptions() with { Expect = MemoryLayoutKind.Standard }, timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(0, gateway.SetCalls);
    }

    /// <summary>
    /// The post-import gate. Nothing else in the toolchain can detect a layout that has drifted back
    /// to Optimized, so this exit code is the only mechanical signal there is.
    /// </summary>
    [Fact]
    public void Expect_Mismatched_FailsWithLayoutMismatch()
    {
        var gateway = new FakeGateway { Current = MemoryLayoutKind.Optimized };

        var (exitCode, _, stderr) = CaptureConsole(() => Program.RunBlockLayout(
            gateway, ReadOptions() with { Expect = MemoryLayoutKind.Standard }, timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.LayoutMismatch, exitCode);
        Assert.Contains("LAYOUT MISMATCH", stderr);
        Assert.Equal(0, gateway.SetCalls);
    }

    // ---- set path --------------------------------------------------------------------------

    [Fact]
    public void Set_ThatTakes_Succeeds_AndReportsBeforeAndAfter()
    {
        var gateway = new FakeGateway { Current = MemoryLayoutKind.Optimized };

        var (exitCode, stdout, _) = CaptureConsole(() =>
            Program.RunBlockLayout(gateway, SetOptions(MemoryLayoutKind.Standard), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(1, gateway.SetCalls);
        Assert.Contains("BEFORE: Optimized", stdout);
        Assert.Contains("REQUESTED: Standard", stdout);
        Assert.Contains("AFTER: Standard", stdout);
        Assert.Contains("VERIFIED", stdout);
        Assert.Contains("DESTROYS ITS RETAINED DATA", stdout);
        Assert.Contains("UNVERIFIED", stdout);
    }

    /// <summary>
    /// THE test this command exists for. The gateway reports a read-back that disagrees with the
    /// request — a set that silently did not take — and the command must fail, not report a success
    /// with a note. There is deliberately no flag anywhere that skips this check.
    /// </summary>
    [Fact]
    public void Set_WhoseReadBackDisagrees_FailsClosed()
    {
        var gateway = new FakeGateway
        {
            Current = MemoryLayoutKind.Optimized,

            // The realistic no-op: the property assignment is accepted, the save succeeds, and the
            // block comes back exactly as it was.
            IgnoreSets = true,
        };

        var (exitCode, stdout, stderr) = CaptureConsole(() =>
            Program.RunBlockLayout(gateway, SetOptions(MemoryLayoutKind.Standard), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.LayoutMismatch, exitCode);
        Assert.NotEqual(ExitCodes.Success, exitCode);
        Assert.Contains("NOT APPLIED", stdout);
        Assert.Contains("SET NOT APPLIED", stderr);
    }

    /// <summary>
    /// Fails closed even when the RESULT RECORD claims verification passed. The command gates on its
    /// own requested value, so a gateway that misreports what it was asked for cannot talk it into a
    /// green exit.
    /// </summary>
    [Fact]
    public void Set_WhoseResultMisreportsTheRequest_StillFailsClosed()
    {
        var gateway = new FakeGateway
        {
            Current = MemoryLayoutKind.Optimized,
            IgnoreSets = true,

            // Result says "you asked for Optimized and got Optimized" — self-consistent, and wrong.
            OverrideReportedRequest = MemoryLayoutKind.Optimized,
        };

        var (exitCode, _, _) = CaptureConsole(() =>
            Program.RunBlockLayout(gateway, SetOptions(MemoryLayoutKind.Standard), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.LayoutMismatch, exitCode);
    }

    [Fact]
    public void Set_Json_CarriesBeforeRequestedAndVerified()
    {
        var gateway = new FakeGateway { Current = MemoryLayoutKind.Optimized };

        var (exitCode, stdout, _) = CaptureConsole(() => Program.RunBlockLayout(
            gateway, SetOptions(MemoryLayoutKind.Standard) with { Json = true }, timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("\"previousLayout\": \"Optimized\"", stdout);
        Assert.Contains("\"requestedLayout\": \"Standard\"", stdout);
        Assert.Contains("\"layout\": \"Standard\"", stdout);
        Assert.Contains("\"verified\": true", stdout);
        Assert.Contains("\"changed\": true", stdout);
    }

    [Fact]
    public void Set_AlreadyAtTheRequestedValue_SucceedsAndSaysNothingChanged()
    {
        var gateway = new FakeGateway { Current = MemoryLayoutKind.Standard };

        var (exitCode, stdout, _) = CaptureConsole(() =>
            Program.RunBlockLayout(gateway, SetOptions(MemoryLayoutKind.Standard), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("nothing changed", stdout, StringComparison.OrdinalIgnoreCase);
    }

    // ---- safety ----------------------------------------------------------------------------

    [Fact]
    public void SafetyBlock_IsRefused_OnTheReadPath()
    {
        var gateway = new FakeGateway { ThrowSafety = true };

        var ex = Assert.Throws<SafetyContentRefusedException>(() =>
            CaptureConsole(() => Program.RunBlockLayout(gateway, ReadOptions(), timeoutOpenSeconds: 1)));

        Assert.Equal(ExitCodes.SafetyRefused, ExitCodes.ForException(ex));
    }

    [Fact]
    public void SafetyBlock_IsRefused_OnTheSetPath_BeforeAnythingIsWritten()
    {
        var gateway = new FakeGateway { ThrowSafety = true };

        Assert.Throws<SafetyContentRefusedException>(() => CaptureConsole(() =>
            Program.RunBlockLayout(gateway, SetOptions(MemoryLayoutKind.Standard), timeoutOpenSeconds: 1)));

        Assert.Equal(0, gateway.SaveCalls);
    }

    [Fact]
    public void LayoutUnavailable_IsAUserFixableCommandError_NotAnInternalFault()
    {
        var ex = new BlockMemoryLayoutUnavailableException("FC_Something", "read", new InvalidOperationException("nope"));
        Assert.Equal(ExitCodes.CommandError, ExitCodes.ForException(ex));
    }

    // ---- formatter shapes ------------------------------------------------------------------

    [Fact]
    public void FormatTable_Read_WarnsThatOptimizedIsInvisibleToClassicS7comm()
    {
        var text = OutputFormatter.FormatBlockLayoutTable(new BlockLayoutResult(
            "DB_Marker", "PLC_1/Program blocks", BlockType.DB, MemoryLayoutKind.Optimized, null, null));

        Assert.Contains("BLOCK: DB_Marker", text);
        Assert.Contains("LAYOUT: Optimized", text);
        Assert.Contains("classic S7comm", text);
        Assert.DoesNotContain("BEFORE:", text);
    }

    [Fact]
    public void FormatTable_ReadOfAStandardBlock_DoesNotWarn()
    {
        var text = OutputFormatter.FormatBlockLayoutTable(new BlockLayoutResult(
            "DB_Marker", "PLC_1/Program blocks", BlockType.DB, MemoryLayoutKind.Standard, null, null));

        Assert.Contains("LAYOUT: Standard", text);
        Assert.DoesNotContain("classic S7comm", text);
    }

    [Fact]
    public void FormatTable_FailedSet_LeadsWithNotApplied()
    {
        var text = OutputFormatter.FormatBlockLayoutTable(new BlockLayoutResult(
            "DB_Marker", "PLC_1/Program blocks", BlockType.DB,
            MemoryLayoutKind.Optimized, MemoryLayoutKind.Optimized, MemoryLayoutKind.Standard));

        Assert.Contains("NOT APPLIED", text);
        Assert.DoesNotContain("VERIFIED", text);
    }

    // ---- helpers ---------------------------------------------------------------------------

    private static BlockLayoutCommandOptions ReadOptions() => new(
        ProjectIdentifier: "C:\\proj\\My.ap20",
        BlockName: "DB_Marker",
        Device: null,
        Set: null,
        Expect: null,
        Confirm: false,
        Json: false,
        TiaInstallOverride: null,
        TimeoutConnectSeconds: ArgumentParser.DefaultTimeoutConnectSeconds,
        TimeoutOpenSeconds: ArgumentParser.DefaultTimeoutOpenSeconds);

    private static BlockLayoutCommandOptions SetOptions(MemoryLayoutKind set) =>
        ReadOptions() with { Set = set, Confirm = true };

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

/// <summary>
/// A gateway that never touches Portal. Only the block-layout pair and <c>OpenProject</c> do
/// anything; every other member throws, so a test that accidentally reaches one fails loudly rather
/// than silently exercising a stub.
/// </summary>
internal sealed class FakeGateway : IOpennessGateway
{
    public MemoryLayoutKind Current { get; set; } = MemoryLayoutKind.Optimized;

    /// <summary>Models the silent no-op: the set is accepted and the value does not move.</summary>
    public bool IgnoreSets { get; set; }

    /// <summary>Makes the result record misreport what it was asked for.</summary>
    public MemoryLayoutKind? OverrideReportedRequest { get; set; }

    public bool ThrowSafety { get; set; }

    public int SetCalls { get; private set; }

    public int SaveCalls { get; private set; }

    public int OpenProjectCalls { get; private set; }

    public BlockLayoutResult GetBlockMemoryLayout(string blockName, string? deviceFilter)
    {
        if (ThrowSafety)
        {
            throw new SafetyContentRefusedException(blockName, "F_LAD");
        }

        return new BlockLayoutResult(blockName, "PLC_1/Program blocks", BlockType.DB, Current, null, null);
    }

    public BlockLayoutResult SetBlockMemoryLayout(string blockName, string? deviceFilter, MemoryLayoutKind requested)
    {
        if (ThrowSafety)
        {
            throw new SafetyContentRefusedException(blockName, "F_LAD");
        }

        SetCalls++;
        var before = Current;
        if (!IgnoreSets)
        {
            Current = requested;
        }

        SaveCalls++;
        return new BlockLayoutResult(
            blockName, "PLC_1/Program blocks", BlockType.DB, Current, before,
            OverrideReportedRequest ?? requested);
    }

    public void OpenProject(string projectIdentifier, TimeSpan timeout) => OpenProjectCalls++;

    public void Save() => SaveCalls++;

    public void Dispose()
    {
    }

    // Everything below is out of scope for this command and must never be reached from it.
    public void Connect(TimeSpan timeout, string? preferProjectIdentifier = null) => throw new NotSupportedException();

    public IReadOnlyList<PortalProcessInfo> EnumeratePortalProcesses() => throw new NotSupportedException();

    public IReadOnlyList<BlockInfo> EnumerateBlocks() => throw new NotSupportedException();

    public IReadOnlyList<TagTableInfo> EnumerateTagTables() => throw new NotSupportedException();

    public IReadOnlyList<PlcTypeInfo> EnumerateTypes() => throw new NotSupportedException();

    public IReadOnlyList<HmiDeviceInfo> EnumerateHmi(string? screenFilter, int maxItems) => throw new NotSupportedException();

    public IReadOnlyList<HmiSchemaReport> EnumerateHmiSchema(string screenFilter, int maxItems) => throw new NotSupportedException();

    public HmiCreateScreenResult CreateHmiScreen(string screenName, long width, long height, IReadOnlyList<string> itemTypes) =>
        throw new NotSupportedException();

    public CompileResult CompileHmi(string? deviceFilter) => throw new NotSupportedException();

    public HmiEditScreenResult EditHmiScreen(
        string screenName,
        IReadOnlyList<(string Target, string Attribute, string Value)> sets,
        IReadOnlyList<(string Target, string EventType, string? Script)> events,
        IReadOnlyList<(string Target, string Property, string Tag)> binds,
        IReadOnlyList<(string What, string Target, string? Detail)> deletes,
        IReadOnlyList<string> addItems,
        IReadOnlyList<(string Target, string Property, string Kind)> bindKinds,
        IReadOnlyList<(string Target, string Property)> mapClears,
        IReadOnlyList<(string Target, string Property, string EntrySpec)> maps) => throw new NotSupportedException();

    public string CreateHmiTag(string tagName, string tableName, string dataType) => throw new NotSupportedException();

    public string CreateHmiObject(string kind, string name, string? parent) => throw new NotSupportedException();

    public string DeleteHmiObject(string kind, string name, bool allowAnyName) => throw new NotSupportedException();

    public IReadOnlyList<HmiObjectInfo> InventoryHmi(string? kindFilter) => throw new NotSupportedException();

    public LibraryInventory InventoryLibrary(bool includeMasterCopies) => throw new NotSupportedException();

    public LibraryExportResult ExportLibraryTypeVersion(string typeName, string? version, string outDirectory) =>
        throw new NotSupportedException();

    public IReadOnlyList<string> ProbeExportAsDocuments(string typeName, string outDirectory) => throw new NotSupportedException();

    public IReadOnlyList<string> SetHmiObjectAttributes(
        string kind,
        string name,
        IReadOnlyList<(string Attribute, string Value)> sets,
        IReadOnlyList<(string Attribute, string Value)> texts) => throw new NotSupportedException();

    public void ExportBlock(string blockName, string? deviceFilter, string outPath) => throw new NotSupportedException();

    public void ExportType(string typeName, string? deviceFilter, string outPath) => throw new NotSupportedException();

    public void ExportTagTable(string tagTableName, string? deviceFilter, string outPath) => throw new NotSupportedException();

    public IReadOnlyList<BlockInfo> ImportBlocks(string groupPath, IReadOnlyList<string> files) => throw new NotSupportedException();

    public IReadOnlyList<string> ImportTypes(string groupPath, IReadOnlyList<string> files) => throw new NotSupportedException();

    public BlockInfo CreateInstanceDb(string groupPath, string dbName, string instanceOfName) => throw new NotSupportedException();

    public IReadOnlyList<string> ImportTagTables(string groupPath, IReadOnlyList<string> files) => throw new NotSupportedException();

    public IReadOnlyList<BlockInfo> ImportBlockFile(string groupPath, string file) => throw new NotSupportedException();

    public IReadOnlyList<string> ImportTypeFile(string groupPath, string file) => throw new NotSupportedException();

    public IReadOnlyList<string> ImportTagTableFile(string groupPath, string file) => throw new NotSupportedException();

    public CompileResult Compile(string? deviceFilter) => throw new NotSupportedException();

    public BlockInfo DeleteBlock(string blockName, string? deviceFilter, bool confirm) => throw new NotSupportedException();

    public CompileResult CompileBlock(string blockName, string? deviceFilter) => throw new NotSupportedException();

    public CompileResult CompileType(string typeName, string? deviceFilter) => throw new NotSupportedException();

    public SanityCheckResult RunSanityCheck() => throw new NotSupportedException();
}
