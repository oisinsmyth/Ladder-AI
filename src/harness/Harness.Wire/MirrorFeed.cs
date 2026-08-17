using System.Globalization;
using System.Text;

namespace Harness.Wire;

/// <summary>
/// Whether the publisher was still running when it last wrote.
///
/// <para>🔴 <b><see cref="Unstated"/> IS THE ZERO VALUE ON PURPOSE.</b> A defaulted or dropped status
/// must not read as "the wave is running" — that is the one value a reader would act on. A document that
/// does not state its status is refused by the parser, so this member is never the answer to a question;
/// it exists so that a struct default cannot answer one either.</para>
/// </summary>
public enum PublisherStatus
{
    /// <summary>Nobody said. Refused by the parser; never a claim.</summary>
    Unstated = 0,

    /// <summary>The publisher was alive and expected to write again.</summary>
    Running = 1,

    /// <summary>The publisher finished and will not write again. The data is FINAL, which is not the same as CURRENT.</summary>
    Ended = 2,
}

/// <summary>
/// What the feed DESCRIBES — the identity a reader must check its own map against before believing a
/// single register.
///
/// <para><b>Why identity travels with the data at all.</b> The viewer parses its register map from the
/// committed IR; the wave derives its map from the binding it is actually running. Those are two
/// independent derivations of one thing, and when they disagree the registers still line up
/// syntactically and mean different signals. That is the failure that renders perfectly.</para>
/// </summary>
/// <param name="BuildStamp">The stamp the running program publishes, as the wave's client expects it.</param>
/// <param name="MapHash">The wave's own map hash.</param>
/// <param name="DeclaredRegisters">Width of the mirror the wave is addressing.</param>
/// <param name="WordOrder">The 32-bit order the wave read under. A reader decoding under the other one is wrong by 65 536.</param>
public sealed record MirrorFeedIdentity(
    uint BuildStamp,
    string MapHash,
    int DeclaredRegisters,
    RegisterWordOrder WordOrder)
{
    /// <summary>The stamp as the rest of this system spells it.</summary>
    public string StampLiteral => "16#" + BuildStamp.ToString("X8", CultureInfo.InvariantCulture);
}

/// <summary>
/// ONE READ THE HARNESS ACTUALLY MADE, with the instant it returned.
///
/// <para>🔴 <b>THIS IS THE UNIT OF TRUTH IN THE WHOLE FEED.</b> A frame is not a snapshot assembled for
/// a viewer; it is the bytes a poll returned to the harness, stamped with when they arrived, and nothing
/// in this system may produce one except the code path that performed the read. Every register a reader
/// displays is displayed with the timestamp of the frame it came from — so a value the harness read
/// forty seconds ago cannot be rendered beside one it read now without saying so.</para>
/// </summary>
/// <param name="StartRegister">First holding register in the read.</param>
/// <param name="ObservedUtc">When the read RETURNED. Never when it was published, and never when it was rendered.</param>
/// <param name="Values">The words, exactly as they came off the wire.</param>
public sealed record MirrorFeedFrame(int StartRegister, DateTimeOffset ObservedUtc, ushort[] Values)
{
    /// <summary>Registers in this read.</summary>
    public int Count => Values.Length;

    /// <summary>One past the last register in this read.</summary>
    public int EndRegister => StartRegister + Values.Length;

    /// <summary>True when this read covered <paramref name="register"/>.</summary>
    public bool Covers(int register) => register >= StartRegister && register < EndRegister;
}

/// <summary>
/// The composed picture: the newest value for each register, and <b>the instant each one was read</b>.
///
/// <para>🔴 <b>A NULL IN <see cref="ObservedUtc"/> IS "NO READ IN THIS FEED COVERED THIS REGISTER", AND
/// THE ZERO BESIDE IT IS NOT A READING.</b> The two arrays are the same length so that no consumer can
/// hold values without their provenance. A consumer that renders <see cref="Values"/> without consulting
/// <see cref="ObservedUtc"/> is showing a table of zeros for registers nobody read, which is the single
/// worst thing this whole design exists to prevent.</para>
/// </summary>
public sealed record MirrorFeedComposite(ushort[] Values, DateTimeOffset?[] ObservedUtc)
{
    /// <summary>Registers this composite has an actual reading for.</summary>
    public int ObservedRegisters => ObservedUtc.Count(t => t is not null);

    /// <summary>Registers in the declared area that no read covered.</summary>
    public int UnobservedRegisters => ObservedUtc.Length - ObservedRegisters;

