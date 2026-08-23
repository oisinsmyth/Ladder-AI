using Harness.Device;
using Harness.Loop;
using Harness.Map;
using System.Text;

namespace Harness.Batch;

/// <summary>Exit codes. 0 is a plan or an enqueue that happened; nothing else is.</summary>
public static class BatchExit
{
    public const int Ok = 0;

    /// <summary>A real answer about the world: refused, or already queued.</summary>
    public const int Refused = 1;

    /// <summary>Nothing was decided — a missing flag, an unreadable document.</summary>
    public const int Unusable = 2;

    /// <summary>
    /// 🔴 <b>The queue was empty, so the batch examined nothing.</b> Its own exit code because "planned a
    /// batch of zero lanes" would otherwise be reported with the same 0 as a real plan, and every number
    /// in the report would read as valid.
    /// </summary>
    public const int NothingBatched = 3;
}

/// <summary>
/// Every decision in <c>harness-batch</c>, so none of them needs a process to test. The composition root
/// in <c>Program.cs</c> decides nothing.
/// </summary>
public static class BatchCli
{
    private const string Usage =
        "Usage: harness-batch enqueue --queue <dir> --lane <name> --binding <file> --submission <file>\n"
        + "                             (--manifest <file> | --program <path>...) [--purpose <text>]\n"
        + "                             # --manifest DERIVES the program set from what the lane emitted; --program is DECLARED by you\n"
        + "       harness-batch plan    --queue <dir> [--out <merged-binding.json>]\n"
        + "       harness-batch list    --queue <dir>\n"
        + "       harness-batch dequeue --queue <dir> --lane <name>\n"
        + "       harness-batch run     --queue <dir> --merged <file> --staging <dir> --leases <dir> --holder <id> --holder-pid <n>\n"
        + "                             --portal-project <path> --portal-evidence <file> --rig <address> [--port <n>] [--unit <n>]\n"
        + "                             [--converter <exe>] [--harness-run <exe>] [--allowlist <file>] [--ttl <minutes>]\n"
        + "                             [--deploy-config <file.json>] --yes    # WITHOUT --yes: prints every command, contacts NOTHING";

    public static int Run(
        string[] args, TextWriter output, Func<string, string> readFile, Action<string, string> writeFile,
        IProcessRunner? runner = null, Func<IReadOnlyList<string>, DeploymentOutcome>? deploy = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);

        if (args.Length == 0)
        {
            output.WriteLine(Usage);
            return BatchExit.Unusable;
        }

        var verb = args[0];
        if (verb is not ("enqueue" or "plan" or "list" or "dequeue" or "run"))
        {
            output.WriteLine($"unknown sub-command '{verb}' — expected one of: enqueue, plan, list, dequeue, run");
            output.WriteLine(Usage);
            return BatchExit.Unusable;
        }

