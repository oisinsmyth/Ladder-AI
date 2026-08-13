namespace Harness.Map;

/// <summary>
/// The type of a signal the copy layer mirrors — <b>and it decides the rung shape, not just the tag.</b>
///
/// <para>🔴 <b>THIS EXISTS BECAUSE A HARD-CODED <c>"Int"</c> REACHED A CONTROLLER AND TIA REFUSED IT:</b>
/// <c>Data type Bool is not permitted here.</c> Every result register was declared <c>Int</c> and every
/// result rendered as a plain <c>MOVE</c>, and <b>a Bool does not move</b> — it is copied by a coil. That
/// was not a corner: <b>every signal both conformance vector sets observe is a Bool</b>, so the copy
/// layer could not mirror a single asserted signal.</para>
///
/// <para><b><see cref="Unstated"/> is the ZERO VALUE and it is refused by name</b>, following
/// <c>AssertionForm.Unstated</c> and <c>PresetSource.Unstated</c>. A default of <see cref="Int"/> is
/// precisely what produced the defect: the case the field exists for gets handled, and the case where
/// nobody said gets handled the same way and looks identical. There is deliberately no implicit
/// conversion from a bare tag name.</para>
/// </summary>
public enum MirrorValueType
{
    /// <summary><b>Nobody said.</b> A refusal that names the signal — never a silent <see cref="Int"/>.</summary>
    Unstated = 0,

    /// <summary>
    /// A single bit. Declared <c>Bool</c> at a BIT address inside its own register, and copied by a
    /// <c>COIL</c>. <b>Occupies a whole register</b> — packing is an explicit non-goal of this generator,
    /// and one register carrying one value is what keeps the client's register-index arithmetic true.
    /// </summary>
    Bool,

    /// <summary>A 16-bit word. Declared <c>Int</c> at a word address, and copied by a <c>MOVE</c>.</summary>
    Int,
}

/// <summary>
/// One signal the copy layer mirrors, <b>with the type it actually is</b>.
///
/// <para>The type is carried per signal rather than per slot because a real slot mixes them — the hopper
/// block publishes two Bools, and a counter-style block publishes Ints. <b>There is no constructor that
/// takes a name alone</b>: use <see cref="Bool"/> or <see cref="Int"/>, which put the type in the call.</para>
/// </summary>
public sealed record MirroredSignal(string Tag, MirrorValueType Type)
{
    /// <summary>A Bool signal — mirrored to one bit of its own register, by a coil.</summary>
    public static MirroredSignal Bool(string tag) => new(tag, MirrorValueType.Bool);

    /// <summary>An Int signal — mirrored to a whole register, by a MOVE.</summary>
    public static MirroredSignal Int(string tag) => new(tag, MirrorValueType.Int);

    /// <summary>Several Bool signals, in order.</summary>
    public static IReadOnlyList<MirroredSignal> Bools(params string[] tags) => tags.Select(Bool).ToArray();

    /// <summary>Several Int signals, in order.</summary>
    public static IReadOnlyList<MirroredSignal> Ints(params string[] tags) => tags.Select(Int).ToArray();

    public override string ToString() => $"{Tag} : {Type}";
}

/// <summary>
/// What one slot's registers are wired to in the program under test.
///
/// <para>The coordinator owns this side entirely: per D13 instrumentation is a property of the COPY
/// LAYER, not of the blocks under test, so nothing here asks the block's author for anything except
/// the names of signals that already exist.</para>
/// </summary>
/// <param name="VectorTargets">
/// Tags the vector registers are moved INTO, in register order. May be empty — a pure observation
/// vector writes nothing (see <c>TestVector.IsReadOnly</c>).
/// </param>
/// <param name="StartCondition">
/// The block's EXISTING start condition, bound to the slot's start bool (D37).
///
/// <para><b>Null is a claim, not a blank.</b> D37: "not every block has one, and the absence is
/// meaningful rather than a default" — a purely reactive block (a comparator, a level alarm) has no
/// start gate and is held inert by its input values alone. So a null here asserts "this block has no
/// start gate", which <see cref="CopyLayerPlan.NoStartGateAsserted"/> records so the assertion is
/// visible rather than looking like a generator that forgot.</para>
///
/// <para>It binds to a start condition the block already has, discovered — never to a test-only input
/// added for the purpose, which is scaffolding inside the block under test and exactly what DB-5
/// forbids: what ships would then not be what was tested.</para>
/// </param>
/// <param name="ResultSources">
/// Signals copied OUT into the result registers, in register order — <b>each with its type</b>, which
/// decides both the mirror tag and the rung shape. One signal occupies one register whatever its width;
/// packing is an explicit non-goal, and the client's register index is this list's index.
/// </param>
public sealed record SlotBinding(
    string SlotId,
    IReadOnlyList<MirroredSignal> VectorTargets,
    string? StartCondition,
    IReadOnlyList<MirroredSignal> ResultSources);

