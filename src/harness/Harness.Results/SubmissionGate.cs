using System.Text.RegularExpressions;

namespace Harness.Results;

/// <summary>
/// The three outcomes per gate, and they may never be collapsed into two.
/// </summary>
public enum GateStatus
{
    /// <summary>A verifier ran against this submission's actual content. A result.</summary>
    Checked,

    /// <summary>No verifier can exist for this; an independent reader decided. A result, LABELLED.</summary>
    Judgement,

    /// <summary>
    /// No verifier exists yet, or the input it needs is absent. <b>NOT A RESULT</b> — and it fails
    /// closed, with no flag that relaxes it.
    /// </summary>
    NotChecked,
}

/// <summary>One gate's outcome, with the verifier that produced it named.</summary>
public sealed record GateResult(string Gate, GateStatus Status, bool Passed, string Verifier, string Detail);

/// <summary>The submission's verdict. <b>There is deliberately no plain "ADMISSIBLE".</b></summary>
public enum SubmissionVerdict
{
    /// <summary>Every mechanical gate ran and passed. Judgement gates remain, and they are named.</summary>
    AdmissibleSubjectToJudgement,

    /// <summary>A gate ran and refused, or a gate could not be run at all.</summary>
    NotAdmissible,

    /// <summary>There was nothing to gate. Empty is not clean.</summary>
    NothingExamined,
}

/// <summary>The whole report for one submission.</summary>
public sealed record SubmissionReport(IReadOnlyList<GateResult> Gates, int VectorsExamined)
{
    public SubmissionVerdict Verdict =>
        VectorsExamined == 0 || Gates.Count == 0 ? SubmissionVerdict.NothingExamined
        : Gates.Any(g => g.Status == GateStatus.NotChecked) ? SubmissionVerdict.NotAdmissible
        : Gates.Any(g => g.Status == GateStatus.Checked && !g.Passed) ? SubmissionVerdict.NotAdmissible
        : SubmissionVerdict.AdmissibleSubjectToJudgement;

    /// <summary>Gates that could not be run. This is the harness lane's build list.</summary>
    public IReadOnlyList<GateResult> NotChecked => Gates.Where(g => g.Status == GateStatus.NotChecked).ToArray();

    /// <summary>Gates that ran and refused.</summary>
    public IReadOnlyList<GateResult> Refused => Gates.Where(g => g.Status == GateStatus.Checked && !g.Passed).ToArray();

    /// <summary>Gates nothing can ever verify, listed so they are not mistaken for verified ones.</summary>
    public IReadOnlyList<GateResult> Judgements => Gates.Where(g => g.Status == GateStatus.Judgement).ToArray();
}

/// <summary>
/// Contract §10's enforcement surface, in the order a submission meets it — <b>as runnable checks.</b>
///
/// <para><b>A gate nobody can run is not a gate, and saying it passed is worse than saying nothing.</b>
/// Every gate below reports which of the three outcomes it reached and which verifier produced it, so a
/// report cannot claim a check that did not happen.</para>
/// </summary>
public static class SubmissionGate
{
    private static readonly Regex AbsoluteAddress = new(@"^%[A-Za-z]{0,2}\d+(\.\d+)?$", RegexOptions.Compiled);

    /// <summary>
    /// A REAL, computed observability report over a trivially supportable expectation.
    ///
    /// <para>The per-gate delegations below reuse <c>Admissibility.Check</c>, which composes every gate;
    /// each call needs a value for the gates it is not asking about. This is deliberately a report
    /// PRODUCED BY the checker rather than a fabricated "supported" — there is no way to fabricate one,
    /// which is the property that made observability a computation in the first place.</para>
    /// </summary>
    private static readonly ObservabilityReport Passing = ObservabilityCheck.Evaluate(
        new[] { new ObservabilityDeclaration("<gate-local>", SignalNature.PersistentState, InstrumentationMode.Latched, 0) },
        AssertionForm.When,
        MirrorObservability.Of(("<gate-local>", new[] { InstrumentationMode.Latched })),
        floorScans: 1, declaredCompression: 1, runtimeCompression: 1);

