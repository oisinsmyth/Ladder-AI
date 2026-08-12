using System;
using System.Collections.Generic;
using System.Linq;

namespace DownloadProbe;

/// <summary>
/// One node of <c>DownloadResult.Messages</c>, already read off the Siemens objects.
///
/// The seam exists for the same reason <see cref="RaisedConfigurationView"/> does:
/// <c>DownloadResultMessage</c> is produced only by a live download and cannot be constructed in a
/// test, and "did anything actually get transferred" is precisely the question that must be answered
/// without running the tool to find out.
/// </summary>
internal sealed class DownloadMessageNode
{
    internal DownloadMessageNode(
        string state,
        int errorCount,
        int warningCount,
        string? text,
        IReadOnlyList<DownloadMessageNode>? children = null,
        string? timestamp = null)
    {
        State = state;
        ErrorCount = errorCount;
        WarningCount = warningCount;
        Text = text;
        Children = children ?? Array.Empty<DownloadMessageNode>();
        Timestamp = timestamp;
    }

    internal string State { get; }

    /// <summary>The message's own <c>DateTime</c>, rendered. Logged, never used to classify.</summary>
    internal string? Timestamp { get; }

    internal int ErrorCount { get; }

    internal int WarningCount { get; }

    /// <summary>Verbatim, exactly as Siemens wrote it. Never trimmed or normalised for matching.</summary>
    internal string? Text { get; }

    internal IReadOnlyList<DownloadMessageNode> Children { get; }

    /// <summary>This node and every descendant, depth-first — the tree flattened for searching.</summary>
    internal IEnumerable<DownloadMessageNode> SelfAndDescendants()
    {
        yield return this;
        foreach (var child in Children)
        {
            foreach (var node in child.SelfAndDescendants())
            {
                yield return node;
            }
        }
    }
}

internal enum TransferVerdictKind
{
    /// <summary>
    /// *** SOMETHING WAS TRANSFERRED. *** The download completed and NOTHING in its message tree said
    /// the target was already up to date. This is the outcome the disruptive experiment exists to
    /// produce, and the only one that would prove this tooling can deliver a program.
    /// </summary>
    SoftwareLoaded,

    /// <summary>
    /// *** NOTHING WAS TRANSFERRED, DESPITE A GREEN RESULT. *** A message said the target was already
    /// up to date. This is what the one previously-"successful" run actually did, and reporting it as
    /// a success is the specific failure this classifier exists to make impossible.
    /// </summary>
    NothingTransferred,

    /// <summary>
    /// Not answerable from what came back — no result at all (an abort), or a result whose message
    /// tree is empty. EMPTY IS NOT CLEAN: a green state with nothing under it is the absence of an
    /// observation, not the observation that everything was fine.
    /// </summary>
    Undetermined,
}

/// <summary>
/// WAS ANYTHING ACTUALLY TRANSFERRED?
///
/// THE REASON THIS TYPE EXISTS, AND IT IS NOT HYPOTHETICAL. Of six live download attempts against the
/// real device, five aborted and the one that returned <c>Success</c> reported "The software has not
/// been loaded, because it is up-to-date" — it carried nothing. <c>DownloadResult.State</c> was
/// <c>Success</c> and <c>ErrorCount</c> was 0 in that run, so state and counts CANNOT answer this
/// question: the answer lives only in the message text, and only a reader who happened to read it
/// would have known. That is not evidence, that is luck.
///
/// The rule, stated so a reviewer can check it against the log in one read:
///   - no result at all, or a result with no messages          -> Undetermined
///   - any message matching an UP-TO-DATE phrase               -> NothingTransferred
///   - otherwise                                               -> SoftwareLoaded
///
/// The third arm is an INFERENCE FROM ABSENCE and is labelled as one everywhere it is printed. TIA
/// does not emit a "the software was loaded" sentence this tool can key on positively, so the honest
/// report is "the download completed and did not say it skipped the transfer", with every message
/// quoted verbatim underneath so the reader can disagree.
/// </summary>
internal static class TransferVerdicts
{
    /// <summary>
    /// Phrases that mean the transfer did NOT happen. Case-insensitive substring matches on the
    /// verbatim message text.
    ///
    /// The first is MEASURED — it is the sentence the live 2026-08-12 run returned, whole. The other
    /// two are the same claim spelled the two other ways Siemens renders it, included because a
    /// classifier that missed the phrasing by a hyphen would produce the exact false "TRANSFERRED"
    /// this type exists to prevent. Matching wide is the safe direction here: a false
    /// "nothing transferred" costs a re-run, a false "transferred" costs a wrong conclusion about
    /// whether the tooling can deliver a program at all.
    /// </summary>
    internal static readonly IReadOnlyList<string> UpToDatePhrases = new[]
    {
        "has not been loaded",
        "up-to-date",
        "up to date",
    };

