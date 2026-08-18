namespace Harness.Results;

/// <summary>
/// <b>What a signal IS.</b> A property of the behaviour under test, not of the harness.
///
/// <para>This is the axis <c>Harness.TestVector</c> has carried since phase 0, and it is NOT the same
/// axis as <see cref="InstrumentationMode"/> — see <see cref="ObservabilityCheck"/> for the mapping and
/// for the one cell of it that is inferred rather than quoted.</para>
/// </summary>
public enum SignalNature
{
    /// <summary>The asserted condition persists — a commanded state through a phase, a latched weight, a raised alarm.</summary>
    PersistentState,

    /// <summary>Momentary — true for one scan. <b>Unobservable at any polling rate</b>; a poll IS one round trip.</summary>
    Transient,

    /// <summary>Two things in the SAME scan. <b>Unobservable by sampling at all</b>, at any rate.</summary>
    Coincidence,
}

/// <summary>
/// <b>What the copy layer DID about it.</b> A property of the instrumentation, not of the signal.
///
/// <para>These are the contract §4.2 modes. The two enums have overlapping names and are genuinely
/// different facts — a signal's nature is discovered, an instrumentation mode is applied — which is
/// exactly why nothing could map them while they were one undifferentiated idea.</para>
/// </summary>
public enum InstrumentationMode
{
    /// <summary>A sticky bit the harness reads then clears. <b>The only mode immune to the tail</b> — a latch cannot fall in a poll gap.</summary>
    Latched,

    /// <summary>Read it when we look. Free, and <b>fully subject to the observability floor</b>.</summary>
    Sampled,

    /// <summary>The program records the scan number of the event. ~32x the memory of a latch; the only mode that answers "when".</summary>
    Stamped,
}

/// <summary>
/// Which of the assertion enumeration's two canonical forms the cited assertion takes.
///
/// <para><c>WHEN &lt;trigger&gt; THEN &lt;response&gt;</c> versus <c>NEVER &lt;forbidden state&gt;</c>.
/// It is a property of the ASSERTION, not of the signal or the instrumentation - which is why it is
/// declared once per vector and passed to the observability check rather than sitting on each
/// expectation.</para>
///
/// <para><b>It changes what a mode may answer</b>, because a NEVER assertion is the one shape whose
/// PASS is produced by seeing nothing. See <see cref="ObservabilityCheck.ModesThatCanAnswer"/>.</para>
/// </summary>
public enum AssertionForm
{
    /// <summary>
    /// <b>Nothing said what form this is — and it is deliberately the ZERO value.</b>
    ///
    /// <para>The form decides whether a SAMPLED observation is admissible (F-3), so a field that
    /// silently defaulted to <see cref="When"/> would hand every author who omitted it the permissive
    /// path. Making the default <i>unusable</i> means <b>a DROPPED form fails the same comparison as a
    /// WRONG one</b>, which is the only arrangement in which "the field was absent" cannot quietly
    /// become "the field said the convenient thing".</para>
    ///
    /// <para>It is treated as fail-closed everywhere it is not refused outright: for sufficiency it
    /// behaves like <see cref="Never"/>, because an unknown form MIGHT be one.</para>
    /// </summary>
    Unstated = 0,

    /// <summary><c>WHEN &lt;trigger&gt; THEN &lt;observable response&gt;</c>. A pass requires having SEEN the response.</summary>
    When,

    /// <summary>
    /// <c>NEVER &lt;forbidden observable state&gt;</c> - interlocks and prohibitions. <b>A pass is
    /// produced by having seen nothing</b>, which is exactly what a poll gap also produces.
    /// </summary>
    Never,
}

