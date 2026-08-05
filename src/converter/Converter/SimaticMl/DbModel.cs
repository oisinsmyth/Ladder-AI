namespace Converter.SimaticMl;

// No sidecar needed the way FlgNetwork has one: a DB has no wiring graph, so this parsed model
// already *is* the IR-facing shape too (Converter.Ir.DbIrSerializer/DbIrParser operate on this
// record directly). RootUId is the only round-trip-only field, carried straight through like
// BlockSource.RootUId.
//
// InstanceOfName: the FB this DB instantiates — present only for an Instance DB (source root
// element SW.Blocks.InstanceDB), null for a Global DB (SW.Blocks.GlobalDB). No separate "kind"
// field, same reasoning as DB kind itself (ir/SPEC.md): which one it is is fully implied by
// whether this is set. InstanceOfType is not carried — every real instance DB seen has Type="FB",
// so the writer always regenerates that constant and the parser hard-errors if a source disagrees.
//
// InputMembers/OutputMembers/InOutMembers: confirmed real 2026-07-13, `TomraControlInst1` (an
// Instance DB of `FB TomraControlSystem`, which itself has real Input/Output formal parameters,
// S1 item 20) — an Instance DB persists its own FB's Input/Output parameter storage alongside
// Static, not just Static alone as every Instance DB grounded before this one happened to have
// (every other `PlantAutoControl` dependency FB has no Input/Output params at all). Same shape,
// same nullable/non-null convention, and same shared `DbInterfaceMembers.ParseMember`/
// `WriteMember` helpers as `BlockSource`'s own Input/Output/InOut fields — a DB's own Interface
// has the identical Input/Output/InOut/Static section shape an FB's does, just without
// Temp/Constant/Return (never seen non-empty on a DB). InOut confirmed present-but-always-empty
// on every DB seen (matching BlockSource's own InOut), hence non-nullable empty-default.
public sealed record DbSource(
    string RootUId,
    string Name,
    int Number,
    string? InstanceOfName,
    string? Comment,
    IReadOnlyList<DbMember> Members,
    IReadOnlyList<DbMember>? InputMembers = null,
    IReadOnlyList<DbMember>? OutputMembers = null,
    IReadOnlyList<DbMember>? InOutMembers = null)
{
    public IReadOnlyList<DbMember> InOutMembers { get; init; } = InOutMembers ?? Array.Empty<DbMember>();
}

