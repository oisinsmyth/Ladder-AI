using Harness.Map;
using Harness.Results;

namespace Harness.Results.Tests;

/// <summary>
/// 🔴 <b>A CITATION MUST RESOLVE TO (SUBJECT, CLAUSE, ASSERTION) — NOT TO (CLAUSE, ASSERTION).</b>
///
/// <para><b>The measured problem, 2026-08-18.</b> A campaign gained a SECOND assertion enumeration with a
/// different subject, and <b>four clause IDs appear in both files</b>, each numbering its assertions from
/// A1. The submission document could hold exactly one enumeration and a citation was two terms checked
/// with two <c>Contains</c> calls against two flat sets — so the only way to submit a two-subject campaign
/// was to MERGE the files, after which a citation to a shared clause is answered by whichever entry
/// survived, the denominator gate 3 reports is the union of two denominators and therefore neither, and
/// <b>the dangling-citation check passes having examined the wrong set.</b></para>
///
/// <para><b>The fixture vocabulary here is invented</b> — PUMP and TANK, <c>REQ-*</c> and <c>SHARED-*</c>
/// — and deliberately shares nothing with the campaign that found this.</para>
///
/// <para><b>Every refusal below has a positive control beside it.</b> A resolver that refused everything
/// would satisfy each ambiguity assertion while making the tool useless, and this file's own denominator
/// is the tests that show an unqualified citation into an UNSHARED clause still resolves, that a set of
/// one still behaves exactly as it always did, and that a qualified citation is admitted.</para>
/// </summary>
public class SubjectResolutionTests
{
    // ---------------------------------------------------------------------------------------------
    // THE CORPUS — two subjects, one clause each of their own, one clause SHARED
    // ---------------------------------------------------------------------------------------------

    private const string PumpSubject = "PUMP";
    private const string TankSubject = "TANK";

    private const string PumpOnlyClause = "REQ-201";
    private const string TankOnlyClause = "REQ-305";
    private const string SharedClause = "SHARED-7";

    private const string PumpOnlyText = "WHEN the demand is raised THEN the drive command is asserted";
    private const string TankOnlyText = "WHEN the level passes the high mark THEN the fill is stopped";

    // The SHARED clause, decomposed DIFFERENTLY by each subject — which is the ordinary case: the same
    // sentence in the register obliges two different things of two different pieces of equipment.
    private const string SharedPumpText = "WHEN the interlock is broken THEN the drive command is dropped";
    private const string SharedTankText = "WHEN the interlock is broken THEN the fill valve is closed";

    private static readonly string PumpOnlyId = AssertionId.Compute(PumpOnlyClause, PumpOnlyText);
    private static readonly string TankOnlyId = AssertionId.Compute(TankOnlyClause, TankOnlyText);
    private static readonly string SharedPumpId = AssertionId.Compute(SharedClause, SharedPumpText);
    private static readonly string SharedTankId = AssertionId.Compute(SharedClause, SharedTankText);

    private static AssertionEnumeration Enumeration(
        string subject,
        (string Clause, string Id, string Text)[] assertions,
        string enumerator = "agent-c",
        IReadOnlyDictionary<string, string>? bounds = null) =>
        AssertionEnumeration.Of(
            assertions.Select(a => a.Clause).Distinct(StringComparer.Ordinal),
            assertions.Select(a => a.Id),
            assertions.ToDictionary(a => a.Id, _ => AssertionForm.When, StringComparer.Ordinal),
            enumerator,
            assertions.ToDictionary(a => a.Id, a => a.Text, StringComparer.Ordinal),
            assertions.ToDictionary(a => a.Id, _ => (IReadOnlySet<string>)new HashSet<string>(StringComparer.Ordinal) { "Demo_Count" }, StringComparer.Ordinal),
            bounds ?? new Dictionary<string, string>(StringComparer.Ordinal) { ["ramp_limit"] = "10" },
            subject: subject);

