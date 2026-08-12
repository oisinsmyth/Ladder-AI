using System.Xml.Linq;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// `MemoryLayout` — optimized vs standard block access — carried through the IR round trip
/// (2026-08-12).
///
/// The defect these guard: a real Standard-access global DB was round-tripped
/// `export -> to-ir -> to-xml` and the regenerated XML had NO MemoryLayout element at all, so a
/// re-import stated no opinion, TIA applied the S7-1200 default (Optimized), and the block became
/// invisible to classic S7comm — while `drift-check` reported *** MATCH ***, import did not error
/// and compile did not error. The first symptom was a runtime Modbus status code.
///
/// <see cref="Fixtures"/>`/GlobalDbStandardMemoryLayout.xml` keeps the grounding export's exact
/// XML shape (a `WithDefaults` export of a standard-access global DB, element for element) with
/// the DB and member names replaced by invented ones — the shape is what is under test.
/// </summary>
public class MemoryLayoutTests
{
    private static XDocument LoadFixture(string name) => XDocument.Load(Path.Combine("Fixtures", name));

    private static string? MemoryLayoutOf(XDocument document) =>
        document.Descendants().FirstOrDefault(e => e.Name.LocalName == "MemoryLayout")?.Value;

    // ---------------------------------------------------------------- the round trip that failed

    /// <summary>
    /// THE test this whole change exists for: take the real Standard export, convert it to IR, and
    /// convert it back — the regenerated XML must still say Standard.
    /// </summary>
    [Fact]
    public void RealStandardExport_ThroughIrAndBack_StillSaysStandard()
    {
        var export = LoadFixture("GlobalDbStandardMemoryLayout.xml");
        Assert.Equal("Standard", MemoryLayoutOf(export));

        var ir = DbIrSerializer.Serialize(DbSourceParser.Parse(export));
        var regenerated = DbSourceWriter.Write(DbIrParser.ParseDb(ir));

        Assert.Equal("Standard", MemoryLayoutOf(regenerated));
    }

    [Theory]
    [InlineData("Standard")]
    [InlineData("Optimized")]
    public void DbMemoryLayout_SurvivesEveryHopOfTheRoundTrip(string layout)
    {
        var export = LoadFixture("GlobalDbStandardMemoryLayout.xml");
        export.Descendants().First(e => e.Name.LocalName == "MemoryLayout").Value = layout;

        var parsed = DbSourceParser.Parse(export);
        Assert.Equal(layout, parsed.MemoryLayout);

        var ir = DbIrSerializer.Serialize(parsed);
        Assert.Contains($"  MEMORYLAYOUT {layout}\n", ir);

        var reparsed = DbIrParser.ParseDb(ir);
        Assert.Equal(layout, reparsed.MemoryLayout);
        Assert.Equal(layout, MemoryLayoutOf(DbSourceWriter.Write(reparsed)));
    }

    /// <summary>
    /// Element order matters: <see cref="Normalizer"/> compares children positionally, so a
    /// regenerated document whose MemoryLayout sat somewhere else would differ from the real export
    /// it came from. Every real export places it immediately before Name.
    /// </summary>
    [Fact]
    public void RegeneratedDbXml_PlacesMemoryLayoutImmediatelyBeforeName()
    {
        var db = DbSourceParser.Parse(LoadFixture("GlobalDbStandardMemoryLayout.xml"));

        var regenerated = DbSourceWriter.Write(db);

        var attributeList = regenerated.Descendants().First(e => e.Name.LocalName == "AttributeList");
        var names = attributeList.Elements().Select(e => e.Name.LocalName).ToList();
        Assert.Equal(names.IndexOf("Name") - 1, names.IndexOf("MemoryLayout"));
    }

