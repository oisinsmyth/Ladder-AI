using System.Xml.Linq;
using Converter;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

public class IrSelfStabilityTests
{
    [Fact]
    public void NetworkOnly_ParseThenSerialize_IsByteIdentical()
    {
        const string text = "NETWORK 1 \"Test AND\"\n  COIL Output1 := Sensor1.Ok AND Sensor2.Ok\n";

        var network = IrParser.ParseNetworkOnly(text);
        var reserialized = IrSerializer.SerializeNetworkOnly(network);

        Assert.Equal(text, reserialized);
    }

    [Fact]
    public void FullBlock_ParseThenSerialize_IsByteIdentical()
    {
        var element = XElement.Load(Path.Combine("Fixtures", "SimpleAndCoil.xml"));
        var flgNetwork = FlgNetParser.Parse(element);
        var reduced = GraphReducer.Reduce(flgNetwork, networkNumber: 1, title: "Test AND", compileUnitUId: "3");

        var block = new IrBlock("0", "FC", "TestBlock", 1, "LAD", "A test block", new[] { reduced.Network });
        var text = IrSerializer.SerializeBlock(block, new[] { reduced.Sidecar });

        var (parsedBlock, parsedSidecars) = IrParser.ParseBlock(text);
        var reserialized = IrSerializer.SerializeBlock(parsedBlock, parsedSidecars);

        Assert.Equal(text, reserialized);
    }

    [Fact]
    public void FullBlock_WithNoComment_OmitsCommentLine()
    {
        var element = XElement.Load(Path.Combine("Fixtures", "SimpleAndCoil.xml"));
        var flgNetwork = FlgNetParser.Parse(element);
        var reduced = GraphReducer.Reduce(flgNetwork, networkNumber: 1, title: "Test AND", compileUnitUId: "3");

        var block = new IrBlock("0", "FC", "TestBlock", 1, "LAD", Comment: null, new[] { reduced.Network });
        var text = IrSerializer.SerializeBlock(block, new[] { reduced.Sidecar });

        Assert.DoesNotContain("COMMENT", text);
    }
}
