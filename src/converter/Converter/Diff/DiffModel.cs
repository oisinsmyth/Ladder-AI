namespace Converter.Diff;

// converter diff (S7 entry requirement, docs/02-roadmap.md line 57): given the before/after IR of
// ONE block, report which networks changed and prove the rest identical in IR — the mechanical form
// of the S7 "untouched-network invariance check" (CLAUDE.md "Workflow for modifying existing logic").
//
// Semantic equality is defined as equality of the sidecar-free *readable* form
// (IrSerializer.SerializeNetworkOnly), which IrSelfStabilityTests already proves is byte-stable.
// Because every volatile UId (Wire/Access/Part/CompileUnit) lives only in the SIDECAR section that
// this form omits, volatile-ID churn from a TIA re-export normalizes out for free — no separate
// Normalizer engine is needed. See DiffRunner for the full contract.

public enum NetworkChangeKind
{
    Identical,
    Changed,
    Added,
    Removed,
}

// One network's before/after verdict. Before/After carry the SerializeNetworkOnly text so the text
// renderer can show the rung-level delta: both set for Changed; only the surviving side for
// Added (After) / Removed (Before); both null for Identical.
public sealed record NetworkDiff(
    int Number,
    NetworkChangeKind Kind,
    string? Title,
    string? BeforeText,
    string? AfterText);

// Block-level (non-network) changes — matters for gen-block-modify-purpose, which changes the
// interface. Title/Comment compared as fields; Interface compared via its sidecar-free canonical
// serialization (no UIds in an INTERFACE section). Block RootUId is deliberately NOT compared: it
// is a volatile block ID, so comparing it would flag an unchanged re-export as changed.
public sealed record HeaderDiff(
    bool TitleChanged,
    string? TitleBefore,
    string? TitleAfter,
    bool CommentChanged,
    string? CommentBefore,
    string? CommentAfter,
    bool InterfaceChanged)
{
    public bool Changed => TitleChanged || CommentChanged || InterfaceChanged;
}

// The whole comparison. AllowedNetworks is the --only set (empty when --only wasn't supplied).
public sealed record DiffReport(
    string BlockName,
    bool BlockNameMismatch,
    string? OtherBlockName,
    HeaderDiff Header,
    IReadOnlyList<NetworkDiff> Networks,
    IReadOnlyList<int> AllowedNetworks)
{
    public int ChangedCount => Networks.Count(n => n.Kind == NetworkChangeKind.Changed);

    public int AddedCount => Networks.Count(n => n.Kind == NetworkChangeKind.Added);

    public int RemovedCount => Networks.Count(n => n.Kind == NetworkChangeKind.Removed);

    public int IdenticalCount => Networks.Count(n => n.Kind == NetworkChangeKind.Identical);

    public bool HasAnyChange => Header.Changed || Networks.Any(n => n.Kind != NetworkChangeKind.Identical);

    // Meaningful only when --only was supplied (AllowedNetworks non-empty). Any network that
    // changed/appeared/disappeared outside the declared change set is an invariance violation — the
    // exact failure the S7 skill gates on. A header change also violates invariance unless it was
    // the point of the modification, but header intent isn't expressible via --only (which names
    // networks), so header changes are surfaced but never counted as a violation here.
    public IReadOnlyList<NetworkDiff> InvarianceViolations =>
        AllowedNetworks.Count == 0
            ? Array.Empty<NetworkDiff>()
            : Networks
                .Where(n => n.Kind != NetworkChangeKind.Identical && !AllowedNetworks.Contains(n.Number))
                .ToList();

    public bool HasInvarianceViolation => InvarianceViolations.Count > 0;
}
