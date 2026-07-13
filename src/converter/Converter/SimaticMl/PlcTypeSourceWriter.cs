using System.Xml.Linq;

namespace Converter.SimaticMl;

/// <summary>
/// Writes an `SW.Types.PlcStruct` export XML from a <see cref="PlcTypeSource"/> — the inverse of
/// <see cref="PlcTypeSourceParser"/>. Element order matches the real source exactly (confirmed
/// real, 2026-07-14, `TypeDOL`): `Interface`, `IsFailsafeCompliant`, `Name`, `Namespace` — a
/// different order from <see cref="DbSourceWriter"/>'s own `Interface`/`Name`/`Namespace`/
/// `Number`/`ProgrammingLanguage` (neither `IsFailsafeCompliant` nor this order applies to a DB).
/// Unlike a DB, a type's own `ObjectList` carries **both** Comment and Title `MultilingualText`
/// elements (a DB's own only carries Comment) — confirmed real, `TypeDOL` has an empty `Title`
/// element present, not absent.
/// </summary>
public static class PlcTypeSourceWriter
{
    public static XDocument Write(PlcTypeSource type)
    {
        var nextAuxId = 100_000;

        var noneSection = new XElement(DbInterfaceMembers.Ns + "Section", new XAttribute("Name", "None"));
        foreach (var member in type.Members)
        {
            noneSection.Add(DbInterfaceMembers.WriteTypeMember(member));
        }

        var sectionsElement = new XElement(DbInterfaceMembers.Ns + "Sections", noneSection);

        var attributeList = new XElement(
            "AttributeList",
            new XElement("Interface", sectionsElement),
            new XElement("IsFailsafeCompliant", "false"),
            new XElement("Name", type.Name),
            new XElement("Namespace"));

        var objectList = new XElement(
            "ObjectList",
            DbSourceWriter.WriteComment(type.Comment, ref nextAuxId),
            DbSourceWriter.WriteMultilingualText(null, "Title", ref nextAuxId));

        var root = new XElement(
            "SW.Types.PlcStruct",
            new XAttribute("ID", type.RootUId),
            attributeList,
            objectList);

        var document = new XElement(
            "Document",
            new XElement("Engineering", new XAttribute("version", "V20")),
            root);

        return new XDocument(document);
    }
}
