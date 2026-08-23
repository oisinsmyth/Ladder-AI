using System.Text;
using Harness.Loop;
using Harness.Map;
using Harness.Device;

namespace Harness.Batch;

/// <summary>How the whole run ended. <b>Never a bool</b>, and "ran" is not "passed".</summary>
public enum BatchRunOutcome
{
    Unstated = 0,

    /// <summary>Every lane's wave was attempted. Says nothing about their verdicts.</summary>
    Ran,

    /// <summary>A gate could not be taken. <b>Nothing was deployed and nothing was run.</b></summary>
    GateRefused,

    /// <summary>The deployment stopped before the download. No wave was attempted.</summary>
    NotDeployed,

    /// <summary>The plan itself was refused; no process was started.</summary>
    NotPlanned,
}

/// <summary>One executed step and how its exit code was read.</summary>
public sealed record BatchStepResult(BatchStep Step, ProcessResult Result, StepVerdict Verdict, string Reason)
{
    public bool Ok => Verdict == StepVerdict.Ok;
}

/// <summary>
/// What the run did.
/// </summary>
/// <param name="LanesRun">
/// Lanes whose wave was ATTEMPTED. <b>Not lanes that passed</b> — a batch runner has no business
/// reading a lane's verdict, which is the result package's job and the gate's.
/// </param>
/// <param name="LeasesReleased">
/// 🔴 <b>Whether every gate was handed back.</b> Carried separately because a release that did not
/// happen must be visible: a lease left held by a finished run blocks every other agent until its TTL,
/// and the TTL bounds that failure rather than avoiding it.
/// </param>
public sealed record BatchRunResult(
    BatchRunOutcome Outcome,
    IReadOnlyList<BatchStepResult> Steps,
    IReadOnlyList<string> LanesRun,
    IReadOnlyList<string> LanesNotRun,
    bool LeasesReleased,
    string Headline,

    /// <summary>
    /// How long the run waited after the download before the first wave, or zero if it did not. Carried
    /// because a wave that passed after a settle and one that passed without it are different evidence.
    /// </summary>
    TimeSpan SettledAfterDownload = default);

/// <summary>
/// Executes a <see cref="BatchRunPlan"/>.
///
/// <para>🔴 <b>READ THIS BEFORE BELIEVING A GREEN FROM IT.</b> Every ORDERING decision here — gates
/// before Portal, abort before the download, teardown whatever happened, which failures stop the batch
/// and which stop only a lane — is exercised by tests with the process runner substituted. <b>The act of
/// running the binaries is not.</b> Those are different claims, and this project's most expensive
/// failures have all been the second mistaken for the first: <c>Harness.Device</c> carries the same
/// seam and its own caveat says no step of it has ever been executed against Portal or a controller.</para>
///
/// <para>What a substituted runner CAN prove is that the argument vectors are the ones a rig session
/// would type, that a refused gate stops everything, and that the leases come back. What it cannot
/// prove is that TIA answers the way the exit-code table says.</para>
/// </summary>
public static class BatchRunner
{
    /// <summary>
    /// 🔴 <b>How long to wait after a successful download before the first wave.</b>
    ///
    /// <para><b>Measured, 2026-08-22:</b> the first wave after a download refused inert with
    /// <c>NotQuiescent</c> — slot 0 R013 moved 1→0 and R014 0→1 between the two observations — and a
    /// re-run minutes later passed 3 of 3. The same transient is recorded once before, from the restore
    /// wave of an earlier session. The refusal is CORRECT: a model still integrating toward its rest
    /// value is not inert, and running the test then would be testing a start state nobody established.
    /// What is wrong is paying it on every deploy.</para>
    ///
    /// <para>⚠️ <b>15 s is a JUDGEMENT, not a measurement.</b> What was measured is that the transient
    /// exists and that it had cleared by the next run some minutes later; nobody has measured how long it
    /// actually takes. 15 s is ~600 scans at the measured 24.9 ms and ~150 presenter ticks at the 100 ms
    /// cadence, which is generous for a model settling and cheap against a 111 s deploy. If a wave still
    /// refuses <c>NotQuiescent</c> on the first index, this number is the first thing to raise — and
    /// raising it is a workaround until someone measures the real settling time.</para>
    /// </summary>
    public static readonly TimeSpan DefaultSettleAfterDownload = TimeSpan.FromSeconds(15);

