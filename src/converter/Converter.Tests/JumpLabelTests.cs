using System.IO;
using System.Linq;
using System.Xml.Linq;
using Converter.Ir;
using Converter.Preflight;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// Conditional jumps and their labels (ADR-0010 scope item, 2026-09-27; fixtures invented). A
/// <c>&lt;Part Name="Jump"&gt;</c> is a rung terminal like a coil whose operand, on port <c>label</c>, is a
/// <c>&lt;Access Scope="Label"&gt;&lt;Label Name="X" /&gt;</c>; the target network declares it in a
/// <c>&lt;Labels&gt;&lt;LabelDeclaration UId&gt;</c> ahead of its Parts. Readable form:
/// <c>JMP &lt;Label&gt; := &lt;condition&gt;</c> and a network-level <c>LABEL &lt;Name&gt;</c> line.
/// </summary>
public class JumpLabelTests
{
    // Two bare <FlgNet>s, compared through the Normalizer's single-document canonical form (UIds replaced
    // by content keys, order-insensitive where TIA's order is not meaningful).
    private static bool SameNetwork(XDocument a, XDocument b) =>
        Normalizer.Strip(a.Root!).ToString() == Normalizer.Strip(b.Root!).ToString();

    private static FlgNetwork Load(string name) => FlgNetParser.Parse(XElement.Load(Path.Combine("Fixtures", name)));

    private static (IrBlock Block, NetworkSidecar[] Sidecars) Block()
    {
        var jump = GraphReducer.Reduce(Load("JumpToLabel.xml"), 1, "Bypass", "3");
        var target = GraphReducer.Reduce(Load("LabelledNetwork.xml"), 2, "Target", "8");
        var block = new IrBlock("0", "FC", "DemoBlock", 1, "LAD", "A test block", new[] { jump.Network, target.Network });
        return (block, new[] { jump.Sidecar, target.Sidecar });
    }

    private const string ReadableIr =
        "NETWORK 1 \"Bypass\"\n" +
        "  JMP SkipAhead := DemoIn.Bypass\n" +
        "\n" +
        "NETWORK 2 \"Target\"\n" +
        "  LABEL SkipAhead\n" +
        "  COIL DemoOut.Lamp := DemoIn.Ready\n";

    [Fact]
    public void ToIr_ReadsTheJumpAndTheLabel()
    {
        var (block, _) = Block();

        var text = IrSerializer.SerializeNetworkOnly(block.Networks[0]) + "\n" + IrSerializer.SerializeNetworkOnly(block.Networks[1]);

        Assert.Equal(ReadableIr, text);
    }

    [Fact]
    public void SidecarRoundTrip_WritesTheSourceShapeBack()
    {
        var (block, sidecars) = Block();
        var text = IrSerializer.SerializeBlock(block, sidecars);
        var (parsed, parsedSidecars) = IrParser.ParseBlock(text);
        Assert.Equal(text, IrSerializer.SerializeBlock(parsed, parsedSidecars));

        var jumpNet = FlgNetWriter.Write(FlgNetBuilder.Build(parsed.Networks[0], parsedSidecars[0]));
        var targetNet = FlgNetWriter.Write(FlgNetBuilder.Build(parsed.Networks[1], parsedSidecars[1]));

        Assert.True(SameNetwork(
            new XDocument(XElement.Load(Path.Combine("Fixtures", "JumpToLabel.xml"))), new XDocument(jumpNet)));
        Assert.True(SameNetwork(
            new XDocument(XElement.Load(Path.Combine("Fixtures", "LabelledNetwork.xml"))), new XDocument(targetNet)));
        Assert.Equal("Labels", targetNet.Elements().First().Name.LocalName); // declared ahead of <Parts>, as TIA does
    }

    [Fact]
    public void Synthesis_WritesTheJumpAndTheLabelDeclaration()
    {
        var (block, _) = Block();
        var sidecars = SidecarSynthesizer.SynthesizeBlock(block);

        var jumpNet = FlgNetWriter.Write(FlgNetBuilder.Build(block.Networks[0], sidecars[0]));
        var targetNet = FlgNetWriter.Write(FlgNetBuilder.Build(block.Networks[1], sidecars[1]));

        Assert.True(SameNetwork(
            new XDocument(XElement.Load(Path.Combine("Fixtures", "JumpToLabel.xml"))), new XDocument(jumpNet)));
        Assert.True(SameNetwork(
            new XDocument(XElement.Load(Path.Combine("Fixtures", "LabelledNetwork.xml"))), new XDocument(targetNet)));
    }

    // Before this, every Label access in a network shared one Normalizer key, so a jump to the WRONG
    // label compared EQUAL — the false-pass direction.
    [Fact]
    public void Compare_TellsTwoLabelsApart()
    {
        var original = XElement.Load(Path.Combine("Fixtures", "JumpToLabel.xml"));
        var retargeted = XElement.Parse(original.ToString().Replace("SkipAhead", "Elsewhere"));

        Assert.False(SameNetwork(new XDocument(original), new XDocument(retargeted)));
    }

    // A label is not a tag: nothing that lists the tags a block reads or writes may report it.
    [Fact]
    public void TheLabelIsNotATag()
    {
        var (block, _) = Block();

        Assert.DoesNotContain("SkipAhead", TagReferences.AllTagPaths(block.Networks[0]));
        Assert.DoesNotContain(TagReferences.AllDirectedUsages(block.Networks[0]), u => u.Path == "SkipAhead");
        Assert.Contains("DemoIn.Bypass", TagReferences.AllTagPaths(block.Networks[0]));
    }

    [Fact]
    public void Preflight_AJumpToAnUndeclaredLabel_IsAFinding()
    {
        var (block, _) = Block();
        var broken = block with { Networks = new[] { block.Networks[0], block.Networks[1] with { Labels = System.Array.Empty<string>() } } };

        Assert.Empty(PreflightRunner.JumpLabelFindings(block));
        var finding = Assert.Single(PreflightRunner.JumpLabelFindings(broken));
        Assert.Contains("JMP SkipAhead", finding.Description);
    }

    [Fact]
    public void Preflight_ALabelDeclaredTwice_IsAFinding()
    {
        var (block, _) = Block();
        var doubled = block with { Networks = new[] { block.Networks[0] with { Labels = new[] { "SkipAhead" } }, block.Networks[1] } };

        var finding = Assert.Single(PreflightRunner.JumpLabelFindings(doubled));
        Assert.Contains("already declared", finding.Description);
    }

    // An unknown sibling of <Parts>/<Wires> used to be ignored — the way <Labels> was dropped before
    // this. Refused now, so the next unrecognized one fails loudly instead of vanishing.
    [Fact]
    public void AnUnknownFlgNetChild_IsRefused()
    {
        var xml = XElement.Load(Path.Combine("Fixtures", "LabelledNetwork.xml"));
        xml.AddFirst(new XElement(xml.Name.Namespace + "Mystery"));

        Assert.Throws<UnsupportedConstructException>(() => FlgNetParser.Parse(xml));
    }
}
