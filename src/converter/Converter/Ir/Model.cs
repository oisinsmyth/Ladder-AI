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

// The EN source for an en-gated production — deliberately NOT folded into Expr. Every production
// built before S1 item 18 (TON/Move/WAND/CALL) has its own `en`/`IN` resolved as an ordinary
// boolean condition tree (TraceChain, terminating at Powerrail or a Contact/comparison chain).
// Confirmed real, 2026-07-12 (S1 item 18, `Mul`->`Convert` pairs in `MotorDOL`/`EquipmentControlSystem`): a
// second, genuinely different source exists — the *immediately preceding* Mul/Convert's own
// `eno` output wired straight into this one's `en`, despite both carrying `DisabledENO="true"`.
// There's no tag to reference "the preceding instruction's own success" by (Mul/Convert have no
// `Instance` element, unlike TON/CALL's own named instance path) — so this is modeled as a
// second EnSource case, not a new Expr leaf, keeping Expr a pure boolean-tag-condition tree.
// Mirrors the existing "TRUE" sentinel precedent (a reserved value in the EN slot) rather than
// inventing a full expression node: readable-form renders `EN := ENO` for the chained case.
public abstract record EnSource
{
    private EnSource()
    {
    }

    public sealed record Condition(Expr Value) : EnSource;

    public sealed record PrecedingEno : EnSource;
}

// TON (on-delay) vs TONR (retentive on-delay) vs TOF (off-delay) — confirmed real, 2026-07-12
// (TONR: S1 item 19, two independent instances, `FB MotorDOL`/`FilterUnitSystem`; TOF: S1 item 23,
// `FB AirStar`). TONR is TON plus one extra port (`R`, reset). TOF is structurally identical to
// TON — same `Version`/`Instance`/`time_type` shape, same `IN`/`PT`/`ET` ports, no reset port, no
// EN/ENO on any of the three — confirmed real, no live example of TOF's own `Q` being consumed
// (neither Access-based nor direct-wire) in the one grounded instance, same "unconfirmed, not
// needed for this network to reduce" status TON's own `ET`-consumed case has. Mirrors the
// CoilKind precedent (Coil/SCoil/RCoil, S1 item 15): the IR doesn't compute
// on-delay-vs-retentive-vs-off-delay runtime semantics, just records which Part Name to
// regenerate.
public enum TimerKind
{
    Ton,
    Tonr,
    Tof,
}

// A TON/TONR instance used in a network — confirmed real, 2026-07-11 (TON) and 2026-07-12
// (TONR), both as a multi-instance (`FB MotorDOL`, Scope="LocalVariable") and a standalone
// instance (`FC ControlDelays`, Scope="GlobalVariable"). No bound IR-level name (project owner's
// call): TIA has no "timer name" concept beyond the instance reference itself, so inventing one
// (`timer0`, `timer1`, ...) would be exactly the kind of synthetic identifier this project avoids
// elsewhere. Later references to Q/ET reuse the instance's own dotted path as a plain
// Expr.TagRef (e.g. `GeneralEnableDelay.Q`) — deliberately not a separate Expr case: whether the
// source wired Q directly (see ChainStepSidecar.TimerOutputStep) or read it back via an ordinary
// Access elsewhere (FC ControlDelays' actual shape), a reader sees the same, correctly tag-shaped
// text either way, and the sidecar (not the Expr tree) is what drives exact XML regeneration.
//
// InstancePath is the same dotted-path text a plain tag reference would use (AccessNode.DottedPath
// on the sidecar's own Instance) — scope is a sidecar-only concern, never re-derived from the
// text (same principle already applied to ordinary tag Access).
//
// Reset is TONR's own `R` port — confirmed real, 2026-07-12: fed directly by a plain tag
// IdentCon in both grounded instances, no chain (same shape as PT, not a TraceChain-based
// condition). Null/absent for a plain TON; required for TONR.
public sealed record TimerBinding(string InstancePath, Expr In, Expr Pt, TimerKind Kind = TimerKind.Ton, Expr? Reset = null);

