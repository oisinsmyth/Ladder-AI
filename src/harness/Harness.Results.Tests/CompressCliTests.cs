using Harness.Gate;
using Harness.Results;
using Xunit;

namespace Harness.Results.Tests;

/// <summary>
/// <c>harness-gate compress</c> — emitting the scaled preset table a deploy applies.
///
/// <para><b>The command produces a FILE and not a number, deliberately.</b> The deploy has to write
/// concrete values into the block's parameters, and re-deriving a factor at deploy time from a figure in
/// another document is how the two drift apart.</para>
/// </summary>
public class CompressCliTests
{
    /// <summary>A submission declaring two gating presets: a 4-second window and a 2-second one.</summary>
    private const string WithPresets = """
    {
      "blockAuthor": "agent-a",
      "runtimeCompression": 1,
      "blockCompression": {
        "plantMs": 8000,
        "budgetMs": 2000,
        "presets": [
          { "name": "LeakWindow", "presetMs": 4000, "source": "Data" },
          { "name": "AvgWindow",  "presetMs": 2000, "source": "Data" }
        ]
      },
      "vectors": []
    }
    """;

    private const string NoPresets = """
    { "blockAuthor": "agent-a", "runtimeCompression": 1, "vectors": [] }
    """;

    private static (int Exit, string Output, Dictionary<string, string> Written) Compress(string submission, params string[] args)
    {
        var writer = new StringWriter();
        var written = new Dictionary<string, string>(StringComparer.Ordinal);

        var exit = CompressCli.Run(
            new[] { "compress" }.Concat(args).ToArray(),
            writer,
            path => path == "sub.json" ? submission : throw new FileNotFoundException(path),
            (path, content) => written[path] = content);

        return (exit, writer.ToString(), written);
    }

    [Fact]
    public void A_declared_factor_within_the_ceilings_writes_the_table()
    {
        var (exit, output, written) = Compress(WithPresets, "--submission", "sub.json", "--out", "presets.json", "--factor", "4");

        Assert.True(exit == CompressExit.Computed, output);

        var table = Assert.Contains("presets.json", (IDictionary<string, string>)written);
        Assert.Contains("\"scaledMs\": 1000", table, StringComparison.Ordinal);   // 4000 / 4
        Assert.Contains("\"scaledMs\": 500", table, StringComparison.Ordinal);    // 2000 / 4, exactly at the floor

        // The factor travels WITH the table, so the deploy and the wave can be shown to mean the same
        // compression rather than each re-deriving one.
        Assert.Contains("\"factor\": 4", table, StringComparison.Ordinal);
    }

    /// <summary>
    /// The shortest preset sets the ceiling — 2000 / 500 = 4.0× — so 5 is refused, <b>and nothing is
    /// written</b>. A partially applied compression is a different plant, not a faster one.
    /// </summary>
    [Fact]
    public void A_factor_past_the_shortest_preset_is_REFUSED_and_writes_NOTHING()
    {
        var (exit, output, written) = Compress(WithPresets, "--submission", "sub.json", "--out", "presets.json", "--factor", "5");

        Assert.Equal(CompressExit.Refused, exit);
        Assert.Empty(written);
        Assert.Contains("REFUSED at comp 5", output, StringComparison.Ordinal);
        Assert.Contains("A PARTIALLY APPLIED COMPRESSION IS A DIFFERENT PLANT", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 <b>The hole this command exists to close.</b> Exit 0 over an empty table would deploy an
    /// UNCOMPRESSED program while the wave's backstop re-expressed at the factor — and the symptom is a
    /// spurious TIMED-OUT on a healthy block, which this project calls worse than a spurious FAILED.
    /// </summary>
    [Fact]
    public void NO_declared_presets_is_NOTHING_COMPUTED_and_never_exit_zero()
    {
        var (exit, output, written) = Compress(NoPresets, "--submission", "sub.json", "--out", "presets.json", "--factor", "4");

        Assert.Equal(CompressExit.NothingComputed, exit);
        Assert.Empty(written);
        Assert.Contains("NOTHING COMPUTED", output, StringComparison.Ordinal);
        Assert.Contains("THIS IS NOT 'no compression is needed'", output, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unreadable_submission_is_NOTHING_COMPUTED_rather_than_a_bare_failure()
    {
        var (exit, output, _) = Compress("{ not json", "--submission", "sub.json", "--out", "presets.json", "--factor", "2");

        Assert.Equal(CompressExit.NothingComputed, exit);
        Assert.Contains("NOTHING COMPUTED", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>USE comp_min, NOT comp_max</b> — X-D's rule. 8000 ms of plant against a 2000 ms budget is 4×,
    /// and the tool must not reach for the ceiling just because the ceiling also happens to be 4.
    /// </summary>
    [Fact]
    public void A_budget_derives_comp_min_and_says_where_the_factor_came_from()
    {
        var (exit, output, written) = Compress(WithPresets, "--submission", "sub.json", "--out", "presets.json", "--budget-ms", "2000");

        Assert.True(exit == CompressExit.Computed, output);
        Assert.Contains("FACTOR: 4", output, StringComparison.Ordinal);
        Assert.Contains("derived as comp_min", output, StringComparison.Ordinal);

        // A generous budget must take a SMALLER factor, not the same one — the ceiling is a limit and
        // never a target, and compressing harder than needed pushes assertions toward the sampling floor.
        var (looseExit, looseOutput, _) = Compress(WithPresets, "--submission", "sub.json", "--out", "p2.json", "--budget-ms", "8000");
        Assert.True(looseExit == CompressExit.Computed, looseOutput);
        Assert.Contains("FACTOR: 1", looseOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void NEITHER_a_factor_nor_a_budget_is_a_refusal_rather_than_a_default_of_one()
    {
        var (exit, output, written) = Compress(WithPresets, "--submission", "sub.json", "--out", "presets.json");

        Assert.Equal(CompressExit.Refused, exit);
        Assert.Empty(written);

        // Defaulting to 1 would emit an uncompressed table that reads exactly like a compressed one.
        Assert.Contains("no default", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_arguments_print_usage_and_do_not_write()
    {
        var (exit, output, written) = Compress(WithPresets, "--submission", "sub.json");

        Assert.Equal(CompressExit.Refused, exit);
        Assert.Empty(written);
        Assert.Contains("usage: harness-gate compress", output, StringComparison.Ordinal);
    }
}
