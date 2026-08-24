using Harness.Map;

namespace Harness.Map.Tests;

/// <summary>
/// 🔴 <b>THE BLOCK UNDER TEST IS PART OF WHAT EXECUTES, SO IT IS PART OF THE STAMP — and it was outside
/// it.</b>
///
/// <para><c>docs/18-project-workbench.md</c>, <b>§5 "Phase 10 — Wave time"</b> — the finding that the
/// subject <i>"appears only as its instance DB"</i>. A deployed program set carried the unit's instance DB
/// and <b>no block</b>. Two result packages describing materially different programs therefore carry the
/// SAME stamp, and the verifying gateway — whose entire job is to refuse a program that is not the one
/// described — would not notice.</para>
///
/// <para>⚠️ <b>Cited by section and phrase, never by line.</b> That document grew by roughly 300 lines on
/// 2026-08-24 alone and every line-only citation into it moved; the parameter-DB half of the same original
/// bullet has since been RETRACTED there (the deploy added it 2 h 32 min later the same day), while the
/// block-under-test half stands. Do not repeat the old "eight objects" figure — the deployed set was nine,
/// and it still had no block in it.</para>
///
/// <para><b>This is set membership, not a mechanism.</b> <c>BuildStamp.Derive</c> appends
/// <c>obj={Kind}:{Name}</c> followed by the object's IR with no per-kind branch and no parser, and
/// <c>ProgramUnderTest.Classify</c> already accepts every top-level IR form. Nothing had to be taught how
/// to hash a block; the block simply was not in the list. <b>So the whole proof is that the stamp MOVES
/// when it enters</b>, and these tests are that proof.</para>
///
/// <para>⚠️ <b>And the <c>ir-hash</c> limitation does not reach here.</b> <c>converter ir-hash</c> keys a
/// parsed block EXPLANATION and refuses content not starting <c>BLOCK </c>
/// (<c>src/converter/Converter/IrHash/IrHashRunner.cs:48-51</c>); <see cref="BuildStamp"/> hashes the UTF-8
/// BYTES of whatever text it is handed. They are different tools in different solutions and share no code
/// — asserted below over a DB and a UDT rather than asserted in prose.</para>
/// </summary>
public class BlockUnderTestEntersTheStampTests
{
    private static readonly CopyLayerNaming Naming =
        new(BlockName: "FC_HarnessCopyLayer", BlockNumber: 9001, TagTableName: "HarnessMirror");

    private static RegisterMap OneSlot() =>
        MapAllocator.Allocate(new WaveSetRequest(
            MirrorGeometry.ForCpu1214C(retentiveBytes: 256, baseByte: 4000, declaredRegisters: (MirrorGeometry.Cpu1214CBitMemoryBytes - 4000) / 2),
            new[] { new SlotRequest("S0", 3, 2) })).Require();

    private static SlotBinding Binding() => new(
        "S0",
        MirroredSignal.Ints("DB_Unit.Setpoint", "DB_Unit.Mode"),
        "DB_Unit.StartCmd",
        MirroredSignal.Ints("DB_Unit.Actual", "DB_Unit.State"));

    /// <summary>The instance DB — the ONLY trace of the block under test the last wave's stamp carried.</summary>
    private static HarnessObject InstanceDb() => new(
        "DB_UnitInstance",
        HarnessObjectKind.DataBlock,
        "DB DB_UnitInstance\n  Setpoint : Int := 0\n  Actual : Int := 0\n");

    /// <summary>The block itself: the executable logic the stamp is supposed to be a claim about.</summary>
    private static HarnessObject BlockUnderTest(string body = "  COIL DB_UnitInstance.Actual := DB_Unit.StartCmd\n") => new(
        "FB_Unit",
        HarnessObjectKind.Block,
        "BLOCK FB FB_Unit\nNETWORK 1 \"drive\"\n" + body);

    private static uint StampOver(params HarnessObject[] program) =>
        BuildStamp.Of(OneSlot(), new[] { Binding() }, Naming, program).Value;

    // ---------------------------------------------------------------------------------------------
    // THE HEADLINE. Before and after, on the same lane.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>THE WHOLE PROOF THAT TRACK 3 DID SOMETHING.</b> The lane's binding, naming, map and instance
    /// DB are byte-identical across the two; the only difference is that the second set contains the block
    /// being tested. If these two stamps were equal, adding the block to the program set would be a
    /// no-op — and the gap docs/18 records would still be open with a manifest on top of it.
    ///
    /// <para><b>The two values on this fixture, recorded rather than described:</b>
    /// <c>16#F6276CA9</c> with the instance DB alone, <c>16#F037F18D</c> once <c>FB_Unit</c> is in the set.
    /// They are pinned in the assertions below so a change to the canonical form has to be deliberate.</para>
    /// </summary>
    [Fact]
    public void The_stamp_changes_when_the_block_under_test_enters_the_set()
    {
        var withoutIt = StampOver(InstanceDb());
        var withIt = StampOver(InstanceDb(), BlockUnderTest());

        Assert.NotEqual(withoutIt, withIt);

        // 🔴 THE LITERAL VALUES, and they are the point rather than decoration. `NotEqual` alone is
        // satisfied by a derivation that has begun returning noise; naming both sides makes the claim
        // "these two specific programs stamp to these two specific things", which is what a person
        // reading a version register off a controller is actually holding.
        Assert.Equal(0xF6276CA9u, withoutIt);
        Assert.Equal(0xF037F18Du, withIt);
    }

