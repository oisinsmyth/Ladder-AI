using Harness.Wire;

namespace Harness.MirrorView;

/// <summary>One row of the table — one holding register.</summary>
/// <param name="Index">Register index, 0-based. The primary key of the whole screen.</param>
/// <param name="MAddress">The <c>%M</c> address, from the map.</param>
/// <param name="Tag">Tag name, or null when no tag in the map covers this register.</param>
/// <param name="TypeName">IR type name, or null when unmapped.</param>
/// <param name="Raw">This register's own word, as <c>16#XXXX</c>. Null when there is no reading.</param>
/// <param name="Decoded">The value, or null when unmapped / undecodable / not read.</param>
/// <param name="Basis">How the decode was reached, including the word order used.</param>
/// <param name="Role">
/// <c>value</c> for a single-register element or the FIRST register of a pair, <c>continuation</c> for
/// the second register of a 32-bit element, <c>unmapped</c> for a register no tag names.
/// </param>
/// <param name="Comment">The tag's own COMMENT text from the IR — the meaning column.</param>
/// <param name="ObservedUtc">
/// 🔴 <b>WHEN THE READ BEHIND THIS ROW RETURNED.</b> In DIRECT mode every row shares the poll's instant,
/// because one FC03 covers the whole area. In FOLLOW mode the picture is composed from the several reads
/// a wave actually makes, so rows legitimately have DIFFERENT AGES and each states its own. Null means no
/// read has covered this register — and the <c>Raw</c> beside it is then null too, never a zero.
/// </param>
/// <param name="AgeSeconds">How old that read is. Null when there is none.</param>
/// <param name="Current">
/// True only when this row's own reading is inside the freshness window. <b>Per row, so a page composed
/// from reads of different ages cannot present the old ones as current</b> — the whole-table treatment
/// remains as well, and in direct mode the two always agree because every row shares one instant.
/// </param>
public sealed record RegisterRow(
    int Index,
    string MAddress,
    string? Tag,
    string? TypeName,
    string? Raw,
    string? Decoded,
    string? Basis,
    string Role,
    string? Comment,
    DateTimeOffset? ObservedUtc = null,
    double? AgeSeconds = null,
    bool Current = false);

/// <summary>What the scan counter says about whether the copy layer is EXECUTING.</summary>
/// <param name="Value">The counter as published, unsigned. Null when it has not been read.</param>
/// <param name="Previous">The reading before it, or null when there has only been one.</param>
/// <param name="Advance">Scans between the two, modular. Null when there is no pair.</param>
/// <param name="IntervalMs">Milliseconds between those two readings.</param>
/// <param name="Advancing">
/// True, false, or <b>null for "not yet established"</b> — one reading can never answer this, and
/// answering it anyway is how a static value from a past download comes to look like a running program.
/// </param>
/// <param name="Note">What the answer does and does not license.</param>
public sealed record ScanReport(
    uint? Value,
    uint? Previous,
    long? Advance,
    long? IntervalMs,
    bool? Advancing,
    string Note);

/// <summary>
/// Everything the page shows, computed from the state and a clock. <b>A pure function of its inputs</b>,
/// which is what makes the staleness rule testable without waiting for time to pass.
/// </summary>
public sealed record MirrorViewModel(
    MirrorStatus Status,
    string StatusText,
    DateTimeOffset GeneratedUtc,
    DateTimeOffset? ObservedUtc,
    double? AgeSeconds,
    double StaleAfterSeconds,
    double PollIntervalSeconds,
    string Target,
    string AllowlistPath,
    string MapSource,
    string AreaSource,
    int BaseByte,
    int DeclaredRegisters,
    string LastAttemptOutcome,
    string LastAttemptDetail,
    DateTimeOffset? LastAttemptUtc,
    long LastAttemptElapsedMs,
    string? BuildStamp,
    ScanReport Scan,
    IReadOnlyList<RegisterRow> Rows,
    IReadOnlyList<int> UnmappedRegisters,

    /// <summary>
    /// Where a FOLLOWED reading came from, or null in direct mode. <b>The page prints it whenever it is
    /// present</b>: a viewer showing another process's reads must say so, or it is an unattributed table
    /// that looks exactly like a direct one.
    /// </summary>
    FeedProvenance? Feed = null,

    /// <summary>Registers whose own reading is inside the freshness window.</summary>
    int RegistersCurrent = 0,

    /// <summary>
    /// Registers holding a reading that is OUTSIDE the window. <b>Printed on every run including zero</b> —
    /// a count that appears only when it is non-zero teaches a reader that its absence means everything
    /// was fresh, when it may mean nothing was counted.
    /// </summary>
    int RegistersStale = 0,

    /// <summary>Registers no read has covered. Shown as NOT READ, never as a value.</summary>
    int RegistersNotObserved = 0)
{
    /// <summary>True only when the numbers on screen were read just now by a poll that succeeded.</summary>
    public bool ValuesAreCurrent => Status == MirrorStatus.Live;

    /// <summary>True when this model was built from a wave's published feed rather than from a socket.</summary>
    public bool IsFollowing => Feed is not null;
}

