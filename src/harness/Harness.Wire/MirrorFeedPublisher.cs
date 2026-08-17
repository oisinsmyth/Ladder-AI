using System.Globalization;
using System.Text;

namespace Harness.Wire;

/// <summary>
/// 🔴 <b>THE PUBLISH SIDE OF THE ONE-SOCKET RULE.</b>
///
/// <para><c>MB_SERVER</c> accepts ONE connection per instance, so a viewer that opens its own socket
/// while a wave is running takes the wave's connection away — measured: a conformance wave against a
/// device with the viewer attached failed with <c>SocketException: actively refused</c> and reported 0 of
/// 22 vectors attempted. A second <c>MB_SERVER</c> instance would not have helped either:
/// <c>LocalPort</c> and the connection <c>ID</c> are FB STATIC start values, so a second instance
/// inherits both and imports and compiles without complaint while colliding on exactly the port and id
/// being escaped.</para>
///
/// <para>*** BUT THE ARGUMENT FOR THIS DESIGN IS TRUTH, NOT COST. *** Two sockets means two samples at
/// two different instants: the page could show a value the harness never acted on, and the two could then
/// disagree about what the device did. One source means the page shows EXACTLY the bytes the harness made
/// its decisions from. <c>Harness.MirrorView</c> already holds that property internally — its page and
/// its <c>/api/mirror</c> JSON are rendered from one object, so they cannot disagree — and this extends
/// the same property ACROSS A PROCESS BOUNDARY. That is deliberate, and it is the whole point.</para>
///
/// <para>🔴 <b>THE ONE WAY TO LOSE IT, STATED SO NOBODY ADDS IT.</b> Nothing here may sample. There is no
/// method on this type that reads a device, no timer that refreshes, and no path by which a reader can
/// cause a read to happen. A publisher that sampled on its own clock, or a gateway that refreshed when
/// the page asked, would bring the two-instants problem back in a new place minus the second socket — and
/// it would LOOK like the fix had been made. This type only ever forwards what a read already returned,
/// stamped with the instant it returned.</para>
/// </summary>
public interface IMirrorFeedPublisher : IDisposable
{
    /// <summary>
    /// Declare what this feed will describe, before any read. <b>Publishes a document with no frames</b>,
    /// which is a real and useful state: <i>a publisher is running here and no read has landed yet</i> —
    /// distinguishable from a feed nobody ever wrote and from one that stopped.
    /// </summary>
    void Begin(MirrorFeedIdentity identity);

    /// <summary>
    /// Forward one read. <b>Called at the moment the read RETURNS</b>, with the values as they came off
    /// the wire and the instant they arrived.
    /// </summary>
    void Publish(int startRegister, ushort[] values, DateTimeOffset observedUtc);

    /// <summary>
    /// Record that no further reads are coming. <b>The difference between "the wave finished" and "the
    /// wave died"</b>, which a reader cannot otherwise tell from a feed that simply stopped advancing.
    /// </summary>
    void End();

    /// <summary>Where it is writing. Printed by the run, so an operator can point a viewer at it.</summary>
    string Destination { get; }

    /// <summary>
    /// Publishes that FAILED. <b>Never zero silently:</b> a publisher that cannot write must not stop the
    /// wave, and it must not be able to look like one that is working either.
    /// </summary>
    int Failures { get; }

    /// <summary>The most recent failure, verbatim, or null.</summary>
    string? LastFailure { get; }

    /// <summary>Documents successfully published.</summary>
    long Published { get; }
}