// Assign (`Part Name="Coil"`) writes the condition directly; Set/Reset (`Part Name="SCoil"`/
// `"RCoil"`) only ever move the target one direction — true when the condition is true, left
// unchanged when it's false — confirmed real, 2026-07-12, `FC PlantAutoControl` (3 of each, two
// independent instances of each grounded before any code). Structurally SCoil/RCoil are
// IDENTICAL to a plain Coil: same bare `<Part Name="..." UId="N" />`, same "in"/"operand" ports,
// never a producer (no "out") — GraphReducer/FlgNetBuilder reuse the exact same chain-resolution
// code (ReduceOneChain/BuildOneChain) for all three, differing only in this one tag. Kind is
// IR-facing (drives which keyword IrSerializer emits); the exact source Part Name for regen is
// derived from Kind in FlgNetBuilder rather than duplicated on CoilAssignmentSidecar, since
// BuildOneChain already takes the model CoilAssignment alongside its sidecar (for the existing
// leaf-count cross-check) — no other production's Build* method needs this, so no new "sidecar
// must be fully self-sufficient" violation. Keyword choice (`SCOIL`/`RCOIL`, not `SET`/`RESET`)
// matches every other IR keyword built so far mirroring its own source Part Name (COIL/TON/
// MOVE/CALL) — WAND is the one deliberate exception, for a naming collision that doesn't apply
// here.
public enum CoilKind
{
    Assign,
    Set,
    Reset,
}

public sealed record CoilAssignment(string CoilTag, Expr Condition, CoilKind Kind = CoilKind.Assign);

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

// A bitwise/word-level AND box instruction (`Part Name="And"`) — confirmed real, 2026-07-12,
// `FB VSDUpdateComs` (`Word AND 16#89 -> ControlWord`). Genuinely NOT the boolean parallel-branch
// "AND-merge" `ir/SPEC.md` originally sketched (that shape was never found real — a systematic
// sweep of 28 real LAD blocks turned up no boolean AND-merge Part at all; architecturally this
// makes sense, since boolean AND in ladder logic is always plain series Contacts, never needing
// an explicit merge Part the way OR genuinely does). Structurally this is closest to `Move`: a
// side effect gated by `en` (fan-out-tapped off a chain, same `TraceChain` mechanism), reading N
// input operands (`Cardinality`-driven — real example has `Card="2"`, mirroring `O`'s own
// Cardinality-driven branch count — one shared resolver, `ResolveTagOrLiteralOperand`, called
// once per `inK`) and writing one destination tag via `out`. `SrcType` (`Word`, confirmed real —
// same `TemplateValue` shape as a comparison's own `SrcType`) is carried since, unlike Move, this
// instruction is genuinely typed (a Word-width bitwise operation, not an untyped value copy). IR
// keyword is `WAND` (Word AND), not `AND` — deliberately avoiding a collision with the existing
// boolean `AND` infix operator; a converter-owned vendor-neutral name mapping, same precedent as
// `MOVE_BLK_VARIANT` → `MOVE`.
public sealed record WordAndStatement(Expr En, IReadOnlyList<Expr> Inputs, string DestTag);

// Mul (multiply) vs Add — confirmed real, 2026-07-12 (S1 item 19), `FB MotorDOL`/`FilterUnitSystem`:
// `Add`'s own XML shape is identical to `Mul`'s (`DisabledENO="true"`, `Card="2"`,
// `<AutomaticTyped Name="SrcType" />`) — same "IR doesn't compute runtime semantics" reasoning as
// CoilKind/TimerKind, just a different Part Name to regenerate.
public enum MulKind
{
    Multiply,
    Add,
}

// A multiply/add box instruction (`Part Name="Mul"`/`"Add"`) — confirmed real, 2026-07-12 (S1
// items 18/19), `FB MotorDOL`/`FB EquipmentControlSystem`/`FilterUnitSystem` (all grounded independently, identical
// shape for both Kinds). Structurally closest to WAND: `en`-gated (via EnSource, see its own doc
// comment — sometimes an ordinary condition, sometimes chained from a preceding Mul/Convert's own
// `eno`; an `Add`'s own `en` fed by a comparison's `out`, e.g. `Lt`, is just the ordinary
// TraceChain-resolved Condition case, confirmed real, no new EnSource variant needed),
// `Cardinality`-driven input list (`Card="2"` in every real instance seen, carried as data rather
// than hard-validated fixed — same "only one value observed, not enough to treat as universal"
// reasoning as WAND's own Cardinality, not Move's fixed `Card="1"`), one destination tag via
// `out`. Genuinely untyped in the source (`<AutomaticTyped Name="SrcType" />`, not a
// `TemplateValue` — TIA infers the type from the connected operands rather than declaring it
// statically) — nothing to carry for it beyond validating the shape is present, unlike WAND's own
// explicit `SrcType`.
public sealed record MulStatement(EnSource En, IReadOnlyList<Expr> Inputs, string DestTag, MulKind Kind = MulKind.Multiply);

