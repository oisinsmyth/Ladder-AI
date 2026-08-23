using Harness.Batch;
using Harness.Device;
using Harness.Loop;

namespace Harness.Batch.Tests;

/// <summary>
/// 🔴 <b><c>declaredRegisters</c> WAS AUTHORED BY HAND, PER LANE, AND NOTHING COMPARED IT TO THE BLOCK
/// THAT SERVES IT.</b>
///
/// <para>It flows into <c>MirrorGeometry.ForCpu1214C</c>, into <c>MapAllocator</c>, into
/// <c>RegisterMap.MapHash</c>'s canonical form and therefore into the build stamp — so the stamp does
/// hash a declared width. <b>The gap is narrower than it looks and that makes it worse:</b> what the
/// stamp is blind to is not the number, it is whether the number is TRUE OF THE PROGRAM. A hand-typed
/// derivable field is only an opportunity to disagree with reality, and the committed binding at
/// <c>gen/test-project001/hopper-blockage-alarm/harness-binding.json</c> admits as much in its own
/// <c>_declaredRegistersNote</c>: 37 was READ FROM the comms block, by a person, once.</para>
///
/// <para>🔴 <b>WHAT THIS GATE CANNOT SEE.</b> The derivation reads the program CORPUS, never the CPU.
/// It cannot tell whether the block it read is the block on the controller — the mirror's widening to
/// 1024 registers was established by probing the device from both sides, and nothing here substitutes
/// for that. This buys exactly one thing: <b>a binding can no longer disagree with the program that was
/// staged.</b> Strictly smaller, and it must be reported as such.</para>
/// </summary>
public sealed class ServedAreaGateTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "served-gate-" + Guid.NewGuid().ToString("N"));

    public ServedAreaGateTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    private sealed class Canned : IProcessRunner
    {
        private readonly ProcessResult _result;

        internal IReadOnlyList<string>? LastArguments { get; private set; }

        internal Canned(ProcessResult result) => _result = result;

        public ProcessResult Run(string executable, IReadOnlyList<string> arguments, TimeSpan timeout)
        {
            LastArguments = arguments;
            return _result;
        }
    }

    private static Canned Derived(int baseByte, int registers) => new(new ProcessResult(
        true, false, 0,
        $$"""
        {
          "derived": true,
          "area": "M",
          "baseByte": {{baseByte}},
          "registers": {{registers}},
          "block": "FB_Comms_ModbusServer",
          "file": "ir/test-project001/FB_Comms_ModbusServer.ir",
          "readableLine": 31,
          "sidecarLine": 39,
          "denominator": "served area: base {{baseByte}}, {{registers}} register(s), derived from ir/test-project001/FB_Comms_ModbusServer.ir:31 + sidecar ir/test-project001/FB_Comms_ModbusServer.ir:39"
        }
        """,
        string.Empty, "canned"));

    private string ProgramDir(string lane, string blockName)
    {
        var dir = Path.Combine(_root, lane, "ir");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, blockName + ".ir"), $"BLOCK FC {blockName}\nEND_BLOCK\n");
        return dir;
    }

    private Lane LaneNamed(string name, string bindingKey) =>
        new(name, bindingKey, name + ".submission.json", new[] { ProgramDir(name, "FC_" + name) });

    private static string Binding(string slotId, int? declaredRegisters = 576, int? baseByte = 1000)
    {
        var geometry = "\"blockName\": \"FC_HarnessCopyLayer\", \"blockNumber\": 9001, "
            + "\"tagTableName\": \"HarnessMirror\", \"tagPrefix\": \"HX_\", \"retentiveBytes\": 256"
            + (baseByte is int b ? $", \"baseByte\": {b}" : string.Empty)
            + (declaredRegisters is int d ? $", \"declaredRegisters\": {d}" : string.Empty);

        return "{ " + geometry + ", \"slots\": [{ \"slotId\": \"" + slotId + "\", "
            + "\"startCondition\": \"Demo_Start\", "
            + "\"vectorTargets\": [{ \"tag\": \"Demo_In\", \"specName\": \"In\", \"type\": \"Int\" }], "
            + "\"resultSources\": [{ \"tag\": \"Demo_Out0\", \"specName\": \"Out0\", \"type\": \"Int\" }] }] }";
    }

    private static Func<string, string> Reader(Dictionary<string, string> bindings) =>
        path => bindings.TryGetValue(path, out var text)
            ? text
            : throw new FileNotFoundException($"no fixture binding at {path}");

    // =============================================================================================
    // 1 — THE PROBE. What the converter says, carried without re-derivation.
    // =============================================================================================

    [Fact]
    public void The_probe_carries_the_derived_width_and_the_producers_own_denominator()
    {
        var runner = Derived(1000, 37);

        var fact = ServedAreaProbe.Derive("converter.exe", new[] { "a/ir", "b/ir" }, runner);

        Assert.True(fact.Derived);
        Assert.Equal(1000, fact.BaseByte);
        Assert.Equal(37, fact.Registers);
        Assert.Empty(fact.Refusals);

        // The producer's own sentence, verbatim. Re-wording it here would create a second place for the
        // provenance to drift away from the file it names.
        Assert.Contains("FB_Comms_ModbusServer.ir:31", fact.Denominator, StringComparison.Ordinal);
        Assert.Contains("sidecar", fact.Denominator, StringComparison.Ordinal);
    }

    /// <summary>The union corpus is not materialised at plan time, so the scope repeats across lanes.</summary>
    [Fact]
    public void The_probe_passes_every_lane_path_as_its_own_scope()
    {
        var runner = Derived(1000, 37);

        ServedAreaProbe.Derive("converter.exe", new[] { "a/ir", "b/ir" }, runner);

        Assert.Equal(new[] { "served-area", "--project", "a/ir", "--project", "b/ir", "--json" }, runner.LastArguments);
    }

    /// <summary>
    /// 🔴 <b>Being unable to CONSULT the producer is reported, never refused</b> — the trade
    /// <c>ReachabilityParity</c> already makes, and for the same reason: a batch must not be blocked by
    /// a missing binary. What refuses is a derivation that DISAGREES, never one that did not happen.
    /// </summary>
    [Fact]
    public void A_converter_that_did_not_run_is_reported_not_refused()
    {
        var runner = new Canned(new ProcessResult(false, false, 0, string.Empty, string.Empty, "not found"));

        var fact = ServedAreaProbe.Derive("converter.exe", new[] { "a/ir" }, runner);

        Assert.False(fact.Derived);
        Assert.Empty(fact.Refusals);
        Assert.StartsWith("NOT DERIVED", fact.Denominator, StringComparison.Ordinal);
        Assert.Contains("not found", fact.Denominator, StringComparison.Ordinal);
    }

    /// <summary>A producer REFUSAL is a defect in the corpus and it comes through as one.</summary>
    [Fact]
    public void A_producer_refusal_is_carried_as_a_refusal()
    {
        var runner = new Canned(new ProcessResult(true, false, 1,
            "{ \"derived\": false, \"refusals\": [ \"the two homes of the served width DISAGREE\" ], "
            + "\"denominator\": \"REFUSED - 1 block(s) in 1 file(s) scanned; 1 refusal(s) below.\" }",
            string.Empty, "canned"));

        var fact = ServedAreaProbe.Derive("converter.exe", new[] { "a/ir" }, runner);

        Assert.False(fact.Derived);
        Assert.Contains(fact.Refusals, r => r.Contains("DISAGREE", StringComparison.Ordinal));
    }

    // =============================================================================================
    // 2 — THE GATE. A declared width that disagrees with the program refuses, naming both.
    // =============================================================================================

    /// <summary>
    /// The exact case the plan names: a binding declaring 576 against a corpus serving 37. Both numbers
    /// AND both sources — the lane whose binding says 576, and the file:line pair the 37 came off.
    /// </summary>
    [Fact]
    public void A_declared_width_the_program_does_not_serve_is_refused_naming_both_numbers_and_both_sources()
    {
        var bindings = new Dictionary<string, string> { ["a.json"] = Binding("S0", declaredRegisters: 576) };

        var result = BatchPlanner.Plan(
            new[] { LaneNamed("valve", "a.json") },
            Reader(bindings),
            ServedAreaFactFrom(1000, 37));

        Assert.False(result.Planned);
        var refusal = Assert.Single(result.Refusals, r => r.Contains("declaredRegisters", StringComparison.Ordinal));

        Assert.Contains("576", refusal, StringComparison.Ordinal);
        Assert.Contains("37", refusal, StringComparison.Ordinal);
        Assert.Contains("a.json", refusal, StringComparison.Ordinal);          // where the 576 was authored
        Assert.Contains("FB_Comms_ModbusServer.ir:31", refusal, StringComparison.Ordinal); // where the 37 was read
        Assert.Contains("sidecar", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void A_declared_width_the_program_does_serve_plans()
    {
        var bindings = new Dictionary<string, string> { ["a.json"] = Binding("S0", declaredRegisters: 37) };

        var result = BatchPlanner.Plan(
            new[] { LaneNamed("valve", "a.json") },
            Reader(bindings),
            ServedAreaFactFrom(1000, 37));

        Assert.True(result.Planned, string.Join(" | ", result.Refusals));
        Assert.Equal(37, result.Map!.Geometry.DeclaredRegisters);
    }

    [Fact]
    public void A_declared_base_byte_the_program_does_not_serve_is_refused()
    {
        var bindings = new Dictionary<string, string> { ["a.json"] = Binding("S0", declaredRegisters: 37, baseByte: 1000) };

        var result = BatchPlanner.Plan(
            new[] { LaneNamed("valve", "a.json") },
            Reader(bindings),
            ServedAreaFactFrom(2000, 37));

        Assert.False(result.Planned);
        Assert.Contains(result.Refusals, r => r.Contains("baseByte", StringComparison.Ordinal)
                                              && r.Contains("2000", StringComparison.Ordinal)
                                              && r.Contains("1000", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>Optional-and-checked, not required-and-trusted.</b> A binding that states no width is planned
    /// against the DERIVED one — which is the only number in the transaction that came from the program.
    /// </summary>
    [Fact]
    public void A_binding_that_states_no_width_is_planned_against_the_derived_one()
    {
        var bindings = new Dictionary<string, string> { ["a.json"] = Binding("S0", declaredRegisters: null) };

        var result = BatchPlanner.Plan(
            new[] { LaneNamed("valve", "a.json") },
            Reader(bindings),
            ServedAreaFactFrom(1000, 37));

        Assert.True(result.Planned, string.Join(" | ", result.Refusals));
        Assert.Equal(37, result.Map!.Geometry.DeclaredRegisters);
    }

    /// <summary>
    /// With nothing derived, an absent width is still a refusal. The derivation is a CHECK on the
    /// authored number, never a way to stop stating one when nobody looked.
    /// </summary>
    [Fact]
    public void A_binding_that_states_no_width_and_no_derivation_is_still_refused()
    {
        var bindings = new Dictionary<string, string> { ["a.json"] = Binding("S0", declaredRegisters: null) };

        var result = BatchPlanner.Plan(new[] { LaneNamed("valve", "a.json") }, Reader(bindings));

        Assert.False(result.Planned);
        Assert.Contains(result.Refusals, r => r.Contains("declaredRegisters", StringComparison.Ordinal));
    }

    /// <summary>
    /// A corpus whose two homes of the number disagree is a defect in the thing about to be deployed,
    /// and it gates — the producer's own refusal, carried through verbatim.
    /// </summary>
    [Fact]
    public void A_producer_refusal_gates_the_batch()
    {
        var bindings = new Dictionary<string, string> { ["a.json"] = Binding("S0", declaredRegisters: 37) };

        var result = BatchPlanner.Plan(
            new[] { LaneNamed("valve", "a.json") },
            Reader(bindings),
            new ServedAreaFact(false, 0, 0, "REFUSED - the corpus is inconsistent",
                new[] { "the two homes of the served width DISAGREE: ...:31 says 37, ...:39 says 40" }));

        Assert.False(result.Planned);
        Assert.Contains(result.Refusals, r => r.Contains("DISAGREE", StringComparison.Ordinal));
    }

    // =============================================================================================
    // 3 — THE DENOMINATOR, ON EVERY PLAN. Including the one where nothing was derived.
    // =============================================================================================

    [Fact]
    public void A_plan_with_no_derivation_says_the_width_was_declared_not_derived()
    {
        var bindings = new Dictionary<string, string> { ["a.json"] = Binding("S0", declaredRegisters: 576) };

        var result = BatchPlanner.Plan(new[] { LaneNamed("valve", "a.json") }, Reader(bindings));
        var described = BatchPlanner.Describe(result);

        Assert.True(result.Planned, string.Join(" | ", result.Refusals));
        Assert.Contains("NOT DERIVED", described, StringComparison.Ordinal);
        Assert.Contains("DECLARED, not derived", described, StringComparison.Ordinal);
    }

    [Fact]
    public void A_plan_with_a_derivation_prints_the_served_area_and_its_two_sources()
    {
        var bindings = new Dictionary<string, string> { ["a.json"] = Binding("S0", declaredRegisters: 37) };

        var result = BatchPlanner.Plan(
            new[] { LaneNamed("valve", "a.json") },
            Reader(bindings),
            ServedAreaFactFrom(1000, 37));

        var described = BatchPlanner.Describe(result);
        Assert.Contains("served area: base 1000, 37 register(s)", described, StringComparison.Ordinal);
        Assert.Contains("FB_Comms_ModbusServer.ir:31", described, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 The smaller claim, printed where the reader meets the green — not left in a design note. A
    /// derived width says the binding matches the STAGED PROGRAM. It says nothing about the controller.
    /// </summary>
    [Fact]
    public void A_derived_plan_states_that_it_read_the_corpus_and_not_the_controller()
    {
        var bindings = new Dictionary<string, string> { ["a.json"] = Binding("S0", declaredRegisters: 37) };

        var result = BatchPlanner.Plan(
            new[] { LaneNamed("valve", "a.json") },
            Reader(bindings),
            ServedAreaFactFrom(1000, 37));

        var described = BatchPlanner.Describe(result);
        Assert.Contains("program CORPUS", described, StringComparison.Ordinal);
        Assert.Contains("not the controller", described, StringComparison.Ordinal);
        Assert.Contains("STAGED program", described, StringComparison.Ordinal);
    }

    // =============================================================================================
    // 4 — THE WIRING. A gate nothing calls is the closed-check shape this repo keeps finding.
    // =============================================================================================

    /// <summary>A queue holding one lane whose binding declares 576, over a real program directory.</summary>
    private string QueueDeclaring(int declaredRegisters)
    {
        var queue = Path.Combine(_root, "queue");
        var binding = Path.Combine(_root, "lane.json");
        var submission = Path.Combine(_root, "sub.json");

        File.WriteAllText(binding, Binding("S0", declaredRegisters));
        File.WriteAllText(submission, "{}");

        var ir = Path.Combine(_root, "lane-ir");
        Directory.CreateDirectory(ir);
        File.WriteAllText(Path.Combine(ir, "FC_LaneFixture.ir"), "BLOCK FC FC_LaneFixture\nEND_BLOCK\n");

        new LaneQueue(queue).Enqueue(new Lane("valve", binding, submission, new[] { ir }));
        return queue;
    }

    [Fact]
    public void Plan_with_a_converter_derives_the_width_and_refuses_a_binding_the_program_does_not_serve()
    {
        var queue = QueueDeclaring(576);
        var writer = new StringWriter();

        var exit = BatchCli.Run(
            new[] { "plan", "--queue", queue, "--converter", "converter.exe" },
            writer, File.ReadAllText, File.WriteAllText, Derived(1000, 37));

        var output = writer.ToString();
        Assert.Equal(BatchExit.Refused, exit);
        Assert.Contains("576", output, StringComparison.Ordinal);
        Assert.Contains("FB_Comms_ModbusServer.ir:31", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_with_a_converter_that_agrees_plans()
    {
        var queue = QueueDeclaring(37);
        var writer = new StringWriter();

        var exit = BatchCli.Run(
            new[] { "plan", "--queue", queue, "--converter", "converter.exe" },
            writer, File.ReadAllText, File.WriteAllText, Derived(1000, 37));

        Assert.Equal(BatchExit.Ok, exit);
        Assert.Contains("served area: base 1000, 37 register(s)", writer.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Without <c>--converter</c> the plan still runs — and says on its own report that nothing
    /// corroborated the width, rather than printing a green that looks like a check.
    /// </summary>
    [Fact]
    public void Plan_without_a_converter_says_the_width_was_declared_and_starts_no_process()
    {
        var queue = QueueDeclaring(576);
        var writer = new StringWriter();
        var runner = new Canned(new ProcessResult(true, false, 0, "{}", string.Empty, "canned"));

        var exit = BatchCli.Run(
            new[] { "plan", "--queue", queue }, writer, File.ReadAllText, File.WriteAllText, runner);

        Assert.Equal(BatchExit.Ok, exit);
        Assert.Null(runner.LastArguments);
        Assert.Contains("DECLARED, not derived", writer.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// The dry run's contract is that it starts NO process at all, so it cannot derive — and it says
    /// which of the two it is rather than leaving the width's provenance unstated.
    /// </summary>
    [Fact]
    public void A_dry_run_says_the_width_was_declared_and_still_starts_no_process()
    {
        var queue = QueueDeclaring(576);
        var writer = new StringWriter();
        var runner = new Canned(new ProcessResult(true, false, 0, "{}", string.Empty, "canned"));

        BatchCli.Run(
            new[] { "run", "--queue", queue, "--converter", "converter.exe", "--merged", Path.Combine(_root, "m.json") },
            writer, File.ReadAllText, File.WriteAllText, runner,
            _ => new DeploymentOutcome(true, true, new HashSet<string>(), "loaded"));

        Assert.Null(runner.LastArguments);
        Assert.Contains("a dry run starts NO process", writer.ToString(), StringComparison.Ordinal);
        Assert.Contains("DECLARED, not derived", writer.ToString(), StringComparison.Ordinal);
    }

    private static ServedAreaFact ServedAreaFactFrom(int baseByte, int registers) => new(
        true, baseByte, registers,
        $"served area: base {baseByte}, {registers} register(s), derived from "
        + "ir/test-project001/FB_Comms_ModbusServer.ir:31 + sidecar ir/test-project001/FB_Comms_ModbusServer.ir:39",
        Array.Empty<string>());
}
