namespace Converter.TagStatus;

// How a name resolved against the project export. Four states, not two: a root can resolve while its
// member namespace is genuinely unknowable (an instance DB whose UDT isn't in the export), and
// calling that "member absent" would manufacture false gaps — the opposite failure to the one the
// member check exists to fix.
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
}

// One classified name: its extracted root and how it resolved.
public sealed record TagStatusEntry(string Name, string Root, TagStatusKind Status)
{
    // Derived convenience, and the JSON payload's back-compatible `exists` field.
    public bool Exists => Status == TagStatusKind.Exists;

    // What a caller gates on: an invented root OR an invented member. MemberUnchecked deliberately
    // does not block — it is an honest "could not verify", not a finding.
    public bool IsBlocking => Status is TagStatusKind.Proposed or TagStatusKind.MemberNotFound;
}

// Mirrors the Review/Preflight/Digest "one record, two renderers" report shape.
public sealed record TagStatusReport(IReadOnlyList<TagStatusEntry> Entries, IReadOnlyList<string> IndexWarnings)
{
    // Drives the non-zero exit so callers can gate on it (CLAUDE.md hard rule 3).
    public bool HasBlocking => Entries.Any(e => e.IsBlocking);
}
