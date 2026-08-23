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

    // ---------------------------------------------------------------------------------------------
    // undriven-scan over the union. It can ONLY be asked here: run against the deliverable alone, every
    // stimulus-driven input reports undriven, because what drives them lives in another file.
    // ---------------------------------------------------------------------------------------------

    private sealed class ExitingRunner : IProcessRunner
    {
        private readonly int _exit;
        private readonly string _stdout;

        internal IReadOnlyList<string>? LastArgs { get; private set; }

        internal ExitingRunner(int exit, string stdout = "") { _exit = exit; _stdout = stdout; }

        public ProcessResult Run(string executable, IReadOnlyList<string> arguments, TimeSpan timeout)
        {
            LastArgs = arguments;
            return new ProcessResult(true, false, _exit, _stdout, string.Empty, "canned");
        }
    }

    /// <summary>Exit 0 is a clean scan and produces no finding.</summary>
    [Fact]
    public void A_clean_undriven_scan_produces_no_finding()
    {
        Assert.Null(UnionPreflight.UndrivenScan("converter.exe", "union", "FB_Widget", new ExitingRunner(0)));
    }

    /// <summary>Exit 1 is a real finding about the block, and it names the block.</summary>
    [Fact]
    public void Undriven_members_are_reported_and_name_the_block()
    {
        var finding = UnionPreflight.UndrivenScan(
            "converter.exe", "union", "FB_Widget", new ExitingRunner(1, "banner\nSUMMARY: 3 undriven"));

        Assert.NotNull(finding);
        Assert.Equal("UNDRIVEN", finding!.Kind);
        Assert.Contains("FB_Widget", finding.Detail);
        // The LAST line, not the first — every tool here prints a banner first.
        Assert.Contains("SUMMARY: 3 undriven", finding.Detail);
    }

    /// <summary>
    /// 🔴 <b>Exit 2 is NOTHING EXAMINED, and it must not read as a clean scan.</b> The converter reports
    /// it explicitly for an FB that names no block in the corpus or has no instances — FI-44's shape, and
    /// the reason `undriven-scan` grew that exit code in the first place.
    /// </summary>
    [Fact]
    public void An_undriven_scan_that_examined_NOTHING_says_so_rather_than_passing()
    {
        var finding = UnionPreflight.UndrivenScan("converter.exe", "union", "FB_Absent", new ExitingRunner(2));

        Assert.NotNull(finding);
        Assert.Contains("NOTHING EXAMINED", finding!.Detail);
        Assert.Contains("not a pass", finding.Detail);
    }

    /// <summary>
    /// 🔴 <b>With no named subject the scan is NOT RUN — and that is reported as an unasked question,
    /// never as a clean one.</b> Guessing which object is under test would be worse than declining: a
    /// per-block check pointed at the wrong block is a confident answer about the wrong thing.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void With_no_named_block_under_test_the_scan_is_NOT_RUN_and_nothing_is_guessed(string? subject)
    {
        var runner = new ExitingRunner(0);

        var finding = UnionPreflight.UndrivenScan("converter.exe", "union", subject, runner);

        Assert.NotNull(finding);
        Assert.Contains("NOT RUN", finding!.Detail);
        Assert.Contains("unasked question", finding.Detail);
        Assert.Null(runner.LastArgs);   // and it did not run the converter to find that out
    }

    /// <summary>The subject reaches the command line as `--fb`, over the UNION and not one lane.</summary>
    [Fact]
    public void The_scan_is_pointed_at_the_union_and_at_the_named_block()
    {
        var runner = new ExitingRunner(0);
        UnionPreflight.UndrivenScan("converter.exe", "union-dir", "FB_Widget", runner);

        Assert.Equal(new[] { "undriven-scan", "--project", "union-dir", "--fb", "FB_Widget" }, runner.LastArgs);
    }
}
