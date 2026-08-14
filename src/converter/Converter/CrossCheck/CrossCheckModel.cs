namespace Converter.CrossCheck;

// FI-22: whole-project cross-block FACTS the review skills reason over — never adjudicated verdicts
// (same discipline as the `converter review` dump). Four fact tables, each supporting one hand-built
// cross-block table the review skills produce today. One record set, two renderers.

public sealed record WriterRef(string Block, int Network, string Kind);

public sealed record ReaderRef(string Block, int Network);

// C-308 support: a full path written by >1 site (a fact; the AI judges whether it's a violation — a
// legitimate Set+Reset pair vs two conflicting Assigns is why each writer carries its kind).
//
// 🔴 Owner (2026-08-14) is the block whose own declaration the path resolves inside, or null when
// the path is GLOBAL (a DB member, a PLC tag, an `iDB_…` reference, a physical address). It exists
// because this table used to key on the verbatim path, and an FB addresses its own interface member
// WITH NO ROOT — so `IO.Step` in FB_PusherControl and `IO.Step` in FB_ShredderSequencer, members of
// two DIFFERENT UDTs, were reported as one cross-block multi-writer. *** A NON-NULL Owner MEANS THE
// WRITERS ARE ALL INSIDE ONE BLOCK BY CONSTRUCTION, AND THE FACT SAYS NOTHING ABOUT CROSS-BLOCK
// CONFLICT. *** Path carries the same information as `<Owner>.<path>` for display, but a consumer
// deciding anything must read Owner rather than parse Path — an emitted string is not a schema.
//
// InstanceAliases lists the `iDB.<suffix>` forms naming the same storage. They are REPORTED rather
// than pooled into this fact: for an FB with one instance DB they are the same storage, but an FB
// with TWO has an internal write landing in BOTH, and picking either would re-create the invented
// conflict this field exists to have stopped.
public sealed record MultiWriterFact(
    string Path,
    IReadOnlyList<WriterRef> Writers,
    string? Owner = null,
    IReadOnlyList<string>? InstanceAliases = null,
    // 2026-08-14. Writers living in blocks NOT reachable from any OB through the call graph — they do
    // not execute, so they cannot contend at runtime. Reported, never subtracted: an unreachable block
    // is usually a block someone means to call, and silently dropping its write would hide the
    // conflict that appears the moment it is wired up. Measured: a two-writer path where one writer's
    // block was called by nothing read as a C-308 multi-writer with exactly one runtime writer — and
    // the call graph needed to say so was already in the same report, under SIBLING REFERENCES.
    //
    // Empty ALSO when the corpus contains no OB at all, which is "unknown", not "all reachable" —
    // hence UnreachableKnown, which says whether the question could be asked.
    IReadOnlyList<string>? UnreachableWriterBlocks = null,
    bool UnreachableKnown = false)
{
    public IReadOnlyList<string> InstanceAliases { get; init; } = InstanceAliases ?? Array.Empty<string>();

    public IReadOnlyList<string> UnreachableWriterBlocks { get; init; } =
        UnreachableWriterBlocks ?? Array.Empty<string>();

    // The writers that actually execute. A count of 1 here beside a Writers count of 2+ is the exact
    // shape of the false finding: reported as a fact, judged by the reader.
    public int ReachableWriterCount => UnreachableKnown
        ? Writers.Select(w => w.Block).Distinct(StringComparer.Ordinal)
            .Count(b => !UnreachableWriterBlocks.Contains(b, StringComparer.Ordinal))
        : Writers.Select(w => w.Block).Distinct(StringComparer.Ordinal).Count();
}

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

/// <summary>
/// FI-67 (2026-08-09). THE COMPLEMENT OF <see cref="MultiWriterFact"/>, AND THE ONE QUESTION A
/// BACK-OUT MUST ASK.
///
/// `multiWriters` lists, by construction, only paths written by MORE THAN ONE site. The sole-writer
/// set is precisely its complement and was never emitted — so the writer graph this tool already
/// builds could not be asked *"which members would lose their only writer if I deleted this?"*
///
/// WHY THAT MATTERS MORE THAN A MISSING VIEW. Deleting a feature that is the sole writer of a
/// RETENTIVE member leaves that member frozen at its last value with nothing able to clear it. On
/// one live job that included a resource reservation whose surviving reader gates every grant:
/// removing the writer with the bit standing would have made a shared machine ungrantable
/// permanently, curable only by an online write. Three successive hand-written passes over that
/// deletion each added one more item and still missed a whole class of four.
///
/// <see cref="Readers"/> is carried because it is what separates a hazard from dead data: a member
/// whose readers all disappear with the feature is inert, while one with a surviving reader is live.
/// The two questions are answered from the same graph, so answering only the first would leave the
/// caller to re-derive the second by hand — which is how the class of four was missed.
///
/// Retention is deliberately NOT filtered here: this layer does not model it, and guessing would be
/// worse than leaving the caller to intersect this set with the declarations. Facts, not verdicts —
/// the same contract as every other table in this report.
/// </summary>
/// <remarks>
/// Owner: see <see cref="MultiWriterFact"/>. 🔴 The path-aliasing defect fixed 2026-08-14 was
/// UNDER-REPORTING this table, which is the more dangerous direction of the same bug: two FBs each
/// writing their own `Time` once pooled into a single two-writer path, so the member read as
/// multi-written — <b>not vulnerable to a deletion</b> — when each was in fact its own FB's SOLE
/// writer. A back-out consulting this table was told the safe thing about a member that was not safe.
/// </remarks>
public sealed record SoleWriterFact(
    string Path,
    WriterRef Writer,
    IReadOnlyList<ReaderRef> Readers,
    string? Owner = null);

public sealed record CrossCheckReport(
    IReadOnlyList<MultiWriterFact> MultiWriters,
    IReadOnlyList<DeadMemberFact> DeadMembers,
    IReadOnlyList<IoBoundaryFact> IoBoundary,
    IReadOnlyList<SiblingRefFact> SiblingRefs,
    IReadOnlyList<string> Warnings,
    // FI-67. Appended last so every existing positional construction keeps compiling; defaulted so a
    // caller that does not care about back-out surface is unaffected.
    IReadOnlyList<SoleWriterFact>? SoleWriters = null)
{
    public IReadOnlyList<SoleWriterFact> SoleWriters { get; init; } = SoleWriters ?? Array.Empty<SoleWriterFact>();
}
