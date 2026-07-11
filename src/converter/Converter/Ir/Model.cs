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

    // A literal operand fed to a TON's `PT` (`Access Scope="TypedConstant"`, e.g. `T#100MS`) or
    // a comparison's `in1`/`in2` (`Access Scope="LiteralConstant"` at the top level, e.g. `1`) —
    // confirmed real, 2026-07-11 (`FB MotorDOL`/`FC ControlDelays`, and `FC ControlDelays` again
    // for the comparison case). One node covers both: at the IR-text level there's no reader-
    // relevant difference (both just render their verbatim value), and the sidecar — not this
    // node — is what carries which of the two source shapes produced it, for exact regeneration.
    // Kept distinct from TagRef (not just reused with the literal text as the "path") because it
    // regenerates to a genuinely different XML shape (`<Constant>`, not `<Symbol>`). Value is
    // stored verbatim, never interpreted (same discipline as DB StartValue).
    public sealed record Literal(string Value) : Expr;

    // A comparison (`Eq`/`Ge` confirmed real, 2026-07-11, `FC ControlDelays`; `Ne`/`Le`/`Gt`/`Lt`
    // unconfirmed — same status as the AND-merge Part Name, not built). Operator is the IR-text
    // infix symbol (`=`, `>=`, ...) already sketched in `ir/SPEC.md`'s readable-form table.
    // Left/Right are ordered (not just an operand set) since `>=`/`<=`/`>`/`<` aren't symmetric.
    public sealed record Compare(string Operator, Expr Left, Expr Right) : Expr;
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

// A gated data assignment (`Part Name="Move"`) — confirmed real, 2026-07-11, `FB MotorDOL`
// ("HMI Motor Status Telemetry": a cascade of Contact->Move taps writing a status code, no Coil
// at all in that network). Structurally unlike everything else built so far: not a boolean chain
// position (Eq/Ge) and not a self-contained production like TON — a Move is a *side effect
// tapped off* a chain position's own output via genuine wire fan-out (the same wire that feeds
// the next chain position also feeds this Move's `en`), while the boolean chain continues
// independently. En is reduced via the exact same TraceChain mechanism as a Coil's condition or
// a TON's IN — nothing new needed there beyond GraphReducer's own fan-out generalization (see
// its doc comment). In is the value written (tag or literal, same tag-or-literal resolver as
// TON's PT/a comparison's operands). DestTag is the plain dotted tag path being written — no
// separate Expr wrapper needed since it's always a bare tag on the write side, never a literal
// or expression (confirmed real: `out1` always wires straight to an ordinary Access).
public sealed record MoveStatement(Expr En, Expr In, string DestTag);

