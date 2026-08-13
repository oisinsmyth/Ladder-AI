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
    /// <summary>Looked at, consciously accepted, clears when the block is re-exported. Never gates.</summary>
    Deferred,

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

        // ---- test-project001: the D-7 deferred re-export ---------------------------------------
        // The B-5/REQ-028 re-arming fix landed in the .ir and was never re-exported, plus its
        // interface cascade into the instance DBs. Consciously deferred by the owner, 2026-07-20 —
        // docs/notes/deferred-items.md D-7. To clear: re-export from TIA and commit the fresh .xml.
        new("DB_Settings", DriftDisposition.Deferred, "D-7 deferred re-export (owner, 2026-07-20)."),
        new("FB_PusherControl", DriftDisposition.Deferred, "D-7 deferred re-export (owner, 2026-07-20)."),
        new("FB_ShredderSequencer", DriftDisposition.Deferred, "D-7 deferred re-export (owner, 2026-07-20)."),

        // ---- 🔴 THREE ENTRIES REMOVED HERE, 2026-08-13, AND THE REASON THEY CARRIED WAS FALSE ----
        //
        // `iDB_MotorFwdRevSystem_Shredder`, `iDB_PusherControl` and `iDB_ShredderSequencer` sat here
        // as `Deferred` — "D-7: interface cascade from its FB", i.e. *their FB's fix was never
        // re-exported, so of course the instance DB differs; re-export clears it*. That reason is
        // WRONG, and it was wrong the whole time.
        //
        // The real cause was in OUR OWN WRITER: `DbSourceWriter` emitted Input, Output, Static for an
        // instance DB where TIA emits Input, Output, InOut, Static, and the Normalizer aligned
        // interface sections POSITIONALLY — so one missing EMPTY element slid Static into InOut's slot
        // and cascaded (26 localized differences on iDB_MotorFwdRevSystem_Shredder). *** NO RE-EXPORT
        // COULD EVER HAVE CLEARED IT ***, which is exactly what a deferred-re-export disposition
        // promises would happen.
        //
        // MEASURED, not argued, and by an authority that is not this fix: while it was being written,
        // ANOTHER LANE re-exported `iDB_PusherControl` and `iDB_ShredderSequencer` from TIA. Both
        // stayed DRIFTED under the pre-fix binary against their own fresh exports. And
        // `iDB_MotorFwdRevSystem_Shredder`, whose export has not been touched since 2026-07-17, went
        // to MATCH on the fix alone. Same corpus, two binaries: 5 drifted -> 1.
        //
        // This is the working agreement's "a plausible mechanism invented to explain" a result nobody
        // re-examined — the red twin of a false green. Once the cascade had a reason, the 26
        // differences stopped being read, and underneath them `drift-check` could not see these
        // blocks' actual content at all. Entries deleted rather than re-filed: there is no residue.

        // ---- test-project001: RULED, REPAIR UNFINISHED -----------------------------------------
        // Surfaced 2026-08-13 by BlockInterfaceFidelityTests (raw, Normalizer-free) and confirmed by
        // drift-check once ae62f76 landed. Both UDTs carry the 2026-07-17 C-115 handshake pass — an
        // `AutoStartSignal : Bool` member plus three member comments — which was never re-exported.
        //
        // *** THE RULING WAS REVERSED, AND THE SECOND ONE IS THE ONE IN FORCE. *** The first reading
        // was "remove the member, the committed export is the truth driver". Applying it left both
        // UDTs STILL drifted, because the same pass also added comments (UDT_PusherIO -> Cycling;
        // UDT_ShredderSequencerIO -> EnableUpstream, InCycle). Putting the bigger question up rather
        // than deleting three more lines is what produced the actual answer: THE THING THAT DRIFTED IS
        // ITSELF AN OWNER RULING, deliberately made and never carried into TIA.
        //
        // OWNER RULING (b), 2026-08-13, IN FORCE: THE IR WAS RIGHT AND THE EXPORT IS STALE. Task 09
        // should have reached the controller. The repair is therefore NOT a deletion — it is: restore
        // the member, import task 09 into the project, re-export, so simatic-ml/ finally reflects what
        // the IR has said since July. In flight with lad-coder (Portal).
        //
        // WHEN THAT LANDS, BOTH ENTRIES SHOULD BE DELETED OUTRIGHT — not re-filed as Deferred, and not
        // trimmed to some residue. The member coming back is part of the repair, not a new drift.
        //
        // *** AND DO NOT PREDICT THE RESULTING COUNT. *** It has been predicted twice on this exact
        // pair (6/17 both times, by two independent people) and measured 8/15 both times. Let the
        // drifted-set assertion above tell you what actually happened, and measure it.
        new("UDT_PusherIO", DriftDisposition.RepairInProgress,
            "Owner ruling (b) 2026-08-13: the IR is right and the EXPORT is stale — the 2026-07-17 C-115 pass (member `AutoStartSignal` + the `Cycling` comment) never reached TIA. Repair = restore, import task 09, re-export. Delete this entry when the fresh .xml is committed."),
        new("UDT_ShredderSequencerIO", DriftDisposition.RepairInProgress,
            "Owner ruling (b) 2026-08-13: the IR is right and the EXPORT is stale — the 2026-07-17 C-115 pass (member `AutoStartSignal` + the `EnableUpstream`/`InCycle` comments) never reached TIA. Repair = restore, import task 09, re-export. Delete this entry when the fresh .xml is committed."),
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
    /// *** THE GATE THE FLAT LIST COULD NOT PROVIDE. *** A ruling that has been made and not finished
    /// is not "known drift" — it is an unfinished repair, and the whole point of separating the two is
    /// that this one must not go quiet. Deferred and IncompleteExport entries never reach here.
    /// </summary>
    [Fact]
    public void NoKnownDrift_IsAnUnfinishedRepair()
    {
        var unfinished = Baseline
            .Where(d => d.Disposition == DriftDisposition.RepairInProgress)
            .OrderBy(d => d.Block, StringComparer.Ordinal)
            .ToList();

        Assert.True(unfinished.Count == 0,
            "A drift repair has been RULED but is not finished, so the baseline is carrying a decision " +
            "rather than a tolerance:\n  " +
            string.Join("\n  ", unfinished.Select(d => $"{d.Block} — {d.Reason}")) +
            "\n\nFinish the repair (then delete the entry), or — if the ruling has changed and this drift " +
            "is now accepted — re-file it as Deferred WITH the new ruling in its Reason. What must not " +
            "happen is that it stays here looking like something someone already agreed to.");
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
