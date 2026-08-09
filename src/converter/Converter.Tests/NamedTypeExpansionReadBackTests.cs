using System.Linq;
using System.Xml.Linq;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// FI-64 (2026-08-09). The named-type expansion FI-56 fixed, on the third parse path.
///
/// TIA expands a member whose type is a NAMED UDT — including an array of one — into a nested
/// <c>&lt;Sections&gt;</c> on re-export. <see cref="DbInterfaceMembers.ParseBareMember"/> learned to
/// collapse that back to the type reference (FI-56); <see cref="DbInterfaceMembers.ParseTypeMember"/>
/// still refused it, so a PLC data type carrying such a member could not be read back at all.
///
/// Why that is worth a test rather than a note: the re-export round trip is the only check on this
/// project that has caught defects every other gate passed — three separate times on one job. A type
/// the converter cannot read back loses that check and degrades to reading raw XML by hand.
///
/// The distinction these tests exist to pin down is the one that makes the collapse safe: a NAMED
/// type's expansion is redundant because the IR names the type, but an ANONYMOUS structured member's
/// nested content is its only definition and must still be refused.
/// </summary>
public class NamedTypeExpansionReadBackTests
{
    private const string Ns = "http://www.siemens.com/automation/Openness/SW/Interface/v5";

    private static XElement Member(string name, string datatype, params XElement[] children)
    {
        var m = new XElement(
            XName.Get("Member", Ns),
            new XAttribute("Name", name),
            new XAttribute("Datatype", datatype),
            new XElement(
                XName.Get("AttributeList", Ns),
                BoolAttr("ExternalAccessible", true),
                BoolAttr("ExternalVisible", true),
                BoolAttr("ExternalWritable", true),
                BoolAttr("SetPoint", false)));

        foreach (var c in children)
        {
            m.Add(c);
        }

        return m;
    }

    private static XElement BoolAttr(string name, bool value) =>
        new(XName.Get("BooleanAttribute", Ns), new XAttribute("Name", name), new XAttribute("SystemDefined", "true"), value ? "true" : "false");

    // TIA's expansion: the named type's own members, rendered inside the referencing member.
    private static XElement ExpansionOf(params (string Name, string Datatype)[] members) =>
        new(
            XName.Get("Sections", Ns),
            new XElement(
                XName.Get("Section", Ns),
                new XAttribute("Name", "None"),
                members.Select(m => new XElement(
                    XName.Get("Member", Ns),
                    new XAttribute("Name", m.Name),
                    new XAttribute("Datatype", m.Datatype)))));

    // The exact shape that could not be read back: an array of a named UDT, expanded by TIA.
    [Fact]
    public void ArrayOfNamedType_ExpandedByTia_CollapsesToTheTypeReference()
    {
        var member = Member(
            "Claim",
            "Array[1..8] of \"UDT_ResourceClaim\"",
            ExpansionOf(("Request", "Bool"), ("Granted", "Bool"), ("Priority", "Bool")));

        var parsed = DbInterfaceMembers.ParseTypeMember(member, "UDT_ResourceQueue");

        Assert.Equal("Claim", parsed.Name);
        // The reference survives; the expansion is discarded because the IR already names the type.
        Assert.Equal("Array[1..8] of \"UDT_ResourceClaim\"", parsed.Datatype);
    }

    [Fact]
    public void PlainNamedType_ExpandedByTia_CollapsesToTheTypeReference()
    {
        var member = Member("IO", "\"UDT_DrumIO\"", ExpansionOf(("State", "Int"), ("SimActive", "Bool")));

        var parsed = DbInterfaceMembers.ParseTypeMember(member, "UDT_Drum");

        Assert.Equal("\"UDT_DrumIO\"", parsed.Datatype);
    }

    // The guard that matters most. An anonymous Struct's nested content is its ONLY definition —
    // collapsing it would silently discard real members, which is far worse than refusing to parse.
    [Fact]
    public void AnonymousStruct_WithSections_IsStillRefused()
    {
        var member = Member("Anon", "Struct", ExpansionOf(("A", "Bool"), ("B", "Int")));

        var ex = Assert.Throws<UnsupportedConstructException>(
            () => DbInterfaceMembers.ParseTypeMember(member, "UDT_Thing"));

        Assert.Contains("Struct", ex.Message);
        // The message must say WHY, not just that it refused — the next reader needs the reason.
        Assert.Contains("only definition", ex.Message);
    }

    // Any non-named datatype carrying <Sections> is unconfirmed and stays refused, not just Struct.
    [Fact]
    public void UnnamedNonStructDatatype_WithSections_IsStillRefused()
    {
        var member = Member("Odd", "Array[0..1] of Struct", ExpansionOf(("A", "Bool")));

        Assert.Throws<UnsupportedConstructException>(
            () => DbInterfaceMembers.ParseTypeMember(member, "UDT_Thing"));
    }

    // A named-type member with no expansion at all must be unaffected — the ordinary case.
    [Fact]
    public void NamedTypeWithoutExpansion_IsUnchanged()
    {
        var parsed = DbInterfaceMembers.ParseTypeMember(Member("IO", "\"UDT_DrumIO\""), "UDT_Drum");

        Assert.Equal("\"UDT_DrumIO\"", parsed.Datatype);
    }

    // The sibling path FI-56 fixed must keep behaving identically — one rule, two call sites.
    [Fact]
    public void BareMemberPath_AgreesWithTypeMemberPath_OnTheSameShape()
    {
        var bare = new XElement(
            XName.Get("Member", Ns),
            new XAttribute("Name", "Claim"),
            new XAttribute("Datatype", "Array[1..8] of \"UDT_ResourceClaim\""),
            ExpansionOf(("Request", "Bool")));

        var parsedBare = DbInterfaceMembers.ParseBareMember(bare, "DB_Queue", "Work");
        var parsedType = DbInterfaceMembers.ParseTypeMember(
            Member("Claim", "Array[1..8] of \"UDT_ResourceClaim\"", ExpansionOf(("Request", "Bool"))),
            "UDT_ResourceQueue");

        Assert.Equal(parsedBare.Datatype, parsedType.Datatype);
    }
}
