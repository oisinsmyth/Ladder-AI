using System.Reflection;
using Harness.MirrorRead;
using Harness.Wire;

namespace Harness.MirrorRead.Tests;

/// <summary>
/// THE FOUR STATES OF A FEED READ, AND WHY THEY MUST NOT BE THREE.
///
/// <para>🔴 <b>"No wave has ever run here", "a publish did not complete", "the file is there and cannot
/// be read" and "it parsed" have four different remedies</b>, and three of them are not a device problem
/// at all. The one that costs the most is the first two being merged: an absent feed after a publisher
/// has written is a LOST feed, and reporting it as "nothing ever ran" hands a viewer the most
/// benign-looking answer available at the exact moment something has gone wrong.</para>
/// </summary>
public class MirrorFeedSourceTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 17, 9, 30, 0, TimeSpan.Zero);

    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "mirrorfeedsource-" + Guid.NewGuid().ToString("N"));

    private string Feed => Path.Combine(_directory, "wave.mirrorfeed");

    public MirrorFeedSourceTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static MirrorFeedIdentity Identity(int registers = 8) =>
        new(0xF52ECEAD, "abc123", registers, RegisterWordOrder.HighWordFirst);

    /// <summary>A source whose contention pause does nothing, so the persistent case costs no wall time.</summary>
    private FileMirrorFeedSource Source() => new(Feed, _ => { });

    private void WriteFeed(PublisherStatus status = PublisherStatus.Running, params MirrorFeedFrame[] frames) =>
        File.WriteAllText(Feed, MirrorFeed.Format(
            new MirrorFeedDocument("pid7@BENCH", Identity(), status, T0.AddSeconds(1), 3, frames)));

    // ---- 1. NOBODY EVER WROTE HERE -------------------------------------------------------------------

    /// <summary>
    /// No feed and no marker. <b>Legitimate, and it is not a fault</b> — nothing has published to this
    /// path, which is what a viewer started before any wave sees.
    /// </summary>
    [Fact]
    public void No_feed_and_no_marker_is_NeverPublished()
    {
        var read = Source().Read();

        Assert.Equal(MirrorFeedState.NeverPublished, read.State);
        Assert.Null(read.Document);
        Assert.Contains("no record that one was ever written", read.Describe(), StringComparison.Ordinal);
    }

    /// <summary>A directory that does not exist at all is the same fact, not an error.</summary>
    [Fact]
    public void A_missing_directory_is_NeverPublished()
    {
        var source = new FileMirrorFeedSource(Path.Combine(_directory, "nope", "wave.mirrorfeed"), _ => { });

        Assert.Equal(MirrorFeedState.NeverPublished, source.Read().State);
    }

    // ---- 2. SOMEBODY WROTE HERE AND THE FEED IS GONE -------------------------------------------------

    /// <summary>
    /// 🔴 <b>THE DISTINCTION THIS WHOLE MARKER EXISTS FOR.</b> The same absence, with the marker present,
    /// means a publish did not complete or the file was removed — and the viewer must say so rather than
    /// reporting that no wave has ever run. <c>File.Replace</c> is not atomic against process death: it
    /// leaves a window in which the destination genuinely does not exist.
    /// </summary>
    [Fact]
    public void An_absent_feed_with_the_marker_present_is_PublishInterrupted()
    {
        File.WriteAllText(Feed + ".initialised", "published at least once");

        var read = Source().Read();

        Assert.Equal(MirrorFeedState.PublishInterrupted, read.State);
        Assert.Null(read.Document);
        Assert.Contains("did not complete", read.Describe(), StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The two absences are not the same state.</b> Written as a direct comparison so that a change
    /// collapsing them fails here rather than in a viewer test that reads like a rendering detail.
    /// </summary>
    [Fact]
    public void The_two_kinds_of_absence_are_different_states()
    {
        var withoutMarker = Source().Read().State;

        File.WriteAllText(Feed + ".initialised", "published at least once");
        var withMarker = Source().Read().State;

        Assert.NotEqual(withoutMarker, withMarker);
        Assert.Equal(MirrorFeedState.NeverPublished, withoutMarker);
        Assert.Equal(MirrorFeedState.PublishInterrupted, withMarker);
    }

    // ---- 3. IT IS THERE AND CANNOT BE READ -----------------------------------------------------------

    [Fact]
    public void A_truncated_feed_is_Unreadable_rather_than_a_short_reading()
    {
        WriteFeed(PublisherStatus.Running, new MirrorFeedFrame(0, T0, new ushort[] { 1, 2, 3, 4 }));

        var whole = File.ReadAllText(Feed);
        File.WriteAllText(Feed, whole[..(whole.Length / 2)]);

        var read = Source().Read();

        Assert.Equal(MirrorFeedState.Unreadable, read.State);
        Assert.Null(read.Document);
        Assert.Contains("never a short table of registers", read.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_file_is_Unreadable_rather_than_an_empty_mirror()
    {
        File.WriteAllText(Feed, string.Empty);

        Assert.Equal(MirrorFeedState.Unreadable, Source().Read().State);
    }

    [Fact]
    public void Something_that_is_not_a_feed_is_Unreadable()
    {
        File.WriteAllText(Feed, "registers = 1 2 3 4\n");

        Assert.Equal(MirrorFeedState.Unreadable, Source().Read().State);
    }

    /// <summary>
    /// <b>A persistently locked feed is Unreadable, and it says how long it waited.</b> A single failed
    /// open cannot tell a publish in flight from a lock, which is why the source retries — and a caller
    /// reading the refusal can see how hard it tried rather than guessing.
    /// </summary>
    [Fact]
    public void A_locked_feed_is_Unreadable_and_the_refusal_says_how_long_it_waited()
    {
        WriteFeed();

        using var exclusive = new FileStream(Feed, FileMode.Open, FileAccess.Read, FileShare.None);

        var read = Source().Read();

        Assert.Equal(MirrorFeedState.Unreadable, read.State);
        Assert.Contains("attempt(s) over", read.Problem, StringComparison.Ordinal);
        Assert.Contains("persistent lock", read.Problem, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 <b>THE READER DOES NOT BREAK THE WRITER.</b> Measured on this repository's sibling store: a
    /// reader holding the destination open WITHOUT <see cref="FileShare.Delete"/> made the publisher's own
    /// rename fail, and the store then blamed "an antivirus scanner or an indexer" for a defect in its own
    /// read path. Asserted here by doing it: publish while a read handle is open.
    /// </summary>
    [Fact]
    public void A_reader_holding_the_feed_open_does_not_stop_the_publisher()
    {
        using var publisher = new MirrorFeedPublisher(Feed, () => T0, "test");
        publisher.Begin(Identity());

        using (var stream = new FileStream(Feed, FileMode.Open, FileAccess.Read,
                   FileShare.ReadWrite | FileShare.Delete))
        {
            publisher.Publish(0, new ushort[] { 1, 2 }, T0);
        }

        Assert.Equal(0, publisher.Failures);
        Assert.Equal(MirrorFeedState.Ok, Source().Read().State);
    }

    // ---- 4. IT PARSED --------------------------------------------------------------------------------

    [Fact]
    public void A_whole_feed_is_Ok_and_carries_its_document()
    {
        WriteFeed(PublisherStatus.Running,
            new MirrorFeedFrame(0, T0, new ushort[] { 0xF52E, 0xCEAD }),
            new MirrorFeedFrame(4, T0.AddSeconds(1), new ushort[] { 0x0007 }));

        var read = Source().Read();

        Assert.Equal(MirrorFeedState.Ok, read.State);
        Assert.NotNull(read.Document);
        Assert.Equal(2, read.Document!.Frames.Count);
        Assert.Equal(PublisherStatus.Running, read.Document.Status);
        Assert.Equal(string.Empty, read.Problem);
    }

    /// <summary>An ENDED feed still reads Ok — "the wave finished" is content, not a failure to read.</summary>
    [Fact]
    public void An_ended_feed_still_parses_and_says_so()
    {
        WriteFeed(PublisherStatus.Ended, new MirrorFeedFrame(0, T0, new ushort[] { 1, 2 }));

        var read = Source().Read();

        Assert.Equal(MirrorFeedState.Ok, read.State);
        Assert.Equal(PublisherStatus.Ended, read.Document!.Status);
    }

    /// <summary>Every refusal names the path, so a reader has somewhere to go and look.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Every_refusal_names_the_path(bool withMarker)
    {
        if (withMarker) File.WriteAllText(Feed + ".initialised", "x");

        var read = Source().Read();

        Assert.Contains(read.Origin, read.Describe(), StringComparison.Ordinal);
        Assert.Equal(Path.GetFullPath(Feed), read.Origin);
    }

    // ---- THE PORT ITSELF -----------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>THE PORT CARRIES NO VERB THAT COULD ASK FOR FRESHER DATA.</b> The mechanism of the one-socket
    /// design is that a viewer CANNOT cause a read; a <c>Refresh</c> or a <c>ReadFrom(device)</c> appearing
    /// here would hand every call site that capability back without a line of the viewer changing. The same
    /// argument, and the same shape of assertion, as <c>IRegisterSource</c> carrying no write verb.
    /// </summary>
    [Fact]
    public void The_feed_port_carries_only_a_read_of_what_was_already_published()
    {
        var members = typeof(IMirrorFeedSource)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "Origin", nameof(IMirrorFeedSource.Read), "get_Origin" }, members);
    }

    /// <summary>The zero value of the state enum REFUSES, so a dropped or defaulted state cannot display.</summary>
    [Fact]
    public void The_zero_state_is_the_refusing_one()
    {
        Assert.Equal(MirrorFeedState.Unreadable, default(MirrorFeedState));
    }
}