    /// <summary>Run every gate over one submission.</summary>
    /// <param name="conflicts">
    /// The computed disjointness graph (D9), <b>with X-G's provenance on every edge</b>. <b>Null means the
    /// graph was not available</b>, which makes the blacklist gate NOT CHECKED rather than passed — a
    /// blacklist compared against an absent graph is a blacklist nobody checked. A graph built by
    /// <see cref="ConflictGraph.WithoutProvenance"/> is available but unprovenanced, which is a different
    /// and equally reportable state.
    /// </param>
    public static SubmissionReport Check(
        IReadOnlyList<SubmissionVector> vectors,
        AssertionEnumeration enumeration,
        FidelityDeclaration? fidelity,
        AgentIdentity blockAuthor,
        MirrorObservability map,
        double floorScans,
        int runtimeCompression,
        ConflictGraph? conflicts,
        BlockCompressionInputs? compressionInputs = null)
    {
        ArgumentNullException.ThrowIfNull(vectors);
        ArgumentNullException.ThrowIfNull(enumeration);
        ArgumentNullException.ThrowIfNull(map);

        var gates = new List<GateResult>();

        if (vectors.Count == 0)
        {
            gates.Add(new GateResult("0 submission", GateStatus.Checked, false, nameof(SubmissionGate),
                "the submission contains no vectors. Empty is not clean: a submission with nothing in it cannot be admitted, and reporting it as admissible would be the purest form of a gate that passed without examining anything."));
            return new SubmissionReport(gates, 0);
        }

        gates.Add(Schema(vectors));
        gates.Add(Authorship(vectors, blockAuthor));
        gates.Add(BasisGate(vectors, enumeration));
        gates.Add(EnumeratorIndependence(vectors, enumeration, blockAuthor));
        gates.Add(AssertionFormAuthority(vectors, enumeration));
        gates.Add(new GateResult("3c basis — faithful reading of the clause", GateStatus.Judgement, true, "none, ever",
            "whether the cited assertion is a faithful reading of the clause is what the independent author is for. It is recorded, never verified."));
        gates.Add(Fidelity(vectors, fidelity));
        gates.Add(Observability(vectors, enumeration, map, floorScans, runtimeCompression));
        gates.Add(Settling(vectors));
        gates.Add(new GateResult("6b settling — does the condition imply the value is final", GateStatus.Judgement, true, "none, ever",
            "whether the declared settling condition really implies finality is judgement, informed by the model's fidelity declaration."));
        gates.Add(StartBool(vectors));
        gates.Add(Blacklist(vectors, conflicts));
        gates.Add(new GateResult("8b blacklist — over-broad?", GateStatus.Judgement, true, "density, reported not gated",
            $"blacklist density is {vectors.Sum(v => v.Blacklist.Count)} entr(ies) across {vectors.Count} vector(s). Over-blacklisting is measurable and not preventable."));
        gates.Add(MultiWriterProvenance(conflicts));
        gates.Add(LivenessPreconditions(vectors, map));
        gates.Add(CompressionCeiling(vectors, floorScans, runtimeCompression));
        gates.Add(CompressionBoundsNotInTheSubmission(vectors, runtimeCompression, floorScans, compressionInputs));

        return new SubmissionReport(gates, vectors.Count);
    }

    // -------------------------------------------------------------------------------------------------
    // 1 — schema
    // -------------------------------------------------------------------------------------------------

    private static GateResult Schema(IReadOnlyList<SubmissionVector> vectors)
    {
        var problems = new List<string>();

        foreach (var v in vectors)
        {
            var label = string.IsNullOrWhiteSpace(v.Id) ? "<unnamed vector>" : v.Id;

            if (string.IsNullOrWhiteSpace(v.Id)) problems.Add($"{label}: Id is empty.");
            if (string.IsNullOrWhiteSpace(v.Slot)) problems.Add($"{label}: Slot is empty.");
            if (v.Index < 0) problems.Add($"{label}: Index is negative.");
            if (string.IsNullOrWhiteSpace(v.StartBool)) problems.Add($"{label}: StartBool is empty.");
            if (string.IsNullOrWhiteSpace(v.CompletionSignal)) problems.Add($"{label}: CompletionSignal is empty.");
            if (v.CompressionFactor < 1) problems.Add($"{label}: CompressionFactor is {v.CompressionFactor}. Scan counts are meaningless without the comp they were stated at.");

            // MaxDuration is not a formality: X-B makes it the per-test timeout and DB-13 needs it for
            // wave length, so a vector without one can neither be packed nor bounded.
            if (v.MaxDurationScans < 1)
                problems.Add($"{label}: MaxDuration is {v.MaxDurationScans} scans. X-B makes it the per-test timeout and DB-13 needs it for wave length — a vector without one can neither be packed nor bounded.");

            if (v.Expectations.Count == 0)
                problems.Add($"{label}: no Expectations. A vector that asserts nothing cannot fail, so its pass says nothing.");

            // *** THE PREDICATE. *** Contract section 2 lists one and ObservabilityDeclaration.Expected
            // carries it, and until now NOTHING CHECKED IT — an expectation with no predicate reached the
            // result package, where a null becomes the string "<no predicate>" and is compared against the
            // observed value. That yields a FAILED on a vector that should have been REFUSED: the author is
            // told the block is wrong when what is wrong is that nobody said what right looks like.
            foreach (var e in v.Expectations.Where(e => string.IsNullOrWhiteSpace(e.Expected)))
            {
                problems.Add($"{label}: expectation '{e.Signal}' declares no expected value. An expectation with nothing to compare against cannot fail, so its pass says nothing — and it does not become an error, it becomes a spurious disagreement against a placeholder.");
            }

            // Kills is in the code and absent from contract section 2's format. Section 10 requires
            // mutation testing and this is the only mechanism for it that exists, so it is required here
            // and the discrepancy is raised rather than silently resolved.
            if (string.IsNullOrWhiteSpace(v.Kills))
                problems.Add($"{label}: no Kills. A vector that no credible wrong implementation would fail only measures uptime. (Contract section 2 omits this field; section 10 requires mutation testing and this is the only mechanism for it — raised as a discrepancy, not resolved.)");
        }

        // The completion condition is REPORTED even on a pass, because contract section 2 defines no
        // completion VALUE at all - it names a signal and stops. A field the contract does not define is
        // one an author cannot check against anything, so the gate says what it read rather than leaving
        // the reader to assume the harness's convention of 1.
        var completions = string.Join(", ", vectors
            .Select(v => $"{v.CompletionSignal} reads {v.CompletionValue}")
            .Distinct(StringComparer.Ordinal));

        return new GateResult("1 schema", GateStatus.Checked, problems.Count == 0, nameof(SubmissionGate),
            problems.Count == 0
                ? $"{vectors.Count} vector(s), every contract section 2 field present and typed. Completion condition(s): {completions} — section 2 names a completion SIGNAL and states no VALUE, so this is reported rather than assumed."
                : string.Join(" | ", problems));
    }