    /// <param name="settleAfterDownload">
    /// Wait between a successful deployment and the first wave. <see cref="TimeSpan.Zero"/> disables it.
    /// </param>
    /// <param name="sleep">
    /// Injected so the wait is a DECISION a test can observe rather than a delay a test has to sit
    /// through. Every test asserts the duration; none of them waits.
    /// </param>
    public static BatchRunResult Execute(
        BatchRunPlan plan,
        IProcessRunner runner,
        Func<DeploymentOutcome>? deploy = null,
        TimeSpan? stepTimeout = null,
        TimeSpan? settleAfterDownload = null,
        Action<TimeSpan>? sleep = null,

        // Reads a finished lane's result file, so the settling MEASUREMENT can reach the headline. Null
        // is honest rather than silent: the headline then says the samples are in the lane packages
        // instead of implying none were taken.
        Func<string, string>? readFile = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(runner);

        var timeout = stepTimeout ?? TimeSpan.FromMinutes(10);

        if (!plan.Planned)
        {
            return new BatchRunResult(BatchRunOutcome.NotPlanned, Array.Empty<BatchStepResult>(),
                Array.Empty<string>(), Array.Empty<string>(), LeasesReleased: true,
                "NOT PLANNED, so no process was started and no gate was taken: " + string.Join(" | ", plan.Refusals));
        }

        var executed = new List<BatchStepResult>();
        var lanesRun = new List<string>();
        var lanesNotRun = plan.Steps.Where(s => s.Kind == BatchStepKind.Wave).Select(s => s.Lane!).ToList();
        var outcome = BatchRunOutcome.Ran;
        var stopReason = string.Empty;
        var gatesTaken = new List<BatchStep>();
        var settled = TimeSpan.Zero;
        var waveOutputs = new List<(string Lane, string? OutPath)>();

        foreach (var step in plan.Steps)
        {
            // Teardown is not part of the forward pass. It runs below, whatever happened here.
            if (step.Kind == BatchStepKind.LeaseRelease)
                continue;

            if (step.Kind == BatchStepKind.Deploy)
            {
                // The deployment is delegated whole, because DeploymentPlan already owns its ordering,
                // its layout re-assert and its exit-code reading. A batch that re-derived those would be
                // a second place for them to drift.
                //
                // 🔴 THE DELEGATE TAKES NO ARGUMENTS, AND IT USED TO TAKE THE OBJECT LIST — WHICH THIS
                // METHOD DOES NOT HAVE. It passed `Array.Empty<HarnessObject>()`, so the gateway would
                // have planned a deployment over NO objects: staged nothing, imported nothing, and
                // reported on an empty set. Found while wiring the first real rig run, which is the only
                // place it could have been found — every test substituted the deployment too, so the
                // empty list was handed to a fake that ignored it.
                //
                // The objects come from generation, which happens in the caller's closure and in-process,
                // so each one carries the Kind its generator gave it. Reconstructing them from the
                // emitted .ir files would mean INFERRING TagTable-vs-Block from file content, and the
                // gateway routes imports on exactly that distinction.
                if (deploy is null)
                {
                    outcome = BatchRunOutcome.NotDeployed;
                    stopReason = "no deployment gateway was supplied, so nothing was written to the project and no download was attempted.";
                    break;
                }

                var deployment = deploy();
                var deployed = deployment.Attempted && deployment.Loaded;
                executed.Add(new BatchStepResult(step,
                    new ProcessResult(true, false, deployed ? 0 : 1, deployment.Detail, string.Empty, deployment.Detail),
                    deployed ? StepVerdict.Ok : StepVerdict.Failed,
                    deployment.Detail));

                if (deployed)
                {
                    // The settle sits HERE — after a deployment that loaded, before any wave — and
                    // nowhere else. It is not a general retry and it does not run when the deployment
                    // failed: there is nothing to settle toward if nothing was downloaded.
                    // Defaults to NO WAIT. The 15 s policy lives at the CLI edge, where an operator can
                    // see and change it; a library that slept by default charged every test that never
                    // mentioned settling - measured as 75 s across one test class.
                    var settle = settleAfterDownload ?? TimeSpan.Zero;
                    if (settle > TimeSpan.Zero)
                    {
                        (sleep ?? Thread.Sleep)(settle);
                        settled = settle;
                    }

                    continue;
                }

                outcome = BatchRunOutcome.NotDeployed;
                stopReason = "the deployment did not load, so NO LANE WAS RUN: " + deployment.Detail;
                break;
            }

            var result = runner.Run(step.Executable, step.Arguments, timeout);
            var verdict = Read(step, result);
            executed.Add(new BatchStepResult(step, result, verdict.Verdict, verdict.Reason));

            if (step.Kind == BatchStepKind.LeaseAcquire && verdict.Verdict == StepVerdict.Ok)
                gatesTaken.Add(step);

            if (verdict.Verdict == StepVerdict.Ok)
            {
                if (step.Kind == BatchStepKind.Wave)
                {
                    lanesRun.Add(step.Lane!);
                    lanesNotRun.Remove(step.Lane!);
                    waveOutputs.Add((step.Lane!, OutPath(step)));
                }

                continue;
            }

            // 🔴 A LANE'S WAVE FAILING DOES NOT STOP THE BATCH. The lanes are independent experiments
            // sharing a deployment; abandoning the rest would throw away the evidence they were queued
            // to produce, and a lane that fails has still RUN. Everything else is a shared precondition,
            // and a shared precondition that failed makes every later step meaningless.
            if (step.Kind == BatchStepKind.Wave)
            {
                lanesRun.Add(step.Lane!);
                lanesNotRun.Remove(step.Lane!);

                // A lane whose wave FAILED still measured the settling if it got as far as index 0, and
                // that measurement is about the DEPLOYMENT rather than about the lane. Dropping it here
                // would discard the sample from the run most likely to want it.
                waveOutputs.Add((step.Lane!, OutPath(step)));
                continue;
            }

            outcome = step.Kind == BatchStepKind.LeaseAcquire ? BatchRunOutcome.GateRefused : BatchRunOutcome.NotDeployed;
            stopReason = $"stopped at {step.Kind} ({verdict.Verdict}): {verdict.Reason}"
                + Environment.NewLine + "  command: " + step.CommandLineText;
            break;
        }

        // ---- TEARDOWN, WHATEVER HAPPENED ABOVE. -------------------------------------------------
        //
        // Only the gates actually TAKEN are released. Releasing one that was never acquired would be
        // refused by the store anyway — only the holder may release — but attempting it would put a
        // spurious refusal in the report of a run whose real problem is somewhere above.
        var released = true;
        foreach (var step in plan.Teardown)
        {
            if (!gatesTaken.Any(g => Resource(g) == Resource(step)))
                continue;

            var result = runner.Run(step.Executable, step.Arguments, timeout);
            var verdict = Read(step, result);
            executed.Add(new BatchStepResult(step, result, verdict.Verdict, verdict.Reason));

            if (verdict.Verdict != StepVerdict.Ok)
                released = false;
        }

        return new BatchRunResult(outcome, executed, lanesRun, lanesNotRun, released,
            Headline(outcome, plan, lanesRun, lanesNotRun, released, stopReason, settled)
                + Settling(waveOutputs, readFile), settled);
    }