/// <summary>One expectation's observability declaration (contract §4.3), on both axes.</summary>
/// <param name="Signal">The tag. Must appear in the map's observability declarations.</param>
/// <param name="Nature">What the signal is like. Decides which modes could answer at all.</param>
/// <param name="Mode">What the copy layer applies. Must be one the map actually provides for this signal.</param>
/// <param name="WindowScans">
/// For <see cref="InstrumentationMode.Sampled"/>: how long the condition is expected to HOLD, in scans,
/// <b>at the vector's declared compression factor</b>. Zero for the other modes, which are exempt.
/// </param>
/// <param name="Expected">
/// The value this expectation asserts, as a string. <b>Null means no predicate was declared</b>, which
/// the schema gate refuses — an expectation with nothing to compare against cannot fail, so its pass
/// says nothing. Defaulted only so that the many call sites that are ABOUT observability need not
/// restate it; the gate is what makes the default unusable.
/// </param>
/// <param name="Shape">
/// 🔴 <b>WHAT THE AUTHOR CLAIMS ABOUT THIS SIGNAL <i>IN TIME</i> — see <see cref="TemporalShape"/>.</b>
///
/// <para><b>OPTIONAL, AND ITS ABSENCE IS THE BACKWARD-COMPATIBILITY GUARANTEE.</b> Every vector written
/// before this field existed carries <see cref="TemporalShape.Unstated"/>, which reproduces the previous
/// evaluation exactly — same verdict, same text. A silent change of verdict for existing vectors would
/// rewrite the meaning of results already recorded against a real job.</para>
///
/// <para>⚠️ <b>NOTHING POPULATES THIS YET, AND THAT IS DELIBERATE — IT IS A PROPOSED CONTRACT CHANGE.</b>
/// Vectors are written by an independent party who does not read the implementation, so the field is
/// proposed rather than imposed. And <b>gate 0b REFUSES unknown submission fields</b>, so the submission
/// parser must map <c>temporalShape</c> BEFORE any author may write it — otherwise a vector using it is
/// not ignored, it is REFUSED, and the whole submission with it. Sequencing and the proposed JSON
/// spelling: <c>docs/notes/observation-window-shapes.md</c> §7.</para>
///
/// <para><b>It is NOT <see cref="Mode"/>.</b> An instrumentation mode is HOW the copy layer watches and is
/// DERIVED from what was generated; a shape is WHAT THE SPECIFICATION CLAIMS, stated by the author, and it
/// can be wrong.</para>
/// </param>
public sealed record ObservabilityDeclaration(
    string Signal,
    SignalNature Nature,
    InstrumentationMode Mode,
    int WindowScans,
    string? Expected = null,
    TemporalShape Shape = TemporalShape.Unstated);

/// <summary>What the generated copy layer actually provides, per signal (contract §4.3's "the map's observability declarations").</summary>
/// <remarks>
/// <b>The contract names this artifact and nothing in the repo was it</b> — an author could not check
/// their signal was in a list they could not find. It is a declared set built by the coordinator from
/// the copy layer it generated — see <see cref="MirrorObservability.FromBindings"/>, which is the only
/// constructor that derives it rather than taking a caller's word for it.
/// </remarks>
public enum MapProvenance
{
    /// <summary>
    /// 🔴 <b>NOBODY SAID WHERE THE MAP CAME FROM — and that is the DEFAULT, so it fails closed.</b>
    ///
    /// <para>The zero value, following <c>AssertionForm.Unstated</c>: a field that silently defaulted to
    /// the trustworthy answer would hand every caller who omitted it the permissive path.</para>
    /// </summary>
    Unstated = 0,

    /// <summary>
    /// 🔴 <b>THE MAP CAME OUT OF THE SUBMISSION THE VECTOR AUTHOR WROTE — i.e. the author vouching for the
    /// artifact the gate exists to check them against.</b>
    ///
    /// <para>This is the authority gap that was measured. <c>GateCli</c> took its map from the submission
    /// while <c>LoopRun</c> took the same map from the COORDINATOR'S BINDINGS, so <b>the standalone CLI
    /// was weaker than the loop in exactly the place the CLI decides whether to proceed</b> — and it is
    /// consulted FIRST, which makes a weaker gate worse than an absent one. It is also why nothing
    /// mechanical could see a stale binding row. Gate 5 therefore refuses to be the deciding voice on a
    /// self-declared map: NOT CHECKED, never a pass.</para>
    /// </summary>
    SelfDeclared,

    /// <summary>
    /// The map was derived from the coordinator's bindings — <b>the same source the loop uses</b>, which
    /// is what makes the CLI's authority no weaker than the loop's.
    /// </summary>
    Bindings,
}

public sealed record MirrorObservability(IReadOnlyDictionary<string, IReadOnlySet<InstrumentationMode>> ProvidedFor)
{
    /// <summary>Where this map came from. <b>Decides whether gate 5 may be a verdict at all.</b></summary>
    public MapProvenance Provenance { get; init; } = MapProvenance.Unstated;

