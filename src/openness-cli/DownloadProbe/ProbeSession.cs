using System;
using System.Collections.Generic;
using System.Linq;
using OpennessCli.Model;
using OpennessCli.Openness;
using Siemens.Engineering;
using Siemens.Engineering.Connection;
using Siemens.Engineering.Download;

namespace DownloadProbe;

/// <summary>The whole run, in the shape the JSON report and the exit code are taken from.</summary>
internal sealed class ProbeOutcome
{
    internal ProbeOutcome(int exitCode, string verdict)
    {
        ExitCode = exitCode;
        Verdict = verdict;
    }

    internal int ExitCode { get; private set; }

    internal string Verdict { get; private set; }

    /// <summary>
    /// Overrides the verdict after the fact. Used for exactly one thing: a configuration the tool
    /// FAILED TO APPLY invalidates whatever the download went on to report, so that finding has to be
    /// able to overrule a "completed" result rather than sit in a footnote beneath it.
    /// </summary>
    internal ProbeOutcome Reclassify(int exitCode, string verdict)
    {
        ExitCode = exitCode;
        Verdict = verdict;
        return this;
    }

    internal string? DevicePath { get; set; }

    internal bool ProviderFound { get; set; }

    internal string? DownloadTarget { get; set; }

    internal IReadOnlyList<RecordedConfiguration> PreDownloadConfigurations { get; set; } = Array.Empty<RecordedConfiguration>();

    internal IReadOnlyList<RecordedConfiguration> PostDownloadConfigurations { get; set; } = Array.Empty<RecordedConfiguration>();

