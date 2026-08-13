namespace Harness.Map;

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
/// <param name="ResultSources">Tags moved OUT into the result registers, in register order.</param>
public sealed record SlotBinding(
    string SlotId,
    IReadOnlyList<string> VectorTargets,
    string? StartCondition,
    IReadOnlyList<string> ResultSources);

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

    /// <summary>Moves the block's outputs into the slot's result registers.</summary>
    ResultsOut,
}

/// <summary>One generated network: its kind, its title, and every source/destination pair in it.</summary>
public sealed record CopyLayerNetwork(
    int Number,
    CopyLayerNetworkKind Kind,
    string Title,
    IReadOnlyList<(string From, string To)> Moves);

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
    SlotBinding Binding,
    BuildStamp Stamp,
    IReadOnlyList<MirrorTag> Tags,
    IReadOnlyList<CopyLayerNetwork> Networks,
    bool NoStartGateAsserted)
{
    /// <summary>Vector registers actually wired. Registers allocated beyond this are declared by nothing and read by nothing.</summary>
    public int BoundVectorRegisters => Binding.VectorTargets.Count;

    /// <summary>Result registers actually wired.</summary>
    public int BoundResultRegisters => Binding.ResultSources.Count;
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
