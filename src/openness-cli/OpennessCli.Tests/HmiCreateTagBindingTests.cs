using OpennessCli.Cli;
using Xunit;

namespace OpennessCli.Tests;

/// <summary>
/// `hmi-create-tag --connection/--plc-tag`: a tag bound to a PLC tag needs both, and its type
/// follows the PLC tag, so an explicit --datatype alongside --plc-tag is a contradiction.
/// </summary>
public class HmiCreateTagBindingTests
{
    [Fact]
    public void BoundTag_ParsesConnectionAndPlcTag()
    {
        var result = ArgumentParser.Parse(new[]
        {
            "hmi-create-tag", "C:\\proj.ap20", "--name", "DemoWord", "--table", "Tags",
            "--connection", "HMI_Connection_1", "--plc-tag", "DemoDb.DemoWord", "--yes",
        });

        var options = Assert.IsType<ParseResult.HmiCreateTagSuccess>(result).Options;
        Assert.Equal("HMI_Connection_1", options.Connection);
        Assert.Equal("DemoDb.DemoWord", options.PlcTag);
        Assert.True(options.Confirm);
    }

    [Fact]
    public void InternalTag_HasNoBindingAndDefaultsToBool()
    {
        var result = ArgumentParser.Parse(new[] { "hmi-create-tag", "C:\\proj.ap20", "--name", "Demo", "--table", "Tags" });

        var options = Assert.IsType<ParseResult.HmiCreateTagSuccess>(result).Options;
        Assert.Null(options.Connection);
        Assert.Null(options.PlcTag);
        Assert.Equal("Bool", options.DataType);
    }

    [Theory]
    [InlineData("--connection", "HMI_Connection_1")]
    [InlineData("--plc-tag", "DemoDb.DemoWord")]
    public void OnlyOneOfConnectionAndPlcTag_IsRefused(string flag, string value)
    {
        var result = ArgumentParser.Parse(new[] { "hmi-create-tag", "C:\\proj.ap20", "--name", "Demo", "--table", "Tags", flag, value });

        var failure = Assert.IsType<ParseResult.Failure>(result);
        Assert.Contains("go together", failure.Message);
    }

    [Fact]
    public void DataTypeWithPlcTag_IsRefused()
    {
        var result = ArgumentParser.Parse(new[]
        {
            "hmi-create-tag", "C:\\proj.ap20", "--name", "Demo", "--table", "Tags",
            "--connection", "HMI_Connection_1", "--plc-tag", "DemoDb.DemoWord", "--datatype", "Word",
        });

        var failure = Assert.IsType<ParseResult.Failure>(result);
        Assert.Contains("follows the PLC tag", failure.Message);
    }
}
