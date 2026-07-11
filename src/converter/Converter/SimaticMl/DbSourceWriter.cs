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

        var sectionsElement = new XElement(
            DbInterfaceMembers.Ns + "Sections",
            staticSection);

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

    internal static XElement WriteComment(string? comment, ref int nextAuxId)
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
