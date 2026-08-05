namespace Converter.SignalSweep;

// How a swept signal is accounted for. "Accounted for" is deliberately weaker than "correctly handled":
// this check computes coverage, never correctness — a signal named in a spec might still be bound to the
// wrong requirement, which is candidate-scan's question, not this one.
public enum Accounting
{
    ClaimedBySpec,   // named in an equipment spec or the derived register
    Disposed,        // listed in the residual sweep's per-DB disposition tables (bound / out-of-scope / safety / unclaimed)
    Unaccounted,     // in neither — nobody has said anything about this signal at all
}

// What kind of container a swept signal was declared in. Kept explicit because the two are shaped
// differently — a DB member's path is qualified, a tag's is bare — and a report that called a tag
// table "a DB" rendered N flat tags as N one-row DBs (FI-45 item 2).
public enum SignalContainerKind
{
    Db,
    TagTable,
}

public sealed record SweptSignal(string Path, string Container, SignalContainerKind Kind, Accounting Accounting);

public sealed record ContainerBreakdown(
    string Container,
    SignalContainerKind Kind,
    int Swept,
    int Claimed,
    int Disposed,
    int Unaccounted);

public sealed record SignalSweepReport(
    string ProjectDir,
    int FilesScanned,
    IReadOnlyList<SweptSignal> Signals,
    IReadOnlyList<ContainerBreakdown> ByContainer,
    bool DispositionTableRead,
    IReadOnlyList<string> Warnings)
{
    public int Swept => Signals.Count;

    public int Claimed => Signals.Count(s => s.Accounting == Accounting.ClaimedBySpec);

    public int Disposed => Signals.Count(s => s.Accounting == Accounting.Disposed);

    public IReadOnlyList<SweptSignal> Unaccounted =>
        Signals.Where(s => s.Accounting == Accounting.Unaccounted).ToList();

    // A signal nobody has said anything about — not bound, not dispositioned, not even excluded. That is
    // a hard coverage fact, not a judgement about whether it "implies control": deciding that is the
    // engineer's, and a tool that guessed would be inventing a verdict.
    public bool HasFindings => Unaccounted.Count > 0;
}

public sealed class SignalSweepFormatException : Exception
{
    public SignalSweepFormatException(string message)
        : base(message)
    {
    }
}
