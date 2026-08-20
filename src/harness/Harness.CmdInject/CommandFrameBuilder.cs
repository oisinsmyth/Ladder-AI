using System.Globalization;
using Harness.Map;
using Harness.Wire;

namespace Harness.CmdInject;

/// <summary>
/// Turns a command request into the two transactions that carry it — <b>parsing, range-checking and
/// encoding every operand, and refusing by name on anything that does not fit.</b>
///
/// <para>🔺 <b>REUSE, AND ITS ONE HONEST GAP.</b> Integer operands are range-checked through
/// <c>Harness.Map.MirrorValueFit</c> — the same "a refusal by name, never a modulo" used everywhere else,
/// so an out-of-range code produces a loud refusal rather than a silent truncation. <b>But
/// <c>MirrorValueFit</c> models only <c>Bool</c>/<c>Int</c>/<c>Time</c>; it has no <c>Real</c> and no
/// unsigned element</b>, so a REAL operand cannot pass through it. A 32-bit IEEE float has no integer
/// range to check — every finite value fits its 32 bits — so a Real's refusal is a PARSE refusal (not a
/// number, NaN, or infinity), which is still a refusal by name and never a coercion. The sequence is a
/// <c>UInt</c> the ledger produces in range by construction and is encoded directly. This split is noted
/// where it happens rather than hidden: the plan's "reuse MirrorValueFit for range refusal" holds for the
/// integer operands it can model, and stops exactly where its element table does.</para>
/// </summary>
public static class CommandFrameBuilder
{
    /// <summary>
    /// Build the frames for one command against a resolved channel, using an already-allocated sequence.
    /// </summary>
    /// <param name="channel">The resolved channel — the registers come from here, never from the request.</param>
    /// <param name="request">The operand values.</param>
    /// <param name="sequence">The sequence the ledger allocated. Never chosen here.</param>
    public static FrameBuildResult Build(ResolvedChannel channel, CommandRequest request, ushort sequence)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(request);

        var refusals = new List<string>();
        var operandRoles = channel.OperandRolesPresent;
        var supplied = request.Operands ?? new Dictionary<InjectionRole, string>();

        // Every operand the channel declares must be given a value; a missing one is not defaulted to zero,
        // because zero is a value the block could legitimately be driven with and writing one on the author's
        // behalf would be inventing the stimulus.
        foreach (var role in operandRoles.Where(r => !supplied.ContainsKey(r)))
        {
            refusals.Add($"channel '{channel.Name}' declares operand role '{role}' but the request supplies no value for it. " +
                         "An absent value is not a zero one — a command is written whole or not at all.");
        }

        // A value for a role the channel does not carry would be written nowhere; that is a request aimed at
        // the wrong channel, not a spare field to ignore.
        foreach (var role in supplied.Keys.Where(r => !operandRoles.Contains(r)))
        {
            refusals.Add($"the request supplies a value for role '{role}', which channel '{channel.Name}' does not declare. " +
                         "It would be written nowhere, so the request names a role this channel has no register for.");
        }

        // Encode in REGISTER ORDER, so the operand transaction's values line up with the contiguous span.
        var operandWords = new List<ushort>();
        foreach (var role in operandRoles)
        {
            if (!supplied.TryGetValue(role, out var text))
                continue; // already refused above.

            var encoded = Encode(channel.Name, role, text);
            if (encoded.Refusal is not null)
                refusals.Add(encoded.Refusal);
            else
                operandWords.AddRange(encoded.Words!);
        }

        if (refusals.Count > 0)
            return FrameBuildResult.Refused(refusals);

        var operandsTarget = InjectionWriteTarget.Operands(channel);

        // A construction invariant, asserted rather than assumed: the encoded words must exactly fill the
        // resolved operand span. If they ever did not, the resolver and the encoder would disagree about how
        // wide an operand is, and that is a bug to surface loudly, not a frame to send.
        if (operandWords.Count != operandsTarget.Length)
        {
            return FrameBuildResult.Refused(new[]
            {
                $"internal: channel '{channel.Name}' encoded {operandWords.Count} operand register(s) but its resolved " +
                $"operand span is {operandsTarget.Length} wide. The encoder and the resolver disagree about a width.",
            });
        }

        var operands = new CommandTransaction(operandsTarget, operandWords,
            $"operands + code for channel '{channel.Name}', written FIRST");

        var sequenceWrite = new CommandTransaction(InjectionWriteTarget.Sequence(channel), new[] { sequence },
            $"sequence {sequence} for channel '{channel.Name}', written ALONE and SECOND");

        return FrameBuildResult.Built(new CommandFrames(channel, sequence, operands, sequenceWrite));
    }

    private readonly record struct Encoded(ushort[]? Words, string? Refusal);

    private static Encoded Encode(string channelName, InjectionRole role, string text)
    {
        var name = $"{channelName}.{role}";
        var shape = Roles.Shape(role);

        // Integer operands go through MirrorValueFit — the shared "refusal by name, never a modulo".
        if (string.Equals(shape.IrType, "Int", StringComparison.Ordinal))
        {
            var fit = MirrorValueFit.Check(name, MirrorValueType.Int, text);
            return fit.Fits
                ? new Encoded(new[] { unchecked((ushort)(short)fit.Value) }, null)
                : new Encoded(null, fit.Refusal);
        }

        // Real operands: MirrorValueFit has no Real element, so parse/finite-check here. Every finite float
        // fits its 32 bits, so this refuses a value that is not a number rather than one that is out of range.
        if (string.Equals(shape.IrType, "Real", StringComparison.Ordinal))
        {
            if (!float.TryParse(text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var real))
            {
                return new Encoded(null,
                    $"'{name}' is a Real and its value '{text}' is not a number. It is not converted on a best-effort " +
                    "basis: a value nobody can read is not a value that should reach a controller.");
            }

            if (float.IsNaN(real) || float.IsInfinity(real))
            {
                return new Encoded(null,
                    $"'{name}' is a Real and its value '{text}' is {(float.IsNaN(real) ? "not-a-number" : "infinite")}. " +
                    "Neither is a value a plant should be driven with, and neither has a defined encoding here.");
            }

            var bits = BitConverter.SingleToUInt32Bits(real);
            return new Encoded(RegisterWords.From32(bits, RegisterWordOrder.HighWordFirst), null);
        }

        // No other IR type is an operand role in the table above, so reaching here is a table/encoder drift.
        return new Encoded(null,
            $"'{name}' has role type '{shape.IrType}', which the frame builder has no encoding for. This is a mismatch " +
            "between the role table and the encoder, not a bad value.");
    }
}
