using System;
using System.IO;
using System.Linq;
using OpennessCli;
using OpennessCli.Cli;
using OpennessCli.Model;
using OpennessCli.Openness;
using Xunit;

namespace OpennessCli.Tests;

/// <summary>
/// `rename` — argument parsing, the pure name rules, and the read-back verdict against
/// <see cref="FakeGateway"/>. Nothing here touches Portal; the gateway half is unmeasured against TIA.
/// </summary>
public class RenameTests
{
    private const string Project = "C:\\proj\\My.ap20";

    private static ParseResult Parse(params string[] rest) =>
        ArgumentParser.Parse(new[] { "rename", Project }.Concat(rest).ToArray());

    private static RenameCommandOptions Options(params string[] rest) =>
        Assert.IsType<ParseResult.RenameSuccess>(Parse(rest)).Options;

    // ---- parsing ----------------------------------------------------------------------------

    [Theory]
    [InlineData("--block", RenameTargetKind.Block)]
    [InlineData("--type", RenameTargetKind.Type)]
    [InlineData("--techobject", RenameTargetKind.TechnologicalObject)]
    [InlineData("--hmitag", RenameTargetKind.HmiTag)]
    [InlineData("--hmialarm", RenameTargetKind.HmiAlarm)]
    public void Parse_EachSelector_MapsToItsKind(string flag, RenameTargetKind kind)
    {
        var options = Options(flag, "Old", "--to", "New");

        Assert.Equal(kind, options.Kind);
        Assert.Equal("Old", options.Name);
        Assert.Equal("New", options.NewName);
        Assert.Equal(Project, options.ProjectIdentifier);
        Assert.Null(options.Device);
        Assert.False(options.Confirm);
        Assert.False(options.Json);
    }

    [Fact]
    public void Parse_AllOptionalFlags()
    {
        var options = Options("--hmitag", "T1", "--to", "T2", "--device", "HMI_1", "--json", "--yes",
            "--timeout-connect", "30", "--timeout-open", "60");

        Assert.Equal("HMI_1", options.Device);
        Assert.True(options.Json);
        Assert.True(options.Confirm);
        Assert.Equal(30, options.TimeoutConnectSeconds);
        Assert.Equal(60, options.TimeoutOpenSeconds);
    }

    [Fact]
    public void Parse_FlagOrderDoesNotMatter()
    {
        var parsed = ArgumentParser.Parse(new[] { "rename", "--to", "New", "--yes", "--block", "Old", Project });
        var options = Assert.IsType<ParseResult.RenameSuccess>(parsed).Options;
        Assert.Equal(Project, options.ProjectIdentifier);
        Assert.Equal("Old", options.Name);
    }

    [Fact]
    public void Parse_MissingTo_Fails()
    {
        var failure = Assert.IsType<ParseResult.Failure>(Parse("--block", "Old"));
        Assert.Contains("--to", failure.Message);
    }

    [Fact]
    public void Parse_ToWithoutValue_Fails()
    {
        Assert.IsType<ParseResult.Failure>(Parse("--block", "Old", "--to"));
    }

    [Fact]
    public void Parse_NoSelector_Fails()
    {
        var failure = Assert.IsType<ParseResult.Failure>(Parse("--to", "New"));
        Assert.Contains("exactly one", failure.Message);
    }

    [Theory]
    [InlineData("--block", "--type")]
    [InlineData("--block", "--techobject")]
    [InlineData("--type", "--hmitag")]
    [InlineData("--hmitag", "--hmialarm")]
    [InlineData("--block", "--block")]
    public void Parse_TwoSelectors_Fail(string first, string second)
    {
        var failure = Assert.IsType<ParseResult.Failure>(Parse(first, "A", second, "B", "--to", "New"));
        Assert.Contains("exactly ONE", failure.Message);
    }

    [Fact]
    public void Parse_ToTwice_Fails()
    {
        Assert.IsType<ParseResult.Failure>(Parse("--block", "Old", "--to", "A", "--to", "B"));
    }

    [Fact]
    public void Parse_MissingProject_Fails()
    {
        var parsed = ArgumentParser.Parse(new[] { "rename", "--block", "Old", "--to", "New" });
        var failure = Assert.IsType<ParseResult.Failure>(parsed);
        Assert.Contains("<project>", failure.Message);
    }

    [Fact]
    public void Parse_ExtraPositional_Fails()
    {
        Assert.IsType<ParseResult.Failure>(Parse("extra", "--block", "Old", "--to", "New"));
    }

    [Fact]
    public void Parse_UnknownFlag_Fails()
    {
        Assert.IsType<ParseResult.Failure>(Parse("--block", "Old", "--to", "New", "--force"));
    }

