using System.Text;
using Harness.Map;

namespace Harness.Skeleton;

/// <summary>How the second block is coupled to the first, for build-plan item 3.2.</summary>
public enum PeakBlockCoupling
{
    /// <summary>Touches only its own tags. Shares no reachable state with the ramp block at all.</summary>
    None,

    /// <summary>
    /// <b>Writes into the ramp block's accumulator.</b> Overlapping reachable state — the first edge
    /// DB-13's conflict graph is defined by, and the multi-writer fact <c>converter cross-check</c>
    /// reports as C-308.
    ///
    /// <para><b>Gated on this block's OWN start condition, which is the load-bearing detail.</b> Every
    /// block is CALLED every scan, including throughout inert (D37) — so a coupling that fired whenever
    /// the block executed would corrupt the ramp's SOLO run too, and a differential cannot see a fault
    /// that is present in both of its arms. Gating on the start condition makes the coupling exist
    /// <b>only when both slots are active</b>, which is the thing A5 actually asks about.</para>
    /// </summary>
    WritesTheRampsAccumulator,
}

/// <summary>
/// The SECOND block under test (build-plan item 3.1) — a different block, not a second copy of the first.
///
/// <para><b>What it does.</b> While its start command is off it holds its peak and its alarm at zero.
/// While the start command is on it holds the highest level it has seen, and raises the alarm once that
/// held peak reaches the trip point. Two vector registers in (level, trip), two result registers out
/// (peak, alarm).</para>
///
/// <para><b>Why a different shape rather than a second ramp.</b> Two instances of one block would share a
/// failure mode, and a wave set of two identical slots cannot show that the map's per-slot addressing is
/// doing anything — every register would hold the same value at the same time and an aliasing bug would
/// be invisible. A peak-hold has different arithmetic, a different completion signal and a different
/// scan profile (it settles in one scan where the ramp takes several), so a divergence between the two
/// is visible rather than symmetric.</para>
/// </summary>
public static class PeakBlock
{
    public const string StartTag = "Demo2_Start";
    public const string LevelTag = "Demo2_Level";
    public const string TripTag = "Demo2_Trip";
    public const string PeakTag = "Demo2_Peak";
    public const string AlarmTag = "Demo2_Alarm";

    /// <summary>Result register index carrying the held peak.</summary>
    public const int PeakRegister = 0;

    /// <summary>Result register index carrying the alarm — the completion signal the client polls.</summary>
    public const int AlarmRegister = 1;

    /// <summary>Generate the block and its tag table.</summary>
    public static IReadOnlyList<HarnessObject> Generate(int baseByte, int blockNumber, PeakBlockCoupling coupling = PeakBlockCoupling.None)
    {
        if (baseByte < 0 || baseByte % 2 != 0)
            throw new ArgumentOutOfRangeException(nameof(baseByte), baseByte, "the region must start on an even byte; a word tag on an odd byte straddles two words.");

        if (blockNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(blockNumber), blockNumber, "block numbers come from the caller's reserved range, never from a generator (hard rule 3).");

        var table = new StringBuilder();
        table.Append("TAGTABLE DemoUnit2\n");
        table.Append("  ROOTID 0\n");
        table.Append("  TAGS\n");
        table.Append($"    {StartTag} 1 : Bool @ %M{baseByte}.0 ACCESSIBLE VISIBLE WRITABLE COMMENT \"Start command. The block runs only while this is on.\"\n");
        table.Append($"    {LevelTag} 4 : Int @ %MW{baseByte + 2} ACCESSIBLE VISIBLE WRITABLE COMMENT \"Level presented to the block.\"\n");
        table.Append($"    {TripTag} 7 : Int @ %MW{baseByte + 4} ACCESSIBLE VISIBLE WRITABLE COMMENT \"Peak at or above which the alarm is raised.\"\n");
        table.Append($"    {PeakTag} A : Int @ %MW{baseByte + 6} ACCESSIBLE VISIBLE WRITABLE COMMENT \"Highest level seen since the start command came on.\"\n");
        table.Append($"    {AlarmTag} D : Int @ %MW{baseByte + 8} ACCESSIBLE VISIBLE WRITABLE COMMENT \"1 once the held peak has reached the trip point.\"\n");

        var name = coupling == PeakBlockCoupling.None ? "FC_DemoPeak" : "FC_DemoPeakCoupled";

        var block = new StringBuilder();
        block.Append($"BLOCK FC {name}\n");
        block.Append("ROOTID 0\n");
        block.Append($"NUMBER {blockNumber}\n");
        block.Append("LANGUAGE LAD\n");
        block.Append("TITLE \"Hold a peak level and alarm on a trip point\"\n");
        block.Append('\n');
        block.Append("INTERFACE\n");
        block.Append("  INPUT\n");
        block.Append("  OUTPUT\n");
        block.Append("  CONSTANT\n");
        block.Append('\n');
        block.Append("NETWORK 1 \"Hold the peak and the alarm at zero while the start command is off\"\n");
        block.Append($"  MOVE(EN := NOT {StartTag}, IN := 0) => {PeakTag}\n");
        block.Append($"  MOVE(EN := NOT {StartTag}, IN := 0) => {AlarmTag}\n");
        block.Append('\n');
        block.Append("NETWORK 2 \"Hold the highest level seen since the start command came on\"\n");
        block.Append($"  MOVE(EN := {StartTag} AND {LevelTag} > {PeakTag}, IN := {LevelTag}) => {PeakTag}\n");

        var number = 3;

        if (coupling == PeakBlockCoupling.WritesTheRampsAccumulator)
        {
            // Deliberately ONE-SHOT — gated on this block's own alarm being still clear, which is true for
            // exactly the first scan after its start condition rises. A continuous coupling would make the
            // ramp's published count depend on WHEN it was polled, which is a real and nastier shape (the
            // spec calls intermittent interference worse than constant), but it would make the
            // demonstration's numbers depend on the poll rate rather than on the coupling.
            block.Append('\n');
            block.Append($"NETWORK {number++} \"Add the level into the ramp block's accumulator\"\n");
            block.Append($"  ADD(EN := {StartTag} AND {AlarmTag} = 0, IN1 := {TrivialBlock.CountTag}, IN2 := {LevelTag}) => {TrivialBlock.CountTag}\n");
        }

        block.Append('\n');
        block.Append($"NETWORK {number} \"Raise the alarm once the held peak reaches the trip point\"\n");
        block.Append($"  MOVE(EN := {StartTag} AND {PeakTag} >= {TripTag}, IN := 1) => {AlarmTag}\n");

        return new[]
        {
            new HarnessObject("DemoUnit2", HarnessObjectKind.TagTable, table.ToString()),
            new HarnessObject(name, HarnessObjectKind.Block, block.ToString()),
        };
    }

