using Converter.RelationReconcile;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// The stopped-D3 marker (2026-08-05, audit F-41).
///
/// <c>RelationReconcileRunner</c> uses <c>LooksStopped</c> to choose between two very different notes
/// when the render leg is ABSENT: "the ledger records a stopped D3, a legitimate state, not a drift"
/// and "no STOPPED marker either. Verify this is intended." Until this change the marker was a bare
/// <c>STOPPED</c> substring match over the whole file, so the uppercase word in unrelated prose — and
/// process prose says "STOPPED" constantly — turned the second note into the first. Neither branch
/// gates the exit code, so the only damage is a misleading reassurance, which is exactly the kind of
/// thing that goes unnoticed.
///
/// The heading forms below are the REAL ones, taken from the committed corpus: rerun2 and rerun3 use
/// <c>## **STOPPED …**</c>, rerun uses a blockquoted <c>## RUN STATUS … STOPPED.</c> with the word
/// mid-sentence. A marker anchored to <c>^##\s+\*\*STOPPED</c> would have matched the first two and
/// silently downgraded the third.
/// </summary>
public class RelationReconcileStoppedMarkerTests : IDisposable
{
    private readonly string _dir;
    private readonly string _specs;

    public RelationReconcileStoppedMarkerTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"relrec-stop-{Guid.NewGuid():N}");
        _specs = Path.Combine(_dir, "equipment-specs");
        Directory.CreateDirectory(_specs);
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

    [Theory]
    // Real heading forms — all of these must keep reading as "stopped".
    [InlineData("## **STOPPED — all six instances. No render produced.**", true)]
    [InlineData("## **STOPPED.** No render was produced, for any of the six instances.", true)]
    [InlineData("> ## RUN STATUS — D3 (render) STOPPED. D0, D1, D2 COMPLETE.", true)]
    [InlineData("### STOPPED", true)]
    // The F-41 defect: the word outside a heading must NOT count as a stop declaration.
    [InlineData("- **Outcome:** D0, D1 and D2 complete. D3 STOPPED for all six instances.", false)]
    [InlineData("| C1 | render | the conveyor must be STOPPED before the door opens | — |", false)]
    [InlineData("The upstream unit is STOPPED whenever the interlock drops.", false)]
    [InlineData("Nothing was UNSTOPPED here, and ## is not a heading mid-sentence.", false)]
    public void LooksStopped_ReadsAStopHeading_NotTheWordInProse(string line, bool expected)
    {
        var path = Path.Combine(_dir, "marker.md");
        File.WriteAllText(path, $"# D2\n\nintro prose\n{line}\ntrailing prose\n");

        Assert.Equal(expected, RelationArtifactParsers.LooksStopped(path));
    }

    // Multiline matters: the marker is matched against the whole file at once, so without it `^` would
    // only ever anchor at character 0 and every real artifact — each of which opens with its own `# `
    // title — would read as un-stopped.
    [Fact]
    public void LooksStopped_FindsAHeadingThatIsNotTheFirstLine()
    {
        var path = Path.Combine(_dir, "marker.md");
        File.WriteAllText(path, "# D2 — code structure\n\nprose prose prose\n\n## **STOPPED — no render produced.**\n");

        Assert.True(RelationArtifactParsers.LooksStopped(path));
    }

    [Fact]
    public void LooksStopped_FileWithNoStopAtAll_IsFalse()
    {
        var path = Path.Combine(_dir, "marker.md");
        File.WriteAllText(path, "# D2\n\n## Ledger — `InstA` (t)\n\nall six instances rendered.\n");

        Assert.False(RelationArtifactParsers.LooksStopped(path));
    }

    // End to end through the runner, since the note wording is the whole point of the distinction.
    [Fact]
    public void AbsentRender_WithTheWordStoppedOnlyInProse_AsksTheReaderToVerify()
    {
        var report = RunWithLedgerBody("| C1 | render | the conveyor must be STOPPED first | — |\n");

        Assert.Contains(report.Warnings, w => w.Contains("Verify this is intended", StringComparison.Ordinal));
    }

    [Fact]
    public void AbsentRender_WithARealStopHeading_IsReportedAsLegitimate()
    {
        var report = RunWithLedgerBody("| C1 | render | x | — |\n\n## **STOPPED — no render produced.**\n");

        Assert.Contains(report.Warnings, w => w.Contains("legitimate state", StringComparison.Ordinal));
    }

    private RelationReconcileReport RunWithLedgerBody(string rowsAndTail)
    {
        File.WriteAllText(Path.Combine(_specs, "InstA.md"), "# spec\n- **C1** something\n");

        var ledger = Path.Combine(_dir, "code-structure.md");
        File.WriteAllText(ledger,
            "# D2\n\n## Ledger — `InstA` (t)\n\n| relation | disposition | evidence | precondition class |\n|---|---|---|---|\n"
            + rowsAndTail);

        var register = Path.Combine(_dir, "requirements.md");
        File.WriteAllText(register,
            "# register\n\n### `InstA` — title (class)\n\n| REQ | Rel | Text | Class | Provenance | Open |\n|---|---|---|---|---|---|\n" +
            "| REQ-001 | C1 | text | control | ref | — |\n");

        return RelationReconcileRunner.Run(_specs, ledger, register, projectDir: null);
    }
}
