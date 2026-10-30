using System;
using System.IO;
using System.Linq;
using OpennessCli;
using OpennessCli.Cli;
using OpennessCli.Model;
using Xunit;

namespace OpennessCli.Tests;

/// <summary>
/// `ob-cycle` — reading, asserting and setting a cyclic-interrupt OB's cycle time. A V20 export
/// carries no cycle time, so a set that silently no-ops would be invisible to every other check;
/// these run against <see cref="FakeGateway"/> to prove that case FAILS.
/// </summary>
public class ObCycleTests
{
    private static ObCycleCommandOptions Options(params string[] extra)
    {
        var args = new[] { "ob-cycle", "C:\\proj\\My.ap20", "--block", "Cyclic" };
        var parsed = ArgumentParser.Parse(args.Concat(extra).ToArray());
        return Assert.IsType<ParseResult.ObCycleSuccess>(parsed).Options;
    }

    [Fact]
    public void Parse_ReadOnly_IsTheDefault()
    {
        var options = Options();
        Assert.Null(options.Set);
        Assert.Null(options.Expect);
        Assert.False(options.Confirm);
    }

    [Fact]
    public void Parse_SetWithPhaseAndYes()
    {
        var options = Options("--set", "100", "--phase", "5", "--yes");
        Assert.Equal(100, options.Set);
        Assert.Equal(5, options.Phase);
        Assert.True(options.Confirm);
    }

    [Theory]
    [InlineData("100ms")]
    [InlineData("-1")]
    [InlineData("1.5")]
    public void Parse_NonIntegerMillisecondValue_Fails(string raw)
    {
        var result = ArgumentParser.Parse(new[] { "ob-cycle", "C:\\proj\\My.ap20", "--block", "Cyclic", "--set", raw });
        var failure = Assert.IsType<ParseResult.Failure>(result);
        Assert.Contains("MILLISECONDS", failure.Message);
    }

    [Fact]
    public void Parse_SetAndExpect_AreMutuallyExclusive()
    {
        var result = ArgumentParser.Parse(new[] { "ob-cycle", "p.ap20", "--block", "C", "--set", "100", "--expect", "100" });
        Assert.IsType<ParseResult.Failure>(result);
    }

    [Fact]
    public void Parse_PhaseWithoutSet_Fails()
    {
        var result = ArgumentParser.Parse(new[] { "ob-cycle", "p.ap20", "--block", "C", "--phase", "0" });
        Assert.IsType<ParseResult.Failure>(result);
    }

    [Fact]
    public void Parse_SetZero_Fails()
    {
        var result = ArgumentParser.Parse(new[] { "ob-cycle", "p.ap20", "--block", "C", "--set", "0" });
        Assert.IsType<ParseResult.Failure>(result);
    }

    [Fact]
    public void UnconfirmedSet_IsRefused_WithoutTouchingTheGateway()
    {
        var (exitCode, _, stderr) = CaptureConsole(() => Program.RefuseUnconfirmedObCycle(Options("--set", "250")));

        Assert.Equal(ExitCodes.NotConfirmed, exitCode);
        Assert.Contains("250 ms", stderr);
        Assert.Contains("Portal was not contacted", stderr);
    }

    [Fact]
    public void Read_PrintsBothValues()
    {
        var gateway = new FakeGateway { CyclicTimeMs = 100, PhaseShiftMs = 0 };

        var (exitCode, stdout, _) = CaptureConsole(() => Program.RunObCycle(gateway, Options(), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("CYCLE TIME: 100 ms", stdout);
        Assert.Contains("PHASE SHIFT: 0 ms", stdout);
        Assert.Equal(0, gateway.ObCycleSetCalls);
    }

    [Fact]
    public void Expect_Mismatch_IsExit15()
    {
        var gateway = new FakeGateway { CyclicTimeMs = 250 };

        var (exitCode, _, stderr) = CaptureConsole(() => Program.RunObCycle(gateway, Options("--expect", "100"), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.LayoutMismatch, exitCode);
        Assert.Contains("CYCLE TIME MISMATCH", stderr);
    }

    [Fact]
    public void Set_ReadBackMatches_Succeeds()
    {
        var gateway = new FakeGateway { CyclicTimeMs = 100 };

        var (exitCode, stdout, _) = CaptureConsole(() =>
            Program.RunObCycle(gateway, Options("--set", "250", "--yes"), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(250, gateway.CyclicTimeMs);
        Assert.Contains("VERIFIED", stdout);
    }

    [Fact]
    public void Set_SilentNoOp_IsExit15_NotSuccess()
    {
        var gateway = new FakeGateway { CyclicTimeMs = 100, IgnoreSets = true };

        var (exitCode, stdout, stderr) = CaptureConsole(() =>
            Program.RunObCycle(gateway, Options("--set", "250", "--yes"), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.LayoutMismatch, exitCode);
        Assert.Contains("NOT APPLIED", stdout);
        Assert.Contains("SET NOT APPLIED", stderr);
    }

    [Fact]
    public void NotACyclicOb_IsACommandError_NamingTheAttributesItHas()
    {
        var ex = new ObCycleUnavailableException("Main", "read", new[] { "Name", "Number" });

        Assert.Equal(ExitCodes.CommandError, ExitCodes.ForException(ex));
        Assert.Contains("Name, Number", ex.Message);
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
