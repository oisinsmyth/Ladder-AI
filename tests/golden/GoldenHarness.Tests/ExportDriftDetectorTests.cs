using Converter.DriftCheck;
using Xunit;

namespace GoldenHarness;

/// <summary>
/// How a known-drift entry got here. *** THE BASELINE USED TO BE A FLAT <c>string[]</c> OF NAMES, AND
/// THAT FLATNESS IS WHY TWO REAL UDT DRIFTS SAT INVISIBLE. *** A list that cannot say WHY a name is on
/// it cannot distinguish "we looked at this and decided to live with it" from "this turned up and
/// nobody has ruled on it" — and once the second kind is written down in the same shape as the first,
/// it stops being a question and becomes furniture (2026-08-13).
/// </summary>
public enum DriftDisposition
{
    /// <summary>
    /// Looked at, consciously accepted, clears when the block is re-exported. Never gates.
    ///
    /// *** A Deferred REASON CAN GO STALE WITHOUT THE ENTRY MOVING, AND THAT HAS NOW HAPPENED TWICE. ***
    /// It says "our .ir is ahead of the corpus and we will re-export later", which is a claim about the
    /// REFERENCE DATA. If the block is re-exported and STILL drifts, that claim is disproven and the
    /// entry must be re-filed — it is not a Deferred any more, whatever it says.
    ///
    /// Both prior cases: the three instance DBs sat here as "interface cascade from its FB" when the
    /// real cause was `converter to-xml` omitting the empty InOut section every real TIA instance-DB
    /// export declares — OUR DEFECT, FILED AS A PROPERTY OF THE ANSWER KEY, WHICH MAKES IT A CLOSED
    /// QUESTION (converter `f2a548a`). And `DB_Settings` sat here as "D-7 deferred re-export" through a
    /// fresh live re-export that did not clear it — see its entry below.
    /// </summary>
    Deferred,

    /// <summary>
    /// A real divergence that NOBODY HAS RULED ON. *** THIS GATES. *** It is deliberately not
    /// <see cref="Deferred"/>: "deferred" claims a decision was made, and writing an undecided thing in
    /// the shape of a decided one is the whole failure this enum exists to prevent. An entry here is a
    /// question waiting for an owner, not a tolerance.
    /// </summary>
    Unruled,

    /// <summary>
    /// The committed EXPORT is not a faithful TIA artifact, so the divergence is in the answer key and
    /// not in our output. Clears on a fresh export. Never gates.
    ///
    /// *** THIS IS A CLAIM ABOUT THE CORPUS, NOT ABOUT THE CONVERTER, AND IT IS THE DISPOSITION MOST
    /// EASILY REACHED FOR AS AN EXCUSE *** — "our output is right and the answer key is wrong" is what
    /// every failing comparison feels like from the inside. So it is the one disposition with a
    /// MECHANISED EVIDENCE BAR rather than a precedent to point at; see
    /// <see cref="ExportDriftDetectorTests.EveryIncompleteExportClaim_MeetsTheEvidenceBar"/>.
    /// </summary>
    IncompleteExport,

    /// <summary>
    /// An owner ruling EXISTS and the repair is not finished. *** THIS ONE GATES. *** It is the state
    /// the flat list could not express, and leaving it silent is exactly how a decided-but-unfinished
    /// repair becomes a permanent baseline entry.
    /// </summary>
    RepairInProgress,
}

public sealed record KnownDrift(string Block, DriftDisposition Disposition, string Reason);

