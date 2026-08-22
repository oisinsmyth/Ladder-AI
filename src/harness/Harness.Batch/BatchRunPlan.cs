using Harness.Device;

namespace Harness.Batch;

/// <summary>What a run step is for, and — where it matters — what its failure means.</summary>
public enum BatchStepKind
{
    /// <summary><c>converter lease acquire</c>. Before anything touches Portal or the rig.</summary>
    LeaseAcquire,

    /// <summary><c>harness-run --generate-only --emit</c>: the merged copy layer, from the merged binding.</summary>
    Generate,

    /// <summary>A Portal or download step, planned by <see cref="DeploymentPlan"/>.</summary>
    Deploy,

    /// <summary><c>harness-run --verify</c> for ONE lane, against the shared map.</summary>
    Wave,

    /// <summary><c>converter lease release</c>. Runs even when something above failed.</summary>
    LeaseRelease,
}

/// <summary>
/// One invocation: the executable, the argument vector, what it is for, and which lane it belongs to.
/// </summary>
/// <param name="Lane">
/// The lane this step serves, or null for a step the whole batch shares. <b>Carried so a failure can be
/// attributed</b> — "the batch failed" is not an actionable sentence when six lanes are in it.
/// </param>
public sealed record BatchStep(BatchStepKind Kind, string Executable, IReadOnlyList<string> Arguments, string Purpose, string? Lane = null)
{
    public string CommandLineText => CommandLine.Render(Executable, Arguments);
}

/// <summary>Where the binaries are and what the run is allowed to touch.</summary>
/// <param name="Holder">
/// Who holds the leases. <b>Must identify this agent specifically</b> — the recorded race on the text
/// file the lease replaces happened because two entries carried the same non-specific name.
/// </param>
/// <param name="HolderPid">
/// The process that holds the gate for the run's lifetime. <b>Not the converter's own pid</b>: a CLI
/// invocation exits the moment it returns, and a lease held by a dead process makes every reclaim
/// decision fall back to the TTL alone.
/// </param>
public sealed record BatchRunOptions(
    string ConverterExe,
    string HarnessRunExe,
    string LeasesDirectory,
    string PortalProject,
    string RigAddress,
    string Holder,
    int HolderPid,
    string StagingDirectory,
    string MergedBindingPath,
    string PortalEvidencePath,
    int RigPort = 503,
    int RigUnit = 1,
    string? DeviceAllowlistPath = null,
    int LeaseTtlMinutes = 60);

/// <summary>The ordered steps of one batch run, or every reason there are none.</summary>
public sealed record BatchRunPlan(IReadOnlyList<BatchStep> Steps, IReadOnlyList<string> Refusals)
{
    public bool Planned => Refusals.Count == 0 && Steps.Count > 0;

    /// <summary>The steps that must run even after a failure above them.</summary>
    public IEnumerable<BatchStep> Teardown => Steps.Where(s => s.Kind == BatchStepKind.LeaseRelease);

    /// <summary>
    /// Build the plan. <b>Pure — no filesystem, no process, no Portal</b> — so the argument vectors a rig
    /// session will execute are the ones a unit test asserts. That is the same standard
    /// <see cref="DeploymentPlan"/> already sets, and it is the only standard available to a component
    /// that cannot be run without a controller.
    /// </summary>
    public static BatchRunPlan For(BatchPlanResult batch, IReadOnlyList<Lane> lanes, BatchRunOptions options)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(lanes);
        ArgumentNullException.ThrowIfNull(options);

        var refusals = new List<string>();

        if (!batch.Planned)
        {
            refusals.Add("the batch was not planned, so there is nothing to run. Its refusals are the ones to read; this plan adds none of its own.");
            return new BatchRunPlan(Array.Empty<BatchStep>(), refusals);
        }

        if (lanes.Count == 0)
            refusals.Add("NOTHING TO RUN: no lanes. A run over an empty batch would acquire the gate, deploy nothing and report a clean pass.");

        foreach (var (value, name) in new[]
                 {
                     (options.Holder, "--holder"),
                     (options.LeasesDirectory, "--leases"),
                     (options.PortalProject, "--portal-project"),
                     (options.RigAddress, "--rig"),
                     (options.PortalEvidencePath, "--portal-evidence"),
                 })
        {
            if (string.IsNullOrWhiteSpace(value))
                refusals.Add($"{name} is required: a batch run takes the Portal and rig gates, and neither can be taken without it.");
        }

        if (options.HolderPid <= 0)
        {
            refusals.Add("--holder-pid is required and must be a live process that outlives this run. It is NOT this process: a "
                + "lease held by a pid that exits immediately makes every later reclaim decision fall back to the TTL alone, while "
                + "still looking evidence-based.");
        }

        if (refusals.Count > 0)
            return new BatchRunPlan(Array.Empty<BatchStep>(), refusals);

        var steps = new List<BatchStep>();

