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
public sealed record DbSource(
    string RootUId,
    string Name,
    int Number,
    string? InstanceOfName,
    string? Comment,
    IReadOnlyList<DbMember> Members);

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
public sealed record DbMember(
    string Name,
    string Datatype,
    bool Retain,
    string? StartValue,
    string? Version = null,
    bool SetPoint = false,
    IReadOnlyList<DbMember>? NestedMembers = null);
