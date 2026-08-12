using System.Xml.Linq;

namespace Converter.SimaticMl;

/// <summary>
/// Extracts block-level metadata and per-network CompileUnit content from a full block export
/// XML (what `openness-cli export` produces). Unlike <see cref="FlgNetParser"/>, this layer
/// searches by local element name rather than a pinned namespace for the outer wrapper
/// (Document/AttributeList/ObjectList/CompileUnit) — the exact wrapper namespace wasn't
/// captured during the ADR-0001 grounding spike, only the inner FlgNet content's was. This is
/// a deliberate scoping call, not an oversight: the wrapper is comparatively stable TIA export
/// scaffolding, whereas semantic-loss risk lives in the FlgNet graph, which
/// <see cref="FlgNetParser"/> parses strictly. Flagged in ir/SPEC.md's open items.
/// </summary>
public static class BlockSourceParser
{
    private static readonly HashSet<string> SupportedLanguages = new(StringComparer.Ordinal) { "LAD" };

    public static BlockSource Parse(XDocument document)
    {
        var root = document.Root
            ?? throw new SimaticMlFormatException("Export file has no root element.");

        var blockElement = root.Descendants().FirstOrDefault(e => e.Name.LocalName.StartsWith("SW.Blocks.", StringComparison.Ordinal))
            ?? throw new SimaticMlFormatException("Could not find an SW.Blocks.* element (OB/FB/FC) in the export.");

        var kind = blockElement.Name.LocalName["SW.Blocks.".Length..];

        var attributeList = blockElement.Element("AttributeList")
            ?? throw new SimaticMlFormatException("Block element is missing its <AttributeList>.");

        var rootUId = RequireAttribute(blockElement, "ID");
        var name = RequireChildValue(attributeList, "Name");
        var number = int.Parse(RequireChildValue(attributeList, "Number"));
        var language = RequireChildValue(attributeList, "ProgrammingLanguage");

        if (!SupportedLanguages.Contains(language))
        {
            throw new UnsupportedConstructException(
                $"Block '{name}' has ProgrammingLanguage '{language}'. This converter only handles LAD " +
                "(CLAUDE.md hard rule 1: LAD only).");
        }

        var objectList = blockElement.Element("ObjectList")
            ?? throw new SimaticMlFormatException("Block element is missing its <ObjectList>.");

        var secondaryType = attributeList.Element("SecondaryType")?.Value;

        // MemoryLayout (2026-08-12) — optional, and present on every real FC/FB/OB export. Absent
        // on a converter-written document from before this existed; null preserves that.
        var memoryLayout = BlockMemoryLayout.ReadOptional(attributeList, $"Block '{name}'");

        var blockComment = ReadComment(objectList);
        // Title (S1 item 17, 2026-07-12) — confirmed real at block level after all (two of
        // PlantAutoControl's own dependency FBs, `MotorVSDSystem`/`AirStar`, both titled "VSD Motor"), read
        // the same way network-level Title is (no longer hard-errored via RequireEmptyTitle).
        var blockTitle = MultilingualTextHelper.ReadMultilingualText(objectList, "Title");
        var (staticMembers, tempMembers, inputMembers, outputMembers, inOutMembers, constantMembers) = ParseInterface(attributeList, $"Block '{name}'");

        var compileUnits = objectList.Elements()
            .Where(e => e.Name.LocalName == "SW.Blocks.CompileUnit")
            .Select(ParseCompileUnit)
            .ToList();

        if (compileUnits.Count == 0)
        {
            throw new SimaticMlFormatException($"Block '{name}' has no CompileUnit (network) content.");
        }

        return new BlockSource(
            rootUId, kind, name, number, language, blockComment, compileUnits, staticMembers, tempMembers, blockTitle,
            inputMembers, outputMembers, inOutMembers, constantMembers, secondaryType, MultiInstanceStatics: null,
            MemoryLayout: memoryLayout);
    }

    private static CompileUnitSource ParseCompileUnit(XElement compileUnit)
    {
        var uid = RequireAttribute(compileUnit, "ID");

        var attributeList = compileUnit.Element("AttributeList")
            ?? throw new SimaticMlFormatException("CompileUnit is missing its <AttributeList>.");

        var networkSource = attributeList.Element("NetworkSource")
            ?? throw new SimaticMlFormatException("CompileUnit is missing its <NetworkSource>.");

        // `<NetworkSource />` (no FlgNet child at all) is a real, confirmed shape — an empty
        // placeholder network, not a format error. Modeled as an empty FlgNetwork; GraphReducer
        // turns that into an explicit empty IrNetwork rather than hard-erroring the whole block.
        var flgNetElement = networkSource.Elements().FirstOrDefault(e => e.Name == FlgNetParser.Ns + "FlgNet");
        var network = flgNetElement is null
            ? new FlgNetwork(Array.Empty<AccessNode>(), Array.Empty<PartNode>(), Array.Empty<WireNode>())
            : FlgNetParser.Parse(flgNetElement);

        var objectList = compileUnit.Element("ObjectList");
        var comment = objectList is null ? null : ReadComment(objectList);
        // Title (S1 item 16, 2026-07-12) — the network's own human-visible label, genuinely
        // distinct from Comment; confirmed real and populated on every network of a real block
        // (FC PlantAutoControl), unlike Comment which has been empty on every real network seen all
        // session. Read the same way Comment is (MultilingualTextHelper.ReadMultilingualText),
        // no longer hard-errored via RequireEmptyTitle.
        var title = objectList is null ? null : MultilingualTextHelper.ReadMultilingualText(objectList, "Title");

        return new CompileUnitSource(uid, comment, title, network);
    }

