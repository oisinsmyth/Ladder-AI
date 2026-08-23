using System.Text.Json;
using Harness.Map;

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
/// <param name="StagedObjects">
/// 🔴 <b>WHAT THIS LANE STAGED, BY NAME — the DENOMINATOR the build stamp is measured against.</b>
///
/// <para><see cref="ProgramPaths"/> feeds the numerator: it is what <c>--program</c> hands the stamp to
/// hash. It cannot serve as the denominator, because a path is a FILE and the stamp matches on the
/// object NAME TIA imports by — and because a short program list and a complete one look identical
/// against a corpus derived from that same list.</para>
///
/// <para><b>Derived from the lane manifest at enqueue, never typed.</b> A lane enqueued from a bare
/// <c>--program</c> list has none, and null/empty is the honest answer: <see cref="LaneCorpus"/> then
/// supplies NO corpus and the run says <i>NO STAGED CORPUS WAS SUPPLIED</i> rather than measuring itself
/// against a denominator nobody stated. <b>Nullable so a <c>.lane</c> file written before this field
/// existed still reads</b> — and reads as "nobody said", which is what it is.</para>
/// </param>
public sealed record Lane(
    string Name,
    string BindingPath,
    string SubmissionPath,
    IReadOnlyList<string> ProgramPaths,
    string Purpose = "",
    IReadOnlyList<string>? StagedObjects = null)
{
    /// <summary>The staged names, with "nobody said" and "nothing staged" both reading as empty.</summary>
    public IReadOnlyList<string> Staged => StagedObjects ?? Array.Empty<string>();

    /// <summary>
    /// This lane's rows of the denominator, <b>each carrying the document a reader must go and fix</b>.
    ///
    /// <para>The source is the LANE, derived from its own name rather than stored beside it: a gap
    /// reported against a bare object name is a gap nobody can trace to a manifest.</para>
    /// </summary>
    public IEnumerable<StagedCorpusEntry> StagedCorpusEntries =>
        Staged.Select(name => new StagedCorpusEntry(name, $"lane '{Name}'"));
}

/// <summary>
/// 🔴 <b>THE PROJECTION — lane manifests into the one denominator the build stamp is measured against.</b>
///
/// <para><b>It lives HERE and not in <c>Harness.Map</c>, and that is structural rather than stylistic.</b>
/// <c>Harness.Map</c> holds zero project references — which is what makes it testable with no device, no
/// rig and no TIA session — and <c>Harness.Batch</c> depends on IT. So <see cref="StagedCorpus"/> takes
/// plain <c>(Name, Source)</c> rows and never the <see cref="LaneManifest"/> type, and the caller does the
/// projection. This is that caller.</para>
///
/// <para><see cref="StagedCorpus.Union"/> does the merging, so an object two lanes both stage keeps BOTH
/// sources — one lane's copy silently winning would make a gap report name the wrong document.</para>
/// </summary>
public static class LaneCorpus
{
    /// <summary>
    /// The union of every lane's staged objects, or the reason there is none. <b>One derivation, two
    /// consumers</b>: <c>BatchRunPlan</c> reads the corpus, <c>BatchCli</c> prints the sentence, and a
    /// second computation of "was there a denominator" is how the report and the command line would come
    /// to disagree.
    ///
    /// <para>🔴 <b>NULL, NEVER AN EMPTY CORPUS.</b> An empty one renders as <i>"hashed n of 0"</i> over an
    /// empty gap list — the exact shape of a check that examined nothing — and <see cref="StagedCorpus.Of"/>
    /// refuses it at construction for that reason. Null renders as NO DENOMINATOR, in words.</para>
    ///
    /// <para>🔴 <b>AND A PARTIAL DENOMINATOR IS REFUSED TOO, WHICH IS THE LESS OBVIOUS HALF.</b> The lanes
    /// share ONE deployment, so the corpus is a claim about that whole deployment. If one lane declares a
    /// manifest and another does not, a union of what happens to be known reads as <i>"6 of 9"</i> for a
    /// batch that staged more than nine — <b>which is the very defect one level up</b>: a short denominator
    /// that looks complete, and the reason this mechanism exists at all. So the batch says NO DENOMINATOR
    /// and names the lanes that owe a manifest, rather than publishing a number it knows is short.</para>
    /// </summary>
    public static LaneCorpusFact Of(IReadOnlyList<Lane> lanes)
    {
        ArgumentNullException.ThrowIfNull(lanes);

        if (lanes.Count == 0)
        {
            return new LaneCorpusFact(null,
                "NO DENOMINATOR: there are no lanes, so nothing stated what this deployment stages.");
        }

        var silent = lanes.Where(l => l.Staged.Count == 0).Select(l => l.Name).ToArray();

        if (silent.Length > 0)
        {
            return new LaneCorpusFact(null,
                $"NO DENOMINATOR: {silent.Length} of {lanes.Count} lane(s) declared no manifest ({string.Join(", ", silent)}), "
                + "so nothing states what they stage. The lanes share ONE deployment, and a corpus covering only the lanes that "
                + "happen to have one would report a denominator SHORTER than the deployment — which is exactly the defect this "
                + "measures: a stamp over 8 of 9 objects reads identically to a complete one. Every wave will report NO STAGED "
                + "CORPUS instead. Enqueue those lanes with --manifest to get a real n of m.");
        }

        StagedCorpus? corpus = null;

        foreach (var lane in lanes)
        {
            var mine = StagedCorpus.Of(lane.StagedCorpusEntries);
            corpus = corpus is null ? mine : corpus.Union(mine);
        }

        return new LaneCorpusFact(corpus,
            $"{corpus!.Count} distinct object(s) staged across {lanes.Count} lane(s) — the denominator every wave's build "
            + "stamp will be reported against.");
    }
}

/// <summary>
/// The denominator the batch derived, <b>or the reason it has none — never silence</b>. Both states are
/// printed, because a corpus line that appears only when there is one teaches a reader that its absence
/// means everything was covered.
/// </summary>
public sealed record LaneCorpusFact(StagedCorpus? Corpus, string Detail)
{
    /// <summary>Whether a denominator exists at all. False means every wave will say so out loud.</summary>
    public bool Stated => Corpus is not null;
}

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
