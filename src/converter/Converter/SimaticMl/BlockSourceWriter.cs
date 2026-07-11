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
    public static XDocument Write(BlockSource block, IReadOnlyList<FlgNetwork> networks, IReadOnlyList<string> compileUnitUIds, IReadOnlyList<string?> networkComments)
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
        var interfaceElement = WriteInterface(block.StaticMembers, block.TempMembers);
        if (interfaceElement is not null)
        {
            attributeListChildren.Add(interfaceElement);
        }

        attributeListChildren.Add(new XElement("Name", block.Name));
        attributeListChildren.Add(new XElement("Namespace"));
        attributeListChildren.Add(new XElement("Number", block.Number));
        attributeListChildren.Add(new XElement("ProgrammingLanguage", block.Language));

        var attributeList = new XElement("AttributeList", attributeListChildren);

        var objectList = new XElement("ObjectList");
        objectList.Add(DbSourceWriter.WriteComment(block.Comment, ref nextAuxId));

        for (var i = 0; i < networks.Count; i++)
        {
            objectList.Add(WriteCompileUnit(compileUnitUIds[i], networks[i], networkComments[i], block.Language, ref nextAuxId));
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

    private static XElement WriteCompileUnit(string uid, FlgNetwork network, string? comment, string language, ref int nextAuxId)
    {
        var isEmpty = network.AccessNodes.Count == 0 && network.Parts.Count == 0 && network.Wires.Count == 0;
        var networkSource = isEmpty ? new XElement("NetworkSource") : new XElement("NetworkSource", FlgNetWriter.Write(network));

        var attributeList = new XElement(
            "AttributeList",
            networkSource,
            new XElement("ProgrammingLanguage", language));

        var objectList = new XElement("ObjectList", DbSourceWriter.WriteComment(comment, ref nextAuxId));

        return new XElement(
            "SW.Blocks.CompileUnit",
            new XAttribute("ID", uid),
            new XAttribute("CompositionName", "CompileUnits"),
            attributeList,
            objectList);
    }

    // Confirmed real, 2026-07-11 (S1 item 7 Phase B, MotorDOL): Input/Output/InOut/Constant
    // always empty, Return always the standard parameterless-FC boilerplate, Static (FB only —
    // absent entirely for an FC, not just empty) and Temp carrying the real member content this
    // slice now models. Returns null (no <Interface> element at all) when there's nothing to
    // say — matches every FC seen and this writer's own proven history: Import() has never
    // complained about its absence.
    private static XElement? WriteInterface(IReadOnlyList<DbMember>? staticMembers, IReadOnlyList<DbMember> tempMembers)
    {
        if (staticMembers is null && tempMembers.Count == 0)
        {
            return null;
        }

        var sectionsChildren = new List<XElement>
        {
            new(DbInterfaceMembers.Ns + "Section", new XAttribute("Name", "Input")),
            new(DbInterfaceMembers.Ns + "Section", new XAttribute("Name", "Output")),
            new(DbInterfaceMembers.Ns + "Section", new XAttribute("Name", "InOut")),
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
        sectionsChildren.Add(new XElement(DbInterfaceMembers.Ns + "Section", new XAttribute("Name", "Constant")));
        sectionsChildren.Add(new XElement(
            DbInterfaceMembers.Ns + "Section",
            new XAttribute("Name", "Return"),
            new XElement(
                DbInterfaceMembers.Ns + "Member",
                new XAttribute("Name", "Ret_Val"),
                new XAttribute("Datatype", "Void"),
                new XAttribute("Accessibility", "Public"))));

        return new XElement("Interface", new XElement(DbInterfaceMembers.Ns + "Sections", sectionsChildren));
    }
}
