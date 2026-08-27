using System.Text;
using System.Text.RegularExpressions;

namespace Harness.Map;

/// <summary>Naming the generator needs and must not invent.</summary>
/// <param name="BlockName">The OB's name.</param>
/// <param name="Number">
/// 🔴 <b>An OB's number is fixed by its EVENT CLASS, which is why OBs are EXCLUDED from the harness
/// 9000–9999 band</b> (CLAUDE.md, "Environment"). Required, never defaulted, and refused if it lands in
/// that band — a number allocated from the harness range would be a number the event class does not allow.
/// </param>
/// <param name="SecondaryType">
/// The event class. Only <c>ProgramCycle</c> is accepted — see <see cref="CyclicObGenerator"/> for why the
/// system parameters make that a grounding question rather than a preference.
/// </param>
/// <param name="Title">The OB's title. Required; the generator will not invent one.</param>
public sealed record CyclicObNaming(string BlockName, int Number, string SecondaryType, string Title);

/// <summary>One call the OB makes, in the position the declaration puts it.</summary>
/// <param name="BlockName">The FB or FC being called.</param>
/// <param name="InstancePath">Its instance DB, or a dotted multi-instance path. <b>Null for an FC</b>, which has no instance.</param>
/// <param name="NetworkTitle">The network's title. Every network gets one (C-201); the generator will not invent it.</param>
/// <param name="NetworkComment">
/// Why the call sits where it sits. Optional — four of the twelve networks in the committed
/// <c>Main.ir</c> have none — but this is where an ordering argument belongs when there is one.
/// </param>
public sealed record ObCall(string BlockName, string? InstancePath, string NetworkTitle, string? NetworkComment = null);

/// <summary>
/// 🔴 <b>ONE ORDERING THE CALL LIST MUST SATISFY, WITH THE REASON IT EXISTS ATTACHED.</b>
///
/// <para>The generator derives NO ordering of its own — the order is the declaration's, because LAD
/// executes in network order and what must run before what is a claim about the program. What the
/// generator does is CHECK the orderings its caller can derive from things already decided elsewhere:
/// <see cref="SlotFcGenerator"/> already knows the block under test runs ahead of the copy layer, and says
/// so in a sentence. This type is that sentence made checkable.</para>
/// </summary>
/// <param name="Earlier">The block that must be called first.</param>
/// <param name="Later">The block that must be called after it.</param>
/// <param name="Why">The reason, printed verbatim in the refusal. A rule whose reason is not in the message gets argued with.</param>
public sealed record ObCallOrder(string Earlier, string Later, string Why);

/// <summary>
/// 🔴 <b>A BLOCK THAT MUST APPEAR IN THE CALL LIST AT ALL — the orphan check, mechanised.</b>
///
/// <para>A hand-written slot FC was once deployed and called by nothing. Every vector in that lane timed
/// out and the start echo reported "commanded, observed to run" throughout, because both halves of that
/// echo live in the copy layer, which IS called. It cost a wave and three hours.
/// <see cref="SlotFcGenerator"/> emits an obligation about it; this makes the obligation a check.</para>
/// </summary>
public sealed record ObCallPresence(string BlockName, string Why);

/// <summary>The generated OB.</summary>
/// <param name="Ir">The block, as readable IR.</param>
/// <param name="BlockName">Echoed so a caller building a manifest does not re-derive it.</param>
/// <param name="CalledBlocks">Every block the OB calls, in call order. The input to a reachability check that no longer has to parse IR.</param>
public sealed record CyclicObResult(string Ir, string BlockName, IReadOnlyList<string> CalledBlocks);