        string? queue = null, lane = null, binding = null, submission = null, outPath = null, purpose = null;
        string? merged = null, staging = null, leases = null, holder = null, portalProject = null;
        string? portalEvidence = null, rig = null, converterExe = null, harnessRunExe = null, allowlist = null;
        string? opennessCliExe = null, manifest = null;
        int holderPid = 0, rigPort = 503, rigUnit = 1, ttlMinutes = 60;
        var settleSeconds = -1;   // -1 = not stated, use the default
        string? attestation = null;
        var confirmed = false;
        var programs = new List<string>();

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--queue": queue = Next(args, ref i); break;
                case "--lane": lane = Next(args, ref i); break;
                case "--binding": binding = Next(args, ref i); break;
                case "--submission": submission = Next(args, ref i); break;
                case "--out": outPath = Next(args, ref i); break;
                case "--purpose": purpose = Next(args, ref i); break;
                // The lane's own emitted object list. Where present the program set is DERIVED from it
                // rather than typed, which is what stops the build stamp describing a program nobody
                // deployed. See LaneManifest.
                case "--manifest": manifest = Next(args, ref i); break;
                case "--program":
                    // Multi-valued, the same shape harness-run's --program uses: consume until the next flag.
                    while (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                        programs.Add(args[++i]);
                    break;
                case "--merged": merged = Next(args, ref i); break;
                case "--staging": staging = Next(args, ref i); break;
                case "--leases": leases = Next(args, ref i); break;
                case "--holder": holder = Next(args, ref i); break;
                case "--portal-project": portalProject = Next(args, ref i); break;
                case "--portal-evidence": portalEvidence = Next(args, ref i); break;
                case "--rig": rig = Next(args, ref i); break;
                case "--converter": converterExe = Next(args, ref i); break;
                case "--harness-run": harnessRunExe = Next(args, ref i); break;
                case "--openness-cli": opennessCliExe = Next(args, ref i); break;
                case "--allowlist": allowlist = Next(args, ref i); break;
                // Read by Program.cs, which builds the gateway and generates the copy layer in-process.
                // They still have to be ACCEPTED here or the parser's unknown-argument arm rejects them —
                // which is exactly what happened on the first real rig run, at exit 2 before any gate was
                // taken. Listed together so the next one added does not repeat it.
                case "--deploy-config":
                case "--deploy-submission": _ = Next(args, ref i); break;
                case "--attest-portal-unjudgeable": attestation = Next(args, ref i); break;
                case "--yes": confirmed = true; break;
                case "--settle-seconds": if (!Number(args, ref i, "--settle-seconds", output, out settleSeconds, allowZero: true)) return BatchExit.Unusable; break;
                case "--holder-pid": if (!Number(args, ref i, "--holder-pid", output, out holderPid)) return BatchExit.Unusable; break;
                case "--port": if (!Number(args, ref i, "--port", output, out rigPort)) return BatchExit.Unusable; break;
                case "--unit": if (!Number(args, ref i, "--unit", output, out rigUnit)) return BatchExit.Unusable; break;
                case "--ttl": if (!Number(args, ref i, "--ttl", output, out ttlMinutes)) return BatchExit.Unusable; break;
                default:
                    output.WriteLine($"Unexpected argument: {args[i]}");
                    return BatchExit.Unusable;
            }
        }

        if (string.IsNullOrWhiteSpace(queue))
        {
            // No default, and the reason is the one --claims and --leases already carry: agents work in
            // separate worktrees, so a per-worktree queue is always empty, accepts every lane, and
            // produces a "batch" of one that looks exactly like success.
            output.WriteLine("--queue <dir> is required. It must be a directory SHARED by every agent on this machine — a per-worktree "
                + "queue would accept every lane and batch nothing with anything. The shared root is C:\\ProgramData\\Ladder-AI\\batch.");
            return BatchExit.Unusable;
        }

        var store = new LaneQueue(queue);

        // Echoed on every act. Two agents passing two different roots fork the queue, and no process can
        // detect that from inside — each queue is well-formed and legitimately holds what it holds.
        output.WriteLine($"queue={store.Root}");

        return verb switch
        {
            "enqueue" => Enqueue(store, output, readFile, lane, binding, submission, programs, purpose, manifest),
            "plan" => Plan(store, output, readFile, writeFile, outPath),
            "list" => List(store, output),
            "dequeue" => Dequeue(store, output, lane),
            _ => Run(store, output, readFile, runner, deploy, new RunArgs(
                merged, staging, leases, holder, holderPid, portalProject, portalEvidence,
                rig, rigPort, rigUnit, converterExe, harnessRunExe, allowlist, ttlMinutes, confirmed, attestation,
                settleSeconds, opennessCliExe)),
        };
    }

    private sealed record RunArgs(
        string? Merged, string? Staging, string? Leases, string? Holder, int HolderPid,
        string? PortalProject, string? PortalEvidence, string? Rig, int RigPort, int RigUnit,
        string? ConverterExe, string? HarnessRunExe, string? Allowlist, int TtlMinutes, bool Confirmed,
        string? PortalAttestation, int SettleSeconds, string? OpennessCliExe);

    /// <summary>
    /// 🔴 <b><c>--yes</c> is required, and without it Portal is NEVER CONTACTED.</b>
    ///
    /// <para>The same shape <c>download-probe</c>, <c>block-layout --set</c> and
    /// <c>hmi-create-screen</c> already use, and for a stronger reason than any of them: this takes two
    /// gates, writes a program into a project and downloads it to a controller. The dry run prints every
    /// command in order, which is worth having on its own — the deploy is otherwise about seven
    /// hand-assembled Portal-touching steps.</para>
    /// </summary>
    private static int Run(
        LaneQueue store, TextWriter output, Func<string, string> readFile,
        IProcessRunner? runner, Func<IReadOnlyList<string>, DeploymentOutcome>? deploy, RunArgs args)
    {
        var lanes = store.All();
        var batch = BatchPlanner.Plan(lanes, readFile);

        if (!batch.Planned)
        {
            output.Write(BatchPlanner.Describe(batch));
            output.WriteLine("  NOTHING WAS RUN: a batch that could not be planned is not a batch that can be deployed.");
            return lanes.Count == 0 ? BatchExit.NothingBatched : BatchExit.Refused;
        }

        // 🔴 DEFAULTS TO THIS PROCESS, AND THE CONVERTER'S OPPOSITE RULE DOES NOT APPLY HERE.
        //
        // `converter lease acquire` REFUSES to default --pid to itself, because a converter invocation
        // exits the moment it returns: the lease would be held by a dead process from birth and every
        // reclaim decision would fall back to the TTL alone.
        //
        // `harness-batch run` is the other case. It SPANS the whole lease - it takes the gates, deploys,
        // runs every wave and releases - so it is exactly "the process that holds the gate for the
        // lease's lifetime", which is what that flag asks for. Requiring an operator to supply one
        // instead produced the failure this comment came from: a pid copied from an earlier shell that
        // had since exited, refused as not running, on the real rig.
        //
        // Still overridable, for a wrapper that genuinely outlives this process.
        var holderPid = args.HolderPid > 0 ? args.HolderPid : Environment.ProcessId;

        var options = new BatchRunOptions(
            ConverterExe: args.ConverterExe ?? "converter",
            HarnessRunExe: args.HarnessRunExe ?? "harness-run",
            OpennessCliExe: args.OpennessCliExe ?? "openness-cli",
            LeasesDirectory: args.Leases ?? string.Empty,
            PortalProject: args.PortalProject ?? string.Empty,
            RigAddress: args.Rig ?? string.Empty,
            Holder: args.Holder ?? string.Empty,
            HolderPid: holderPid,
            StagingDirectory: args.Staging ?? string.Empty,
            MergedBindingPath: args.Merged ?? string.Empty,
            PortalEvidencePath: args.PortalEvidence ?? string.Empty,
            RigPort: args.RigPort,
            RigUnit: args.RigUnit,
            DeviceAllowlistPath: args.Allowlist,
            LeaseTtlMinutes: args.TtlMinutes,
            PortalAttestation: args.PortalAttestation);

        var plan = BatchRunPlan.For(batch, lanes, options with
        {
            UnionIrDirectory = MaterialiseUnionIr(batch.ProgramPaths, args.Staging, output),
            ProjectExportDirectory = string.IsNullOrWhiteSpace(args.Staging) ? null : Path.Combine(args.Staging!, "project-xml"),
        });

        output.WriteLine($"lanes {lanes.Count}: {string.Join(", ", batch.LanesBatched)}");
        output.WriteLine();

        foreach (var step in plan.Steps)
            output.WriteLine($"  [{step.Kind}]{(step.Lane is null ? "" : " " + step.Lane)}  {step.CommandLineText}");

        foreach (var refusal in plan.Refusals)
            output.WriteLine($"  REFUSED  {refusal}");

        output.WriteLine();

        if (!plan.Planned)
            return BatchExit.Unusable;

        // 🔴 CHECKED BEFORE THE GATES, NOT AT THE DEPLOY STEP. BatchRunner would stop there and release
        // correctly, but it would have taken and handed back two gates to discover a missing argument —
        // locking another agent out of Portal and the rig for the duration of a run that could never
        // have deployed.
        if (args.Confirmed && deploy is null)
        {
            output.WriteLine("REFUSED  --yes needs --deploy-config <file.json>, which supplies the Portal project, the group path, the "
                + "binaries and the download target (DeviceGatewayOptions). Without it there is nothing to deploy with, and taking the "
                + "gates first would lock another agent out of a run that cannot happen. NO GATE WAS TAKEN.");
            return BatchExit.Unusable;
        }

        if (!args.Confirmed)
        {
            // Portal has not been contacted, no lease has been taken, and nothing has been written. Said
            // explicitly rather than left to be inferred from the absence of output.
            output.WriteLine("DRY RUN — --yes was not passed, so NO GATE WAS TAKEN, NOTHING WAS WRITTEN and PORTAL WAS NOT CONTACTED.");
            output.WriteLine("The commands above are the ones that would run, in that order.");
            return BatchExit.Ok;
        }

        if (runner is null)
        {
            output.WriteLine("REFUSED  --yes was passed but this build has no process runner wired in, so nothing could be executed. "
                + "Nothing was written and no gate was taken.");
            return BatchExit.Unusable;
        }

        // The program union goes to the deployment, so the stamp it writes to the device is computed
        // over the SAME objects every lane's wave will compute over. Passing none - which this did -
        // stamps the device with a value no wave can reproduce, and the wave then refuses with a
        // version mismatch that reads as a failed download. Measured on the rig: device 16#CBE1D692,
        // staged 16#679E7923, and the download had in fact succeeded.
        var result = BatchRunner.Execute(plan, runner, () => deploy!(batch.ProgramPaths),
            // 🔴 THE BLIND WAIT NOW DEFAULTS OFF. The wave retries its own inert phase instead, which
            // MEASURES readiness rather than guessing at it — and the fixed 15 s was measured to be too
            // short anyway, so keeping it as the default would be paying for a wait that does not work.
            // --settle-seconds survives for a caller who wants a wait as well.
            settleAfterDownload: TimeSpan.FromSeconds(args.SettleSeconds >= 0 ? args.SettleSeconds : 0),

            // So the settling MEASUREMENT reaches the headline. The lanes share one download, so their
            // samples are repeated observations of one transient — the number nobody has ever read back.
            readFile: readFile);

        output.WriteLine(result.Headline);
        output.WriteLine();

        foreach (var step in result.Steps)
            output.WriteLine($"  {(step.Ok ? "ok    " : step.Verdict.ToString().ToUpperInvariant())}  [{step.Step.Kind}]  {step.Reason}");

        return result.Outcome == BatchRunOutcome.Ran ? BatchExit.Ok : BatchExit.Refused;
    }

    /// <param name="allowZero">
    /// Zero is meaningful for <c>--settle-seconds</c> — it disables the wait — and meaningless for a
    /// port or a TTL, so it is opted into rather than allowed everywhere.
    /// </param>
    /// <summary>
    /// Copy the lanes' program IR into one directory so <c>drift-check</c> has a <c>--project</c> to
    /// point at. Basename collisions were already refused at plan time, so a clash here would be a bug.
    ///
    /// <para>Returns null when there is nowhere to put it — and the plan then omits the drift steps and
    /// says so, rather than proceeding as though the comparison had passed.</para>
    /// </summary>
    private static string? MaterialiseUnionIr(IReadOnlyList<string> programPaths, string? staging, TextWriter output)
    {
        // 🔴 *** SAID, NOT SKIPPED. *** Without a staging directory there is nowhere to put the union, so
        // the drift check cannot run — and until now that produced NO LINE AT ALL. The steps were simply
        // absent from the plan, while the comment at their construction site claimed "the run SAYS so
        // rather than passing quietly". It did not. The most important gate this batch has could be
        // dropped by omitting one flag, and the report read exactly like a run that had passed it.
        if (string.IsNullOrWhiteSpace(staging))
        {
            output.WriteLine("  union     NOT STAGED — no --staging, so THE SUPPLIED PROGRAM WAS NOT COMPARED AGAINST THE PROJECT.");
            output.WriteLine("            The build stamp still claims the supplied program is what executes; nothing here checked it.");
            return null;
        }

        var union = Path.Combine(staging, "union-ir");

        try
        {
            if (Directory.Exists(union))
                Directory.Delete(union, recursive: true);

            Directory.CreateDirectory(union);

            // One enumeration, shared with the planner — see ProgramFiles. A basename collision between
            // two lanes has already been refused at plan time (BatchPlanner's union-corpus check), so the
            // overwrite:false below is a backstop against a case that should be unreachable, not the
            // place that finding is made.
            var contributions = ProgramFiles.Resolve(programPaths);
            var copied = 0;
            foreach (var file in contributions.SelectMany(c => c.Files).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                File.Copy(file, Path.Combine(union, Path.GetFileName(file)), overwrite: false);
                copied++;
            }

            // THE DENOMINATOR. "12 files staged" cannot be told from "12 of 15"; only the second says
            // whether the comparison about to run covers what was asked for.
            output.WriteLine($"  union     {copied} staged at {union} for the drift check — {ProgramFiles.Summary(contributions)}");

            foreach (var empty in contributions.Where(c => c.ContributedNothing))
                output.WriteLine($"            CONTRIBUTED NOTHING: {empty.Requested} ({empty.Kind})");

            return union;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Reported, and the drift steps are then omitted. A batch that silently skipped the check
            // because a copy failed would be claiming a comparison it never made.
            output.WriteLine($"  union     COULD NOT STAGE the program union, so the drift check will NOT run: {error.Message}");
            return null;
        }
    }

    private static bool Number(string[] args, ref int i, string flag, TextWriter output, out int value, bool allowZero = false)
    {
        if (int.TryParse(Next(args, ref i), out value) && (value > 0 || (allowZero && value == 0)))
            return true;

        output.WriteLine($"{flag} requires a positive whole number.");
        return false;
    }

    private static int Enqueue(
        LaneQueue store, TextWriter output, Func<string, string> readFile,
        string? lane, string? binding, string? submission, List<string> programs, string? purpose, string? manifestPath)
    {
        if (string.IsNullOrWhiteSpace(lane) || string.IsNullOrWhiteSpace(binding) || string.IsNullOrWhiteSpace(submission))
        {
            output.WriteLine("enqueue needs --lane <name>, --binding <file> and --submission <file>.");
            return BatchExit.Unusable;
        }

        // 🔴 *** THE PROGRAM SET IS DERIVED OR IT IS DECLARED, AND THE REPORT SAYS WHICH. ***
        //
        // Declared is the old path and still works. It is the weaker one: the stamp is computed over this
        // set and means "what is executing", and a hand-typed list is how a lane once got pointed at a
        // pre-fix Main with the stamp following it. Saying which of the two happened costs one line and
        // is the difference between a reader knowing and a reader assuming.
        if (!string.IsNullOrWhiteSpace(manifestPath))
        {
            LaneManifest manifest;
            try
            {
                manifest = LaneManifest.Read(manifestPath, readFile);
            }
            catch (Exception error) when (error is InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                output.WriteLine("REFUSED  " + error.Message);
                return BatchExit.Unusable;
            }

            // Never chooses a winner. The caller meant something by --program, and silently overriding it
            // would swap one unexamined program set for another.
            if (manifest.Disagreement(programs) is { } disagreement)
            {
                output.WriteLine("REFUSED  " + disagreement);
                return BatchExit.Unusable;
            }

            programs = manifest.ProgramPaths.ToList();

            var generated = manifest.Objects.Count(o => o.Origin == ObjectOrigin.Generated);
            output.WriteLine($"  program set DERIVED from the manifest: {manifest.Objects.Count} object(s) "
                           + $"({generated} generated, {manifest.Objects.Count - generated} authored) "
                           + $"across {programs.Count} path(s).");

            // Printed, never enforced: this tool cannot create a call site, and a generated FC nothing
            // calls is deployed, loaded, healthy in every artifact, and never runs.
            foreach (var obligation in manifest.Obligations)
                output.WriteLine("  OBLIGATION: " + obligation);
        }
        else
        {
            output.WriteLine($"  program set DECLARED by the caller: {programs.Count} path(s), from --program. "
                           + "Nothing emitted this list, so nothing checks it against what the lane actually built.");
        }

        var outcome = store.Enqueue(new Lane(lane, binding, submission, programs, purpose ?? string.Empty));
        output.WriteLine((outcome.Ok ? "QUEUED   " : "REFUSED  ") + outcome.Detail);

        return outcome.Result switch
        {
            EnqueueResult.Queued => BatchExit.Ok,
            EnqueueResult.AlreadyQueued => BatchExit.Refused,
            _ => BatchExit.Unusable,
        };
    }

    private static int Plan(LaneQueue store, TextWriter output, Func<string, string> readFile, Action<string, string> writeFile, string? outPath)
    {
        var lanes = store.All();
        var result = BatchPlanner.Plan(lanes, readFile);

        output.Write(BatchPlanner.Describe(result));

        if (!result.Planned)
            return lanes.Count == 0 ? BatchExit.NothingBatched : BatchExit.Refused;

        if (outPath is not null)
        {
            writeFile(outPath, result.MergedBindingJson!);
            output.WriteLine($"  merged binding written to {outPath}");
        }

        // The union pre-flight is a SEPARATE act and it is named rather than implied. Printing the
        // commands rather than running them is deliberate: this tool has no business deciding that a
        // cross-check finding is acceptable, and a batch that swallowed one would be the correlated
        // check this project exists to avoid.
        output.WriteLine();
        output.WriteLine("  UNION PRE-FLIGHT — run these before deploying. This is the step a batch earns:");
        output.WriteLine("  two blocks that each compiled clean in isolation can still conflict, and only a union check sees it.");
        foreach (var path in result.ProgramPaths.Distinct())
            output.WriteLine($"    converter cross-check --project {path}");

        return BatchExit.Ok;
    }

    private static int List(LaneQueue store, TextWriter output)
    {
        var lanes = store.All();
        output.WriteLine($"lanes {lanes.Count}");

        foreach (var lane in lanes)
        {
            output.WriteLine($"  {lane.Name}");
            output.WriteLine($"    binding    {lane.BindingPath}");
            output.WriteLine($"    submission {lane.SubmissionPath}");
            output.WriteLine($"    program    {string.Join(", ", lane.ProgramPaths)}");
            if (lane.Purpose.Length > 0)
                output.WriteLine($"    purpose    {lane.Purpose}");
        }

        if (lanes.Count == 0)
            output.WriteLine("  (none) — an empty queue and a mistyped --queue path look identical from here; the queue line above is the one to check.");

        return BatchExit.Ok;
    }

    private static int Dequeue(LaneQueue store, TextWriter output, string? lane)
    {
        if (string.IsNullOrWhiteSpace(lane))
        {
            output.WriteLine("dequeue needs --lane <name>.");
            return BatchExit.Unusable;
        }

        if (store.Dequeue(lane))
        {
            output.WriteLine($"REMOVED  lane '{lane}' is no longer queued.");
            return BatchExit.Ok;
        }

        output.WriteLine($"REFUSED  lane '{lane}' is not in this queue.");
        return BatchExit.Refused;
    }

    private static string? Next(string[] args, ref int i) => i + 1 < args.Length ? args[++i] : null;
}
