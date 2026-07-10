using System.Xml.Linq;

namespace Converter.SimaticMl;

/// <summary>
/// Writes a `SW.Blocks.GlobalDB` export XML from a <see cref="DbSource"/> — the inverse of
/// <see cref="DbSourceParser"/>. `Accessibility="Public"` and the four `BooleanAttribute`s are
/// written as fixed defaults, matching every real member observed so far
/// (`DbSourceParser.RequireDefaultBooleanAttributes` hard-errors on parse if a source member
/// ever differs, so nothing here silently regenerates a value that wasn't actually confirmed).
/// </summary>
public static class DbSourceWriter
{
    private static readonly XNamespace InterfaceNs = "http://www.siemens.com/automation/Openness/SW/Interface/v5";

    public static XDocument Write(DbSource db)
    {
        var nextAuxId = 100_000;

        var staticSection = new XElement(InterfaceNs + "Section", new XAttribute("Name", "Static"));
        foreach (var member in db.Members)
        {
            staticSection.Add(WriteMember(member));
        }

        var sectionsElement = new XElement(
            InterfaceNs + "Sections",
            staticSection);

        var attributeList = new XElement(
            "AttributeList",
            new XElement("Interface", sectionsElement),
            new XElement("Name", db.Name),
            new XElement("Namespace"),
            new XElement("Number", db.Number),
            new XElement("ProgrammingLanguage", "DB"));

        var objectList = new XElement("ObjectList", WriteComment(db.Comment, ref nextAuxId));

        var root = new XElement(
            "SW.Blocks.GlobalDB",
            new XAttribute("ID", db.RootUId),
            attributeList,
            objectList);

        var document = new XElement(
            "Document",
            new XElement("Engineering", new XAttribute("version", "V20")),
            root);

        return new XDocument(document);
    }

    private static XElement WriteMember(DbMember member)
    {
        // Every element here must stay in InterfaceNs (inherited in the real source from the
        // single xmlns declared on <Sections>, not re-declared per element) — an unnamespaced
        // XElement nested under a namespaced parent would round-trip as an incorrect explicit
        // `xmlns=""` reset instead of matching the real shape. Confirmed real, 2026-07-10 (caught
        // live: parsing this writer's own output, every BooleanAttribute read back as "absent").
        var attributeList = new XElement(
            InterfaceNs + "AttributeList",
            new XElement(InterfaceNs + "BooleanAttribute", new XAttribute("Name", "ExternalAccessible"), new XAttribute("SystemDefined", "true"), "true"),
            new XElement(InterfaceNs + "BooleanAttribute", new XAttribute("Name", "ExternalVisible"), new XAttribute("SystemDefined", "true"), "true"),
            new XElement(InterfaceNs + "BooleanAttribute", new XAttribute("Name", "ExternalWritable"), new XAttribute("SystemDefined", "true"), "true"),
            new XElement(InterfaceNs + "BooleanAttribute", new XAttribute("Name", "SetPoint"), new XAttribute("SystemDefined", "true"), "false"));

        var memberElement = new XElement(
            InterfaceNs + "Member",
            new XAttribute("Name", member.Name),
            new XAttribute("Datatype", member.Datatype),
            new XAttribute("Remanence", member.Retain ? "Retain" : "NonRetain"),
            new XAttribute("Accessibility", "Public"),
            attributeList);

        if (member.StartValue is not null)
        {
            memberElement.Add(new XElement(InterfaceNs + "StartValue", member.StartValue));
        }

        return memberElement;
    }

    private static XElement WriteComment(string? comment, ref int nextAuxId)
    {
        var textId = nextAuxId++;
        var itemId = nextAuxId++;

        var textElement = new XElement("Text");
        if (!string.IsNullOrEmpty(comment))
        {
            textElement.Value = comment;
        }

        var item = new XElement(
            "MultilingualTextItem",
            new XAttribute("ID", itemId),
            new XAttribute("CompositionName", "Items"),
            new XElement("AttributeList", new XElement("Culture", "en-US"), textElement));

        return new XElement(
            "MultilingualText",
            new XAttribute("ID", textId),
            new XAttribute("CompositionName", "Comment"),
            new XElement("ObjectList", item));
    }
}
