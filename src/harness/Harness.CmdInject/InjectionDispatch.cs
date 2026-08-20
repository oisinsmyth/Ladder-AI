namespace Harness.CmdInject;

/// <summary>
/// 🔴 <b>THE ONE METHOD IN THIS BINARY THAT NAMES THE TRANSPORT'S WRITE MEMBER.</b>
///
/// <para>Every write this tool can ever perform goes through <see cref="Apply"/>, and a structural test
/// (<c>InjectionStructureTests</c>) walks the compiled assembly and asserts that the number of methods
/// naming <see cref="IInjectionTransport.WriteRegisters"/> is <b>exactly one</b>. A single chokepoint is
/// what makes "the split write is honoured" and "nothing writes outside a built frame" properties of the
/// assembly rather than of every future call site — the failure this guards against is the same one that
/// let a live socket client sit inside a "cannot write" binary behind 202 green tests, and the guard is
/// the same shape: check the IL, not a declaration.</para>
///
/// <para><b>It performs transactions in the order it is given them and does nothing else</b> — no
/// connect, no poll, no ack loop, no arming. Those belong to <see cref="InjectionSession"/>. Here the
/// transport is a parameter; the only thing in this assembly that constructs one is
/// <see cref="ModbusInjectionTransportFactory"/>, reached only from the composition root.</para>
///
/// <para><b>The restore writes go through here too, and that is why <see cref="Apply(IInjectionTransport, CommandTransaction)"/>
/// exists.</b> Dropping the enable and putting the command band back are writes like any other; giving
/// them their own call to the transport would make the chokepoint count two and the guard would be
/// measuring nothing. Every write in this tool — command, enable, restore — passes through the single
/// private loop below.</para>
/// </summary>
public static class InjectionDispatch
{
    /// <summary>
    /// Apply a built command: the operands FIRST, then the sequence ALONE. The order is not a convention
    /// here — it is enforced by writing <see cref="CommandFrames.Operands"/> before
    /// <see cref="CommandFrames.SequenceWrite"/>, so a reader of this method sees the split the frame model
    /// promises.
    /// </summary>
    public static void Apply(IInjectionTransport transport, CommandFrames frames)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(frames);

        ApplyEach(transport, frames.InOrder);
    }

    /// <summary>Apply one transaction — the enable drop and the band restore, which are not commands.</summary>
    public static void Apply(IInjectionTransport transport, CommandTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(transaction);

        ApplyEach(transport, new[] { transaction });
    }

    /// <summary>
    /// 🔴 <b>THE SINGLE LINE IN THIS BINARY THAT PUTS REGISTERS ON THE WIRE.</b> Everything above is
    /// ordering; this is the write. The structural test counts the methods naming
    /// <see cref="IInjectionTransport.WriteRegisters"/> and requires the answer to be one.
    /// </summary>
    private static void ApplyEach(IInjectionTransport transport, IReadOnlyList<CommandTransaction> transactions)
    {
        foreach (var transaction in transactions)
            transport.WriteRegisters(transaction.Target.Register, transaction.Values);
    }
}
