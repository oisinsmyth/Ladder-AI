using Converter.Ir;
using Converter.Preflight;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// The denominator `converter preflight` never printed (2026-08-23).
///
/// <para>Reproduced on the real corpus, same file, same instant, two scopes:
/// <c>--project ir/test-project001</c> (43 .ir files) reported 2 findings and
/// <c>--project ir/reference</c> (15) reported 5, and the SUMMARY line was identical in shape —
/// <c>1 file(s), N finding(s)</c>, where <c>1 file(s)</c> is the BATCH SIZE, the numerator's
/// subject rather than its index. Neither 43 nor 15 appeared anywhere. The three extra findings
/// were worded "does not resolve to any block in the project or batch" and were indistinguishable
/// from genuine unresolved calls.</para>
///
/// <para>The inverse of this repository's EMPTY IS NOT CLEAN principle: <b>narrow is not dirty</b>.
/// Shaped after <see cref="ReuseScanDenominatorTests"/>, which closed the identical defect in
/// <c>reuse-scan</c> — every test asserts the STRUCTURED field on the report AND the rendered
/// substring, so a formatter that drops the line fails even when the count is right.</para>
/// </summary>
public class PreflightDenominatorTests : IDisposable
{
    private readonly List<string> _dirs = new();

    public void Dispose()
    {
        foreach (var dir in _dirs)
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private string NewDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"preflight-denom-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _dirs.Add(dir);
        return dir;
    }

    private static void Write(string dir, string fileName, string content) =>
        File.WriteAllText(Path.Combine(dir, fileName), content);

    private static DbSource Db(string name, int number, string? instanceOf = null) => new(
        "0", name, number, InstanceOfName: instanceOf, Comment: "Seed.",
        Members: new[] { new DbMember("Run", "Bool", Retain: false, StartValue: null) });

    /// <summary>Four project files: two DBs, one FB, one tag table.</summary>
    private string WideProject()
    {
        var dir = NewDir();
        Write(dir, "DB_Marks.ir", DbIrSerializer.Serialize(Db("DB_Marks", 20)));
        Write(dir, "DB_Settings.ir", DbIrSerializer.Serialize(Db("DB_Settings", 22)));
        Write(dir, "FB_Callee.ir", IrSerializer.SerializeBlockReadable(new IrBlock(
            "0", "FB", "FB_Callee", 5, "LAD", "Callee.", new[]
            {
                new IrNetwork(1, "Body", new[] { new CoilAssignment("Done", new Expr.TagRef("Go")) }),
            },
            TempMembers: new[]
            {
                new DbMember("Done", "Bool", Retain: false, StartValue: null),
                new DbMember("Go", "Bool", Retain: false, StartValue: null),
            })));
        Write(dir, "Tags.ir", TagTableIrSerializer.Serialize(new PlcTagTableSource("0", "Tags", new[]
        {
            new PlcTagSource("1", "Start_PB", "Bool", "%I0.0", true, true, true, null),
        })));
        return dir;
    }

    /// <summary>The same corpus MINUS the FB — two files, and no block of any name.</summary>
    private string NarrowProject()
    {
        var dir = NewDir();
        Write(dir, "DB_Marks.ir", DbIrSerializer.Serialize(Db("DB_Marks", 20)));
        Write(dir, "Tags.ir", TagTableIrSerializer.Serialize(new PlcTagTableSource("0", "Tags", new[]
        {
            new PlcTagSource("1", "Start_PB", "Bool", "%I0.0", true, true, true, null),
        })));
        return dir;
    }

    /// <summary>An instance DB whose INSTANCEOF target lives in the WIDE project only.</summary>
    private string BatchInstanceDb()
    {
        var dir = NewDir();
        var path = Path.Combine(dir, "iDB_Callee.ir");
        File.WriteAllText(path, DbIrSerializer.Serialize(new DbSource(
            "0", "iDB_Callee", 21, InstanceOfName: "FB_Callee", Comment: "Instance.",
            Members: Array.Empty<DbMember>())));
        return path;
    }

    // *** THE REPRODUCTION. *** One file, two --project scopes. The verdict flips and the wording of
    // the finding claims the project was consulted; before this the two runs' summaries were
    // indistinguishable, so an exit code could not tell "resolved against a wide corpus" from
    // "resolved against a narrow one".
    [Fact]
    public void SameFile_TwoScopes_DifferentDenominators_AndTheFindingFlips()
    {
        var target = BatchInstanceDb();

        var wide = PreflightRunner.Run(new[] { target }, WideProject());
        var narrow = PreflightRunner.Run(new[] { target }, NarrowProject());

        // The finding that flips — and is worded as a fact about "the project".
        Assert.DoesNotContain(wide.Files[0].Findings, f => f.Check == "instanceof");
        Assert.Contains(narrow.Files[0].Findings, f => f.Check == "instanceof");

        // The corpus the two verdicts were reached against, structurally…
        Assert.Equal(4, wide.Corpus.ProjectFileCount);
        Assert.Equal(2, narrow.Corpus.ProjectFileCount);
        Assert.Equal(1, wide.Corpus.BlockNameCount);
        Assert.Equal(0, narrow.Corpus.BlockNameCount);

        // …and rendered, which is the half a reader actually sees.
        var wideText = PreflightOutputFormatter.FormatText(wide);
        var narrowText = PreflightOutputFormatter.FormatText(narrow);
        Assert.Contains("CORPUS: 4 project file(s)", wideText);
        Assert.Contains("CORPUS: 2 project file(s)", narrowText);
        Assert.Contains("1 block name(s)", wideText);
        Assert.Contains("0 block name(s)", narrowText);
        Assert.NotEqual(
            wideText[wideText.IndexOf("CORPUS:", StringComparison.Ordinal)..],
            narrowText[narrowText.IndexOf("CORPUS:", StringComparison.Ordinal)..]);
    }