    /// <summary>
    /// Configurations left unanswered ON PURPOSE — the fail-closed guard working. An abort caused by
    /// these is the experiment's result.
    /// </summary>
    internal IReadOnlyList<string> UnhandledConfigurations { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Configurations the policy answered and the API REFUSED. Never merged with the list above:
    /// these mean the tool is broken and the run's result means nothing.
    /// </summary>
    internal IReadOnlyList<string> FailedToApplyConfigurations { get; set; } = Array.Empty<string>();

    internal string? ResultState { get; set; }

    internal int ResultErrorCount { get; set; }

    internal int ResultWarningCount { get; set; }

    internal string? ExceptionText { get; set; }
}

/// <summary>
/// The Portal half: attach, open, plan, resolve the provider, and call <c>Download</c>.
///
/// This is the one type in the repository from which a device write is reachable. It is small and
/// linear on purpose — everything that can be decided without Portal (the path guard, the option
/// literal, the selection policy) was decided before control arrived here, and is unit-tested.
/// </summary>
internal static class ProbeSession
{
    internal static ProbeOutcome Execute(ProbeArguments arguments, ProbeLog log)
    {
        try
        {
            TiaInstallLocator.Resolve(null);
        }
        catch (TiaInstallNotFoundException ex)
        {
            log.Line("ENVIRONMENT: " + ex.Message);
            return new ProbeOutcome(ProbeExitCodes.EnvironmentError, "Siemens.Engineering not found; nothing was attempted.");
        }

        // FI-61: this binary has almost certainly never been approved — its post-build step
        // deliberately does not self-approve (see DownloadProbe.csproj). Say so BEFORE the attach,
        // because an unapproved caller is refused either silently (the connect burns the whole
        // timeout) or with a bare security exception, and neither names the cause.
        var approval = OpennessWhitelist.CheckRunningExecutable();
        var approvalWarning = OpennessWhitelist.DescribeIfNotApproved(
            approval, System.Reflection.Assembly.GetEntryAssembly()?.Location ?? "(unknown)");
        if (approvalWarning is not null)
        {
            log.Blank();
            log.Line(approvalWarning);
        }

        using var gateway = new OpennessGateway();

        try
        {
            log.Blank();
            log.Rule("PORTAL SESSION");
            log.Line($"Connecting (timeout {arguments.ConnectTimeoutSeconds}s)...");
            gateway.Connect(TimeSpan.FromSeconds(arguments.ConnectTimeoutSeconds), arguments.ProjectPath);

            log.Line($"Opening '{arguments.ProjectPath}' (timeout {arguments.OpenTimeoutSeconds}s)...");
            gateway.OpenProject(arguments.ProjectPath, TimeSpan.FromSeconds(arguments.OpenTimeoutSeconds));
            log.Line("Project open.");

            return RunDownload(gateway, arguments, log);
        }
        catch (SafetyContentRefusedException ex)
        {
            log.Blank();
            log.Line("REFUSED (hard rule 2): " + ex.Message);
            return new ProbeOutcome(ProbeExitCodes.SafetyRefused, "Safety content present; download is device-level, so planning one is planning to write it.");
        }
        catch (DeviceNotFoundException ex)
        {
            log.Blank();
            log.Line("NO UNAMBIGUOUS PLC DEVICE: " + ex.Message);
            return new ProbeOutcome(ProbeExitCodes.NoDownloadTarget, "No single PLC device resolved; refusing to guess which device to write to.");
        }
        catch (ConnectTimeoutException ex)
        {
            log.Blank();
            log.Line("CONNECT TIMEOUT: " + ex.Message);
            log.Line("On a binary TIA has not approved this is the SILENT face of the refusal, not a wedged Portal.");
            ExceptionReport.Write(log, "with every inner exception:", ex);
            return new ProbeOutcome(ProbeExitCodes.UnexpectedError, "Portal connect timed out.") { ExceptionText = ex.ToString() };
        }
        catch (ProjectOpenTimeoutException ex)
        {
            log.Blank();
            log.Line("PROJECT OPEN TIMEOUT: " + ex.Message);
            ExceptionReport.Write(log, "with every inner exception:", ex);
            return new ProbeOutcome(ProbeExitCodes.UnexpectedError, "Opening the project timed out.") { ExceptionText = ex.ToString() };
        }
        catch (EngineeringSecurityException ex)
        {
            log.Blank();
            log.Line("SECURITY REFUSAL: " + ex.Message);
            log.Line("This is the THROWN face of an unapproved-binary refusal. Approve this exact exe:");
            log.Line("    tools\\openness-approve-build.ps1 -Exe <path to download-probe.exe>");
            ExceptionReport.Write(log, "with every inner exception:", ex);
            return new ProbeOutcome(ProbeExitCodes.UnexpectedError, "Openness refused this binary.") { ExceptionText = ex.ToString() };
        }
    }

