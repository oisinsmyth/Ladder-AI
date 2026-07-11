using System.Xml.Linq;
using Converter;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// Comparisons (`Part Name="Eq"`/`"Ge"`) — S1's next converter capability after TON, 2026-07-11.
/// Grounded against a real export, `FC ControlDelays`: only `Eq`/`Ge` are directly observed
/// (`Ne`/`Le`/`Gt`/`Lt`'s Part Names are unconfirmed, same status as AND-merge — not built).
///
/// The key finding: a comparison behaves like a <b>Contact</b>, not like an OR-merge or TON — it's
/// a pass-through chain position, not a terminal leaf. Its own rail-facing/continuation port is
/// `pre` (genuinely different from a Contact's `in`, not a typo); after resolving its two
/// operands (`in1`/`in2`, tag or `LiteralConstant` literal), the backward trace continues from
/// `(ComparePartUId, "pre")` exactly as it would from a Contact's own `in`.
///
/// Composing with an OR-merge (as a branch, or feeding one) is real (`ControlDelays`' own `O(41)`
/// combines two comparisons) but deliberately not modeled this phase — the *existing* OR-merge
/// branch check and the *existing* wire fan-out check already safely refuse this shape without
/// any new code, proven by <see cref="Reduce_ComparisonAsOrMergeBranch_ThrowsNonReducible"/>.
/// </summary>
public class ComparisonTests
{
    private static FlgNetwork LoadFixture(string name)
    {
        var element = XElement.Load(Path.Combine("Fixtures", name));
        return FlgNetParser.Parse(element);
    }

    [Fact]
    public void Parse_EqFeedsContact_ProducesEqPartWithSrcType()
    {
        var network = LoadFixture("EqFeedsContact.xml");

        var eq = Assert.Single(network.Parts, p => p.Name == "Eq");
        Assert.Equal("Int", eq.SrcType);

        var literal = Assert.Single(network.Constants);
        Assert.Equal("Int", literal.ConstantType);
        Assert.Equal("1", literal.Value);
    }

    [Fact]
    public void Reduce_EqFeedsContact_ProducesCompareAndedWithContact()
    {
        var network = LoadFixture("EqFeedsContact.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Eq rail-facing", compileUnitUId: "3");

        var assignment = Assert.Single(reduced.Network.Assignments);
        Assert.Equal("Output1", assignment.CoilTag);
        var and = Assert.IsType<Expr.And>(assignment.Condition);
        Assert.Equal(2, and.Operands.Count);
        var compare = Assert.IsType<Expr.Compare>(and.Operands[0]);
        Assert.Equal("=", compare.Operator);
        Assert.Equal("Mode", Assert.IsType<Expr.TagRef>(compare.Left).Path);
        Assert.Equal("1", Assert.IsType<Expr.Literal>(compare.Right).Value);
        Assert.Equal("Enable", Assert.IsType<Expr.TagRef>(and.Operands[1]).Path);
    }

    [Fact]
    public void Reduce_EqFeedsContact_SidecarRecordsComparePartAndOperands()
    {
        var network = LoadFixture("EqFeedsContact.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Eq rail-facing", compileUnitUId: "3");

        var assignment = Assert.Single(reduced.Sidecar.Assignments);
        Assert.Equal(30, assignment.RailWireUId);
        var compareStep = Assert.IsType<ChainStepSidecar.CompareStep>(assignment.Steps[0]);
        Assert.Equal(25, compareStep.ComparePartUId);
        Assert.Equal("Eq", compareStep.PartName);
        Assert.Equal("Int", compareStep.SrcType);
        var leftTag = Assert.IsType<OperandSidecar.TagOperand>(compareStep.Left);
        Assert.Equal(21, leftTag.AccessUId);
        var rightLiteral = Assert.IsType<OperandSidecar.LiteralOperand>(compareStep.Right);
        Assert.Equal(22, rightLiteral.ConstantUId);
        Assert.IsType<ChainStepSidecar.ContactStep>(assignment.Steps[1]);
    }

    [Fact]
    public void RoundTrip_EqFeedsContact_RebuildsIdenticalTopology()
    {
        var original = LoadFixture("EqFeedsContact.xml");
        var reduced = GraphReducer.Reduce(original, networkNumber: 1, title: "Eq rail-facing", compileUnitUId: "3");

        var rebuilt = FlgNetBuilder.Build(reduced.Network, reduced.Sidecar);
        var xml = FlgNetWriter.Write(rebuilt);
        var reparsed = FlgNetParser.Parse(xml);

        var eq = Assert.Single(reparsed.Parts, p => p.Name == "Eq");
        Assert.Equal(25, eq.UId);
        Assert.Equal("Int", eq.SrcType);

        var preWire = Assert.Single(reparsed.Wires, w => w.UId == 30);
        Assert.Contains(preWire.Endpoints, e => e.Kind == EndpointKind.Powerrail);
        Assert.Contains(preWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 25 && e.PortName == "pre");

        var literal = Assert.Single(reparsed.Constants);
        Assert.Equal("Int", literal.ConstantType);
    }

