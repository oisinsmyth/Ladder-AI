namespace Harness.Cleanup.Tests;

/// <summary>
/// <b>DB-7 rule 1 at the file level.</b> <c>Cleanup.Plan</c> already separates <i>unknown</i> from
/// <i>drained</i>; everything here exists so the two cannot be confused ON DISK, where an empty file
/// and a truncated write are byte-identical at zero length.
/// </summary>
public class DrainReportTests
{
    private const string Complete = "format=1\ncomputed-by=the coordinator\ncomputed-at=2026-08-17T09:00:00Z\nin-flight=0\nend\n";

    [Fact]
    public void A_complete_report_with_no_tests_is_a_POSITIVE_statement_that_the_drain_ran()
    {
        var report = DrainReport.Parse(Complete, "drain.txt");

        Assert.True(report.IsDrained);
        Assert.Equal("the coordinator", report.ComputedBy);
        Assert.Equal("2026-08-17T09:00:00Z", report.ComputedAt);
    }

    [Fact]
    public void A_TRUNCATED_report_is_UNUSABLE_and_never_reads_as_drained()
    {
        // The whole reason the format carries a terminator. Without 'end', a file still being written
        // reads as "nothing in flight" — the one wrong answer that deletes something a running test
        // depends on.
        var truncated = "format=1\ncomputed-by=the coordinator\ncomputed-at=2026-08-17T09:00:00Z\nin-flight=2\ntest=V-1\n";

        var ex = Assert.Throws<CleanupInputException>(() => DrainReport.Parse(truncated, "drain.txt"));
        Assert.Contains("truncated", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_DECLARED_COUNT_that_does_not_match_the_lines_is_refused()
    {
        var short_ = "format=1\ncomputed-by=x\ncomputed-at=t\nin-flight=3\ntest=V-1\nend\n";

        var ex = Assert.Throws<CleanupInputException>(() => DrainReport.Parse(short_, "drain.txt"));
        Assert.Contains("in-flight=3", ex.Message, StringComparison.Ordinal);
        Assert.Contains("1 test line(s)", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_EMPTY_FILE_is_not_a_drained_one()
    {
        // The case the whole format exists for: zero bytes says nothing, and must not say "drained".
        Assert.Throws<CleanupInputException>(() => DrainReport.Parse(string.Empty, "drain.txt"));
    }

    [Fact]
    public void A_report_with_no_DECLARER_is_refused()
    {
        var anonymous = "format=1\ncomputed-at=t\nin-flight=0\nend\n";

        var ex = Assert.Throws<CleanupInputException>(() => DrainReport.Parse(anonymous, "drain.txt"));
        Assert.Contains("computed-by", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_report_with_no_TIMESTAMP_is_refused()
    {
        var undated = "format=1\ncomputed-by=x\nin-flight=0\nend\n";

        var ex = Assert.Throws<CleanupInputException>(() => DrainReport.Parse(undated, "drain.txt"));
        Assert.Contains("computed-at", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_FUTURE_FORMAT_is_refused_rather_than_read_optimistically()
    {
        var newer = "format=2\ncomputed-by=x\ncomputed-at=t\nin-flight=0\nend\n";

        Assert.Throws<CleanupInputException>(() => DrainReport.Parse(newer, "drain.txt"));
    }

    [Fact]
    public void An_UNKNOWN_KEY_is_a_hard_error_and_never_a_skip()
    {
        // The likeliest cause is a newer producer whose extra field changes what the document MEANS.
        var extra = "format=1\ncomputed-by=x\ncomputed-at=t\nin-flight=0\nexcluding=slot-3\nend\n";

        var ex = Assert.Throws<CleanupInputException>(() => DrainReport.Parse(extra, "drain.txt"));
        Assert.Contains("excluding", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Tests_in_flight_are_carried_through_by_name()
    {
        var busy = "format=1\ncomputed-by=x\ncomputed-at=t\nin-flight=2\ntest=V-7\ntest=V-8\nend\n";

        var report = DrainReport.Parse(busy, "drain.txt");

        Assert.False(report.IsDrained);
        Assert.Equal(new[] { "V-7", "V-8" }, report.TestsInFlight);
    }

    [Fact]
    public void Comments_and_blank_lines_are_content_free_and_do_not_change_the_count()
    {
        var commented = "# who ran this\nformat=1\n\ncomputed-by=x\ncomputed-at=t\nin-flight=1\ntest=V-1\nend\n";

        Assert.Single(DrainReport.Parse(commented, "drain.txt").TestsInFlight);
    }

    [Fact]
    public void Content_AFTER_the_terminator_is_refused()
    {
        // An 'end' in the middle would let a truncation be masked by whatever followed it.
        var trailing = "format=1\ncomputed-by=x\ncomputed-at=t\nin-flight=0\nend\ntest=V-9\n";

        Assert.Throws<CleanupInputException>(() => DrainReport.Parse(trailing, "drain.txt"));
    }
}
