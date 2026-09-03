using System.Xml.Linq;
using Converter.Ir;

namespace Converter.SimaticMl;

/// <summary>
/// Writes a `SW.Blocks.GlobalDB`/`SW.Blocks.InstanceDB` export XML from a <see cref="DbSource"/>
/// — the inverse of <see cref="DbSourceParser"/>. Member-level writing is shared with
/// <see cref="BlockSourceWriter"/> via <see cref="DbInterfaceMembers"/> — same XML shape for a
/// DB's own Static section and an FB's Static/Temp sections.
/// </summary>
public static class DbSourceWriter
{
    /// <param name="instanceTypes">
    /// FI-102 (2026-09-03). The corpus this instance DB's member types resolve against — see
    /// <see cref="InstanceDbTypeResolution"/> for the defect and the reasoning. Consulted ONLY for an
    /// instance DB (a global DB cannot hold an FB instance; that is what an instance DB is), and only
    /// for TOP-LEVEL Static members, which are the only ones this writer ever states
    /// <c>Remanence</c> on.
    ///
    /// <para>🔴 <b><c>null</c> means "this caller has no corpus in hand", and it keeps the pre-FI-102
    /// shape — which is WRONG for any instance DB nesting an FB instance.</b> It is left as the default
    /// for the paths that are not headed for a controller (<c>sanitize</c>'s XML→XML de-identification,
    /// and the writer's own unit tests). <b>Every path whose output can be imported passes one</b>:
    /// <c>to-xml</c>, <c>drift-check</c> and <c>preflight</c> all build it from their batch plus
    /// <c>--project</c>. Adding a fourth such path without one silently reintroduces FI-102.</para>
    /// </param>
    public static XDocument Write(DbSource db, InstanceDbTypeResolution? instanceTypes = null)
    {
        var nextAuxId = 100_000;
        var isInstanceDb = db.InstanceOfName is not null;

        var staticSection = new XElement(DbInterfaceMembers.Ns + "Section", new XAttribute("Name", "Static"));
        foreach (var member in db.Members)
        {
            staticSection.Add(DbInterfaceMembers.WriteMember(
                member, omitRemanence: OmitRemanence(db, member, isInstanceDb, instanceTypes)));
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

    // FI-102 (2026-09-03). A MULTI-INSTANCE static of an instance DB never carries `Remanence` — TIA
    // answers "The attribute 'Remanence' cannot be set" and refuses the whole InstanceDB object. The
    // rule itself, and why neither the datatype string nor the member's NAME can decide it, lives in
    // InstanceDbTypeResolution; this is only the placement of it.
    //
    // A GLOBAL DB IS NEVER ASKED. It cannot hold an FB instance — an instance DB is what that is — so
    // there is nothing here for the corpus to decide, and asking would let an unresolvable UDT in a
    // global DB refuse a conversion that TIA accepts today.
    private static bool OmitRemanence(
        DbSource db, DbMember member, bool isInstanceDb, InstanceDbTypeResolution? instanceTypes)
    {
        if (!isInstanceDb || instanceTypes is null)
        {
            return false;
        }

        var ruling = instanceTypes.RuleOn(member);
        return ruling.Ruling switch
        {
            RemanenceRuling.Omit => true,
            RemanenceRuling.Emit => false,

            // Refuse rather than pick one. Both answers are wrong in a way nothing downstream would
            // catch: emitting is FI-102 itself (rejected at import, after convert/preflight/review all
            // passed), omitting silently changes a UDT member's retention on a document that imports
            // and compiles clean.
            _ => throw new UnsupportedConstructException(
                $"instance DB '{db.Name}' member '{member.Name}' {ruling.Reason}"),
        };
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