    /// <summary>
    /// A Standard DB regenerated from its own IR is now semantically equivalent to the export it
    /// came from — where before this change the comparison passed only because neither side was
    /// looked at.
    /// </summary>
    [Fact]
    public void RealStandardExport_RegeneratedFromIr_IsSemanticallyEquivalent()
    {
        var export = LoadFixture("GlobalDbStandardMemoryLayout.xml");

        // Serialize-then-reparse so both sides are disk-parsed trees, exactly as DriftCheckRunner
        // does and for the same pre-existing reason: a namespace declaration is a real XAttribute on
        // a file-loaded element but only an implicit part of the name on a freshly built one, and
        // XNode.DeepEquals compares attributes.
        var regenerated = XDocument.Parse(
            DbSourceWriter.Write(DbIrParser.ParseDb(DbIrSerializer.Serialize(DbSourceParser.Parse(export)))).ToString());

        Assert.True(Normalizer.AreSemanticallyEquivalent(export, regenerated));
    }

    // ----------------------------------------------------- absent in IR must emit nothing at all

    /// <summary>
    /// The backward-compatibility rule, and it is not optional: every `.ir` in the repo predates
    /// this and carries no layout. Absent must keep emitting NOTHING — emitting a default would
    /// silently restate the layout of every DB in the corpus, replacing one silent corruption with
    /// a broader one.
    /// </summary>
    [Fact]
    public void DbIrWithNoMemoryLayout_EmitsNoElement()
    {
        var db = DbSourceParser.Parse(LoadFixture("GlobalDbSource.xml"));
        Assert.Null(db.MemoryLayout);

        var ir = DbIrSerializer.Serialize(db);
        Assert.DoesNotContain("MEMORYLAYOUT", ir);

        var regenerated = DbSourceWriter.Write(DbIrParser.ParseDb(ir));
        Assert.Null(MemoryLayoutOf(regenerated));
    }

    [Fact]
    public void CodeBlockIrWithNoMemoryLayout_EmitsNoElement()
    {
        var block = BlockSourceParser.Parse(LoadFixture("FcWithBareParameterMembers.xml"));
        Assert.Null(block.MemoryLayout);

        var regenerated = BlockSourceWriter.Write(
            block,
            Array.Empty<FlgNetwork>(),
            Array.Empty<string>(),
            Array.Empty<string?>(),
            Array.Empty<string?>());

        Assert.Null(MemoryLayoutOf(regenerated));
    }

    // ------------------------------------------------------------- code blocks, not only DBs

    /// <summary>
    /// `MemoryLayout` is a `PlcBlock` property, not a DB one — it is present on every FC, FB and OB
    /// in the committed `simatic-ml/` corpus. An FB's own access mode governs the layout of every
    /// instance DB made from it, so the same silent path exists here.
    /// </summary>
    [Theory]
    [InlineData("Standard")]
    [InlineData("Optimized")]
    public void CodeBlockMemoryLayout_SurvivesEveryHopOfTheRoundTrip(string layout)
    {
        var export = LoadFixture("FcWithBareParameterMembers.xml");
        var attributeList = export.Descendants().First(e => e.Name.LocalName == "AttributeList");
        attributeList.Element("Name")!.AddBeforeSelf(new XElement("MemoryLayout", layout));

        var parsed = BlockSourceParser.Parse(export);
        Assert.Equal(layout, parsed.MemoryLayout);

        // The IR text hop, at the same granularity a whole-block `to-ir`/`to-xml` uses.
        var irBlock = new IrBlock(
            parsed.RootUId, parsed.Kind, parsed.Name, parsed.Number, parsed.Language, parsed.Comment,
            Array.Empty<IrNetwork>(), parsed.StaticMembers, parsed.TempMembers, parsed.Title,
            parsed.InputMembers, parsed.OutputMembers, parsed.InOutMembers, parsed.ConstantMembers,
            parsed.SecondaryType, parsed.MemoryLayout);

        var ir = IrSerializer.SerializeBlockReadable(irBlock);
        Assert.Contains($"MEMORYLAYOUT {layout}\n", ir);

        var reparsed = IrParser.ParseBlockWithoutSidecar(ir);
        Assert.Equal(layout, reparsed.MemoryLayout);

        var regenerated = BlockSourceWriter.Write(
            parsed with { MemoryLayout = reparsed.MemoryLayout },
            Array.Empty<FlgNetwork>(),
            Array.Empty<string>(),
            Array.Empty<string?>(),
            Array.Empty<string?>());

        Assert.Equal(layout, MemoryLayoutOf(regenerated));
    }

