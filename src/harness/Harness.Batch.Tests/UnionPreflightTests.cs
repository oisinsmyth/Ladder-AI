using Harness.Batch;
using Harness.Device;

namespace Harness.Batch.Tests;

/// <summary>
/// 🔴 <b>The union check, run, and run over the UNION.</b>
///
/// <para>It used to be printed for an operator to type — and what it printed was one <c>cross-check</c>
/// PER LANE PATH, directly beneath a sentence correctly arguing that only a check over the union finds a
/// cross-lane conflict. The claim and the command contradicted each other on adjacent lines: a closed
/// check, naming something real (each lane's own corpus) that is not the thing it claimed.</para>
/// </summary>
public sealed class UnionPreflightTests
{
    private sealed class Canned : IProcessRunner
    {
        private readonly string _stdout;
        private readonly bool _started;
        private readonly bool _timedOut;

        internal int Calls { get; private set; }

        internal Canned(string stdout, bool started = true, bool timedOut = false)
        {
            _stdout = stdout;
            _started = started;
            _timedOut = timedOut;
        }

        public ProcessResult Run(string executable, IReadOnlyList<string> arguments, TimeSpan timeout)
        {
            Calls++;
            return new ProcessResult(_started, _timedOut, 0, _stdout, string.Empty, "canned");
        }
    }

    private const string TwoBlocks = "\"reachability\": { \"codeBlocks\": [\"FB_A\",\"FB_B\"], \"known\": true }";

    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>The finding a union check exists for: a path written by two blocks from two lanes.</b> A
    /// per-lane run cannot see it by construction, which is the whole argument.
    /// </summary>
    [Fact]
    public void A_cross_lane_multi_writer_is_reported_with_its_path()
    {
        var json = "{ \"multiWriters\": [ { \"path\": \"DB_Shared.Command\" } ], " + TwoBlocks + " }";

        var result = UnionPreflight.Run("converter.exe", "union", new Canned(json));

        Assert.True(result.Ran);
        var finding = Assert.Single(result.Findings);
        Assert.Equal("MULTI-WRITER", finding.Kind);
        Assert.Equal("DB_Shared.Command", finding.Detail);
    }

    /// <summary>Dead members and IO-boundary facts come through too, each labelled by kind.</summary>
    [Fact]
    public void Dead_members_and_io_boundary_facts_are_reported_and_labelled()
    {
        var json = "{ \"deadMembers\": [ { \"path\": \"DB_X.Unused\" } ], "
                 + "\"ioBoundary\": [ { \"block\": \"FB_A\" } ], " + TwoBlocks + " }";

        var kinds = UnionPreflight.Run("converter.exe", "union", new Canned(json)).Findings
            .Select(f => f.Kind).ToArray();

        Assert.Contains("DEAD-MEMBER", kinds);
        Assert.Contains("IO-BOUNDARY", kinds);
    }

    /// <summary>
    /// 🔴 <b>THE NEGATIVE CONTROL.</b> A clean union reports zero findings and STILL prints its
    /// denominator. Without this, a pre-flight that reported everything as a finding would satisfy the
    /// tests above.
    /// </summary>
    [Fact]
    public void A_clean_union_reports_zero_findings_and_still_states_its_denominator()
    {
        var result = UnionPreflight.Run("converter.exe", "union", new Canned("{ " + TwoBlocks + " }"));

        Assert.True(result.Ran);
        Assert.Empty(result.Findings);
        Assert.Equal(2, result.ObjectsExamined);
        Assert.Contains("2 object(s) in the union, 0 finding(s)", result.Summary);
    }

    /// <summary>
    /// 🔴 <b>A union with NO code blocks is NOT a clean union.</b> It is a union containing nothing, and
    /// reporting "0 findings" over it would be the FI-44 absence exactly: a green obtained by examining
    /// nothing.
    /// </summary>
    [Fact]
    public void An_EMPTY_union_is_NOT_PERFORMED_rather_than_zero_findings()
    {
        var json = "{ \"reachability\": { \"codeBlocks\": [], \"known\": false } }";

        var result = UnionPreflight.Run("converter.exe", "union", new Canned(json));

        Assert.False(result.Ran);
        Assert.Contains("NOT PERFORMED", result.Summary);
        Assert.Contains("NOTHING EXAMINED is not the same as nothing found", result.Detail);
    }

    /// <summary>Each way of failing to run says which it was — a reader must be able to tell them apart.</summary>
    [Theory]
    [InlineData("not json", "not readable JSON")]
    public void An_unreadable_report_is_NOT_PERFORMED_and_names_the_reason(string stdout, string expected)
    {
        var result = UnionPreflight.Run("converter.exe", "union", new Canned(stdout));

        Assert.False(result.Ran);
        Assert.Contains(expected, result.Detail);
    }

    [Fact]
    public void A_converter_that_does_not_start_is_NOT_PERFORMED_and_says_so()
    {
        var result = UnionPreflight.Run("converter.exe", "union", new Canned(string.Empty, started: false));

        Assert.False(result.Ran);
        Assert.Contains("did not start", result.Detail);
        Assert.Contains("NOT PERFORMED", result.Summary);
    }

    [Fact]
    public void A_converter_that_times_out_is_NOT_PERFORMED_and_says_nothing_was_compared()
    {
        var result = UnionPreflight.Run("converter.exe", "union", new Canned(string.Empty, timedOut: true));

        Assert.False(result.Ran);
        Assert.Contains("nothing across the lanes was compared", result.Detail);
    }

    /// <summary>
    /// 🔴 <b>ONE subprocess, two consumers.</b> The pre-flight and the reachability parity check both need
    /// <c>cross-check</c> over the same corpus; the report travels on the result so the second does not
    /// pay for an identical walk.
    /// </summary>
    [Fact]
    public void The_report_is_carried_so_the_parity_check_can_reuse_it()
    {
        var runner = new Canned("{ " + TwoBlocks + " }");

        var result = UnionPreflight.Run("converter.exe", "union", runner);

        Assert.Equal(1, runner.Calls);
        Assert.NotNull(result.CrossCheckJson);

        // And it is genuinely usable by the other consumer, not merely non-null.
        var mine = new ReachabilityReport(true, new[] { "Main" }, Array.Empty<string>(),
            Array.Empty<string>(), Array.Empty<string>(), "summary");

        Assert.Equal(ParityOutcome.Agreed, ReachabilityParity.CheckAgainst(mine, result.CrossCheckJson!).Outcome);
    }

    /// <summary>A run that did not happen carries no report, so a caller cannot mistake one for the other.</summary>
    [Fact]
    public void A_run_that_did_not_happen_carries_NO_report()
    {
        Assert.Null(UnionPreflight.Run("converter.exe", "union", new Canned("not json")).CrossCheckJson);
    }
}
