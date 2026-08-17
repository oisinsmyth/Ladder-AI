using Harness.Wire;

namespace Harness.MirrorRead;

/// <summary>
/// What a read of the published feed found. <b>The zero value REFUSES.</b>
///
/// <para>🔴 <b>THESE ARE FOUR DIFFERENT FACTS AND NOT ONE OF THEM IS "SOMETHING WENT WRONG".</b> "No
/// wave has ever run here", "a publish did not complete", "the file is there and cannot be read" and "the
/// feed parsed" have four different remedies and three of them are not a device problem at all. A source
/// that collapsed them would hand a viewer one indistinguishable failure to render, and the most likely
/// rendering of an indistinguishable failure is a plausible-looking page.</para>
/// </summary>
public enum MirrorFeedState
{
    /// <summary>
    /// *** THE FEED IS THERE AND COULD NOT BE READ *** — truncated, no terminator, a frame count that
    /// does not match, a header stated twice, a locked file, or a format this build does not know. The
    /// zero value, so a dropped or defaulted state refuses rather than displaying.
    /// </summary>
    Unreadable = 0,

    /// <summary>
    /// *** NO PUBLISHER HAS EVER WRITTEN HERE. *** No feed file and no <c>.initialised</c> marker beside
    /// it. Legitimate before any wave has run, and NOT the same fact as a publish that did not complete —
    /// see <see cref="PublishInterrupted"/>, which is what an absent file means once one has.
    /// </summary>
    NeverPublished = 1,

    /// <summary>
    /// *** A PUBLISHER WROTE HERE AND THE FEED IS GONE. *** The <c>.initialised</c> marker exists and the
    /// feed does not: either a publish is mid-rename right now, or one died inside that window, or
    /// somebody deleted the file. Never reported as "no wave has ever run", which is the benign-looking
    /// state and therefore the worst one to land on by accident.
    /// </summary>
    PublishInterrupted = 2,

    /// <summary>The feed was read and every line of it parsed. What it SAYS still has to be checked against a map.</summary>
    Ok = 3,
}

/// <summary>One read of the feed.</summary>
/// <param name="State">Which of the four things was found. Read this before anything else.</param>
/// <param name="Document">The feed. Null unless <see cref="MirrorFeedState.Ok"/>.</param>
/// <param name="Origin">Where it looked, so a refusal names a path somebody can go and check.</param>
/// <param name="Problem">Why not, when not. Empty on <see cref="MirrorFeedState.Ok"/>.</param>
public sealed record MirrorFeedResult(
    MirrorFeedState State,
    MirrorFeedDocument? Document,
    string Origin,
    string Problem)
{
    /// <summary>One line naming what was found, in the affirmative.</summary>
    public string Describe() => State switch
    {
        MirrorFeedState.Ok =>
            $"FEED READ from '{Origin}': {Document!.Status}, sequence {Document.Sequence}, " +
            $"{Document.Frames.Count} read(s) of {Document.Identity.DeclaredRegisters} declared register(s).",

        MirrorFeedState.NeverPublished =>
            $"NO FEED AT '{Origin}', and no record that one was ever written there. This is NOT an empty " +
            "mirror and NOT a stopped wave: nothing has ever published to this path. Start a wave with " +
            "--publish pointed here, or point this viewer at the path a wave is using.",

        MirrorFeedState.PublishInterrupted =>
            $"A PUBLISHER WROTE TO '{Origin}' AND THE FEED IS NOT THERE NOW. {Problem} This is not 'no wave " +
            "has run' - something published here and the file is absent, so a publish did not complete or " +
            "the file was removed.",

        _ => $"FEED UNREADABLE at '{Origin}': {Problem} Nothing from it may be displayed - a document that " +
             "cannot be fully accounted for is a refusal, never a short table of registers.",
    };
}

/// <summary>
/// 🔴 <b>THE SIBLING OF <see cref="IRegisterSource"/>: THE SAME VIEWER, FED FROM A RUNNING WAVE INSTEAD
/// OF FROM A SOCKET.</b>
///
/// <para><b>Why it is a separate port rather than an <see cref="IRegisterSource"/> implementation.</b>
/// That interface's one verb answers <c>ushort[] Read(start, count)</c> — values, or an exception. A feed
/// read has FOUR outcomes that must stay apart, and it carries per-register provenance: which registers
/// were covered by an actual read, and WHEN each one was read. Forcing it through <c>IRegisterSource</c>
/// would collapse "no wave has ever run", "the publisher died", and "the file is truncated" into one
/// thrown exception, which <c>RegisterRead.Perform</c> would then report as a transport failure — a
/// device diagnosis for three conditions in which no device was involved. The seam is the same SHAPE
/// (a port, a real implementation, a factory a test can substitute) and it is deliberately not the same
/// TYPE, because the type cannot carry the distinctions the whole change exists to preserve.</para>
///
/// <para><b>It is read-only about the device in the strongest possible sense: it does not know a device
/// exists.</b> There is no host, no port, no unit, no socket and no allowlist on this path. The device
/// fence governs the PUBLISHER, which is the process that actually contacts the rig.</para>
///
/// <para>🔴 <b>AND IT NEVER CAUSES A READ.</b> Nothing on this interface can ask for fresher data. It
/// returns what the publisher last wrote, or a named reason it cannot. A source that could trigger a
/// refresh would reintroduce the second sample the one-socket design removes.</para>
/// </summary>
public interface IMirrorFeedSource
{
    /// <summary>Where this source reads from. For the banner and for every refusal message.</summary>
    string Origin { get; }

