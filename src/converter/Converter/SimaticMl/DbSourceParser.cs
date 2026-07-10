using System.Xml.Linq;

namespace Converter.SimaticMl;

/// <summary>
/// Parses a `SW.Blocks.GlobalDB` export (what `openness-cli export` produces for a global DB)
/// into a <see cref="DbSource"/>. Scope confirmed against three real GlobalDB exports,
/// 2026-07-10 (`CommsProcessData`, `Alarms`, `Input` — see `src/converter/README.md`): `Static`
/// section only, scalar and `Array[m..n] of &lt;scalar&gt;` members. Hard-errors on
/// `SW.Blocks.InstanceDB` (real, out of scope this slice — needs structured-member support too,
/// deferred as one unit) and on anything this scope hasn't confirmed: non-`Static` section
/// content, a member with nested `&lt;Sections&gt;` (UDT-typed or system-function-block-typed,
/// e.g. a timer instance), or a member whose `BooleanAttribute`s differ from the default every
/// real member observed so far has.
/// </summary>
public static class DbSourceParser
{
    // Confirmed real, 2026-07-10, on every scalar/array member across three real GlobalDB
    // exports — flips only on structured members (SetPoint=true), which are out of scope.
    private static readonly IReadOnlyDictionary<string, bool> DefaultBooleanAttributes = new Dictionary<string, bool>(StringComparer.Ordinal)
    {
        ["ExternalAccessible"] = true,
        ["ExternalVisible"] = true,
        ["ExternalWritable"] = true,
        ["SetPoint"] = false,
    };

    public static DbSource Parse(XDocument document)
    {
        var root = document.Root
            ?? throw new SimaticMlFormatException("Export file has no root element.");

        var instanceDb = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "SW.Blocks.InstanceDB");
        if (instanceDb is not null)
        {
            throw new UnsupportedConstructException(
                "Instance DBs are not supported yet by this converter slice (needs structured/timer-instance " +
                "member support too — see src/converter/README.md).");
        }

        var dbElement = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "SW.Blocks.GlobalDB")
            ?? throw new SimaticMlFormatException("Could not find an SW.Blocks.GlobalDB element in the export.");

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

        return new DbSource(rootUId, name, number, comment, members);
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
            .Select(m => ParseMember(m, dbName))
            .ToList();
    }

    private static DbMember ParseMember(XElement member, string dbName)
    {
        var name = RequireAttribute(member, "Name");
        var datatype = RequireAttribute(member, "Datatype");

        if (member.Elements().Any(e => e.Name.LocalName == "Sections"))
        {
            throw new UnsupportedConstructException(
                $"DB '{dbName}' member '{name}' has nested structure (UDT-typed or a system-function-block " +
                "instance, e.g. a timer) — not supported by this converter slice yet.");
        }

        var remanence = (string?)member.Attribute("Remanence");
        var retain = remanence switch
        {
            "NonRetain" => false,
            "Retain" => true,
            _ => throw new SimaticMlFormatException($"DB '{dbName}' member '{name}' has unrecognized Remanence '{remanence}'."),
        };

        RequireDefaultBooleanAttributes(member, dbName, name);

        // Member/AttributeList/StartValue inherit the Interface namespace declared on the
        // ancestor <Sections xmlns="..."> — Element("StartValue")/Element("AttributeList")
        // (implicit empty-namespace XName) never match, silently returning null. Confirmed real,
        // 2026-07-10 (caught live: every BooleanAttribute read back as "absent"). Search by
        // LocalName instead, same discipline used everywhere else in this parser.
        var startValue = member.Elements().FirstOrDefault(e => e.Name.LocalName == "StartValue")?.Value;

        return new DbMember(name, datatype, retain, string.IsNullOrEmpty(startValue) ? null : startValue);
    }

    private static void RequireDefaultBooleanAttributes(XElement member, string dbName, string memberName)
    {
        var memberAttributeList = member.Elements().FirstOrDefault(e => e.Name.LocalName == "AttributeList");
        var actual = memberAttributeList?
            .Elements()
            .Where(e => e.Name.LocalName == "BooleanAttribute")
            .ToDictionary(e => (string)e.Attribute("Name")!, e => bool.Parse(e.Value), StringComparer.Ordinal)
            ?? new Dictionary<string, bool>();

        foreach (var (attrName, expected) in DefaultBooleanAttributes)
        {
            if (!actual.TryGetValue(attrName, out var value) || value != expected)
            {
                throw new UnsupportedConstructException(
                    $"DB '{dbName}' member '{memberName}' has a non-default BooleanAttribute '{attrName}' " +
                    $"(expected {expected}, got {(actual.TryGetValue(attrName, out var v) ? v.ToString() : "absent")}) — " +
                    "not confirmed safe to ignore by this converter slice yet.");
            }
        }
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
