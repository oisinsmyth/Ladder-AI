using Converter.DriftCheck;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// Tooling-hammer campaign, 2026-08-14. Two families of defect in <c>drift-check</c>, both of which
/// end in the same place: a clean green over a comparison that never happened.
///
/// <para>
/// <b>EMPTY IS NOT CLEAN.</b> Three measured routes to <c>exit 0</c> having compared nothing —
/// (1) both directories existing but empty, which with <c>--complete</c> then printed a SCOPE line
/// declaring "the 0 absence(s) above are findings"; (2) every <c>.ir</c> unparseable (a zero-byte file,
/// a truncated write), because <c>DriftStatus.Error</c> never gated; (3) either path pointing one level
/// ABOVE the files, since both walks are <c>TopDirectoryOnly</c> — the likeliest real mistake of the
/// three, rewarded with a pass. <c>--complete</c> covered a missing FILE and said nothing about a
/// comparison that could not RUN, which is the same absence one level in.
/// </para>
///
/// <para>
/// <b>PAIRING BY FILENAME.</b> Found against the live controller, which is why no offline probe reached
/// it. TIA's own name for the default tag table contains spaces ("Default tag table"); the <c>.ir</c>
/// filename does not (<c>DefaultTagTable.ir</c>) — and <b>both documents declare the real name in their
/// own content</b>. Pairing on the filename made one object fail to pair and then counted it twice,
/// once in each direction: <c>EXPORT-ONLY</c> (which under <c>--complete</c> means "this is in the
/// controller and no .ir describes it" — serious, and false) plus <c>SKIPPED</c>, with nothing anywhere
/// suggesting they might be the same object. A spurious finding AND a silently skipped comparison out
/// of one naming mismatch.
/// </para>
/// </summary>
public class DriftCheckEmptyAndPairingTests : IDisposable
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
        var dir = Path.Combine(Path.GetTempPath(), $"drift-hammer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _dirs.Add(dir);
        return dir;
    }

    private static DbSource Db(string name) =>
        new("0", name, 1, InstanceOfName: null, Comment: null,
            Members: new[] { new DbMember("A", "Bool", Retain: false, StartValue: null) });

    private static void WriteIr(string dir, string fileName, DbSource db) =>
        File.WriteAllText(Path.Combine(dir, fileName), DbIrSerializer.Serialize(db));

    private static void WriteXml(string dir, string fileName, DbSource db) =>
        DbSourceWriter.Write(db).SaveAsTiaExport(Path.Combine(dir, fileName));

    // --- empty is not clean --------------------------------------------------------------------

    [Fact]
    public void BothDirectoriesEmpty_IsNotAPass()
    {
        var report = DriftCheckRunner.Run(NewDir(), NewDir());

        Assert.Equal(0, report.ComparedCount);
        Assert.True(report.ExaminedNothing);
        Assert.True(report.HasDrift);
        Assert.Contains("NOTHING COMPARED", DriftCheckOutputFormatter.FormatText(report));
    }

    [Fact]
    public void BothDirectoriesEmpty_WithComplete_IsStillNotAPass()
    {
        Assert.True(DriftCheckRunner.Run(NewDir(), NewDir(), complete: true).HasDrift);
    }

    [Theory]
    [InlineData("")]                                      // zero-byte: an interrupted write
    [InlineData("DB DB_Match\n  ROOTID 0\n  NUM")]        // truncated mid-header
    [InlineData("this is not IR at all")]
    public void UnparseableIr_Gates_RatherThanBeingACountedNonEvent(string irText)
    {
        var project = NewDir();
        var exports = NewDir();
        File.WriteAllText(Path.Combine(project, "DB_Match.ir"), irText);
        WriteXml(exports, "DB_Match.xml", Db("DB_Match"));

        var report = DriftCheckRunner.Run(project, exports);

        Assert.Contains(report.Entries, e => e.Status == DriftStatus.Error);
        Assert.Equal(0, report.ComparedCount);
        Assert.True(report.HasDrift);
    }

    // An unparseable EXPORT was an unhandled XmlException that ended the whole walk. Same class as an
    // unbuildable .ir, and now reported the same way: a comparison that could not be made.
    [Fact]
    public void UnparseableExport_IsAnError_NotACrash()
    {
        var project = NewDir();
        var exports = NewDir();
        WriteIr(project, "DB_Match.ir", Db("DB_Match"));
        File.WriteAllText(Path.Combine(exports, "DB_Match.xml"), "<?xml version=\"1.0\"?><Document><Unclosed>");

        var report = DriftCheckRunner.Run(project, exports);

        Assert.Contains(report.Entries, e => e.Status == DriftStatus.Error);
        Assert.True(report.HasDrift);
    }

    [Fact]
    public void ParentDirectoryOfTheRealFiles_IsNotAPass()
    {
        var parent = NewDir();
        var nested = Path.Combine(parent, "test-project001");
        Directory.CreateDirectory(nested);
        WriteIr(nested, "DB_Match.ir", Db("DB_Match"));

        var exports = NewDir();
        WriteXml(exports, "DB_Match.xml", Db("DB_Match"));

        var report = DriftCheckRunner.Run(parent, exports);

        Assert.True(report.ExaminedNothing);
        Assert.True(report.HasDrift);
    }

    // --- pairing by declared identity ----------------------------------------------------------

    [Fact]
    public void FilenameAndDeclaredNameDiffer_PairAndCompare_NotTwoAbsences()
    {
        var project = NewDir();
        var exports = NewDir();

        var table = new PlcTagTableSource("0", "Default tag table", new[]
        {
            new PlcTagSource("1", "Start_PB", "Bool", "%I0.0", true, true, true, null),
        });
        File.WriteAllText(Path.Combine(project, "DefaultTagTable.ir"), TagTableIrSerializer.Serialize(table));
        PlcTagTableSourceWriter.Write(table).SaveAsTiaExport(Path.Combine(exports, "Default tag table.xml"));

        var report = DriftCheckRunner.Run(project, exports, complete: true);

        // ONE entry, and it is a comparison — not an EXPORT-ONLY plus a SKIPPED for the same object.
        var entry = Assert.Single(report.Entries);
        Assert.Equal(DriftStatus.Match, entry.Status);
        Assert.Equal(1, report.ComparedCount);
        Assert.False(report.HasDrift);
    }

    // The other half: a genuine absence must still be ONE row, in ONE direction.
    [Fact]
    public void GenuineAbsence_IsOneRowNotTwo()
    {
        var project = NewDir();
        var exports = NewDir();
        WriteIr(project, "DB_Match.ir", Db("DB_Match"));
        WriteIr(project, "DB_Absent.ir", Db("DB_Absent"));
        WriteXml(exports, "DB_Match.xml", Db("DB_Match"));

        var report = DriftCheckRunner.Run(project, exports);

        Assert.Single(report.Entries, e => e.Name == "DB_Absent");
        Assert.Equal(DriftStatus.Skipped, report.Entries.Single(e => e.Name == "DB_Absent").Status);
    }

    // Two files claiming one identity is a THIRD thing — not an absence in either direction, because
    // nothing can be compared and calling one of them "missing" would be a guess about which was meant.
    [Fact]
    public void TwoFilesClaimingOneIdentity_IsAPairingFailure_AndGates()
    {
        var project = NewDir();
        var exports = NewDir();
        WriteIr(project, "DB_Match.ir", Db("DB_Match"));
        WriteIr(project, "DB Match.ir", Db("DB_Match"));
        WriteXml(exports, "DB_Match.xml", Db("DB_Match"));

        var report = DriftCheckRunner.Run(project, exports);

        var failure = Assert.Single(report.Entries, e => e.Status == DriftStatus.PairingFailure);
        Assert.Contains("claim the identity", failure.Detail);
        Assert.True(report.HasDrift);
    }

    // --- the unaffected case, tested as deliberately as the refused ones ------------------------

    [Fact]
    public void HealthyCorpus_IsStillAPass_AndStatesItsDenominatorAndItsScope()
    {
        var project = NewDir();
        var exports = NewDir();
        WriteIr(project, "DB_Match.ir", Db("DB_Match"));
        WriteXml(exports, "DB_Match.xml", Db("DB_Match"));

        var report = DriftCheckRunner.Run(project, exports, complete: true);

        Assert.False(report.HasDrift);
        Assert.Equal(1, report.ComparedCount);

        var text = DriftCheckOutputFormatter.FormatText(report);
        Assert.Contains("COMPARED: 1 object(s)", text);
        // The scope it never stated: `compare` prints a MEMORYLAYOUT line on every run and REFUSES
        // (exit 2) the very pair this reports as a MATCH.
        Assert.Contains("MemoryLayout is NOT compared", text);
        Assert.DoesNotContain("NOTHING COMPARED", text);
    }
}
