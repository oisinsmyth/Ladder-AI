using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using DownloadProbe;
using Ladder.Download;

// `TransferVerdict` exists in BOTH namespaces and they are different types: DownloadProbe's is the
// probe's own message-text classifier, Ladder.Download's is the manifest-keyed enum this JSON
// reports. Fully qualified below rather than aliased, so which one is meant is never a guess.
using Xunit;

using ProbeProgram = DownloadProbe.Program;

namespace OpennessCli.Tests;

/// <summary>
/// THE LOAD MANIFEST IN `--json`, AS DATA (2026-08-13).
///
/// *** WHY THIS EXISTS. *** The manifest is the ONLY positive evidence that a download carried
/// anything — <c>DownloadResult.State</c> was <c>Success</c> on a live run that transferred nothing —
/// so it is the value the harness's `Loaded` verdict keys on, and the most load-bearing thing this
/// binary produces. Until now the only way another process could reach it was to RE-PARSE THE
/// RENDERED LOG embedded in the JSON: the deployment gateway targets net8.0 and must never link
/// <c>Siemens.Engineering</c> (or every harness build joins the (Path,FileHash) approval cycle), so
/// it was confined to the probe's own prose, and anything the renderer dropped was invisible to it.
///
/// *** THE FIXTURES ARE RECORDED LIVE-RIG DOWNLOADS, NOT HAND-AUTHORED. *** They are read from
/// `src/download-feedback/DownloadFeedback.Tests/Fixtures/`, each the artifact of a real download
/// that cost a CPU stop and a restart. A missing one is a FAILURE, never a skip — a fixture that
/// silently vanished would turn every assertion below into a vacuous pass.
/// </summary>
public class DownloadProbeManifestJsonTests
{
    /// <summary>
    /// A run whose session produced <paramref name="feedback"/>, parsed back out of the JSON on
    /// stdout. Everything before the session — including the fence — really runs.
    /// </summary>
    private static JsonElement ManifestFrom(DownloadFeedback? feedback, string? source)
    {
        using var repo = ProbeFenceRepo.Permitting();
        var logDir = Path.Combine(Path.GetTempPath(), "probe-manifest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(logDir);
        var stdout = new StringWriter();

        ProbeProgram.Run(
            new[] { repo.ProjectPath, "--options", "SoftwareOnlyChanges", "--json", "--log-dir", logDir },
            stdout,
            new StringWriter(),
            (_, _) => new ProbeOutcome(ProbeExitCodes.Completed, "stub")
            {
                Feedback = feedback,
                FeedbackSource = source,
            },
            repo.BinaryDirectory);

        using var document = JsonDocument.Parse(stdout.ToString());
        return document.RootElement.GetProperty("loadManifest").Clone();
    }

    private static DownloadFeedback FromRecordedRun(string fixtureName) =>
        DownloadFeedbackParser.Parse(ProbeLogReader.Read(ReadFixture(fixtureName)));

    private static string ReadFixture(string name)
    {
        var repoRoot = ScratchProjectGuard.FindRepoRoot(AppContext.BaseDirectory);
        Assert.NotNull(repoRoot);

        var path = Path.Combine(
            repoRoot!, "src", "download-feedback", "DownloadFeedback.Tests", "Fixtures", name);

        Assert.True(
            File.Exists(path),
            $"the recorded download '{name}' is missing: {path}. It is the evidence these assertions rest on, "
            + "so its absence is a failure and never a skip.");

        return File.ReadAllText(path);
    }

    // ---- the manifest is first-class, and it is the real thing -----------------------------------

    /// <summary>
    /// A measured differential download: ONE object, named. The names come out of the JSON as a
    /// list, not out of a sentence a consumer has to parse.
    /// </summary>
    [Fact]
    public void TheManifestIsEmittedAsData_WithTheObjectNamesFromARecordedDownload()
    {
        var manifest = ManifestFrom(FromRecordedRun("differential-one-object.txt"), nameof(ProbeLogReader));

        Assert.True(manifest.GetProperty("available").GetBoolean());
        Assert.Equal(nameof(Ladder.Download.TransferVerdict.Transferred), manifest.GetProperty("verdict").GetString());
        Assert.Equal(1, manifest.GetProperty("loadedObjectCount").GetInt32());
        Assert.Equal(
            new[] { "DB_Data01" },
            manifest.GetProperty("loadedObjects").EnumerateArray().Select(e => e.GetString()).ToArray());

        // The source is REPORTED, not assumed: this feedback was re-derived from a rendering, and the
        // JSON says so rather than claiming the first-hand path.
        Assert.Equal(nameof(ProbeLogReader), manifest.GetProperty("source").GetString());
    }

    /// <summary>
    /// The full run, at its measured size. A stub or a truncation would not survive this number.
    /// </summary>
    [Fact]
    public void TheManifestCarriesEveryObject_OnAFullDownload()
    {
        var manifest = ManifestFrom(FromRecordedRun("full-ninety-nine-objects.txt"), nameof(ProbeLogReader));

        Assert.Equal(99, manifest.GetProperty("loadedObjectCount").GetInt32());
        Assert.Equal(99, manifest.GetProperty("loadedObjects").GetArrayLength());
        Assert.Equal(nameof(Ladder.Download.TransferVerdict.Transferred), manifest.GetProperty("verdict").GetString());
        Assert.True(manifest.GetProperty("transferredItemCount").GetInt32() >= 99);
    }

    /// <summary>
    /// *** THE THREE OUTCOMES THAT MUST NEVER RENDER ALIKE. *** "TIA said it skipped the transfer",
    /// "TIA named nothing and said nothing" and "there was no result to read at all" are different
    /// findings, and the middle and last ones are the pair that gets read as the first.
    /// </summary>
    [Fact]
    public void NothingTransferred_AndNothingExamined_AreDifferentInTheJson()
    {
        var upToDate = ManifestFrom(FromRecordedRun("up-to-date-nothing-transferred.txt"), nameof(ProbeLogReader));

        Assert.True(upToDate.GetProperty("available").GetBoolean());
        Assert.Equal(nameof(Ladder.Download.TransferVerdict.NothingTransferred), upToDate.GetProperty("verdict").GetString());
        Assert.Equal(0, upToDate.GetProperty("loadedObjectCount").GetInt32());
        Assert.True(upToDate.GetProperty("upToDateSignalPresent").GetBoolean());

        // An abort: no DownloadResult was produced at all.
        var aborted = ManifestFrom(FromRecordedRun("aborted-no-download-result.txt"), nameof(ProbeLogReader));
        Assert.Equal(nameof(Ladder.Download.TransferVerdict.Undetermined), aborted.GetProperty("verdict").GetString());
        Assert.False(aborted.GetProperty("resultPresent").GetBoolean());
    }

    /// <summary>
    /// *** EMPTY IS NOT CLEAN (FI-44), AT THE JSON BOUNDARY. *** When no manifest was derived at all,
    /// every list and count is NULL — never an empty array, which a consumer would read as "TIA
    /// loaded nothing". <c>available:false</c> is the flag that says nobody looked.
    /// </summary>
    [Fact]
    public void WhenNoResultExisted_TheManifestIsNull_NotEmpty()
    {
        var manifest = ManifestFrom(feedback: null, source: null);

        Assert.False(manifest.GetProperty("available").GetBoolean());
        Assert.Equal("none", manifest.GetProperty("source").GetString());
        Assert.Equal(nameof(Ladder.Download.TransferVerdict.Undetermined), manifest.GetProperty("verdict").GetString());
        Assert.Contains("NOTHING WAS EXAMINED", manifest.GetProperty("verdictReason").GetString()!, StringComparison.Ordinal);

        foreach (var key in new[]
        {
            "loadedObjects", "loadedObjectCount", "loadedObjectMessageCount", "duplicateLoadedObjects",
            "nonObjectLoadSubjects", "nonObjectLoadCount", "transferredItemCount", "runStateDisclosed",
            "runStateTransitions", "finalRunStateEvent", "upToDateSignalPresent", "unrecognisedMessages",
            "unrecognisedMessageCount", "anomalies",
        })
        {
            Assert.Equal(JsonValueKind.Null, manifest.GetProperty(key).ValueKind);
        }
    }

    /// <summary>
    /// Run state is carried as data too, and its DISCLOSURE is carried separately from its content.
    /// An empty transition list means the download said nothing about run state — it does NOT mean
    /// the CPU kept running, and a consumer must be able to tell those apart.
    /// </summary>
    [Fact]
    public void RunStateIsCarried_WithDisclosureSeparateFromContent()
    {
        var manifest = ManifestFrom(FromRecordedRun("full-ninety-nine-objects.txt"), nameof(ProbeLogReader));

        Assert.True(manifest.TryGetProperty("runStateDisclosed", out var disclosed));
        Assert.True(disclosed.ValueKind is JsonValueKind.True or JsonValueKind.False);
        Assert.Equal(JsonValueKind.Array, manifest.GetProperty("runStateTransitions").ValueKind);

        if (disclosed.GetBoolean())
        {
            Assert.NotEqual(0, manifest.GetProperty("runStateTransitions").GetArrayLength());
            Assert.Equal(JsonValueKind.String, manifest.GetProperty("finalRunStateEvent").ValueKind);
        }
    }

    /// <summary>
    /// Messages this parser does not recognise are CARRIED, never dropped — every download option
    /// measured so far produced a vocabulary nobody predicted, and this list being non-empty is the
    /// signal that the parser has gone out of date. It can only be that signal if it is reported.
    /// </summary>
    [Fact]
    public void UnrecognisedMessagesReachTheJson_RatherThanBeingCountedAsNothing()
    {
        var manifest = ManifestFrom(
            FromRecordedRun("unrecognised-vocabulary-SYNTHETIC.txt"), nameof(ProbeLogReader));

        Assert.NotEqual(0, manifest.GetProperty("unrecognisedMessageCount").GetInt32());
        Assert.NotEqual(0, manifest.GetProperty("unrecognisedMessages").GetArrayLength());
        Assert.NotEqual(0, manifest.GetProperty("anomalies").GetArrayLength());
    }

    /// <summary>
    /// The keys are <c>Ladder.Download.DownloadFeedback</c>'s own property names, camel-cased — the
    /// vocabulary both existing consumers already speak. Pinned so that a rename here becomes a
    /// deliberate act with a downstream conversation, rather than a silent break in another process
    /// that only shows up on a rig.
    /// </summary>
    [Fact]
    public void TheKeysAreTheFeedbackTypesOwnVocabulary()
    {
        var manifest = ManifestFrom(FromRecordedRun("differential-one-object.txt"), nameof(ProbeLogReader));

        var names = manifest.EnumerateObject().Select(p => p.Name).ToList();

        foreach (var expected in new[]
        {
            "available", "source", "resultPresent", "verdict", "verdictReason",
            "loadedObjects", "loadedObjectCount", "loadedObjectMessageCount", "duplicateLoadedObjects",
            "nonObjectLoadSubjects", "nonObjectLoadCount", "transferredItemCount",
            "runStateDisclosed", "runStateTransitions", "finalRunStateEvent",
            "upToDateSignalPresent", "unrecognisedMessages", "unrecognisedMessageCount", "anomalies",
        })
        {
            Assert.Contains(expected, names);
        }

        // Each of these is a real member of DownloadFeedback — so the JSON cannot drift into naming
        // something that type does not have without this failing.
        foreach (var member in new[]
        {
            nameof(DownloadFeedback.LoadedObjects), nameof(DownloadFeedback.LoadedObjectCount),
            nameof(DownloadFeedback.LoadedObjectMessageCount), nameof(DownloadFeedback.DuplicateLoadedObjects),
            nameof(DownloadFeedback.NonObjectLoadSubjects), nameof(DownloadFeedback.NonObjectLoadCount),
            nameof(DownloadFeedback.TransferredItemCount), nameof(DownloadFeedback.RunStateDisclosed),
            nameof(DownloadFeedback.RunStateTransitions), nameof(DownloadFeedback.FinalRunStateEvent),
            nameof(DownloadFeedback.UpToDateSignalPresent), nameof(DownloadFeedback.UnrecognisedMessages),
            nameof(DownloadFeedback.UnrecognisedMessageCount), nameof(DownloadFeedback.Anomalies),
            nameof(DownloadFeedback.Verdict), nameof(DownloadFeedback.VerdictReason),
            nameof(DownloadFeedback.ResultPresent),
        })
        {
            var camel = char.ToLowerInvariant(member[0]) + member.Substring(1);
            Assert.Contains(camel, names);
        }
    }

    /// <summary>
    /// The embedded log stays. Removing it would break the existing consumer on the day this landed,
    /// and the verbatim configuration text is this tool's other product — the manifest is an ADDITION,
    /// not a replacement.
    /// </summary>
    [Fact]
    public void TheEmbeddedLogIsStillThere_SoTheChangeIsAdditive()
    {
        using var repo = ProbeFenceRepo.Permitting();
        var logDir = Path.Combine(Path.GetTempPath(), "probe-manifest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(logDir);
        var stdout = new StringWriter();

        ProbeProgram.Run(
            new[] { repo.ProjectPath, "--options", "SoftwareOnlyChanges", "--json", "--log-dir", logDir },
            stdout,
            new StringWriter(),
            (_, log) =>
            {
                log.Line("MARKER-LINE-IN-THE-SESSION");
                return new ProbeOutcome(ProbeExitCodes.Completed, "stub");
            },
            repo.BinaryDirectory);

        using var document = JsonDocument.Parse(stdout.ToString());
        Assert.Equal(JsonValueKind.Array, document.RootElement.GetProperty("log").ValueKind);
        Assert.Contains(
            document.RootElement.GetProperty("log").EnumerateArray(),
            e => (e.GetString() ?? string.Empty).Contains("MARKER-LINE-IN-THE-SESSION"));

        // And the three keys the gateway reads today are untouched.
        foreach (var key in new[] { "transferVerdict", "softwareLoaded", "logFile" })
        {
            Assert.True(document.RootElement.TryGetProperty(key, out _), $"'{key}' disappeared from the envelope.");
        }
    }
}
