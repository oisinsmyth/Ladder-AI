using Harness.Run;
using Harness.Wire;

namespace Harness.Loop.Tests;

/// <summary>
/// <b><c>harness-run</c> — the entry point <c>Harness.Loop</c> did not have.</b>
///
/// <para>The loop could be unit-tested and could not be RUN: the only executables were Gate, RigRead and
/// RigWrite. These tests exercise the whole CLI <b>without a process and without a socket</b>, because
/// the transport arrives as a factory — a decision only reachable through a process is a decision nobody
/// tests.</para>
/// </summary>
public class LoopCliTests
{
    private const string Submission = """
    {
      "blockAuthor": "agent-a",
      "runtimeCompression": 1,
      "slotsInWaveSet": 1,
      "resultRegistersPerSlot": 4,
      "computedConflicts": [],
      "model": { "id": "M", "represents": ["ramp"], "validatedAgainstPlantData": true },
      "enumeration": { "clauses": ["REQ-1"], "assertions": ["REQ-1:aaaaaa"] },
      "map": { "providedFor": { "Demo_Count": ["Sampled"] } },
      "vectors": []
    }
    """;

    private const string Binding = """
    {
      "blockNumber": 9001,
      "baseByte": 1000,
      "slots": [{
        "slotId": "HBA",
        "vectorTargets": [{ "tag": "Stim_Total", "type": "Time" }],
        "startCondition": "Stim_Start",
        "resultSources": [{ "tag": "Alarm", "type": "Bool" }]
      }]
    }
    """;

    private static (int Exit, string Output, List<string> Written) Run(
        string[] args,
        Func<string, int, byte, IRegisterTransport>? connect = null,
        Func<string, string?>? env = null,
        string? allowlistPath = null)
    {
        var writer = new StringWriter();
        var written = new List<string>();

        var exit = LoopCli.Run(args, writer,
            path => path switch
            {
                "sub.json" => Submission,
                "binding.json" => Binding,
                _ => throw new FileNotFoundException(path),
            },
            (path, _) => written.Add(path),
            connect,
            env ?? (_ => null));

        return (exit, writer.ToString(), written);
    }