/// <summary>
/// The real publisher: one file, replaced atomically, holding the newest read per RANGE.
///
/// <para><b>Newest per range, not a rolling log.</b> A wave reads the same few ranges over and over — the
/// control region every poll, each slot's results per poll round — so keying on <c>(start, count)</c>
/// bounds the file at a handful of frames while keeping every register's own provenance. A log would grow
/// without limit and would make the reader choose which entry to believe.</para>
///
/// <para><b>The atomicity mechanism is <c>CoordinatorStateStore</c>'s, deliberately.</b> Write a
/// temporary file in the SAME DIRECTORY with <see cref="FileOptions.WriteThrough"/>, flush it to disk,
/// then rename it over the destination. A reader opens either the whole old document or the whole new
/// one. The <c>END &lt;count&gt;</c> terminator in <see cref="MirrorFeed"/> is the second line of defence
/// for the cases the rename does not cover — a hand-edited file, a copy taken mid-write, a share that
/// does not honour the rename.</para>
///
/// <para>🔴 <b>AND ITS HARD-WON COMPANION: THE <c>.initialised</c> MARKER, WRITTEN BEFORE THE FIRST
/// PUBLISH.</b> <see cref="File.Replace(string,string,string)"/> is not atomic against process death — it
/// leaves a window in which the destination DOES NOT EXIST. Without the marker a reader landing in that
/// window reads "no publisher has ever written here", which is correct exactly once and catastrophic
/// every time after. Same defect, same fix, as the wave store: with the marker present, an absent feed is
/// a PUBLISH THAT DID NOT COMPLETE, and it is a different state on the screen.</para>
///
/// <para><b>A failure to publish never throws.</b> The feed is a view; the wave is the work. But the
/// failures are COUNTED and the last one is kept, and the run reports both — a viewer feed that quietly
/// stopped writing would otherwise be indistinguishable from a wave that quietly stopped reading.</para>
/// </summary>
public sealed class MirrorFeedPublisher : IMirrorFeedPublisher
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly object _gate = new();
    private readonly SortedDictionary<(int Start, int Count), MirrorFeedFrame> _frames = new();
    private readonly Func<DateTimeOffset> _clock;
    private readonly string _publisherId;

    private MirrorFeedIdentity? _identity;
    private long _sequence;

    /// <param name="path">The feed file. Its directory is created if it does not exist.</param>
    /// <param name="clock">Injectable so a test does not have to wait for time to pass.</param>
    /// <param name="publisherId">
    /// Who is writing. Defaults to the process id and machine, which is what tells two runs apart in the
    /// same directory.
    /// </param>
    public MirrorFeedPublisher(string path, Func<DateTimeOffset>? clock = null, string? publisherId = null)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("a feed path is required.", nameof(path));

        Destination = Path.GetFullPath(path);
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _publisherId = publisherId ?? $"pid{Environment.ProcessId.ToString(CultureInfo.InvariantCulture)}@{Environment.MachineName}";
    }

    /// <inheritdoc />
    public string Destination { get; }

    /// <summary>
    /// The marker whose only purpose is to make the ABSENCE of the feed mean something. Written once,
    /// BEFORE the first publish — one written afterwards would leave the very window it exists to qualify.
    /// </summary>
    public string InitialisedPath => Destination + ".initialised";

    /// <inheritdoc />
    public int Failures { get; private set; }

    /// <inheritdoc />
    public string? LastFailure { get; private set; }

    /// <inheritdoc />
    public long Published { get; private set; }

    /// <summary>
    /// TEST SEAM — invoked with the temporary file's path after it has been written and flushed, and
    /// BEFORE it is renamed over the destination.
    ///
    /// <para>🔴 <b>IT EXISTS BECAUSE THE TWO CENTRAL GUARANTEES OF THIS CLASS ARE OTHERWISE UNTESTABLE,
    /// AND MUTATION PROVED IT.</b> Replacing the whole temp-then-rename with a plain
    /// <see cref="File.WriteAllText(string,string)"/> left every test green, and so did writing the
    /// <c>.initialised</c> marker AFTER the publish instead of before. Nothing observed the moments that
    /// matter: that the NEW document is already complete somewhere else while the destination still holds
    /// the whole PREVIOUS one, and that the marker is on disk before anything is ever published. This makes
    /// exactly those observable, and it is the difference between an argued guarantee and a demonstrated
    /// one — the same seam, for the same reason, as <c>CoordinatorStateStore.OnTemporaryWritten</c>.</para>
    /// </summary>
    internal Action<string>? OnTemporaryWritten { get; set; }

    /// <inheritdoc />
    public void Begin(MirrorFeedIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        lock (_gate)
        {
            _identity = identity;
            _frames.Clear();
            Emit(PublisherStatus.Running);
        }
    }

    /// <inheritdoc />
    public void Publish(int startRegister, ushort[] values, DateTimeOffset observedUtc)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (values.Length == 0) return;

        lock (_gate)
        {
            if (_identity is null)
            {
                // Unreachable through MirrorClient, which calls Begin in its own constructor. Counted
                // rather than thrown, because a feed defect must never be able to stop a wave — and
                // counted rather than ignored, because a publisher that silently drops every read is
                // exactly the thing the reader cannot see.
                Fail("a read was offered before Begin, so this publisher does not know what mirror it is describing.");
                return;
            }

            // A DEFENSIVE COPY, so the frame is the bytes as they arrived and stays that way whatever the
            // caller does with its array afterwards. The whole claim is "these are the bytes the harness
            // acted on"; sharing the array would make that a claim about a live object.
            var frame = new MirrorFeedFrame(startRegister, observedUtc, values.ToArray());
            _frames[(startRegister, values.Length)] = frame;

            Emit(PublisherStatus.Running);
        }
    }

    /// <inheritdoc />
    public void End()
    {
        lock (_gate)
        {
            if (_identity is null) return;
            Emit(PublisherStatus.Ended);
        }
    }

    /// <summary>Ends the feed. A publisher disposed without <see cref="End"/> would look like one that died.</summary>
    public void Dispose() => End();

    private void Emit(PublisherStatus status)
    {
        var identity = _identity;
        if (identity is null) return;

        var document = new MirrorFeedDocument(
            _publisherId, identity, status, _clock(), ++_sequence, _frames.Values.ToArray());

        try
        {
            var directory = Path.GetDirectoryName(Destination);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            // *** BEFORE THE PUBLISH, NEVER AFTER - THE ORDER IS THE GUARANTEE. *** See InitialisedPath.
            if (!File.Exists(InitialisedPath))
            {
                File.WriteAllText(
                    InitialisedPath,
                    "This mirror feed HAS been published at least once.\n" +
                    "If '" + Path.GetFileName(Destination) + "' is missing while this file exists, the feed was NOT\n" +
                    "never-written - a publish did not complete. A viewer must report that as an INTERRUPTED\n" +
                    "publish, never as 'no wave has run'.\n",
                    Utf8NoBom);
            }

            var temporary = Destination + ".tmp-" + Guid.NewGuid().ToString("N");

            using (var stream = new FileStream(
                       temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                       bufferSize: 1, options: FileOptions.WriteThrough))
            {
                var payload = Utf8NoBom.GetBytes(MirrorFeed.Format(document));
                stream.Write(payload, 0, payload.Length);

                // The content is durable BEFORE the rename publishes it. The ORDER is the point.
                stream.Flush(flushToDisk: true);
            }

            OnTemporaryWritten?.Invoke(temporary);

            Replace(temporary);
            Published++;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Fail($"{ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// The rename. <see cref="File.Replace(string,string,string)"/> needs an existing destination and
    /// <see cref="File.Move(string,string)"/> needs an absent one, so the choice is made from the
    /// destination and the race between checking and acting is handled by falling through to the other
    /// call rather than by trusting the check.
    /// </summary>
    private void Replace(string temporary)
    {
        if (File.Exists(Destination))
        {
            File.Replace(temporary, Destination, destinationBackupFileName: null);
            return;
        }

        try
        {
            File.Move(temporary, Destination);
        }
        catch (IOException)
        {
            File.Replace(temporary, Destination, destinationBackupFileName: null);
        }
    }

    private void Fail(string detail)
    {
        Failures++;
        LastFailure = detail;
    }
}
