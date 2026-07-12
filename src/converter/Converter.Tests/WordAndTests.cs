using System.Xml.Linq;
using Converter;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// Bitwise/word-level AND (`Part Name="And"`) — S1 item 12, 2026-07-12. Grounded against a real
/// export, `FB VSDUpdateComs` (`Word AND 16#89 -> ControlWord`), found while searching 28 real
/// LAD blocks specifically for a boolean parallel-branch "AND-merge" (`ir/SPEC.md`'s original,
/// speculative "`A` (AND-merge)" table row) — that shape was never found real anywhere in the
/// sweep. This is something structurally different: a boxed, typed, N-input bitwise instruction,
/// closest to `Move` (side effect gated by `en`, same `TraceChain` fan-out-tap mechanism) crossed
/// with an OR-merge's own `Cardinality`-driven multi-operand shape (here driving input count, not
/// branch count) and a comparison's own `SrcType`. IR keyword is `WAND` (Word AND), not `AND` —
/// deliberately avoiding a collision with the existing boolean `AND` infix operator; a
/// converter-owned vendor-neutral name mapping, same precedent as `MOVE_BLK_VARIANT` -&gt; `MOVE`.
///
/// Also surfaced by this grounding: Siemens' own `&lt;base&gt;#&lt;value&gt;` numeric-literal notation
/// (`16#89`) wasn't recognized by `IrParser.ParseLeaf`'s shape-based literal detection (only
/// `T#`-prefixed and bare-integer shapes were) — a real, previously-unexercised gap, fixed
/// alongside this work.
/// </summary>
public class WordAndTests
{
    private static FlgNetwork LoadFixture(string name)
    {
        var element = XElement.Load(Path.Combine("Fixtures", name));
        return FlgNetParser.Parse(element);
    }

    [Fact]
    public void Parse_WordAndFedByContacts_ProducesAndPartWithCardinalityAndSrcType()
    {
        var network = LoadFixture("WordAndFedByContacts.xml");

        var wordAnd = Assert.Single(network.Parts, p => p.Name == "And");
        Assert.Equal(36, wordAnd.UId);
        Assert.Equal(2, wordAnd.Cardinality);
        Assert.Equal("Word", wordAnd.SrcType);
    }

    [Fact]
    public void Reduce_WordAndFedByContacts_ProducesWordAndStatement()
    {
        var network = LoadFixture("WordAndFedByContacts.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Status word update", compileUnitUId: "17");

        Assert.Equal(3, reduced.Network.Assignments.Count);
        var wordAnd = Assert.Single(reduced.Network.WordAnds);

        // The And's own "en" is fed directly by Powerrail (shared with three sibling Contacts on
        // the same wire, confirmed real) — no Contact of its own in between, so its condition is
        // the "always on" empty-And sentinel, same as any other zero-step chain.
        var enCondition = Assert.IsType<Expr.And>(wordAnd.En);
        Assert.Empty(enCondition.Operands);

        Assert.Equal(2, wordAnd.Inputs.Count);
        Assert.Equal("StatusWord", Assert.IsType<Expr.TagRef>(wordAnd.Inputs[0]).Path);
        Assert.Equal("16#89", Assert.IsType<Expr.Literal>(wordAnd.Inputs[1]).Value);
        Assert.Equal("CommandWord", wordAnd.DestTag);
    }

    [Fact]
    public void Reduce_WordAndFedByContacts_SidecarRecordsFields()
    {
        var network = LoadFixture("WordAndFedByContacts.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Status word update", compileUnitUId: "17");

        var sidecar = Assert.Single(reduced.Sidecar.WordAnds);
        Assert.Equal(36, sidecar.AndPartUId);
        Assert.Equal(37, sidecar.RailWireUId);
        Assert.Empty(sidecar.Steps);
        Assert.Equal("Word", sidecar.SrcType);

        Assert.Equal(2, sidecar.Inputs.Count);
        var firstInput = Assert.IsType<OperandSidecar.TagOperand>(sidecar.Inputs[0]);
        Assert.Equal(27, firstInput.AccessUId);
        var secondInput = Assert.IsType<OperandSidecar.LiteralOperand>(sidecar.Inputs[1]);
        Assert.Equal(28, secondInput.ConstantUId);

        Assert.Equal(29, sidecar.DestAccessUId);
        Assert.Equal(49, sidecar.DestWireUId);
    }

