using System.Xml.Linq;

namespace Converter.SimaticMl;

/// <summary>
/// Parses a `SW.Blocks.GlobalDB` or `SW.Blocks.InstanceDB` export (what `openness-cli export`
/// produces for a DB) into a <see cref="DbSource"/>. Scope confirmed against real exports:
/// `Static` section only, scalar and `Array[m..n] of &lt;scalar&gt;` members (2026-07-10 — three
/// real GlobalDBs, `src/converter/README.md`), plus structured members (UDT-typed or
/// system-function-block instance-typed, e.g. a `TON_TIME` timer) and Instance DBs, one level of
/// nesting only (2026-07-11, S1 item 7 Phase B — `ir/SPEC.md` "Structured members"). Member-level
/// parsing (<see cref="DbInterfaceMembers"/>) is shared with <see cref="BlockSourceParser"/>'s
/// own FB Static/Temp section support — same XML shape both places. Hard-errors on anything this
/// scope hasn't confirmed: non-`Static` section content, a nested member with any shape beyond
/// `Name`/`Datatype`/`StartValue`, a doubly-nested structured member, a nested section named
/// anything but `"None"`, an Instance DB whose `InstanceOfType` isn't `"FB"`, or a member whose
/// `BooleanAttribute`s differ from the default every real member of its kind has shown so far.
/// </summary>
public static class DbSourceParser
{
    public static DbSource Parse(XDocument document)
    {
        var root = document.Root
            ?? throw new SimaticMlFormatException("Export file has no root element.");

        var globalDb = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "SW.Blocks.GlobalDB");
        var instanceDb = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "SW.Blocks.InstanceDB");

        if (globalDb is not null)
        {
            return ParseDbElement(globalDb, instanceOfName: null);
        }

        if (instanceDb is not null)
        {
            var attributeList = instanceDb.Element("AttributeList")
                ?? throw new SimaticMlFormatException("DB element is missing its <AttributeList>.");

            var instanceOfName = RequireChildValue(attributeList, "InstanceOfName");
            var instanceOfType = RequireChildValue(attributeList, "InstanceOfType");
            if (instanceOfType != "FB")
            {
                throw new UnsupportedConstructException(
                    $"Instance DB '{instanceOfName}' has InstanceOfType '{instanceOfType}' — only \"FB\" has been observed.");
            }

            return ParseDbElement(instanceDb, instanceOfName);
        }

        throw new SimaticMlFormatException("Could not find an SW.Blocks.GlobalDB or SW.Blocks.InstanceDB element in the export.");
    }

    private static DbSource ParseDbElement(XElement dbElement, string? instanceOfName)
    {
        var attributeList = dbElement.Element("AttributeList")
            ?? throw new SimaticMlFormatException("DB element is missing its <AttributeList>.");

        var rootUId = RequireAttribute(dbElement, "ID");
        var name = RequireChildValue(attributeList, "Name");
        var number = int.Parse(RequireChildValue(attributeList, "Number"));
        var language = RequireChildValue(attributeList, "ProgrammingLanguage");

        if (language != "DB")
        {
            throw new SimaticMlFormatException($"DB '{name}' has unexpected ProgrammingLanguage '{language}' (expected 'DB').");
        }

        var members = ParseMembers(attributeList, name);

        var objectList = dbElement.Element("ObjectList")
            ?? throw new SimaticMlFormatException("DB element is missing its <ObjectList>.");

        var comment = MultilingualTextHelper.ReadMultilingualText(objectList, "Comment");
        MultilingualTextHelper.RequireEmptyTitle(objectList, $"DB '{name}'");

        return new DbSource(rootUId, name, number, instanceOfName, comment, members);
    }

    private static IReadOnlyList<DbMember> ParseMembers(XElement attributeList, string dbName)
    {
        var interfaceElement = attributeList.Element("Interface");
        if (interfaceElement is null)
        {
            throw new SimaticMlFormatException($"DB '{dbName}' is missing its <Interface>.");
        }

        // Direct children of the <Sections> wrapper only — NOT .Descendants(), which would also
        // pick up a structured member's own nested <Sections><Section Name="None">...</Section>
        // (a timer/UDT instance's own sub-members), wrongly tripping the non-Static-section
        // check below before ParseMember's own, more specific structured-member check ever runs.
        // Confirmed real, caught live by this parser's own test suite.
        var sectionsWrapper = interfaceElement.Elements().FirstOrDefault(e => e.Name.LocalName == "Sections")
            ?? throw new SimaticMlFormatException($"DB '{dbName}' Interface is missing its <Sections> wrapper.");
        var sections = sectionsWrapper.Elements().Where(e => e.Name.LocalName == "Section").ToList();
        var staticSection = sections.FirstOrDefault(s => (string?)s.Attribute("Name") == "Static")
            ?? throw new SimaticMlFormatException($"DB '{dbName}' has no 'Static' Interface section.");

        foreach (var section in sections)
        {
            var sectionName = (string?)section.Attribute("Name");
            if (sectionName == "Static")
            {
                continue;
            }

            if (section.Elements().Any(e => e.Name.LocalName == "Member"))
            {
                throw new UnsupportedConstructException(
                    $"DB '{dbName}' has a non-empty Interface section '{sectionName}' — this converter only models the Static section for DBs.");
            }
        }

        return staticSection.Elements()
            .Where(e => e.Name.LocalName == "Member")
            .Select(m => DbInterfaceMembers.ParseMember(m, $"DB '{dbName}'"))
            .ToList();
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
