using Converter.DriftCheck;
using Xunit;

namespace GoldenHarness;

/// <summary>
/// FI-26 (2026-07-20): the export-drift DETECTOR as an automated guard. `converter drift-check`
/// Normalizer-compares each committed <c>ir/&lt;proj&gt;/*.ir</c> against its paired
/// <c>simatic-ml/&lt;proj&gt;/&lt;name&gt;.xml</c>; this test asserts the set of DRIFTED blocks equals a
/// documented KNOWN-drift baseline — so the build stays green today but goes RED the moment any
/// currently-in-sync block silently drifts (a fix landed in the .ir but never re-exported — the exact
/// landmine that poisoned the synthesis-parity audit this session).
///
/// The complementary round-trip tests (CommittedBlocksRoundTripTests / FrozenAnswerKeyRoundTripTests)
/// deliberately *tolerate* this drift via an own-sidecar oracle; this is the detector they don't provide.
///
/// The baseline is the drift that already exists from the B-5/REQ-028 re-arming fix (and its interface
/// cascade into the instance DBs and DB_Settings) never being re-exported to <c>simatic-ml/</c>. This is
/// CONSCIOUSLY DEFERRED — docs/notes/deferred-items.md D-7 (owner, 2026-07-20). To clear an entry:
/// re-export that block from TIA, commit the fresh .xml, and remove it here. A NEW name appearing means a
/// block drifted unexpectedly — investigate, don't just add it.
/// </summary>
public class ExportDriftDetectorTests
{
    public static IEnumerable<object[]> Projects()
    {
        yield return new object[] { "reference", Array.Empty<string>() };
        yield return new object[]
        {
            "test-project001",
            new[]
            {
                "DB_Settings",
                "FB_PusherControl",
                "FB_ShredderSequencer",
                "iDB_MotorFwdRevSystem_Shredder",
                "iDB_PusherControl",
                "iDB_ShredderSequencer",
            },
        };
    }

    [Theory]
    [MemberData(nameof(Projects))]
    public void CommittedExports_DriftMatchesKnownBaseline(string project, string[] knownDrift)
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

        var drifted = report.Entries
            .Where(e => e.Status == DriftStatus.Drifted)
            .Select(e => e.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        var baseline = knownDrift.OrderBy(n => n, StringComparer.Ordinal).ToArray();

        Assert.True(
            drifted.SequenceEqual(baseline, StringComparer.Ordinal),
            $"{project}: export drift changed.\n" +
            $"  expected (known baseline): [{string.Join(", ", baseline)}]\n" +
            $"  actual now DRIFTED:        [{string.Join(", ", drifted)}]\n" +
            "A NEW name = a block's .ir changed but simatic-ml/ was never re-exported (investigate). " +
            "A MISSING name = it was re-exported and is now in sync (remove it from the baseline in this test).");
    }
}
