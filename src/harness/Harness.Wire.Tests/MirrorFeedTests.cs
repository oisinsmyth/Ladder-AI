using Harness.Wire;

namespace Harness.Wire.Tests;

/// <summary>
/// THE FEED FORMAT — the thing that carries "these bytes, at that instant" across a process boundary.
///
/// <para><b>What these tests are FOR.</b> The whole point of the one-socket design is that the page shows
/// exactly what the harness read. That claim survives serialisation only if the format is lossless about
/// the two things it carries — the words and the instants — and only if a document that cannot be fully
/// accounted for is REFUSED rather than half-read. A lenient parser here would turn a truncated file into
/// a short table of registers that looks entirely reasonable.</para>
/// </summary>
public class MirrorFeedTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 17, 9, 30, 0, TimeSpan.Zero);

    private static MirrorFeedIdentity Identity(int registers = 8) =>
        new(0xF52ECEAD, "abc123", registers, RegisterWordOrder.HighWordFirst);

    private static MirrorFeedDocument Document(params MirrorFeedFrame[] frames) =>
        new("pid7@BENCH", Identity(), PublisherStatus.Running, T0.AddSeconds(1), 42, frames);

    // ---- ROUND TRIP ---------------------------------------------------------------------------------

    [Fact]
    public void A_document_round_trips_through_the_format_unchanged()
    {
        var original = Document(
            new MirrorFeedFrame(0, T0, new ushort[] { 0xF52E, 0xCEAD, 0x0000, 0x0011 }),
            new MirrorFeedFrame(4, T0.AddMilliseconds(72), new ushort[] { 0x1234, 0xFFFF }));

        Assert.True(MirrorFeed.TryParse(MirrorFeed.Format(original), out var read, out var problem), problem);

        Assert.Equal(original.PublisherId, read!.PublisherId);
        Assert.Equal(original.Status, read.Status);
        Assert.Equal(original.Sequence, read.Sequence);
        Assert.Equal(original.PublishedUtc, read.PublishedUtc);
        Assert.Equal(original.Identity, read.Identity);
        Assert.Equal(original.Frames.Count, read.Frames.Count);

        for (var i = 0; i < original.Frames.Count; i++)
        {
            Assert.Equal(original.Frames[i].StartRegister, read.Frames[i].StartRegister);
            Assert.Equal(original.Frames[i].ObservedUtc, read.Frames[i].ObservedUtc);
            Assert.Equal(original.Frames[i].Values, read.Frames[i].Values);
        }
    }

    /// <summary>
    /// <b>Sub-second precision survives, and that is not a detail.</b> A read costs ~72 ms to this rig and
    /// a wave publishes many per second; a format that rounded to the second would make every reading in a
    /// poll round look simultaneous and would flatten the very ages the page exists to show.
    /// </summary>
    [Fact]
    public void The_observed_instant_keeps_its_sub_second_precision()
    {
        var precise = T0.AddTicks(1234567);
        var text = MirrorFeed.Format(Document(new MirrorFeedFrame(0, precise, new ushort[] { 1 })));

        Assert.True(MirrorFeed.TryParse(text, out var read, out var problem), problem);
        Assert.Equal(precise, read!.Frames[0].ObservedUtc);
    }

    [Fact]
    public void The_format_is_deterministic()
    {
        var document = Document(new MirrorFeedFrame(0, T0, new ushort[] { 1, 2, 3 }));
        Assert.Equal(MirrorFeed.Format(document), MirrorFeed.Format(document));
    }

    [Fact]
    public void The_format_is_ascii_only()
    {
        var text = MirrorFeed.Format(Document(new MirrorFeedFrame(0, T0, new ushort[] { 0xDEAD })));
        Assert.All(text, c => Assert.True(c < 128, $"non-ASCII character U+{(int)c:X4} in the feed format."));
    }

    // ---- THE TERMINATOR, AND WHY IT EXISTS ----------------------------------------------------------

    /// <summary>
    /// 🔴 <b>A TRUNCATED FEED IS A REFUSAL, NEVER A SHORT READING.</b> Cutting the document at any point
    /// removes the END terminator, and the registers that did arrive are perfectly valid — which is
    /// exactly why displaying them would be wrong. There is no way to know how many did not.
    /// </summary>
    [Theory]
    [InlineData(0.25)]
    [InlineData(0.5)]
    [InlineData(0.9)]
    public void A_truncated_feed_is_refused_rather_than_partly_read(double fraction)
    {
        var text = MirrorFeed.Format(Document(
            new MirrorFeedFrame(0, T0, new ushort[] { 1, 2, 3, 4 }),
            new MirrorFeedFrame(4, T0, new ushort[] { 5, 6, 7, 8 })));

        var cut = text[..(int)(text.Length * fraction)];

        Assert.False(MirrorFeed.TryParse(cut, out var read, out var problem));
        Assert.Null(read);
        Assert.NotEqual(string.Empty, problem);
    }

    /// <summary>
    /// 🔴 <b>THE TERMINATOR'S OWN CASE, AND THE THEORY ABOVE DOES NOT COVER IT.</b>
    ///
    /// <para><b>Found by mutation.</b> Making the parser <i>helpfully fill in</i> a missing terminator left
    /// every truncation case above GREEN: each fractional cut breaks a header or a frame line as well, and
    /// something else refuses first. So those three were passing for a reason that had nothing to do with
    /// the check they appear to be about — <i>a test that passes for the wrong reason is indistinguishable
    /// from one that passes.</i></para>
    ///
    /// <para>This removes the terminator LINE AND NOTHING ELSE. Every header is intact, every frame parses,
    /// and the only thing missing is the statement of how many frames there should have been — which is
    /// exactly what a truncation that happens to land on a line boundary produces, and exactly the document
    /// a lenient parser renders as a short but entirely reasonable table of registers.</para>
    /// </summary>
    [Fact]
    public void A_feed_whose_terminator_alone_is_missing_is_refused()
    {
        var text = MirrorFeed.Format(Document(
            new MirrorFeedFrame(0, T0, new ushort[] { 1, 2, 3, 4 }),
            new MirrorFeedFrame(4, T0, new ushort[] { 5, 6, 7, 8 })));

        var withoutTerminator = string.Join("\n",
            text.Split('\n').Where(l => !l.StartsWith("END ", StringComparison.Ordinal)));

        // The control: everything else about the document is still whole and still parses.
        Assert.True(MirrorFeed.TryParse(withoutTerminator + "\nEND 2\n", out _, out var controlProblem), controlProblem);

        Assert.False(MirrorFeed.TryParse(withoutTerminator, out var read, out var problem));
        Assert.Null(read);
        Assert.Contains("TRUNCATED", problem, StringComparison.Ordinal);
    }

    /// <summary>
    /// The terminator carries a COUNT, and the count is checked. A document whose frames were dropped
    /// while its terminator survived is the case a bare terminator would wave through.
    /// </summary>
    [Fact]
    public void A_frame_count_that_does_not_match_is_refused()
    {
        var text = MirrorFeed.Format(Document(new MirrorFeedFrame(0, T0, new ushort[] { 1 })));
        var tampered = text.Replace("END 1", "END 2", StringComparison.Ordinal);

        Assert.False(MirrorFeed.TryParse(tampered, out _, out var problem));
        Assert.Contains("2 frame(s)", problem, StringComparison.Ordinal);
    }

    /// <summary>Two documents concatenated is neither of them, and both halves are refused together.</summary>
    [Fact]
    public void Content_after_the_terminator_is_refused()
    {
        var one = MirrorFeed.Format(Document(new MirrorFeedFrame(0, T0, new ushort[] { 1 })));

        Assert.False(MirrorFeed.TryParse(one + one, out _, out var problem));
        Assert.Contains("END", problem, StringComparison.Ordinal);
    }

    // ---- EVERY HEADER IS REQUIRED -------------------------------------------------------------------

    /// <summary>
    /// <b>A missing header is a refusal, not a default.</b> A reader that filled one in would be inventing
    /// the very thing it is meant to be checking — the identity, the status, or the instant.
    /// </summary>
    [Theory]
    [InlineData("publisher")]
    [InlineData("status")]
    [InlineData("publishedUtc")]
    [InlineData("sequence")]
    [InlineData("buildStamp")]
    [InlineData("mapHash")]
    [InlineData("declaredRegisters")]
    [InlineData("wordOrder")]
    public void Every_header_is_required(string header)
    {
        var lines = MirrorFeed.Format(Document(new MirrorFeedFrame(0, T0, new ushort[] { 1 })))
            .Split('\n')
            .Where(l => !l.StartsWith(header + " ", StringComparison.Ordinal));

        Assert.False(MirrorFeed.TryParse(string.Join("\n", lines), out _, out var problem));
        Assert.Contains(header, problem, StringComparison.Ordinal);
    }

    [Fact]
    public void A_header_stated_twice_is_refused()
    {
        var text = MirrorFeed.Format(Document(new MirrorFeedFrame(0, T0, new ushort[] { 1 })));
        var doubled = text.Replace("sequence 42\n", "sequence 42\nsequence 43\n", StringComparison.Ordinal);

        Assert.False(MirrorFeed.TryParse(doubled, out _, out var problem));
        Assert.Contains("twice", problem, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 <b>A STATUS THAT IS NEITHER RUNNING NOR ENDED IS REFUSED, NOT DEFAULTED TO EITHER.</b> Whether
    /// the publisher is still alive is the difference between a live page and a final one, and
    /// <see cref="PublisherStatus.Unstated"/> exists so that a dropped value cannot answer that question.
    /// </summary>
    [Fact]
    public void An_unknown_status_is_refused()
    {
        var text = MirrorFeed.Format(Document(new MirrorFeedFrame(0, T0, new ushort[] { 1 })))
            .Replace("status RUNNING", "status PROBABLY", StringComparison.Ordinal);

        Assert.False(MirrorFeed.TryParse(text, out _, out var problem));
        Assert.Contains("RUNNING", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unknown_word_order_is_refused()
    {
        var text = MirrorFeed.Format(Document(new MirrorFeedFrame(0, T0, new ushort[] { 1, 2 })))
            .Replace("wordOrder HighWordFirst", "wordOrder Whichever", StringComparison.Ordinal);

        Assert.False(MirrorFeed.TryParse(text, out _, out var problem));
        Assert.Contains("65 536", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_file_is_refused_rather_than_read_as_an_empty_mirror()
    {
        Assert.False(MirrorFeed.TryParse(string.Empty, out _, out var problem));
        Assert.Contains("empty", problem, StringComparison.OrdinalIgnoreCase);

        Assert.False(MirrorFeed.TryParse(null, out _, out _));
    }

    [Fact]
    public void Something_that_is_not_a_feed_is_refused_by_its_first_line()
    {
        Assert.False(MirrorFeed.TryParse("{\"registers\":[1,2,3]}\n", out _, out var problem));
        Assert.Contains(MirrorFeed.Magic, problem, StringComparison.Ordinal);
    }

    /// <summary>A frame that claims registers the feed does not declare makes the document self-contradictory.</summary>
    [Fact]
    public void A_frame_outside_the_declared_area_is_refused()
    {
        var document = new MirrorFeedDocument("p", Identity(registers: 4), PublisherStatus.Running, T0, 1,
            new[] { new MirrorFeedFrame(2, T0, new ushort[] { 1, 2, 3, 4 }) });

        Assert.False(MirrorFeed.TryParse(MirrorFeed.Format(document), out _, out var problem));
        Assert.Contains("contradicts itself", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void A_frame_with_no_words_is_refused()
    {
        var text = MirrorFeed.Format(Document(new MirrorFeedFrame(0, T0, new ushort[] { 1 })));
        var stripped = text.Replace(" 0001\n", "\n", StringComparison.Ordinal);

        Assert.False(MirrorFeed.TryParse(stripped, out _, out var problem));
        Assert.Contains("frame", problem, StringComparison.Ordinal);
    }

    // ---- COMPOSITION --------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>A REGISTER NO READ COVERED HAS A NULL INSTANT, AND THE ZERO BESIDE IT IS NOT A READING.</b>
    /// This is the single assertion the whole "no data is not old data" rule rests on: the two arrays are
    /// the same length so a consumer cannot hold the values without their provenance.
    /// </summary>
    [Fact]
    public void An_uncovered_register_composes_to_a_null_instant()
    {
        var document = new MirrorFeedDocument("p", Identity(registers: 6), PublisherStatus.Running, T0, 1,
            new[] { new MirrorFeedFrame(0, T0, new ushort[] { 0xAAAA, 0xBBBB }) });

        var composite = document.Compose();

        Assert.Equal(6, composite.Values.Length);
        Assert.Equal(6, composite.ObservedUtc.Length);
        Assert.Equal(2, composite.ObservedRegisters);
        Assert.Equal(4, composite.UnobservedRegisters);

        Assert.Equal(T0, composite.ObservedUtc[0]);
        Assert.Equal(T0, composite.ObservedUtc[1]);
        for (var r = 2; r < 6; r++)
        {
            Assert.Null(composite.ObservedUtc[r]);
            Assert.Equal(0, composite.Values[r]);
        }
    }

    /// <summary>
    /// <b>Where two reads overlap, the LATER READ wins — never the one listed later.</b> Deciding by
    /// listing order would make the answer depend on a serialisation detail, and the two frames genuinely
    /// carry different facts about the same register.
    /// </summary>
    [Fact]
    public void Overlapping_reads_resolve_by_observed_instant_not_by_order()
    {
        var older = new MirrorFeedFrame(0, T0, new ushort[] { 0x1111, 0x1111 });
        var newer = new MirrorFeedFrame(0, T0.AddSeconds(5), new ushort[] { 0x2222, 0x2222 });

        var forwards = new MirrorFeedDocument("p", Identity(2), PublisherStatus.Running, T0, 1, new[] { older, newer });
        var backwards = new MirrorFeedDocument("p", Identity(2), PublisherStatus.Running, T0, 1, new[] { newer, older });

        foreach (var document in new[] { forwards, backwards })
        {
            var composite = document.Compose();
            Assert.Equal(0x2222, composite.Values[0]);
            Assert.Equal(T0.AddSeconds(5), composite.ObservedUtc[0]);
        }
    }

    /// <summary>The oldest and newest are over the REGISTERS that were read, which is what the page ages.</summary>
    [Fact]
    public void The_composite_reports_its_oldest_and_newest_reads()
    {
        var document = new MirrorFeedDocument("p", Identity(4), PublisherStatus.Running, T0, 1, new[]
        {
            new MirrorFeedFrame(0, T0, new ushort[] { 1, 2 }),
            new MirrorFeedFrame(2, T0.AddSeconds(30), new ushort[] { 3, 4 }),
        });

        var composite = document.Compose();

        Assert.Equal(T0, composite.Oldest);
        Assert.Equal(T0.AddSeconds(30), composite.Newest);
    }

    /// <summary>A feed carrying no reads composes to nothing observed — not to a mirror of zeros.</summary>
    [Fact]
    public void A_feed_with_no_frames_composes_to_nothing_observed()
    {
        var composite = new MirrorFeedDocument("p", Identity(4), PublisherStatus.Running, T0, 1,
            Array.Empty<MirrorFeedFrame>()).Compose();

        Assert.Equal(0, composite.ObservedRegisters);
        Assert.Equal(4, composite.UnobservedRegisters);
        Assert.Null(composite.Newest);
        Assert.Null(composite.Oldest);
    }
}