    // -------------------------------------------------------------------------------------------------
    // 2 — authorship (D6)
    // -------------------------------------------------------------------------------------------------

    private static GateResult Authorship(IReadOnlyList<SubmissionVector> vectors, AgentIdentity blockAuthor)
    {
        var problems = new List<string>();

        if (!blockAuthor.IsRecorded)
            problems.Add("the block's author is not recorded, so D6's independence cannot be established. Unknown is not independent.");

        foreach (var v in vectors)
        {
            if (!v.Author.IsRecorded)
                problems.Add($"{v.Id}: the vector's author is not recorded.");
            else if (blockAuthor.IsRecorded && v.Author.SameAs(blockAuthor))
                problems.Add($"{v.Id}: '{v.Author}' wrote both the block and the vector. That is a correlated check and it is a refusal, not a warning.");
        }

        return new GateResult("2 authorship (D6)", GateStatus.Checked, problems.Count == 0, nameof(AgentIdentity),
            problems.Count == 0
                ? $"vector author(s) differ from the block author '{blockAuthor}' under a normalised comparison. NOTE: what MAKES two agents different is undefined — see AgentIdentity."
                : string.Join(" | ", problems));
    }

    // -------------------------------------------------------------------------------------------------
    // 3 — basis, 4 — fidelity, 6 — settling: delegated to Admissibility, which already computes them
    // -------------------------------------------------------------------------------------------------

    private static GateResult BasisGate(IReadOnlyList<SubmissionVector> vectors, AssertionEnumeration enumeration)
    {
        var problems = vectors
            .SelectMany(v => Admissibility.Check(v.Basis, enumeration, FidelityDeclaration.Of("<n/a>", new[] { "x" }, null, true),
                    Array.Empty<string>(), new SettlingDeclaration("n/a", Array.Empty<string>()), v.CompletionSignal,
                    new AgentIdentity("a"), new AgentIdentity("b"), Passing)
                .Refusals
                .Where(r => r.Reason is RefusalReason.BasisNotCited or RefusalReason.ClauseNotEnumerated
                                     or RefusalReason.AssertionNotEnumerated or RefusalReason.EnumerationEmpty)
                .Select(r => $"{v.Id}: {r.Reason} — {r.Detail}"))
            .ToArray();

        return new GateResult("3 basis — clause AND assertion", GateStatus.Checked, problems.Length == 0, nameof(Admissibility),
            problems.Length == 0 ? $"every citation resolves into an enumeration of {enumeration.Assertions.Count} assertion(s)." : string.Join(" | ", problems));
    }

    private static GateResult Fidelity(IReadOnlyList<SubmissionVector> vectors, FidelityDeclaration? fidelity)
    {
        var problems = vectors
            .SelectMany(v => Admissibility.Check(new Basis("c", "a"), AssertionEnumeration.Of(new[] { "c" }, new[] { "a" }),
                    fidelity, v.AssertedBehaviours, new SettlingDeclaration("n/a", Array.Empty<string>()), v.CompletionSignal,
                    new AgentIdentity("a"), new AgentIdentity("b"), Passing)
                .Refusals
                .Where(r => r.Reason is RefusalReason.FidelityExceeded or RefusalReason.FidelityUnusable or RefusalReason.NothingExamined)
                .Select(r => $"{v.Id}: {r.Reason} — {r.Detail}"))
            .ToArray();

        return new GateResult("4 fidelity (M4)", GateStatus.Checked, problems.Length == 0, nameof(Admissibility),
            problems.Length == 0 ? $"every asserted behaviour is in model '{fidelity?.ModelId}'s Represents set." : string.Join(" | ", problems));
    }