    [Fact]
    public void RegeneratedCodeBlockXml_PlacesMemoryLayoutImmediatelyBeforeName()
    {
        var export = LoadFixture("FcWithBareParameterMembers.xml");
        var sourceAttributes = export.Descendants().First(e => e.Name.LocalName == "AttributeList");
        sourceAttributes.Element("Name")!.AddBeforeSelf(new XElement("MemoryLayout", "Standard"));

        var regenerated = BlockSourceWriter.Write(
            BlockSourceParser.Parse(export),
            Array.Empty<FlgNetwork>(),
            Array.Empty<string>(),
            Array.Empty<string?>(),
            Array.Empty<string?>());

        var attributeList = regenerated.Descendants().First(e => e.Name.LocalName == "AttributeList");
        var names = attributeList.Elements().Select(e => e.Name.LocalName).ToList();
        Assert.Equal(names.IndexOf("Name") - 1, names.IndexOf("MemoryLayout"));
    }

    // --------------------------------------------------------------------- the value set is closed

    [Theory]
    [InlineData("standard")]
    [InlineData("Unoptimized")]
    [InlineData("")]
    public void Export_WithUnknownMemoryLayoutValue_IsRefused(string value)
    {
        var export = LoadFixture("GlobalDbStandardMemoryLayout.xml");
        export.Descendants().First(e => e.Name.LocalName == "MemoryLayout").Value = value;

        var ex = Assert.Throws<UnsupportedConstructException>(() => DbSourceParser.Parse(export));
        Assert.Contains("MemoryLayout", ex.Message);
    }

    [Fact]
    public void DbIr_WithUnknownMemoryLayoutValue_IsRefused()
    {
        var ir = DbIrSerializer.Serialize(DbSourceParser.Parse(LoadFixture("GlobalDbStandardMemoryLayout.xml")))
            .Replace("MEMORYLAYOUT Standard", "MEMORYLAYOUT Sideways");

        Assert.Throws<IrFormatException>(() => DbIrParser.ParseDb(ir));
    }

    // --------------------------------------------- the Normalizer no longer looks straight past it

    private static XDocument DbWithLayout(string? layout)
    {
        var document = LoadFixture("GlobalDbStandardMemoryLayout.xml");
        var element = document.Descendants().First(e => e.Name.LocalName == "MemoryLayout");
        if (layout is null)
        {
            element.Remove();
        }
        else
        {
            element.Value = layout;
        }

        return document;
    }

    /// <summary>
    /// The blindness IS the defect: until 2026-08-12 `MemoryLayout` was on the Normalizer's
    /// volatile-element list, so this pair compared EQUAL and `drift-check` could not see a layout
    /// change in either direction.
    /// </summary>
    [Fact]
    public void Normalizer_DifferingMemoryLayouts_AreNotEquivalent()
    {
        Assert.False(Normalizer.AreSemanticallyEquivalent(DbWithLayout("Standard"), DbWithLayout("Optimized")));
    }

    [Fact]
    public void Normalizer_MatchingMemoryLayouts_AreEquivalent()
    {
        Assert.True(Normalizer.AreSemanticallyEquivalent(DbWithLayout("Standard"), DbWithLayout("Standard")));
    }

    /// <summary>
    /// Absent means "no opinion", on this side of the comparison too: a document that states no
    /// layout is not held to the other's value. This is what keeps the whole committed corpus —
    /// every `.ir` of which predates the emit side — from reporting as drifted for a benign reason.
    /// Measured: comparing strictly instead turns 30 committed blocks red at once, against a known
    /// drift baseline of 6.
    /// </summary>
    [Theory]
    [InlineData("Standard")]
    [InlineData("Optimized")]
    public void Normalizer_OneSideStatingNoLayout_IsStillEquivalent(string declared)
    {
        Assert.True(Normalizer.AreSemanticallyEquivalent(DbWithLayout(declared), DbWithLayout(null)));
        Assert.True(Normalizer.AreSemanticallyEquivalent(DbWithLayout(null), DbWithLayout(declared)));
    }
}
