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

        // MemoryLayout goes immediately before Name, matching every real export's own element
        // order. Emitted ONLY when the IR carries one — absent means "no opinion", never a
        // default; see BlockMemoryLayout.
        BlockMemoryLayout.AppendIfPresent(attributeListChildren, block.MemoryLayout);

        attributeListChildren.Add(new XElement("Name", block.Name));
        attributeListChildren.Add(new XElement("Namespace"));
        attributeListChildren.Add(new XElement("Number", block.Number));
        attributeListChildren.Add(new XElement("ProgrammingLanguage", block.Language));
        if (block.SecondaryType is not null)
        {
            attributeListChildren.Add(new XElement("SecondaryType", block.SecondaryType));
        }

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
    // instances). Originally gated `block.Kind != "FB"` (so: present for FC, absent for FB) —
    // corrected 2026-07-15 to `block.Kind == "FC"` (present *only* for FC) once OB1 Main became
    // the first OB this project ever wrote fresh content into: TIA Import() rejected the old
    // FB-only exclusion just as directly as it rejected Output/InOut below ("Section 'Return' is
    // not valid for this block") — OBs, like FBs, don't carry a Return/Ret_Val section either
    // (an OB is system-called with no return value concept, same as an FB call). The FB-vs-FC
    // distinction this was originally grounded on undersold its own scope: it's really an
    // FC-only feature, not merely an FB exclusion — this hadn't surfaced before because no real
    // OB had ever gone through this writer with content until now.
    //
    // Output/InOut on an OB: confirmed real, 2026-07-15 — building this project's own OB1 Main
    // for the first time (previously always empty, so this path had never been exercised for any
    // OB) hit a genuine TIA Import() rejection: "Section 'Output' is not valid for this block."
    // The old code emitted Output/InOut unconditionally for every block kind (even when the
    // underlying member list is null/empty, WriteMemberSection still regenerates an empty
    // `<Section>` element) — harmless for FB/FC, which tolerate an empty Output/InOut section, but
    // an OB apparently doesn't tolerate the section's mere *presence* at all, empty or not. Same
    // "block kind changes which sections are even legal" reasoning as the Return omission above,
    // now extended to Output/InOut for OBs specifically (System-called OBs take a fixed Input
    // parameter set defined by the OB type itself — TemplateValue/SecondaryType — never Output/
    // InOut in any real example seen, so this isn't just papering over the one rejection).
    private static XElement? WriteInterface(BlockSource block)
    {
        var staticMembers = block.StaticMembers;
        var tempMembers = block.TempMembers;
        var inputMembers = block.InputMembers;
        var outputMembers = block.OutputMembers;
        var inOutMembers = block.InOutMembers;
        var constantMembers = block.ConstantMembers;
        var writeReturn = block.Kind == "FC";
        var writeOutputAndInOut = block.Kind != "OB";

        if (staticMembers is null && tempMembers.Count == 0 && inputMembers is null && outputMembers is null
            && inOutMembers.Count == 0 && constantMembers is null && !writeReturn)
        {
            return null;
        }

        var sectionsChildren = new List<XElement> { WriteMemberSection("Input", inputMembers) };
        if (writeOutputAndInOut)
        {
            sectionsChildren.Add(WriteMemberSection("Output", outputMembers));
            sectionsChildren.Add(WriteMemberSection("InOut", inOutMembers));
        }

        if (staticMembers is not null)
        {
            // MULTI-INSTANCE statics take the minimal member shape — see WriteMember's `bareShape`.
            // Derived from the block's own CALL statements rather than from an IR token, because the
            // datatype cannot tell an FB-typed static from a UDT-typed one (both are quoted names, and
            // the UDT-typed C-132 interface member genuinely does carry Remanence). A member that is
            // called as an instance in this block IS an FB instance; nothing else can be.
            var multiInstanceNames = new HashSet<string>(block.MultiInstanceStatics, StringComparer.Ordinal);

            var staticSection = new XElement(DbInterfaceMembers.Ns + "Section", new XAttribute("Name", "Static"));
            foreach (var member in staticMembers)
            {
                staticSection.Add(DbInterfaceMembers.WriteMember(member, bareShape: multiInstanceNames.Contains(member.Name)));
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
    // FI-59 (2026-08-08). A PARAMETER SECTION NEVER CARRIES `Remanence`, whatever the IR says.
    //
    // TIA rejects it outright, at IMPORT, with a message that names the parameter and not the
    // cause:
    //     "FC_EStopAlarms.FirstScan: The Openness import failed: The attribute 'Remanence' cannot
    //      be set."
    // Retention is meaningless on an Input/Output/InOut parameter — it has no storage of its own —
    // so there is no case in which emitting it is right.
    //
    // Previously the bare shape was opt-in per member, via a `BAREPARAM` token the author had to
    // remember to write. That made the failure mode a late, confusing import rejection for a
    // missing token, on a block that converted and preflighted clean. An agent adding the first
    // parameterised FC call on this project hit exactly that and had to be told the token existed.
    //
    // Deriving it from the SECTION rather than from a token is the general fix: the section already
    // knows these are parameters, which is a fact the file contains rather than something an author
    // must not forget. Same reasoning as deriving multi-instance shape from the block's own CALL
    // statements instead of a marker.
    private static readonly HashSet<string> ParameterSectionNames =
        new(StringComparer.Ordinal) { "Input", "Output", "InOut" };

    private static XElement WriteMemberSection(string name, IReadOnlyList<DbMember>? members)
    {
        var section = new XElement(DbInterfaceMembers.Ns + "Section", new XAttribute("Name", name));
        var isParameterSection = ParameterSectionNames.Contains(name);

        foreach (var member in members ?? Array.Empty<DbMember>())
        {
            section.Add(DbInterfaceMembers.WriteMember(
                member, includeSetPoint: false, bareShape: isParameterSection));
        }

        return section;
    }
}
