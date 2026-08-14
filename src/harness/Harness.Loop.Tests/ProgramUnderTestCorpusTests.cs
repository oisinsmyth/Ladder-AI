using Harness.Map;
using Harness.Run;

namespace Harness.Loop.Tests;

/// <summary>
/// 🔴 <b>THE IR LOADER, SWEPT OVER THE WHOLE COMMITTED CORPUS — because the input you would otherwise
/// have to invent is already in the repository.</b>
///
/// <para>A loader validated only against fixtures is a loader validated against what its author expected.
/// <b>Every fixture in <see cref="LoopCliTests"/> was written by the same hand that wrote the classifier</b>,
/// so agreement between them demonstrates self-consistency and nothing else. These files were written for
/// entirely different reasons, by a different stage of the pipeline, and they are what a real
/// <c>--program</c> will actually be pointed at.</para>
///
/// <para><b>What the sweep found, 2026-08-14:</b> 43 committed <c>.ir</c> files. <b>38 classify</b> — 18
/// blocks, 18 data blocks, 2 tag tables — and <b>5 are refused BY NAME as PLC data types</b>
/// (<c>TYPE &lt;Name&gt;</c>). That refusal is correct rather than convenient: <see cref="HarnessObjectKind"/>
/// has no member for a type, and mapping one onto <c>DataBlock</c> would hand it a data block's retention
/// rules and enter it in the downloadable set the load manifest is compared against. <b>It is also a real
/// capability gap</b> — <c>FB_HopperBlockageMonitor</c> and its instance DB both reference
/// <c>UDT_HopperBlockageIO</c>, so a program under test that needs its type present cannot be supplied
/// whole today. Named here so it is a build-list item rather than a surprise on the rig.</para>
/// </summary>
public class ProgramUnderTestCorpusTests
{
    private static string Corpus()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "ir", "test-project001");
            if (Directory.Exists(candidate) && Directory.GetFiles(candidate, "*.ir").Length > 0)
                return candidate;

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "the committed IR corpus was not found by walking up from " + AppContext.BaseDirectory
            + ". *** THIS IS A FAILURE, NOT A SKIP: *** a corpus sweep that quietly stops sweeping is the "
            + "check nobody notices has gone.");
    }

    [Fact]
    public void EVERY_COMMITTED_IR_FILE_IS_EITHER_CLASSIFIED_OR_REFUSED_BY_NAME_and_never_skipped()
    {
        var files = Directory.GetFiles(Corpus(), "*.ir").OrderBy(f => f, StringComparer.Ordinal).ToArray();

        // Denominator first. A sweep that cannot say how many files it examined cannot say they passed.
        Assert.True(files.Length >= 40, $"the corpus holds {files.Length} .ir file(s); this sweep would be near-vacuous.");

        var loaded = new List<HarnessObject>();
        var refused = new List<string>();

        foreach (var file in files)
        {
            try
            {
                loaded.AddRange(ProgramUnderTest.Load(new[] { file }, File.ReadAllText, p => new[] { p }));
            }
            catch (InvalidDataException ex)
            {
                // *** A REFUSAL MUST NAME THE FILE. *** A message that does not is indistinguishable from a
                // skip to the person holding forty files and one error.
                Assert.Contains(Path.GetFileName(file), ex.Message, StringComparison.Ordinal);
                refused.Add(Path.GetFileName(file));
            }
        }

        // *** NOTHING FELL BETWEEN THE TWO. *** This is the assertion the whole test exists for: a file
        // silently left out of the program set is left out of the BUILD STAMP, and the version register
        // would then confirm a build that is not the one running.
        Assert.Equal(files.Length, loaded.Count + refused.Count);

        // And the loaded half is real work, not an empty pass. Written as a floor rather than an exact
        // count so that ADDING type support — which moves files from `refused` to `loaded` — does not turn
        // this red. A test that punishes its own fix is how a defect acquires tenure.
        Assert.True(loaded.Count >= 35, $"only {loaded.Count} of {files.Length} corpus files classified.");
        Assert.Contains(loaded, o => o.Kind == HarnessObjectKind.Block);
        Assert.Contains(loaded, o => o.Kind == HarnessObjectKind.DataBlock);
        Assert.Contains(loaded, o => o.Kind == HarnessObjectKind.TagTable);

        // Every refusal in this corpus is the ONE named gap. A refusal for any other reason is a defect in
        // the classifier meeting real IR, and it fails here with the file that caused it.
        foreach (var name in refused)
        {
            var ir = File.ReadAllText(Path.Combine(Corpus(), name));
            Assert.StartsWith("TYPE ", ir.TrimStart(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void THE_NAME_COMES_FROM_THE_IR_HEADER_and_not_from_the_filename()
    {
        // The deploy path WRITES `<Name>.ir`, so reading the name back out of the filename would be this
        // tool agreeing with itself. The corpus contains a file whose stem is not its object name — a tag
        // table called `Default tag table`, in `DefaultTagTable.ir` — which is exactly that difference.
        var loaded = Directory.GetFiles(Corpus(), "*.ir")
            .SelectMany(f =>
            {
                try { return ProgramUnderTest.Load(new[] { f }, File.ReadAllText, p => new[] { p }); }
                catch (InvalidDataException) { return Array.Empty<HarnessObject>(); }
            })
            .ToArray();

        var table = Assert.Single(loaded, o => o.Kind == HarnessObjectKind.TagTable && o.Name.Contains(' ', StringComparison.Ordinal));

        Assert.Equal("Default tag table", table.Name);
    }
}
