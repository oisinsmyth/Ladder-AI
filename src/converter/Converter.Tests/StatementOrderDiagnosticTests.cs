using Converter.Ir;
using Xunit;

namespace Converter.Tests;

// FI-69. The kind-ordering rule itself (ir/SPEC.md "Statement-kind ordering within one network") is
// real and is not what these tests are about — they are about the DIAGNOSTIC. A violation used to be
// consumed by no section loop, fall through to the outer network loop, and report
// "Expected 'NETWORK <n> \"<title>\"' at line N" — a message naming a header that is perfectly
// well-formed, at a line number that points at the first statement the parser could not place. It cost
// two import passes on a live job before the actual rule was recognised.
//
// Same family as FI-61's ConnectTimeout and FI-66's non-converging count: a message that confidently
// names the wrong cause. What each test pins is that the message names the RULE, the kind found, and
// the kind it has to move above.
public class StatementOrderDiagnosticTests
{
    [Fact]
    public void CoilAfterMove_NamesTheOrderingRuleRatherThanTheNetworkHeader()
    {
        var ex = Assert.Throws<IrFormatException>(() => IrParser.ParseNetworkOnly(
            "NETWORK 12 \"Publish\"\n" +
            "  MOVE(EN := RunPermit, IN := SourceValue) => Status.Code\n" +
            "  COIL Status.Valid := RunPermit\n"));

        // The three things the old message left the author to infer.
        Assert.Contains("COIL/SCOIL/RCOIL", ex.Message);
        Assert.Contains("cannot follow a MOVE", ex.Message);
        Assert.Contains("move it above the first MOVE", ex.Message);

        // And the thing it used to say instead, which sent the reader to a line that was fine.
        Assert.DoesNotContain("Expected 'NETWORK", ex.Message);
    }

    [Fact]
    public void TheMessageCarriesTheNetworkNumberTheLineAndTheOffendingText()
    {
        var ex = Assert.Throws<IrFormatException>(() => IrParser.ParseNetworkOnly(
            "NETWORK 12 \"Publish\"\n" +
            "  MOVE(EN := RunPermit, IN := SourceValue) => Status.Code\n" +
            "  COIL Status.Valid := RunPermit\n"));

        Assert.Contains("Network 12", ex.Message);
        Assert.Contains("line 3", ex.Message);
        Assert.Contains("COIL Status.Valid := RunPermit", ex.Message);
    }

    // The fix is one table, not one special case: the pair that was actually hit must not be the only
    // pair that gets a usable message.
    [Theory]
    [InlineData("  CALL PumpControl(Pump1_DB, EN := TRUE)", "  TON(RunDelay, IN := Sensor1.Ok, PT := T#100MS)", "TON/TONR/TOF", "CALL")]
    [InlineData("  MOVE(EN := RunPermit, IN := 1) => Status.Code", "  TON(RunDelay, IN := Sensor1.Ok, PT := T#100MS)", "TON/TONR/TOF", "MOVE")]
    [InlineData("  CONVERT(EN := TRUE, IN := RawValue) => Scaled", "  MOVE(EN := RunPermit, IN := 1) => Status.Code", "MOVE", "CONVERT")]
    [InlineData("  MODBUS_COMM_LOAD(Load_DB, EN := TRUE, REQ := TRUE, PORT := 1, BAUD := 9600, PARITY := 0, RESP_TO := 100, MB_DB := 1, DONE => D, ERROR => E, STATUS => S)", "  WAIT(EN := TRUE, WT := 10)", "WAIT", "MODBUS_COMM_LOAD")]
    public void EveryKindGetsTheSameDiagnostic_NotJustTheOneThatWasHit(
        string firstLine, string strayLine, string strayKind, string blockerKind)
    {
        var ex = Assert.Throws<IrFormatException>(() => IrParser.ParseNetworkOnly(
            $"NETWORK 1 \"T\"\n{firstLine}\n{strayLine}\n"));

        Assert.Contains($"a {strayKind} cannot follow a {blockerKind}", ex.Message);
        Assert.DoesNotContain("Expected 'NETWORK", ex.Message);
    }

    // A network COMMENT is subject to a position rule too (immediately after the header), and failed
    // with the same misleading message. Fixed by the same table rather than separately — the standing
    // preference on this project is one general fix over stacked special cases.
    [Fact]
    public void CommentAfterAStatement_IsAlsoNamedRatherThanReportedAsABadHeader()
    {
        var ex = Assert.Throws<IrFormatException>(() => IrParser.ParseNetworkOnly(
            "NETWORK 4 \"T\"\n" +
            "  COIL Status.Valid := RunPermit\n" +
            "  COMMENT \"Late\"\n"));

        Assert.Contains("a COMMENT cannot follow a COIL/SCOIL/RCOIL", ex.Message);
        Assert.DoesNotContain("Expected 'NETWORK", ex.Message);
    }

    // The message states the whole order, so an author who hit one pair learns the rule rather than
    // just that one pair.
    [Fact]
    public void TheMessageStatesTheFullKindOrder()
    {
        var ex = Assert.Throws<IrFormatException>(() => IrParser.ParseNetworkOnly(
            "NETWORK 1 \"T\"\n" +
            "  MOVE(EN := RunPermit, IN := 1) => Status.Code\n" +
            "  COIL Status.Valid := RunPermit\n"));

        Assert.Contains("grouped by kind, one contiguous run each, in this order:", ex.Message);
        Assert.Contains("COMMENT, TON/TONR/TOF, COIL/SCOIL/RCOIL, MOVE, WAND, CALL", ex.Message);
        Assert.Contains("MODBUS_COMM_LOAD", ex.Message);
    }

    // The guard has to stay silent on correct input — a check that fires on a well-ordered network
    // would be worse than the message it replaces.
    [Fact]
    public void AWellOrderedNetworkStillParses()
    {
        var network = IrParser.ParseNetworkOnly(
            "NETWORK 7 \"Mixed\"\n" +
            "  COMMENT \"Everything, in order\"\n" +
            "  TON(RunDelay, IN := Sensor1.Ok, PT := T#100MS)\n" +
            "  COIL Status.Valid := RunPermit\n" +
            "  RCOIL Status.Fault := ResetCmd\n" +
            "  MOVE(EN := RunPermit, IN := SourceValue) => Status.Code\n" +
            "  CALL PumpControl(Pump1_DB, EN := TRUE)\n" +
            "  CONVERT(EN := TRUE, IN := RawValue) => Scaled\n");

        Assert.Equal(7, network.Number);
        Assert.Equal("Everything, in order", network.Comment);
        Assert.Single(network.Timers);
        Assert.Equal(2, network.Assignments.Count);
        Assert.Single(network.Moves);
        Assert.Single(network.Calls);
        Assert.Single(network.Converts);
    }

    // Two runs of one kind split by another is the same violation and the same message — the rule is
    // "one contiguous run per kind", not "sorted".
    [Fact]
    public void ASecondRunOfAnAlreadyClosedKindIsReportedAsOutOfOrder()
    {
        var ex = Assert.Throws<IrFormatException>(() => IrParser.ParseNetworkOnly(
            "NETWORK 1 \"T\"\n" +
            "  MOVE(EN := A, IN := 1) => Status.Code\n" +
            "  CALL PumpControl(Pump1_DB, EN := TRUE)\n" +
            "  MOVE(EN := B, IN := 2) => Status.Word\n"));

        Assert.Contains("a MOVE cannot follow a CALL", ex.Message);
    }
}
