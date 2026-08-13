namespace Harness;

/// <summary>
/// What kind of thing a vector asserts on — and therefore what the program under test must provide
/// before the assertion can be believed.
///
/// This is not documentation. The runner refuses to run a vector whose observability the transport
/// cannot support, because the alternative is a green result that means nothing.
///
/// MEASURED ON THE RIG, 2026-08-12, and the numbers this comment used to carry (~100 ms poll against
/// a ~10 ms scan, "roughly ten times") were all wrong: the scan is 23.33 ms under load, the Modbus
/// round trip is 78 ms typical and 173 ms at the p99, and A POLL IS ONE ROUND TRIP - there is no
/// separate poll period to tune. So a one-scan event sits ~3.3 scans below the sampler typically and
/// ~7.4 at the p99, with 3.0 the irreducible floor at the fastest median observed. The floor is
/// SMALLER than was believed, and it is a distribution rather than a number. No polling rate
/// recovers a one-scan event, which is the part that was right and is why this enum exists.
/// The single source for these constants is the spec's section 12a; do not re-choose them here.
/// </summary>
public enum Observability
{
    /// <summary>
    /// The asserted condition persists — a valve's commanded state through a phase, a latched weight,
    /// a computed setpoint, a raised alarm. Readable at any sane rate. Most vectors are this.
    /// </summary>
    PersistentState,

    /// <summary>
    /// The condition is momentary — a coil true for one scan. Needs the program to LATCH it (a sticky
    /// bit the harness reads then clears), otherwise the sampler will miss it, and worse, will miss it
    /// intermittently.
    /// </summary>
    Transient,

    /// <summary>
    /// The assertion is about two things happening in the SAME scan — "the drain valve opened and the
    /// drain timer started together". Unobservable by sampling at any rate. Needs the program to
    /// record the scan number at which each event occurred, so the assertion becomes a comparison of
    /// two persistent integers.
    /// </summary>
    Coincidence,
}

/// <summary>Where a vector is meaningful. Running one in the wrong environment proves nothing.</summary>
public enum TargetEnvironment
{
    /// <summary>Real hardware only — real timing, real retentive semantics, real counters.</summary>
    Hardware,

    /// <summary>Simulator only.</summary>
    Simulator,

    /// <summary>Meaningful in either.</summary>
    Both,
}

/// <summary>One value written to the device as stimulus.</summary>
/// <param name="Area">The named area (a DB or region). Also what the write fence is scoped on.</param>
public sealed record TagWrite(string Area, string Tag, string Value);

/// <summary>One thing read back and compared.</summary>
/// <param name="Tolerance">
/// For real-valued comparisons. Null means exact string comparison — which is what you want for
/// booleans, states and enumerations, and what you must NOT use for floats.
/// </param>
public sealed record Expectation(string Tag, string Expected, double? Tolerance = null);

/// <summary>
/// One step: apply stimulus, let N scans elapse, then assert.
/// </summary>
/// <param name="WaitScans">
/// Scans to let pass before asserting. This is OBSERVED, not commanded — an S7-1200 cannot be
/// stepped, so the runner watches a free-running scan counter in the program and waits for it to
/// advance. Zero means assert immediately after the write.
/// </param>
public sealed record VectorStep(
    string? Note = null,
    IReadOnlyList<TagWrite>? Stimulus = null,
    int WaitScans = 0,
    IReadOnlyList<Expectation>? Expect = null)
{
    public IReadOnlyList<TagWrite> Writes => Stimulus ?? Array.Empty<TagWrite>();
    public IReadOnlyList<Expectation> Assertions => Expect ?? Array.Empty<Expectation>();
}

/// <summary>
/// A declarative conformance vector: initial state, per-step stimulus, expected observations after
/// N scans — the shape `docs/08-testing-strategy.md` specifies, with three fields added from what
/// building the first real vector set taught us.
///
/// <para><b>Basis</b> is required. A vector that cannot cite the specification clause it comes from
/// is not admissible, because the only vectors worth trusting are the ones derived from the spec
/// rather than from the code. A suite whose expectations were read off the implementation is a
/// change detector, not a correctness check — that is the correlated-check failure this project
/// exists to avoid, and the citation is what keeps the two kinds distinguishable.</para>
///
/// <para><b>Observability</b> decides whether the assertion is believable at all — see the enum.</para>
///
/// <para><b>Kills</b> is the plausible wrong implementation this vector is designed to catch. A
/// vector that no credible wrong implementation would fail only measures uptime.</para>
/// </summary>
public sealed record TestVector(
    string Id,
    string Title,
    string Basis,
    Observability Observability = Observability.PersistentState,
    TargetEnvironment Environment = TargetEnvironment.Both,
    string? Kills = null,
    IReadOnlyList<VectorStep>? Steps = null)
{
    public IReadOnlyList<VectorStep> Sequence => Steps ?? Array.Empty<VectorStep>();

    /// <summary>Every distinct area this vector writes. Feeds the write fence's per-run scope.</summary>
    public IReadOnlyList<string> AreasWritten =>
        Sequence.SelectMany(s => s.Writes)
                .Select(w => w.Area?.Trim() ?? string.Empty)
                .Where(a => a.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(a => a, StringComparer.OrdinalIgnoreCase)
                .ToArray();

    /// <summary>True when the vector never writes — a pure observation vector.</summary>
    public bool IsReadOnly => AreasWritten.Count == 0;
}

/// <summary>Helpers over a set of vectors about to be run together.</summary>
public static class VectorSet
{
    /// <summary>
    /// The areas a run will write, derived from the vectors themselves.
    ///
    /// This is deliberately computed rather than hand-declared. The write fence scopes per RUN
    /// because the writable surface is a property of what a test needs, not of the device — so
    /// deriving it from the vector set makes the declaration exactly what the tests touch, and keeps
    /// it honest as vectors are added or removed. Hand-maintained scope lists drift wide.
    ///
    /// Returned as plain strings rather than a DeviceGuard type on purpose: the harness has no
    /// reference to the fence, so neither can drag the other's dependencies around. The composition
    /// root turns this into the guard's scope.
    /// </summary>
    public static IReadOnlyList<string> DeclaredAreas(IEnumerable<TestVector> vectors) =>
        (vectors ?? Array.Empty<TestVector>())
            .SelectMany(v => v.AreasWritten)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(a => a, StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