    private static ProbeOutcome RunDownload(OpennessGateway gateway, ProbeArguments arguments, ProbeLog log)
    {
        // The plan first, always. It costs one read and it puts the device, the block counts, the
        // provider-acquisition trail and the configured connection into the same log as the download
        // — so the artifact says what was downloaded to, not just what happened.
        log.Blank();
        log.Rule("DOWNLOAD PLAN (read-only, from openness-cli)");
        var plan = gateway.BuildDownloadPlan(arguments.Device, ToPlanKind(arguments.Options));
        LogPlan(plan, log);

        var target = gateway.ResolveDownloadProviderForExternalProbe(arguments.Device);
        if (target.Provider is null)
        {
            log.Blank();
            log.Line("NO DOWNLOAD PROVIDER: nothing in this device's tree answered GetService<DownloadProvider>().");
            log.Line("The run describes nothing. Empty is not clean.");
            return new ProbeOutcome(ProbeExitCodes.NoProvider, "No DownloadProvider obtainable.")
            {
                DevicePath = target.DevicePath,
            };
        }

        var candidates = ReadConnectionTargets(target.Provider, log);

        var pre = new ConfigurationRecorder(log, "PRE");
        var post = new ConfigurationRecorder(log, "POST");
        var downloadRan = false;

        var outcome = DownloadDispatch.Dispatch(
            candidates,
            arguments.PcInterface,
            arguments.Target,
            log,
            chosen =>
            {
                downloadRan = true;

                log.Blank();
                log.Rule("DOWNLOAD — CONFIGURATION LOG (the artifact)");
                log.Line($"device        : {target.DevicePath}");
                log.Line($"target        : {chosen.Label}");
                log.Line($"options       : {arguments.Options} (Siemens DownloadOptions.{SiemensDownloadOptions.ToSiemens(arguments.Options)})");
                log.Line("policy        : NoAction wherever it exists; otherwise the sole permitted selection;");
                log.Line("                never a denied selection, even if it is the only one offered.");

                var result = Invoke(target.Provider, chosen.Node, pre, post, arguments.Options, log);
                result.DownloadTarget = chosen.Label;
                return result;
            });

        outcome.DevicePath = target.DevicePath;
        outcome.ProviderFound = true;

        if (!downloadRan)
        {
            return outcome;
        }

        outcome.PreDownloadConfigurations = pre.Recorded;
        outcome.PostDownloadConfigurations = post.Recorded;
        outcome.UnhandledConfigurations = pre.RefusedByPolicy.Concat(post.RefusedByPolicy)
            .Concat(pre.NotAnswerable).Concat(post.NotAnswerable)
            .Select(r => $"{r.Phase} #{r.Ordinal} {r.TypeName}: {r.Reason}")
            .ToList();

        var failedToApply = pre.FailedToApply.Concat(post.FailedToApply).ToList();
        outcome.FailedToApplyConfigurations = failedToApply
            .Select(r => $"{r.Phase} #{r.Ordinal} {r.TypeName}: {r.WhyUnanswered}")
            .ToList();

        LogConfigurationSummary(pre, post, log);

        // Precedence, and the reason this is checked HERE rather than only on the abort path: a
        // failed apply invalidates every other reading of the run, including a download that went on
        // to complete. Whatever the exit code was about to be, it becomes this one.
        if (failedToApply.Count > 0)
        {
            log.Blank();
            log.Rule("THIS RUN PROVES NOTHING — A SELECTION COULD NOT BE APPLIED");
            log.Line($"{failedToApply.Count} configuration(s) were answered by the policy and REFUSED by the API:");
            foreach (var record in failedToApply)
            {
                log.Line($"  - [{record.Phase} #{record.Ordinal}] {record.TypeName} — attempted '{record.AttemptedSelection}'");
                log.Line($"      {record.FailureSummary}");
            }

            log.Blank();
            log.Line("This is NOT the fail-closed guard working. The tool intended to answer these and could");
            log.Line("not, so the list of configurations above is a list of what was raised BEFORE the tool");
            log.Line("broke — a configuration raised later could not appear even if it would have been.");

            return outcome.Reclassify(
                ProbeExitCodes.SelectionApplyFailed,
                $"TOOL FAILURE: {failedToApply.Count} selection(s) could not be applied " +
                $"({string.Join(", ", failedToApply.Select(r => $"{r.TypeName}='{r.AttemptedSelection}'"))}). " +
                "The run's result means nothing.");
        }

        return outcome;
    }

