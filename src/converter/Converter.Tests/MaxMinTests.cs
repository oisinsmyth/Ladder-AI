using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// `MAX` / `MIN` — the maximum/minimum selector boxes (ADR-0010 scope item, 2026-09-24). Grounded on a
/// live-run export (uncommittable; this fixture is invented and built fresh):
/// <c>&lt;Part Name="MAX" Version="1.0" UId="N" DisabledENO="true"&gt;&lt;TemplateValue Name="card"
/// Type="Cardinality"&gt;3&lt;/TemplateValue&gt;&lt;TemplateValue Name="value_type"
/// Type="Type"&gt;Real&lt;/TemplateValue&gt;&lt;/Part&gt;</c>, ports <c>en</c>, <c>IN1</c>..<c>INn</c>,
/// <c>OUT</c>.
///
/// Modelled as two more kinds of the ADD/MUL family (<see cref="MulKind.Maximum"/>/<see cref="MulKind.Minimum"/>)
/// — the same en-gated N-input one-output shape — so the readable line reads like ADD's:
/// <c>MAX(EN := TRUE, IN1 := a, IN2 := b, IN3 := c) => dest</c>. The fixture's MIN deliberately has
/// no DisabledENO (the ENO-enabled form) and a literal input, so both shapes are pinned.
/// </summary>
public class MaxMinTests
{
    private static FlgNetwork LoadFixture(string name) =>
        FlgNetParser.Parse(XElement.Load(Path.Combine("Fixtures", name)));

    private static ReducedNetwork Reduce() =>
        GraphReducer.Reduce(LoadFixture("MaxMinRailFed.xml"), networkNumber: 1, title: "Select", compileUnitUId: "3");

    private const string ReadableIr =
        "NETWORK 1 \"Select\"\n" +
        "  MAX(EN := TRUE, IN1 := DemoData.ValueA, IN2 := DemoData.ValueB, IN3 := DemoData.ValueC) => DemoData.Highest\n" +
        "  MIN(EN := TRUE, IN1 := DemoData.ValueA, IN2 := 5.0) => DemoData.Lowest\n";

    private static XElement PartElement(XElement flgNet, string name) =>
        flgNet.Elements().First(e => e.Name.LocalName == "Parts")
            .Elements().Single(e => e.Name.LocalName == "Part" && e.Attribute("Name")!.Value == name);

    private static TagTypeRegistry RealTypes() =>
        TagTypeRegistry.FromSources(
            System.Array.Empty<DbSource>(), System.Array.Empty<PlcTypeSource>(),
            new[]
            {
                new PlcTagSource("1", "ValueA", "Real", "%MD0", true, true, true, null),
                new PlcTagSource("2", "ValueB", "Real", "%MD4", true, true, true, null),
                new PlcTagSource("3", "Highest", "Real", "%MD8", true, true, true, null),
            });

    [Fact]
    public void Parse_ReadsVersionCardValueTypeAndEnoShape()
    {
        var network = LoadFixture("MaxMinRailFed.xml");

        var max = Assert.Single(network.Parts, p => p.Name == "MAX");
        Assert.Equal("1.0", max.Version);
        Assert.Equal(3, max.Cardinality);
        Assert.Equal("Real", max.SrcType);
        Assert.False(max.EnoEnabled);

        var min = Assert.Single(network.Parts, p => p.Name == "MIN");
        Assert.Equal(2, min.Cardinality);
        Assert.True(min.EnoEnabled); // no DisabledENO attribute in the source
    }

    [Fact]
    public void ToIr_ReadsLikeAdd()
    {
        Assert.Equal(ReadableIr, IrSerializer.SerializeNetworkOnly(Reduce().Network));
    }

