using System.Xml.Linq;
using Converter;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

public class ToXmlTests
{
    private static FlgNetwork LoadFixture(string name)
    {
        var element = XElement.Load(Path.Combine("Fixtures", name));
        return FlgNetParser.Parse(element);
    }

    [Fact]
    public void RoundTrip_SimpleAndCoil_IsSemanticallyEquivalentToFixture()
    {
        var original = LoadFixture("SimpleAndCoil.xml");
        var reduced = GraphReducer.Reduce(original, networkNumber: 1, title: "Test AND", compileUnitUId: "3");

        var rebuilt = FlgNetBuilder.Build(reduced.Network, reduced.Sidecar);

        // Structural comparison (ir/SPEC.md: not byte-for-byte at this layer, only IR
        // self-stability claims that) — same access nodes, same parts, same wire topology,
        // same UIds throughout since the sidecar preserves them exactly.
        AssertSameAccessNodes(original.AccessNodes, rebuilt.AccessNodes);
        AssertSameParts(original.Parts, rebuilt.Parts);
        AssertSameWires(original.Wires, rebuilt.Wires);
    }

    [Fact]
    public void RoundTrip_WrittenXml_ParsesBackToTheSameNetwork()
    {
        var original = LoadFixture("SimpleAndCoil.xml");
        var reduced = GraphReducer.Reduce(original, networkNumber: 1, title: "Test AND", compileUnitUId: "3");
        var rebuilt = FlgNetBuilder.Build(reduced.Network, reduced.Sidecar);

        var xml = FlgNetWriter.Write(rebuilt);
        var reparsed = FlgNetParser.Parse(xml);

        AssertSameAccessNodes(original.AccessNodes, reparsed.AccessNodes);
        AssertSameParts(original.Parts, reparsed.Parts);
        AssertSameWires(original.Wires, reparsed.Wires);
    }

    private static void AssertSameAccessNodes(IReadOnlyList<AccessNode> expected, IReadOnlyList<AccessNode> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        foreach (var e in expected)
        {
            var a = Assert.Single(actual, x => x.UId == e.UId);
            Assert.Equal(e.Scope, a.Scope);
            Assert.Equal(e.ComponentPath, a.ComponentPath);
        }
    }

    private static void AssertSameParts(IReadOnlyList<PartNode> expected, IReadOnlyList<PartNode> actual)
    {
        Assert.Equal(
            expected.OrderBy(p => p.UId).Select(p => (p.UId, p.Name)),
            actual.OrderBy(p => p.UId).Select(p => (p.UId, p.Name)));
    }

    private static void AssertSameWires(IReadOnlyList<WireNode> expected, IReadOnlyList<WireNode> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        foreach (var e in expected)
        {
            var a = Assert.Single(actual, x => x.UId == e.UId);
            Assert.Equal(
                e.Endpoints.Select(ep => (ep.Kind, ep.UId, ep.PortName)),
                a.Endpoints.Select(ep => (ep.Kind, ep.UId, ep.PortName)));
        }
    }
}
