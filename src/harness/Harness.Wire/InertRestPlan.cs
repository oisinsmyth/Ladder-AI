using Harness.Map;

namespace Harness.Wire;

/// <summary>
/// Where one result register's inert expectation came from. <b>Four different facts, never collapsed to
/// "it has one"</b> — the whole defect this file closes was a register that had an expectation nobody
/// stated.
/// </summary>
public enum InertRestProvenance
{
    /// <summary>The binding stated this signal's resting value. The strong case, and the only one worth having.</summary>
    Declared,

    /// <summary>The binding stated that this signal has no meaningful resting value. <b>Nothing is checked here</b>, by declaration.</summary>
    Excluded,

    /// <summary>
    /// 🔴 <b>Nobody stated it and the slot's <c>assumedZeroRest</c> claim filled the hole with 0.</b> A
    /// DEFAULT, and it is carried as one everywhere it is reported — including into the failure text of
    /// any inert check that fires on it, so a reader is told the expectation may be the wrong half.
    /// </summary>
    Defaulted,

    /// <summary>
    /// The harness DERIVED it from the artifact it emits itself, not from anyone's word. Latch registers
    /// only — nobody declares a latch, because the copy layer generates the rungs that drive it.
    /// </summary>
    Derived,
}

/// <summary>One result register, what is expected of it at inert, and on whose authority.</summary>
/// <param name="Expected">Meaningless when <see cref="Provenance"/> is <see cref="InertRestProvenance.Excluded"/>.</param>
public sealed record InertRestRegister(
    int Register,
    InertRestProvenance Provenance,
    ushort Expected,
    string Signal,
    string Detail)
{
    public bool Gated => Provenance != InertRestProvenance.Excluded;

    public override string ToString() =>
        $"R{Register:000} {Provenance.ToString().ToUpperInvariant()} "
        + (Gated ? $"= {Expected}" : "= <not gated>")
        + $"  {Signal}" + (Detail.Length == 0 ? string.Empty : $" — {Detail}");
}