    /// <summary>Reads a MultilingualText[CompositionName=Comment]'s en-US Text, or null if empty/absent.</summary>
    private static string? ReadComment(XElement objectList) => MultilingualTextHelper.ReadMultilingualText(objectList, "Comment");

    // FC/FB parameters live here — genuinely semantic content (a block's own contract), unlike
    // Title/block-config flags. Return must hold exactly the standard parameterless-FC boilerplate
    // (one Void "Ret_Val" member) when present — confirmed real, 2026-07-10; and confirmed
    // FC-specific, 2026-07-12 (S1 item 20) — absent entirely (not even an empty Section element)
    // on every FB grounded for this item.
    //
    // Static/Temp: real, confirmed 2026-07-11 (S1 item 7 Phase B, MotorDOL) — an FB's own
    // instance-data declaration (Static, same shape as a DB's own Static section — reused via
    // DbInterfaceMembers) and its scan-local working variables (Temp, the bare Name/Datatype
    // shape). Static is absent entirely (not just empty) on every FC seen — no Section element
    // for it at all — distinct from an FB with one, hence the nullable return.
    //
    // Input/Output/InOut/Constant: real, confirmed 2026-07-12 (S1 item 20, `FB TomraControlSystem`'s
    // own Input/Output; `FB MotorVSDSystem`/`AirStar`'s own Constant). Input/Output reuse
    // DbInterfaceMembers.ParseMember with `requireSetPoint: false` (same shape as Static, minus
    // the SetPoint BooleanAttribute Static's own AttributeList always carries). Constant is a
    // genuinely distinct third shape (no AttributeList at all, always a required StartValue) —
    // DbInterfaceMembers.ParseConstantMember. InOut is present but empty in every real instance
    // seen (never populated) — parsed the same way as Static/Input/Output would be, so a real
    // populated example (if one ever surfaces) round-trips without further changes, but nothing
    // in this converter has proven that shape live yet.
    //
    // Direct children of the <Sections> wrapper only — NOT .Descendants(), which would also pick
    // up a Static member's own nested <Sections><Section Name="None">...</Section> (a
    // timer/UDT-typed member's own sub-members) and wrongly require it to be empty before
    // DbInterfaceMembers.ParseMember's own, more specific structured-member handling ever runs —
    // same trap DbSourceParser.ParseMembers was fixed for earlier, real recurrence.
    private static (
        IReadOnlyList<DbMember>? StaticMembers,
        IReadOnlyList<DbMember> TempMembers,
        IReadOnlyList<DbMember>? InputMembers,
        IReadOnlyList<DbMember>? OutputMembers,
        IReadOnlyList<DbMember> InOutMembers,
        IReadOnlyList<DbMember>? ConstantMembers) ParseInterface(XElement attributeList, string context)
    {
        var interfaceElement = attributeList.Element("Interface");
        if (interfaceElement is null)
        {
            return (null, Array.Empty<DbMember>(), null, null, Array.Empty<DbMember>(), null);
        }

        var sectionsWrapper = interfaceElement.Elements().FirstOrDefault(e => e.Name.LocalName == "Sections")
            ?? throw new SimaticMlFormatException($"{context} Interface is missing its <Sections> wrapper.");
        var sections = sectionsWrapper.Elements().Where(e => e.Name.LocalName == "Section").ToList();

        IReadOnlyList<DbMember>? staticMembers = null;
        var tempMembers = Array.Empty<DbMember>() as IReadOnlyList<DbMember>;
        IReadOnlyList<DbMember>? inputMembers = null;
        IReadOnlyList<DbMember>? outputMembers = null;
        var inOutMembers = Array.Empty<DbMember>() as IReadOnlyList<DbMember>;
        IReadOnlyList<DbMember>? constantMembers = null;

        foreach (var section in sections)
        {
            var sectionName = (string?)section.Attribute("Name");
            var memberElements = section.Elements().Where(e => e.Name.LocalName == "Member").ToList();

            switch (sectionName)
            {
                case "Static":
                    staticMembers = memberElements.Select(m => DbInterfaceMembers.ParseMember(m, context)).ToList();
                    break;

                case "Temp":
                    tempMembers = memberElements.Select(m => DbInterfaceMembers.ParseBareMember(m, context, "Temp")).ToList();
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

                case "Constant":
                    constantMembers = memberElements.Select(m => DbInterfaceMembers.ParseConstantMember(m, context)).ToList();
                    break;

                case "Return":
                    var isStandardVoidReturn = memberElements.Count == 1
                        && (string?)memberElements[0].Attribute("Name") == "Ret_Val"
                        && (string?)memberElements[0].Attribute("Datatype") == "Void";
                    if (!isStandardVoidReturn)
                    {
                        throw new UnsupportedConstructException(
                            $"{context} has a non-standard Return interface — this converter doesn't model block " +
                            "return values yet, only the default parameterless-FC boilerplate.");
                    }

                    break;

                default:
                    if (memberElements.Count > 0)
                    {
                        throw new UnsupportedConstructException(
                            $"{context} has a non-empty Interface section '{sectionName}' — this converter doesn't model this " +
                            "section yet (Static/Temp/Input/Output/InOut/Constant/Return only).");
                    }

                    break;
            }
        }

        return (staticMembers, tempMembers, inputMembers, outputMembers, inOutMembers, constantMembers);
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
