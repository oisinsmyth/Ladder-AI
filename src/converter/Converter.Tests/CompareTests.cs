using System.Text.Json;
using System.Xml.Linq;
using Converter.Compare;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// `converter compare` — the judgement half of THE CONFIRM LOOP
/// (docs/notes/test-environment-build-plan.md, owner 2026-08-12):
///
/// <code>
/// export ──► to-ir ──► to-xml ──► import ──► compile ──► export
///    └──────────────── compare THESE TWO ──────────────────┘
/// </code>
///
/// Strictly stronger than `drift-check` because the second document has been through TIA. The two
/// ways to build it wrong are both guarded here: a byte-compare fires on every reassigned UId
/// (<see cref="ShuffledUIds_CompareEqual"/>), and a comparison that ignores too much is how the
/// `MemoryLayout` hole survived a `drift-check` that said MATCH
/// (<see cref="DifferingMemoryLayout_IsReportedByPathAndValue"/> — the regression guard this
/// command exists for).
/// </summary>
public class CompareTests : IDisposable
{
    private readonly string _dir;

    public CompareTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"compare-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    private static XDocument Fixture(string name) => XDocument.Load(Path.Combine("Fixtures", name));

    private string Write(string name, XDocument document)
    {
        var path = Path.Combine(_dir, name);
        document.Save(path);
        return path;
    }

    private string WriteText(string name, string content)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static XElement MemoryLayoutElement(XDocument document) =>
        document.Descendants().First(e => e.Name.LocalName == "MemoryLayout");

    // ------------------------------------------------------------------- identical documents

    [Fact]
    public void IdenticalDocuments_AreEquivalent()
    {
        var first = Write("first.xml", Fixture("GlobalDbStandardMemoryLayout.xml"));
        var second = Write("second.xml", Fixture("GlobalDbStandardMemoryLayout.xml"));

        var report = CompareRunner.Run(first, second);

        Assert.Equal(CompareStatus.Equivalent, report.Status);
        Assert.Empty(report.Differences);
    }

    /// <summary>
    /// Both sides declare a layout, so the optional assertion inside the Normalizer is ACTIVE — which
    /// is the premise the whole confirm loop rests on (two TIA exports both declare one).
    /// </summary>
    [Fact]
    public void TwoRealExports_CompareTheirMemoryLayout()
    {
        var first = Write("first.xml", Fixture("GlobalDbStandardMemoryLayout.xml"));
        var second = Write("second.xml", Fixture("GlobalDbStandardMemoryLayout.xml"));

        var report = CompareRunner.Run(first, second);

        Assert.True(report.MemoryLayout.Compared);
        Assert.Equal("Standard", report.MemoryLayout.First);
        Assert.Equal("Standard", report.MemoryLayout.Second);
    }

    // ------------------------------------------------------- UId churn must NOT read as a change

    /// <summary>
    /// TIA reassigns Part/Wire/Access/CompileUnit/MultilingualText identifiers unprompted on every
    /// import/compile/export cycle — recorded repeatedly in this project. A byte-compare therefore
    /// fires constantly on semantically identical content, which is why the confirm loop needs the
    /// Normalizer rather than `fc` or `diff`.
    /// </summary>
    [Fact]
    public void ShuffledUIds_CompareEqual()
    {
        var original = Fixture("SanitizeSource.xml");
        var renumbered = Fixture("SanitizeSource.xml");
        Renumber(renumbered, offset: 1000);

        // Sanity: the renumbering really did change the file, or this test proves nothing.
        Assert.NotEqual(original.ToString(), renumbered.ToString());

        var report = CompareRunner.Run(
            Write("first.xml", original),
            Write("second.xml", renumbered),
            allowSilentLayout: true);

        Assert.Equal(CompareStatus.Equivalent, report.Status);
    }

    // ------------------------------------------------------------- THE regression guard

    /// <summary>
    /// The hole the confirm loop was designed to catch, in the form it actually took: the first
    /// export says <c>Standard</c>, the round trip states no opinion, TIA applies its S7-1200 default,
    /// and the second export says <c>Optimized</c>. `drift-check` reported MATCH, import did not
    /// error and compile did not error; the first symptom was a runtime Modbus status code.
    ///
    /// A verdict alone would not be enough — the loop's value is learning WHICH element TIA changed,
    /// so the path and both values are asserted, not just the count.
    /// </summary>
    [Fact]
    public void DifferingMemoryLayout_IsReportedByPathAndValue()
    {
        var standard = Fixture("GlobalDbStandardMemoryLayout.xml");
        var optimized = Fixture("GlobalDbStandardMemoryLayout.xml");
        MemoryLayoutElement(optimized).Value = "Optimized";

        var report = CompareRunner.Run(Write("first.xml", standard), Write("second.xml", optimized));

        Assert.Equal(CompareStatus.Differs, report.Status);
        var difference = Assert.Single(report.Differences);
        Assert.Equal(DifferenceKind.ValueDiffers, difference.Kind);
        Assert.Equal("/Document/SW.Blocks.GlobalDB/AttributeList/MemoryLayout", difference.Path);
        Assert.Equal("Standard", difference.First);
        Assert.Equal("Optimized", difference.Second);
    }