/// <summary>
/// 🔴 <b>ONE SLOT'S INERT EXPECTATION, COMPUTED FROM THE BINDING INSTEAD OF ASSUMED — and the ruling on
/// what an UNDECLARED resting value means.</b>
///
/// <para>*** WHAT WAS THERE BEFORE. *** <c>LoopRun.ToWireVector</c> built the declaration as
/// <c>Enumerable.Range(0, binding.ResultRegistersNeeded).ToDictionary(i =&gt; i, _ =&gt; (ushort)0)</c>:
/// <b>every result register asserted to rest at zero, hardcoded</b>, under a type whose own summary says
/// <i>"it must be declared: a check with no expectation passes over anything"</i>.</para>
///
/// <para><b>THE RULING: AN UNDECLARED RESTING VALUE REFUSES THE RUN.</b> Not zero, not excluded. Three
/// readings were available and each was tested against what it does when it is wrong:</para>
/// <list type="number">
/// <item><b>absent ⇒ 0</b> — today's behaviour. <i>Rejected, and not merely because it is the defect:
/// it fails in BOTH directions and one of them is silent.</i> Loud half: a signal resting at a
/// <c>-1</c> sentinel refuses a correct program, and gets blamed on the block. <b>Silent half, which is
/// the disqualifying one: where 0 is a MEASURED PASS VERDICT, asserting 0 accepts the previous index's
/// leftover result as an inert start state — the gate passes over exactly the state it exists to
/// catch.</b> A default whose failure modes include a false pass cannot be the default.</item>
/// <item><b>absent ⇒ excluded</b> — <i>rejected.</i> It is the contract's opening sentence, inverted:
/// D33's first check would examine nothing and still report Established. It is also the one wrong answer
/// that leaves no trace at all.</item>
/// <item><b>absent ⇒ REFUSE</b> — <i>chosen.</i> Its failure mode is loud, it is paid once per slot by the
/// party that already owns every other instrumentation fact, and it is the treatment this codebase gives
/// every other undeclared-but-load-bearing thing: <c>MirrorValueType.Unstated</c>, an absent
/// <c>specName</c> (which is NOT the tag), a null <c>completionValue</c>, a <c>startCondition</c> whose
/// null is a claim.</item>
/// </list>
///
/// <para><b>AND THE REFUSAL HAS A MIGRATION PATH, BECAUSE A SLOT IS DEPLOYED AND RUNNING AGAINST THE OLD
/// BEHAVIOUR RIGHT NOW.</b> <see cref="SlotBinding.AssumedZeroRest"/> is the named escape (FI-71's shape):
/// per slot, reason required, every register it covers counted and listed as DEFAULTED rather than
/// declared. <b>An escape flag must cost something</b> — this one costs the claim's visibility on every
/// run and the word DEFAULTED in the text of any failure it produces.</para>
///
/// <para><b>LATCH REGISTERS ARE DERIVED AND NOT DECLARABLE.</b> A latch is not a signal the block
/// publishes; it is a rung THIS HARNESS EMITS, so its resting value is a computed fact about our own
/// artifact and asking the coordinator for it would be asking them to guess at our output. A PHASE-ARMED
/// latch carries <c>RCOIL := NOT &lt;start bool&gt;</c> and the inert phase lowers the start bool and waits
/// at least one scan before it reads, so it rests at 0. A once-per-wave latch is an unconditional
/// <c>SCOIL</c> that stays set for the rest of the wave once it fires — <b>it has no per-index resting
/// value at all</b>, so it is EXCLUDED, derived, and the reason is stated rather than left as a zero
/// somebody would eventually be surprised by.</para>
/// </summary>
public sealed record InertRestPlan(
    string SlotId,
    InertDeclaration? Declaration,
    IReadOnlyList<InertRestRegister> Registers,
    IReadOnlyList<string> Refusals,
    IReadOnlyList<string> Notes)
{
    /// <summary>True only when every register in the band has an expectation somebody is answerable for.</summary>
    public bool Planned => Refusals.Count == 0 && Declaration is not null;

    /// <summary>The declaration, or a throw. For callers that must not read a null as an empty one.</summary>
    public InertDeclaration Require() =>
        Declaration ?? throw new InvalidOperationException(
            $"slot '{SlotId}' has no inert declaration: {string.Join(" | ", Refusals)}");

    public int DeclaredCount => Registers.Count(r => r.Provenance == InertRestProvenance.Declared);
    public int ExcludedCount => Registers.Count(r => r.Provenance == InertRestProvenance.Excluded);
    public int DefaultedCount => Registers.Count(r => r.Provenance == InertRestProvenance.Defaulted);
    public int DerivedCount => Registers.Count(r => r.Provenance == InertRestProvenance.Derived);

    /// <summary>
    /// What this plan did, <b>with its denominator, on every run including the clean one.</b> A report that
    /// appears only on bad news teaches its reader that absence means it did not run.
    /// </summary>
    public string Summary() =>
        $"slot '{SlotId}': {Registers.Count} result register(s) — "
        + $"{DeclaredCount} DECLARED, {ExcludedCount} EXCLUDED by declaration, {DefaultedCount} DEFAULTED, "
        + $"{DerivedCount} DERIVED (latch band)"
        + (Refusals.Count > 0 ? $"; NOT PLANNED, {Refusals.Count} refusal(s)" : string.Empty);

    /// <summary>
    /// Compute one slot's inert expectation from its binding.
    /// </summary>
    /// <param name="order">
    /// The 32-bit word order, needed because a <c>Time</c> resting value spans two registers.
    /// <b>The same value the stimulus side writes under</b> — reading under one order and writing under the
    /// other cancels out on our own loopback and disagrees only against the device.
    /// </param>
    public static InertRestPlan For(SlotBinding binding, RegisterWordOrder order, int quiescenceScans = 1)
    {
        ArgumentNullException.ThrowIfNull(binding);

        var refusals = new List<string>();
        var notes = new List<string>();
        var registers = new List<InertRestRegister>();

        var escape = EscapeOf(binding, refusals);
        var offsets = binding.ResultRegisterOffsets;
        var sources = binding.ResultSources ?? Array.Empty<MirroredSignal>();

        for (var i = 0; i < sources.Count; i++)
        {
            var signal = sources[i];
            var element = MirrorElements.For(signal.Type);

            if (element is null)
            {
                refusals.Add(
                    $"'{signal.Tag}' has element type {signal.Type}, which has no width, so its resting value cannot be "
                    + $"placed in the register band at all. Supported: {MirrorElements.Supported}.");
                continue;
            }

            var span = Enumerable.Range(offsets[i], element.Registers).ToArray();
            var name = signal.JoinKey;

            // ---- the binding said nothing --------------------------------------------------------
            if (signal.Rest is null)
            {
                if (escape is null)
                {
                    refusals.Add(
                        $"'{name}' declares no resting value, so R{string.Join("/", span.Select(r => r.ToString("000")))} would be "
                        + "gated on an expectation nobody stated. *** THIS IS A REFUSAL AND NOT A ZERO. *** A hardcoded zero is "
                        + "wrong in both directions on real hardware: it refuses a signal resting at a -1 sentinel, AND it accepts "
                        + "a stale 0 on a signal where 0 is a measured PASS verdict — which is the inert check passing over exactly "
                        + "the state it exists to catch. State `inertRest` on this signal: a value, or EXCLUDED with a reason. "
                        + "If this slot cannot be re-declared yet, `assumedZeroRest` on the slot says so out loud and every register "
                        + "it covers is reported as DEFAULTED.");
                    continue;
                }

                foreach (var register in span)
                {
                    registers.Add(new InertRestRegister(register, InertRestProvenance.Defaulted, 0, name,
                        $"NOBODY DECLARED THIS. Assumed 0 under the slot's assumedZeroRest claim: {escape}"));
                }

                continue;
            }

            // ---- the binding said something, and it may still be malformed ------------------------
            var malformed = signal.Rest.Malformed(name);
            if (malformed is not null)
            {
                refusals.Add(malformed);
                continue;
            }

            if (signal.Rest.IsExcluded)
            {
                foreach (var register in span)
                {
                    registers.Add(new InertRestRegister(register, InertRestProvenance.Excluded, 0, name,
                        $"NOT GATED, by declaration: {signal.Rest.Basis}"));
                }

                continue;
            }

            // *** ONE RANGE RULE, SHARED WITH THE STIMULUS SIDE. *** The declared rest goes through
            // MirrorValueFit with this signal's own type AND its own encoding, so a Bool resting `true`, an
            // Int resting `-1` and a symbolic value resolved through a declared table all work without a
            // second numeric path here. A second path beside the first is how two derivations of one rule
            // come to disagree, which this codebase has three recorded instances of.
            var fit = MirrorValueFit.Check(name, signal.Type, signal.Rest.Value, signal.Encoding);

            if (!fit.Fits)
            {
                refusals.Add($"the declared RESTING value of '{name}' does not fit its mirror element. {fit.Refusal}");
                continue;
            }

            var words = element.Form == MirrorAddressForm.DoubleWord
                ? RegisterWords.From32(unchecked((uint)(int)fit.Value), order)
                : new[] { unchecked((ushort)(short)fit.Value) };

            for (var w = 0; w < span.Length; w++)
            {
                registers.Add(new InertRestRegister(span[w], InertRestProvenance.Declared, words[w], name,
                    $"declared to rest at {signal.Rest.Value}"
                    + (signal.Rest.Basis.Length == 0 ? string.Empty : $" ({signal.Rest.Basis})")
                    + (span.Length > 1 ? $" [word {w + 1} of {span.Length}, {order}]" : string.Empty)));
            }
        }

        // ---- the latch band: DERIVED from the rungs this harness emits, never declared -------------
        var latches = binding.LatchRegisterOffsets;

        foreach (var signal in sources.Where(s => s.Transient))
        {
            if (!latches.TryGetValue(signal.Tag, out var register))
                continue;

            if (signal.PhaseArmed)
            {
                registers.Add(new InertRestRegister(register, InertRestProvenance.Derived, 0, signal.JoinKey + " (latch)",
                    "DERIVED, not declared: the generated latch carries RCOIL := NOT <start bool>, and the inert phase lowers "
                    + "the start bool and waits at least one scan before it reads, so a phase-armed latch rests cleared."));
                continue;
            }

            registers.Add(new InertRestRegister(register, InertRestProvenance.Excluded, 0, signal.JoinKey + " (latch)",
                "DERIVED as NOT GATED: this is a once-per-WAVE latch — an unconditional SCOIL that stays set for the rest of "
                + "the wave once it fires — so it has no per-index resting value. It is 0 before the first firing and 1 "
                + "afterwards, and both are correct."));
        }

        // ---- the band must be COVERED, and the check is against the width, not against the loop ----
        var width = binding.ResultRegistersNeeded;
        var covered = registers.Select(r => r.Register).ToHashSet();
        var missing = Enumerable.Range(0, width).Where(r => !covered.Contains(r)).ToArray();

        if (refusals.Count == 0 && missing.Length > 0)
        {
            refusals.Add(
                $"{missing.Length} register(s) of slot '{binding.SlotId}''s {width}-register result band are covered by no "
                + $"signal at all: R{string.Join(", R", missing.Select(r => r.ToString("000")))}. An uncovered register is one "
                + "nothing checks is quiet, and it is a fault in this computation rather than in the binding.");
        }

        if (escape is not null && registers.All(r => r.Provenance != InertRestProvenance.Defaulted))
        {
            notes.Add(
                $"slot '{binding.SlotId}' claims assumedZeroRest and it covered NOTHING — every signal declares its own resting "
                + "value. The claim is inert and can be deleted; leaving it in place means the day a declaration is removed, the "
                + "hole is filled silently instead of refusing.");
        }

        var declaration = refusals.Count > 0
            ? null
            : new InertDeclaration(
                registers.Where(r => r.Gated).ToDictionary(r => r.Register, r => r.Expected),
                quiescenceScans,
                registers.Where(r => !r.Gated).ToDictionary(r => r.Register, r => r.Detail),
                registers.Where(r => r.Provenance == InertRestProvenance.Defaulted).Select(r => r.Register).ToHashSet());

        return new InertRestPlan(
            binding.SlotId,
            declaration,
            registers.OrderBy(r => r.Register).ToArray(),
            refusals,
            notes);
    }

    /// <summary>
    /// The slot's assume-zero basis, or null when it makes no such claim. <b>A claim with no basis is a
    /// refusal even when it covers nothing</b> — a malformed escape sitting in a document is one that
    /// activates silently the day a declaration is removed.
    /// </summary>
    private static string? EscapeOf(SlotBinding binding, List<string> refusals)
    {
        if (!binding.AssumedZeroRest)
            return null;

        if (string.IsNullOrWhiteSpace(binding.AssumedZeroRestBasis))
        {
            refusals.Add(
                $"slot '{binding.SlotId}' claims assumedZeroRest and gives no basis. The claim weakens D33's first check on "
                + "every register it covers, so it is checked by a reader or by nobody — state why the undeclared signals may "
                + "be assumed to rest at zero, and what is being done about declaring them.");

            return null;
        }

        return binding.AssumedZeroRestBasis!.Trim();
    }
}