    /// <summary>The copy-layer binding for this block.</summary>
    public static SlotBinding Binding(string slotId = "S1") => new(
        slotId,
        new[] { LevelTag, TripTag },
        StartTag,
        new[] { PeakTag, AlarmTag });
}

/// <summary>What the model says the peak block should produce.</summary>
public sealed record PeakBlockPrediction(int Peak, int Alarm, int Scans);

/// <summary>How one observation compared with the model.</summary>
public sealed record PeakBlockVerdict(bool Held, PeakBlockPrediction Predicted, int ObservedPeak, int ObservedAlarm, string Detail);

/// <summary>
/// The second block's model — written, like the first, from the block's SPECIFICATION.
///
/// <para>It never mentions a rung, an operator or an address, and in particular it knows nothing about
/// the ramp block, which is what makes it able to report the coupled build as wrong.</para>
/// </summary>
public static class PeakBlockModel
{
    /// <summary>Predict the block's settled outputs for one vector.</summary>
    public static PeakBlockPrediction Predict(int level, int trip)
    {
        if (level <= 0)
            throw new ArgumentOutOfRangeException(nameof(level), level, "a level of zero or less never exceeds a peak that starts at zero; this vector moves nothing and has no settled outcome to predict.");

        if (trip <= 0)
            throw new ArgumentOutOfRangeException(nameof(trip), trip, "a trip at or below zero is already reached at rest, which makes the vector's intent ambiguous rather than trivial.");

        return new PeakBlockPrediction(level, level >= trip ? 1 : 0, Scans: 1);
    }

    /// <summary>Compare one observation of the result registers with the prediction.</summary>
    public static PeakBlockVerdict Judge(int level, int trip, ushort[] results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var predicted = Predict(level, trip);

        if (results.Length <= PeakBlock.AlarmRegister)
        {
            return new PeakBlockVerdict(false, predicted, 0, 0,
                $"the slot published {results.Length} result register(s); the model needs {PeakBlock.AlarmRegister + 1}. An unread register is not a passing one.");
        }

        var peak = unchecked((short)results[PeakBlock.PeakRegister]);
        var alarm = unchecked((short)results[PeakBlock.AlarmRegister]);

        var problems = new List<string>();
        if (peak != predicted.Peak)
            problems.Add($"peak is {peak}, the model predicts {predicted.Peak}");
        if (alarm != predicted.Alarm)
            problems.Add($"alarm is {alarm}, the model predicts {predicted.Alarm}");

        return problems.Count == 0
            ? new PeakBlockVerdict(true, predicted, peak, alarm, $"level {level} against trip {trip}: peak {peak}, alarm {alarm}, as predicted.")
            : new PeakBlockVerdict(false, predicted, peak, alarm, $"level {level} against trip {trip}: " + string.Join("; ", problems) + ".");
    }
}
