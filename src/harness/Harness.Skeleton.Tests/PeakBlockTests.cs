using Harness.Map;
using Harness.Skeleton;

namespace Harness.Skeleton.Tests;

/// <summary>
/// The second block under test (3.1) and its model.
///
/// <para>The IR asserted here was round-tripped through <c>converter to-xml</c> / <c>to-ir
/// --no-sidecar</c> and came back byte-identical, in both the disjoint and the coupled build.</para>
/// </summary>
public class PeakBlockTests
{
    private static string Block(PeakBlockCoupling coupling = PeakBlockCoupling.None) =>
        PeakBlock.Generate(3100, 902, coupling).Single(o => o.Kind == HarnessObjectKind.Block).Ir;

    [Fact]
    public void The_disjoint_block_is_the_IR_the_converter_round_trips()
    {
        Assert.Equal(
            """
            BLOCK FC FC_DemoPeak
            ROOTID 0
            NUMBER 902
            LANGUAGE LAD
            TITLE "Hold a peak level and alarm on a trip point"

            INTERFACE
              INPUT
              OUTPUT
              CONSTANT

            NETWORK 1 "Hold the peak and the alarm at zero while the start command is off"
              MOVE(EN := NOT Demo2_Start, IN := 0) => Demo2_Peak
              MOVE(EN := NOT Demo2_Start, IN := 0) => Demo2_Alarm

            NETWORK 2 "Hold the highest level seen since the start command came on"
              MOVE(EN := Demo2_Start AND Demo2_Level > Demo2_Peak, IN := Demo2_Level) => Demo2_Peak

            NETWORK 3 "Raise the alarm once the held peak reaches the trip point"
              MOVE(EN := Demo2_Start AND Demo2_Peak >= Demo2_Trip, IN := 1) => Demo2_Alarm

            """.ReplaceLineEndings("\n"),
            Block());
    }

    [Fact]
    public void The_coupled_block_is_the_same_IR_plus_ONE_network()
    {
        Assert.Equal(
            """
            BLOCK FC FC_DemoPeakCoupled
            ROOTID 0
            NUMBER 902
            LANGUAGE LAD
            TITLE "Hold a peak level and alarm on a trip point"

            INTERFACE
              INPUT
              OUTPUT
              CONSTANT

            NETWORK 1 "Hold the peak and the alarm at zero while the start command is off"
              MOVE(EN := NOT Demo2_Start, IN := 0) => Demo2_Peak
              MOVE(EN := NOT Demo2_Start, IN := 0) => Demo2_Alarm

            NETWORK 2 "Hold the highest level seen since the start command came on"
              MOVE(EN := Demo2_Start AND Demo2_Level > Demo2_Peak, IN := Demo2_Level) => Demo2_Peak

            NETWORK 3 "Add the level into the ramp block's accumulator"
              ADD(EN := Demo2_Start AND Demo2_Alarm = 0, IN1 := Demo_Count, IN2 := Demo2_Level) => Demo_Count

            NETWORK 4 "Raise the alarm once the held peak reaches the trip point"
              MOVE(EN := Demo2_Start AND Demo2_Peak >= Demo2_Trip, IN := 1) => Demo2_Alarm

            """.ReplaceLineEndings("\n"),
            Block(PeakBlockCoupling.WritesTheRampsAccumulator));
    }

    [Fact]
    public void The_two_blocks_have_different_names_because_they_are_different_downloads()
    {
        // Same name, two contents, is how a build gets confused for another one. The build stamp already
        // separates them; the name makes it visible to a person reading a list of objects.
        Assert.Contains("FC_DemoPeak\n", Block(), StringComparison.Ordinal);
        Assert.Contains("FC_DemoPeakCoupled\n", Block(PeakBlockCoupling.WritesTheRampsAccumulator), StringComparison.Ordinal);
    }

    [Fact]
    public void The_coupling_is_gated_on_the_blocks_OWN_start_condition()
    {
        // The load-bearing detail of 3.2. Every block is CALLED every scan (D37), so a coupling that
        // fired whenever the block executed would corrupt the other slot's SOLO run too — and a
        // differential cannot see a fault present in both of its arms.
        var coupling = Block(PeakBlockCoupling.WritesTheRampsAccumulator)
            .Split('\n')
            .Single(l => l.Contains("=> Demo_Count", StringComparison.Ordinal));

        Assert.Contains($"EN := {PeakBlock.StartTag} AND", coupling, StringComparison.Ordinal);
    }

    [Fact]
    public void The_peak_blocks_tags_are_disjoint_from_the_ramp_blocks()
    {
        // Sharing an address would be interference by construction rather than by logic, and it would
        // make the disjoint pair not disjoint.
        var ramp = TrivialBlock.Generate(3000, 901).Single(o => o.Kind == HarnessObjectKind.TagTable).Ir;
        var peak = PeakBlock.Generate(3100, 902).Single(o => o.Kind == HarnessObjectKind.TagTable).Ir;

        var addresses = (string ir) => ir.Split('\n')
            .Where(l => l.Contains(" @ %", StringComparison.Ordinal))
            .Select(l => l.Split(" @ ")[1].Split(' ')[0])
            .ToArray();

        Assert.Empty(addresses(ramp).Intersect(addresses(peak)));
    }

    // ---------------------------------------------------------------------------------------------
    // The model
    // ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(7, 6, 7, 1)]
    [InlineData(7, 7, 7, 1)]
    [InlineData(3, 9, 3, 0)]
    public void The_model_predicts_the_held_peak_and_the_alarm(int level, int trip, int peak, int alarm)
    {
        var prediction = PeakBlockModel.Predict(level, trip);

        Assert.Equal(peak, prediction.Peak);
        Assert.Equal(alarm, prediction.Alarm);
        Assert.Equal(1, prediction.Scans);
    }

    [Fact]
    public void A_vector_with_no_settled_outcome_is_refused_rather_than_predicted()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PeakBlockModel.Predict(0, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => PeakBlockModel.Predict(5, 0));
    }

    [Fact]
    public void A_slot_that_published_too_few_registers_is_judged_FAILED_not_passed_over()
    {
        var verdict = PeakBlockModel.Judge(7, 6, new ushort[] { 7 });

        Assert.False(verdict.Held);
        Assert.Contains("An unread register is not a passing one", verdict.Detail, StringComparison.Ordinal);
    }
}
