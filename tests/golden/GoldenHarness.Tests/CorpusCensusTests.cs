using Converter.DriftCheck;
using Xunit;

namespace GoldenHarness;

/// <summary>
/// One <c>drift-check</c> run per committed project, computed once and shared. Both this file and
/// <see cref="ExportOnlyResidueTests"/> ask questions of the same report, and running the Normalizer
/// over 41 objects twice for the privilege of asking twice is waste, not independence.
/// </summary>
internal static class CommittedCorpus
{
    /// <summary>The projects with BOTH a committed <c>ir/</c> and a committed <c>simatic-ml/</c>.</summary>
    internal static readonly string[] Projects = { "reference", "test-project001" };

    private static readonly Dictionary<string, DriftCheckReport> Cache = new(StringComparer.Ordinal);
    private static readonly object Gate = new();

    internal static string IrDir(string project) => Path.Combine(ToolPaths.RepoRoot(), "ir", project);

    internal static string XmlDir(string project) => Path.Combine(ToolPaths.RepoRoot(), "simatic-ml", project);

    internal static DriftCheckReport Report(string project)
    {
        lock (Gate)
        {
            if (!Cache.TryGetValue(project, out var report))
            {
                report = DriftCheckRunner.Run(IrDir(project), XmlDir(project));
                Cache[project] = report;
            }

            return report;
        }
    }

    internal static int Count(DriftCheckReport report, DriftStatus status) =>
        report.Entries.Count(e => e.Status == status);

    internal static int IrFileCount(string project) =>
        Directory.EnumerateFiles(IrDir(project), "*.ir", SearchOption.TopDirectoryOnly).Count();

    internal static int ExportFileCount(string project) =>
        Directory.EnumerateFiles(XmlDir(project), "*.xml", SearchOption.TopDirectoryOnly).Count();
}

/// <summary>
/// A row of the census. Every field is a COUNT OF FILES OR OF REPORT ENTRIES — nothing here is a
/// judgement about any block, so a row going stale is a fact about the corpus and never a fact about
/// the converter.
/// </summary>
internal sealed record CorpusCensus(
    string Project,
    int IrFiles,
    int Exports,
    int Compared,
    int UnpairedIr,
    int ExportOnly,
    string Measured);