// Datatype is carried verbatim as the source XML shows it (e.g. "Word", "Array[0..14] of Bool")
// — not re-parsed into a separate array/type grammar. Confirmed real, 2026-07-10: every scalar
// and array-of-scalar member's own type string round-trips exactly as an opaque string; this
// slice never needs to understand it structurally.
//
// Version: only present when the source member itself carries a Version attribute (confirmed
// real on TON_TIME-family structured members, 2026-07-11) — captured generically whenever present
// rather than tied to a specific Datatype, since nothing confirms it's exclusive to timers.
//
// SetPoint: captured verbatim, not assumed. Originally treated as a fixed default (false for a
// scalar member, true for a structured one, both confirmed against the first real examples of
// each) until a real counterexample disproved it, 2026-07-11: `ConveyorMotor1`'s `RecentStartSignal`
// (a `TOF_TIME` structured member) has SetPoint=false. It genuinely varies per member — not a
// derivable default — so it's captured and regenerated exactly, same discipline as StartValue.
// Meaningless (never present in the source) on a nested member — always false there, unused.
//
// NestedMembers: populated only for a structured member (UDT-typed or system-function-block
// instance-typed, e.g. TON_TIME) — the DB source XML inlines the member's own sub-members one
// level deep (confirmed real 2026-07-11, ir/SPEC.md "Structured members"), reused recursively as
// DbMember since a nested member is structurally the same shape, just narrower in practice (no
// Retain/Version/further nesting has ever been observed on one — DbSourceParser hard-errors
// rather than silently accept an unconfirmed nested shape). Null for a plain scalar/array member.
//
// Informative/InformativeComment: confirmed real 2026-07-14, `OB1 Main`'s own system-defined
// Input parameters (`Initial_Call`/`Remanence`) — the same bare (no Remanence attribute, no
// AttributeList) shape as IsBareParameter, plus `Informative="true"` and a
// `<Comment><MultiLanguageText Lang="en-US">...</MultiLanguageText></Comment>` child. TIA's own
// Import() requires OB system parameters specifically to carry this ("OB system parameters must
// be informative") — never seen on an ordinary bare FC/FB parameter, hence its own pair of fields
// rather than folding into IsBareParameter's existing shape.
//
// IsBareParameter: confirmed real 2026-07-14 (`FC Scale`'s own Input/Output params, a small
// project utility FC) — a genuinely minimal Input/Output/InOut member shape with no `Remanence`
// attribute and no `<AttributeList>` at all (distinct from the ordinary Input/Output shape,
// `FB TomraControlSystem`, which has both, just missing `SetPoint`). Needed only to round-trip the
// writer's own shape choice — Retain/SetPoint alone can't distinguish "genuinely bare" from "an
// ordinary member that happens to have Retain=false, SetPoint=false".
//
// ExternalAccessible/ExternalVisible/ExternalWritable: default true, matching every member seen
// until a real counterexample — `FB VSDSim`'s own `SpeedCalcArray` has `ExternalAccessible=false`
// (confirmed real 2026-07-14) — disproved the earlier assumption that all three are always true
// (`DbInterfaceMembers.RequireDefaultBooleanAttributes` used to hard-error on any of them being
// false, per `GlobalDbWithNonDefaultAttribute.xml`'s own deliberately-synthetic test case, built
// before a real example existed). Same "capture and regenerate exactly" discipline as `SetPoint`
// — not derivable from the member's own kind/shape, genuinely varies per member.
//
// Live-verified constraint, 2026-07-14 (not enforced by this converter — TIA's own Import() is
// the check): `ExternalAccessible=false` requires `ExternalVisible`/`ExternalWritable` to *also*
// be false — `ExternalAccessible=false` alone with the other two left `true` was tried first and
// rejected ("The attribute 'ExternalVisible' cannot be set"). `ExternalWritable=false` alone
// (Accessible/Visible left `true` — `GlobalDbWithNonDefaultAttribute.xml`'s own shape, a common
// "externally readable but not writable" pattern) *is* independently valid — confirmed separately.
// So `ExternalAccessible` gates the other two; they don't gate each other or it.
// Comment: a member's own "Comment" column in the TIA interface editor — genuinely distinct from
// InformativeComment below (that one is OB-bare-system-parameter-only machinery, gated by
// Informative). Confirmed real 2026-07-15 on an *ordinary* (non-bare, non-Informative) Static
// member — grounded by reusing the exact <Comment><MultiLanguageText Lang="en-US">...</
// MultiLanguageText></Comment> shape Informative's own handling already proved real (OB1 Main,
// 2026-07-14), tried on a plain member, and confirmed by a live TIA import + export round-trip
// (docs/notes/openness-quirks.md has no entry for this — it worked on the first real attempt, no
// quirk to record). Not yet extended to a structured member's own *nested* fields (ParseBareMember/
// WriteBareMember's shape, one level deeper — a UDT-typed member's own inner fields, or a timer
// instance's own PT/ET/IN/Q) — only top-level Static/Input/Output/Temp/DB members carry this so far.
//
// Field-count watch, resolved 2026-08-05 (audit F-51): 14 fields, UNCHANGED since 2026-07-20 — the
// same finding, and the same disposition, as PartNode in Model.cs (see the note there for the
// reasoning). REVISIT TRIGGER: this record passing 16 fields.
public sealed record DbMember(
    string Name,
    string Datatype,
    bool Retain,
    string? StartValue,
    string? Version = null,
    bool SetPoint = false,
    IReadOnlyList<DbMember>? NestedMembers = null,
    bool IsBareParameter = false,
    bool Informative = false,
    string? InformativeComment = null,
    bool ExternalAccessible = true,
    bool ExternalVisible = true,
    bool ExternalWritable = true,
    string? Comment = null);
