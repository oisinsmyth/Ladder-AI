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
    string Headline);

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
    public static BatchRunResult Execute(
        BatchRunPlan plan,
        IProcessRunner runner,
        Func<DeploymentOutcome>? deploy = null,
        TimeSpan? stepTimeout = null)
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
                    continue;

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
            Headline(outcome, plan, lanesRun, lanesNotRun, released, stopReason));
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
                0 => new StepReading(StepVerdict.Ok, "the gate is held."),
                1 => new StepReading(StepVerdict.Failed, "REFUSED: " + FirstLine(result.StandardError, result.StandardOutput)),
                _ => new StepReading(StepVerdict.NotProven, "nothing was decided: " + FirstLine(result.StandardError, result.StandardOutput)),
            },

            // harness-run: Generated == Ran == 0, deliberately.
            BatchStepKind.Generate or BatchStepKind.Wave => result.ExitCode == 0
                ? new StepReading(StepVerdict.Ok, "ran.")
                : new StepReading(StepVerdict.Failed, $"exit {result.ExitCode}: " + FirstLine(result.StandardError, result.StandardOutput)),

            _ => new StepReading(StepVerdict.NotProven, $"exit {result.ExitCode}, and this step kind has no reading."),
        };
    }

    private static string FirstLine(params string[] candidates) =>
        candidates
            .SelectMany(c => (c ?? string.Empty).Split('\n'))
            .Select(l => l.Trim())
            .FirstOrDefault(l => l.Length > 0) ?? "(no output)";

    private static string Headline(
        BatchRunOutcome outcome, BatchRunPlan plan,
        IReadOnlyList<string> lanesRun, IReadOnlyList<string> lanesNotRun, bool released, string stopReason)
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
        sb.Append(" Each lane's verdict is in its own result package — a wave that ran is not a wave that passed.");

        sb.Append(released
            ? " Every gate taken was handed back."
            : " ⚠️ A GATE WAS NOT RELEASED: another agent is blocked on it until its TTL expires. Release it by hand.");

        return sb.ToString();
    }
}