/// <summary>
/// *** THE DENOMINATOR, ASSERTED. *** Every other population guard in this tree is a NON-EMPTINESS
/// guard — <c>Assert.True(count &gt; 0)</c> — and before this file there was not one
/// <c>Assert.Equal</c> on a corpus size anywhere in <c>tests/golden/</c>.
///
/// <para><b>🔴 THE MEASURED CONSEQUENCE, WHICH IS WHY THIS FILE EXISTS.</b> If
/// <c>simatic-ml/test-project001/</c> lost eleven exports tomorrow, <c>COMPARED</c> would fall
/// <b>26 → 15</b>, <c>SKIPPED</c> would rise 17 → 28, the drifted set would still equal the (empty)
/// baseline, <c>Entries.Count</c> would still be &gt; 0 — and <b>the whole suite would stay green.</b>
/// The ratchet this project relies on for the drift BASELINE (<see cref="ExportDriftDetectorTests"/>)
/// had no counterpart for the DENOMINATOR. ADR-0012 has <c>drift-check</c> print <c>COMPARED: n</c>
/// precisely because <i>"an invariance claim over an approximate denominator"</i> is worthless; the
/// number was printed for that reason and then never asserted.</para>
///
/// <para><b>THE RATCHET'S SHAPE, AND THE ARGUMENT FOR IT.</b> Three shapes were available and the
/// obviously-wrong one is easy to name: <i>a number nobody understands</i>. So:</para>
///
/// <list type="bullet">
/// <item><b>Not a bare floor (<c>&gt;=</c>).</b> A floor catches the loss, which is the headline case,
/// and it never fires on a legitimate addition — that is its whole appeal. It is rejected because it
/// goes stale UPWARDS in silence: the recorded number drifts further below the real corpus with every
/// export added, and the day somebody deletes six exports from a corpus that has grown by six, the
/// floor is still met. A guard whose margin grows without anyone noticing is a guard that stops being
/// one. Same disease as the drift baseline that <see cref="ExportDriftDetectorTests"/> keeps honest in
/// both directions, and for the same reason.</item>
/// <item><b>Not a bare <c>Assert.Equal(26, …)</c> either.</b> A pin whose expected value is a magic
/// integer fails on every legitimate addition with nothing to tell the reader whether the move was the
/// good kind, so it trains people to bump the number without looking — which IS the stale-baseline
/// disease, one file over.</item>
/// <item><b>What is here: an EXACT pin whose number is DERIVABLE, DIRECTIONAL, and CROSS-CHECKED.</b>
/// Exact, so it fires in both directions. Derivable, because each row states how to recompute it from
/// a directory listing (<c>ls ir/&lt;p&gt;/*.ir | wc -l</c>), so no reader has to take it on trust.
/// Directional, because the failure message says which way it moved and what each direction MEANS —
/// down is the defect, up is a corpus that grew and a row that has to be re-measured. And
/// cross-checked, because <see cref="TheCensusArithmetic_Closes"/> re-derives the same quantities from
/// the live report independently of the recorded row, so a bump to a number that is not the true pair
/// count fails anyway. *** THAT LAST PART IS WHAT MAKES THE BUMP SAFE TO ASK FOR: *** the only thing
/// the bumper can actually get wrong is failing to ask why it moved, and the message asks.</item>
/// </list>
///
/// <para><b>⚠️ THE ARITHMETIC, CORRECTED HERE — THE DENOMINATOR IS 43, NOT 41.</b>
/// <c>test-project001</c> holds <b>43</b> <c>.ir</c> and <b>26</b> <c>.xml</c>; 26 pair, <b>17</b> are
/// unpaired and every one of them is one-directional (an <c>.ir</c> with no export — <b>zero</b>
/// export-only). The "41" that has been quoted for this denominator is 43 minus the two tag tables
/// (<c>DefaultTagTable</c>, <c>HarnessMirror</c>), and it is wrong.</para>
///
/// <para>*** AND 41 IS A REAL NUMBER IN THIS TREE, WHICH IS EXACTLY WHY THE WRONG ONE READS
/// PLAUSIBLY. *** <b>41 is the repo-wide count of committed EXPORTS</b> (15 reference + 26
/// test-project001) and therefore also the count of paired objects — the population
/// <see cref="DerivedInterfaceBlindSideTests"/> means by "41 pairs" and
/// <see cref="MemoryLayoutFidelityTests"/> by "28 of 41 committed exports". Those are correct.
/// "26 of 41" is not: within <c>test-project001</c> it is <b>26 of 43</b>, and repo-wide it is
/// <b>41 of 58</b>. <see cref="TheRepoWideTotals_MatchTheRecordedCensus"/> pins all three so the
/// ambiguity cannot recur silently.</para>
///
/// <para><b>🔴 WHAT THIS FILE CANNOT SEE, stated because a pinned number is the most convincing kind
/// of wrong answer.</b>
/// <list type="bullet">
/// <item><b>Whether the corpus is the RIGHT size.</b> Nothing here says 43 objects is the whole
/// project. The live <c>test-project001</c> controller carries objects that this corpus does not
/// describe at all — see <see cref="ExportOnlyResidueTests"/> — and a census over a corpus cannot
/// discover an object the corpus has never heard of.</item>
/// <item><b>Whether a comparison MEANT anything.</b> <c>COMPARED</c> counts objects put through the
/// Normalizer, not objects compared against something TIA wrote. That is a different
/// quantity — <c>DriftCheckReport.TiaExportCount</c> — and it is 26 of 26 here and 11 of 15 in
/// <c>reference</c>. A corpus could hold its count and lose all its provenance.</item>
/// <item><b>Any project outside <see cref="CommittedCorpus.Projects"/>.</b> <c>ir/PlantAutoControl-bench/</c>
/// has no <c>simatic-ml/</c> counterpart, so it has no census row and no drift comparison; it is
/// outside this suite entirely, not silently passing inside it.</item>
/// <item><b>The third leg.</b> Both sides of every count are committed files. Whether the LIVE project
/// holds these 41 objects and no others needs Portal, which this lane does not have.</item>
/// </list></para>
/// </summary>
public class CorpusCensusTests
{
    /// <summary>
    /// MEASURED 2026-08-23 by <c>converter drift-check</c> over the committed corpus, and recomputable
    /// from a directory listing in two commands per row.
    ///
    /// <para>*** HOW TO UPDATE A ROW — the procedure, so that a bump is an act with a reason. ***
    /// <list type="number">
    /// <item>Establish WHICH DIRECTION moved and why. Up: an export or an <c>.ir</c> was added — name
    /// it in <c>Measured</c>. Down: something was DELETED, and that is the case this whole file exists
    /// to make loud. A deletion is a corpus decision and needs the same argument any other deletion of
    /// validation data needs (<c>CLAUDE.md</c>: <c>simatic-ml/</c> is validation data, not a
    /// regenerable cache).</item>
    /// <item>Re-measure, do not arithmetic. Run <c>converter drift-check --project ir/&lt;p&gt;
    /// --exports simatic-ml/&lt;p&gt;</c> and read <c>SUMMARY</c> and <c>COMPARED</c> off its output.</item>
    /// <item>Re-date <c>Measured</c> and say what changed. A row carrying a stale date is a row nobody
    /// has looked at.</item>
    /// </list></para>
    /// </summary>
    internal static readonly CorpusCensus[] Recorded =
    {
        // 15 .ir, 15 .xml, every one paired. The four 2026-07-10 seed artifacts committed with TIA's
        // scaffolding trimmed are IN this count — they are compared, they just carry no <DocumentInfo>
        // (PROVENANCE: 11 of 15). One of the 15 is DRIFTED (NodeStatusAlarms) and drift does not change
        // the denominator: a drifted object was compared.
        new("reference", IrFiles: 15, Exports: 15, Compared: 15, UnpairedIr: 0, ExportOnly: 0,
            Measured: "2026-08-23, `drift-check --project ir/reference --exports simatic-ml/reference` — SUMMARY: 1 drifted, 14 match, 0 skipped, 0 export-only, 0 error, 0 pairing-failure; COMPARED: 15."),

        // 🔴 THE ROW THE "41" WAS WRONG ABOUT. 50 .ir, 26 .xml, 26 compared, 24 unpaired — and all 24
        // unpaired are .ir-only, so the residue is ONE-DIRECTIONAL and ExportOnly is zero. The 24 break
        // down, ENUMERATED FROM THE FILES rather than remembered (CommittedBlocksRoundTripTests' prose
        // covers the same set): 11 BLOCKs — the 10 in that file's KnownMissingExports plus the
        // sidecar-carrying FB_Comms_ModbusServer, which its population walk drops one method earlier —
        // 1 tag table (HarnessMirror; DefaultTagTable IS paired and compared), 3 UDTs
        // (UDT_HopperBlockageStim, UDT_PusherStim, UDT_ShredderSequencerStim), and 9 harness instance
        // DBs (iDB_Hx* ×4, iDB_Comms_ModbusServer, iDB_HarnessViolationLatch, iDB_HopperBlockageStim,
        // iDB_PusherStim, iDB_ShredderSequencerStim).
        //
        // 🔴 UP BY 7, RATIFIED 2026-09-18. `eba7033` added the three-slot foundation and the suite was
        // never re-pointed, so this row sat 16 days behind the corpus it claims to measure. The seven:
        // FC_HarnessStimArbiter, FB_PusherStim, FB_ShredderSequencerStim, their two UDTs and their two
        // instance DBs. RE-MEASURED with drift-check, not arithmetic from the delta — which is how the
        // "1 drifted" below was found, and it is Main: its .ir gained three networks and
        // simatic-ml/test-project001/Main.xml was never re-exported. That is an OPEN DEBT needing a TIA
        // session, tracked in FI-93, not a number to bump.
        new("test-project001", IrFiles: 50, Exports: 26, Compared: 26, UnpairedIr: 24, ExportOnly: 0,
            Measured: "2026-09-18, `drift-check --project ir/test-project001 --exports simatic-ml/test-project001` — SUMMARY: 1 drifted, 25 match, 24 skipped, 0 export-only, 0 error, 0 pairing-failure; COMPARED: 26. The 1 drifted is Main, awaiting a TIA re-export."),
    };

