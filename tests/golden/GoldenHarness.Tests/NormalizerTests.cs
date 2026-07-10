using System.Xml.Linq;
using Xunit;

namespace GoldenHarness;

public class NormalizerTests
{
    [Fact]
    public void AreSemanticallyEquivalent_IdenticalDocuments_ReturnsTrue()
    {
        var a = XDocument.Parse("<Block><Name>Foo</Name></Block>");
        var b = XDocument.Parse("<Block><Name>Foo</Name></Block>");

        Assert.True(Normalizer.AreSemanticallyEquivalent(a, b));
    }

    [Fact]
    public void AreSemanticallyEquivalent_DifferingModifiedDate_ReturnsTrue()
    {
        var a = XDocument.Parse("<Block><Name>Foo</Name><ModifiedDate>2026-01-01T00:00:00Z</ModifiedDate></Block>");
        var b = XDocument.Parse("<Block><Name>Foo</Name><ModifiedDate>2026-07-10T09:30:00Z</ModifiedDate></Block>");

        Assert.True(Normalizer.AreSemanticallyEquivalent(a, b));
    }

    [Fact]
    public void AreSemanticallyEquivalent_DifferingCompileDate_ReturnsTrue()
    {
        var a = XDocument.Parse("<Block><CompileDate>2026-01-01T00:00:00Z</CompileDate></Block>");
        var b = XDocument.Parse("<Block><CompileDate>2026-07-10T09:30:00Z</CompileDate></Block>");

        Assert.True(Normalizer.AreSemanticallyEquivalent(a, b));
    }

    [Fact]
    public void AreSemanticallyEquivalent_DifferingHeaderVersion_ReturnsTrue()
    {
        var a = XDocument.Parse("<Block><HeaderVersion>0.1</HeaderVersion><Name>Foo</Name></Block>");
        var b = XDocument.Parse("<Block><HeaderVersion>0.2</HeaderVersion><Name>Foo</Name></Block>");

        Assert.True(Normalizer.AreSemanticallyEquivalent(a, b));
    }

    [Fact]
    public void AreSemanticallyEquivalent_DifferingActualContent_ReturnsFalse()
    {
        var a = XDocument.Parse("<Block><Name>Foo</Name></Block>");
        var b = XDocument.Parse("<Block><Name>Bar</Name></Block>");

        Assert.False(Normalizer.AreSemanticallyEquivalent(a, b));
    }

    [Fact]
    public void AreSemanticallyEquivalent_DifferingWireUId_SameEndpoints_ReturnsTrue()
    {
        // Reverses the original assumption behind this test — disproven by real data,
        // 2026-07-10 (first live reference-project round-trip): TIA reassigns Wire UId on every
        // import/compile cycle regardless of what the source/sidecar held (the shared rail wire
        // went from UId 126 to a fresh 81, every other wire shifted too), even though the set of
        // connections it makes was byte-identical. A wire's own UId isn't part of its identity —
        // see docs/notes/openness-quirks.md.
        var a = XDocument.Parse("<Wires><Wire UId=\"41\"><Powerrail/><NameCon UId=\"5\" Name=\"in\"/></Wire></Wires>");
        var b = XDocument.Parse("<Wires><Wire UId=\"99\"><Powerrail/><NameCon UId=\"5\" Name=\"in\"/></Wire></Wires>");

        Assert.True(Normalizer.AreSemanticallyEquivalent(a, b));
    }

    [Fact]
    public void AreSemanticallyEquivalent_DifferingWireEndpoint_ReturnsFalse()
    {
        // What actually must never be normalized away: which Access/Part UId a wire connects to
        // — that's a real semantic drift (rewired logic), not a volatile ID. Only the wire's own
        // UId is volatile; the UIds it references are a different thing and stay significant.
        var a = XDocument.Parse("<Wires><Wire UId=\"41\"><Powerrail/><NameCon UId=\"5\" Name=\"in\"/></Wire></Wires>");
        var b = XDocument.Parse("<Wires><Wire UId=\"41\"><Powerrail/><NameCon UId=\"6\" Name=\"in\"/></Wire></Wires>");

        Assert.False(Normalizer.AreSemanticallyEquivalent(a, b));
    }

    [Fact]
    public void AreSemanticallyEquivalent_ReorderedWires_ReturnsTrue()
    {
        // <Wire> order within <Wires> isn't semantically meaningful either — confirmed real,
        // 2026-07-10, same round-trip: TIA relocates the shared rail wire earlier in the list on
        // re-export instead of leaving it last (where BlockSourceWriter puts it).
        var a = XDocument.Parse(
            "<Wires>" +
            "<Wire UId=\"1\"><NameCon UId=\"5\" Name=\"in\"/></Wire>" +
            "<Wire UId=\"2\"><Powerrail/></Wire>" +
            "</Wires>");
        var b = XDocument.Parse(
            "<Wires>" +
            "<Wire UId=\"1\"><Powerrail/></Wire>" +
            "<Wire UId=\"2\"><NameCon UId=\"5\" Name=\"in\"/></Wire>" +
            "</Wires>");

        Assert.True(Normalizer.AreSemanticallyEquivalent(a, b));
    }

    [Fact]
    public void AreSemanticallyEquivalent_CodeBlockInterface_IsStripped()
    {
        // Confirmed real, 2026-07-10: BlockSourceParser.RequireDefaultInterface already
        // hard-errors upstream if a code block's Interface is anything but the standard
        // parameterless-FC boilerplate, so by the time a document reaches here it's known-inert
        // — safe to strip, same as it's always been.
        var a = XDocument.Parse("<SW.Blocks.FC><AttributeList><Interface><Sections><Section Name=\"Input\"/></Sections></Interface></AttributeList></SW.Blocks.FC>");
        var b = XDocument.Parse("<SW.Blocks.FC><AttributeList><Interface><Sections><Section Name=\"Output\"/></Sections></Interface></AttributeList></SW.Blocks.FC>");

        Assert.True(Normalizer.AreSemanticallyEquivalent(a, b));
    }

    [Fact]
    public void AreSemanticallyEquivalent_DbInterface_IsNotStripped()
    {
        // The bug this guards against, caught live 2026-07-10 building DB support: a blanket
        // "Interface" strip (correct for code blocks) would make a DB round-trip trivially pass
        // without ever comparing its actual member declarations — a DB's Interface always has a
        // "Static" Section (a code block's never does), which is what distinguishes the two.
        var a = XDocument.Parse("<SW.Blocks.GlobalDB><AttributeList><Interface><Sections><Section Name=\"Static\"><Member Name=\"Foo\"/></Section></Sections></Interface></AttributeList></SW.Blocks.GlobalDB>");
        var b = XDocument.Parse("<SW.Blocks.GlobalDB><AttributeList><Interface><Sections><Section Name=\"Static\"><Member Name=\"Bar\"/></Section></Sections></Interface></AttributeList></SW.Blocks.GlobalDB>");

        Assert.False(Normalizer.AreSemanticallyEquivalent(a, b));
    }

    [Fact]
    public void Strip_RemovesOnlyVolatileElements_LeavesStructureIntact()
    {
        var element = XElement.Parse("<Block><Name>Foo</Name><ModifiedDate>x</ModifiedDate><Number>2</Number></Block>");

        var stripped = Normalizer.Strip(element);

        Assert.Null(stripped.Element("ModifiedDate"));
        Assert.Equal("Foo", stripped.Element("Name")?.Value);
        Assert.Equal("2", stripped.Element("Number")?.Value);
    }
}