    /// <summary>
    /// The call. The ONLY invocation of <c>DownloadProvider.Download</c> in this repository.
    ///
    /// The overload taken is <c>Download(IConfiguration, pre, post, DownloadOptions)</c> — a
    /// connection, two callbacks and an option value. It takes no block, group, selection or
    /// exclusion, which is why "download my one DB" is not a thing this API does: the smallest real
    /// unit is the whole PLC software.
    ///
    /// The <c>IConfiguration</c> passed is the <c>ConfigurationTargetInterface</c> the project
    /// already declares, selected by name — not one this tool built, and not one it wrote back into
    /// the project. That is what keeps the five-argument overload, <c>ConfigurationAddress</c> and
    /// <c>ApplyConfiguration</c> out of this binary entirely.
    /// </summary>
    private static ProbeOutcome Invoke(
        DownloadProvider provider,
        IConfiguration connection,
        ConfigurationRecorder pre,
        ConfigurationRecorder post,
        DownloadOptionChoice options,
        ProbeLog log)
    {
        try
        {
            var result = provider.Download(
                connection,
                configuration => pre.Record(SiemensConfigurationReader.Read(configuration)),
                configuration => post.Record(SiemensConfigurationReader.Read(configuration)),
                SiemensDownloadOptions.ToSiemens(options));

            return ReportResult(result, log);
        }
        catch (EngineeringTargetInvocationException ex)
        {
            return ClassifyAbort(ex, pre, post, log);
        }
        catch (Exception ex)
        {
            log.Blank();
            log.Rule("DOWNLOAD FAILED");
            ExceptionReport.Write(log, "the failure, with every inner exception:", ex);
            log.Blank();
            log.Verbatim("full ToString() : ", ex.ToString());

            // The verdict names the INNERMOST cause, not the outermost wrapper: a verdict of
            // "TargetInvocationException: Exception has been thrown by the target of an invocation"
            // is the same sentence for every failure there has ever been.
            return new ProbeOutcome(ProbeExitCodes.UnexpectedError, ExceptionReport.Summarise(ex))
            {
                ExceptionText = ex.ToString(),
            };
        }
    }

    /// <summary>
    /// Turns the documented abort into a result.
    ///
    /// Openness raises <c>EngineeringTargetInvocationException</c> when a configuration that can
    /// prevent the download is left unhandled — which is precisely what this tool does on purpose to
    /// anything it will not answer. So the abort is the fail-closed guard WORKING, and it gets its
    /// own exit code so a script can tell it from a tool that never started.
    ///
    /// The distinction that keeps that honest: it is only reported as a refusal when something
    /// really was left unhandled. If every configuration was answered and the download threw anyway,
    /// the same exception means something else went wrong, and dressing that up as a successful
    /// refusal would manufacture the most reassuring possible reading of an unexplained failure.
    ///
    /// AND THE SECOND DISTINCTION, WHICH COST A LIVE RUN. A configuration this tool DELIBERATELY left
    /// unanswered and one it FAILED TO ANSWER both leave the download unable to proceed — and used to
    /// produce the same summary, under the same heading, with the same exit code, each line carrying
    /// the POLICY's reason. So a run whose ANSWER line read "FAILED to apply 'NoAction'" was
    /// summarised as "why unhandled : a NoAction selection exists, so it is chosen". They are
    /// opposite findings: the first says the guard held, the second says the tool is broken and the
    /// run means nothing. They are now separated everywhere — heading, section, per-line reason,
    /// verdict and exit code.
    /// </summary>
    internal static ProbeOutcome ClassifyAbort(
        EngineeringTargetInvocationException ex, ConfigurationRecorder pre, ConfigurationRecorder post, ProbeLog log)
    {
        var refused = pre.RefusedByPolicy.Concat(post.RefusedByPolicy).ToList();
        var notAnswerable = pre.NotAnswerable.Concat(post.NotAnswerable).ToList();
        var failed = pre.FailedToApply.Concat(post.FailedToApply).ToList();
        var unanswered = refused.Count + notAnswerable.Count + failed.Count;

        log.Blank();
        log.Rule(failed.Count > 0
            ? "DOWNLOAD ABORTED - THE TOOL FAILED TO ANSWER A CONFIGURATION"
            : unanswered > 0 ? "DOWNLOAD ABORTED - THIS IS A RESULT" : "DOWNLOAD THREW");

        ExceptionReport.Write(log, "the abort, with every inner exception:", ex);

        log.Blank();
        log.Verbatim("full ToString() : ", ex.ToString());

        if (unanswered == 0)
        {
            log.Blank();
            log.Line("NOTE: every configuration raised was answered, so this abort was NOT caused by this");
            log.Line("      tool refusing one. Reported as an unexpected failure, not as a refusal.");
            return new ProbeOutcome(ProbeExitCodes.UnexpectedError, "Download threw with no configuration left unhandled.")
            {
                ExceptionText = ex.ToString(),
            };
        }

        if (failed.Count > 0)
        {
            log.Blank();
            log.Line($"*** {failed.Count} CONFIGURATION(S) THIS TOOL FAILED TO ANSWER — THE TOOL IS BROKEN ***");
            log.Line("    Not a refusal. The policy chose a permitted selection and the API rejected the set,");
            log.Line("    so this abort says nothing about what the download would have done.");
            LogRecords(failed, log);
        }

        if (refused.Count > 0)
        {
            log.Blank();
            log.Line($"{refused.Count} CONFIGURATION(S) REFUSED BY POLICY — THE TOOL WORKED, THE DOWNLOAD WAS PREVENTED:");
            LogRecords(refused, log);
        }

        if (notAnswerable.Count > 0)
        {
            log.Blank();
            log.Line($"{notAnswerable.Count} CONFIGURATION(S) WITH NOTHING TO ANSWER (no CurrentSelection at all):");
            LogRecords(notAnswerable, log);
        }

        if (failed.Count > 0)
        {
            return new ProbeOutcome(
                ProbeExitCodes.SelectionApplyFailed,
                $"TOOL FAILURE: aborted after {failed.Count} selection(s) could not be applied " +
                $"({string.Join(", ", failed.Select(r => $"{r.TypeName}='{r.AttemptedSelection}'"))}). " +
                "The run's result means nothing.")
            {
                ExceptionText = ex.ToString(),
            };
        }

        var byPolicy = refused.Concat(notAnswerable).ToList();
        return new ProbeOutcome(
            ProbeExitCodes.AbortedByUnhandledConfiguration,
            $"Aborted by {byPolicy.Count} unhandled configuration(s): {string.Join(", ", byPolicy.Select(u => u.TypeName))}.")
        {
            ExceptionText = ex.ToString(),
        };
    }

