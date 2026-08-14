using Converter.CrossCheck;
using Converter.Ir;
using Converter.SimaticMl;
using Converter.ReuseScan;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// Tooling-hammer campaign, 2026-08-14. Two "facts not verdicts" tools that stated a fact narrower
/// than the one a reader takes from it.
///
/// <para>
/// <b><c>reuse-scan</c> printed no denominator at all.</b> A <c>--tag</c> naming nothing in the corpus
/// gave <c>SUMMARY: 0 block(s) matched</c> and <b>exit 0</b> — and exit 0 here LICENSES "nothing to
/// reuse, write a new block". A wrong <c>--project</c> read exactly like a thorough scan. Note the
/// asymmetry the tool already carried: <c>--kind</c> IS validated against a known set and refuses an
/// unknown value by name, while <c>--tag</c> was validated against nothing.
/// </para>
///
/// <para>
/// <b><c>cross-check</c> counted writes from blocks that never execute.</b> A path written by two
/// blocks, one of which no OB can reach, reported as a C-308 multi-writer with exactly ONE runtime
/// writer — and the call graph that settles it was already in the same report, under SIBLING
/// REFERENCES, unused. Reported, never subtracted: wiring the block up restores the contention, and a
/// silently dropped writer would hide it.
/// </para>
/// </summary>
public class ReuseScanDenominatorTests : IDisposable
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
        var dir = Path.Combine(Path.GetTempPath(), $"reuse-hammer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _dirs.Add(dir);
        return dir;
    }

    // A project holding one DB and one FC that reads it.
    private string SeededProject()
    {
        var dir = NewDir();
        File.WriteAllText(Path.Combine(dir, "DB_Plant.ir"), DbIrSerializer.Serialize(new DbSource(
            "0", "DB_Plant", 20, InstanceOfName: null, Comment: "Plant.", Members: new[]
            {
                new DbMember("Run", "Bool", Retain: false, StartValue: null),
                new DbMember("Ready", "Bool", Retain: false, StartValue: null),
            })));
        File.WriteAllText(Path.Combine(dir, "FC_Uses.ir"),
            "BLOCK FC FC_Uses\nROOTID 0\nNUMBER 41\nLANGUAGE LAD\nTITLE \"t\"\nCOMMENT \"c\"\n\n"
            + "INTERFACE\n  INPUT\n  OUTPUT\n\nNETWORK 1 \"n\"\n  COMMENT \"c\"\n"
            + "  COIL DB_Plant.Run := DB_Plant.Ready\n");
        return dir;
    }

    // --- reuse-scan ----------------------------------------------------------------------------

    [Fact]
    public void TagRootAbsentFromTheCorpus_IsNotAnAnswer()
    {
        var report = ReuseScanRunner.Run(SeededProject(), new[] { "DB_NoSuchThing.Member" }, Array.Empty<string>());

        Assert.Empty(report.Matches);
        Assert.True(report.AskedAboutNothing);
        Assert.Contains("DB_NoSuchThing", report.Absent);
    }

    // The unaffected case: a root that IS in the corpus but matches no block satisfying the whole
    // query is a real answer of "no reuse candidate", and must stay one.
    [Fact]
    public void TagRootPresentButNoBlockMatches_IsAnAnswer()
    {
        var report = ReuseScanRunner.Run(
            SeededProject(), new[] { "DB_Plant.Run" }, new[] { "timer" });   // no block has a timer

        Assert.Empty(report.Matches);
        Assert.False(report.AskedAboutNothing);
        Assert.Empty(report.Absent);
    }

    // A Design-stage query legitimately mixes existing tags with proposed ones — gen-architecture
    // designs AGAINST gaps. Refusing that would be a gate firing outside its scope, which is noise.
    [Fact]
    public void PartlyAbsentQuery_StillAnswers_ButNamesTheAbsentRoot()
    {
        var report = ReuseScanRunner.Run(
            SeededProject(), new[] { "DB_Plant.Run", "DB_Proposed.Later" }, Array.Empty<string>());

        Assert.False(report.AskedAboutNothing);
        Assert.Contains("DB_Proposed", report.Absent);
        Assert.NotEmpty(report.Matches);
    }

    [Fact]
    public void EmptyProject_AsksAboutNothing_AndSaysSo()
    {
        var report = ReuseScanRunner.Run(NewDir(), new[] { "DB_Plant.Run" }, Array.Empty<string>());

        Assert.Equal(0, report.FilesScanned);
        Assert.True(report.AskedAboutNothing);
    }

    [Fact]
    public void Report_StatesItsDenominator()
    {
        var report = ReuseScanRunner.Run(SeededProject(), new[] { "DB_Plant.Run" }, Array.Empty<string>());

        Assert.Equal(2, report.FilesScanned);
        Assert.Contains("of 2 file(s) scanned", ReuseScanOutputFormatter.FormatText(report));
    }

    // --- cross-check reachability --------------------------------------------------------------

    private string ProjectWithTwoWriters(bool callBoth)
    {
        var dir = NewDir();
        File.WriteAllText(Path.Combine(dir, "DB_Shared.ir"), DbIrSerializer.Serialize(new DbSource(
            "0", "DB_Shared", 20, InstanceOfName: null, Comment: "Shared.", Members: new[]
            {
                new DbMember("Target", "Bool", Retain: false, StartValue: null),
                new DbMember("Source", "Bool", Retain: false, StartValue: null),
            })));

        var number = 40;
        foreach (var name in new[] { "FC_A", "FC_B" })
        {
            number++;
            File.WriteAllText(Path.Combine(dir, $"{name}.ir"),
                $"BLOCK FC {name}\nROOTID 0\nNUMBER {number}\nLANGUAGE LAD\nTITLE \"t\"\nCOMMENT \"c\"\n\n"
                + "INTERFACE\n  INPUT\n  OUTPUT\n\nNETWORK 1 \"n\"\n  COMMENT \"c\"\n"
                + "  COIL DB_Shared.Target := DB_Shared.Source\n");
        }

        var calls = "  CALL FC_A(EN := TRUE)\n" + (callBoth ? "  CALL FC_B(EN := TRUE)\n" : string.Empty);
        File.WriteAllText(Path.Combine(dir, "Main.ir"),
            "BLOCK OB Main\nROOTID 0\nNUMBER 1\nLANGUAGE LAD\nTITLE \"t\"\nCOMMENT \"c\"\n\n"
            + "INTERFACE\n  INPUT\n  OUTPUT\n\nNETWORK 1 \"calls\"\n  COMMENT \"c\"\n" + calls);
        return dir;
    }

    [Fact]
    public void MultiWriter_WhereOneWriterIsUnreachable_SaysSo_AndDoesNotDropIt()
    {
        var report = CrossCheckRunner.Run(ProjectWithTwoWriters(callBoth: false));

        var fact = Assert.Single(report.MultiWriters, m => m.Path.EndsWith("Target", StringComparison.Ordinal));
        Assert.Equal(2, fact.Writers.Select(w => w.Block).Distinct().Count());   // NOT subtracted
        Assert.Contains("FC_B", fact.UnreachableWriterBlocks);
        Assert.True(fact.UnreachableKnown);
        Assert.Equal(1, fact.ReachableWriterCount);
        Assert.Contains("NOT REACHABLE from any OB", CrossCheckOutputFormatter.FormatText(report));
    }

    // The unaffected case: both writers execute, so nothing is annotated and the finding stands whole.
    [Fact]
    public void MultiWriter_WhereBothWritersExecute_IsNotAnnotated()
    {
        var report = CrossCheckRunner.Run(ProjectWithTwoWriters(callBoth: true));

        var fact = Assert.Single(report.MultiWriters, m => m.Path.EndsWith("Target", StringComparison.Ordinal));
        Assert.Empty(fact.UnreachableWriterBlocks);
        Assert.Equal(2, fact.ReachableWriterCount);
        Assert.DoesNotContain("NOT REACHABLE", CrossCheckOutputFormatter.FormatText(report));
    }

    // A corpus with NO OB cannot answer the reachability question at all, and "unknown" must not
    // render as "everything executes" — nor as "nothing does. An absent root set cannot tell them
    // apart, so the flag says the question was not askable.
    [Fact]
    public void CorpusWithNoOb_ReportsUnknown_NotUnreachable()
    {
        var dir = ProjectWithTwoWriters(callBoth: true);
        File.Delete(Path.Combine(dir, "Main.ir"));

        var report = CrossCheckRunner.Run(dir);

        var fact = Assert.Single(report.MultiWriters, m => m.Path.EndsWith("Target", StringComparison.Ordinal));
        Assert.False(fact.UnreachableKnown);
        Assert.Empty(fact.UnreachableWriterBlocks);
        Assert.Equal(2, fact.ReachableWriterCount);   // falls back to "all of them", labelled unknown
    }
}