    public static IEnumerable<object[]> Projects() =>
        CommittedCorpus.Projects.Select(p => new object[] { p });

    internal static CorpusCensus For(string project) =>
        Recorded.Single(c => string.Equals(c.Project, project, StringComparison.Ordinal));

    /// <summary>
    /// THE RATCHET. Exact, both directions, one message per field naming which way it moved.
    ///
    /// <para>All five numbers are collected before asserting, deliberately: a shrinking corpus moves
    /// several at once (deleting eleven exports moves <c>Exports</c>, <c>Compared</c> and
    /// <c>UnpairedIr</c> together), and a first-failing-assert would show one third of the picture and
    /// invite one third of a fix.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(Projects))]
    public void TheCorpusCensus_MatchesTheRecordedNumbers(string project)
    {
        var recorded = For(project);
        var report = CommittedCorpus.Report(project);

        var actual = new CorpusCensus(
            project,
            IrFiles: CommittedCorpus.IrFileCount(project),
            Exports: CommittedCorpus.ExportFileCount(project),
            Compared: report.ComparedCount,
            UnpairedIr: CommittedCorpus.Count(report, DriftStatus.Skipped),
            ExportOnly: CommittedCorpus.Count(report, DriftStatus.ExportOnly),
            Measured: recorded.Measured);

        var moves = new List<string>();
        Check("IrFiles (`ir/" + project + "/*.ir`)", recorded.IrFiles, actual.IrFiles);
        Check("Exports (`simatic-ml/" + project + "/*.xml`)", recorded.Exports, actual.Exports);
        Check("Compared (drift-check's own COMPARED line — the DENOMINATOR)", recorded.Compared, actual.Compared);
        Check("UnpairedIr (SKIPPED: an .ir with no export)", recorded.UnpairedIr, actual.UnpairedIr);
        Check("ExportOnly (an export with no .ir — see ExportOnlyResidueTests)", recorded.ExportOnly, actual.ExportOnly);

        void Check(string field, int was, int now)
        {
            if (was == now)
            {
                return;
            }

            moves.Add($"{field}: recorded {was}, now {now} ({(now < was ? "DOWN" : "UP")} by {Math.Abs(now - was)})");
        }

        Assert.True(moves.Count == 0,
            $"the committed corpus of '{project}' is not the size this suite records.\n  " +
            string.Join("\n  ", moves) + "\n" +
            $"Row measured: {recorded.Measured}\n\n" +
            "DOWN is the case this guard was written for, and NOTHING ELSE IN THIS TREE WOULD HAVE " +
            "NOTICED IT. Eleven exports deleted from test-project001 leaves the drifted set equal to " +
            "its baseline, Entries.Count > 0, and every other assertion green — an invariance claim " +
            "over a denominator that quietly shrank. `simatic-ml/` is committed validation data, not a " +
            "regenerable cache (CLAUDE.md): if the deletion was deliberate, say so in the row's " +
            "Measured string; if it was not, restore the files.\n" +
            "UP means the corpus grew, which is ordinary and good. RE-MEASURE the row with " +
            "`converter drift-check` — do not arithmetic it from the delta — re-date it, and name what " +
            "was added. A number bumped without a reason is the same stale baseline this file exists " +
            "to prevent, written one file over.");
    }

