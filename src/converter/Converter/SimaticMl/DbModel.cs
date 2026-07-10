namespace Converter.SimaticMl;

// No sidecar needed the way FlgNetwork has one: a DB has no wiring graph, so this parsed model
// already *is* the IR-facing shape too (Converter.Ir.DbIrSerializer/DbIrParser operate on this
// record directly). RootUId is the only round-trip-only field, carried straight through like
// BlockSource.RootUId.
public sealed record DbSource(
    string RootUId,
    string Name,
    int Number,
    string? Comment,
    IReadOnlyList<DbMember> Members);

// Datatype is carried verbatim as the source XML shows it (e.g. "Word", "Array[0..14] of Bool")
// — not re-parsed into a separate array/type grammar. Confirmed real, 2026-07-10: every scalar
// and array-of-scalar member's own type string round-trips exactly as an opaque string; this
// slice never needs to understand it structurally (in scope: GlobalDB, Static section, scalar
// and Array[m..n] of <scalar> members only — src/converter/README.md has the full scope note).
public sealed record DbMember(string Name, string Datatype, bool Retain, string? StartValue);