/// <summary>
/// FI-26 (2026-07-20): the export-drift DETECTOR as an automated guard. <c>converter drift-check</c>
/// Normalizer-compares each committed <c>ir/&lt;proj&gt;/*.ir</c> against its paired
/// <c>simatic-ml/&lt;proj&gt;/&lt;name&gt;.xml</c>; this asserts the DRIFTED set equals a documented
/// baseline, so the build goes red the moment a currently-in-sync block silently drifts.
///
/// <para>The complementary round-trip suites (<see cref="CommittedBlocksRoundTripTests"/>,
/// <see cref="FrozenAnswerKeyRoundTripTests"/>) deliberately TOLERATE this drift via an own-sidecar
/// oracle; this is the detector they do not provide.</para>
///
/// <para><b>2026-08-13 — the baseline is no longer a bare list of names.</b> Two things happened on the
/// same day that it could not have described. First, converter <c>ae62f76</c> made an
/// <c>&lt;Interface&gt;</c> compared rather than discarded, and the drifted set immediately grew by
/// three: a UDT's interface IS its whole definition, so until that fix <c>drift-check</c> reported
/// <c>MATCH</c> on any UDT no matter what had changed. Second, the repair for two of those was ruled
/// and started but is NOT COMPLETE — and a flat list would have swallowed that fact whole. See
/// <see cref="DriftDisposition"/>.</para>
/// </summary>
public class ExportDriftDetectorTests
{
    private static readonly KnownDrift[] Baseline =
    {
        // ---- reference -------------------------------------------------------------------------
        new("NodeStatusAlarms", DriftDisposition.IncompleteExport,
            "reference: one of four 2026-07-10 seed artifacts committed with TIA's scaffolding trimmed (they carry no <DocumentInfo> either). On this FC the <Interface> went with the trim, so the export has none while any regenerated FC emits the standard boilerplate. `converter compare` localises exactly ONE difference, ELEMENT-ADDED at /Document/SW.Blocks.FC/AttributeList/Interface — an ADDITION of TIA's own defaults. Our output is right; the answer key is incomplete. Clears on a fresh export. Became visible only with ae62f76."),

        // ---- test-project001: D-7 IS DISCHARGED, AND WHAT REMAINS IS NOT D-7 -------------------
        //
        // The D-7 deferred re-export (owner, 2026-07-20 - docs/notes/deferred-items.md) listed six
        // blocks. FIVE ARE NOW IN SYNC and are gone from here: the three instance DBs (removed
        // 2026-08-13 when converter `f2a548a` showed their reason had never been true - see the block
        // comment below), and `FB_PusherControl` / `FB_ShredderSequencer`, cleared by the live
        // re-export committed in `f0fb0cb`. MEASURED after that landed: test-project001 drops to
        // 1 drifted / 25 match / 0 skipped.
        //
        // The sixth, `DB_Settings`, SURVIVED ITS OWN RE-EXPORT - so "deferred re-export" is disproven
        // for it and re-filing is not optional. See its entry.

        // ---- 🔴 THREE ENTRIES REMOVED HERE, 2026-08-13, AND THE REASON THEY CARRIED WAS FALSE ----
        //
        // `iDB_MotorFwdRevSystem_Shredder`, `iDB_PusherControl` and `iDB_ShredderSequencer` sat here
        // as `Deferred` - "D-7: interface cascade from its FB", i.e. *their FB's fix was never
        // re-exported, so of course the instance DB differs; re-export clears it*. That reason is
        // WRONG, and it was wrong the whole time.
        //
        // The real cause was in OUR OWN WRITER: `DbSourceWriter` emitted Input, Output, Static for an
        // instance DB where TIA emits Input, Output, InOut, Static, and the Normalizer aligned
        // interface sections POSITIONALLY - so one missing EMPTY element slid Static into InOut's slot
        // and cascaded (26 localized differences on iDB_MotorFwdRevSystem_Shredder). *** NO RE-EXPORT
        // COULD EVER HAVE CLEARED IT ***, which is exactly what a deferred-re-export disposition
        // promises would happen. Kept as the worked example of why `Deferred` is a claim with an
        // expiry date. (Diagnosis and fix: converter lane, `f2a548a`.)

        // ---- test-project001: REAL, MEASURED, AND NOBODY HAS RULED ON IT ------------------------
        //
        // `DB_Settings` is the one block still drifting after the live re-export, and it is NOT the
        // deferred re-export its old reason claimed - that claim died when `f0fb0cb` refreshed the
        // corpus from a live dump and this block stayed red. Re-filed rather than left sitting under a
        // disposition that had been disproven, which is the exact mistake the instance DBs above
        // record.
        //
        // MEASURED with `converter compare` (2026-08-13), .ir -> to-xml against the fresh export:
        // EXACTLY ONE difference, VALUE-DIFFERS at
        //   /Document/SW.Blocks.GlobalDB/ObjectList/MultilingualText/ObjectList/MultilingualTextItem/AttributeList/Text
        // i.e. the DB's own block comment. The two texts disagree about whether a decision happened:
        //   export (live controller): "...Fix-wave-1 (2026-07-16) proposed defaults ... PENDING THE
        //                              OWNER'S SETTINGS SIGN-OFF (gen/GenProject1/fix-wave-1.md is the
        //                              signature page)."
        //   .ir                     : "...were given site-practice defaults and SIGNED OFF 2026-07-16."
        //
        // *** THAT IS A CONTENT DIFFERENCE, SO IT IS NOT IncompleteExport *** (bar condition 4 - and
        // the export carries a <DocumentInfo>, so it fails condition 1 too). And it is structurally the
        // SAME CLASS as the C-115/task-09 case that was just repaired under ruling (b): an IR that
        // records a decision the controller has never been told about. That parallel is why this is
        // Unruled and not quietly tolerated - but it is a PARALLEL, not a ruling, and the same question
        // got two different answers last time.
        //
        // NOT VERIFIED BY THIS LANE: the third leg. This measures .ir vs the committed export. Whether
        // the LIVE project agrees with its own export needs Portal, which this lane does not have.
        //
        // The decision needed is one line: which text is true - has fix-wave-1 been signed off? If yes,
        // the controller comment is stale and this is ruling (b) again. If no, the .ir overstates it.
        new("DB_Settings", DriftDisposition.Unruled,
            "One VALUE-DIFFERS, the DB's block comment: the .ir says fix-wave-1 was SIGNED OFF 2026-07-16, the live controller still says PENDING THE OWNER'S SIGN-OFF. Survived the f0fb0cb re-export, so the old 'D-7 deferred re-export' reason is disproven. Needs an owner ruling on which is true; same shape as the C-115 case ruled (b). Third leg (live project vs its export) not checked - no Portal in this lane."),
    };

