using System.Text;
using Harness.Map;

namespace Harness.Skeleton;

/// <summary>
/// The defect the block under test is generated with. Phase 2's exit criterion needs exactly two of
/// these — one that is right and one that is wrong.
/// </summary>
public enum TrivialBlockDefect
{
    /// <summary>The block as specified.</summary>
    None,

    /// <summary>
    /// The ramp's guard reads <c>&lt;=</c> where the specification says <c>&lt;</c>, so the count takes
    /// one step too many.
    ///
    /// <para><b>It is deliberately not visible under every vector.</b> With step 3 and limit 10 the count
    /// runs 0,3,6,9,12 either way and the defect produces the RIGHT answer; with step 5 and limit 10 the
    /// correct block stops at 10 and this one overshoots to 15. That asymmetry is the useful part: it
    /// makes the demonstration prove the RED came from the artifact and the vector rather than from a
    /// harness that reddens everything.</para>
    /// </summary>
    OffByOneAtTheLimit,

    /// <summary>
    /// <b>The ramp never stops.</b> The accumulate rung loses its limit guard entirely, so the count
    /// climbs for as long as the start command is on while the done flag still latches at the limit.
    ///
    /// <para><b>This is "a completion flag is NOT a settling signal" as an artifact.</b> The block says
    /// it is finished and goes on changing the value it was asked about, so any single observation is a
    /// snapshot of something still moving - and a harness that compared that snapshot and believed it
    /// would return a confidently wrong verdict rather than a missed one. It exists because the settling
    /// check had no build that could make it fire, and <b>a guard nothing can exercise is a guard nobody
    /// knows works.</b></para>
    /// </summary>
    DoneWhileStillRunning,
}

/// <summary>
/// Build-plan item 2.3 — the one trivial block under test, generated as IR.
///
/// <para><b>What it does.</b> While its start command is off it holds its count and its done flag at
/// zero. While the start command is on it adds one step to the count each scan until the count reaches
/// the limit, then reports done. Two vector registers in (step, limit), two result registers out
/// (count, done).</para>
///
/// <para><b>Why an FC over PLC tags rather than an FB with an instance DB.</b> D37 binds the start bool
/// to the block's EXISTING start condition, and the minimal copy layer drives that with a coil — which
/// needs a writable global. A real block under test will be an FB with an iDB and that is phase 5's
/// business; making the skeleton's subject an FB now would drag in the instance-DB layout question
/// (<c>MemoryLayout</c>, and its device-side re-assertion) for no phase-2 return.</para>
///
/// <para><b>Its state lives in <c>%M</c>, above the retentive window and clear of the mirror.</b> That is
/// visible to <see cref="RetentionCheck"/>'s address rule, which is the point: the block under test
/// consumes the same bit memory the mirror does and the same rule applies to it.</para>
///
/// <para><b>The start condition is the block's own.</b> <c>Demo_Start</c> is what makes the block run at
/// all — not a test-only input added for the harness, which is the scaffolding DB-5 forbids: what ships
/// would then not be what was tested.</para>
/// </summary>
public static class TrivialBlock
{
    /// <summary>Names the copy-layer binding and the model both refer to.</summary>
    public const string StartTag = "Demo_Start";

    public const string StepTag = "Demo_Step";
    public const string LimitTag = "Demo_Limit";
    public const string CountTag = "Demo_Count";
    public const string DoneTag = "Demo_Done";

    /// <summary>Result register index carrying the count.</summary>
    public const int CountRegister = 0;

    /// <summary>Result register index carrying the done flag — the completion signal the client polls.</summary>
    public const int DoneRegister = 1;

