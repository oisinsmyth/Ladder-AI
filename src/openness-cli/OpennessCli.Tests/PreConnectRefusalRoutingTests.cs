using System;
using System.Collections.Generic;
using System.IO;
using OpennessCli;
using OpennessCli.Cli;
using OpennessCli.Model;
using OpennessCli.Openness;
using Xunit;

namespace OpennessCli.Tests;

/// <summary>
/// THE ROUTING, NOT THE MESSAGE (2026-08-14).
///
/// <para><b>Every pre-Connect refusal in this binary was tested by calling its refusal helper
/// directly.</b> That checks what the refusal SAYS. It does not check that <see cref="Program"/>
/// ever reaches it — and for <c>block-layout --set</c> that distinction is the whole guard:
/// <c>RunBlockLayout</c> does not re-check <c>Confirm</c>, so the single <c>if</c> in
/// <c>Program.Run</c> is all that stands between a missing <c>--yes</c> and a write that DESTROYS
/// THE BLOCK'S RETAINED DATA on the next download.</para>
///
/// <para>*** MEASURED: commenting that arm out left all 712 tests green. *** This file is the
/// did-not-run test for it. The mutation now fails
/// <see cref="BlockLayoutSetWithoutYes_NeverReachesConnect"/> on the sentinel, before the exit code
/// is even considered.</para>
///
/// <para><b>The assertion is the observable consequence, not the exit code.</b> A refusal returning
/// 10 after Portal had already been attached would look identical on the exit code alone, and
/// "Portal was not contacted" is the property that actually matters. So the gateway records its own
/// <c>Connect</c>, exactly as the <c>-Arm</c> fence's stub <c>openness-cli</c> records its own
/// launch. <b>And the instrument is CONTROLLED</b> — see
/// <see cref="AConfirmedCommand_DoesReachConnect_SoTheSentinelMeansSomething"/>, without which
/// "Connect was not called" is also what a broken gateway or a test that never ran looks like.</para>
/// </summary>
public class PreConnectRefusalRoutingTests
{
    /// <summary>
    /// Drives <c>Program.Run</c> — the real dispatch path — with the Portal half replaced by
    /// <see cref="FakeGateway"/>, whose <c>Connect</c> RECORDS rather than throws. A throw would also
    /// produce a non-zero exit, so "the gate held" and "the gate broke and the gateway blew up" would
    /// be told apart only by reading a message; a counter is a positive observation of the thing that
    /// must not happen.
    /// </summary>
    private static (int ExitCode, FakeGateway Gateway, string StdErr) RunCli(params string[] args)
    {
        var gateway = new FakeGateway { Current = MemoryLayoutKind.Optimized };
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var stderr = new StringWriter();

        try
        {
            Console.SetOut(new StringWriter());
            Console.SetError(stderr);
            var exit = Program.Run(args, () => gateway);
            return (exit, gateway, stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    /// <summary>
    /// *** THE ONE THAT MATTERS. *** Disconnect the arm in <c>Program.Run</c> and this fails on the
    /// sentinel — <c>Connect</c>, <c>OpenProject</c> and <c>SetBlockMemoryLayout</c> would all have
    /// been called, because <c>RunBlockLayout</c> has no <c>Confirm</c> check of its own.
    /// </summary>
    [Fact]
    public void BlockLayoutSetWithoutYes_NeverReachesConnect()
    {
        var (exit, gateway, stderr) = RunCli(
            "block-layout", @"C:\proj\My.ap20", "--block", "DB_Marker", "--set", "Standard");

        Assert.Equal(0, gateway.ConnectCalls);
        Assert.Equal(0, gateway.OpenProjectCalls);

        // Stated separately from Connect: reaching the destructive call is a strictly worse outcome
        // than merely attaching, and collapsing them into one assertion would hide which happened.
        Assert.Equal(0, gateway.SetCalls);
        Assert.Equal(0, gateway.SaveCalls);

        Assert.Equal(ExitCodes.NotConfirmed, exit);
        Assert.Contains("Portal was not contacted", stderr, StringComparison.Ordinal);
    }

    /// <summary>
    /// A <c>--set</c> WITH <c>--yes</c> must not be refused. A gate that fires on the cases it was
    /// not built for is noise, and noise gets switched off — after which the cases it was right
    /// about go through unchecked.
    /// </summary>
    [Fact]
    public void BlockLayoutSetWithYes_IsNotRefused_AndDoesReachTheSet()
    {
        var (exit, gateway, _) = RunCli(
            "block-layout", @"C:\proj\My.ap20", "--block", "DB_Marker", "--set", "Standard", "--yes");

        Assert.NotEqual(ExitCodes.NotConfirmed, exit);
        Assert.Equal(1, gateway.SetCalls);
    }

    /// <summary>
    /// *** THE CONTROL FOR THE INSTRUMENT. *** Without it, <c>ConnectCalls == 0</c> above is equally
    /// what a gateway that never gets constructed, a parse that failed for an unrelated reason, or a
    /// test that never ran would produce. A plain READ carries no <c>--yes</c> gate, so it must go
    /// all the way through.
    /// </summary>
    [Fact]
    public void AConfirmedCommand_DoesReachConnect_SoTheSentinelMeansSomething()
    {
        var (exit, gateway, _) = RunCli("block-layout", @"C:\proj\My.ap20", "--block", "DB_Marker");

        Assert.Equal(1, gateway.ConnectCalls);
        Assert.Equal(1, gateway.OpenProjectCalls);
        Assert.Equal(ExitCodes.Success, exit);
    }

    /// <summary>
    /// The other pre-Connect refusals share the seam and had the same gap, so they are pinned in the
    /// same breath. Each is a WRITE whose unconfirmed form must answer from the arguments alone.
    /// </summary>
    [Theory]
    [InlineData("hmi-create-screen", "--name", "Screen_1")]
    [InlineData("hmi-new", "--kind", "screen", "--name", "Screen_1")]
    [InlineData("hmi-delete", "--kind", "screen", "--name", "Screen_1")]
    [InlineData("hmi-create-tag", "--name", "Tag_1", "--table", "Tags", "--datatype", "Bool")]
    public void EveryUnconfirmedWrite_NeverReachesConnect(string subcommand, params string[] rest)
    {
        var args = new List<string> { subcommand, @"C:\proj\My.ap20" };
        args.AddRange(rest);

        var (exit, gateway, _) = RunCli(args.ToArray());

        Assert.Equal(0, gateway.ConnectCalls);

        // NOT merely "some non-zero exit". A UsageError would also leave ConnectCalls at 0 while
        // proving nothing about the confirm gate — the arguments would never have parsed, so the
        // gate would never have been asked. Pinning 10 keeps this case from silently degrading into
        // a test of the argument parser.
        Assert.Equal(ExitCodes.NotConfirmed, exit);
    }
}
