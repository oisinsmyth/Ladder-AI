using Harness.MirrorRead;
using Harness.Wire;

namespace Harness.MirrorView;

/// <summary>
/// 🔴 <b>FOLLOW MODE: THE SAME PAGE, FED FROM A RUNNING WAVE INSTEAD OF FROM A SECOND SOCKET.</b>
///
/// <para><b>The problem, measured.</b> <c>MB_SERVER</c> accepts ONE connection per instance. With the
/// viewer connected to the rig, a conformance wave against the same device failed with
/// <c>SocketException: actively refused</c> and reported 0 of 22 vectors attempted. A second
/// <c>MB_SERVER</c> instance would not have helped: <c>LocalPort</c> and the connection <c>ID</c> are FB
/// STATIC start values, so a second instance inherits both and collides on exactly the port and id being
/// escaped — and it would import and compile without complaint.</para>
///
/// <para>*** THE ARGUMENT IS TRUTH, NOT COST. *** Two sockets means two samples at two instants: the page
/// could show a value the harness never acted on, and the two could then disagree about what the device
/// did. <c>Harness.MirrorView</c> already holds the one-source property internally — its page and its
/// <c>/api/mirror</c> JSON render from one object, so they cannot disagree. This EXTENDS that property
/// ACROSS THE PROCESS BOUNDARY: every register on the page is a byte the harness read, stamped with the
/// instant the harness's read returned.</para>
///
/// <para>🔴 <b>NOTHING HERE CAN CAUSE A READ, AND THAT IS THE INVARIANT TO PROTECT.</b> There is no host,
/// no port, no unit, no socket, no fence and no refresh on this path. This class reads a file the wave
/// wrote and publishes what it finds. If a future edit lets the viewer ask for fresher data — a gateway
/// that refreshes when the page asks, a publisher that samples on its own clock — the two-instants
/// problem returns in a new place minus the second socket, and it will LOOK like the fix is still in
/// place. <c>MirrorFollowPollerTests</c> pins the absence of any such path.</para>
///
/// <para><b>The device fence lives with the PUBLISHER</b>, which is the process that actually contacts
/// the rig. Consulting an allowlist here would fence a file read, which authorises nothing and would
/// refuse a viewer whose wave is properly authorised — an over-firing gate, which decays into a warning
/// and then into nothing.</para>
/// </summary>
public sealed class MirrorFollowPoller : IDisposable
{
    private readonly MirrorViewOptions _options;
    private readonly MirrorState _state;
    private readonly IMirrorFeedSource _source;
    private readonly Func<DateTimeOffset> _clock;
    private readonly MirrorMap _map;

    private CancellationTokenSource? _stopping;
    private Task? _loop;

    public MirrorFollowPoller(
        MirrorViewOptions options,
        MirrorMap map,
        MirrorState state,
        IMirrorFeedSource source,
        Func<DateTimeOffset> clock)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _map = map ?? throw new ArgumentNullException(nameof(map));
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>Read the feed once and publish what it says. Public because it is the whole unit under test.</summary>
    public PollAttempt PollOnce()
    {
        var attempt = Attempt();
        _state.Publish(attempt);
        return attempt;
    }