    /// <summary>The most recent read behind any register here, or null when nothing was observed.</summary>
    public DateTimeOffset? Newest => ObservedRegisters > 0
        ? ObservedUtc.Where(t => t is not null).Select(t => t!.Value).Max()
        : null;

    /// <summary>The oldest read behind any register here, or null when nothing was observed.</summary>
    public DateTimeOffset? Oldest => ObservedRegisters > 0
        ? ObservedUtc.Where(t => t is not null).Select(t => t!.Value).Min()
        : null;
}

/// <summary>
/// One published feed document, and the format both halves of the process boundary agree on.
/// </summary>
/// <param name="PublisherId">Who wrote it. For a human reading the file, and for telling two runs apart.</param>
/// <param name="Identity">What the feed describes. Checked by the reader against its own map.</param>
/// <param name="Status">Whether the publisher expected to write again.</param>
/// <param name="PublishedUtc">When this DOCUMENT was written. Distinct from any frame's observed time.</param>
/// <param name="Sequence">Publishes since the publisher began. A reader can see the feed advancing.</param>
/// <param name="Frames">The reads, newest-per-range. May be empty: a publisher that has begun and read nothing.</param>
public sealed record MirrorFeedDocument(
    string PublisherId,
    MirrorFeedIdentity Identity,
    PublisherStatus Status,
    DateTimeOffset PublishedUtc,
    long Sequence,
    IReadOnlyList<MirrorFeedFrame> Frames)
{
    /// <summary>
    /// The newest value for every register in the declared area, each with the instant it was read.
    ///
    /// <para>Where two reads cover one register, the one with the later <see cref="MirrorFeedFrame.ObservedUtc"/>
    /// wins — never the one listed later, which would make the answer depend on a serialisation order.</para>
    /// </summary>
    public MirrorFeedComposite Compose()
    {
        var values = new ushort[Identity.DeclaredRegisters];
        var observed = new DateTimeOffset?[Identity.DeclaredRegisters];

        foreach (var frame in Frames)
        {
            for (var i = 0; i < frame.Values.Length; i++)
            {
                var register = frame.StartRegister + i;
                if (register < 0 || register >= values.Length) continue;

                if (observed[register] is { } already && already >= frame.ObservedUtc) continue;

                values[register] = frame.Values[i];
                observed[register] = frame.ObservedUtc;
            }
        }

        return new MirrorFeedComposite(values, observed);
    }
}

/// <summary>
/// 🔴 <b>THE WIRE FORMAT OF THE FEED — one definition, shared by the publisher and the reader.</b>
///
/// <para><b>Why it is a format at all rather than a serializer call.</b> The publisher lives in the
/// harness process and the reader lives in the viewer process, and the property being carried across
/// that boundary is <i>these are the exact bytes the harness acted on, at the exact instant it read
/// them</i>. Two hand-written projections would be two places for that claim to drift; a lenient
/// deserializer would turn a truncated file into a plausible half-document. So: one formatter, one
/// parser, and a parse that REFUSES anything it cannot fully account for.</para>
///
/// <para><b>The terminator is load-bearing.</b> Every document ends with <c>END &lt;frameCount&gt;</c>,
/// and the parser checks the count against the frames it actually read. Combined with the atomic rename
/// in <c>MirrorFeedPublisher</c> that is belt AND braces: the rename means a reader should never see a
/// half-written file, and the terminator means that if one ever does — a truncated copy, a partially
/// flushed network share, a hand-edited file — it is a REFUSAL rather than a short table of registers
/// that looks entirely reasonable.</para>
///
/// <para><b>ASCII, LF, and no BOM.</b> The file is read by tools and by people on a Windows box where
/// a UTF-8 em dash has already cost this project a day.</para>
/// </summary>
public static class MirrorFeed
{
    /// <summary>The first line of every document. A file that does not start with this is not a feed.</summary>
    public const string Magic = "MIRRORFEED 1";

    /// <summary>How an instant is written. Round-trippable, sortable, and unambiguous about the zone.</summary>
    private const string TimeFormat = "yyyy-MM-ddTHH:mm:ss.fffffffZ";

    private static readonly char[] Space = { ' ' };

