using System.Xml.Linq;
using Converter.Compare;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// A DB's interface SECTION SET, held against real TIA exports rather than against hand-authored
/// fixtures — and the comparison behaviour that made getting it wrong unreadable.
///
/// <para><b>The defect (2026-08-13).</b> <c>DbSourceWriter</c> emitted <c>Input, Output, Static</c>
/// for an instance DB. TIA emits <c>Input, Output, InOut, Static</c>, the first three empty. The
/// Normalizer aligned sections POSITIONALLY, so one missing EMPTY element slid Static into InOut's
/// slot and the whole section read as added-then-missing: *** 26 differences on
/// iDB_MotorFwdRevSystem_Shredder, a block whose committed export was independently confirmed
/// current. *** No re-export could ever have cleared it, and underneath the noise
/// <c>drift-check</c> could not see the block's real content at all — a permanently-red check that
/// was also blind.</para>
///
/// <para><b>Why these tests read <c>simatic-ml/</c> and not <c>Fixtures/</c>.</b> The two
/// hand-authored instance-DB fixtures both declared <c>Static</c> alone, which is not a shape TIA
/// produces — so every fixture-based round trip agreed with the writer and none of them could have
/// caught this. Reading the expectation out of TIA's own files puts an authority in the loop that is
/// not us (the working agreement's "who in this loop could disagree with us?"). The population
/// assertions below exist so the premise is a MEASUREMENT over the whole corpus, not one sample.</para>
/// </summary>
public class InstanceDbSectionShapeTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "simatic-ml")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException(
            "Could not find repo root (no 'simatic-ml' directory found above test output).");
    }

    private static string ExportPath(string block) =>
        Path.Combine(RepoRoot(), "simatic-ml", "test-project001", block + ".xml");

    /// <summary>Top-level interface section names, in document order.</summary>
    private static List<string> SectionNames(XDocument document)
    {
        var sections = document.Descendants()
            .First(e => e.Name.LocalName == "Interface")
            .Elements().First(e => e.Name.LocalName == "Sections");

        return sections.Elements()
            .Where(e => e.Name.LocalName == "Section")
            .Select(e => (string?)e.Attribute("Name") ?? string.Empty)
            .ToList();
    }

    private static IEnumerable<string> ExportsOfKind(string rootLocalName)
    {
        var dir = Path.Combine(RepoRoot(), "simatic-ml", "test-project001");
        foreach (var path in Directory.EnumerateFiles(dir, "*.xml").OrderBy(p => p, StringComparer.Ordinal))
        {
            var document = XDocument.Load(path);
            if (document.Descendants().Any(e => e.Name.LocalName == rootLocalName))
            {
                yield return path;
            }
        }
    }

    // ------------------------------------------------------------------ the premise, measured

    [Fact]
    public void EveryRealInstanceDbExport_DeclaresInputOutputInOutStatic()
    {
        var exports = ExportsOfKind("SW.Blocks.InstanceDB").ToList();

        // Empty is not clean: a population of zero would make every assertion below vacuous and the
        // run output would be indistinguishable from one where they all held.
        Assert.NotEmpty(exports);

        foreach (var path in exports)
        {
            Assert.Equal(
                new[] { "Input", "Output", "InOut", "Static" },
                SectionNames(XDocument.Load(path)));
        }
    }

    [Fact]
    public void EveryRealGlobalDbExport_DeclaresStaticAlone()
    {
        var exports = ExportsOfKind("SW.Blocks.GlobalDB").ToList();

        Assert.NotEmpty(exports);

        foreach (var path in exports)
        {
            Assert.Equal(new[] { "Static" }, SectionNames(XDocument.Load(path)));
        }
    }

    // ------------------------------------------------------------------------- what we now emit

    /// <summary>
    /// The cure. The input is the LEGACY shape — an instance DB carrying Static alone, which is both
    /// the old hand-authored fixture and, not coincidentally, what our own pre-fix writer produced —
    /// and the expected value is lifted out of a real TIA export rather than written here.
    /// </summary>
    [Fact]
    public void Write_InstanceDb_DeclaresTheSameSectionsARealTiaExportDoes()
    {
        var expected = SectionNames(XDocument.Load(ExportPath("iDB_MotorFwdRevSystem_Shredder")));

        var db = DbSourceParser.Parse(XDocument.Load(Path.Combine("Fixtures", "InstanceDbSource.xml")));
        Assert.Equal(new[] { "Static" }, SectionNames(XDocument.Load(Path.Combine("Fixtures", "InstanceDbSource.xml"))));

        Assert.Equal(expected, SectionNames(DbSourceWriter.Write(db)));
    }

    /// <summary>
    /// The converse, and the reason the rule is keyed on DB KIND rather than applied to every DB: a
    /// global DB must NOT gain the three parameter sections. Without this, "always emit them" would
    /// pass the test above and quietly drift all seven global DBs in the corpus.
    /// </summary>
    [Fact]
    public void Write_GlobalDb_DeclaresTheSameSectionsARealTiaExportDoes()
    {
        var expected = SectionNames(XDocument.Load(ExportPath("DB_Alarms")));

        var db = DbSourceParser.Parse(XDocument.Load(Path.Combine("Fixtures", "GlobalDbSource.xml")));

        Assert.Equal(expected, SectionNames(DbSourceWriter.Write(db)));
    }

    /// <summary>
    /// A global DB that genuinely declares a parameter section still round-trips it. The kind-keyed
    /// rule only ever ADDS sections for an instance DB; it must not start suppressing a real one.
    /// </summary>
    [Fact]
    public void Write_GlobalDbWithARealInputSection_StillEmitsIt()
    {
        var db = DbSourceParser.Parse(XDocument.Load(Path.Combine("Fixtures", "GlobalDbSource.xml")))
            with
        { InputMembers = new[] { new DbMember("Param", "Bool", Retain: false, StartValue: null, IsBareParameter: true) } };

        Assert.Equal(new[] { "Input", "Static" }, SectionNames(DbSourceWriter.Write(db)));
    }

    // --------------------------------------------------- the whole round trip, against real TIA

    /// <summary>
    /// The end-to-end statement of the fix: a real instance-DB export, parsed and written back by
    /// this converter, is EQUIVALENT to what TIA produced. This is the assertion that was worth 26
    /// differences before the fix.
    /// </summary>
    [Theory]
    [InlineData("iDB_MotorFwdRevSystem_Shredder")]
    [InlineData("iDB_PusherControl")]
    [InlineData("iDB_ShredderSequencer")]
    public void RealInstanceDbExport_ParsedAndRewritten_IsEquivalentToTia(string block)
    {
        var export = ExportPath(block);
        var rewritten = WriteTemp(block + "-rewritten", DbSourceWriter.Write(DbSourceParser.Parse(XDocument.Load(export))));

        var report = CompareRunner.Run(export, rewritten);

        Assert.Equal(CompareStatus.Equivalent, report.Status);
        Assert.Empty(report.Differences);
    }

    // ------------------------------------------------- the other half: a real difference is CAUGHT

    /// <summary>
    /// *** THE PROOF THAT THE 26 DIFFERENCES WENT AWAY BECAUSE THEY WERE NEVER REAL. *** A
    /// comparator that stopped reporting them by becoming more forgiving would pass the test above
    /// and be worse than the defect — that is precisely how the <c>MemoryLayout</c> hole survived a
    /// green <c>drift-check</c>. So the same block, same path, with ONE member retyped, must still
    /// come back DIFFERS and must point at the member.
    /// </summary>
    [Fact]
    public void RealInstanceDbExport_WithOneMemberRetyped_StillDiffers()
    {
        var export = ExportPath("iDB_MotorFwdRevSystem_Shredder");
        var db = DbSourceParser.Parse(XDocument.Load(export));

        var members = db.Members.ToList();
        var index = members.FindIndex(m => m.Datatype == "DInt");
        Assert.True(index >= 0, "fixture premise: the block declares at least one DInt Static member.");
        var retyped = members[index] with { Datatype = "Int" };
        members[index] = retyped;

        var mutated = WriteTemp("iDB-retyped", DbSourceWriter.Write(db with { Members = members }));
        var report = CompareRunner.Run(export, mutated);

        Assert.Equal(CompareStatus.Differs, report.Status);
        Assert.Contains(report.Differences, d =>
            d.Path.Contains("Section[@Name='Static']", StringComparison.Ordinal)
            && d.Path.EndsWith("/@Datatype", StringComparison.Ordinal));
    }

    /// <summary>
    /// A member ADDED to Static — the shape the cascade was drowning. One difference, naming Static.
    /// </summary>
    [Fact]
    public void RealInstanceDbExport_WithAnExtraMember_StillDiffers()
    {
        var export = ExportPath("iDB_PusherControl");
        var db = DbSourceParser.Parse(XDocument.Load(export));

        var members = db.Members.Append(new DbMember("SmuggledIn", "Bool", Retain: false, StartValue: null)).ToList();
        var mutated = WriteTemp("iDB-extra-member", DbSourceWriter.Write(db with { Members = members }));

        var report = CompareRunner.Run(export, mutated);

        Assert.Equal(CompareStatus.Differs, report.Status);
        Assert.Contains(report.Differences, d => d.Kind == DifferenceKind.ElementAdded
            && d.Path.Contains("Section[@Name='Static']", StringComparison.Ordinal));
    }

    // ------------------------------------------- section pairing: legible, but never forgiving

    /// <summary>
    /// Name-keyed section pairing reports a MISSING SECTION as one difference that names it — and
    /// still reports it. This is the pre-fix output shape, reproduced deliberately by deleting the
    /// section from a rewritten document: before the pairing change the same input produced 26
    /// differences, of which 25 were phantom.
    /// </summary>
    [Fact]
    public void ASectionPresentOnOneSideOnly_IsOneDifferenceThatNamesIt()
    {
        var export = ExportPath("iDB_MotorFwdRevSystem_Shredder");
        var written = DbSourceWriter.Write(DbSourceParser.Parse(XDocument.Load(export)));

        written.Descendants()
            .First(e => e.Name.LocalName == "Section" && (string?)e.Attribute("Name") == "InOut")
            .Remove();

        var report = CompareRunner.Run(export, WriteTemp("iDB-no-inout", written));

        Assert.Equal(CompareStatus.Differs, report.Status);
        var difference = Assert.Single(report.Differences);
        Assert.Equal(DifferenceKind.ElementMissing, difference.Kind);
        Assert.EndsWith("/Section[@Name='InOut']", difference.Path, StringComparison.Ordinal);
    }

    /// <summary>
    /// Pairing by name is blind to ORDER by construction, so order is asserted separately rather
    /// than assumed away. Without this, swapping two sections would report nothing from the walk and
    /// surface only as the Normalizer-disagreement NotCompared, whose message names the wrong cause.
    /// </summary>
    [Fact]
    public void SectionsReorderedButOtherwiseIdentical_AreReportedAsAnOrderDifference()
    {
        var export = ExportPath("iDB_MotorFwdRevSystem_Shredder");
        var written = DbSourceWriter.Write(DbSourceParser.Parse(XDocument.Load(export)));

        var sections = written.Descendants().First(e => e.Name.LocalName == "Sections");
        var input = sections.Elements().First(e => (string?)e.Attribute("Name") == "Input");
        var inOut = sections.Elements().First(e => (string?)e.Attribute("Name") == "InOut");
        input.Remove();
        inOut.AddAfterSelf(input);

        var report = CompareRunner.Run(export, WriteTemp("iDB-reordered", written));

        Assert.Equal(CompareStatus.Differs, report.Status);
        var difference = Assert.Single(report.Differences);
        Assert.Equal(DifferenceKind.SectionOrderDiffers, difference.Kind);
        Assert.Equal("Input, Output, InOut, Static", difference.First);
        Assert.Equal("Output, InOut, Input, Static", difference.Second);
    }

    /// <summary>
    /// The did-not-run case for the pairing itself: repeated or unnamed <c>Section</c> siblings are a
    /// shape the rule does not describe, and it must fall through to the positional walk rather than
    /// silently collapsing two sections into one key. A duplicate is still a difference.
    /// </summary>
    [Fact]
    public void DuplicateSectionNames_FallThroughToThePositionalWalk_AndStillDiffer()
    {
        var export = ExportPath("iDB_MotorFwdRevSystem_Shredder");
        var written = DbSourceWriter.Write(DbSourceParser.Parse(XDocument.Load(export)));

        var sections = written.Descendants().First(e => e.Name.LocalName == "Sections");
        var inOut = sections.Elements().First(e => (string?)e.Attribute("Name") == "InOut");
        inOut.AddAfterSelf(new XElement(inOut));

        var report = CompareRunner.Run(export, WriteTemp("iDB-duplicate-section", written));

        Assert.Equal(CompareStatus.Differs, report.Status);
        Assert.NotEmpty(report.Differences);
    }

    private static string WriteTemp(string name, XDocument document)
    {
        var dir = Path.Combine(Path.GetTempPath(), "instance-db-section-shape");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name + ".xml");
        document.Save(path);
        return path;
    }
}
