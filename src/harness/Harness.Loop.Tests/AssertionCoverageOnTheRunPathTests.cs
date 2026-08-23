using System.Text.Json;
using Harness.Loop;
using Harness.Results;
using Harness.Run;

namespace Harness.Loop.Tests;

/// <summary>
/// 🔴 <b>THE ASSERTION-COVERAGE NUMERATOR EXISTED AND DID NOT REACH THE PATH THAT SPENDS RIG TIME.</b>
///
/// <para><b>The measured problem.</b> <see cref="AssertionCoverage.Lines"/> had exactly two callers —
/// <c>GateCli</c>, and <c>LoopCli.WriteGate</c>, whose only caller is the <c>--generate-only</c> branch.
/// The renderer for an ACTUAL wave run printed <c>StampCoverage</c> (what the build stamp hashed) and
/// nothing else, and the run's JSON artifact carried the same thing under
/// <c>programUnderTest.coverage</c>. <b>Assertion coverage landed in no artifact at all.</b></para>
///
/// <para>🔴 <b>And in <c>harness-batch</c> the Generate step passes <c>--generate-only</c> for
/// <c>lanes[0]</c> ALONE</b> while the Wave step is per-lane without it, and there is no
/// <c>harness gate</c> step in the plan. So a two-lane batch printed the fraction once, for lane 0, to
/// scrollback — and never computed it for lane 1. The exact blind spot the numerator was built to close
/// was still open for every lane but the first.</para>
///
/// <para><b>What is asserted here is the WIRING, per lane and into the artifact.</b> The arithmetic is
/// <c>AssertionCoverageTests</c>' subject; these tests exist because a number computed into a field
/// nobody renders changes no behaviour. Every positive is paired with a negative that would pass on an
/// emitter which never fires.</para>
/// </summary>
public class AssertionCoverageOnTheRunPathTests
{
    // -------------------------------------------------------------------------------------------------
    // THE FIXTURE — a real wave run through the loop, gate and all
    // -------------------------------------------------------------------------------------------------

    private static LoopResult Ran() =>
        LoopRun.Execute(LoopRunTests.Request(), new SimulatedGateway(LoopRunTests.Geometry()));

    private static string Console(LoopResult result)
    {
        var writer = new StringWriter();
        LoopCli.Write(result, writer);
        return writer.ToString();
    }

    private static JsonElement Artifact(LoopResult result) =>
        JsonDocument.Parse(LoopCli.Render(result)).RootElement;

    private static JsonElement Coverage(LoopResult result) =>
        Artifact(result).GetProperty("assertionCoverage");

    /// <summary>
    /// A gate report carrying exactly the coverage a test wants, grafted onto a real run.
    ///
    /// <para><b>The three empty states are reachable no other way from a healthy fixture.</b> The loop's
    /// own request always carries an enumeration, so <c>NO DENOMINATOR</c> and the zero-denominator case
    /// cannot be driven through it — and they are precisely the states that must not read as a pass.</para>
    /// </summary>
    private static LoopResult With(AssertionCoverage coverage) =>
        Ran() with { Gate = new SubmissionReport(Array.Empty<GateResult>(), 0) { Coverage = coverage } };

    private const string SubjectA = "MIXER";
    private const string SubjectB = "FILTER";

    private static readonly (string Clause, string Text)[] AssertionsA =
    {
        ("REQ-401", "WHEN the start is commanded THEN the agitator command is asserted"),
        ("REQ-401", "WHEN the guard is opened THEN the agitator command is dropped"),
    };

    private static readonly (string Clause, string Text)[] AssertionsB =
    {
        ("REQ-455", "WHEN the differential passes the mark THEN the alarm is raised"),
    };

    private static string Id((string Clause, string Text) a) => AssertionId.Compute(a.Clause, a.Text);