    private static GateResult Settling(IReadOnlyList<SubmissionVector> vectors)
    {
        var problems = vectors
            .SelectMany(v => Admissibility.Check(new Basis("c", "a"), AssertionEnumeration.Of(new[] { "c" }, new[] { "a" }),
                    FidelityDeclaration.Of("<n/a>", new[] { "x" }, null, true), Array.Empty<string>(),
                    v.Settling, v.CompletionSignal, new AgentIdentity("a"), new AgentIdentity("b"), Passing)
                .Refusals
                .Where(r => r.Reason is RefusalReason.SettlingNotDeclared or RefusalReason.SettlingIsTheCompletionFlag)
                .Select(r => $"{v.Id}: {r.Reason} — {r.Detail}"))
            .ToArray();

        return new GateResult("6 settling", GateStatus.Checked, problems.Length == 0, nameof(Admissibility),
            problems.Length == 0 ? "every vector declares a settling condition that is not the completion flag alone." : string.Join(" | ", problems));
    }

    /// <summary>
    /// The enumeration is the coverage DENOMINATOR, so who produced it decides whether citing into it
    /// buys anything.
    ///
    /// <para><b>If the block's author performed the decomposition, D6's independence is lost at the
    /// denominator</b> - the same reading that produced the block produced the set of things anyone may
    /// assert about it, and the vector author's citation stops being a second reading. An UNRECORDED
    /// enumerator is NOT CHECKED, never a pass: unknown is not independent.</para>
    /// </summary>
    private static GateResult EnumeratorIndependence(IReadOnlyList<SubmissionVector> vectors, AssertionEnumeration enumeration, AgentIdentity blockAuthor)
    {
        if (!enumeration.Enumerator.IsRecorded)
        {
            return new GateResult("3d enumerator independence", GateStatus.NotChecked, false, "the enumeration's own identity field",
                "the enumeration does not record who produced it, so it cannot be shown independent of the block's author. If the block's author decomposed the requirement, D6's independence is lost AT THE DENOMINATOR and citing into it buys nothing. Unknown is not independent.");
        }

        var problems = new List<string>();

        if (blockAuthor.IsRecorded && enumeration.Enumerator.SameAs(blockAuthor))
            problems.Add($"'{enumeration.Enumerator}' both wrote the block and enumerated its assertions. The denominator is then the block author's own reading of the requirement, and a vector citing into it is agreeing with the block by construction.");

        foreach (var v in vectors.Where(v => v.Author.IsRecorded && enumeration.Enumerator.SameAs(v.Author)))
            problems.Add($"{v.Id}: '{enumeration.Enumerator}' both enumerated the assertions and wrote this vector. The enumeration is meant to be a THIRD party to both authors.");

        return new GateResult("3d enumerator independence", GateStatus.Checked, problems.Count == 0, nameof(AgentIdentity),
            problems.Count == 0
                ? $"the enumeration was produced by '{enumeration.Enumerator}', who is neither the block's author nor any vector's."
                : string.Join(" | ", problems));
    }

    /// <summary>
    /// <b>Where F-3 gets its authority.</b>
    ///
    /// <para>F-3 refuses a SAMPLED observation of a NEVER assertion, and the form was declared by the
    /// VECTOR - so the ruling was enforced against what an author claimed, and an author who cited a
    /// NEVER and declared WHEN took the permissive path. An enumeration that carries the form lets the
    /// two be compared.</para>
    ///
    /// <para><b>An enumeration carrying no forms is NOT CHECKED, not a pass.</b> That is the flat
    /// projection, and against it the hole is exactly as open as it was - reporting it as verified would
    /// be the failure this whole gate table exists to prevent.</para>
    /// </summary>
    private static GateResult AssertionFormAuthority(IReadOnlyList<SubmissionVector> vectors, AssertionEnumeration enumeration)
    {
        if (enumeration.CarriesNoForms)
        {
            return new GateResult("3e assertion form authority", GateStatus.NotChecked, false, "per-assertion form in the enumeration",
                "the enumeration is the flat projection (clause and assertion IDs only) and carries no canonical form, so a vector's declared form was compared against nothing. F-3's refusal of a SAMPLED NEVER is therefore enforced against WHAT THE VECTOR CLAIMS: cite a NEVER, declare WHEN, take the permissive path.");
        }

        var problems = new List<string>();

        foreach (var v in vectors)
        {
            if (v.Basis is null)
                continue;

            var declared = v.Form;
            var enumerated = enumeration.FormOf(v.Basis.AssertionId);

            if (enumerated is null)
            {
                problems.Add($"{v.Id}: the enumeration carries forms but none for '{v.Basis.AssertionId}', so this citation's form could not be checked. A partially-formed enumeration is not a permissive one.");
            }
            else if (enumerated == AssertionForm.Unstated)
            {
                problems.Add($"{v.Id}: the enumeration lists '{v.Basis.AssertionId}' with an UNSTATED form. The form is the enumeration's to state, and a blank there is not a WHEN - it is a decomposition that has not been finished. F-3 cannot be enforced against it.");
            }
            else if (declared == AssertionForm.Unstated)
            {
                problems.Add($"{v.Id}: declares no assertion form. *** A DROPPED FORM FAILS THE SAME COMPARISON AS A WRONG ONE, DELIBERATELY *** - the form decides whether a SAMPLED observation is admissible (F-3), so a field that defaulted to WHEN would hand every author who omitted it the permissive path. The enumeration says '{v.Basis.AssertionId}' is {enumerated}; declare it.");
            }
            else if (enumerated != declared)
                problems.Add($"{v.Id}: declares form {declared} and the enumeration says '{v.Basis.AssertionId}' is {enumerated}. The assertion's form is the ENUMERATION's to state; a vector that disagrees with it is asserting something other than what it cites - and if the disagreement is Never-declared-as-When it is F-3's refusal being walked around.");
        }

        return new GateResult("3e assertion form authority", GateStatus.Checked, problems.Count == 0, nameof(AssertionEnumeration),
            problems.Count == 0
                ? "every citation's declared form matches the enumeration's, so F-3 is enforced against what the assertion IS rather than what the vector claims."
                : string.Join(" | ", problems));
    }

