using System.Diagnostics;
using NModbus;

namespace Harness.MirrorRead;

/// <summary>
/// How one FC03 ended. <b>Three outcomes, not two</b>, and the third is the whole point of the
/// boundary probe.
/// </summary>
public enum ReadOutcome
{
    /// <summary>The server answered with the registers asked for.</summary>
    Ok,

    /// <summary>
    /// The server answered with a Modbus EXCEPTION RESPONSE — it received the request, understood it,
    /// and refused it. This is the only outcome that is evidence about the server's address space.
    /// </summary>
    RefusedByServer,

    /// <summary>
    /// The request did not produce an answer from the server at all — a timeout, a dropped socket, a
    /// short frame.
    ///
    /// <para>*** THIS IS NOT A REFUSAL AND MUST NEVER BE READ AS ONE. *** A boundary probe that times
    /// out has measured nothing: an area that IS wide enough and an area that is not both look like
    /// silence. Reporting it as "refused" would turn a failed measurement into a confirmation of
    /// whatever was expected — a comparison that could not be made, reported as one that came out
    /// negative.</para>
    /// </summary>
    TransportFailed,
}

/// <summary>
/// One FC03 and everything that came back from it, raw.
/// </summary>
/// <param name="Start">First register asked for.</param>
/// <param name="Count">How many were asked for.</param>
/// <param name="Outcome">See <see cref="ReadOutcome"/> — three-valued on purpose.</param>
/// <param name="Values">The registers, as 16-bit words, undecoded. Empty unless <see cref="ReadOutcome.Ok"/>.</param>
/// <param name="SlaveExceptionCode">
/// The Modbus exception code the server returned (02 = Illegal Data Address), or null when the server
/// did not answer with one. Reported as a number because that is what a protocol table is indexed by.
/// </param>
/// <param name="Failure">The exception type and message, verbatim, for anything that was not a clean read.</param>
/// <param name="ElapsedMs">
/// Wall time for the transaction. A refusal that cost a full round trip was ANSWERED; one that cost a
/// fraction of one was rejected locally and says nothing about the device.
/// </param>
public sealed record RegisterRead(
    int Start,
    int Count,
    ReadOutcome Outcome,
    ushort[] Values,
    byte? SlaveExceptionCode,
    string? Failure,
    long ElapsedMs)
{
    public bool Ok => Outcome == ReadOutcome.Ok;

    /// <summary>The registers as <c>16#XXXX</c> words, in address order. The measurement itself.</summary>
    public string Hex => string.Join(" ", Values.Select(v => v.ToString("X4")));

    /// <summary>
    /// Perform one read, converting the three ways it can end into the three outcomes above.
    ///
    /// <para><c>SlaveException</c> is caught FIRST and separately from everything else, because that
    /// distinction is the measurement. Every other exception — <c>WireException</c> for a short answer,
    /// a socket error, a timeout — collapses to <see cref="ReadOutcome.TransportFailed"/>, which no
    /// verdict in this tool is allowed to treat as a boundary.</para>
    /// </summary>
    public static RegisterRead Perform(IRegisterSource source, int start, int count)
    {
        ArgumentNullException.ThrowIfNull(source);

        var clock = Stopwatch.StartNew();
        try
        {
            var values = source.Read(start, count);
            clock.Stop();
            return new RegisterRead(start, count, ReadOutcome.Ok, values, null, null, clock.ElapsedMilliseconds);
        }
        catch (SlaveException ex)
        {
            clock.Stop();
            return new RegisterRead(start, count, ReadOutcome.RefusedByServer, Array.Empty<ushort>(),
                ex.SlaveExceptionCode, $"{ex.GetType().FullName}: {ex.Message}", clock.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            clock.Stop();
            return new RegisterRead(start, count, ReadOutcome.TransportFailed, Array.Empty<ushort>(),
                null, $"{ex.GetType().FullName}: {ex.Message}", clock.ElapsedMilliseconds);
        }
    }

    /// <summary>
    /// 🔴 <b>The same read, split into transactions the protocol can actually carry.</b>
    ///
    /// <para><b>Why this exists.</b> FC03 carries at most 125 registers, and until 2026-08-22 neither
    /// this tool nor <c>harness-mirror-view</c> mentioned that limit anywhere — both issued ONE
    /// <c>Perform(source, 0, declaredRegisters)</c> over the whole area. It worked only because the live
    /// run used <c>--declared-registers 37</c>. Against the rig's real 576 that read is 4.6x the ceiling
    /// and comes back refused, which the viewer then renders as <i>"the area is narrower than the map
    /// declares"</i> — pointing at the area pointer when the fault is in the request.</para>
    ///
    /// <para>⚠️ <b>The boundary probe must NOT use this.</b> <c>MirrorReadRun</c> proves the declared
    /// width from both sides by reading the first register PAST the area and requiring exception 2.
    /// Paging that read would split it and mask the very refusal being measured — so the probe stays on
    /// <see cref="Perform"/>, deliberately, and this is only for the bulk read.</para>
    ///
    /// <para>A failing page is reported as ITSELF — its own start and count — with the whole request
    /// named in the failure text. Reporting the whole span would claim a boundary at an address that was
    /// never asked for.</para>
    /// </summary>
    public static RegisterRead PerformPaged(IRegisterSource source, int start, int count, int pageSize = Harness.Map.ModbusLimits.MaxReadRegisters)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (pageSize < 1)
            throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, "a page of no registers reads nothing.");

        if (count <= pageSize)
            return Perform(source, start, count);

        var values = new List<ushort>(count);
        long elapsed = 0;

        for (var offset = 0; offset < count; offset += pageSize)
        {
            var length = Math.Min(pageSize, count - offset);
            var page = Perform(source, start + offset, length);
            elapsed += page.ElapsedMs;

            if (page.Outcome != ReadOutcome.Ok)
            {
                return page with
                {
                    ElapsedMs = elapsed,
                    Failure = $"{page.Failure} [page {start + offset}..{start + offset + length - 1} of the "
                        + $"{count}-register read from {start}, split into {pageSize}-register transactions because FC03 carries no more]",
                };
            }

            values.AddRange(page.Values);
        }

        // Reported as the WHOLE request, because that is what the caller asked for and every page of it
        // succeeded. The paging is a protocol detail, not a different measurement.
        return new RegisterRead(start, count, ReadOutcome.Ok, values.ToArray(), null, null, elapsed);
    }

    /// <summary>One line describing what happened, with the raw words when there are any.</summary>
    public string Describe() => Outcome switch
    {
        ReadOutcome.Ok => $"OK              [{ElapsedMs,5} ms]  {Hex}",
        ReadOutcome.RefusedByServer =>
            $"REFUSED BY SERVER [{ElapsedMs,3} ms]  Modbus exception code {SlaveExceptionCode} " +
            $"({ExceptionCodeName(SlaveExceptionCode)}) — {DeviceFactsFrom(Failure)}",
        _ => $"NOT ESTABLISHED [{ElapsedMs,5} ms]  no answer from the server, so this probe measured " +
             $"NOTHING about the address space — {Failure}",
    };

    /// <summary>
    /// The part of an NModbus <c>SlaveException</c> message that came from the DEVICE, without the
    /// library's stock 150-word explanation of what exception code 2 means in general.
    ///
    /// <para>The first two lines carry the function code and the exception code — the facts the server
    /// sent. Everything after them is the same paragraph for every occurrence, and printing it on every
    /// probe row buries the measurement in boilerplate.</para>
    ///
    /// <para><b>The truncation is announced rather than silent.</b> A narrowing nobody can see becomes a
    /// place to hide: the marker says text was dropped and how much, so a reader can tell "the library
    /// said nothing more" from "something was cut".</para>
    /// </summary>
    public static string DeviceFactsFrom(string? failure)
    {
        if (string.IsNullOrEmpty(failure)) return "<no message>";

        var lines = failure.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        var kept = lines.Take(2).Select(l => l.Length > 120 ? l[..120] : l).ToArray();
        var dropped = failure.Length - kept.Sum(l => l.Length);

        return string.Join(" | ", kept) +
               (dropped > 0 ? $" [+{dropped} chars of NModbus's stock explanation, not device data]" : string.Empty);
    }

    /// <summary>The standard names, so a code does not have to be looked up to be read.</summary>
    public static string ExceptionCodeName(byte? code) => code switch
    {
        1 => "Illegal Function",
        2 => "Illegal Data Address",
        3 => "Illegal Data Value",
        4 => "Slave Device Failure",
        null => "none",
        _ => "unlisted",
    };
}