    /// <summary>
    /// A code block, not only a DB: `MemoryLayout` is a `PlcBlock` property and an FB's own access
    /// mode governs every instance DB made from it.
    /// </summary>
    [Fact]
    public void DifferingMemoryLayoutOnACodeBlock_IsReported()
    {
        var standard = Fixture("FcWithBareParameterMembers.xml");
        standard.Descendants().First(e => e.Name.LocalName == "AttributeList")
            .Element("Name")!.AddBeforeSelf(new XElement("MemoryLayout", "Standard"));

        var optimized = XDocument.Parse(standard.ToString());
        MemoryLayoutElement(optimized).Value = "Optimized";

        var report = CompareRunner.Run(Write("first.xml", standard), Write("second.xml", optimized));

        Assert.Equal(CompareStatus.Differs, report.Status);
        Assert.Contains(report.Differences, d => d.Path.EndsWith("/MemoryLayout", StringComparison.Ordinal)
                                                 && d.First == "Standard" && d.Second == "Optimized");
    }

    // ---------------------------------------------------------------- ordinary content changes

    [Fact]
    public void DifferingMemberType_IsReportedByPathAndValue()
    {
        var first = Fixture("GlobalDbStandardMemoryLayout.xml");
        var second = Fixture("GlobalDbStandardMemoryLayout.xml");
        second.Descendants().First(e => e.Name.LocalName == "Member")
            .SetAttributeValue("Datatype", "Array[0..67] of Word");

        var report = CompareRunner.Run(Write("first.xml", first), Write("second.xml", second));

        Assert.Equal(CompareStatus.Differs, report.Status);
        var difference = Assert.Single(report.Differences);
        Assert.Equal(DifferenceKind.AttributeDiffers, difference.Kind);
        Assert.EndsWith("/Member/@Datatype", difference.Path, StringComparison.Ordinal);
        Assert.Equal("Array[0..67] of Byte", difference.First);
        Assert.Equal("Array[0..67] of Word", difference.Second);
    }

    [Fact]
    public void AMemberPresentOnlyInTheSecondDocument_IsReportedAsAdded()
    {
        var first = Fixture("GlobalDbStandardMemoryLayout.xml");
        var second = Fixture("GlobalDbStandardMemoryLayout.xml");
        var member = second.Descendants().First(e => e.Name.LocalName == "Member");
        member.AddAfterSelf(new XElement(member.Name, new XAttribute("Name", "Extra"), new XAttribute("Datatype", "Bool")));

        var report = CompareRunner.Run(Write("first.xml", first), Write("second.xml", second));

        Assert.Equal(CompareStatus.Differs, report.Status);
        var difference = Assert.Single(report.Differences);
        Assert.Equal(DifferenceKind.ElementAdded, difference.Kind);
        Assert.EndsWith("/Member[2]", difference.Path, StringComparison.Ordinal);
        Assert.Contains("Extra", difference.Second);
    }

    /// <summary>
    /// A block whose name changed is a difference, not a silent pass — the realistic orchestration
    /// slip of exporting a different block the second time round.
    /// </summary>
    [Fact]
    public void DifferingBlockName_IsReported()
    {
        var first = Fixture("GlobalDbStandardMemoryLayout.xml");
        var second = Fixture("GlobalDbStandardMemoryLayout.xml");
        second.Descendants().First(e => e.Name.LocalName == "Name").Value = "DB_SomethingElse";

        var report = CompareRunner.Run(Write("first.xml", first), Write("second.xml", second));

        Assert.Equal(CompareStatus.Differs, report.Status);
        Assert.Contains(report.Differences, d => d.Path.EndsWith("/Name", StringComparison.Ordinal));
    }

    // ---------------------------------------------- could not compare — and it is NEVER exit 0

    [Fact]
    public void MissingFirstFile_IsNotCompared()
    {
        var second = Write("second.xml", Fixture("GlobalDbStandardMemoryLayout.xml"));

        var report = CompareRunner.Run(Path.Combine(_dir, "does-not-exist.xml"), second);

        Assert.Equal(CompareStatus.NotCompared, report.Status);
        Assert.Contains("file not found", report.Detail);
    }

    [Fact]
    public void MissingSecondFile_IsNotCompared()
    {
        var first = Write("first.xml", Fixture("GlobalDbStandardMemoryLayout.xml"));

        var report = CompareRunner.Run(first, Path.Combine(_dir, "does-not-exist.xml"));

        Assert.Equal(CompareStatus.NotCompared, report.Status);
    }