    private static AssertionEnumeration Pump(IReadOnlyDictionary<string, string>? bounds = null) =>
        Enumeration(PumpSubject, new[]
        {
            (PumpOnlyClause, PumpOnlyId, PumpOnlyText),
            (SharedClause, SharedPumpId, SharedPumpText),
        }, bounds: bounds);

    private static AssertionEnumeration Tank(IReadOnlyDictionary<string, string>? bounds = null) =>
        Enumeration(TankSubject, new[]
        {
            (TankOnlyClause, TankOnlyId, TankOnlyText),
            (SharedClause, SharedTankId, SharedTankText),
        }, bounds: bounds);

    private static AssertionEnumerationSet BothSubjects() => AssertionEnumerationSet.Of(Pump(), Tank());

    // ---------------------------------------------------------------------------------------------
    // THE FIXTURE'S OWN DENOMINATOR — asserted before anything is concluded from it
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>THE FIXTURE TRAP, CLOSED FIRST: the two subjects must genuinely SHARE a clause and must
    /// genuinely differ elsewhere.</b>
    ///
    /// <para>If they shared nothing, every ambiguity test below would pass while measuring nothing — the
    /// same shape as a scoped scan that matched no files and reported clean.</para>
    /// </summary>
    [Fact]
    public void THE_FIXTURE_HAS_ONE_SHARED_CLAUSE_AND_TWO_PRIVATE_ONES()
    {
        var set = BothSubjects();

        Assert.Equal(new[] { SharedClause }, set.SharedClauses());
        Assert.Equal(new[] { PumpSubject, TankSubject }, set.Subjects);
        Assert.Empty(set.DuplicateSubjects);
    }

    // ---------------------------------------------------------------------------------------------
    // THE REFUSAL — and the mutation control that proves the subject is load-bearing
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// <b>An unqualified citation to a clause TWO subjects declare is refused, and the refusal names both.</b>
    /// </summary>
    [Fact]
    public void AN_UNQUALIFIED_CITATION_TO_A_SHARED_CLAUSE_IS_REFUSED_AND_NAMES_BOTH_SUBJECTS()
    {
        var resolution = BothSubjects().Resolve(new Basis(SharedClause, SharedTankId));

        Assert.Equal(CitationResolutionState.Ambiguous, resolution.State);
        Assert.Null(resolution.Enumeration);
        Assert.Equal(new[] { PumpSubject, TankSubject }, resolution.Candidates);
        Assert.Contains(PumpSubject, resolution.Detail, StringComparison.Ordinal);
        Assert.Contains(TankSubject, resolution.Detail, StringComparison.Ordinal);

        // *** IT IS A REFUSAL AND NOT A PICK. *** Stated as its own assertion because "returns Ambiguous"
        // would also be true of an implementation that returned Ambiguous AND an enumeration for the caller
        // to fall back on, which is the guess this exists to remove.
        var admissibility = Admissibility.Check(
            new Basis(SharedClause, SharedTankId), BothSubjects(),
            FidelityDeclaration.Of("M", new[] { "x" }, null, true), Array.Empty<string>(),
            new SettlingDeclaration("n/a", Array.Empty<string>()), "Done",
            new AgentIdentity("agent-b"), new AgentIdentity("agent-a"), null);

        Assert.Contains(admissibility.Refusals, r => r.Reason == RefusalReason.AmbiguousSubject);
        Assert.False(admissibility.Admissible);
    }

