using System.Linq;
using OpennessCli.Cli;
using OpennessCli.Openness;
using Xunit;

namespace OpennessCli.Tests;

/// <summary>
/// The `graphics` subcommand (2026-08-17). Covers the argument surface only — the Openness half is
/// unreachable without Portal, and what it does there was established by running it against a real
/// project rather than by asserting it here.
/// </summary>
public class GraphicsCommandTests
{
    [Fact]
    public void Graphics_BareProject_IsTheListMode()
    {
        var result = ArgumentParser.Parse(new[] { "graphics", "C:\\proj\\My.ap20" });

        var success = Assert.IsType<ParseResult.GraphicsSuccess>(result);
        Assert.Equal("C:\\proj\\My.ap20", success.Options.ProjectIdentifier);
        Assert.Null(success.Options.ExportName);
        Assert.Null(success.Options.InspectName);
        Assert.Empty(success.Options.ImportFiles);
    }

    /// <summary>--list is what a caller writes even though it is the default; refusing it as unknown
    /// would be a gratuitous trap.</summary>
    [Fact]
    public void Graphics_ExplicitList_IsAccepted()
    {
        var result = ArgumentParser.Parse(new[] { "graphics", "C:\\proj\\My.ap20", "--list" });
        Assert.IsType<ParseResult.GraphicsSuccess>(result);
    }

    [Fact]
    public void Graphics_MissingProject_Fails()
    {
        var result = ArgumentParser.Parse(new[] { "graphics", "--list" });
        Assert.IsType<ParseResult.Failure>(result);
    }

    /// <summary>An export with nowhere to write is a silent no-op waiting to happen — the same rule
    /// `library --export-version` applies.</summary>
    [Fact]
    public void Graphics_ExportWithoutOut_Fails()
    {
        var result = ArgumentParser.Parse(new[] { "graphics", "C:\\proj\\My.ap20", "--export", "Pump" });
        var failure = Assert.IsType<ParseResult.Failure>(result);
        Assert.Contains("--out", failure.Message);
    }

    [Fact]
    public void Graphics_OutWithoutExport_Fails()
    {
        var result = ArgumentParser.Parse(new[] { "graphics", "C:\\proj\\My.ap20", "--out", "C:\\tmp\\x.xml" });
        Assert.IsType<ParseResult.Failure>(result);
    }

    [Fact]
    public void Graphics_ExportAndImportTogether_Fails()
    {
        var result = ArgumentParser.Parse(new[]
        {
            "graphics", "C:\\proj\\My.ap20",
            "--export", "Pump", "--out", "C:\\tmp\\x.xml",
            "--import", "C:\\tmp\\y.xml",
        });

        var failure = Assert.IsType<ParseResult.Failure>(result);
        Assert.Contains("mutually exclusive", failure.Message);
    }

    [Fact]
    public void Graphics_RepeatedImport_CollectsEveryFile()
    {
        var result = ArgumentParser.Parse(new[]
        {
            "graphics", "C:\\proj\\My.ap20",
            "--import", "C:\\tmp\\a.xml",
            "--import", "C:\\tmp\\b.xml",
            "--overwrite",
        });

        var success = Assert.IsType<ParseResult.GraphicsSuccess>(result);
        Assert.Equal(2, success.Options.ImportFiles.Count);
        Assert.True(success.Options.Overwrite);
    }

    /// <summary>Paths are made absolute at parse time — Openness rejects a relative one with an
    /// exception naming something else entirely (FI-68, see PathArguments).</summary>
    [Fact]
    public void Graphics_RelativePaths_AreMadeAbsolute()
    {
        var result = ArgumentParser.Parse(new[]
        {
            "graphics", "C:\\proj\\My.ap20", "--export", "Pump", "--out", "out\\Pump.xml",
        });

        var success = Assert.IsType<ParseResult.GraphicsSuccess>(result);
        Assert.True(System.IO.Path.IsPathRooted(success.Options.OutPath));
    }

    /// <summary>The two second-dispatch switches every new subcommand gets forgotten in. `hmi`
    /// shipped once, built clean, passed the whole suite and still died here at runtime.</summary>
    [Fact]
    public void Graphics_IsHandledByBothCommonAccessors()
    {
        var result = ArgumentParser.Parse(new[] { "graphics", "C:\\proj\\My.ap20" });

        Assert.Equal("C:\\proj\\My.ap20", ArgumentParser.ProjectIdentifier(result));
        var common = ArgumentParser.CommonOptions(result);
        Assert.Equal(ArgumentParser.DefaultTimeoutConnectSeconds, common.TimeoutConnectSeconds);
    }

    /// <summary>
    /// Both graphics exceptions must be classified as CommandError. The HMI family shipped
    /// unclassified twice — the second time within hours of the first being fixed — and a refusal
    /// reported as an internal fault buries the message, which on this command IS the finding.
    /// </summary>
    [Fact]
    public void GraphicsExceptionsAreClassifiedAsCommandErrors()
    {
        var notFound = new GraphicNotFoundException("Pump", new[] { "Valve", "Motor" });
        var importFailed = new GraphicImportFailedException("C:\\tmp\\a.xml", "because");

        Assert.Equal(ExitCodes.CommandError, ExitCodes.ForException(notFound));
        Assert.Equal(ExitCodes.CommandError, ExitCodes.ForException(importFailed));
    }

    /// <summary>The not-found message names what IS there. A bare "no such graphic" leaves the
    /// caller guessing whether they mistyped the name or pointed at the wrong project.</summary>
    [Fact]
    public void GraphicNotFound_NamesTheAvailableGraphics()
    {
        var ex = new GraphicNotFoundException("Pump", new[] { "Valve", "Motor" });
        Assert.Contains("Valve", ex.Message);
        Assert.Contains("Motor", ex.Message);
    }
}
