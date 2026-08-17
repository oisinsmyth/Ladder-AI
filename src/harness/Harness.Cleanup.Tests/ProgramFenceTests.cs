namespace Harness.Cleanup.Tests;

/// <summary>
/// <b>The confirmation fence, driven through the entry point a caller actually uses.</b>
///
/// <para>Measured 2026-08-14: <c>block-layout --set</c>'s <c>--yes</c> gate can be disconnected with a
/// one-token mutation and 712 of 712 tests stay green, because the tests call the refusal helper
/// DIRECTLY — pinning the message and not the routing. So these go through
/// <see cref="Program.Main"/>, and they assert the OBSERVABLE CONSEQUENCE (nothing was planned) rather
/// than the exit code alone, which a disconnected gate can produce by accident.</para>
/// </summary>
public class ProgramFenceTests
{
    [Theory]
    [InlineData("--yes")]
    [InlineData("--force")]
    [InlineData("--confirm")]
    [InlineData("--delete")]
    [InlineData("--execute")]
    public void A_CONFIRMATION_FLAG_IS_REFUSED_BY_NAME_and_never_silently_ignored(string flag)
    {
        using var corpus = new TempCorpus();
        corpus.Fc("FC_Orphan", 9098);
        var xc = corpus.CrossCheck("xc.json", "[]");

        var (exit, stdout, stderr) = Run(
            "--project", corpus.Ir, "--authority", "lane-c",
            "--cross-check", xc, "--drain-report", corpus.DrainedReport(),
            "--test-artifact", corpus.File_("v.json", "{}"), flag);

        Assert.Equal((int)CleanupExit.ConfirmationRefused, exit);
        Assert.Contains(flag, stderr, StringComparison.Ordinal);

        // The observable consequence: no plan was produced at all. An accepted-then-ignored flag would
        // have printed the whole report and let the caller believe something happened.
        Assert.DoesNotContain("REMOVE", stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("SCOPE", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void THE_ORDINARY_RUN_WITH_NO_CONFIRMATION_FLAG_IS_UNTOUCHED_BY_THAT_FENCE()
    {
        // The anti-over-fire half, tested as deliberately as the refusal. A gate that refuses every
        // ordinary submission is removed within a week, by someone who is right to.
        using var corpus = new TempCorpus();
        corpus.Fc("FC_Orphan", 9098);
        var xc = corpus.CrossCheck("xc.json", "[]");

        var (exit, stdout, _) = Run(
            "--project", corpus.Ir, "--authority", "lane-c",
            "--cross-check", xc, "--drain-report", corpus.DrainedReport(),
            "--test-artifact", corpus.File_("v.json", "{}"));

        Assert.Equal((int)CleanupExit.Planned, exit);
        Assert.Contains("REMOVE Block 'FC_Orphan'", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void PROJECT_and_AUTHORITY_are_required_at_the_entry_point()
    {
        using var corpus = new TempCorpus();

        Assert.Equal((int)CleanupExit.Usage, Run("--authority", "x").Exit);
        Assert.Equal((int)CleanupExit.Usage, Run("--project", corpus.Ir).Exit);
    }

    [Fact]
    public void NO_ARGUMENTS_prints_usage_and_does_not_scan_anything()
    {
        var (exit, stdout, _) = Run();

        Assert.Equal((int)CleanupExit.Usage, exit);
        Assert.Contains("PLANS a batch; CANNOT delete", stdout, StringComparison.Ordinal);
    }

    private static (int Exit, string Stdout, string Stderr) Run(params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var oldOut = Console.Out;
        var oldErr = Console.Error;

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            var exit = Program.Main(args);
            return (exit, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(oldOut);
            Console.SetError(oldErr);
        }
    }
}
