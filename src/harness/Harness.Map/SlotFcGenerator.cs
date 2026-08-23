using System.Text;
using System.Text.RegularExpressions;

namespace Harness.Map;

/// <summary>One call the slot FC makes.</summary>
/// <param name="BlockName">The FB or FC being called.</param>
/// <param name="InstancePath">
/// Its instance DB, or a multi-instance static path. <b>Null for an FC</b>, which is stateless and has
/// no instance — the readable form then omits the leading positional argument entirely
/// (<c>ir/SPEC.md</c>, S1 item 24).
/// </param>
/// <param name="NetworkTitle">The network's title. Every network gets one (C-201).</param>
public sealed record SlotCall(string BlockName, string? InstancePath, string NetworkTitle);

/// <summary>Naming and numbering the generator needs and must not invent.</summary>
/// <param name="BlockName">Name of the generated slot FC.</param>
/// <param name="BlockNumber">
/// Required, with no default — hard rule 3 forbids inventing block numbers, and X-J reserves
/// 9000–9999 for harness objects, which the CALLER allocates from
/// (<c>converter claim --allocate --kind block-number --type FC --floor 9000</c>).
/// </param>
public sealed record SlotFcNaming(string BlockName, int BlockNumber);

/// <summary>
/// The generated slot FC and the obligation that comes with it.
/// </summary>
/// <param name="Ir">The block, as readable IR.</param>
/// <param name="BlockName">Echoed so a caller building a manifest does not re-derive it.</param>
/// <param name="CallSiteObligation">
/// 🔴 <b>The sentence a caller must act on, emitted WITH the block rather than left to be remembered.</b>
/// A generated FC that nothing calls is deployed, loaded, reported healthy, and never runs.
/// </param>
public sealed record SlotFcResult(string Ir, string BlockName, string CallSiteObligation);

/// <summary>
/// 🔴 <b>The slot FC: two calls, and the ORDER IS THE WHOLE CONTENT.</b>
///
/// <para>A conformance lane needs three blocks. The copy layer is generated
/// (<see cref="CopyLayerGenerator"/>); this is the second. It is two <c>CALL</c> statements, which is
/// exactly why it should never have been hand-authored: there is nothing in it to get right except the
/// thing that is easy to get wrong.</para>
///
/// <para><b>THE STIMULUS HEAD IS CALLED FIRST AND THAT IS NOT A PARAMETER.</b> The head commands the
/// plant model for THIS scan; the block under test must then see this scan's commands, not last scan's.
/// Reversing them costs one scan of latency on every transition, which does not fail a compile, does not
/// fail an import, and shows up as vectors that time out near their backstop for reasons nobody can
/// see. A caller cannot express the wrong order because there is no argument for it.</para>
///
/// <para>🔴 <b>WHY THIS EXISTS AT ALL: the orphan.</b> A hand-written slot FC was deployed and called by
/// nothing. Every vector in that lane timed out, and X-E's start echo reported <i>"commanded, observed to
/// run"</i> throughout — both halves of that echo live in the copy layer, which IS called, so the loop
/// closes without the block under test ever executing. It cost a wave and three hours. A generated FC
/// emits its own <see cref="SlotFcResult.CallSiteObligation"/>, which makes the omission a thing somebody
/// declined to do rather than a thing nobody was told about. <b>It does NOT make
/// <c>Harness.Batch.Reachability</c> redundant</b>: generation prevents the orphan THIS generator would
/// create and says nothing about a hand-authored block in the same union.</para>
///
/// <para>Contract copied from <see cref="CopyLayerGenerator"/> exactly: pure text-out, no filesystem, no
/// Portal, block number required, and every uncertainty a refusal rather than a guess.</para>
/// </summary>
public static class SlotFcGenerator
{
    private static readonly Regex SafeIdentifier = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    /// <summary>A multi-instance placement is a dotted path of safe identifiers; an iDB is one segment.</summary>
    private static readonly Regex SafeInstancePath =
        new("^[A-Za-z_][A-Za-z0-9_]*(\\.[A-Za-z_][A-Za-z0-9_]*)*$", RegexOptions.Compiled);

