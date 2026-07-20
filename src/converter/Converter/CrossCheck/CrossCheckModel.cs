namespace Converter.CrossCheck;

// FI-22: whole-project cross-block FACTS the review skills reason over — never adjudicated verdicts
// (same discipline as the `converter review` dump). Four fact tables, each supporting one hand-built
// cross-block table the review skills produce today. One record set, two renderers.

public sealed record WriterRef(string Block, int Network, string Kind);

public sealed record ReaderRef(string Block, int Network);

// C-308 support: a full path written by >1 site (a fact; the AI judges whether it's a violation — a
// legitimate Set+Reset pair vs two conflicting Assigns is why each writer carries its kind).
public sealed record MultiWriterFact(string Path, IReadOnlyList<WriterRef> Writers);

// Whether a dead member is a global-DB member (unambiguous full path) or an interface-UDT member of
// an FB (aliased between the FB-internal bare form and the external iDB-qualified form — correlated
// across both before the deadness verdict). Lets a consumer treat the two classes distinctly.
public enum DeadMemberScope
{
    GlobalDb,
    InterfaceMember,
}

// review-functional Pass-2 dead-wiring support. A member with no writer (consumed-but-never-written /
// fully unused) or no reader (written-but-never-consumed) across ALL the ways it is addressed:
//   - GlobalDb: the single full path `DBName.member…`.
//   - InterfaceMember: an FB interface member, whose writers/readers are aggregated across the
//     FB-internal bare form AND every `iDB.<suffix>` alias (FB->iDB is one-to-many) before the
//     no-writer/no-reader test — so a member written internally and read via an iDB is NOT dead. Its
//     Path is rendered FB-rooted (`<FB>.<suffix>`). Exactly one of Writers/Readers is empty.
public sealed record DeadMemberFact(
    string Path,
    IReadOnlyList<WriterRef> Writers,
    IReadOnlyList<ReaderRef> Readers,
    DeadMemberScope Scope = DeadMemberScope.GlobalDb);

// C-304 support: a physical-IO tag reference and the block that makes it. The AI excludes the Map FCs
// (mapping IS their job) and flags any OTHER block touching raw IO.
public sealed record IoBoundaryFact(string Block, string Path, string Direction);

// C-127 support: per block, the blocks it CALLs and any iDB_* root it references — the hardcoded
// sibling dependencies a reusable equipment FB must not contain.
public sealed record SiblingRefFact(string Block, IReadOnlyList<string> Calls, IReadOnlyList<string> InstanceDbRoots);

public sealed record CrossCheckReport(
    IReadOnlyList<MultiWriterFact> MultiWriters,
    IReadOnlyList<DeadMemberFact> DeadMembers,
    IReadOnlyList<IoBoundaryFact> IoBoundary,
    IReadOnlyList<SiblingRefFact> SiblingRefs,
    IReadOnlyList<string> Warnings);