    // -------------------------------------------------------------------------------------------------
    // 5 — observability, COMPUTED
    // -------------------------------------------------------------------------------------------------

    private static GateResult Observability(IReadOnlyList<SubmissionVector> vectors, AssertionEnumeration enumeration, MirrorObservability map, double floorScans, int runtimeCompression)
    {
        var problems = new List<string>();

        foreach (var v in vectors)
        {
            // A vector whose declared comp is not a comp is refused HERE as well as at the schema gate,
            // and the check is then run at 1 so the rest of the vector is still evaluated. Skipping it
            // would report the observability gate as passed on a vector it never looked at.
            var declared = v.CompressionFactor;
            if (declared < 1)
            {
                problems.Add($"{v.Id}: CompressionFactor is {declared}, so the window arithmetic has no basis. Scan counts are meaningless without the comp they were stated at; the check below was run at comp=1 to evaluate the rest.");
                declared = 1;
            }

            // *** THE FORM COMES FROM THE ENUMERATION WHERE THE ENUMERATION HAS ONE. *** F-3's refusal
            // keys on the assertion's form, so evaluating it against the VECTOR's declaration would
            // enforce the ruling against what an author claimed. The mismatch itself is refused by the
            // form-authority gate; this makes the observability verdict right even so.
            var form = (v.Basis is not null ? enumeration.FormOf(v.Basis.AssertionId) : null) ?? v.Form;
            var report = ObservabilityCheck.Evaluate(v.Expectations, form, map, floorScans, declared, runtimeCompression);
            problems.AddRange(report.Refusals.Select(r => $"{v.Id}/{r.Signal}: {r.Outcome} — {r.Detail}"));
        }

        return new GateResult("5 observability", GateStatus.Checked, problems.Count == 0, nameof(ObservabilityCheck),
            problems.Count == 0
                ? $"every expectation is supportable by the map, against a floor of {floorScans:0.0} scan(s) at comp={runtimeCompression}."
                : string.Join(" | ", problems));
    }

    // -------------------------------------------------------------------------------------------------
    // 7 — start bool
    // -------------------------------------------------------------------------------------------------

    private static GateResult StartBool(IReadOnlyList<SubmissionVector> vectors)
    {
        var problems = new List<string>();

        foreach (var group in vectors.GroupBy(v => v.Slot, StringComparer.Ordinal))
        {
            var names = group.Select(v => v.StartBool).Distinct(StringComparer.Ordinal).ToArray();
            if (names.Length > 1)
                problems.Add($"slot '{group.Key}' names {names.Length} different start bools ({string.Join(", ", names)}). Exactly one per slot: the commit raises one bit per slot.");
        }

        foreach (var v in vectors.Where(v => AbsoluteAddress.IsMatch(v.StartBool?.Trim() ?? string.Empty)))
        {
            problems.Add($"{v.Id}: StartBool '{v.StartBool}' is a bit POSITION, not a name. Bind by NAME — the bit order within the start-bool register is [I], not [M], and the simulator and BitAddressOf agree FROM THE SAME PREMISE, so their agreement is worth nothing.");
        }

        return new GateResult("7 start bool (submission half)", GateStatus.Checked, problems.Count == 0, nameof(SubmissionGate),
            problems.Count == 0
                ? "exactly one start bool per slot, every one bound by name. The LATER-SCAN rule is enforced at run time by InertPhase against the observed counter, and is not checkable here."
                : string.Join(" | ", problems));
    }

    // -------------------------------------------------------------------------------------------------
    // 8 — blacklist
    // -------------------------------------------------------------------------------------------------