// A type-conversion box instruction (`Part Name="Convert"`) — confirmed real, 2026-07-12 (S1 item
// 18). Structurally closest to Move: `en`-gated (via EnSource), a single tag-or-literal input,
// one destination tag via `out`. Genuinely typed *between* two types (`SrcType`/`DestType`, e.g.
// `Real`->`DInt` — confirmed real, both `TemplateValue`s present, unlike `Mul`'s own untyped
// shape) — both carried sidecar-only, same precedent as a comparison's own `SrcType`. Confirmed
// real both standalone (independently rail-fed `en`, `FB ShredderControlSystem`) and as the second half
// of a `Mul`->`Convert` ENO-chained pair (`FB MotorDOL`/`EquipmentControlSystem`) — both cases reduce
// identically once `EnSource` is resolved, no special-casing needed beyond that one field.
public sealed record ConvertStatement(EnSource En, Expr In, string DestTag);

// One bound argument at a Call site — only wired parameters ever appear at all (confirmed real,
// 2026-07-12: 19 of 20 real <Call> instances in FC PlantAutoControl have zero; the one wired example,
// TomraControlSystem, has 8 InputArgs + 2 OutputArgs, in source declaration order). InputArg's Value
// uses the same tag-or-literal resolver as everywhere else (ResolveTagOrLiteralOperand);
// OutputArg's DestTag is a bare write target, same as Move's out1/WAND's dest — never a literal
// or expression in the one real instance seen. Section (Input/Output) is encoded by which
// subtype this is, not a separate field — mirrors how Contact's Negated regenerates
// <Negated Name="operand"/> without storing the XML shape verbatim.
public abstract record CallArgument
{
    private CallArgument()
    {
    }

    public sealed record InputArg(string ParamName, Expr Value) : CallArgument;

    public sealed record OutputArg(string ParamName, string DestTag) : CallArgument;
}

// An FB/FC call (`<Call>`/`<CallInfo Name="<callee>" BlockType="FB"/"FC">`) — confirmed real,
// 2026-07-12, `FC PlantAutoControl` (20 real FB-call instances, 8 distinct callees, S1 item 14) and
// `FB MotorVSDSystem` (1 real FC-call instance, S1 item 24 — `Scale`, Siemens' own standard-library
// function). Genuinely different from every other production built so far in one respect:
// `<Call>` isn't even a `<Part>` in the source XML (see SimaticMl.PartNode's own doc comment) —
// but once adapted into an ordinary PartNode(Name="Call") by the parser, it reduces as its own
// top-level production exactly like TON/Move/WAND (not like Not, which is a chain-position
// discovered incidentally): a call is invoked directly, not merely traced through. En is reduced
// via the exact same TraceChain fan-out mechanism as Move/WAND's own en (confirmed real: all 21
// real instances across both blocks are directly rail-fed, reducing to the existing "wired
// directly to rail" TRUE sentinel — Expr.And with zero operands — same as WAND's own live-verified
// case; a Contact-gated en on a Call specifically remains unconfirmed, low-risk by analogy).
// InstancePath is the same dotted-path text a plain tag reference or TON's own instance would use
// (mirrors TimerBinding.InstancePath exactly — same AccessNode-shaped reference, confirmed
// identical shape to TON's own <Instance>) when present — **nullable**, confirmed real 2026-07-12
// (S1 item 24): an FC call's own `<CallInfo>` genuinely has no `<Instance>` element at all (FCs
// are stateless, no instance DB), unlike every FB call grounded so far. BlockType is deliberately
// NOT carried here (sidecar-only, same precedent as a comparison's/WAND's own SrcType — not shown
// in the readable IR text, since the callee's own .ir file is ADR-0001's source of truth for its
// interface, not duplicated here) — so the readable form can't derive FB-vs-FC from BlockType
// directly; instance presence/absence is the only signal it needs anyway.
public sealed record CallStatement(string BlockName, string? InstancePath, Expr En, IReadOnlyList<CallArgument> Arguments);

