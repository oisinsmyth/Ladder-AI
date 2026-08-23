using Harness.Gate;
using Harness.Results;

namespace Harness.Results.Tests;

/// <summary>
/// 🔴 <b>COVERAGE IS DISTINCT ASSERTIONS CITED OVER THE ENUMERATION'S SIZE — AND NOTHING COUNTED THE
/// NUMERATOR.</b>
///
/// <para><b>The measured problem.</b> Across five rig events over five days the set of assertions ever
/// asserted against a block did not change. One lane's vector count rose from two to three and its
/// coverage did not move at all, because <b>two of its vectors cite the same assertion</b> — and no report
/// added it up. Every mechanical gate was green throughout, correctly: each vector was individually
/// admissible. What was missing was not a refusal, it was a NUMBER.</para>
///
/// <para><b>Every fixture here is INVENTED</b> — MIXER and FILTER, <c>REQ-4xx</c>, signal <c>Demo_*</c> —
/// and shares nothing with the campaign that found this. The real projections are re-run in the job
/// folder, where the numbers are reproduced against the real artifacts, and are never committed.</para>
///
/// <para><b>The positive controls are as important as the refusals</b>: a coverage report that always said
/// "nothing is covered" would satisfy every under-count assertion below while measuring nothing, so each
/// under-count test has a beside-it case that shows the numerator DOES move when a genuinely new
/// assertion is cited.</para>
/// </summary>
public class AssertionCoverageTests
{
    // ---------------------------------------------------------------------------------------------
    // THE CORPUS — one subject of four assertions, one of two
    // ---------------------------------------------------------------------------------------------

    private const string MixerSubject = "MIXER";
    private const string FilterSubject = "FILTER";

    private const string ClauseA = "REQ-401";
    private const string ClauseB = "REQ-402";
    private const string FilterClause = "REQ-455";

    private static readonly (string Clause, string Text)[] MixerAssertions =
    {
        (ClauseA, "WHEN the start is commanded THEN the agitator command is asserted"),
        (ClauseA, "WHEN the guard is opened THEN the agitator command is dropped"),
        (ClauseB, "NEVER the agitator command with the guard open"),
        (ClauseB, "WHEN the stop is commanded THEN the agitator command is dropped"),
    };

    private static readonly (string Clause, string Text)[] FilterAssertions =
    {
        (FilterClause, "WHEN the differential passes the mark THEN the alarm is raised"),
        (FilterClause, "NEVER the backwash command during a fill"),
    };

    private static string Id((string Clause, string Text) a) => AssertionId.Compute(a.Clause, a.Text);

    private static readonly string MixerA1 = Id(MixerAssertions[0]);
    private static readonly string MixerA2 = Id(MixerAssertions[1]);
    private static readonly string FilterA1 = Id(FilterAssertions[0]);

    private static AssertionEnumeration Enumeration(
        string subject, (string Clause, string Text)[] assertions, string enumerator = "agent-c") =>
        AssertionEnumeration.Of(
            assertions.Select(a => a.Clause).Distinct(StringComparer.Ordinal),
            assertions.Select(Id),
            assertions.ToDictionary(Id, _ => AssertionForm.When, StringComparer.Ordinal),
            enumerator,
            assertions.ToDictionary(Id, a => a.Text, StringComparer.Ordinal),
            subject: subject);

    private static AssertionEnumeration Mixer(string enumerator = "agent-c") =>
        Enumeration(MixerSubject, MixerAssertions, enumerator);

    private static AssertionEnumeration Filter() => Enumeration(FilterSubject, FilterAssertions);

    private static SubmissionVector Vector(string id, string clause, string assertion, string? subject = null) =>
        new(id, "S0", 0, new AgentIdentity("agent-b"),
            new Basis(clause, assertion, subject),
            new Dictionary<string, string>(StringComparer.Ordinal),
            "Demo_Start",
            Array.Empty<ObservabilityDeclaration>(),
            AssertionForm.When,
            new SettlingDeclaration("count unchanged across 3 scans", new[] { "Demo_Count" }, 3),
            20, 1, Array.Empty<BlacklistEntry>(), 1, Array.Empty<string>(), "Demo_Done", null);

