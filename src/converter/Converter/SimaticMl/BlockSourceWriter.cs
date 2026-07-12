using System.Xml.Linq;

namespace Converter.SimaticMl;

/// <summary>
/// Writes a full block export XML from a BlockSource + its networks' rebuilt FlgNetwork
/// content — the inverse of <see cref="BlockSourceParser"/>. The FlgNet content itself
/// (<see cref="FlgNetWriter"/>) is schema-verified against real exports (ADR-0001 spike). The
/// surrounding wrapper (this file) reconstructs what's confirmed required by a real
/// `PlcBlockComposition.Import()` call (2026-07-10): the block element's own "ID" attribute,
/// and a unique "ID" on every `MultilingualText`/`MultilingualTextItem` (comments) — TIA
/// rejected each of these in turn as they were found missing, one live-import attempt at a
/// time. `DocumentInfo` (product/version provenance) hasn't been confirmed required or not;
/// omitted as the simpler assumption pending a live attempt that says otherwise.
/// </summary>
public static class BlockSourceWriter
{
    public static XDocument Write(
        BlockSource block,
        IReadOnlyList<FlgNetwork> networks,
        IReadOnlyList<string> compileUnitUIds,
        IReadOnlyList<string?> networkTitles,
        IReadOnlyList<string?> networkComments)
    {
        // Comment-wrapper IDs are synthetic (not round-tripped from the source — not captured
        // during parsing, and not believed to carry semantic meaning beyond "must exist and be
        // unique"). Started well above any real UId/CompileUnit-ID seen so far (small integers,
        // or single hex-ish characters like "D") to make collision unlikely; not proven
        // impossible for an arbitrarily large real block.
        var nextAuxId = 100_000;

        // Namespace is required by Import() even when empty — confirmed real, 2026-07-10
        // ("Missing 'Namespace' identifier attribute"). Not currently captured from the
        // source (BlockSourceParser doesn't read it) — written empty, matching every real
        // export inspected so far (`<Namespace />`), until a project with a non-empty one
        // is seen and this needs to actually round-trip a value.
        var attributeListChildren = new List<XElement>();
        var interfaceElement = WriteInterface(block);
        if (interfaceElement is not null)
        {
            attributeListChildren.Add(interfaceElement);
        }

        attributeListChildren.Add(new XElement("Name", block.Name));
        attributeListChildren.Add(new XElement("Namespace"));
        attributeListChildren.Add(new XElement("Number", block.Number));
        attributeListChildren.Add(new XElement("ProgrammingLanguage", block.Language));

        var attributeList = new XElement("AttributeList", attributeListChildren);

        // Comment then Title — matches the real source's own element order (same order confirmed
        // real at network level; assumed consistent at block level, S1 item 17, 2026-07-12).
        var objectList = new XElement("ObjectList");
        objectList.Add(DbSourceWriter.WriteComment(block.Comment, ref nextAuxId));
        objectList.Add(DbSourceWriter.WriteMultilingualText(block.Title, "Title", ref nextAuxId));

        for (var i = 0; i < networks.Count; i++)
        {
            objectList.Add(WriteCompileUnit(compileUnitUIds[i], networks[i], networkTitles[i], networkComments[i], block.Language, ref nextAuxId));
        }

        var root = new XElement(
            $"SW.Blocks.{block.Kind}",
            new XAttribute("ID", block.RootUId),
            attributeList,
            objectList);

        var document = new XElement(
            "Document",
            new XElement("Engineering", new XAttribute("version", "V20")),
            root);

        return new XDocument(document);
    }

    private static XElement WriteCompileUnit(string uid, FlgNetwork network, string? title, string? comment, string language, ref int nextAuxId)
    {
        var isEmpty = network.AccessNodes.Count == 0 && network.Parts.Count == 0 && network.Wires.Count == 0;
        var networkSource = isEmpty ? new XElement("NetworkSource") : new XElement("NetworkSource", FlgNetWriter.Write(network));

        var attributeList = new XElement(
            "AttributeList",
            networkSource,
            new XElement("ProgrammingLanguage", language));

        // Comment then Title — matches the real source's own element order (confirmed real,
        // 2026-07-12, FC PlantAutoControl).
        var objectList = new XElement(
            "ObjectList",
            DbSourceWriter.WriteMultilingualText(comment, "Comment", ref nextAuxId),
            DbSourceWriter.WriteMultilingualText(title, "Title", ref nextAuxId));

        return new XElement(
            "SW.Blocks.CompileUnit",
            new XAttribute("ID", uid),
            new XAttribute("CompositionName", "CompileUnits"),
            attributeList,
            objectList);
    }

