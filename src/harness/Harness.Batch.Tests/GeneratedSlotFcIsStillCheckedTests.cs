using Harness.Batch;
using Harness.Map;

namespace Harness.Batch.Tests;

/// <summary>
/// 🔴 <b>Generation prevents the orphan THIS generator would create. It says nothing about anyone
/// else's, and removing the backstop is the tempting mistake.</b>
///
/// <para><see cref="SlotFcGenerator"/> emits a call-site obligation with every block it produces, which
/// makes the missing call a thing somebody declined to do rather than a thing nobody was told about. It
/// cannot create the call site, and it cannot see a hand-authored block sitting in the same union. So the
/// by-construction fix and the static refusal are asserted TOGETHER, in one file, so that deleting
/// <c>Reachability</c> as "now redundant" fails a test that says why it is not.</para>
/// </summary>
public sealed class GeneratedSlotFcIsStillCheckedTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "gen-slot-" + Guid.NewGuid().ToString("N"));

    public GeneratedSlotFcIsStillCheckedTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private string Write(string name, string ir)
    {
        var path = Path.Combine(_root, name + ".ir");
        File.WriteAllText(path, ir);
        return path;
    }

    private static string GeneratedSlotFc() => SlotFcGenerator.Generate(
        new SlotFcNaming("FC_HarnessSlot", 9010),
        new SlotCall("FB_DemoStim", "iDB_DemoStim", "Drive The Plant Model"),
        new SlotCall("FB_DemoUnderTest", "iDB_DemoUnderTest", "The Block Under Test")).Ir;

    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>A GENERATED slot FC that the OB does not call is STILL REFUSED.</b> This is the measured
    /// defect, reproduced against generated output: the block was deployed, loaded, reported healthy by
    /// every artifact, and never executed — while X-E's start echo reported "commanded, observed to run"
    /// throughout, because both halves of that echo live in the copy layer, which IS called.
    /// </summary>
    [Fact]
    public void A_generated_slot_FC_nothing_calls_is_still_refused_by_Reachability()
    {
        var slot = Write("FC_HarnessSlot", GeneratedSlotFc());
        var stim = Write("FB_DemoStim", "BLOCK FB FB_DemoStim\nEND_BLOCK\n");
        var uut = Write("FB_DemoUnderTest", "BLOCK FB FB_DemoUnderTest\nEND_BLOCK\n");

        // An OB that calls the copy layer and NOT the slot FC — exactly the shape that cost a wave.
        var copy = Write("FC_HarnessCopyLayer", "BLOCK FC FC_HarnessCopyLayer\nEND_BLOCK\n");
        var main = Write("Main", "BLOCK OB Main\nNETWORK 1 \"x\"\n  CALL FC_HarnessCopyLayer(EN := TRUE)\nEND_BLOCK\n");

        var result = Reachability.Of(new[] { slot, stim, uut, copy, main });

        Assert.NotEmpty(result.Refusals);
        Assert.Contains(result.Refusals, r => r.Contains("FC_HarnessSlot", StringComparison.Ordinal));
    }

    /// <summary>
    /// 🔴 <b>THE NEGATIVE CONTROL.</b> With the obligation honoured — the OB calls the slot FC — the same
    /// union passes. Without this, a Reachability that refused everything would satisfy the test above and
    /// the "backstop still works" claim would be worthless.
    /// </summary>
    [Fact]
    public void With_the_obligation_honoured_the_same_union_is_reachable()
    {
        var slot = Write("FC_HarnessSlot", GeneratedSlotFc());
        var stim = Write("FB_DemoStim", "BLOCK FB FB_DemoStim\nEND_BLOCK\n");
        var uut = Write("FB_DemoUnderTest", "BLOCK FB FB_DemoUnderTest\nEND_BLOCK\n");
        var copy = Write("FC_HarnessCopyLayer", "BLOCK FC FC_HarnessCopyLayer\nEND_BLOCK\n");

        var main = Write("Main",
            "BLOCK OB Main\n"
            + "NETWORK 1 \"slot\"\n  CALL FC_HarnessSlot(EN := TRUE)\n"
            + "NETWORK 2 \"copy\"\n  CALL FC_HarnessCopyLayer(EN := TRUE)\n"
            + "END_BLOCK\n");

        var result = Reachability.Of(new[] { slot, stim, uut, copy, main });

        Assert.Empty(result.Refusals);
    }

    /// <summary>
    /// The obligation the generator emits names the block the OB has to call, so honouring it does not
    /// require reading the IR. Pinned here rather than only in the generator's own tests, because this is
    /// the file that explains what the obligation is FOR.
    /// </summary>
    [Fact]
    public void The_generators_obligation_names_the_block_the_OB_must_call()
    {
        var result = SlotFcGenerator.Generate(
            new SlotFcNaming("FC_HarnessSlot", 9010),
            new SlotCall("FB_DemoStim", "iDB_DemoStim", "a"),
            new SlotCall("FB_DemoUnderTest", "iDB_DemoUnderTest", "b"));

        Assert.Contains(result.BlockName, result.CallSiteObligation);
    }
}