    private static AssertionEnumeration Enumeration(
        string subject, (string Clause, string Text)[] assertions) =>
        AssertionEnumeration.Of(
            assertions.Select(a => a.Clause).Distinct(StringComparer.Ordinal),
            assertions.Select(Id),
            assertions.ToDictionary(Id, _ => AssertionForm.When, StringComparer.Ordinal),
            "agent-c",
            assertions.ToDictionary(Id, a => a.Text, StringComparer.Ordinal),
            subject: subject);

    private static SubmissionVector Citing(string id, string clause, string assertion, string? subject) =>
        new(id, "S0", 0, new AgentIdentity("agent-b"),
            new Basis(clause, assertion, subject),
            new Dictionary<string, string>(StringComparer.Ordinal),
            "Demo_Start",
            Array.Empty<ObservabilityDeclaration>(),
            AssertionForm.When,
            new SettlingDeclaration("count unchanged across 3 scans", new[] { "Demo_Count" }, 3),
            20, 1, Array.Empty<BlacklistEntry>(), 1, Array.Empty<string>(), "Demo_Done", null);

    // -------------------------------------------------------------------------------------------------
    // 1. THE CONSOLE OF AN ACTUAL RUN — the path a wave takes, per lane
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>THE ACCEPTANCE TEST.</b> A wave run — not <c>--generate-only</c> — prints the fraction, and
    /// prints the enumerator beside it because deleting assertions is the one way to move the ratio.
    /// </summary>
    [Fact]
    public void A_WAVE_RUN_PRINTS_THE_ASSERTION_COVERAGE_FRACTION()
    {
        var output = Console(Ran());

        Assert.Contains("ASSERTION COVERAGE", output, StringComparison.Ordinal);
        Assert.Contains("1 of 1", output, StringComparison.Ordinal);
        Assert.Contains("agent-c", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The negative control for the emitter that never fires.</b> A run whose gate never produced a
    /// report must print <c>NOT COMPUTED</c> — never a zero fraction, and never nothing at all. A
    /// rendering that only ever prints on the happy path would pass the test above and teach its reader
    /// that silence means zero.
    /// </summary>
    [Fact]
    public void AND_A_RUN_WITH_NO_GATE_REPORT_PRINTS_NOT_COMPUTED_rather_than_a_zero_fraction()
    {
        var output = Console(Ran() with { Gate = null });

        Assert.Contains("ASSERTION COVERAGE", output, StringComparison.Ordinal);
        Assert.Contains("NOT COMPUTED", output, StringComparison.Ordinal);
        Assert.DoesNotContain("0 of 0", output, StringComparison.Ordinal);
        Assert.DoesNotContain("100%", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Exactly once.</b> The run path already renders <c>StampCoverage</c>; a second rendering of the
    /// assertion figure in one output stream is noise, and noise is what gets skimmed.
    /// </summary>
    [Fact]
    public void THE_RUN_CONSOLE_RENDERS_IT_ONCE_and_not_twice()
    {
        var output = Console(Ran());

        Assert.Equal(1, output.Split("ASSERTION COVERAGE").Length - 1);
    }

    /// <summary>
    /// 🔴 <b>TWO DIFFERENT THINGS CALLED "COVERAGE" IN ONE OUTPUT STREAM IS ITS OWN DEFECT.</b>
    ///
    /// <para><c>StampCoverage</c> counts OBJECTS the build stamp hashed; <c>AssertionCoverage</c> counts
    /// ASSERTIONS the vectors cited. They share no denominator, no subject and no unit. The stamp line's
    /// label was the bare word <c>COVERAGE</c>, which — once the assertion block joined it on the same
    /// path — reads as the general figure of which the other is a part. It is not.</para>
    /// </summary>
    [Fact]
    public void THE_TWO_COVERAGES_ARE_LABELLED_APART()
    {
        var output = Console(Ran());

        Assert.Contains("STAMP COVERAGE", output, StringComparison.Ordinal);
        Assert.Contains("stamp over", output, StringComparison.Ordinal);

        // The bare label is gone: no line begins "COVERAGE" without saying which coverage it is.
        Assert.DoesNotContain("\nCOVERAGE ", output, StringComparison.Ordinal);
        Assert.DoesNotContain("\nCOVERAGE:", output, StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------------------------------------
    // 2. THE ARTIFACT — what a reviewer opens weeks later, when the scrollback is gone
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>IT LANDS IN THE RESULT JSON.</b> The console belongs to whoever was watching; the artifact is
    /// the run. Coverage that exists only in scrollback is coverage nobody can compare across lanes.
    /// </summary>
    [Fact]
    public void THE_RUN_ARTIFACT_CARRIES_THE_ASSERTION_COVERAGE()
    {
        var coverage = Coverage(Ran());

        Assert.Equal("Computed", coverage.GetProperty("state").GetString());

        var subject = Assert.Single(coverage.GetProperty("subjects").EnumerateArray());
        Assert.Equal(1, subject.GetProperty("numerator").GetInt32());
        Assert.Equal(1, subject.GetProperty("denominator").GetInt32());
        Assert.Equal("1 of 1", subject.GetProperty("fraction").GetString());
        Assert.Equal("agent-c", subject.GetProperty("enumerator").GetString());
    }

    /// <summary>
    /// 🔴 <b>PER SUBJECT, AND NEVER SUMMED — enforced by there being nothing to sum.</b> Two subjects
    /// summed is the denominator of neither, and a fraction is the shape somebody would most want to add
    /// up. The artifact therefore publishes no aggregate numerator or denominator at all.
    /// </summary>
    [Fact]
    public void THE_ARTIFACT_PUBLISHES_NO_SUMMED_FRACTION()
    {
        var vectors = new[]
        {
            Citing("V-1", AssertionsA[0].Clause, Id(AssertionsA[0]), SubjectA),
            Citing("V-2", AssertionsB[0].Clause, Id(AssertionsB[0]), SubjectB),
        };

        var coverage = Coverage(With(AssertionCoverage.Compute(
            vectors,
            AssertionEnumerationSet.Of(
                Enumeration(SubjectA, AssertionsA),
                Enumeration(SubjectB, AssertionsB)))));

        Assert.Equal(2, coverage.GetProperty("subjects").GetArrayLength());

        Assert.False(coverage.TryGetProperty("numerator", out _));
        Assert.False(coverage.TryGetProperty("denominator", out _));
        Assert.False(coverage.TryGetProperty("fraction", out _));

        // 1 of 2 and 1 of 1 — never 2 of 3.
        var fractions = coverage.GetProperty("subjects").EnumerateArray()
            .Select(s => s.GetProperty("fraction").GetString())
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "1 of 1", "1 of 2" }, fractions);
    }

    /// <summary>
    /// <b>A subject nothing cites still prints <c>0 of N</c>.</b> Omitting it would publish a run that
    /// covered everything it looked at.
    /// </summary>
    [Fact]
    public void A_SUBJECT_NOTHING_CITES_IS_IN_THE_ARTIFACT_AT_ZERO()
    {
        var coverage = Coverage(With(AssertionCoverage.Compute(
            new[] { Citing("V-1", AssertionsA[0].Clause, Id(AssertionsA[0]), SubjectA) },
            AssertionEnumerationSet.Of(
                Enumeration(SubjectA, AssertionsA),
                Enumeration(SubjectB, AssertionsB)))));

        var uncited = Assert.Single(
            coverage.GetProperty("subjects").EnumerateArray()
                .Where(s => s.GetProperty("subject").GetString() == SubjectB));

        Assert.Equal("0 of 1", uncited.GetProperty("fraction").GetString());
    }

    // -------------------------------------------------------------------------------------------------
    // 3. THE THREE EMPTY STATES — none of which may read as a pass
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// <b>Nobody computed one.</b> The key is present and says so; an absent key reads as "fine" to
    /// everyone who did not write the emitter.
    /// </summary>
    [Fact]
    public void EMPTY_STATE_1_NOT_COMPUTED_is_stated_in_the_artifact()
    {
        var coverage = Coverage(Ran() with { Gate = null });

        Assert.Equal("NotComputed", coverage.GetProperty("state").GetString());
        Assert.Empty(coverage.GetProperty("subjects").EnumerateArray());
        Assert.Contains(coverage.GetProperty("lines").EnumerateArray(),
            l => l.GetString()!.Contains("NOT COMPUTED", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>Computed, against nothing.</b> This is NOT <c>0 of 0</c> and it is NOT 100% — the artifact has
    /// to say the words, because the reader who most needs them is the one who never opened the source.
    /// </summary>
    [Fact]
    public void EMPTY_STATE_2_NO_DENOMINATOR_is_stated_in_the_artifact()
    {
        var coverage = Coverage(With(AssertionCoverage.Compute(
            Array.Empty<SubmissionVector>(), AssertionEnumerationSet.Empty)));

        Assert.Equal("NoDenominator", coverage.GetProperty("state").GetString());
        Assert.Contains(coverage.GetProperty("lines").EnumerateArray(),
            l => l.GetString()!.Contains("NOT 0 OF 0 AND IT IS NOT 100%", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>A denominator of zero.</b> An enumeration exists and contains nothing, so a citation checked
    /// against it is checked against nothing — which is not full coverage.
    /// </summary>
    [Fact]
    public void EMPTY_STATE_3_A_ZERO_DENOMINATOR_is_stated_in_the_artifact()
    {
        var coverage = Coverage(With(AssertionCoverage.Compute(
            Array.Empty<SubmissionVector>(),
            AssertionEnumerationSet.Of(AssertionEnumeration.Of(
                Array.Empty<string>(), Array.Empty<string>(), enumerator: "agent-c")))));

        Assert.Equal("Computed", coverage.GetProperty("state").GetString());

        var subject = Assert.Single(coverage.GetProperty("subjects").EnumerateArray());
        Assert.Equal("0 of 0", subject.GetProperty("fraction").GetString());

        Assert.Contains(coverage.GetProperty("lines").EnumerateArray(),
            l => l.GetString()!.Contains("THE ENUMERATION IS EMPTY", StringComparison.Ordinal));
    }

    // -------------------------------------------------------------------------------------------------
    // 4. THE BLIND SPOTS TRAVEL WITH EVERY RENDERING, INCLUDING THE MACHINE-READABLE ONE
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>What the number cannot see, in the artifact and not only on the terminal.</b> A reader of the
    /// file is the person most likely to quote the fraction onward, and the three standing limits are what
    /// stop it being quoted as a completeness claim.
    /// </summary>
    [Theory]
    [InlineData("CITED at admission")]
    [InlineData("cannot judge whether the DENOMINATOR is complete")]
    [InlineData("NO IMPLEMENTING LOGIC")]
    public void EVERY_ARTIFACT_RENDERING_CARRIES_THE_BLIND_SPOTS(string expected)
    {
        foreach (var result in new[] { Ran(), Ran() with { Gate = null } })
        {
            var spots = Coverage(result).GetProperty("blindSpots").EnumerateArray()
                .Select(s => s.GetString()!)
                .ToArray();

            Assert.Contains(spots, s => s.Contains(expected, StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// <b>One source for the blind spots, not two.</b> The console rendering and the artifact must not be
    /// able to come to say different things about one measurement — the same rule
    /// <c>ResultPackageJson.CoverageOf</c> follows for the stamp's coverage.
    /// </summary>
    [Fact]
    public void THE_CONSOLE_AND_THE_ARTIFACT_RENDER_THE_SAME_LINES()
    {
        var result = Ran();
        var output = Console(result);

        foreach (var line in Coverage(result).GetProperty("lines").EnumerateArray())
            Assert.Contains(line.GetString()!, output, StringComparison.Ordinal);
    }
}