/// <summary>
/// 🔴 <b>THE CYCLIC OB: AN ORDERED CALL LIST, AND THE ORDER IS THE PART THAT IS NOT MECHANICAL.</b>
///
/// <para>The block's shape is entirely derivable — a header, the event class's system parameters, and one
/// network per call. <b>The ORDER is not.</b> Which block runs before which is a claim about the program:
/// the stimulus model must command the plant before the map that reads it, the copy layer must publish
/// after the control that produced what it publishes, and none of that is recoverable from the set of
/// blocks. So <b>the order comes from the declaration, verbatim, and this generator originates none of
/// it</b> — it only refuses a declared order that contradicts an ordering its caller derived from a
/// decision already taken (<see cref="ObCallOrder"/>) and a call list that leaves a generated block
/// unreachable (<see cref="ObCallPresence"/>).</para>
///
/// <para>🔴 <b>WHY THE EVENT CLASS IS GATED TO <c>ProgramCycle</c>.</b> TIA's own <c>Import()</c> requires
/// an OB's system parameters to be present and informative, and they DIFFER BY EVENT CLASS. The committed
/// corpus grounds exactly one set — <c>Main</c>'s <c>Initial_Call</c>/<c>Remanence</c> pair. Emitting a
/// guessed pair for a Startup or a Cyclic Interrupt OB would produce a block that converts, preflights and
/// then fails at import for a reason nothing in the toolchain says out loud, so any other event class is
/// refused by name instead.</para>
///
/// <para>Contract copied from <see cref="SlotFcGenerator"/> unchanged: pure text-out, no filesystem, no
/// Portal, block number required, every uncertainty a refusal rather than a guess.</para>
/// </summary>
public static class CyclicObGenerator
{
    private static readonly Regex SafeIdentifier = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    private static readonly Regex SafeInstancePath =
        new("^[A-Za-z_][A-Za-z0-9_]*(\\.[A-Za-z_][A-Za-z0-9_]*)*$", RegexOptions.Compiled);

    /// <summary>The one grounded event class. See the type doc.</summary>
    public const string GroundedSecondaryType = "ProgramCycle";

    /// <summary>
    /// The system parameters TIA requires on a ProgramCycle OB, copied verbatim from the committed
    /// <c>ir/test-project001/Main.ir</c>. <b>Not invented and not parameterised</b>: they are TIA's, they
    /// are fixed for this event class, and a declaration that could change them would be a declaration that
    /// could break the import.
    /// </summary>
    private static readonly string[] SystemParameters =
    {
        "    Initial_Call : Bool BAREPARAM INFORMATIVE \"Initial call of this OB\"",
        "    Remanence : Bool BAREPARAM INFORMATIVE \"=True, if remanent data are available\"",
    };

    /// <param name="calls">
    /// 🔴 <b>IN THE ORDER THEY WILL EXECUTE.</b> Empty is refused: an OB calling nothing is a scan that runs
    /// no program, and it is the shape a declaration collapses to when somebody misspells the key that holds
    /// the list.
    /// </param>
    /// <param name="presence">What must be called at all. Derived by the caller; see <see cref="ObCallPresence"/>.</param>
    /// <param name="order">What must precede what. Derived by the caller; see <see cref="ObCallOrder"/>.</param>
    public static CyclicObResult Generate(
        CyclicObNaming naming,
        IReadOnlyList<ObCall> calls,
        IReadOnlyList<ObCallPresence>? presence = null,
        IReadOnlyList<ObCallOrder>? order = null)
    {
        ArgumentNullException.ThrowIfNull(naming);
        ArgumentNullException.ThrowIfNull(calls);

        Validate(naming, calls);
        CheckPresence(naming, calls, presence ?? Array.Empty<ObCallPresence>());
        CheckOrder(naming, calls, order ?? Array.Empty<ObCallOrder>());

        var ir = new StringBuilder();
        ir.Append($"BLOCK OB {naming.BlockName}\n");
        ir.Append("ROOTID 0\n");
        ir.Append($"NUMBER {naming.Number}\n");
        ir.Append("LANGUAGE LAD\n");
        ir.Append($"SECONDARYTYPE {naming.SecondaryType}\n");

        // The committed Main.ir carries its title quoted INSIDE the IR string — `TITLE "\"...\""` — because
        // that is what TIA exported. Reproduced rather than tidied: the round trip is the authority on this
        // field, not taste.
        ir.Append($"TITLE \"\\\"{Escape(naming.Title)}\\\"\"\n");
        ir.Append('\n');
        ir.Append("INTERFACE\n");
        ir.Append("  INPUT\n");
        foreach (var parameter in SystemParameters)
            ir.Append(parameter).Append('\n');

        ir.Append("  CONSTANT\n");

        for (var i = 0; i < calls.Count; i++)
        {
            ir.Append('\n');
            ir.Append($"NETWORK {i + 1} \"{Escape(calls[i].NetworkTitle)}\"\n");
            if (!string.IsNullOrWhiteSpace(calls[i].NetworkComment))
                ir.Append($"  COMMENT \"{Escape(calls[i].NetworkComment!)}\"\n");

            ir.Append("  ").Append(CallStatement(calls[i])).Append('\n');
        }

        return new CyclicObResult(ir.ToString(), naming.BlockName, calls.Select(c => c.BlockName).ToArray());
    }