    [Fact]
    public void UnparseableFile_IsNotCompared()
    {
        var first = Write("first.xml", Fixture("GlobalDbStandardMemoryLayout.xml"));
        var second = WriteText("second.xml", "<Document><SW.Blocks.GlobalDB>truncated");

        var report = CompareRunner.Run(first, second);

        Assert.Equal(CompareStatus.NotCompared, report.Status);
        Assert.Contains("not parseable XML", report.Detail);
    }

    /// <summary>
    /// FI-44, "empty is not clean" in its purest form: two well-formed XML files carrying no
    /// SimaticML object at all would walk to zero differences and report a pass — a comparison of
    /// nothing, wearing the face of a comparison that succeeded.
    /// </summary>
    [Fact]
    public void XmlThatIsNotABlockExport_IsNotCompared()
    {
        var first = WriteText("first.xml", "<Document><Engineering version=\"V20\" /></Document>");
        var second = WriteText("second.xml", "<Document><Engineering version=\"V20\" /></Document>");

        var report = CompareRunner.Run(first, second);

        Assert.Equal(CompareStatus.NotCompared, report.Status);
        Assert.Contains("no SimaticML object", report.Detail);
    }

    [Fact]
    public void ComparingAFileWithItself_IsNotCompared()
    {
        var path = Write("first.xml", Fixture("GlobalDbStandardMemoryLayout.xml"));

        var report = CompareRunner.Run(path, path);

        Assert.Equal(CompareStatus.NotCompared, report.Status);
        Assert.Contains("same file", report.Detail);
    }

    // ------------------------------------------- the optional-assertion premise, enforced

    /// <summary>
    /// The Normalizer holds neither side to the other's layout when one is silent — deliberately, so
    /// the committed corpus (every `.ir` of which predates the emit side) does not turn red for a
    /// benign reason. In the confirm loop that weakness cannot legitimately arise, because both
    /// inputs are TIA exports and a TIA export always states a layout. So a silent side means an
    /// input is not what the loop assumes, and the comparison is quietly weaker than it looks —
    /// which is the very failure class being hunted. It fails closed rather than warning.
    /// </summary>
    [Fact]
    public void OneSideSilentOnMemoryLayout_IsNotComparedByDefault()
    {
        var declared = Fixture("GlobalDbStandardMemoryLayout.xml");
        var silent = Fixture("GlobalDbStandardMemoryLayout.xml");
        MemoryLayoutElement(silent).Remove();

        var report = CompareRunner.Run(Write("first.xml", declared), Write("second.xml", silent));

        Assert.Equal(CompareStatus.NotCompared, report.Status);
        Assert.Contains("MemoryLayout", report.Detail);
        Assert.Equal("Standard", report.MemoryLayout.First);
        Assert.Null(report.MemoryLayout.Second);
    }

    [Fact]
    public void OneSideSilentOnMemoryLayout_IsComparedWhenExplicitlyAllowed()
    {
        var declared = Fixture("GlobalDbStandardMemoryLayout.xml");
        var silent = Fixture("GlobalDbStandardMemoryLayout.xml");
        MemoryLayoutElement(silent).Remove();

        var report = CompareRunner.Run(
            Write("first.xml", declared), Write("second.xml", silent), allowSilentLayout: true);

        Assert.Equal(CompareStatus.Equivalent, report.Status);
        Assert.False(report.MemoryLayout.Compared);
    }

    /// <summary>
    /// Neither side declaring one is the pre-2026-08-12 converter-output shape and is not a premise
    /// failure — there is no assertion to make. It must not be refused.
    /// </summary>
    [Fact]
    public void NeitherSideDeclaringMemoryLayout_IsStillCompared()
    {
        var first = Fixture("GlobalDbStandardMemoryLayout.xml");
        var second = Fixture("GlobalDbStandardMemoryLayout.xml");
        MemoryLayoutElement(first).Remove();
        MemoryLayoutElement(second).Remove();

        var report = CompareRunner.Run(Write("first.xml", first), Write("second.xml", second));

        Assert.Equal(CompareStatus.Equivalent, report.Status);
        Assert.False(report.MemoryLayout.Compared);
    }

    // ------------------------------------------------------------------------- CLI surface

    [Fact]
    public void Cli_ExitsZeroOnEquivalent_OneOnDiffers_TwoOnNotCompared()
    {
        var first = Write("first.xml", Fixture("GlobalDbStandardMemoryLayout.xml"));
        var same = Write("same.xml", Fixture("GlobalDbStandardMemoryLayout.xml"));

        var changed = Fixture("GlobalDbStandardMemoryLayout.xml");
        MemoryLayoutElement(changed).Value = "Optimized";
        var different = Write("different.xml", changed);

        Assert.Equal(0, Program.RunCompare(new[] { first, same }));
        Assert.Equal(1, Program.RunCompare(new[] { first, different }));
        Assert.Equal(2, Program.RunCompare(new[] { first, Path.Combine(_dir, "nope.xml") }));
        Assert.Equal(2, Program.RunCompare(new[] { first }));
        Assert.Equal(2, Program.RunCompare(new[] { first, same, different }));
        Assert.Equal(2, Program.RunCompare(new[] { first, same, "--not-a-flag" }));
    }

