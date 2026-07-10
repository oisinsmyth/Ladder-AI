using System.Xml.Linq;
using Converter;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

public class ToIrTests
{
    private static FlgNetwork LoadFixture(string name)
    {
        var element = XElement.Load(Path.Combine("Fixtures", name));
        return FlgNetParser.Parse(element);
    }

    [Fact]
    public void Reduce_SeriesContactsIntoCoil_ProducesAndExpression()
    {
        var network = LoadFixture("SimpleAndCoil.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Test AND", compileUnitUId: "3");

        var assignment = Assert.Single(reduced.Network.Assignments);
        Assert.Equal("Output1", assignment.CoilTag);
        var and = Assert.IsType<Expr.And>(assignment.Condition);
        Assert.Equal(2, and.Operands.Count);
        Assert.Equal("Sensor1.Ok", Assert.IsType<Expr.TagRef>(and.Operands[0]).Path);
        Assert.Equal("Sensor2.Ok", Assert.IsType<Expr.TagRef>(and.Operands[1]).Path);
    }

    [Fact]
    public void Reduce_RecordsSidecarForExactRegeneration()
    {
        var network = LoadFixture("SimpleAndCoil.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Test AND", compileUnitUId: "3");

        Assert.Equal(1, reduced.Sidecar.NetworkNumber);
        Assert.Equal("3", reduced.Sidecar.CompileUnitUId);

        var assignment = Assert.Single(reduced.Sidecar.Assignments);
        Assert.Equal(41, assignment.RailWireUId);
        Assert.Equal(new[] { 31, 32 }, assignment.ContactUIds);
        Assert.Equal(33, assignment.CoilUId);
        Assert.Equal(new[] { 42, 43, 44, 45, 46 }, assignment.WireUIds);
        Assert.Contains(reduced.Sidecar.AccessUIds, a => a.TagPath == "Sensor1.Ok" && a.UId == 21);
        Assert.Contains(reduced.Sidecar.AccessUIds, a => a.TagPath == "Sensor2.Ok" && a.UId == 22);
        Assert.Contains(reduced.Sidecar.AccessUIds, a => a.TagPath == "Output1" && a.UId == 23);
    }

    [Fact]
    public void SerializeNetworkOnly_ProducesReadableAndExpression()
    {
        var network = LoadFixture("SimpleAndCoil.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Test AND", compileUnitUId: "3");

        var text = IrSerializer.SerializeNetworkOnly(reduced.Network);

        Assert.Equal("NETWORK 1 \"Test AND\"\n  COIL Output1 := Sensor1.Ok AND Sensor2.Ok\n", text);
    }
}
