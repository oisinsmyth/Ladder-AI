using System.Xml.Linq;

namespace Converter.SimaticMl;

/// <summary>
/// Shared parse/write logic for the DB member-interface XML shape (`&lt;Member Name=... Datatype=...
/// Remanence=... Accessibility=...&gt;&lt;AttributeList&gt;...BooleanAttributes...&lt;/AttributeList&gt;
/// [&lt;StartValue&gt;...&lt;/StartValue&gt; | &lt;Sections&gt;&lt;Section Name="None"&gt;...nested
/// Members...&lt;/Section&gt;&lt;/Sections&gt;]&lt;/Member&gt;`) — used identically by a DB's own
/// `Static` section (<see cref="DbSourceParser"/>/<see cref="DbSourceWriter"/>) and an FB's own
/// `Static`/`Temp` Interface sections (<see cref="BlockSourceParser"/>/<see cref="BlockSourceWriter"/>,
/// confirmed real 2026-07-11, S1 item 7 Phase B: an instance DB's Static section is a direct
/// realization of its FB's own Static section, same XML shape in both places, confirmed by
/// comparing real `ConveyorMotor1`/`MotorDOL` exports).
/// </summary>
internal static class DbInterfaceMembers
{
    public static readonly XNamespace Ns = "http://www.siemens.com/automation/Openness/SW/Interface/v5";

    // Confirmed real, 2026-07-10 across three GlobalDBs and 2026-07-11 across structured/FB
    // members too — always true, no exception seen. SetPoint is deliberately excluded: see
    // DbModel.cs's DbMember.SetPoint doc comment for the real counterexample that disproved
    // treating it as a fixed default.
    private static readonly IReadOnlyCollection<string> RequiredTrueBooleanAttributes = new[] { "ExternalAccessible", "ExternalVisible", "ExternalWritable" };

    // Confirmed real, 2026-07-11: both a structured member's own nested members (a DB's) and an
    // FB's Temp-section members share this same minimal shape — only Name/Datatype attributes
    // and an optional StartValue child, no Remanence/Accessibility/AttributeList/further nesting.
    private static readonly IReadOnlyCollection<string> AllowedBareMemberAttributes = new HashSet<string>(StringComparer.Ordinal) { "Name", "Datatype" };

    /// <summary>Parses a full member (Static-section shape): Name/Datatype/Remanence/Version, BooleanAttributes, and either a StartValue or nested structured content.</summary>
    public static DbMember ParseMember(XElement member, string context)
    {
        var name = RequireAttribute(member, "Name");
        var datatype = RequireAttribute(member, "Datatype");
        var version = (string?)member.Attribute("Version");

        var nestedSections = member.Elements().FirstOrDefault(e => e.Name.LocalName == "Sections");
        var isStructured = nestedSections is not null;

        var remanence = (string?)member.Attribute("Remanence");
        var retain = remanence switch
        {
            "NonRetain" => false,
            "Retain" => true,
            _ => throw new SimaticMlFormatException($"{context} member '{name}' has unrecognized Remanence '{remanence}'."),
        };

        var setPoint = RequireDefaultBooleanAttributes(member, context, name);

        // Member/AttributeList/StartValue inherit the Interface namespace declared on the
        // ancestor <Sections xmlns="..."> — Element("StartValue")/Element("AttributeList")
        // (implicit empty-namespace XName) never match, silently returning null. Confirmed real,
        // 2026-07-10 (caught live: every BooleanAttribute read back as "absent"). Search by
        // LocalName instead, same discipline used everywhere else in this parser.
        var startValueElement = member.Elements().FirstOrDefault(e => e.Name.LocalName == "StartValue");

        if (isStructured)
        {
            if (startValueElement is not null)
            {
                throw new UnsupportedConstructException(
                    $"{context} member '{name}' is structured but also has its own <StartValue> — this combination hasn't been observed.");
            }

            var nestedMembers = ParseNestedMembers(nestedSections!, context, name);
            return new DbMember(name, datatype, retain, StartValue: null, Version: version, SetPoint: setPoint, NestedMembers: nestedMembers);
        }

        var startValue = startValueElement?.Value;
        return new DbMember(name, datatype, retain, string.IsNullOrEmpty(startValue) ? null : startValue, Version: version, SetPoint: setPoint, NestedMembers: null);
    }

