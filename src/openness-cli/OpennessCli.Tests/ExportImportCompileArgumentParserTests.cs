using OpennessCli.Cli;
using Xunit;

namespace OpennessCli.Tests;

public class ExportImportCompileArgumentParserTests
{
    [Fact]
    public void Parse_Export_WithRequiredFlags_Succeeds()
    {
        var result = ArgumentParser.Parse(new[] { "export", "MyProject", "--block", "PlantAutoControl", "--out", "C:\\out.xml" });

        var success = Assert.IsType<ParseResult.ExportSuccess>(result);
        Assert.Equal("MyProject", success.Options.ProjectIdentifier);
        Assert.Equal("PlantAutoControl", success.Options.BlockName);
        Assert.Equal("C:\\out.xml", success.Options.OutPath);
        Assert.Null(success.Options.Device);
    }

    [Fact]
    public void Parse_Export_WithDevice_Succeeds()
    {
        var result = ArgumentParser.Parse(new[]
        {
            "export", "MyProject", "--block", "PlantAutoControl", "--out", "C:\\out.xml", "--device", "station_2",
        });

        var success = Assert.IsType<ParseResult.ExportSuccess>(result);
        Assert.Equal("station_2", success.Options.Device);
    }

    [Fact]
    public void Parse_Export_MissingBlock_Fails()
    {
        var result = ArgumentParser.Parse(new[] { "export", "MyProject", "--out", "C:\\out.xml" });
        Assert.IsType<ParseResult.Failure>(result);
    }

    [Fact]
    public void Parse_Export_MissingOut_Fails()
    {
        var result = ArgumentParser.Parse(new[] { "export", "MyProject", "--block", "PlantAutoControl" });
        Assert.IsType<ParseResult.Failure>(result);
    }

    [Fact]
    public void Parse_Export_MissingProject_Fails()
    {
        var result = ArgumentParser.Parse(new[] { "export", "--block", "PlantAutoControl", "--out", "C:\\out.xml" });
        Assert.IsType<ParseResult.Failure>(result);
    }

    [Fact]
    public void Parse_Import_WithGroupAndFiles_Succeeds()
    {
        var result = ArgumentParser.Parse(new[]
        {
            "import", "MyProject", "--group", "station_2/JOB9002_PLC/Control", "C:\\a.xml", "C:\\b.xml",
        });

        var success = Assert.IsType<ParseResult.ImportSuccess>(result);
        Assert.Equal("MyProject", success.Options.ProjectIdentifier);
        Assert.Equal("station_2/JOB9002_PLC/Control", success.Options.GroupPath);
        Assert.Equal(new[] { "C:\\a.xml", "C:\\b.xml" }, success.Options.Files);
    }

    [Fact]
    public void Parse_Import_MissingGroup_Fails()
    {
        var result = ArgumentParser.Parse(new[] { "import", "MyProject", "C:\\a.xml" });
        Assert.IsType<ParseResult.Failure>(result);
    }

    [Fact]
    public void Parse_Import_MissingFiles_Fails()
    {
        var result = ArgumentParser.Parse(new[] { "import", "MyProject", "--group", "station_2/PLC/Control" });
        Assert.IsType<ParseResult.Failure>(result);
    }

    [Fact]
    public void Parse_Compile_WithProjectOnly_UsesDefaults()
    {
        var result = ArgumentParser.Parse(new[] { "compile", "MyProject" });

        var success = Assert.IsType<ParseResult.CompileSuccess>(result);
        Assert.Equal("MyProject", success.Options.ProjectIdentifier);
        Assert.Null(success.Options.Device);
        Assert.False(success.Options.Json);
    }

    [Fact]
    public void Parse_Compile_WithDeviceAndJson_Succeeds()
    {
        var result = ArgumentParser.Parse(new[] { "compile", "MyProject", "--device", "station_2", "--json" });

        var success = Assert.IsType<ParseResult.CompileSuccess>(result);
        Assert.Equal("station_2", success.Options.Device);
        Assert.True(success.Options.Json);
        Assert.Null(success.Options.Block);
    }

