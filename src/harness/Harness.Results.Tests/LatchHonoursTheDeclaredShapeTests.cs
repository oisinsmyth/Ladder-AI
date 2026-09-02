using Harness.Map;
using Harness.Results;

namespace Harness.Results.Tests;

/// <summary>
/// 🔴 <b>A <c>Latched</c> EXPECTATION DECLARED <c>AtNoPoint</c> WAS JUDGED EXACTLY BACKWARDS.</b>
///
/// <para><b>MEASURED 2026-09-02, on a real bench-rig wave, and found by a blind reviewer.</b>
/// <see cref="SeriesEvaluation.FromLatch"/> took no <see cref="TemporalShape"/>. It compared
/// <c>latchValue == expected</c> and built its <see cref="ObservationWindow"/> without setting
/// <see cref="ObservationWindow.Shape"/>. But on <see cref="TemporalShape.AtNoPoint"/> the declared value
/// is the <b>FORBIDDEN</b> one — the series path has always answered that shape before every other
/// branch, precisely because "every verdict below would read exactly backwards".</para>
///
/// <para>So a latch that had read the forbidden value was reported <b>Held</b>, and a latch that never saw
/// it was reported <b>Disagreed</b>. On the wave that found this, two rows failed whose latches had read
/// exactly what their assertion required — and one of them was the strongest positive result in the run,
/// a motor start output whose seal-in correctly broke when the request dropped. The reviewer's verdict:
/// <i>"the vector is right, the block is right, the evaluator is wrong."</i></para>
///
/// <para><b>WHY NOBODY SAW IT IN THE OUTPUT.</b> The dropped shape also meant every <c>Latched</c> row
/// printed <c>temporalShape: "Unstated"</c> in the result file whatever its vector declared, so the
/// evidence that the shape had been discarded was itself discarded. The window now carries the shape as
/// well as obeying it.</para>
///
/// <para><b>AND IT IS INVISIBLE ON THE OTHER COMMON SHAPE.</b> On <c>AtSomePoint</c> the equality fold and
/// the shape's own reading coincide, so the defect produces a correct verdict there. That is why it
/// survived: the only rows that expose it are <c>Latched</c> + <c>AtNoPoint</c>, and the first vectors to
/// carry that combination ran for the first time on the day it was found.</para>
/// </summary>
public class LatchHonoursTheDeclaredShapeTests
{
    private const string Id = "REQ-055:362174";
    private const string Signal = "IO.Run";
    private static readonly SeriesAccounting Series = new(233, 233, 59, Truncated: false);

    private static AssertionOutcome Latch(string expected, string latchValue, TemporalShape shape) =>
        SeriesEvaluation.FromLatch(Id, Signal, expected, latchValue, 900, Series, LatchSource.Generated, shape);

    // -------------------------------------------------------------------------------------------------
    // AtNoPoint — the inverted fold. `expected` names what must NOT happen.
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 THE HARMFUL DIRECTION, and the exact row the wave got wrong. The forbidden value is
    /// <c>true</c>; the latch read <c>false</c>, so the condition never occurred. That is the PASS.
    /// Red before the fix — it reported <c>Disagreed</c> and a correct block was accused.
    /// </summary>
    [Fact]
    public void AtNoPoint_ALatchThatNeverSawTheForbiddenValue_Holds()
    {
        var outcome = Latch(expected: "true", latchValue: "false", TemporalShape.AtNoPoint);

        Assert.Equal(AssertionState.Held, outcome.State);
    }

    /// <summary>
    /// THE CONVERSE, and without it the fix could be "always hold on AtNoPoint". The forbidden value was
    /// latched, so the condition DID occur inside the window. An occurrence is positive evidence.
    /// </summary>
    [Fact]
    public void AtNoPoint_ALatchThatHeldTheForbiddenValue_Disagrees()
    {
        var outcome = Latch(expected: "true", latchValue: "true", TemporalShape.AtNoPoint);

        Assert.Equal(AssertionState.Disagreed, outcome.State);
    }

    /// <summary>The same inversion with the polarity swapped, so the fix cannot be a hardcoded reading of
    /// the string "true".</summary>
    [Theory]
    [InlineData("false", "true", AssertionState.Held)]
    [InlineData("false", "false", AssertionState.Disagreed)]
    public void AtNoPoint_InvertsRegardlessOfWhichValueIsForbidden(
        string expected, string latchValue, AssertionState state)
    {
        Assert.Equal(state, Latch(expected, latchValue, TemporalShape.AtNoPoint).State);
    }

    // -------------------------------------------------------------------------------------------------
    // EVERY OTHER SHAPE IS UNCHANGED — the controls that keep this fix surgical
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 THE CONTROLS THAT MATTER MOST. Only <c>AtNoPoint</c> was ever wrong, so only <c>AtNoPoint</c> may
    /// change. If the inversion leaked into any other shape it would silently re-judge every latch row in
    /// every wave already run — including results already reported as passing.
    /// </summary>
    [Theory]
    [InlineData(TemporalShape.Unstated)]
    [InlineData(TemporalShape.AtSomePoint)]
    [InlineData(TemporalShape.Throughout)]
    public void EveryOtherShapeKeepsThePlainEqualityFold(TemporalShape shape)
    {
        Assert.Equal(AssertionState.Held, Latch("true", "true", shape).State);
        Assert.Equal(AssertionState.Disagreed, Latch("true", "false", shape).State);
    }

    /// <summary>
    /// The default-argument path is the old call shape, and it must still behave as it always did — the
    /// signature gained an optional parameter so that no existing caller changed meaning by recompiling.
    /// </summary>
    [Fact]
    public void OmittingTheShapeEntirely_IsStillPlainEquality()
    {
        var outcome = SeriesEvaluation.FromLatch(
            Id, Signal, "true", "false", 900, Series, LatchSource.Generated);

        Assert.Equal(AssertionState.Disagreed, outcome.State);
    }

    // -------------------------------------------------------------------------------------------------
    // THE SHAPE IS REPORTED, NOT ONLY OBEYED
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 The dropped shape is how this hid: the result file printed <c>Unstated</c> on rows whose vector
    /// declared otherwise, so the one clue that the fold had been asked the wrong question was erased on
    /// the way out. A reader must be able to see WHICH question was asked.
    /// </summary>
    [Fact]
    public void TheWindowCarriesTheDeclaredShape()
    {
        Assert.Equal(TemporalShape.AtNoPoint, Latch("true", "false", TemporalShape.AtNoPoint).Window!.Shape);
        Assert.Equal(TemporalShape.AtSomePoint, Latch("true", "true", TemporalShape.AtSomePoint).Window!.Shape);
    }

    /// <summary>
    /// A pass on this shape is produced by SEEING NOTHING — here by a single latch bit standing for a whole
    /// window — and the row says so where the verdict is, rather than leaving a reader to infer that a
    /// green means the condition was watched for continuously.
    /// </summary>
    [Fact]
    public void AnAtNoPointPassCarriesItsOwnWeakness()
    {
        var detail = Latch("true", "false", TemporalShape.AtNoPoint).Detail;

        Assert.Contains("FORBIDDEN", detail, StringComparison.Ordinal);
        Assert.Contains("SEEING NOTHING", detail, StringComparison.Ordinal);
    }
}
