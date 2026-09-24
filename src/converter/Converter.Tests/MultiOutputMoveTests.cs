using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// Two widenings found blocking the same live-run block as MAX/MIN (2026-09-24; fixtures invented):
/// <list type="bullet">
/// <item>A MOVE with <c>Card</c> &gt; 1 is a MULTI-OUTPUT move — one <c>in</c> copied to <c>out1</c>..<c>outN</c>,
/// each wired to its own tag — read as <c>MOVE(EN := c, IN := x) => A, B, C</c>. It was refused on the guess that
/// a larger Card meant a block copy.</item>
/// <item>ADD as an ENO <i>producer</i> (a DIV -> ADD -> SUB chain), previously held back as ungrounded.</item>
/// </list>
/// </summary>
public class MultiOutputMoveTests
{
    private const string Ns = "http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5";

    private const string MultiMoveXml = """
        <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
          <Parts>
            <Access Scope="GlobalVariable" UId="21"><Symbol><Component Name="DemoData" /><Component Name="Gate" /></Symbol></Access>
            <Access Scope="LiteralConstant" UId="22"><Constant><ConstantType>Real</ConstantType><ConstantValue>0.0</ConstantValue></Constant></Access>
            <Access Scope="GlobalVariable" UId="23"><Symbol><Component Name="DemoData" /><Component Name="OutA" /></Symbol></Access>
            <Access Scope="GlobalVariable" UId="24"><Symbol><Component Name="DemoData" /><Component Name="OutB" /></Symbol></Access>
            <Access Scope="GlobalVariable" UId="25"><Symbol><Component Name="DemoData" /><Component Name="OutC" /></Symbol></Access>
            <Part Name="Contact" UId="31" />
            <Part Name="Move" UId="32" DisabledENO="true">
              <TemplateValue Name="Card" Type="Cardinality">3</TemplateValue>
            </Part>
          </Parts>
          <Wires>
            <Wire UId="41"><Powerrail /><NameCon UId="31" Name="in" /></Wire>
            <Wire UId="42"><IdentCon UId="21" /><NameCon UId="31" Name="operand" /></Wire>
            <Wire UId="43"><NameCon UId="31" Name="out" /><NameCon UId="32" Name="en" /></Wire>
            <Wire UId="44"><IdentCon UId="22" /><NameCon UId="32" Name="in" /></Wire>
            <Wire UId="45"><NameCon UId="32" Name="out1" /><IdentCon UId="23" /></Wire>
            <Wire UId="46"><NameCon UId="32" Name="out2" /><IdentCon UId="24" /></Wire>
            <Wire UId="47"><NameCon UId="32" Name="out3" /><IdentCon UId="25" /></Wire>
          </Wires>
        </FlgNet>
        """;

    private const string ReadableMove =
        "NETWORK 1 \"Clear\"\n" +
        "  MOVE(EN := DemoData.Gate, IN := 0.0) => DemoData.OutA, DemoData.OutB, DemoData.OutC\n";

    private static ReducedNetwork ReduceMultiMove() =>
        GraphReducer.Reduce(FlgNetParser.Parse(XElement.Parse(MultiMoveXml)), networkNumber: 1, title: "Clear", compileUnitUId: "3");

    private static XElement Move(XElement flgNet) =>
        flgNet.Element(XName.Get("Parts", Ns))!.Elements(XName.Get("Part", Ns)).Single(e => e.Attribute("Name")!.Value == "Move");

    [Fact]
    public void ToIr_ListsEveryOutput()
    {
        var reduced = ReduceMultiMove();

        Assert.Equal(ReadableMove, IrSerializer.SerializeNetworkOnly(reduced.Network));
        Assert.Equal(new[] { "DemoData.OutA", "DemoData.OutB", "DemoData.OutC" }, Assert.Single(reduced.Network.Moves).DestTags);
    }

    [Fact]
    public void SidecarRoundTrip_WritesCardAndEveryOutputWire()
    {
        var reduced = ReduceMultiMove();
        var block = new IrBlock("0", "FC", "DemoBlock", 1, "LAD", "A test block", new[] { reduced.Network });
        var text = IrSerializer.SerializeBlock(block, new[] { reduced.Sidecar });
        var (parsedBlock, parsedSidecars) = IrParser.ParseBlock(text);
        Assert.Equal(text, IrSerializer.SerializeBlock(parsedBlock, parsedSidecars));

        var flgNet = FlgNetWriter.Write(FlgNetBuilder.Build(parsedBlock.Networks[0], parsedSidecars[0]));

        Assert.Equal("3", Move(flgNet).Element(XName.Get("TemplateValue", Ns))!.Value);
        var reparsed = FlgNetParser.Parse(flgNet);
        foreach (var (port, access) in new[] { ("out1", 23), ("out2", 24), ("out3", 25) })
        {
            Assert.Contains(reparsed.Wires, w =>
                w.Endpoints[0] is { Kind: EndpointKind.NameCon, UId: 32 } first && first.PortName == port
                && w.Endpoints[1] is { Kind: EndpointKind.IdentCon } second && second.UId == access);
        }
    }

