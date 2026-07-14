using System.Xml.Linq;
using Converter;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// Arithmetic support — `Mul`/`Convert` (S1 item 18, 2026-07-12). Picked up per the project
/// owner's own explicit decision after `Title`: 5 of `FC PlantAutoControl`'s 8 dependency FBs were
/// blocked by `Mul`/`Convert`, the larger of the two remaining real gaps by block count.
///
/// Grounded first (two independent real instances, `FB MotorDOL`/`FB EquipmentControlSystem`) before any
/// code, per CLAUDE.md hard rule 3. The real surprise: despite `DisabledENO="true"` on both
/// (matching Move/WAND's own precedent), `eno` is genuinely wired in practice — each network has
/// three `Mul`-&gt;`Convert` pairs where the `Mul`'s own `eno` output feeds the following
/// `Convert`'s own `en` directly, a genuine control-flow chain ("only run `Convert` if `Mul`
/// succeeded") unlike anything built earlier this session (every `en`/`IN` before this has been
/// independently rail-fed or contact-gated, never fed by a *preceding box instruction's own
/// output port*). Confirmed not universal: `FB ShredderControlSystem` has a standalone `Convert` with a
/// plain, independently rail-fed `en` (no `Mul` involved) — the ordinary case, already fully
/// covered by the existing `TraceChain` mechanism.
///
/// Modeled via a new `EnSource` concept (`Condition` | `PrecedingEno`) rather than folding the
/// ENO-chained case into `Expr` — there's no tag to reference "the preceding instruction's own
/// success" by (`Mul`/`Convert` have no `Instance` element, unlike `TON`/`CALL`'s own named
/// instance path). Readable-form mirrors the existing `TRUE` sentinel precedent: `EN := ENO`
/// means "gated by the immediately preceding statement's own ENO" (project owner confirmed this
/// design before implementation, once the ENO-chaining finding changed this item's own shape from
/// what grounding was expected to confirm).
///
/// `Mul`'s own type is genuinely different from every other typed instruction built so far:
/// `&lt;AutomaticTyped Name="SrcType" /&gt;`, a self-closing element with no value at all (TIA
/// infers the type from the connected operands rather than declaring it statically) — nothing to
/// carry, only the shape to validate. `Convert` is typed *between* two types (`SrcType`/
/// `DestType`, e.g. `Real`-&gt;`DInt`), both carried sidecar-only.
/// </summary>
public class MulConvertTests
{
    private static FlgNetwork LoadFixture(string name)
    {
        var element = XElement.Load(Path.Combine("Fixtures", name));
        return FlgNetParser.Parse(element);
    }

    [Fact]
    public void Parse_ConvertStandaloneFedByRail_ProducesMoveAndConvertParts()
    {
        var network = LoadFixture("ConvertStandaloneFedByRail.xml");

        var convert = Assert.Single(network.Parts, p => p.Name == "Convert");
        Assert.Equal(26, convert.UId);
        Assert.Equal("Int", convert.SrcType);
        Assert.Equal("Int", convert.DestType);
        Assert.False(convert.AutomaticSrcType);
    }

    [Fact]
    public void Reduce_ConvertStandaloneFedByRail_EnIsTrueConditionAndValueResolves()
    {
        var network = LoadFixture("ConvertStandaloneFedByRail.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Speed conversion", compileUnitUId: "17");

        var convert = Assert.Single(reduced.Network.Converts);
        var enCondition = Assert.IsType<EnSource.Condition>(convert.En);
        var enExpr = Assert.IsType<Expr.And>(enCondition.Value);
        Assert.Empty(enExpr.Operands);
        Assert.Equal("StagingWord", Assert.IsType<Expr.TagRef>(convert.In).Path);
        Assert.Equal("ConvertedResult", convert.DestTag);
    }

    [Fact]
    public void Reduce_ConvertStandaloneFedByRail_SidecarRecordsConditionEnSource()
    {
        var network = LoadFixture("ConvertStandaloneFedByRail.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Speed conversion", compileUnitUId: "17");

        var sidecar = Assert.Single(reduced.Sidecar.Converts);
        var enSidecar = Assert.IsType<EnSourceSidecar.ConditionSidecar>(sidecar.En);
        Assert.Equal(27, enSidecar.RailWireUId);
        Assert.Empty(enSidecar.Steps);
        Assert.Equal("Int", sidecar.SrcType);
        Assert.Equal("Int", sidecar.DestType);
    }

