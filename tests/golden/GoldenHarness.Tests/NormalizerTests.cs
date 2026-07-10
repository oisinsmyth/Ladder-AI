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
        var b = XDocument.Parse("<Block><Name>Foo</Name><ModifiedDate>2026-07-11T09:30:00Z</ModifiedDate></Block>");

        Assert.True(Normalizer.AreSemanticallyEquivalent(a, b));
    }

    [Fact]
    public void AreSemanticallyEquivalent_DifferingCompileDate_ReturnsTrue()
    {
        var a = XDocument.Parse("<Block><CompileDate>2026-01-01T00:00:00Z</CompileDate></Block>");
        var b = XDocument.Parse("<Block><CompileDate>2026-07-11T09:30:00Z</CompileDate></Block>");

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
    public void AreSemanticallyEquivalent_DifferingWireUId_ReturnsFalse()
    {
        // A UId difference is exactly the thing the IR sidecar exists to prevent — must never
        // be normalized away, or a real semantic drift could hide behind "it's just volatile IDs".
        var a = XDocument.Parse("<Wires><Wire UId=\"41\"><Powerrail/></Wire></Wires>");
        var b = XDocument.Parse("<Wires><Wire UId=\"99\"><Powerrail/></Wire></Wires>");

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
