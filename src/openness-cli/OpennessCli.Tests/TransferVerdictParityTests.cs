using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using DownloadProbe;
using Ladder.Download;
using Xunit;

namespace OpennessCli.Tests;

/// <summary>
/// TWO IMPLEMENTATIONS OF ONE RULE — measured against each other on every real download this
/// repository has recorded.
///
/// `download-probe` grew its own transfer classifier (<c>TransferVerdicts</c>, 2026-08-12) and
/// `Ladder.Download`'s <c>DownloadFeedbackParser</c> landed later the SAME DAY, written to replace
/// it: its remarks name the two defects it excludes by construction — inferring a transfer from the
/// ABSENCE of an up-to-date phrase, and reading <c>state=Success</c> as evidence. The probe's copy
/// was left in place.
///
/// *** THE DUPLICATION IS NOT THE DEFECT. THE DIVERGENCE IS. *** So this file exists to answer one
/// question with data rather than with reasoning: <b>do they agree on real logs?</b> It is kept after
/// the answer, because the probe's verdict is now DERIVED from the library and this is what proves
/// the derivation did not quietly reintroduce a second opinion.
/// </summary>
public class TransferVerdictParityTests
{
    /// <summary>Every recorded download in the repository, with where it came from.</summary>
    public static IEnumerable<object[]> RecordedRuns() => new[]
    {
        new object[] { "full-ninety-nine-objects.txt" },
        new object[] { "differential-one-object.txt" },
        new object[] { "up-to-date-nothing-transferred.txt" },
        new object[] { "aborted-no-download-result.txt" },
        new object[] { "hardware-three-wordings.txt" },
        new object[] { "unrecognised-vocabulary-SYNTHETIC.txt" },
        new object[] { "folder-run-stdout-20260813.json" },
    };

    private static string RepoRoot()
    {
        var root = DownloadProbe.RepoRoot.Find(AppContext.BaseDirectory);
        Assert.NotNull(root);
        return root!;
    }

