using System;
using System.Collections.Generic;
using System.Linq;
using OpennessCli.Cli;
using OpennessCli.Model;
using Xunit;

namespace OpennessCli.Tests;

/// <summary>
/// The compile-scope gap, measured 2026-08-13 on the JOB9004 scratch project and fixed here.
///
/// **What was wrong.** `openness-cli compile &lt;project&gt;` resolves its compilable from the PLC
/// **DeviceItem**. On an S7-1200 that object exists, so the fallback to `PlcSoftware` in
/// `CompileDeviceItem` was dead code and the software-scope compile had never once been invoked. The
/// two are not the same compile: measured live, the DeviceItem compile's entire message tree is
/// *"Hardware configuration — Hardware was not compiled. The configuration is up-to-date"*, while the
/// PlcSoftware compile's is *"Program blocks — …"*. **The gate was compiling the HARDWARE and
/// reporting it as a program check** — which is also the real mechanism behind FI-52, previously
/// recorded as "device compile does not clear the inconsistent flag". It does not clear it because it
/// never looks at the program.
///
/// These tests hold the three scopes apart at the argument, dispatch and reporting layers. They
/// cannot check what TIA does — that is measured evidence, recorded in src/openness-cli/README.md —
/// but they can stop the three collapsing back into one, which is how the gap arose.
/// </summary>
public class CompileScopeTests
{
    // ---- argument parsing ------------------------------------------------------------------

    [Fact]
    public void Compile_NamesNoScopeByDefault_AndDispatchDecidesIt()
    {
        var result = ArgumentParser.Parse(new[] { "compile", "C:\\proj\\My.ap20" });

        var success = Assert.IsType<ParseResult.CompileSuccess>(result);
        Assert.False(success.Options.Software);
        Assert.False(success.Options.Station);
        Assert.False(success.Options.Hardware);
    }

    [Fact]
    public void Compile_HardwareFlag_SelectsTheOldDefaultScope()
    {
        var result = ArgumentParser.Parse(new[] { "compile", "C:\\proj\\My.ap20", "--hardware" });

        Assert.True(Assert.IsType<ParseResult.CompileSuccess>(result).Options.Hardware);
    }

    [Fact]
    public void Compile_SoftwareFlag_SelectsTheSoftwareScope()
    {
        var result = ArgumentParser.Parse(new[] { "compile", "C:\\proj\\My.ap20", "--software" });

        var success = Assert.IsType<ParseResult.CompileSuccess>(result);
        Assert.True(success.Options.Software);
        Assert.False(success.Options.Station);
    }

    [Fact]
    public void Compile_StationFlag_SelectsTheStationScope()
    {
        var result = ArgumentParser.Parse(new[] { "compile", "C:\\proj\\My.ap20", "--station" });

        var success = Assert.IsType<ParseResult.CompileSuccess>(result);
        Assert.True(success.Options.Station);
        Assert.False(success.Options.Software);
    }

    // A scope flag plus an item flag is a contradiction, not a refinement: `--software --block X`
    // reads as "compile the software, but only X", which is not a thing the API can express. Silently
    // honouring one of them is how a caller ends up believing it ran a scope it did not.
    [Theory]
    [InlineData("--software", "--block")]
    [InlineData("--software", "--type")]
    [InlineData("--station", "--block")]
    [InlineData("--station", "--type")]
    public void Compile_ScopeFlagWithItemFlag_IsRefused(string scopeFlag, string itemFlag)
    {
        var result = ArgumentParser.Parse(new[] { "compile", "C:\\proj\\My.ap20", scopeFlag, itemFlag, "X" });

        Assert.IsType<ParseResult.Failure>(result);
    }