    /// <summary>Read the feed as it stands. Never throws: every failure is one of the four states.</summary>
    MirrorFeedResult Read();
}

/// <summary>
/// The real source: a feed file on disk, written by a wave running <c>harness-run --publish</c>.
///
/// <para><b>Contention with a publish in flight is not corruption.</b> The publisher renames a complete
/// temporary file over the destination, and on Windows a rename briefly refuses opens of its destination
/// and leaves a window in which the destination does not exist. Both were measured on this repository's
/// sibling store and both were being reported as corruption. So: open with
/// <see cref="FileShare.Delete"/> (whose absence broke the WRITER, not the reader), retry on the
/// exception the OS actually raises, and separate the persistent case — a violation that clears is
/// contention, one that never clears is a lock or a permissions problem and saying "contention" would
/// hide it.</para>
/// </summary>
public sealed class FileMirrorFeedSource : IMirrorFeedSource
{
    /// <summary>How many opens are attempted before a sharing violation is called persistent.</summary>
    private const int ContentionAttempts = 40;

    /// <summary>Pause between attempts. 40 x 5 ms comfortably outlives a rename.</summary>
    private const int ContentionPauseMs = 5;

    private readonly Action<int> _pause;

    /// <param name="path">The feed file the publisher writes.</param>
    /// <param name="pause">
    /// How to wait between contention retries. Injectable ONLY so a test does not spend 200 ms proving
    /// the persistent case; the production value is a plain sleep.
    /// </param>
    public FileMirrorFeedSource(string path, Action<int>? pause = null)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("a feed path is required.", nameof(path));

        Origin = Path.GetFullPath(path);
        _pause = pause ?? (ms => Thread.Sleep(ms));
    }

    /// <inheritdoc />
    public string Origin { get; }

    /// <summary>
    /// The marker the publisher writes once, before its first publish. <b>Its only purpose is to make the
    /// ABSENCE of the feed mean something</b> — without it, the rename's own window reads as "nobody ever
    /// wrote one here", which is correct exactly once and wrong every time after.
    /// </summary>
    public string InitialisedPath => Origin + ".initialised";

    /// <inheritdoc />
    public MirrorFeedResult Read()
    {
        string text;
        var attempt = 0;

        while (true)
        {
            try
            {
                // Deliberately NOT File.Exists first: it swallows every error and returns false, so an
                // unreadable directory would read as "no publisher ever wrote here" - the one state from
                // which a viewer would happily report that no wave has run.
                using var stream = new FileStream(
                    Origin, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);

                text = reader.ReadToEnd();
                break;
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                if (!File.Exists(InitialisedPath))
                    return new MirrorFeedResult(MirrorFeedState.NeverPublished, null, Origin, string.Empty);

                if (++attempt >= ContentionAttempts)
                {
                    return new MirrorFeedResult(MirrorFeedState.PublishInterrupted, null, Origin,
                        $"The feed is ABSENT although '{InitialisedPath}' records that a publisher HAS written here, " +
                        $"and it was still absent after {attempt} attempt(s) over {attempt * ContentionPauseMs} ms. " +
                        "A publish leaves a window in which the destination does not exist; one that never closes " +
                        "means the rename died or the file was deleted.");
                }

                _pause(ContentionPauseMs);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (++attempt >= ContentionAttempts)
                {
                    return new MirrorFeedResult(MirrorFeedState.Unreadable, null, Origin,
                        $"the feed exists and could not be opened after {attempt} attempt(s) over " +
                        $"{attempt * ContentionPauseMs} ms ({ex.Message}). That is long enough that this is a " +
                        "persistent lock or a permissions problem rather than a publish in flight - the two are " +
                        "indistinguishable from a single attempt, which is why this waited.");
                }

                _pause(ContentionPauseMs);
            }
        }

        return MirrorFeed.TryParse(text, out var document, out var problem) && document is not null
            ? new MirrorFeedResult(MirrorFeedState.Ok, document, Origin, string.Empty)
            : new MirrorFeedResult(MirrorFeedState.Unreadable, null, Origin, problem);
    }
}