    [Fact]
    public void Parse_EmptySourceName_Fails()
    {
        Assert.IsType<ParseResult.Failure>(Parse("--block", "", "--to", "New"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" Lead")]
    [InlineData("Trail ")]
    [InlineData("Has\"Quote")]
    [InlineData("Zero\u200BWidth")]
    [InlineData("Tab\tIn")]
    public void Parse_InvalidNewName_Fails(string newName)
    {
        var failure = Assert.IsType<ParseResult.Failure>(Parse("--block", "Old", "--to", newName));
        Assert.Contains("Invalid --to", failure.Message);
    }

    [Fact]
    public void Parse_ToEqualsCurrentName_Fails()
    {
        var failure = Assert.IsType<ParseResult.Failure>(Parse("--block", "Same", "--to", "Same"));
        Assert.Contains("nothing to rename", failure.Message);
    }

    [Fact]
    public void Parse_CaseOnlyRename_IsAllowed()
    {
        Assert.Equal("MOTOR", Options("--block", "Motor", "--to", "MOTOR").NewName);
    }

    [Fact]
    public void Parse_IsCoveredByTheCommonDispatchSwitches()
    {
        var parsed = Parse("--block", "Old", "--to", "New");
        Assert.Equal(Project, ArgumentParser.ProjectIdentifier(parsed));
        Assert.Equal(ArgumentParser.DefaultTimeoutOpenSeconds, ArgumentParser.CommonOptions(parsed).TimeoutOpenSeconds);
    }

    // ---- pure rules -------------------------------------------------------------------------

    [Theory]
    [InlineData("FB_Motor")]
    [InlineData("DB Motor 1")]
    [InlineData("Ölpumpe_2")]
    public void ValidateNewName_AcceptsOrdinaryNames(string name)
    {
        Assert.Null(RenameRules.ValidateNewName(name));
    }

    [Fact]
    public void ValidateNewName_RejectsNull()
    {
        Assert.NotNull(RenameRules.ValidateNewName(null));
    }

    [Fact]
    public void FindCollisions_IsCaseInsensitive_AndNamesWhatCollided()
    {
        var existing = new[]
        {
            new ExistingName("FB_Pump", "block at PLC_1/Program blocks"),
            new ExistingName("UDT_Pump", "PLC data type at PLC_1/PLC data types"),
        };

        var hits = RenameRules.FindCollisions("fb_pump", existing);

        Assert.Single(hits);
        Assert.Equal("FB_Pump", hits[0].Name);
    }

    [Fact]
    public void FindCollisions_NoMatch_IsEmpty()
    {
        Assert.Empty(RenameRules.FindCollisions("New", new[] { new ExistingName("Old", "x") }));
    }

    [Fact]
    public void TargetExists_IsACommandError_NamingTheHolder()
    {
        var ex = new RenameTargetExistsException(
            RenameTargetKind.Block, "Old", "New", new[] { new ExistingName("NEW", "PLC data type at PLC_1") });

        Assert.Equal(ExitCodes.CommandError, ExitCodes.ForException(ex));
        Assert.Contains("PLC data type at PLC_1", ex.Message);
        Assert.Contains("Nothing was changed", ex.Message);
    }

    [Fact]
    public void SourceNotFound_IsACommandError_AndNamesANearMatch()
    {
        var ex = new RenameSourceNotFoundException(RenameTargetKind.HmiTag, "Tag_1", new[] { "Tag_1 ", "Other" });

        Assert.Equal(ExitCodes.CommandError, ExitCodes.ForException(ex));
        Assert.Contains("NEAR MATCH", ex.Message);
        Assert.Contains("2 HMI tag(s) present", ex.Message);
    }

    [Fact]
    public void Ambiguous_IsACommandError()
    {
        var ex = new AmbiguousRenameSourceException(RenameTargetKind.Type, "UDT", new[] { "PLC_1", "PLC_2" });
        Assert.Equal(ExitCodes.CommandError, ExitCodes.ForException(ex));
        Assert.Contains("--device", ex.Message);
    }

    [Fact]
    public void Refused_IsItsOwnExitCode()
    {
        var ex = new RenameRefusedException(RenameTargetKind.TechnologicalObject, "PID_1", "PID_2", new NotSupportedException("no"));

        Assert.Equal(ExitCodes.RenameRefused, ExitCodes.ForException(ex));
        Assert.NotEqual(ExitCodes.CommandError, ExitCodes.RenameRefused);
        Assert.NotEqual(ExitCodes.LayoutMismatch, ExitCodes.RenameRefused);
        Assert.Contains("did not save", ex.Message);
    }

    // ---- the command against a fake gateway -------------------------------------------------

    [Fact]
    public void Unconfirmed_IsRefused_BeforePortal()
    {
        var (exitCode, _, stderr) = CaptureConsole(() => Program.RefuseUnconfirmedRename(Options("--block", "Old", "--to", "New")));

        Assert.Equal(ExitCodes.NotConfirmed, exitCode);
        Assert.Contains("Would rename block 'Old' to 'New'", stderr);
        Assert.Contains("Portal was not contacted", stderr);
        Assert.Contains("REFERENCES NOT CHECKED", stderr);
    }

    [Fact]
    public void Confirmed_ReadBackMatches_Succeeds_AndPassesTheArgumentsThrough()
    {
        var gateway = new FakeGateway
        {
            RenameOutcome = new RenameResult(RenameTargetKind.Block, "PLC_1/Program blocks", "Old", "New", "New", 12, 12, false),
        };

        var (exitCode, stdout, _) = CaptureConsole(() =>
            Program.RunRename(gateway, Options("--block", "Old", "--to", "New", "--device", "PLC_1", "--yes"), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal((RenameTargetKind.Block, "Old", "PLC_1", "New"), gateway.LastRename);
        Assert.Contains("VERIFIED", stdout);
        Assert.Contains("(number 12)", stdout);
        Assert.Contains("REFERENCES NOT CHECKED", stdout);
    }

    [Fact]
    public void Confirmed_NothingAnswersToNewName_IsExit15()
    {
        var gateway = new FakeGateway
        {
            RenameOutcome = new RenameResult(RenameTargetKind.Type, "PLC_1", "Old", "New", null, null, null, true),
        };

        var (exitCode, stdout, stderr) = CaptureConsole(() =>
            Program.RunRename(gateway, Options("--type", "Old", "--to", "New", "--yes"), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.LayoutMismatch, exitCode);
        Assert.Contains("NOT APPLIED", stdout);
        Assert.Contains("RENAME NOT APPLIED", stderr);
        Assert.Contains("still resolves", stderr);
    }

    [Fact]
    public void Confirmed_NumberChanged_IsExit15()
    {
        var gateway = new FakeGateway
        {
            RenameOutcome = new RenameResult(RenameTargetKind.Block, "PLC_1", "Old", "New", "New", 12, 13, false),
        };

        var (exitCode, stdout, stderr) = CaptureConsole(() =>
            Program.RunRename(gateway, Options("--block", "Old", "--to", "New", "--yes"), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.LayoutMismatch, exitCode);
        Assert.Contains("CHANGED", stdout);
        Assert.Contains("went from 12 to 13", stderr);
    }

    [Fact]
    public void Confirmed_ResultMisreportingItsRequest_StillFailsClosed()
    {
        // The result claims it was asked for "Other" and read back "Other": self-consistent, so
        // Verified is true — but this invocation asked for "New", and that is what gates the exit.
        var gateway = new FakeGateway
        {
            RenameOutcome = new RenameResult(RenameTargetKind.Block, "PLC_1", "Old", "Other", "Other", 1, 1, false),
        };

        var (exitCode, _, _) = CaptureConsole(() =>
            Program.RunRename(gateway, Options("--block", "Old", "--to", "New", "--yes"), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.LayoutMismatch, exitCode);
    }

    [Fact]
    public void Confirmed_HmiTag_HasNoNumberColumn()
    {
        var gateway = new FakeGateway
        {
            RenameOutcome = new RenameResult(RenameTargetKind.HmiTag, "HMI_1/PC-System_1/Default tag table", "T1", "T2", "T2", null, null, false),
        };

        var (exitCode, stdout, _) = CaptureConsole(() =>
            Program.RunRename(gateway, Options("--hmitag", "T1", "--to", "T2", "--yes"), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.DoesNotContain("number", stdout);
    }

    [Fact]
    public void Json_CarriesTheVerdictAndSaysReferencesWereNotChecked()
    {
        var json = OutputFormatter.FormatRenameJson(
            new RenameResult(RenameTargetKind.HmiAlarm, "HMI_1", "A1", "A2", "A2", 7, 7, false));

        Assert.Contains("\"kind\": \"HmiAlarm\"", json);
        Assert.Contains("\"numberLabel\": \"alarm ID\"", json);
        Assert.Contains("\"verified\": true", json);
        Assert.Contains("\"referencesChecked\": false", json);
    }

    private static (int ExitCode, string StdOut, string StdErr) CaptureConsole(Func<int> action)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            var exitCode = action();
            return (exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}
