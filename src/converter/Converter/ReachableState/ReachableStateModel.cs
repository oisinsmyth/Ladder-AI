namespace Converter.ReachableState;

/// <summary>
/// One block's REACHABLE-STATE CLOSURE — the transitive closure, through that block's call tree, of
/// every storage location it touches, keyed on STORAGE IDENTITY rather than on spelling.
///
/// <para>🔴 <b>THIS IS D9's MISSING PRODUCER.</b> `Ladder.Wave.SlotConflictDerivation.OverlappingReachableState`
/// turns two such sets into a conflict edge by set intersection, and
/// `Ladder.Wave.WaveSetAdmission` refuses a slot whose closure carries no provenance. Until this
/// existed the set arrived from `wave-cli submit --reaches`, i.e. <b>declared by the submitting
/// agent</b> — and *ask of any rule: who computes its inputs? If the answer is "the party the rule
/// constrains", it is not a rule.*</para>
///
/// <para>*** <see cref="Computed"/> IS THE WHOLE POINT OF THIS RECORD EXISTING RATHER THAN A BARE
/// LIST. *** `reachableState: []` is the positive claim <i>"the closure was computed and is empty"</i>;
/// a closure nobody could compute must OMIT THE KEY, never write an empty array. Both arrive as an
/// empty set downstream and call for opposite actions —
/// <c>ColouringDefect.ReachableStateNotComputed</c> refuses the slot on the second and admits it on
/// the first, so collapsing them silently converts a refusal into a pass.</para>
/// </summary>
/// <param name="Block">The block the closure is for.</param>
/// <param name="Computed">
/// FALSE when the closure could not be completed. <see cref="ReachableState"/> is then empty and
/// carries NO claim, and <see cref="Provenance"/> is empty so admission refuses rather than treating
/// the slot as independent.
/// </param>
/// <param name="NotComputedReason">Why, when <see cref="Computed"/> is false. Empty otherwise.</param>
/// <param name="ReachableState">
/// The canonical storage keys, sorted. <c>&lt;Owner&gt;|&lt;path&gt;</c> for storage a block declares
/// for itself; the path verbatim for global storage (a DB member, a PLC tag, a physical address).
/// </param>
/// <param name="CallClosure">
/// Every block whose touches were unioned in, INCLUDING this one. Printed so a reader can check the
/// derivation rather than take the set on trust — a closure that reached one block is a different
/// claim from one that reached nine.
/// </param>
/// <param name="UnresolvedCalls">
/// Blocks this closure CALLs that the corpus does not contain. Their storage is unknown, so the
/// closure cannot be completed and <see cref="Computed"/> is false — an incomplete closure fails
/// toward FALSE DISJOINTNESS, which puts two agents on one instance.
/// </param>
/// <param name="AliasCanonicalisations">
/// Every <c>iDB.&lt;suffix&gt; -&gt; &lt;FB&gt;|&lt;suffix&gt;</c> rewrite applied, sorted. Printed
/// because *a narrowing nobody can see becomes a place to hide*: this rewrite is what makes a
/// caller's `iDB_X.IO.Step` intersect the FB's own `IO.Step`, and its absence would read as
/// disjointness.
/// </param>
/// <param name="Provenance">
/// WHERE the closure came from — the string that lands in <c>TestSlot.ReachableStateProvenance</c>.
/// EMPTY when <see cref="Computed"/> is false, which is what makes admission refuse.
/// </param>
public sealed record BlockReachableState(
    string Block,
    bool Computed,
    string NotComputedReason,
    IReadOnlyList<string> ReachableState,
    IReadOnlyList<string> CallClosure,
    IReadOnlyList<string> UnresolvedCalls,
    IReadOnlyList<string> AliasCanonicalisations,
    string Provenance);

/// <summary>
/// The emission. Same discipline as <c>Converter.ConflictGraph.ConflictGraphReport</c>, and for the
/// same reason: <b>an absent answer and an empty one are different documents</b>, at the report level
/// and again at each block.
/// </summary>
/// <param name="CorpusHash">
/// A digest over the .ir files the closure was computed from — name and content, sorted. It is carried
/// into <see cref="ReachableStateReport.Provenance"/> so a stale closure is DETECTABLE rather than
/// merely unlikely: *a declaration is a transferred responsibility, not a verification*, and carrying
/// what it was established against is what makes "nobody did it" and "did it against a different
/// corpus" distinct facts.
/// </param>
public sealed record ReachableStateReport(
    bool Computed,
    string NotComputedReason,
    IReadOnlyList<BlockReachableState> Blocks,
    IReadOnlyList<string> Warnings,
    string CorpusDescription,
    string CorpusHash,
    string Provenance)
{
    /// <summary>
    /// Blocks whose own closure could not be completed. The report may be computed while some of these
    /// are not — a whole-project run reports every block it can and withholds the rest BY NAME, which
    /// is a stronger statement than refusing the lot.
    /// </summary>
    public IReadOnlyList<BlockReachableState> NotComputedBlocks =>
        Blocks.Where(b => !b.Computed).ToList();
}