    [Fact]
    public void SidecarRoundTrip_WritesTheSourceShapeBack()
    {
        var reduced = Reduce();

        var flgNet = FlgNetWriter.Write(FlgNetBuilder.Build(reduced.Network, reduced.Sidecar));

        var max = PartElement(flgNet, "MAX");
        Assert.Equal(new[] { "Name", "Version", "UId", "DisabledENO" }, max.Attributes().Select(a => a.Name.LocalName));
        Assert.Equal("true", max.Attribute("DisabledENO")!.Value);
        Assert.Equal(
            new[] { ("card", "Cardinality", "3"), ("value_type", "Type", "Real") },
            max.Elements().Select(e => (e.Attribute("Name")!.Value, e.Attribute("Type")!.Value, e.Value)));

        // The ENO-enabled MIN comes back without the attribute rather than having it silently added.
        Assert.Null(PartElement(flgNet, "MIN").Attribute("DisabledENO"));

        var reparsed = FlgNetParser.Parse(flgNet);
        var original = LoadFixture("MaxMinRailFed.xml");
        Assert.Equal(original.Parts.Count, reparsed.Parts.Count);
        Assert.Equal(original.Wires.Count, reparsed.Wires.Count);
        Assert.Contains(reparsed.Wires, w => w.Endpoints[0] is { Kind: EndpointKind.NameCon, UId: 31, PortName: "OUT" });
        Assert.Contains(reparsed.Wires, w => w.Endpoints.Any(e => e is { Kind: EndpointKind.NameCon, UId: 31, PortName: "IN3" }));
    }

    [Fact]
    public void FullBlock_ParseThenSerialize_IsByteIdentical()
    {
        var reduced = Reduce();
        var block = new IrBlock("0", "FC", "DemoBlock", 1, "LAD", "A test block", new[] { reduced.Network });
        var text = IrSerializer.SerializeBlock(block, new[] { reduced.Sidecar });

        var (parsedBlock, parsedSidecars) = IrParser.ParseBlock(text);

        Assert.Equal(text, IrSerializer.SerializeBlock(parsedBlock, parsedSidecars));
        Assert.Contains("    kind = max\n", text);
        Assert.Contains("    version = 1.0\n", text);
        Assert.Contains("    eno = enabled\n", text); // MIN only
    }

    [Fact]
    public void Synthesis_TypesTheBoxFromItsOperands()
    {
        var network = IrParser.ParseNetworkOnly(
            "NETWORK 1 \"Select\"\n" +
            "  MAX(EN := TRUE, IN1 := ValueA, IN2 := ValueB) => Highest\n");

        var sidecar = SidecarSynthesizer.Synthesize(network, new HashSet<string>(), callees: null, tagTypes: RealTypes());
        var flgNet = FlgNetWriter.Write(FlgNetBuilder.Build(network, sidecar));

        var max = PartElement(flgNet, "MAX");
        Assert.Equal("1.0", max.Attribute("Version")!.Value);
        Assert.Equal("true", max.Attribute("DisabledENO")!.Value);
        Assert.Equal(
            new[] { ("card", "2"), ("value_type", "Real") },
            max.Elements().Select(e => (e.Attribute("Name")!.Value, e.Value)));
    }

    [Fact]
    public void Synthesis_RefusesWhenNoTypeResolves()
    {
        var network = IrParser.ParseNetworkOnly(
            "NETWORK 1 \"Select\"\n" +
            "  MIN(EN := TRUE, IN1 := Unknown1, IN2 := Unknown2) => Unknown3\n");

        var ex = Assert.Throws<UnsupportedSynthesisConstructException>(() => SidecarSynthesizer.Synthesize(network));
        Assert.Contains("value_type", ex.Message);
    }

