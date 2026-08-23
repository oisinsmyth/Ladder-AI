using System.Text.Json;
using NModbus;

namespace Harness.MirrorRead.Tests;

/// <summary>
/// 🔴 <b>THE WIDTH, IN A FORM SOMETHING OTHER THAN A PERSON CAN READ.</b>
///
/// <para>Until now the only structured output of this tool was its exit code: the width, the denominator
/// and every finding reached a reader as console prose and reached a caller not at all. A batch that
/// feeds this tool a DERIVED width needs the verdict as an artifact a reviewer opens weeks later, next
/// to the result packages — <i>"the plan's line is printed to a terminal and gone with it"</i>.</para>
///
/// <para><b>The rule the shape is built around:</b> a run that measured nothing must not serialise into
/// anything a reader could mistake for a pass. So <c>measured</c> is a field, the examined counts are
/// present on EVERY run including the zero case, and the fence-refused and connect-failed paths produce
/// a report rather than no report.</para>
/// </summary>
public class MirrorReadReportTests : IDisposable
{
    private readonly string _allowlist;
    private readonly string _directory =
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "mirror-read-report-" + Guid.NewGuid().ToString("N"))).FullName;

    public MirrorReadReportTests()
    {
        _allowlist = Path.Combine(_directory, "allowlist.json");
        File.WriteAllText(_allowlist, """
            { "entries": [ { "address": "10.10.10.10", "label": "fixture", "kind": "test-rig" } ] }
            """);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp directory is not a test failure.
        }
    }

    private MirrorReadOptions Options(int declared = 37, int scanRetries = 0) =>
        new("10.10.10.10", 503, 1, _allowlist, declared, declared - 3, declared + 1, IntervalMs: 0,
            ScanRetries: scanRetries, ScanRetryIntervalMs: 0);

    private JsonElement ReportFor(IRegisterSource source, int declared = 37, string? address = null)
    {
        var options = Options(declared) with { Address = address ?? "10.10.10.10" };
        var outcome = MirrorReadRun.ExecuteAndReport(options, new RecordingFactory(() => source), new StringWriter());
        return JsonDocument.Parse(outcome.Report.ToJson()).RootElement.Clone();
    }

    // =============================================================================================
    // 1 — THE SHAPE, OVER A RUN THAT MEASURED SOMETHING.
    // =============================================================================================

    /// <summary>
    /// The passing case carries the claim, the verdict and the denominator. All three, because a verdict
    /// without the width it was about, or without how much was looked at, is not citable evidence.
    /// </summary>
    [Fact]
    public void A_passing_run_serialises_the_claim_the_verdict_and_the_denominator()
    {
        var report = ReportFor(new ScriptedSource(available: 37));

        Assert.Equal(37, report.GetProperty("declaredRegisters").GetInt32());
        Assert.Equal(36, report.GetProperty("lastDeclaredRegister").GetInt32());
        Assert.Equal(0, report.GetProperty("exit").GetInt32());
        Assert.Equal("Ok", report.GetProperty("exitName").GetString());
        Assert.Equal("ExactlyAsDeclared", report.GetProperty("widthVerdict").GetString());
        Assert.True(report.GetProperty("measured").GetBoolean());

        var examined = report.GetProperty("examined");
        Assert.Equal(37, examined.GetProperty("registersReadWhole").GetInt32());
        Assert.Equal(1, examined.GetProperty("pages").GetInt32());
        Assert.Equal(5, examined.GetProperty("boundaryProbes").GetInt32());

        Assert.Empty(report.GetProperty("findings").EnumerateArray());
    }

    /// <summary>
    /// Every boundary probe is serialised with the fact that decides the verdict: whether the server
    /// REFUSED it (a Modbus exception response) or merely said nothing. Collapsing those two is how a
    /// dropped packet becomes proof of a boundary.
    /// </summary>
    [Fact]
    public void Every_boundary_probe_carries_its_outcome_and_whether_it_was_inside_the_claim()
    {
        var report = ReportFor(new ScriptedSource(available: 37));

        var probes = report.GetProperty("probes").EnumerateArray().ToList();
        Assert.Equal(5, probes.Count);

        var inside = probes.First(p => p.GetProperty("register").GetInt32() == 36);
        Assert.True(inside.GetProperty("declared").GetBoolean());
        Assert.Equal("Ok", inside.GetProperty("outcome").GetString());

        var outside = probes.First(p => p.GetProperty("register").GetInt32() == 37);
        Assert.False(outside.GetProperty("declared").GetBoolean());
        Assert.Equal("RefusedByServer", outside.GetProperty("outcome").GetString());

        // The exception code is the SERVER's — 2, Illegal Data Address, from a real MB_SERVER. What is
        // asserted here is that the field carries the server's number where there is one and NULL where
        // there is not: this in-process fixture raises a SlaveException without populating the code, so
        // asserting the 2 would be asserting the fixture rather than the serialiser.
        Assert.Equal(JsonValueKind.Null, inside.GetProperty("exceptionCode").ValueKind);
        Assert.Equal(JsonValueKind.Number, outside.GetProperty("exceptionCode").ValueKind);
    }

    /// <summary>
    /// *** THE HEADLINE FAILURE, STRUCTURED. *** A device narrower than the claim serialises as its own
    /// verdict with the findings intact — the caller must not have to grep prose to learn the widening
    /// did not reach the wire.
    /// </summary>
    [Fact]
    public void A_device_NARROWER_than_the_claim_serialises_as_its_own_verdict()
    {
        var report = ReportFor(new ScriptedSource(available: 35));

        Assert.Equal(6, report.GetProperty("exit").GetInt32());
        Assert.Equal("NarrowerThanDeclared", report.GetProperty("widthVerdict").GetString());
        Assert.True(report.GetProperty("measured").GetBoolean());
        Assert.Contains(report.GetProperty("findings").EnumerateArray(),
            f => f.GetString()!.Contains("NARROWER THAN DECLARED", StringComparison.Ordinal));
    }

    /// <summary>A server exposing more than the claim is the other verdict, and it is not a pass either.</summary>
    [Fact]
    public void A_device_WIDER_than_the_claim_serialises_as_its_own_verdict()
    {
        var report = ReportFor(new ScriptedSource(available: 64), declared: 37);

        Assert.Equal(7, report.GetProperty("exit").GetInt32());
        Assert.Equal("WiderThanDeclared", report.GetProperty("widthVerdict").GetString());
    }

    // =============================================================================================
    // 2 — THE RUNS THAT MEASURED NOTHING. THEY STILL PRODUCE A REPORT, AND IT SAYS SO.
    // =============================================================================================

    /// <summary>
    /// 🔴 <b>A refused fence still writes a report.</b> Emitting nothing would leave the consumer holding
    /// the previous run's file, or no file at all — and "no file" is exactly as easy to overlook as a
    /// passing one. The report says <c>measured: false</c> and its examined counts are zero.
    /// </summary>
    [Fact]
    public void A_run_the_fence_REFUSED_still_reports_and_says_it_measured_nothing()
    {
        var report = ReportFor(new ScriptedSource(available: 37), address: "192.168.99.99");

        Assert.Equal(1, report.GetProperty("exit").GetInt32());
        Assert.False(report.GetProperty("measured").GetBoolean());
        Assert.Equal("NotEstablished", report.GetProperty("widthVerdict").GetString());
        Assert.Equal(0, report.GetProperty("examined").GetProperty("registersReadWhole").GetInt32());
        Assert.Equal(0, report.GetProperty("examined").GetProperty("boundaryProbes").GetInt32());
        Assert.NotEmpty(report.GetProperty("findings").EnumerateArray());
    }

    /// <summary>A connect that never opened is the same class of nothing, and reports the same way.</summary>
    [Fact]
    public void A_run_that_could_not_CONNECT_reports_that_it_measured_nothing()
    {
        var options = Options();
        var outcome = MirrorReadRun.ExecuteAndReport(options, new FailingFactory(), new StringWriter());
        var report = JsonDocument.Parse(outcome.Report.ToJson()).RootElement;

        Assert.Equal(3, report.GetProperty("exit").GetInt32());
        Assert.False(report.GetProperty("measured").GetBoolean());
        Assert.Equal("NotEstablished", report.GetProperty("widthVerdict").GetString());
    }

    /// <summary>
    /// 🔴 <b>THE CONTROL FOR THE PAIR ABOVE.</b> A serializer that hardcoded <c>measured: false</c> would
    /// pass both. The passing run must say <c>true</c>, and it is asserted here rather than left implied
    /// by the section above.
    /// </summary>
    [Fact]
    public void The_measured_flag_is_TRUE_when_the_run_actually_read_the_device()
    {
        Assert.True(ReportFor(new ScriptedSource(available: 37)).GetProperty("measured").GetBoolean());
    }

    /// <summary>
    /// What the report cannot see travels WITH it. A consumer holding a passing file must be able to
    /// read, without going anywhere else, that reachability is not coherence and that a corpus-derived
    /// claim is only as good as the corpus it came from.
    /// </summary>
    [Fact]
    public void The_report_carries_what_this_measurement_cannot_see()
    {
        var report = ReportFor(new ScriptedSource(available: 37));

        Assert.NotEmpty(report.GetProperty("notSeen").EnumerateArray());
    }

    // =============================================================================================
    // 3 — THE JUST-RESTARTED CPU. RETRIED, AND ONLY FOR THE ONE CODE THAT IS TRANSIENT.
    // =============================================================================================

    /// <summary>
    /// 🔴 <b>A download stops the CPU, so a step placed after one meets a counter that has not moved
    /// yet.</b> Exit 8 is not a width verdict and it clears by itself. The wave already answers this by
    /// ASKING AGAIN rather than sleeping a guessed duration, and this is the same mechanism: N attempts,
    /// an interval, and the number of attempts REPORTED so the settling is measured rather than assumed.
    /// </summary>
    [Fact]
    public void A_counter_that_starts_stalled_and_then_rises_PASSES_on_a_later_attempt()
    {
        var source = new RestartingSource(available: 37, liveAfterControlReads: 2);

        var outcome = MirrorReadRun.ExecuteAndReport(
            Options(scanRetries: 3), new RecordingFactory(() => source), new StringWriter());

        Assert.Equal(MirrorReadExit.Ok, outcome.Exit);
        Assert.Equal(2, outcome.Report.Attempts);
    }

    /// <summary>
    /// And a counter that never rises exhausts the attempts and reports exit 8 WITH the count. "Asked
    /// once" and "asked twelve times over a minute" are different evidence about the same device.
    /// </summary>
    [Fact]
    public void A_counter_that_never_rises_exhausts_the_attempts_and_says_how_many()
    {
        var source = new RestartingSource(available: 37, liveAfterControlReads: int.MaxValue);

        var outcome = MirrorReadRun.ExecuteAndReport(
            Options(scanRetries: 2), new RecordingFactory(() => source), new StringWriter());

        Assert.Equal(MirrorReadExit.ScanCounterNotRising, outcome.Exit);
        Assert.Equal(3, outcome.Report.Attempts);
    }

    /// <summary>
    /// 🔴 <b>AND NOTHING ELSE IS RETRIED.</b> A narrower area is a CONCLUSION, and re-rolling a
    /// conclusion until it changes is how a check becomes a random number generator. One attempt, one
    /// verdict.
    /// </summary>
    [Fact]
    public void A_width_verdict_is_never_retried_however_many_attempts_are_allowed()
    {
        var source = new ScriptedSource(available: 35);

        var outcome = MirrorReadRun.ExecuteAndReport(
            Options(scanRetries: 9), new RecordingFactory(() => source), new StringWriter());

        Assert.Equal(MirrorReadExit.NarrowerThanDeclared, outcome.Exit);
        Assert.Equal(1, outcome.Report.Attempts);
    }

    /// <summary>
    /// The default is ZERO retries, so nothing that does not ask for the batch's behaviour pays for it —
    /// and a tool run by hand still reports the stall on its first reading rather than after a minute.
    /// </summary>
    [Fact]
    public void With_no_retry_asked_for_the_stall_is_reported_on_the_first_attempt()
    {
        var source = new RestartingSource(available: 37, liveAfterControlReads: 2);

        var outcome = MirrorReadRun.ExecuteAndReport(
            Options(), new RecordingFactory(() => source), new StringWriter());

        Assert.Equal(MirrorReadExit.ScanCounterNotRising, outcome.Exit);
        Assert.Equal(1, outcome.Report.Attempts);
    }

    // =============================================================================================
    // 4 — THE CLI EDGE: --out AND --json.
    // =============================================================================================

    /// <summary>
    /// <c>--out</c> writes the report where it was told to, and the prose still goes to stdout — the
    /// artifact is an addition, not a replacement for the transcript a person reads.
    /// </summary>
    [Fact]
    public void The_out_flag_writes_the_report_and_leaves_the_prose_on_stdout()
    {
        var written = new List<(string Path, string Text)>();
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exit = MirrorReadCli.Run(
            new[] { "--address", "10.10.10.10", "--declared-registers", "37", "--allowlist", _allowlist,
                    "--interval-ms", "0", "--out", @"C:\staging\mirror-width.json" },
            new RecordingFactory(() => new ScriptedSource(available: 37)), stdout, stderr,
            (path, text) => written.Add((path, text)));

        Assert.Equal(0, exit);

        var file = Assert.Single(written);
        Assert.Equal(@"C:\staging\mirror-width.json", file.Path);
        Assert.Equal(37, JsonDocument.Parse(file.Text).RootElement.GetProperty("declaredRegisters").GetInt32());

        Assert.Contains("== verdict ==", stdout.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, stderr.ToString());
    }

    /// <summary>
    /// <c>--json</c> puts the DOCUMENT on stdout and the prose on stderr. Nothing is discarded: a caller
    /// redirecting stdout gets something parseable, and the human transcript survives on the other
    /// channel instead of being thrown away for being unstructured.
    /// </summary>
    [Fact]
    public void The_json_flag_puts_the_document_on_stdout_and_the_prose_on_stderr()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        MirrorReadCli.Run(
            new[] { "--address", "10.10.10.10", "--declared-registers", "37", "--allowlist", _allowlist,
                    "--interval-ms", "0", "--json" },
            new RecordingFactory(() => new ScriptedSource(available: 37)), stdout, stderr, (_, _) => { });

        var document = JsonDocument.Parse(stdout.ToString());
        Assert.Equal("ExactlyAsDeclared", document.RootElement.GetProperty("widthVerdict").GetString());
        Assert.Contains("== verdict ==", stderr.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 <b>AND A REFUSED RUN STILL WRITES THE FILE.</b> A consumer that finds no file cannot tell a
    /// refusal from a run nobody started; one that finds a file saying <c>measured: false</c> can.
    /// </summary>
    [Fact]
    public void A_usage_refusal_still_writes_a_report_that_says_nothing_was_measured()
    {
        var written = new List<(string Path, string Text)>();

        var exit = MirrorReadCli.Run(
            new[] { "--address", "10.10.10.10", "--declared-registers", "0", "--allowlist", _allowlist,
                    "--out", @"C:\staging\mirror-width.json" },
            new RecordingFactory(() => new ScriptedSource(available: 37)), new StringWriter(), new StringWriter(),
            (path, text) => written.Add((path, text)));

        Assert.Equal(2, exit);

        var file = Assert.Single(written);
        Assert.False(JsonDocument.Parse(file.Text).RootElement.GetProperty("measured").GetBoolean());
    }

    /// <summary>
    /// The retry numbers reach the run from the command line, because the batch is the only caller that
    /// knows a download just happened.
    /// </summary>
    [Fact]
    public void The_scan_retry_flags_are_read_from_the_command_line()
    {
        var source = new RestartingSource(available: 37, liveAfterControlReads: 2);
        var stdout = new StringWriter();

        var exit = MirrorReadCli.Run(
            new[] { "--address", "10.10.10.10", "--declared-registers", "37", "--allowlist", _allowlist,
                    "--interval-ms", "0", "--scan-retry", "3", "--scan-retry-interval-ms", "0" },
            new RecordingFactory(() => source), stdout, new StringWriter(), (_, _) => { });

        Assert.Equal(0, exit);
        Assert.Contains("attempt 2 of 4", stdout.ToString(), StringComparison.Ordinal);
    }
}