    private static void LogRecords(IReadOnlyList<RecordedConfiguration> records, ProbeLog log)
    {
        foreach (var record in records)
        {
            log.Line($"  - [{record.Phase} #{record.Ordinal}] {record.TypeName}");
            log.Line($"      selections offered : {(record.AvailableSelections.Count == 0 ? "(none)" : string.Join(", ", record.AvailableSelections))}");

            // WhyUnanswered, never Reason: on a failed apply the policy's reason is the sentence
            // that contradicted the ANSWER line three screens above it.
            log.Line($"      why unanswered     : {record.WhyUnanswered}");
            log.Verbatim("      message            : ", record.Message);
        }
    }

    private static ProbeOutcome ReportResult(DownloadResult result, ProbeLog log)
    {
        log.Blank();
        log.Rule("DOWNLOAD RESULT");
        log.Line($"state         : {result.State}");
        log.Line($"errors        : {result.ErrorCount}");
        log.Line($"warnings      : {result.WarningCount}");
        log.Blank();
        log.Line("messages (recursive, verbatim):");
        LogMessages(result.Messages, log, depth: 1);

        var exitCode = result.ErrorCount > 0 ? ProbeExitCodes.CompletedWithErrors : ProbeExitCodes.Completed;
        return new ProbeOutcome(
            exitCode,
            $"Download completed: state={result.State}, errors={result.ErrorCount}, warnings={result.WarningCount}.")
        {
            ResultState = result.State.ToString(),
            ResultErrorCount = result.ErrorCount,
            ResultWarningCount = result.WarningCount,
        };
    }