// A network can bundle multiple independent Contact-chain-into-Coil rungs with no shared
// wiring between them — confirmed against a real export, 2026-07-10 (a 16-independent-rung
// alarm-bit network). Assignments is empty for a genuinely empty network (source
// `<NetworkSource />` with no FlgNet content at all, also confirmed real) — both are
// unambiguous, not guesses, so both are modeled directly rather than hard-erroring.
// Timers/Moves are separate lists from Assignments (not folded into one statement list) because
// neither is reduced from a Coil at all — each is its own kind of production, terminating in a
// TON/Move Part rather than a Coil (confirmed real, 2026-07-11: S1 items 7/10's grounding). A
// network can have any combination, or just one kind alone (a Move-only network with no Coil or
// TON at all is real — the whole "HMI Motor Status Telemetry" network is exactly this shape).
public sealed record IrNetwork(
    int Number,
    string Title,
    IReadOnlyList<CoilAssignment> Assignments,
    IReadOnlyList<TimerBinding>? Timers = null,
    IReadOnlyList<MoveStatement>? Moves = null)
{
    public IReadOnlyList<TimerBinding> Timers { get; init; } = Timers ?? Array.Empty<TimerBinding>();

    public IReadOnlyList<MoveStatement> Moves { get; init; } = Moves ?? Array.Empty<MoveStatement>();

    public bool IsEmpty => Assignments.Count == 0 && Timers.Count == 0 && Moves.Count == 0;
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

// A literal operand — sidecar counterpart of SidecarAccessEntry for the constant case. Value is
// verbatim, same discipline as everywhere else in this format. ConstantType is null for
// TypedConstant (TON's PT) and set for LiteralConstant (a comparison operand, e.g. "Int") —
// mirrors ConstantAccessNode's own field, confirmed real 2026-07-11.
public sealed record SidecarConstantEntry(string Value, int UId, string? ConstantType = null);

// One position in a chain, rail-to-coil. A position is either a single contact, a comparison, or
// an OR-merge of several branches (`Part Name="O"` with a `Cardinality` TemplateValue —
// docs/notes/stage-gates.md, S1 item 7). Each OR-merge branch is itself a full chain
// (`OrBranch`, S1 item 11) — real, confirmed 2026-07-11 against both `FC ControlDelays` (`O(41)`
// combining two comparisons, each fed by a further OR-merge rather than Powerrail directly) and
// `FB MotorDOL` (`O(45)`'s branches fed by a shared upstream Contact's own fan-out, not directly
// rail-fed). A branch is resolved exactly like any other chain — same `TraceChain` mechanism as
// a Coil's condition/a TON's `IN`/a Move's `en` — rather than the single-Contact-only,
// rail-required special case this was originally built as (S1 item 7).
//
// OutgoingWireUId carries one meaning throughout: the wire from *this* step's own "out" port to
// whatever's next. At the top level that's the next chain step (or the coil, for the last step);
// for a step used as an OR-merge branch, it's the wire into the OR's own "inK" port instead —
// same field, same role, so a branch's own steps are ordinary ChainStepSidecar entries like any
// other chain's.
public abstract record ChainStepSidecar
{
    private ChainStepSidecar()
    {
    }

    public sealed record ContactStep(int ContactUId, int OperandAccessUId, int OperandWireUId, bool Negated, int OutgoingWireUId) : ChainStepSidecar;

    public sealed record OrStep(int OrPartUId, IReadOnlyList<OrBranch> Branches, int OutgoingWireUId) : ChainStepSidecar;

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

    // A comparison (`Eq`/`Ge`) as an ordinary chain position — confirmed real, 2026-07-11,
    // `FC ControlDelays`. Behaves like ContactStep, not like OrStep/TimerOutputStep: it's a
    // pass-through position, not a terminal — its own rail-facing/continuation port is `pre`
    // (not `in`, genuinely different naming, not a typo), and after resolving its own two
    // operands (Left/Right — order matters, not just a set, since `>=`/`<=`/`>`/`<` aren't
    // symmetric) the backward trace continues from `(ComparePartUId, "pre")` exactly as it would
    // from a Contact's own `in`. PartName is carried (not just Operator) so the exact source Part
    // Name regenerates unambiguously — `Operator` is IR/Expr-facing, `PartName` is XML-facing,
    // kept as two fields rather than deriving one from the other.
    //
    // Composing with an OR-merge (as a branch, or feeding one) is real — `FC ControlDelays`'
    // `O(41)` combines two comparisons — and built, S1 item 11, 2026-07-11: an OR-merge branch is
    // an ordinary chain (`OrBranch`), so a comparison appearing there needs no special case.
    public sealed record CompareStep(
        int ComparePartUId,
        string PartName,
        string SrcType,
        OperandSidecar Left,
        OperandSidecar Right,
        int OutgoingWireUId) : ChainStepSidecar;
}

// One OR-merge branch's full chain — mirrors the (Steps, RailWireUId) shape every other
// production (CoilAssignmentSidecar/TimerBindingSidecar/MoveStatementSidecar) already carries,
// since a branch is resolved via the exact same TraceChain mechanism as any of those. RailWireUId
// is nullable for the same reason theirs is: a branch whose own chain terminates at a TON's `Q`
// (TimerOutputStep) never touches Powerrail — not yet seen live for a branch specifically, but
// the same mechanism, handled identically rather than assumed impossible. Confirmed real,
// 2026-07-11: `FC ControlDelays` (`O(41)`'s branches are comparisons, each fed by a further
// OR-merge rather than Powerrail — Steps.Count > 0, RailWireUId null since the branch's own
// chain terminates at another O, not the rail) and `FB MotorDOL` (`O(45)`'s branches are
// Contacts fed by a shared upstream Contact's own fan-out, not directly rail-fed — Steps.Count
// 1, RailWireUId pointing at the shared rail wire further upstream).
public sealed record OrBranch(IReadOnlyList<ChainStepSidecar> Steps, int? RailWireUId);

// A tag-or-literal operand — used by a TON's `PT` and, since 2026-07-11, a comparison's `in1`/
// `in2` (`FC ControlDelays`). TagOperand resolves the same way a Contact operand does
// (AccessUId); LiteralOperand is a TypedConstant or LiteralConstant (ConstantUId — which kind is
// carried by the referenced SidecarConstantEntry/ConstantAccessNode, not duplicated here). Named
// generically (not "TimerPreset") since it's no longer TON-specific — reusing one shape for both
// is the same design choice already made for TON's own Instance reference reusing AccessNode.
public abstract record OperandSidecar
{
    private OperandSidecar()
    {
    }

    public sealed record TagOperand(int AccessUId, int WireUId) : OperandSidecar;

    public sealed record LiteralOperand(int ConstantUId, int WireUId) : OperandSidecar;
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
    OperandSidecar Preset,
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

// One Move's full round-trip data. RailWireUId/Steps mirror CoilAssignmentSidecar/
// TimerBindingSidecar's own chain shape exactly (same TraceChain mechanism produces all three) —
// a Move's `en` is reduced the same way a Coil's condition or a TON's IN is, just terminating at
// the Move's own `en` port. DestAccessUId/DestWireUId are the `out1` write target, resolved the
// same way an operand is (ResolveOperand — the wire shape is identical, only the read/write
// direction differs semantically, not structurally). DisabledENO/Cardinality aren't carried as
// fields — every real instance seen has `DisabledENO="true"` and `Card="1"`, so the writer always
// regenerates those constants and the parser hard-errors if a source ever disagrees (the same
// "don't carry a field whose value is always the one confirmed constant" reasoning already used
// for TON's `InstanceOfType`).
public sealed record MoveStatementSidecar(
    int MovePartUId,
    int? RailWireUId,
    IReadOnlyList<ChainStepSidecar> Steps,
    OperandSidecar In,
    int DestAccessUId,
    int DestWireUId);

public sealed record NetworkSidecar(
    int NetworkNumber,
    string CompileUnitUId,
    IReadOnlyList<SidecarAccessEntry> AccessUIds,
    IReadOnlyList<CoilAssignmentSidecar> Assignments,
    IReadOnlyList<SidecarConstantEntry>? ConstantUIds = null,
    IReadOnlyList<TimerBindingSidecar>? Timers = null,
    IReadOnlyList<MoveStatementSidecar>? Moves = null)
{
    public IReadOnlyList<SidecarConstantEntry> ConstantUIds { get; init; } = ConstantUIds ?? Array.Empty<SidecarConstantEntry>();

    public IReadOnlyList<TimerBindingSidecar> Timers { get; init; } = Timers ?? Array.Empty<TimerBindingSidecar>();

    public IReadOnlyList<MoveStatementSidecar> Moves { get; init; } = Moves ?? Array.Empty<MoveStatementSidecar>();
}

public sealed record ReducedNetwork(IrNetwork Network, NetworkSidecar Sidecar);

public sealed class IrFormatException : Exception
{
    public IrFormatException(string message)
        : base(message)
    {
    }
}