// A network can bundle multiple independent Contact-chain-into-Coil rungs with no shared
// wiring between them — confirmed against a real export, 2026-07-10 (a 16-independent-rung
// alarm-bit network). Assignments is empty for a genuinely empty network (source
// `<NetworkSource />` with no FlgNet content at all, also confirmed real) — both are
// unambiguous, not guesses, so both are modeled directly rather than hard-erroring.
// Timers/Moves/WordAnds are separate lists from Assignments (not folded into one statement list)
// because none is reduced from a Coil at all — each is its own kind of production, terminating in
// a TON/Move/And Part rather than a Coil (confirmed real, 2026-07-11/12: S1 items 7/10/12's
// grounding). A network can have any combination, or just one kind alone (a Move-only network
// with no Coil or TON at all is real — the whole "HMI Motor Status Telemetry" network is exactly
// this shape).
// Title is the network's own real, human-visible label (source `MultilingualText
// [CompositionName="Title"]`) — confirmed real and populated on every network of a real block
// (S1 item 16, 2026-07-12, `FC PlantAutoControl`), unlike Comment (below), which has been empty on
// every real network seen all session. Comment is a separate, optional field (its own
// `COMMENT "..."` line in the readable form, mirroring the block-level COMMENT line) — genuinely
// distinct source content, not folded into Title the way this converter's earlier build
// accidentally did.
public sealed record IrNetwork(
    int Number,
    string Title,
    IReadOnlyList<CoilAssignment> Assignments,
    IReadOnlyList<TimerBinding>? Timers = null,
    IReadOnlyList<MoveStatement>? Moves = null,
    IReadOnlyList<WordAndStatement>? WordAnds = null,
    IReadOnlyList<CallStatement>? Calls = null,
    string? Comment = null,
    IReadOnlyList<MulStatement>? Muls = null,
    IReadOnlyList<ConvertStatement>? Converts = null)
{
    public IReadOnlyList<TimerBinding> Timers { get; init; } = Timers ?? Array.Empty<TimerBinding>();

    public IReadOnlyList<MoveStatement> Moves { get; init; } = Moves ?? Array.Empty<MoveStatement>();

    public IReadOnlyList<WordAndStatement> WordAnds { get; init; } = WordAnds ?? Array.Empty<WordAndStatement>();

    public IReadOnlyList<CallStatement> Calls { get; init; } = Calls ?? Array.Empty<CallStatement>();

    public IReadOnlyList<MulStatement> Muls { get; init; } = Muls ?? Array.Empty<MulStatement>();

    public IReadOnlyList<ConvertStatement> Converts { get; init; } = Converts ?? Array.Empty<ConvertStatement>();

    public bool IsEmpty => Assignments.Count == 0 && Timers.Count == 0 && Moves.Count == 0 && WordAnds.Count == 0
        && Calls.Count == 0 && Muls.Count == 0 && Converts.Count == 0;
}

