namespace Harness.CmdInject;

/// <summary>
/// A role the tool holds — <b>the protocol vocabulary, and the whole of what this binary knows about the
/// map.</b>
///
/// <para>🔴 <b>THE TOOL KNOWS THE PROTOCOL AND MUST NOT KNOW THE MAP.</b> Which register plays
/// <see cref="Seq"/>, which plays <see cref="AckCount"/>, which plays <see cref="Enable"/> — those are
/// facts about the protocol and they live here. Which register NUMBER, which TAG NAME, which command
/// CODE — those are live-run restricted data (docs/13) and load at runtime from a binding file. Not one
/// of them is a member of this enum, and that is the point: a role is a thing the tool reasons about,
/// a register number is a thing it is handed.</para>
/// </summary>
public enum InjectionRole
{
    // ---- band-level roles: one register each, shared across every channel ----

    /// <summary>The heartbeat the client stamps and the program echoes — proof the write reached the block.</summary>
    Heartbeat,

    /// <summary>The master enable. A command is executed by the block only while this is set.</summary>
    Enable,

    /// <summary>
    /// The safety-permissive substitution (a simulated "healthy" contact). <b>Optional, and written only
    /// under a consent SEPARATE from arming.</b> Absent from a binding that does not simulate safety.
    /// </summary>
    SafetyPermissive,

    // ---- per-channel command roles: the client writes these ----

    /// <summary>
    /// The sequence register — <b>at the LOW address of its channel, and written ALONE, second.</b> A new
    /// sequence landing before its operands would execute a command against the previous one's operands,
    /// so the sequence is the thing torn writes must never be able to manufacture.
    /// </summary>
    Seq,

    /// <summary>The command code. Read and sent verbatim — the tool never judges which codes are valid (job data).</summary>
    Code,

    /// <summary>First integer operand.</summary>
    Int1,

    /// <summary>Second integer operand.</summary>
    Int2,

    /// <summary>First real operand — two registers, high-word-first.</summary>
    Real1,

    /// <summary>Second real operand — two registers, high-word-first.</summary>
    Real2,

    // ---- per-channel observation roles: the client reads these back ----

    /// <summary>The sequence the block acknowledges having processed.</summary>
    AckSeq,

    /// <summary>The code the block acknowledges. Optional.</summary>
    AckCode,

    /// <summary>
    /// The result the block reports. <b>Read, printed VERBATIM, and never branched on</b> — carried as a
    /// string so a call site comparing it to an integer would not compile.
    /// </summary>
    AckResult,

    /// <summary>
    /// The processed-command counter — <b>the ack model is keyed on THIS, never on the result.</b> A move
    /// here is the only thing that says a command was processed.
    /// </summary>
    AckCount,
}

/// <summary>What a role requires of the tag that plays it — nothing job-specific, only shape.</summary>
/// <param name="Role">The role.</param>
/// <param name="IrType">
/// The IR type name the tag must declare, verbatim (<c>UInt</c>, <c>Int</c>, <c>Real</c>, <c>Bool</c>).
/// The map's own parser already cross-checks the type against the address form; this is the second half,
/// binding the type to the role's meaning.
/// </param>
/// <param name="Width">The register width the address must resolve to — derived from, and agreeing with, the type.</param>
/// <param name="Section">Which side of the mirror the role lives on: a command a client writes, or an observation it reads.</param>
public sealed record RoleShape(
    InjectionRole Role,
    string IrType,
    Harness.MirrorView.MirrorWidth Width,
    RoleSection Section);

/// <summary>Which band a role must resolve into.</summary>
public enum RoleSection
{
    /// <summary>A band-level command register (heartbeat, enable, safety) — the client writes it.</summary>
    BandCommand,

    /// <summary>A per-channel command register — the client writes it.</summary>
    ChannelCommand,

    /// <summary>A per-channel observation register — the client reads it.</summary>
    Observation,
}

/// <summary>
/// The complete role table. <b>The only table in this binary — everything else about the map is data.</b>
/// </summary>
public static class Roles
{
    // Aliased so the table below reads as protocol facts rather than a namespace tour.
    private const Harness.MirrorView.MirrorWidth Bit = Harness.MirrorView.MirrorWidth.Bit;
    private const Harness.MirrorView.MirrorWidth Word = Harness.MirrorView.MirrorWidth.Word;
    private const Harness.MirrorView.MirrorWidth DWord = Harness.MirrorView.MirrorWidth.DoubleWord;

    private static readonly IReadOnlyDictionary<InjectionRole, RoleShape> Table =
        new[]
        {
            new RoleShape(InjectionRole.Heartbeat,        "UInt", Word,  RoleSection.BandCommand),
            new RoleShape(InjectionRole.Enable,           "Bool", Bit,   RoleSection.BandCommand),
            new RoleShape(InjectionRole.SafetyPermissive, "Bool", Bit,   RoleSection.BandCommand),

            new RoleShape(InjectionRole.Seq,   "UInt", Word,  RoleSection.ChannelCommand),
            new RoleShape(InjectionRole.Code,  "Int",  Word,  RoleSection.ChannelCommand),
            new RoleShape(InjectionRole.Int1,  "Int",  Word,  RoleSection.ChannelCommand),
            new RoleShape(InjectionRole.Int2,  "Int",  Word,  RoleSection.ChannelCommand),
            new RoleShape(InjectionRole.Real1, "Real", DWord, RoleSection.ChannelCommand),
            new RoleShape(InjectionRole.Real2, "Real", DWord, RoleSection.ChannelCommand),

            new RoleShape(InjectionRole.AckSeq,    "UInt", Word, RoleSection.Observation),
            new RoleShape(InjectionRole.AckCode,   "Int",  Word, RoleSection.Observation),
            new RoleShape(InjectionRole.AckResult, "Int",  Word, RoleSection.Observation),
            new RoleShape(InjectionRole.AckCount,  "UInt", Word, RoleSection.Observation),
        }.ToDictionary(r => r.Role);

    /// <summary>The shape a role requires.</summary>
    public static RoleShape Shape(InjectionRole role) =>
        Table.TryGetValue(role, out var shape)
            ? shape
            : throw new ArgumentOutOfRangeException(nameof(role), role, "no role shape defined.");

    /// <summary>Band-level roles required in every binding. Safety is not here — it is optional.</summary>
    public static readonly IReadOnlyList<InjectionRole> RequiredBandRoles =
        new[] { InjectionRole.Heartbeat, InjectionRole.Enable };

    /// <summary>Per-channel command roles required in every channel. The operands are optional.</summary>
    public static readonly IReadOnlyList<InjectionRole> RequiredChannelCommandRoles =
        new[] { InjectionRole.Seq, InjectionRole.Code };

    /// <summary>The operand roles, in the order they occupy registers above the sequence.</summary>
    public static readonly IReadOnlyList<InjectionRole> OperandRoles =
        new[] { InjectionRole.Code, InjectionRole.Int1, InjectionRole.Int2, InjectionRole.Real1, InjectionRole.Real2 };

    /// <summary>Per-channel observation roles required in every channel. AckCode is optional.</summary>
    public static readonly IReadOnlyList<InjectionRole> RequiredObservationRoles =
        new[] { InjectionRole.AckSeq, InjectionRole.AckResult, InjectionRole.AckCount };
}