    [Fact]
    public void RoundTrip_WordAndFedByContacts_RebuildsIdenticalTopology()
    {
        var original = LoadFixture("WordAndFedByContacts.xml");
        var reduced = GraphReducer.Reduce(original, networkNumber: 1, title: "Status word update", compileUnitUId: "17");

        var rebuilt = FlgNetBuilder.Build(reduced.Network, reduced.Sidecar);
        var xml = FlgNetWriter.Write(rebuilt);
        var reparsed = FlgNetParser.Parse(xml);

        Assert.Equal(original.Parts.Count, reparsed.Parts.Count);
        Assert.Equal(original.Wires.Count, reparsed.Wires.Count);

        var wordAnd = Assert.Single(reparsed.Parts, p => p.Name == "And");
        Assert.Equal(36, wordAnd.UId);
        Assert.Equal(2, wordAnd.Cardinality);
        Assert.Equal("Word", wordAnd.SrcType);

        // The shared rail wire ends up with all 4 real rail-facing endpoints (3 sibling Contacts
        // plus the And's own "en") — not just the ones any single production happened to add.
        var railWire = Assert.Single(reparsed.Wires, w => w.UId == 37);
        Assert.Equal(5, railWire.Endpoints.Count);
        Assert.Contains(railWire.Endpoints, e => e.Kind == EndpointKind.Powerrail);
        Assert.Contains(railWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 30 && e.PortName == "in");
        Assert.Contains(railWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 32 && e.PortName == "in");
        Assert.Contains(railWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 34 && e.PortName == "in");
        Assert.Contains(railWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 36 && e.PortName == "en");

        var destWire = Assert.Single(reparsed.Wires, w => w.UId == 49);
        Assert.Contains(destWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 36 && e.PortName == "out");
        Assert.Contains(destWire.Endpoints, e => e.Kind == EndpointKind.IdentCon && e.UId == 29);
    }

    [Fact]
    public void SerializeNetworkOnly_WordAndFedByContacts_ProducesWandStatementSyntax()
    {
        var network = LoadFixture("WordAndFedByContacts.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Status word update", compileUnitUId: "17");

        var text = IrSerializer.SerializeNetworkOnly(reduced.Network);

        Assert.Contains("  WAND(EN := TRUE, IN1 := StatusWord, IN2 := 16#89) => CommandWord\n", text);
    }

    [Fact]
    public void FullBlock_WordAnd_ParseThenSerialize_IsByteIdentical()
    {
        var network = LoadFixture("WordAndFedByContacts.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Status word update", compileUnitUId: "17");

        var block = new IrBlock("0", "FB", "TestBlock", 1, "LAD", "A test block", new[] { reduced.Network });
        var text = IrSerializer.SerializeBlock(block, new[] { reduced.Sidecar });

        var (parsedBlock, parsedSidecars) = IrParser.ParseBlock(text);
        var reserialized = IrSerializer.SerializeBlock(parsedBlock, parsedSidecars);

        Assert.Equal(text, reserialized);
    }

    [Fact]
    public void Parse_WordAndMissingDisabledEno_ThrowsUnsupportedConstruct()
    {
        var xml = """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Part Name="And" UId="1">
                  <TemplateValue Name="Card" Type="Cardinality">2</TemplateValue>
                  <TemplateValue Name="SrcType" Type="Type">Word</TemplateValue>
                </Part>
                <Part Name="Coil" UId="2" />
              </Parts>
              <Wires />
            </FlgNet>
            """;

        var element = XElement.Parse(xml);

        var ex = Assert.Throws<UnsupportedConstructException>(() => FlgNetParser.Parse(element));
        Assert.Contains("DisabledENO", ex.Message);
    }

    [Fact]
    public void Parse_WordAndMissingSrcType_ThrowsSimaticMlFormatException()
    {
        var xml = """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Part Name="And" UId="1" DisabledENO="true">
                  <TemplateValue Name="Card" Type="Cardinality">2</TemplateValue>
                </Part>
                <Part Name="Coil" UId="2" />
              </Parts>
              <Wires />
            </FlgNet>
            """;

        var element = XElement.Parse(xml);

        var ex = Assert.Throws<SimaticMlFormatException>(() => FlgNetParser.Parse(element));
        Assert.Contains("SrcType", ex.Message);
    }
}
