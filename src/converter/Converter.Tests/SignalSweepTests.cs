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
