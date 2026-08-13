using System.Text.Json;
using Ladder.Download;

namespace Harness.Device.Tests;

/// <summary>
/// Recovering the LOAD MANIFEST from what <c>download-probe --json</c> actually prints.
///
/// <para><b>The inputs are recorded logs from real rig downloads</b>, linked from
/// <c>src/download-feedback</c>'s own fixture set — a 99-object full load, a 1-object differential,
/// an up-to-date run that transferred nothing, and an aborted run with no <c>DownloadResult</c> at
/// all. They are the only authority in this assembly's test suite that this lane did not write, and
/// each cost a live rig, a CPU stop and a restart.</para>
///
/// <para>What is still substituted is the JSON envelope, because the probe's own <c>--json</c> shape
/// is built in <c>DownloadProbe/Program.BuildJson</c> and nothing in this repo has a recorded copy of
/// one. That envelope is asserted key by key against those literals rather than invented.</para>
/// </summary>
public class ProbeReportTests
{
    private static string ReadFixture(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

        Assert.True(File.Exists(path),
            $"the recorded probe log '{name}' was not copied to the output directory. It is the only independent evidence this test has, so a missing one is a failure, not a skip.");

        return File.ReadAllText(path);
    }

    /// <summary>
    /// The probe's own JSON envelope, built from the keys <c>Program.BuildJson</c> writes: the log is
    /// EMBEDDED as an array of lines rather than merely referenced.
    /// </summary>
    private static string ProbeJson(string logText, string? transferVerdict = "Transferred", bool? softwareLoaded = true, string? logFile = null)
    {
        var payload = new Dictionary<string, object?>
        {
            ["tool"] = "download-probe",
            ["options"] = "SoftwareOnlyChanges",
            ["disruptive"] = true,
            ["transferVerdict"] = transferVerdict,
            ["softwareLoaded"] = softwareLoaded,
            ["exitCode"] = 0,
            ["logFile"] = logFile ?? @"C:\staging\probe-logs\run.txt",
            ["log"] = logText.Replace("\r\n", "\n").Split('\n'),
        };

        return JsonSerializer.Serialize(payload);
    }

    // ---- THE FIRST-CLASS MANIFEST (the probe's `loadManifest`, 2026-08-13) -------------------------

