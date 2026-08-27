namespace Converter.HarnessBinding;

// WHICH SIDE OF THE WIRE A BLOCK PUTS THE HARNESS ON. Declared by the caller, never inferred: a
// corpus cannot say which of its blocks is the stimulus model and which is the thing under test —
// both are ordinary FBs — and guessing it would decide, from nothing, which signals a harness is
// allowed to DRIVE. That is the one axis where a wrong answer writes to the plant.
public enum BindingRole
{
    // The stimulus head. Its `read` members are what the harness must drive, because nothing else
    // drives them; its `written` members are its own progress reporting.
    Stimulus,

    // The block under test, or an observation block sitting beside it. CONTRIBUTES OBSERVATIONS ONLY.
    // Its `read` members are driven by the STIMULUS MODEL, not by the harness — a harness that drove
    // them would be testing its own arithmetic. They are excluded, and counted, never dropped.
    Observed,
}

// Why one candidate did not become an entry. Every value is REPORTED with the signal that earned it:
// a scaffold that silently shortens its own output produces a binding that looks complete, and the
// missing rows are exactly the ones nothing else will mention (the argument SignalDeclaration.
// Undeclared already makes one layer down).
public enum ExclusionReason
{
    // A member of an OBSERVED block that the block reads. The stimulus model drives it.
    ObservedInput,

    // Read and written by the same block. NEVER a vector target — the harness would contend with the
    // block's own coil, and `writers` is the evidence that it would. Still offered as an observation:
    // reading is non-destructive.
    ReadWriteContention,

    // Declared on the interface and neither read nor written anywhere in the corpus.
    Unused,

    // The origin/scope filters the caller supplied did not admit it.
    OutOfScope,
}

// A signal the scaffold could not carry, stated as a REFUSAL rather than an omission. The distinction
// is the whole point: an omitted row reads as "the block does not have that signal", a refusal names
// the signal and says what is missing and who supplies it.
public sealed record BindingRefusal(string Subject, string Detail);

// ONE HOLE THE CORPUS CANNOT FILL. Each of these is a claim about the PLANT or about a SPECIFICATION,
// and a scaffold that supplies one has invented the fact the whole pipeline exists to keep out.
//
// 🔴 THE HOLES ARE EMITTED INTO THE DOCUMENT UNDER A NON-ANNOTATION KEY, DELIBERATELY. Gate 0b splits
// a binding's unmapped fields on the leading underscore (`SubmissionDocument.Split`): `_note` is an
// annotation and is counted, anything else is UNKNOWN and is REFUSED. So `unresolvedHoles` makes the
// scaffold fail closed — the document cannot reach a run until a human has removed the key, and
// removing it is only sane after filling the fields it names. A comment would have been skimmed;
// this one is a gate.
//
// ONE HOLE PER FIELD, carrying every path it applies to — not one hole per signal per field. 27
// signals needing a `specName` is ONE decision made 27 times, and repeating the whole explanation
// beside each of them buries the other three claims in a list nobody finishes reading.
public sealed record BindingHole(
    string Field,
    IReadOnlyList<string> Paths,
    string Missing,
    string ResolvedBy);

// One derived signal row. Carries ONLY what the corpus states. `SpecName`, `Encoding`, `InertRest`
// have no field here at all rather than a null one — there is nothing to hold, and a nullable
// property is an invitation to fill it in from somewhere.
public sealed record DerivedSignal(
    string Tag,
    string MirrorType,
    string IrType,
    int RegisterWidth,
    // Non-null ONLY when the corpus PROVES a latch: a Set or Reset coil, and every writer in one
    // block. A plain COIL is not a latch however it is sealed, and `latchedBy` absent is itself the
    // claim "this binding claims no latch" — so it is never written speculatively.
    string? LatchedBy,
    string LatchEvidence,
    IReadOnlyList<string> Writers,
    IReadOnlyList<string> Readers);

public sealed record ExcludedSignal(string Path, string IrType, string Direction, ExclusionReason Reason);

// WHY THE SET IS EMPTY, in the vocabulary every mechanical-floor command in this converter uses.
// Three doors, and they need different words because they need different actions.
public enum BindingScope
{
    Derived,            // a real result
    UnknownBlock,       // --stimulus or --observe names no block in this corpus
    NothingInScope,     // the blocks exist; the scope/origin filters left nothing
}

public sealed record HarnessBindingReport(
    string ProjectDir,
    int FilesScanned,
    string StimulusBlock,
    string StimulusInstance,
    IReadOnlyList<string> ObservedBlocks,
    IReadOnlyList<string> Scopes,
    string SlotId,
    IReadOnlyList<DerivedSignal> VectorTargets,
    IReadOnlyList<DerivedSignal> ResultSources,
    IReadOnlyList<ExcludedSignal> Excluded,
    IReadOnlyList<BindingRefusal> Refusals,
    IReadOnlyList<BindingHole> Holes,
    IReadOnlyList<string> Warnings,
    // From `converter served-area` — the SAME derivation, not a second one, so the two commands
    // cannot disagree about the window this binding is allocated in.
    int? ServedBaseByte,
    int? ServedRegisters,
    string ServedDenominator,
    BindingScope Scope = BindingScope.Derived)
{
    // The width the derived rows actually need, so a caller can see the fit BEFORE a map is
    // allocated. It is REPORTED beside the served width and never substituted for it: matching
    // BatchPlanner, where a derived number that quietly replaced an authored one would hide which of
    // the two is stale.
    public int RequiredRegisters =>
        VectorTargets.Sum(s => s.RegisterWidth) + ResultSources.Sum(s => s.RegisterWidth);

    // 🔴 "EMPTY IS NOT CLEAN", keyed on the ROW COUNT as well as on the scope enum — undriven-scan's
    // lesson, where a third shape of empty arrived three weeks after the first two were called
    // complete. The enum is a catalogue of causes somebody thought of; the count is the fact.
    public bool ExaminedNothing =>
        Scope is not BindingScope.Derived || (VectorTargets.Count == 0 && ResultSources.Count == 0);

    // A corpus file would not parse, so a member may be missing and a referenced path may be
    // misclassified. EXIT-BEARING, following signal-set: the consumer of this document is a person
    // filling in a binding, and an incompleteness that only warns is one they read past.
    public bool Partial => Warnings.Count > 0;

    // NOTHING MAY BE USED FROM A REFUSED SCAFFOLD — CopyLayerResult's rule, and for its reason: a
    // partially-emitted binding is the shape that looks finished.
    public bool Refused => Refusals.Count > 0;
}
