using Harness.MirrorView;

namespace Harness.CmdInject;

/// <summary>
/// Resolves a binding against a parsed mirror map, and <b>refuses, by name, on every way the two can
/// disagree.</b>
///
/// <para>🔴 <b>THIS IS THE GATE THAT KEEPS THE MAP HONEST.</b> The tool holds roles; the binding says
/// which tag plays each; the map says where each tag lives and what type it is. A wrong binding is the
/// second-most-dangerous input this tool takes (after the wrong address) — a binding naming the wrong
/// registers would write commands into the wrong place and look like it worked. So every mismatch is a
/// refusal that names the offender, and the <c>map</c> verb prints the whole resolution for a person to
/// read before anyone arms anything. Nothing is clamped, defaulted or guessed.</para>
/// </summary>
public static class BindingResolver
{
    public static ResolveResult Resolve(InjectionBinding binding, MirrorMap map)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(map);

        var refusals = new List<string>();
        var byName = map.Tags.ToDictionary(t => t.Name, StringComparer.Ordinal);

        // Each register that is claimed, and by whom — for the overlap check across every member.
        var owners = new List<(int Register, string Owner)>();

        // ---- band-level roles ----
        var bandRoles = new Dictionary<InjectionRole, MirrorTag>();
        foreach (var (role, tagName) in binding.BandRoles)
        {
            var tag = Bind(role, tagName, $"band role '{role}'", byName, refusals);
            if (tag is null) continue;

            if (!PlacedIn(tag, binding.CommandBand))
            {
                refusals.Add($"command member '{tagName}' (band role '{role}') resolves to register(s) " +
                             $"{Span(tag)}, outside the declared command band {binding.CommandBand}.");
                continue;
            }

            bandRoles[role] = tag;
            Claim(owners, tag, $"{tagName} (band {role})");
        }

        // ---- channels ----
        var channels = new List<ResolvedChannel>();
        foreach (var channel in binding.Channels)
        {
            var commandRoles = new Dictionary<InjectionRole, MirrorTag>();
            var observationRoles = new Dictionary<InjectionRole, MirrorTag>();

            foreach (var (role, tagName) in channel.Roles)
            {
                var tag = Bind(role, tagName, $"role '{role}' of channel '{channel.Name}'", byName, refusals);
                if (tag is null) continue;

                var section = Roles.Shape(role).Section;
                if (section == RoleSection.Observation)
                {
                    if (!binding.ObservationBands.Any(b => PlacedIn(tag, b)))
                    {
                        refusals.Add($"ack member '{tagName}' (role '{role}' of channel '{channel.Name}') resolves to " +
                                     $"register(s) {Span(tag)}, outside every declared observation band " +
                                     $"({string.Join(", ", binding.ObservationBands)}).");
                        continue;
                    }

                    observationRoles[role] = tag;
                }
                else
                {
                    if (!PlacedIn(tag, binding.CommandBand))
                    {
                        refusals.Add($"command member '{tagName}' (role '{role}' of channel '{channel.Name}') resolves to " +
                                     $"register(s) {Span(tag)}, outside the declared command band {binding.CommandBand}.");
                        continue;
                    }

                    commandRoles[role] = tag;
                }

                Claim(owners, tag, $"{tagName} ({channel.Name}.{role})");
            }

            // Contiguity + sequence-lowest, but only when the sequence and code both resolved — otherwise the
            // refusal is already recorded above and a second one about contiguity would just be noise.
            if (commandRoles.ContainsKey(InjectionRole.Seq))
                CheckContiguity(channel.Name, commandRoles, refusals);

            channels.Add(new ResolvedChannel(channel.Name, commandRoles, observationRoles));
        }

        // ---- overlap across every claimed register ----
        foreach (var group in owners.GroupBy(o => o.Register).Where(g => g.Select(x => x.Owner).Distinct().Count() > 1))
        {
            refusals.Add($"register {group.Key} is claimed by more than one role: " +
                         $"{string.Join(", ", group.Select(x => x.Owner).Distinct().OrderBy(x => x, StringComparer.Ordinal))}. " +
                         "Two roles sharing a register means one command member is writing over another.");
        }

        // ---- declared bands vs the union of what resolved into them ----
        CheckBandAgreement(
            "command band", binding.CommandBand,
            owners.Where(o => binding.CommandBand.Contains(o.Register)).Select(o => o.Register),
            refusals);

