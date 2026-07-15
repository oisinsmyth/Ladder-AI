using System.Xml.Linq;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// Sidecar synthesis (2026-07-15) — semantic-fidelity round trip. Reuses existing real fixtures
/// (no new ones needed): reduce a real fixture, discard its real sidecar entirely, synthesize a
/// fresh one instead from the same IrNetwork, rebuild XML via the unmodified FlgNetBuilder/
/// FlgNetWriter, reparse, reduce again, and confirm the re-reduced network reads back exactly the
/// same as the original — proving synthesis preserves logical meaning, using ground truth that
/// already exists rather than hand-crafted expectations.
///
/// Text comparison (IrSerializer.SerializeNetworkOnly), not Assert.Equal on the Expr records
/// directly — Expr.And/Or's own Operands (IReadOnlyList&lt;Expr&gt;, backed by List&lt;Expr&gt;)
/// don't get structural equality for free, so a direct record comparison of two independently-
/// built trees would fail even when they're logically identical.
/// </summary>
public class SidecarSynthesizerFidelityTests
{
    private static FlgNetwork LoadFixture(string name)
    {
        var element = XElement.Load(Path.Combine("Fixtures", name));
        return FlgNetParser.Parse(element);
    }

    private static void AssertSynthesisPreservesLogic(string fixtureName, string title, string compileUnitUId, int networkNumber = 1)
    {
        var original = LoadFixture(fixtureName);
        var originalReduced = GraphReducer.Reduce(original, networkNumber: networkNumber, title: title, compileUnitUId: compileUnitUId);

        // The real sidecar is deliberately never touched from here on — only originalReduced.Network
        // (the plain Expr/CoilAssignment tree) feeds the synthesizer.
        var synthesizedSidecar = SidecarSynthesizer.Synthesize(originalReduced.Network);

        var rebuilt = FlgNetBuilder.Build(originalReduced.Network, synthesizedSidecar);
        var xml = FlgNetWriter.Write(rebuilt);
        var reparsed = FlgNetParser.Parse(xml);
        var reReduced = GraphReducer.Reduce(reparsed, networkNumber: networkNumber, title: title, compileUnitUId: compileUnitUId);

        Assert.Equal(
            IrSerializer.SerializeNetworkOnly(originalReduced.Network),
            IrSerializer.SerializeNetworkOnly(reReduced.Network));
    }

    [Fact]
    public void SimpleAndCoil_SynthesizedSidecar_PreservesLogic() =>
        AssertSynthesisPreservesLogic("SimpleAndCoil.xml", "Test AND", "3");

    [Fact]
    public void OrMergeCoil_SynthesizedSidecar_PreservesLogic() =>
        AssertSynthesisPreservesLogic("OrMergeCoil.xml", "Any fault", "3");

    [Fact]
    public void NestedOrMerge_SynthesizedSidecar_PreservesLogic() =>
        AssertSynthesisPreservesLogic("NestedOrMerge.xml", "Nested OR", "3");

    [Fact]
    public void OrMergeSharedPrefixBranches_SynthesizedSidecar_PreservesLogic() =>
        AssertSynthesisPreservesLogic("OrMergeSharedPrefixBranches.xml", "Shared prefix", "3");

    [Fact]
    public void CoilSetResetFedByRail_SynthesizedSidecar_PreservesLogic() =>
        AssertSynthesisPreservesLogic("CoilSetResetFedByRail.xml", "Motor latch", "8");

    // Deliberately interesting: the ORIGINAL fixture represents "NOT EnableCmd" via a standalone
    // Not Part fed by genuine wire fan-out (a different XML shape than the synthesizer itself
    // would ever produce for the same Expr — the synthesizer represents NOT-of-a-bare-tag as a
    // negated Contact, never a standalone Not Part). This test still passes: the fidelity claim is
    // about preserved *meaning* (the re-reduced Expr reads identically), not preserved *XML shape*.
    [Fact]
    public void NotFedByContact_SynthesizedSidecar_PreservesLogicDespiteDifferentXmlShape() =>
        AssertSynthesisPreservesLogic("NotFedByContact.xml", "Not tap", "3");

    // Compare support (v2, 2026-07-15): a real comparison feeding a Coil directly (Contact-like
    // chain position, per ir/SPEC.md's own "a comparison behaves like a Contact" finding) now
    // synthesizes and round-trips, same fidelity proof as every other real fixture above.
    [Fact]
    public void GtFeedsCoil_SynthesizedSidecar_PreservesLogic() =>
        AssertSynthesisPreservesLogic("GtFeedsCoil.xml", "Level greater-than check", "3");

    // TON support (v2): a plain TON, PT fed by a tag (not a literal — this build's own real need).
    [Fact]
    public void WithTon_SynthesizedSidecar_PreservesLogic() =>
        AssertSynthesisPreservesLogic("WithTon.xml", "Run enable delay", "3");

    // TON support (v2): Q read back via an ordinary Access elsewhere in the network (this
    // synthesizer's only supported way to read a timer's own output — see BuildTimerSidecar's own
    // doc comment for why TimerOutputStep, a Q wired *directly* into a downstream Part, is a
    // deliberately different, unimplemented shape).
    [Fact]
    public void WithTonAndQReadBack_SynthesizedSidecar_PreservesLogic() =>
        AssertSynthesisPreservesLogic("WithTonAndQReadBack.xml", "General enable delay", "5", networkNumber: 3);

    // MOVE support (v2): a Move tapped off a chain position via genuine wire fan-out in the
    // original fixture — proves synthesis preserves the *logic* (the tap and the chain's own
    // continuation both still read the same value) even though the synthesizer itself never
    // produces fan-out on its own (this class's own fidelity claim, stated above: preserved
    // meaning, not preserved XML shape).
    [Fact]
    public void MoveFedByContact_SynthesizedSidecar_PreservesLogic() =>
        AssertSynthesisPreservesLogic("MoveFedByContact.xml", "Status tap", "3");

    // MUL/CONVERT support (v2): the real "EN := ENO" chained pair this build's whole HMI-seconds-
    // to-milliseconds idiom (C-307/DB_Settings) is grounded on.
    [Fact]
    public void MulConvertEnoChainedPair_SynthesizedSidecar_PreservesLogic() =>
        AssertSynthesisPreservesLogic("MulConvertEnoChainedPair.xml", "Speed scale and convert", "1C");

    // MUL/CONVERT support (v2): two independent ENO-chained pairs in one network — the exact case
    // that requires index-paired (not batched) synthesis, see Synthesize's own doc comment on the
    // Mul/Convert loop. This is the fixture that would catch a regression back to batching.
    [Fact]
    public void TwoIndependentMulConvertChainsInterleaved_SynthesizedSidecar_PreservesLogic() =>
        AssertSynthesisPreservesLogic("TwoIndependentMulConvertChainsInterleaved.xml", "Two independent scale chains", "1C");

    // CALL support (v2): a zero-argument FB call, rail-fed — the only Call shape this synthesizer
    // supports (see BuildCallSidecar's own doc comment for why a wired-argument call hard-errors).
    [Fact]
    public void CallBareFedByRail_SynthesizedSidecar_PreservesLogic() =>
        AssertSynthesisPreservesLogic("CallBareFedByRail.xml", "Pump control", "58");
}