    private static SubmissionVector Uncited(string id) =>
        new(id, "S0", 0, new AgentIdentity("agent-b"), null,
            new Dictionary<string, string>(StringComparer.Ordinal),
            "Demo_Start", Array.Empty<ObservabilityDeclaration>(), AssertionForm.When,
            new SettlingDeclaration("count unchanged across 3 scans", new[] { "Demo_Count" }, 3),
            20, 1, Array.Empty<BlacklistEntry>(), 1, Array.Empty<string>(), "Demo_Done", null);

    private static string Render(AssertionCoverage coverage) => string.Join("\n", coverage.Lines());

    // ---------------------------------------------------------------------------------------------
    // THE FIXTURE'S OWN DENOMINATOR — asserted before anything is concluded from it
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// <b>The fixture trap, closed first.</b> Every count below is meaningless if the two subjects do not
    /// actually hold four and two DISTINCT assertions — four texts that collided into three would make an
    /// under-count test pass for the wrong reason.
    /// </summary>
    [Fact]
    public void The_fixture_holds_four_distinct_mixer_assertions_and_two_filter_assertions()
    {
        Assert.Equal(4, Mixer().Assertions.Count);
        Assert.Equal(2, Filter().Assertions.Count);
    }

    // ---------------------------------------------------------------------------------------------
    // 1. THE HEADLINE: THREE VECTORS, TWO ASSERTIONS, AND THE NUMERATOR IS TWO
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>THE EXACT SHAPE THAT WENT UNNOTICED ON A REAL LANE.</b> The vector count went 2 → 3 and
    /// coverage did not move, because the third vector cited an assertion already cited.
    /// </summary>
    [Fact]
    public void Three_vectors_citing_two_assertions_is_two_of_four_and_never_three()
    {
        var coverage = AssertionCoverage.Compute(
            new[]
            {
                Vector("V-1", ClauseA, MixerA1),
                Vector("V-2", ClauseA, MixerA1),
                Vector("V-3", ClauseA, MixerA2),
            },
            Mixer());

        var subject = Assert.Single(coverage.Subjects);
        Assert.Equal("2 of 4", subject.Fraction);
    }

    /// <summary>The positive control: a genuinely new citation DOES move the numerator.</summary>
    [Fact]
    public void A_third_DISTINCT_citation_does_move_it()
    {
        var coverage = AssertionCoverage.Compute(
            new[]
            {
                Vector("V-1", ClauseA, MixerA1),
                Vector("V-2", ClauseA, MixerA2),
                Vector("V-3", ClauseB, Id(MixerAssertions[2])),
            },
            Mixer());

        Assert.Equal("3 of 4", Assert.Single(coverage.Subjects).Fraction);
    }

    /// <summary>
    /// <b>The duplication is NAMED, not merely absorbed.</b> A fraction that silently stayed at 2 would be
    /// correct and would still not tell the reader why their third vector bought nothing.
    /// </summary>
    [Fact]
    public void The_assertion_two_vectors_share_is_named_with_both_of_them()
    {
        var coverage = AssertionCoverage.Compute(
            new[] { Vector("V-1", ClauseA, MixerA1), Vector("V-2", ClauseA, MixerA1) },
            Mixer());

        var shared = Assert.Single(Assert.Single(coverage.Subjects).MultiplyCited);
        Assert.Equal(new[] { "V-1", "V-2" }, shared.Vectors);
    }

