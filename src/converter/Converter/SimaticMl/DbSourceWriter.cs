using System.Xml.Linq;

namespace Converter.SimaticMl;

/// <summary>
/// Writes a `SW.Blocks.GlobalDB`/`SW.Blocks.InstanceDB` export XML from a <see cref="DbSource"/>
/// — the inverse of <see cref="DbSourceParser"/>. Member-level writing is shared with
/// <see cref="BlockSourceWriter"/> via <see cref="DbInterfaceMembers"/> — same XML shape for a
/// DB's own Static section and an FB's Static/Temp sections.
/// </summary>
public static class DbSourceWriter
{
    public static XDocument Write(DbSource db)
    {
        var nextAuxId = 100_000;

        var staticSection = new XElement(DbInterfaceMembers.Ns + "Section", new XAttribute("Name", "Static"));
        foreach (var member in db.Members)
        {
            staticSection.Add(DbInterfaceMembers.WriteMember(member));
        }

        // Input/Output/InOut: confirmed real 2026-07-13, `TomraControlInst1` — see DbModel.cs's own
        // doc comment.
        //
        // 🔴 SECTION PRESENCE IS DECIDED BY DB KIND, NOT BY WHETHER MEMBERS EXIST (2026-08-13).
        //
        // AN INSTANCE DB'S Interface ALWAYS DECLARES ALL FOUR — Input, Output, InOut, Static — even
        // when the first three are empty. Measured against every real TIA V20 export of one in the
        // corpus (`simatic-ml/test-project001/iDB_MotorFwdRevSystem_Shredder.xml`,
        // `iDB_PusherControl.xml`, `iDB_ShredderSequencer.xml`): each opens
        // `<Section Name="Input" /><Section Name="Output" /><Section Name="InOut" />` before Static.
        // Not a coincidence of those three — an instance DB IS an FB's storage, and
        // BlockSourceWriter already emits all three unconditionally for an FB (the sole exception
        // being an OB, where TIA's own Import() rejects Output/InOut outright).
        //
        // A GLOBAL DB is the opposite, and equally measured: all seven in that corpus declare Static
        // ALONE, with no Input/Output/InOut element of any kind. So the discriminator is the DB kind,
        // which the model already carries as InstanceOfName — the same field that decides the root
        // element name below. Nothing is stored twice and nothing new has to be authored.
        //
        // WHY NOT "emit it when the model says the section is present". The model CANNOT say:
        // InOutMembers is non-nullable with an empty default, so *absent* and *present-but-empty* are
        // the same value, and the readable IR has no INOUT header for an empty section either.
        // Making all three nullable and teaching the IR to carry the distinction would also work —
        // but it puts a TIA STRUCTURAL CONSTANT into hand-authored text, so an AI writing a new
        // iDB's `.ir` would have to remember three empty section headers or silently emit a document
        // TIA never produces. Deriving it is the fix that cannot be forgotten.
        //
        // WHAT IT COST, AND WHY NOTHING SAID SO: `to-xml` emitted Input, Output, Static for an
        // instance DB where TIA emits Input, Output, InOut, Static. The Normalizer aligns interface
        // sections POSITIONALLY, so the one missing empty element slid Static into InOut's slot and
        // every member of it read as added-then-missing — 26 differences on
        // `iDB_MotorFwdRevSystem_Shredder`, whose committed export was independently confirmed
        // current. *** NO RE-EXPORT COULD EVER HAVE CLEARED THAT ***, and while it stood
        // `drift-check` could not see the block's real content at all: a permanently-red check that
        // is also blind. `CompareRunner` now pairs sections by NAME so the same shape reports as ONE
        // difference rather than a cascade — that is diagnosis, this is the cure, and the two are
        // deliberately separate (a comparator taught to forgive a missing section would have HIDDEN
        // this instead, which is how the MemoryLayout hole survived a green drift-check).
        var isInstanceDb = db.InstanceOfName is not null;
        var sectionsChildren = new List<XElement>();
        if (isInstanceDb || db.InputMembers is not null)
        {
            sectionsChildren.Add(WriteMemberSection("Input", db.InputMembers ?? Array.Empty<DbMember>()));
        }

        if (isInstanceDb || db.OutputMembers is not null)
        {
            sectionsChildren.Add(WriteMemberSection("Output", db.OutputMembers ?? Array.Empty<DbMember>()));
        }

        if (isInstanceDb || db.InOutMembers.Count > 0)
        {
            sectionsChildren.Add(WriteMemberSection("InOut", db.InOutMembers));
        }

        sectionsChildren.Add(staticSection);

        var sectionsElement = new XElement(
            DbInterfaceMembers.Ns + "Sections",
            sectionsChildren);

        var attributeListChildren = new List<XElement>();
        if (db.InstanceOfName is not null)
        {
            attributeListChildren.Add(new XElement("InstanceOfName", db.InstanceOfName));
            attributeListChildren.Add(new XElement("InstanceOfType", "FB"));
        }

        attributeListChildren.Add(new XElement("Interface", sectionsElement));

        // MemoryLayout goes immediately before Name, matching every real export's own element
        // order (TIA writes this AttributeList alphabetically). Emitted ONLY when the IR carries
        // one — absent means "no opinion", never a default; see BlockMemoryLayout.
        BlockMemoryLayout.AppendIfPresent(attributeListChildren, db.MemoryLayout);

        attributeListChildren.Add(new XElement("Name", db.Name));
        attributeListChildren.Add(new XElement("Namespace"));
        attributeListChildren.Add(new XElement("Number", db.Number));
        attributeListChildren.Add(new XElement("ProgrammingLanguage", "DB"));

        var attributeList = new XElement("AttributeList", attributeListChildren);

        var objectList = new XElement("ObjectList", WriteComment(db.Comment, ref nextAuxId));

        var rootElementName = db.InstanceOfName is not null ? "SW.Blocks.InstanceDB" : "SW.Blocks.GlobalDB";
        var root = new XElement(
            rootElementName,
            new XAttribute("ID", db.RootUId),
            attributeList,
            objectList);

        var document = new XElement(
            "Document",
            new XElement("Engineering", new XAttribute("version", "V20")),
            root);

        return new XDocument(document);
    }