    // Box-to-box wiring is refused, exactly as it is for LIMIT and ADD: every input must be a tag or a
    // literal. MAX's OUT feeding MIN's IN1 directly has no IdentCon source to name.
    [Fact]
    public void BoxToBoxWiring_IsRefusedLikeLimitAndAdd()
    {
        const string xml = """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Access Scope="GlobalVariable" UId="21"><Symbol><Component Name="A" /></Symbol></Access>
                <Access Scope="GlobalVariable" UId="22"><Symbol><Component Name="B" /></Symbol></Access>
                <Access Scope="GlobalVariable" UId="23"><Symbol><Component Name="C" /></Symbol></Access>
                <Access Scope="GlobalVariable" UId="24"><Symbol><Component Name="D" /></Symbol></Access>
                <Part Name="MAX" Version="1.0" UId="31" DisabledENO="true">
                  <TemplateValue Name="card" Type="Cardinality">2</TemplateValue>
                  <TemplateValue Name="value_type" Type="Type">Real</TemplateValue>
                </Part>
                <Part Name="MIN" Version="1.0" UId="32" DisabledENO="true">
                  <TemplateValue Name="card" Type="Cardinality">2</TemplateValue>
                  <TemplateValue Name="value_type" Type="Type">Real</TemplateValue>
                </Part>
              </Parts>
              <Wires>
                <Wire UId="41"><Powerrail /><NameCon UId="31" Name="en" /><NameCon UId="32" Name="en" /></Wire>
                <Wire UId="42"><IdentCon UId="21" /><NameCon UId="31" Name="IN1" /></Wire>
                <Wire UId="43"><IdentCon UId="22" /><NameCon UId="31" Name="IN2" /></Wire>
                <Wire UId="44"><NameCon UId="31" Name="OUT" /><NameCon UId="32" Name="IN1" /></Wire>
                <Wire UId="45"><IdentCon UId="23" /><NameCon UId="32" Name="IN2" /></Wire>
                <Wire UId="46"><NameCon UId="32" Name="OUT" /><IdentCon UId="24" /></Wire>
              </Wires>
            </FlgNet>
            """;

        var network = FlgNetParser.Parse(XElement.Parse(xml));

        Assert.ThrowsAny<System.Exception>(() => GraphReducer.Reduce(network, networkNumber: 1, title: "T", compileUnitUId: "3"));
    }

    [Theory]
    [InlineData("<Part Name=\"MAX\" Version=\"1.0\" UId=\"1\" DisabledENO=\"false\"><TemplateValue Name=\"card\" Type=\"Cardinality\">2</TemplateValue><TemplateValue Name=\"value_type\" Type=\"Type\">Real</TemplateValue></Part>", "DisabledENO")]
    [InlineData("<Part Name=\"MAX\" Version=\"1.0\" UId=\"1\" DisabledENO=\"true\"><TemplateValue Name=\"card\" Type=\"Cardinality\">1</TemplateValue><TemplateValue Name=\"value_type\" Type=\"Type\">Real</TemplateValue></Part>", "2 or more")]
    [InlineData("<Part Name=\"MIN\" Version=\"1.0\" UId=\"1\" DisabledENO=\"true\"><TemplateValue Name=\"Card\" Type=\"Cardinality\">2</TemplateValue><TemplateValue Name=\"value_type\" Type=\"Type\">Real</TemplateValue></Part>", "Name=\"Card\"")]
    [InlineData("<Part Name=\"MIN\" Version=\"1.0\" UId=\"1\" DisabledENO=\"true\"><TemplateValue Name=\"card\" Type=\"Cardinality\">2</TemplateValue><AutomaticTyped Name=\"SrcType\" /></Part>", "AutomaticTyped")]
    public void Parse_RefusesUnobservedShapes(string part, string expectedInMessage)
    {
        var xml = "<FlgNet xmlns=\"http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5\"><Parts>"
            + part + "</Parts><Wires /></FlgNet>";

        var ex = Assert.Throws<UnsupportedConstructException>(() => FlgNetParser.Parse(XElement.Parse(xml)));
        Assert.Contains(expectedInMessage, ex.Message);
    }

    [Fact]
    public void Parse_MissingVersion_IsRefused()
    {
        const string xml = """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Part Name="MAX" UId="1" DisabledENO="true">
                  <TemplateValue Name="card" Type="Cardinality">2</TemplateValue>
                  <TemplateValue Name="value_type" Type="Type">Real</TemplateValue>
                </Part>
              </Parts>
              <Wires />
            </FlgNet>
            """;

        Assert.ThrowsAny<System.Exception>(() => FlgNetParser.Parse(XElement.Parse(xml)));
    }
}
