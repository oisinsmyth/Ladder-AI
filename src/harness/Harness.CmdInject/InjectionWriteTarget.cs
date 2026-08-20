namespace Harness.CmdInject;

/// <summary>Which part of the mirror a write lands in. Every write target has exactly one.</summary>
public enum InjectionRegion
{
    /// <summary>A channel's operand + code registers — the first of a command's two transactions.</summary>
    Operands,

    /// <summary>A channel's sequence register — the second transaction, written ALONE.</summary>
    Sequence,

    /// <summary>The master enable.</summary>
    Enable,

    /// <summary>The heartbeat register.</summary>
    Heartbeat,

    /// <summary>The safety-permissive substitution — written only under a consent separate from arming.</summary>
    SafetyPermissive,

    /// <summary>
    /// The whole command band, put back as the session's opening read found it.
    ///
    /// <para>It is one region and one write on purpose: the band fits a single FC16, so the restore is
    /// atomic as far as the server is concerned and cannot leave the surface half-old. A restore split
    /// across two writes could be interrupted between them, which is the one state the restore exists to
    /// make impossible.</para>
    /// </summary>
    CommandBandRestore,
}

/// <summary>
/// 🔴 <b>A DESTINATION A CLIENT MAY WRITE — AND THERE IS NO WAY TO CONSTRUCT ONE THAT NAMES A REGISTER
/// OUTSIDE THE COMMAND SURFACE.</b>
///
/// <para><b>Transplanted from <c>Harness.Map.MirrorWriteTarget</c>, for the same reason it exists there.</b>
/// Until this type, "the client only writes command registers" was a property of our code — a convention,
/// and a convention is not a fence. A raw write took a BARE REGISTER NUMBER, so any future line could
/// address an observation register, and the failure mode is not an exception: it is a plausible wrong
/// place. <b>There is no public constructor and no factory that takes a bare register.</b> Every factory
/// names a ROLE and derives the register from the resolved binding, so an out-of-band write is not
/// refused at runtime — <b>it is unaddressable</b>, which no behavioural test could have proved absent.</para>
///
/// <para>⚠️ <b>WHAT THIS DOES NOT BUY, stated here rather than only in a report.</b> It stops OUR client
/// writing outside the command surface. It stops nothing else on that network — the server's holding
/// registers are read-write to every Modbus master alive, and the instruction offers no read-only write
/// space. The fence for that is the address, the isolation and the arming, not this type.</para>
/// </summary>
public readonly record struct InjectionWriteTarget
{
    private InjectionWriteTarget(int register, int length, InjectionRegion region)
    {
        Register = register;
        Length = length;
        Region = region;
    }

    /// <summary>First holding register of the span.</summary>
    public int Register { get; }

    /// <summary>How many registers the span covers.</summary>
    public int Length { get; }

    /// <summary>The region this span lies in.</summary>
    public InjectionRegion Region { get; }

    /// <summary>The last register the span covers.</summary>
    public int LastRegister => Register + Length - 1;

    public override string ToString() => $"{Region} [{Register}..{LastRegister}]";

    /// <summary>
    /// A channel's operand transaction — code plus any integer/real operands, one contiguous span above
    /// the sequence. <b>The FIRST of a command's two writes.</b>
    /// </summary>
    public static InjectionWriteTarget Operands(ResolvedChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);

        if (channel.OperandRolesPresent.Count == 0)
        {
            throw new InvalidOperationException(
                $"channel '{channel.Name}' resolved with no operand registers, not even a code. A command with nothing " +
                "to write but a sequence is not a command, and the resolver requires a code — reaching here means the " +
                "resolved model was built by something that skipped it.");
        }

        return new InjectionWriteTarget(channel.OperandFirstRegister, channel.OperandRegisterCount, InjectionRegion.Operands);
    }

    /// <summary>
    /// A channel's sequence register — <b>always one register, the SECOND write, alone.</b>
    ///
    /// <para>Kept a whole factory of its own rather than folded into <see cref="Operands"/> precisely
    /// because it must be a separate transaction: the sequence at the low address written together with
    /// the operands above it is the torn-write hazard, and a single factory returning both would make
    /// that hazard constructible.</para>
    /// </summary>
    public static InjectionWriteTarget Sequence(ResolvedChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);
        return new InjectionWriteTarget(channel.SequenceRegister, 1, InjectionRegion.Sequence);
    }

    /// <summary>The master enable register.</summary>
    public static InjectionWriteTarget Enable(ResolvedBinding binding) =>
        BandRole(binding, InjectionRole.Enable, InjectionRegion.Enable);

    /// <summary>The heartbeat register.</summary>
    public static InjectionWriteTarget Heartbeat(ResolvedBinding binding) =>
        BandRole(binding, InjectionRole.Heartbeat, InjectionRegion.Heartbeat);

    /// <summary>
    /// The safety-permissive register. <b>Throws when the binding declares none</b> — a job that does not
    /// simulate safety has no such register, and inventing one is the leak this whole design removes.
    /// </summary>
    public static InjectionWriteTarget SafetyPermissive(ResolvedBinding binding) =>
        BandRole(binding, InjectionRole.SafetyPermissive, InjectionRegion.SafetyPermissive);

    /// <summary>
    /// The whole command band — <b>the restore write, and the only target that spans more than one role.</b>
    ///
    /// <para>It takes the resolved binding, never a register and a length, for the same reason every other
    /// factory here does: a restore aimed at a band somebody typed is a restore that can be aimed at the
    /// wrong place. The span comes from the binding the session's opening read was taken over, so the image
    /// and the target are derived from one source.</para>
    /// </summary>
    public static InjectionWriteTarget CommandBand(ResolvedBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        return new InjectionWriteTarget(
            binding.CommandBand.FirstRegister, binding.CommandBand.RegisterCount, InjectionRegion.CommandBandRestore);
    }

    private static InjectionWriteTarget BandRole(ResolvedBinding binding, InjectionRole role, InjectionRegion region)
    {
        ArgumentNullException.ThrowIfNull(binding);

        if (!binding.BandRoles.TryGetValue(role, out var tag))
        {
            throw new InvalidOperationException(
                $"this binding declares no '{role}' role, so there is no register to write it. That is not an error to " +
                "route around — it is the binding stating this job has no such register.");
        }

        return new InjectionWriteTarget(tag.Register, tag.RegisterCount, region);
    }
}
