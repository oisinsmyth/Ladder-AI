namespace Converter.TagStatus;

// How a name resolved against the project export. Five states, not two: a root can resolve while its
// member namespace is genuinely unknowable (an instance DB whose UDT isn't in the export), and
// calling that "member absent" would manufacture false gaps — the opposite failure to the one the
// member check exists to fix; and an out-of-range array subscript is a real defect of its own, not a
// flavour of "invented member".
public enum TagStatusKind
{
    // The whole dotted path resolves (or a bare name resolves as a tag/DB).
    Exists,

    // The ROOT does not resolve in the export — a named gap the engineer creates (hard rule 3).
    Proposed,

    // The root resolves and its members ARE enumerable, but this member is not among them. The
    // anti-laundering case: `SomeDb.InventedMember` used to report EXISTS on the strength of its
    // root alone, which let an invented member pass the gate that exists to stop it.
    MemberNotFound,

    // The root resolves but its member namespace cannot be enumerated (unknown UDT/type), so member
    // existence is genuinely unverified — reported as such, never guessed in either direction.
    MemberUnchecked,

    // Every component of the path names a real member, but a written array subscript falls outside
    // the declared bounds (`Vessel[7]` of `Array[0..3] of "UDT_Vessel"`). Its own status because the
    // fault is precisely located: the member is real, the element named is not — telling an engineer
    // "member not found" here would send them looking for the wrong thing (FI-45 item 1).
    IndexOutOfRange,
}

// One classified name: its extracted root, how it resolved, and — where the walk can say something
// more precise than the status alone (which subscript, against which declared bounds) — a short
// detail for the reader.
public sealed record TagStatusEntry(string Name, string Root, TagStatusKind Status, string? Detail = null)
{
    // Derived convenience, and the JSON payload's back-compatible `exists` field.
    public bool Exists => Status == TagStatusKind.Exists;

    // What a caller gates on: an invented root, an invented member, or a subscript naming an element
    // that does not exist. MemberUnchecked deliberately does not block — it is an honest "could not
    // verify", not a finding.
    public bool IsBlocking => Status is TagStatusKind.Proposed
        or TagStatusKind.MemberNotFound
        or TagStatusKind.IndexOutOfRange;
}

// Mirrors the Review/Preflight/Digest "one record, two renderers" report shape.
public sealed record TagStatusReport(IReadOnlyList<TagStatusEntry> Entries, IReadOnlyList<string> IndexWarnings)
{
    // Drives the non-zero exit so callers can gate on it (CLAUDE.md hard rule 3).
    public bool HasBlocking => Entries.Any(e => e.IsBlocking);
}
