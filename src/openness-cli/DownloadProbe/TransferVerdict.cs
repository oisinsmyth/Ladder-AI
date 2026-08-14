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
/// question.
///
/// *** THE DECISION IS NO LONGER MADE HERE. IT IS `Ladder.Download`'s, AND THIS TYPE IS ITS
/// PRESENTATION (2026-08-14). ***
///
/// This class carried its own classifier — three loose up-to-date phrases, and "otherwise, loaded".
/// <c>DownloadFeedbackParser</c> landed LATER THE SAME DAY (`79f1596` then `ccaae5a`) written to
/// replace exactly that rule; its remarks name the two things it excludes by construction, and both
/// were live here. The duplicate was simply left behind, and *** TWO IMPLEMENTATIONS OF ONE RULE
/// DIVERGE, AND THE ONE THAT DIVERGES IS THE ONE NOBODY RUNS TESTS AGAINST. ***
///
/// **They agreed on all seven recorded downloads — and that was a property of the corpus, not of the
/// rules.** `TransferVerdictParityTests` measures it, and constructs the two shapes the corpus does
/// not contain, where they parted in OPPOSITE directions:
///
///   * a result carrying messages but naming nothing loaded — the old rule inferred
///     **"YES — THE SOFTWARE WAS LOADED"** from the ABSENCE of an up-to-date phrase, which is the
///     precise defect `DownloadFeedbackParser` exists to make impossible;
///   * a REWORDED up-to-date sentence — the old rule's loose substrings matched it and concluded
///     `NothingTransferred`, where the library declines to conclude AND reports the message as
///     unrecognised, which is how that parser is designed to go out of date rather than silently
///     wrong.
///
/// So the verdict is now DERIVED from <see cref="Ladder.Download.DownloadFeedback"/> — keyed on the
/// LOAD MANIFEST, positive evidence, never on <c>state</c> and never on an absence. What stays here is
/// the thing the library does not produce and this tool needs: the one-line headline and the verbatim
/// evidence block that go in the log a person reads.
/// </summary>
internal static class TransferVerdicts
{
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

    /// <summary>
    /// *** A FOLDER RUN'S VERDICT, DECIDED BY CONSTRUCTION AND NEVER BY READING MESSAGES. ***
    ///
    /// MEASURED 2026-08-13, on the first live rehearsal: a folder download DOES return a
    /// <c>DownloadResult</c>, and its message tree names 27 objects as loaded. <see cref="Classify"/>
    /// therefore answered *"YES — THE SOFTWARE WAS LOADED"* for a run that contacted no controller,
    /// and that sentence reached the run's verdict line, the log and the JSON.
    ///
    /// The classifier was not wrong about the messages — TIA really did write those objects. It was
    /// asked the wrong question. <b>Whether anything reached a CONTROLLER is not a property of the
    /// message text on this path; it is a property of which overload was called</b>, and the folder
    /// overload takes no connection at all. So the answer is decided here, from the destination, and
    /// the message tree is reported as what it is: a description of the IMAGE.
    /// </summary>
    internal static TransferVerdict ImageOnly(string folder) => new(
        TransferVerdictKind.Undetermined,
        "WAS ANYTHING TRANSFERRED? *** NO — NOTHING REACHED ANY CONTROLLER (this was a FOLDER run). ***",
        new[]
        {
            $"The download image was written to: {folder}",
            "Download(DirectoryInfo, delegate) takes NO connection, so no device was contacted, none was",
            "even selected, and nothing went on the wire. That is true BY CONSTRUCTION and does not",
            "depend on reading a single message.",
            string.Empty,
            "*** THE OBJECTS NAMED IN THE RESULT DESCRIBE THE IMAGE, NOT A TRANSFER. *** They are real",
            "and they are reported in full — under `image` in the JSON — but no count of them is",
            "evidence that a controller holds anything.",
            string.Empty,
            "Reported as UNDETERMINED rather than as a negative about a device: no device was chosen, so",
            "this run says nothing about what any controller holds. What it proves is about THE COMPILE.",
        },
        matchedPhrase: null);

    /// <summary>
    /// Convenience for a caller holding only the message tree: adapt, parse, derive. The probe's own
    /// session does NOT use this — it already has the feedback and passes it to
    /// <see cref="FromFeedback"/>, so the tree is never classified twice.
    /// </summary>
    internal static TransferVerdict Classify(
        string resultState, int errorCount, IReadOnlyList<DownloadMessageNode> messages)
    {
        var summary = Ladder.Download.DownloadResultAdapter.Adapt<DownloadMessageNode>(
            resultState, errorCount, 0, messages,
            m => m.Text, m => m.State, m => m.ErrorCount, m => m.WarningCount, _ => null, m => m.Children);

        return FromFeedback(Ladder.Download.DownloadFeedbackParser.Parse(summary), resultState);
    }