    /// <summary>
    /// 🔴 <b>THE MUTATION CONTROL. Drop the subject from the resolution and the SAME citation is admitted.</b>
    ///
    /// <para><b>This is the pre-fix behaviour, reconstructed exactly.</b> Before 2026-08-18 a two-subject
    /// campaign had one way to be submitted: merge the enumerations. The merge below is that — the union of
    /// both clause sets and both assertion sets, which is what <c>Contains</c> was then asked — and the
    /// citation that is refused above <b>passes every basis check against it</b>. Nothing dangles, nothing
    /// reports, and the denominator quoted belongs to neither subject.</para>
    ///
    /// <para><b>It is expressed as a test rather than left as a manual source edit</b> because a mutation
    /// somebody has to re-apply by hand is a mutation that stops being run. The manual edit was ALSO
    /// performed — see this file's summary in the hand-back — and it moves exactly these tests.</para>
    /// </summary>
    [Fact]
    public void THE_MUTATION_CONTROL_dropping_the_subject_admits_the_citation_that_is_refused_above()
    {
        var merged = AssertionEnumeration.Of(
            new[] { PumpOnlyClause, TankOnlyClause, SharedClause },
            new[] { PumpOnlyId, TankOnlyId, SharedPumpId, SharedTankId },
            enumerator: "agent-c");

        var admissibility = Admissibility.Check(
            new Basis(SharedClause, SharedTankId), merged,
            FidelityDeclaration.Of("M", new[] { "x" }, null, true), Array.Empty<string>(),
            new SettlingDeclaration("n/a", Array.Empty<string>()), "Done",
            new AgentIdentity("agent-b"), new AgentIdentity("agent-a"), null);

        // Not one basis refusal: the clause is "there", the assertion is "there", and which SUBJECT was
        // meant is a question nothing asked.
        Assert.DoesNotContain(admissibility.Refusals, r => r.Reason is RefusalReason.ClauseNotEnumerated
            or RefusalReason.AssertionNotEnumerated or RefusalReason.AmbiguousSubject);
    }

    /// <summary>
    /// 🔴 <b>AND THE ASSERTION HASH CANNOT BE THE TIE-BREAKER — WHICH IS WHY MATCHING IS AT CLAUSE LEVEL.</b>
    ///
    /// <para>An ID is <c>clause + ":" + hash(normalised text)</c> and <b>no subject term appears in it</b>,
    /// so two subjects that share a clause AND a sentence mint the SAME ID. That is not hypothetical: the
    /// live vessel enumeration records it having already happened, in those words — <i>"THE TEXT IS
    /// BYTE-IDENTICAL, so the id is identical, so nothing dangles."</i></para>
    ///
    /// <para><b>A resolver that fell back on the hash where the clause was ambiguous would therefore be
    /// right until the first shared sentence and silently wrong after it.</b></para>
    /// </summary>
    [Fact]
    public void TWO_SUBJECTS_SHARING_A_CLAUSE_AND_A_SENTENCE_MINT_THE_SAME_ASSERTION_ID()
    {
        const string sameText = "WHEN the interlock is broken THEN the alarm is raised";

        Assert.Equal(
            AssertionId.Compute(SharedClause, sameText),
            AssertionId.Compute(SharedClause, sameText));

        // And the converse, so this test is not merely restating that a function is a function: two
        // DIFFERENT sentences under one clause do separate, which is what makes the collision a property of
        // the shared TEXT rather than of the shared clause.
        Assert.NotEqual(SharedPumpId, SharedTankId);

        var set = AssertionEnumerationSet.Of(
            Enumeration(PumpSubject, new[] { (SharedClause, AssertionId.Compute(SharedClause, sameText), sameText) }),
            Enumeration(TankSubject, new[] { (SharedClause, AssertionId.Compute(SharedClause, sameText), sameText) }));

        // Both subjects hold that exact ID, so an assertion-level resolver would have TWO answers and no
        // way to report it. The clause-level one refuses.
        Assert.Equal(CitationResolutionState.Ambiguous,
            set.Resolve(new Basis(SharedClause, AssertionId.Compute(SharedClause, sameText))).State);
    }

    // ---------------------------------------------------------------------------------------------
    // THE POSITIVE CONTROLS — a resolver that refused everything would pass every test above
    // ---------------------------------------------------------------------------------------------

