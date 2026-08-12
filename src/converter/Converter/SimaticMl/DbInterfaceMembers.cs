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
    //
    // FI-58 (2026-08-08) adds `Version`. A nested member whose type is a SYSTEM STRUCTURED TYPE —
    // `DTL` is the one this corpus hit — carries a `Version` attribute in TIA's own export, and
    // refusing it hard-errored `to-ir` on any DB with such a member nested inside a structure:
    //     "member 'Silo' has a nested/bare member 'LastCleaned' with unexpected attribute(s)
    //      [Version]"
    // Two DBs could not be read back at all, so a re-export could not be verified and the agent
    // had to extract member sets from the raw XML by hand instead.
    //
    // Same family as FI-56 and the same reasoning: this is TIA stating the version of a type it
    // owns, on a member the IR names BY TYPE. It carries nothing the IR needs and nothing that can
    // be lost by ignoring it — `ParseMember` already accepts and discards `Version` on the
    // full-member shape for exactly this reason. Accepting it here makes the two shapes agree.
    private static readonly IReadOnlyCollection<string> AllowedBareMemberAttributes = new HashSet<string>(StringComparer.Ordinal) { "Name", "Datatype", "Version" };

    // Every child element name any member shape here has ever been observed to carry. The guard
    // built from it (RequireKnownChildren) exists because of what its absence cost: `<Subelement>`
    // — an ARRAY member's per-element start values — was read by no parse path and written by no
    // write path, and since nothing checked for unknown children it was DROPPED IN SILENCE. A real
    // block converted `exit 0` with 200+ of them gone, taking its whole per-node configuration
    // table (addresses, node numbers, lengths) with it. An unrecognised child is now a named
    // refusal, in the same fail-closed spirit as FlgNetParser.SupportedPartNames.
    private static readonly IReadOnlyCollection<string> AllowedMemberChildren = new HashSet<string>(StringComparer.Ordinal)
    {
        "AttributeList", "Comment", "Member", "Sections", "StartValue", "Subelement",
    };

    private const string SubelementElementName = "Subelement";

    // An ANONYMOUS struct element type. `Array[1..10] of Struct` nests its members exactly the way
    // a bare `Struct` does — direct <Member> children, each carrying its own full <AttributeList> —
    // because it IS the same anonymous struct, dimensioned. Confirmed real 2026-08-12 against a
    // genuine TIA V20 export of an S7-1200 Modbus TCP FB, whose `Array[1..10] of Struct` per-node
    // configuration table hard-errored here. Deliberately narrow: any OTHER datatype carrying
    // direct nested members is still refused rather than guessed at.
    private static bool IsAnonymousStructDatatype(string datatype) =>
        datatype == "Struct"
        || (datatype.StartsWith("Array[", StringComparison.Ordinal)
            && datatype.EndsWith(" of Struct", StringComparison.Ordinal));

    private static void RequireKnownChildren(XElement member, string context, string memberName)
    {
        var unexpected = member.Elements()
            .Select(e => e.Name.LocalName)
            .Where(n => !AllowedMemberChildren.Contains(n))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (unexpected.Count > 0)
        {
            throw new UnsupportedConstructException(
                $"{context} member '{memberName}' carries unrecognised child element(s) [{string.Join(", ", unexpected)}]. " +
                "Refused rather than ignored: an unread child is data that crosses to-ir and vanishes, which is exactly " +
                "how <Subelement> array start values were lost. Ground the shape against a real export and add it here.");
        }
    }

    /// <summary>
    /// Reads a member's `&lt;Subelement Path="…"&gt;&lt;StartValue&gt;…&lt;/StartValue&gt;&lt;/Subelement&gt;`
    /// children — an ARRAY member's per-element start values, and the only place an array's initial
    /// data lives (an array has no scalar `&lt;StartValue&gt;` of its own). See
    /// <see cref="DbSubelement"/> for the provenance. Empty when there are none.
    /// </summary>
    private static IReadOnlyList<DbSubelement>? ParseSubelements(XElement member, string context, string memberName)
    {
        var elements = member.Elements().Where(e => e.Name.LocalName == SubelementElementName).ToList();
        if (elements.Count == 0)
        {
            return null;
        }

        var subelements = new List<DbSubelement>(elements.Count);
        foreach (var element in elements)
        {
            var path = (string?)element.Attribute("Path")
                ?? throw new SimaticMlFormatException(
                    $"{context} member '{memberName}' has a <Subelement> with no Path attribute.");

            var unexpectedChildren = element.Elements().Select(e => e.Name.LocalName).Where(n => n != "StartValue").ToList();
            if (unexpectedChildren.Count > 0)
            {
                throw new UnsupportedConstructException(
                    $"{context} member '{memberName}' <Subelement Path=\"{path}\"> carries unexpected content " +
                    $"[{string.Join(", ", unexpectedChildren)}] — only a single <StartValue> has been observed.");
            }

            var startValues = element.Elements().Where(e => e.Name.LocalName == "StartValue").ToList();
            if (startValues.Count != 1)
            {
                throw new UnsupportedConstructException(
                    $"{context} member '{memberName}' <Subelement Path=\"{path}\"> has {startValues.Count} <StartValue> " +
                    "children — exactly one has been observed. Refused rather than dropped: a subelement carries an " +
                    "array's only initial data.");
            }

            subelements.Add(new DbSubelement(path, startValues[0].Value));
        }

        return subelements;
    }

    // Written in the position TIA itself uses: after </AttributeList> (and after a member <Comment>,
    // which shares that slot), where a scalar member's own <StartValue> would go.
    private static void AddSubelements(XElement memberElement, DbMember member)
    {
        foreach (var subelement in member.Subelements)
        {
            memberElement.Add(new XElement(
                Ns + SubelementElementName,
                new XAttribute("Path", subelement.Path),
                new XElement(Ns + "StartValue", subelement.StartValue)));
        }
    }

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

        RequireKnownChildren(member, context, name);
        var subelements = ParseSubelements(member, context, name);

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

        // A MULTI-INSTANCE static, on the way BACK from TIA (2026-08-07). The write half of this was
        // fixed first and the read half was missed, so `to-ir` hard-errored on every block containing
        // one — "unrecognized Remanence ''" — which took the re-export leg of the gate contract, plus
        // drift-check and the round-trip harness, out for any such block.
        //
        // TIA re-exports it as: NO `Remanence` (retention belongs to the CALLED block's members, so
        // there is nothing to state here), but WITH an `AttributeList`, and with a `<Sections>` child
        // carrying the callee's ENTIRE interface expanded inline. The `<Sections>` child is what makes
        // it unambiguous — an anonymous struct nests `<Member>` directly and never has one.
        //
        // The expanded interface is DISCARDED on purpose. It is the callee's own declaration, owned by
        // the called block and already exported with it; reading it back as nested members would
        // duplicate that declaration into the caller and make the two free to disagree. Name and
        // datatype are the whole of what the caller declares, which is exactly what the bare shape
        // writes back out — so this round-trips as a fixed point.
        //
        // READ AS AN ORDINARY MEMBER SINCE 2026-08-12, not as a bare parameter — the correction to a
        // SILENT LOSS. The bare read discarded `Version` AND the whole `<AttributeList>`, because the
        // bare shape (`FC Scale`'s genuinely attribute-less parameters) has neither to write back. So
        // a multi-instance round-tripped as
        //     <Member Name="MB_Server" Datatype="MB_SERVER" Accessibility="Public" />
        // — a VERSIONLESS instance declaration, from a source that said `Version="5.3"`. It converted
        // without error and nothing warned. Measured on a real Modbus TCP FB; the `TON_TIME` member
        // beside it kept its `Version="1.0"` because it carries `Remanence` and never took this path.
        //
        // A multi-instance is not a bare parameter: it is an ordinary Static member that happens
        // never to carry `Remanence`. Retention belongs to the CALLED block's members, so there is
        // nothing to state here — and that ONE difference is now expressed on the WRITE side
        // (`WriteMember`'s `omitRemanence`, supplied by the block's own CALL/fixed-shape instance
        // names) rather than by flattening the member to a shape that cannot hold its own data.
        var hasExpandedInterface = member.Elements().Any(e => e.Name.LocalName == "Sections");
        if (member.Attribute("Remanence") is null && hasExpandedInterface)
        {
            // requireSetPoint: false — a multi-instance's AttributeList carries SetPoint in the real
            // MB_SERVER export but is not assumed to, so its absence is tolerated rather than fatal.
            var instanceAttributes = RequireDefaultBooleanAttributes(member, context, name, requireSetPoint: false);
            return new DbMember(
                name, datatype, Retain: false, StartValue: null, Version: version, SetPoint: instanceAttributes.SetPoint,
                NestedMembers: null, IsBareParameter: false,
                ExternalAccessible: instanceAttributes.ExternalAccessible,
                ExternalVisible: instanceAttributes.ExternalVisible,
                ExternalWritable: instanceAttributes.ExternalWritable,
                Comment: ParseOptionalComment(member),
                Subelements: subelements);
        }

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
                NestedMembers: null, IsBareParameter: true, Informative: isInformative, InformativeComment: informativeComment,
                Subelements: subelements);
        }

        var remanence = (string?)member.Attribute("Remanence");
        var retain = remanence switch
        {
            "NonRetain" => false,
            "Retain" => true,

            // ABSENT entirely, on a member that HAS an <AttributeList> and no expanded interface —
            // a PARAMETER (Input/Output/InOut). Retention is meaningless on one: it has no storage of
            // its own, which is why TIA refuses `Remanence` there at import (FI-59) and why this
            // writer omits it. Absence therefore means "not applicable", not "unknown", and reads as
            // NonRetain.
            //
            // This branch is the READ HALF of that write rule, and it was missing — the same
            // write-fixed/read-missed shape as FI-56 and FI-58, caught here by the `to-ir -> to-xml
            // -> to-ir` self-stability check rather than by a test: the converter could not read back
            // its own parameter-section output, which takes the re-export leg of the gate contract,
            // drift-check and the round-trip harness out for every block with a parameter.
            null => false,

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

            if (!IsAnonymousStructDatatype(datatype))
            {
                throw new UnsupportedConstructException(
                    $"{context} member '{name}' has direct nested <Member> children but Datatype is '{datatype}', not 'Struct' or 'Array[…] of Struct' — only those have been observed with this shape.");
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
                Comment: comment, Subelements: subelements);
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
                Comment: comment, Subelements: subelements);
        }

        var startValue = startValueElement?.Value;
        return new DbMember(
            name, datatype, retain, string.IsNullOrEmpty(startValue) ? null : startValue, Version: version, SetPoint: booleanAttributes.SetPoint, NestedMembers: null,
            ExternalAccessible: booleanAttributes.ExternalAccessible, ExternalVisible: booleanAttributes.ExternalVisible, ExternalWritable: booleanAttributes.ExternalWritable,
            Comment: comment, Subelements: subelements);
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

    // FI-56. A datatype that NAMES a type — `"UDT_X"`, or `Array[1..8] of "UDT_X"` — as opposed to
    // an anonymous `Struct`. The quotes are TIA's own marker for a named-type reference, which is
    // what makes this decidable without a type table: a member whose type is named elsewhere has its
    // definition elsewhere, so an inline expansion of it is redundant. `Struct` has no name and no
    // definition but the inline one, so its expansion is load-bearing and is still refused.
    private static bool IsNamedTypeReference(string datatype) => datatype.Contains('"');

    /// <summary>Parses the minimal Name/Datatype[/StartValue]-only member shape — a structured member's own nested members, and an FB's Temp-section members.</summary>
    public static DbMember ParseBareMember(XElement member, string context, string ownerMemberName)
    {
        var bareName = (string?)member.Attribute("Name") ?? "<unnamed>";
        var datatypeAttribute = (string?)member.Attribute("Datatype") ?? string.Empty;

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

        // TIA EXPANDS A MEMBER WHOSE TYPE IS A NAMED UDT — including an ARRAY OF ONE — into a nested
        // <Sections> on re-export. FI-56 (2026-08-08) taught this path to accept that expansion
        // instead of hard-erroring on it, which is what had made a re-export unreadable: `to-ir`
        // rejected any block or DB carrying an array-of-UDT interface member, `drift-check` reported
        // DRIFTED for files that were themselves the to-ir output of the exports it compared them
        // against, and two separate agents fell back to grepping raw XML to prove a round trip.
        //
        // 🔴 FI-75 (2026-08-12). FI-56 accepted it by COLLAPSING it — discarding the whole expansion
        // on the reasoning that the IR already names the type, so TIA's rendering of that type is
        // redundant. *** THE VALUES INSIDE AN EXPANSION ARE THE USE SITE'S OWN, NOT THE TYPE'S. ***
        // Measured on the committed corpus: `MotorFwdRevIOSet` declares NO start value for `FTTime`,
        // `ReverseDelay` or `ReverseIgnoreFT`, while `iDB_MotorFwdRevSystem_Shredder` sets them to
        // 10.0 / 8.0 / 12.0 — commissioning setpoints, present only at the use site. A collapse
        // discards them at exit 0: the same silent-loss class as the 182 dropped <Subelement> values.
        //
        // That corpus never lost one only because the TOP-LEVEL ParseMember path always KEPT the
        // expansion, and every quoted-UDT member in it sits at the top level (measured: 6 of 6, all
        // at nesting depth 0). So the two paths disagreed about the identical construct, and the
        // collapse was an asymmetry rather than a principle. It is also no longer needed: the
        // recurse-and-keep machinery this requires was built for the doubly-nested case below, and
        // WriteBareMember already re-emits TIA's own <Sections><Section Name="None"> shape.
        //
        // A `<Sections>` at the bare position therefore has TWO dispositions, not three:
        //
        //   1. bare `Struct` -> STILL A HARD ERROR, unchanged, and this is the distinction the whole
        //      rule turns on. An ANONYMOUS structured member's <Sections> carries its ONLY
        //      definition, and there is no type name to write it back out under.
        //   2. anything else — a QUOTED named-type reference (`"UDT_X"`, FI-75) or an UNQUOTED
        //      SYSTEM structured type (`IP_V4`, `TCON_IP_v4`, `DTL`, `TON_TIME`) -> RECURSE AND KEEP.
        //
        // The system-type half was the 2026-08-12 doubly-nested fix, on the critical path for
        // `MB_SERVER`, whose `CONNECT` port must point at a `TCON_IP_v4` static that nests an `IP_V4`
        // that nests an `Array[1..4] of Byte`: `IP_V4`'s expansion holds `ADDR`'s four <Subelement>
        // start values — the remote IP address. Both halves are now the same rule, and it is the one
        // the top-level ParseMember has always followed (`T_Modbus_Comms : TON_TIME` keeps PT/ET/IN/Q).
        //
        // The test is the literal `Struct` rather than IsAnonymousStructDatatype deliberately:
        // `Array[…] of Struct` has recursed and kept here since the doubly-nested fix, and narrowing
        // it now would turn a shape that round-trips into a new hard error for no gain.
        var isRecursableStructuredType = datatypeAttribute != "Struct";

        var unexpectedChildren = member.Elements()
            .Select(e => e.Name.LocalName)
            .Where(n => n != "StartValue" && n != SubelementElementName)
            .Where(n => !(n == "Sections" && isRecursableStructuredType))
            .ToList();
        if (unexpectedChildren.Count > 0)
        {
            throw new UnsupportedConstructException(
                $"{context} member '{ownerMemberName}' has a nested/bare member '{bareName}' with unexpected content " +
                $"[{string.Join(", ", unexpectedChildren)}] — an anonymous 'Struct' whose <Sections> carries its only " +
                "definition, or any shape beyond a bare Name/Datatype/StartValue/Subelement member, is outside this slice.");
        }

        var name = RequireAttribute(member, "Name");
        var datatype = RequireAttribute(member, "Datatype");
        var startValue = member.Elements().FirstOrDefault(e => e.Name.LocalName == "StartValue")?.Value;
        var subelements = ParseSubelements(member, context, name);

        var bareSections = member.Elements().FirstOrDefault(e => e.Name.LocalName == "Sections");
        var nestedMembers = bareSections is not null && isRecursableStructuredType
            ? ParseNestedMembers(bareSections, context, name)
            : null;

        return new DbMember(
            name, datatype, Retain: false, string.IsNullOrEmpty(startValue) ? null : startValue,
            Version: (string?)member.Attribute("Version"), NestedMembers: nestedMembers, Subelements: subelements);
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
    ///
    /// Also reads the optional Member-level `<Comment>` (2026-07-16) — same shape and optionality
    /// as <see cref="ParseMember"/>'s; see <see cref="WriteTypeMember"/> for the shape provenance
    /// and its live-verification status.
    /// </summary>
    public static DbMember ParseTypeMember(XElement member, string context)
    {
        var name = RequireAttribute(member, "Name");
        var datatype = RequireAttribute(member, "Datatype");

        RequireKnownChildren(member, context, name);
        var subelements = ParseSubelements(member, context, name);

        // FI-64 (2026-08-09). THE SAME NAMED-TYPE EXPANSION FI-56 FIXED, ON THE THIRD PARSE PATH.
        //
        // TIA expands a member whose type is a NAMED UDT — including an array of one — into a
        // nested <Sections> on re-export. FI-56 taught `ParseBareMember` to collapse that back to
        // the type reference the IR already names; `ParseTypeMember` was left refusing it outright,
        // so a PLC data type carrying such a member could not be read back at all:
        //     "member 'Claim' has nested structured content (<Sections>)"
        //
        // WHY THAT MATTERS MORE THAN A PARSE ERROR. The re-export round trip is the only check on
        // this project that has caught defects every other gate passed — three separate times on one
        // job: a stale .xml imported with every gate green; a UDT member present in the type but
        // missing from a block's inline interface expansion, preflight clean over it; and a
        // comment-only edit left out of a to-xml list. A type the converter cannot read back loses
        // that check entirely and degrades to reading raw XML by hand, which is what an agent had to
        // do here.
        //
        // 🔴 FI-75 (2026-08-12). FI-56's rule was applied here too — and its COLLAPSE came with it.
        // The expansion is NOT redundant: its values are the USE SITE's, not the type's (see
        // ParseBareMember's own note for the corpus measurement — three commissioning setpoints that
        // exist nowhere but the use site). So a named-type expansion is now RECURSED INTO AND KEPT
        // here as well, matching ParseBareMember and the top-level ParseMember. THE DISTINCTION THAT
        // MUST HOLD is unchanged: an ANONYMOUS structured member's <Sections> carries its only
        // definition and is still refused outright. Anonymous nesting on this path arrives as direct
        // <Member> children with Datatype "Struct" (handled below), so refusing <Sections> for
        // anything that is not a named-type reference keeps that case exactly as it was.
        var hasNestedSections = member.Elements().Any(e => e.Name.LocalName == "Sections");
        if (hasNestedSections && !IsNamedTypeReference(datatype))
        {
            throw new UnsupportedConstructException(
                $"{context} member '{name}' has nested structured content (<Sections>) with Datatype '{datatype}', which is not a named type reference — " +
                "an anonymous structured member's <Sections> carries its only definition, so collapsing it would discard real members. Refused rather than guessed at.");
        }

        var booleanAttributes = RequireDefaultBooleanAttributes(member, context, name, requireSetPoint: true);

        // Same optional Member-level <Comment> shape ParseMember reads (WriteTypeMember has the
        // shape provenance). Previously not read at all here — a Comment on a member in this
        // position was silently dropped crossing to-ir, a real silent-loss bug (this parser is
        // also the anonymous-Struct nested-member path, where WriteMember's own inverse would
        // then hard-error on the re-write it could never round-trip to).
        var comment = ParseOptionalComment(member);

        var directNestedMembers = member.Elements().Where(e => e.Name.LocalName == "Member").ToList();
        if (directNestedMembers.Count > 0)
        {
            if (!IsAnonymousStructDatatype(datatype))
            {
                throw new UnsupportedConstructException(
                    $"{context} member '{name}' has direct nested <Member> children but Datatype is '{datatype}', not 'Struct' or 'Array[…] of Struct' — only those have been observed with this shape.");
            }

            var nestedMembers = directNestedMembers.Select(m => ParseTypeMember(m, $"{context} member '{name}'")).ToList();
            return new DbMember(
                name, datatype, Retain: false, StartValue: null, Version: (string?)member.Attribute("Version"),
                SetPoint: booleanAttributes.SetPoint, NestedMembers: nestedMembers,
                ExternalAccessible: booleanAttributes.ExternalAccessible, ExternalVisible: booleanAttributes.ExternalVisible, ExternalWritable: booleanAttributes.ExternalWritable,
                Comment: comment, Subelements: subelements);
        }

        var startValueElement = member.Elements().FirstOrDefault(e => e.Name.LocalName == "StartValue");
        var startValue = startValueElement?.Value;

        // FI-75. A named type's expansion arrives in the bare shape inside <Sections><Section
        // Name="None">, exactly as it does at the other two positions, so it is read with the same
        // parser — one shape, one reader. Only a named-type reference can reach here with a
        // <Sections>: anything else threw above.
        var expandedSections = member.Elements().FirstOrDefault(e => e.Name.LocalName == "Sections");
        var expandedMembers = expandedSections is not null
            ? ParseNestedMembers(expandedSections, context, name)
            : null;

        return new DbMember(
            name, datatype, Retain: false, string.IsNullOrEmpty(startValue) ? null : startValue,
            Version: (string?)member.Attribute("Version"), SetPoint: booleanAttributes.SetPoint,
            NestedMembers: expandedMembers,
            ExternalAccessible: booleanAttributes.ExternalAccessible, ExternalVisible: booleanAttributes.ExternalVisible, ExternalWritable: booleanAttributes.ExternalWritable,
            Comment: comment, Subelements: subelements);
    }

    /// <summary>
    /// Inverse of <see cref="ParseTypeMember"/> — no Remanence/Accessibility attribute, same four
    /// BooleanAttributes as WriteMember's own AttributeList. Recursive, with the SAME two nesting
    /// shapes <see cref="WriteMember"/> chooses between, on the same test: an ANONYMOUS structured
    /// member writes its nested members as direct children, and a NAMED type's expansion writes as a
    /// <c>&lt;Sections&gt;&lt;Section Name="None"&gt;</c> wrapper of bare members (FI-75). Writing a
    /// named type's expansion as direct children would have replaced a silent loss with a silent
    /// corruption, so the shape is chosen by the datatype rather than by which parser produced it.
    /// A member's own <see cref="DbMember.Comment"/> is written in the same position as
    /// <see cref="WriteMember"/>'s — see the shape note inside.
    /// </summary>
    public static XElement WriteTypeMember(DbMember member)
    {
        var attributeList = new XElement(
            Ns + "AttributeList",
            new XElement(Ns + "BooleanAttribute", new XAttribute("Name", "ExternalAccessible"), new XAttribute("SystemDefined", "true"), member.ExternalAccessible ? "true" : "false"),
            new XElement(Ns + "BooleanAttribute", new XAttribute("Name", "ExternalVisible"), new XAttribute("SystemDefined", "true"), member.ExternalVisible ? "true" : "false"),
            new XElement(Ns + "BooleanAttribute", new XAttribute("Name", "ExternalWritable"), new XAttribute("SystemDefined", "true"), member.ExternalWritable ? "true" : "false"),
            new XElement(Ns + "BooleanAttribute", new XAttribute("Name", "SetPoint"), new XAttribute("SystemDefined", "true"), member.SetPoint ? "true" : "false"));

        var memberElement = new XElement(
            Ns + "Member",
            new XAttribute("Name", member.Name),
            new XAttribute("Datatype", member.Datatype));

        if (member.Version is not null)
        {
            memberElement.Add(new XAttribute("Version", member.Version));
        }

        memberElement.Add(attributeList);

        // Comment position (after </AttributeList>, before nested Members/<StartValue>) is
        // mirrored from the proven WriteMember shape (FB_PusherControl/FB_ShredderSequencer,
        // committed 2026-07-16, simatic-ml/test-project001/). LIVE-VERIFIED on SW.Types.PlcStruct
        // 2026-07-16: commented flat and nested-sub-struct UDTs imported, type-compiled,
        // re-exported from SampleProject and round-tripped to-ir byte-identically, comments
        // intact (stage-gates, "UDT member comments live-verified").
        AddCommentElement(memberElement, member);
        AddSubelements(memberElement, member);

        if (member.NestedMembers is not null && IsAnonymousStructDatatype(member.Datatype))
        {
            // Anonymous struct: nested members are direct children, each with its own full
            // AttributeList. Unchanged, and the same disposition WriteMember makes on the same test.
            foreach (var nested in member.NestedMembers)
            {
                memberElement.Add(WriteTypeMember(nested));
            }
        }
        else if (member.NestedMembers is not null)
        {
            // FI-75: a NAMED type's expansion, back in TIA's own shape — the bare members it was
            // read from, inside <Sections><Section Name="None">.
            var noneSection = new XElement(Ns + "Section", new XAttribute("Name", "None"));
            foreach (var nested in member.NestedMembers)
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

    // The one proven Member-level Comment shape (<Comment><MultiLanguageText Lang="en-US">…
    // </MultiLanguageText></Comment>, directly after </AttributeList>) — confirmed by live TIA
    // import + compile + re-export on an ordinary Static member 2026-07-15, and present verbatim
    // in the committed genuine re-exports (simatic-ml/test-project001/FB_PusherControl.xml, 5
    // Member-level instances). Shared by WriteMember and WriteTypeMember so both positions emit
    // the identical shape rather than two hand-kept copies.
    private static void AddCommentElement(XElement memberElement, DbMember member)
    {
        if (member.Comment is not null)
        {
            memberElement.Add(new XElement(
                Ns + "Comment",
                new XElement(Ns + "MultiLanguageText", new XAttribute("Lang", "en-US"), member.Comment)));
        }
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
    /// <param name="omitRemanence">
    /// Emits the FULL member shape but WITHOUT <c>Remanence</c> — the MULTI-INSTANCE static shape, and
    /// the PARAMETER-section shape (2026-08-12). Deliberately narrower than the minimal shape
    /// <see cref="DbMember.IsBareParameter"/> selects, which drops the <c>AttributeList</c> and
    /// <c>Version</c> too: TIA's own export carries both on a multi-instance and on an FB parameter, and
    /// flattening them away is what silently produced a VERSIONLESS instance declaration (see
    /// <see cref="ParseMember"/>'s multi-instance branch).
    ///
    /// <para>The one thing TIA actually refuses in either position is <c>Remanence</c> — "The attribute
    /// 'Remanence' cannot be set" — because retention belongs to the CALLED block's members for a
    /// multi-instance, and a parameter has no storage of its own at all. Supplied by
    /// <see cref="BlockSourceWriter"/> from the SECTION and from the block's own CALL and fixed-shape
    /// instance names — facts the file already contains, rather than a token an author must remember.
    /// It cannot be read off the datatype: a UDT-typed static (the C-132 interface member) is also a
    /// quoted name and genuinely DOES carry Remanence. Timers are unaffected — they arrive as
    /// TimerBindings, not Calls, and their full shape is confirmed real.</para>
    /// </param>
    public static XElement WriteMember(DbMember member, bool includeSetPoint = true, bool omitRemanence = false)
    {
        // The GENUINELY minimal shape — `FC Scale`'s attribute-less parameters and OB system
        // parameters. Selected by the member's own captured shape, never forced by a caller: a
        // `bareShape` flag existed for that until 2026-08-12 and was left with no production caller
        // once multi-instances and parameter sections both moved to `omitRemanence`. Removed rather
        // than kept as a vestige — an unused forcing flag is exactly the thing a later change reaches
        // for by mistake.
        if (member.IsBareParameter)
        {
            var bareElement = new XElement(
                Ns + "Member",
                new XAttribute("Name", member.Name),
                new XAttribute("Datatype", member.Datatype),
                new XAttribute("Accessibility", "Public"));

            // Version on the minimal shape too: it is stated by the source, printed by the IR line
            // (`VERSION 5.3`), and was the only thing standing between a correct instance declaration
            // and a versionless one.
            if (member.Version is not null)
            {
                bareElement.Add(new XAttribute("Version", member.Version));
            }

            if (member.Informative)
            {
                bareElement.Add(new XAttribute("Informative", "true"));
                bareElement.Add(new XElement(
                    Ns + "Comment",
                    new XElement(Ns + "MultiLanguageText", new XAttribute("Lang", "en-US"), member.InformativeComment)));
            }

            AddSubelements(bareElement, member);

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

        if (!omitRemanence)
        {
            memberElement.Add(new XAttribute("Remanence", member.Retain ? "Retain" : "NonRetain"));
        }

        memberElement.Add(
            new XAttribute("Accessibility", "Public"),
            attributeList);

        AddCommentElement(memberElement, member);
        AddSubelements(memberElement, member);

        if (isStructured && IsAnonymousStructDatatype(member.Datatype))
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
        // Comment (DbModel.cs's own DbMember.Comment doc comment) is supported on the ordinary
        // WriteMember shape and (as of 2026-07-16) the PLC-data-type/anonymous-Struct
        // WriteTypeMember shape — but Temp members and a UDT-typed/SFB-instance structured
        // member's own nested fields (both routed here) were part of neither grounding, and a
        // UDT-typed member's fields carry their comments on the TYPE definition itself, not per
        // use site. Hard-erroring rather than silently dropping the text if one somehow arrives
        // here (e.g. hand-authored IR nesting a COMMENT under a UDT-typed Static member's own
        // field).
        if (bare.Comment is not null)
        {
            throw new UnsupportedConstructException(
                $"Member '{bare.Name}' has a Comment but is in the bare-member position (a Temp member, or a UDT-typed/SFB-instance structured member's own nested field) — member comments are supported on an ordinary Static/Input/Output/DB member (WriteMember) and on a PLC-data-type or anonymous-Struct member (WriteTypeMember), not here; a UDT-typed member's fields take their comments from the TYPE definition itself.");
        }

        var element = new XElement(
            Ns + "Member",
            new XAttribute("Name", bare.Name),
            new XAttribute("Datatype", bare.Datatype));

        // Version is now CARRIED at this position rather than accepted-and-discarded (FI-58's
        // disposition, correct while the expansion was always collapsed). Once a doubly-nested
        // structured member's own <Sections> is kept and re-emitted, dropping its Version produces a
        // versionless system type where the source stated one — the same silent-loss shape as the
        // multi-instance Version drop, one level deeper.
        if (bare.Version is not null)
        {
            element.Add(new XAttribute("Version", bare.Version));
        }

        AddSubelements(element, bare);

        if (bare.NestedMembers is not null)
        {
            var noneSection = new XElement(Ns + "Section", new XAttribute("Name", "None"));
            foreach (var nested in bare.NestedMembers)
            {
                noneSection.Add(WriteBareMember(nested));
            }

            element.Add(new XElement(Ns + "Sections", noneSection));
        }
        else if (bare.StartValue is not null)
        {
            element.Add(new XElement(Ns + "StartValue", bare.StartValue));
        }

        return element;
    }

    private static string RequireAttribute(XElement element, string name) =>
        element.Attribute(name)?.Value
            ?? throw new SimaticMlFormatException($"<{element.Name.LocalName}> is missing required attribute '{name}'.");
}