    /// <summary>
    /// Generate the block and its tag table.
    /// </summary>
    /// <param name="baseByte">
    /// First byte of the block's own <c>%M</c> region. Required, with no default: it must sit above the
    /// program's retentive window and clear of the mirror, and neither of those is this generator's to
    /// assume (see <see cref="MirrorGeometry.RetentiveBytes"/>, which has no default for the same reason).
    /// </param>
    /// <param name="blockNumber">
    /// Its block number. Required and positive — hard rule 3 forbids inventing block numbers, and X-J
    /// reserves a range for harness objects that the caller allocates from.
    /// </param>
    public static IReadOnlyList<HarnessObject> Generate(int baseByte, int blockNumber, TrivialBlockDefect defect = TrivialBlockDefect.None)
    {
        if (baseByte < 0 || baseByte % 2 != 0)
            throw new ArgumentOutOfRangeException(nameof(baseByte), baseByte, "the region must start on an even byte; a word tag on an odd byte straddles two words.");

        if (blockNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(blockNumber), blockNumber, "block numbers come from the caller's reserved range, never from a generator (hard rule 3).");

        // The one difference between the correct block and the defective one, and it is ONE OPERATOR in
        // ONE rung. Everything else about the two builds is byte-identical, so a red result cannot be
        // attributed to anything else.
        var guard = defect == TrivialBlockDefect.OffByOneAtTheLimit ? "<=" : "<";

        // The accumulate rung's condition. The third defect drops the limit test altogether, so the count
        // keeps climbing while the done flag latches - the value under test never settles.
        var accumulate = defect == TrivialBlockDefect.DoneWhileStillRunning
            ? StartTag
            : $"{StartTag} AND {CountTag} {guard} {LimitTag}";

        var table = new StringBuilder();
        table.Append("TAGTABLE DemoUnit\n");
        table.Append("  ROOTID 0\n");
        table.Append("  TAGS\n");
        table.Append($"    {StartTag} 1 : Bool @ %M{baseByte}.0 ACCESSIBLE VISIBLE WRITABLE COMMENT \"Start command. The block runs only while this is on.\"\n");
        table.Append($"    {StepTag} 4 : Int @ %MW{baseByte + 2} ACCESSIBLE VISIBLE WRITABLE COMMENT \"Amount added to the count each scan.\"\n");
        table.Append($"    {LimitTag} 7 : Int @ %MW{baseByte + 4} ACCESSIBLE VISIBLE WRITABLE COMMENT \"Count at or above which the ramp stops.\"\n");
        table.Append($"    {CountTag} A : Int @ %MW{baseByte + 6} ACCESSIBLE VISIBLE WRITABLE COMMENT \"Accumulated count.\"\n");
        table.Append($"    {DoneTag} D : Int @ %MW{baseByte + 8} ACCESSIBLE VISIBLE WRITABLE COMMENT \"1 once the count has reached the limit.\"\n");

        var block = new StringBuilder();
        block.Append("BLOCK FC FC_DemoRamp\n");
        block.Append("ROOTID 0\n");
        block.Append($"NUMBER {blockNumber}\n");
        block.Append("LANGUAGE LAD\n");
        block.Append("TITLE \"Ramp a count to a limit\"\n");
        block.Append('\n');
        block.Append("INTERFACE\n");
        block.Append("  INPUT\n");
        block.Append("  OUTPUT\n");
        block.Append("  CONSTANT\n");
        block.Append('\n');
        block.Append("NETWORK 1 \"Hold the count and the done flag at zero while the start command is off\"\n");
        block.Append($"  MOVE(EN := NOT {StartTag}, IN := 0) => {CountTag}\n");
        block.Append($"  MOVE(EN := NOT {StartTag}, IN := 0) => {DoneTag}\n");
        block.Append('\n');
        block.Append("NETWORK 2 \"Add one step to the count each scan until it reaches the limit\"\n");
        block.Append($"  ADD(EN := {accumulate}, IN1 := {CountTag}, IN2 := {StepTag}) => {CountTag}\n");
        block.Append('\n');
        block.Append("NETWORK 3 \"Report done once the count has reached the limit\"\n");
        block.Append($"  MOVE(EN := {StartTag} AND {CountTag} >= {LimitTag}, IN := 1) => {DoneTag}\n");

        return new[]
        {
            new HarnessObject("DemoUnit", HarnessObjectKind.TagTable, table.ToString()),
            new HarnessObject("FC_DemoRamp", HarnessObjectKind.Block, block.ToString()),
        };
    }

    /// <summary>The copy-layer binding for this block: two vector registers in, two result registers out.</summary>
    public static SlotBinding Binding(string slotId = "S0") => new(
        slotId,
        // Every one of these is declared Int in this block's own tag table above, so the binding says
        // Int. It is stated rather than defaulted: a generator that assumed Int is what put an
        // unmirrorable copy layer on a controller.
        MirroredSignal.Ints(StepTag, LimitTag),
        StartTag,
        // *** THE SPEC NAME IS STATED EVEN THOUGH IT EQUALS THE TAG. *** This block's specification and
        // its tag table use the same names, and that is now SAID rather than assumed. Absent would mean
        // "nobody stated the join", which is a different fact and is NOT CHECKED - the silent identity is
        // exactly the assumption that failed on 16 of 17 real signals.
        new[]
        {
            new MirroredSignal(CountTag, MirrorValueType.Int, SpecName: CountTag),
            new MirroredSignal(DoneTag, MirrorValueType.Int, SpecName: DoneTag),
        });
}
