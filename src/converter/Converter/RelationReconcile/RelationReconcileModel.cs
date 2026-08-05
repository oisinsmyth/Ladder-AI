namespace Converter.RelationReconcile;

// A relation is only identified by BOTH its instance and its id: two instances' `C1` are different
// relations, so a bare union of ids across specs is vacuous.
public readonly record struct RelationKey(string Instance, string Id)
{
    public override string ToString() => $"{Instance}.{Id}";
}

public enum LegKind
{
    Specs,     // equipment-specs/<Instance>.md   — `- **C1**` bullets
    Ledger,    // code-structure.md D2            — `| C1 | disposition | evidence | precondition |`
    Register,  // requirements.md                 — the `Rel` column of the per-instance REQ table
    Render,    // code-structure.md D3            — `[C2]` / `[P1,P2]` term tags (absent when stopped)
}

// A leg that is Absent is a legitimate state (a stopped D3), NOT a leg with zero relations — the
// difference matters, because reporting "0 differences" for an artifact that does not exist is the
// silent-green failure this whole check exists to avoid.
public sealed record Leg(LegKind Kind, bool Present, IReadOnlyList<RelationKey> Keys)
{
    public int Count => Keys.Count;
}

// How a backticked token in a `verified-cross-block` evidence cell resolves against the corpus.
public enum TokenResolution
{
    WrittenMember,      // resolves to a member with at least one armed writer — the precondition is probative
    DisarmedWriters,    // resolves, has writers, but every one is provably-false-gated
    DeclarationOnly,    // resolves as a declared member with no writer IN THIS EXPORT (state the denominator)
    Unresolved,         // not a member/tag in this corpus (a file name, a network label, an expression fragment)
}

public sealed record CitationToken(string Token, TokenResolution Resolution, int WriterCount);

// One `verified-cross-block` ledger row and how each of its cited tokens resolved. A row where NO token
// resolves to a written member is the finding: the citation names the form of proof without carrying it.
public sealed record CitationRow(RelationKey Relation, string Evidence, IReadOnlyList<CitationToken> Tokens)
{
    public bool IsFinding => Tokens.All(t => t.Resolution != TokenResolution.WrittenMember);
}

public sealed record PairDifference(LegKind From, LegKind To, IReadOnlyList<RelationKey> MissingInTo);

// How many ledger rows carried each disposition, and whether that disposition obliges a D3 term
// (FI-45 item 3). The five non-render-bound dispositions are ACCOUNTED FOR, not differences.
public sealed record DispositionGroup(string Disposition, DispositionClass Class, int Count);

// A leg that parsed relations and shares NOT ONE key with any other present leg — so every
// comparison it took part in compared nothing (FI-44 item 3).
//
// This is a different situation from an ABSENT leg and must report differently. ABSENT means the
// artifact is not there (a stopped D3): the check knows it did not look, says so, and deliberately
// does not gate. UNCOMPARED means the artifact IS there, parsed, and was measured against a set it
// cannot intersect — the demonstrated cause being a render leg keyed on the instance-DB name while
// every other leg is keyed on the spec instance. Nothing was compared, and unlike ABSENT nothing
// said so. That is the silent success FI-44 exists to kill.
public sealed record UncomparedLeg(LegKind Kind, int Count, IReadOnlyList<RelationKey> SampleKeys);

public sealed record RelationReconcileReport(
    IReadOnlyList<Leg> Legs,
    IReadOnlyList<PairDifference> Differences,
    IReadOnlyList<CitationRow> Citations,
    string? ProjectDir,
    int FilesScanned,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<DispositionGroup> LedgerDispositions,
    IReadOnlyList<UncomparedLeg> UncomparedLegs)
{
    public IReadOnlyList<CitationRow> CitationFindings => Citations.Where(c => c.IsFinding).ToList();

    // Exit-bearing: any relation present in one leg and missing from another, a citation that cites no
    // written member, or a leg that compared against nothing. An ABSENT leg does not gate — a stopped
    // rung is a legitimate state, not a drift — but a PRESENT leg matching nothing is not the same
    // thing and does gate.
    public bool HasFindings =>
        Differences.Any(d => d.MissingInTo.Count > 0) || CitationFindings.Count > 0 || UncomparedLegs.Count > 0;
}

// A leg that parsed zero rows is a hard error, never a clean reconcile: format drift that silently
// reports "all green" would make this check worse than not having it.
public sealed class RelationReconcileFormatException : Exception
{
    public RelationReconcileFormatException(string message)
        : base(message)
    {
    }
}
