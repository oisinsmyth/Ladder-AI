using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using DownloadProbe;
using Ladder.Download;
using Xunit;

using ProbeProgram = DownloadProbe.Program;

namespace OpennessCli.Tests;

/// <summary>
/// 🔴 A FOLDER RUN REPORTED THAT THE SOFTWARE HAD BEEN LOADED (found 2026-08-13, first live rehearsal).
///
/// `--to-folder` writes a download IMAGE to a directory through
/// <c>Download(DirectoryInfo, delegate)</c> — an overload that takes no connection, so no device is
/// contacted and none is even selected. *** IT RETURNS A `DownloadResult` ANYWAY, AND THAT RESULT
/// NAMES OBJECTS AS LOADED. *** On the rehearsal against `GenProject1`: 27 objects, 3 non-object
/// items, exit 0.
///
/// The report then contradicted itself three ways in one document:
/// <list type="bullet">
/// <item><description><c>transferVerdict</c>: <c>Undetermined</c> — correct, set by a post-hoc override.</description></item>
/// <item><description><c>loadManifest.verdict</c>: <b><c>Transferred</c></b>.</description></item>
/// <item><description>top-level <c>verdict</c>: <b>"YES — THE SOFTWARE WAS LOADED"</b> — the sentence
/// had already been rendered from the pre-override value.</description></item>
/// </list>
///
/// The middle two are exactly a gateway's <c>Loaded</c> conditions. *** A CONSUMER HANDED THAT REPORT
/// COMPUTES `Loaded = true` FOR A RUN THAT CONTACTED NO CONTROLLER. ***
///
/// *** AND THE GUARD FOR IT EXISTED AND COULD NOT FIRE. *** The consumer's
/// <c>available is not true</c> branch names "a folder download" in its own message, and was proved
/// to work by a HAND-AUTHORED <c>available: false</c> fixture — a fixture asserting the very premise
/// this run falsified. Written, tested around, never executed.
///
/// *** SO THESE TESTS RUN ON THE RECORDED STDOUT OF THAT REAL FOLDER RUN ***
/// (<c>Fixtures/folder-run-stdout-20260813.json</c>), never on a fixture describing what we believed
/// a folder run does. Its message tree is the input; its conclusions are what is under test.
/// </summary>
public class DownloadProbeFolderRunTests
{
    private const string RecordedFolderRun = "folder-run-stdout-20260813.json";

    private static string ReadFixture(string name)
    {
        var repoRoot = ScratchProjectGuard.FindRepoRoot(AppContext.BaseDirectory);
        Assert.NotNull(repoRoot);

        var path = Path.Combine(repoRoot!, "src", "openness-cli", "OpennessCli.Tests", "Fixtures", name);
        Assert.True(
            File.Exists(path),
            $"the recorded folder run '{name}' is missing: {path}. It is the evidence these assertions rest "
            + "on — its absence is a failure, never a skip.");

        return File.ReadAllText(path);
    }

    private static JsonElement RecordedRoot()
    {
        using var document = JsonDocument.Parse(ReadFixture(RecordedFolderRun));
        return document.RootElement.Clone();
    }

    /// <summary>
    /// The recorded run's own message tree, re-derived — the SAME 27 objects the live
    /// <c>DownloadResult</c> carried. This is the input a folder run really produces.
    /// </summary>
    private static DownloadFeedback RecordedFolderRunFeedback()
    {
        var root = RecordedRoot();
        var log = string.Join(
            "\n",
            root.GetProperty("log").EnumerateArray().Select(e => e.GetString() ?? string.Empty));

        return DownloadFeedbackParser.Parse(ProbeLogReader.Read(log));
    }

    // ---- THE CONTROL: the fixture really does contain the data that lied ------------------------

    /// <summary>
    /// *** WITHOUT THIS, EVERY TEST BELOW COULD BE VACUOUS. *** It pins what the probe ACTUALLY
    /// EMITTED on the rehearsal, so "the new output is honest" is measured against a recording that
    /// demonstrably was not. If a future edit made this fixture benign, this test fails and says so.
    /// </summary>
    [Fact]
    public void TheRecordedRun_IsTheDefect_NotAHandAuthoredStandIn()
    {
        var root = RecordedRoot();

        // It was a folder run: an image directory, and no PC interface or target was ever chosen.
        Assert.EndsWith("image", root.GetProperty("downloadTarget").GetString()!, StringComparison.Ordinal);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("pcInterface").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("targetInterface").ValueKind);

