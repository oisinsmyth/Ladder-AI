namespace Harness.CmdInject;

/// <summary>
/// One Modbus write of a command — a target span and the register values to put there, with a plain-language
/// note of what it is. <b>Data, not an action:</b> building one contacts nothing.
/// </summary>
public sealed record CommandTransaction(InjectionWriteTarget Target, IReadOnlyList<ushort> Values, string Purpose)
{
    /// <summary>True when this transaction's span covers <paramref name="register"/>.</summary>
    public bool Covers(int register) => register >= Target.Register && register <= Target.LastRegister;
}

/// <summary>
/// A whole command, as two transactions.
///
/// <para>🔴 <b>OPERANDS FIRST, THE SEQUENCE ALONE SECOND — AND THE SPLIT IS THE POINT.</b> The sequence
/// sits at the LOW address of the channel, so a single write of the whole channel, applied by the device
/// in address order, would land the new sequence BEFORE the new operands — executing the command against
/// the PREVIOUS command's operands, silently and plausibly. Split into two transactions, a tear between
/// them can only leave a new sequence against operands that were never written (harmless: the block sees
/// no new operands until the next command) or new operands against an old sequence (harmless: the block
/// acts on nothing until a new sequence appears). <b>Tearing cannot manufacture a command in either
/// direction.</b> The cost is one extra round trip.</para>
/// </summary>
public sealed record CommandFrames(
    ResolvedChannel Channel,
    ushort Sequence,
    CommandTransaction Operands,
    CommandTransaction SequenceWrite)
{
    /// <summary>The two transactions in the order they MUST be applied — operands, then the sequence alone.</summary>
    public IReadOnlyList<CommandTransaction> InOrder => new[] { Operands, SequenceWrite };
}

/// <summary>The outcome of building a command's frames — <b>Ok, or a list of reasons, each naming its operand.</b></summary>
public sealed record FrameBuildResult(bool Ok, CommandFrames? Frames, IReadOnlyList<string> Refusals)
{
    public static FrameBuildResult Built(CommandFrames frames) => new(true, frames, Array.Empty<string>());

    public static FrameBuildResult Refused(IReadOnlyList<string> refusals) => new(false, null, refusals);
}

/// <summary>
/// A request to send one command — <b>a channel and a value for each operand it declares.</b>
///
/// <para>Values are the caller's numbers as text; the builder parses, range-checks and encodes them.
/// There is no value for the sequence here — the sequence is the ledger's to allocate, never the caller's
/// to state, and there is no value for the code's MEANING — the tool sends what you ask and never judges
/// which codes are valid, because that knowledge is job data that changes with the plant.</para>
/// </summary>
public sealed record CommandRequest(string ChannelName, IReadOnlyDictionary<InjectionRole, string> Operands);
