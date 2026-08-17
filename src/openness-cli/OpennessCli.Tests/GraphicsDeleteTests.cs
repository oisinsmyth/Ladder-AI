using System.Linq;
using OpennessCli.Cli;
using OpennessCli.Openness;
using Xunit;

namespace OpennessCli.Tests;

/// <summary>
/// `graphics --delete` and `hmi-delete-screen` (2026-08-17). These are the destructive commands in
/// the HMI family, so the gates get more attention than the happy path: the confirm fence, the
/// absence of any wildcard form, and the exit code of a run that deleted nothing.
/// </summary>
public class GraphicsDeleteTests
{
    private static string[] Args(params string[] a) => a;

    // ---- graphics --delete --------------------------------------------------------------------

    [Fact]
    public void GraphicsDelete_CollectsEveryNameLiterally()
    {
        var result = ArgumentParser.Parse(Args(
            "graphics", "C:\\proj\\My.ap20", "--delete", "One", "--delete", "Two", "--yes"));

        var success = Assert.IsType<ParseResult.GraphicsSuccess>(result);
        Assert.Equal(new[] { "One", "Two" }, success.Options.DeleteNames!.ToArray());
        Assert.True(success.Options.Confirm);
    }

    [Fact]
    public void GraphicsDelete_WithoutYes_DoesNotConfirm()
    {
        var result = ArgumentParser.Parse(Args("graphics", "C:\\proj\\My.ap20", "--delete", "One"));
        var success = Assert.IsType<ParseResult.GraphicsSuccess>(result);
        Assert.False(success.Options.Confirm);
    }

    /// <summary>
    /// A repeated name would make the run report two deletions of one object, overstating what
    /// happened. Refused at parse time so Portal is never contacted to discover a typing mistake.
    /// </summary>
    [Fact]
    public void GraphicsDelete_DuplicateName_Fails()
    {
        var result = ArgumentParser.Parse(Args(
            "graphics", "C:\\proj\\My.ap20", "--delete", "One", "--delete", "One", "--yes"));

        var failure = Assert.IsType<ParseResult.Failure>(result);
        Assert.Contains("more than once", failure.Message);
    }

    [Fact]
    public void GraphicsDelete_CombinedWithExport_Fails()
    {
        var result = ArgumentParser.Parse(Args(
            "graphics", "C:\\proj\\My.ap20", "--delete", "One", "--export", "Two", "--out", "C:\\t\\x.xml"));

        var failure = Assert.IsType<ParseResult.Failure>(result);
        Assert.Contains("mutually exclusive", failure.Message);
    }

    /// <summary>
    /// THE ONE THAT MATTERS. There is no `--delete-matching`, `--pattern` or `--prefix`, and a name
    /// containing a wildcard character is carried through as a LITERAL — it is not expanded, so it
    /// simply fails to resolve rather than matching anything. This test exists so that adding an
    /// expansion later has to delete an explicit statement that it does not happen.
    /// </summary>
    [Fact]
    public void GraphicsDelete_HasNoWildcardForm_AndAWildcardIsTakenLiterally()
    {
        // No pattern-style flag is recognised — each is rejected rather than quietly widening a delete.
        foreach (var flag in new[] { "--delete-matching", "--prefix", "--pattern", "--glob", "--all" })
        {
            Assert.IsType<ParseResult.Failure>(
                ArgumentParser.Parse(Args("graphics", "C:\\proj\\My.ap20", flag, "Zz", "--yes")));
        }

        var result = ArgumentParser.Parse(Args("graphics", "C:\\proj\\My.ap20", "--delete", "Zz*", "--yes"));
        var success = Assert.IsType<ParseResult.GraphicsSuccess>(result);
        Assert.Equal("Zz*", Assert.Single(success.Options.DeleteNames!));
    }

    // ---- hmi-delete-screen --------------------------------------------------------------------

    [Fact]
    public void DeleteScreen_CollectsNamesAndDevice()
    {
        var result = ArgumentParser.Parse(Args(
            "hmi-delete-screen", "C:\\proj\\My.ap20", "--name", "A", "--name", "B", "--device", "HMI_1", "--yes"));

        var success = Assert.IsType<ParseResult.HmiDeleteScreenSuccess>(result);
        Assert.Equal(new[] { "A", "B" }, success.Options.ScreenNames.ToArray());
        Assert.Equal("HMI_1", success.Options.Device);
        Assert.True(success.Options.Confirm);
    }

