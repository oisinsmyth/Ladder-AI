using Harness.Batch;
using Harness.Device;

namespace Harness.Batch.Tests;

/// <summary>
/// 🔴 <b>The pinning test for a rule that is necessarily derived twice — in the same commit as the copy
/// became replaceable.</b>
///
/// <para>The harness cannot reference the converter: <c>src/harness/Directory.Build.props</c> keeps it
/// dependency-free on purpose, and that is what makes a harness change free of the TIA re-approval cycle.
/// So the usual remedy — delete one derivation — is unavailable, and the two are compared at runtime
/// instead, over the corpus actually in front of them.</para>
/// </summary>
public sealed class ReachabilityParityTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "parity-" + Guid.NewGuid().ToString("N"));

    public ReachabilityParityTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    /// <summary>A runner that answers with one canned stdout, whatever it is asked.</summary>
    private sealed class Canned : IProcessRunner
    {
        private readonly string _stdout;
        private readonly bool _started;

        internal Canned(string stdout, bool started = true)
        {
            _stdout = stdout;
            _started = started;
        }

        public ProcessResult Run(string executable, IReadOnlyList<string> arguments, TimeSpan timeout) =>
            new(_started, false, 0, _stdout, string.Empty, "canned");
    }

    private static string Json(bool known, params string[] unreachable) =>
        "{ \"reachability\": { \"codeBlocks\": [\"A\",\"B\",\"C\"], \"organizationBlocks\": "
        + (known ? "[\"Main\"]" : "[]")
        + ", \"reachableFromAnOb\": [], \"unreachable\": ["
        + string.Join(",", unreachable.Select(u => $"\"{u}\""))
        + "], \"known\": " + (known ? "true" : "false") + " } }";

    private static ReachabilityReport Mine(bool verified, params string[] unreachable) =>
        new(verified, new[] { "Main" }, Array.Empty<string>(), unreachable, Array.Empty<string>(), "summary");

    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Two_derivations_that_agree_report_Agreed_with_their_denominator()
    {
        var result = ReachabilityParity.Check(
            Mine(true, "Orphan"), "converter.exe", _root, new Canned(Json(true, "Orphan")));

        Assert.Equal(ParityOutcome.Agreed, result.Outcome);
        Assert.Contains("3 code block(s)", result.Detail);
    }

    /// <summary>
    /// 🔴 <b>A disagreement REFUSES and names which side saw what.</b> Not a warning: when two derivations
    /// differ, at least one is wrong and neither knows which, so continuing would pick a winner by
    /// accident of code path.
    /// </summary>
    [Fact]
    public void A_block_only_the_CONVERTER_calls_unreachable_is_a_disagreement()
    {
        var result = ReachabilityParity.Check(
            Mine(true), "converter.exe", _root, new Canned(Json(true, "Orphan")));

        Assert.Equal(ParityOutcome.Disagreed, result.Outcome);
        Assert.Contains("The converter alone calls these unreachable: Orphan", result.Detail);
    }

    /// <summary>The other direction, which a one-sided comparison would miss entirely.</summary>
    [Fact]
    public void A_block_only_the_TEXT_WALK_calls_unreachable_is_also_a_disagreement()
    {
        var result = ReachabilityParity.Check(
            Mine(true, "Orphan"), "converter.exe", _root, new Canned(Json(true)));

        Assert.Equal(ParityOutcome.Disagreed, result.Outcome);
        Assert.Contains("The text walk alone calls these unreachable: Orphan", result.Detail);
    }

    /// <summary>
    /// 🔴 <b>Disagreeing about whether the question is ANSWERABLE is checked first and separately.</b>
    /// "No OB, so unknown" and "checked, all reachable" both produce an empty unreachable list and mean
    /// opposite things — comparing only the lists would call that pair a match.
    /// </summary>
    [Fact]
    public void Disagreeing_about_DECIDABILITY_is_caught_before_the_lists_are_compared()
    {
        var result = ReachabilityParity.Check(
            Mine(true), "converter.exe", _root, new Canned(Json(known: false)));

        Assert.Equal(ParityOutcome.Disagreed, result.Outcome);
        Assert.Contains("disagree about whether reachability is DECIDABLE", result.Detail);
        Assert.Contains("ROOTS", result.Detail);
    }

    /// <summary>Both saying "no OB" is agreement — on UNKNOWN, which is not the same as agreement on a pass.</summary>
    [Fact]
    public void Both_saying_UNKNOWN_is_agreement_and_says_so_in_those_words()
    {
        var result = ReachabilityParity.Check(
            Mine(false), "converter.exe", _root, new Canned(Json(known: false)));

        Assert.Equal(ParityOutcome.Agreed, result.Outcome);
        Assert.Contains("not \"all reachable\"", result.Detail);
    }

    /// <summary>
    /// 🔴 <b>Not being able to consult the second derivation is NOT a refusal.</b> The converter is not
    /// always on the path, and a batch that stopped because it could not double-check would be a check
    /// refusing ordinary work — which is how checks get switched off.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("{ \"multiWriters\": [] }")]
    public void An_unusable_second_opinion_is_NotCompared_rather_than_a_refusal(string stdout)
    {
        var result = ReachabilityParity.Check(Mine(true), "converter.exe", _root, new Canned(stdout));

        Assert.Equal(ParityOutcome.NotCompared, result.Outcome);
        Assert.NotEqual(ParityOutcome.Disagreed, result.Outcome);
    }

    /// <summary>An older converter build simply has no such section, and says so rather than being read as agreement.</summary>
    [Fact]
    public void A_converter_build_with_no_reachability_section_says_the_walk_stands_alone()
    {
        var result = ReachabilityParity.Check(
            Mine(true), "converter.exe", _root, new Canned("{ \"multiWriters\": [] }"));

        Assert.Contains("only one derivation", result.Detail);
        Assert.Contains("weaker", result.Detail);
    }

    [Fact]
    public void A_converter_that_does_not_start_is_NotCompared()
    {
        var result = ReachabilityParity.Check(
            Mine(true), "converter.exe", _root, new Canned(string.Empty, started: false));

        Assert.Equal(ParityOutcome.NotCompared, result.Outcome);
        Assert.Contains("did not start", result.Detail);
    }
}
