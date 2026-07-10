using OpennessCli.Cli;
using Xunit;

namespace OpennessCli.Tests;

public class ArgumentParserTests
{
    [Fact]
    public void Parse_NoArgs_Fails()
    {
        var result = ArgumentParser.Parse(System.Array.Empty<string>());
        Assert.IsType<ParseResult.Failure>(result);
    }

    [Fact]
    public void Parse_UnknownSubcommand_Fails()
    {
        var result = ArgumentParser.Parse(new[] { "export", "C:\\proj" });
        var failure = Assert.IsType<ParseResult.Failure>(result);
        Assert.Contains("export", failure.Message);
    }

    [Fact]
    public void Parse_ListWithoutProjectIdentifier_Fails()
    {
        var result = ArgumentParser.Parse(new[] { "list", "--json" });
        Assert.IsType<ParseResult.Failure>(result);
    }

    [Fact]
    public void Parse_ListWithProjectIdentifier_UsesDefaults()
    {
        var result = ArgumentParser.Parse(new[] { "list", "C:\\proj\\My.ap20" });

        var success = Assert.IsType<ParseResult.ListSuccess>(result);
        Assert.Equal("C:\\proj\\My.ap20", success.Options.ProjectIdentifier);
        Assert.False(success.Options.Json);
        Assert.Null(success.Options.TiaInstallOverride);
        Assert.Equal(ArgumentParser.DefaultTimeoutConnectSeconds, success.Options.TimeoutConnectSeconds);
        Assert.Equal(ArgumentParser.DefaultTimeoutOpenSeconds, success.Options.TimeoutOpenSeconds);
    }

    [Fact]
    public void Parse_ListWithAllFlags_ParsesEachOne()
    {
        var result = ArgumentParser.Parse(new[]
        {
            "list", "C:\\proj\\My.ap20",
            "--json",
            "--tia-install", "C:\\custom\\dir",
            "--timeout-connect", "42",
            "--timeout-open", "999",
        });

        var success = Assert.IsType<ParseResult.ListSuccess>(result);
        Assert.Equal("C:\\proj\\My.ap20", success.Options.ProjectIdentifier);
        Assert.True(success.Options.Json);
        Assert.Equal("C:\\custom\\dir", success.Options.TiaInstallOverride);
        Assert.Equal(42, success.Options.TimeoutConnectSeconds);
        Assert.Equal(999, success.Options.TimeoutOpenSeconds);
    }

    [Fact]
    public void Parse_ProjectIdentifierCanComeAfterFlags()
    {
        var result = ArgumentParser.Parse(new[] { "list", "--json", "C:\\proj\\My.ap20" });

        var success = Assert.IsType<ParseResult.ListSuccess>(result);
        Assert.Equal("C:\\proj\\My.ap20", success.Options.ProjectIdentifier);
        Assert.True(success.Options.Json);
    }

    [Fact]
    public void Parse_UnknownFlag_Fails()
    {
        var result = ArgumentParser.Parse(new[] { "list", "C:\\proj", "--bogus" });
        var failure = Assert.IsType<ParseResult.Failure>(result);
        Assert.Contains("--bogus", failure.Message);
    }

    [Fact]
    public void Parse_ExtraPositionalArgument_Fails()
    {
        var result = ArgumentParser.Parse(new[] { "list", "C:\\proj", "C:\\extra" });
        Assert.IsType<ParseResult.Failure>(result);
    }

    [Fact]
    public void Parse_TiaInstallFlagMissingValue_Fails()
    {
        var result = ArgumentParser.Parse(new[] { "list", "C:\\proj", "--tia-install" });
        var failure = Assert.IsType<ParseResult.Failure>(result);
        Assert.Contains("--tia-install", failure.Message);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("notanumber")]
    public void Parse_TimeoutFlagWithInvalidValue_Fails(string badValue)
    {
        var result = ArgumentParser.Parse(new[] { "list", "C:\\proj", "--timeout-connect", badValue });
        Assert.IsType<ParseResult.Failure>(result);
    }
}
