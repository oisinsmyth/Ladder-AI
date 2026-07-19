using System.Linq;
using System.Xml.Linq;
using Converter.Ir;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// The verified `to-ir --no-sidecar` gate (ADR-0005 follow-on, 2026-07-19). Dropping a block's stored
/// sidecar is safe only if its *derived* form is semantically equivalent to the source export — "synthesis
/// succeeds" alone is not proof (a real block can synthesise-but-diverge). `SynthesizeReadableVerified`
/// derives a sidecar from the readable form, rebuilds the SimaticML, and Normalizer-compares it to the
/// source: it returns the readable-only text only when equivalent, and errors otherwise. These tests pin
/// both directions — the safety property that motivated porting the Normalizer into the converter.
/// </summary>
public class NoSidecarEquivalenceTests
{
    private static IrBlock Block() =>
        new("0", "FC", "T", 1, "LAD", null,
            new[] { IrParser.ParseNetworkOnly("NETWORK 1 \"N\"\n  COIL X := A AND NOT B\n") });

    // The block's own correct SimaticML — computed exactly as the gate does internally, then round-tripped
    // through a parse so it is file-shaped like a real `XDocument.Load`-ed export (the gate reloads its own
    // synth for the same reason). By construction this is the equivalent "source export" for the positive
    // case, and the base to perturb for the negative one.
    private static XDocument CorrectSource(IrBlock block)
    {
        var reparsed = IrParser.ParseBlockWithoutSidecar(IrSerializer.SerializeBlockReadable(block));
        return XDocument.Parse(Program.BuildBlockXml(reparsed, SidecarSynthesizer.SynthesizeBlock(reparsed)).ToString());
    }

    [Fact]
    public void DerivedFormMatchesSource_ReturnsReadableOnly()
    {
        var block = Block();

        var text = Program.SynthesizeReadableVerified(block, CorrectSource(block), callees: null, tagTypes: null);

        Assert.DoesNotContain("\nSIDECAR\n", text);
        Assert.Contains("COIL X := A AND NOT B", text);
    }

    [Fact]
    public void DerivedFormDivergesFromSource_ThrowsAndDoesNotOmit()
    {
        var block = Block();

        // Perturb one operand tag (A -> Z) in the otherwise-correct source, so the derived graph no longer
        // matches semantically — the exact "synthesises but diverges" hazard the gate exists to catch.
        var mutated = new XDocument(CorrectSource(block));
        var component = mutated.Descendants()
            .First(e => e.Name.LocalName == "Component" && (string?)e.Attribute("Name") == "A");
        component.SetAttributeValue("Name", "Z");

        var ex = Assert.Throws<UnsupportedSynthesisConstructException>(
            () => Program.SynthesizeReadableVerified(block, mutated, callees: null, tagTypes: null));
        Assert.Contains("semantically equivalent", ex.Message);
    }
}
