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
        "Usage: harness-batch enqueue --queue <dir> --lane <name> --binding <file> --submission <file> --program <path>... [--purpose <text>]\n"
        + "       harness-batch plan    --queue <dir> [--out <merged-binding.json>]\n"
        + "       harness-batch list    --queue <dir>\n"
        + "       harness-batch dequeue --queue <dir> --lane <name>";

    public static int Run(string[] args, TextWriter output, Func<string, string> readFile, Action<string, string> writeFile)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);

        if (args.Length == 0)
        {
            output.WriteLine(Usage);
            return BatchExit.Unusable;
        }

        var verb = args[0];
        if (verb is not ("enqueue" or "plan" or "list" or "dequeue"))
        {
            output.WriteLine($"unknown sub-command '{verb}' — expected one of: enqueue, plan, list, dequeue");
            output.WriteLine(Usage);
            return BatchExit.Unusable;
        }

        string? queue = null, lane = null, binding = null, submission = null, outPath = null, purpose = null;
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
                case "--program":
                    // Multi-valued, the same shape harness-run's --program uses: consume until the next flag.
                    while (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                        programs.Add(args[++i]);
                    break;
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
            "enqueue" => Enqueue(store, output, lane, binding, submission, programs, purpose),
            "plan" => Plan(store, output, readFile, writeFile, outPath),
            "list" => List(store, output),
            _ => Dequeue(store, output, lane),
        };
    }

    private static int Enqueue(
        LaneQueue store, TextWriter output,
        string? lane, string? binding, string? submission, List<string> programs, string? purpose)
    {
        if (string.IsNullOrWhiteSpace(lane) || string.IsNullOrWhiteSpace(binding) || string.IsNullOrWhiteSpace(submission))
        {
            output.WriteLine("enqueue needs --lane <name>, --binding <file> and --submission <file>.");
            return BatchExit.Unusable;
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
