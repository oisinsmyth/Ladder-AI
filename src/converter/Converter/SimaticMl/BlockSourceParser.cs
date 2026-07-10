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

        var blockComment = ReadComment(objectList);
        MultilingualTextHelper.RequireEmptyTitle(objectList, $"Block '{name}'");
        RequireDefaultInterface(attributeList, $"Block '{name}'");

        var compileUnits = objectList.Elements()
            .Where(e => e.Name.LocalName == "SW.Blocks.CompileUnit")
            .Select(ParseCompileUnit)
            .ToList();

        if (compileUnits.Count == 0)
        {
            throw new SimaticMlFormatException($"Block '{name}' has no CompileUnit (network) content.");
        }

        return new BlockSource(rootUId, kind, name, number, language, blockComment, compileUnits);
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
        if (objectList is not null)
        {
            MultilingualTextHelper.RequireEmptyTitle(objectList, $"Network (CompileUnit ID={uid})");
        }

        return new CompileUnitSource(uid, comment, network);
    }

    /// <summary>Reads a MultilingualText[CompositionName=Comment]'s en-US Text, or null if empty/absent.</summary>
    private static string? ReadComment(XElement objectList) => MultilingualTextHelper.ReadMultilingualText(objectList, "Comment");

    // FC/FB parameters (Input/Output/InOut/Temp/Constant/Return) live here — genuinely semantic
    // content (a block's own contract), unlike Title/block-config flags. Not modeled/round-tripped
    // by this converter slice yet, so a block with real parameters must hard-error, not silently
    // lose them. The only shape confirmed safe to proceed on: every non-Return section empty, and
    // Return holding exactly the standard parameterless-FC boilerplate (one Void "Ret_Val" member)
    // — confirmed real, 2026-07-10, on every block seen so far in this slice.
    private static void RequireDefaultInterface(XElement attributeList, string context)
    {
        var interfaceElement = attributeList.Element("Interface");
        if (interfaceElement is null)
        {
            return;
        }

        var sections = interfaceElement.Descendants().Where(e => e.Name.LocalName == "Section").ToList();
        foreach (var section in sections)
        {
            var sectionName = (string?)section.Attribute("Name");
            var members = section.Elements().Where(e => e.Name.LocalName == "Member").ToList();

            if (sectionName != "Return")
            {
                if (members.Count > 0)
                {
                    throw new UnsupportedConstructException(
                        $"{context} has a non-empty Interface section '{sectionName}' — this converter doesn't model block " +
                        "parameters yet (only Contact/Coil network content).");
                }

                continue;
            }

            var isStandardVoidReturn = members.Count == 1
                && (string?)members[0].Attribute("Name") == "Ret_Val"
                && (string?)members[0].Attribute("Datatype") == "Void";
            if (!isStandardVoidReturn)
            {
                throw new UnsupportedConstructException(
                    $"{context} has a non-standard Return interface — this converter doesn't model block " +
                    "return values yet, only the default parameterless-FC boilerplate.");
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