    // The number must match the finding it sits under. "does not resolve to any BLOCK" is falsifiable
    // against the block-name count and nothing else — a file count under it is a category slip, and
    // these two quantities are not the same number.
    [Fact]
    public void TheBlockNameCount_IsNotTheFileCount()
    {
        var report = PreflightRunner.Run(new[] { BatchInstanceDb() }, WideProject());

        Assert.Equal(5, report.Corpus.ProjectFileCount + report.Corpus.BatchFileCount);
        Assert.Equal(1, report.Corpus.BlockNameCount);                       // FB_Callee, and nothing else
        Assert.Equal(4, report.Corpus.TagRootNameCount);   // DB_Marks, DB_Settings, iDB_Callee (batch), Start_PB
    }

    // Each of the FOUR corpus walks reports its own denominator, because each decides a different
    // finding class and no single number honestly covers them all.
    [Fact]
    public void EveryCorpusWalk_StatesItsOwnDenominator()
    {
        var report = PreflightRunner.Run(new[] { BatchInstanceDb() }, WideProject());
        var text = PreflightOutputFormatter.FormatText(report);

        Assert.Contains("RESOLVED AGAINST:", text);
        Assert.Contains("block name(s) [call, instanceof]", text);
        Assert.Contains("tag/DB root name(s) [tag root]", text);
        Assert.Contains("DB/UDT body(ies) [member path, literal-fit]", text);
        Assert.Contains("callee interface(s) [convert: wired CALL]", text);
        Assert.Contains("file(s) classified [review:harness-scope]", text);
    }

    // The paths are printed beside the counts, on drift-check's precedent (COMPARED: … (project=…)),
    // because the whole defect is that the answer depends on --project.
    [Fact]
    public void TheProjectDir_IsPrintedBesideTheCount()
    {
        var project = WideProject();
        var report = PreflightRunner.Run(new[] { BatchInstanceDb() }, project);

        Assert.Equal(project, report.Corpus.ProjectDir);
        Assert.Contains($"(project={project})", PreflightOutputFormatter.FormatText(report));
    }

    // EMPTY IS NOT CLEAN, at the other end of the same line. An empty --project must be
    // distinguishable from a populated one that resolved everything.
    [Fact]
    public void EmptyProject_IsDistinguishableFromAPopulatedOneThatResolvedEverything()
    {
        var target = BatchInstanceDb();

        var empty = PreflightRunner.Run(new[] { target }, NewDir());
        var populated = PreflightRunner.Run(new[] { target }, WideProject());

        Assert.True(empty.Corpus.ProjectContributedNothing);
        Assert.False(populated.Corpus.ProjectContributedNothing);

        var emptyText = PreflightOutputFormatter.FormatText(empty);
        Assert.Contains("CORPUS: 0 project file(s)", emptyText);
        Assert.Contains("PROJECT CORPUS EMPTY:", emptyText);
        Assert.DoesNotContain("PROJECT CORPUS EMPTY:", PreflightOutputFormatter.FormatText(populated));
    }

    // Printed at zero, for the reason the harness-scope clause beside it already is: an absent line
    // cannot distinguish "the corpus held none" from "the walk never ran".
    [Fact]
    public void TheZeroIsPrinted()
    {
        var text = PreflightOutputFormatter.FormatText(PreflightRunner.Run(new[] { BatchInstanceDb() }, NewDir()));

        Assert.Contains("CORPUS: 0 project file(s)", text);
        Assert.Contains("0 block name(s) [call, instanceof]", text);
        Assert.Contains("0 callee interface(s) [convert: wired CALL]", text);
    }

    // The JSON payload carried NO summary object at all — a consumer counted array elements and
    // could reach no denominator. This is its first summary field.
    [Fact]
    public void Json_CarriesTheDenominator()
    {
        var report = PreflightRunner.Run(new[] { BatchInstanceDb() }, WideProject());
        var json = PreflightOutputFormatter.FormatJson(report);

        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var corpus = doc.RootElement.GetProperty("corpus");
        Assert.Equal(4, corpus.GetProperty("projectFileCount").GetInt32());
        Assert.Equal(1, corpus.GetProperty("batchFileCount").GetInt32());
        Assert.Equal(1, corpus.GetProperty("blockNameCount").GetInt32());
        Assert.Equal(4, corpus.GetProperty("tagRootNameCount").GetInt32());
    }

    // The existing SUMMARY line is untouched: "1 file(s)" there is the BATCH, and bolting "of 43"
    // onto it would assert that 1 of 43 project files was pre-flighted — the exact category slip
    // this change exists to remove. Scripts and skills grep that line.
    [Fact]
    public void TheExistingSummaryLine_IsUnchanged()
    {
        var text = PreflightOutputFormatter.FormatText(PreflightRunner.Run(new[] { BatchInstanceDb() }, WideProject()));

        Assert.Contains("SUMMARY: 1 file(s), 0 finding(s), 0 reported but not gating (harness-scope)", text);
        Assert.Contains("PRE-FLIGHT ONLY:", text);
    }
}