    /// <summary>
    /// A REAL allowlist file, because <see cref="DeviceGuard.DeviceAccessGuard"/> loads from the
    /// filesystem — it is a fence, and a fence that could be satisfied by a string a test handed it
    /// would not be one.
    /// </summary>
    private static string WriteAllowlist(string address = "10.10.10.10")
    {
        var path = Path.Combine(Path.GetTempPath(), $"loopcli-allow-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, $$"""{ "entries": [ { "address": "{{address}}", "label": "fixture rig", "kind": "test-rig" } ] }""");
        return path;
    }

    [Fact]
    public void NO_ARGUMENTS_PRINTS_USAGE_AND_EXITS_NOTHING_EXAMINED_never_zero()
    {
        var (exit, output, _) = Run(Array.Empty<string>());

        Assert.Equal(LoopExit.NothingExamined, exit);
        Assert.Contains("usage: harness-run", output, StringComparison.Ordinal);

        // The usage text has to say what --verify is, because the alternative reading — "a flag that
        // skips the download" — is exactly the gateway that lies.
        Assert.Contains("reads the build stamp off the device", output, StringComparison.Ordinal);
        Assert.Contains("There is no flag that asserts a deployment instead of", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_BINDING_IS_REQUIRED_because_a_guessed_one_mirrors_the_wrong_things()
    {
        var (exit, output, _) = Run(new[] { "--submission", "sub.json" });

        Assert.Equal(LoopExit.NothingExamined, exit);
        Assert.Contains("--binding is required", output, StringComparison.Ordinal);
    }

    [Fact]
    public void WITHOUT_VERIFY_THE_GATEWAY_REFUSES_and_that_is_a_real_outcome_not_a_stub()
    {
        var (exit, output, _) = Run(new[] { "--submission", "sub.json", "--binding", "binding.json" });

        Assert.Equal(LoopExit.DidNotRun, exit);
        Assert.Contains("gateway     : REFUSING", output, StringComparison.Ordinal);
        Assert.Contains("NO PACKAGES", output, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // THE FENCE RUNS BEFORE THE SOCKET
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void WITH_NO_ALLOWLIST_EVERY_TARGET_IS_REFUSED_AND_NO_SOCKET_IS_OPENED()
    {
        var opened = 0;

        var (exit, output, _) = Run(
            new[] { "--submission", "sub.json", "--binding", "binding.json", "--verify", "--host", "10.10.10.10" },
            connect: (_, _, _) => { opened++; throw new InvalidOperationException("must not be reached"); });

        Assert.Equal(LoopExit.Refused, exit);
        Assert.Contains("no allowlist configured", output, StringComparison.Ordinal);

        // *** THE ORDERING IS THE POINT. *** A check performed after the bytes have moved authorizes nothing.
        Assert.Equal(0, opened);
    }

    [Fact]
    public void A_TARGET_NOT_ON_THE_ALLOWLIST_IS_REFUSED_AND_NO_SOCKET_IS_OPENED()
    {
        var opened = 0;

        var (exit, output, _) = Run(
            new[] { "--submission", "sub.json", "--binding", "binding.json", "--verify", "--host", "192.0.2.9", "--allowlist", WriteAllowlist() },
            connect: (_, _, _) => { opened++; throw new InvalidOperationException("must not be reached"); });

        Assert.Equal(LoopExit.Refused, exit);
        Assert.Contains("the device fence refused", output, StringComparison.Ordinal);
        Assert.Equal(0, opened);
    }

    [Fact]
    public void VERIFY_WITHOUT_A_HOST_IS_NOTHING_EXAMINED_because_there_is_no_offline_verification()
    {
        var (exit, output, _) = Run(new[] { "--submission", "sub.json", "--binding", "binding.json", "--verify" });

        Assert.Equal(LoopExit.NothingExamined, exit);
        Assert.Contains("a stamp nobody read is not evidence", output, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // The gateway surface, pinned at the CLI
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void THE_ONLY_TWO_GATEWAYS_REACHABLE_FROM_THIS_BINARY_ARE_VERIFYING_AND_REFUSING()
    {
        // 🔴 The CLI is what an operator runs, so a fake gateway reachable from here is a fake gateway in
        // production. The composition is a single ternary and this asserts BOTH of its arms rather than
        // only the one a happy path takes.
        var refusing = Run(new[] { "--submission", "sub.json", "--binding", "binding.json" }).Output;
        Assert.Contains("REFUSING", refusing, StringComparison.Ordinal);

        var verifying = Run(
            new[] { "--submission", "sub.json", "--binding", "binding.json", "--verify", "--host", "10.10.10.10", "--allowlist", WriteAllowlist() },
            connect: (_, _, _) => throw new IOException("no route")).Output;

        Assert.Contains("VERIFYING", verifying, StringComparison.Ordinal);
        Assert.Contains("Loaded` comes from the measurement and from nowhere else", verifying, StringComparison.Ordinal);

        // And an unreachable device does NOT become a run: the transport threw, and the loop stopped.
        Assert.DoesNotContain("PACKAGES\n", verifying, StringComparison.Ordinal);
    }

    [Fact]
    public void AN_EMPTY_SUBMISSION_IS_REFUSED_BEFORE_THE_DEVICE_IS_EVER_READ()
    {
        // The fixture carries no vectors, and the gate stops the loop at "0 submission" — BEFORE the
        // gateway is asked for anything. That ordering matters: an empty submission must not become a
        // device round trip, and *** EMPTY IS NOT CLEAN *** — it exits DidNotRun, never 0.
        //
        // The unreachable-device path itself is covered where it lives, in
        // VerifyingDeviceGatewayTests.A_TRANSPORT_THAT_WILL_NOT_OPEN_IS_NOT_CHECKED_and_refuses.
        var opened = 0;

        var (exit, output, _) = Run(
            new[] { "--submission", "sub.json", "--binding", "binding.json", "--verify", "--host", "10.10.10.10", "--allowlist", WriteAllowlist() },
            connect: (_, _, _) => { opened++; throw new IOException("no route to host"); });

        Assert.Equal(LoopExit.DidNotRun, exit);
        Assert.Contains("NotAdmissible", output, StringComparison.Ordinal);
        Assert.Equal(0, opened);
    }
}
