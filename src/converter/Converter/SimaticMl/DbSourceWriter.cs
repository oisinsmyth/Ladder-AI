using System.Xml.Linq;

namespace Converter.SimaticMl;

/// <summary>
/// Writes a `SW.Blocks.GlobalDB`/`SW.Blocks.InstanceDB` export XML from a <see cref="DbSource"/>
/// — the inverse of <see cref="DbSourceParser"/>. Member-level writing is shared with
/// <see cref="BlockSourceWriter"/> via <see cref="DbInterfaceMembers"/> — same XML shape for a
/// DB's own Static section and an FB's Static/Temp sections.
/// </summary>
public static class DbSourceWriter
{
    public static XDocument Write(DbSource db)
    {
        var nextAuxId = 100_000;

        var staticSection = new XElement(DbInterfaceMembers.Ns + "Section", new XAttribute("Name", "Static"));
        foreach (var member in db.Members)
        {
            staticSection.Add(DbInterfaceMembers.WriteMember(member));
        }

        // Input/Output/InOut: confirmed real 2026-07-13, `TomraControlInst1` — see DbModel.cs's own
        // doc comment. Only emitted when present (mirrors BlockSourceWriter's own
        // WriteMemberSection); every Instance/Global DB grounded before this one has none, so
        // this stays a no-op for them.
        var sectionsChildren = new List<XElement>();
        if (db.InputMembers is not null)
        {
            sectionsChildren.Add(WriteMemberSection("Input", db.InputMembers));
        }

        if (db.OutputMembers is not null)
        {
            sectionsChildren.Add(WriteMemberSection("Output", db.OutputMembers));
        }

        if (db.InOutMembers.Count > 0)
        {
            sectionsChildren.Add(WriteMemberSection("InOut", db.InOutMembers));
        }

        sectionsChildren.Add(staticSection);

        var sectionsElement = new XElement(
            DbInterfaceMembers.Ns + "Sections",
            sectionsChildren);

        var attributeListChildren = new List<XElement>();
        if (db.InstanceOfName is not null)
        {
            attributeListChildren.Add(new XElement("InstanceOfName", db.InstanceOfName));
            attributeListChildren.Add(new XElement("InstanceOfType", "FB"));
        }

        attributeListChildren.Add(new XElement("Interface", sectionsElement));
        attributeListChildren.Add(new XElement("Name", db.Name));
        attributeListChildren.Add(new XElement("Namespace"));
        attributeListChildren.Add(new XElement("Number", db.Number));
        attributeListChildren.Add(new XElement("ProgrammingLanguage", "DB"));

        var attributeList = new XElement("AttributeList", attributeListChildren);

        var objectList = new XElement("ObjectList", WriteComment(db.Comment, ref nextAuxId));

        var rootElementName = db.InstanceOfName is not null ? "SW.Blocks.InstanceDB" : "SW.Blocks.GlobalDB";
        var root = new XElement(
            rootElementName,
            new XAttribute("ID", db.RootUId),
            attributeList,
            objectList);

        var document = new XElement(
            "Document",
            new XElement("Engineering", new XAttribute("version", "V20")),
            root);

        return new XDocument(document);
    }

    // Input/Output/InOut all share the same member shape (Static's own full shape minus the
    // SetPoint BooleanAttribute) — same helper BlockSourceWriter uses for its own Input/Output/
    // InOut sections.
    private static XElement WriteMemberSection(string name, IReadOnlyList<DbMember> members)
    {
        var section = new XElement(DbInterfaceMembers.Ns + "Section", new XAttribute("Name", name));
        foreach (var member in members)
        {
            section.Add(DbInterfaceMembers.WriteMember(member, includeSetPoint: false));
        }

        return section;
    }

    internal static XElement WriteComment(string? comment, ref int nextAuxId) =>
        WriteMultilingualText(comment, "Comment", ref nextAuxId);

    // Generalized once a second real CompositionName (S1 item 16, network Title, 2026-07-12)
    // confirmed the shape is identical to Comment's own, differing only in CompositionName and
    // text content — same synthetic-ID reasoning as Comment's own (not round-tripped from the
    // source, not believed to carry semantic meaning beyond must-exist-and-be-unique).
    internal static XElement WriteMultilingualText(string? text, string compositionName, ref int nextAuxId)
    {
        var textId = nextAuxId++;
        var itemId = nextAuxId++;

        var textElement = new XElement("Text");
        if (!string.IsNullOrEmpty(text))
        {
            textElement.Value = text;
        }

        var item = new XElement(
            "MultilingualTextItem",
            new XAttribute("ID", itemId),
            new XAttribute("CompositionName", "Items"),
            new XElement("AttributeList", new XElement("Culture", "en-US"), textElement));

        return new XElement(
            "MultilingualText",
            new XAttribute("ID", textId),
            new XAttribute("CompositionName", compositionName),
            new XElement("ObjectList", item));
    }
}
