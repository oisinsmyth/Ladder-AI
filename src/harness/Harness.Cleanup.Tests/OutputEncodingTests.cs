namespace Harness.Cleanup.Tests;

/// <summary>
/// <b>Everything this tool EMITS is ASCII, and that is checked rather than remembered.</b>
///
/// <para>Measured on this tool's own first real run: an em dash, a section sign and a warning glyph came
/// back through the console as <c>?</c> and <c>\ufffd</c>, so the line reading <i>"section 16.12c"</i>
/// arrived as mojibake and the emphasis marks arrived as question marks. The repo already carries this
/// hazard for <c>.ps1</c> — PS 5.1 reads a BOM-less script as ANSI and a UTF-8 em dash decodes to a
/// quote delimiter — and a report that has to be read on a Windows console is in the same position.</para>
///
/// <para>Typography stays in the SOURCE, where it is read by people and not by a codepage. This test
/// covers the emitted text only, and it walks a run that produces the LONGEST output rather than a
/// refusal, so the sections that only appear on a full plan are covered too.</para>
/// </summary>
public class OutputEncodingTests
{
    [Fact]
    public void A_FULL_PLAN_emits_only_ASCII()
    {
        using var corpus = new TempCorpus();
        corpus.Fb("FB_Live", 9001).Fc("FC_Orphan", 9098).Type("UDT_Thing").TagTable("Some tag table", "SomeTagTable");
        var xc = corpus.CrossCheck("xc.json", "[{\"block\":\"Main\",\"calls\":[\"FB_Live\"],\"instanceDbRoots\":[]}]");
        var claims = corpus.File_("claims.json",
            "{\"store\":\"C:/store\",\"claims\":[{\"kind\":\"block-number\",\"value\":\"FC9098\",\"agent\":\"a\",\"purpose\":\"p\",\"created\":\"t\"}]}");

        var outcome = CleanupRun.Execute(new CleanupOptions(
            corpus.Ir, xc, new[] { corpus.File_("v.json", "{}") }, corpus.DrainedReport(),
            "lane-c", claims, Array.Empty<string>()));

        // The run must be the interesting one, or this asserts ASCII over a refusal banner.
        Assert.Equal(CleanupExit.Planned, outcome.Exit);
        Assert.Contains("REMOVE", outcome.Text, StringComparison.Ordinal);

        AssertAscii(outcome.Text);
    }

    [Theory]
    [InlineData(CleanupExit.Refused)]
    [InlineData(CleanupExit.NothingExamined)]
    [InlineData(CleanupExit.TestsNotDrained)]
    public void EVERY_REFUSAL_BANNER_emits_only_ASCII(CleanupExit expected)
    {
        using var corpus = new TempCorpus();
        var xc = corpus.CrossCheck("xc.json", "[]");
        var artifacts = new[] { corpus.File_("v.json", "{}") };

        var options = expected switch
        {
            CleanupExit.Refused => new CleanupOptions(corpus.Ir, null, artifacts, corpus.DrainedReport(), "a", null, Array.Empty<string>()),
            CleanupExit.NothingExamined => new CleanupOptions(corpus.Ir, xc, artifacts, corpus.DrainedReport(), "a", null, Array.Empty<string>()),
            _ => Busy(corpus, xc, artifacts),
        };

        if (expected == CleanupExit.TestsNotDrained) corpus.Fc("FC_Orphan", 9098);

        var outcome = CleanupRun.Execute(options);

        Assert.Equal(expected, outcome.Exit);
        AssertAscii(outcome.Text);

        static CleanupOptions Busy(TempCorpus c, string xc, string[] artifacts) => new(
            c.Ir, xc, artifacts,
            c.File_("busy.txt", "format=1\ncomputed-by=x\ncomputed-at=t\nin-flight=1\ntest=V-7\nend\n"),
            "a", null, Array.Empty<string>());
    }

    private static void AssertAscii(string text)
    {
        var offenders = text
            .Select((c, i) => (c, i))
            .Where(x => x.c > '\u007f')
            .Select(x => $"U+{(int)x.c:X4} at offset {x.i}")
            .ToArray();

        Assert.True(offenders.Length == 0,
            "harness-cleanup emitted non-ASCII, which a Windows console will mangle: " + string.Join(", ", offenders));
    }
}