    // Static/Temp: confirmed real, 2026-07-11 (S1 item 7 Phase B, MotorDOL) — Static (FB only,
    // absent entirely for an FC) and Temp carrying real member content. Input/Output/InOut/
    // Constant: confirmed real, 2026-07-12 (S1 item 20, TomraControlSystem/MotorVSDSystem/AirStar) —
    // Input/Output reuse DbInterfaceMembers.WriteMember with `includeSetPoint: false` (mirrors
    // ParseMember's own `requireSetPoint` parameter); Constant uses the genuinely distinct
    // WriteConstantMember shape; InOut only ever regenerates empty (no real populated example
    // seen). Returns null (no <Interface> element at all) only when every one of these is
    // absent/empty — matches every FC seen and this writer's own proven history.
    //
    // Return: confirmed real 2026-07-10 on FCs (the standard parameterless "Ret_Val" boilerplate)
    // and confirmed absent entirely — not even an empty Section element — on every real FB
    // grounded for this item (2026-07-12, TomraControlSystem/MotorVSDSystem/AirStar, 3 independent
    // instances). Emitted only for non-FB blocks (`block.Kind != "FB"`) rather than
    // unconditionally as before this item — the prior unconditional emission was never actually
    // exercised against a real FB's own Interface-section content before now, so this asymmetry
    // went unnoticed; fixed here since it's directly in the path of what this item already
    // extends and would otherwise regenerate an incorrect shape for every FB.
    private static XElement? WriteInterface(BlockSource block)
    {
        var staticMembers = block.StaticMembers;
        var tempMembers = block.TempMembers;
        var inputMembers = block.InputMembers;
        var outputMembers = block.OutputMembers;
        var inOutMembers = block.InOutMembers;
        var constantMembers = block.ConstantMembers;
        var writeReturn = block.Kind != "FB";

        if (staticMembers is null && tempMembers.Count == 0 && inputMembers is null && outputMembers is null
            && inOutMembers.Count == 0 && constantMembers is null && !writeReturn)
        {
            return null;
        }

        var sectionsChildren = new List<XElement>
        {
            WriteMemberSection("Input", inputMembers),
            WriteMemberSection("Output", outputMembers),
            WriteMemberSection("InOut", inOutMembers),
        };

        if (staticMembers is not null)
        {
            var staticSection = new XElement(DbInterfaceMembers.Ns + "Section", new XAttribute("Name", "Static"));
            foreach (var member in staticMembers)
            {
                staticSection.Add(DbInterfaceMembers.WriteMember(member));
            }

            sectionsChildren.Add(staticSection);
        }

        var tempSection = new XElement(DbInterfaceMembers.Ns + "Section", new XAttribute("Name", "Temp"));
        foreach (var member in tempMembers)
        {
            tempSection.Add(DbInterfaceMembers.WriteBareMember(member));
        }

        sectionsChildren.Add(tempSection);

        var constantSection = new XElement(DbInterfaceMembers.Ns + "Section", new XAttribute("Name", "Constant"));
        foreach (var member in constantMembers ?? Array.Empty<DbMember>())
        {
            constantSection.Add(DbInterfaceMembers.WriteConstantMember(member));
        }

        sectionsChildren.Add(constantSection);

        if (writeReturn)
        {
            sectionsChildren.Add(new XElement(
                DbInterfaceMembers.Ns + "Section",
                new XAttribute("Name", "Return"),
                new XElement(
                    DbInterfaceMembers.Ns + "Member",
                    new XAttribute("Name", "Ret_Val"),
                    new XAttribute("Datatype", "Void"),
                    new XAttribute("Accessibility", "Public"))));
        }

        return new XElement("Interface", new XElement(DbInterfaceMembers.Ns + "Sections", sectionsChildren));
    }

    // Input/Output/InOut all share the same member shape (Static's own full shape minus the
    // SetPoint BooleanAttribute — confirmed real, 2026-07-12, S1 item 20) and the same
    // present-but-possibly-empty regeneration pattern, so one helper covers all three.
    private static XElement WriteMemberSection(string name, IReadOnlyList<DbMember>? members)
    {
        var section = new XElement(DbInterfaceMembers.Ns + "Section", new XAttribute("Name", name));
        foreach (var member in members ?? Array.Empty<DbMember>())
        {
            section.Add(DbInterfaceMembers.WriteMember(member, includeSetPoint: false));
        }

        return section;
    }
}
