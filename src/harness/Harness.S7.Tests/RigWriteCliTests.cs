using System.Text.Json;
using Harness.RigWrite;
using Harness.S7;

namespace Harness.S7.Tests;

/// <summary>
/// The command line, and the one property everything else rests on: <b>a dry run contacts nothing</b>.
///
/// <para>That is asserted, not described. The CLI is handed a client factory that throws if it is ever
/// called, and a client whose every member throws if it is ever touched — so a run that reached for a
/// device would fail here rather than on a network.</para>
/// </summary>
public class RigWriteCliTests : IDisposable
{
    private const string Rig = "10.10.10.10";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ladder-cli-" + Guid.NewGuid().ToString("N"));

    public RigWriteCliTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// A client that fails the test if anything at all is asked of it. Not a fake device — the point
    /// is that no member is reachable, so an accidental socket shows up as a test failure with a name
    /// that says what happened.
    /// </summary>
    private sealed class NeverTouchedClient : IS7Client
    {
        public bool Connected => throw Used(nameof(Connected));

        public S7Status Connect(string address, int rack, int slot, int connectTimeoutMs) =>
            throw Used(nameof(Connect));

        public void Disconnect() => throw Used(nameof(Disconnect));
        public S7Status ReadOrderCode(out string orderCode) => throw Used(nameof(ReadOrderCode));
        public S7Status ReadCpuInfo(out S7CpuInfo info) => throw Used(nameof(ReadCpuInfo));
        public S7Status ReadDataBlock(int db, int start, byte[] buffer) => throw Used(nameof(ReadDataBlock));
        public S7Status WriteDataBlock(int db, int start, byte[] buffer) => throw Used(nameof(WriteDataBlock));
        public S7Status WriteBit(int db, int b, int bit, bool value) => throw Used(nameof(WriteBit));

        private static Exception Used(string member) =>
            new InvalidOperationException($"the dry run touched the device: IS7Client.{member} was called.");
    }

    private sealed class CountingFactory
    {
        public int Calls { get; private set; }

        public Func<IS7Client> Factory => () =>
        {
            Calls++;
            return new NeverTouchedClient();
        };
    }

    // ---------------------------------------------------------------- helpers

    private string WriteAllowlist(string json)
    {
        var path = Path.Combine(_dir, "allowlist.json");
        File.WriteAllText(path, json);
        return path;
    }

    /// <summary>The entry as it stands on this machine: not write-eligible, unreadable serial.</summary>
    private string LiveShapedAllowlist() => WriteAllowlist(
        """
        { "entries": [ {
            "address": "10.10.10.10",
            "label": "Bench rig",
            "kind": "test-rig",
            "approvedBy": "Owner",
            "approvedDate": "2026-08-11",
            "orderNumber": "6ES7 214-1AG40-0XB0",
            "serialNumber": "S C-EXAMPLE00001",
            "writeEligible": false,
            "outputsIsolated": true,
            "isolationAssertedBy": "Owner"
        } ] }
        """);

    private (int Exit, string Out, string Err, int FactoryCalls) Run(params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var factory = new CountingFactory();

        var exit = RigWriteCli.Run(args, _ => null, factory.Factory, stdout, stderr);

        return (exit, stdout.ToString(), stderr.ToString(), factory.Calls);
    }

    // ---------------------------------------------------------------- no device, ever