    /// <summary><b>Qualifying the citation resolves it, and resolves it to the subject NAMED.</b></summary>
    [Fact]
    public void QUALIFYING_THE_CITATION_RESOLVES_IT_TO_THE_NAMED_SUBJECT()
    {
        var toTank = BothSubjects().Resolve(new Basis(SharedClause, SharedTankId, TankSubject));

        Assert.Equal(CitationResolutionState.Resolved, toTank.State);
        Assert.Equal(TankSubject, toTank.Enumeration!.Subject);

        var toPump = BothSubjects().Resolve(new Basis(SharedClause, SharedPumpId, PumpSubject));

        Assert.Equal(CitationResolutionState.Resolved, toPump.State);
        Assert.Equal(PumpSubject, toPump.Enumeration!.Subject);

        // 🔴 *** AND THE RESOLUTION IS LOAD-BEARING RATHER THAN DECORATIVE. *** Citing TANK's subject with
        // PUMP's assertion resolves cleanly — the subject was named — and then DANGLES against the subject
        // it named. That is the whole point: the denominator a citation is checked against is now the one
        // the citation chose, so citing into the wrong file is visible instead of absorbed by a union.
        var crossed = Admissibility.Check(
            new Basis(SharedClause, SharedPumpId, TankSubject), BothSubjects(),
            FidelityDeclaration.Of("M", new[] { "x" }, null, true), Array.Empty<string>(),
            new SettlingDeclaration("n/a", Array.Empty<string>()), "Done",
            new AgentIdentity("agent-b"), new AgentIdentity("agent-a"), null);

        Assert.Contains(crossed.Refusals, r => r.Reason == RefusalReason.AssertionNotEnumerated);
        Assert.DoesNotContain(crossed.Refusals, r => r.Reason == RefusalReason.AmbiguousSubject);
    }

    /// <summary>
    /// <b>An unqualified citation to a clause only ONE subject declares still resolves — for ever.</b>
    ///
    /// <para>The refusal is scoped to genuinely contested clauses. A rule that demanded qualification
    /// everywhere the moment a second subject existed would refuse a corpus of correct prose for a reason
    /// that does not apply to it.</para>
    /// </summary>
    [Fact]
    public void AN_UNQUALIFIED_CITATION_TO_AN_UNSHARED_CLAUSE_STILL_RESOLVES()
    {
        var pump = BothSubjects().Resolve(new Basis(PumpOnlyClause, PumpOnlyId));
        Assert.Equal(CitationResolutionState.Resolved, pump.State);
        Assert.Equal(PumpSubject, pump.Enumeration!.Subject);

        var tank = BothSubjects().Resolve(new Basis(TankOnlyClause, TankOnlyId));
        Assert.Equal(CitationResolutionState.Resolved, tank.State);
        Assert.Equal(TankSubject, tank.Enumeration!.Subject);
    }

    /// <summary>
    /// 🔴 <b>THE COMPATIBILITY GUARANTEE: a set of ONE cannot be ambiguous, and an unqualified citation
    /// takes exactly the path it took before subjects existed.</b>
    ///
    /// <para><b>Every citation written before 2026-08-18 is unqualified and means the file that was there
    /// first.</b> This asserts the mechanism that keeps that true — resolution answers RESOLVED for a
    /// one-enumeration set before it examines a single clause — and it asserts it for a DANGLING citation
    /// too, which is the case that would otherwise change: routed through the clause scan, a dangling
    /// citation would come back NotFound and gate 3 would report something new about an old corpus.</para>
    /// </summary>
    [Fact]
    public void A_SET_OF_ONE_RESOLVES_EVERY_CITATION_INCLUDING_ONE_THAT_DANGLES()
    {
        var single = AssertionEnumerationSet.Of(Pump());

        var good = single.Resolve(new Basis(PumpOnlyClause, PumpOnlyId));
        Assert.Equal(CitationResolutionState.Resolved, good.State);

        // The clause is not in this enumeration at all — and it still RESOLVES, because there is nowhere
        // else it could have meant. Gate 3 then refuses it as dangling, in its own words, exactly as before.
        var dangling = single.Resolve(new Basis("REQ-999", "REQ-999:aaaaaa"));
        Assert.Equal(CitationResolutionState.Resolved, dangling.State);
        Assert.Same(single.Enumerations[0], dangling.Enumeration);

        var admissibility = Admissibility.Check(
            new Basis("REQ-999", "REQ-999:aaaaaa"), single,
            FidelityDeclaration.Of("M", new[] { "x" }, null, true), Array.Empty<string>(),
            new SettlingDeclaration("n/a", Array.Empty<string>()), "Done",
            new AgentIdentity("agent-b"), new AgentIdentity("agent-a"), null);

        Assert.Contains(admissibility.Refusals, r => r.Reason == RefusalReason.ClauseNotEnumerated);
        Assert.DoesNotContain(admissibility.Refusals, r => r.Reason == RefusalReason.AmbiguousSubject);

        // The old text, byte for byte. A refusal whose WORDS changed would send a reader looking for a new
        // defect in a corpus that has not moved.
        Assert.Contains(admissibility.Refusals,
            r => r.Detail == "clause 'REQ-999' resolves to nothing written.");
    }

