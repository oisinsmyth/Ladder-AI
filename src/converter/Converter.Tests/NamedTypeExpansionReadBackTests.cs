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
            "Array[1..8] of \"UDT_SlotTicket\"",
            ExpansionOf(("Request", "Bool"), ("Granted", "Bool"), ("Priority", "Bool")));

        var parsed = DbInterfaceMembers.ParseTypeMember(member, "UDT_SlotQueue");

        Assert.Equal("Claim", parsed.Name);
        // The reference survives; the expansion is discarded because the IR already names the type.
        Assert.Equal("Array[1..8] of \"UDT_SlotTicket\"", parsed.Datatype);
    }

    [Fact]
    public void PlainNamedType_ExpandedByTia_CollapsesToTheTypeReference()
    {
        var member = Member("IO", "\"UDT_RackIO\"", ExpansionOf(("State", "Int"), ("SimActive", "Bool")));

        var parsed = DbInterfaceMembers.ParseTypeMember(member, "UDT_Rack");

        Assert.Equal("\"UDT_RackIO\"", parsed.Datatype);
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
        var parsed = DbInterfaceMembers.ParseTypeMember(Member("IO", "\"UDT_RackIO\""), "UDT_Rack");

        Assert.Equal("\"UDT_RackIO\"", parsed.Datatype);
    }

    // ------------------------------------------------- FI-75: the collapse discarded start values

    /// <summary>
    /// TIA's expansion, with a per-use-site <c>StartValue</c> on one of the expanded members — the
    /// shape that carries the data the collapse was throwing away.
    /// </summary>
    private static XElement ExpansionWithStartValue(string memberName, string datatype, string startValue) =>
        new(
            XName.Get("Sections", Ns),
            new XElement(
                XName.Get("Section", Ns),
                new XAttribute("Name", "None"),
                new XElement(
                    XName.Get("Member", Ns),
                    new XAttribute("Name", memberName),
                    new XAttribute("Datatype", datatype),
                    new XElement(XName.Get("StartValue", Ns), startValue))));

    /// <summary>
    /// *** THE DEFECT. *** A named type's expansion is NOT purely redundant: the values inside it
    /// are the USE SITE's own, not the type's defaults. Measured on the committed corpus —
    /// `MotorFwdRevIOSet` declares no start value for `FTTime`/`ReverseDelay`/`ReverseIgnoreFT`,
    /// while `iDB_MotorFwdRevSystem_Shredder` sets them to 10.0 / 8.0 / 12.0. Those are commissioning
    /// setpoints, and collapsing the expansion discards them with an exit 0 — the same silent-loss
    /// class as the 182 dropped <c>&lt;Subelement&gt;</c> values.
    ///
    /// The top-level <see cref="DbInterfaceMembers.ParseMember"/> path always kept them, which is why
    /// the committed corpus never lost one. The collapse fires only at the NESTED positions, so the
    /// two paths disagreed about the identical construct.
    /// </summary>
    [Fact]
    public void BareMember_NamedTypeExpansion_KeepsPerUseSiteStartValues()
    {
        var bare = new XElement(
            XName.Get("Member", Ns),
            new XAttribute("Name", "Drive"),
            new XAttribute("Datatype", "\"UDT_DriveIO\""),
            ExpansionWithStartValue("FailToRunTime", "Real", "10.0"));

        var parsed = DbInterfaceMembers.ParseBareMember(bare, "DB_Line", "Section1");

        Assert.Equal("\"UDT_DriveIO\"", parsed.Datatype);
        var nested = Assert.Single(parsed.NestedMembers!);
        Assert.Equal("FailToRunTime", nested.Name);
        Assert.Equal("10.0", nested.StartValue);
    }

    [Fact]
    public void TypeMember_NamedTypeExpansion_KeepsPerUseSiteStartValues()
    {
        var member = Member("Drive", "\"UDT_DriveIO\"", ExpansionWithStartValue("FailToRunTime", "Real", "10.0"));

        var parsed = DbInterfaceMembers.ParseTypeMember(member, "UDT_Line");

        Assert.Equal("\"UDT_DriveIO\"", parsed.Datatype);
        var nested = Assert.Single(parsed.NestedMembers!);
        Assert.Equal("10.0", nested.StartValue);
    }

    /// <summary>
    /// Round trip, both nested positions: what is read must come back out in TIA's own shape — a
    /// <c>&lt;Sections&gt;&lt;Section Name="None"&gt;</c> wrapper, NOT the direct-<c>&lt;Member&gt;</c>
    /// children an anonymous Struct uses. Keeping a value on the read side and writing it in the
    /// wrong shape would replace one silent loss with a silent corruption.
    /// </summary>
    [Fact]
    public void BareMember_NamedTypeExpansion_RoundTripsInTiasOwnShape()
    {
        var original = new XElement(
            XName.Get("Member", Ns),
            new XAttribute("Name", "Drive"),
            new XAttribute("Datatype", "\"UDT_DriveIO\""),
            ExpansionWithStartValue("FailToRunTime", "Real", "10.0"));

        var written = DbInterfaceMembers.WriteBareMember(
            DbInterfaceMembers.ParseBareMember(original, "DB_Line", "Section1"));

        Assert.True(XNode.DeepEquals(original, written),
            $"expected TIA's own shape back.\noriginal: {original}\nwritten : {written}");
    }

    [Fact]
    public void TypeMember_NamedTypeExpansion_RoundTripsInTiasOwnShape()
    {
        var original = Member("Drive", "\"UDT_DriveIO\"", ExpansionWithStartValue("FailToRunTime", "Real", "10.0"));

        var written = DbInterfaceMembers.WriteTypeMember(
            DbInterfaceMembers.ParseTypeMember(original, "UDT_Line"));

        Assert.True(XNode.DeepEquals(original, written),
            $"expected TIA's own shape back.\noriginal: {original}\nwritten : {written}");
    }

    /// <summary>
    /// An ANONYMOUS Struct's nested members still write as DIRECT <c>&lt;Member&gt;</c> children on
    /// the type path — the shape must be chosen by the datatype, and the named-type change must not
    /// leak into the anonymous one.
    /// </summary>
    [Fact]
    public void TypeMember_AnonymousStruct_StillWritesDirectMemberChildren()
    {
        var anonymous = Member(
            "Anon",
            "Struct",
            new XElement(
                XName.Get("Member", Ns),
                new XAttribute("Name", "A"),
                new XAttribute("Datatype", "Bool"),
                new XElement(
                    XName.Get("AttributeList", Ns),
                    BoolAttr("ExternalAccessible", true),
                    BoolAttr("ExternalVisible", true),
                    BoolAttr("ExternalWritable", true),
                    BoolAttr("SetPoint", false))));

        var written = DbInterfaceMembers.WriteTypeMember(
            DbInterfaceMembers.ParseTypeMember(anonymous, "UDT_Thing"));

        Assert.Contains(written.Elements(), e => e.Name.LocalName == "Member");
        Assert.DoesNotContain(written.Elements(), e => e.Name.LocalName == "Sections");
    }

    // The sibling path FI-56 fixed must keep behaving identically — one rule, two call sites.
    [Fact]
    public void BareMemberPath_AgreesWithTypeMemberPath_OnTheSameShape()
    {
        var bare = new XElement(
            XName.Get("Member", Ns),
            new XAttribute("Name", "Claim"),
            new XAttribute("Datatype", "Array[1..8] of \"UDT_SlotTicket\""),
            ExpansionOf(("Request", "Bool")));

        var parsedBare = DbInterfaceMembers.ParseBareMember(bare, "DB_Queue", "Work");
        var parsedType = DbInterfaceMembers.ParseTypeMember(
            Member("Claim", "Array[1..8] of \"UDT_SlotTicket\"", ExpansionOf(("Request", "Bool"))),
            "UDT_SlotQueue");

        Assert.Equal(parsedBare.Datatype, parsedType.Datatype);
    }
}
