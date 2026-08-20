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

    // ---- hmi-delete-tagtable -------------------------------------------------------------------
    //
    // Classic tag tables (2026-08-20). `hmi-delete --kind TagTables` resolves through
    // FindSingleUnifiedSoftware and is blind to a classic device, so this is a separate verb for the
    // same reason `hmi-delete-screen` is. Its parse is SHARED with hmi-delete-screen
    // (ArgumentParser.ParseNamedDelete), so these assertions are about the wiring — that the shared
    // gates actually reach this verb — as much as about the parse itself.

    [Fact]
    public void DeleteTagTable_CollectsNamesAndDevice()
    {
        var result = ArgumentParser.Parse(Args(
            "hmi-delete-tagtable", "C:\\proj\\My.ap20", "--name", "ZZ_ProbeA", "--name", "ZZ_ProbeB",
            "--device", "HMI_1", "--yes"));

        var success = Assert.IsType<ParseResult.HmiDeleteTagTableSuccess>(result);
        Assert.Equal(new[] { "ZZ_ProbeA", "ZZ_ProbeB" }, success.Options.TagTableNames.ToArray());
        Assert.Equal("HMI_1", success.Options.Device);
        Assert.True(success.Options.Confirm);
    }

    /// <summary>A classic tag table's real name contains spaces ("Default tag table"), so the value
    /// must survive the parse verbatim rather than being split or trimmed.</summary>
    [Fact]
    public void DeleteTagTable_NameWithSpaces_SurvivesVerbatim()
    {
        var result = ArgumentParser.Parse(Args(
            "hmi-delete-tagtable", "C:\\proj\\My.ap20", "--name", "Default tag table", "--yes"));

        var success = Assert.IsType<ParseResult.HmiDeleteTagTableSuccess>(result);
        Assert.Equal("Default tag table", Assert.Single(success.Options.TagTableNames));
    }

    /// <summary>No --name means no work; exiting 0 on that is the empty-is-not-clean defect.</summary>
    [Fact]
    public void DeleteTagTable_WithNoNames_Fails()
    {
        var result = ArgumentParser.Parse(Args("hmi-delete-tagtable", "C:\\proj\\My.ap20", "--yes"));
        var failure = Assert.IsType<ParseResult.Failure>(result);
        Assert.Contains("--name", failure.Message);
        Assert.Contains("tag table name", failure.Message);
    }

    [Fact]
    public void DeleteTagTable_DuplicateName_Fails()
    {
        var result = ArgumentParser.Parse(Args(
            "hmi-delete-tagtable", "C:\\proj\\My.ap20", "--name", "ZZ_ProbeA", "--name", "ZZ_ProbeA", "--yes"));

        var failure = Assert.IsType<ParseResult.Failure>(result);
        Assert.Contains("more than once", failure.Message);
    }

    [Fact]
    public void DeleteTagTable_MissingProject_Fails()
    {
        var result = ArgumentParser.Parse(Args("hmi-delete-tagtable", "--name", "ZZ_ProbeA", "--yes"));
        Assert.IsType<ParseResult.Failure>(result);
    }

    /// <summary>The two second-dispatch switches a new subcommand gets forgotten in.</summary>
    [Fact]
    public void DeleteTagTable_IsHandledByBothCommonAccessors()
    {
        var result = ArgumentParser.Parse(Args(
            "hmi-delete-tagtable", "C:\\proj\\My.ap20", "--name", "ZZ_ProbeA", "--yes"));

        Assert.Equal("C:\\proj\\My.ap20", ArgumentParser.ProjectIdentifier(result));
        var common = ArgumentParser.CommonOptions(result);
        Assert.Equal(ArgumentParser.DefaultTimeoutOpenSeconds, common.TimeoutOpenSeconds);
        Assert.Equal(ArgumentParser.DefaultTimeoutConnectSeconds, common.TimeoutConnectSeconds);
    }

    /// <summary>The shared parse must still carry the per-command flags through to THIS record.</summary>
    [Fact]
    public void DeleteTagTable_CarriesTiaInstallAndTimeouts()
    {
        var result = ArgumentParser.Parse(Args(
            "hmi-delete-tagtable", "C:\\proj\\My.ap20", "--name", "ZZ_ProbeA",
            "--tia-install", "C:\\TIA", "--timeout-connect", "77", "--timeout-open", "88", "--yes"));

        var common = ArgumentParser.CommonOptions(result);
        Assert.Equal(77, common.TimeoutConnectSeconds);
        Assert.Equal(88, common.TimeoutOpenSeconds);
        Assert.NotNull(common.TiaInstallOverride);
    }

    /// <summary>
    /// THE ONE THAT MATTERS, the graphics version's twin. There is no `--prefix`, `--pattern`,
    /// `--glob`, `--all` or `--delete-matching`, and a name containing a wildcard character is
    /// carried through as a LITERAL — it is not expanded, so it fails to resolve rather than
    /// matching anything. `ZZ_*` would take all four probe tables AND anything else beginning ZZ_,
    /// and a classic tag table cannot be re-created through the API. This test exists so that adding
    /// an expansion later has to delete an explicit statement that it does not happen.
    /// </summary>
    [Fact]
    public void DeleteTagTable_HasNoWildcardForm_AndAWildcardIsTakenLiterally()
    {
        foreach (var flag in new[] { "--delete-matching", "--prefix", "--pattern", "--glob", "--all" })
        {
            Assert.IsType<ParseResult.Failure>(
                ArgumentParser.Parse(Args("hmi-delete-tagtable", "C:\\proj\\My.ap20", flag, "ZZ_", "--yes")));
        }

        var result = ArgumentParser.Parse(Args(
            "hmi-delete-tagtable", "C:\\proj\\My.ap20", "--name", "ZZ_*", "--yes"));
        var success = Assert.IsType<ParseResult.HmiDeleteTagTableSuccess>(result);
        Assert.Equal("ZZ_*", Assert.Single(success.Options.TagTableNames));
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

    [Fact]
    public void DeleteTagTable_WithoutYes_RefusesWithoutOpeningTheProject()
    {
        var gateway = new FakeGateway();
        var exit = Program.Run(
            Args("hmi-delete-tagtable", "C:\\proj\\My.ap20", "--name", "ZZ_ProbeA"), () => gateway);

        Assert.Equal(ExitCodes.NotConfirmed, exit);
        Assert.Equal(0, gateway.OpenProjectCalls);
        Assert.Null(gateway.DeletedTagTableNames);
    }

    [Fact]
    public void DeleteTagTable_WithYes_PassesTheLiteralNamesAndDeviceThrough()
    {
        var gateway = new FakeGateway();
        var exit = Program.Run(
            Args("hmi-delete-tagtable", "C:\\proj\\My.ap20",
                 "--name", "ZZ_MuxProbe", "--name", "ZZ_ProbeTime", "--device", "HMI_1", "--yes"),
            () => gateway);

        Assert.Equal(ExitCodes.Success, exit);
        Assert.Equal(1, gateway.OpenProjectCalls);
        Assert.Equal(new[] { "ZZ_MuxProbe", "ZZ_ProbeTime" }, gateway.DeletedTagTableNames!.ToArray());
        Assert.Equal("HMI_1", gateway.DeletedTagTableDevice);
    }

    /// <summary>
    /// EMPTY IS NOT CLEAN. A run that came back with no lines deleted nothing and verified nothing;
    /// reporting 0 would make the most reassuring output in the tool the one that examined least.
    /// </summary>
    [Fact]
    public void DeleteTagTable_ThatReportsNothing_IsNotASuccess()
    {
        var gateway = new FakeGateway { ReturnNoDeleteLines = true };
        var exit = Program.Run(
            Args("hmi-delete-tagtable", "C:\\proj\\My.ap20", "--name", "ZZ_ProbeA", "--yes"), () => gateway);

        Assert.Equal(ExitCodes.CommandError, exit);
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

    /// <summary>
    /// The tag-table delete's "not found" must name what IS present — the ScreenNotFoundException
    /// precedent. A classic tag table's name is not guessable (TIA's own default is "Default tag
    /// table", spaces included), so without the present-set a wrong name leaves the caller with no
    /// route to the right one. And it is a user-fixable naming mistake, not an internal fault.
    /// </summary>
    [Fact]
    public void TagTableNotFound_NamesWhatIsPresent_AndIsACommandError()
    {
        var ex = new HmiTagTableNotFoundException("ZZ_ProbeA", new[] { "Default tag table [HMI_1]" });

        Assert.Equal(ExitCodes.CommandError, ExitCodes.ForException(ex));
        Assert.Contains("ZZ_ProbeA", ex.Message);
        Assert.Contains("PRESENT", ex.Message);
        Assert.Contains("Default tag table [HMI_1]", ex.Message);
    }

    /// <summary>An empty present-set says so in words rather than trailing off after "PRESENT:".</summary>
    [Fact]
    public void TagTableNotFound_WithNothingPresent_SaysSo()
    {
        var ex = new HmiTagTableNotFoundException("ZZ_ProbeA", new string[0]);
        Assert.Contains("none", ex.Message);
    }

    /// <summary>
    /// The near-match diagnostic reaches the tag-table refusal too. Same exposure as
    /// `hmi-delete-screen`: repeated `--name` on a long generated command line, taken verbatim and
    /// matched Ordinal, so a trailing space is invisible to an echo AND to the output.
    /// The bare names are supplied separately because folding the device-qualified form
    /// ("ZZ_ProbeA [HMI_1]") could never match the requested one.
    /// </summary>
    [Fact]
    public void TagTableNotFound_WithATrailingSpace_ReportsTheNearMatchAndCodePoints()
    {
        var ex = new HmiTagTableNotFoundException(
            "ZZ_ProbeA ",
            new[] { "ZZ_ProbeA [HMI_1]" },
            new[] { "ZZ_ProbeA" });

        Assert.Contains("NEAR MATCH", ex.Message);
        Assert.Contains("'ZZ_ProbeA'", ex.Message);
        Assert.Contains("REQUESTED, code point by code point:", ex.Message);
    }

    /// <summary>A non-breaking space — the classic paste artefact — is rendered as its code point.
    /// Written as an escape, not as the character: a literal U+00A0 in this source file is exactly
    /// as invisible to the next reader as it is on a command line.</summary>
    [Fact]
    public void TagTableNotFound_WithANonBreakingSpace_ShowsTheCodePoint()
    {
        var ex = new HmiTagTableNotFoundException(
            "ZZ_Probe\u00A0A",
            new[] { "ZZ_Probe A [HMI_1]" },
            new[] { "ZZ_Probe A" });

        Assert.Contains("NEAR MATCH", ex.Message);
        Assert.Contains("U+00A0", ex.Message);
    }

    /// <summary>Silence is evidence too: nothing is emitted when no present name is close.</summary>
    [Fact]
    public void TagTableNotFound_WithNoCloseName_EmitsNoNearMatch()
    {
        var ex = new HmiTagTableNotFoundException(
            "ZZ_ProbeA",
            new[] { "Default tag table [HMI_1]" },
            new[] { "Default tag table" });

        Assert.DoesNotContain("NEAR MATCH", ex.Message);
    }

    /// <summary>The two-argument form — the export path's — is unchanged by the addition.</summary>
    [Fact]
    public void TagTableNotFound_WithoutBareNames_EmitsNoNearMatch()
    {
        var ex = new HmiTagTableNotFoundException("ZZ_ProbeA ", new[] { "ZZ_ProbeA [HMI_1]" });
        Assert.DoesNotContain("NEAR MATCH", ex.Message);
    }
}