    private static GateResult Blacklist(IReadOnlyList<SubmissionVector> vectors, ConflictGraph? conflicts)
    {
        var computedConflicts = conflicts?.BlocksForPacking;

        if (computedConflicts is null)
        {
            return new GateResult("8 blacklist", GateStatus.NotChecked, false, "cross-check conflict graph",
                "no computed disjointness graph was supplied, so the add-only property was compared against nothing. A blacklist checked against an absent graph is a blacklist nobody checked, and reporting it as passed is exactly the failure this gate exists to prevent.");
        }

        var problems = new List<string>();

        foreach (var v in vectors)
        {
            foreach (var entry in v.Blacklist)
            {
                if (string.IsNullOrWhiteSpace(entry.Block))
                    problems.Add($"{v.Id}: a blacklist entry names no block.");

                if (string.IsNullOrWhiteSpace(entry.Reason))
                    problems.Add($"{v.Id}: blacklist entry '{entry.Block}' carries no reason. The failure mode here is defensive over-blacklisting, concurrency collapsing toward serial, and nobody noticing BECAUSE IT STILL WORKS — a recorded reason is what makes that visible.");
            }
        }

        // ADD-ONLY IS A PROPERTY OF THE TYPE, NOT OF THIS CHECK: BlacklistEntry carries no negation, no
        // "allow" and no override, so an agent cannot express a removal. What is verified here is that
        // the effective set is a SUPERSET of the computed one — which it is by construction, and is
        // asserted rather than assumed.
        var declared = vectors.SelectMany(v => v.Blacklist.Select(b => b.Block)).ToHashSet(StringComparer.Ordinal);
        var effective = new HashSet<string>(computedConflicts, StringComparer.Ordinal);
        effective.UnionWith(declared);

        if (!computedConflicts.IsSubsetOf(effective))
            problems.Add("the effective exclusion set does not contain every computed conflict. That should be impossible — the blacklist can only add — so this is a defect in the gate, not in the submission.");

        return new GateResult("8 blacklist", GateStatus.Checked, problems.Count == 0, nameof(SubmissionGate),
            problems.Count == 0
                ? $"{declared.Count} declared exclusion(s) on top of {computedConflicts.Count} computed conflict(s); every entry carries a reason, and the type carries no way to remove one. NOTE: the blacklist names BLOCKS while admission colours SLOTS, so naming a block excludes every slot testing it."
                : string.Join(" | ", problems));
    }

    // -------------------------------------------------------------------------------------------------
    // 8c — X-G: the packer can mask a genuine multi-writer defect
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// <b>X-G, and the thing worth getting right is what an EMPTY report means.</b>
    ///
    /// <para>Two blocks that both write the same coil are a conflict, so DB-13 puts them in different
    /// tensors and both tests pass — <b>the scheduler has silently repaired a defect that will ship.</b>
    /// The fact is already computed by <c>converter cross-check</c> (C-308) and this design consumed it
    /// only as a graph edge. Reporting it is the fix X-G asks for.</para>
    ///
    /// <para><b>It reports on clean graphs too</b>, for the same reason F-6's collapse report prints its
    /// no-collapse line: a report that appears only on bad news teaches its reader that absence means
    /// "not run". And <b>a graph with unrecorded provenance is NOT CHECKED, never a clean bill</b> — that
    /// is the one state in which "0 multi-writer findings" would be true of the report and say nothing
    /// about the program.</para>
    ///
    /// <para><b>A finding does not refuse the submission, and that is a decision.</b> The defect is in the
    /// DELIVERABLE — two blocks writing one signal in one scan cycle — not in the vectors, and X-G's own
    /// treatment says "reported as FINDINGS as well as being used for packing". Refusing here would make a
    /// vector author responsible for a program defect they cannot fix. <b>Whether it should instead be a
    /// hard refusal is an owner question</b>, and it is recorded rather than decided in the code.</para>
    /// </summary>
    private static GateResult MultiWriterProvenance(ConflictGraph? conflicts)
    {
        if (conflicts is null)
        {
            return new GateResult("8c multi-writer provenance (X-G)", GateStatus.NotChecked, false, "cross-check conflict graph with provenance",
                "no conflict graph was supplied, so no multi-writer fact could be reported. An absent graph and a graph with no multi-writers produce the same empty report, which is why this is NOT CHECKED rather than a pass.");
        }

        if (!conflicts.ProvenanceComplete)
        {
            return new GateResult("8c multi-writer provenance (X-G)", GateStatus.NotChecked, false, "provenance on every conflict edge",
                conflicts.Render()
                + " Edges without provenance are the state this gate exists for: the packer will still separate the blocks, both tests will still pass, and nothing will have said that a multi-writer on a deliverable signal is what is being separated.");
        }

        return new GateResult("8c multi-writer provenance (X-G)", GateStatus.Checked, true, nameof(ConflictGraph),
            conflicts.Render()
            + (conflicts.MultiWriterFindings.Count > 0
                ? " REPORTED, NOT REFUSED: the defect is in the deliverable rather than in this submission, and X-G's treatment is to report. Whether it should refuse is an open owner question."
                : string.Empty));
    }