/// <summary>
/// A server whose scan counter is FROZEN until it has answered a given number of control reads — the
/// shape of a CPU that has just been stopped by a download and is coming back.
///
/// <para>Separate from <see cref="ScriptedSource"/> rather than a flag on it: that fixture's per-read
/// advance is what every other test in this assembly depends on, and a fixture that changes behaviour
/// mid-run is exactly the thing to keep out of the shared one.</para>
/// </summary>
internal sealed class RestartingSource : IRegisterSource
{
    private readonly ushort[] _registers;
    private readonly int _liveAfterControlReads;
    private int _controlReads;

    internal RestartingSource(int available, int liveAfterControlReads, uint stamp = 0xF52ECEAD)
    {
        _registers = new ushort[available];
        _liveAfterControlReads = liveAfterControlReads;

        _registers[0] = (ushort)(stamp >> 16);
        _registers[1] = (ushort)(stamp & 0xFFFF);
    }

    public ushort[] Read(int startRegister, int count)
    {
        if (startRegister < 0 || startRegister + count > _registers.Length)
        {
            throw new NModbus.SlaveException(
                $"scripted: Function Code: 3, Exception Code: 2 - Illegal Data Address " +
                $"(asked for {count} at {startRegister}; this server holds 0..{_registers.Length - 1}).");
        }

        if (startRegister == 0 && count == 4)
            _controlReads++;

        var answer = _registers[startRegister..(startRegister + count)];

        // The counter only starts moving once the CPU is back. Advancing on the read is the only clock a
        // test has, and it is the same compromise ScriptedSource documents.
        if (_controlReads > _liveAfterControlReads)
        {
            var scan = ((uint)_registers[2] << 16) | _registers[3];
            scan = unchecked(scan + 7);
            _registers[2] = (ushort)(scan >> 16);
            _registers[3] = (ushort)(scan & 0xFFFF);
        }

        return answer;
    }

    public void Dispose()
    {
    }
}
