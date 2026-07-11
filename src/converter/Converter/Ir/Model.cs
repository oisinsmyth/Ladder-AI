using Converter.SimaticMl;

namespace Converter.Ir;

public abstract record Expr
{
    private Expr()
    {
    }

    public sealed record TagRef(string Path) : Expr;

    public sealed record And(IReadOnlyList<Expr> Operands) : Expr;

    public sealed record Or(IReadOnlyList<Expr> Operands) : Expr;

    // A normally-closed ("negated") contact — confirmed real, 2026-07-10, via a
    // `<Negated Name="operand" />` child on the source `<Part Name="Contact">`. Wraps a single
    // operand (only ever seen on a TagRef in practice; modeled generally since nothing about the
    // source shape ties negation to any particular operand kind).
    public sealed record Not(Expr Operand) : Expr;

    // A TON's PT fed by a literal time constant (`Access Scope="TypedConstant"`) rather than a
    // tag — confirmed real, 2026-07-11, `FC ControlDelays` (`T#100MS`). Kept distinct from
    // TagRef (not just reused with the literal text as the "path") because it regenerates to a
    // genuinely different XML shape (`<Constant>`, not `<Symbol>`) — the sidecar needs the two
    // told apart, so the model does too. Value is stored verbatim, never interpreted (same
    // discipline as DB StartValue).
    public sealed record TimeLiteral(string Value) : Expr;
}

// A TON instance used in a network — confirmed real, 2026-07-11, both as a multi-instance
// (`FB MotorDOL`, Scope="LocalVariable") and a standalone instance (`FC ControlDelays`,
// Scope="GlobalVariable"). No bound IR-level name (project owner's call): TIA has no "timer
// name" concept beyond the instance reference itself, so inventing one (`timer0`, `timer1`, ...)
// would be exactly the kind of synthetic identifier this project avoids elsewhere. Later
// references to Q/ET reuse the instance's own dotted path as a plain Expr.TagRef (e.g.
// `GeneralEnableDelay.Q`) — deliberately not a separate Expr case: whether the source wired Q
// directly (see ChainStepSidecar.TimerOutputStep) or read it back via an ordinary Access
// elsewhere (FC ControlDelays' actual shape), a reader sees the same, correctly tag-shaped text
// either way, and the sidecar (not the Expr tree) is what drives exact XML regeneration.
//
// InstancePath is the same dotted-path text a plain tag reference would use (AccessNode.DottedPath
// on the sidecar's own Instance) — scope is a sidecar-only concern, never re-derived from the
// text (same principle already applied to ordinary tag Access).
public sealed record TimerBinding(string InstancePath, Expr In, Expr Pt);

public sealed record CoilAssignment(string CoilTag, Expr Condition);

// A network can bundle multiple independent Contact-chain-into-Coil rungs with no shared
// wiring between them — confirmed against a real export, 2026-07-10 (a 16-independent-rung
// alarm-bit network). Assignments is empty for a genuinely empty network (source
// `<NetworkSource />` with no FlgNet content at all, also confirmed real) — both are
// unambiguous, not guesses, so both are modeled directly rather than hard-erroring.
// Timers is a separate list from Assignments (not folded into one statement list) because a
// TimerBinding isn't reduced from a Coil at all — it's its own kind of production, terminating in
// a TON Part rather than a Coil (confirmed real, 2026-07-11: S1 item 7's TON grounding). A
// network can have both, or either alone.
public sealed record IrNetwork(
    int Number,
    string Title,
    IReadOnlyList<CoilAssignment> Assignments,
    IReadOnlyList<TimerBinding>? Timers = null)
{
    public IReadOnlyList<TimerBinding> Timers { get; init; } = Timers ?? Array.Empty<TimerBinding>();

    public bool IsEmpty => Assignments.Count == 0 && Timers.Count == 0;
}

