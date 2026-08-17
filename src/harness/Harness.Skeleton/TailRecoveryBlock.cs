using System.Text;
using Harness.Map;

namespace Harness.Skeleton;

/// <summary>The shape this block is generated in. Both are CORRECT programs — neither is a defect in the harness's sense.</summary>
public enum TailRecoveryShape
{
    /// <summary>
    /// The response is presented for ticks <c>[1, Hold]</c> and the arm window is open for
    /// <c>[Arm, Hold]</c>, with <c>Arm &gt; 1</c>. So the response is up BEFORE the window opens and down
    /// AFTER it closes, and the completing frame has it down.
    /// </summary>
    ResponsePresentedThroughTheWindow,

    /// <summary>
    /// 🔴 <b>The response is withdrawn BEFORE the arm window opens</b> — presented for ticks
    /// <c>[1, Arm)</c> only.
    ///
    /// <para><b>This is the discriminating half of the pair, and it is what makes the window check
    /// falsifiable.</b> Over the whole series the two shapes look alike: both present the response at some
    /// frames and not at others, so both are <i>mixed</i>. They differ only in the IN-WINDOW frames — all
    /// agreeing in the first, all disagreeing here. <b>A window check that read the wrong register, or no
    /// register, could not tell them apart.</b></para>
    /// </summary>
    ResponseWithdrawnBeforeTheWindowOpens,
}

/// <summary>
/// 🔴 <b>THE FIXTURE FOR THE OBSERVATION DEFECT: A BLOCK THAT RETURNS TO INERT BEFORE IT SIGNALS
/// COMPLETION.</b>
///
/// <para><b>Why it exists, and why the existing skeleton blocks could not serve.</b> Both
/// <see cref="TrivialBlock"/> and <see cref="PeakBlock"/> hold their result at its final value once they
/// raise their completion flag — the completing frame IS the answer for them. So every fixture in this
/// repository agreed with the completion snapshot, and <i>every one of the 1,770 tests was blind to a
/// harness that kept only that snapshot</i>. That is the same trap as the one that hid the spec-name join
/// defect: a fixture in which two things coincide cannot tell them apart.</para>
///
/// <para><b>The shape reproduced here is the one every well-built stimulus model has</b>, and it is
/// required rather than incidental: a model must return the block under test to inert BEFORE it announces
/// it has finished, or the wave dies after one vector. JOB9004's valve model does exactly this — its own
/// tail recovery runs a disarm, a reset pulse and a verify, and only then raises <c>ScenarioDone</c>. The
/// measured consequence was an assertion expecting <c>Y11 = true</c> being sampled ~600 ms after the
/// scenario ended and reading false: <b>a value impossible for ANY block at that instant</b>, reported as
/// a FAIL against a block proven correct from an earlier frame.</para>
///
/// <para><b>What it does.</b> While its start command is off it holds its tick and its completion flag at
/// zero. While the start command is on it advances a tick each scan, presents a response through a window,
/// publishes its own arm flag through a NARROWER window, and — after the response has been withdrawn —
/// reports the scenario complete. Three vector registers in (arm, hold, end), three result registers out
/// (response, armed, done).</para>
/// </summary>
public static class TailRecoveryBlock
{
    public const string StartTag = "Tail_Start";

    /// <summary>First tick at which the arm flag is published. Deliberately LATER than the response rises.</summary>
    public const string ArmTag = "Tail_Arm";

    /// <summary>Last tick at which the response is presented, and the last at which the arm flag is published.</summary>
    public const string HoldTag = "Tail_Hold";

    /// <summary>Tick at which the scenario reports itself complete — AFTER the response has been withdrawn.</summary>
    public const string EndTag = "Tail_End";

    public const string TickTag = "Tail_Tick";

    /// <summary>The signal under assertion.</summary>
    public const string ResponseTag = "Tail_Response";

    /// <summary>The model's own arm flag — the observation window, published in-band exactly as JOB9004's model publishes <c>Stim.Armed</c>.</summary>
    public const string ArmedTag = "Tail_Armed";

    /// <summary>The completion signal the client polls.</summary>
    public const string DoneTag = "Tail_Done";