// RootUId: the source block element's own opaque "ID" attribute (required by Import(),
// confirmed real 2026-07-10 — separate from any CompileUnit's own ID). Round-trip-only, like
// NetworkSidecar.CompileUnitUId.
//
// StaticMembers/TempMembers: an FB's own Static/Temp Interface sections — see
// SimaticMl.BlockSource's own doc comment (S1 item 7 Phase B, 2026-07-11) for the full story;
// this is the same data, just living on the IR-facing model instead of the SimaticML-facing one.
// Title: a block-level MultilingualText[CompositionName="Title"] — confirmed real, 2026-07-12
// (S1 item 17, two of FC PlantAutoControl's own dependency FBs, `MotorVSDSystem`/`AirStar`, both titled
// "VSD Motor" — a shared, templated title across that FB family). Its own optional `TITLE "..."`
// line in the readable form (mirroring the existing `COMMENT "..."` line) — genuinely a new
// line, not a repurposed one, since the BLOCK line's own quoted text is the block's real Name,
// unlike a NETWORK line's label which Title could repurpose (S1 item 16).
public sealed record IrBlock(
    string RootUId,
    string Kind,
    string Name,
    int Number,
    string Language,
    string? Comment,
    IReadOnlyList<IrNetwork> Networks,
    IReadOnlyList<DbMember>? StaticMembers = null,
    IReadOnlyList<DbMember>? TempMembers = null,
    string? Title = null,
    IReadOnlyList<DbMember>? InputMembers = null,
    IReadOnlyList<DbMember>? OutputMembers = null,
    IReadOnlyList<DbMember>? InOutMembers = null,
    IReadOnlyList<DbMember>? ConstantMembers = null)
{
    public IReadOnlyList<DbMember> TempMembers { get; init; } = TempMembers ?? Array.Empty<DbMember>();

    public IReadOnlyList<DbMember> InOutMembers { get; init; } = InOutMembers ?? Array.Empty<DbMember>();
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

    // A standalone boolean inverter (`Part Name="Not"`) — confirmed real, 2026-07-12, `FC
    // PlantAutoControl` (found while investigating whether that block round-trips; genuinely different
    // from a Contact's own `<Negated Name="operand" />`, which negates a *tag read*, not a chain
    // position). Structurally the simplest chain position possible: a single `in`/`out` port pair
    // (same names as Contact's own), no operand/Access lookup at all — it just inverts whatever
    // boolean value arrives on `in`. Unlike ContactStep/CompareStep (flat pass-through positions
    // that extend the *same* AND-chain), a Not must wrap only its own upstream in `Expr.Not`, not
    // the whole rest of the chain — so its own `in` is resolved via a fully self-contained,
    // recursive `TraceChain` call (exactly like an OR-merge branch's own resolution), and `Steps`/
    // `RailWireUId` here mirror `OrBranch`'s own nested `(Steps, RailWireUId)` shape for that
    // reason. Always chain-terminal like `OrStep` — the recursive call already resolves everything
    // upstream, so nothing further to trace at the position where the `Not` itself was found.
    // Confirmed real (two independent instances, `FC PlantAutoControl`): a Not's own `in` is commonly
    // fed via genuine wire fan-out (an upstream Part's output feeds both a separately-continuing
    // chain *and* the Not) — the same fan-out-tap mechanism already proven for Move/OR-merge
    // branches, here feeding back into a boolean chain instead of terminating in a side-effect
    // write. No new IR-text grammar needed: `Expr.Not`/`NOT <expr>` already exist (S1 item 7,
    // negated contacts) and already render/parse with correct precedence (S1 item 11).
    public sealed record NotStep(int NotPartUId, IReadOnlyList<ChainStepSidecar> Steps, int? RailWireUId, int OutgoingWireUId) : ChainStepSidecar;
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
//
// Kind is duplicated here (also on TimerBinding, the model) rather than derived from it —
// FlgNetBuilder.BuildTimer works entirely off the sidecar, never cross-referencing the model
// (same discipline as CallStatementSidecar's own BlockName; unlike CoilAssignmentSidecar's one
// documented exception, BuildTimer was never sidecar-only-adjacent to begin with). Reset is
// TONR's own `R` port (OperandSidecar-shaped, same as Preset) — null for a plain TON, non-null
// for TONR (confirmed real, 2026-07-12, S1 item 19: always tag-fed in both grounded instances,
// but modeled as the same generic tag-or-literal OperandSidecar PT already uses, not narrowed to
// tag-only).
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
    OpenConnectionSidecar? Et,
    TimerKind Kind = TimerKind.Ton,
    OperandSidecar? Reset = null);

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
//
// No CoilKind/PartName field here (S1 item 15, SCoil/RCoil) — deliberately not duplicated on the
// sidecar the way BlockName is on CallStatementSidecar, since FlgNetBuilder.BuildOneChain already
// takes the model CoilAssignment alongside this sidecar (for the pre-existing leaf-count
// cross-check), so it derives the exact source Part Name ("Coil"/"SCoil"/"RCoil") from
// CoilAssignment.Kind directly rather than re-storing it.
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

// One bitwise-And's full round-trip data. RailWireUId/Steps mirror every other production's own
// chain shape exactly (same TraceChain mechanism). Inputs is positional (Inputs[0] is `in1`,
// Inputs[1] is `in2`, ...) — length equals the source Part's own Cardinality, validated at parse
// time. SrcType/DestAccessUId/DestWireUId mirror a comparison's SrcType and Move's own
// DestAccessUId/DestWireUId respectively. DisabledENO isn't carried as a field — every real
// instance seen has `DisabledENO="true"`, same "don't carry a confirmed constant" reasoning as
// Move's own DisabledENO/TON's InstanceOfType (Cardinality itself IS carried, via Inputs.Count,
// since — unlike Move's fixed Card="1" — only one real example (Card="2") has been seen, not
// enough to treat any particular value as a universal constant).
public sealed record WordAndStatementSidecar(
    int AndPartUId,
    int? RailWireUId,
    IReadOnlyList<ChainStepSidecar> Steps,
    IReadOnlyList<OperandSidecar> Inputs,
    string SrcType,
    int DestAccessUId,
    int DestWireUId);