    // ---------------------------------------------------------------------------------------------
    // THE OTHER TWO WAYS A RESOLUTION FAILS
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_CITATION_NAMING_A_SUBJECT_NOBODY_ENUMERATED_IS_REFUSED_AND_LISTS_THE_ONES_THAT_EXIST()
    {
        var resolution = BothSubjects().Resolve(new Basis(SharedClause, SharedTankId, "VESSEL"));

        Assert.Equal(CitationResolutionState.UnknownSubject, resolution.State);
        Assert.Null(resolution.Enumeration);
        Assert.Contains(PumpSubject, resolution.Detail, StringComparison.Ordinal);
        Assert.Contains(TankSubject, resolution.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Two enumerations under one subject name is refused</b> — a citation naming it would resolve to
    /// whichever was listed first, which is the pick this whole type exists to prevent, wearing a QUALIFIED
    /// citation's clothes so nobody would think to look.
    /// </summary>
    [Fact]
    public void TWO_ENUMERATIONS_DECLARING_THE_SAME_SUBJECT_ARE_REFUSED()
    {
        var set = AssertionEnumerationSet.Of(Pump(), Pump());

        Assert.Equal(new[] { PumpSubject }, set.DuplicateSubjects);
        Assert.Equal(CitationResolutionState.Ambiguous, set.Resolve(new Basis(PumpOnlyClause, PumpOnlyId, PumpSubject)).State);

        var report = Check(enumerations: set);
        var gate = Assert.Single(report.Gates, g => g.Gate == "3j subject resolution");

        Assert.False(gate.Passed);
        Assert.Contains(PumpSubject, gate.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void AN_EMPTY_SET_IS_NOTHING_EXAMINED_AND_NEVER_A_PASS()
    {
        var resolution = AssertionEnumerationSet.Empty.Resolve(new Basis(PumpOnlyClause, PumpOnlyId));
        Assert.Equal(CitationResolutionState.NothingToResolveInto, resolution.State);

        var report = Check(enumerations: AssertionEnumerationSet.Empty);
        var gate = Assert.Single(report.Gates, g => g.Gate == "3j subject resolution");

        Assert.False(gate.Passed);
        Assert.Contains("NOTHING EXAMINED", gate.Detail, StringComparison.Ordinal);
        Assert.Equal(SubmissionVerdict.NotAdmissible, report.Verdict);
    }

    // ---------------------------------------------------------------------------------------------
    // THE GATE — 3j, and the denominator gate 3 is now allowed to state
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void GATE_3J_RUNS_AND_PASSES_ON_A_SINGLE_ENUMERATION_and_says_that_is_what_it_examined()
    {
        var report = Check();
        var gate = Assert.Single(report.Gates, g => g.Gate == "3j subject resolution");

        Assert.Equal(GateStatus.Checked, gate.Status);
        Assert.True(gate.Passed, gate.Detail);

        // *** THE PASS STATES ITS OWN SCOPE. *** "every citation resolved" is also true of a submission in
        // which nothing could be contested, and saying which case it was is the difference between a check
        // and a formality.
        Assert.Contains("ONE enumeration", gate.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 <b>THE DENOMINATOR IS STATED PER SUBJECT AND NEVER SUMMED.</b>
    ///
    /// <para>Gate 3's pass line used to read <i>"an enumeration of N assertion(s)"</i>. Summed over two
    /// subjects that number is the coverage denominator of NEITHER, and a reader taking a coverage figure
    /// from it would be quoting a denominator that does not exist.</para>
    /// </summary>
    [Fact]
    public void THE_DENOMINATOR_IS_STATED_PER_SUBJECT_AND_THE_SUM_IS_NEVER_PRINTED()
    {
        var text = BothSubjects().DenominatorText();

        Assert.Contains($"{PumpSubject}: 2 assertion(s)", text, StringComparison.Ordinal);
        Assert.Contains($"{TankSubject}: 2 assertion(s)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("4 assertion(s)", text, StringComparison.Ordinal);

        // And the ONE-UNNAMED-SUBJECT text is unchanged byte for byte, because that line is in every report
        // this project has ever produced and every enumeration written before today is exactly that shape.
        var unnamed = Enumeration(string.Empty, new[] { (PumpOnlyClause, PumpOnlyId, PumpOnlyText) });
        Assert.Equal("an enumeration of 1 assertion(s)", AssertionEnumerationSet.Of(unnamed).DenominatorText());

        // A set of one that DOES name its subject says so — more information, and it cannot be mistaken for
        // the old line by a reader diffing two reports.
        Assert.Equal($"{PumpSubject}: 2 assertion(s) across 2 clause(s)", AssertionEnumerationSet.Of(Pump()).DenominatorText());
    }

    // ---------------------------------------------------------------------------------------------
    // THE SECOND AMBIGUITY SURFACE — bound names carry no subject either
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>A BOUND IS COMPARED AGAINST THE TABLE THE VECTOR'S OWN SUBJECT PUBLISHES — never a merge.</b>
    ///
    /// <para><b>Bound names are BARE</b> (<c>ramp_limit</c>), carrying no subject and no clause, so two
    /// subject files' <c>bounds:</c> tables collide by name wherever they share one. Measured on the live
    /// campaign: <b>9 bound names appear in both enumerations.</b> Their values agree today — which is luck,
    /// not a property — and under a merge a vector would be compared against whichever entry survived the
    /// moment one subject retuned. That is AMB-19 re-opened across a file boundary, and it would move no
    /// assertion ID, so nothing else in the gate table would notice.</para>
    /// </summary>
    [Fact]
    public void A_VECTOR_IS_COMPARED_AGAINST_ITS_OWN_SUBJECTS_BOUND_VALUE_NOT_THE_OTHERS()
    {
        var pumpBounds = new Dictionary<string, string>(StringComparer.Ordinal) { ["ramp_limit"] = "10" };
        var tankBounds = new Dictionary<string, string>(StringComparer.Ordinal) { ["ramp_limit"] = "42" };

        var set = AssertionEnumerationSet.Of(Pump(pumpBounds), Tank(tankBounds));

        // A TANK vector written against TANK's value passes...
        var agreeing = Check(
            vectors: new[] { Vector(basis: new Basis(TankOnlyClause, TankOnlyId, TankSubject), boundsUsed: tankBounds) },
            enumerations: set);

        var passed = Assert.Single(agreeing.Gates, g => g.Gate == "3i bounds currency (AMB-19)");
        Assert.True(passed.Passed, passed.Detail);

        // ...and the SAME vector written against PUMP's value is STALE, which is the whole point: under a
        // merged table one of these two verdicts would have been wrong and nothing would have said which.
        var stale = Check(
            vectors: new[] { Vector(basis: new Basis(TankOnlyClause, TankOnlyId, TankSubject), boundsUsed: pumpBounds) },
            enumerations: set);

        var refused = Assert.Single(stale.Gates, g => g.Gate == "3i bounds currency (AMB-19)");
        Assert.False(refused.Passed, refused.Detail);
    }

    /// <summary>
    /// <b>A vector whose citation did not resolve is NOT quietly dropped by the per-vector gates.</b>
    ///
    /// <para>A gate that skipped its unexaminable subjects would report a pass over the remainder — and
    /// where EVERY citation is ambiguous it would examine nothing and pass. Empty is not clean.</para>
    /// </summary>
    [Fact]
    public void THE_PER_VECTOR_GATES_REPORT_THE_VECTORS_THEY_COULD_NOT_EXAMINE()
    {
        var report = Check(
            vectors: new[] { Vector(basis: new Basis(SharedClause, SharedTankId)) },
            enumerations: BothSubjects());

        foreach (var name in new[] { "3h required observations (AMB-14)", "3e assertion form authority", "3i bounds currency (AMB-19)" })
        {
            var gate = Assert.Single(report.Gates, g => g.Gate == name);

            Assert.False(gate.Status == GateStatus.Checked && gate.Passed,
                $"{name} PASSED over a vector whose citation resolves to no subject: {gate.Detail}");
        }

        Assert.Equal(SubmissionVerdict.NotAdmissible, report.Verdict);
    }

    // ---------------------------------------------------------------------------------------------
    // FIXTURE PLUMBING — mirrors SubmissionGateTests so the gate table is exercised, not stubbed
    // ---------------------------------------------------------------------------------------------

    private static SubmissionVector Vector(Basis? basis = null, IReadOnlyDictionary<string, string>? boundsUsed = null) =>
        new("V-1", "S0", 0, new AgentIdentity("agent-b"),
            basis ?? new Basis(PumpOnlyClause, PumpOnlyId, PumpSubject),
            new Dictionary<string, string> { ["Demo_Step"] = "5" },
            "Demo_Start",
            new[] { new ObservabilityDeclaration("Demo_Count", SignalNature.PersistentState, InstrumentationMode.Latched, 0, "10") },
            AssertionForm.When,
            new SettlingDeclaration("count unchanged across 3 scans", new[] { "Demo_Count" }),
            MaxDurationScans: 20,
            CompletionValue: 1,
            new[] { new BlacklistEntry("FC_Other", "shares the plant model instance") },
            CompressionFactor: 1,
            new[] { "ramp-to-limit" },
            "Demo_Done",
            "a ramp that overshoots by one step",
            boundsUsed ?? new Dictionary<string, string>(StringComparer.Ordinal) { ["ramp_limit"] = "10" });

    private static SubmissionReport Check(
        IReadOnlyList<SubmissionVector>? vectors = null,
        AssertionEnumerationSet? enumerations = null) =>
        SubmissionGate.Check(
            vectors ?? new[] { Vector() },
            enumerations ?? AssertionEnumerationSet.Of(Pump()),
            FidelityDeclaration.Of("M_Ramp", new[] { "ramp-to-limit" }, new[] { "overshoot" }, true, declaredBy: "agent-m"),
            new AgentIdentity("agent-a"),
            MirrorObservability.Of(("Demo_Count", new[] { InstrumentationMode.Latched })) with { Provenance = MapProvenance.Bindings },
            floorScans: 9,
            runtimeCompression: 1,
            ConflictGraph.Empty,
            compressionInputs: null,
            new DeploymentDeclaration("fixture-import", Array.Empty<S7ObjectDeclaration>()),
            TagMapReach.Of(Array.Empty<S7Reach>()),
            SignalStorageMap.Of(new[] { ("Demo_Count", new SignalStorage("DemoUnit", "Demo_Count")) }),
            conflictEdgesExplicitlyNull: false,
            unknownFields: Array.Empty<string>());
}
