using Harness.S7;

namespace Harness.S7.Tests;

/// <summary>
/// The run-state decoder, and the one thing it must never be "fixed" into doing.
///
/// <para>The rig was measured in both CPU states on 2026-08-12: RUN answers PDU byte 0x08, STOP
/// answers <b>0x03</b>. 0x03 is not one of Sharp7's three named constants, so it reaches the decoder
/// as 4 through Sharp7's catch-all arm and <c>S7CpuStatusStop</c> is never actually returned by this
/// device. A decoder tightened to accept only {0,4,8} would therefore report a stopped CPU as
/// not-stopped. These tests are here to make that change fail.</para>
/// </summary>
public class S7RunStateTests
{
    // ---- the regression guard -------------------------------------------------------------------

    [Theory]
    [InlineData(4)]   // what Sharp7 hands on for this rig's STOP, and for anything it does not know
    [InlineData(3)]   // the rig's ACTUAL byte in STOP, in case a future path ever reaches here undecoded
    [InlineData(0)]   // S7CpuStatusUnknown: the CPU answered, and the answer was not RUN
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(9)]
    [InlineData(0x10)]
    [InlineData(255)]
    public void Every_stop_class_and_unrecognised_value_is_not_running(int value)
    {
        var reading = S7RunStateReading.Decode(value);

        Assert.Equal(S7RunState.NotRunning, reading.State);
        Assert.False(reading.Running);
    }

    [Fact]
    public void No_value_other_than_the_run_value_maps_to_running()
    {
        // The whole space a byte can occupy, plus the sentinel. Only one of them may be Running: a
        // decoder that rejected unfamiliar values instead of treating them as not-running would report
        // this rig's stopped CPU as not-stopped.
        for (var value = -1; value <= 255; value++)
        {
            var expected = value == S7RunStateReading.RunValue ? S7RunState.Running : S7RunState.NotRunning;
            Assert.Equal(expected, S7RunStateReading.Decode(value).State);
        }
    }

    [Fact]
    public void The_run_value_is_the_one_sharp7_passes_through_unmapped()
    {
        Assert.Equal(8, S7RunStateReading.RunValue);

        var reading = S7RunStateReading.Decode(S7RunStateReading.RunValue);
        Assert.Equal(S7RunState.Running, reading.State);
        Assert.True(reading.Running);
    }

    [Fact]
    public void The_state_type_does_not_claim_a_specific_non_running_mode()
    {
        // A design point, not a naming preference: the read establishes "not RUN" and cannot establish
        // WHICH non-running mode, so a member asserting one would be an overclaim. Pinned here because
        // it is exactly the kind of thing a later tidy-up adds back.
        Assert.Equal(
            new[] { "Unknown", "Running", "NotRunning" }.OrderBy(n => n),
            Enum.GetNames<S7RunState>().OrderBy(n => n));
    }

    // ---- the raw value, kept for diagnostics ----------------------------------------------------

    [Theory]
    [InlineData(8)]
    [InlineData(4)]
    [InlineData(0)]
    [InlineData(99)]
    public void A_reading_carries_the_value_it_decoded(int value)
    {
        Assert.Equal(value, S7RunStateReading.Decode(value).Sharp7Value);
    }

    [Fact]
    public void A_read_that_produced_nothing_says_so_rather_than_reporting_a_value()
    {
        Assert.Equal(S7RunState.Unknown, S7RunStateReading.Unread.State);
        Assert.Equal(S7RunStateReading.NotRead, S7RunStateReading.Unread.Sharp7Value);
        Assert.False(S7RunStateReading.Unread.Running);
    }

    // ---- through the client interface -----------------------------------------------------------

    [Fact]
    public void A_running_cpu_reads_as_running()
    {
        var client = new FakeS7Client { RunStateValue = S7RunStateReading.RunValue };

        var status = client.ReadRunState(out var reading);

        Assert.True(status.Ok);
        Assert.Equal(S7RunState.Running, reading.State);
        Assert.Equal(1, client.RunStateReadCount);
    }

    [Fact]
    public void A_stopped_cpu_reads_as_not_running()
    {
        // 4 is what Sharp7 produces for this rig's 0x03.
        var client = new FakeS7Client { RunStateValue = 4 };

        var status = client.ReadRunState(out var reading);

        Assert.True(status.Ok);
        Assert.Equal(S7RunState.NotRunning, reading.State);
        Assert.Equal(4, reading.Sharp7Value);
    }

    [Fact]
    public void A_failed_read_is_unknown_and_never_not_running()
    {
        // The point of the whole feature is telling "the CPU is not running" from "the CPU could not be
        // asked". If a comms failure decayed into NotRunning, a dropped link would read as a stopped
        // CPU and the ambiguity this read removes would be back.
        var client = new FakeS7Client { RunStateFailureCode = -7 };

        var status = client.ReadRunState(out var reading);

        Assert.False(status.Ok);
        Assert.Equal(-7, status.Code);
        Assert.Equal(S7RunState.Unknown, reading.State);
        Assert.NotEqual(S7RunState.NotRunning, reading.State);
        Assert.False(reading.Running);
        Assert.Equal(S7RunStateReading.NotRead, reading.Sharp7Value);
    }

    [Fact]
    public void A_reading_describes_itself_with_the_value_behind_it()
    {
        Assert.Contains("Running", S7RunStateReading.Decode(8).ToString());
        Assert.Contains("4", S7RunStateReading.Decode(4).ToString());
        Assert.Contains("not read", S7RunStateReading.Unread.ToString());
    }
}