    /// <summary>
    /// The recorded row is not the only place these quantities can be obtained from, and this is the
    /// second derivation. It re-computes the split from the LIVE report and the LIVE directory listing
    /// and requires the two identities to close:
    ///
    /// <code>
    ///   .ir files   ==  paired  +  SKIPPED       (an .ir either has an export or it does not)
    ///   .xml files  ==  paired  +  EXPORT-ONLY   (an export either has an .ir or it does not)
    ///   paired      ==  COMPARED  +  ERROR       (a paired object was compared, or the compare failed)
    /// </code>
    ///
    /// <para><b>This is the arithmetic the "41" got wrong, mechanised.</b> Nobody can quote a
    /// denominator that does not close any more: for <c>test-project001</c> the first identity is
    /// <c>43 == 26 + 17</c>, and 41 satisfies nothing.</para>
    ///
    /// <para>It also catches something the recorded row cannot: a bump made to the WRONG number. The
    /// row could be edited to any pair of integers; these identities are derived from the filesystem
    /// and the report and hold regardless of what the row says.</para>
    ///
    /// <para>⚠️ The identities assume <c>PairingFailure == 0</c>, and that is asserted rather than
    /// assumed — a pairing failure drops a file out of the index entirely, so it appears in NEITHER
    /// side of the equation and the arithmetic would close while an object went unexamined.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(Projects))]
    public void TheCensusArithmetic_Closes(string project)
    {
        var report = CommittedCorpus.Report(project);

        var pairingFailures = report.Entries
            .Where(e => e.Status == DriftStatus.PairingFailure)
            .Select(e => $"{e.Name}: {e.Detail}")
            .ToList();
        Assert.True(pairingFailures.Count == 0,
            $"{project}: two committed files claim one object identity, so a file is indexed on NEITHER " +
            "side and the census identities below would close while an object went unexamined:\n  " +
            string.Join("\n  ", pairingFailures));

        var irFiles = CommittedCorpus.IrFileCount(project);
        var exportFiles = CommittedCorpus.ExportFileCount(project);
        var compared = report.ComparedCount;
        var errors = CommittedCorpus.Count(report, DriftStatus.Error);
        var skipped = CommittedCorpus.Count(report, DriftStatus.Skipped);
        var exportOnly = CommittedCorpus.Count(report, DriftStatus.ExportOnly);
        var paired = compared + errors;

        Assert.True(irFiles == paired + skipped,
            $"{project}: the .ir side does not close. {irFiles} file(s) in ir/{project}/, but the report " +
            $"accounts for {paired} paired ({compared} compared + {errors} error) + {skipped} skipped = " +
            $"{paired + skipped}. Every .ir is either paired with an export or it is not; a shortfall " +
            "means drift-check's walk and the directory disagree about the population.");

        Assert.True(exportFiles == paired + exportOnly,
            $"{project}: the export side does not close. {exportFiles} file(s) in simatic-ml/{project}/, " +
            $"but the report accounts for {paired} paired + {exportOnly} export-only = " +
            $"{paired + exportOnly}. An export that is in neither bucket was not judged at all.");
    }

