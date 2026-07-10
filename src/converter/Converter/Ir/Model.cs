namespace Converter.Ir;

public abstract record Expr
{
    private Expr()
    {
    }

    public sealed record TagRef(string Path) : Expr;

    public sealed record And(IReadOnlyList<Expr> Operands) : Expr;

    public sealed record Or(IReadOnlyList<Expr> Operands) : Expr;
}

public sealed record CoilAssignment(string CoilTag, Expr Condition);

// A network can bundle multiple independent Contact-chain-into-Coil rungs with no shared
// wiring between them — confirmed against a real export, 2026-07-11 (a 16-independent-rung
// alarm-bit network). Assignments is empty for a genuinely empty network (source
// `<NetworkSource />` with no FlgNet content at all, also confirmed real) — both are
// unambiguous, not guesses, so both are modeled directly rather than hard-erroring.
public sealed record IrNetwork(int Number, string Title, IReadOnlyList<CoilAssignment> Assignments)
{
    public bool IsEmpty => Assignments.Count == 0;
}

// RootUId: the source block element's own opaque "ID" attribute (required by Import(),
// confirmed real 2026-07-11 — separate from any CompileUnit's own ID). Round-trip-only, like
// NetworkSidecar.CompileUnitUId.
public sealed record IrBlock(
    string RootUId,
    string Kind,
    string Name,
    int Number,
    string Language,
    string? Comment,
    IReadOnlyList<IrNetwork> Networks);

/// <summary>
/// Machine-owned round-trip data (ir/SPEC.md "Sidecar") — never hand-edited. Records the exact
/// source UIds this slice's reducer flattened away when it built the readable form, so the
/// serializer can regenerate byte-for-byte-equivalent structure on to-xml. Only valid for
/// networks the reducer actually reduced to pure series chains — see GraphReducer's doc comment.
/// </summary>
public sealed record SidecarAccessEntry(string TagPath, int UId);

// RailWireUId is separated from WireUIds because the source wire connecting Powerrail to a
// chain's first element is often shared across MANY independent chains in the same network
// (one wire, many endpoints — confirmed real, 2026-07-11) — it is not exclusively "owned" by
// any one assignment the way every other wire in the chain is. WireUIds holds everything else,
// in order: [operand_0, flow_0to1, operand_1, flow_1to2, ..., operand_last, flow_lastToCoil, coilOperand].
public sealed record CoilAssignmentSidecar(int RailWireUId, IReadOnlyList<int> ContactUIds, int CoilUId, IReadOnlyList<int> WireUIds);

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
