using System;
using System.Collections.Generic;
using System.Linq;
using OpennessCli.Model;
using OpennessCli.Openness;
using Siemens.Engineering;
using Siemens.Engineering.Connection;
using Siemens.Engineering.Download;

namespace DownloadProbe;

/// <summary>
/// Where a download wrote. Two overloads, two destinations, and *** THE DIFFERENCE IS NOT VISIBLE IN
/// THE RESULT'S MESSAGE TEXT *** — which is exactly how a folder run came to report that the software
/// had been loaded.
/// </summary>
internal enum DownloadDestination
{
    /// <summary>A real device, via <c>Download(IConfiguration, pre, post, DownloadOptions)</c>.</summary>
    Controller,

    /// <summary>
    /// A directory, via <c>Download(DirectoryInfo, delegate)</c>. No connection is passed, no device
    /// is selected, nothing goes on the wire. A result IS produced and it DOES name objects.
    /// </summary>
    Folder,
}

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

    /// <summary>
    /// *** WAS ANYTHING ACTUALLY TRANSFERRED? *** Never null on a run that reached a download: an
    /// abort or a throw sets <see cref="TransferVerdicts.NoResult"/> rather than leaving this blank,
    /// because a missing answer and a negative answer must not look alike.
    /// </summary>
    internal TransferVerdict? Transfer { get; set; }

    /// <summary>
    /// *** THE LOAD MANIFEST — WHAT WAS REPORTED LOADED, BY NAME. *** Derived by
    /// <c>Ladder.Download</c> from the live <c>DownloadResult</c> message tree, never from this
    /// tool's rendering of it, and emitted as first-class JSON so a consumer never has to scrape the
    /// log for it.
    ///
    /// NULL MEANS NO DOWNLOAD RESULT EXISTED — an abort, a throw, or a folder run. Empty is not
    /// clean: null and "a manifest naming nothing" are different findings and must never render
    /// alike, which is why this is nullable rather than an empty feedback object.
    /// </summary>
    internal Ladder.Download.DownloadFeedback? Feedback { get; set; }

    /// <summary>
    /// WHICH AUTHORITY PRODUCED <see cref="Feedback"/>. <c>DownloadResultAdapter</c> is first-hand —
    /// the live Openness objects; <c>ProbeLogReader</c> re-derives from a RENDERING and is forensics
    /// only. Reported rather than assumed, because a consumer that cannot tell them apart cannot tell
    /// a first-hand answer from a second-hand one, and this binary is the only place that can ever
    /// hold the first-hand one.
    /// </summary>
    internal string? FeedbackSource { get; set; }

    /// <summary>
    /// *** WHERE THIS RUN WROTE — AND THE FIELD A "WAS IT LOADED?" CHECK MUST KEY ON. ***
    ///
    /// Null means no download ran at all. <see cref="DownloadDestination.Folder"/> means the image
    /// went to a directory and NO CONTROLLER WAS CONTACTED, however many objects the result names.
    ///
    /// This exists because the run that proved it necessary reported <c>Transferred</c>, 27 objects,
    /// and *"YES — THE SOFTWARE WAS LOADED"* — for a folder run. The destination is a fact about the
    /// overload that was called, so it is carried as data rather than re-derived from message text
    /// that cannot possibly express it.
    /// </summary>
    internal DownloadDestination? Destination { get; set; }

    /// <summary>
    /// The last thing printed, after the verdict. Carries the CPU-run-state advisory — the sentence
    /// someone has to read before walking away from a rig whose CPU may be stopped.
    /// </summary>
    internal IReadOnlyList<string> ClosingLines { get; set; } = Array.Empty<string>();
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

        if (arguments.ToFolder is not null)
        {
            return DownloadToFolder(target.Provider, target.DevicePath, arguments, log);
        }

        var candidates = ReadConnectionTargets(target.Provider, log);

        if (arguments.Disruptive)
        {
            // Printed only on the path that can stop the CPU, because it is the answer to the
            // question that path raises: is there any route back to RUN other than StartModules?
            log.Blank();
            log.Rule("ONLINE-MODE SURVEY (reflection only — nothing acquired, nothing invoked)");
            log.Block(OnlineModeSurvey.Describe(typeof(DownloadProvider).Assembly));
        }

        var pre = new ConfigurationRecorder(log, "PRE", arguments.PolicyMode);
        var post = new ConfigurationRecorder(log, "POST", arguments.PolicyMode);
        var injection = new ThrowInjection(arguments.InjectThrow);
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
                log.Line($"policy        : {arguments.PolicyMode}");
                if (arguments.Disruptive)
                {
                    log.Line("                the disruptive allowance FIRST (" + NoActionFirstPolicy.DisruptiveAllowanceSummary + ");");
                    log.Line("                then NoAction wherever it exists; then the sole permitted selection;");
                    log.Line("                and NEVER " + NoActionFirstPolicy.DisruptiveDenySummary + ".");
                }
                else
                {
                    log.Line("                NoAction wherever it exists; otherwise the sole permitted selection;");
                    log.Line("                never a denied selection, even if it is the only one offered.");
                }

                var result = Invoke(target.Provider, chosen.Node, pre, post, arguments.Options, log, injection);
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

        // A download that aborted or threw produced no DownloadResult, so the transfer question has
        // no answer rather than a negative one. Named explicitly here so that every run which reached
        // a download carries a verdict, and "we never found out" is never rendered as "nothing moved".
        outcome.Transfer ??= TransferVerdicts.NoResult(
            "The download did not return a DownloadResult — it aborted or threw; see the section above.");

        // The advisory closes the run whatever else happened, including after a failure: a tool
        // failure that occurred AFTER StopModules -> StopAll was answered still leaves a stopped CPU.
        outcome.ClosingLines = RunStateAdvisory.Describe(
            arguments.PolicyMode, pre.Recorded.Concat(post.Recorded).ToList());
        log.Blank();
        log.Block(outcome.ClosingLines);

        // THE PRECEDENCE CHAIN IS DECIDED IN ONE PLACE, AND THAT PLACE IS TESTABLE (2026-08-13).
        // Both branches below act on this single verdict rather than each deciding for itself — which
        // is how the injection arm came to be missing entirely: the decision lived inline in a method
        // that cannot run without Portal, a device and a download, so nothing could assert it.
        var verdict = ProbeVerdict.Decide(failedToApply.Count, injection.IsArmed, injection.Fired is not null);

        // Precedence, and the reason this is checked HERE rather than only on the abort path: a
        // failed apply invalidates every other reading of the run, including a download that went on
        // to complete. Whatever the exit code was about to be, it becomes this one.
        if (verdict == ProbeExitCodes.SelectionApplyFailed)
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

            // The transfer headline rides along even here. "The run proves nothing" is a statement
            // about the EXPERIMENT; whether a program reached the controller is a fact about the
            // DEVICE, and someone standing at a rig needs it whether or not the run was valid.
            return outcome.Reclassify(
                ProbeExitCodes.SelectionApplyFailed,
                $"TOOL FAILURE: {failedToApply.Count} selection(s) could not be applied " +
                $"({string.Join(", ", failedToApply.Select(r => $"{r.TypeName}='{r.AttemptedSelection}'"))}). " +
                $"The run's result means nothing.  {outcome.Transfer?.Headline}");
        }

        // 🔴 THE GUARD THAT DID NOT FIRE, IN THE BINARY WRITTEN TO CARRY IT (fixed 2026-08-13).
        //
        // Measured on a real run: --throw-from-post-delegate was ARMED, the download raised ZERO
        // post configurations, the delegate was NEVER INVOKED, the download completed — AND THE TOOL
        // EXITED 0. Its own contract says 12 means "armed and the delegate was never invoked". The
        // lane caught it only by reading the configuration list; the exit code said everything was
        // fine. That is "if you get zero configurations you have not tested what you think you are
        // testing" failing in the one program written to enforce it.
        //
        // The cause was a missing arm, not a wrong one: the completion path checked `Fired is not
        // null` and had no branch for armed-and-never-fired at all, so the case fell through to the
        // ordinary result. Empty is not clean — an experiment that did not run is not a pass.
        //
        // WHY IT MATTERS BEYOND THE EXIT CODE: the raised-configuration set DEPENDS ON --options.
        // Forcing `--options Software` made the download stop the CPU, and the post delegate was then
        // reached. So "the delegate was never invoked" is a real, option-dependent finding about the
        // API, and 12 is exactly the code that should have reported it.
        //
        // Placed AFTER the failedToApply block deliberately: a failed apply means the run proves
        // nothing at all, which is a stronger statement than "this particular experiment did not
        // run", so it keeps precedence.
        if (verdict == ProbeExitCodes.InjectionNeverFired)
        {
            log.Blank();
            log.Rule("*** THE INJECTION WAS ARMED AND NEVER FIRED — NOTHING WAS LEARNED ***");
            log.Line($"--throw-from-{injection.Phase.ToString()!.ToLowerInvariant()}-delegate was given and that delegate was");
            log.Line("NEVER INVOKED, so no exception was thrown. This run says nothing about what Openness");
            log.Line("does with one, whatever else it reports below.");
            log.Line($"{injection.Phase.ToString()!.ToUpperInvariant()} configurations raised on this run: " +
                     $"{(injection.Phase == DelegatePhase.Pre ? pre.Recorded.Count : post.Recorded.Count)}.");
            log.Line("The set of configurations a download raises DEPENDS ON --options; a different option value");
            log.Line("may reach this delegate where this one did not.");

            return outcome.Reclassify(
                ProbeExitCodes.InjectionNeverFired,
                $"INJECTION NEVER FIRED: the {injection.Phase} delegate was armed and never invoked, so the " +
                $"experiment did not run. {outcome.Transfer?.Headline}");
        }

        return outcome;
    }

    /// <summary>
    /// *** THE COMPILE THE DOWNLOAD RUNS, WITHOUT THE DOWNLOAD. ***
    ///
    /// Calls <c>DownloadProvider.Download(DirectoryInfo, DownloadConfigurationDelegate)</c> — the
    /// folder overload. No connection is passed and none is opened; nothing goes on the wire and no
    /// CPU is stopped, so the scratch-rig fence is not in play here at all.
    ///
    /// It exists because a download COMPILES FIRST, and that compile is demonstrably not any of the
    /// four this repository already runs: on 2026-08-13 the per-block compile, the device compile,
    /// <c>compile-all --force</c> and <c>sanity-check</c> all reported clean while the download's
    /// own compile failed with *"The following blocks could not be compiled: FC_ModbusTCP_Sample
    /// [FC8]"*. Until this mode existed, reaching that compile at all meant writing to a controller.
    ///
    /// THE EXCEPTION IS THE PRODUCT. On failure Openness throws with a one-sentence message that
    /// omits the block entirely, so everything obtainable is dumped verbatim — every detail message,
    /// every inner exception — rather than summarised. If the block-level detail is anywhere in this
    /// object, this is where it shows up; if it is nowhere, this is the evidence for saying so.
    /// </summary>
    private static ProbeOutcome DownloadToFolder(
        DownloadProvider provider, string? devicePath, ProbeArguments arguments, ProbeLog log)
    {
        var directory = new System.IO.DirectoryInfo(arguments.ToFolder!);

        log.Blank();
        log.Rule("DOWNLOAD TO FOLDER — NON-DESTRUCTIVE (no device, no wire, no CPU stop)");
        log.Line($"device        : {devicePath}");
        log.Line($"folder        : {directory.FullName}");
        log.Line($"options       : (the folder overload takes NO DownloadOptions — '{arguments.Options}' does not apply here)");
        log.Line($"policy        : {arguments.PolicyMode}");
        log.Line("overload      : Download(DirectoryInfo, DownloadConfigurationDelegate) — one callback, not two.");
        log.Blank();
        log.Line("WHY THIS RUN EXISTS: a download COMPILES the program before it transfers anything, and that");
        log.Line("compile has been measured to FAIL on a project where every compile this repo runs reported");
        log.Line("clean. This reaches that compile without reaching the controller.");

        var pre = new ConfigurationRecorder(log, "PRE", arguments.PolicyMode);
        var injection = new ThrowInjection(arguments.InjectThrow);

        try
        {
            var result = provider.Download(
                directory,
                configuration =>
                {
                    var recorded = pre.Record(SiemensConfigurationReader.Read(configuration));

                    // THE REHEARSAL ROUTE. This delegate is measurably reached on the folder path —
                    // a folder run raises ConsistentBlocksDownload and AlarmTextLibrariesDownload
                    // here — so the whole PRE throw can be exercised with nothing on the wire and no
                    // CPU to strand. There is no post delegate on this overload, which is why
                    // --throw-from-post-delegate is refused with --to-folder rather than silently
                    // arming something that can never fire.
                    injection.MaybeThrow(DelegatePhase.Pre, recorded.TypeName, log);
                });

            if (injection.Fired is not null)
            {
                return ReportInjection(
                    injection,
                    thrown: null,
                    log,
                    ReportResult(result, log, DownloadDestination.Folder, directory.FullName));
            }

            // 🔴 THE OVERRIDE THAT USED TO SIT BELOW THIS LINE IS GONE, AND ITS ABSENCE IS THE FIX.
            //
            // It reassigned `reported.Transfer` AFTER ReportResult had already rendered the run's
            // verdict SENTENCE from the old value — so the object said "undetermined, nothing was
            // transferred by construction" while the sentence beside it, the log section above it and
            // the manifest below it all said the software had been loaded. One document, three
            // answers, measured on the first live rehearsal (27 objects, `Transferred`, exit 0).
            //
            // Passing the destination INTO ReportResult fixes all three at once, because all three
            // are rendered from the one verdict. A post-hoc correction can only ever fix the copy it
            // reaches.
            var reported = ReportResult(result, log, DownloadDestination.Folder, directory.FullName);
            reported.DevicePath = devicePath;
            reported.ProviderFound = true;
            reported.DownloadTarget = directory.FullName;
            reported.PreDownloadConfigurations = pre.Recorded;

            LogConfigurationSummary(pre, new ConfigurationRecorder(log, "POST", arguments.PolicyMode), log);

            if (injection.IsArmed)
            {
                return ReportInjection(injection, thrown: null, log, reported);
            }

            return reported;
        }
        catch (Exception ex) when (injection.Fired is not null)
        {
            LogConfigurationSummary(pre, new ConfigurationRecorder(log, "POST", arguments.PolicyMode), log);
            return ReportInjection(injection, ex, log, fallback: null);
        }
        catch (EngineeringTargetInvocationException ex)
        {
            log.Blank();
            log.Rule("*** FOLDER DOWNLOAD THREW — AND THIS IS THE INTERESTING CASE ***");
            log.Line("A folder download stops the CPU of nothing and touches no network. So a throw here is");
            log.Line("almost certainly the COMPILE refusing, which is exactly what this mode was built to catch.");
            ExceptionReport.Write(log, "the failure, with every detail message:", ex);
            log.Blank();
            log.Verbatim("full ToString() : ", ex.ToString());
            log.Blank();
            log.Line("READ THE ABOVE FOR A BLOCK NAME. If the message names no block, then the block-level detail");
            log.Line("TIA shows in its own Info -> Compile tab is NOT in this exception, and no amount of dumping");
            log.Line("will produce it — that is a finding about the API, and it is the one to report.");
            LogConfigurationSummary(pre, new ConfigurationRecorder(log, "POST", arguments.PolicyMode), log);

            return new ProbeOutcome(ProbeExitCodes.UnexpectedError, ExceptionReport.Summarise(ex))
            {
                DevicePath = devicePath,
                ProviderFound = true,
                DownloadTarget = directory.FullName,
                ExceptionText = ex.ToString(),
                PreDownloadConfigurations = pre.Recorded,
                Transfer = TransferVerdicts.NoResult("Nothing was transferred to a device: this was a folder run, and it threw."),
                Destination = DownloadDestination.Folder,
            };
        }
        catch (Exception ex)
        {
            log.Blank();
            log.Rule("FOLDER DOWNLOAD FAILED");
            ExceptionReport.Write(log, "the failure, with every inner exception:", ex);
            log.Blank();
            log.Verbatim("full ToString() : ", ex.ToString());
            return new ProbeOutcome(ProbeExitCodes.UnexpectedError, ExceptionReport.Summarise(ex))
            {
                DevicePath = devicePath,
                ProviderFound = true,
                ExceptionText = ex.ToString(),
                Transfer = TransferVerdicts.NoResult("Nothing was transferred to a device: this was a folder run, and it threw."),
                Destination = DownloadDestination.Folder,
            };
        }
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
        ProbeLog log,
        ThrowInjection injection)
    {
        try
        {
            var result = provider.Download(
                connection,
                configuration =>
                {
                    var recorded = pre.Record(SiemensConfigurationReader.Read(configuration));
                    injection.MaybeThrow(DelegatePhase.Pre, recorded.TypeName, log);
                },
                configuration =>
                {
                    var recorded = post.Record(SiemensConfigurationReader.Read(configuration));
                    injection.MaybeThrow(DelegatePhase.Post, recorded.TypeName, log);
                },
                SiemensDownloadOptions.ToSiemens(options));

            // Reached AFTER an armed injection fired = the API swallowed it. Classified before the
            // ordinary result reporting, because "the download completed" means something entirely
            // different once we know a callback threw and was ignored.
            if (injection.Fired is not null)
            {
                return ReportInjection(injection, thrown: null, log, ReportResult(result, log, DownloadDestination.Controller));
            }

            return ReportResult(result, log, DownloadDestination.Controller);
        }
        catch (Exception ex) when (injection.Fired is not null)
        {
            // The injection path owns any exception once it has fired: attributing a deliberate
            // failure to the project or the device is the one outcome this feature must never
            // produce.
            return ReportInjection(injection, ex, log, fallback: null);
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
    /// EXPERIMENT 1.7's answer, written so that nobody reading this log later can mistake it for a
    /// defect report. Says "deliberate" in the rule, in the first line and in the verdict.
    /// </summary>
    private static ProbeOutcome ReportInjection(
        ThrowInjection injection, Exception? thrown, ProbeLog log, ProbeOutcome? fallback)
    {
        var (outcome, detail) = injection.Classify(thrown);

        // Armed and never invoked. Empty is not clean: this is not a pass, and it is not a failure of
        // the download either — the experiment simply did not run, and the exit code says so.
        if (injection.Fired is null)
        {
            log.Blank();
            log.Rule("*** THE INJECTION WAS ARMED AND NEVER FIRED — NOTHING WAS LEARNED ***");
            log.Line($"--throw-from-{injection.Phase.ToString()!.ToLowerInvariant()}-delegate was given and that delegate was NEVER INVOKED,");
            log.Line("so no exception was thrown and this run answers nothing about what Openness does with one.");
            log.Line("This is NOT a clean run of the experiment. Check the configuration summary above:");
            log.Line("if the delegate raised nothing, there was nothing to throw from.");

            var never = new ProbeOutcome(
                ProbeExitCodes.InjectionNeverFired,
                $"INJECTION NEVER FIRED ({injection.Phase} delegate was not invoked). The experiment did not run.");
            never.Transfer = fallback?.Transfer
                ?? TransferVerdicts.NoResult("No DownloadResult, and the armed injection never fired.");
            return never;
        }

        log.Blank();
        log.Rule("*** DELIBERATE TEST INJECTION — THE FAILURE BELOW WAS CAUSED BY THIS TOOL ***");
        log.Line("EXPERIMENT 1.7. A DeliberateProbeInjectionException was thrown out of the");
        log.Line($"{injection.Fired!.Phase.ToString().ToUpperInvariant()} download-configuration delegate ON PURPOSE. Nothing here is evidence");
        log.Line("about the project, the program or the controller.");
        log.Blank();
        log.Line($"injected from : {injection.Fired.Phase.ToString().ToUpperInvariant()} delegate, invocation #{injection.Fired.Ordinal}");
        log.Line($"after answering: {injection.Fired.ConfigurationType}");
        log.Blank();
        log.Line($"*** WHAT OPENNESS DID WITH IT: {outcome.ToString().ToUpperInvariant()} ***");
        log.Line($"    {detail}");

        // Classified on OBJECT IDENTITY — is our exception in the chain, at what depth — never on a
        // state or a message string. A classifier that reads text changes meaning the day Siemens
        // rewords something, and that lesson is one day old here.
        log.Blank();
        log.Line("(Classified by object identity against the exception this tool threw, not by reading");
        log.Line(" any state or message text.)");

        if (thrown is not null)
        {
            log.Blank();
            ExceptionReport.Write(log, "what came back out of Download, with every inner exception:", thrown);
            log.Blank();
            log.Verbatim("full ToString() : ", thrown.ToString());
        }
        else
        {
            log.Blank();
            log.Line("Download RETURNED NORMALLY. Its result is reported above — read it knowing that a");
            log.Line("callback threw during it and the API did not care.");
        }

        var verdict = $"DELIBERATE INJECTION ({injection.Fired.Phase.ToString().ToUpperInvariant()} delegate): Openness {outcome} it. {detail}";
        var exitCode = outcome == InjectionOutcome.NeverFired
            ? ProbeExitCodes.InjectionNeverFired
            : ProbeExitCodes.InjectedThrowFired;

        var result = new ProbeOutcome(exitCode, verdict)
        {
            ExceptionText = thrown?.ToString(),
        };

        // The transfer question still gets an answer, and it has to be the honest one: on a swallow
        // the download really did complete, and somebody standing at a rig needs that fact more than
        // they need the experiment's tidiness.
        result.Transfer = fallback?.Transfer
            ?? TransferVerdicts.NoResult(
                "The download did not return a DownloadResult — this tool threw from inside its own callback, " +
                "deliberately. Nothing here says whether anything reached the controller.");

        return result;
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

    /// <summary>
    /// The result, and then THE QUESTION: was anything actually transferred?
    ///
    /// <c>State</c> and the counts do not answer it and are not allowed to look as though they do —
    /// the one live run that returned <c>Success</c> with <c>ErrorCount</c> 0 had loaded nothing, and
    /// said so only in a message. So the message tree is read into a form
    /// <see cref="TransferVerdicts"/> can classify, and the verdict goes into the run's own verdict
    /// line rather than into a footnote below a green result.
    /// </summary>
    /// <param name="destination">
    /// *** REQUIRED, AND DELIBERATELY NOT OPTIONAL. *** A default would be a guess about whether a
    /// controller was contacted, and the wrong guess is the defect this parameter exists to close: a
    /// folder run's result names loaded objects exactly like a device run's, so a caller that forgets
    /// to say where it wrote must fail to COMPILE rather than fall back to "controller".
    /// </param>
    /// <param name="folder">The image directory, on the folder path. Null on the device path.</param>
    private static ProbeOutcome ReportResult(
        DownloadResult result, ProbeLog log, DownloadDestination destination, string? folder = null)
    {
        log.Blank();
        log.Rule("DOWNLOAD RESULT");
        log.Line($"state         : {result.State}");
        log.Line($"errors        : {result.ErrorCount}");
        log.Line($"warnings      : {result.WarningCount}");
        log.Line("              (NONE of those three says whether anything was TRANSFERRED — see below.)");
        log.Blank();
        log.Line("messages (recursive, verbatim):");

        var messages = ReadMessages(result.Messages);
        LogMessages(messages, log, depth: 1);

        var resultState = Safe(() => result.State.ToString());

        // THE FEEDBACK IS BUILT FIRST AND USED FOR BOTH, so the verdict and the manifest can never be
        // two readings of the same download. Before 2026-08-14 the verdict came from this file's own
        // phrase search and the manifest from Ladder.Download — one input, two rules.
        var feedback = BuildFeedback(resultState, result.ErrorCount, result.WarningCount, messages);
        var transfer = ClassifyTransfer(destination, folder, resultState, feedback);

        log.Blank();
        log.Rule("WAS ANYTHING ACTUALLY TRANSFERRED?");
        log.Line(transfer.Headline);
        log.Block(transfer.Evidence);
        log.Blank();
        log.Block(TransferVerdicts.DescribeRunState(messages));

        log.Blank();
        log.Rule(destination == DownloadDestination.Folder
            ? "IMAGE CONTENTS — NOT A LOAD MANIFEST (this run wrote to a folder)"
            : "LOAD MANIFEST (Ladder.Download, from the live result — not from this log)");

        if (destination == DownloadDestination.Folder)
        {
            log.Line("*** READ THE HEADINGS INSIDE THIS BLOCK AS BEING ABOUT THE IMAGE. *** The parser below");
            log.Line("says TRANSFERRED because objects are named; they were written to a DIRECTORY. No");
            log.Line("controller was contacted and none was even selected. In the JSON these facts appear");
            log.Line("under `image`, and the device-facing manifest is `available: false`.");
            log.Blank();
        }

        log.Block(feedback.ToReport().Replace("\r\n", "\n").TrimEnd('\n').Split('\n'));
        log.Line("(Emitted as first-class JSON under --json's `loadManifest`. A consumer reads THAT,");
        log.Line(" never this rendering — anything a renderer drops is gone before a scraper sees it.)");

        var exitCode = result.ErrorCount > 0 ? ProbeExitCodes.CompletedWithErrors : ProbeExitCodes.Completed;
        return new ProbeOutcome(
            exitCode,
            $"Download completed: state={result.State}, errors={result.ErrorCount}, " +
            $"warnings={result.WarningCount}.  {transfer.Headline}")
        {
            ResultState = resultState,
            ResultErrorCount = result.ErrorCount,
            ResultWarningCount = result.WarningCount,
            Transfer = transfer,
            Feedback = feedback,
            FeedbackSource = nameof(Ladder.Download.DownloadResultAdapter),
            Destination = destination,
        };
    }

    /// <summary>
    /// *** THE DESTINATION DECIDES THIS, NOT THE MESSAGE TEXT — AND THAT IS THE WHOLE FIX. ***
    ///
    /// On the folder path the question "did anything reach a controller" is answered by which
    /// overload was called, and the answer is no — whatever objects the message tree names.
    /// <see cref="TransferVerdicts.Classify"/> reads message text and therefore CANNOT tell a folder
    /// run from a device run: the recorded rehearsal's tree names 27 objects as loaded, exactly as a
    /// real download's would. Asking it to decide was the defect.
    ///
    /// EXTRACTED so the rule is reachable without Portal. A live <c>DownloadResult</c> cannot be
    /// constructed in a test, so while this ternary lived inline inside
    /// <see cref="ReportResult"/> the only thing a test could do was restate it — and the first
    /// version of the regression test did exactly that, setting the verdict it then asserted.
    /// </summary>
    internal static TransferVerdict ClassifyTransfer(
        DownloadDestination destination,
        string? folder,
        string resultState,
        Ladder.Download.DownloadFeedback feedback) =>
        destination == DownloadDestination.Folder
            ? TransferVerdicts.ImageOnly(folder ?? "(unnamed directory)")
            : TransferVerdicts.FromFeedback(feedback, resultState);

    /// <summary>
    /// *** THE LIVE PATH <c>DownloadResultAdapter</c> WAS BUILT FOR, AND THIS IS ITS FIRST CALLER. ***
    ///
    /// The projection is taken from the nodes this tool already read off the Siemens objects rather
    /// than from <c>DownloadResult</c> a second time, for one reason: <see cref="ReadMessages"/>
    /// guards every property read and RECORDS A FAILURE AS A NODE. Re-reading here would either
    /// duplicate that care or drop it, and a dropped message reads downstream as "nothing said that".
    ///
    /// The delegates are where the guess about Siemens' member names belongs — in the caller, where
    /// it fails to compile if it is wrong. That guess has already been made and survived a live run,
    /// in <see cref="ReadMessages"/>; this method only re-shapes what it produced.
    /// </summary>
    private static Ladder.Download.DownloadFeedback BuildFeedback(
        string? resultState, int errorCount, int warningCount, IReadOnlyList<DownloadMessageNode> messages)
    {
        var summary = Ladder.Download.DownloadResultAdapter.Adapt<DownloadMessageNode>(
            resultState,
            errorCount,
            warningCount,
            messages,
            m => m.Text,
            m => m.State,
            m => m.ErrorCount,
            m => m.WarningCount,
            m => ParseTimestamp(m.Timestamp),
            m => m.Children);

        return Ladder.Download.DownloadFeedbackParser.Parse(summary);
    }

    /// <summary>
    /// The node's own timestamp, rendered "O" by <see cref="ReadMessages"/>. Unparseable is null —
    /// a timestamp takes no part in any classification, so losing one costs ordering detail in the
    /// report and nothing else.
    /// </summary>
    private static DateTimeOffset? ParseTimestamp(string? rendered) =>
        DateTimeOffset.TryParse(
            rendered,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind,
            out var parsed)
            ? parsed
            : (DateTimeOffset?)null;

    /// <summary>
    /// Reads the whole message TREE off the Siemens objects, recursively, into a form a test can
    /// build — which is what lets the transfer classifier be exercised without a controller.
    ///
    /// Recursive because <c>DownloadResultMessage.Messages</c> nests, and the sentence that decides
    /// whether anything was transferred is not guaranteed to sit at the top level. Every read is
    /// guarded and a failure is RECORDED AS A NODE rather than dropped: a dropped message reads
    /// downstream as "nothing said that", which is the one conclusion it must not produce.
    /// </summary>
    private static IReadOnlyList<DownloadMessageNode> ReadMessages(DownloadResultMessageComposition? messages)
    {
        var nodes = new List<DownloadMessageNode>();
        if (messages is null)
        {
            return nodes;
        }

        try
        {
            foreach (DownloadResultMessage message in messages)
            {
                nodes.Add(new DownloadMessageNode(
                    Safe(() => message.State.ToString()),
                    SafeInt(() => message.ErrorCount),
                    SafeInt(() => message.WarningCount),
                    Safe(() => message.Message),
                    ReadMessages(SafeObject(() => message.Messages)),
                    Safe(() => message.DateTime.ToString("O"))));
            }
        }
        catch (Exception ex)
        {
            nodes.Add(new DownloadMessageNode(
                "(unreadable)", 0, 0, $"<<ENUMERATING MESSAGES FAILED: {ExceptionReport.Summarise(ex)}>>"));
        }

        return nodes;
    }

    private static void LogMessages(IReadOnlyList<DownloadMessageNode> messages, ProbeLog log, int depth)
    {
        if (messages.Count == 0)
        {
            log.Line(new string(' ', depth * 2) + "(none)");
            return;
        }

        var indent = new string(' ', depth * 2);
        foreach (var message in messages)
        {
            log.Line($"{indent}- state={message.State} errors={message.ErrorCount} " +
                     $"warnings={message.WarningCount} at={message.Timestamp ?? "(none)"}");
            log.Verbatim($"{indent}  text : ", message.Text);
            if (message.Children.Count > 0)
            {
                LogMessages(message.Children, log, depth + 1);
            }
        }
    }

    private static int SafeInt(Func<int> read)
    {
        try
        {
            return read();
        }
        catch (Exception)
        {
            return 0;
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
                // Name alone does NOT identify an adapter: two Hyper-V adapters on this machine share
                // a Name byte for byte and differ only in Number (System.Int32, read-only). The #N in
                // `openness-cli download-plan`'s output is this property, not part of the Name.
                int pcNumber;
                try
                {
                    pcNumber = pc.Number;
                }
                catch (Exception ex)
                {
                    pcNumber = ConnectionTarget<IConfiguration>.UnknownPcInterfaceNumber;
                    log.Line(
                        $"  WARNING: PC interface '{Safe(() => pc.Name)}' has an unreadable Number: " +
                        ExceptionReport.Summarise(ex) +
                        " — it cannot be named on --pc-interface, so it can only be selected if it is unambiguous.");
                }

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
                        pcNumber,
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