    [Fact]
    public void RoundTrip_ConvertStandaloneFedByRail_RebuildsIdenticalTopology()
    {
        var original = LoadFixture("ConvertStandaloneFedByRail.xml");
        var reduced = GraphReducer.Reduce(original, networkNumber: 1, title: "Speed conversion", compileUnitUId: "17");

        var rebuilt = FlgNetBuilder.Build(reduced.Network, reduced.Sidecar);
        var xml = FlgNetWriter.Write(rebuilt);
        var reparsed = FlgNetParser.Parse(xml);

        Assert.Equal(original.Parts.Count, reparsed.Parts.Count);
        Assert.Equal(original.Wires.Count, reparsed.Wires.Count);

        var convert = Assert.Single(reparsed.Parts, p => p.Name == "Convert");
        Assert.Equal("Int", convert.SrcType);
        Assert.Equal("Int", convert.DestType);

        var railWire = Assert.Single(reparsed.Wires, w => w.UId == 27);
        Assert.Equal(3, railWire.Endpoints.Count);
        Assert.Contains(railWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 26 && e.PortName == "en");
    }

    [Fact]
    public void SerializeNetworkOnly_ConvertStandaloneFedByRail_ProducesReadableSyntax()
    {
        var network = LoadFixture("ConvertStandaloneFedByRail.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Speed conversion", compileUnitUId: "17");

        var text = IrSerializer.SerializeNetworkOnly(reduced.Network);

        Assert.Equal(
            "NETWORK 1 \"Speed conversion\"\n" +
            "  MOVE(EN := TRUE, IN := RawInput) => StagingWord\n" +
            "  CONVERT(EN := TRUE, IN := StagingWord) => ConvertedResult\n",
            text);
    }

    [Fact]
    public void Parse_MulConvertEnoChainedPair_ProducesMulAndConvertParts()
    {
        var network = LoadFixture("MulConvertEnoChainedPair.xml");

        var mul = Assert.Single(network.Parts, p => p.Name == "Mul");
        Assert.Equal(36, mul.UId);
        Assert.Equal(2, mul.Cardinality);
        Assert.True(mul.AutomaticSrcType);
        Assert.Null(mul.SrcType);

        var convert = Assert.Single(network.Parts, p => p.Name == "Convert");
        Assert.Equal(37, convert.UId);
        Assert.Equal("Real", convert.SrcType);
        Assert.Equal("DInt", convert.DestType);
    }

    [Fact]
    public void Reduce_MulConvertEnoChainedPair_MulIsRailFedConvertIsEnoChained()
    {
        var network = LoadFixture("MulConvertEnoChainedPair.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Speed scale and convert", compileUnitUId: "1C");

        var mul = Assert.Single(reduced.Network.Muls);
        var mulEn = Assert.IsType<EnSource.Condition>(mul.En);
        Assert.Empty(Assert.IsType<Expr.And>(mulEn.Value).Operands);
        Assert.Equal(2, mul.Inputs.Count);
        Assert.Equal("SpeedRawA", Assert.IsType<Expr.TagRef>(mul.Inputs[0]).Path);
        Assert.Equal("ScaleFactor", Assert.IsType<Expr.TagRef>(mul.Inputs[1]).Path);
        Assert.Equal("SpeedScaled", mul.DestTag);

        var convert = Assert.Single(reduced.Network.Converts);
        Assert.IsType<EnSource.PrecedingEno>(convert.En);
        Assert.Equal("SpeedScaledSource", Assert.IsType<Expr.TagRef>(convert.In).Path);
        Assert.Equal("SpeedConverted", convert.DestTag);
    }

    [Fact]
    public void Reduce_MulConvertEnoChainedPair_SidecarRecordsPrecedingEnoReference()
    {
        var network = LoadFixture("MulConvertEnoChainedPair.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Speed scale and convert", compileUnitUId: "1C");

        var mulSidecar = Assert.Single(reduced.Sidecar.Muls);
        var mulEnSidecar = Assert.IsType<EnSourceSidecar.ConditionSidecar>(mulSidecar.En);
        Assert.Equal(42, mulEnSidecar.RailWireUId);

        var convertSidecar = Assert.Single(reduced.Sidecar.Converts);
        var convertEnSidecar = Assert.IsType<EnSourceSidecar.PrecedingEnoSidecar>(convertSidecar.En);
        Assert.Equal(36, convertEnSidecar.PrecedingPartUId);
        Assert.Equal(45, convertEnSidecar.WireUId);
    }