    // -------------------------------------------------------------------------------------------------
    // 10 — X-D: time compression, and the half of it a submission can answer
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// <b>X-D's assertion ceiling, per vector — and it catches a case gate 5 structurally cannot.</b>
    ///
    /// <para>Gate 5 exempts LATCHED and STAMPED expectations from the observability floor, correctly: a
    /// latch holds until cleared at test start and cannot fall in a poll gap. <b>X-D does not exempt them
    /// from the scan-period ceiling.</b> An event compressed below one scan of real time does not happen
    /// long enough to be latched either — <c>comp_max = T_event / scan_period</c> — so a latched
    /// declaration that clears gate 5 at every compression can still be void at the one being run.</para>
    ///
    /// <para><b>For SAMPLED expectations this is deliberately the same inequality as gate 5</b>, computed
    /// from the other side: <c>window.At(comp) &gt;= floor</c> is <c>comp &lt;= window.PlantScans /
    /// floor</c>. They cannot disagree because they are one division in <c>ScanBudget</c>, and a test
    /// sweeps the range asserting the boundary is the same number. What this adds is the CEILING as a
    /// number the author can act on, rather than only the verdict that one particular factor failed.</para>
    /// </summary>
    private static GateResult CompressionCeiling(IReadOnlyList<SubmissionVector> vectors, double floorScans, int runtimeCompression)
    {
        var problems = new List<string>();
        var ceilings = new List<string>();

        foreach (var v in vectors)
        {
            var declared = Math.Max(1, v.CompressionFactor);
            var perVector = new List<(string Signal, double Ceiling)>();

            foreach (var e in v.Expectations.Where(e => e.WindowScans >= 1))
            {
                var window = new Harness.Wire.ScanBudget(e.WindowScans, declared);
                perVector.Add((e.Signal, e.Mode == InstrumentationMode.Sampled
                    ? TimeCompression.SampledCeiling(window, floorScans)
                    : TimeCompression.LatchedCeiling(window)));
            }

            if (perVector.Count == 0)
            {
                ceilings.Add($"{v.Id}: no window declared on any expectation, so no assertion ceiling could be computed for it");
                continue;
            }

            var binding = perVector.MinBy(c => c.Ceiling);
            ceilings.Add($"{v.Id}: comp_max(assertion) = {binding.Ceiling:0.##}x, bound by '{binding.Signal}'");

            if (runtimeCompression > binding.Ceiling)
            {
                problems.Add($"{v.Id}: this wave runs at comp={runtimeCompression} and the vector's own assertion ceiling is {binding.Ceiling:0.##}x, bound by '{binding.Signal}'. "
                    + "X-D: compression shortens the REAL-TIME separation of the events being observed, so past this factor the assertion is not merely hard to catch — it is never sampled, and every check still reports green. USE comp_min, NOT comp_max.");
            }
        }

        return new GateResult("10a time compression — assertion ceiling (X-D)", GateStatus.Checked, problems.Count == 0, nameof(TimeCompression),
            problems.Count == 0
                ? $"this wave runs at comp={runtimeCompression}, under every vector's assertion ceiling. {string.Join("; ", ceilings)}. NOTE: the LATCHED ceiling is checked here and NOT by gate 5 — latching is exempt from the observability floor, never from the scan-period term."
                : string.Join(" | ", problems));
    }

