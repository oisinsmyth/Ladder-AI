namespace Converter.CandidateScan;

// Whether the FB itself writes a member (a status it reports) or reads it (a command it obeys).
// COMPUTED from the usage graph, never inferred from the interface section or the member's name — the
// members that matter live under STATIC in the site's interface-UDT structs, so section-filtering gets
// the wrong answer on the very block the narrowed-fault-gate defect concerns.
public enum MemberRole
{
    Status,   // the FB writes it
    Command,  // the FB reads it
    Both,     // read and written (an internal latch exposed on the interface)
    Unused,   // neither, in this corpus
}

public sealed record IoCandidate(string Path, string Type, IReadOnlyList<string> ReadBy);

public sealed record FbCandidate(string Member, string Type, MemberRole Role, IReadOnlyList<string> WrittenAt);

// The transposition signature: N same-typed IO signals in one name family facing N FB members of
// matching direction is the shape a 1:1 by-name-resemblance assignment silently gets wrong. Counting it
// is a group-by over data already gathered; judging it is the engineer's.
public sealed record FamilyFact(int SameTypedIoSignals, int FbMembersOfMatchingDirection);

public sealed record CandidateScanReport(
    string ProjectDir,
    int FilesScanned,
    string FbName,
    string? Instance,
    IReadOnlyList<string> Scopes,
    string? TypeFilter,
    string Direction,
    IReadOnlyList<IoCandidate> IoCandidates,
    IReadOnlyList<FbCandidate> FbCandidates,
    FamilyFact Family,
    IReadOnlyList<string> PhraseMatches,
    IReadOnlyList<string> Warnings)
{
    public int Size => IoCandidates.Count + FbCandidates.Count;

    // Finding-bearing when the requirement could be satisfied by more than one signal — the mechanical
    // trigger that makes a spec stage's "ambiguous binding" question non-discretionary instead of a
    // judgement call. Keyed on the UNFILTERED size: a phrase filter never suppresses a finding.
    public bool HasChoice => Size > 1;
}