// One Call argument's full round-trip data — parallels CallArgument (Model) 1:1, positionally
// matched within CallStatementSidecar.Arguments, same pairing pattern as
// WordAndStatement.Inputs/WordAndStatementSidecar.Inputs. Carries ParamName again (not just
// derivable by index from the model side) because FlgNetBuilder works entirely off the sidecar,
// never cross-referencing the model's own Arguments list (same discipline as every other
// sidecar/model pairing in this codebase — see MoveStatementSidecar's own doc comment). Type
// mirrors the source's own <Parameter Type="..."> attribute (sidecar-only, not shown in the
// readable IR text, same precedent as WAND's SrcType).
public abstract record CallArgumentSidecar
{
    private CallArgumentSidecar()
    {
    }

    public sealed record InputArgSidecar(string ParamName, string Type, OperandSidecar Value) : CallArgumentSidecar;

    public sealed record OutputArgSidecar(string ParamName, string Type, int DestAccessUId, int DestWireUId) : CallArgumentSidecar;
}

// One Call's full round-trip data. RailWireUId/Steps mirror every other production's own en/IN
// chain shape exactly (same TraceChain mechanism). BlockName is repeated here (also on
// CallStatement, the model) rather than only on the model side, because FlgNetBuilder works
// entirely off the sidecar, never cross-referencing the model — same discipline as every other
// sidecar/model pairing in this codebase (see CallArgumentSidecar's own doc comment for the same
// reasoning re: ParamName). BlockType mirrors a comparison's/WAND's own SrcType (sidecar-only,
// carried verbatim rather than hard-validated to a constant) — "FB" and, confirmed real 2026-07-12
// (S1 item 24), "FC" both seen now.
//
// Instance fields mirror TimerBindingSidecar's own (InstanceUId/InstanceScope/
// InstanceComponentPath) — same AccessNode-shaped reference — **all three nullable together**
// (confirmed real, S1 item 24: an FC call, `FB MotorVSDSystem`'s own `Scale` call, has no `<Instance>`
// at all; every real FB call still carries one). A plain 3-nullable-fields group rather than a
// discriminated union (unlike EnSource's own Condition/PrecedingEno split): there's no second
// "kind" of instance to distinguish here, only presence-or-absence, so a union would be pure
// ceremony over what's really one optional group. All three are null together or non-null
// together — never partially populated, since they only ever come from parsing one real
// `<Instance>` element as a whole.
//
// Arguments is positional, in the source's own <Parameter> declaration order (confirmed real: 8
// inputs then 2 outputs in the one real FB-call wired example; 5 inputs then 1 output in the one
// real FC-call example).
public sealed record CallStatementSidecar(
    int CallPartUId,
    string BlockName,
    string BlockType,
    int? RailWireUId,
    IReadOnlyList<ChainStepSidecar> Steps,
    int? InstanceUId,
    string? InstanceScope,
    IReadOnlyList<string>? InstanceComponentPath,
    IReadOnlyList<CallArgumentSidecar> Arguments);

// The sidecar counterpart of EnSource (Model) — see its own doc comment for why this is a
// separate concept from an ordinary chain's RailWireUId/Steps. ConditionSidecar carries exactly
// what every other production's own en/IN chain does (RailWireUId/Steps, same TraceChain
// mechanism). PrecedingEnoSidecar carries the exact source UId being chained from (confirmed
// real, 2026-07-12, S1 item 18: a Mul/Convert's own `eno` wired straight into the next one's
// `en`) plus the wire UId connecting them, both needed for exact regeneration — FlgNetBuilder
// works entirely off the sidecar, never cross-referencing the model.
public abstract record EnSourceSidecar
{
    private EnSourceSidecar()
    {
    }

    public sealed record ConditionSidecar(int? RailWireUId, IReadOnlyList<ChainStepSidecar> Steps) : EnSourceSidecar;

    public sealed record PrecedingEnoSidecar(int PrecedingPartUId, int WireUId) : EnSourceSidecar;
}