    /// <summary>
    /// *** THE SINGLE RULE, RENDERED. *** The verdict is <see cref="Ladder.Download.DownloadFeedback"/>'s
    /// and is not recomputed, second-guessed or overridden here; only the wording is this tool's.
    ///
    /// The mapping is 1:1 and total, so there is no arm in which this type can hold an opinion the
    /// library does not: Transferred → loaded, NothingTransferred → not loaded, Undetermined → null.
    /// </summary>
    internal static TransferVerdict FromFeedback(Ladder.Download.DownloadFeedback feedback, string resultState)
    {
        var upToDate = feedback.UpToDateSignals.Count > 0 ? feedback.UpToDateSignals[0].Text : null;

        switch (feedback.Verdict)
        {
            case Ladder.Download.TransferVerdict.Transferred:
                return new TransferVerdict(
                    TransferVerdictKind.SoftwareLoaded,
                    $"WAS ANYTHING TRANSFERRED? *** YES — {feedback.TransferredItemCount} ITEM(S) WERE REPORTED LOADED BY NAME " +
                    $"(state={resultState}). ***",
                    new[]
                    {
                        feedback.VerdictReason,
                        string.Empty,
                        "*** THE BASIS IS POSITIVE EVIDENCE — the load manifest — NOT the absence of an",
                        "up-to-date sentence, and NOT state=Success. The one live run that returned Success",
                        "carried nothing, and an earlier version of this tool read ninety-nine load messages",
                        "as unremarkable while deriving its answer from a phrase that was not there.",
                        string.Empty,
                        $"{feedback.LoadedObjectCount} program object(s) + {feedback.NonObjectLoadCount} non-object item(s).",
                        "The full manifest is listed in the LOAD MANIFEST section below, and every message is",
                        "logged verbatim above — check it.",
                    },
                    matchedPhrase: upToDate);

            case Ladder.Download.TransferVerdict.NothingTransferred:
                return new TransferVerdict(
                    TransferVerdictKind.NothingTransferred,
                    $"WAS ANYTHING TRANSFERRED? *** NO — NOTHING WAS TRANSFERRED (state={resultState}). ***",
                    new[]
                    {
                        feedback.VerdictReason,
                        "state and error count say otherwise and are not the evidence: the one previously-",
                        "'successful' live run reported exactly this.",
                        string.Empty,
                        "the statement, verbatim:",
                        "    | " + (upToDate ?? "(none)").Replace("\r\n", " ").Replace("\n", " "),
                    },
                    matchedPhrase: upToDate);

            default:
                var evidence = new List<string> { feedback.VerdictReason };

                if (!feedback.ResultPresent)
                {
                    evidence.Add("There is no DownloadResult to read at all.");
                }
                else if (feedback.AllMessages.Count == 0)
                {
                    evidence.Add($"state={resultState}, and not one message under it.");
                    evidence.Add("EMPTY IS NOT CLEAN: a green state with nothing beneath it is the absence of an");
                    evidence.Add("observation, not an observation that the transfer happened. Do not read it as a pass.");
                }
                else
                {
                    evidence.Add($"The result carries {feedback.AllMessages.Count} message(s) and names NOTHING loaded.");
                    evidence.Add("*** THAT IS NOT A NEGATIVE, AND IT IS NOT A POSITIVE. *** An earlier version of this");
                    evidence.Add("tool answered YES here, from the absence of an up-to-date phrase. Read the device.");
                }

                if (feedback.UnrecognisedMessageCount > 0)
                {
                    evidence.Add(string.Empty);
                    evidence.Add($"{feedback.UnrecognisedMessageCount} message(s) of a shape the parser does not recognise —");
                    evidence.Add("a new TIA vocabulary is the expected way it goes out of date, and they are listed in the");
                    evidence.Add("LOAD MANIFEST section rather than counted as anything.");
                }

                return new TransferVerdict(
                    TransferVerdictKind.Undetermined,
                    feedback.ResultPresent
                        ? $"WAS ANYTHING TRANSFERRED? *** UNDETERMINED — THE RESULT NAMES NOTHING LOADED (state={resultState}). ***"
                        : "WAS ANYTHING TRANSFERRED? *** UNDETERMINED — NO DOWNLOAD RESULT EXISTS. ***",
                    evidence,
                    matchedPhrase: upToDate);
        }
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