    [Fact]
    public void A_dry_run_never_builds_a_client_let_alone_uses_one()
    {
        var result = Run("plan", "--target", Rig, "--allowlist", LiveShapedAllowlist(), "--restore-dir", _dir);

        Assert.Equal(0, result.FactoryCalls);
        Assert.Contains("no device was contacted", result.Out, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_dry_run_against_a_fully_eligible_entry_still_never_builds_a_client()
    {
        // The case where the plan has nothing to refuse is the one where a run might be tempted to
        // carry on, so it is asserted separately from the refused case.
        var path = WriteAllowlist(
            """
            { "entries": [ {
                "address": "10.10.10.10", "kind": "test-rig", "label": "rig",
                "orderNumber": "6ES7 214-1AG40-0XB0", "serialNumber": "RIG-BENCH-01",
                "writeEligible": true, "outputsIsolated": true, "isolationAssertedBy": "Owner",
                "marker": { "dbNumber": 38, "byteOffset": 2, "length": 32 }
            } ] }
            """);

        var result = Run("plan", "--target", Rig, "--allowlist", path, "--restore-dir", _dir);

        Assert.Equal(0, result.FactoryCalls);
        Assert.Equal(RigWriteCli.ExitWouldRefuse, result.Exit);   // no restore point on disk
    }

    // ---------------------------------------------------------------- arming

    [Theory]
    [InlineData("--arm")]
    [InlineData("--execute")]
    [InlineData("--write")]
    [InlineData("--yes")]
    [InlineData("--force")]
    public void Asking_this_build_to_write_is_a_named_refusal_not_an_unknown_option(string flag)
    {
        var result = Run("plan", "--target", Rig, "--allowlist", LiveShapedAllowlist(), flag);

        Assert.Equal(RigWriteCli.ExitNotArmed, result.Exit);
        Assert.Contains("CANNOT write to a device", result.Err);
        Assert.Equal(0, result.FactoryCalls);
    }

    [Fact]
    public void An_arming_flag_is_refused_wherever_it_appears_in_the_line()
    {
        // A refusal that depends on argument order is a refusal somebody gets round by reordering.
        Assert.Equal(RigWriteCli.ExitNotArmed, Run("--arm", "plan", "--target", Rig).Exit);
        Assert.Equal(RigWriteCli.ExitNotArmed, Run("layout", "--arm").Exit);
    }

    [Fact]
    public void The_usage_text_says_the_build_cannot_write()
    {
        Assert.Contains("CANNOT WRITE TO A DEVICE", RigWriteCli.Usage);
        Assert.False(Arming.CompiledIn);
    }

    // ---------------------------------------------------------------- verdicts

    [Fact]
    public void The_live_entry_plans_to_a_refusal_naming_the_write_eligibility_gate()
    {
        var result = Run("plan", "--target", Rig, "--allowlist", LiveShapedAllowlist(), "--restore-dir", _dir);

        Assert.Equal(RigWriteCli.ExitWouldRefuse, result.Exit);
        Assert.Contains("NotWriteEligible", result.Out);
        Assert.Contains("not marked write-eligible", result.Out);
    }

    [Fact]
    public void Json_output_says_plainly_that_no_device_was_contacted()
    {
        var result = Run("plan", "--target", Rig, "--allowlist", LiveShapedAllowlist(),
            "--restore-dir", _dir, "--json");

        using var doc = JsonDocument.Parse(result.Out);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("dryRun").GetBoolean());
        Assert.False(root.GetProperty("deviceContacted").GetBoolean());
        Assert.False(root.GetProperty("armingCompiledIn").GetBoolean());
        Assert.Equal("NotWriteEligible", root.GetProperty("fence").GetProperty("gate").GetString());
        Assert.False(root.GetProperty("offlineClear").GetBoolean());
    }

    [Fact]
    public void With_no_allowlist_configured_every_target_is_refused()
    {
        var result = Run("plan", "--target", Rig, "--restore-dir", _dir);

        Assert.Equal(RigWriteCli.ExitWouldRefuse, result.Exit);
        Assert.Contains("No allowlist configured", result.Out);
    }

    // ---------------------------------------------------------------- arguments

    [Fact]
    public void Plan_requires_a_target()
    {
        Assert.Equal(RigWriteCli.ExitUsage, Run("plan").Exit);
    }

    [Fact]
    public void A_numeric_option_that_is_not_a_number_is_refused_rather_than_defaulted()
    {
        // Falling back to the default would address a different DB than the one asked for.
        var result = Run("plan", "--target", Rig, "--allowlist", LiveShapedAllowlist(), "--db", "thirty-eight");

        Assert.Equal(RigWriteCli.ExitUsage, result.Exit);
        Assert.Contains("not a whole number", result.Err);
    }

    [Fact]
    public void A_value_too_long_for_the_target_string_is_a_usage_error_not_a_gate_refusal()
    {
        var result = Run("plan", "--target", Rig, "--allowlist", LiveShapedAllowlist(),
            "--restore-dir", _dir, "--text", new string('x', 40));

        Assert.Equal(RigWriteCli.ExitUsage, result.Exit);
        Assert.Contains("will not fit", result.Err);
    }

    [Fact]
    public void No_arguments_prints_usage_and_fails()
    {
        var result = Run();

        Assert.Equal(RigWriteCli.ExitUsage, result.Exit);
        Assert.Contains("rig-write", result.Err);
    }

    [Fact]
    public void Unknown_commands_are_refused()
    {
        Assert.Equal(RigWriteCli.ExitUsage, Run("download").Exit);
    }

    // ---------------------------------------------------------------- layout

    [Fact]
    public void Layout_reports_the_computed_offsets_and_that_they_are_computed()
    {
        var result = Run("layout");

        Assert.Equal(RigWriteCli.ExitOfflineClear, result.Exit);
        Assert.Contains("DBB70", result.Out);
        Assert.Contains("COMPUTED", result.Out);
        Assert.Contains("SELF-CONSISTENT", result.Out);
    }
}