    private static void LogMessages(DownloadResultMessageComposition? messages, ProbeLog log, int depth)
    {
        if (messages is null || messages.Count == 0)
        {
            log.Line(new string(' ', depth * 2) + "(none)");
            return;
        }

        var indent = new string(' ', depth * 2);
        foreach (DownloadResultMessage message in messages)
        {
            log.Line($"{indent}- state={Safe(() => message.State.ToString())} errors={Safe(() => message.ErrorCount.ToString())} " +
                     $"warnings={Safe(() => message.WarningCount.ToString())} at={Safe(() => message.DateTime.ToString("O"))}");
            log.Verbatim($"{indent}  text : ", Safe(() => message.Message));
            LogMessages(SafeObject(() => message.Messages), log, depth + 1);
        }
    }

    private static void LogPlan(DownloadPlanResult plan, ProbeLog log)
    {
        log.Line($"device        : {plan.DevicePath}");
        log.Line($"granularity   : {plan.Granularity}");
        log.Line($"blocks        : {plan.BlockCount}");
        log.Line($"types         : {plan.TypeCount}");
        log.Line($"inconsistent  : {(plan.InconsistentBlocks.Count == 0 ? "(none)" : string.Join(", ", plan.InconsistentBlocks))}");
        log.Line("provider acquisition trail (every object asked, in order):");
        foreach (var attempt in plan.Provider.Attempts)
        {
            log.Line($"  - {attempt.ObjectPath} [{attempt.ClrType}] -> {attempt.Outcome}");
            if (attempt.Detail is not null)
            {
                log.Line($"      {attempt.Detail}");
            }
        }

        if (plan.Connection is { } connection)
        {
            log.Line($"connection configured : {connection.IsConfigured}");
            log.Line($"configured target addresses : {(connection.AllTargetAddresses.Count == 0 ? "(none)" : string.Join(", ", connection.AllTargetAddresses))}");
            log.Line("  (these are DECLARED addresses read from the project model — no scan was performed,");
            log.Line("   so they say nothing about what is actually at the other end.)");
        }
    }

    private static void LogConfigurationSummary(ConfigurationRecorder pre, ConfigurationRecorder post, ProbeLog log)
    {
        log.Blank();
        log.Rule("CONFIGURATIONS RAISED — SUMMARY");
        log.Line("THE PRIMARY OBSERVATION: which configurations the API raised at all. What was chosen");
        log.Line("does not change that list, and the list is the answer this experiment exists to get.");
        log.Blank();

        foreach (var recorder in new[] { pre, post })
        {
            var records = recorder.Recorded;
            var phase = records.Count > 0 ? records[0].Phase : "(phase)";
            log.Line($"{(records.Count == 0 ? "(none)" : $"{records.Count} raised")} in the {phase} delegate:");
            foreach (var record in records)
            {
                // OUTCOME first, decision second. What the policy decided and what the API did with
                // it are different facts, and only the second one says whether the run means anything.
                log.Line($"  #{record.Ordinal} {record.TypeName} -> {record.Outcome}" +
                         (record.ChosenSelection is null
                             ? $" (nothing applied; policy said {record.Decision})"
                             : $" ('{record.ChosenSelection}' applied; policy said {record.Decision})"));
                if (record.Outcome == ConfigurationOutcomeKind.FailedToApply)
                {
                    log.Line($"       *** attempted '{record.AttemptedSelection}' and the API refused it: {record.FailureSummary}");
                }
            }
        }

        var all = pre.Recorded.Concat(post.Recorded).ToList();
        var reinit = all.Any(r => string.Equals(r.TypeName, "DataBlockReinitialization", StringComparison.Ordinal));
        var failedToApply = all.Any(r => r.Outcome == ConfigurationOutcomeKind.FailedToApply);
        log.Blank();
        log.Line(reinit
            ? "DataBlockReinitialization WAS RAISED on this run."
            : "DataBlockReinitialization WAS NOT RAISED on this run.");

        // "NOT RAISED" after a failed apply is not an observation, it is the absence of one — the
        // download stopped at the configuration the tool could not answer, so nothing after it could
        // be raised whether or not it would have been.
        if (!reinit && failedToApply)
        {
            log.Line("  *** AND THAT IS NOT A FINDING. A selection could not be applied earlier in this run, so");
            log.Line("      the download stopped there. A configuration raised after that point could not appear");
            log.Line("      in this log even if it would have been raised. This run does not answer the question.");
        }

        log.Line("(One run answers nothing on its own — the answer is the DELTA between the phase A and");
        log.Line(" phase B runs. Recorded here so neither log has to be re-read to find it.)");
    }

