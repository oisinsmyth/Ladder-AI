using Converter.Ir;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// Fan-out marker grammar (ADR-0006, phase 1): the `{split N}`/`{recv N}` node markers round-trip through
/// the readable serializer and parser. Model + serializer + parser only — the reducer (phase 2) and
/// synthesizer (phase 3) don't yet emit/consume them. The boundary semantics ("the chain up to and
/// including the marked element is node N") are a synthesis concern; here we prove the *text* round-trips
/// exactly and the marker lands on the right node.
/// </summary>
public class FanoutMarkerGrammarTests
{
    private static string RoundTrip(string network) =>
        IrSerializer.SerializeNetworkOnly(IrParser.ParseNetworkOnly(network));

    [Theory]
    // Single-contact share inside one coil's OR (real: MotorStarter N1) — the intra-statement case.
    [InlineData("NETWORK 1 \"N1\"\n" +
        "  COIL IO.Run := (IO.TryRunMotor{split 1} AND PreStartMemory AND IO.RecentStart OR IO.TryRunMotor{recv 1} AND IO.Run) AND NOT IO.StopMotor AND NOT IO.Shutdown\n")]
    // Cascade share across four MOVEs (real: MotorStarter N13) — every rung self-contained, boundary marked.
    [InlineData("NETWORK 13 \"N13\"\n" +
        "  MOVE(EN := NOT IO.FaultActive{split 1}, IN := 0) => IO.Telemetry\n" +
        "  MOVE(EN := NOT IO.FaultActive{recv 1} AND IO.Run{split 2}, IN := 1) => IO.Telemetry\n" +
        "  MOVE(EN := NOT IO.FaultActive AND IO.Run{recv 2} AND IO.RunningFB{split 3}, IN := 2) => IO.Telemetry\n" +
        "  MOVE(EN := NOT IO.FaultActive AND IO.Run AND IO.RunningFB{recv 3} AND IO.UPSEnable OR NOT IO.FaultActive AND IO.Run AND IO.RunningFB{recv 3} AND IO.InHand, IN := 3) => IO.Telemetry\n")]
    // Marked negated contact — the marker binds `NOT X`, not the inner `X`.
    [InlineData("NETWORK 2 \"neg\"\n  COIL Out := NOT EnableCmd{split 1} AND ModeA OR NOT EnableCmd{recv 1} AND ModeB\n")]
    // Marked comparison — parenthesised so the suffix binds the whole comparison.
    [InlineData("NETWORK 3 \"cmp\"\n  COIL Out := (Level = 100){split 1} AND Ready OR (Level = 100){recv 1} AND Alt\n")]
    // Marked OR-merge (a shared parallel branch, real shape: HandAuthorSplitsMerges N7).
    [InlineData("NETWORK 4 \"ormerge\"\n  COIL Out := (A OR B){split 1} AND C OR (A OR B){recv 1} AND D\n")]
    // Whole-condition single shared element (real: MotorStarter N12's coils reading a shared timer Q).
    [InlineData("NETWORK 12 \"whole\"\n  COIL RisingEdgeFlags[2] := HrTotaliserTimer.Q{split 1}\n  COIL HrTotaliserTimerReset := HrTotaliserTimer.Q{recv 1}\n")]
    public void Markers_RoundTripExactly(string network) => Assert.Equal(network, RoundTrip(network));

    [Fact]
    public void Split_OnWholeConditionTag_ParsesToTagRefWithSplitMarker()
    {
        var network = IrParser.ParseNetworkOnly("NETWORK 1 \"T\"\n  COIL X := SomeTag{split 3}\n");
        var tag = Assert.IsType<Expr.TagRef>(Assert.Single(network.Assignments).Condition);
        Assert.Equal("SomeTag", tag.Path);
        Assert.Equal(new FanoutMarker(FanoutMarkerKind.Split, 3), tag.Fanout);
    }

    [Fact]
    public void Recv_OnNegatedContact_BindsToTheNotNotTheInnerTag()
    {
        // `NOT EnableCmd{recv 2}` → the marker is on the Not, and its inner TagRef is unmarked.
        var network = IrParser.ParseNetworkOnly("NETWORK 1 \"T\"\n  COIL X := NOT EnableCmd{recv 2}\n");
        var not = Assert.IsType<Expr.Not>(Assert.Single(network.Assignments).Condition);
        Assert.Equal(new FanoutMarker(FanoutMarkerKind.Recv, 2), not.Fanout);
        Assert.Null(Assert.IsType<Expr.TagRef>(not.Operand).Fanout);
    }

    [Fact]
    public void UnmarkedExpression_HasNullFanout_AndIsUnchanged()
    {
        var network = IrParser.ParseNetworkOnly("NETWORK 1 \"T\"\n  COIL X := A AND NOT B OR C\n");
        Assert.Null(Assert.Single(network.Assignments).Condition.Fanout);
    }

    [Theory]
    [InlineData("{split}")]        // no label
    [InlineData("{recv x}")]       // non-numeric label
    [InlineData("{share 1}")]      // unknown kind
    [InlineData("{split 1")]       // unterminated
    public void MalformedMarker_IsAHardError(string marker) =>
        Assert.Throws<IrFormatException>(() => IrParser.ParseNetworkOnly($"NETWORK 1 \"T\"\n  COIL X := A{marker}\n"));
}
