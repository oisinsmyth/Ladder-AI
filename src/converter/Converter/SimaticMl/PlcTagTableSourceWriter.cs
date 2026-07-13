using System.Xml.Linq;

namespace Converter.SimaticMl;

/// <summary>
/// Writes an `SW.Tags.PlcTagTable` export XML from a <see cref="PlcTagTableSource"/> — the
/// inverse of <see cref="PlcTagTableSourceParser"/>. Element order matches the real source exactly
/// (confirmed real, 2026-07-14, `Default tag table`): a tag's own `AttributeList` children are
/// `DataTypeName`/`ExternalAccessible`/`ExternalVisible`/`ExternalWritable`/`LogicalAddress`/`Name`
/// (alphabetical), and its own `ObjectList` carries only a Comment (no Title, unlike a UDT's own
/// member — see `PlcTypeSourceWriter`'s own doc comment for that contrast).
/// </summary>
public static class PlcTagTableSourceWriter
{
    public static XDocument Write(PlcTagTableSource tagTable)
    {
        var nextAuxId = 100_000;

        var attributeList = new XElement("AttributeList", new XElement("Name", tagTable.Name));

        var objectList = new XElement("ObjectList");
        foreach (var tag in tagTable.Tags)
        {
            objectList.Add(WriteTag(tag, ref nextAuxId));
        }

        var root = new XElement(
            "SW.Tags.PlcTagTable",
            new XAttribute("ID", tagTable.RootUId),
            attributeList,
            objectList);

        var document = new XElement(
            "Document",
            new XElement("Engineering", new XAttribute("version", "V20")),
            root);

        return new XDocument(document);
    }

    private static XElement WriteTag(PlcTagSource tag, ref int nextAuxId)
    {
        var attributeList = new XElement(
            "AttributeList",
            new XElement("DataTypeName", tag.DataTypeName),
            new XElement("ExternalAccessible", tag.ExternalAccessible ? "true" : "false"),
            new XElement("ExternalVisible", tag.ExternalVisible ? "true" : "false"),
            new XElement("ExternalWritable", tag.ExternalWritable ? "true" : "false"),
            new XElement("LogicalAddress", tag.LogicalAddress),
            new XElement("Name", tag.Name));

        var objectList = new XElement("ObjectList", DbSourceWriter.WriteComment(tag.Comment, ref nextAuxId));

        return new XElement(
            "SW.Tags.PlcTag",
            new XAttribute("ID", tag.RootUId),
            new XAttribute("CompositionName", "Tags"),
            attributeList,
            objectList);
    }
}