    /// <param name="stimulusHead">Called FIRST. See the type doc — the order is not negotiable.</param>
    /// <param name="blockUnderTest">Called SECOND, so it observes this scan's commands.</param>
    public static SlotFcResult Generate(SlotFcNaming naming, SlotCall stimulusHead, SlotCall blockUnderTest)
    {
        ArgumentNullException.ThrowIfNull(naming);
        ArgumentNullException.ThrowIfNull(stimulusHead);
        ArgumentNullException.ThrowIfNull(blockUnderTest);

        Validate(naming, stimulusHead, blockUnderTest);

        var ir = new StringBuilder();
        ir.Append($"BLOCK FC {naming.BlockName}\n");
        ir.Append("ROOTID 0\n");
        ir.Append($"NUMBER {naming.BlockNumber}\n");
        ir.Append("LANGUAGE LAD\n");
        ir.Append("TITLE \"Harness slot\"\n");
        ir.Append('\n');
        ir.Append("INTERFACE\n");
        ir.Append("  INPUT\n");
        ir.Append("  OUTPUT\n");
        ir.Append("  CONSTANT\n");

        // 🔴 The head first. Emitted from a fixed list, not from anything the caller ordered.
        var calls = new[] { stimulusHead, blockUnderTest };
        for (var i = 0; i < calls.Length; i++)
        {
            ir.Append('\n');
            ir.Append($"NETWORK {i + 1} \"{Escape(calls[i].NetworkTitle)}\"\n");
            ir.Append("  ").Append(CallStatement(calls[i])).Append('\n');
        }

        return new SlotFcResult(
            ir.ToString(),
            naming.BlockName,
            $"'{naming.BlockName}' MUST be called from the cyclic OB, ahead of the copy layer. "
            + "A slot FC that nothing calls is deployed, loaded, reported healthy by every artifact, and "
            + "never executes — and the start echo cannot detect it, because both halves of the echo live "
            + "in the copy layer, which is called. Verify the call site; do not infer it from a green deploy.");
    }

    /// <summary>
    /// <c>CALL FB_X(iDB_X, EN := TRUE)</c> for an FB, <c>CALL FC_X(EN := TRUE)</c> for an FC — the exact
    /// readable form in <c>ir/SPEC.md</c> and in the committed corpus. <c>EN</c> is always shown
    /// explicitly, matching the convention every other statement kind follows.
    /// </summary>
    private static string CallStatement(SlotCall call) =>
        call.InstancePath is null
            ? $"CALL {call.BlockName}(EN := TRUE)"
            : $"CALL {call.BlockName}({call.InstancePath}, EN := TRUE)";

    private static void Validate(SlotFcNaming naming, SlotCall head, SlotCall uut)
    {
        if (naming.BlockNumber <= 0)
        {
            throw new ArgumentException(
                "the slot FC's block number is required and must be positive. Hard rule 3 forbids inventing one, and "
                + "X-J reserves 9000-9999 for harness objects — allocate it with "
                + "`converter claim --allocate --kind block-number --type FC --floor 9000`.",
                nameof(naming));
        }

        if (!SafeIdentifier.IsMatch(naming.BlockName ?? string.Empty))
            throw new ArgumentException($"'{naming.BlockName}' is not a usable block name.", nameof(naming));

        ValidateCall(head, nameof(head));
        ValidateCall(uut, nameof(uut));

        // 🔴 TWO CALLS ONTO ONE INSTANCE IS NOT A SLOT, IT IS A BLOCK CALLED TWICE. The head and the block
        // under test are different things with different state; sharing an instance would have them
        // overwrite each other's statics every scan, and the symptom is results that are neither block's.
        if (head.InstancePath is not null
            && string.Equals(head.InstancePath, uut.InstancePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"the stimulus head and the block under test both name instance '{head.InstancePath}'. They are "
                + "different blocks with different state; one instance cannot hold both.",
                nameof(uut));
        }

        if (string.Equals(head.BlockName, uut.BlockName, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"the stimulus head and the block under test are both '{head.BlockName}'. A block cannot drive itself: "
                + "the head exists to command the plant the block under test reads.",
                nameof(uut));
        }
    }

    private static void ValidateCall(SlotCall call, string parameter)
    {
        if (!SafeIdentifier.IsMatch(call.BlockName ?? string.Empty))
            throw new ArgumentException($"'{call.BlockName}' is not a usable block name.", parameter);

        if (call.InstancePath is not null && !SafeInstancePath.IsMatch(call.InstancePath))
        {
            throw new ArgumentException(
                $"'{call.InstancePath}' is not a usable instance path. Pass an instance DB name, or a dotted "
                + "multi-instance path, or null for an FC — which has no instance at all.",
                parameter);
        }

        // Every network gets a title (C-201), and a generated one is no exception. Refused rather than
        // defaulted: a generator that invents "Network 1" produces a block that passes review and tells
        // a reader nothing.
        if (string.IsNullOrWhiteSpace(call.NetworkTitle))
            throw new ArgumentException("every network needs a title; the generator will not invent one.", parameter);
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