    [Fact]
    public void SerializeNetworkOnly_EqFeedsContact_ProducesInfixEquals()
    {
        var network = LoadFixture("EqFeedsContact.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Eq rail-facing", compileUnitUId: "3");

        var text = IrSerializer.SerializeNetworkOnly(reduced.Network);

        Assert.Equal("NETWORK 1 \"Eq rail-facing\"\n  COIL Output1 := Mode = 1 AND Enable\n", text);
    }

    [Fact]
    public void Reduce_GeMidChain_ContinuesTraceFromPrePort()
    {
        var network = LoadFixture("GeMidChainFeedsCoil.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 2, title: "Ge mid-chain", compileUnitUId: "4");

        var assignment = Assert.Single(reduced.Network.Assignments);
        var and = Assert.IsType<Expr.And>(assignment.Condition);
        Assert.Equal("Enable", Assert.IsType<Expr.TagRef>(and.Operands[0]).Path);
        var compare = Assert.IsType<Expr.Compare>(and.Operands[1]);
        Assert.Equal(">=", compare.Operator);
        Assert.Equal("Level", Assert.IsType<Expr.TagRef>(compare.Left).Path);
        Assert.Equal("10", Assert.IsType<Expr.Literal>(compare.Right).Value);
    }

    [Fact]
    public void SerializeNetworkOnly_GeMidChain_ProducesInfixGreaterEquals()
    {
        var network = LoadFixture("GeMidChainFeedsCoil.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 2, title: "Ge mid-chain", compileUnitUId: "4");

        var text = IrSerializer.SerializeNetworkOnly(reduced.Network);

        Assert.Equal("NETWORK 2 \"Ge mid-chain\"\n  COIL Output2 := Enable AND Level >= 10\n", text);
    }

    [Fact]
    public void RoundTrip_GeMidChain_RebuildsPreFedByContactOut()
    {
        var original = LoadFixture("GeMidChainFeedsCoil.xml");
        var reduced = GraphReducer.Reduce(original, networkNumber: 2, title: "Ge mid-chain", compileUnitUId: "4");

        var rebuilt = FlgNetBuilder.Build(reduced.Network, reduced.Sidecar);
        var xml = FlgNetWriter.Write(rebuilt);
        var reparsed = FlgNetParser.Parse(xml);

        var midWire = Assert.Single(reparsed.Wires, w => w.UId == 32);
        Assert.Contains(midWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 25 && e.PortName == "out");
        Assert.Contains(midWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 26 && e.PortName == "pre");
    }

    [Fact]
    public void FullBlock_Comparisons_ParseThenSerialize_IsByteIdentical()
    {
        var eqNetwork = LoadFixture("EqFeedsContact.xml");
        var eqReduced = GraphReducer.Reduce(eqNetwork, networkNumber: 1, title: "Eq rail-facing", compileUnitUId: "3");
        var geNetwork = LoadFixture("GeMidChainFeedsCoil.xml");
        var geReduced = GraphReducer.Reduce(geNetwork, networkNumber: 2, title: "Ge mid-chain", compileUnitUId: "4");

        var block = new IrBlock("0", "FC", "TestBlock", 1, "LAD", "A test block", new[] { eqReduced.Network, geReduced.Network });
        var text = IrSerializer.SerializeBlock(block, new[] { eqReduced.Sidecar, geReduced.Sidecar });

        var (parsedBlock, parsedSidecars) = IrParser.ParseBlock(text);
        var reserialized = IrSerializer.SerializeBlock(parsedBlock, parsedSidecars);

        Assert.Equal(text, reserialized);
    }

    // The real, grounded ControlDelays shape (O(41) combining Ge(39)/Eq(40)) — deliberately not
    // supported this phase. Proves the *existing*, unmodified OR-merge branch check already
    // refuses it correctly, without any new hard-error code.
    [Fact]
    public void Reduce_ComparisonAsOrMergeBranch_ThrowsNonReducible()
    {
        var network = LoadFixture("OrMergeOfComparisons.xml");

        var ex = Assert.Throws<NonReducibleNetworkException>(
            () => GraphReducer.Reduce(network, networkNumber: 1, title: "OR of comparisons", compileUnitUId: "3"));

        Assert.Contains("not a Contact", ex.Message);
    }

    [Fact]
    public void Parse_UnconfirmedComparisonPartName_ThrowsUnsupportedConstruct()
    {
        var xml = """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Part Name="Ne" UId="1">
                  <TemplateValue Name="SrcType" Type="Type">Int</TemplateValue>
                </Part>
                <Part Name="Coil" UId="2" />
              </Parts>
              <Wires />
            </FlgNet>
            """;

        var element = XElement.Parse(xml);

        var ex = Assert.Throws<UnsupportedConstructException>(() => FlgNetParser.Parse(element));
        Assert.Contains("Ne", ex.Message);
    }
}
