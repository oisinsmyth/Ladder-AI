using Harness.Batch;
using Harness.Map;

namespace Harness.Batch.Tests;

/// <summary>
/// 🔴 <b>The planner's width sum, pinned against the authority it duplicates.</b>
///
/// <para><b>Measured on the rig, 2026-08-22.</b> <c>harness-batch plan</c> reported the vessel lane at
/// <b>154</b> registers and the real derivation needed <b>165</b> — the difference being exactly its 11
/// transient result signals, each carrying a latch register appended after the values. The planner had
/// summed value widths only.</para>
///
/// <para><b>An under-counting capacity gate is worse than none.</b> It says "fits", the batch proceeds,
/// and the refusal lands on the step after it — in that run, on generation, whose message is about the
/// map rather than about the batch that authorised it.</para>
///
/// <para>The authority is <c>SlotBinding.ResultRegistersNeeded</c>. The planner sums the same terms over
/// the JSON documents rather than over the domain type, so these tests hold the two against each other:
/// the copy is allowed to exist, and not allowed to drift in silence.</para>
/// </summary>
public class BatchPlannerWidthTests : IDisposable
{
    /// <summary>
    /// A real program directory, because the planner refuses a path that contributes nothing — the union
    /// feeds the build stamp, so a set that is quietly short stamps a program nobody deployed. These tests
    /// are about register arithmetic and want the cheapest corpus that is genuinely there.
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "batch-width-" + Guid.NewGuid().ToString("N"));

    public BatchPlannerWidthTests()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "FC_WidthFixture.ir"), "BLOCK FC FC_WidthFixture\nEND_BLOCK\n");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    private const string Geometry =
        "\"blockName\": \"FC_HarnessCopyLayer\", \"blockNumber\": 9001, \"tagTableName\": \"HarnessMirror\", "
        + "\"tagPrefix\": \"HX_\", \"baseByte\": 1000, \"retentiveBytes\": 256, \"declaredRegisters\": 576";

    /// <param name="results">(type, transient) per result signal.</param>
    private static string Binding(params (string Type, bool Transient)[] results) =>
        "{ " + Geometry + ", \"slots\": [{ \"slotId\": \"S0\", \"startCondition\": \"Go\", "
        + "\"vectorTargets\": [{ \"tag\": \"In\", \"specName\": \"In\", \"type\": \"Int\" }], "
        + "\"resultSources\": ["
        + string.Join(",", results.Select((r, i) =>
            $"{{ \"tag\": \"Out{i}\", \"specName\": \"Out{i}\", \"type\": \"{r.Type}\""
            + (r.Transient ? ", \"transient\": true, \"latchedBy\": \"the block pulses it\"" : "")
            + " }"))
        + "] }] }";

    private int MappedResultRegisters(params (string Type, bool Transient)[] results)
    {
        var plan = BatchPlanner.Plan(
            new[] { new Lane("a", "b.json", "s.json", new[] { _root }) },
            _ => Binding(results));

        Assert.True(plan.Planned, string.Join(" | ", plan.Refusals));
        return plan.Map!.ResultBlock.Length;
    }

    /// <summary>The domain type's own answer, which the planner must agree with.</summary>
    private static int Authority(params (string Type, bool Transient)[] results) =>
        new SlotBinding(
            "S0",
            MirroredSignal.Ints("In"),
            "Go",
            results.Select((r, i) => new MirroredSignal(
                $"Out{i}",
                r.Type == "Time" ? MirrorValueType.Time : MirrorValueType.Int,
                $"Out{i}",
                Transient: r.Transient,
                LatchedBy: r.Transient ? "the block pulses it" : null)).ToArray())
            .ResultRegistersNeeded;

    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 The defect itself: a transient result costs TWO registers, its value and its latch. Counted as
    /// one, a lane of eleven transients comes out eleven short.
    /// </summary>
    [Fact]
    public void A_TRANSIENT_result_costs_its_value_AND_its_latch()
    {
        Assert.Equal(2, MappedResultRegisters(("Int", true)));
        Assert.Equal(1, MappedResultRegisters(("Int", false)));
    }

    /// <summary>The exact shape measured on the rig: eleven transients are eleven extra registers.</summary>
    [Fact]
    public void Eleven_transients_add_eleven_registers()
    {
        var plain = Enumerable.Range(0, 11).Select(_ => ("Int", false)).ToArray();
        var transient = Enumerable.Range(0, 11).Select(_ => ("Int", true)).ToArray();

        Assert.Equal(11, MappedResultRegisters(plain));
        Assert.Equal(22, MappedResultRegisters(transient));
    }

    /// <summary>A <c>Time</c> is still two registers, and a transient <c>Time</c> is three.</summary>
    [Fact]
    public void A_Time_is_two_registers_and_a_transient_Time_is_three()
    {
        Assert.Equal(2, MappedResultRegisters(("Time", false)));
        Assert.Equal(3, MappedResultRegisters(("Time", true)));
    }

    /// <summary>
    /// <b>The pinning test.</b> Across a mixed set, the planner's sum equals
    /// <c>SlotBinding.ResultRegistersNeeded</c> exactly. If either side gains a term the other does not,
    /// this fails — which is the only thing that keeps a duplicated rule honest.
    /// </summary>
    [Fact]
    public void The_planners_sum_agrees_with_SlotBinding_ResultRegistersNeeded()
    {
        var mixed = new[]
        {
            ("Int", false), ("Int", true), ("Time", false), ("Time", true),
            ("Int", true), ("Int", false), ("Time", true),
        };

        Assert.Equal(Authority(mixed), MappedResultRegisters(mixed));
    }
}