    [Fact]
    public void RoundTrip_MulConvertEnoChainedPair_RebuildsEnoWireWithNoRail()
    {
        var original = LoadFixture("MulConvertEnoChainedPair.xml");
        var reduced = GraphReducer.Reduce(original, networkNumber: 1, title: "Speed scale and convert", compileUnitUId: "1C");

        var rebuilt = FlgNetBuilder.Build(reduced.Network, reduced.Sidecar);
        var xml = FlgNetWriter.Write(rebuilt);
        var reparsed = FlgNetParser.Parse(xml);

        Assert.Equal(original.Parts.Count, reparsed.Parts.Count);
        Assert.Equal(original.Wires.Count, reparsed.Wires.Count);

        // The eno->en wire has exactly two endpoints, no Powerrail — genuinely different from
        // every other `en` wire built this session.
        var enoWire = Assert.Single(reparsed.Wires, w => w.UId == 45);
        Assert.Equal(2, enoWire.Endpoints.Count);
        Assert.DoesNotContain(enoWire.Endpoints, e => e.Kind == EndpointKind.Powerrail);
        Assert.Contains(enoWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 36 && e.PortName == "eno");
        Assert.Contains(enoWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 37 && e.PortName == "en");

        var mul = Assert.Single(reparsed.Parts, p => p.Name == "Mul");
        Assert.True(mul.AutomaticSrcType);
    }

    [Fact]
    public void SerializeNetworkOnly_MulConvertEnoChainedPair_ProducesEnoSentinel()
    {
        var network = LoadFixture("MulConvertEnoChainedPair.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Speed scale and convert", compileUnitUId: "1C");

        var text = IrSerializer.SerializeNetworkOnly(reduced.Network);

        Assert.Equal(
            "NETWORK 1 \"Speed scale and convert\"\n" +
            "  MUL(EN := TRUE, IN1 := SpeedRawA, IN2 := ScaleFactor) => SpeedScaled\n" +
            "  CONVERT(EN := ENO, IN := SpeedScaledSource) => SpeedConverted\n",
            text);
    }

    [Fact]
    public void FullBlock_MulConvertEnoChainedPair_ParseThenSerialize_IsByteIdentical()
    {
        var network = LoadFixture("MulConvertEnoChainedPair.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Speed scale and convert", compileUnitUId: "1C");

        var block = new IrBlock("0", "FB", "TestBlock", 1, "LAD", null, new[] { reduced.Network });
        var text = IrSerializer.SerializeBlock(block, new[] { reduced.Sidecar });

        var (parsedBlock, parsedSidecars) = IrParser.ParseBlock(text);
        var reserialized = IrSerializer.SerializeBlock(parsedBlock, parsedSidecars);

        Assert.Equal(text, reserialized);
    }