    private PollAttempt Attempt()
    {
        var read = _source.Read();

        switch (read.State)
        {
            case MirrorFeedState.NeverPublished:
                return new PollAttempt(_clock(), PollOutcome.FeedNeverPublished, read.Describe(),
                    Array.Empty<ushort>(), 0);

            case MirrorFeedState.PublishInterrupted:
                return new PollAttempt(_clock(), PollOutcome.FeedPublishInterrupted, read.Describe(),
                    Array.Empty<ushort>(), 0);

            case MirrorFeedState.Unreadable:
                return new PollAttempt(_clock(), PollOutcome.FeedUnreadable, read.Describe(),
                    Array.Empty<ushort>(), 0);
        }

        var document = read.Document!;

        // ---- THE IDENTITY CHECK, BEFORE A SINGLE REGISTER IS BELIEVED ------------------------------
        //
        // 🔴 The viewer parses its map from the committed IR; the wave derives its map from the binding
        // it is running. Two independent derivations of one thing, and when they disagree THE REGISTERS
        // STILL LINE UP and mean different signals. That failure renders perfectly, which is why it has
        // to be a refusal rather than a note.
        var mismatches = Mismatches(document);
        if (mismatches.Count > 0)
        {
            return new PollAttempt(_clock(), PollOutcome.FeedDescribesADifferentMirror,
                "THE FEED DESCRIBES A DIFFERENT MIRROR: " + string.Join(" | ", mismatches) +
                $". Read from '{read.Origin}', published by {document.PublisherId}. Nothing is shown, because " +
                "the registers would line up and would mean different signals.",
                Array.Empty<ushort>(), 0, null, Provenance(document, read.Origin, composite: null));
        }

        var composite = document.Compose();
        var provenance = Provenance(document, read.Origin, composite);

        if (composite.ObservedRegisters == 0)
        {
            return new PollAttempt(_clock(), PollOutcome.FeedCarriesNoReading,
                $"the publisher {document.PublisherId} is {Word(document.Status)} and the feed carries no read yet " +
                $"({document.Frames.Count} frame(s), sequence {document.Sequence}). Nothing has been read from the " +
                "device on this feed, which is not the same as a mirror full of zeros.",
                Array.Empty<ushort>(), 0, null, provenance);
        }

        // 🔴 *** THE ATTEMPT'S INSTANT IS THE HARNESS'S OWN NEWEST READ - NEVER NOW, AND NEVER THE
        // PUBLISH TIME. *** The whole claim being carried across the process boundary is "these bytes,
        // at that moment". Stamping the attempt with the viewer's clock would reset the age on every
        // poll and make a dead wave's data look permanently fresh; stamping it with the publish time
        // would be a minute wrong on a feed republished after a slow read. Every register additionally
        // carries its OWN instant, because a composed picture has several.
        var newest = composite.Newest!.Value;

        return new PollAttempt(newest, PollOutcome.Ok,
            $"followed '{read.Origin}': {document.Frames.Count} read(s) composing {composite.ObservedRegisters} of " +
            $"{document.Identity.DeclaredRegisters} register(s), publisher {document.PublisherId} {Word(document.Status)}, " +
            $"sequence {document.Sequence}. NO SOCKET WAS OPENED BY THIS VIEWER - these are the bytes the harness read.",
            composite.Values, 0, composite.ObservedUtc, provenance);
    }

    /// <summary>
    /// Every way the feed's identity disagrees with this viewer's map. <b>All of them, not the first</b>:
    /// a reader fixing one mismatch should not have to run again to discover the next.
    /// </summary>
    private IReadOnlyList<string> Mismatches(MirrorFeedDocument document)
    {
        var mismatches = new List<string>();

        if (document.Identity.DeclaredRegisters != _map.DeclaredRegisters)
        {
            mismatches.Add(
                $"the feed declares {document.Identity.DeclaredRegisters} register(s) and this viewer's map " +
                $"({_map.AreaPointerSource}) declares {_map.DeclaredRegisters}");
        }

        if (document.Identity.WordOrder != RegisterDecode.Order)
        {
            mismatches.Add(
                $"the feed was read {document.Identity.WordOrder} and this viewer decodes {RegisterDecode.Order}, " +
                "so every 32-bit value would be wrong by up to 65 536 and would look plausible");
        }

        return mismatches;
    }

    private static FeedProvenance Provenance(MirrorFeedDocument document, string origin, MirrorFeedComposite? composite) =>
        new(origin,
            document.PublisherId,
            document.Status,
            document.PublishedUtc,
            document.Sequence,
            document.Identity.StampLiteral,
            document.Identity.MapHash,
            document.Identity.DeclaredRegisters,
            document.Identity.WordOrder,
            document.Frames.Count,
            composite?.ObservedRegisters ?? 0,
            composite?.UnobservedRegisters ?? document.Identity.DeclaredRegisters,
            composite?.Oldest);

    private static string Word(PublisherStatus status) => status == PublisherStatus.Ended ? "ENDED" : "RUNNING";

    /// <summary>Start the background loop. Returns immediately.</summary>
    public void Start()
    {
        if (_loop is not null) throw new InvalidOperationException("already started.");

        _stopping = new CancellationTokenSource();
        var token = _stopping.Token;

        _loop = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    PollOnce();
                }
                catch (Exception ex)
                {
                    // A crash is loud without being NAMED. The loop must not die silently, and the page
                    // must not keep showing the last good numbers as though a follower were still running.
                    _state.Publish(new PollAttempt(_clock(), PollOutcome.FeedUnreadable,
                        $"the follow loop threw ({ex.GetType().Name}: {ex.Message}). This is a defect in the " +
                        "viewer, not a reading of the device and not a statement about the wave.",
                        Array.Empty<ushort>(), 0));
                }

                try
                {
                    await Task.Delay(_options.PollIntervalMs, token).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    return;
                }
            }
        }, token);
    }

    public void Dispose()
    {
        _stopping?.Cancel();

        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(5));
        }
        catch (Exception)
        {
            // Shutting down; a loop that will not stop is not worth throwing over.
        }

        _stopping?.Dispose();
    }
}