    /// <summary>No --name means no work; exiting 0 on that is the empty-is-not-clean defect.</summary>
    [Fact]
    public void DeleteScreen_WithNoNames_Fails()
    {
        var result = ArgumentParser.Parse(Args("hmi-delete-screen", "C:\\proj\\My.ap20", "--yes"));
        var failure = Assert.IsType<ParseResult.Failure>(result);
        Assert.Contains("--name", failure.Message);
    }

    [Fact]
    public void DeleteScreen_DuplicateName_Fails()
    {
        var result = ArgumentParser.Parse(Args(
            "hmi-delete-screen", "C:\\proj\\My.ap20", "--name", "A", "--name", "A", "--yes"));

        Assert.IsType<ParseResult.Failure>(ArgumentParser.Parse(Args(
            "hmi-delete-screen", "C:\\proj\\My.ap20", "--name", "A", "--name", "A", "--yes")));
        Assert.IsType<ParseResult.Failure>(result);
    }

    [Fact]
    public void DeleteScreen_MissingProject_Fails()
    {
        var result = ArgumentParser.Parse(Args("hmi-delete-screen", "--name", "A", "--yes"));
        Assert.IsType<ParseResult.Failure>(result);
    }

    /// <summary>The two second-dispatch switches a new subcommand gets forgotten in.</summary>
    [Fact]
    public void DeleteScreen_IsHandledByBothCommonAccessors()
    {
        var result = ArgumentParser.Parse(Args("hmi-delete-screen", "C:\\proj\\My.ap20", "--name", "A", "--yes"));

        Assert.Equal("C:\\proj\\My.ap20", ArgumentParser.ProjectIdentifier(result));
        var common = ArgumentParser.CommonOptions(result);
        Assert.Equal(ArgumentParser.DefaultTimeoutOpenSeconds, common.TimeoutOpenSeconds);
    }

    // ---- the confirm fence, end to end through Program ----------------------------------------

    /// <summary>
    /// The fence must hold BEFORE Portal is contacted. Asserted through the real entry point with a
    /// gateway that counts calls: a refusal that still opened the project would pass any test that
    /// only checked the exit code.
    /// </summary>
    [Fact]
    public void GraphicsDelete_WithoutYes_RefusesWithoutOpeningTheProject()
    {
        var gateway = new FakeGateway();
        var exit = Program.Run(
            Args("graphics", "C:\\proj\\My.ap20", "--delete", "One"), () => gateway);

        Assert.Equal(ExitCodes.NotConfirmed, exit);
        Assert.Equal(0, gateway.OpenProjectCalls);
        Assert.Null(gateway.DeletedGraphicNames);
    }

    [Fact]
    public void DeleteScreen_WithoutYes_RefusesWithoutOpeningTheProject()
    {
        var gateway = new FakeGateway();
        var exit = Program.Run(
            Args("hmi-delete-screen", "C:\\proj\\My.ap20", "--name", "A"), () => gateway);

        Assert.Equal(ExitCodes.NotConfirmed, exit);
        Assert.Equal(0, gateway.OpenProjectCalls);
        Assert.Null(gateway.DeletedScreenNames);
    }

    /// <summary>The names reaching the gateway are exactly the names typed — nothing expands them
    /// on the way through.</summary>
    [Fact]
    public void GraphicsDelete_WithYes_PassesTheLiteralNamesThrough()
    {
        var gateway = new FakeGateway();
        var exit = Program.Run(
            Args("graphics", "C:\\proj\\My.ap20", "--delete", "One", "--delete", "Two", "--yes"), () => gateway);

        Assert.Equal(ExitCodes.Success, exit);
        Assert.Equal(new[] { "One", "Two" }, gateway.DeletedGraphicNames!.ToArray());
    }

    // ---- exception classification --------------------------------------------------------------

    /// <summary>
    /// A delete that reported success and did not happen is NOT a user error — nothing they typed
    /// can fix it, and it must not be dressed up as one.
    /// </summary>
    [Fact]
    public void DeleteDidNotTakeEffect_IsAnInternalFault_NotACommandError()
    {
        var ex = new DeleteDidNotTakeEffectException("graphic(s)", new[] { "One" });

        Assert.Equal(ExitCodes.UnexpectedError, ExitCodes.ForException(ex));
        Assert.Contains("STILL PRESENT", ex.Message);
        Assert.Contains("One", ex.Message);
    }
}