    [Fact]
    public void Parse_MulMissingDisabledEno_ThrowsUnsupportedConstruct()
    {
        var xml = """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Part Name="Mul" UId="1">
                  <TemplateValue Name="Card" Type="Cardinality">2</TemplateValue>
                  <AutomaticTyped Name="SrcType" />
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
    public void Parse_MulMissingAutomaticTyped_ThrowsSimaticMlFormatException()
    {
        var xml = """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Part Name="Mul" UId="1" DisabledENO="true">
                  <TemplateValue Name="Card" Type="Cardinality">2</TemplateValue>
                </Part>
                <Part Name="Coil" UId="2" />
              </Parts>
              <Wires />
            </FlgNet>
            """;

        var element = XElement.Parse(xml);

        var ex = Assert.Throws<SimaticMlFormatException>(() => FlgNetParser.Parse(element));
        Assert.Contains("AutomaticTyped", ex.Message);
    }

    [Fact]
    public void Parse_ConvertMissingDestType_ThrowsSimaticMlFormatException()
    {
        var xml = """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Part Name="Convert" UId="1" DisabledENO="true">
                  <TemplateValue Name="SrcType" Type="Type">Int</TemplateValue>
                </Part>
                <Part Name="Coil" UId="2" />
              </Parts>
              <Wires />
            </FlgNet>
            """;

        var element = XElement.Parse(xml);

        var ex = Assert.Throws<SimaticMlFormatException>(() => FlgNetParser.Parse(element));
        Assert.Contains("DestType", ex.Message);
    }

    // A genuine second real shape for Mul's own type — found live-verifying S1 item 20 against
    // FB AirStar (Mul UId=43): an ordinary <TemplateValue Name="SrcType" Type="Type">Real
    // </TemplateValue>, the same explicit shape Convert/comparisons already use, instead of the
    // self-closing <AutomaticTyped /> this class's own earlier tests (above) confirmed universal
    // from MotorDOL/EquipmentControlSystem. Neither shape is assumed to be "the real one" — both are modeled.
    [Fact]
    public void Parse_MulWithExplicitSrcType_ProducesMulPartWithSrcTypeNotAutomatic()
    {
        var network = LoadFixture("MulWithExplicitSrcType.xml");

        var mul = Assert.Single(network.Parts, p => p.Name == "Mul");
        Assert.False(mul.AutomaticSrcType);
        Assert.Equal("Real", mul.SrcType);
    }

    [Fact]
    public void Reduce_MulWithExplicitSrcType_SidecarRecordsSrcType()
    {
        var network = LoadFixture("MulWithExplicitSrcType.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Explicit-type multiply", compileUnitUId: "3");

        var mul = Assert.Single(reduced.Network.Muls);
        Assert.Equal("FactorA", Assert.IsType<Expr.TagRef>(mul.Inputs[0]).Path);
        Assert.Equal("FactorB", Assert.IsType<Expr.TagRef>(mul.Inputs[1]).Path);

        var mulSidecar = Assert.Single(reduced.Sidecar.Muls);
        Assert.Equal("Real", mulSidecar.SrcType);
    }

    [Fact]
    public void RoundTrip_MulWithExplicitSrcType_RebuildsTemplateValueNotAutomaticTyped()
    {
        var original = LoadFixture("MulWithExplicitSrcType.xml");
        var reduced = GraphReducer.Reduce(original, networkNumber: 1, title: "Explicit-type multiply", compileUnitUId: "3");

        var rebuilt = FlgNetBuilder.Build(reduced.Network, reduced.Sidecar);
        var xml = FlgNetWriter.Write(rebuilt);
        var reparsed = FlgNetParser.Parse(xml);

        var mulPart = Assert.Single(reparsed.Parts, p => p.Name == "Mul");
        Assert.False(mulPart.AutomaticSrcType);
        Assert.Equal("Real", mulPart.SrcType);
    }

    [Fact]
    public void FullBlock_MulWithExplicitSrcType_ParseThenSerialize_IsByteIdentical()
    {
        var network = LoadFixture("MulWithExplicitSrcType.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Explicit-type multiply", compileUnitUId: "3");

        var block = new IrBlock("0", "FC", "TestBlock", 1, "LAD", "A test block", new[] { reduced.Network });
        var text = IrSerializer.SerializeBlock(block, new[] { reduced.Sidecar });

        Assert.Contains("    srctype = Real\n", text);

        var (parsedBlock, parsedSidecars) = IrParser.ParseBlock(text);
        var reserialized = IrSerializer.SerializeBlock(parsedBlock, parsedSidecars);

        Assert.Equal(text, reserialized);
    }

    [Fact]
    public void Parse_MulWithBothAutomaticTypedAndSrcType_ThrowsUnsupportedConstruct()
    {
        var xml = """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Part Name="Mul" UId="1" DisabledENO="true">
                  <TemplateValue Name="Card" Type="Cardinality">2</TemplateValue>
                  <AutomaticTyped Name="SrcType" />
                  <TemplateValue Name="SrcType" Type="Type">Real</TemplateValue>
                </Part>
                <Part Name="Coil" UId="2" />
              </Parts>
              <Wires />
            </FlgNet>
            """;

        var element = XElement.Parse(xml);

        var ex = Assert.Throws<UnsupportedConstructException>(() => FlgNetParser.Parse(element));
        Assert.Contains("both", ex.Message);
    }

    // Real bug, found live 2026-07-14 (`FB MotorStarter`'s own "HMI Times" network, discovered via
    // the full export/convert/import/compile/re-export cycle run against every block already in
    // the scratch project): FlgNetBuilder.Build processed every Mul in one loop then every Convert
    // in a second loop, so two independent Mul->Convert chains that are genuinely interleaved in
    // the real source (`Mul, Convert, Mul, Convert`) got rebuilt grouped by kind (`Mul, Mul,
    // Convert, Convert`) — TIA's own Import() validator rejects this ("The elements must be sorted
    // according to the current flow"). Fixed by merge-sorting both lists on their own real
    // MulPartUId/ConvertPartUId before building.
    [Fact]
    public void RoundTrip_TwoIndependentMulConvertChains_PreservesInterleavedPartOrder()
    {
        var original = LoadFixture("TwoIndependentMulConvertChainsInterleaved.xml");
        var reduced = GraphReducer.Reduce(original, networkNumber: 1, title: "Two independent scale chains", compileUnitUId: "1C");

        var rebuilt = FlgNetBuilder.Build(reduced.Network, reduced.Sidecar);

        var order = rebuilt.Parts.Select(p => p.Name).ToList();
        Assert.Equal(new[] { "Mul", "Convert", "Mul", "Convert" }, order);
    }
}