    /// <summary>Generate the block and its tag table.</summary>
    public static IReadOnlyList<HarnessObject> Generate(
        int baseByte, int blockNumber, TailRecoveryShape shape = TailRecoveryShape.ResponsePresentedThroughTheWindow)
    {
        if (baseByte < 0 || baseByte % 2 != 0)
            throw new ArgumentOutOfRangeException(nameof(baseByte), baseByte, "the region must start on an even byte; a word tag on an odd byte straddles two words.");

        if (blockNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(blockNumber), blockNumber, "block numbers come from the caller's reserved range, never from a generator (hard rule 3).");

        // The ONE difference between the two shapes, and it is one rung's guard. Everything else about the
        // two builds is identical, so a difference in outcome cannot be attributed to anything else.
        var responseWindow = shape == TailRecoveryShape.ResponseWithdrawnBeforeTheWindowOpens
            ? $"{TickTag} >= 1 AND {TickTag} < {ArmTag}"
            : $"{TickTag} >= 1 AND {TickTag} <= {HoldTag}";

        var table = new StringBuilder();
        table.Append("TAGTABLE DemoTail\n");
        table.Append("  ROOTID 0\n");
        table.Append("  TAGS\n");
        table.Append($"    {StartTag} 1 : Bool @ %M{baseByte}.0 ACCESSIBLE VISIBLE WRITABLE COMMENT \"Start command. The scenario runs only while this is on.\"\n");
        table.Append($"    {ResponseTag} 2 : Bool @ %M{baseByte}.1 ACCESSIBLE VISIBLE WRITABLE COMMENT \"The response the specification asks about.\"\n");
        table.Append($"    {ArmedTag} 3 : Bool @ %M{baseByte}.2 ACCESSIBLE VISIBLE WRITABLE COMMENT \"Open while the scenario is inside the window in which the response is claimed.\"\n");
        table.Append($"    {ArmTag} 4 : Int @ %MW{baseByte + 2} ACCESSIBLE VISIBLE WRITABLE COMMENT \"First tick at which the arm flag is published.\"\n");
        table.Append($"    {HoldTag} 7 : Int @ %MW{baseByte + 4} ACCESSIBLE VISIBLE WRITABLE COMMENT \"Last tick at which the response is presented.\"\n");
        table.Append($"    {EndTag} A : Int @ %MW{baseByte + 6} ACCESSIBLE VISIBLE WRITABLE COMMENT \"Tick at which the scenario reports itself complete.\"\n");
        table.Append($"    {TickTag} D : Int @ %MW{baseByte + 8} ACCESSIBLE VISIBLE WRITABLE COMMENT \"Scans since the start command came on.\"\n");
        table.Append($"    {DoneTag} 10 : Int @ %MW{baseByte + 10} ACCESSIBLE VISIBLE WRITABLE COMMENT \"1 once the scenario has finished.\"\n");

        var block = new StringBuilder();
        block.Append("BLOCK FC FC_DemoTail\n");
        block.Append("ROOTID 0\n");
        block.Append($"NUMBER {blockNumber}\n");
        block.Append("LANGUAGE LAD\n");
        block.Append("TITLE \"Present a response through a window, withdraw it, then report the scenario complete\"\n");
        block.Append('\n');
        block.Append("INTERFACE\n");
        block.Append("  INPUT\n");
        block.Append("  OUTPUT\n");
        block.Append("  CONSTANT\n");
        block.Append('\n');
        block.Append("NETWORK 1 \"Hold the tick and the completion flag at zero while the start command is off\"\n");
        block.Append($"  MOVE(EN := NOT {StartTag}, IN := 0) => {TickTag}\n");
        block.Append($"  MOVE(EN := NOT {StartTag}, IN := 0) => {DoneTag}\n");
        block.Append('\n');
        block.Append("NETWORK 2 \"Advance the tick each scan until the scenario has finished\"\n");
        block.Append($"  ADD(EN := {StartTag} AND {DoneTag} = 0, IN1 := {TickTag}, IN2 := 1) => {TickTag}\n");
        block.Append('\n');
        block.Append("NETWORK 3 \"Present the response through its window\"\n");
        block.Append($"  COIL {ResponseTag} := {StartTag} AND {responseWindow}\n");
        block.Append('\n');
        block.Append("NETWORK 4 \"Publish the arm flag through the window in which the response is claimed\"\n");
        block.Append($"  COIL {ArmedTag} := {StartTag} AND {TickTag} >= {ArmTag} AND {TickTag} <= {HoldTag}\n");
        block.Append('\n');
        block.Append("NETWORK 5 \"Report the scenario complete, after the response has been withdrawn\"\n");
        block.Append($"  MOVE(EN := {StartTag} AND {TickTag} >= {EndTag}, IN := 1) => {DoneTag}\n");

        return new[]
        {
            new HarnessObject("DemoTail", HarnessObjectKind.TagTable, table.ToString()),
            new HarnessObject("FC_DemoTail", HarnessObjectKind.Block, block.ToString()),
        };
    }

    /// <summary>
    /// The copy-layer binding for this block.
    /// </summary>
    /// <param name="latchTheResponse">
    /// Whether the response is declared <c>Transient</c> + <c>RearmsEachIndex</c> + <c>ArmedBy</c>, which
    /// is what makes the copy layer generate a PHASE-ARMED LATCH for it and what makes an expectation
    /// declared <c>Latched</c> answerable. <b>Both bindings are offered because the difference between
    /// them is the whole of H-2</b>, and a fixture that only ever declared the latch could not show what
    /// happens without one.
    /// </param>
    /// <param name="declareTheArmWindow">
    /// Whether the response's arm window is declared at all. <b>Not the same question as
    /// <paramref name="latchTheResponse"/></b>: the generator refuses an arm window on a signal with no
    /// generated latch, so an arm window implies a latch — but a latch does not imply an arm window, and
    /// the <c>Sampled</c> path reads the arm window without reading the latch.
    /// </param>
    public static SlotBinding Binding(string slotId = "S0", bool latchTheResponse = false, bool declareTheArmWindow = false) => new(
        slotId,
        MirroredSignal.Ints(ArmTag, HoldTag, EndTag),
        StartTag,
        new[]
        {
            // Spec names stated and DELIBERATELY DIFFERENT FROM THE TAGS, so this fixture cannot pass by
            // the two keys coinciding — the trap that hid the spec-name join defect for a whole wave.
            new MirroredSignal(ResponseTag, MirrorValueType.Bool, SpecName: "SPEC.Response",
                Transient: latchTheResponse || declareTheArmWindow,
                RearmsEachIndex: latchTheResponse || declareTheArmWindow,
                ArmedBy: declareTheArmWindow ? ArmedTag : null),
            new MirroredSignal(ArmedTag, MirrorValueType.Bool, SpecName: "SPEC.Armed"),
            new MirroredSignal(DoneTag, MirrorValueType.Int, SpecName: "SPEC.Done"),
        });
}
