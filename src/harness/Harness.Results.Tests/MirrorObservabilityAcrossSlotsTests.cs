using Harness.Map;
using Harness.Results;

namespace Harness.Results.Tests;

/// <summary>
/// 🔴 <b>THE OBSERVABILITY MAP HAS NO SLOT AXIS, AND A WAVE SET WIDER THAN ONE SLOT PUTS SEVERAL
/// DECLARATIONS UNDER ONE KEY.</b>
///
/// <para>Gate 5 asks "in which modes is signal X watchable" and is keyed on the SPECIFICATION's name,
/// which is right — a vector cites that name and nothing else. But a wave set may legitimately have
/// several slots observing one signal, and the hopper set is exactly that shape
/// (<c>harness-binding.md:179-181</c>: all six slots drive the same instance and the same members).
/// <c>FromBindings</c> flattens every binding's result sources into one stream, so those six arrive as
/// six declarations of one key.</para>
///
/// <para><b>The plain overwrite it used to do gave the LAST binding in the list the final say for every
/// slot</b> — silently, and with no way for a reader to know it had happened. Agreeing declarations must
/// fold (or shared observation would be unusable); disagreeing ones must fail closed and say so.</para>
/// </summary>
public class MirrorObservabilityAcrossSlotsTests
{
    private static MirroredSignal Signal(string tag, string spec, string? latchedBy = null) =>
        new(tag, MirrorValueType.Bool, SpecName: spec, LatchedBy: latchedBy);

    /// <summary>
    /// The derivation under test, attributed. <b>These tests are about the MODE ALGEBRA and not about
    /// authority</b> — but <c>FromBindings</c> takes the author as a required argument precisely so that
    /// no call site can leave it unsaid, and a bare <c>default</c> repeated four times below would read
    /// as an oversight rather than as the choice it is.
    /// </summary>
    private static MirrorObservability Map(IEnumerable<MirroredSignal> signals) =>
        MirrorObservability.FromBindings(signals, new AgentIdentity("agent-k"));

    [Fact]
    public void SIX_AGREEING_DECLARATIONS_FOLD_INTO_ONE_ENTRY_and_nothing_is_reported_as_divergent()
    {
        // The deliverable's own shape: six slots, one block, identical instrumentation on each.
        var map = Map(
            Enumerable.Range(0, 6).Select(_ => Signal("IO.HopperBlockedAlarm", "HopperBlockedAlarm", "FB_HarnessViolationLatch")));

        var modes = Assert.Single(map.ProvidedFor).Value;

        Assert.Contains(InstrumentationMode.Sampled, modes);
        Assert.Contains(InstrumentationMode.Latched, modes);
        Assert.Empty(map.DivergentAcrossDeclarations);
        Assert.Equal("FB_HarnessViolationLatch", map.LatchProvenance["HopperBlockedAlarm"]);
    }

    [Fact]
    public void DISAGREEING_DECLARATIONS_INTERSECT_rather_than_letting_the_LAST_one_decide()
    {
        // One slot claims a latch, one does not. Under the old overwrite the answer depended entirely on
        // list order: reversed, the same two declarations produced opposite maps.
        var withLatch = Signal("IO.Alarm", "Alarm", "FB_HarnessViolationLatch");
        var without = Signal("IO.Alarm", "Alarm");

        var forwards = Map(new[] { withLatch, without });
        var backwards = Map(new[] { without, withLatch });

        // Order-independent, which is the property that makes it a rule rather than an accident.
        Assert.Equal(forwards.For("Alarm"), backwards.For("Alarm"));

        // Fails CLOSED: a vector needing Latched is refused and argues, where admitting it would be a
        // silent miss on whichever slot has no latch.
        Assert.DoesNotContain(InstrumentationMode.Latched, forwards.For("Alarm"));
        Assert.Contains(InstrumentationMode.Sampled, forwards.For("Alarm"));

        // The provenance goes with the mode. A named latch beside a mode nobody offers is a claim about
        // nothing, and it would be printed in the gate's report as though it applied.
        Assert.False(forwards.LatchProvenance.ContainsKey("Alarm"));

        // And it is NAMED, so the refusal can be argued with rather than being mysterious.
        Assert.Equal(new[] { "Alarm" }, forwards.DivergentAcrossDeclarations);
    }

    [Fact]
    public void A_SINGLE_DECLARATION_IS_UNTOUCHED_and_reports_no_divergence()
    {
        // The unaffected case, asserted as deliberately as the narrowed one.
        var map = Map(new[] { Signal("IO.Alarm", "Alarm", "FB_HarnessViolationLatch") });

        Assert.Contains(InstrumentationMode.Latched, map.For("Alarm"));
        Assert.Empty(map.DivergentAcrossDeclarations);
        Assert.Equal("FB_HarnessViolationLatch", map.LatchProvenance["Alarm"]);
    }
}
