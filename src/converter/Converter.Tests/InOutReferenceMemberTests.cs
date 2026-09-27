using System.IO;
using System.Linq;
using System.Xml.Linq;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// Two interface-member shapes grounded on a live-run export (2026-09-27; fixture invented):
/// <list type="bullet">
/// <item>An InOut parameter typed as a technology object or a UDT: TIA expands the referenced type inline
/// (<c>&lt;Sections&gt;</c>) but writes NO <c>Remanence</c> and NO <c>&lt;AttributeList&gt;</c> — a reference has
/// no storage or external-access flags of its own. It failed with "missing the 'ExternalAccessible'
/// BooleanAttribute". Read as the bare parameter shape (<c>BAREPARAM VERSION 2.3</c>); nothing invented on
/// the way back.</item>
/// <item>An anonymous <c>Struct</c> whose fields are DIRECT <c>&lt;Member&gt;</c> children, inside a named UDT's
/// expansion, carrying the use site's own start values — the values an engineer edits. Refused before;
/// read, kept and written back in the same direct-children form.</item>
/// </list>
/// </summary>
public class InOutReferenceMemberTests
{
    private static XDocument Fixture() => XDocument.Load(Path.Combine("Fixtures", "InstanceDbInOutReference.xml"));

    private static string ToIr(XDocument document) => DbIrSerializer.Serialize(DbSourceParser.Parse(document));

    [Fact]
    public void ToIr_ReadsTheReferencesBareAndKeepsTheNestedStartValues()
    {
        var ir = ToIr(Fixture());

        Assert.Contains("Loop : DemoController BAREPARAM VERSION 2.3", ir);
        Assert.Contains("Link : \"DemoUdt\" BAREPARAM", ir);
        Assert.Contains("Upper : Real = 30.0", ir);
        Assert.Contains("Lower : Real = 9.0", ir);
    }

    [Fact]
    public void RoundTrip_IsEquivalentToTheSource_AndSelfStable()
    {
        var ir = ToIr(Fixture());
        // Re-parsed from text, as `to-xml` then `compare` would see it (an in-memory XElement keeps
        // empty-vs-absent value distinctions that a saved file does not).
        var regenerated = XDocument.Parse(DbSourceWriter.Write(DbIrParser.ParseDb(ir)).ToString());

        Assert.True(Normalizer.AreSemanticallyEquivalent(Fixture(), regenerated));
        Assert.Equal(ir, ToIr(regenerated));
    }

    [Fact]
    public void TheReferenceIsWrittenWithoutInventedAttributes_VersionBeforeAccessibility()
    {
        var regenerated = DbSourceWriter.Write(DbIrParser.ParseDb(ToIr(Fixture())));
        var loop = regenerated.Descendants().Single(e => e.Name.LocalName == "Member" && (string?)e.Attribute("Name") == "Loop");

        Assert.Equal(new[] { "Name", "Datatype", "Version", "Accessibility" }, loop.Attributes().Select(a => a.Name.LocalName));
        Assert.DoesNotContain(loop.Elements(), e => e.Name.LocalName == "AttributeList");
    }

    // The point of reading the nested struct: an edited start value reaches the XML.
    [Fact]
    public void AnEditedNestedStartValue_ReachesTheXml()
    {
        var edited = ToIr(Fixture()).Replace("Upper : Real = 30.0", "Upper : Real = 42.5");

        var regenerated = DbSourceWriter.Write(DbIrParser.ParseDb(edited));

        var upper = regenerated.Descendants().Single(e => e.Name.LocalName == "Member" && (string?)e.Attribute("Name") == "Upper");
        Assert.Equal("42.5", upper.Elements().Single(e => e.Name.LocalName == "StartValue").Value);
    }
}