    /// <summary>
    /// Words whose appearance in a result message means the message is TALKING ABOUT the CPU's run
    /// state. Not a reading of the run state — this tool never asks the CPU anything — which is why
    /// everything matched is quoted verbatim and labelled a MENTION.
    ///
    /// Matched on word boundaries: a bare <c>Contains("run")</c> hits "during", "running" and
    /// "runtime" and would report a disclosure on almost every message there is.
    /// </summary>
    internal static readonly IReadOnlyList<string> RunStateWords = new[]
    {
        "run", "stop", "stopped", "stopping", "start", "started", "starting", "restart", "restarted",
    };

    /// <summary>
    /// The verdict for a download that never produced a result — an abort, or a throw. Named rather
    /// than defaulted, so that "we never got an answer" cannot be rendered by the same code path that
    /// renders "we got an answer and it was fine".
    /// </summary>
    internal static TransferVerdict NoResult(string why) => new(
        TransferVerdictKind.Undetermined,
        "WAS ANYTHING TRANSFERRED? *** UNDETERMINED — NO DOWNLOAD RESULT EXISTS. ***",
        new[] { why, "The download did not run to a DownloadResult, so there is nothing to read." },
        matchedPhrase: null);

    internal static TransferVerdict Classify(
        string resultState, int errorCount, IReadOnlyList<DownloadMessageNode> messages)
    {
        var all = messages.SelectMany(m => m.SelfAndDescendants()).ToList();
        var texts = all
            .Select(m => m.Text)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t!)
            .ToList();

        if (texts.Count == 0)
        {
            return new TransferVerdict(
                TransferVerdictKind.Undetermined,
                "WAS ANYTHING TRANSFERRED? *** UNDETERMINED — THE RESULT CARRIES NO MESSAGES. ***",
                new[]
                {
                    $"state={resultState}, errors={errorCount}, and not one message under it.",
                    "EMPTY IS NOT CLEAN: a green state with nothing beneath it is the absence of an",
                    "observation, not an observation that the transfer happened. Do not read it as a pass.",
                },
                matchedPhrase: null);
        }

        foreach (var phrase in UpToDatePhrases)
        {
            var hit = texts.FirstOrDefault(t => t.IndexOf(phrase, StringComparison.OrdinalIgnoreCase) >= 0);
            if (hit is null)
            {
                continue;
            }

            var evidence = new List<string>
            {
                $"A result message contains the up-to-date phrase '{phrase}', so the target already had",
                "this software and the download CARRIED NOTHING. state and error count say otherwise and",
                "are not the evidence: the one previously-'successful' live run reported exactly this.",
                string.Empty,
                "every message containing the phrase, verbatim:",
            };
            evidence.AddRange(
                texts.Where(t => t.IndexOf(phrase, StringComparison.OrdinalIgnoreCase) >= 0)
                     .Select(t => "    | " + t.Replace("\r\n", " ").Replace("\n", " ")));

            return new TransferVerdict(
                TransferVerdictKind.NothingTransferred,
                $"WAS ANYTHING TRANSFERRED? *** NO — NOTHING WAS TRANSFERRED (state={resultState}). ***",
                evidence,
                phrase);
        }

