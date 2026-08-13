using System.Text;
using Harness.Loop;
using Harness.Map;
using Harness.Wire;
using Ladder.Download;

namespace Harness.Device;

/// <summary>One step that was actually executed, and what it produced.</summary>
public sealed record ExecutedStep(DeviceStep Step, ProcessResult Result, StepReading Reading);

/// <summary>
/// *** THE REAL DEVICE GATEWAY. *** It imports, gates and downloads by invoking the binaries that
/// already do those things, and it is the thing whose absence put eight items on
/// <see cref="LoopResult.OwedOnTheDevice"/>.
///
/// <para><b>What it does NOT do, and the list is the honest part.</b> It does not decide whether a
/// result is good — DB-8 owns that. It does not compute a verdict from the download's
/// <c>State</c> — <c>State</c> was <c>Success</c> on a live run that carried nothing, so the LOAD
/// MANIFEST is the only positive evidence of transfer it will accept. And it does not proceed past a
/// step it could not read: a <see cref="StepVerdict.NotProven"/> stops the deployment exactly as a
/// <see cref="StepVerdict.Failed"/> does, because a gate that did not run is not a gate that
/// passed.</para>
///
/// <para><b>The ordering is the deliverable.</b> Convert, import, RE-ASSERT THE MEMORY LAYOUT OF
/// EVERY DATA BLOCK, gate, download. The layout re-assertion sits where it does because the import is
/// what reverts it — measured, and invisible to every other check in the toolchain.</para>
///
/// <para><b>Every deployment stops the CPU.</b> Download granularity is device-level: there is no
/// per-block download, and the smallest unit Openness offers is the whole PLC software.</para>
/// </summary>
public sealed class OpennessDeviceGateway : IDeviceGateway
{
    private readonly DeviceGatewayOptions _options;
    private readonly IProcessRunner _runner;
    private readonly Func<IRegisterTransport> _openTransport;
    private readonly TimeSpan _stepTimeout;

    private DeploymentOutcome? _lastDeployment;

    public OpennessDeviceGateway(
        DeviceGatewayOptions options,
        IProcessRunner runner,
        Func<IRegisterTransport>? openTransport = null,
        TimeSpan? stepTimeout = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _stepTimeout = stepTimeout ?? TimeSpan.FromMinutes(45);
        _openTransport = openTransport ?? (() =>
            NModbusTransport.Connect(_options.ModbusHost, _options.ModbusPort, _options.ModbusUnitId));
    }

    /// <summary>Every step run by the last <see cref="Deploy"/>, in order. The evidence, not the summary.</summary>
    public IReadOnlyList<ExecutedStep> LastRun { get; private set; } = Array.Empty<ExecutedStep>();

    /// <summary>What the probe reported, when a download was reached at all.</summary>
    public ProbeReport? LastProbeReport { get; private set; }

    /// <summary>The plan the last deployment executed — or refused to.</summary>
    public DeploymentPlan? LastPlan { get; private set; }

    public DeploymentOutcome Deploy(IReadOnlyList<HarnessObject> objects, BuildStamp stamp)
    {
        ArgumentNullException.ThrowIfNull(objects);

        LastRun = Array.Empty<ExecutedStep>();
        LastProbeReport = null;
        _lastDeployment = null;

        var plan = DeploymentPlan.For(objects, _options);
        LastPlan = plan;

        var missing = _options.MissingBinaries;
        if (!plan.Planned || missing.Count > 0)
        {
            return NotAttempted(
                "no deployment was planned, so nothing was written and nothing was downloaded: "
                + string.Join(" | ", plan.Refusals.Concat(missing)));
        }

        try
        {
            StageIr(objects);
        }
        catch (IOException ex)
        {
            return NotAttempted($"the generated IR could not be staged under '{_options.IrStagingDirectory}': {ex.Message}. Nothing was written to the project.");
        }

        var executed = new List<ExecutedStep>();
        foreach (var step in plan.Steps)
        {
            var result = _runner.Run(step.Executable, step.Arguments, _stepTimeout);
            var reading = DeviceExitCodes.Interpret(step.Kind, result);
            executed.Add(new ExecutedStep(step, result, reading));
            LastRun = executed;

            if (reading.Verdict == StepVerdict.Ok)
                continue;

            // The download is the only step whose failure is still an ATTEMPT: the CPU may have been
            // stopped and part of the program may be on it, and reporting that as "nothing happened"
            // is the more dangerous direction.
            if (step.Kind == DeviceStepKind.Download)
            {
                LastProbeReport = ProbeReport.FromStdout(result.StandardOutput, ReadLogFile);
                return Attempted(false, LastProbeReport.Manifest, plan, executed, LastProbeReport);
            }

            return NotAttempted(
                $"the deployment stopped at {step.Kind} ({reading.Verdict}) and NO DOWNLOAD WAS ATTEMPTED. {reading.Reason}"
                + Environment.NewLine + "  command: " + step.CommandLineText,
                executed, plan);
        }

        var download = executed[^1];
        LastProbeReport = ProbeReport.FromStdout(download.Result.StandardOutput, ReadLogFile);

        var loaded = IsLoaded(plan, LastProbeReport, out var loadedDetail);
        var outcome = Attempted(loaded, LastProbeReport.Manifest, plan, executed, LastProbeReport, loadedDetail);
        _lastDeployment = outcome;
        return outcome;
    }