    [Fact]
    public void Synthesis_WritesCardAndEveryOutput()
    {
        var network = IrParser.ParseNetworkOnly(ReadableMove);

        var flgNet = FlgNetWriter.Write(FlgNetBuilder.Build(network, SidecarSynthesizer.Synthesize(network)));

        Assert.Equal("3", Move(flgNet).Element(XName.Get("TemplateValue", Ns))!.Value);
        var ports = FlgNetParser.Parse(flgNet).Wires.Select(w => w.Endpoints[0].PortName).ToList();
        Assert.Contains("out2", ports);
        Assert.Contains("out3", ports);
    }

    // A single-output MOVE is untouched: no Cardinality on its PartNode, fixed `Card` 1 on the way out.
    [Fact]
    public void SingleOutputMove_IsUnchanged()
    {
        var network = IrParser.ParseNetworkOnly(
            "NETWORK 1 \"One\"\n  MOVE(EN := Gate, IN := 1) => Dest\n");

        var flgNet = FlgNetWriter.Write(FlgNetBuilder.Build(network, SidecarSynthesizer.Synthesize(network)));

        Assert.Equal("1", Move(flgNet).Element(XName.Get("TemplateValue", Ns))!.Value);
        Assert.Empty(Assert.Single(network.Moves).AdditionalDestTags);
    }

    // Every analysis that asks "what does this MOVE write" must see all of its outputs.
    [Fact]
    public void TagReferences_SeeEveryOutputAsAWrite()
    {
        var network = IrParser.ParseNetworkOnly(ReadableMove);
        var writes = TagReferences.AllDirectedUsages(network)
            .Where(u => u.Direction == TagDirection.Write)
            .Select(u => u.Path)
            .ToList();

        Assert.Contains("DemoData.OutB", writes);
        Assert.Contains("DemoData.OutC", writes);
    }

    [Fact]
    public void AddAsAnEnoProducer_ReducesToAnEnoChain()
    {
        const string xml = """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Access Scope="GlobalVariable" UId="21"><Symbol><Component Name="A" /></Symbol></Access>
                <Access Scope="GlobalVariable" UId="22"><Symbol><Component Name="B" /></Symbol></Access>
                <Access Scope="GlobalVariable" UId="23"><Symbol><Component Name="Sum" /></Symbol></Access>
                <Access Scope="GlobalVariable" UId="24"><Symbol><Component Name="Diff" /></Symbol></Access>
                <Part Name="Add" UId="31" DisabledENO="true">
                  <TemplateValue Name="Card" Type="Cardinality">2</TemplateValue>
                  <AutomaticTyped Name="SrcType" />
                </Part>
                <Part Name="Sub" UId="32" DisabledENO="true">
                  <AutomaticTyped Name="SrcType" />
                </Part>
              </Parts>
              <Wires>
                <Wire UId="41"><Powerrail /><NameCon UId="31" Name="en" /></Wire>
                <Wire UId="42"><IdentCon UId="21" /><NameCon UId="31" Name="in1" /></Wire>
                <Wire UId="43"><IdentCon UId="22" /><NameCon UId="31" Name="in2" /></Wire>
                <Wire UId="44"><NameCon UId="31" Name="eno" /><NameCon UId="32" Name="en" /></Wire>
                <Wire UId="45"><NameCon UId="31" Name="out" /><IdentCon UId="23" /></Wire>
                <Wire UId="46"><IdentCon UId="21" /><NameCon UId="32" Name="in1" /></Wire>
                <Wire UId="47"><IdentCon UId="22" /><NameCon UId="32" Name="in2" /></Wire>
                <Wire UId="48"><NameCon UId="32" Name="out" /><IdentCon UId="24" /></Wire>
              </Wires>
            </FlgNet>
            """;

        var reduced = GraphReducer.Reduce(FlgNetParser.Parse(XElement.Parse(xml)), networkNumber: 1, title: "T", compileUnitUId: "3");

        Assert.IsType<EnSource.PrecedingEno>(reduced.Network.Muls[1].En);
        var eno = Assert.IsType<EnSourceSidecar.PrecedingEnoSidecar>(reduced.Sidecar.Muls[1].En);
        Assert.Equal(31, eno.PrecedingPartUId);
    }
}
