namespace Converter.Trace;

// FI-25: the forward-tracer report. Facts + candidate classifications per hop — NEVER an adjudicated
// pass (the AI reviewer confirms each candidate semantically; this only reports what the reader/writer
// graph and DB start values say). One record set, two renderers.

public enum HopKind
{
    OutputPath,       // hop 1: is out_tag written anywhere?
    InterfaceChain,   // hop 2: is iface_member written anywhere?
    NumberConstraint, // hop 4: DB start value vs spec
    Timing,           // hop 5 (v2): seconds member reaches the timer's PT via the ×1000 s→ms chain
}

public enum Verdict
{
    Ok,             // the hop's expectation holds (still needs the AI's semantic confirm)
    Unimplemented,  // out_tag has no writer — no output path
    BrokenChain,    // iface_member has no writer — the in-cycle-lamp class
    Disarmed,       // every writer is placeholder-gated (NOT AlwaysTrue) — built but switched off (FI-25 v2)
    Contradicted,   // number constraint present and DB start value != spec
    Partial,        // number constraint present but member has no start value (cite the REQ's Q)
    NotApplicable,  // the binding did not name this hop's anchor (not emitted; reserved)
}

public sealed record HopResult(HopKind Hop, Verdict Verdict, string Detail, IReadOnlyList<string> Evidence);

public sealed record ReqTrace(string Req, IReadOnlyList<HopResult> Hops);

public sealed record TraceReport(IReadOnlyList<ReqTrace> Requirements, IReadOnlyList<string> Warnings)
{
    // Any non-Ok candidate — a convenience for a caller that wants to gate; the tool itself is a facts
    // provider (exit 0). None of these is an adjudicated verdict.
    public bool HasCandidateFindings =>
        Requirements.Any(r => r.Hops.Any(h => h.Verdict != Verdict.Ok));
}