    /// <summary>Render a document. Deterministic: same input, same bytes.</summary>
    public static string Format(MirrorFeedDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var text = new StringBuilder();
        text.Append(Magic).Append('\n');
        text.Append("publisher ").Append(document.PublisherId).Append('\n');
        text.Append("status ").Append(document.Status == PublisherStatus.Ended ? "ENDED" : "RUNNING").Append('\n');
        text.Append("publishedUtc ").Append(Time(document.PublishedUtc)).Append('\n');
        text.Append("sequence ").Append(document.Sequence.ToString(CultureInfo.InvariantCulture)).Append('\n');
        text.Append("buildStamp ").Append(document.Identity.StampLiteral).Append('\n');
        text.Append("mapHash ").Append(document.Identity.MapHash).Append('\n');
        text.Append("declaredRegisters ").Append(document.Identity.DeclaredRegisters.ToString(CultureInfo.InvariantCulture)).Append('\n');
        text.Append("wordOrder ").Append(document.Identity.WordOrder.ToString()).Append('\n');

        foreach (var frame in document.Frames.OrderBy(f => f.StartRegister).ThenBy(f => f.Count))
        {
            text.Append("frame ")
                .Append(frame.StartRegister.ToString(CultureInfo.InvariantCulture)).Append(' ')
                .Append(Time(frame.ObservedUtc));

            foreach (var word in frame.Values)
                text.Append(' ').Append(word.ToString("X4", CultureInfo.InvariantCulture));

            text.Append('\n');
        }

        text.Append("END ").Append(document.Frames.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
        return text.ToString();
    }

    /// <summary>
    /// Read a document back. <b>Returns false and a reason for anything it cannot fully account for</b> —
    /// there is no partial success, because half a feed is a table of registers that looks fine.
    /// </summary>
    public static bool TryParse(string? text, out MirrorFeedDocument? document, out string problem)
    {
        document = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            problem = "the feed is empty. An empty file is not an empty mirror: nothing here was read from any device.";
            return false;
        }

        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Where(l => l.Length > 0)
            .ToArray();

        if (lines.Length == 0 || !string.Equals(lines[0], Magic, StringComparison.Ordinal))
        {
            problem = $"the first line is '{(lines.Length == 0 ? string.Empty : lines[0])}' and every feed begins with '{Magic}'. " +
                      "This is not a mirror feed, or it is a newer format this build cannot read.";
            return false;
        }

        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        var frames = new List<MirrorFeedFrame>();
        string? terminator = null;

        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i];

            if (terminator is not null)
            {
                problem = $"line {i + 1} follows the END terminator. A document with content after its own end is one " +
                          "that was appended to or concatenated with another, and neither half can be trusted.";
                return false;
            }

            var parts = line.Split(Space, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;

            switch (parts[0])
            {
                case "frame":
                    if (!TryFrame(parts, i + 1, out var frame, out problem)) return false;
                    frames.Add(frame!);
                    break;

                case "END":
                    terminator = parts.Length == 2 ? parts[1] : null;
                    if (terminator is null)
                    {
                        problem = $"line {i + 1}: the END terminator must state a frame count.";
                        return false;
                    }

                    break;

                default:
                    if (parts.Length < 2)
                    {
                        problem = $"line {i + 1}: '{parts[0]}' carries no value.";
                        return false;
                    }

                    if (headers.ContainsKey(parts[0]))
                    {
                        problem = $"line {i + 1}: '{parts[0]}' is stated twice. Two answers to one question is not a document " +
                                  "a reader may pick from.";
                        return false;
                    }

                    headers[parts[0]] = string.Join(" ", parts.Skip(1));
                    break;
            }
        }

        // *** THE TERMINATOR, AND THE COUNT IT CARRIES. *** A truncated file loses its last lines, so the
        // absence of END is what a torn read looks like; a file cut mid-frame keeps an END from an older,
        // longer document only if something appended, which the check above already refuses.
        if (terminator is null)
        {
            problem = "the feed has no END terminator, so it is TRUNCATED. This is a refusal and not a short " +
                      "reading: the registers that did arrive are real, and there is no way to know how many did not.";
            return false;
        }

        if (!int.TryParse(terminator, NumberStyles.None, CultureInfo.InvariantCulture, out var declaredFrames))
        {
            problem = $"the END terminator reads '{terminator}', which is not a frame count.";
            return false;
        }

        if (declaredFrames != frames.Count)
        {
            problem = $"the END terminator declares {declaredFrames} frame(s) and {frames.Count} were read. " +
                      "The document is incomplete or was written by something that miscounted; either way it is not " +
                      "safe to display part of it.";
            return false;
        }