    // Input/Output/InOut all share the same member shape (Static's own full shape minus the
    // SetPoint BooleanAttribute) — same helper BlockSourceWriter uses for its own Input/Output/
    // InOut sections.
    private static XElement WriteMemberSection(string name, IReadOnlyList<DbMember> members)
    {
        var section = new XElement(DbInterfaceMembers.Ns + "Section", new XAttribute("Name", name));
        foreach (var member in members)
        {
            section.Add(DbInterfaceMembers.WriteMember(member, includeSetPoint: false));
        }

        return section;
    }

    internal static XElement WriteComment(string? comment, ref int nextAuxId) =>
        WriteMultilingualText(comment, "Comment", ref nextAuxId);

    // Generalized once a second real CompositionName (S1 item 16, network Title, 2026-07-12)
    // confirmed the shape is identical to Comment's own, differing only in CompositionName and
    // text content — same synthetic-ID reasoning as Comment's own (not round-tripped from the
    // source, not believed to carry semantic meaning beyond must-exist-and-be-unique).
    internal static XElement WriteMultilingualText(string? text, string compositionName, ref int nextAuxId)
    {
        var textId = nextAuxId++;
        var itemId = nextAuxId++;

        var textElement = new XElement("Text");
        if (!string.IsNullOrEmpty(text))
        {
            textElement.Value = text;
        }

        var item = new XElement(
            "MultilingualTextItem",
            new XAttribute("ID", itemId),
            new XAttribute("CompositionName", "Items"),
            new XElement("AttributeList", new XElement("Culture", "en-US"), textElement));

        return new XElement(
            "MultilingualText",
            new XAttribute("ID", textId),
            new XAttribute("CompositionName", compositionName),
            new XElement("ObjectList", item));
    }
}
