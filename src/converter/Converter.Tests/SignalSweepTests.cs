using Converter.Ir;
using Converter.SignalSweep;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// FI-39 check 5 (2026-08-05): `converter signal-sweep` — the project-level residual sweep, computed.
///
/// Its value is EXACTNESS, not a catch (the design study grades it Medium, no oracle): it replaces an
/// artifact's own self-reported approximations ("swept: ~190", "out-of-scope: ~80") with computed
/// integers and an exact residue. An approximate denominator cannot support a completeness claim.
/// </summary>
public class SignalSweepTests : IDisposable
{
    private readonly string _dir;
    private readonly string _project;
    private readonly string _specs;

    public SignalSweepTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"sweep-{Guid.NewGuid():N}");
        _project = Path.Combine(_dir, "ir");
        _specs = Path.Combine(_dir, "equipment-specs");
        Directory.CreateDirectory(_project);
        Directory.CreateDirectory(_specs);

        File.WriteAllText(Path.Combine(_project, "DB_In.ir"), DbIrSerializer.Serialize(
            new DbSource("0", "DB_In", 1, InstanceOfName: null, Comment: null, Members: new[]
            {
                new DbMember("Bound", "Bool", Retain: false, StartValue: null),
                new DbMember("Disposed", "Bool", Retain: false, StartValue: null),
                new DbMember("Nobody", "Bool", Retain: false, StartValue: null),
            })));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private void WriteSpec(string body) => File.WriteAllText(Path.Combine(_specs, "InstA.md"), body);

    // A tag table under whatever name the export gives it. Tag names are BARE — a PLC tag never
    // contains a dot, which is the whole of FI-45 item 2.
    private void WriteTagTable(string tableName, params string[] tagNames)
    {
        var tags = tagNames.Select((t, i) =>
            new PlcTagSource($"9{i}", t, "Bool", $"%I0.{i}", true, true, true, null)).ToList();

        File.WriteAllText(Path.Combine(_project, $"TT_{tagNames[0]}.ir"),
            TagTableIrSerializer.Serialize(new PlcTagTableSource("9", tableName, tags)));
    }

    private string WriteUnclaimed(string body)
    {
        var path = Path.Combine(_dir, "unclaimed-signals.md");
        File.WriteAllText(path, body);
        return path;
    }

    [Fact]
    public void ClassifiesClaimed_Disposed_AndUnaccounted()
    {
        WriteSpec("- **C1** bound to `DB_In.Bound` [io]\n");
        var unclaimed = WriteUnclaimed(
            "# residual\n\n## Full disposition table\n\n### `DB_In` (DB 1)\n\n| Members | Disposition |\n|---|---|\n" +
            "| `Disposed` | out-of-scope-instance |\n");

        var report = SignalSweepRunner.Run(_project, _specs, registerPath: null, unclaimedPath: unclaimed);

        Assert.Equal(3, report.Swept);
        Assert.Equal(1, report.Claimed);
        Assert.Equal(1, report.Disposed);
        Assert.Equal("DB_In.Nobody", Assert.Single(report.Unaccounted).Path);
        Assert.True(report.HasFindings);
    }

    // The qualification problem: the disposition tables list BARE leaf names under a per-DB heading, so
    // without qualifying by the enclosing heading every leaf silently fails to match the inventory.
    [Fact]
    public void BareLeafNames_AreQualifiedByTheirDbHeading()
    {
        WriteSpec("- **C1** nothing here\n");
        var unclaimed = WriteUnclaimed(
            "## Full disposition table\n\n### `DB_In` (DB 1)\n\n| Members | Disposition |\n|---|---|\n" +
            "| `Bound`, `Disposed`, `Nobody` | out-of-scope-instance |\n");

        var report = SignalSweepRunner.Run(_project, _specs, registerPath: null, unclaimedPath: unclaimed);

        Assert.Equal(3, report.Disposed);
        Assert.Empty(report.Unaccounted);
        Assert.False(report.HasFindings);
    }

    // An unclaimed artifact whose tables cannot be read must not silently make every signal "unaccounted".
    [Fact]
    public void UnclaimedArtifactWithNoDispositionTables_IsAHardError()
    {
        WriteSpec("- **C1** `DB_In.Bound`\n");
        var unclaimed = WriteUnclaimed("# residual\n\nAll prose, no tables.\n");

        var ex = Assert.Throws<SignalSweepFormatException>(
            () => SignalSweepRunner.Run(_project, _specs, registerPath: null, unclaimedPath: unclaimed));
        Assert.Contains("Refusing to report signals as unaccounted", ex.Message);
    }

    [Fact]
    public void EmptyCorpus_IsAHardError_NotFullCoverage()
    {
        var emptyProject = Path.Combine(_dir, "empty-ir");
        Directory.CreateDirectory(emptyProject);
        WriteSpec("- **C1** x\n");

        var ex = Assert.Throws<SignalSweepFormatException>(
            () => SignalSweepRunner.Run(emptyProject, _specs, registerPath: null, unclaimedPath: null));
        Assert.Contains("Refusing to report full coverage of an empty denominator", ex.Message);
    }

    [Fact]
    public void WithoutUnclaimedArtifact_DisposedIsZeroByConstruction_AndSaysSo()
    {
        WriteSpec("- **C1** `DB_In.Bound`\n");

        var report = SignalSweepRunner.Run(_project, _specs, registerPath: null, unclaimedPath: null);

        Assert.False(report.DispositionTableRead);
        Assert.Equal(0, report.Disposed);
        Assert.Contains("disposed' is 0 by construction", SignalSweepOutputFormatter.FormatText(report));
    }

    // FI-45 item 2, the defect itself: dispositions are qualified `<heading>.<leaf>` and the inventory
    // keys a tag BARE, so before the fix a tag-table signal could never match its own disposition row —
    // it was swept, it was dispositioned in the artifact, and it still reported as unaccounted.
    [Fact]
    public void TagTableSignal_IsDispositionedByItsTagTableHeading()
    {
        WriteTagTable("IO_Line", "DI1_LineHealthy", "DQ1_LineRun");
        WriteSpec("- **C1** bound to `DB_In.Bound` [io]\n");
        var unclaimed = WriteUnclaimed(
            "## Full disposition table\n\n### `IO_Line`\n\n| Members | Disposition |\n|---|---|\n" +
            "| `DI1_LineHealthy`, `DQ1_LineRun` | out-of-scope-pilot |\n" +
            "\n### `DB_In` (DB 1)\n\n| Members | Disposition |\n|---|---|\n" +
            "| `Disposed`, `Nobody` | out-of-scope-instance |\n");

        var report = SignalSweepRunner.Run(_project, _specs, registerPath: null, unclaimedPath: unclaimed);

        Assert.Equal(5, report.Swept);
        Assert.Equal(1, report.Claimed);
        Assert.Equal(4, report.Disposed);
        Assert.Empty(report.Unaccounted);
        Assert.False(report.HasFindings);
    }

    // A real tag table is routinely called "Default tag table" — a heading regex that only accepted an
    // identifier made that container undispositionable by construction.
    [Fact]
    public void TagTableWithSpacesInItsName_IsStillDispositionable()
    {
        WriteTagTable("Default tag table", "DI9_Spare");
        WriteSpec("- **C1** `DB_In.Bound`, `DB_In.Disposed`, `DB_In.Nobody`\n");
        var unclaimed = WriteUnclaimed(
            "### `Default tag table`\n\n| Members | Disposition |\n|---|---|\n| `DI9_Spare` | unused spare |\n");

        var report = SignalSweepRunner.Run(_project, _specs, registerPath: null, unclaimedPath: unclaimed);

        Assert.Equal(1, report.Disposed);
        Assert.Empty(report.Unaccounted);
    }

    // The true positive must survive the fix: a tag nobody dispositioned still reports. Matching is
    // strict on `<TableName>.<TagName>` — a same-named member dispositioned under a DIFFERENT container
    // does not account for the tag.
    [Fact]
    public void TagTableSignal_WithNoDispositionRow_StillReportsUnaccounted()
    {
        WriteTagTable("IO_Line", "DI1_LineHealthy", "DQ1_LineRun");
        WriteSpec("- **C1** bound to `DB_In.Bound` [io]\n");
        var unclaimed = WriteUnclaimed(
            "### `IO_Line`\n\n| Members | Disposition |\n|---|---|\n| `DI1_LineHealthy` | out-of-scope-pilot |\n" +
            "\n### `DB_In` (DB 1)\n\n| Members | Disposition |\n|---|---|\n" +
            "| `Disposed`, `Nobody`, `DQ1_LineRun` | out-of-scope-instance |\n");

        var report = SignalSweepRunner.Run(_project, _specs, registerPath: null, unclaimedPath: unclaimed);

        Assert.True(report.HasFindings);
        var unaccounted = Assert.Single(report.Unaccounted);
        Assert.Equal("DQ1_LineRun", unaccounted.Path);
        Assert.Equal("IO_Line", unaccounted.Container);
        Assert.Equal(SignalContainerKind.TagTable, unaccounted.Kind);
    }

    // A spec claims a tag by its bare name — that leg worked before and must keep working.
    [Fact]
    public void TagTableSignal_ClaimedBareBySpec_CountsAsClaimed()
    {
        WriteTagTable("IO_Line", "DI1_LineHealthy");
        WriteSpec("- **C1** bound to `DI1_LineHealthy` [io]\n");

        var report = SignalSweepRunner.Run(_project, _specs, registerPath: null, unclaimedPath: null);

        Assert.Equal(4, report.Swept);
        Assert.Equal(1, report.Claimed);
        Assert.Equal(3, report.Unaccounted.Count);
    }

    // Mixed corpus: both legs counted, each signal attributed to the container that declares it.
    [Fact]
    public void MixedDbAndTagTableInventory_AttributesEachSignalToItsOwnContainer()
    {
        WriteTagTable("IO_Line", "DI1_LineHealthy", "DQ1_LineRun");
        WriteTagTable("IO_Units", "DI2_UnitLevel");
        WriteSpec("- **C1** `DB_In.Bound`, `DI1_LineHealthy`\n");

        var report = SignalSweepRunner.Run(_project, _specs, registerPath: null, unclaimedPath: null);

        Assert.Equal(6, report.Swept);
        Assert.Equal(3, report.ByContainer.Count);

        var byName = report.ByContainer.ToDictionary(c => c.Container, StringComparer.Ordinal);
        Assert.Equal(3, byName["DB_In"].Swept);
        Assert.Equal(SignalContainerKind.Db, byName["DB_In"].Kind);
        Assert.Equal(2, byName["IO_Line"].Swept);
        Assert.Equal(SignalContainerKind.TagTable, byName["IO_Line"].Kind);
        Assert.Equal(1, byName["IO_Units"].Swept);
        Assert.Equal(1, byName["IO_Line"].Claimed);
    }

    // The presentation half of FI-45 item 2: the old grouping split on the first dot, so N flat tags
    // rendered as N one-row "DBs". A report nobody can read is not a report.
    [Fact]
    public void GroupedOutput_RendersATagTableAsOneTable_NotOneRowPerTag()
    {
        WriteTagTable("IO_Line", "DI1_LineHealthy", "DQ1_LineRun", "DQ2_LineStop");
        WriteSpec("- **C1** `DB_In.Bound`\n");

        var report = SignalSweepRunner.Run(_project, _specs, registerPath: null, unclaimedPath: null);
        var text = SignalSweepOutputFormatter.FormatText(report);

        Assert.Contains("by container (DB / tag table)", text);
        Assert.Contains("tag table IO_Line", text);

        // One row for the table, not one per tag.
        var rows = text.Split('\n').Where(l => l.Contains("IO_Line") && l.Contains("swept")).ToList();
        Assert.Single(rows);
        Assert.DoesNotContain("DI1_LineHealthy   swept", text);

        // And the unaccounted list names the table a tag belongs to, since the tag name cannot.
        Assert.Contains("DI1_LineHealthy   (tag table IO_Line)", text);

        var json = SignalSweepOutputFormatter.FormatJson(report);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var containers = doc.RootElement.GetProperty("byContainer").EnumerateArray().ToList();
        Assert.Equal(2, containers.Count);
        Assert.Contains(containers, c => c.GetProperty("container").GetString() == "IO_Line"
                                         && c.GetProperty("kind").GetString() == "tag table"
                                         && c.GetProperty("swept").GetInt32() == 3);
    }

    // Strictness has a cost: a heading that names no real container qualifies its rows into nothing.
    // Said out loud that is a five-second fix; unsaid it is the same silent noise all over again.
    [Fact]
    public void DispositionHeadingNamingNoContainer_IsWarnedAbout()
    {
        WriteTagTable("IO_Line", "DI1_LineHealthy");
        WriteSpec("- **C1** `DB_In.Bound`, `DB_In.Disposed`, `DB_In.Nobody`\n");
        var unclaimed = WriteUnclaimed(
            "### `IO_Tags`\n\n| Members | Disposition |\n|---|---|\n| `DI1_LineHealthy` | out-of-scope |\n");

        var report = SignalSweepRunner.Run(_project, _specs, registerPath: null, unclaimedPath: unclaimed);

        Assert.Equal("DI1_LineHealthy", Assert.Single(report.Unaccounted).Path);
        Assert.Contains(report.Warnings, w => w.Contains("'IO_Tags' names no global DB or tag table"));
    }

    [Fact]
    public void Report_StatesItsDenominator_AndJsonIsValid()
    {
        WriteSpec("- **C1** `DB_In.Bound`\n");

        var report = SignalSweepRunner.Run(_project, _specs, registerPath: null, unclaimedPath: null);

        Assert.Equal(1, report.FilesScanned);
        Assert.Contains("1 file(s) scanned", SignalSweepOutputFormatter.FormatText(report));

        var json = SignalSweepOutputFormatter.FormatJson(report);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal(3, doc.RootElement.GetProperty("swept").GetInt32());
    }
}
