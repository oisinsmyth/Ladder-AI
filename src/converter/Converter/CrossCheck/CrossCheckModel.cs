namespace Converter.CrossCheck;

// FI-22: whole-project cross-block FACTS the review skills reason over — never adjudicated verdicts
// (same discipline as the `converter review` dump). Four fact tables, each supporting one hand-built
// cross-block table the review skills produce today. One record set, two renderers.

public sealed record WriterRef(string Block, int Network, string Kind);

public sealed record ReaderRef(string Block, int Network);

// C-308 support: a full path written by >1 site (a fact; the AI judges whether it's a violation — a
// legitimate Set+Reset pair vs two conflicting Assigns is why each writer carries its kind).
public sealed record MultiWriterFact(string Path, IReadOnlyList<WriterRef> Writers);

// review-functional Pass-2 dead-wiring support (scoped to GLOBAL-DB members — unambiguous addressing):
// a member with no writer (consumed-but-never-written / fully unused) or no reader
// (written-but-never-consumed). Exactly one side may be non-empty.
public sealed record DeadMemberFact(string Path, IReadOnlyList<WriterRef> Writers, IReadOnlyList<ReaderRef> Readers);

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
