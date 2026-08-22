using Harness.Map;
using Harness.Wire;

namespace Harness.Wire.Tests;

/// <summary>
/// THE PUBLISH SIDE: what a wave writes, when it writes it, and what its silence means.
///
/// <para>Every test here uses a real temporary directory, because the properties being checked are
/// properties of FILES — the marker's ordering relative to the first publish, the terminator surviving a
/// rename, an unwritable destination being counted rather than thrown. A fake filesystem would assert
/// them about a fake.</para>
/// </summary>
public class MirrorFeedPublisherTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 17, 9, 30, 0, TimeSpan.Zero);

    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "mirrorfeed-" + Guid.NewGuid().ToString("N"));

    private string Feed => Path.Combine(_directory, "wave.mirrorfeed");

    private static MirrorFeedIdentity Identity(int registers = 8) =>
        new(0xF52ECEAD, "abc123", registers, RegisterWordOrder.HighWordFirst);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A leaked temp directory is not worth failing a test over.
        }
    }

    private MirrorFeedDocument ReadBack()
    {
        Assert.True(MirrorFeed.TryParse(File.ReadAllText(Feed), out var document, out var problem), problem);
        return document!;
    }

    // ---- WHAT BEGIN MEANS ----------------------------------------------------------------------------

    /// <summary>
    /// <b>A publisher that has begun and read nothing publishes a document with NO FRAMES.</b> That is a
    /// real state and a useful one — "a wave is here and its first poll has not returned" — and it is
    /// distinguishable from a feed nobody wrote and from one that stopped.
    /// </summary>
    [Fact]
    public void Begin_publishes_an_identity_with_no_frames()
    {
        using var publisher = new MirrorFeedPublisher(Feed, () => T0, "test");
        publisher.Begin(Identity());

        var document = ReadBack();

        Assert.Equal(PublisherStatus.Running, document.Status);
        Assert.Empty(document.Frames);
        Assert.Equal(Identity(), document.Identity);
        Assert.Equal(0, document.Compose().ObservedRegisters);
    }

    /// <summary>
    /// 🔴 <b>THE MARKER IS WRITTEN BEFORE THE FIRST PUBLISH, AND THE ORDER IS THE WHOLE GUARANTEE.</b>
    /// <c>File.Replace</c> leaves a window in which the destination does not exist; a reader landing there
    /// without the marker reads "nobody ever wrote here", which is correct exactly once and catastrophic
    /// every time after. A marker written AFTER the rename would leave that window unmarked.
    /// </summary>
    [Fact]
    public void The_initialised_marker_exists_as_soon_as_anything_has_been_published()
    {
        using var publisher = new MirrorFeedPublisher(Feed, () => T0, "test");

        Assert.False(File.Exists(publisher.InitialisedPath),
            "the marker exists before anything was published, so an absent feed could not be distinguished " +
            "from one that was never written.");

        publisher.Begin(Identity());

        Assert.True(File.Exists(publisher.InitialisedPath));
    }

    // ---- WHAT PUBLISH CARRIES ------------------------------------------------------------------------

    [Fact]
    public void A_published_read_carries_its_words_and_its_instant()
    {
        using var publisher = new MirrorFeedPublisher(Feed, () => T0.AddSeconds(9), "test");
        publisher.Begin(Identity());
        publisher.Publish(2, new ushort[] { 0xBEEF, 0xCAFE }, T0.AddMilliseconds(72));

        var frame = Assert.Single(ReadBack().Frames);

        Assert.Equal(2, frame.StartRegister);
        Assert.Equal(new ushort[] { 0xBEEF, 0xCAFE }, frame.Values);

        // *** THE READ'S OWN INSTANT, NOT THE PUBLISH TIME. *** The clock says T0+9s and the frame says
        // T0+72ms, and only one of those is when the values existed.
        Assert.Equal(T0.AddMilliseconds(72), frame.ObservedUtc);
    }

    /// <summary>
    /// <b>The frame is a copy.</b> The claim being carried is "these are the bytes the harness acted on";
    /// keeping the caller's array would make that a claim about a live object somebody else may reuse.
    /// </summary>
    [Fact]
    public void A_published_read_is_not_affected_by_later_mutation_of_the_callers_array()
    {
        var values = new ushort[] { 1, 2, 3 };

        using var publisher = new MirrorFeedPublisher(Feed, () => T0, "test");
        publisher.Begin(Identity());
        publisher.Publish(0, values, T0);

        values[0] = 0xFFFF;

        Assert.Equal(new ushort[] { 1, 2, 3 }, ReadBack().Frames[0].Values);
    }

    /// <summary>
    /// A wave reads the same few RANGES over and over, so the feed holds the newest read per range rather
    /// than a growing log. The composite still carries every register's own provenance.
    /// </summary>
    [Fact]
    public void Re_reading_a_range_replaces_it_rather_than_appending()
    {
        using var publisher = new MirrorFeedPublisher(Feed, () => T0, "test");
        publisher.Begin(Identity());

        for (var i = 0; i < 50; i++)
            publisher.Publish(0, new ushort[] { (ushort)i, 0 }, T0.AddSeconds(i));

        var document = ReadBack();

        Assert.Single(document.Frames);
        Assert.Equal(49, document.Frames[0].Values[0]);
        Assert.Equal(T0.AddSeconds(49), document.Frames[0].ObservedUtc);
    }

    [Fact]
    public void Distinct_ranges_are_kept_side_by_side_with_their_own_instants()
    {
        using var publisher = new MirrorFeedPublisher(Feed, () => T0, "test");
        publisher.Begin(Identity());
        publisher.Publish(0, new ushort[] { 1, 2, 3, 4 }, T0);
        publisher.Publish(4, new ushort[] { 5, 6 }, T0.AddSeconds(30));

        var composite = ReadBack().Compose();

        Assert.Equal(6, composite.ObservedRegisters);
        Assert.Equal(T0, composite.ObservedUtc[0]);
        Assert.Equal(T0.AddSeconds(30), composite.ObservedUtc[4]);
    }

    [Fact]
    public void The_sequence_advances_on_every_publish()
    {
        using var publisher = new MirrorFeedPublisher(Feed, () => T0, "test");
        publisher.Begin(Identity());
        var first = ReadBack().Sequence;

        publisher.Publish(0, new ushort[] { 1 }, T0);
        var second = ReadBack().Sequence;

        Assert.True(second > first, "the sequence did not advance, so a viewer cannot see the feed moving.");
    }

    // ---- END, AND WHY IT IS NOT THE SAME AS STOPPING ------------------------------------------------

    /// <summary>
    /// 🔴 <b>"THE WAVE FINISHED" AND "THE WAVE DIED" ARE DIFFERENT FACTS, AND ONLY <c>End</c> RECORDS THE
    /// FIRST.</b> A publisher that simply stops writing is indistinguishable from one that was killed —
    /// so ending is an explicit act, and <see cref="IDisposable"/> performs it because a publisher that
    /// went out of scope without one would report an incident.
    /// </summary>
    [Fact]
    public void End_records_that_the_publisher_finished()
    {
        var publisher = new MirrorFeedPublisher(Feed, () => T0, "test");
        publisher.Begin(Identity());
        publisher.Publish(0, new ushort[] { 1 }, T0);

        Assert.Equal(PublisherStatus.Running, ReadBack().Status);

        publisher.End();

        Assert.Equal(PublisherStatus.Ended, ReadBack().Status);
    }

    [Fact]
    public void Disposing_ends_the_feed()
    {
        using (var publisher = new MirrorFeedPublisher(Feed, () => T0, "test"))
        {
            publisher.Begin(Identity());
            publisher.Publish(0, new ushort[] { 1 }, T0);
        }

        Assert.Equal(PublisherStatus.Ended, ReadBack().Status);
    }

    /// <summary>Ending keeps the reads. The values a wave finished on are the ones worth looking at.</summary>
    [Fact]
    public void Ending_keeps_the_last_reads()
    {
        using var publisher = new MirrorFeedPublisher(Feed, () => T0, "test");
        publisher.Begin(Identity());
        publisher.Publish(0, new ushort[] { 0xAAAA, 0xBBBB }, T0);
        publisher.End();

        var document = ReadBack();

        Assert.Equal(PublisherStatus.Ended, document.Status);
        Assert.Equal(new ushort[] { 0xAAAA, 0xBBBB }, document.Frames[0].Values);
    }

    /// <summary>A publisher that never began writes nothing at all — including no marker.</summary>
    [Fact]
    public void A_publisher_that_never_began_writes_nothing()
    {
        using (var publisher = new MirrorFeedPublisher(Feed, () => T0, "test"))
        {
            publisher.End();
        }

        Assert.False(File.Exists(Feed));
        Assert.False(Directory.Exists(_directory) && File.Exists(Feed + ".initialised"));
    }

    // ---- FAILURE IS COUNTED, NEVER THROWN AND NEVER SILENT ------------------------------------------

    /// <summary>
    /// 🔴 <b>A FEED THAT CANNOT BE WRITTEN MUST NOT STOP A WAVE — AND MUST NOT LOOK LIKE ONE THAT IS
    /// WORKING.</b> The wave is the work and the feed is a view, so the publish is swallowed; but a
    /// publisher that silently dropped every read would be indistinguishable from a healthy one, and the
    /// run's report keys on these counters.
    /// </summary>
    [Fact]
    public void An_unwritable_destination_is_counted_rather_than_thrown()
    {
        // A path whose PARENT is a file cannot be created as a directory, so every write fails and the
        // failure is a property of the filesystem rather than of a mock.
        Directory.CreateDirectory(_directory);
        var blocker = Path.Combine(_directory, "blocker");
        File.WriteAllText(blocker, "not a directory");

        using var publisher = new MirrorFeedPublisher(Path.Combine(blocker, "wave.mirrorfeed"), () => T0, "test");

        publisher.Begin(Identity());
        publisher.Publish(0, new ushort[] { 1 }, T0);

        Assert.True(publisher.Failures >= 2, $"expected every publish to be counted as failed; got {publisher.Failures}.");
        Assert.NotNull(publisher.LastFailure);
        Assert.Equal(0, publisher.Published);
    }

    /// <summary>The converse control: an ordinary run reports its publishes and NO failures.</summary>
    [Fact]
    public void A_healthy_publisher_reports_its_publishes_and_no_failures()
    {
        using var publisher = new MirrorFeedPublisher(Feed, () => T0, "test");
        publisher.Begin(Identity());
        publisher.Publish(0, new ushort[] { 1 }, T0);

        Assert.Equal(0, publisher.Failures);
        Assert.Null(publisher.LastFailure);
        Assert.Equal(2, publisher.Published);
    }

    /// <summary>A read offered before <c>Begin</c> is counted, never written under a guessed identity.</summary>
    [Fact]
    public void A_read_before_begin_is_counted_and_nothing_is_written()
    {
        using var publisher = new MirrorFeedPublisher(Feed, () => T0, "test");
        publisher.Publish(0, new ushort[] { 1 }, T0);

        Assert.Equal(1, publisher.Failures);
        Assert.False(File.Exists(Feed));
    }

    // ---- NO DEBRIS, AND A WHOLE DOCUMENT AT EVERY MOMENT --------------------------------------------

    /// <summary>A completed publish leaves no temporary files beside the feed.</summary>
    [Fact]
    public void A_publish_leaves_no_temporary_files()
    {
        using var publisher = new MirrorFeedPublisher(Feed, () => T0, "test");
        publisher.Begin(Identity());

        for (var i = 0; i < 10; i++)
            publisher.Publish(0, new ushort[] { (ushort)i }, T0.AddSeconds(i));

        Assert.Empty(Directory.GetFiles(_directory, "*.tmp-*"));
    }

    /// <summary>
    /// 🔴 <b>AT THE MOMENT BEFORE PUBLICATION, THE NEW DOCUMENT IS COMPLETE SOMEWHERE ELSE AND THE
    /// DESTINATION STILL HOLDS THE WHOLE PREVIOUS ONE.</b>
    ///
    /// <para>That sentence IS the atomicity guarantee, and nothing could observe it from outside — which
    /// is why a mutation replacing the whole temp-then-rename with a plain <c>File.WriteAllText</c> left
    /// every other test in this file green. The seam makes the moment visible, and this asserts both
    /// halves of it.</para>
    /// </summary>
    [Fact]
    public void At_the_moment_before_publication_both_documents_are_whole()
    {
        var publisher = new MirrorFeedPublisher(Feed, () => T0, "test");
        publisher.Begin(Identity());
        publisher.Publish(0, new ushort[] { 0x1111 }, T0);

        var observations = 0;

        publisher.OnTemporaryWritten = temporary =>
        {
            observations++;

            // The NEW document, complete, somewhere else.
            Assert.True(MirrorFeed.TryParse(File.ReadAllText(temporary), out var staged, out var stagedProblem), stagedProblem);
            Assert.Equal(0x2222, staged!.Frames[0].Values[0]);

            // The PREVIOUS document, whole, still at the destination.
            Assert.True(MirrorFeed.TryParse(File.ReadAllText(Feed), out var current, out var currentProblem), currentProblem);
            Assert.Equal(0x1111, current!.Frames[0].Values[0]);
        };

        publisher.Publish(0, new ushort[] { 0x2222 }, T0.AddSeconds(1));

        Assert.Equal(1, observations);
    }

    /// <summary>
    /// 🔴 <b>THE MARKER IS ON DISK BEFORE ANYTHING IS EVER PUBLISHED, AND THE ORDER IS THE GUARANTEE.</b>
    ///
    /// <para>A marker written after the rename would leave the very window it exists to qualify, unmarked —
    /// and a reader landing there would report "no publisher has ever written here", which is the one state
    /// from which a viewer happily declares that no wave has run. The seam is what makes the ORDER
    /// observable: asserting the marker exists after <c>Begin</c> returns is satisfied by either order.</para>
    /// </summary>
    [Fact]
    public void The_marker_is_written_before_the_very_first_publication()
    {
        var publisher = new MirrorFeedPublisher(Feed, () => T0, "test");

        var observations = 0;

        publisher.OnTemporaryWritten = _ =>
        {
            observations++;
            Assert.True(File.Exists(publisher.InitialisedPath),
                "the marker does not exist at the moment the FIRST document is about to be published, so a " +
                "reader landing in the rename's window would read the absent feed as 'nobody ever wrote here'.");
        };

        publisher.Begin(Identity());

        Assert.Equal(1, observations);
    }

    /// <summary>
    /// <b>A reader interleaved with a hundred publishes never sees a partial document.</b> This is the
    /// property the atomic rename buys, and it is asserted by READING rather than by inspecting the code:
    /// every read either finds no file yet or parses whole.
    /// </summary>
    [Fact]
    public void A_reader_racing_the_publisher_never_sees_a_half_written_document()
    {
        using var publisher = new MirrorFeedPublisher(Feed, () => T0, "test");
        publisher.Begin(Identity());

        var partial = 0;
        var whole = 0;

        for (var i = 0; i < 100; i++)
        {
            publisher.Publish(0, new ushort[] { (ushort)i, (ushort)(i + 1) }, T0.AddSeconds(i));

            string text;
            try
            {
                using var stream = new FileStream(Feed, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                text = reader.ReadToEnd();
            }
            catch (IOException)
            {
                continue;
            }

            if (MirrorFeed.TryParse(text, out _, out _)) whole++;
            else partial++;
        }

        Assert.True(whole > 0, "no read succeeded at all, so this proves nothing about atomicity.");
        Assert.Equal(0, partial);
    }

    /// <summary>
    /// <b>A reader on ANOTHER THREAD, reading continuously while the publisher works.</b>
    ///
    /// <para>⚠️ The sequential interleaving above cannot fail under a non-atomic write: it reads between
    /// publishes, so a torn file would already have been repaired. This one reads DURING them, which is
    /// the situation a live viewer is actually in. The denominator is asserted, because a reader that
    /// never landed on a write proves nothing about writes.</para>
    /// </summary>
    [Fact]
    public async Task A_concurrent_reader_never_observes_a_partial_document()
    {
        using var publisher = new MirrorFeedPublisher(Feed, () => T0, "test");
        publisher.Begin(Identity());

        var stop = false;
        var whole = 0;
        var partial = 0;
        var absent = 0;

        var reader = Task.Run(() =>
        {
            while (!Volatile.Read(ref stop))
            {
                string text;
                try
                {
                    using var stream = new FileStream(Feed, FileMode.Open, FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete);
                    using var handle = new StreamReader(stream);
                    text = handle.ReadToEnd();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // A rename briefly refuses opens of its destination, and it briefly does not exist.
                    // Neither is a partial document, and neither is what this test is about.
                    absent++;
                    continue;
                }

                if (MirrorFeed.TryParse(text, out _, out _)) whole++;
                else partial++;
            }
        });

        for (var i = 0; i < 2000; i++)
            publisher.Publish(0, new ushort[] { (ushort)i, (ushort)(i >> 8) }, T0.AddMilliseconds(i));

        Volatile.Write(ref stop, true);
        await reader.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.True(whole + absent > 0, "the reader never ran, so this proves nothing.");
        Assert.True(whole > 0, $"the reader never read a whole document at all ({absent} open failures), so a " +
                               "zero partial count is a green over nothing.");
        Assert.Equal(0, partial);
    }

    // ---- THE CLIENT'S END OF IT ----------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>THE CLIENT PUBLISHES AT THE MOMENT THE READ RETURNS, AND NOTHING ELSE CAN CAUSE ONE.</b> This
    /// is the whole truthfulness claim: the page renders the bytes the harness acted on, stamped with when
    /// the harness read them. A publisher that sampled on its own clock would reintroduce two instants
    /// minus the second socket, and would look exactly like the fix.
    /// </summary>
    [Fact]
    public void The_client_publishes_every_read_with_the_instant_it_returned()
    {
        var map = SmallMap();
        var transport = new RecordingTransport(map);
        var clock = T0;

        using var publisher = new MirrorFeedPublisher(Feed, () => T0, "test");

        var client = new MirrorClient(map, transport, new BuildStamp(0xF52ECEAD),
            RegisterWordOrder.HighWordFirst, publisher, () => clock);

        // Begin fired in the constructor, before any read.
        Assert.Empty(ReadBack().Frames);
        Assert.Equal(0, transport.Reads);

        clock = T0.AddMilliseconds(72);
        client.ReadControlUnverified();

        var frame = Assert.Single(ReadBack().Frames);
        Assert.Equal(map.Control.Register, frame.StartRegister);
        Assert.Equal(map.Control.Length, frame.Count);
        Assert.Equal(T0.AddMilliseconds(72), frame.ObservedUtc);
    }

    /// <summary>
    /// *** THE CONVERSE, AND IT IS THE ONE THAT MATTERS. *** Publishing must not be a reason a read
    /// happens. The transport's read count is identical with and without a feed attached — so the feed is
    /// a pure forward, and a wave's cost in round trips is unchanged by somebody watching it.
    /// </summary>
    [Fact]
    public void Attaching_a_feed_does_not_change_how_many_reads_the_client_makes()
    {
        var map = SmallMap();

        var without = new RecordingTransport(map);
        var bare = new MirrorClient(map, without, new BuildStamp(0xF52ECEAD));
        bare.ReadControlUnverified();
        bare.ReadControlUnverified();

        var with = new RecordingTransport(map);
        using var publisher = new MirrorFeedPublisher(Feed, () => T0, "test");
        var watched = new MirrorClient(map, with, new BuildStamp(0xF52ECEAD),
            RegisterWordOrder.HighWordFirst, publisher, () => T0);
        watched.ReadControlUnverified();
        watched.ReadControlUnverified();

        Assert.Equal(without.Reads, with.Reads);
        Assert.Equal(bare.RoundTrips, watched.RoundTrips);
    }

    /// <summary>The identity a client publishes is its OWN map and stamp, never something a caller stated.</summary>
    [Fact]
    public void The_published_identity_comes_from_the_clients_own_map_and_stamp()
    {
        var map = SmallMap();

        using var publisher = new MirrorFeedPublisher(Feed, () => T0, "test");
        _ = new MirrorClient(map, new RecordingTransport(map), new BuildStamp(0x12345678),
            RegisterWordOrder.LowWordFirst, publisher, () => T0);

        var identity = ReadBack().Identity;

        Assert.Equal(0x12345678u, identity.BuildStamp);
        Assert.Equal(map.MapHash, identity.MapHash);
        Assert.Equal(map.TotalRegisters, identity.DeclaredRegisters);
        Assert.Equal(RegisterWordOrder.LowWordFirst, identity.WordOrder);
    }

    private static RegisterMap SmallMap() =>
        MapAllocator.Allocate(new WaveSetRequest(
            MirrorGeometry.ForCpu1214C(retentiveBytes: 256, baseByte: 1000, declaredRegisters: (MirrorGeometry.Cpu1214CBitMemoryBytes - 1000) / 2),
            new[] { new SlotRequest("S1", 2, 2) })).Require();

    /// <summary>A transport that answers with zeros and counts. The reads are the measurement.</summary>
    private sealed class RecordingTransport : IRegisterTransport
    {
        private readonly RegisterMap _map;

        public RecordingTransport(RegisterMap map) => _map = map;

        public int Reads { get; private set; }

        public ushort[] ReadHoldingRegisters(int start, int count)
        {
            Reads++;

            var values = new ushort[count];

            // The version register, so ReadControl does not refuse. Everything else stays zero.
            var versionOffset = _map.Version.Register - start;
            if (versionOffset >= 0 && versionOffset + 1 < count)
            {
                values[versionOffset] = 0xF52E;
                values[versionOffset + 1] = 0xCEAD;
            }

            return values;
        }

        public void WriteHoldingRegisters(int start, ushort[] values) =>
            throw new InvalidOperationException("nothing in these tests writes.");

        public void Dispose()
        {
        }
    }
}