    [Fact]
    public void Parse_Compile_WithBlock_Succeeds()
    {
        var result = ArgumentParser.Parse(new[] { "compile", "MyProject", "--block", "NodeStatusAlarms", "--device", "station_1" });

        var success = Assert.IsType<ParseResult.CompileSuccess>(result);
        Assert.Equal("NodeStatusAlarms", success.Options.Block);
        Assert.Equal("station_1", success.Options.Device);
    }

    [Fact]
    public void Parse_Delete_WithBlockAndYes_Succeeds()
    {
        var result = ArgumentParser.Parse(new[] { "delete", "MyProject", "--block", "PlantAutoControl", "--yes" });

        var success = Assert.IsType<ParseResult.DeleteSuccess>(result);
        Assert.Equal("MyProject", success.Options.ProjectIdentifier);
        Assert.Equal("PlantAutoControl", success.Options.BlockName);
        Assert.True(success.Options.Confirm);
        Assert.Null(success.Options.Device);
    }

    [Fact]
    public void Parse_Delete_WithoutYes_ConfirmIsFalse()
    {
        var result = ArgumentParser.Parse(new[] { "delete", "MyProject", "--block", "PlantAutoControl" });

        var success = Assert.IsType<ParseResult.DeleteSuccess>(result);
        Assert.False(success.Options.Confirm);
    }

    [Fact]
    public void Parse_Delete_WithDevice_Succeeds()
    {
        var result = ArgumentParser.Parse(new[] { "delete", "MyProject", "--block", "PlantAutoControl", "--device", "station_2", "--yes" });

        var success = Assert.IsType<ParseResult.DeleteSuccess>(result);
        Assert.Equal("station_2", success.Options.Device);
    }

    [Fact]
    public void Parse_Delete_MissingBlock_Fails()
    {
        var result = ArgumentParser.Parse(new[] { "delete", "MyProject", "--yes" });
        Assert.IsType<ParseResult.Failure>(result);
    }

    [Fact]
    public void Parse_Delete_MissingProject_Fails()
    {
        var result = ArgumentParser.Parse(new[] { "delete", "--block", "PlantAutoControl", "--yes" });
        Assert.IsType<ParseResult.Failure>(result);
    }

    [Fact]
    public void Parse_UnknownSubcommand_ListsAllSix()
    {
        var result = ArgumentParser.Parse(new[] { "bogus" });
        var failure = Assert.IsType<ParseResult.Failure>(result);
        Assert.Contains("list", failure.Message);
        Assert.Contains("export", failure.Message);
        Assert.Contains("import", failure.Message);
        Assert.Contains("compile", failure.Message);
        Assert.Contains("delete", failure.Message);
        Assert.Contains("sanity-check", failure.Message);
    }

    [Fact]
    public void Parse_SanityCheck_WithProjectOnly_UsesDefaults()
    {
        var result = ArgumentParser.Parse(new[] { "sanity-check", "MyProject" });

        var success = Assert.IsType<ParseResult.SanityCheckSuccess>(result);
        Assert.Equal("MyProject", success.Options.ProjectIdentifier);
        Assert.False(success.Options.Json);
        Assert.Equal(ArgumentParser.DefaultTimeoutConnectSeconds, success.Options.TimeoutConnectSeconds);
        Assert.Equal(ArgumentParser.DefaultTimeoutOpenSeconds, success.Options.TimeoutOpenSeconds);
    }

    [Fact]
    public void Parse_SanityCheck_WithJson_Succeeds()
    {
        var result = ArgumentParser.Parse(new[] { "sanity-check", "MyProject", "--json" });

        var success = Assert.IsType<ParseResult.SanityCheckSuccess>(result);
        Assert.True(success.Options.Json);
    }

    [Fact]
    public void Parse_SanityCheck_MissingProject_Fails()
    {
        var result = ArgumentParser.Parse(new[] { "sanity-check", "--json" });
        Assert.IsType<ParseResult.Failure>(result);
    }
}