        // ---- 1. THE GATES, BEFORE ANYTHING ELSE. ------------------------------------------------
        //
        // Portal first, because it is the one a human can take and the one whose refusal is not a
        // queue position. Taking the rig first would mean holding it while discovering a person is in
        // the project — locking an agent out of the rig for a run that was never going to happen.
        steps.Add(Lease(options, "acquire", "portal:" + options.PortalProject, withEvidence: true));
        steps.Add(Lease(options, "acquire", "rig:" + options.RigAddress, withEvidence: false));

        // ---- 2. THE MERGED COPY LAYER. ----------------------------------------------------------
        //
        // Generated from the MERGED binding, so the map covers every lane's slots. Any lane's
        // submission will do for this step — the copy layer is a function of the map, the bindings and
        // the naming, and of none of the vectors — but the first lane's is used rather than a synthetic
        // one, because a submission that exists is one somebody has already gated.
        var generatorLane = lanes[0];
        steps.Add(new BatchStep(
            BatchStepKind.Generate,
            options.HarnessRunExe,
            new[]
            {
                "--submission", generatorLane.SubmissionPath,
                "--binding", options.MergedBindingPath,
                "--generate-only",
                "--emit", options.StagingDirectory,
            },
            "generate the merged copy layer and mirror tag table for every lane's slots",
            generatorLane.Name));

        // ---- 3. THE DEPLOYMENT. -----------------------------------------------------------------
        //
        // Deliberately NOT re-planned here. DeploymentPlan already encodes the order, the layout
        // re-assert after every import (required, not precautionary — a re-import silently reverts a
        // block to Optimized, where classic S7comm cannot see it at all), and the exit-code reading
        // where "ran, reported nothing, examined nothing" is NotProven rather than Ok. Re-deriving that
        // sequence here would be a second place for it to drift.
        steps.Add(new BatchStep(
            BatchStepKind.Deploy,
            "<deployment>",
            Array.Empty<string>(),
            "import-all, layout re-assert, compile-all, sanity-check and download — planned by DeploymentPlan over the staged IR"));

        // ---- 4. ONE WAVE PER LANE, against the shared map. --------------------------------------
        //
        // Each lane runs its OWN submission against the MERGED binding. The other lanes' slots simply
        // have no vector at any index, and the wave already treats that as inert — the same mechanism
        // that covers an excised slot. So no lane's gate evaluation is touched by being batched.
        foreach (var lane in lanes)
        {
            var arguments = new List<string>
            {
                "--submission", lane.SubmissionPath,
                "--binding", options.MergedBindingPath,
                "--verify",
                "--host", options.RigAddress,
                "--port", options.RigPort.ToString(),
                "--unit", options.RigUnit.ToString(),
            };

            foreach (var program in lane.ProgramPaths)
            {
                arguments.Add("--program");
                arguments.Add(program);
            }

            if (!string.IsNullOrWhiteSpace(options.DeviceAllowlistPath))
            {
                arguments.Add("--allowlist");
                arguments.Add(options.DeviceAllowlistPath);
            }

            arguments.Add("--out");
            arguments.Add(Path.Combine(options.StagingDirectory, $"{lane.Name}-result.json"));

            steps.Add(new BatchStep(BatchStepKind.Wave, options.HarnessRunExe, arguments,
                $"run lane '{lane.Name}' against the shared map", lane.Name));
        }

        // ---- 5. THE GATES BACK. -----------------------------------------------------------------
        //
        // Released in the reverse order they were taken, and they run WHATEVER happened above: a lease
        // left held by a finished run is the failure the TTL exists to bound, and bounding it is not
        // the same as avoiding it.
        steps.Add(Lease(options, "release", "rig:" + options.RigAddress, withEvidence: false));
        steps.Add(Lease(options, "release", "portal:" + options.PortalProject, withEvidence: false));

        return new BatchRunPlan(steps, Array.Empty<string>());
    }

    private static BatchStep Lease(BatchRunOptions options, string verb, string resource, bool withEvidence)
    {
        var arguments = new List<string>
        {
            "lease", verb,
            "--resource", resource,
            "--leases", options.LeasesDirectory,
            "--holder", options.Holder,
        };

        if (verb == "acquire")
        {
            arguments.Add("--pid");
            arguments.Add(options.HolderPid.ToString());
            arguments.Add("--ttl");
            arguments.Add(options.LeaseTtlMinutes.ToString());
            arguments.Add("--purpose");
            arguments.Add("batch run");

            // Only the Portal lease takes evidence, and it REFUSES without it. Nothing can detect a rig
            // in use — MB_SERVER's one connection is discovered by failure — so passing evidence there
            // would imply an observation nobody made.
            if (withEvidence)
            {
                arguments.Add("--portal-evidence");
                arguments.Add(options.PortalEvidencePath);
            }
        }

        return new BatchStep(
            verb == "acquire" ? BatchStepKind.LeaseAcquire : BatchStepKind.LeaseRelease,
            options.ConverterExe,
            arguments,
            $"{verb} the {resource} gate");
    }
}