/// <summary>What one generated network does. The plan is inspectable before any IR is rendered.</summary>
public enum CopyLayerNetworkKind
{
    /// <summary>
    /// Publishes the build stamp into the version register (§9). Unconditional, one per program, and a
    /// LITERAL — the constant lives in the code, so it can only be present if that code is running.
    /// </summary>
    Version,

    /// <summary>Increments the free-running scan counter. Unconditional, one per program.</summary>
    ScanCounter,

    /// <summary>Moves mirror registers into the block's inputs.</summary>
    VectorIn,

    /// <summary>Drives the block's start condition from the slot's start bool.</summary>
    StartBool,

    /// <summary>
    /// Latches the block's OWN start condition back into the echo register (X-E) — what the program
    /// actually ran, as opposed to what the client commanded.
    /// </summary>
    StartEcho,

    /// <summary>Moves the block's outputs into the slot's result registers.</summary>
    ResultsOut,
}

/// <summary>
/// One copy: where the value comes from, where it goes, and <b>what type it is</b> — which is what
/// decides whether the rung is a <c>MOVE</c> or a <c>COIL</c>.
/// </summary>
public sealed record CopyLayerCopy(string From, string To, MirrorValueType Type);

/// <summary>
/// One generated network: its kind, its title, and every copy in it.
///
/// <para><b>A network holds copies of ONE type.</b> IR groups statements within a network by kind in a
/// fixed order (<c>ir/SPEC.md</c>: COIL before MOVE), so a mixed slot emits one network per type rather
/// than one network that has to be ordered correctly. The generator's standing rule — one KIND per
/// network makes the ordering rule unreachable rather than merely satisfied.</para>
/// </summary>
public sealed record CopyLayerNetwork(
    int Number,
    CopyLayerNetworkKind Kind,
    string Title,
    IReadOnlyList<CopyLayerCopy> Moves);

/// <summary>One artifact the generator emits, as IR text ready to hand to the converter.</summary>
public sealed record HarnessObject(string Name, HarnessObjectKind Kind, string Ir);

/// <summary>The kinds of object the harness generates. Used by <see cref="RetentionCheck"/> to pick its rules.</summary>
public enum HarnessObjectKind
{
    /// <summary>A PLC tag table declaring the mirror's symbolic names at their <c>%M</c> addresses.</summary>
    TagTable,

    /// <summary>A code block — the copy layer FC.</summary>
    Block,

    /// <summary>A data block. The harness generates none in phase 2; the rule exists because it is where retain bites.</summary>
    DataBlock,
}

/// <summary>
/// The copy layer for one slot: the mirror tags it declares, the networks it will emit, and the two
/// facts about the binding that must not be inferred from silence.
/// </summary>
public sealed record CopyLayerPlan(
    RegisterMap Map,
    IReadOnlyList<SlotBinding> Bindings,
    BuildStamp Stamp,
    IReadOnlyList<MirrorTag> Tags,
    IReadOnlyList<CopyLayerNetwork> Networks,
    IReadOnlyList<string> SlotsAssertingNoStartGate)
{
    /// <summary>The single binding, for a one-slot wave set.</summary>
    public SlotBinding Binding => Bindings.Count == 1
        ? Bindings[0]
        : throw new InvalidOperationException($"this plan covers {Bindings.Count} slots; ask for the one you mean.");

    /// <summary>Vector registers actually wired for a slot. Registers beyond this are declared by nothing and read by nothing.</summary>
    public int BoundVectorRegisters(string slotId) => Bindings.Single(b => b.SlotId == slotId).VectorTargets.Count;

    /// <summary>Result registers actually wired for a slot.</summary>
    public int BoundResultRegisters(string slotId) => Bindings.Single(b => b.SlotId == slotId).ResultSources.Count;

    /// <summary>
    /// True when EVERY slot asserts it has no start gate. D37: a null start condition is a CLAIM about
    /// the block ("this one is purely reactive and is held inert by its input values alone"), never a
    /// blank, so it is recorded per slot rather than collapsed to one flag.
    /// </summary>
    public bool NoStartGateAsserted => SlotsAssertingNoStartGate.Count == Bindings.Count;
}

/// <summary>One symbolic name in the mirror tag table, with the <c>%M</c> address it sits at.</summary>
public sealed record MirrorTag(string Name, string DataType, string Address, int ByteAddress, string Comment);

/// <summary>A copy layer, or the reasons there is not one.</summary>
public sealed record CopyLayerResult(CopyLayerPlan? Plan, IReadOnlyList<HarnessObject> Objects, IReadOnlyList<string> Refusals)
{
    public bool Generated => Plan is not null;

    public CopyLayerPlan Require() =>
        Plan ?? throw new InvalidOperationException(
            "copy-layer generation refused:" + Environment.NewLine + "  - " + string.Join(Environment.NewLine + "  - ", Refusals));
}