    /// <summary>
    /// Open the Modbus transport onto the mirror.
    ///
    /// <para>Refuses unless the last deployment was attempted AND loaded — the same refusal the
    /// default gateway makes, for the same reason: a wave run against a mirror nothing maintains
    /// would agree with itself perfectly.</para>
    /// </summary>
    public IRegisterTransport Open()
    {
        if (_lastDeployment is not { Attempted: true, Loaded: true })
        {
            throw new InvalidOperationException(
                "no transport is opened: the last deployment was not both attempted and loaded"
                + (_lastDeployment is null ? " (nothing has been deployed by this gateway)." : $" ({_lastDeployment.Detail})."));
        }

        return _openTransport();
    }

    public void Dispose() { }

    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// <b>Loaded means every DOWNLOADABLE object is named in the manifest</b>, and the verdict is
    /// <c>Transferred</c>.
    ///
    /// <para>Tag tables are excluded because a PLC tag table carries no load message — the one
    /// measured 19-object manifest on this rig lists an FC, an FB, its iDB, OB1, <c>MB_SERVER</c> and
    /// nine <c>TCP_MB_*</c> helpers, and no tag table. A comparison that demanded one would report
    /// <c>Absent</c> on every healthy download.</para>
    ///
    /// <para><c>NothingTransferred</c> is NOT loaded here even though it is a positive statement of
    /// success: it means the controller already held this build, and this loop's build stamp changes
    /// with every turn, so an up-to-date answer means the download did not carry what was generated.</para>
    /// </summary>
    private static bool IsLoaded(DeploymentPlan plan, ProbeReport report, out string detail)
    {
        if (!report.ManifestAvailable)
        {
            detail = "no load manifest could be recovered, so transfer is not positively evidenced. §9c forbids inferring it from the absence of a failure.";
            return false;
        }

        if (report.Verdict != TransferVerdict.Transferred)
        {
            detail = report.Verdict == TransferVerdict.NothingTransferred
                ? "TIA stated it transferred nothing because the target was already up to date. Every turn of this loop generates a new build stamp, so an up-to-date answer means what was generated is NOT what is running."
                : "the transfer verdict is Undetermined: nothing whatever is known about what the device now holds, and it must be read before it is trusted.";
            return false;
        }

        var absent = plan.DownloadableObjects
            .Select(o => o.Name)
            .Where(name => !report.Manifest.Contains(name))
            .ToArray();

        if (absent.Length > 0)
        {
            detail = $"the download transferred {report.Manifest.Count} object(s), and {absent.Length} of the objects supplied are not among them: {string.Join(", ", absent)}. "
                + "A result read after this would be about whatever WAS on the device.";
            return false;
        }

        detail = $"every one of the {plan.DownloadableObjects.Count} downloadable object(s) supplied appears in the device's own load manifest of {report.Manifest.Count} (source: {report.Source}).";
        return true;
    }

    private void StageIr(IReadOnlyList<HarnessObject> objects)
    {
        Directory.CreateDirectory(_options.IrStagingDirectory);
        Directory.CreateDirectory(_options.XmlStagingDirectory);
        Directory.CreateDirectory(_options.ResolvedProbeLogDirectory);

        // The XML staging directory is handed to import-all AS A DIRECTORY, so a stale file left by a
        // previous turn would be imported alongside this turn's. Cleared rather than overwritten.
        foreach (var stale in Directory.EnumerateFiles(_options.XmlStagingDirectory, "*.xml"))
            File.Delete(stale);

        foreach (var obj in objects)
            File.WriteAllText(Path.Combine(_options.IrStagingDirectory, obj.Name + ".ir"), obj.Ir);
    }

    private static string? ReadLogFile(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private DeploymentOutcome NotAttempted(string detail, IReadOnlyList<ExecutedStep>? executed = null, DeploymentPlan? plan = null) =>
        new(Attempted: false, Loaded: false, new HashSet<string>(StringComparer.Ordinal),
            detail + Narrative(executed, plan));

    private DeploymentOutcome Attempted(
        bool loaded, IReadOnlySet<string> manifest, DeploymentPlan plan,
        IReadOnlyList<ExecutedStep> executed, ProbeReport report, string? loadedDetail = null)
    {
        var head = loaded
            ? "the download completed and the device's own load manifest accounts for every object supplied."
            : "the download was ATTEMPTED and the objects are not accounted for on the device. This is NOT 'nothing happened': the CPU may be stopped and part of the program may be on it.";

        return new DeploymentOutcome(
            Attempted: true, Loaded: loaded, manifest,
            head
            + (loadedDetail is null ? string.Empty : " " + loadedDetail)
            + " " + report.Detail
            + Narrative(executed, plan));
    }

    private string Narrative(IReadOnlyList<ExecutedStep>? executed, DeploymentPlan? plan)
    {
        var sb = new StringBuilder();
        sb.AppendLine().Append("  project: ").Append(_options.ProjectPath);

        if (plan is not null)
            sb.AppendLine().Append("  layout: ").Append(plan.LayoutNote);

        if (executed is null)
            return sb.ToString();

        foreach (var step in executed)
        {
            sb.AppendLine()
              .Append("  [").Append(step.Reading.Verdict.ToString().ToUpperInvariant()).Append("] ")
              .Append(step.Step.Kind).Append(" — ").Append(step.Reading.Reason)
              .AppendLine().Append("      ").Append(step.Step.CommandLineText);
        }

        return sb.ToString();
    }
}
