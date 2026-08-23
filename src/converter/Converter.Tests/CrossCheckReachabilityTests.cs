using System.Text.Json;
using Converter.CrossCheck;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// 🔴 <b>Which code blocks actually execute — a question the graph could already answer and nothing could
/// ask it.</b>
///
/// <para>The reachability walk lived on <c>ProjectUsageGraph</c> and surfaced only as a footnote on
/// multi-writer lines. There was no way to ask the whole question, so <c>Harness.Batch</c> grew a SECOND
/// derivation of it — a regex over raw IR — with nothing holding the two together. That is the shape
/// <c>GateParityTests</c> exists for. Exposing the facts here does not make the harness's copy correct; it
/// makes it comparable, which is the strongest thing available while the harness stays deliberately
/// dependency-free.</para>
/// </summary>
public class CrossCheckReachabilityTests : IDisposable
{
    private readonly string _dir;

    public CrossCheckReachabilityTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"reach-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private void Write(string kind, string name, int number, params string[] calls)
    {
        var networks = calls.Length == 0
            ? new[] { new IrNetwork(1, "work", new[] { new CoilAssignment("Scratch", new Expr.TagRef("Enable")) }) }
            : calls.Select((c, i) => new IrNetwork(
                i + 1, "call", Array.Empty<CoilAssignment>(),
                Calls: new[] { new CallStatement(c, null, new Expr.TagRef("TRUE"), Array.Empty<CallArgument>()) })).ToArray();

        var block = new IrBlock("0", kind, name, number, "LAD", null, networks);
        var sidecars = block.Networks
            .Select(n => new NetworkSidecar(n.Number, n.Number.ToString(),
                Array.Empty<SidecarAccessEntry>(), Array.Empty<CoilAssignmentSidecar>()))
            .ToArray();

        File.WriteAllText(Path.Combine(_dir, name + ".ir"), IrSerializer.SerializeBlock(block, sidecars));
    }

    // ---------------------------------------------------------------------------------------------

    /// <summary>The shape that cost a wave: an FC deployed, loaded, and called by nothing.</summary>
    [Fact]
    public void An_FC_no_OB_reaches_is_reported_as_unreachable()
    {
        Write("OB", "Main", 1, "FC_Called");
        Write("FC", "FC_Called", 2);
        Write("FC", "FC_Orphan", 3);

        var reach = CrossCheckRunner.Run(_dir).Reachability;

        Assert.NotNull(reach);
        Assert.True(reach!.Known);
        Assert.Equal(new[] { "FC_Orphan" }, reach.Unreachable);
        Assert.Contains("Main", reach.ReachableFromAnOb);
        Assert.Contains("FC_Called", reach.ReachableFromAnOb);
        Assert.Equal(3, reach.CodeBlocks.Count);
    }

    /// <summary>
    /// 🔴 <b>THE NEGATIVE CONTROL.</b> A graph that called everything unreachable would satisfy the test
    /// above. With every block called, nothing is unreachable and the denominator is still stated.
    /// </summary>
    [Fact]
    public void A_corpus_where_everything_is_called_reports_nothing_unreachable()
    {
        Write("OB", "Main", 1, "FC_One", "FC_Two");
        Write("FC", "FC_One", 2);
        Write("FC", "FC_Two", 3);

        var reach = CrossCheckRunner.Run(_dir).Reachability!;

        Assert.True(reach.Known);
        Assert.Empty(reach.Unreachable);
        Assert.Equal(3, reach.CodeBlocks.Count);
        Assert.Equal(3, reach.ReachableFromAnOb.Count);
    }

    /// <summary>Reachability is transitive: an OB reaching an FC that reaches another still counts.</summary>
    [Fact]
    public void Reachability_follows_the_call_graph_transitively()
    {
        Write("OB", "Main", 1, "FC_Mid");
        Write("FC", "FC_Mid", 2, "FC_Leaf");
        Write("FC", "FC_Leaf", 3);

        Assert.Empty(CrossCheckRunner.Run(_dir).Reachability!.Unreachable);
    }

    /// <summary>
    /// 🔴 <b>NO OB MEANS UNKNOWN, NOT "ALL REACHABLE" — and <c>Unreachable</c> is empty BY CONSTRUCTION
    /// there, not because anything was checked.</b> A caller reading the empty list without reading
    /// <c>Known</c> would have it exactly backwards, which is why the flag exists at all.
    /// </summary>
    [Fact]
    public void A_corpus_with_NO_OB_reports_UNKNOWN_and_an_empty_list_that_means_nothing()
    {
        Write("FC", "FC_One", 1);
        Write("FC", "FC_Two", 2);

        var reach = CrossCheckRunner.Run(_dir).Reachability!;

        Assert.False(reach.Known);
        Assert.Empty(reach.OrganizationBlocks);
        Assert.Empty(reach.Unreachable);
        Assert.Equal(2, reach.CodeBlocks.Count);   // the denominator survives even when the verdict does not
    }

    /// <summary>The text report says which of the two it is, in words, on every run.</summary>
    [Fact]
    public void The_text_report_distinguishes_NOT_DECIDED_from_a_clean_scan()
    {
        Write("FC", "FC_One", 1);
        var undecided = CrossCheckOutputFormatter.FormatText(CrossCheckRunner.Run(_dir));

        Assert.Contains("REACHABILITY: NOT DECIDED", undecided);
        Assert.Contains("not \"all reachable\"", undecided);

        Write("OB", "Main", 2, "FC_One");
        var decided = CrossCheckOutputFormatter.FormatText(CrossCheckRunner.Run(_dir));

        Assert.Contains("REACHABILITY: 2 of 2 code block(s) reachable from 1 OB(s)", decided);
        Assert.DoesNotContain("NOT DECIDED", decided);
    }

    /// <summary>An unreachable block is NAMED in the text report — that is the whole point of reporting it.</summary>
    [Fact]
    public void The_text_report_names_an_unreachable_block()
    {
        Write("OB", "Main", 1, "FC_Called");
        Write("FC", "FC_Called", 2);
        Write("FC", "FC_Orphan", 3);

        var text = CrossCheckOutputFormatter.FormatText(CrossCheckRunner.Run(_dir));

        Assert.Contains("NOT IN THE SCAN: FC_Orphan", text);
        Assert.Contains("deployed, loaded, and never executed", text);
    }

    /// <summary>
    /// The JSON is what the harness's parity check consumes, so its shape is pinned here rather than
    /// discovered at the batch's expense.
    /// </summary>
    [Fact]
    public void The_JSON_carries_the_facts_the_parity_check_reads()
    {
        Write("OB", "Main", 1, "FC_Called");
        Write("FC", "FC_Called", 2);
        Write("FC", "FC_Orphan", 3);

        using var doc = JsonDocument.Parse(CrossCheckOutputFormatter.FormatJson(CrossCheckRunner.Run(_dir)));
        var reach = doc.RootElement.GetProperty("reachability");

        Assert.True(reach.GetProperty("known").GetBoolean());
        Assert.Equal("FC_Orphan", Assert.Single(reach.GetProperty("unreachable").EnumerateArray()).GetString());
        Assert.Equal(3, reach.GetProperty("codeBlocks").GetArrayLength());
        Assert.Equal(1, reach.GetProperty("organizationBlocks").GetArrayLength());
    }
}