        return new TransferVerdict(
            TransferVerdictKind.SoftwareLoaded,
            $"WAS ANYTHING TRANSFERRED? *** YES — THE SOFTWARE WAS LOADED (state={resultState}, errors={errorCount}). ***",
            new[]
            {
                $"The download ran to a result over {texts.Count} message(s) and NONE of them says the target",
                $"was already up to date (searched for: {string.Join(", ", UpToDatePhrases.Select(p => $"'{p}'"))}).",
                "THE BASIS IS AN ABSENCE, and is stated as one: TIA emits no positive 'the software was",
                "loaded' sentence to key on, so this reads 'the download completed and did not say it",
                "skipped the transfer'. Every message is logged verbatim above — check it.",
            },
            matchedPhrase: null);
    }

    /// <summary>
    /// What, if anything, the result messages SAY about the CPU's run state.
    ///
    /// Deliberately not a reading of the CPU: this tool has no online connection and asks the
    /// controller nothing. The distinction is the whole value of the section — a mention quoted from
    /// TIA's own text is evidence a person can weigh; a claim about the run state manufactured from
    /// silence is not, so silence is reported as silence.
    /// </summary>
    internal static IReadOnlyList<string> DescribeRunState(IReadOnlyList<DownloadMessageNode> messages)
    {
        var texts = messages
            .SelectMany(m => m.SelfAndDescendants())
            .Select(m => m.Text)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t!)
            .ToList();

        var mentions = texts.Where(MentionsRunState).ToList();

        if (mentions.Count == 0)
        {
            return new[]
            {
                "RESULTING RUN STATE: *** NOT DISCLOSED. ***",
                "  No result message mentions run, stop or start at all, so this run says NOTHING about",
                "  whether the CPU is in RUN or in STOP. That is a gap in the evidence, not a reassurance:",
                "  look at the CPU's mode switch / LEDs, or read it in TIA Portal.",
            };
        }

        var lines = new List<string>
        {
            $"RESULTING RUN STATE: {mentions.Count} result message(s) MENTION run/stop/start. Verbatim:",
        };
        lines.AddRange(mentions.Select(t => "    | " + t.Replace("\r\n", " ").Replace("\n", " ")));
        lines.Add("  These are TIA's own words about what the download did — NOT a reading of the CPU's");
        lines.Add("  operating mode. Nothing in this tool asks the controller what mode it is in.");
        return lines;
    }

    private static bool MentionsRunState(string text)
    {
        foreach (var word in RunStateWords)
        {
            var index = 0;
            while ((index = text.IndexOf(word, index, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                var beforeOk = index == 0 || !char.IsLetterOrDigit(text[index - 1]);
                var after = index + word.Length;
                var afterOk = after >= text.Length || !char.IsLetterOrDigit(text[after]);
                if (beforeOk && afterOk)
                {
                    return true;
                }

                index = after;
            }
        }

        return false;
    }
}

/// <summary>The answer, its one-line headline, and the verbatim evidence it was drawn from.</summary>
internal sealed class TransferVerdict
{
    internal TransferVerdict(
        TransferVerdictKind kind, string headline, IReadOnlyList<string> evidence, string? matchedPhrase)
    {
        Kind = kind;
        Headline = headline;
        Evidence = evidence;
        MatchedPhrase = matchedPhrase;
    }

    internal TransferVerdictKind Kind { get; }

    /// <summary>The one line that goes into the run's verdict. Says YES, NO or UNDETERMINED outright.</summary>
    internal string Headline { get; }

    internal IReadOnlyList<string> Evidence { get; }

    /// <summary>Which up-to-date phrase matched, when one did. Null otherwise.</summary>
    internal string? MatchedPhrase { get; }

    /// <summary>
    /// Tri-state on purpose: <c>null</c> is "not known", and must never collapse into <c>false</c>.
    /// A consumer reading a bool would turn "we could not tell" into "nothing was transferred".
    /// </summary>
    internal bool? SoftwareLoaded => Kind switch
    {
        TransferVerdictKind.SoftwareLoaded => true,
        TransferVerdictKind.NothingTransferred => false,
        _ => null,
    };
}