        // And it said all three of these at once.
        Assert.Equal("Undetermined", root.GetProperty("transferVerdict").GetString());
        Assert.Equal("Transferred", root.GetProperty("loadManifest").GetProperty("verdict").GetString());
        Assert.Contains("THE SOFTWARE WAS LOADED", root.GetProperty("verdict").GetString()!, StringComparison.Ordinal);

        // The manifest that a Loaded check would have believed.
        var manifest = root.GetProperty("loadManifest");
        Assert.True(manifest.GetProperty("available").GetBoolean());
        Assert.Equal(27, manifest.GetProperty("loadedObjectCount").GetInt32());

        // The premise the old comment stated, falsified in one line: a folder run DOES produce a result.
        Assert.True(manifest.GetProperty("resultPresent").GetBoolean());
    }

    /// <summary>
    /// And the re-derived feedback carries the same 27 objects — so the input these tests feed the
    /// probe is the real thing, not a reduced stand-in.
    /// </summary>
    [Fact]
    public void TheRecordedMessageTree_StillNames27Objects_AndTheParserStillCallsItTransferred()
    {
        var feedback = RecordedFolderRunFeedback();

        Assert.Equal(27, feedback.LoadedObjectCount);
        Assert.Equal(Ladder.Download.TransferVerdict.Transferred, feedback.Verdict);
        Assert.Contains("Main", feedback.LoadedObjects);
    }

    /// <summary>
    /// The recorded run's message tree in the probe's OWN node type — the form
    /// <see cref="TransferVerdicts.Classify"/> reads. Rebuilt from the recorded log rather than
    /// hand-written, so the classifier is exercised on the real 27-object tree.
    /// </summary>
    private static IReadOnlyList<DownloadProbe.DownloadMessageNode> RecordedMessageNodes()
    {
        var root = RecordedRoot();
        var log = string.Join(
            "\n",
            root.GetProperty("log").EnumerateArray().Select(e => e.GetString() ?? string.Empty));

        return ProbeLogReader.Read(log).Messages.Select(Convert).ToList();

        static DownloadProbe.DownloadMessageNode Convert(Ladder.Download.DownloadMessageNode node) =>
            new(node.State ?? "(none)", node.ErrorCount, node.WarningCount, node.Text,
                node.Children.Select(Convert).ToList());
    }

    // ---- THE FIX: a folder run reports what it is ------------------------------------------------

    /// <summary>
    /// *** THE RULE THAT BROKE, WITH ITS CONTROL. *** The SAME message tree, classified twice. The
    /// device path still reads the text and still concludes the software was loaded — correctly, for
    /// a device run. The folder path does not ask the text at all.
    ///
    /// The control is the half that matters: it proves the tree really does contain the sentence that
    /// misled, so "the folder verdict does not say it" is a fact about the destination rule and not
    /// about a harmless input.
    /// </summary>
    [Fact]
    public void TheSameMessageTree_IsClassifiedByDestination_NotByItsText()
    {
        var messages = RecordedMessageNodes();
        Assert.NotEmpty(messages);

        var asDevice = ProbeSession.ClassifyTransfer(
            DownloadDestination.Controller, null, "Success", 0, messages);
        var asFolder = ProbeSession.ClassifyTransfer(
            DownloadDestination.Folder, @"C:\somewhere\image", "Success", 0, messages);

        // THE CONTROL: this tree really does produce the misleading answer when read as a device run.
        Assert.Contains("THE SOFTWARE WAS LOADED", asDevice.Headline, StringComparison.Ordinal);
        Assert.True(asDevice.SoftwareLoaded);

        // And the folder run, from the same bytes, does not.
        Assert.DoesNotContain("THE SOFTWARE WAS LOADED", asFolder.Headline, StringComparison.Ordinal);
        Assert.Contains("NOTHING REACHED ANY CONTROLLER", asFolder.Headline, StringComparison.Ordinal);
        Assert.Null(asFolder.SoftwareLoaded);
        Assert.Contains(@"C:\somewhere\image", string.Join("\n", asFolder.Evidence), StringComparison.Ordinal);
    }

    private static JsonElement RunFolder(DownloadFeedback? feedback)
    {
        using var repo = ProbeFenceRepo.Permitting();
        var logDir = Path.Combine(Path.GetTempPath(), "probe-folder-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(logDir);
        var stdout = new StringWriter();

        ProbeProgram.Run(
            new[] { repo.ProjectPath, "--options", "SoftwareOnlyChanges", "--json", "--log-dir", logDir },
            stdout,
            new StringWriter(),
            (_, _) => new ProbeOutcome(ProbeExitCodes.Completed, "stub")
            {
                Feedback = feedback,
                FeedbackSource = nameof(DownloadResultAdapter),
                Destination = DownloadDestination.Folder,
                DownloadTarget = @"C:\somewhere\image",

                // FROM THE REAL CLASSIFIER, over the REAL recorded message tree — never a value this
                // test chose. The first version of this test set the verdict it then asserted, which
                // is the shape of check this whole task exists to remove.
                Transfer = ProbeSession.ClassifyTransfer(
                    DownloadDestination.Folder, @"C:\somewhere\image", "Success", 0, RecordedMessageNodes()),
            },
            repo.BinaryDirectory);

        using var document = JsonDocument.Parse(stdout.ToString());
        return document.RootElement.Clone();
    }

    /// <summary>
    /// *** THE REGRESSION TEST. *** The same 27-object message tree, through the fixed code: no field
    /// anywhere may say a controller holds anything.
    /// </summary>
    [Fact]
    public void AFolderRun_ReportsNoDeviceManifest_HoweverManyObjectsTheResultNames()
    {
        var manifest = RunFolder(RecordedFolderRunFeedback()).GetProperty("loadManifest");

        Assert.False(manifest.GetProperty("available").GetBoolean());
        Assert.Equal("folder", manifest.GetProperty("target").GetString());
        Assert.False(manifest.GetProperty("describesDeviceTransfer").GetBoolean());
        Assert.Equal(nameof(Ladder.Download.TransferVerdict.Undetermined), manifest.GetProperty("verdict").GetString());

        // Every device-facing field is null — not an empty array, which would say "TIA loaded nothing".
        foreach (var key in new[]
        {
            "loadedObjects", "loadedObjectCount", "transferredItemCount", "nonObjectLoadSubjects",
            "runStateDisclosed", "runStateTransitions", "upToDateSignalPresent", "anomalies",
        })
        {
            Assert.Equal(JsonValueKind.Null, manifest.GetProperty(key).ValueKind);
        }
    }

    /// <summary>
    /// A folder run is NOT an abort, and the two must stay distinguishable: an abort produced no
    /// result at all, a folder run produced one and it was read in full. <c>resultPresent</c> is what
    /// separates them now that both are <c>available: false</c>.
    /// </summary>
    [Fact]
    public void AFolderRun_IsDistinguishableFromAnAbort()
    {
        var folder = RunFolder(RecordedFolderRunFeedback()).GetProperty("loadManifest");
        Assert.True(folder.GetProperty("resultPresent").GetBoolean());
        Assert.Equal("folder", folder.GetProperty("target").GetString());

        // The abort case: no feedback at all.
        var aborted = RunFolder(feedback: null).GetProperty("loadManifest");
        Assert.False(aborted.GetProperty("resultPresent").GetBoolean());
    }

    /// <summary>
    /// The image facts are KEPT, in full, under a key no "was it loaded?" check would read. Discarding
    /// them would trade one dishonesty for another — the image was really written and the objects are
    /// really named.
    /// </summary>
    [Fact]
    public void TheImageFactsAreKept_UnderAKeyThatCannotBeMistakenForATransfer()
    {
        var manifest = RunFolder(RecordedFolderRunFeedback()).GetProperty("loadManifest");
        var image = manifest.GetProperty("image");

        Assert.Equal(27, image.GetProperty("objectCount").GetInt32());
        Assert.Equal(27, image.GetProperty("objects").GetArrayLength());
        Assert.Equal(@"C:\somewhere\image", image.GetProperty("folder").GetString());

        // The parser's own word, labelled as being about the image — the thing that used to mislead,
        // kept visible rather than hidden.
        Assert.Equal(nameof(Ladder.Download.TransferVerdict.Transferred), image.GetProperty("parserVerdict").GetString());
    }

    /// <summary>
    /// *** THE SENTENCE, TOO. *** The old override fixed the verdict OBJECT after the verdict STRING
    /// had already been rendered from the un-overridden value, so the run's headline said the
    /// software was loaded while the object beside it said undetermined. A post-hoc correction can
    /// only fix the copy it reaches; the destination now decides the verdict once, and everything is
    /// rendered from that.
    /// </summary>
    [Fact]
    public void NoFieldInTheWholeDocument_SaysTheSoftwareWasLoaded()
    {
        var root = RunFolder(RecordedFolderRunFeedback());

        Assert.Equal(nameof(Ladder.Download.TransferVerdict.Undetermined), root.GetProperty("transferVerdict").GetString());

        // Tri-state, and it must be null: neither true nor a false that reads as "TIA declined".
        Assert.Equal(JsonValueKind.Null, root.GetProperty("softwareLoaded").ValueKind);

        Assert.DoesNotContain("THE SOFTWARE WAS LOADED", root.GetProperty("verdict").GetString()!, StringComparison.Ordinal);
        Assert.Contains("NOTHING REACHED ANY CONTROLLER", root.GetProperty("transferHeadline").GetString()!, StringComparison.Ordinal);

        // *** WHAT THIS TEST DOES NOT PROVE, STATED RATHER THAN QUIETLY OMITTED. *** The human log's
        // "WAS ANYTHING ACTUALLY TRANSFERRED?" section is written inside ReportResult, which needs a
        // live DownloadResult and cannot run here. So the LOG's wording is not asserted by this test.
        // What is asserted: the recorded run's log DID print the false sentence (below), and the log
        // section, the run's verdict string and this JSON field are all rendered from the SAME
        // `transfer.Headline` — whose destination rule is under test in
        // TheSameMessageTree_IsClassifiedByDestination_NotByItsText.
        var recordedLog = string.Join(
            "\n", RecordedRoot().GetProperty("log").EnumerateArray().Select(e => e.GetString()));
        Assert.Contains("THE SOFTWARE WAS LOADED", recordedLog, StringComparison.Ordinal);
    }

    /// <summary>
    /// *** THE CONSUMER'S QUESTION, ASKED HERE SO IT CANNOT BE ANSWERED WRONG THERE. *** These are the
    /// three conditions a gateway's <c>Loaded</c> check combines: a first-class manifest is available,
    /// its verdict is <c>Transferred</c>, and it names the objects that were deployed. On the
    /// recorded run ALL THREE HELD. They cannot all hold now, and this test fails if the fix is
    /// reverted by any route — the destination, the verdict, or the nulling.
    /// </summary>
    [Fact]
    public void TheThreeConditionsOfLoaded_CannotAllHoldForAFolderRun()
    {
        // The recorded run: all three held. This is the negative control that gives the assertion
        // below its meaning — without it, "they do not all hold" could be true of any document.
        var before = RecordedRoot().GetProperty("loadManifest");
        Assert.True(
            before.GetProperty("available").GetBoolean()
            && before.GetProperty("verdict").GetString() == nameof(Ladder.Download.TransferVerdict.Transferred)
            && before.GetProperty("loadedObjects").GetArrayLength() > 0,
            "the recorded run no longer satisfies Loaded's conditions, so this test proves nothing.");

        var after = RunFolder(RecordedFolderRunFeedback()).GetProperty("loadManifest");

        var available = after.GetProperty("available").ValueKind == JsonValueKind.True;
        var transferred = after.GetProperty("verdict").GetString() == nameof(Ladder.Download.TransferVerdict.Transferred);
        var named = after.GetProperty("loadedObjects").ValueKind == JsonValueKind.Array
                    && after.GetProperty("loadedObjects").GetArrayLength() > 0;

        Assert.False(
            available && transferred && named,
            "a folder run still satisfies every condition of Loaded. It contacted no controller.");

        // Each one independently, so a partial revert cannot pass by satisfying the conjunction only.
        Assert.False(available);
        Assert.False(transferred);
        Assert.False(named);
    }

    /// <summary>
    /// The device path is untouched by all of this: a controller run still produces a real manifest.
    /// Without this, "nothing says loaded" could be satisfied by breaking the feature outright.
    /// </summary>
    [Fact]
    public void TheControllerPathStillReportsARealManifest()
    {
        using var repo = ProbeFenceRepo.Permitting();
        var logDir = Path.Combine(Path.GetTempPath(), "probe-folder-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(logDir);
        var stdout = new StringWriter();

        ProbeProgram.Run(
            new[] { repo.ProjectPath, "--options", "SoftwareOnlyChanges", "--json", "--log-dir", logDir },
            stdout,
            new StringWriter(),
            (_, _) => new ProbeOutcome(ProbeExitCodes.Completed, "stub")
            {
                Feedback = RecordedFolderRunFeedback(),
                FeedbackSource = nameof(DownloadResultAdapter),
                Destination = DownloadDestination.Controller,
            },
            repo.BinaryDirectory);

        using var document = JsonDocument.Parse(stdout.ToString());
        var manifest = document.RootElement.GetProperty("loadManifest");

        Assert.True(manifest.GetProperty("available").GetBoolean());
        Assert.Equal("controller", manifest.GetProperty("target").GetString());
        Assert.True(manifest.GetProperty("describesDeviceTransfer").GetBoolean());
        Assert.Equal(nameof(Ladder.Download.TransferVerdict.Transferred), manifest.GetProperty("verdict").GetString());
        Assert.Equal(27, manifest.GetProperty("loadedObjectCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, manifest.GetProperty("image").ValueKind);
    }
}