    /// <summary>
    /// Did-not-run guard on the census itself. A row deleted, or a project added to
    /// <see cref="CommittedCorpus.Projects"/> without a row, would make the theories above silently
    /// stop covering it — and a ratchet that has quietly stopped covering half the corpus is worse than
    /// no ratchet, because the green now asserts something narrower than it appears to.
    /// </summary>
    [Fact]
    public void EveryWalkedProject_HasACensusRow_AndViceVersa()
    {
        Assert.True(Recorded.Length > 0, "the census is empty — empty is not clean.");

        var walked = CommittedCorpus.Projects.OrderBy(p => p, StringComparer.Ordinal).ToArray();
        var rows = Recorded.Select(c => c.Project).OrderBy(p => p, StringComparer.Ordinal).ToArray();

        Assert.True(walked.SequenceEqual(rows, StringComparer.Ordinal),
            $"walked projects [{string.Join(", ", walked)}] and census rows [{string.Join(", ", rows)}] " +
            "differ. A project with no row is uncounted; a row for a project nobody walks asserts nothing.");

        foreach (var project in walked)
        {
            Assert.True(Directory.Exists(CommittedCorpus.IrDir(project)),
                $"missing corpus dir {CommittedCorpus.IrDir(project)}");
            Assert.True(Directory.Exists(CommittedCorpus.XmlDir(project)),
                $"missing exports dir {CommittedCorpus.XmlDir(project)}");
        }
    }