    /// <summary>Where a wave step was told to write its result, so the settling sample can be read back.</summary>
    private static string? OutPath(BatchStep step)
    {
        var index = step.Arguments.ToList().IndexOf("--out");
        return index >= 0 && index + 1 < step.Arguments.Count ? step.Arguments[index + 1] : null;
    }

    /// <summary>
    /// 🔴 <b>The post-download settling, per lane — N samples of ONE transient from ONE download.</b>
    ///
    /// <para>This is the whole reason the measurement is worth surfacing at the batch level rather than
    /// leaving in each package: the lanes share a deployment, so they are repeated observations of the
    /// same event, and disagreement between them is itself information. One lane needing four attempts
    /// while another needed one is not two settling times — it is a sign the lanes are not observing the
    /// same thing.</para>
    ///
    /// <para><b>Every branch says which nothing it is.</b> No reader supplied, no <c>--out</c>, an
    /// unreadable file and a file that recorded no measurement are four different states, and reporting
    /// them all as silence is how "nobody asked" became indistinguishable from "the answer was zero" in
    /// the first place.</para>
    /// </summary>
    private static string Settling(IReadOnlyList<(string Lane, string? OutPath)> waves, Func<string, string>? readFile)
    {
        if (waves.Count == 0)
            return string.Empty;

        if (readFile is null)
            return " Post-download settling was measured per lane and is in each lane's result package "
                 + "(`postDownloadSettling`); this run was given no reader to summarise it here.";

        var parts = new List<string>();
        foreach (var (lane, path) in waves)
        {
            if (path is null)
            {
                parts.Add($"{lane}: no --out, so nothing was read back");
                continue;
            }

            try
            {
                var node = System.Text.Json.Nodes.JsonNode.Parse(readFile(path));
                var settling = node?["postDownloadSettling"];

                if (settling is null)
                    parts.Add($"{lane}: the result file records no settling measurement");
                else if (settling["measured"]?.GetValue<bool>() != true)
                    parts.Add($"{lane}: NOT MEASURED ({settling["reason"]?.GetValue<string>() ?? "no reason given"})");
                else
                    parts.Add($"{lane}: {settling["attempts"]?.GetValue<int>()} attempt(s), "
                            + $"{settling["waitedSeconds"]?.GetValue<double>():0.#}s waited"
                            + (settling["quiescent"]?.GetValue<bool>() == true ? string.Empty : ", STILL NOT QUIESCENT"));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                // Named, not swallowed. An unreadable result file is a fact about this run.
                parts.Add($"{lane}: its result file could not be read ({error.GetType().Name})");
            }
        }

        return " POST-DOWNLOAD SETTLING, one sample per lane off the same download — " + string.Join("; ", parts) + ".";
    }

