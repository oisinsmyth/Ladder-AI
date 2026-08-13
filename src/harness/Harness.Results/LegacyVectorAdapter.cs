namespace Harness.Results;

/// <summary>What a legacy <c>Harness.TestVector</c> could not supply, and why it matters.</summary>
public sealed record AdaptationGap(string Field, string Detail);

/// <summary>An adapted vector, or the reasons it could not be adapted.</summary>
public sealed record Adaptation(SubmissionVector? Vector, IReadOnlyList<AdaptationGap> Gaps)
{
    public bool Adapted => Vector is not null;
}

/// <summary>
/// <b>The reconciliation of the two vector models — and it is a mapping, not a merge.</b>
///
/// <para><c>Harness.TestVector</c> and contract §2's vector were being read as rival versions of one
/// type. They are not: they are two objects that were never distinguished.</para>
///
/// <list type="bullet">
/// <item><b><c>Harness.TestVector</c> is the RUNNER's vector.</b> An ordered sequence of steps, each
/// with stimulus, a scan wait and expectations, executed by <c>VectorRunner</c> against an
/// <c>ITransport</c>. It predates the wave model entirely and it is what the 19 tests in
/// <c>Harness.Tests</c> exercise.</item>
/// <item><b><see cref="SubmissionVector"/> is the SUBMISSION's vector.</b> What a wave set is admitted
/// on: slot, index, author, basis, observability declarations, settling, MaxDuration, blacklist,
/// compression. None of it is about execution.</item>
/// </list>
///
/// <para>Editing either into the other would lose something real. So the legacy type keeps its job, and
/// this converts what can be converted and <b>REPORTS THE REST AS GAPS BY NAME rather than defaulting
/// it</b> — a default here would be a submission that passed the schema gate on values nobody wrote.</para>
///
/// <para>🔴 <b>ONE THING IS A SPEC QUESTION AND IS NOT RESOLVED HERE.</b> Contract §2's vector is FLAT —
/// one <c>Inputs</c> map, one set of <c>Expectations</c>, one <c>MaxDuration</c>. <c>TestVector</c> is
/// STEPPED — N steps, each with its own stimulus, wait and assertions. <b>A multi-step vector has
/// several stimulus phases and the contract's start-bool, settling and <c>MaxDuration</c> model assumes
/// exactly one</b>: T=0 is the rising edge of the start bool (D37), and a second stimulus mid-test has
/// no defined relationship to it. Whether a submission vector may be stepped is a question about the
/// design, not about this code, and <see cref="Adapt"/> refuses a multi-step vector rather than
/// flattening it or picking a reading.</para>
/// </summary>
public static class LegacyVectorAdapter
{
    /// <summary>
    /// Adapt a runner vector into a submission vector, naming everything the legacy shape cannot supply.
    /// </summary>
    /// <param name="nature">
    /// The legacy <c>Observability</c> enum, mapped straight across — it is the SAME axis as
    /// <see cref="SignalNature"/>, which is why that enum exists under a name that says so.
    /// </param>
    public static Adaptation Adapt(
        string id,
        string basisText,
        SignalNature nature,
        int stepCount,
        IReadOnlyList<string> expectationSignals,
        string? kills)
    {
        var gaps = new List<AdaptationGap>();

        // *** THE SPEC QUESTION. Refused, not flattened. ***
        if (stepCount > 1)
        {
            gaps.Add(new AdaptationGap("Steps",
                $"this vector has {stepCount} steps and contract section 2's vector is FLAT — one Inputs map, one set of Expectations, one MaxDuration. T=0 is the rising edge of the start bool (D37) and a second stimulus mid-test has no defined relationship to it, so flattening would invent a semantics the design does not state. WHETHER A SUBMISSION VECTOR MAY BE STEPPED IS A SPEC QUESTION AND NEEDS A RULING."));
        }

        // Basis: a plain string on the legacy type, two required parts on the contract's. The clause
        // half may be recoverable; the ASSERTION half cannot be, and it is the decorrelating half.
        if (string.IsNullOrWhiteSpace(basisText))
        {
            gaps.Add(new AdaptationGap("Basis", "the legacy vector cites nothing."));
        }
        else
        {
            gaps.Add(new AdaptationGap("Basis.Assertion",
                $"the legacy Basis is a single string ('{basisText}'), and the contract requires a clause AND an assertion ID drawn from the spec-derived enumeration. The clause half may be this string; THE ASSERTION HALF DOES NOT EXIST IN THE LEGACY SHAPE AND CANNOT BE INFERRED — it is the decorrelating half, and inventing it here would re-create the correlated check inside the adapter."));
        }

        foreach (var field in new[]
        {
            ("Slot", "which methodology column this vector is an index of (D26a). Not a property of a step list."),
            ("Index", "its position in the slot's column."),
            ("Author", "D6's independence turns on it, and unknown is not independent."),
            ("StartBool", "the commit, and T=0. The legacy runner has no start bool at all — it writes stimulus and waits scans."),
            ("MaxDuration", "X-B's per-test timeout and DB-13's wave-length input. The legacy WaitScans is per STEP and is not the same quantity."),
            ("Settling", "phase 2's finding. The legacy runner asserts after a scan wait and has no notion of a value being final."),
            ("Blacklist", "D22's add-only exclusions."),
            ("CompressionFactor", "the comp the scan counts are stated at. Without it the observability check is sound at authoring time and void at run time."),
            ("CompletionSignal", "what the poll watches, and what the settling declaration is checked against."),
        })
        {
            gaps.Add(new AdaptationGap(field.Item1, field.Item2));
        }

        if (string.IsNullOrWhiteSpace(kills))
        {
            gaps.Add(new AdaptationGap("Kills",
                "no mutant declared. The legacy type CARRIES this field and contract section 2 omits it — section 10 requires mutation testing and this is the only mechanism for it that exists, so the omission is raised rather than resolved."));
        }

        // Nothing is fabricated. The adapter reports what it would need; it does not produce a
        // half-populated SubmissionVector that would then pass a schema gate on values nobody wrote.
        return new Adaptation(null, gaps);
    }

    /// <summary>
    /// The mapping between the two observability axes, as a single statement.
    ///
    /// <para>The legacy enum is a <b>signal nature</b> and the contract's modes are <b>instrumentation
    /// applied</b>. They were two enums with overlapping names and nothing between them; the relation is
    /// <see cref="ObservabilityCheck.ModesThatCanAnswer"/>, and it is a SUFFICIENCY relation rather than
    /// a translation — a nature does not become a mode, it constrains which modes could answer.</para>
    /// </summary>
    public static SignalNature NatureOf(int legacyObservability) => legacyObservability switch
    {
        0 => SignalNature.PersistentState,
        1 => SignalNature.Transient,
        2 => SignalNature.Coincidence,
        _ => throw new ArgumentOutOfRangeException(nameof(legacyObservability), legacyObservability,
            "the legacy Observability enum has three members; a fourth would need a row in ModesThatCanAnswer before it could be admitted."),
    };
}
