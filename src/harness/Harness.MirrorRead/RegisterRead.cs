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

    /// <summary>One line describing what happened, with the raw words when there are any.</summary>
    public string Describe() => Outcome switch
    {
        ReadOutcome.Ok => $"OK              [{ElapsedMs,5} ms]  {Hex}",
        ReadOutcome.RefusedByServer =>
            $"REFUSED BY SERVER [{ElapsedMs,3} ms]  Modbus exception code {SlaveExceptionCode} " +
            $"({ExceptionCodeName(SlaveExceptionCode)}) — {Failure}",
        _ => $"NOT ESTABLISHED [{ElapsedMs,5} ms]  no answer from the server, so this probe measured " +
             $"NOTHING about the address space — {Failure}",
    };

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
