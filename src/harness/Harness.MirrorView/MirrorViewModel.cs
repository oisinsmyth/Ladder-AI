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
public sealed record RegisterRow(
    int Index,
    string MAddress,
    string? Tag,
    string? TypeName,
    string? Raw,
    string? Decoded,
    string? Basis,
    string Role,
    string? Comment);

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
    IReadOnlyList<int> UnmappedRegisters)
{
    /// <summary>True only when the numbers on screen were read just now by a poll that succeeded.</summary>
    public bool ValuesAreCurrent => Status == MirrorStatus.Live;
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

        var (status, statusText) = Classify(lastAttempt, lastSuccess, age, staleAfter);

        var registers = lastSuccess?.Registers ?? Array.Empty<ushort>();

        return new MirrorViewModel(
            Status: status,
            StatusText: statusText,
            GeneratedUtc: now,
            ObservedUtc: lastSuccess?.At,
            AgeSeconds: age,
            StaleAfterSeconds: staleAfter,
            PollIntervalSeconds: options.PollIntervalMs / 1000.0,
            Target: $"{options.Address}:{options.Port} unit {options.UnitId}",
            AllowlistPath: options.AllowlistPath ?? "<none configured>",
            MapSource: map.TagTableSource,
            AreaSource: map.AreaPointerSource,
            BaseByte: map.BaseByte,
            DeclaredRegisters: map.DeclaredRegisters,
            LastAttemptOutcome: lastAttempt?.Outcome.ToString() ?? "None",
            LastAttemptDetail: lastAttempt?.Detail ?? "no poll has been attempted yet.",
            LastAttemptUtc: lastAttempt?.At,
            LastAttemptElapsedMs: lastAttempt?.ElapsedMs ?? 0,
            BuildStamp: StampOf(registers),
            Scan: ScanOf(lastSuccess, previousSuccess),
            Rows: RowsOf(map, registers),
            UnmappedRegisters: map.UnmappedRegisters);
    }

    /// <summary>
    /// 🔴 <b>THE RULE.</b> Green requires two things at once: the last ATTEMPT succeeded, and its reading
    /// is inside the freshness window. Either one alone is a way for old numbers to look current — a
    /// failing poller still has a recent-ish reading for a while, and a stalled loop still has a
    /// last attempt that "succeeded".
    /// </summary>
    private static (MirrorStatus, string) Classify(PollAttempt? lastAttempt, PollAttempt? lastSuccess, double? age, double staleAfter)
    {
        if (lastAttempt is null)
        {
            return (MirrorStatus.NeverRead,
                "NO POLL HAS BEEN ATTEMPTED YET. Nothing on this page has been read from the device.");
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

        if (age is null || age > staleAfter)
        {
            return (MirrorStatus.Stale,
                $"STALE: the newest reading is {age?.ToString("F1") ?? "?"} s old, past the " +
                $"{staleAfter:F1} s freshness window. The poller is not delivering — these numbers are not now.");
        }

        return (MirrorStatus.Live, "LIVE: the last poll succeeded and its reading is inside the freshness window.");
    }

    private static string? StampOf(IReadOnlyList<ushort> registers)
    {
        if (registers.Count < BuildStampRegister + 2) return null;

        var value = RegisterWords.To32(registers[BuildStampRegister], registers[BuildStampRegister + 1], RegisterDecode.Order);
        return "16#" + value.ToString("X8", System.Globalization.CultureInfo.InvariantCulture);
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
        if (lastSuccess is null || lastSuccess.Registers.Length < ScanCounterRegister + 2)
        {
            return new ScanReport(null, null, null, null, null,
                "NOT ESTABLISHED — the scan counter has not been read. Nothing here says whether the copy " +
                "layer is executing.");
        }

        var now = ScanCount.FromRegisters(
            lastSuccess.Registers[ScanCounterRegister], lastSuccess.Registers[ScanCounterRegister + 1], RegisterDecode.Order);

        if (previousSuccess is null || previousSuccess.Registers.Length < ScanCounterRegister + 2)
        {
            return new ScanReport(now.Raw, null, null, null, null,
                "NOT ESTABLISHED — one reading cannot say whether a counter is advancing. A static value " +
                "from a past download reads exactly like a running program until a second reading arrives.");
        }

        var before = ScanCount.FromRegisters(
            previousSuccess.Registers[ScanCounterRegister], previousSuccess.Registers[ScanCounterRegister + 1], RegisterDecode.Order);

        var advance = now.Since(before);
        var interval = (long)(lastSuccess.At - previousSuccess.At).TotalMilliseconds;

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

    private static IReadOnlyList<RegisterRow> RowsOf(MirrorMap map, IReadOnlyList<ushort> registers)
    {
        var rows = new List<RegisterRow>(map.DeclaredRegisters);
        var haveValues = registers.Count >= map.DeclaredRegisters;

        for (var index = 0; index < map.DeclaredRegisters; index++)
        {
            var address = $"%MW{map.BaseByte + (2 * index)}";
            var raw = haveValues ? RegisterDecode.Hex(registers[index]) : null;
            var tag = map.TagAt(index);

            if (tag is null)
            {
                rows.Add(new RegisterRow(index, address, null, null, raw, null, null, "unmapped",
                    "No tag in the mirror tag table covers this register. Shown rather than omitted: an " +
                    "absent row would read as 'there is nothing here'."));
                continue;
            }

            if (tag.Register != index)
            {
                rows.Add(new RegisterRow(index, address, tag.Name, tag.TypeName, raw, null,
                    $"the low word of {tag.Name} — a 32-bit value read HIGH-WORD-FIRST, so this register " +
                    $"carries the LOW half and register {tag.Register} the high one.",
                    "continuation", tag.Comment));
                continue;
            }

            DecodedValue? decoded = null;
            if (haveValues)
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
                decoded?.Basis ?? (haveValues
                    ? $"NO DECODE for type '{tag.TypeName}'. The raw words are shown; a plausible number " +
                      "against an encoding this tool does not know would be worse than an admitted gap."
                    : null),
                "value",
                tag.Comment));
        }

        return rows;
    }
}