    [Fact]
    public void Json_CarriesEveryDifferenceAndTheLayoutObservation()
    {
        var first = Fixture("GlobalDbStandardMemoryLayout.xml");
        var second = Fixture("GlobalDbStandardMemoryLayout.xml");
        MemoryLayoutElement(second).Value = "Optimized";

        var report = CompareRunner.Run(Write("first.xml", first), Write("second.xml", second));
        using var document = JsonDocument.Parse(CompareOutputFormatter.FormatJson(report));
        var root = document.RootElement;

        Assert.Equal("Differs", root.GetProperty("status").GetString());
        Assert.False(root.GetProperty("equivalent").GetBoolean());
        Assert.Equal(1, root.GetProperty("differenceCount").GetInt32());
        Assert.True(root.GetProperty("memoryLayout").GetProperty("compared").GetBoolean());
        Assert.Equal("Optimized", root.GetProperty("memoryLayout").GetProperty("second").GetString());

        var difference = root.GetProperty("differences")[0];
        Assert.Contains("MemoryLayout", difference.GetProperty("path").GetString());
        Assert.Equal("Standard", difference.GetProperty("first").GetString());
    }

    [Fact]
    public void Text_NamesThePathAndBothValues_NotJustAVerdict()
    {
        var first = Fixture("GlobalDbStandardMemoryLayout.xml");
        var second = Fixture("GlobalDbStandardMemoryLayout.xml");
        MemoryLayoutElement(second).Value = "Optimized";

        var report = CompareRunner.Run(Write("first.xml", first), Write("second.xml", second));
        var text = CompareOutputFormatter.FormatText(report, maxDifferences: 50);

        Assert.Contains("VERDICT: DIFFERS", text);
        Assert.Contains("/Document/SW.Blocks.GlobalDB/AttributeList/MemoryLayout", text);
        Assert.Contains("first : Standard", text);
        Assert.Contains("second: Optimized", text);
    }

    [Fact]
    public void Text_ForANotComparedReport_NeverReadsAsAPass()
    {
        var report = CompareRunner.Run(Path.Combine(_dir, "a.xml"), Path.Combine(_dir, "b.xml"));

        var text = CompareOutputFormatter.FormatText(report, maxDifferences: 50);

        Assert.Contains("NOT COMPARED", text);
        Assert.DoesNotContain("EQUIVALENT", text);
    }

    [Fact]
    public void Text_CapsTheDifferenceListAndSaysHowManyItHeldBack()
    {
        var first = Fixture("GlobalDbStandardMemoryLayout.xml");
        var second = Fixture("GlobalDbStandardMemoryLayout.xml");
        var member = second.Descendants().First(e => e.Name.LocalName == "Member");
        for (var i = 0; i < 5; i++)
        {
            member.AddAfterSelf(new XElement(member.Name, new XAttribute("Name", $"Extra{i}"), new XAttribute("Datatype", "Bool")));
        }

        var report = CompareRunner.Run(Write("first.xml", first), Write("second.xml", second));
        Assert.Equal(5, report.Differences.Count);

        Assert.Contains("2 further difference(s) not shown", CompareOutputFormatter.FormatText(report, maxDifferences: 3));
        Assert.DoesNotContain("not shown", CompareOutputFormatter.FormatText(report, maxDifferences: 0));
    }

    // ------------------------------------------------------------------------------- helpers

    /// <summary>
    /// Reproduces what TIA does on an import/compile/export cycle: every volatile identifier gets a
    /// different number, with referential integrity intact (a uniform offset keeps every
    /// IdentCon/NameCon pointing where it pointed). The block's own ID is deliberately left alone —
    /// that one is not in the volatile set.
    /// </summary>
    private static void Renumber(XDocument document, int offset)
    {
        foreach (var element in document.Descendants())
        {
            if ((string?)element.Attribute("UId") is string uid && int.TryParse(uid, out var uidValue))
            {
                element.SetAttributeValue("UId", (uidValue + offset).ToString());
            }

            if (element.Name.LocalName is "MultilingualText" or "MultilingualTextItem" or "SW.Blocks.CompileUnit"
                && (string?)element.Attribute("ID") is string id && int.TryParse(id, out var idValue))
            {
                element.SetAttributeValue("ID", (idValue + offset).ToString());
            }
        }
    }
}