    /// <summary>
    /// The repo-wide totals, pinned in ONE place — because two other files quote them in prose and a
    /// number in a comment has no way of going red.
    ///
    /// <para><see cref="MemoryLayoutFidelityTests"/> says <i>"1 of the 58 committed .ir files declares a
    /// layout, while 28 of 41 committed exports do"</i>; <see cref="DerivedInterfaceBlindSideTests"/>
    /// says <i>"MEASURED over 41 pairs"</i> three times. Both 41s are CORRECT and mean the repo-wide
    /// export count. Pinning 58 / 41 / 41 here means the day the corpus moves, this fails and names the
    /// two files whose prose has just gone stale — which is what the MemoryLayout comment itself
    /// records happening once already, unnoticed, while the corpus more than doubled.</para>
    ///
    /// <para>⚠️ This asserts the totals only. It does NOT re-check the sentences those files carry
    /// around them — "1 of 58 declares a layout" and "28 of 41 exports do" are a different measurement
    /// with its own <c>[Fact]</c> in <see cref="MemoryLayoutFidelityTests"/>; what fails here is the
    /// population those fractions are taken over.</para>
    /// </summary>
    [Fact]
    public void TheRepoWideTotals_MatchTheRecordedCensus()
    {
        const int ExpectedIr = 65;        // 15 reference + 50 test-project001
        const int ExpectedExports = 41;   // 15 reference + 26 test-project001 — the real "41"
        const int ExpectedCompared = 41;  // every committed export pairs, in both projects

        var ir = Recorded.Sum(c => c.IrFiles);
        var exports = Recorded.Sum(c => c.Exports);
        var compared = Recorded.Sum(c => c.Compared);

        Assert.True(
            ir == ExpectedIr && exports == ExpectedExports && compared == ExpectedCompared,
            $"repo-wide corpus totals moved: .ir {ir} (was {ExpectedIr}), exports {exports} " +
            $"(was {ExpectedExports}), compared {compared} (was {ExpectedCompared}).\n" +
            "Two files quote these in PROSE and cannot go red on their own — update them in the same " +
            "commit:\n" +
            "  MemoryLayoutFidelityTests.cs (class comment: \"1 of the 58 committed .ir\", \"28 of 41 " +
            "committed exports\")\n" +
            "  DerivedInterfaceBlindSideTests.cs (three \"MEASURED ... over 41 pairs\" comments)\n" +
            "🔴 And keep the two 41s apart: 41 is the repo-wide EXPORT count. The denominator of " +
            "test-project001's drift comparison is 43 (26 compared + 17 unpaired), never 41 — 41 there " +
            "is 43 minus the two tag tables and is wrong.");
    }

