using System.Xml.Linq;
using Converter;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// Sub (subtract)/Div (divide) — confirmed real 2026-07-14, grounding `FB MotorVSDSystem`'s own
/// dependency closure (`FC Scale`, a small project utility FC, `PlantAutoControl` round-trip plan
/// Phase 1). Structurally close to `Mul`/`Add` (`DisabledENO="true"`, `AutomaticTyped SrcType`,
/// `en`/`in1`/`in2`/`out`/`eno` ports, modeled via the same `MulKind`/`MulStatement`) but with one
/// genuine difference: **no `&lt;TemplateValue Name="Card"&gt;` element at all** — Sub/Div are
/// always binary (confirmed: every real instance seen uses exactly `in1`/`in2`, never a chain),
/// whereas Mul/Add always carry `Card="2"`. `PartNode.Cardinality` is left `null` for Sub/Div
/// specifically (defaulted to 2 only during reduction, never regenerated as a `Card` element on
/// write) — this is the one place Sub/Div's own model deliberately diverges from Mul/Add's.
///
/// Also confirmed real in the same FC: a `Sub`'s own `eno` chaining into a following `Div`'s own
/// `en` — extending `ResolveEnSource`'s ENO-chain-producer check (previously `Mul`/`Convert` only)
/// to include `Sub`/`Div` too. `Add` as a *producer* in this role remains unconfirmed (see
/// `TonrTests`'s own note) — not added, same "don't guess" discipline.
/// </summary>
public class SubDivTests
{
    private static FlgNetwork LoadFixture(string name)
    {
        var element = XElement.Load(Path.Combine("Fixtures", name));
        return FlgNetParser.Parse(element);
    }

    [Fact]
    public void Parse_SubDivChain_ProducesSubAndDivPartsWithNullCardinality()
    {
        var network = LoadFixture("SubDivChain.xml");

        var sub = Assert.Single(network.Parts, p => p.Name == "Sub");
        Assert.Null(sub.Cardinality);
        Assert.True(sub.AutomaticSrcType);

        var div = Assert.Single(network.Parts, p => p.Name == "Div");
        Assert.Null(div.Cardinality);
        Assert.True(div.AutomaticSrcType);
    }

    [Fact]
    public void Reduce_SubDivChain_SubEnIsOrdinaryConditionDivEnIsEnoChained()
    {
        var network = LoadFixture("SubDivChain.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Sub then Div", compileUnitUId: "3");

        Assert.Equal(2, reduced.Network.Muls.Count);

        var sub = reduced.Network.Muls[0];
        Assert.Equal(MulKind.Subtract, sub.Kind);
        var condition = Assert.IsType<EnSource.Condition>(sub.En);
        var and = Assert.IsType<Expr.And>(condition.Value);
        Assert.Equal("Enable", Assert.IsType<Expr.TagRef>(and.Operands[0]).Path);
        var compare = Assert.IsType<Expr.Compare>(and.Operands[1]);
        Assert.Equal("<", compare.Operator);
        Assert.Equal("MinuendA", Assert.IsType<Expr.TagRef>(sub.Inputs[0]).Path);
        Assert.Equal("SubtrahendA", Assert.IsType<Expr.TagRef>(sub.Inputs[1]).Path);
        Assert.Equal("Difference", sub.DestTag);

        var div = reduced.Network.Muls[1];
        Assert.Equal(MulKind.Divide, div.Kind);
        Assert.IsType<EnSource.PrecedingEno>(div.En);
        Assert.Equal("DividendB", Assert.IsType<Expr.TagRef>(div.Inputs[0]).Path);
        Assert.Equal("DivisorB", Assert.IsType<Expr.TagRef>(div.Inputs[1]).Path);
        Assert.Equal("Quotient", div.DestTag);

        Assert.Equal(2, reduced.Sidecar.Muls.Count);
        Assert.IsType<EnSourceSidecar.ConditionSidecar>(reduced.Sidecar.Muls[0].En);
        Assert.IsType<EnSourceSidecar.PrecedingEnoSidecar>(reduced.Sidecar.Muls[1].En);
    }

    [Fact]
    public void RoundTrip_SubDivChain_RebuildsWithoutCardElement()
    {
        var original = LoadFixture("SubDivChain.xml");
        var reduced = GraphReducer.Reduce(original, networkNumber: 1, title: "Sub then Div", compileUnitUId: "3");

        var rebuilt = FlgNetBuilder.Build(reduced.Network, reduced.Sidecar);
        var xml = FlgNetWriter.Write(rebuilt);
        var reparsed = FlgNetParser.Parse(xml);

        var subPart = Assert.Single(reparsed.Parts, p => p.Name == "Sub");
        Assert.Null(subPart.Cardinality);
        var divPart = Assert.Single(reparsed.Parts, p => p.Name == "Div");
        Assert.Null(divPart.Cardinality);

        // The one shape-fidelity assertion that matters most here: no <TemplateValue Name="Card">
        // element anywhere in the regenerated XML for Sub/Div, matching the real source exactly.
        Assert.DoesNotContain(xml.Descendants().Where(e => e.Name.LocalName == "Part" && (e.Attribute("Name")?.Value is "Sub" or "Div")),
            part => part.Elements().Any(e => e.Name.LocalName == "TemplateValue" && e.Attribute("Name")?.Value == "Card"));
    }

    [Fact]
    public void SerializeNetworkOnly_SubDivChain_ProducesSubAndDivKeywords()
    {
        var network = LoadFixture("SubDivChain.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Sub then Div", compileUnitUId: "3");

        var text = IrSerializer.SerializeNetworkOnly(reduced.Network);

        Assert.Equal(
            "NETWORK 1 \"Sub then Div\"\n" +
            "  SUB(EN := Enable AND Level < 10, IN1 := MinuendA, IN2 := SubtrahendA) => Difference\n" +
            "  DIV(EN := ENO, IN1 := DividendB, IN2 := DivisorB) => Quotient\n",
            text);
    }

    [Fact]
    public void FullBlock_SubDivChain_ParseThenSerialize_IsByteIdentical()
    {
        var network = LoadFixture("SubDivChain.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Sub then Div", compileUnitUId: "3");

        var block = new IrBlock("0", "FC", "TestBlock", 1, "LAD", "A test block", new[] { reduced.Network });
        var text = IrSerializer.SerializeBlock(block, new[] { reduced.Sidecar });

        Assert.Contains("    kind = sub\n", text);
        Assert.Contains("    kind = div\n", text);

        var (parsedBlock, parsedSidecars) = IrParser.ParseBlock(text);
        var reserialized = IrSerializer.SerializeBlock(parsedBlock, parsedSidecars);

        Assert.Equal(text, reserialized);
    }
}
