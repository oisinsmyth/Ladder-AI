using Harness.RigControl;
using Harness.S7;

namespace Harness.RigControl.Tests;

/// <summary>
/// The command line end to end, driven through the entry point a caller actually uses.
///
/// <para>*** WHY THROUGH THE ENTRY POINT AND NOT AGAINST THE FENCE HELPER. *** Measured in this
/// repository on 2026-08-14: the <c>--yes</c> gate on <c>block-layout --set</c> — the only thing
/// between a missing flag and a write that destroys retained data — can be disconnected with a
/// one-token mutation while 712 of 712 tests stay green, because its test calls the refusal helper
/// DIRECTLY and pins the MESSAGE rather than the ROUTING. Every test here goes through
/// <see cref="RigControlCli.Run"/>, and every refusal asserts the OBSERVABLE CONSEQUENCE — that the
/// transport factory was never called — alongside the exit code, which a disconnected gate can produce
/// by accident.</para>
/// </summary>
public class RigControlCliTests : IDisposable
{
    private const string Rig = "10.10.10.10";
    private const string OrderCode = "6ES7 214-1AG40-0XB0";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ladder-rigcontrol-" + Guid.NewGuid().ToString("N"));

    public RigControlCliTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        GC.SuppressFinalize(this);
    }

    // ---------------------------------------------------------------- fixtures

    private string Allowlist(string json)
    {
        var path = Path.Combine(_dir, Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, json);
        return path;
    }

    /// <summary>The live entry as it stands on this machine on 2026-08-14: NOT write-eligible.</summary>
    private string LiveShaped() => Allowlist(
        $$"""
        { "entries": [ {
            "address": "10.10.10.10",
            "label": "Bench rig",
            "kind": "test-rig",
            "orderNumber": "{{OrderCode}}",
            "serialNumber": "S C-EXAMPLE00001",
            "writeEligible": false,
            "outputsIsolated": true,
            "isolationAssertedBy": "Owner",
            "isolationAssertedDate": "2026-08-11"
        } ] }
        """);

    /// <summary>Everything the fence asks for. The ONLY fixture that reaches a device.</summary>
    private string FullyEligible() => Allowlist(
        $$"""
        { "entries": [ {
            "address": "10.10.10.10",
            "label": "Bench rig",
            "kind": "test-rig",
            "orderNumber": "{{OrderCode}}",
            "serialNumber": "S C-EXAMPLE00001",
            "writeEligible": true,
            "outputsIsolated": true,
            "isolationAssertedBy": "Owner"
        } ] }
        """);

    private sealed class CountingFactory
    {
        private readonly IRunTransitionTransport _transport;

        public CountingFactory(IRunTransitionTransport transport) => _transport = transport;

        public int Calls { get; private set; }

        public Func<IRunTransitionTransport> Factory => () =>
        {
            Calls++;
            return _transport;
        };
    }

    private sealed record Outcome(int Exit, string Out, string Err, int FactoryCalls);

    private static Outcome Run(IRunTransitionTransport transport, params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var factory = new CountingFactory(transport);

        var exit = RigControlCli.Run(args, _ => null, factory.Factory, stdout, stderr);
        return new Outcome(exit, stdout.ToString(), stderr.ToString(), factory.Calls);
    }

    /// <summary>The default form: nothing may be touched.</summary>
    private static Outcome Run(params string[] args) => Run(new NeverTouchedTransport(), args);

    // ================================================================ the fence refuses

    /// <summary>
    /// *** THE REFUSAL THIS LANE WAS BUILT TO PRODUCE. *** The live allowlist entry reads
    /// <c>"writeEligible": false</c>, which is a HUMAN AUTHORISATION and not a configuration value, so
    /// the correct behaviour today is to refuse — by name, without opening a socket, without editing
    /// anything.
    /// </summary>
    [Fact]
    public void The_live_entry_is_refused_by_name_at_the_writeEligible_gate()
    {
        var result = Run("--run", "--target", Rig, "--allowlist", LiveShaped(), "--yes");

        Assert.Equal(RigControlCli.ExitRefused, result.Exit);
        Assert.Equal(0, result.FactoryCalls);
        Assert.Contains("NotWriteEligible", result.Out);
        Assert.Contains("writeEligible", result.Out);
        Assert.Contains("no connection attempted", result.Out, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// An absent fence must not read as an open one — and it must not read as a USAGE error either,
    /// which is a different fact and exits differently.
    /// </summary>
    [Fact]
    public void With_no_allowlist_configured_the_target_is_refused_not_merely_unusable()
    {
        var result = Run("--run", "--target", Rig, "--yes");

        Assert.Equal(RigControlCli.ExitRefused, result.Exit);
        Assert.Equal(0, result.FactoryCalls);
        Assert.Contains("NoAllowlistConfigured", result.Out);
    }

    [Fact]
    public void A_missing_allowlist_FILE_is_refused()
    {
        var result = Run("--run", "--target", Rig, "--yes",
            "--allowlist", Path.Combine(_dir, "there-is-no-such-file.json"));

        Assert.Equal(RigControlCli.ExitRefused, result.Exit);
        Assert.Equal(0, result.FactoryCalls);
        Assert.Contains("AllowlistUnusable", result.Out);
    }

    [Fact]
    public void A_malformed_allowlist_is_refused_rather_than_read_as_empty()
    {
        var result = Run("--run", "--target", Rig, "--yes", "--allowlist", Allowlist("{ not json at all"));

        Assert.Equal(RigControlCli.ExitRefused, result.Exit);
        Assert.Equal(0, result.FactoryCalls);
        Assert.Contains("AllowlistUnusable", result.Out);
    }

    [Theory]
    [InlineData("\"kind\": \"production\"", "NotAnApprovedTestRig")]
    [InlineData("\"kind\": \"test-rig\", \"writeEligible\": false", "NotWriteEligible")]
    [InlineData("\"kind\": \"test-rig\", \"writeEligible\": true", "OutputsNotIsolated")]
    [InlineData("\"kind\": \"test-rig\", \"writeEligible\": true, \"outputsIsolated\": true", "IsolationUnattributed")]
    public void Each_gate_refuses_by_name_and_opens_nothing(string fields, string expectedGate)
    {
        var path = Allowlist($$"""
            { "entries": [ { "address": "10.10.10.10", "label": "rig",
                "orderNumber": "{{OrderCode}}", {{fields}} } ] }
            """);

        var result = Run("--run", "--target", Rig, "--allowlist", path, "--yes");

        Assert.Equal(RigControlCli.ExitRefused, result.Exit);
        Assert.Equal(0, result.FactoryCalls);
        Assert.Contains(expectedGate, result.Out);
    }

    /// <summary>
    /// An entry that passes every other gate but declares no identity is refused BEFORE the socket.
    /// The address is a routing hint: on this network 10.10.10.10 answers at several sites.
    /// </summary>
    [Fact]
    public void An_entry_with_no_declared_order_number_is_refused_before_connecting()
    {
        var path = Allowlist("""
            { "entries": [ { "address": "10.10.10.10", "label": "rig", "kind": "test-rig",
                "writeEligible": true, "outputsIsolated": true, "isolationAssertedBy": "Owner" } ] }
            """);

        var result = Run("--run", "--target", Rig, "--allowlist", path, "--yes");

        Assert.Equal(RigControlCli.ExitRefused, result.Exit);
        Assert.Equal(0, result.FactoryCalls);
        Assert.Contains("NoDeclaredIdentity", result.Out);
    }

    [Fact]
    public void An_unlisted_address_is_refused_even_when_an_approved_rig_exists()
    {
        var result = Run("--run", "--target", "10.10.10.11", "--allowlist", FullyEligible(), "--yes");

        Assert.Equal(RigControlCli.ExitRefused, result.Exit);
        Assert.Equal(0, result.FactoryCalls);
        Assert.Contains("NotAnApprovedTestRig", result.Out);
    }

    // ================================================================ the --yes gate

    /// <summary>
    /// Without <c>--yes</c>: the plan prints and NOTHING is contacted. Asserted on the FULLY ELIGIBLE
    /// entry deliberately — the case where every gate passed is the one where a run might be tempted to
    /// carry on, and it is the only case that tests the <c>--yes</c> gate rather than a gate above it.
    /// </summary>
    [Fact]
    public void Without_yes_the_plan_prints_and_no_transport_is_even_constructed()
    {
        var result = Run("--run", "--target", Rig, "--allowlist", FullyEligible());

        Assert.Equal(RigControlCli.ExitNotConfirmed, result.Exit);
        Assert.Equal(0, result.FactoryCalls);
        Assert.Contains("NOTHING WAS CONTACTED", result.Out);
        Assert.Contains("== plan ==", result.Out);
    }

    [Fact]
    public void The_plan_states_that_this_binary_cannot_undo_what_it_does()
    {
        var result = Run("--run", "--target", Rig, "--allowlist", FullyEligible());

        Assert.Contains("CANNOT STOP IT", result.Out);
        Assert.Contains("TIA Portal", result.Out);
    }

    // ================================================================ the positive control

    /// <summary>
    /// *** THE CONTROL THAT MAKES EVERY REFUSAL ABOVE MEAN SOMETHING. *** A fence that refuses
    /// everything passes every test that only checks refusals. This one asserts the whole happy path:
    /// the transport IS constructed, the CPU IS asked, and the state IS read back.
    /// </summary>
    [Fact]
    public void A_fully_eligible_entry_with_yes_connects_requests_and_verifies()
    {
        // Stopped on the first read (so a request is warranted), running on every read afterwards.
        var cpu = new ScriptedTransport(new[] { ScriptedTransport.Stopped });
        var result = Run(cpu, "--run", "--target", Rig, "--allowlist", FullyEligible(), "--yes", "--settle-ms", "0");

        Assert.Equal(RigControlCli.ExitOk, result.Exit);
        Assert.Equal(1, result.FactoryCalls);
        Assert.Equal(1, cpu.Connects);
        Assert.Equal(1, cpu.OrderCodeReads);
        Assert.Equal(1, cpu.RunRequests);
        Assert.Equal(2, cpu.RunStateReads);          // before, and the read-back
        Assert.True(cpu.Disposed);
        Assert.Contains("== done ==", result.Out);
    }

    /// <summary>
    /// The CPU passing through a non-running startup state before reaching RUN must not read as a
    /// mismatch. Polling makes the check about the OUTCOME rather than about the timing.
    /// </summary>
    [Fact]
    public void A_cpu_that_takes_a_moment_to_reach_run_is_not_a_mismatch()
    {
        var cpu = new ScriptedTransport(new[]
        {
            ScriptedTransport.Stopped,   // before
            ScriptedTransport.Stopped,   // read-back #1 — startup
            ScriptedTransport.Running,   // read-back #2
        });

        var result = Run(cpu, "--run", "--target", Rig, "--allowlist", FullyEligible(), "--yes", "--settle-ms", "3000");

        Assert.Equal(RigControlCli.ExitOk, result.Exit);
        Assert.Equal(3, cpu.RunStateReads);
    }

    // ================================================================ the read-back

    /// <summary>
    /// *** THE FAILURE THIS TOOL EXISTS TO CATCH. *** The request is acknowledged and the CPU does not
    /// run. It is its OWN exit code, never an exit 0 with a note in the output.
    /// </summary>
    [Fact]
    public void An_acknowledged_request_that_does_nothing_is_a_readback_mismatch()
    {
        var cpu = new ScriptedTransport(settledState: ScriptedTransport.Stopped);
        var result = Run(cpu, "--run", "--target", Rig, "--allowlist", FullyEligible(), "--yes", "--settle-ms", "0");

        Assert.Equal(RigControlCli.ExitReadBackMismatch, result.Exit);
        Assert.Equal(1, cpu.RunRequests);
        Assert.Contains("MISMATCH", result.Out);
        Assert.NotEqual(RigControlCli.ExitOk, result.Exit);
    }

    /// <summary>
    /// A read-back that could not be PERFORMED is a mismatch too. Empty is not clean: nothing was
    /// established about the CPU's mode, and "no error seen" is not "running".
    /// </summary>
    [Fact]
    public void A_readback_that_could_not_be_performed_is_a_mismatch_not_a_pass()
    {
        var cpu = new ScriptedTransport { RunStateResult = S7Status.Failure("link dropped") };
        var result = Run(cpu, "--run", "--target", Rig, "--allowlist", FullyEligible(), "--yes", "--settle-ms", "0");

        Assert.Equal(RigControlCli.ExitReadBackMismatch, result.Exit);
        Assert.Contains("COULD NOT BE PERFORMED", result.Out);
    }

    /// <summary>
    /// *** THERE IS NO FLAG THAT SKIPS THE READ-BACK, AND ASKING FOR ONE IS A NAMED REFUSAL. *** An
    /// unknown-option error would read as a typo and send somebody looking for the right spelling.
    /// </summary>
    [Theory]
    [InlineData("--skip-readback")]
    [InlineData("--no-verify")]
    [InlineData("--assume-run")]
    [InlineData("--force")]
    [InlineData("--override")]
    [InlineData("--stop")]
    [InlineData("--halt")]
    [InlineData("--cold-start")]
    [InlineData("--write-eligible")]
    public void Flags_that_would_weaken_this_tool_are_refused_by_name(string flag)
    {
        var result = Run("--run", "--target", Rig, "--allowlist", FullyEligible(), "--yes", flag);

        Assert.Equal(RigControlCli.ExitUsage, result.Exit);
        Assert.Equal(0, result.FactoryCalls);
        Assert.Contains("REFUSED", result.Err);
        Assert.Contains(flag, result.Err);
    }

    [Fact]
    public void A_refused_flag_is_refused_wherever_it_appears_in_the_line()
    {
        // A refusal that depends on argument order is a refusal somebody gets round by reordering.
        Assert.Equal(RigControlCli.ExitUsage, Run("--stop", "--run", "--target", Rig).Exit);
        Assert.Equal(RigControlCli.ExitUsage, Run("--run", "--target", Rig, "--force").Exit);
    }

    // ================================================================ the device half

    [Fact]
    public void A_cpu_that_is_already_running_is_left_alone()
    {
        var cpu = new ScriptedTransport(new[] { ScriptedTransport.Running });
        var result = Run(cpu, "--run", "--target", Rig, "--allowlist", FullyEligible(), "--yes");

        Assert.Equal(RigControlCli.ExitOk, result.Exit);
        Assert.Equal(0, cpu.RunRequests);
        Assert.Contains("ALREADY RUNNING", result.Out);
    }

    [Fact]
    public void A_device_answering_with_the_wrong_order_code_is_refused_and_never_asked_to_run()
    {
        var cpu = new ScriptedTransport { OrderCode = "6ES7 214-1AH50-0XB0" };   // G2, not the classic
        var result = Run(cpu, "--run", "--target", Rig, "--allowlist", FullyEligible(), "--yes");

        Assert.Equal(RigControlCli.ExitRefused, result.Exit);
        Assert.Equal(0, cpu.RunRequests);
        Assert.Contains("NOT the approved device", result.Out);
    }

    [Fact]
    public void An_unreadable_order_code_is_a_refusal_never_a_pass()
    {
        var cpu = new ScriptedTransport { OrderCodeResult = S7Status.Failure("refused") };
        var result = Run(cpu, "--run", "--target", Rig, "--allowlist", FullyEligible(), "--yes");

        Assert.Equal(RigControlCli.ExitRefused, result.Exit);
        Assert.Equal(0, cpu.RunRequests);
        Assert.Contains("unidentified", result.Out);
    }

    [Fact]
    public void A_failed_connect_stops_before_anything_is_asked_of_the_cpu()
    {
        var cpu = new ScriptedTransport { ConnectResult = S7Status.Failure("no route") };
        var result = Run(cpu, "--run", "--target", Rig, "--allowlist", FullyEligible(), "--yes");

        Assert.Equal(RigControlCli.ExitConnectFailed, result.Exit);
        Assert.Equal(0, cpu.OrderCodeReads);
        Assert.Equal(0, cpu.RunRequests);
    }

    /// <summary>
    /// A refused request is its own exit code — and the state is read back ANYWAY, because a failed
    /// request is not evidence that nothing happened.
    /// </summary>
    [Fact]
    public void A_refused_run_request_still_reads_the_state_back()
    {
        var cpu = new ScriptedTransport(new[] { ScriptedTransport.Stopped }, ScriptedTransport.Stopped)
        {
            RequestRunResult = S7Status.Failure("negative acknowledgement", 0x00040000),
        };

        var result = Run(cpu, "--run", "--target", Rig, "--allowlist", FullyEligible(), "--yes", "--settle-ms", "0");

        Assert.Equal(RigControlCli.ExitRequestFailed, result.Exit);
        Assert.Equal(1, cpu.RunRequests);
        Assert.Equal(2, cpu.RunStateReads);
        Assert.Contains("full round trip", result.Out);
    }

    /// <summary>A throw in the device half is NAMED, not merely loud.</summary>
    [Fact]
    public void A_transport_that_throws_is_reported_as_a_named_fault()
    {
        var result = Run(new ThrowingTransport(), "--run", "--target", Rig, "--allowlist", FullyEligible(), "--yes");

        Assert.Equal(RigControlCli.ExitRequestFailed, result.Exit);
        Assert.Contains("FAULT", result.Err);
        Assert.Contains(nameof(NotSupportedException), result.Err);
    }

    private sealed class ThrowingTransport : IRunTransitionTransport
    {
        public bool Connected => false;
        public S7Status Connect(string a, int r, int s, int t) => throw new NotSupportedException("no transport in this build");
        public void Disconnect() { }
        public S7Status ReadOrderCode(out string orderCode) { orderCode = ""; return S7Status.Success; }
        public S7Status ReadRunState(out S7RunStateReading runState) { runState = S7RunStateReading.Unread; return S7Status.Success; }
        public S7Status RequestRun() => S7Status.Success;
        public void Dispose() { }
    }

    // ================================================================ narrowing, printed always

    /// <summary>
    /// The identity check does not cover the serial number, and it says so ON EVERY RUN — including the
    /// run where it excludes nothing. A checker that only speaks when it exempts something cannot be
    /// told from one that has stopped working.
    /// </summary>
    [Fact]
    public void What_the_identity_check_does_not_cover_is_printed_with_a_count()
    {
        var withSerial = Run("--run", "--target", Rig, "--allowlist", FullyEligible());
        Assert.Contains("1 declared identifier(s) EXCLUDED", withSerial.Out);
        Assert.Contains("serialNumber", withSerial.Out);

        var withoutSerial = Run("--run", "--target", Rig, "--allowlist", Allowlist(
            $$"""
            { "entries": [ { "address": "10.10.10.10", "label": "rig", "kind": "test-rig",
                "orderNumber": "{{OrderCode}}", "writeEligible": true, "outputsIsolated": true,
                "isolationAssertedBy": "Owner" } ] }
            """));
        Assert.Contains("0 declared identifier(s) EXCLUDED", withoutSerial.Out);
    }

    // ================================================================ arguments

    [Fact]
    public void No_arguments_prints_usage_and_fails()
    {
        var result = Run();

        Assert.Equal(RigControlCli.ExitUsage, result.Exit);
        Assert.Contains("rig-control", result.Err);
        Assert.Equal(0, result.FactoryCalls);
    }

    [Fact]
    public void The_run_verb_is_required_so_a_bare_target_does_nothing()
    {
        Assert.Equal(RigControlCli.ExitUsage, Run("--target", Rig, "--yes").Exit);
    }

    [Fact]
    public void A_numeric_option_that_is_not_a_number_is_refused_rather_than_defaulted()
    {
        var result = Run("--run", "--target", Rig, "--allowlist", FullyEligible(), "--slot", "one");

        Assert.Equal(RigControlCli.ExitUsage, result.Exit);
        Assert.Equal(0, result.FactoryCalls);
        Assert.Contains("not a whole number", result.Err);
    }

    [Fact]
    public void The_usage_text_states_the_two_things_this_binary_will_not_do()
    {
        Assert.Contains("CANNOT STOP ONE", RigControlCli.Usage);
        Assert.Contains("skips the read-back", RigControlCli.Usage);
    }
}