    /// <summary>
    /// THE CONTROL, and it is not optional. A stamp that moved on every re-derivation would satisfy the
    /// test above while proving nothing at all — the version register would then refuse every healthy
    /// device, which is the exact false-<c>Stale</c> failure the self-reference exclusion was added to fix.
    /// </summary>
    [Fact]
    public void The_same_set_stamps_the_same_twice()
    {
        Assert.Equal(
            StampOver(InstanceDb(), BlockUnderTest()),
            StampOver(InstanceDb(), BlockUnderTest()));
    }

    /// <summary>
    /// And the point of putting it in: <b>editing the block under test now moves the stamp.</b> Before it
    /// was in the set, the two programs below were indistinguishable to the verifying gateway.
    /// </summary>
    [Fact]
    public void Editing_the_block_under_test_moves_the_stamp_once_it_is_in_the_set()
    {
        Assert.NotEqual(
            StampOver(InstanceDb(), BlockUnderTest("  COIL DB_UnitInstance.Actual := DB_Unit.StartCmd\n")),
            StampOver(InstanceDb(), BlockUnderTest("  COIL DB_UnitInstance.Actual := NOT DB_Unit.StartCmd\n")));
    }

    /// <summary>
    /// The negative half of the same claim, kept because it is what was actually happening: with only the
    /// instance DB in the set, <b>two materially different programs carry ONE stamp</b>. Reverting the set
    /// membership reddens the test above and greens this one — they are the same fact from two sides.
    /// </summary>
    [Fact]
    public void With_only_the_instance_DB_two_different_programs_carry_one_stamp()
    {
        Assert.Equal(
            StampOver(InstanceDb()),
            StampOver(InstanceDb()));

        // Stated explicitly: the difference that is invisible is a difference in EXECUTABLE LOGIC.
        Assert.NotEqual(
            BlockUnderTest("  COIL A := B\n").Ir,
            BlockUnderTest("  COIL A := NOT B\n").Ir);
    }

    // ---------------------------------------------------------------------------------------------
    // The kinds. No branch, no parser — asserted rather than assumed.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>The stamp hashes BYTES, so a DB and a UDT move it exactly as a block does.</b> This is the
    /// claim that <c>ir-hash</c>'s "block files only" refusal does not reach the build stamp: were the two
    /// sharing anything, a DB in the program set would be refused or ignored rather than hashed.
    /// </summary>
    [Theory]
    [InlineData(HarnessObjectKind.DataBlock, "DB DB_Params\n  Preset : Time := T#30s\n", "DB DB_Params\n  Preset : Time := T#7s\n")]
    [InlineData(HarnessObjectKind.DataType, "TYPE UDT_Io\n  Run : Bool\n", "TYPE UDT_Io\n  Run : Bool\n  Fault : Bool\n")]
    [InlineData(HarnessObjectKind.TagTable, "TAGTABLE Plant tags\n  Go 1 : Bool @ %I0.0\n", "TAGTABLE Plant tags\n  Go 1 : Bool @ %I0.1\n")]
    public void Every_kind_the_loader_accepts_moves_the_stamp_when_its_text_changes(
        HarnessObjectKind kind, string before, string after)
    {
        Assert.NotEqual(
            StampOver(InstanceDb(), new HarnessObject("X", kind, before)),
            StampOver(InstanceDb(), new HarnessObject("X", kind, after)));
    }

    /// <summary>
    /// The parameter DB, named separately because it is the OTHER half of the same docs/18 finding —
    /// <i>"compressing them changes the controller without changing the stamp"</i>. Same mechanism, same
    /// fix, and it must be a real difference rather than a restatement of the block case.
    /// </summary>
    [Fact]
    public void The_parameter_DB_entering_the_set_moves_the_stamp_too()
    {
        var parameters = new HarnessObject("DB_Params", HarnessObjectKind.DataBlock, "DB DB_Params\n  FillTime : Time := T#30s\n");

        Assert.NotEqual(
            StampOver(InstanceDb(), BlockUnderTest()),
            StampOver(InstanceDb(), BlockUnderTest(), parameters));
    }
}