    [Fact]
    public void Compile_BothScopeFlags_IsRefused()
    {
        var result = ArgumentParser.Parse(new[] { "compile", "C:\\proj\\My.ap20", "--software", "--station" });

        var failure = Assert.IsType<ParseResult.Failure>(result);
        Assert.Contains("three different scopes", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compile_HardwareWithAnotherScope_IsRefused()
    {
        var result = ArgumentParser.Parse(new[] { "compile", "C:\\proj\\My.ap20", "--hardware", "--station" });

        Assert.IsType<ParseResult.Failure>(result);
    }

    [Fact]
    public void CompileScopes_Parses()
    {
        var result = ArgumentParser.Parse(new[] { "compile-scopes", "C:\\proj\\My.ap20", "--json" });

        var success = Assert.IsType<ParseResult.CompileScopesSuccess>(result);
        Assert.Equal("C:\\proj\\My.ap20", success.Options.ProjectIdentifier);
        Assert.True(success.Options.Json);
    }

    // ---- dispatch --------------------------------------------------------------------------

    /// <summary>
    /// *** THE DEFAULT CHANGED ON 2026-08-13, AND THIS IS THE TEST THAT SAYS SO. ***
    ///
    /// A bare `compile` used to reach the DeviceItem compilable — the HARDWARE compile. It reported
    /// `Success, errors=0` on a project whose FC8 failed with `Tag "DB_Example".DataStore not
    /// defined`, measured. Every caller in the repository invokes a bare `compile` intending a
    /// program check, so the default is now the station scope: hardware AND program, a superset of
    /// what it did before.
    /// </summary>
    [Fact]
    public void RunCompile_Default_IsNowTheStationScope_NotTheHardwareOne()
    {
        var gateway = new FakeGateway
        {
            DeviceCompileResult = Clean(),
            SoftwareCompileResult = Clean(),
            StationCompileResult = Clean(),
            BlocksForEnumeration = AllConsistent,
        };

        Program.RunCompile(gateway, Options(), timeoutOpenSeconds: 1);

        Assert.Equal(1, gateway.StationCompileCalls);
        Assert.Equal(0, gateway.SoftwareCompileCalls);
        Assert.Equal(0, gateway.DeviceCompileCalls);
    }

    // The old default stays reachable BY NAME. A behaviour that can only be obtained by not asking
    // for anything is one nobody can reason about — and this one is still the right call when the
    // question really is about the hardware configuration.
    [Fact]
    public void RunCompile_Hardware_CallsTheOldDeviceItemScope()
    {
        var gateway = new FakeGateway { DeviceCompileResult = Clean(), BlocksForEnumeration = AllConsistent };

        Program.RunCompile(gateway, Options(hardware: true), timeoutOpenSeconds: 1);

        Assert.Equal(1, gateway.DeviceCompileCalls);
        Assert.Equal(0, gateway.StationCompileCalls);
    }

    [Fact]
    public void RunCompile_Software_CallsTheSoftwareScope()
    {
        var gateway = new FakeGateway { SoftwareCompileResult = Clean(), BlocksForEnumeration = AllConsistent };

        Program.RunCompile(gateway, Options(software: true), timeoutOpenSeconds: 1);

        Assert.Equal(1, gateway.SoftwareCompileCalls);
        Assert.Equal(0, gateway.StationCompileCalls);
    }

    [Fact]
    public void RunCompile_Station_CallsTheStationScope()
    {
        var gateway = new FakeGateway { StationCompileResult = Clean(), BlocksForEnumeration = AllConsistent };

        Program.RunCompile(gateway, Options(station: true), timeoutOpenSeconds: 1);

        Assert.Equal(1, gateway.StationCompileCalls);
        Assert.Equal(0, gateway.SoftwareCompileCalls);
    }

    // ---- sanity-check's compile half -------------------------------------------------------

    /// <summary>
    /// The verdict must key on ERRORS, never on <c>State</c>. This was survivable while
    /// `sanity-check` ran the hardware-only compile, which had nothing to warn about. The station
    /// scope reports the project's real warnings — a hardware-interrupt OB with no trigger, IO absent
    /// from the configured hardware — so a State-keyed verdict would call a perfectly good project
    /// unhealthy on every run, for reasons no action of ours can clear.
    /// </summary>
    [Fact]
    public void SanityCheck_WarningStateWithZeroErrors_IsStillHealthy()
    {
        var result = Sanity(new CompileResult(
            CompileState.Warning,
            ErrorCount: 0,
            WarningCount: 2,
            Messages: new[] { new CompileMessage(CompileState.Warning, "Inputs or outputs are used that do not exist", "PLC_1") }));

        Assert.True(result.IsHealthy);
    }

    // Fail-closed on the count: the compiler's own aggregate and its message tree demonstrably
    // disagree, so the larger wins. An error present only in the tree must still sink the verdict.
    [Fact]
    public void SanityCheck_ErrorInTheMessageTreeOnly_IsNotHealthy()
    {
        var result = Sanity(new CompileResult(
            CompileState.Error,
            ErrorCount: 0,
            WarningCount: 0,
            Messages: new[] { new CompileMessage(CompileState.Error, "Tag \"DB_Example\".DataStore not defined.", "FC8") }));

        Assert.False(result.IsHealthy);
    }

    // The scope is reported, because the whole failure was a line that did not disclose which
    // question it had asked.
    [Fact]
    public void SanityCheck_Table_NamesTheCompileScope_AndPrintsEveryError()
    {
        var result = Sanity(new CompileResult(
            CompileState.Error,
            ErrorCount: 1,
            WarningCount: 0,
            Messages: new[] { new CompileMessage(CompileState.Error, "Tag \"DB_Example\".DataStore not defined.", "FC8") }));

        var text = OutputFormatter.FormatSanityCheckTable(result);

        Assert.Contains("scope=station (hardware + program)", text, StringComparison.Ordinal);
        Assert.Contains("DB_Example", text, StringComparison.Ordinal);
    }

    private static SanityCheckResult Sanity(CompileResult compile) => new(
        TotalBlocks: 1,
        InconsistentBlocks: Array.Empty<BlockConsistencyIssue>(),
        DeviceCompiles: new[] { new DeviceCompileSummary("S7-1200 station_1/PLC_1", compile, "station (hardware + program)") },
        TotalTypes: 0,
        InconsistentTypes: Array.Empty<TypeConsistencyIssue>(),
        DuplicateNumbers: Array.Empty<DuplicateBlockNumber>());

    // The FI-52 backstop must survive the new scopes. A software compile that reports zero errors and
    // leaves a block inconsistent is exactly as incomplete as a device compile that does.
    [Fact]
    public void RunCompile_Software_StillFailsClosedOnBlocksLeftInconsistent()
    {
        var gateway = new FakeGateway
        {
            SoftwareCompileResult = Clean(),
            BlocksForEnumeration = new[]
            {
                new BlockInfo("FC_Left", BlockType.FC, 8, "LAD", false, "PLC_1", IsConsistent: false),
            },
        };

        var exit = Program.RunCompile(gateway, Options(software: true), timeoutOpenSeconds: 1);

        Assert.Equal(ExitCodes.CompileIncomplete, exit);
    }

    // ---- reporting -------------------------------------------------------------------------

    [Fact]
    public void ScopeTable_OneCompiler_SaysThereIsNothingToMiss()
    {
        var survey = new CompileScopeSurvey(new[] { Scope("Device 'A'", group: 1) }, DistinctCompilers: 1);

        var text = OutputFormatter.FormatCompileScopesTable(survey);

        Assert.Contains("SAME compiler", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DIFFERENT COMPILERS", text, StringComparison.Ordinal);
    }

    // The whole reason the command exists. More than one compiler must be stated as a finding a
    // reader has to act on, not printed as a row they can scroll past.
    [Fact]
    public void ScopeTable_SeveralCompilers_NamesTheGapAndHowToSettleIt()
    {
        var survey = new CompileScopeSurvey(
            new[] { Scope("Device 'A'/PLC_1", group: 1), Scope("Device 'A'/PLC_1 -> PlcSoftware", group: 2) },
            DistinctCompilers: 2);

        var text = OutputFormatter.FormatCompileScopesTable(survey);

        Assert.Contains("2 DIFFERENT COMPILERS", text, StringComparison.Ordinal);
        Assert.Contains("--software", text, StringComparison.Ordinal);
    }

    // An empty attribute/invocation list is the MEASUREMENT that no rebuild-all can be requested
    // through Openness — Compile() takes no arguments and the objects carry no knobs. Printing
    // nothing at all would leave a reader unable to tell "asked, and there are none" from "not asked".
    [Fact]
    public void ScopeTable_EmptyAttributeList_IsPrintedAsAMeasurement()
    {
        var survey = new CompileScopeSurvey(new[] { Scope("Device 'A'", group: 1) }, DistinctCompilers: 1);

        var text = OutputFormatter.FormatCompileScopesTable(survey);

        Assert.Contains("attributes : (none", text, StringComparison.Ordinal);
        Assert.Contains("invocations: (none", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ScopeJson_CarriesEveryScopeAndTheCount()
    {
        var survey = new CompileScopeSurvey(
            new[] { Scope("Device 'A'/PLC_1", group: 1), Scope("Device 'A'/PLC_1 -> PlcSoftware", group: 2) },
            DistinctCompilers: 2);

        var json = OutputFormatter.FormatCompileScopesJson(survey);

        Assert.Contains("\"distinctCompilers\": 2", json, StringComparison.Ordinal);
        Assert.Contains("PlcSoftware", json, StringComparison.Ordinal);
    }

    private static CompileScope Scope(string label, int group) => new(
        label,
        "Siemens.Engineering.HW.DeviceItem",
        HasCompilable: true,
        "Siemens.Engineering.Compiler.CompileProvider",
        "Siemens.Engineering.HW.DeviceItem",
        "PLC_1",
        group,
        Array.Empty<string>(),
        Array.Empty<string>(),
        Error: null);

    // Every whole-scope compile runs the FI-52 backstop afterwards, so a scope test has to supply a
    // block enumeration — otherwise it fails for the backstop's reasons rather than its own.
    private static readonly BlockInfo[] AllConsistent =
    {
        new("FC_Fine", BlockType.FC, 1, "LAD", false, "PLC_1", IsConsistent: true),
    };

    private static CompileResult Clean() =>
        new(CompileState.Success, 0, 0, Array.Empty<CompileMessage>());

    private static CompileCommandOptions Options(bool software = false, bool station = false, bool hardware = false) => new(
        "C:\\proj\\My.ap20",
        Device: null,
        Block: null,
        Type: null,
        Json: false,
        TiaInstallOverride: null,
        TimeoutConnectSeconds: 1,
        TimeoutOpenSeconds: 1,
        Software: software,
        Station: station,
        Hardware: hardware);
}
