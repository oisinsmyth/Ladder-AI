using System.Text.Json;

namespace Harness.Batch;

/// <summary>One lane waiting for a deployment.</summary>
/// <param name="Name">
/// The lane's identity, and the queue file's name. <b>Unique by construction</b>: enqueueing a name that
/// is already queued is refused rather than overwritten, because the second submitter believes their
/// lane is in the batch and it would not be.
/// </param>
/// <param name="BindingPath">The lane's binding document — the half that gets merged.</param>
/// <param name="SubmissionPath">The lane's submission — <b>not</b> merged; it runs on its own.</param>
/// <param name="ProgramPaths">IR directories or files making up this lane's program under test.</param>
public sealed record Lane(
    string Name,
    string BindingPath,
    string SubmissionPath,
    IReadOnlyList<string> ProgramPaths,
    string Purpose = "");

/// <summary>Why an enqueue did or did not happen.</summary>
public enum EnqueueResult
{
    Unstated = 0,
    Queued,
    AlreadyQueued,
    Invalid,
}

public sealed record EnqueueOutcome(EnqueueResult Result, string Detail)
{
    public bool Ok => Result == EnqueueResult.Queued;
}

/// <summary>
/// The queue of lanes awaiting a batch: <b>one file per lane</b>, claimed by atomic move.
///
/// <para><b>The same primitive as the claims registry and the lease, for the same reason.</b> Two
/// agents enqueueing at once must not be able to lose one another's lane, and a directory of disjoint
/// files is conflict-free under concurrent writers by construction. Nothing here needs a TTL — unlike a
/// lease, a queued lane is not holding anything, so a crashed submitter leaves a lane that is merely
/// waiting rather than a gate that is wedged.</para>
///
/// <para><b>The root must be SHARED, and is not defaulted.</b> Agents work in separate worktrees; a
/// per-worktree queue is always empty, accepts every lane, and produces a "batch" of one that looks
/// exactly like success.</para>
/// </summary>
public sealed class LaneQueue
{
    private const string Extension = ".lane";

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _root;

    public LaneQueue(string root) => _root = root ?? throw new ArgumentNullException(nameof(root));

    public string Root => _root;

    /// <summary>Every queued lane, in a stable order so two readers see one batch.</summary>
    public IReadOnlyList<Lane> All()
    {
        if (!Directory.Exists(_root))
            return Array.Empty<Lane>();

        return Directory.EnumerateFiles(_root, "*" + Extension)
            .Select(Read)
            .Where(l => l is not null)
            .Select(l => l!)
            .OrderBy(l => l.Name, StringComparer.Ordinal)
            .ToList();
    }

    public EnqueueOutcome Enqueue(Lane lane)
    {
        ArgumentNullException.ThrowIfNull(lane);

        if (string.IsNullOrWhiteSpace(lane.Name))
            return new EnqueueOutcome(EnqueueResult.Invalid, "a lane with no name cannot be reported on afterwards.");

        if (lane.ProgramPaths.Count == 0)
        {
            return new EnqueueOutcome(EnqueueResult.Invalid,
                $"lane '{lane.Name}' names no program under test. A lane that deploys nothing is not a lane — and the union check "
                + "this batch exists for has nothing to examine.");
        }

        foreach (var (path, what) in new[] { (lane.BindingPath, "binding"), (lane.SubmissionPath, "submission") })
        {
            if (!File.Exists(path))
                return new EnqueueOutcome(EnqueueResult.Invalid, $"lane '{lane.Name}': {what} not found at {path}.");
        }

        Directory.CreateDirectory(_root);

        var path2 = PathFor(lane.Name);
        var temp = Path.Combine(_root, $".{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(lane, Json));

            try
            {
                File.Move(temp, path2, overwrite: false);
                return new EnqueueOutcome(EnqueueResult.Queued, $"lane '{lane.Name}' is queued for the next batch.");
            }
            catch (IOException)
            {
                // Refused, never overwritten. The submitter of the lane already there believes it is in
                // the batch; replacing it would drop a lane while reporting a success to the wrong agent.
                var existing = Read(path2);
                return new EnqueueOutcome(EnqueueResult.AlreadyQueued,
                    existing is null
                        ? $"lane '{lane.Name}' is already queued, and its file could not be read. Inspect {path2} by hand."
                        : $"lane '{lane.Name}' is already queued (binding {existing.BindingPath}). Refusing to replace it: "
                          + "whoever queued it is expecting it in this batch.");
            }
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { }
        }
    }

    /// <summary>Remove one lane. Used after a batch has run, and by an agent withdrawing.</summary>
    public bool Dequeue(string name)
    {
        var path = PathFor(name);
        if (!File.Exists(path))
            return false;

        try
        {
            File.Delete(path);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private string PathFor(string name) => Path.Combine(_root, Sanitize(name) + Extension);

    private static Lane? Read(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<Lane>(File.ReadAllText(path), Json);
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            return null;
        }
    }

    private static string Sanitize(string value)
    {
        var clean = new string(value.Trim().Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray());
        return clean.Length == 0 ? "lane" : clean;
    }
}
