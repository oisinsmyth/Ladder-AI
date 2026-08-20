using Harness.MirrorView;

namespace Harness.CmdInject;

/// <summary>
/// One channel after resolution — <b>every role bound to a real place on the register grid.</b>
///
/// <para>The dictionaries hold the map's own <see cref="MirrorTag"/>, so the register, the width and the
/// declared type all travel together and the resolver's checks cannot drift from what the frame builder
/// later encodes against.</para>
/// </summary>
public sealed record ResolvedChannel(
    string Name,
    IReadOnlyDictionary<InjectionRole, MirrorTag> CommandRoles,
    IReadOnlyDictionary<InjectionRole, MirrorTag> ObservationRoles)
{
    /// <summary>The sequence register — at the low address of the channel by construction (the resolver refuses otherwise).</summary>
    public int SequenceRegister => CommandRoles[InjectionRole.Seq].Register;

    /// <summary>The operand roles this channel actually declares, in register order.</summary>
    public IReadOnlyList<InjectionRole> OperandRolesPresent =>
        Roles.OperandRoles.Where(CommandRoles.ContainsKey).OrderBy(r => CommandRoles[r].Register).ToList();

    /// <summary>Every register a command member of this channel occupies (sequence + operands), expanded across 32-bit spans.</summary>
    public IReadOnlyList<int> CommandRegisters =>
        CommandRoles.Values.SelectMany(t => Enumerable.Range(t.Register, t.RegisterCount)).OrderBy(r => r).ToList();

    /// <summary>The first operand register — one above the sequence — or -1 when the channel carries only a sequence and code with nothing above.</summary>
    public int OperandFirstRegister =>
        OperandRolesPresent.Count == 0 ? -1 : CommandRoles[OperandRolesPresent[0]].Register;

    /// <summary>How many registers the operands occupy in total (the single contiguous operand transaction).</summary>
    public int OperandRegisterCount =>
        OperandRolesPresent.Sum(r => CommandRoles[r].RegisterCount);
}

/// <summary>
/// The whole binding after resolution — <b>the thing the <c>map</c> verb prints and the frame builder
/// reads.</b> Nothing here is job data the tool reasons over; it is the map, placed, with the roles bound.
/// </summary>
public sealed record ResolvedBinding(
    BandDeclaration CommandBand,
    IReadOnlyList<BandDeclaration> ObservationBands,
    IReadOnlyDictionary<InjectionRole, MirrorTag> BandRoles,
    IReadOnlyList<ResolvedChannel> Channels)
{
    /// <summary>A channel by name, or null.</summary>
    public ResolvedChannel? Channel(string name) =>
        Channels.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.Ordinal));
}

/// <summary>The outcome of resolving a binding against a map — <b>Ok, or a list of reasons, each naming its offender.</b></summary>
public sealed record ResolveResult(bool Ok, ResolvedBinding? Binding, IReadOnlyList<string> Refusals)
{
    public static ResolveResult Resolved(ResolvedBinding binding) => new(true, binding, Array.Empty<string>());

    public static ResolveResult Refused(IReadOnlyList<string> refusals) => new(false, null, refusals);
}