    public static IEnumerable<object[]> Projects()
    {
        yield return new object[] { "reference" };
        yield return new object[] { "test-project001" };
    }

    private static string[] BaselineFor(string project) =>
        Baseline.Where(d => ProjectOf(d.Block) == project)
            .Select(d => d.Block)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

    private static string ProjectOf(string block) =>
        Directory.Exists(Path.Combine(ToolPaths.RepoRoot(), "ir", "reference"))
        && File.Exists(Path.Combine(ToolPaths.RepoRoot(), "ir", "reference", block + ".ir"))
            ? "reference"
            : "test-project001";

    [Theory]
    [MemberData(nameof(Projects))]
    public void CommittedExports_DriftMatchesKnownBaseline(string project)
    {
        var repo = ToolPaths.RepoRoot();
        var irDir = Path.Combine(repo, "ir", project);
        var xmlDir = Path.Combine(repo, "simatic-ml", project);
        Assert.True(Directory.Exists(irDir), $"missing corpus dir {irDir}");
        Assert.True(Directory.Exists(xmlDir), $"missing exports dir {xmlDir}");

        var report = DriftCheckRunner.Run(irDir, xmlDir);

        // An ERROR means a committed .ir could not be converted to XML at all — a real regression,
        // never expected in the committed corpus.
        var errors = report.Entries.Where(e => e.Status == DriftStatus.Error).Select(e => $"{e.Name}: {e.Detail}").ToList();
        Assert.True(errors.Count == 0, $"{project}: unexpected conversion errors — {string.Join("; ", errors)}");

        // Empty is not clean: a run that examined nothing must not read as a run that found nothing.
        Assert.True(report.Entries.Count > 0, $"{project}: drift-check examined no files at all.");

        var drifted = report.Entries
            .Where(e => e.Status == DriftStatus.Drifted)
            .Select(e => e.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        var baseline = BaselineFor(project);

        Assert.True(
            drifted.SequenceEqual(baseline, StringComparer.Ordinal),
            $"{project}: export drift changed.\n" +
            $"  expected (documented baseline): [{string.Join(", ", baseline)}]\n" +
            $"  actual now DRIFTED:             [{string.Join(", ", drifted)}]\n" +
            "A NEW name = a block's .ir changed but simatic-ml/ was never re-exported — investigate, and " +
            "when you add it, ADD IT WITH A DriftDisposition. A bare name says nothing about whether " +
            "anyone decided to live with it, and that is what let two UDT drifts sit invisible.\n" +
            "A MISSING name = it was re-exported or repaired and is now in sync (remove the entry).");
    }

    /// <summary>
    /// *** THE GATE THE FLAT LIST COULD NOT PROVIDE. *** Two states must not go quiet: a ruling that was
    /// made and not finished (<see cref="DriftDisposition.RepairInProgress"/>), and a real divergence
    /// nobody has ruled on (<see cref="DriftDisposition.Unruled"/>). Both are QUESTIONS. `Deferred` and
    /// `IncompleteExport` are ANSWERS, and never reach here.
    ///
    /// <para>Yes, this can hold the suite red. That is the trade, made deliberately: the alternative is
    /// an open question written in the shape of a settled one, which is how the instance-DB defect
    /// survived a re-export without being re-examined. The gate is scoped so it can only ever be held
    /// red by an entry someone has to make ONE decision about — it is not a bucket that fills up.</para>
    /// </summary>
    [Fact]
    public void NoDriftEntry_IsAnOpenQuestion()
    {
        var open = Baseline
            .Where(d => d.Disposition is DriftDisposition.RepairInProgress or DriftDisposition.Unruled)
            .OrderBy(d => d.Block, StringComparer.Ordinal)
            .ToList();

        Assert.True(open.Count == 0,
            "The drift baseline is carrying an open QUESTION rather than a tolerance:\n  " +
            string.Join("\n  ", open.Select(d => $"{d.Block} [{d.Disposition}] — {d.Reason}")) +
            "\n\nRepairInProgress: finish the repair, then DELETE the entry.\n" +
            "Unruled: get the ruling, then re-file as Deferred/IncompleteExport WITH it, or repair and " +
            "delete.\nWhat must not happen is that it stays here looking like something someone already " +
            "agreed to — that is exactly how the three instance DBs survived a re-export unexamined.");
    }

    /// <summary>
    /// *** THE EVIDENCE BAR FOR <see cref="DriftDisposition.IncompleteExport"/>, MECHANISED. ***
    ///
    /// <para>"Our output is right and the answer key is wrong" is what EVERY failing comparison feels
    /// like from the inside, so this disposition would otherwise become the excuse of choice, and
    /// `NodeStatusAlarms` would become the precedent people point at rather than the standard they have
    /// to meet. What earned it there was specific and checkable, so it is checked:</para>
    ///
    /// <list type="number">
    /// <item><b>The committed export is independently identifiable as NOT a faithful TIA artifact.</b>
    /// The marker this corpus has is a missing <c>&lt;DocumentInfo&gt;</c> envelope — every genuine V20
    /// export carries one, and the four 2026-07-10 seed files were committed with TIA's scaffolding
    /// trimmed off. That is a property of the FILE, checkable without any opinion about our output, and
    /// it is what this test enforces.</item>
    /// <item><b>A genuine peer export exists.</b> At least one <c>&lt;DocumentInfo&gt;</c>-carrying
    /// export of the same root kind must be in the corpus — otherwise "TIA would have carried this
    /// element" is an assertion with nothing behind it. (`ScaleValue.xml` is the peer that proves a real
    /// FC export carries an <c>&lt;Interface&gt;</c>.)</item>
    /// </list>
    ///
    /// <para>Two further conditions cannot be mechanised here and are the AUTHOR's to satisfy and record
    /// in the Reason — state them or do not use this disposition:</para>
    /// <list type="number">
    /// <item><b>The difference is LOCALISED AND ENUMERATED</b>, with <c>converter compare</c>, not
    /// "VERDICT: DIFFERS". `NodeStatusAlarms` was ONE difference and the Reason says which.</item>
    /// <item><b>Every difference is an ADDITION of TIA's own defaults</b> — an element the trimmed
    /// export lacks and a genuine one carries. *** IF ANY DIFFERENCE IS A CONTENT CHANGE — A MEMBER, A
    /// VALUE, A WIRE, A TYPE — IT IS NEVER IncompleteExport, *** however sure you are that we are right.
    /// That is a drift, and the two UDTs above are what happens when you rule on one without asking why
    /// it is there.</item>
    /// </list>
    /// </summary>
    [Fact]
    public void EveryIncompleteExportClaim_MeetsTheEvidenceBar()
    {
        var repo = ToolPaths.RepoRoot();
        var allExports = ExportRoundTripRunner.CommittedExports().ToList();
        Assert.True(allExports.Count > 0, "no committed exports found — empty is not clean");

        // Vacuity guard: the marker only discriminates if some exports actually carry it. If nothing in
        // the corpus had a <DocumentInfo>, this test would wave everything through.
        var faithful = allExports.Where(e => File.ReadAllText(e.ExportPath).Contains("<DocumentInfo")).ToList();
        Assert.True(faithful.Count > 0,
            "no committed export carries a <DocumentInfo>, so 'missing DocumentInfo' distinguishes " +
            "nothing and this bar is vacuous. Empty is not clean.");

        // Every site that can file an IncompleteExport claim, so the bar cannot be dodged by filing in
        // the easiest of the three.
        var claims = Baseline.Where(d => d.Disposition == DriftDisposition.IncompleteExport).Select(d => d.Block)
            .Concat(CommittedBlocksRoundTripTests.KnownIncompleteAnswerKeys.Keys)
            .Concat(SynthesisParityTests.KnownIncompleteAnswerKey.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        foreach (var block in claims)
        {
            var export = allExports.FirstOrDefault(e => e.Name == block);
            Assert.True(export.Name is not null,
                $"'{block}' is filed as IncompleteExport but has no committed export at all — that is a " +
                "missing answer key, not an incomplete one, and belongs in KnownMissingExports.");

            Assert.True(
                !File.ReadAllText(export.ExportPath).Contains("<DocumentInfo", StringComparison.Ordinal),
                $"'{block}' is filed as IncompleteExport, but its committed export CARRIES a " +
                "<DocumentInfo> envelope — i.e. it is a faithful TIA artifact and there is no corpus " +
                "defect to blame. The difference is therefore ours until proven otherwise. " +
                "'Our output is right and the answer key is wrong' is what every failing comparison " +
                "feels like from the inside; this bar exists so that it has to be shown rather than felt.");

            var kind = RootKindOf(export.ExportPath);
            Assert.True(
                faithful.Any(e => RootKindOf(e.ExportPath) == kind),
                $"'{block}' is filed as IncompleteExport (root <{kind}>) but the corpus holds no genuine " +
                $"<DocumentInfo>-carrying export of that kind, so there is nothing to establish what a " +
                "real TIA export of it looks like. Without a peer, 'the answer key is incomplete' is an " +
                "assertion with nothing behind it.");
        }
    }

    private static string RootKindOf(string exportPath)
    {
        using var reader = new StreamReader(exportPath);
        var doc = System.Xml.Linq.XDocument.Load(reader);
        return doc.Root?.Elements()
            .Select(e => e.Name.LocalName)
            .FirstOrDefault(n => n.StartsWith("SW.", StringComparison.Ordinal)) ?? "?";
    }

    /// <summary>
    /// Did-not-run guard on the baseline itself: an entry naming a block that no longer exists asserts
    /// nothing, and would keep the drifted-set comparison passing for the wrong reason.
    /// </summary>
    [Fact]
    public void EveryBaselineEntry_NamesABlockThatExists()
    {
        var repo = ToolPaths.RepoRoot();
        Assert.True(Baseline.Length > 0, "the drift baseline is empty — empty is not clean");

        var missing = Baseline
            .Where(d => !File.Exists(Path.Combine(repo, "ir", ProjectOf(d.Block), d.Block + ".ir")))
            .Select(d => d.Block)
            .ToList();

        Assert.True(missing.Count == 0,
            $"drift-baseline entries name blocks with no committed .ir: [{string.Join(", ", missing)}]. " +
            "Remove them — an entry for a block that does not exist can never come back into sync, so it " +
            "would sit here forever.");
    }
}