    private static string WithLoadManifest(string? source, bool available, string[]? loaded, string verdict = "Transferred")
    {
        var manifest = new Dictionary<string, object?>
        {
            ["available"] = available,
            ["source"] = source,
            ["loadedObjects"] = loaded,
            ["loadedObjectCount"] = loaded?.Length,
            ["verdict"] = available ? verdict : null,
            ["verdictReason"] = available ? "at least one item was reported loaded by name." : null,
            ["unrecognisedMessageCount"] = available ? 0 : (int?)null,
        };

        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["tool"] = "download-probe",
            ["exitCode"] = 0,
            ["loadManifest"] = manifest,

            // The log is STILL embedded and STILL describes a different set. If the reader silently fell
            // back to it, this test would pass for the wrong reason — so the two are deliberately made to
            // disagree.
            ["log"] = ReadFixture("differential-one-object.txt").Replace("\r\n", "\n").Split('\n'),
        });
    }

    /// <summary>
    /// *** THE SCRAPE IS GONE. *** The probe now emits the manifest built by
    /// <c>DownloadResultAdapter</c> — first-hand, off the live <c>DownloadResult</c>, which is the path
    /// <c>ProbeLogReader</c>'s own documentation names and which nothing had ever used. This is F2.
    /// </summary>
    [Fact]
    public void The_first_class_loadManifest_is_preferred_over_the_embedded_log()
    {
        var report = ProbeReport.FromStdout(WithLoadManifest("DownloadResultAdapter", true, new[] { "FC_A", "FB_B", "DB_C" }));

        Assert.Equal(new[] { "DB_C", "FB_B", "FC_A" }, report.Manifest.OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(TransferVerdict.Transferred, report.Verdict);
        Assert.Equal("DownloadResultAdapter", report.Source);
        Assert.Contains("FIRST-HAND", report.Detail, StringComparison.Ordinal);

        // The embedded log names DB_Data01 and nothing else. If it had been read, this would be it.
        Assert.DoesNotContain("DB_Data01", report.Manifest);
    }

    /// <summary>
    /// <c>available: false</c> is <b>"nobody looked"</b>, never "TIA loaded nothing". The probe emits every
    /// list as null in that case precisely so the two cannot be confused; reading a null as an empty set
    /// here would undo that at the first consumer.
    /// </summary>
    [Fact]
    public void An_unavailable_loadManifest_is_Undetermined_and_NOT_an_empty_load()
    {
        var report = ProbeReport.FromStdout(WithLoadManifest("none", available: false, loaded: null));

        Assert.Equal(TransferVerdict.Undetermined, report.Verdict);
        Assert.NotEqual(TransferVerdict.NothingTransferred, report.Verdict);
        Assert.Empty(report.Manifest);
        Assert.False(report.ManifestAvailable);
        Assert.Contains("nobody looked", report.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// A manifest the probe says came from <c>ProbeLogReader</c> is USED and LABELLED, not upgraded. The
    /// source is recorded by whoever built the feedback and is never assumed by the reader.
    /// </summary>
    [Fact]
    public void A_manifest_the_probe_says_is_RE_DERIVED_is_labelled_as_such()
    {
        var report = ProbeReport.FromStdout(WithLoadManifest("ProbeLogReader", true, new[] { "FC_A" }));

        Assert.Equal("ProbeLogReader", report.Source);
        Assert.Contains("NOT first-hand", report.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>An older probe build is a real thing to be handed.</b> Without a <c>loadManifest</c> the reader
    /// falls back to the embedded log rather than reporting no manifest — losing it silently would be
    /// worse than reading a rendering, and the source says which happened.
    /// </summary>
    [Fact]
    public void Without_a_loadManifest_the_reader_still_falls_back_to_the_embedded_log()
    {
        var report = ProbeReport.FromStdout(ProbeJson(ReadFixture("differential-one-object.txt")));

        Assert.Contains("DB_Data01", report.Manifest);
        Assert.Equal(nameof(ProbeLogReader), report.Source);
    }

    // ---- THE FALLBACK PATH, against recorded live-rig logs -----------------------------------------

    [Fact]
    public void A_full_load_yields_the_ninety_nine_names_the_device_reported()
    {
        var report = ProbeReport.FromStdout(ProbeJson(ReadFixture("full-ninety-nine-objects.txt")));

        Assert.True(report.JsonParsed);
        Assert.Equal(TransferVerdict.Transferred, report.Verdict);
        Assert.Equal(99, report.Manifest.Count);
    }

    [Fact]
    public void A_differential_load_yields_the_one_name_and_not_the_whole_program()
    {
        var report = ProbeReport.FromStdout(ProbeJson(ReadFixture("differential-one-object.txt")));

        Assert.Equal(TransferVerdict.Transferred, report.Verdict);
        Assert.Single(report.Manifest);
        Assert.Contains("DB_Data01", report.Manifest);
    }

    [Fact]
    public void An_up_to_date_run_is_NothingTransferred_and_has_an_EMPTY_manifest()
    {
        var report = ProbeReport.FromStdout(ProbeJson(ReadFixture("up-to-date-nothing-transferred.txt"), "NothingTransferred", false));

        Assert.Equal(TransferVerdict.NothingTransferred, report.Verdict);
        Assert.Empty(report.Manifest);
    }

    /// <summary>
    /// An aborted run must read as <c>Undetermined</c>, never as <c>NothingTransferred</c>. The two
    /// differ in what the caller may do next: after the second the device is known to hold what it
    /// held, after the first nothing whatever is known.
    /// </summary>
    [Fact]
    public void An_aborted_run_is_Undetermined_and_specifically_NOT_NothingTransferred()
    {
        var report = ProbeReport.FromStdout(ProbeJson(ReadFixture("aborted-no-download-result.txt"), null, null));

        Assert.Equal(TransferVerdict.Undetermined, report.Verdict);
        Assert.NotEqual(TransferVerdict.NothingTransferred, report.Verdict);
    }

    [Fact]
    public void No_stdout_at_all_is_a_reported_gap_and_never_a_manifest()
    {
        var report = ProbeReport.FromStdout(null);

        Assert.False(report.JsonParsed);
        Assert.Empty(report.Manifest);
        Assert.Equal(TransferVerdict.Undetermined, report.Verdict);
        Assert.Contains("not the same as nothing having been", report.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Stdout_that_is_not_json_is_reported_with_its_first_characters_rather_than_swallowed()
    {
        var report = ProbeReport.FromStdout("Unknown option '--yes'.\n\ndownload-probe <project.ap20> ...");

        Assert.False(report.JsonParsed);
        Assert.Empty(report.Manifest);
        Assert.Contains("Unknown option", report.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Json_with_no_embedded_log_falls_back_to_the_log_FILE_when_one_can_be_read()
    {
        var json = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["transferVerdict"] = "Transferred",
            ["logFile"] = @"C:\staging\probe-logs\run.txt",
        });

        var report = ProbeReport.FromStdout(json, _ => ReadFixture("differential-one-object.txt"));

        Assert.Single(report.Manifest);
        Assert.Contains("DB_Data01", report.Manifest);
    }

    [Fact]
    public void Json_with_no_log_anywhere_reports_that_the_verdict_line_alone_is_not_a_manifest()
    {
        var json = JsonSerializer.Serialize(new Dictionary<string, object?> { ["transferVerdict"] = "Transferred" });

        var report = ProbeReport.FromStdout(json);

        Assert.True(report.JsonParsed);
        Assert.Equal("Transferred", report.TransferVerdictText);
        Assert.Empty(report.Manifest);
        Assert.Equal(TransferVerdict.Undetermined, report.Verdict);
        Assert.Contains("not a manifest", report.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The probe's own verdict STRING is carried but never trusted.</b> The verdict this gateway
    /// acts on is re-derived from the message tree by <c>Ladder.Download</c>, so a JSON header
    /// claiming <c>Transferred</c> over a log that shows an abort does not produce a manifest.
    /// </summary>
    [Fact]
    public void A_json_header_claiming_Transferred_over_an_aborted_log_does_not_manufacture_a_manifest()
    {
        var report = ProbeReport.FromStdout(ProbeJson(ReadFixture("aborted-no-download-result.txt"), "Transferred", true));

        Assert.Equal("Transferred", report.TransferVerdictText);
        Assert.Equal(TransferVerdict.Undetermined, report.Verdict);
        Assert.Empty(report.Manifest);
    }
}
