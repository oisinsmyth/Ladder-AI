namespace Harness;

/// <summary>
/// How a vector ended. Note that only <see cref="Passed"/> is good news, and that three of the five
/// are neither pass nor fail — the report must never let those collapse into either.
/// </summary>
public enum VectorOutcome
{
    /// <summary>Every assertion held.</summary>
    Passed,

    /// <summary>An assertion did not hold. The useful outcome.</summary>
    Failed,

    /// <summary>Not meaningful in this environment (a hardware vector run against a simulator).
    /// Legitimate, but it proves nothing and must be counted separately.</summary>
    Skipped,

    /// <summary>
    /// The transport cannot observe what this vector asserts on — a coincidence vector with no
    /// event scan-stamps, a transient with no latching, a scan wait with no scan counter.
    /// **This is the most dangerous outcome to mistake for a pass**, because the vector looks
    /// perfectly well-formed. It is reported loudly and it makes a run not-green.
    /// </summary>
    NotObservable,

    /// <summary>The transport threw, or a wait timed out. Nothing was proved either way.</summary>
    Errored,
}

/// <summary>One assertion's outcome.</summary>
public sealed record AssertionResult(string Tag, string Expected, string Actual, bool Held, string? Detail = null);

/// <summary>One step's outcome.</summary>
public sealed record StepResult(
    int Index,
    string? Note,
    long ScansWaited,
    IReadOnlyList<AssertionResult> Assertions)
{
    public bool AllHeld => Assertions.All(a => a.Held);
}

/// <summary>One vector's outcome.</summary>
public sealed record RunResult(
    TestVector Vector,
    VectorOutcome Outcome,
    string Message,
    IReadOnlyList<StepResult>? Steps = null)
{
    public IReadOnlyList<StepResult> StepResults => Steps ?? Array.Empty<StepResult>();

    /// <summary>The assertions that did not hold, across all steps — what a reader actually wants.</summary>
    public IReadOnlyList<AssertionResult> Failures =>
        StepResults.SelectMany(s => s.Assertions).Where(a => !a.Held).ToArray();
}

/// <summary>
/// The outcome of a whole run.
///
/// <see cref="IsGreen"/> deliberately requires that nothing was unobservable and nothing errored.
/// A suite that reports "all passed" while quietly having been unable to evaluate a third of its
/// vectors is worse than a red one, because it is trusted. Environment skips do not spoil green —
/// running the hardware subset on a simulator is a legitimate thing to do — but they are counted and
/// printed, never swallowed.
/// </summary>
public sealed record RunReport(IReadOnlyList<RunResult> Results)
{
    public int Passed => Results.Count(r => r.Outcome == VectorOutcome.Passed);
    public int Failed => Results.Count(r => r.Outcome == VectorOutcome.Failed);
    public int Skipped => Results.Count(r => r.Outcome == VectorOutcome.Skipped);
    public int NotObservable => Results.Count(r => r.Outcome == VectorOutcome.NotObservable);
    public int Errored => Results.Count(r => r.Outcome == VectorOutcome.Errored);

    public bool IsGreen => Failed == 0 && NotObservable == 0 && Errored == 0 && Passed > 0;

    public string Summary()
    {
        var parts = new List<string> { $"{Passed} passed" };
        if (Failed > 0) parts.Add($"{Failed} FAILED");
        if (NotObservable > 0) parts.Add($"{NotObservable} NOT OBSERVABLE");
        if (Errored > 0) parts.Add($"{Errored} errored");
        if (Skipped > 0) parts.Add($"{Skipped} skipped (wrong environment)");

        var verdict = IsGreen ? "GREEN" : "NOT GREEN";
        var caveat = NotObservable > 0
            ? "  <-- unobservable vectors are NOT passes; the program under test is missing the instrumentation they need"
            : string.Empty;

        return $"{verdict}: {string.Join(", ", parts)}.{caveat}";
    }
}