        foreach (var required in new[] { "publisher", "status", "publishedUtc", "sequence", "buildStamp", "mapHash", "declaredRegisters", "wordOrder" })
        {
            if (!headers.ContainsKey(required))
            {
                problem = $"the feed does not state '{required}'. Every header is required: a reader that filled one in " +
                          "would be inventing the very thing it is meant to be checking.";
                return false;
            }
        }

        var status = headers["status"] switch
        {
            "RUNNING" => PublisherStatus.Running,
            "ENDED" => PublisherStatus.Ended,
            _ => PublisherStatus.Unstated,
        };

        if (status == PublisherStatus.Unstated)
        {
            problem = $"the feed's status reads '{headers["status"]}', which is neither RUNNING nor ENDED. " +
                      "Whether the publisher is still alive is the difference between a live view and a final one.";
            return false;
        }

        if (!TryTime(headers["publishedUtc"], out var publishedUtc))
        {
            problem = $"publishedUtc reads '{headers["publishedUtc"]}', which is not an instant.";
            return false;
        }

        if (!long.TryParse(headers["sequence"], NumberStyles.None, CultureInfo.InvariantCulture, out var sequence))
        {
            problem = $"sequence reads '{headers["sequence"]}', which is not a whole number.";
            return false;
        }

        if (!TryStamp(headers["buildStamp"], out var stamp))
        {
            problem = $"buildStamp reads '{headers["buildStamp"]}', which is not a 16#XXXXXXXX literal.";
            return false;
        }

        if (!int.TryParse(headers["declaredRegisters"], NumberStyles.None, CultureInfo.InvariantCulture, out var declaredRegisters)
            || declaredRegisters < 1)
        {
            problem = $"declaredRegisters reads '{headers["declaredRegisters"]}', and a mirror of no registers is " +
                      "not one anything can be shown from.";
            return false;
        }

        if (!Enum.TryParse<RegisterWordOrder>(headers["wordOrder"], ignoreCase: false, out var wordOrder))
        {
            problem = $"wordOrder reads '{headers["wordOrder"]}', which names no order this build knows. " +
                      "Decoding a 32-bit value under the wrong order is wrong by up to 65 536 and looks plausible.";
            return false;
        }

        foreach (var frame in frames)
        {
            if (frame.EndRegister > declaredRegisters)
            {
                problem = $"a frame covers registers {frame.StartRegister}..{frame.EndRegister - 1} and the feed declares " +
                          $"{declaredRegisters}. The document contradicts itself, so neither half of it is evidence.";
                return false;
            }
        }

        document = new MirrorFeedDocument(
            headers["publisher"],
            new MirrorFeedIdentity(stamp, headers["mapHash"], declaredRegisters, wordOrder),
            status,
            publishedUtc,
            sequence,
            frames);

        problem = string.Empty;
        return true;
    }

    private static bool TryFrame(string[] parts, int lineNumber, out MirrorFeedFrame? frame, out string problem)
    {
        frame = null;

        if (parts.Length < 4)
        {
            problem = $"line {lineNumber}: a frame is 'frame <startRegister> <observedUtc> <word>...' and this one " +
                      "carries no words. A read that returned nothing is not a frame.";
            return false;
        }

        if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var start))
        {
            problem = $"line {lineNumber}: '{parts[1]}' is not a register index.";
            return false;
        }

        if (!TryTime(parts[2], out var observed))
        {
            problem = $"line {lineNumber}: '{parts[2]}' is not an instant. Every frame carries the moment the read " +
                      "RETURNED, and a frame without one cannot be aged.";
            return false;
        }

        var values = new ushort[parts.Length - 3];
        for (var i = 0; i < values.Length; i++)
        {
            if (!ushort.TryParse(parts[i + 3], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out values[i]))
            {
                problem = $"line {lineNumber}: word {i} reads '{parts[i + 3]}', which is not a 16-bit hex register value.";
                return false;
            }
        }

        frame = new MirrorFeedFrame(start, observed, values);
        problem = string.Empty;
        return true;
    }

    private static string Time(DateTimeOffset instant) =>
        instant.ToUniversalTime().ToString(TimeFormat, CultureInfo.InvariantCulture);

    private static bool TryTime(string text, out DateTimeOffset instant) =>
        DateTimeOffset.TryParseExact(text, TimeFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out instant);

    private static bool TryStamp(string text, out uint stamp)
    {
        stamp = 0;
        return text.StartsWith("16#", StringComparison.Ordinal)
               && uint.TryParse(text.AsSpan(3), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out stamp);
    }
}