    /// <summary>
    /// *** THE NEGATIVE CONTROL, AND THE ONLY THING THAT MAKES THE GREEN ABOVE MEAN ANYTHING. *** A
    /// census that reads 26 because the corpus holds 26, and a census that reads 26 because the walk
    /// stopped counting, are indistinguishable in a run log — this project has been bitten by exactly
    /// that often enough to have a slogan for it (EMPTY IS NOT CLEAN).
    ///
    /// <para>So the shortfall is DEMONSTRATED, not assumed: the whole <c>reference</c> corpus is copied
    /// to a temp dir, four exports are deleted, and the same <c>DriftCheckRunner</c> call the theories
    /// above use is run over it. <c>COMPARED</c> must fall by exactly four and <c>SKIPPED</c> must rise
    /// by exactly four — i.e. the loss lands where the recorded row would catch it, rather than
    /// vanishing into a bucket nobody asserts.</para>
    ///
    /// <para>The real corpus is never touched: everything happens under a fresh temp directory, and the
    /// <c>.ir</c> side is copied WHOLE so the callee/tag registries resolve exactly as they do against
    /// the committed project.</para>
    /// </summary>
    [Fact]
    public void TheCensus_SeesTheShortfall_WhenExportsAreDeleted()
    {
        var baseline = For("reference");
        var (irDir, xmlDir) = CopyCorpusToTemp("reference", nameof(TheCensus_SeesTheShortfall_WhenExportsAreDeleted));

        string[] removed = { "TimerSample.xml", "ThresholdAlarms.xml", "ScaleValue.xml", "DB_Timers.xml" };
        foreach (var file in removed)
        {
            var path = Path.Combine(xmlDir, file);
            Assert.True(File.Exists(path),
                $"the shortfall control expected to delete {file} and it is not in the reference corpus — " +
                "the control is not exercising what it claims to. Pick an export that exists.");
            File.Delete(path);
        }

        var report = DriftCheckRunner.Run(irDir, xmlDir);

        Assert.True(report.ComparedCount == baseline.Compared - removed.Length,
            $"deleting {removed.Length} export(s) from a copy of the reference corpus changed COMPARED to " +
            $"{report.ComparedCount}; expected {baseline.Compared - removed.Length}. If it did not fall, " +
            "the census is not measuring what it claims to and every recorded number above is decoration.");

        Assert.True(CommittedCorpus.Count(report, DriftStatus.Skipped) == baseline.UnpairedIr + removed.Length,
            $"the {removed.Length} orphaned .ir did not land in SKIPPED (got " +
            $"{CommittedCorpus.Count(report, DriftStatus.Skipped)}, expected " +
            $"{baseline.UnpairedIr + removed.Length}). The loss has to be visible in a bucket the census " +
            "asserts, or a shrinking corpus just moves quietly between two numbers nobody reads.");

        // And the recorded row is what would have caught it: the shortened corpus does not match it.
        Assert.True(report.ComparedCount != baseline.Compared,
            "the shortened corpus still matches the recorded reference row, so the ratchet would have " +
            "passed over a corpus that lost four exports.");
    }

    /// <summary>
    /// A full copy of one committed project into a fresh temp directory. Both sides are copied whole so
    /// the copy is a faithful starting point; the caller then removes exactly what it means to test.
    /// </summary>
    internal static (string IrDir, string XmlDir) CopyCorpusToTemp(string project, string scope)
    {
        var root = Path.Combine(Path.GetTempPath(), "corpus-census", scope, project);
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        var irDir = Path.Combine(root, "ir");
        var xmlDir = Path.Combine(root, "xml");
        Directory.CreateDirectory(irDir);
        Directory.CreateDirectory(xmlDir);

        foreach (var path in Directory.EnumerateFiles(CommittedCorpus.IrDir(project), "*.ir"))
        {
            File.Copy(path, Path.Combine(irDir, Path.GetFileName(path)));
        }

        foreach (var path in Directory.EnumerateFiles(CommittedCorpus.XmlDir(project), "*.xml"))
        {
            File.Copy(path, Path.Combine(xmlDir, Path.GetFileName(path)));
        }

        return (irDir, xmlDir);
    }
}