    /// <summary>The <c>--resource</c> value, so an acquire and its release can be paired.</summary>
    private static string Resource(BatchStep step)
    {
        var index = step.Arguments.ToList().IndexOf("--resource");
        return index >= 0 && index + 1 < step.Arguments.Count ? step.Arguments[index + 1] : string.Empty;
    }

    /// <summary>
    /// How a step's exit code is read.
    ///
    /// <para>The lease and the wave have their own codes, so they are read here rather than through
    /// <see cref="DeviceExitCodes"/>, which is about Portal steps. <b>A timed-out step is never Ok</b> —
    /// a Portal command that expired may still be holding the project, and treating the timeout as a
    /// pass is how a run continues into a session it does not have.</para>
    /// </summary>
    private static StepReading Read(BatchStep step, ProcessResult result)
    {
        if (!result.Started)
            return new StepReading(StepVerdict.NotProven, "the binary did not start: " + result.Detail);

        if (result.TimedOut)
        {
            return new StepReading(StepVerdict.NotProven,
                "the step timed out. It is NOT recorded as a failure: a Portal command that expired may still be holding the "
                + "project, and the run cannot tell from here which happened.");
        }

        return step.Kind switch
        {
            // 0 acquired or released; 1 refused (someone has it, or a person is in the project); 2 unusable.
            BatchStepKind.LeaseAcquire or BatchStepKind.LeaseRelease => result.ExitCode switch
            {
                // Worded per verb. It read "the gate is held" for BOTH, so every successful release
                // reported the opposite of what had happened — visible in the first real rig run, where
                // two correct releases both claimed the gate was still held.
                0 => new StepReading(StepVerdict.Ok,
                    step.Kind == BatchStepKind.LeaseAcquire ? "the gate is held." : "the gate was handed back."),
                1 => new StepReading(StepVerdict.Failed, "REFUSED: " + LastLines(result.StandardError, result.StandardOutput)),
                _ => new StepReading(StepVerdict.NotProven, "nothing was decided: " + LastLines(result.StandardError, result.StandardOutput)),
            },

            // export-all: 12 = ExportIncomplete — everything attempted worked and the dump is not whole,
            // which is NOT a failure of the project and IS a reason the comparison below cannot be
            // trusted. NotProven, so the run stops without accusing anything.
            BatchStepKind.ExportAll => result.ExitCode switch
            {
                0 => new StepReading(StepVerdict.Ok, "the project's objects were exported."),
                12 => new StepReading(StepVerdict.NotProven,
                    "the export is INCOMPLETE, so a drift comparison against it would be over an unstated denominator: "
                    + LastLines(result.StandardError, result.StandardOutput)),
                _ => new StepReading(StepVerdict.Failed, "export failed: " + LastLines(result.StandardError, result.StandardOutput)),
            },

            // drift-check: 1 = something drifted. That is a REAL finding about the supplied program and
            // it stops the batch — the stamp would otherwise describe a program the controller is not
            // running, which is exactly the hole an uncalled slot FC hid in.
            BatchStepKind.DriftCheck => result.ExitCode switch
            {
                0 => new StepReading(StepVerdict.Ok, "every supplied object matches the project."),
                1 => new StepReading(StepVerdict.Failed,
                    "SUPPLIED PROGRAM DOES NOT MATCH THE PROJECT, so the build stamp would describe something that is not "
                    + "running: " + LastLines(result.StandardError, result.StandardOutput)),
                _ => new StepReading(StepVerdict.NotProven,
                    "the drift check could not run, so nothing was compared: " + LastLines(result.StandardError, result.StandardOutput)),
            },

            // harness-run: Generated == Ran == 0, deliberately.
            BatchStepKind.Generate or BatchStepKind.Wave => result.ExitCode == 0
                ? new StepReading(StepVerdict.Ok, "ran.")
                : new StepReading(StepVerdict.Failed, $"exit {result.ExitCode}: " + LastLines(result.StandardError, result.StandardOutput)),

            _ => new StepReading(StepVerdict.NotProven, $"exit {result.ExitCode}, and this step kind has no reading."),
        };
    }

