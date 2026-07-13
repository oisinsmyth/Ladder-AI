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
    public void Parse_Export_WithType_Succeeds()
    {
        var result = ArgumentParser.Parse(new[] { "export", "MyProject", "--type", "TypeDOL", "--out", "C:\\out.xml" });

        var success = Assert.IsType<ParseResult.ExportSuccess>(result);
        Assert.Equal("TypeDOL", success.Options.TypeName);
        Assert.Null(success.Options.BlockName);
    }

    [Fact]
    public void Parse_Export_BlockAndType_Fails()
    {
        var result = ArgumentParser.Parse(new[]
        {
            "export", "MyProject", "--block", "PlantAutoControl", "--type", "TypeDOL", "--out", "C:\\out.xml",
        });
        Assert.IsType<ParseResult.Failure>(result);
    }

    [Fact]
    public void Parse_Export_WithTagTable_Succeeds()
    {
        var result = ArgumentParser.Parse(new[] { "export", "MyProject", "--tagtable", "Control", "--out", "C:\\out.xml" });

        var success = Assert.IsType<ParseResult.ExportSuccess>(result);
        Assert.Equal("Control", success.Options.TagTableName);
        Assert.Null(success.Options.BlockName);
        Assert.Null(success.Options.TypeName);
    }

    [Fact]
    public void Parse_Export_BlockAndTagTable_Fails()
    {
        var result = ArgumentParser.Parse(new[]
        {
            "export", "MyProject", "--block", "PlantAutoControl", "--tagtable", "Control", "--out", "C:\\out.xml",
        });
        Assert.IsType<ParseResult.Failure>(result);
    }

    [Fact]
    public void Parse_Export_TypeAndTagTable_Fails()
    {
        var result = ArgumentParser.Parse(new[]
        {
            "export", "MyProject", "--type", "TypeDOL", "--tagtable", "Control", "--out", "C:\\out.xml",
        });
        Assert.IsType<ParseResult.Failure>(result);
    }

    [Fact]
    public void Parse_List_WithTagTables_SetsFlag()
    {
        var result = ArgumentParser.Parse(new[] { "list", "MyProject", "--tagtables" });

        var success = Assert.IsType<ParseResult.ListSuccess>(result);
        Assert.True(success.Options.TagTables);
    }

    [Fact]
    public void Parse_List_WithoutTagTables_FlagIsFalse()
    {
        var result = ArgumentParser.Parse(new[] { "list", "MyProject" });

        var success = Assert.IsType<ParseResult.ListSuccess>(result);
        Assert.False(success.Options.TagTables);
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
    public void Parse_Import_WithType_SetsAsType()
    {
        var result = ArgumentParser.Parse(new[]
        {
            "import", "MyProject", "--group", "station_2/JOB9002_PLC/Control", "--type", "C:\\TypeDOL.xml",
        });

        var success = Assert.IsType<ParseResult.ImportSuccess>(result);
        Assert.True(success.Options.AsType);
        Assert.Equal(new[] { "C:\\TypeDOL.xml" }, success.Options.Files);
    }

    [Fact]
    public void Parse_Import_WithoutType_AsTypeIsFalse()
    {
        var result = ArgumentParser.Parse(new[]
        {
            "import", "MyProject", "--group", "station_2/JOB9002_PLC/Control", "C:\\a.xml",
        });

        var success = Assert.IsType<ParseResult.ImportSuccess>(result);
        Assert.False(success.Options.AsType);
    }

    [Fact]
    public void Parse_Import_WithTagTable_SetsAsTagTable()
    {
        var result = ArgumentParser.Parse(new[]
        {
            "import", "MyProject", "--group", "station_2/JOB9002_PLC/Default tag table", "--tagtable", "C:\\Minimal.xml",
        });

        var success = Assert.IsType<ParseResult.ImportSuccess>(result);
        Assert.True(success.Options.AsTagTable);
        Assert.False(success.Options.AsType);
        Assert.Equal(new[] { "C:\\Minimal.xml" }, success.Options.Files);
    }

    [Fact]
    public void Parse_Import_TypeAndTagTable_Fails()
    {
        var result = ArgumentParser.Parse(new[]
        {
            "import", "MyProject", "--group", "station_2/JOB9002_PLC/Control", "--type", "--tagtable", "C:\\a.xml",
        });

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
    public void Parse_Compile_WithType_Succeeds()
    {
        var result = ArgumentParser.Parse(new[] { "compile", "MyProject", "--type", "TypeDOL", "--device", "station_1" });

        var success = Assert.IsType<ParseResult.CompileSuccess>(result);
        Assert.Equal("TypeDOL", success.Options.Type);
        Assert.Null(success.Options.Block);
    }

    [Fact]
    public void Parse_Compile_BlockAndType_Fails()
    {
        var result = ArgumentParser.Parse(new[] { "compile", "MyProject", "--block", "NodeStatusAlarms", "--type", "TypeDOL" });
        Assert.IsType<ParseResult.Failure>(result);
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