// One Mul/Add's full round-trip data. En mirrors every other production's own EnSource (see its
// own doc comment). Inputs is positional (Inputs[0] is `in1`, Inputs[1] is `in2`, ...) — length
// equals the source Part's own Cardinality (`Card="2"` in every real instance seen, carried as
// data rather than hard-validated fixed, same reasoning as WordAndStatementSidecar's own
// Inputs.Count). DisabledENO isn't carried — always `"true"`, same "don't carry a confirmed
// constant" reasoning as Move/WAND's own. Kind is duplicated here (also on MulStatement, the
// model) rather than derived from it — same "sidecar works entirely on its own" discipline as
// TimerBindingSidecar's own Kind field, since BuildMul is sidecar-only.
//
// SrcType is nullable — confirmed real, 2026-07-12 (S1 item 20 live verification, `FB AirStar`,
// `Mul UId=43`): a genuine second real shape for this Part's own type, contradicting S1 item 18's
// own original "always AutomaticTyped" assumption (`MotorDOL`/`EquipmentControlSystem`). Null means
// `<AutomaticTyped Name="SrcType" />` (no value, TIA infers the type from the connected
// operands — the original, still-real shape); non-null means an ordinary `<TemplateValue
// Name="SrcType" Type="Type">X</TemplateValue>` (e.g. `"Real"`), the same explicit shape
// `Convert`'s own SrcType/DestType already use. Exactly one of the two shapes is present in any
// real instance seen — never both, never neither.
public sealed record MulStatementSidecar(
    int MulPartUId,
    EnSourceSidecar En,
    IReadOnlyList<OperandSidecar> Inputs,
    int DestAccessUId,
    int DestWireUId,
    MulKind Kind = MulKind.Multiply,
    string? SrcType = null);

// One Convert's full round-trip data. En mirrors every other production's own EnSource.
// SrcType/DestType mirror a comparison's own SrcType (sidecar-only, not shown in the readable IR
// text) — confirmed real, both present as ordinary TemplateValues (unlike Mul's own untyped
// shape), e.g. `Real`->`DInt`.
public sealed record ConvertStatementSidecar(
    int ConvertPartUId,
    EnSourceSidecar En,
    OperandSidecar In,
    string SrcType,
    string DestType,
    int DestAccessUId,
    int DestWireUId);

public sealed record NetworkSidecar(
    int NetworkNumber,
    string CompileUnitUId,
    IReadOnlyList<SidecarAccessEntry> AccessUIds,
    IReadOnlyList<CoilAssignmentSidecar> Assignments,
    IReadOnlyList<SidecarConstantEntry>? ConstantUIds = null,
    IReadOnlyList<TimerBindingSidecar>? Timers = null,
    IReadOnlyList<MoveStatementSidecar>? Moves = null,
    IReadOnlyList<WordAndStatementSidecar>? WordAnds = null,
    IReadOnlyList<CallStatementSidecar>? Calls = null,
    IReadOnlyList<MulStatementSidecar>? Muls = null,
    IReadOnlyList<ConvertStatementSidecar>? Converts = null)
{
    public IReadOnlyList<SidecarConstantEntry> ConstantUIds { get; init; } = ConstantUIds ?? Array.Empty<SidecarConstantEntry>();

    public IReadOnlyList<TimerBindingSidecar> Timers { get; init; } = Timers ?? Array.Empty<TimerBindingSidecar>();

    public IReadOnlyList<MoveStatementSidecar> Moves { get; init; } = Moves ?? Array.Empty<MoveStatementSidecar>();

    public IReadOnlyList<WordAndStatementSidecar> WordAnds { get; init; } = WordAnds ?? Array.Empty<WordAndStatementSidecar>();

    public IReadOnlyList<CallStatementSidecar> Calls { get; init; } = Calls ?? Array.Empty<CallStatementSidecar>();

    public IReadOnlyList<MulStatementSidecar> Muls { get; init; } = Muls ?? Array.Empty<MulStatementSidecar>();

    public IReadOnlyList<ConvertStatementSidecar> Converts { get; init; } = Converts ?? Array.Empty<ConvertStatementSidecar>();
}

public sealed record ReducedNetwork(IrNetwork Network, NetworkSidecar Sidecar);

public sealed class IrFormatException : Exception
{
    public IrFormatException(string message)
        : base(message)
    {
    }
}
