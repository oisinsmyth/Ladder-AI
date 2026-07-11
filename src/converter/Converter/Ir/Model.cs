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
}

public sealed record CoilAssignment(string CoilTag, Expr Condition);

// A network can bundle multiple independent Contact-chain-into-Coil rungs with no shared
// wiring between them — confirmed against a real export, 2026-07-10 (a 16-independent-rung
// alarm-bit network). Assignments is empty for a genuinely empty network (source
// `<NetworkSource />` with no FlgNet content at all, also confirmed real) — both are
// unambiguous, not guesses, so both are modeled directly rather than hard-erroring.
public sealed record IrNetwork(int Number, string Title, IReadOnlyList<CoilAssignment> Assignments)
{
    public bool IsEmpty => Assignments.Count == 0;
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
public sealed record SidecarAccessEntry(string TagPath, int UId);

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
}

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
public sealed record CoilAssignmentSidecar(
    int RailWireUId,
    IReadOnlyList<ChainStepSidecar> Steps,
    int CoilUId,
    int CoilOperandAccessUId,
    int CoilOperandWireUId);

public sealed record NetworkSidecar(
    int NetworkNumber,
    string CompileUnitUId,
    IReadOnlyList<SidecarAccessEntry> AccessUIds,
    IReadOnlyList<CoilAssignmentSidecar> Assignments);

public sealed record ReducedNetwork(IrNetwork Network, NetworkSidecar Sidecar);

public sealed class IrFormatException : Exception
{
    public IrFormatException(string message)
        : base(message)
    {
    }
}