    public static IReadOnlyList<DbMember> ParseNestedMembers(XElement sections, string context, string ownerMemberName)
    {
        // Confirmed real, 2026-07-11: a structured member's nested Sections has exactly one
        // Section, named "None". Any other section name is real-but-unconfirmed — refused rather
        // than guessed at.
        var sectionElements = sections.Elements().Where(e => e.Name.LocalName == "Section").ToList();
        if (sectionElements.Count != 1 || (string?)sectionElements[0].Attribute("Name") != "None")
        {
            var actualNames = string.Join(", ", sectionElements.Select(s => (string?)s.Attribute("Name") ?? "<unnamed>"));
            throw new UnsupportedConstructException(
                $"{context} member '{ownerMemberName}' has a nested Sections shape this converter hasn't confirmed " +
                $"(expected exactly one Section named \"None\", got: [{actualNames}]).");
        }

        var nestedMembers = sectionElements[0].Elements().Where(e => e.Name.LocalName == "Member").ToList();
        if (nestedMembers.Count == 0)
        {
            throw new UnsupportedConstructException(
                $"{context} member '{ownerMemberName}' has an empty nested Section \"None\" — no real example has shown this.");
        }

        return nestedMembers.Select(m => ParseBareMember(m, context, ownerMemberName)).ToList();
    }

    /// <summary>Parses the minimal Name/Datatype[/StartValue]-only member shape — a structured member's own nested members, and an FB's Temp-section members.</summary>
    public static DbMember ParseBareMember(XElement member, string context, string ownerMemberName)
    {
        var bareName = (string?)member.Attribute("Name") ?? "<unnamed>";

        var unexpectedAttributes = member.Attributes()
            .Select(a => a.Name.LocalName)
            .Where(n => !AllowedBareMemberAttributes.Contains(n))
            .ToList();
        if (unexpectedAttributes.Count > 0)
        {
            throw new UnsupportedConstructException(
                $"{context} member '{ownerMemberName}' has a nested/bare member '{bareName}' with unexpected attribute(s) " +
                $"[{string.Join(", ", unexpectedAttributes)}] — only Name/Datatype/StartValue have been observed on this shape.");
        }

        var unexpectedChildren = member.Elements()
            .Select(e => e.Name.LocalName)
            .Where(n => n != "StartValue")
            .ToList();
        if (unexpectedChildren.Count > 0)
        {
            throw new UnsupportedConstructException(
                $"{context} member '{ownerMemberName}' has a nested/bare member '{bareName}' with unexpected content " +
                $"[{string.Join(", ", unexpectedChildren)}] — a doubly-nested structured member or any shape beyond a bare " +
                "Name/Datatype/StartValue member is outside this slice.");
        }

        var name = RequireAttribute(member, "Name");
        var datatype = RequireAttribute(member, "Datatype");
        var startValue = member.Elements().FirstOrDefault(e => e.Name.LocalName == "StartValue")?.Value;

        return new DbMember(name, datatype, Retain: false, string.IsNullOrEmpty(startValue) ? null : startValue);
    }

