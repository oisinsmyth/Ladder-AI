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
    IReadOnlyList<int> AllowedNetworks,
    // The caller's explicit declaration that a header change (interface / title / comment) or a block
    // rename is INTENDED - `--allow-header`. Absent, an unclaimed header change is an invariance
    // violation like any other change outside --only. FI-71's shape: the escape is NAMED, so the
    // default cannot be a false assurance.
    bool HeaderChangeAllowed = false)
{
    public int ChangedCount => Networks.Count(n => n.Kind == NetworkChangeKind.Changed);

    public int AddedCount => Networks.Count(n => n.Kind == NetworkChangeKind.Added);

    public int RemovedCount => Networks.Count(n => n.Kind == NetworkChangeKind.Removed);

    public int IdenticalCount => Networks.Count(n => n.Kind == NetworkChangeKind.Identical);

    public bool HasAnyChange => Header.Changed || Networks.Any(n => n.Kind != NetworkChangeKind.Identical);

    // Meaningful only when --only was supplied (AllowedNetworks non-empty). Any network that
    // changed/appeared/disappeared outside the declared change set is an invariance violation — the
    // exact failure the S7 skill gates on.
    public IReadOnlyList<NetworkDiff> InvarianceViolations =>
        AllowedNetworks.Count == 0
            ? Array.Empty<NetworkDiff>()
            : Networks
                .Where(n => n.Kind != NetworkChangeKind.Identical && !AllowedNetworks.Contains(n.Number))
                .ToList();

    // 2026-08-14, ruled a VIOLATION. The removed comment above explained why the CHECK was absent —
    // "header intent isn't expressible via --only, so header changes are surfaced but never counted
    // as a violation" — and that explains the check, it does not license the CLAIM. Measured:
    // retyping an interface member Bool -> Int with NO network touched printed
    //     HEADER changed: interface
    //     INVARIANCE OK: all changes confined to --only {1}
    // two lines apart, the second contradicting the first, and EXIT 0 — and the exit code follows the
    // sentence. This is the S7 modification gate, whose whole job is "prove the rest is identical",
    // and a retyped member is precisely the change that compiles, imports, and misbehaves on the
    // controller. A block rename + renumber behaved the same way, as a warning.
    //
    // So an unclaimed header change or rename now VIOLATES, and `--allow-header` makes the intent
    // EXPRESSIBLE rather than leaving the default a false assurance (FI-71's shape). The escape is
    // real work, not a formality: gen-block-modify-purpose changes interfaces on purpose and says so.
    public bool HasUnclaimedHeaderChange =>
        AllowedNetworks.Count > 0 && !HeaderChangeAllowed && (Header.Changed || BlockNameMismatch);

    public bool HasInvarianceViolation => InvarianceViolations.Count > 0 || HasUnclaimedHeaderChange;
}
