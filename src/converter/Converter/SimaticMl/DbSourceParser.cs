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

        // MemoryLayout (2026-08-12) — optional, since only a genuine TIA export carries it; a
        // converter-written document from before this existed has none, and null preserves that.
        var memoryLayout = BlockMemoryLayout.ReadOptional(attributeList, $"DB '{name}'");

        var (members, inputMembers, outputMembers, inOutMembers) = ParseMembers(attributeList, name);

        var objectList = dbElement.Element("ObjectList")
            ?? throw new SimaticMlFormatException("DB element is missing its <ObjectList>.");

        var comment = MultilingualTextHelper.ReadMultilingualText(objectList, "Comment");
        MultilingualTextHelper.RequireEmptyTitle(objectList, $"DB '{name}'");

        return new DbSource(rootUId, name, number, instanceOfName, comment, members, inputMembers, outputMembers, inOutMembers, memoryLayout);
    }

    // Input/Output/InOut: confirmed real 2026-07-13, `TomraControlInst1` (an Instance DB of
    // `FB TomraControlSystem`, which has real Input/Output formal parameters, S1 item 20) — an Instance
    // DB persists its own FB's Input/Output storage alongside Static, every other Instance DB
    // grounded before this one just happened to have none. Same section shape/parsing as
    // BlockSourceParser's own Input/Output/InOut (`DbInterfaceMembers.ParseMember` with
    // `requireSetPoint: false`) — a DB's own Interface has the identical shape an FB's does,
    // minus Temp/Constant/Return (never seen non-empty on a DB, so still hard-errored below).
    private static (
        IReadOnlyList<DbMember> Members,
        IReadOnlyList<DbMember>? InputMembers,
        IReadOnlyList<DbMember>? OutputMembers,
        IReadOnlyList<DbMember>? InOutMembers) ParseMembers(XElement attributeList, string dbName)
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

        var context = $"DB '{dbName}'";
        IReadOnlyList<DbMember>? inputMembers = null;
        IReadOnlyList<DbMember>? outputMembers = null;
        IReadOnlyList<DbMember>? inOutMembers = null;

        foreach (var section in sections)
        {
            var sectionName = (string?)section.Attribute("Name");
            var memberElements = section.Elements().Where(e => e.Name.LocalName == "Member").ToList();

            switch (sectionName)
            {
                case "Static":
                    break;

                case "Input":
                    inputMembers = memberElements.Select(m => DbInterfaceMembers.ParseMember(m, context, requireSetPoint: false)).ToList();
                    break;

                case "Output":
                    outputMembers = memberElements.Select(m => DbInterfaceMembers.ParseMember(m, context, requireSetPoint: false)).ToList();
                    break;

                case "InOut":
                    inOutMembers = memberElements.Select(m => DbInterfaceMembers.ParseMember(m, context, requireSetPoint: false)).ToList();
                    break;

                default:
                    if (memberElements.Count > 0)
                    {
                        throw new UnsupportedConstructException(
                            $"DB '{dbName}' has a non-empty Interface section '{sectionName}' — this converter only models the Static/Input/Output/InOut sections for DBs.");
                    }

                    break;
            }
        }

        var members = staticSection.Elements()
            .Where(e => e.Name.LocalName == "Member")
            .Select(m => DbInterfaceMembers.ParseMember(m, context))
            .ToList();

        return (members, inputMembers, outputMembers, inOutMembers);
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