    /// <summary>
    /// 🔴 <b>The LAST lines, not the first — and the first is what this used to take.</b>
    ///
    /// <para>Every tool here prints a banner before it prints a verdict, so the first non-empty line
    /// of a failing run is a heading. Measured repeatedly on 2026-08-22: a batch that stopped at
    /// generation reported <i>"program : 8 object(s) under test; THE BUILD STAMP IS TAKEN OVER 8 OF
    /// THEM."</i> — informational, true, and nothing to do with the failure. The real reasons (a map
    /// that did not fit, a submission gate refusal, an unresolved member type) were all on the last
    /// lines, and finding them meant re-running each child command by hand. Three round trips, three
    /// times.</para>
    ///
    /// <para>stderr wins when there is any, because a tool that wrote there was reporting a problem.
    /// Several lines rather than one: these tools end on a verdict plus the sentence that explains it,
    /// and taking one line splits them.</para>
    /// </summary>
    private static string LastLines(string standardError, string standardOutput, int lines = 4)
    {
        var source = string.IsNullOrWhiteSpace(standardError) ? standardOutput : standardError;

        var tail = (source ?? string.Empty)
            .Split('\n')
            .Select(l => l.TrimEnd('\r').Trim())
            .Where(l => l.Length > 0)
            .TakeLast(lines)
            .ToArray();

        return tail.Length == 0 ? "(no output)" : string.Join(" / ", tail);
    }

    private static string Headline(
        BatchRunOutcome outcome, BatchRunPlan plan,
        IReadOnlyList<string> lanesRun, IReadOnlyList<string> lanesNotRun, bool released, string stopReason,
        TimeSpan settled)
    {
        var planned = plan.Steps.Count(s => s.Kind == BatchStepKind.Wave);

        var sb = new StringBuilder();
        sb.Append(outcome switch
        {
            BatchRunOutcome.Ran => "THE BATCH RAN",
            BatchRunOutcome.GateRefused => "THE GATE WAS REFUSED",
            BatchRunOutcome.NotDeployed => "NOT DEPLOYED",
            _ => "NOT PLANNED",
        });

        // The denominator, on the headline. "3 lanes ran" is a different claim from "3 of 7 lanes ran",
        // and only the second can be acted on.
        sb.Append($": {lanesRun.Count} of {planned} lane(s) were run");

        if (lanesNotRun.Count > 0)
            sb.Append($"; {lanesNotRun.Count} were NOT ({string.Join(", ", lanesNotRun)})");

        sb.Append('.');

        if (stopReason.Length > 0)
            sb.Append(' ').Append(stopReason);

        // *** RUN IS NOT PASSED. *** This component attempts waves; it does not read verdicts, and a
        // reader who took "THE BATCH RAN" for "every lane passed" would be believing something nothing
        // here checked.
        if (settled > TimeSpan.Zero)
            sb.Append($" Waited {settled.TotalSeconds:0}s after the download before the first wave, for the post-download transient.");

        sb.Append(" Each lane's verdict is in its own result package — a wave that ran is not a wave that passed.");

        sb.Append(released
            ? " Every gate taken was handed back."
            : " ⚠️ A GATE WAS NOT RELEASED: another agent is blocked on it until its TTL expires. Release it by hand.");

        return sb.ToString();
    }
}