    /// <summary>
    /// The log text of a recorded run. The six <c>.txt</c> fixtures are probe logs as recorded; the
    /// <c>.json</c> one is a full <c>--json</c> stdout, whose embedded <c>log</c> array is the same
    /// artifact. A missing file is a FAILURE, never a skip.
    /// </summary>
    private static string LogTextOf(string name)
    {
        var path = name.EndsWith(".json", StringComparison.Ordinal)
            ? Path.Combine(RepoRoot(), "src", "openness-cli", "OpennessCli.Tests", "Fixtures", name)
            : Path.Combine(RepoRoot(), "src", "download-feedback", "DownloadFeedback.Tests", "Fixtures", name);

        Assert.True(File.Exists(path), $"recorded run '{name}' is missing: {path}");

        if (!name.EndsWith(".json", StringComparison.Ordinal))
        {
            return File.ReadAllText(path);
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return string.Join(
            "\n",
            document.RootElement.GetProperty("log").EnumerateArray().Select(e => e.GetString() ?? string.Empty));
    }

    private static IReadOnlyList<DownloadProbe.DownloadMessageNode> NodesOf(DownloadResultSummary summary)
    {
        return summary.Messages.Select(Convert).ToList();

        static DownloadProbe.DownloadMessageNode Convert(Ladder.Download.DownloadMessageNode node) =>
            new(node.State ?? "(none)", node.ErrorCount, node.WarningCount, node.Text,
                node.Children.Select(Convert).ToList());
    }

    /// <summary>The library's three-valued verdict, in the probe's own vocabulary.</summary>
    private static TransferVerdictKind Expected(Ladder.Download.TransferVerdict verdict) => verdict switch
    {
        Ladder.Download.TransferVerdict.Transferred => TransferVerdictKind.SoftwareLoaded,
        Ladder.Download.TransferVerdict.NothingTransferred => TransferVerdictKind.NothingTransferred,
        _ => TransferVerdictKind.Undetermined,
    };

    /// <summary>
    /// *** THE PARITY CHECK. *** Same bytes, both implementations, one comparison per recorded run.
    ///
    /// The failure message carries the whole picture — the manifest size, the up-to-date signal and
    /// both verdicts — because a bare "expected X got Y" on a parity test tells you they diverged and
    /// nothing about which one is wrong.
    /// </summary>
    [Theory]
    [MemberData(nameof(RecordedRuns))]
    public void TheProbeAndTheLibraryAgree_OnEveryRecordedDownload(string name)
    {
        var summary = ProbeLogReader.Read(LogTextOf(name));
        var feedback = DownloadFeedbackParser.Parse(summary);
        var nodes = NodesOf(summary);

        var probe = TransferVerdicts.Classify(summary.State ?? "(none)", summary.ErrorCount, nodes);

        var detail = new StringBuilder()
            .AppendLine($"recorded run   : {name}")
            .AppendLine($"result present : {feedback.ResultPresent}")
            .AppendLine($"objects named  : {feedback.LoadedObjectCount}")
            .AppendLine($"non-object     : {feedback.NonObjectLoadCount}")
            .AppendLine($"up-to-date said: {feedback.UpToDateSignalPresent}")
            .AppendLine($"messages       : {feedback.AllMessages.Count}")
            .AppendLine($"LIBRARY says   : {feedback.Verdict}  ({feedback.VerdictReason})")
            .AppendLine($"PROBE   says   : {probe.Kind}  ({probe.Headline})")
            .ToString();

        Assert.True(Expected(feedback.Verdict) == probe.Kind, detail);

        // And the tri-state must agree too: null is "nobody knows" and must never become false.
        var expectedLoaded = feedback.Verdict switch
        {
            Ladder.Download.TransferVerdict.Transferred => (bool?)true,
            Ladder.Download.TransferVerdict.NothingTransferred => false,
            _ => null,
        };

        Assert.True(expectedLoaded == probe.SoftwareLoaded, detail);
    }

    /// <summary>
    /// *** THE SHAPE THE RECORDED CORPUS DOES NOT CONTAIN — AND WHERE THE TWO RULES USED TO PART. ***
    ///
    /// The seven runs above agree. That agreement was a property of THE CORPUS, not of the rules: no
    /// recorded run has a result that carries messages while naming nothing loaded. Construct one and
    /// the old probe rule answered <b>"YES — THE SOFTWARE WAS LOADED"</b> from an ABSENCE, where the
    /// library answers <c>Undetermined</c> from the manifest.
    ///
    /// ⚠️ <b>CONSTRUCTED, NOT MEASURED.</b> No rig produced this input. It is written down because a
    /// parity test that only ever sees inputs both rules happen to handle proves the rules are
    /// equivalent on those inputs and nothing more — and this is the exact shape
    /// <c>DownloadFeedbackParser</c>'s own remarks say it was built to stop being read as a transfer.
    /// </summary>
    [Fact]
    public void OnAResultThatNamesNothing_TheOldAbsenceInferenceIsGone()
    {
        var summary = new DownloadResultSummary(
            "Success", 0, 0,
            new[] { new Ladder.Download.DownloadMessageNode("Something nobody has recorded yet.", "Success", 0, 0, null, null) });

        var feedback = DownloadFeedbackParser.Parse(summary);

        // The library: a result that names nothing loaded AND does not say it skipped the transfer is
        // NOT a negative and NOT a positive. Nobody knows.
        Assert.Equal(Ladder.Download.TransferVerdict.Undetermined, feedback.Verdict);
        Assert.Equal(0, feedback.LoadedObjectCount);

        // The probe, now derived from it, says the same — and in particular does NOT infer a transfer
        // from the absence of an up-to-date phrase, which is what it used to do.
        var probe = TransferVerdicts.Classify(summary.State ?? "(none)", summary.ErrorCount, NodesOf(summary));

        Assert.Equal(TransferVerdictKind.Undetermined, probe.Kind);
        Assert.Null(probe.SoftwareLoaded);
        Assert.DoesNotContain("THE SOFTWARE WAS LOADED", probe.Headline, StringComparison.Ordinal);
    }

    /// <summary>
    /// The second place they parted, in the opposite direction: the probe matched three LOOSE
    /// substrings (<c>"up to date"</c> among them) where the library matches one ANCHORED sentence.
    /// A reworded up-to-date message therefore read as a positive "nothing transferred" to the probe,
    /// while the library declines to conclude and <b>reports the message as unrecognised</b> — which is
    /// the designed way that parser goes out of date rather than silently wrong.
    ///
    /// ⚠️ CONSTRUCTED. TIA has not been observed emitting this wording.
    /// </summary>
    [Fact]
    public void OnARewordedUpToDateMessage_TheLibrarysSurfacingWins()
    {
        var summary = new DownloadResultSummary(
            "Success", 0, 0,
            new[] { new Ladder.Download.DownloadMessageNode("The software has not been loaded, because it is up to date!", "Success", 0, 0, null, null) });

        var feedback = DownloadFeedbackParser.Parse(summary);

        Assert.Equal(Ladder.Download.TransferVerdict.Undetermined, feedback.Verdict);
        Assert.Equal(1, feedback.UnrecognisedMessageCount);
        Assert.NotEmpty(feedback.Anomalies);

        var probe = TransferVerdicts.Classify(summary.State ?? "(none)", summary.ErrorCount, NodesOf(summary));
        Assert.Equal(TransferVerdictKind.Undetermined, probe.Kind);
    }

    /// <summary>
    /// *** THE CONTROL. *** Parity is only meaningful if the runs it is measured over actually differ
    /// from each other. Without this, seven runs that all happened to be `Transferred` would make the
    /// theory above pass while comparing nothing interesting.
    /// </summary>
    [Fact]
    public void TheRecordedRuns_CoverAllThreeVerdicts()
    {
        var verdicts = RecordedRuns()
            .Select(r => (string)r[0])
            .Select(name => DownloadFeedbackParser.Parse(ProbeLogReader.Read(LogTextOf(name))).Verdict)
            .Distinct()
            .ToList();

        Assert.Contains(Ladder.Download.TransferVerdict.Transferred, verdicts);
        Assert.Contains(Ladder.Download.TransferVerdict.NothingTransferred, verdicts);
        Assert.Contains(Ladder.Download.TransferVerdict.Undetermined, verdicts);
    }
}
