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

    /// <summary>
    /// One block under test, as IR. <b>Not a fixture standing in for one</b> — the loader classifies it
    /// from this header exactly as it will classify a real export.
    /// </summary>
    private const string BlockIr = "BLOCK FC FC_DemoRamp\n  NUMBER 901\n  NETWORK 1 \"ramp\"\n";

    /// <summary>A SECOND, different block — so "two objects" is a different stamp for a real reason.</summary>
    private const string SecondBlockIr = "BLOCK FC FC_DemoHold\n  NUMBER 902\n  NETWORK 1 \"hold\"\n";

    /// <summary>
    /// The two arguments every test that is not ABOUT the program under test has to carry now.
    ///
    /// <para><b>Declaring the absence is the point.</b> The build stamp is a hash of what will run, so an
    /// empty program set is a real claim with a consequence — it can only confirm against a device
    /// carrying no block under test — and it must be somebody's claim rather than a default.</para>
    /// </summary>
    private static readonly string[] NoProgram = { "--no-program-under-test" };

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
                "ramp.ir" => BlockIr,
                "empty.ir" => "\n\n",
                "type.ir" => "TYPE TypeDOL\n  MEMBERS\n",
                "junk.ir" => "PROGRAM Whatever\n",
                "table.ir" => "TAGTABLE Default tag table\n  TAGS\n",
                "dup.ir" => BlockIr,
                "hold.ir" => SecondBlockIr,
                "good-sub.json" => GateParityTests.SubmissionJson(),
                "good-binding.json" => GateParityTests.BindingJson(),
                _ => throw new FileNotFoundException(path),
            },
            (path, _) => written.Add(path),
            connect,
            env ?? (_ => null),
            // A directory is expanded by the caller in production; here it is injected so the loader is
            // exercised without a filesystem, exactly as the transport is.
            path => path switch
            {
                "progdir" => new[] { "ramp.ir", "table.ir" },
                "twoblocks" => new[] { "ramp.ir", "hold.ir" },
                "emptydir" => Array.Empty<string>(),
                _ => new[] { path },
            });

        return (exit, writer.ToString(), written);
    }

    private static string[] Args(params string[] args) => args.Concat(NoProgram).ToArray();

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
        var (exit, output, _) = Run(Args("--submission", "sub.json"));

        Assert.Equal(LoopExit.NothingExamined, exit);
        Assert.Contains("--binding is required", output, StringComparison.Ordinal);
    }

    [Fact]
    public void WITHOUT_VERIFY_THE_GATEWAY_REFUSES_and_that_is_a_real_outcome_not_a_stub()
    {
        var (exit, output, _) = Run(Args("--submission", "sub.json", "--binding", "binding.json"));

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
            Args("--submission", "sub.json", "--binding", "binding.json", "--verify", "--host", "10.10.10.10", "--port", "503"),
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
            Args("--submission", "sub.json", "--binding", "binding.json", "--verify", "--host", "192.0.2.9", "--port", "503", "--allowlist", WriteAllowlist()),
            connect: (_, _, _) => { opened++; throw new InvalidOperationException("must not be reached"); });

        Assert.Equal(LoopExit.Refused, exit);
        Assert.Contains("the device fence refused", output, StringComparison.Ordinal);
        Assert.Equal(0, opened);
    }

    [Fact]
    public void VERIFY_WITHOUT_A_HOST_IS_NOTHING_EXAMINED_because_there_is_no_offline_verification()
    {
        var (exit, output, _) = Run(Args("--submission", "sub.json", "--binding", "binding.json", "--verify", "--port", "503"));

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
        var refusing = Run(Args("--submission", "sub.json", "--binding", "binding.json")).Output;
        Assert.Contains("REFUSING", refusing, StringComparison.Ordinal);

        var verifying = Run(
            Args("--submission", "sub.json", "--binding", "binding.json", "--verify", "--host", "10.10.10.10", "--port", "503", "--allowlist", WriteAllowlist()),
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
            Args("--submission", "sub.json", "--binding", "binding.json", "--verify", "--host", "10.10.10.10", "--port", "503", "--allowlist", WriteAllowlist()),
            connect: (_, _, _) => { opened++; throw new IOException("no route to host"); });

        Assert.Equal(LoopExit.DidNotRun, exit);
        Assert.Contains("NotAdmissible", output, StringComparison.Ordinal);
        Assert.Equal(0, opened);
    }

    // ---------------------------------------------------------------------------------------------
    // THE PROGRAM UNDER TEST — the input this CLI had no way to take
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void NEITHER_program_NOR_no_program_IS_NOTHING_EXAMINED_because_the_empty_set_is_not_a_safe_default()
    {
        // 🔴 It used to be Array.Empty<HarnessObject>() with no flag behind it, so the BUILD STAMP was
        // computed over an empty program set on EVERY run — a stamp that cannot match a rig carrying the
        // block under test. Silence is no longer one of the options.
        var (exit, output, _) = Run(new[] { "--submission", "sub.json", "--binding", "binding.json" });

        Assert.Equal(LoopExit.NothingExamined, exit);
        Assert.Contains("no program under test was declared", output, StringComparison.Ordinal);
        Assert.Contains("CANNOT MATCH a rig carrying the block under test", output, StringComparison.Ordinal);
    }

    [Fact]
    public void BOTH_program_AND_no_program_IS_A_CONTRADICTION_AND_IS_REFUSED_rather_than_resolved()
    {
        var (exit, output, _) = Run(new[]
        {
            "--submission", "sub.json", "--binding", "binding.json",
            "--program", "ramp.ir", "--no-program-under-test",
        });

        Assert.Equal(LoopExit.NothingExamined, exit);
        Assert.Contains("contradict each other", output, StringComparison.Ordinal);
    }

    [Fact]
    public void THE_STAMP_MOVES_WITH_THE_PROGRAM_UNDER_TEST_which_is_the_whole_point_of_supplying_one()
    {
        // *** THE MUTATION THAT MATTERS. *** A --program flag that loaded objects and did not reach
        // BuildStamp.Of would look identical from the inventory line, and every one of these runs would
        // publish the same stamp — which is exactly the defect being fixed, wearing a fix's clothes.
        var withProgram = Stamp(Run(new[]
        {
            "--submission", "sub.json", "--binding", "binding.json", "--generate-only", "--program", "ramp.ir",
        }).Output);

        var without = Stamp(Run(Args("--submission", "sub.json", "--binding", "binding.json", "--generate-only")).Output);

        var withTwo = Stamp(Run(new[]
        {
            "--submission", "sub.json", "--binding", "binding.json", "--generate-only", "--program", "twoblocks",
        }).Output);

        Assert.NotEqual(without, withProgram);
        Assert.NotEqual(withProgram, withTwo);

        // And the CONVERSE, without which the two above would pass against a stamp that is simply random:
        // the same program set twice is the same stamp, so the stamp identifies a build rather than a run.
        Assert.Equal(withProgram, Stamp(Run(new[]
        {
            "--submission", "sub.json", "--binding", "binding.json", "--generate-only", "--program", "ramp.ir",
        }).Output));
    }

    /// <summary>The BUILD STAMP line's value, read out of the report the operator reads.</summary>
    private static string Stamp(string output)
    {
        var line = output.Split('\n').FirstOrDefault(l => l.StartsWith("BUILD STAMP", StringComparison.Ordinal));
        Assert.NotNull(line);
        return line!;
    }

    [Fact]
    public void THE_INVENTORY_NAMES_WHAT_THE_STAMP_WAS_TAKEN_OVER_including_the_empty_case()
    {
        var loaded = Run(new[]
        {
            "--submission", "sub.json", "--binding", "binding.json", "--generate-only", "--program", "progdir",
        }).Output;

        Assert.Contains("2 object(s) under test", loaded, StringComparison.Ordinal);
        Assert.Contains("FC_DemoRamp", loaded, StringComparison.Ordinal);

        // A tag table's name is the REST of the line, because a real one is called `Default tag table`.
        Assert.Contains("Default tag table", loaded, StringComparison.Ordinal);

        // Printed on the empty run too: a report that appears only when there is something to say teaches
        // a reader that its absence means it did not run.
        Assert.Contains("program     : NONE", Run(Args("--submission", "sub.json", "--binding", "binding.json", "--generate-only")).Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("junk.ir", "not one of the IR top-level forms")]
    [InlineData("type.ir", "declares a PLC data type")]
    [InlineData("empty.ir", "holds no IR at all")]
    [InlineData("emptydir", "expanded to no .ir file")]
    public void AN_IR_FILE_THIS_LOADER_CANNOT_CLASSIFY_IS_A_REFUSAL_NAMING_IT_never_a_skip(string path, string expected)
    {
        // *** A FILE QUIETLY LEFT OUT OF THE SET IS LEFT OUT OF THE STAMP, *** and the version register
        // would then confirm a build that is not the one running — the one thing the stamp exists to stop.
        var (exit, output, _) = Run(new[] { "--submission", "sub.json", "--binding", "binding.json", "--program", path });

        Assert.Equal(LoopExit.NothingExamined, exit);
        Assert.Contains("the PROGRAM UNDER TEST could not be loaded", output, StringComparison.Ordinal);
        Assert.Contains(expected, output, StringComparison.Ordinal);
    }

    [Fact]
    public void TWO_FILES_DECLARING_ONE_OBJECT_ARE_REFUSED_because_the_stamp_could_not_say_which_one_ran()
    {
        var (exit, output, _) = Run(new[]
        {
            "--submission", "sub.json", "--binding", "binding.json", "--program", "ramp.ir", "dup.ir",
        });

        Assert.Equal(LoopExit.NothingExamined, exit);
        Assert.Contains("which one is running would not be recoverable", output, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // --port: no default, because the one it had was wrong for the only rig there is
    // ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("70000")]
    [InlineData("five-oh-three")]
    public void VERIFY_WITHOUT_A_USABLE_PORT_IS_NOTHING_EXAMINED_AND_NO_SOCKET_IS_OPENED(string? port)
    {
        // 🔴 It defaulted to 502. This rig serves 503 and REFUSES 502 (measured) — and 502 is the port
        // every other Modbus device answers on, so the failure that is not loud is reading a DIFFERENT
        // device and believing it.
        var opened = 0;

        var args = new List<string> { "--submission", "sub.json", "--binding", "binding.json", "--verify", "--host", "10.10.10.10", "--allowlist", WriteAllowlist() };
        if (port is not null)
        {
            args.Add("--port");
            args.Add(port);
        }

        var (exit, output, _) = Run(
            args.Concat(NoProgram).ToArray(),
            connect: (_, _, _) => { opened++; throw new InvalidOperationException("must not be reached"); });

        Assert.Equal(LoopExit.NothingExamined, exit);
        Assert.Contains("--verify needs --port", output, StringComparison.Ordinal);
        Assert.Equal(0, opened);
    }

    [Fact]
    public void THE_PORT_THAT_WAS_GIVEN_IS_THE_PORT_THAT_IS_OPENED_and_nothing_supplies_a_default()
    {
        // The converse, and it is what stops "no default" from being satisfied by refusing everything: a
        // stated port reaches the fence line AND the transport factory, unchanged.
        //
        // It uses the ADMISSIBLE submission, deliberately: the gate stops an empty one before any gateway
        // is asked for a transport, so against that fixture the factory is never called and this test
        // would have passed while measuring nothing.
        var seen = new List<(string Host, int Port, byte Unit)>();

        var (_, output, _) = Run(
            Args("--submission", "good-sub.json", "--binding", "good-binding.json", "--verify", "--host", "10.10.10.10", "--port", "503", "--unit", "7", "--allowlist", WriteAllowlist()),
            connect: (h, p, unit) => { seen.Add((h, p, unit)); throw new IOException("no route"); });

        Assert.Contains("ALLOWED 10.10.10.10:503 unit 7", output, StringComparison.Ordinal);
        Assert.Equal(new[] { ("10.10.10.10", 503, (byte)7) }, seen);
    }
}