/// <summary>
/// Builds the view model — <b>the one place the "is this current?" question is answered.</b>
/// </summary>
public static class MirrorView
{
    /// <summary>Register index of the build stamp, by the map's own convention (a DWord at register 0).</summary>
    private const int BuildStampRegister = 0;

    /// <summary>Register index of the scan counter (a DInt at register 2).</summary>
    private const int ScanCounterRegister = 2;

    public static MirrorViewModel Build(MirrorMap map, MirrorViewOptions options, MirrorState state, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(state);

        var (lastAttempt, lastSuccess, previousSuccess) = state.Snapshot();

        var age = lastSuccess is null ? (double?)null : (now - lastSuccess.At).TotalSeconds;
        var staleAfter = options.StaleAfterMs / 1000.0;

        var (status, statusText) = Classify(lastAttempt, lastSuccess, age, staleAfter, now);

        var registers = lastSuccess?.Registers ?? Array.Empty<ushort>();

        // *** ONE READING PER REGISTER, WITH THE INSTANT IT WAS READ. *** Direct mode leaves this null and
        // every row falls back to the poll's own instant, which is correct there by construction: one FC03
        // covers the whole area, so every register genuinely was read at that moment. Follow mode fills it,
        // because a wave reads the control region and each slot's results separately and the ages differ.
        var rows = RowsOf(map, registers, lastSuccess?.RegisterObservedUtc, lastSuccess?.At, now, staleAfter);

        return new MirrorViewModel(
            Status: status,
            StatusText: statusText,
            GeneratedUtc: now,
            ObservedUtc: lastSuccess?.At,
            AgeSeconds: age,
            StaleAfterSeconds: staleAfter,
            PollIntervalSeconds: options.PollIntervalMs / 1000.0,
            // 🔴 *** THROUGH THE OPTIONS' OWN Describe, NOT REBUILT HERE. *** This line read
            // `$"{Address}:{Port} unit {UnitId}"` and, in follow mode, rendered `:503 unit 1` at the top of
            // the page — a plausible-looking target for a viewer that had opened no socket at all. FOUND BY
            // RUNNING THE BINARY, not by any test: `Describe` existed, was correct, was printed in the
            // console banner, and nothing had wired it to the page. A value with two readers has as many
            // truths as it has readers.
            Target: options.Describe,
            AllowlistPath: options.AllowlistPath ?? "<none configured>",
            MapSource: map.TagTableSource,
            AreaSource: map.AreaPointerSource,
            BaseByte: map.BaseByte,
            DeclaredRegisters: map.DeclaredRegisters,
            LastAttemptOutcome: lastAttempt?.Outcome.ToString() ?? "None",
            LastAttemptDetail: lastAttempt?.Detail ?? "no poll has been attempted yet.",
            LastAttemptUtc: lastAttempt?.At,
            LastAttemptElapsedMs: lastAttempt?.ElapsedMs ?? 0,
            BuildStamp: StampOf(registers, lastSuccess?.RegisterObservedUtc),
            Scan: ScanOf(lastSuccess, previousSuccess),
            Rows: rows,
            UnmappedRegisters: map.UnmappedRegisters,
            Feed: lastSuccess?.Feed ?? lastAttempt?.Feed,

            // 🔴 *** THE DENOMINATOR IS REGISTERS, AND EVERY ROW IS ONE REGISTER. *** Continuation rows are
            // counted like any other: a 32-bit element's second register is a register the server publishes
            // and this page displays, with its own reading and its own age. Counting elements instead would
            // make the three numbers sum to something smaller than `DeclaredRegisters` for no stated
            // reason, and a count whose denominator is not the one on screen is a count nobody can check.
            RegistersCurrent: rows.Count(r => r.Current),
            RegistersStale: rows.Count(r => r.ObservedUtc is not null && !r.Current),
            RegistersNotObserved: rows.Count(r => r.ObservedUtc is null));
    }

