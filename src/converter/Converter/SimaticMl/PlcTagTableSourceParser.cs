using System.Xml.Linq;

namespace Converter.SimaticMl;

/// <summary>
/// Parses an `SW.Tags.PlcTagTable` export into a <see cref="PlcTagTableSource"/>. Confirmed real,
/// 2026-07-14 (`Default tag table`, `station_2/JOB9002_PLC` — grounding `PlantAutoControl`'s own
/// dependency closure): the root's own `AttributeList` carries only `Name` (no `Namespace`, no
/// safety-adjacent field the way a UDT's own `IsFailsafeCompliant` is) and no table-level
/// Comment/Title at all — `ObjectList` goes straight to the `SW.Tags.PlcTag` children. Each tag's
/// own `AttributeList` carries `DataTypeName`/`ExternalAccessible`/`ExternalVisible`/
/// `ExternalWritable`/`LogicalAddress`/`Name` as plain child elements (not wrapped in
/// `BooleanAttribute` the way a DB/UDT member's own booleans are) and its own `ObjectList` carries
/// only a Comment (no Title).
/// </summary>
public static class PlcTagTableSourceParser
{
    public static PlcTagTableSource Parse(XDocument document)
    {
        var root = document.Root
            ?? throw new SimaticMlFormatException("Export file has no root element.");

        var tagTable = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "SW.Tags.PlcTagTable")
            ?? throw new SimaticMlFormatException("Could not find an SW.Tags.PlcTagTable element in the export.");

        var attributeList = tagTable.Element("AttributeList")
            ?? throw new SimaticMlFormatException("PlcTagTable element is missing its <AttributeList>.");

        var rootUId = RequireAttribute(tagTable, "ID");
        var name = RequireChildValue(attributeList, "Name");

        var objectList = tagTable.Element("ObjectList")
            ?? throw new SimaticMlFormatException("PlcTagTable element is missing its <ObjectList>.");

        var tags = objectList.Elements().Where(e => e.Name.LocalName == "SW.Tags.PlcTag")
            .Select(t => ParseTag(t, name))
            .ToList();

        return new PlcTagTableSource(rootUId, name, tags);
    }

    private static PlcTagSource ParseTag(XElement tagElement, string tableName)
    {
        var rootUId = RequireAttribute(tagElement, "ID");
        var attributeList = tagElement.Element("AttributeList")
            ?? throw new SimaticMlFormatException($"Tag table '{tableName}': tag ID={rootUId} is missing its <AttributeList>.");

        var name = RequireChildValue(attributeList, "Name");
        var dataTypeName = RequireChildValue(attributeList, "DataTypeName");
        var logicalAddress = RequireChildValue(attributeList, "LogicalAddress");
        var externalAccessible = RequireBoolChildValue(attributeList, "ExternalAccessible", tableName, name);
        var externalVisible = RequireBoolChildValue(attributeList, "ExternalVisible", tableName, name);
        var externalWritable = RequireBoolChildValue(attributeList, "ExternalWritable", tableName, name);

        var tagObjectList = tagElement.Element("ObjectList");
        var comment = tagObjectList is null ? null : MultilingualTextHelper.ReadMultilingualText(tagObjectList, "Comment");

        return new PlcTagSource(rootUId, name, dataTypeName, logicalAddress, externalAccessible, externalVisible, externalWritable, comment);
    }

    private static bool RequireBoolChildValue(XElement attributeList, string localName, string tableName, string tagName)
    {
        var raw = RequireChildValue(attributeList, localName);
        return raw switch
        {
            "true" => true,
            "false" => false,
            _ => throw new UnsupportedConstructException(
                $"Tag table's tag '{tagName}': <{localName}> is \"{raw}\", expected \"true\" or \"false\"."),
        };
    }

    private static string RequireChildValue(XElement parent, string localName)
    {
        var child = parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName)
            ?? throw new SimaticMlFormatException($"<{parent.Name.LocalName}> is missing child <{localName}>.");
        return child.Value;
    }

    private static string RequireAttribute(XElement element, string name) =>
        element.Attribute(name)?.Value
            ?? throw new SimaticMlFormatException($"<{element.Name.LocalName}> is missing required attribute '{name}'.");
}
