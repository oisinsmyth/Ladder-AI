using System.Xml.Linq;

namespace Converter.SimaticMl;

/// <summary>
/// Parses an `SW.Types.PlcStruct` export (a PLC data type / UDT) into a <see cref="PlcTypeSource"/>.
/// Confirmed real, 2026-07-14 (`TypeDOL`, `FB MotorDOL`'s own dependency): the root's own
/// `AttributeList` carries `Name`/`Namespace`/`IsFailsafeCompliant` as child elements (same shape
/// as a DB's own `AttributeList` — see <see cref="DbSourceParser"/>) and one `Interface > Sections
/// > Section Name="None"` — a single, unnamed section, genuinely different from a block/DB's own
/// multi-section (`Static`/`Temp`/...) Interface, structurally closer to a *structured member's
/// own nested* `Section Name="None"` shape (<see cref="DbInterfaceMembers.ParseNestedMembers"/>)
/// than to a top-level block Interface — except each member here carries a full `AttributeList`
/// (<see cref="DbInterfaceMembers.ParseTypeMember"/>), not the bare Name/Datatype/StartValue shape
/// a nested member uses. Hard-errors on anything beyond this confirmed shape: more than one
/// section, a section not named `"None"`, no members at all.
/// </summary>
public static class PlcTypeSourceParser
{
    public static PlcTypeSource Parse(XDocument document)
    {
        var root = document.Root
            ?? throw new SimaticMlFormatException("Export file has no root element.");

        var plcStruct = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "SW.Types.PlcStruct")
            ?? throw new SimaticMlFormatException("Could not find an SW.Types.PlcStruct element in the export.");

        var attributeList = plcStruct.Element("AttributeList")
            ?? throw new SimaticMlFormatException("PlcStruct element is missing its <AttributeList>.");

        var rootUId = RequireAttribute(plcStruct, "ID");
        var name = RequireChildValue(attributeList, "Name");

        // Safety-adjacent field, confirmed real 2026-07-14 (TypeDOL: "false") — refused rather
        // than silently passed through if ever "true" (CLAUDE.md hard rule 2: never touch safety,
        // even to read/convert). Not stored as a PlcTypeSource field since only one value has ever
        // been observed — regenerated as a validated fixed constant on write, same "don't store a
        // confirmed constant" reasoning as Move's own DisabledENO="true".
        var isFailsafeCompliant = RequireChildValue(attributeList, "IsFailsafeCompliant");
        if (isFailsafeCompliant != "false")
        {
            throw new UnsupportedConstructException(
                $"Type '{name}' has IsFailsafeCompliant=\"{isFailsafeCompliant}\" — this pipeline never touches safety/failsafe content (CLAUDE.md hard rule 2).");
        }

        var members = ParseMembers(attributeList, name);

        var objectList = plcStruct.Element("ObjectList")
            ?? throw new SimaticMlFormatException("PlcStruct element is missing its <ObjectList>.");

        var comment = MultilingualTextHelper.ReadMultilingualText(objectList, "Comment");
        MultilingualTextHelper.RequireEmptyTitle(objectList, $"Type '{name}'");

        return new PlcTypeSource(rootUId, name, comment, members);
    }

    private static IReadOnlyList<DbMember> ParseMembers(XElement attributeList, string typeName)
    {
        var interfaceElement = attributeList.Element("Interface");
        if (interfaceElement is null)
        {
            throw new SimaticMlFormatException($"Type '{typeName}' is missing its <Interface>.");
        }

        var sectionsWrapper = interfaceElement.Elements().FirstOrDefault(e => e.Name.LocalName == "Sections")
            ?? throw new SimaticMlFormatException($"Type '{typeName}' Interface is missing its <Sections> wrapper.");
        var sections = sectionsWrapper.Elements().Where(e => e.Name.LocalName == "Section").ToList();

        if (sections.Count != 1 || (string?)sections[0].Attribute("Name") != "None")
        {
            var actualNames = string.Join(", ", sections.Select(s => (string?)s.Attribute("Name") ?? "<unnamed>"));
            throw new UnsupportedConstructException(
                $"Type '{typeName}' has a Sections shape this converter hasn't confirmed " +
                $"(expected exactly one Section named \"None\", got: [{actualNames}]).");
        }

        var members = sections[0].Elements().Where(e => e.Name.LocalName == "Member").ToList();
        if (members.Count == 0)
        {
            throw new UnsupportedConstructException($"Type '{typeName}' has an empty Section \"None\" — no real example has shown this.");
        }

        return members.Select(m => DbInterfaceMembers.ParseTypeMember(m, $"Type '{typeName}'")).ToList();
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
