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

    // True in the overwhelming majority of members, but not a fixed default: `FB VSDSim`'s own
    // `SpeedCalcArray` has `ExternalAccessible=false` (confirmed real 2026-07-14) — see
    // DbModel.cs's DbMember.ExternalAccessible/Visible/Writable doc comment. Captured and
    // regenerated verbatim per member, same discipline as SetPoint, not hard-refused.
    private static readonly IReadOnlyCollection<string> DefaultTrueBooleanAttributes = new[] { "ExternalAccessible", "ExternalVisible", "ExternalWritable" };

    // Confirmed real, 2026-07-11: both a structured member's own nested members (a DB's) and an
    // FB's Temp-section members share this same minimal shape — only Name/Datatype attributes
    // and an optional StartValue child, no Remanence/Accessibility/AttributeList/further nesting.
    private static readonly IReadOnlyCollection<string> AllowedBareMemberAttributes = new HashSet<string>(StringComparer.Ordinal) { "Name", "Datatype" };

    /// <summary>
    /// Parses a full member (Static-section shape): Name/Datatype/Remanence/Version, BooleanAttributes, and either a StartValue or nested structured content.
    /// <paramref name="requireSetPoint"/> defaults to true (Static's own confirmed shape); pass false for Input/Output members, which are missing the
    /// SetPoint BooleanAttribute entirely — confirmed real, 2026-07-12, S1 item 20 (`FB TomraControlSystem`: Static members carry 4 BooleanAttributes
    /// including SetPoint, Input/Output members carry only the other 3).
    /// </summary>
    public static DbMember ParseMember(XElement member, string context, bool requireSetPoint = true)
    {
        var name = RequireAttribute(member, "Name");
        var datatype = RequireAttribute(member, "Datatype");
        var version = (string?)member.Attribute("Version");

        var nestedSections = member.Elements().FirstOrDefault(e => e.Name.LocalName == "Sections");
        var isStructured = nestedSections is not null;

        // A third structured-member shape, confirmed real 2026-07-14 (`FB EquipmentControlSystem`, an
        // untyped `Inputs`/`Outputs : Struct` member — an anonymous inline struct, no reusable
        // UDT name): nested `<Member>` elements are *direct children* of the owning member, not
        // wrapped in a `<Sections><Section Name="None">` the way a UDT-typed/system-function-
        // block-instance member's nested members are — and each nested member carries its own
        // full `<AttributeList>` (the exact shape `ParseTypeMember` already handles for a PLC
        // data type's own members), not the bare Name/Datatype[/StartValue]-only shape
        // `ParseNestedMembers`/`ParseBareMember` expect. Previously silently mis-parsed as a
        // plain scalar member (Datatype="Struct", no start value) — the nested content was
        // dropped entirely rather than hard-erroring, a real bug (`docs/notes/stage-gates.md`,
        // "Phase 1: FB EquipmentControlSystem").
        var directNestedMembers = member.Elements().Where(e => e.Name.LocalName == "Member").ToList();
        var isAnonymousStruct = directNestedMembers.Count > 0;

        // A fourth, genuinely minimal member shape, confirmed real 2026-07-14 (`FC Scale`'s own
        // Input/Output parameters — a small project utility FC, grounding `FB MotorVSDSystem`'s own
        // dependency closure): no `Remanence` attribute at all (not merely an unrecognized value)
        // and no `<AttributeList>` — just `Name`/`Datatype`[/`Accessibility="Public"`]. Distinct
        // from `ParseBareMember`'s own shape (which forbids `Accessibility`) and from the ordinary
        // Input/Output shape confirmed for `FB TomraControlSystem` (which has `Remanence`+`AttributeList`,
        // just missing `SetPoint`) — this FC's own interface carries neither. Checked before the
        // `Remanence` switch below would otherwise hard-error on its absence.
        var hasAttributeList = member.Elements().Any(e => e.Name.LocalName == "AttributeList");
        if (member.Attribute("Remanence") is null && !hasAttributeList)
        {
            var bareStartValueElement = member.Elements().FirstOrDefault(e => e.Name.LocalName == "StartValue");
            var bareStartValue = bareStartValueElement?.Value;

            // Informative/InformativeComment: confirmed real 2026-07-14, `OB1 Main`'s own system
            // parameters — see DbModel.cs's own doc comment.
            var isInformative = (string?)member.Attribute("Informative") == "true";
            string? informativeComment = null;
            if (isInformative)
            {
                var commentElement = member.Elements().FirstOrDefault(e => e.Name.LocalName == "Comment");
                var textElement = commentElement?.Elements().FirstOrDefault(e => e.Name.LocalName == "MultiLanguageText");
                informativeComment = textElement?.Value
                    ?? throw new SimaticMlFormatException($"{context} member '{name}' has Informative=\"true\" but no <Comment><MultiLanguageText> child.");
            }

            return new DbMember(
                name, datatype, Retain: false, string.IsNullOrEmpty(bareStartValue) ? null : bareStartValue, Version: version, SetPoint: false,
                NestedMembers: null, IsBareParameter: true, Informative: isInformative, InformativeComment: informativeComment);
        }

        var remanence = (string?)member.Attribute("Remanence");
        var retain = remanence switch
        {
            "NonRetain" => false,
            "Retain" => true,
            _ => throw new SimaticMlFormatException($"{context} member '{name}' has unrecognized Remanence '{remanence}'."),
        };

        var booleanAttributes = RequireDefaultBooleanAttributes(member, context, name, requireSetPoint);
        var comment = ParseOptionalComment(member);

        // Member/AttributeList/StartValue inherit the Interface namespace declared on the
        // ancestor <Sections xmlns="..."> — Element("StartValue")/Element("AttributeList")
        // (implicit empty-namespace XName) never match, silently returning null. Confirmed real,
        // 2026-07-10 (caught live: every BooleanAttribute read back as "absent"). Search by
        // LocalName instead, same discipline used everywhere else in this parser.
        var startValueElement = member.Elements().FirstOrDefault(e => e.Name.LocalName == "StartValue");

        if (isAnonymousStruct)
        {
            if (isStructured)
            {
                throw new UnsupportedConstructException(
                    $"{context} member '{name}' has both direct nested <Member> children and a <Sections> wrapper — this combination hasn't been observed.");
            }

            if (datatype != "Struct")
            {
                throw new UnsupportedConstructException(
                    $"{context} member '{name}' has direct nested <Member> children but Datatype is '{datatype}', not 'Struct' — only 'Struct' has been observed with this shape.");
            }

            if (startValueElement is not null)
            {
                throw new UnsupportedConstructException(
                    $"{context} member '{name}' is an anonymous struct but also has its own <StartValue> — this combination hasn't been observed.");
            }

            var anonymousNestedMembers = directNestedMembers.Select(m => ParseTypeMember(m, $"{context} member '{name}'")).ToList();
            return new DbMember(
                name, datatype, retain, StartValue: null, Version: version, SetPoint: booleanAttributes.SetPoint, NestedMembers: anonymousNestedMembers,
                ExternalAccessible: booleanAttributes.ExternalAccessible, ExternalVisible: booleanAttributes.ExternalVisible, ExternalWritable: booleanAttributes.ExternalWritable,
                Comment: comment);
        }

        if (isStructured)
        {
            if (startValueElement is not null)
            {
                throw new UnsupportedConstructException(
                    $"{context} member '{name}' is structured but also has its own <StartValue> — this combination hasn't been observed.");
            }

            var nestedMembers = ParseNestedMembers(nestedSections!, context, name);
            return new DbMember(
                name, datatype, retain, StartValue: null, Version: version, SetPoint: booleanAttributes.SetPoint, NestedMembers: nestedMembers,
                ExternalAccessible: booleanAttributes.ExternalAccessible, ExternalVisible: booleanAttributes.ExternalVisible, ExternalWritable: booleanAttributes.ExternalWritable,
                Comment: comment);
        }

        var startValue = startValueElement?.Value;
        return new DbMember(
            name, datatype, retain, string.IsNullOrEmpty(startValue) ? null : startValue, Version: version, SetPoint: booleanAttributes.SetPoint, NestedMembers: null,
            ExternalAccessible: booleanAttributes.ExternalAccessible, ExternalVisible: booleanAttributes.ExternalVisible, ExternalWritable: booleanAttributes.ExternalWritable,
            Comment: comment);
    }

    // Shared with the Informative/bare-parameter path's own Comment reading below, minus the
    // required-not-optional throw — an ordinary member's Comment is genuinely optional (absent on
    // the overwhelming majority of members seen), confirmed real 2026-07-15 by successfully
    // importing/exporting a plain Static member carrying one (FB_PusherControl, this session).
    private static string? ParseOptionalComment(XElement member)
    {
        var commentElement = member.Elements().FirstOrDefault(e => e.Name.LocalName == "Comment");
        var textElement = commentElement?.Elements().FirstOrDefault(e => e.Name.LocalName == "MultiLanguageText");
        return textElement?.Value is { Length: > 0 } text ? text : null;
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

    // Only Name/Datatype/StartValue are ever real on this shape — Accessibility is validated
    // separately below (always "Public" in both grounded instances) since it's a genuinely new
    // attribute this shape carries that ParseBareMember's own shape doesn't.
    private static readonly IReadOnlyCollection<string> AllowedConstantMemberAttributes =
        new HashSet<string>(StringComparer.Ordinal) { "Name", "Datatype", "Accessibility" };

    /// <summary>
    /// Parses a Constant-section member — confirmed real, 2026-07-12, S1 item 20, two independent
    /// instances (`FB MotorVSDSystem`/`AirStar`): `Name`/`Datatype`/`Accessibility="Public"` plus a
    /// required `&lt;StartValue&gt;` — genuinely distinct from both <see cref="ParseMember"/>
    /// (requires an `AttributeList`, absent here entirely) and <see cref="ParseBareMember"/>
    /// (rejects `Accessibility` as unexpected). `StartValue` is required, not optional, since
    /// every real Constant member seen carries one — unsurprising given the section's own name.
    /// </summary>
    public static DbMember ParseConstantMember(XElement member, string context)
    {
        var name = (string?)member.Attribute("Name") ?? "<unnamed>";

        var unexpectedAttributes = member.Attributes()
            .Select(a => a.Name.LocalName)
            .Where(n => !AllowedConstantMemberAttributes.Contains(n))
            .ToList();
        if (unexpectedAttributes.Count > 0)
        {
            throw new UnsupportedConstructException(
                $"{context} Constant member '{name}' has unexpected attribute(s) " +
                $"[{string.Join(", ", unexpectedAttributes)}] — only Name/Datatype/Accessibility have been observed on this shape.");
        }

        var unexpectedChildren = member.Elements().Select(e => e.Name.LocalName).Where(n => n != "StartValue").ToList();
        if (unexpectedChildren.Count > 0)
        {
            throw new UnsupportedConstructException(
                $"{context} Constant member '{name}' has unexpected content [{string.Join(", ", unexpectedChildren)}] — " +
                "only a bare Name/Datatype/Accessibility/StartValue member has been observed on this shape.");
        }

        var accessibility = (string?)member.Attribute("Accessibility");
        if (accessibility != "Public")
        {
            throw new UnsupportedConstructException(
                $"{context} Constant member '{name}' has Accessibility=\"{accessibility ?? "(absent)"}\" — only \"Public\" has been observed.");
        }

        var datatype = RequireAttribute(member, "Datatype");
        var startValue = member.Elements().FirstOrDefault(e => e.Name.LocalName == "StartValue")?.Value
            ?? throw new SimaticMlFormatException($"{context} Constant member '{name}' is missing its <StartValue> — every real Constant member seen has one.");

        return new DbMember(name, datatype, Retain: false, startValue);
    }

    /// <summary>
    /// Parses a PLC data type (UDT)'s own member shape — confirmed real, 2026-07-14 (`TypeDOL`,
    /// grounding S1's UDT support): `&lt;Member Name=... Datatype=...&gt;&lt;AttributeList&gt;
    /// ...same four BooleanAttributes as a DB/FB Static member (ExternalAccessible/Visible/
    /// Writable/SetPoint, via <see cref="RequireDefaultBooleanAttributes"/>)...&lt;/AttributeList&gt;
    /// [&lt;StartValue&gt;...&lt;/StartValue&gt;]&lt;/Member&gt;` — genuinely distinct from
    /// <see cref="ParseMember"/>'s own shape: **no `Remanence`/`Accessibility` attribute on the
    /// `&lt;Member&gt;` tag itself** (every real `TypeDOL` member confirmed lacking both).
    ///
    /// Recursive as of 2026-07-14 (`FB ShredderControlSystem`'s own `ComsOutByte501`, an anonymous
    /// `Struct` member — see <see cref="ParseMember"/> — with a nested `Struct`-typed field of its
    /// own, one of which nests a *third* level): a member's own direct `<Member>` children (no
    /// `<Sections>` wrapper) are parsed via this same method, arbitrarily deep, as long as each
    /// intermediate level's own `Datatype` is literally `"Struct"` (anything else with direct
    /// nested children is refused as unconfirmed). `<Sections>`-wrapped nested content is still
    /// refused outright — that shape was confirmed for DB/FB Static members specifically
    /// (<see cref="ParseNestedMembers"/>), not for a PLC data type's own member.
    /// </summary>
    public static DbMember ParseTypeMember(XElement member, string context)
    {
        var name = RequireAttribute(member, "Name");
        var datatype = RequireAttribute(member, "Datatype");

        if (member.Elements().Any(e => e.Name.LocalName == "Sections"))
        {
            throw new UnsupportedConstructException(
                $"{context} member '{name}' has nested structured content (<Sections>) — not confirmed real for a PLC data type's own member, refused rather than guessed at.");
        }

        var booleanAttributes = RequireDefaultBooleanAttributes(member, context, name, requireSetPoint: true);

        var directNestedMembers = member.Elements().Where(e => e.Name.LocalName == "Member").ToList();
        if (directNestedMembers.Count > 0)
        {
            if (datatype != "Struct")
            {
                throw new UnsupportedConstructException(
                    $"{context} member '{name}' has direct nested <Member> children but Datatype is '{datatype}', not 'Struct' — only 'Struct' has been observed with this shape.");
            }

            var nestedMembers = directNestedMembers.Select(m => ParseTypeMember(m, $"{context} member '{name}'")).ToList();
            return new DbMember(
                name, datatype, Retain: false, StartValue: null, SetPoint: booleanAttributes.SetPoint, NestedMembers: nestedMembers,
                ExternalAccessible: booleanAttributes.ExternalAccessible, ExternalVisible: booleanAttributes.ExternalVisible, ExternalWritable: booleanAttributes.ExternalWritable);
        }

        var startValueElement = member.Elements().FirstOrDefault(e => e.Name.LocalName == "StartValue");
        var startValue = startValueElement?.Value;

        return new DbMember(
            name, datatype, Retain: false, string.IsNullOrEmpty(startValue) ? null : startValue, SetPoint: booleanAttributes.SetPoint,
            ExternalAccessible: booleanAttributes.ExternalAccessible, ExternalVisible: booleanAttributes.ExternalVisible, ExternalWritable: booleanAttributes.ExternalWritable);
    }

    /// <summary>
    /// Inverse of <see cref="ParseTypeMember"/> — no Remanence/Accessibility attribute, same four
    /// BooleanAttributes as WriteMember's own AttributeList. Recursive: a member with
    /// <see cref="DbMember.NestedMembers"/> populated writes them as direct children (no
    /// `&lt;Sections&gt;` wrapper), arbitrarily deep, mirroring <see cref="ParseTypeMember"/>.
    /// </summary>
    public static XElement WriteTypeMember(DbMember member)
    {
        // Comment (DbModel.cs's own DbMember.Comment doc comment) is confirmed real only on the
        // ordinary WriteMember shape — a PLC data type's own member shape wasn't part of that
        // grounding. Hard-erroring on an unconfirmed combination rather than silently dropping the
        // text, matching this converter's own established discipline elsewhere (e.g. ParseTypeMember's
        // <Sections> refusal just above).
        if (member.Comment is not null)
        {
            throw new UnsupportedConstructException(
                $"Member '{member.Name}' has a Comment but is in a PLC-data-type (TYPE/UDT) member position — member comments are only confirmed on an ordinary Static/Input/Output/DB member (WriteMember), not here.");
        }

        var attributeList = new XElement(
            Ns + "AttributeList",
            new XElement(Ns + "BooleanAttribute", new XAttribute("Name", "ExternalAccessible"), new XAttribute("SystemDefined", "true"), member.ExternalAccessible ? "true" : "false"),
            new XElement(Ns + "BooleanAttribute", new XAttribute("Name", "ExternalVisible"), new XAttribute("SystemDefined", "true"), member.ExternalVisible ? "true" : "false"),
            new XElement(Ns + "BooleanAttribute", new XAttribute("Name", "ExternalWritable"), new XAttribute("SystemDefined", "true"), member.ExternalWritable ? "true" : "false"),
            new XElement(Ns + "BooleanAttribute", new XAttribute("Name", "SetPoint"), new XAttribute("SystemDefined", "true"), member.SetPoint ? "true" : "false"));

        var memberElement = new XElement(
            Ns + "Member",
            new XAttribute("Name", member.Name),
            new XAttribute("Datatype", member.Datatype),
            attributeList);

        if (member.NestedMembers is not null)
        {
            foreach (var nested in member.NestedMembers)
            {
                memberElement.Add(WriteTypeMember(nested));
            }
        }
        else if (member.StartValue is not null)
        {
            memberElement.Add(new XElement(Ns + "StartValue", member.StartValue));
        }

        return memberElement;
    }

    public static XElement WriteConstantMember(DbMember member)
    {
        if (member.Comment is not null)
        {
            throw new UnsupportedConstructException(
                $"Constant member '{member.Name}' has a Comment — not confirmed real on this shape, refused rather than silently dropped.");
        }

        var startValue = member.StartValue
            ?? throw new SimaticMlFormatException($"Constant member '{member.Name}' has no StartValue to write — every real Constant member requires one.");

        return new XElement(
            Ns + "Member",
            new XAttribute("Name", member.Name),
            new XAttribute("Datatype", member.Datatype),
            new XAttribute("Accessibility", "Public"),
            new XElement(Ns + "StartValue", startValue));
    }

    /// <summary>
    /// Reads ExternalAccessible/Visible/Writable and SetPoint verbatim — none are a fixed default,
    /// each captured and regenerated exactly (DbModel.cs's own DbMember doc comments have the real
    /// counterexamples that disproved treating any of them as always-true/false). When
    /// <paramref name="requireSetPoint"/> is false (Input/Output members — confirmed real,
    /// 2026-07-12, S1 item 20), a missing SetPoint attribute is not an error; the returned value is
    /// simply `false` in that case (nothing to carry, mirrors the "don't store a confirmed
    /// constant" reasoning already used for Move's own DisabledENO).
    /// </summary>
    private static BooleanAttributes RequireDefaultBooleanAttributes(XElement member, string context, string memberName, bool requireSetPoint)
    {
        var memberAttributeList = member.Elements().FirstOrDefault(e => e.Name.LocalName == "AttributeList");
        var actual = memberAttributeList?
            .Elements()
            .Where(e => e.Name.LocalName == "BooleanAttribute")
            .ToDictionary(e => (string)e.Attribute("Name")!, e => bool.Parse(e.Value), StringComparer.Ordinal)
            ?? new Dictionary<string, bool>();

        var values = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var attrName in DefaultTrueBooleanAttributes)
        {
            if (!actual.TryGetValue(attrName, out var value))
            {
                throw new UnsupportedConstructException(
                    $"{context} member '{memberName}' is missing the '{attrName}' BooleanAttribute.");
            }

            values[attrName] = value;
        }

        if (!actual.TryGetValue("SetPoint", out var setPoint))
        {
            if (!requireSetPoint)
            {
                return new BooleanAttributes(values["ExternalAccessible"], values["ExternalVisible"], values["ExternalWritable"], false);
            }

            throw new UnsupportedConstructException($"{context} member '{memberName}' is missing the 'SetPoint' BooleanAttribute.");
        }

        return new BooleanAttributes(values["ExternalAccessible"], values["ExternalVisible"], values["ExternalWritable"], setPoint);
    }

    private readonly record struct BooleanAttributes(bool ExternalAccessible, bool ExternalVisible, bool ExternalWritable, bool SetPoint);

    /// <summary>
    /// <paramref name="includeSetPoint"/> defaults to true (Static's own confirmed shape); pass false for Input/Output members, whose
    /// AttributeList never carries a SetPoint BooleanAttribute at all — confirmed real, 2026-07-12, S1 item 20.
    /// </summary>
    public static XElement WriteMember(DbMember member, bool includeSetPoint = true)
    {
        if (member.IsBareParameter)
        {
            var bareElement = new XElement(
                Ns + "Member",
                new XAttribute("Name", member.Name),
                new XAttribute("Datatype", member.Datatype),
                new XAttribute("Accessibility", "Public"));

            if (member.Informative)
            {
                bareElement.Add(new XAttribute("Informative", "true"));
                bareElement.Add(new XElement(
                    Ns + "Comment",
                    new XElement(Ns + "MultiLanguageText", new XAttribute("Lang", "en-US"), member.InformativeComment)));
            }

            if (member.StartValue is not null)
            {
                bareElement.Add(new XElement(Ns + "StartValue", member.StartValue));
            }

            return bareElement;
        }

        var isStructured = member.NestedMembers is not null;

        // Every element here must stay in Ns (inherited in the real source from the single
        // xmlns declared on <Sections>, not re-declared per element) — an unnamespaced XElement
        // nested under a namespaced parent would round-trip as an incorrect explicit `xmlns=""`
        // reset instead of matching the real shape. Confirmed real, 2026-07-10 (caught live:
        // parsing this writer's own output, every BooleanAttribute read back as "absent").
        // SetPoint is written back verbatim from what was captured on parse — not inferred from
        // isStructured, which a real counterexample disproved (DbModel.cs has the story).
        var attributeListChildren = new List<XElement>
        {
            new(Ns + "BooleanAttribute", new XAttribute("Name", "ExternalAccessible"), new XAttribute("SystemDefined", "true"), member.ExternalAccessible ? "true" : "false"),
            new(Ns + "BooleanAttribute", new XAttribute("Name", "ExternalVisible"), new XAttribute("SystemDefined", "true"), member.ExternalVisible ? "true" : "false"),
            new(Ns + "BooleanAttribute", new XAttribute("Name", "ExternalWritable"), new XAttribute("SystemDefined", "true"), member.ExternalWritable ? "true" : "false"),
        };
        if (includeSetPoint)
        {
            attributeListChildren.Add(new XElement(
                Ns + "BooleanAttribute", new XAttribute("Name", "SetPoint"), new XAttribute("SystemDefined", "true"), member.SetPoint ? "true" : "false"));
        }

        var attributeList = new XElement(Ns + "AttributeList", attributeListChildren);

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

        if (member.Comment is not null)
        {
            memberElement.Add(new XElement(
                Ns + "Comment",
                new XElement(Ns + "MultiLanguageText", new XAttribute("Lang", "en-US"), member.Comment)));
        }

        if (isStructured && member.Datatype == "Struct")
        {
            // Anonymous struct (`FB EquipmentControlSystem`'s own `Inputs`/`Outputs`, confirmed real
            // 2026-07-14): nested members are direct children, each with its own full
            // AttributeList (ParseTypeMember's exact shape) — no <Sections> wrapper, unlike the
            // UDT-typed/system-function-block-instance case below.
            foreach (var nested in member.NestedMembers!)
            {
                memberElement.Add(WriteTypeMember(nested));
            }
        }
        else if (isStructured)
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
        // Comment (DbModel.cs's own DbMember.Comment doc comment) is confirmed real only on the
        // ordinary WriteMember shape — Temp members and a structured member's own nested fields
        // (both routed here) weren't part of that grounding. Hard-erroring rather than silently
        // dropping the text if one somehow arrives here (e.g. hand-authored IR nesting a COMMENT
        // under a UDT-typed Static member's own field).
        if (bare.Comment is not null)
        {
            throw new UnsupportedConstructException(
                $"Member '{bare.Name}' has a Comment but is in the bare-member position (a Temp member, or a structured member's own nested field) — member comments are only confirmed on an ordinary top-level Static/Input/Output/DB member (WriteMember), not here.");
        }

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