// RootUId: the source block element's own opaque "ID" attribute (required by Import(),
// confirmed real 2026-07-10 — separate from any CompileUnit's own ID). Round-trip-only, like
// NetworkSidecar.CompileUnitUId.
//
// StaticMembers/TempMembers: an FB's own Static/Temp Interface sections — see
// SimaticMl.BlockSource's own doc comment (S1 item 7 Phase B, 2026-07-11) for the full story;
// this is the same data, just living on the IR-facing model instead of the SimaticML-facing one.
public sealed record IrBlock(
    string RootUId,
    string Kind,
    string Name,
    int Number,
    string Language,
    string? Comment,
    IReadOnlyList<IrNetwork> Networks,
    IReadOnlyList<DbMember>? StaticMembers = null,
    IReadOnlyList<DbMember>? TempMembers = null)
{
    public IReadOnlyList<DbMember> TempMembers { get; init; } = TempMembers ?? Array.Empty<DbMember>();
}

/// <summary>
/// Machine-owned round-trip data (ir/SPEC.md "Sidecar") — never hand-edited. Records the exact
/// source UIds this slice's reducer flattened away when it built the readable form, so the
/// serializer can regenerate byte-for-byte-equivalent structure on to-xml. Only valid for
/// networks the reducer actually reduced to pure series chains — see GraphReducer's doc comment.
/// </summary>
// Scope carries the source Access's real scope (GlobalVariable/LocalVariable) so FlgNetBuilder
// can regenerate it exactly — added 2026-07-11 while grounding TON's PT (FC ControlDelays' PT is
// LocalVariable-scoped): before this, every ordinary tag Access was silently rebuilt as
// GlobalVariable regardless of its real source scope, which happened to be harmless for every
// tag seen before now (all GlobalVariable) but is exactly the kind of silent scope drift this
// project's discipline exists to prevent, now that a real LocalVariable-scoped ordinary tag
// reference is grounded.
public sealed record SidecarAccessEntry(string TagPath, int UId, string Scope);

// A TON's PT literal (Access Scope="TypedConstant") — sidecar counterpart of SidecarAccessEntry
// for the constant case. Value is verbatim, same discipline as everywhere else in this format.
public sealed record SidecarConstantEntry(string Value, int UId);

// One position in a chain, rail-to-coil. Confirmed real, 2026-07-10: a position is either a
// single contact, or an OR-merge of several single-contact branches (`Part Name="O"` with a
// `Cardinality` TemplateValue — docs/notes/stage-gates.md, S1 item 7). No branch confirmed real
// is itself a multi-contact chain, so branches are `ContactStep`, not `ChainStepSidecar` — a
// multi-element branch is out of scope (GraphReducer hard-errors rather than guess).
//
// OutgoingWireUId carries one meaning throughout: the wire from *this* step's own "out" port to
// whatever's next. At the top level that's the next chain step (or the coil, for the last step);
// for a step used as an OR-merge branch, it's the wire into the OR's own "inK" port instead —
// same field, same role, so a branch's contact is just a ContactStep like any other.
public abstract record ChainStepSidecar
{
    private ChainStepSidecar()
    {
    }

    public sealed record ContactStep(int ContactUId, int OperandAccessUId, int OperandWireUId, bool Negated, int OutgoingWireUId) : ChainStepSidecar;

    public sealed record OrStep(int OrPartUId, IReadOnlyList<ContactStep> Branches, int OutgoingWireUId) : ChainStepSidecar;

    // A chain position whose boolean value comes directly from an already-defined TON's `Q`
    // output — the source wired `NameCon(tonUId, "Q")` straight into this chain, no Contact, no
    // Access/IdentCon at all. Confirmed real, 2026-07-11, `FC TimerSample` (purpose-built by the
    // project owner specifically to close this out): `Q` feeds a plain `Coil` directly. Always a
    // leaf/terminal like OrStep — nothing further upstream to trace within *this* chain; the
    // TON's own IN/PT definition is a separate TimerBindingSidecar entry, and this step doesn't
    // touch the rail at all (the owning CoilAssignmentSidecar/TimerBindingSidecar's RailWireUId
    // is null when its first step is a TimerOutputStep). "ET" as an upstream leaf is not
    // modeled — no live example — GraphReducer only accepts "Q" here.
    public sealed record TimerOutputStep(int TonPartUId, string Port, int OutgoingWireUId) : ChainStepSidecar;
}