    [Fact]
    public void And_the_rendering_says_the_vector_count_is_not_the_numerator()
    {
        var coverage = AssertionCoverage.Compute(
            new[] { Vector("V-1", ClauseA, MixerA1), Vector("V-2", ClauseA, MixerA1) },
            Mixer());

        Assert.Contains("THE VECTOR COUNT IS NOT THE NUMERATOR", Render(coverage), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // 2. THE AUTHOR CANNOT WIDEN EITHER HALF
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>A CITATION THE ENUMERATION DOES NOT CONTAIN RAISES NOTHING.</b> If it did, coverage would be
    /// a number the vector author could set by typing, which is the self-referential unit §7 calls
    /// absolutely forbidden — you cannot be missing an assertion nobody wrote.
    /// </summary>
    [Fact]
    public void A_citation_the_enumeration_does_not_contain_is_not_in_the_numerator()
    {
        var coverage = AssertionCoverage.Compute(
            new[] { Vector("V-1", ClauseA, MixerA1), Vector("V-2", ClauseA, "REQ-401:decafe") },
            Mixer());

        Assert.Equal("1 of 4", Assert.Single(coverage.Subjects).Fraction);
    }

    [Fact]
    public void And_it_is_reported_rather_than_dropped()
    {
        var coverage = AssertionCoverage.Compute(
            new[] { Vector("V-1", ClauseA, "REQ-401:decafe") },
            Mixer());

        var dangling = Assert.Single(Assert.Single(coverage.Subjects).CitedNotEnumerated);
        Assert.Equal("REQ-401:decafe", dangling.AssertionId);
    }

    /// <summary>
    /// <b>The producer of the denominator is printed beside the fraction.</b> Deleting assertions from the
    /// enumeration is the one way left to raise the ratio, and it is only detectable if a reader can see
    /// who produced it — gate 3d refuses a self-produced denominator; this makes it visible in the number.
    /// </summary>
    [Fact]
    public void The_enumerator_is_named_beside_the_fraction()
    {
        var coverage = AssertionCoverage.Compute(
            new[] { Vector("V-1", ClauseA, MixerA1) }, Mixer("agent-enumerator"));

        Assert.Contains("denominator by agent-enumerator", Render(coverage), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // 3. EMPTY IS NOT CLEAN, IN THREE DIFFERENT SHAPES
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// <b>No enumeration at all is NOT a coverage of zero and NOT a coverage of everything.</b> It is the
    /// absence of a denominator, and the three must not print alike.
    /// </summary>
    [Fact]
    public void No_enumeration_is_NO_DENOMINATOR_rather_than_a_fraction()
    {
        var coverage = AssertionCoverage.Compute(
            new[] { Vector("V-1", ClauseA, MixerA1) }, AssertionEnumerationSet.Empty);

        Assert.Equal(CoverageState.NoDenominator, coverage.State);
        Assert.Contains("THIS IS NOT 0 OF 0 AND IT IS NOT 100%", Render(coverage), StringComparison.Ordinal);
    }

    /// <summary>An enumeration holding nothing is a denominator of nothing, and must be loud about it.</summary>
    [Fact]
    public void An_empty_enumeration_is_a_denominator_of_nothing_and_not_full_coverage()
    {
        var coverage = AssertionCoverage.Compute(
            new[] { Vector("V-1", ClauseA, MixerA1) },
            AssertionEnumeration.Of(Array.Empty<string>(), Array.Empty<string>(), enumerator: "agent-c"));

        Assert.Contains("THE ENUMERATION IS EMPTY", Render(coverage), StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>A subject nothing cites still gets a line.</b> Omitting it prints a submission that covers
    /// everything it happened to look at, which is the shape of every self-referential coverage figure.
    /// </summary>
    [Fact]
    public void A_subject_no_vector_cites_still_prints_its_zero_and_its_denominator()
    {
        var coverage = AssertionCoverage.Compute(
            new[] { Vector("V-1", ClauseA, MixerA1, MixerSubject) },
            AssertionEnumerationSet.Of(Mixer(), Filter()));

        var filter = Assert.Single(coverage.Subjects, s => s.Subject == FilterSubject);
        Assert.Equal("0 of 2", filter.Fraction);
    }

    /// <summary>Two subjects are two fractions. <b>Summing them produces the denominator of neither.</b></summary>
    [Fact]
    public void Two_subjects_are_never_summed_into_one_fraction()
    {
        var coverage = AssertionCoverage.Compute(
            new[]
            {
                Vector("V-1", ClauseA, MixerA1, MixerSubject),
                Vector("V-2", FilterClause, FilterA1, FilterSubject),
            },
            AssertionEnumerationSet.Of(Mixer(), Filter()));

        Assert.Equal(new[] { "1 of 4", "1 of 2" }, coverage.Subjects.Select(s => s.Fraction));
    }

    // ---------------------------------------------------------------------------------------------
    // 4. THE VECTORS THAT RAISE NOTHING ARE STILL COUNTED
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_vector_citing_nothing_raises_no_numerator_and_is_named()
    {
        var coverage = AssertionCoverage.Compute(
            new[] { Vector("V-1", ClauseA, MixerA1), Uncited("V-2") }, Mixer());

        Assert.Equal(new[] { "V-2" }, coverage.VectorsCitingNothing);
        Assert.Equal("1 of 4", Assert.Single(coverage.Subjects).Fraction);
    }

    /// <summary>
    /// An ambiguous citation resolves to no single subject, so it is coverage for nobody. <b>Reported,
    /// never silently attributed to the first candidate</b> — that pick is what gate 3j exists to refuse.
    /// </summary>
    [Fact]
    public void An_unresolvable_citation_raises_no_subjects_numerator_and_is_named()
    {
        // Both subjects would have to declare the clause for the citation to be ambiguous, so this fixture
        // gives FILTER a decomposition of the MIXER clause — the ordinary case of one sentence obliging
        // two pieces of equipment.
        var sharedFilter = Enumeration(FilterSubject, new[] { (ClauseA, "WHEN the start is commanded THEN the inlet is opened") });

        var coverage = AssertionCoverage.Compute(
            new[] { Vector("V-1", ClauseA, MixerA1) },
            AssertionEnumerationSet.Of(Mixer(), sharedFilter));

        Assert.Equal(new[] { "V-1" }, coverage.VectorsUnresolved);
        Assert.All(coverage.Subjects, s => Assert.Equal(0, s.Numerator));
    }

    // ---------------------------------------------------------------------------------------------
    // 5. IT STATES WHAT IT CANNOT SEE — ON EVERY RENDERING, INCLUDING THE EMPTY ONES
    // ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("CITED at admission")]
    [InlineData("cannot judge whether the DENOMINATOR is complete")]
    [InlineData("NO IMPLEMENTING LOGIC")]
    public void Every_rendering_states_a_blind_spot(string expected)
    {
        var populated = AssertionCoverage.Compute(new[] { Vector("V-1", ClauseA, MixerA1) }, Mixer());
        var empty = AssertionCoverage.Compute(Array.Empty<SubmissionVector>(), Mixer());

        Assert.Contains(expected, Render(populated), StringComparison.Ordinal);
        Assert.Contains(expected, Render(empty), StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The zero case prints the denominator too.</b> A submission with no vectors covers nothing, and
    /// a report that went silent there would be indistinguishable from one that was never run.
    /// </summary>
    [Fact]
    public void A_submission_with_no_vectors_still_prints_its_denominator()
    {
        var coverage = AssertionCoverage.Compute(Array.Empty<SubmissionVector>(), Mixer());

        Assert.Contains("0 of 4", Render(coverage), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // 6. IT REACHES THE REPORT, AND THE DEFAULT FAILS CLOSED
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// <b>A report nobody computed coverage for says so.</b> The default must not be a zero fraction: a
    /// figure of <c>0 of 0</c> on an unmeasured report is exactly the shape that reads as a measurement.
    /// </summary>
    [Fact]
    public void A_report_built_without_coverage_reads_NOT_COMPUTED_rather_than_zero()
    {
        var report = new SubmissionReport(Array.Empty<GateResult>(), 0);

        Assert.Equal(CoverageState.NotComputed, report.Coverage.State);
        Assert.Contains("NOT COMPUTED", string.Join("\n", report.Coverage.Lines()), StringComparison.Ordinal);
    }

    /// <summary>The gate computes it, so every consumer of a real report has the figure without asking.</summary>
    [Fact]
    public void The_submission_gate_computes_coverage_onto_the_report()
    {
        var report = SubmissionGate.Check(
            new[] { Vector("V-1", ClauseA, MixerA1), Vector("V-2", ClauseA, MixerA1) },
            Mixer(), null, new AgentIdentity("agent-a"),
            MirrorObservability.Of(("Demo_Count", new[] { InstrumentationMode.Sampled })),
            floorScans: 1, runtimeCompression: 1, conflicts: null);

        Assert.Equal("1 of 4", Assert.Single(report.Coverage.Subjects).Fraction);
    }

    /// <summary>
    /// <b>And the CLI prints it.</b> A number computed into a field nobody renders changes no behaviour,
    /// which is the whole argument for this being a printed report rather than a property.
    /// </summary>
    [Fact]
    public void The_gate_CLI_prints_the_coverage_block()
    {
        var writer = new StringWriter();
        var submission = DerivedFixture.WithDerivation(GateCliTests.Good, "binding.json", GateCliTests.Binding);

        GateCli.Run(new[] { "check", "sub.json", "--binding", "binding.json" }, writer,
            path => path switch
            {
                "tags.json" => GateCliTests.TagMap,
                "binding.json" => GateCliTests.Binding,
                _ => submission,
            });

        Assert.Contains("ASSERTION COVERAGE", writer.ToString(), StringComparison.Ordinal);
        Assert.Contains("1 of 1", writer.ToString(), StringComparison.Ordinal);
    }
}