    /// <summary>
    /// Walks <c>DownloadProvider.Configuration</c> and reports every TARGET INTERFACE the project
    /// declares. Read-only throughout: no <c>ApplyConfiguration</c>, no <c>GetAccessibleDevices</c>.
    ///
    /// The unit is the target interface, not the ADDRESS, and that is the change that made this tool
    /// usable. On the project it was built for, every <c>ConfigurationTargetInterface.Addresses</c>
    /// collection is empty — the human downloads through TIA's "Extended download to device" dialog,
    /// which scans and lets a person pick, and that choice never lands in the project model. Keying
    /// selection on an address therefore found nothing to choose between and refused, correctly, at
    /// exit 5. <c>ConfigurationTargetInterface</c> implements
    /// <c>Siemens.Engineering.Connection.IConfiguration</c> (measured by reflection on the installed
    /// V20 assembly, along with only <c>ConfigurationAddress</c> and <c>ConfigurationAccessibleDevice</c>
    /// — notably NOT <c>ConfigurationPcInterface</c>), so the four-argument <c>Download</c> overload
    /// accepts the interface node itself and the empty address collection is simply not required.
    /// </summary>
    private static IReadOnlyList<ConnectionTarget<IConfiguration>> ReadConnectionTargets(
        DownloadProvider provider, ProbeLog log)
    {
        var candidates = new List<ConnectionTarget<IConfiguration>>();

        log.Blank();
        log.Rule("CONNECTION TARGET");
        var configuration = provider.Configuration;
        if (configuration is null)
        {
            log.Line("DownloadProvider.Configuration is null — the project declares no online path.");
            return candidates;
        }

        foreach (ConfigurationMode mode in configuration.Modes)
        {
            foreach (ConfigurationPcInterface pc in mode.PcInterfaces)
            {
                foreach (ConfigurationTargetInterface targetInterface in pc.TargetInterfaces)
                {
                    var addresses = new List<string>();
                    try
                    {
                        foreach (ConfigurationAddress address in targetInterface.Addresses)
                        {
                            addresses.Add($"{Safe(() => address.Name)} = {Safe(() => address.Address)}");
                        }
                    }
                    catch (Exception ex)
                    {
                        addresses.Add($"<<READ FAILED: {ExceptionReport.Summarise(ex)}>>");
                    }

                    candidates.Add(new ConnectionTarget<IConfiguration>(
                        Safe(() => mode.Name),
                        Safe(() => pc.Name),
                        Safe(() => targetInterface.Name),
                        addresses,
                        targetInterface.GetType().FullName ?? nameof(ConfigurationTargetInterface),
                        targetInterface));
                }
            }
        }

        return candidates;
    }

    private static DownloadOptionKind ToPlanKind(DownloadOptionChoice choice) => choice switch
    {
        DownloadOptionChoice.Software => DownloadOptionKind.Software,
        DownloadOptionChoice.SoftwareOnlyChanges => DownloadOptionKind.SoftwareOnlyChanges,
        DownloadOptionChoice.Hardware => DownloadOptionKind.Hardware,
        _ => throw new ArgumentOutOfRangeException(nameof(choice), choice, "Unhandled download option."),
    };

    private static string Safe(Func<string?> read)
    {
        try
        {
            return read() ?? "(null)";
        }
        catch (Exception ex)
        {
            return $"<<READ FAILED: {ExceptionReport.Summarise(ex)}>>";
        }
    }

    private static T? SafeObject<T>(Func<T?> read)
        where T : class
    {
        try
        {
            return read();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