// A TON's PT source — either a tag (AccessUId, resolved the same way a Contact operand is) or a
// literal time constant (ConstantUId, Access Scope="TypedConstant"). Both confirmed real,
// 2026-07-11 (FB MotorDOL / FC ControlDelays respectively).
public abstract record TimerPresetSidecar
{
    private TimerPresetSidecar()
    {
    }

    public sealed record TagPreset(int AccessUId, int WireUId) : TimerPresetSidecar;

    public sealed record LiteralPreset(int ConstantUId, int WireUId) : TimerPresetSidecar;
}

// A wire to an OpenCon endpoint — both UIds must be preserved exactly for regeneration
// (OpenCon carries its own UId, distinct from the wire's), never fabricated.
public sealed record OpenConnectionSidecar(int WireUId, int OpenConUId);

// One TON instance's full round-trip data. RailWireUId/Steps mirror CoilAssignmentSidecar's own
// IN-chain shape exactly (same TraceChain mechanism produces both) — a TON's IN is reduced the
// same way a Coil's condition is, just terminating at the TON's own "IN" port instead of a
// Coil's "operand". Et is null when ET has no wire at all (confirmed real, FC ControlDelays —
// TIA doesn't require every port to have a wire, even an OpenCon one) and non-null when ET is
// wired to OpenCon (confirmed real, FB MotorDOL) — anything else on ET is a hard error in
// GraphReducer, not represented here.
// RailWireUId is nullable — confirmed necessary real, 2026-07-11, `FC TimerSample`: when a
// TON's IN is fed directly by another TON's Q (a TimerOutputStep as steps[0]), the chain never
// touches Powerrail at all, so there's no rail wire to record. Non-null in every other case.
public sealed record TimerBindingSidecar(
    int TonPartUId,
    string Version,
    string TimeType,
    int InstanceUId,
    string InstanceScope,
    IReadOnlyList<string> InstanceComponentPath,
    int? RailWireUId,
    IReadOnlyList<ChainStepSidecar> Steps,
    TimerPresetSidecar Preset,
    OpenConnectionSidecar? Et);

// RailWireUId is separated from the per-step wires because the source wire connecting Powerrail
// to a chain's first element(s) is often shared across MANY independent chains — and, since
// OR-merge support, potentially across multiple branches of the *same* assignment's leading
// OR-merge too — in the same network (one wire, many endpoints — confirmed real, 2026-07-10). It
// is not exclusively "owned" by any one assignment/step the way every other wire is.
//
// CoilOperandAccessUId carries the exact source Access-element UId for the coil operand,
// positionally — confirmed necessary real, 2026-07-10: the same tag path can be referenced by
// two genuinely separate <Access> elements (different UIds) in the same network, so a
// TagPath-keyed lookup at rebuild time is ambiguous/wrong. Each step's own ContactStep carries
// the same for its own operand.
//
// RailWireUId is nullable — confirmed necessary real, 2026-07-11, `FC TimerSample`: a Coil fed
// directly by a TON's Q (a TimerOutputStep as steps[0]) never touches Powerrail, so there's no
// rail wire to record for that assignment. Non-null in every other case.
public sealed record CoilAssignmentSidecar(
    int? RailWireUId,
    IReadOnlyList<ChainStepSidecar> Steps,
    int CoilUId,
    int CoilOperandAccessUId,
    int CoilOperandWireUId);

public sealed record NetworkSidecar(
    int NetworkNumber,
    string CompileUnitUId,
    IReadOnlyList<SidecarAccessEntry> AccessUIds,
    IReadOnlyList<CoilAssignmentSidecar> Assignments,
    IReadOnlyList<SidecarConstantEntry>? ConstantUIds = null,
    IReadOnlyList<TimerBindingSidecar>? Timers = null)
{
    public IReadOnlyList<SidecarConstantEntry> ConstantUIds { get; init; } = ConstantUIds ?? Array.Empty<SidecarConstantEntry>();

    public IReadOnlyList<TimerBindingSidecar> Timers { get; init; } = Timers ?? Array.Empty<TimerBindingSidecar>();
}

public sealed record ReducedNetwork(IrNetwork Network, NetworkSidecar Sidecar);

public sealed class IrFormatException : Exception
{
    public IrFormatException(string message)
        : base(message)
    {
    }
}
