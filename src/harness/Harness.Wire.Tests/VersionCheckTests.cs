using Harness.Map;
using Harness.Wire;

namespace Harness.Wire.Tests;

/// <summary>
/// Build-plan item 2.6's client half — the post-download check, and the four ways it must NOT read as a
/// confirmation.
/// </summary>
public class VersionCheckTests
{
    private static readonly BuildStamp Stamp = MirrorClientTests.Stamp;

    private static (MirrorClient Client, RecordingTransport Wire) Wired(uint? published = null)
    {
        var map = MirrorClientTests.Map();
        var wire = new RecordingTransport(map);
        if (published is { } value)
            wire.SetVersion(value);

        return (new MirrorClient(map, wire, Stamp), wire);
    }

    [Fact]
    public void A_stable_matching_register_confirms_and_reports_the_window_it_measured()
    {
        var (client, _) = Wired(Stamp.Value);

        var report = VersionCheck.Confirm(client, Stamp);

        Assert.True(report.Confirmed, report.Detail);
        Assert.Equal(3, report.Reads);
        Assert.Equal(1, report.ReadsToSettle);
    }

    [Fact]
    public void A_register_that_reads_zero_says_the_copy_layer_is_not_running_at_all()
    {
        // Zero is what bit memory reads before anything writes it, which is why a zero build stamp is
        // refused at generation: it would "confirm" against a CPU that never ran the copy layer.
        var (client, _) = Wired();

        var report = VersionCheck.Confirm(client, Stamp);

        Assert.Equal(VersionOutcome.Absent, report.Outcome);
        Assert.Contains("not running", report.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_register_holding_a_different_stamp_is_stale_which_is_the_aborted_download_case()
    {
        var (client, _) = Wired(0x0BADF00D);

        var report = VersionCheck.Confirm(client, Stamp);

        Assert.Equal(VersionOutcome.Stale, report.Outcome);
        Assert.Contains("DIFFERENT build", report.Detail, StringComparison.Ordinal);
        Assert.Contains("excision", report.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_register_holding_the_stamp_with_its_halves_swapped_names_the_uncalibrated_word_order()
    {
        // The residual §11 records: MB_SERVER's byte-to-register presentation is Siemens' behaviour and
        // no fake we write can validate it. Reporting this as a failed download would send someone
        // looking at TIA for a whole afternoon.
        var (client, _) = Wired(RegisterWords.Swapped(Stamp.Value));

        var report = VersionCheck.Confirm(client, Stamp);

        Assert.Equal(VersionOutcome.WordOrderSuspect, report.Outcome);
        Assert.Contains("swapped", report.Detail, StringComparison.Ordinal);
        Assert.Contains("Calibrate", report.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_register_still_flapping_is_unsettled_and_that_is_not_a_confirmation()
    {
        // §9: the value "flaps or reads old until integration completes". A single read during that
        // window returns the OLD value and looks exactly like a failed download.
        var (client, wire) = Wired(Stamp.Value);
        var reads = 0;
        wire.OnTransaction = t => t.SetVersion(reads++ % 2 == 0 ? Stamp.Value : 0u);

        var report = VersionCheck.Confirm(client, Stamp, stableReads: 3, maxReads: 12);

        Assert.Equal(VersionOutcome.Unsettled, report.Outcome);
        Assert.Equal(12, report.Reads);
    }

    [Fact]
    public void A_register_that_settles_late_is_confirmed_and_the_settling_window_is_reported()
    {
        var (client, wire) = Wired();
        var reads = 0;
        wire.OnTransaction = t =>
        {
            reads++;
            t.SetVersion(reads < 4 ? (uint)reads : Stamp.Value);
        };

        var report = VersionCheck.Confirm(client, Stamp);

        Assert.True(report.Confirmed, report.Detail);
        Assert.Equal(6, report.Reads);
        Assert.Equal(4, report.ReadsToSettle);
    }

    [Fact]
    public void One_read_is_a_sample_not_stability()
    {
        var (client, _) = Wired(Stamp.Value);

        Assert.Throws<ArgumentOutOfRangeException>(() => VersionCheck.Confirm(client, Stamp, stableReads: 1));
    }

    [Fact]
    public void The_check_reads_the_register_WITHOUT_the_clients_own_version_guard()
    {
        // If it used the guarded read, every mismatch would arrive as an exception and this check could
        // never report WHICH mismatch it was — the whole point of having four outcomes.
        var (client, _) = Wired(0x0BADF00D);

        var report = VersionCheck.Confirm(client, Stamp);

        Assert.Equal(VersionOutcome.Stale, report.Outcome);
        Assert.Throws<WireException>(() => client.ReadControl());
    }
}