    /// <summary>
    /// 🔴 <b>THE RULE.</b> Green requires two things at once: the last ATTEMPT succeeded, and its reading
    /// is inside the freshness window. Either one alone is a way for old numbers to look current — a
    /// failing poller still has a recent-ish reading for a while, and a stalled loop still has a
    /// last attempt that "succeeded".
    /// </summary>
    private static (MirrorStatus, string) Classify(
        PollAttempt? lastAttempt, PollAttempt? lastSuccess, double? age, double staleAfter, DateTimeOffset now)
    {
        if (lastAttempt is null)
        {
            return (MirrorStatus.NeverRead,
                "NO POLL HAS BEEN ATTEMPTED YET. Nothing on this page has been read from the device.");
        }

        // ---- FOLLOW MODE, FIRST, BECAUSE NONE OF THESE IS A STATEMENT ABOUT A DEVICE -----------------
        //
        // 🔴 Every one of these renders as its own banner with its own words. The temptation is to fold
        // them into "failing" — and "no wave has ever run here" and "the wave died mid-run" have nothing
        // in common except that neither produced a number.
        if (lastAttempt.Outcome is PollOutcome.FeedNeverPublished)
        {
            return (MirrorStatus.NoFeed,
                "NO PUBLISHER HAS EVER WRITTEN TO THIS FEED. No wave has run with --publish pointed here. " +
                "Nothing is broken and nothing is stale: there is no reading, and there never was one.");
        }

        if (lastAttempt.Outcome is PollOutcome.FeedPublishInterrupted)
        {
            return (MirrorStatus.FeedInterrupted,
                "A PUBLISHER WROTE TO THIS FEED AND THE FILE IS NOT THERE NOW. This is NOT 'no wave has run' " +
                "- something published here. Either a publish did not complete, or the feed was deleted. " +
                "Nothing below was read under it.");
        }

        if (lastAttempt.Outcome is PollOutcome.FeedUnreadable)
        {
            return (MirrorStatus.FeedUnreadable,
                "THE FEED IS PRESENT AND COULD NOT BE FULLY ACCOUNTED FOR. A document that cannot be read " +
                "whole is a REFUSAL, never a short table of registers: the values that did parse are real and " +
                "there is no way to know which ones did not.");
        }

        if (lastAttempt.Outcome is PollOutcome.FeedDescribesADifferentMirror)
        {
            return (MirrorStatus.FeedMismatch,
                "THE FEED AND THIS VIEWER'S MAP DESCRIBE DIFFERENT MIRRORS. The registers would still line up " +
                "and would mean different signals, so nothing is shown. This renders perfectly when it is " +
                "wrong, which is why it refuses.");
        }

        if (lastAttempt.Outcome is PollOutcome.FeedCarriesNoReading)
        {
            return (MirrorStatus.FeedCarriesNoReading,
                "A PUBLISHER IS THERE AND NO READ HAS LANDED YET. The wave has begun and its first poll has " +
                "not returned. There is nothing to show - which is not the same as a mirror full of zeros.");
        }

        if (lastAttempt.Outcome is PollOutcome.RefusedByFence)
        {
            return (MirrorStatus.Refused,
                "THE DEVICE FENCE REFUSED THIS TARGET — no socket was opened. This is a governance " +
                "decision, not a network fault, and no value below was read under it.");
        }

        if (lastAttempt.Outcome is PollOutcome.FenceFault)
        {
            return (MirrorStatus.FenceFault,
                "THE DEVICE FENCE FAULTED. Nothing examined the target, so this is neither a refusal nor " +
                "a reading — the run measured nothing and says so.");
        }

        if (!lastAttempt.Ok)
        {
            return (MirrorStatus.Failing, lastSuccess is null
                ? "NO POLL HAS EVER SUCCEEDED. There are no values to show."
                : "THE LAST POLL DID NOT PRODUCE A READING. Every value below is from an EARLIER poll and " +
                  "is NOT current — its age is stated beside it.");
        }

        if (lastSuccess is null)
        {
            // Unreachable through Publish (an Ok attempt is recorded as a success), and asserted rather
            // than assumed: if it ever happens, saying "live" would be a green over nothing.
            return (MirrorStatus.NeverRead,
                "the last attempt reports success but no reading was stored. That is a defect in this " +
                "viewer; nothing below may be read as current.");
        }

        // ---- A NEGATIVE AGE IS NOT A FRESH ONE -------------------------------------------------------
        //
        // 🔴 In follow mode the instants come from ANOTHER PROCESS's clock. On the intended deployment
        // that is the same machine and the same clock, so a reading in this viewer's future means the
        // assumption has broken — and the arithmetic then makes an ANCIENT reading look extraordinarily
        // fresh. Refused rather than clamped: a clamp would hide the one input that has gone wrong.
        if (age is < -ClockToleranceSeconds)
        {
            return (MirrorStatus.ClockDisagreement,
                $"THE READING IS {(-age.Value):F1} s IN THIS VIEWER'S FUTURE. Ages are computed against the " +
                "instant the harness's own read returned, and a negative age would render an old reading as " +
                "an extremely fresh one. Nothing below may be read as current until the two clocks agree.");
        }

        // ---- FOLLOW MODE: THE PUBLISHER'S OWN LIVENESS, BEFORE THE DATA'S AGE ------------------------
        //
        // *** ENDED WINS OVER EVERY FRESHNESS VERDICT, INCLUDING A FRESH ONE. *** A wave that finished one
        // second ago has a reading well inside the window and is not LIVE in any sense a reader cares
        // about: nothing further is coming. "Final" and "current" are different facts and only one of them
        // is green.
        if (lastAttempt.Feed is { } feed)
        {
            if (feed.Status == PublisherStatus.Ended)
            {
                return (MirrorStatus.PublisherEnded,
                    $"THE PUBLISHER FINISHED. The wave ended and said so; the newest reading below is " +
                    $"{age?.ToString("F1") ?? "?"} s old and is FINAL, not current. Nothing further will arrive on " +
                    "this feed. Values below are the last ones the harness actually read.");
            }

            var sincePublish = (now - feed.PublishedUtc).TotalSeconds;
            if (sincePublish > staleAfter)
            {
                return (MirrorStatus.PublisherStopped,
                    $"THE PUBLISHER WAS RUNNING AND HAS STOPPED WRITING - last publish {sincePublish:F1} s ago, " +
                    $"past the {staleAfter:F1} s window, and it never said it had finished. The wave died, was " +
                    "killed, or is wedged. Every value below is from BEFORE that, and none of it is current.");
            }
        }

        if (age is null || age > staleAfter)
        {
            return (MirrorStatus.Stale,
                $"STALE: the newest reading is {age?.ToString("F1") ?? "?"} s old, past the " +
                $"{staleAfter:F1} s freshness window. The poller is not delivering — these numbers are not now.");
        }

        return (MirrorStatus.Live, "LIVE: the last poll succeeded and its reading is inside the freshness window.");
    }

