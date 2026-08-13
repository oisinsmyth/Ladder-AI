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
public sealed record ObservabilityDeclaration(
    string Signal,
    SignalNature Nature,
    InstrumentationMode Mode,
    int WindowScans,
    string? Expected = null);

/// <summary>What the generated copy layer actually provides, per signal (contract §4.3's "the map's observability declarations").</summary>
/// <remarks>
/// <b>The contract names this artifact and nothing in the repo was it</b> — an author could not check
/// their signal was in a list they could not find. It is a declared set built by the coordinator from
/// the copy layer it generated, and <see cref="FromMinimalCopyLayer"/> is the honest constructor for
/// what phase 2's layer actually emits.
/// </remarks>
public sealed record MirrorObservability(IReadOnlyDictionary<string, IReadOnlySet<InstrumentationMode>> ProvidedFor)
{
    public bool IsEmpty => ProvidedFor.Count == 0;

    public IReadOnlySet<InstrumentationMode> For(string signal) =>
        ProvidedFor.TryGetValue(signal, out var modes) ? modes : new HashSet<InstrumentationMode>();

    public static MirrorObservability Of(params (string Signal, InstrumentationMode[] Modes)[] entries) =>
        new(entries.ToDictionary(
            e => e.Signal,
            e => (IReadOnlySet<InstrumentationMode>)e.Modes.ToHashSet(),
            StringComparer.Ordinal));

    /// <summary>
    /// What the MINIMAL copy layer provides: <b>Sampled, and nothing else.</b>
    ///
    /// <para>Phase 2's generator emits result-register MOVEs and no per-signal latch or scan-stamp at
    /// all — those are listed in its own doc comment as deliberate absences. So a Transient or
    /// Coincidence vector against this map is refused for a TRUE, COMPUTED reason rather than because
    /// somebody passed a false. That refusal is the gate working, not a gap in it.</para>
    /// </summary>
    public static MirrorObservability FromMinimalCopyLayer(IEnumerable<string> resultSignals) =>
        new(resultSignals.ToDictionary(
            s => s,
            _ => (IReadOnlySet<InstrumentationMode>)new HashSet<InstrumentationMode> { InstrumentationMode.Sampled },
            StringComparer.Ordinal));
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
    // CompletionValue is below, beside MaxDuration. *** CONTRACT SECTION 2 HAS NO FIELD FOR IT *** -
    // it names a completion SIGNAL and never says what value on that signal means "finished". The loop
    // was assuming 1. Carrying it as data removes the assumption from the code; whether the CONTRACT
    // should state it is a spec question and is not settled here.
    SettlingDeclaration? Settling,
    int MaxDurationScans,
    int CompletionValue,
    IReadOnlyList<BlacklistEntry> Blacklist,
    int CompressionFactor,
    IReadOnlyCollection<string> AssertedBehaviours,
    string CompletionSignal,
    string? Kills);

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