    /// <summary>
    /// For each signal offering <c>Latched</c>, <b>the block that does the latching</b>.
    ///
    /// <para><b>Carried so the gate's report can name it</b>: "Latched, provided by FB_X" is checkable
    /// against the deployed objects, where a bare "Latched" is not.</para>
    ///
    /// <para>🔴 <b>IT NO LONGER SAYS "THE GENERATOR EMITS NO PER-SIGNAL LATCH".</b> That was true when this
    /// was written and stopped being true when the transient latch landed —
    /// <c>MirroredSignal.LatchClaimed</c> reads <c>… || Transient</c>, one line below its own copy of the
    /// same stale sentence. <b>Both went stale together</b>, and
    /// <c>docs/notes/spec-reconciliation.md</c> item 2.1 cited one of them as its authority for the claim
    /// that no latches are generated, so the citation kept reading as corroboration while pointing at
    /// changed text.</para>
    ///
    /// <para>So a value here is one of two things, and <c>MirroredSignal.LatchProvenance</c> says which:
    /// the GENERATED copy layer (derived from the signal being declared transient, and readable out of the
    /// emitted IR), or a named block that latches it OUTSIDE the copy layer (taken on trust, and reported
    /// as taken on trust).</para>
    /// </summary>
    public IReadOnlyDictionary<string, string> LatchProvenance { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Block tags the binding carried <b>without stating a specification name</b> — so nothing could join
    /// them to what a vector cites.
    /// </summary>
    /// <remarks>
    /// <b>Kept rather than dropped.</b> A signal missing from the map because nobody stated its spec name
    /// and a signal missing because the copy layer does not carry it are different facts with different
    /// repairs, and collapsing them is how "the harness assumed the two names were the same" survived.
    /// </remarks>
    public IReadOnlyList<string> TagsWithNoSpecName { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 🔴 <b>Specification names declared MORE THAN ONCE with DIFFERENT modes — the multi-slot case this
    /// map has no dimension for.</b>
    ///
    /// <para>This map is keyed on the specification's signal name and has no slot axis, so a wave set in
    /// which several slots observe the same signal folds them into one entry. <b>That is correct and
    /// intended when the declarations agree</b> — and the hopper set's six slots agree exactly, which is
    /// what makes shared observation admissible at all. It is <i>not</i> correct when they differ: with a
    /// plain overwrite the last binding in the list silently decided the answer for every slot, so a
    /// vector on a slot with no latch could be admitted against a latch declared on a different slot.</para>
    ///
    /// <para><b>The resolution is the INTERSECTION, and it fails closed.</b> A mode is offered only where
    /// every declaration of that name offers it, so a vector needing <c>Latched</c> is refused rather than
    /// admitted against a slot that has none — <i>a false refusal argues and gets looked at; a false
    /// admission is a silent miss</i>. The names are listed here so the refusal is arguable rather than
    /// mysterious: the repair is to give the slots distinct specification names, or to declare the same
    /// instrumentation on each.</para>
    /// </summary>
    public IReadOnlyList<string> DivergentAcrossDeclarations { get; init; } = Array.Empty<string>();

    public bool IsEmpty => ProvidedFor.Count == 0;

    public IReadOnlySet<InstrumentationMode> For(string signal) =>
        ProvidedFor.TryGetValue(signal, out var modes) ? modes : new HashSet<InstrumentationMode>();

    public static MirrorObservability Of(params (string Signal, InstrumentationMode[] Modes)[] entries) =>
        new(entries.ToDictionary(
            e => e.Signal,
            e => (IReadOnlySet<InstrumentationMode>)e.Modes.ToHashSet(),
            StringComparer.Ordinal));

    // 🔴 *** `FromMinimalCopyLayer` WAS DELETED HERE (2026-08-14), NOT FIXED. *** It built a map by keying
    // on the BLOCK'S TAG NAME and hard-coding every signal to `{ Sampled }`. Both halves are now wrong:
    // the key is the SPECIFICATION's name (a bare tag entered a signal nobody had joined, so gate 5
    // CHECKED it instead of reporting NOT CHECKED and naming it), and the mode set is DERIVED by
    // `FromBindings`, which reads a generated latch out of `Transient` and a hand-authored one out of
    // `LatchedBy`.
    //
    // *** ITS ONLY CALLER WAS A TEST, AND THAT IS THE ARGUMENT FOR DELETING RATHER THAN REPAIRING IT. ***
    // A second, more permissive constructor that nothing in production reaches is worse than an absent
    // one: it is consulted first by whoever finds it and believed, and its permissiveness is invisible
    // because no run exercises it. The behaviour its test actually pinned - a Sampled-only map refuses a
    // Latched expectation - is covered by the test above it, which builds that map directly.

    /// <summary>
    /// 🔴 <b>THE MAP DERIVED FROM THE COORDINATOR'S BINDINGS — KEYED ON THE SPECIFICATION'S SIGNAL NAMES,
    /// AND WITH THE MODES COMPUTED RATHER THAN DECLARED.</b>
    ///
    /// <para><b>Two defects met here in one gate run</b>, both in the constructor this replaced (since
    /// deleted): it keyed on the BLOCK's tag name and hard-coded every signal to <c>Sampled</c>. A vector cites the SPECIFICATION's name, so
    /// wherever the two differ the lookup misses entirely — measured at <b>16 of 17 signals</b>, the sole
    /// success being the sole name collision. And a real, deployed hand-authored latch was structurally
    /// undeclarable, so <b>4 of 17 refusals were FALSE while 13 were correct</b>.</para>
    ///
    /// <para><b>What is DERIVED:</b> <c>Sampled</c>, for every mirrored signal, because that is what the
    /// generator emits — a result-register MOVE or COIL, with no per-signal latch and no scan stamp, both
    /// named absences in its own documentation. Nothing here takes a caller's word for it.</para>
    ///
    /// <para><b>What is STATED, and only with provenance:</b> <c>Latched</c>, and only when the binding
    /// NAMES THE BLOCK that latches the signal. The generator cannot derive that — the latch is not its
    /// output — so the alternative was leaving a deployed capability undeclarable. <b>A block name is
    /// checkable against the object set; <c>latched: true</c> would have been a caller assertion, and a
    /// caller assertion is forgotten exactly when it matters.</b></para>
    ///
    /// <para><b>A signal whose spec name the binding never stated is NOT in this map at all</b>, rather
    /// than being entered under its tag name. Entering it under the tag would be the silent identity this
    /// whole change removes; leaving it out makes gate 5 say NOT CHECKED and name it.</para>
    /// </summary>
    public static MirrorObservability FromBindings(IEnumerable<Harness.Map.MirroredSignal> signals)
    {
        ArgumentNullException.ThrowIfNull(signals);

        var provided = new Dictionary<string, IReadOnlySet<InstrumentationMode>>(StringComparer.Ordinal);
        var latchedBy = new Dictionary<string, string>(StringComparer.Ordinal);
        var unjoined = new List<string>();
        var divergent = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var signal in signals)
        {
            if (signal is null)
                continue;

            if (signal.CitableName is not { } citable)
            {
                // Recorded rather than dropped: "the binding named no spec name for this tag" is the
                // finding, and a signal that simply vanished from the map would be indistinguishable from
                // one the copy layer does not carry.
                unjoined.Add(signal.Tag);
                continue;
            }

            var modes = new HashSet<InstrumentationMode> { InstrumentationMode.Sampled };

            // *** DERIVED FOR A GENERATED LATCH, TAKEN ON TRUST FOR A HAND-AUTHORED ONE, AND THE REPORT
            // SAYS WHICH. *** A generated latch is a computed fact about the artifact this harness emits -
            // it can be read out of the generated IR - where a named block can only be believed.
            if (signal.LatchClaimed)
            {
                modes.Add(InstrumentationMode.Latched);
                latchedBy[citable] = signal.LatchProvenance!;
            }

            // *** THE SECOND AND LATER DECLARATIONS OF ONE NAME ARE INTERSECTED, NOT OVERWRITTEN. ***
            // This map has no slot axis, and a wave set may legitimately have several slots observing one
            // signal. `provided[citable] = modes` gave the LAST binding in the list the final say for
            // every slot, silently - so a Latched declared on one slot licensed a vector running on a
            // slot that has no latch. Intersecting fails closed and DivergentAcrossDeclarations names
            // what was narrowed, so the refusal can be argued with. See that property for the reasoning.
            if (provided.TryGetValue(citable, out var already))
            {
                if (!already.SetEquals(modes))
                {
                    divergent.Add(citable);
                    modes.IntersectWith(already);

                    if (!modes.Contains(InstrumentationMode.Latched))
                        latchedBy.Remove(citable);
                }
            }

            provided[citable] = modes;
        }

        return new MirrorObservability(provided)
        {
            Provenance = MapProvenance.Bindings,
            LatchProvenance = latchedBy,
            TagsWithNoSpecName = unjoined,
            DivergentAcrossDeclarations = divergent.ToArray(),
        };
    }
}

/// <summary>
/// One blacklist entry: a block that must not run concurrently with this test, and why.
///
/// <para><b>There is no removal, and that is the enforcement.</b> D22: a blacklist may only ever ADD
/// exclusions. The type carries no negation, no "allow" and no override, so an agent declaring itself
/// compatible with something the conflict graph excludes has nothing to type — the same shape as the
/// split read that cannot be named.</para>
/// </summary>
public sealed record BlacklistEntry(string Block, string Reason);

/// <summary>
/// A submission vector — <b>contract §2's shape</b>, which is a different object from
/// <c>Harness.TestVector</c>.
///
/// <para><b>The two are not rival versions of one type and reconciling them by editing either would
/// lose something.</b> <c>Harness.TestVector</c> is the RUNNER's vector: an ordered sequence of steps,
/// each with stimulus, a scan wait and expectations, executed by <c>VectorRunner</c> against an
/// <c>ITransport</c>. This is the SUBMISSION's vector: what a wave set is admitted on. They overlap in
/// three fields and disagree about everything else, including whether a vector is one stimulus or a
/// sequence — see <c>LegacyVectorAdapter</c>, which converts what can be converted and REFUSES the rest
/// by name rather than defaulting it.</para>
/// </summary>
public sealed record SubmissionVector(
    string Id,
    string Slot,
    int Index,
    AgentIdentity Author,
    Basis? Basis,
    IReadOnlyDictionary<string, string> Inputs,
    string StartBool,
    IReadOnlyList<ObservabilityDeclaration> Expectations,
    AssertionForm Form,
    // CompletionValue is below, beside MaxDuration. *** IT HAS NO DEFAULT, AND THAT IS A DEFECT FIX
    // RATHER THAN A TIGHTENING. *** SlotRun compares a result register against it and reports TIMED-OUT
    // otherwise, so a default of 1 meant a block signalling completion with a STATE NUMBER was compared
    // against a value nobody stated - and a healthy block read as never having finished. Exactly the
    // missing-predicate defect, one field over: the case the field exists for gets tested, and the case
    // where it was ignored does not. Null is REFUSED at the schema gate.
    SettlingDeclaration? Settling,
    int MaxDurationScans,
    int? CompletionValue,
    IReadOnlyList<BlacklistEntry> Blacklist,
    int CompressionFactor,
    IReadOnlyCollection<string> AssertedBehaviours,
    string CompletionSignal,
    string? Kills,
    // *** WHICH SPECIFIED BOUND THIS VECTOR WAS WRITTEN AGAINST — AMB-19. *** Bound name to the value
    // the author used, e.g. { "persistence_threshold": "T#60S" }. NULL MEANS THE VECTOR SAID NOTHING,
    // which is NOT CHECKED and never a pass: a vector that records no number can never be found stale,
    // so it survives a retune with every mechanical check still green. See BoundsCurrencyCheck for why
    // that channel exists at all — no hashed assertion text contains a numeric bound, deliberately, so
    // retuning the table moves no assertion ID and nothing downstream notices.
    IReadOnlyDictionary<string, string>? BoundsUsed = null);

/// <summary>
/// Who wrote something, for D6's independence check.
///
/// <para><b>Structured rather than a bare string, and the comparison is normalised</b> — but the
/// underlying question is not settled. D6 turns on "a different agent", and what makes two agents
/// different (session? model? worktree? role?) is <b>not defined anywhere</b>. Ordinal equality on an
/// unspecified string is a gate passed by typing a different string; normalising trim and case closes
/// the trivial variants (<c>"agent-a"</c> versus <c>"Agent-A "</c>, which an ordinal comparison
/// happily calls independent) and closes nothing else. <b>The definition needs a ruling; this only
/// stops the gate being defeated by a keystroke.</b></para>
/// </summary>
public readonly record struct AgentIdentity(string Value)
{
    public bool IsRecorded => !string.IsNullOrWhiteSpace(Value);

    /// <summary>Normalised form: trimmed and case-folded. STRICTLY TIGHTER than the ordinal comparison it replaces.</summary>
    public string Normalised => (Value ?? string.Empty).Trim();

    public bool SameAs(AgentIdentity other) =>
        string.Equals(Normalised, other.Normalised, StringComparison.OrdinalIgnoreCase);

    public override string ToString() => IsRecorded ? Value : "<unrecorded>";
}