    /// <summary>
    /// <b>X-D's other three ceilings, which a submission does not carry — and the treatment depends on
    /// whether anything is actually being compressed.</b>
    ///
    /// <para>The timer bound (<c>PT / (k x scan)</c>, which X-D says <i>often binds first</i>), the model's
    /// declared <c>comp_stable</c>, and the ratio-distortion bound on unscaled literals are properties of
    /// the BLOCK and the MODEL, not of the vectors. Contract §2 gives an author nowhere to state them.</para>
    ///
    /// <para><b>So: at comp = 1 this is a real pass</b> — nothing is scaled, and none of the three can bind.
    /// That is computed from the submission, not assumed. <b>Above comp = 1 it is NOT CHECKED and fails
    /// closed</b>, because the plan is then compressing a block whose shortest preset nobody stated. On a
    /// 500 ms preset the timer ceiling is 4.3x, not the 10x X-D originally assumed, so this is exactly the
    /// range where a submission would otherwise sail through.</para>
    /// </summary>
    private static GateResult CompressionBoundsNotInTheSubmission(
        IReadOnlyList<SubmissionVector> vectors, int runtimeCompression, double floorScans, BlockCompressionInputs? inputs)
    {
        var declaredFactors = vectors.Select(v => Math.Max(1, v.CompressionFactor)).ToArray();

        // *** IT KEYS ON THE RUNTIME FACTOR ALONE, AND THAT IS A DECISION. *** A vector's DECLARED comp is
        // the unit its scan counts are stated in; it says nothing about how fast the model will be driven.
        // Only the RUNTIME factor scales presets, distorts the ratio of an unscaled literal, and asks a
        // model to behave at a rate. A vector declaring comp=10 while the wave runs at 1 is running a model
        // in real time and none of these three ceilings can bind on it.
        if (runtimeCompression <= 1)
        {
            return new GateResult("10b time compression — timer / model / ratio ceilings (X-D)", GateStatus.Checked, true, nameof(TimeCompression),
                $"nothing is compressed at run time — this wave runs at comp={runtimeCompression}, whatever the vectors' declared factor(s) of {string.Join(", ", declaredFactors.Distinct().OrderBy(f => f))} — so X-D's timer, model-stability and ratio-distortion ceilings cannot bind. "
                + $"That is computed from the submission, not assumed: at comp=1 a DATA preset is unscaled, an unscaled LITERAL keeps its proportion, and the model is not being asked to run at a factor. "
                + $"For reference, the timer floor is k x scan = {TimeCompression.TimerScanMultiple} x {Harness.Wire.WireTiming.ScanPeriodMs} = {TimeCompression.TimerFloorMs:0.#} ms, so a 500 ms preset would cap compression at {500 / TimeCompression.TimerFloorMs:0.0}x — not the 10x X-D originally assumed.");
        }

        if (inputs is null)
        {
            return new GateResult("10b time compression — timer / model / ratio ceilings (X-D)", GateStatus.NotChecked, false, "TimeCompression.Plan, via BlockCompressionInputs",
                $"this wave runs at comp={runtimeCompression} with declared factor(s) {string.Join(", ", declaredFactors.Distinct().OrderBy(f => f))}, so compression IS being applied — and three of X-D's four ceilings were compared against nothing. "
                + $"No timer presets were supplied (the term X-D says OFTEN BINDS FIRST: PT / (k x scan), floor {TimeCompression.TimerFloorMs:0.#} ms, so a 500 ms preset caps at {500 / TimeCompression.TimerFloorMs:0.0}x), no model comp_stable, and no negligible-fraction threshold for the ratio-distortion bound. "
                + "Supply them as BlockCompressionInputs. An unknown ceiling is not a high one.");
        }

        var plan = TimeCompression.Plan(
            new CompressionRequest(inputs.PlantMs, inputs.BudgetMs,
                vectors.SelectMany(v => v.Expectations).ToArray(),
                declaredFactors.Max(), Math.Max(1, (int)Math.Round(floorScans * Harness.Wire.WireTiming.ScanPeriodMs / Harness.Wire.WireTiming.RttP99Ms)),
                inputs.Presets, inputs.ModelCompStable, inputs.NegligibleFraction),
            floorScans);

        if (!plan.Runnable)
        {
            return new GateResult("10b time compression — timer / model / ratio ceilings (X-D)", GateStatus.Checked, false, nameof(TimeCompression),
                plan.Render());
        }

        if (runtimeCompression > plan.CompMax)
        {
            return new GateResult("10b time compression — timer / model / ratio ceilings (X-D)", GateStatus.Checked, false, nameof(TimeCompression),
                $"this wave runs at comp={runtimeCompression} and comp_max is {plan.CompMax:0.##}x, bound by {plan.BindingBound}. " + plan.Render());
        }

        return new GateResult("10b time compression — timer / model / ratio ceilings (X-D)", GateStatus.Checked, true, nameof(TimeCompression),
            plan.Render()
            + (runtimeCompression > plan.CompMin
                ? $" *** THIS WAVE RUNS AT comp={runtimeCompression}, ABOVE comp_min. *** It clears the ceiling, and X-D's rule is to take the LEAST compression that meets the budget and bank the remainder as margin — compression is a fidelity risk, and running above comp_min spends that margin for nothing."
                : string.Empty));
    }

    // -------------------------------------------------------------------------------------------------
    // 9 — liveness, the half that IS separable to submission time
    // -------------------------------------------------------------------------------------------------

    private static GateResult LivenessPreconditions(IReadOnlyList<SubmissionVector> vectors, MirrorObservability map)
    {
        // The liveness CHECK is post-run and cannot be brought forward — nothing has happened yet. What
        // CAN be brought forward is whether liveness could be established at all, and a vector whose
        // liveness would be unanswerable is refused before a wave is spent on it rather than after.
        var problems = new List<string>();

        foreach (var v in vectors)
        {
            if (string.IsNullOrWhiteSpace(v.StartBool))
                problems.Add($"{v.Id}: no start bool, so neither 'was it commanded' nor 'did it run' could be answered after the wave. The result would be unreadable rather than failing.");

            if (map.IsEmpty)
                problems.Add($"{v.Id}: the map declares nothing, so no signal this vector reads could be shown to have been published.");
        }

        return new GateResult("9 liveness preconditions (submission half)", GateStatus.Checked, problems.Count == 0, nameof(SubmissionGate),
            problems.Count == 0
                ? "every vector could have its liveness established after the run: it has a start bool, and the map publishes something. The stimulus check itself is post-run (StimulusCheck) and is not a submission-time gate."
                : string.Join(" | ", problems));
    }
}
