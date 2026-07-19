using System.Linq;
using Converter.Ir;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// Marker-driven fan-out synthesis (ADR-0006 phase 3): `SidecarSynthesizer` reads the `{split N}`/`{recv N}`
/// markers and reproduces the shared parts — a `{recv N}` reuses node N's registered step (same part UId, no
/// duplicate Access), rather than the old per-network SPLIT prefix heuristic. Byte-exact reproduction across
/// the real corpus is covered by the golden parity harness (HandAuthorSplitsMerges); these unit tests pin the
/// mechanism directly.
/// </summary>
public class SidecarSynthesizerFanoutTests
{
    private static ChainStepSidecar.ContactStep Contact(ChainStepSidecar step) =>
        Assert.IsType<ChainStepSidecar.ContactStep>(step);

    [Fact]
    public void Recv_ReusesSplitMasterContact_WithNoDuplicateAccess()
    {
        var network = IrParser.ParseNetworkOnly(
            "NETWORK 1 \"T\"\n  COIL A := Shared{split 1} AND X\n  COIL B := Shared{recv 1} AND Y\n");

        var sidecar = SidecarSynthesizer.Synthesize(network);

        var sharedInA = Contact(sidecar.Assignments[0].Steps[0]);
        var sharedInB = Contact(sidecar.Assignments[1].Steps[0]);
        Assert.Equal(sharedInA.ContactUId, sharedInB.ContactUId); // reused, not rebuilt
        Assert.Equal(sharedInA.OperandAccessUId, sharedInB.OperandAccessUId);
        Assert.Equal(1, sidecar.AccessUIds.Count(a => a.TagPath == "Shared")); // one Access, fanned out
    }

    [Fact]
    public void Cascade_EachRecvReusesTheDeeperNode()
    {
        // NF{split1} → NF{recv1} Run{split2} → NF Run{recv2} RunningFB: node 2 = NF·Run, so the third rung
        // (receiving node 2) reuses both NF and Run steps by reference (MotorStarter N13 shape).
        var network = IrParser.ParseNetworkOnly(
            "NETWORK 1 \"T\"\n" +
            "  COIL A0 := NF{split 1}\n" +
            "  COIL A1 := NF{recv 1} AND Run{split 2}\n" +
            "  COIL A2 := NF AND Run{recv 2} AND RunningFB\n");

        var sidecar = SidecarSynthesizer.Synthesize(network);
        var a0 = sidecar.Assignments[0].Steps;
        var a1 = sidecar.Assignments[1].Steps;
        var a2 = sidecar.Assignments[2].Steps;

        Assert.Equal(Contact(a0[0]).ContactUId, Contact(a1[0]).ContactUId); // A1 reuses NF
        Assert.Equal(Contact(a0[0]).ContactUId, Contact(a2[0]).ContactUId); // A2 reuses NF (via node 2)
        Assert.Equal(Contact(a1[1]).ContactUId, Contact(a2[1]).ContactUId); // A2 reuses Run (via node 2)
        Assert.Equal(3, a2.Count); // NF, Run (both reused) + RunningFB (new)
        Assert.Equal(1, sidecar.AccessUIds.Count(a => a.TagPath == "NF"));
    }

    [Fact]
    public void IntraStatementSplit_SharesContactAcrossTheOneCoilsOrBranches()
    {
        // MotorStarter N1: a single contact fans out to both OR-branches of one coil.
        var network = IrParser.ParseNetworkOnly(
            "NETWORK 1 \"T\"\n  COIL Run := (TryRunMotor{split 1} AND PreStart OR TryRunMotor{recv 1} AND Other) AND NOT Stop\n");

        var sidecar = SidecarSynthesizer.Synthesize(network);
        var orStep = Assert.IsType<ChainStepSidecar.OrStep>(sidecar.Assignments[0].Steps[0]);

        var trmBranch0 = Contact(orStep.Branches[0].Steps[0]);
        var trmBranch1 = Contact(orStep.Branches[1].Steps[0]);
        Assert.Equal(trmBranch0.ContactUId, trmBranch1.ContactUId); // one physical TryRunMotor, fanned out
        Assert.Equal(1, sidecar.AccessUIds.Count(a => a.TagPath == "TryRunMotor"));
    }

    [Fact]
    public void Recv_WithNoMatchingSplit_IsAHardError()
    {
        var network = IrParser.ParseNetworkOnly("NETWORK 1 \"T\"\n  COIL A := Orphan{recv 7} AND X\n");
        Assert.Throws<UnsupportedSynthesisConstructException>(() => SidecarSynthesizer.Synthesize(network));
    }

    [Fact]
    public void BoxEn_ReusesPrefixSharedWithAMove()
    {
        // MotorStarter N12 shape: a MOVE (reset) and an ADD (increment, a box) share a `Shared AND Gate`
        // prefix. The box EN is now in fan-out scope (ADR-0006 phase 4), so the ADD reuses node 2 rather than
        // rebuilding — the exact +2 contacts that used to divergence.
        var network = IrParser.ParseNetworkOnly(
            "NETWORK 1 \"T\"\n" +
            "  MOVE(EN := Shared{split 1} AND Gate{split 2}, IN := 0) => Dest\n" +
            "  ADD(EN := Shared AND Gate{recv 2}, IN1 := 1, IN2 := Dest) => Dest\n");

        var sidecar = SidecarSynthesizer.Synthesize(network);
        var moveSteps = sidecar.Moves[0].Steps;
        var addEn = Assert.IsType<EnSourceSidecar.ConditionSidecar>(sidecar.Muls[0].En);

        Assert.Equal(Contact(moveSteps[0]).ContactUId, Contact(addEn.Steps[0]).ContactUId); // ADD reuses Shared
        Assert.Equal(Contact(moveSteps[1]).ContactUId, Contact(addEn.Steps[1]).ContactUId); // ADD reuses Gate
        Assert.Equal(1, sidecar.AccessUIds.Count(a => a.TagPath == "Gate")); // one Gate Access, fanned out
    }
}