        foreach (var band in binding.ObservationBands)
        {
            var landed = channels
                .SelectMany(c => c.ObservationRoles.Values)
                .Where(t => PlacedIn(t, band))
                .SelectMany(t => Enumerable.Range(t.Register, t.RegisterCount));

            CheckBandAgreement($"observation band {band}", band, landed, refusals);
        }

        if (refusals.Count > 0)
            return ResolveResult.Refused(refusals);

        return ResolveResult.Resolved(new ResolvedBinding(
            binding.CommandBand, binding.ObservationBands, bandRoles, channels));
    }

    private static MirrorTag? Bind(
        InjectionRole role, string tagName, string where,
        IReadOnlyDictionary<string, MirrorTag> byName, List<string> refusals)
    {
        if (!byName.TryGetValue(tagName, out var tag))
        {
            refusals.Add($"the map does not carry tag '{tagName}', named for {where}. A tag the map does not " +
                         "describe cannot be placed on the register grid, and this tool will not invent one.");
            return null;
        }

        var shape = Roles.Shape(role);
        if (!string.Equals(tag.TypeName, shape.IrType, StringComparison.Ordinal) || tag.Width != shape.Width)
        {
            refusals.Add($"tag '{tagName}' plays {where}, which requires a {shape.IrType} ({shape.Width}) register, " +
                         $"but the map declares it {tag.TypeName} ({tag.Width}). The type decides how the register is " +
                         "read and written, so a mismatch would encode a command the block cannot interpret.");
            return null;
        }

        return tag;
    }

    private static void CheckContiguity(string channelName, IReadOnlyDictionary<InjectionRole, MirrorTag> commandRoles, List<string> refusals)
    {
        var registers = commandRoles.Values
            .SelectMany(t => Enumerable.Range(t.Register, t.RegisterCount))
            .OrderBy(r => r)
            .ToList();

        var seqRegister = commandRoles[InjectionRole.Seq].Register;
        if (seqRegister != registers[0])
        {
            refusals.Add($"channel '{channelName}' has its sequence at register {seqRegister}, but its lowest command " +
                         $"register is {registers[0]}. The sequence must sit at the LOW address of the channel: it is " +
                         "written alone and last, and an operand below it would be written after the sequence, which is " +
                         "exactly the torn-write hazard the split write exists to remove.");
        }

        for (var i = 1; i < registers.Count; i++)
        {
            if (registers[i] - registers[i - 1] != 1) // same test: consecutive registers differ by exactly one.
            {
                refusals.Add($"channel '{channelName}' has non-contiguous command members: register {registers[i - 1]} is " +
                             $"followed by {registers[i]}, leaving a gap. The command is written as one operand transaction " +
                             "plus the sequence, so a gap would either skip a register or write one that belongs to nothing.");
                return;
            }
        }
    }

    private static void CheckBandAgreement(string label, BandDeclaration band, IEnumerable<int> resolvedRegisters, List<string> refusals)
    {
        var registers = resolvedRegisters.Distinct().OrderBy(r => r).ToList();

        if (registers.Count == 0)
        {
            refusals.Add($"the declared {label} {band} has NO role resolve into it. A band nothing lands in is a " +
                         "declaration describing registers no command or ack occupies — empty is not clean.");
            return;
        }

        if (registers[0] != band.FirstRegister)
        {
            refusals.Add($"the declared {label} {band} disagrees with what resolved into it: it says it begins at register " +
                         $"{band.FirstRegister}, but the lowest role that landed in it is at {registers[0]}. The band claims a " +
                         "start no member occupies, so either the band or the map is stale.");
        }
    }

    private static bool PlacedIn(MirrorTag tag, BandDeclaration band) =>
        band.Contains(tag.Register) && band.Contains(tag.LastRegister);

    private static void Claim(List<(int, string)> owners, MirrorTag tag, string owner)
    {
        foreach (var register in Enumerable.Range(tag.Register, tag.RegisterCount))
            owners.Add((register, owner));
    }

    private static string Span(MirrorTag tag) =>
        tag.RegisterCount == 1 ? tag.Register.ToString() : $"{tag.Register}..{tag.LastRegister}";
}