    /// <summary>Validates ExternalAccessible/Visible/Writable are all true (no exception seen), and returns the member's own SetPoint value verbatim.</summary>
    private static bool RequireDefaultBooleanAttributes(XElement member, string context, string memberName)
    {
        var memberAttributeList = member.Elements().FirstOrDefault(e => e.Name.LocalName == "AttributeList");
        var actual = memberAttributeList?
            .Elements()
            .Where(e => e.Name.LocalName == "BooleanAttribute")
            .ToDictionary(e => (string)e.Attribute("Name")!, e => bool.Parse(e.Value), StringComparer.Ordinal)
            ?? new Dictionary<string, bool>();

        foreach (var attrName in RequiredTrueBooleanAttributes)
        {
            if (!actual.TryGetValue(attrName, out var value) || !value)
            {
                throw new UnsupportedConstructException(
                    $"{context} member '{memberName}' has a non-default BooleanAttribute '{attrName}' " +
                    $"(expected True, got {(actual.TryGetValue(attrName, out var v) ? v.ToString() : "absent")}) — " +
                    "not confirmed safe to ignore by this converter slice yet.");
            }
        }

        if (!actual.TryGetValue("SetPoint", out var setPoint))
        {
            throw new UnsupportedConstructException($"{context} member '{memberName}' is missing the 'SetPoint' BooleanAttribute.");
        }

        return setPoint;
    }

    public static XElement WriteMember(DbMember member)
    {
        var isStructured = member.NestedMembers is not null;

        // Every element here must stay in Ns (inherited in the real source from the single
        // xmlns declared on <Sections>, not re-declared per element) — an unnamespaced XElement
        // nested under a namespaced parent would round-trip as an incorrect explicit `xmlns=""`
        // reset instead of matching the real shape. Confirmed real, 2026-07-10 (caught live:
        // parsing this writer's own output, every BooleanAttribute read back as "absent").
        // SetPoint is written back verbatim from what was captured on parse — not inferred from
        // isStructured, which a real counterexample disproved (DbModel.cs has the story).
        var attributeList = new XElement(
            Ns + "AttributeList",
            new XElement(Ns + "BooleanAttribute", new XAttribute("Name", "ExternalAccessible"), new XAttribute("SystemDefined", "true"), "true"),
            new XElement(Ns + "BooleanAttribute", new XAttribute("Name", "ExternalVisible"), new XAttribute("SystemDefined", "true"), "true"),
            new XElement(Ns + "BooleanAttribute", new XAttribute("Name", "ExternalWritable"), new XAttribute("SystemDefined", "true"), "true"),
            new XElement(Ns + "BooleanAttribute", new XAttribute("Name", "SetPoint"), new XAttribute("SystemDefined", "true"), member.SetPoint ? "true" : "false"));

        var memberElement = new XElement(
            Ns + "Member",
            new XAttribute("Name", member.Name),
            new XAttribute("Datatype", member.Datatype));

        if (member.Version is not null)
        {
            memberElement.Add(new XAttribute("Version", member.Version));
        }

        memberElement.Add(
            new XAttribute("Remanence", member.Retain ? "Retain" : "NonRetain"),
            new XAttribute("Accessibility", "Public"),
            attributeList);

        if (isStructured)
        {
            var noneSection = new XElement(Ns + "Section", new XAttribute("Name", "None"));
            foreach (var nested in member.NestedMembers!)
            {
                noneSection.Add(WriteBareMember(nested));
            }

            memberElement.Add(new XElement(Ns + "Sections", noneSection));
        }
        else if (member.StartValue is not null)
        {
            memberElement.Add(new XElement(Ns + "StartValue", member.StartValue));
        }

        return memberElement;
    }

    public static XElement WriteBareMember(DbMember bare)
    {
        var element = new XElement(
            Ns + "Member",
            new XAttribute("Name", bare.Name),
            new XAttribute("Datatype", bare.Datatype));

        if (bare.StartValue is not null)
        {
            element.Add(new XElement(Ns + "StartValue", bare.StartValue));
        }

        return element;
    }

    private static string RequireAttribute(XElement element, string name) =>
        element.Attribute(name)?.Value
            ?? throw new SimaticMlFormatException($"<{element.Name.LocalName}> is missing required attribute '{name}'.");
}