    /// <summary>
    /// How far a followed reading may sit in this viewer's future before the clocks are called into
    /// question. Publisher and viewer are expected on ONE machine, so any real negative age is a problem —
    /// this only absorbs the sub-second jitter of two <c>DateTimeOffset.UtcNow</c> calls.
    /// </summary>
    private const double ClockToleranceSeconds = 2.0;

    /// <summary>
    /// The build stamp, <b>only when both of its registers were actually read</b>.
    ///
    /// <para>🔴 In follow mode the picture is composed from the reads a wave made, and a register nobody
    /// read holds a default. Reassembling a stamp from one read half and one default produces
    /// <c>16#XXXX0000</c> — a plausible-looking literal that names no build, printed in the card a reader
    /// consults to answer "is the right program running?". Withheld instead.</para>
    /// </summary>
    private static string? StampOf(IReadOnlyList<ushort> registers, DateTimeOffset?[]? observed)
    {
        if (registers.Count < BuildStampRegister + 2) return null;
        if (!WholeElementObserved(observed, BuildStampRegister, 2)) return null;

        var value = RegisterWords.To32(registers[BuildStampRegister], registers[BuildStampRegister + 1], RegisterDecode.Order);
        return "16#" + value.ToString("X8", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// When the read behind one register returned. Falls back to the attempt's own instant, which is the
    /// truth in direct mode and the best available in follow mode when the mask is short.
    /// </summary>
    private static DateTimeOffset ObservedAtRegister(PollAttempt attempt, int register)
    {
        var mask = attempt.RegisterObservedUtc;

        return mask is not null && register >= 0 && register < mask.Length && mask[register] is { } observed
            ? observed
            : attempt.At;
    }

    /// <summary>
    /// Whether an element's every register carries a real reading. <b>Null mask means direct mode</b>,
    /// where the single transaction covered the whole area by construction.
    /// </summary>
    private static bool WholeElementObserved(DateTimeOffset?[]? observed, int first, int count)
    {
        if (observed is null) return true;

        for (var i = first; i < first + count; i++)
        {
            if (i < 0 || i >= observed.Length || observed[i] is null) return false;
        }

        return true;
    }

    /// <summary>
    /// 🔴 <b>THE COUNTER, AND WHY IT IS ON THE SCREEN AT ALL.</b> A build stamp is static: it cannot
    /// tell "the layer is running" from "a value from a past download is sitting in memory". A rising
    /// counter can, and nothing else here can.
    ///
    /// <para>Null <c>Advancing</c> until there are two readings. The difference is modular
    /// (<c>Harness.Wire.ScanCount</c>), so it is correct across the wrap and can never be negative — and
    /// an implausibly large "advance" is reported as a counter that most likely went BACKWARDS rather
    /// than absorbed as liveness.</para>
    /// </summary>
    private static ScanReport ScanOf(PollAttempt? lastSuccess, PollAttempt? previousSuccess)
    {
        if (lastSuccess is null || lastSuccess.Registers.Length < ScanCounterRegister + 2
            || !WholeElementObserved(lastSuccess.RegisterObservedUtc, ScanCounterRegister, 2))
        {
            return new ScanReport(null, null, null, null, null,
                "NOT ESTABLISHED — the scan counter has not been read. Nothing here says whether the copy " +
                "layer is executing.");
        }

        var now = ScanCount.FromRegisters(
            lastSuccess.Registers[ScanCounterRegister], lastSuccess.Registers[ScanCounterRegister + 1], RegisterDecode.Order);

        if (previousSuccess is null || previousSuccess.Registers.Length < ScanCounterRegister + 2
            || !WholeElementObserved(previousSuccess.RegisterObservedUtc, ScanCounterRegister, 2))
        {
            return new ScanReport(now.Raw, null, null, null, null,
                "NOT ESTABLISHED — one reading cannot say whether a counter is advancing. A static value " +
                "from a past download reads exactly like a running program until a second reading arrives.");
        }

        var before = ScanCount.FromRegisters(
            previousSuccess.Registers[ScanCounterRegister], previousSuccess.Registers[ScanCounterRegister + 1], RegisterDecode.Order);

        var advance = now.Since(before);

        // 🔴 *** THE INTERVAL IS BETWEEN THE TWO READS OF THE COUNTER, NOT BETWEEN THE TWO PAGES. *** In
        // follow mode a composed picture's own instant is the NEWEST read behind any register, and the
        // scan counter may not be that read — so differencing the picture instants would divide a real
        // scan advance by the wrong milliseconds and publish a confident wrong "ms per scan". Each side
        // supplies the instant of the read that produced ITS counter, falling back to the picture's own
        // instant in direct mode, where they are the same thing by construction.
        var interval = (long)(ObservedAtRegister(lastSuccess, ScanCounterRegister)
                              - ObservedAtRegister(previousSuccess, ScanCounterRegister)).TotalMilliseconds;

        if (advance == 0)
        {
            return new ScanReport(now.Raw, before.Raw, 0, interval, false,
                $"NOT ADVANCING — the counter read {now.Raw} twice, {interval} ms apart. Either the CPU is " +
                "not executing the copy layer, or these two readings were not separated in time.");
        }

        if (!now.IsPlausibleAdvanceFrom(before))
        {
            return new ScanReport(now.Raw, before.Raw, advance, interval, false,
                $"WENT BACKWARDS — {before.Raw} to {now.Raw} is a modular advance of {advance}, past the " +
                "plausible ceiling, so the counter most likely restarted or a different program is running. " +
                "Reading that as an advance would manufacture liveness.");
        }

        var perScan = interval > 0 ? $" ({interval / (double)advance:F2} ms per scan, DERIVED from two readings)" : string.Empty;
        return new ScanReport(now.Raw, before.Raw, advance, interval, true,
            $"ADVANCING — {advance} scan(s) in {interval} ms{perScan}. The copy layer is EXECUTING, which is " +
            "the one thing the build stamp cannot tell you.");
    }

    /// <summary>
    /// One row per declared register, <b>each carrying the instant its own reading was taken</b>.
    /// </summary>
    /// <param name="observedPerRegister">
    /// 🔴 <b>THE COVERAGE MASK, AND IT IS WHAT STOPS AN UNREAD REGISTER BECOMING A ZERO.</b> Null in
    /// direct mode, where one transaction covers the whole area and <paramref name="fallbackObserved"/>
    /// is every register's true instant. In follow mode a register no read covered has a null here, and
    /// its row then shows NO RAW AND NO VALUE — because the value beside it is a default, not a reading.
    /// </param>
    /// <param name="fallbackObserved">The single instant every register shares in direct mode.</param>
    private static IReadOnlyList<RegisterRow> RowsOf(
        MirrorMap map,
        IReadOnlyList<ushort> registers,
        DateTimeOffset?[]? observedPerRegister,
        DateTimeOffset? fallbackObserved,
        DateTimeOffset now,
        double staleAfter)
    {
        var rows = new List<RegisterRow>(map.DeclaredRegisters);
        var haveValues = registers.Count >= map.DeclaredRegisters;

        DateTimeOffset? ObservedAt(int register) =>
            !haveValues ? null
            : observedPerRegister is null ? fallbackObserved
            : register >= 0 && register < observedPerRegister.Length ? observedPerRegister[register]
            : null;

        for (var index = 0; index < map.DeclaredRegisters; index++)
        {
            var address = $"%MW{map.BaseByte + (2 * index)}";
            var tag = map.TagAt(index);

            // A 32-bit element needs BOTH halves observed. Half a value is not a value — the same rule
            // the loop applies when it decodes a wide result, and for the same reason: reassembling one
            // read half with one unread half produces a plausible number out of a default.
            var span = tag is not null && tag.Register == index ? tag.RegisterCount : 1;

            var observed = Enumerable.Range(index, span)
                .Select(ObservedAt)
                .ToArray();

            var wholeElementObserved = observed.All(o => o is not null);
            var rowObserved = wholeElementObserved ? observed.Min() : null;
            var ageSeconds = rowObserved is null ? (double?)null : (now - rowObserved.Value).TotalSeconds;
            var current = ageSeconds is not null && ageSeconds >= -ClockToleranceSeconds && ageSeconds <= staleAfter;

            var raw = wholeElementObserved ? RegisterDecode.Hex(registers[index]) : null;

            if (tag is null)
            {
                rows.Add(new RegisterRow(index, address, null, null, raw, null,
                    wholeElementObserved ? null : NotRead,
                    "unmapped",
                    "No tag in the mirror tag table covers this register. Shown rather than omitted: an " +
                    "absent row would read as 'there is nothing here'.",
                    rowObserved, ageSeconds, current));
                continue;
            }

            if (tag.Register != index)
            {
                rows.Add(new RegisterRow(index, address, tag.Name, tag.TypeName, raw, null,
                    $"the low word of {tag.Name} — a 32-bit value read HIGH-WORD-FIRST, so this register " +
                    $"carries the LOW half and register {tag.Register} the high one.",
                    "continuation", tag.Comment,
                    rowObserved, ageSeconds, current));
                continue;
            }

            DecodedValue? decoded = null;
            if (wholeElementObserved)
            {
                var slice = new ushort[tag.RegisterCount];
                for (var i = 0; i < tag.RegisterCount; i++) slice[i] = registers[index + i];
                decoded = RegisterDecode.Decode(tag, slice);
            }

            rows.Add(new RegisterRow(
                index,
                tag.Address,
                tag.Name,
                tag.TypeName,
                raw,
                decoded?.Text,
                decoded?.Basis ?? (wholeElementObserved
                    ? $"NO DECODE for type '{tag.TypeName}'. The raw words are shown; a plausible number " +
                      "against an encoding this tool does not know would be worse than an admitted gap."
                    : NotRead),
                "value",
                tag.Comment,
                rowObserved, ageSeconds, current));
        }

        return rows;
    }

    /// <summary>
    /// What a row says when nothing has read it. <b>Not a value and not a zero</b> — the register's word
    /// is withheld entirely, because a default rendered in the raw column is indistinguishable from a
    /// device that really is publishing zeros.
    /// </summary>
    private const string NotRead =
        "NOT READ. No read in this feed covered this register (or covered only half of a 32-bit element), " +
        "so there is no value to show. The zero underneath is a default, not a reading, and is deliberately " +
        "not printed.";
}
