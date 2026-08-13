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
        new("iDB_MotorFwdRevSystem_Shredder", DriftDisposition.Deferred, "D-7: interface cascade from its FB."),
        new("iDB_PusherControl", DriftDisposition.Deferred, "D-7: interface cascade from its FB."),
        new("iDB_ShredderSequencer", DriftDisposition.Deferred, "D-7: interface cascade from its FB."),

        // ---- test-project001: RULED, REPAIR UNFINISHED -----------------------------------------
        // Surfaced 2026-08-13 by BlockInterfaceFidelityTests (raw, Normalizer-free) and confirmed by
        // drift-check once ae62f76 landed. Both UDTs had been edited by the 2026-07-17 C-115 handshake
        // pass and never re-exported. Owner ruling: REMOVE THE MEMBER — THE COMMITTED EXPORT IS THE
        // TRUTH DRIVER.
        //
        // *** THE RULING WAS APPLIED TO THE MEMBER ONLY, AND THE SAME PASS ALSO ADDED COMMENTS. ***
        // `AutoStartSignal : Bool` is gone from both .ir files, and both are STILL drifted. Measured
        // with `converter compare` after the edit — the residue is member COMMENTS present in the .ir
        // and absent from the export:
        //     UDT_PusherIO             -> Cycling
        //     UDT_ShredderSequencerIO  -> EnableUpstream, InCycle
        // Same pass, same 2026-07-17 ruling, same never-re-exported cause. Applying the ruling as
        // stated removes those three comments too (lad-coder, hard rule 8 — not this lane's edit).
        new("UDT_PusherIO", DriftDisposition.RepairInProgress,
            "C-115 pass residue: the `Cycling` member COMMENT is in the .ir and not in the export. `AutoStartSignal` was removed; the comment was not."),
        new("UDT_ShredderSequencerIO", DriftDisposition.RepairInProgress,
            "C-115 pass residue: the `EnableUpstream` and `InCycle` member COMMENTS are in the .ir and not in the export. `AutoStartSignal` was removed; the comments were not."),
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
