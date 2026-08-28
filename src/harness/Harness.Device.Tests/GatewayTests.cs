using System.Text;
using System.Text.Json;
using Harness.Map;
using Harness.Wire;

namespace Harness.Device.Tests;

/// <summary>
/// The gateway's ordering and its refusals, driven through a recording runner.
///
/// <para><b>None of this is evidence about a controller</b>, and the suite is deliberately mostly
/// NEGATIVE: what it pins is that a failed or unreadable step means NO DOWNLOAD IS ATTEMPTED, that a
/// download that did not load opens no transport, and that <c>State</c>-shaped success never becomes
/// a deployment on its own.</para>
/// </summary>
public sealed class GatewayTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "harness-device-" + Guid.NewGuid().ToString("N"));

    public GatewayTests()
    {
        Directory.CreateDirectory(_root);
        foreach (var name in new[] { "converter.exe", "openness-cli.exe", "download-probe.exe" })
            File.WriteAllText(Path.Combine(_root, name), "not a real binary; the runner is injected.");

        // No allowlist is written: the project fence was removed on 2026-08-28 (ADR-0013), so there
        // is no file that makes a project allowed and nothing here needs to make one.
        Directory.CreateDirectory(Path.Combine(_root, "tools"));
        File.WriteAllText(Path.Combine(_root, "CLAUDE.md"), "# repo root marker");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A temp directory that could not be removed is not a test failure.
        }
    }

    private DeviceGatewayOptions Options(bool withDevice = true) =>
        new(
            ConverterExe: Path.Combine(_root, "converter.exe"),
            OpennessCliExe: Path.Combine(_root, "openness-cli.exe"),
            DownloadProbeExe: Path.Combine(_root, "download-probe.exe"),
            ProjectPath: Path.Combine(_root, "Scratch.ap20"),
            GroupPath: "PLC1 6ES7 214-1AG40-0XB0/Program blocks",
            PcInterface: "Intel(R) Ethernet Connection",
            DownloadOption: DownloadOption.SoftwareOnlyChanges,
            AllowCpuStop: true,
            ModbusHost: "192.0.2.10",
            StagingDirectory: Path.Combine(_root, "staging"),
            Device: withDevice ? "PLC1" : null,
            AllowlistStartDirectory: _root);

    private static IReadOnlyList<HarnessObject> Objects() => ArgumentVocabularyTests.Objects();

    /// <summary>A probe log in the shape <c>ProbeSession.ReportResult</c> writes, naming the given objects.</summary>
    private static string ProbeLog(params string[] loaded)
    {
        var sb = new StringBuilder();
        sb.AppendLine("==== DOWNLOAD RESULT ===========================================================");
        sb.AppendLine("state         : Success");
        sb.AppendLine("errors        : 0");
        sb.AppendLine("warnings      : 0");
        sb.AppendLine("              (NONE of those three says whether anything was TRANSFERRED — see below.)");
        sb.AppendLine();
        sb.AppendLine("messages (recursive, verbatim):");
        sb.AppendLine("  - state=Success errors=0 warnings=0 at=2026-08-13T10:00:00.0000000Z");
        sb.AppendLine("    text : PLC_1");

        foreach (var name in loaded)
        {
            sb.AppendLine("    - state=Success errors=0 warnings=0 at=2026-08-13T10:00:01.0000000Z");
            sb.AppendLine($"      text : '{name}' was loaded successfully.");
        }

        return sb.ToString();
    }

    private static string ProbeJson(string log) =>
        JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["tool"] = "download-probe",
            ["exitCode"] = 0,
            ["logFile"] = "(embedded)",
            ["log"] = log.Replace("\r\n", "\n").Split('\n'),
        });

    private static string LoadedEverything() =>
        ProbeJson(ProbeLog("FC_HarnessCopyLayer", "FC_DemoRamp", "DB_Program"));

    private OpennessDeviceGateway Gateway(RecordingRunner runner, Func<IRegisterTransport>? transport = null) =>
        new(Options(), runner, transport ?? (() => throw new InvalidOperationException("no transport was expected in this test.")));

    // ---- THE HAPPY PATH --------------------------------------------------------------------------

    [Fact]
    public void A_clean_run_executes_every_step_in_order_and_reports_the_devices_own_manifest()
    {
        var runner = new RecordingRunner { DefaultStdout = LoadedEverything() };
        var outcome = Gateway(runner).Deploy(Objects(), new BuildStamp(0x1234ABCD));

        Assert.True(outcome.Attempted);
        Assert.True(outcome.Loaded);
        Assert.Equal(
            new[] { "DB_Program", "FC_DemoRamp", "FC_HarnessCopyLayer" },
            outcome.Manifest.OrderBy(n => n, StringComparer.Ordinal).ToArray());

        // convert, import, layout set, layout expect, compile-all, sanity-check, download.
        Assert.Equal(7, runner.Calls.Count);
        Assert.Equal("to-xml", runner.Calls[0].Arguments[0]);
        Assert.Equal("import-all", runner.Calls[1].Arguments[0]);
        Assert.Equal("block-layout", runner.Calls[2].Arguments[0]);
        Assert.Equal("block-layout", runner.Calls[3].Arguments[0]);
        Assert.Equal("compile-all", runner.Calls[4].Arguments[0]);
        Assert.Equal("sanity-check", runner.Calls[5].Arguments[0]);
        Assert.EndsWith("download-probe.exe", runner.Calls[6].Executable, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_generated_IR_is_written_to_disk_before_the_converter_is_invoked()
    {
        var runner = new RecordingRunner { DefaultStdout = LoadedEverything() };
        Gateway(runner).Deploy(Objects(), new BuildStamp(1));

        var irDir = Options().IrStagingDirectory;
        foreach (var obj in Objects())
        {
            var path = Path.Combine(irDir, obj.Name + ".ir");
            Assert.True(File.Exists(path), $"'{path}' was not staged, so the converter would have been handed a name with no file behind it.");
            Assert.Equal(obj.Ir, File.ReadAllText(path));
        }
    }

    [Fact]
    public void A_stale_xml_from_a_previous_turn_is_deleted_rather_than_imported_alongside_this_one()
    {
        var options = Options();
        Directory.CreateDirectory(options.XmlStagingDirectory);
        var stale = Path.Combine(options.XmlStagingDirectory, "FC_FromLastTurn.xml");
        File.WriteAllText(stale, "<Document/>");

        var runner = new RecordingRunner { DefaultStdout = LoadedEverything() };
        Gateway(runner).Deploy(Objects(), new BuildStamp(1));

        Assert.False(File.Exists(stale),
            "import-all is handed the DIRECTORY, so a file left by a previous turn would be imported alongside this turn's and would be on the controller unnoticed.");
    }

    // ---- NOTHING IS SPENT PAST A STEP THAT DID NOT PASS ------------------------------------------

    [Theory]
    [InlineData(DeviceStepKind.Convert, 1)]
    [InlineData(DeviceStepKind.Import, DeviceExitCodes.CliImportIncomplete)]
    [InlineData(DeviceStepKind.LayoutSet, DeviceExitCodes.CliLayoutMismatch)]
    [InlineData(DeviceStepKind.LayoutExpect, DeviceExitCodes.CliLayoutMismatch)]
    [InlineData(DeviceStepKind.CompileAll, DeviceExitCodes.CliCompileFailed)]
    [InlineData(DeviceStepKind.CompileAll, DeviceExitCodes.CliCompileIncomplete)]
    [InlineData(DeviceStepKind.SanityCheck, DeviceExitCodes.CliSanityCheckFailed)]
    public void A_step_that_did_not_pass_means_NO_DOWNLOAD_WAS_ATTEMPTED(DeviceStepKind kind, int exitCode)
    {
        var runner = new RecordingRunner { DefaultStdout = LoadedEverything() };
        runner.ScriptExit(kind, exitCode);

        var outcome = Gateway(runner).Deploy(Objects(), new BuildStamp(1));

        Assert.False(outcome.Attempted);
        Assert.False(outcome.Loaded);
        Assert.Empty(outcome.Manifest);
        Assert.Contains("NO DOWNLOAD WAS ATTEMPTED", outcome.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain(runner.Calls, c => c.Executable.EndsWith("download-probe.exe", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// *** THE ONE THAT LOOKS LIKE A PASS. *** <c>compile-all</c> exiting 14 reports no errors and
    /// compiled nothing. It must stop the deployment exactly as a failure does.
    /// </summary>
    [Fact]
    public void A_gate_that_EXAMINED_NOTHING_stops_the_deployment_just_as_a_failing_one_does()
    {
        var runner = new RecordingRunner { DefaultStdout = LoadedEverything() };
        runner.ScriptExit(DeviceStepKind.CompileAll, DeviceExitCodes.CliNothingExamined);

        var outcome = Gateway(runner).Deploy(Objects(), new BuildStamp(1));

        Assert.False(outcome.Attempted);
        Assert.Contains("NotProven", outcome.Detail, StringComparison.Ordinal);
        Assert.Contains("Empty is not clean", outcome.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_binary_that_does_not_exist_is_refused_before_anything_is_run()
    {
        var options = Options() with { OpennessCliExe = Path.Combine(_root, "not-there.exe") };
        var runner = new RecordingRunner();

        var outcome = new OpennessDeviceGateway(options, runner).Deploy(Objects(), new BuildStamp(1));

        Assert.False(outcome.Attempted);
        Assert.Empty(runner.Calls);
        Assert.Contains("not-there.exe", outcome.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_project_NOBODY_NAMED_IS_NOW_ATTEMPTED_BecauseTheFenceWasRemoved()
    {
        // 🔴 THE INVERSE OF WHAT THIS ASSERTED, AND THE MOST CONSEQUENTIAL ONE IN THIS FILE. It
        // required that a project nobody allowlisted be refused BEFORE A SINGLE COMMAND RAN — this
        // gateway imports and compiles into the project, so it writes before download-probe would
        // ever have been reached. That refusal was removed on 2026-08-28 (ADR-0013).
        //
        // The name below is shaped like a live engineering job on purpose: it is what the fence existed
        // to stop, and it now proceeds.
        var options = Options() with { ProjectPath = Path.Combine(_root, "RealProject scratch.ap20") };
        var runner = new RecordingRunner();

        var outcome = new OpennessDeviceGateway(options, runner).Deploy(Objects(), new BuildStamp(1));

        Assert.True(outcome.Attempted, "A project nobody named was refused. ADR-0013 removed that fence.");
        Assert.NotEmpty(runner.Calls);
        Assert.DoesNotContain("ALLOWLISTED PROJECT", outcome.Detail, StringComparison.Ordinal);
    }

    // ---- A DOWNLOAD THAT RAN AND DID NOT LAND ----------------------------------------------------

    [Fact]
    public void A_download_that_failed_is_ATTEMPTED_because_the_CPU_may_be_stopped()
    {
        var runner = new RecordingRunner { DefaultStdout = LoadedEverything() };
        runner.ScriptExit(DeviceStepKind.Download, DeviceExitCodes.ProbeAbortedByUnhandledConfiguration, LoadedEverything());

        var outcome = Gateway(runner).Deploy(Objects(), new BuildStamp(1));

        Assert.True(outcome.Attempted);
        Assert.False(outcome.Loaded);
        Assert.Contains("NOT 'nothing happened'", outcome.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_download_whose_manifest_omits_an_object_is_attempted_and_NOT_loaded()
    {
        var runner = new RecordingRunner
        {
            DefaultStdout = ProbeJson(ProbeLog("FC_HarnessCopyLayer", "DB_Program")),
        };

        var outcome = Gateway(runner).Deploy(Objects(), new BuildStamp(1));

        Assert.True(outcome.Attempted);
        Assert.False(outcome.Loaded);
        Assert.Contains("FC_DemoRamp", outcome.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// A download that exits 0 with a result carrying no messages is <c>Undetermined</c>, and
    /// <b>Undetermined is not loaded</b>. This is the exact shape of the live run that reported
    /// <c>State = Success</c> and carried nothing.
    /// </summary>
    [Fact]
    public void An_exit_0_download_with_an_EMPTY_manifest_is_not_a_deployment()
    {
        var runner = new RecordingRunner { DefaultStdout = ProbeJson(ProbeLog()) };

        var outcome = Gateway(runner).Deploy(Objects(), new BuildStamp(1));

        Assert.True(outcome.Attempted);
        Assert.False(outcome.Loaded);
        Assert.Contains("Undetermined", outcome.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_download_that_printed_nothing_parseable_yields_no_manifest_and_is_not_loaded()
    {
        var runner = new RecordingRunner { DefaultStdout = string.Empty };

        var outcome = Gateway(runner).Deploy(Objects(), new BuildStamp(1));

        Assert.True(outcome.Attempted);
        Assert.False(outcome.Loaded);
        Assert.Empty(outcome.Manifest);
    }

    // ---- THE TRANSPORT ---------------------------------------------------------------------------

    [Fact]
    public void No_transport_is_opened_before_anything_is_deployed()
    {
        var gateway = Gateway(new RecordingRunner());

        var ex = Assert.Throws<InvalidOperationException>(() => gateway.Open());
        Assert.Contains("nothing has been deployed", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void No_transport_is_opened_after_a_deployment_that_did_not_load()
    {
        var runner = new RecordingRunner { DefaultStdout = ProbeJson(ProbeLog()) };
        var gateway = Gateway(runner);
        gateway.Deploy(Objects(), new BuildStamp(1));

        Assert.Throws<InvalidOperationException>(() => gateway.Open());
    }

    [Fact]
    public void A_transport_is_opened_only_after_a_deployment_that_was_attempted_AND_loaded()
    {
        var opened = 0;
        var runner = new RecordingRunner { DefaultStdout = LoadedEverything() };
        var gateway = new OpennessDeviceGateway(Options(), runner, () =>
        {
            opened++;
            return new NoTransport();
        });

        gateway.Deploy(Objects(), new BuildStamp(1));
        using var transport = gateway.Open();

        Assert.Equal(1, opened);
    }

    /// <summary>
    /// The narrative carries the command line of every step, so a rig session's failure can be
    /// reproduced from the result alone rather than from scrollback.
    /// </summary>
    [Fact]
    public void The_detail_carries_the_actual_command_lines()
    {
        var runner = new RecordingRunner { DefaultStdout = LoadedEverything() };
        var gateway = Gateway(runner);
        gateway.Deploy(Objects(), new BuildStamp(1));

        Assert.Contains("import-all", gateway.LastRun.Single(s => s.Step.Kind == DeviceStepKind.Import).Step.CommandLineText, StringComparison.Ordinal);
        Assert.Contains("--disruptive", gateway.LastRun.Single(s => s.Step.Kind == DeviceStepKind.Download).Step.CommandLineText, StringComparison.Ordinal);
    }

    private sealed class NoTransport : IRegisterTransport
    {
        public ushort[] ReadHoldingRegisters(int startRegister, int count) => throw new NotSupportedException();

        public void WriteHoldingRegisters(int startRegister, ushort[] values) => throw new NotSupportedException();

        public void Dispose() { }
    }
}
