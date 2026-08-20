namespace Harness.CmdInject;

/// <summary>
/// What a poll of a channel's acknowledgement registers means. <b>Four outcomes, and there is no fifth</b>
/// — the two observed facts are each a boolean, and every cell of the 2×2 has a name.
/// </summary>
public enum AckOutcome
{
    /// <summary>The count advanced AND the acknowledged sequence is ours. The command was processed, and it was ours.</summary>
    Acknowledged,

    /// <summary>Nothing has advanced and no sequence of ours is echoed yet. Not processed — keep polling.</summary>
    Pending,

    /// <summary>
    /// The count advanced, but on a sequence that is not ours. Another command was processed — a competing
    /// writer, or the device has already moved past ours. Not our acknowledgement.
    /// </summary>
    Superseded,

    /// <summary>
    /// 🔴 <b>THE INCOHERENT CELL.</b> The device echoes OUR sequence, but the processed-count did not move.
    /// The two observations disagree, so neither is trusted: this is not read as a success with a caveat,
    /// it is its own outcome. The most likely causes are a torn read or a device mid-update, and the right
    /// response is to poll again, never to conclude.
    /// </summary>
    Incoherent,
}

/// <summary>
/// One reading of a channel's acknowledgement registers.
/// </summary>
/// <param name="AckSeq">The sequence the device reports having acknowledged.</param>
/// <param name="AckCount">The processed-command counter.</param>
/// <param name="Result">
/// 🔴 <b>THE RESULT CODE, AS A STRING.</b> It is read and printed VERBATIM and <b>never branched on</b> —
/// the outcome is keyed on the count, never on this. It is a <c>string</c> on purpose, so a call site that
/// tried to compare it to an integer would not compile (the same trick as <c>ScanCount</c> having no
/// <c>operator -</c>). What the codes mean is job data that changes with the plant, and a second copy of
/// that meaning on the PC would drift from the block and eventually refuse what the block would accept.
///
/// <para>⚠️ <b>AND THERE IS A SECOND REASON, WHICH IS ABOUT THE CODES THEMSELVES.</b> A refusal cascade
/// evaluated in order publishes ONE value, and the check written last wins — so one refusal value in a
/// protocol of this shape can mask every other reason the command was declined. A reader who saw that
/// value and concluded "the cause was X" would be right only by luck about everything except X. That is a
/// property of how the codes are produced, not of any particular plant, and it is the strongest possible
/// argument for reporting the code and never reasoning from it.</para>
/// </param>
/// <param name="AckCode">
/// The echo of the command code the device processed, when the binding declares that role.
/// <b>Reported, never decisive — the same rule as <see cref="Result"/>, and carried as a string for the
/// same reason.</b> What it is FOR is telling you which command a HELD result belongs to: the result
/// register keeps its value until the next command is processed, so the code beside it is the only thing
/// that says which command produced it. Null when the binding declares no such role — <b>absent, which is
/// not the same as an echo of zero.</b>
/// </param>
public sealed record AckObservation(ushort AckSeq, ushort AckCount, string Result, string? AckCode = null);

/// <summary>What one classification decided, with both observed facts kept beside the verdict.</summary>
/// <param name="Outcome">The cell of the 2×2.</param>
/// <param name="SeqMatches">Whether the acknowledged sequence equals the one sent.</param>
/// <param name="CountAdvanced">Whether the processed-count moved from before the send.</param>
/// <param name="Result">The result code, carried through verbatim — reported, never decisive.</param>
/// <param name="AckCode">The echoed command code, carried through verbatim — reported, never decisive. Null when the binding declares no such role.</param>
public sealed record AckClassification(AckOutcome Outcome, bool SeqMatches, bool CountAdvanced, string Result, string? AckCode = null);

/// <summary>
/// Classifies an acknowledgement — <b>keyed on the count, cross-checked against the sequence, blind to the
/// result.</b>
/// </summary>
public static class AckModel
{
    /// <summary>
    /// Decide what an acknowledgement reading means.
    /// </summary>
    /// <param name="sentSeq">The sequence the client wrote for this command.</param>
    /// <param name="priorCount">The processed-count observed BEFORE the command was sent.</param>
    /// <param name="observed">The acknowledgement registers as read back.</param>
    public static AckClassification Classify(ushort sentSeq, ushort priorCount, AckObservation observed)
    {
        ArgumentNullException.ThrowIfNull(observed);

        var seqMatches = observed.AckSeq == sentSeq;
        var countAdvanced = observed.AckCount != priorCount;

        // Semantics-preserving rewrite of the same 2x2 (arms reordered; every cell still distinct).
        var outcome = (seqMatches, countAdvanced) switch
        {
            (true, false) => AckOutcome.Incoherent,
            (false, true) => AckOutcome.Superseded,
            (false, false) => AckOutcome.Pending,
            (true, true) => AckOutcome.Acknowledged,
        };

        return new AckClassification(outcome, seqMatches, countAdvanced, observed.Result, observed.AckCode);
    }
}