    private static string CallStatement(ObCall call) =>
        call.InstancePath is null
            ? $"CALL {call.BlockName}(EN := TRUE)"
            : $"CALL {call.BlockName}({call.InstancePath}, EN := TRUE)";

    private static void CheckPresence(CyclicObNaming naming, IReadOnlyList<ObCall> calls, IReadOnlyList<ObCallPresence> presence)
    {
        var called = new HashSet<string>(calls.Select(c => c.BlockName), StringComparer.OrdinalIgnoreCase);

        foreach (var required in presence)
        {
            if (called.Contains(required.BlockName))
                continue;

            throw new ArgumentException(
                $"'{naming.BlockName}' does not call '{required.BlockName}'. {required.Why}",
                nameof(calls));
        }
    }

    private static void CheckOrder(CyclicObNaming naming, IReadOnlyList<ObCall> calls, IReadOnlyList<ObCallOrder> order)
    {
        var position = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < calls.Count; i++)
            position.TryAdd(calls[i].BlockName, i);

        foreach (var rule in order)
        {
            // 🔴 A RULE ABOUT TWO BLOCKS, ONE OF WHICH IS NOT CALLED, IS A RULE THAT PASSED BY EXAMINING
            // NOTHING. Empty is not clean: the absent block is either the orphan the presence check exists
            // for, or a rule aimed at a block that is not in this program, and both need saying.
            if (!position.TryGetValue(rule.Earlier, out var earlier))
            {
                throw new ArgumentException(
                    $"'{naming.BlockName}' is required to call '{rule.Earlier}' before '{rule.Later}', and it does not call "
                    + $"'{rule.Earlier}' at all — so the ordering was checked against nothing. {rule.Why}",
                    nameof(calls));
            }

            if (!position.TryGetValue(rule.Later, out var later))
            {
                throw new ArgumentException(
                    $"'{naming.BlockName}' is required to call '{rule.Earlier}' before '{rule.Later}', and it does not call "
                    + $"'{rule.Later}' at all — so the ordering was checked against nothing. {rule.Why}",
                    nameof(calls));
            }

            if (earlier < later)
                continue;

            throw new ArgumentException(
                $"'{naming.BlockName}' calls '{rule.Later}' at network {later + 1} and '{rule.Earlier}' at network "
                + $"{earlier + 1}. LAD executes in network order, so this runs them the wrong way round. {rule.Why}",
                nameof(calls));
        }
    }

    private static void Validate(CyclicObNaming naming, IReadOnlyList<ObCall> calls)
    {
        if (!SafeIdentifier.IsMatch(naming.BlockName ?? string.Empty))
            throw new ArgumentException($"'{naming.BlockName}' is not a usable block name.", nameof(naming));

        if (naming.Number <= 0)
        {
            throw new ArgumentException(
                $"'{naming.BlockName}' has no OB number. It is required and never defaulted: an OB's number is fixed by its "
                + "event class, so it is a fact to be read off the project rather than allocated.",
                nameof(naming));
        }

        if (naming.Number is >= 9000 and <= 9999)
        {
            throw new ArgumentException(
                $"'{naming.BlockName}' declares OB number {naming.Number}, which is inside the 9000-9999 harness band. "
                + "OBs are EXCLUDED from that band precisely because an OB's number is fixed by its event class and is not "
                + "the harness's to allocate.",
                nameof(naming));
        }

        if (!string.Equals(naming.SecondaryType, GroundedSecondaryType, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"'{naming.BlockName}' declares event class '{naming.SecondaryType}'. Only '{GroundedSecondaryType}' is "
                + "grounded: TIA requires an OB's system parameters to be present and informative, they differ by event "
                + "class, and the committed corpus contains exactly one set. A guessed pair produces a block that converts "
                + "and preflights clean and is then refused at import for a reason nothing prints.",
                nameof(naming));
        }

        if (string.IsNullOrWhiteSpace(naming.Title))
            throw new ArgumentException($"'{naming.BlockName}' has no title; the generator will not invent one.", nameof(naming));

        if (calls.Count == 0)
        {
            throw new ArgumentException(
                $"'{naming.BlockName}' declares no calls. A cyclic OB that calls nothing is a scan that runs no program — and "
                + "it is what a declaration collapses to when the key holding the call list is misspelt, so it is refused "
                + "rather than emitted as an empty block.",
                nameof(calls));
        }

        var seenBlocks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var seenInstances = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < calls.Count; i++)
        {
            var call = calls[i];
            ArgumentNullException.ThrowIfNull(call);

            if (!SafeIdentifier.IsMatch(call.BlockName ?? string.Empty))
                throw new ArgumentException($"'{call.BlockName}' is not a usable block name (network {i + 1}).", nameof(calls));

            if (call.InstancePath is not null && !SafeInstancePath.IsMatch(call.InstancePath))
            {
                throw new ArgumentException(
                    $"'{call.InstancePath}' is not a usable instance path (network {i + 1}). Pass an instance DB name, or a "
                    + "dotted multi-instance path, or null for an FC — which has no instance at all.",
                    nameof(calls));
            }

            if (string.IsNullOrWhiteSpace(call.NetworkTitle))
            {
                throw new ArgumentException(
                    $"network {i + 1} calls '{call.BlockName}' with no title. Every network gets one (C-201) and the generator "
                    + "will not invent it: an invented title passes review and tells a reader nothing about why the call is "
                    + "where it is — which, in the one block whose entire content is an order, is the only thing worth saying.",
                    nameof(calls));
            }

            // 🔴 A BLOCK CALLED TWICE IN ONE SCAN IS NOT A LONGER PROGRAM, IT IS A BLOCK WHOSE SECOND
            // EXECUTION OVERWRITES THE FIRST'S OUTPUTS. Refused rather than emitted: it is a plausible typo
            // (a copy-pasted network) with no plausible correct reading in a harness OB.
            if (!seenBlocks.TryAdd(call.BlockName!, i))
            {
                throw new ArgumentException(
                    $"'{call.BlockName}' is called twice, at networks {seenBlocks[call.BlockName!] + 1} and {i + 1}. In one "
                    + "scan the second execution overwrites what the first produced.",
                    nameof(calls));
            }

            if (call.InstancePath is not null && !seenInstances.TryAdd(call.InstancePath, i))
            {
                throw new ArgumentException(
                    $"instance '{call.InstancePath}' is used by two calls, at networks {seenInstances[call.InstancePath] + 1} "
                    + $"and {i + 1}. Two blocks sharing one instance overwrite each other's statics every scan, and the "
                    + "symptom is results that are neither block's.",
                    nameof(calls));
            }
        }
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
