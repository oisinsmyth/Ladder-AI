using System.Text.Json;
using Harness.Results;

namespace Harness.Gate;

/// <summary>Whether an artifact may be used as evidence, and why not when it may not.</summary>
public sealed record ArtifactVerdict(bool Accepted, string Detail);

/// <summary>
/// 🔴 <b>AN ARTIFACT BEING PRESENT IS NOT THE ARTIFACT BEING AN ANSWER.</b>
///
/// <para>*** MEASURED ON A REAL JOB, TWICE, AND ATTRIBUTION ALONE WOULD HAVE STAMPED BOTH. *** The
/// artifact that should have produced the conflict edges was a <c>notComputed</c> report — it resolved
/// none of the submission's signals and said so — while the submission declared the edges anyway. The
/// deployment artifact read <c>outcome: NotDeployed</c>, its own detail stating the device was not
/// running the staged build. Both exist, both are readable, both hash perfectly.</para>
///
/// <para><b>Two rejection classes, kept apart because they are different mistakes with different
/// fixes:</b> WRONG KIND means you pointed the tool at the wrong file; REPORTS FAILURE means the right
/// file, and it says it has no answer.</para>
/// </summary>
public static class ArtifactCheck
{
    /// <summary>Inspect an artifact against the kind its producer is supposed to emit.</summary>
    public static ArtifactVerdict Inspect(ArtifactKind kind, string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        return kind switch
        {
            ArtifactKind.Binding => Binding(content),
            ArtifactKind.ReachableState => Composer(ComposerArtifacts.ReadReachableState(content)),
            ArtifactKind.ConflictGraph => Composer(ComposerArtifacts.ReadConflictGraph(content)),
            ArtifactKind.DeployResult => Deployment(content),
            ArtifactKind.TagMap => TagMap(content),
            _ => new ArtifactVerdict(true, "no artifact kind is required for this producer."),
        };
    }

    private static ArtifactVerdict Binding(string content)
    {
        try
        {
            var binding = BindingDocument.Read(content);
            return binding.Slots is { Count: > 0 }
                ? new ArtifactVerdict(true, "a binding document naming at least one slot.")
                : new ArtifactVerdict(false, "WRONG KIND - the artifact parses but names no slots, so it cannot be the binding this map came from.");
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            return new ArtifactVerdict(false, $"WRONG KIND - the artifact is not a binding document: {ex.Message}");
        }
    }

    private static ArtifactVerdict Composer(ComposerRead read) => read.Outcome switch
    {
        ComposerReadOutcome.Read => new ArtifactVerdict(true, read.Detail),

        // 🔴 The finding this whole check exists for.
        ComposerReadOutcome.NotComputed => new ArtifactVerdict(false,
            $"ARTIFACT REPORTS FAILURE - {read.Detail} A document that says it has no answer cannot be the "
            + "evidence for a field that claims one."),

        _ => new ArtifactVerdict(false, $"WRONG KIND - {read.Detail}"),
    };

    /// <summary>
    /// The deployment artifact is a serialized loop result, and <b>only <c>Ran</c> means the deployment
    /// actually happened.</b>
    ///
    /// <para><c>NotDeployed</c> is the device refusing the load; <c>NotConfirmed</c> is a load whose
    /// version register did not match, which is worse than useless as provenance because the addresses
    /// the submission carries were derived for a build the device is not running. <b>An unrecognised
    /// outcome is rejected too — fail closed</b>, because a new outcome value will be added by someone
    /// who is not thinking about this check.</para>
    /// </summary>
    private static ArtifactVerdict Deployment(string content)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(content);
        }
        catch (JsonException ex)
        {
            return new ArtifactVerdict(false, $"WRONG KIND - the deployment artifact is not valid JSON: {ex.Message}");
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("outcome", out var outcome)
                || outcome.ValueKind != JsonValueKind.String)
            {
                return new ArtifactVerdict(false,
                    "WRONG KIND - the deployment artifact carries no 'outcome', so it is not a loop result and cannot say whether anything was deployed.");
            }

            var value = outcome.GetString() ?? string.Empty;
            if (string.Equals(value, "Ran", StringComparison.OrdinalIgnoreCase))
                return new ArtifactVerdict(true, "a loop result whose outcome is Ran.");

            var detail = document.RootElement.TryGetProperty("detail", out var d) && d.ValueKind == JsonValueKind.String
                ? $" It says: {Truncate(d.GetString() ?? string.Empty)}"
                : string.Empty;

            return new ArtifactVerdict(false,
                $"ARTIFACT REPORTS FAILURE - the deployment artifact's own outcome is '{value}', not 'Ran'. "
                + "The deployment it describes did not complete, so it cannot be the provenance for a deployment "
                + "declaration." + detail);
        }
    }

    private static ArtifactVerdict TagMap(string content)
    {
        try
        {
            var map = Harness.S7.S7TagMap.FromJson(content);
            return map.Tags.Count > 0
                ? new ArtifactVerdict(true, $"an S7 tag map with {map.Tags.Count} tag(s).")
                : new ArtifactVerdict(false, "WRONG KIND - the artifact parses as a tag map with no tags, which reaches nothing.");
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or Harness.S7.S7ConfigurationException)
        {
            return new ArtifactVerdict(false, $"WRONG KIND - the artifact is not an S7 tag map: {ex.Message}");
        }
    }

    private static string Truncate(string text) =>
        text.Length <= 160 ? text : text[..160] + "...";
}

/// <summary>
/// 🔴 <b>RECOMPUTE THE VALUE FROM THE ARTIFACT AND COMPARE — the difference between evidence and a
/// citation.</b>
///
/// <para><b>Compare rather than overwrite, deliberately.</b> Overwriting would make an author's wrong
/// map vanish without anybody learning; comparing makes the disagreement impossible to miss. Once the
/// two agree, which copy survives does not matter.</para>
///
/// <para><b>Compared as SETS over identity, never as serialized text.</b> Ordering and formatting must
/// never produce a refusal — a gate that fires on whitespace is a gate that gets switched off.</para>
/// </summary>
public static class Recompute
{
    /// <summary>
    /// 🔴 <b>THREE OUTCOMES, BECAUSE "IT DID NOT DISAGREE" IS NOT "IT AGREED".</b>
    ///
    /// <para>A two-state check returning null for both <i>matched</i> and <i>nothing to compare</i> would
    /// let a field be stamped <c>Computed</c> when no comparison ran — this project's signature failure,
    /// committed by the very tool built to prevent it. Only <see cref="Matched"/> earns the strong
    /// label.</para>
    /// </summary>
    public enum Outcome
    {
        /// <summary>A comparison ran and the values agree. The only state that earns <c>Computed</c>.</summary>
        Matched = 0,

        /// <summary>Nothing to compare — the field has no in-harness recomputation, or the submission declared nothing. Stamped <c>Attributed</c>.</summary>
        NotComparable = 1,

        /// <summary>A comparison ran and the values disagree. A refusal.</summary>
        Differs = 2,
    }

    /// <summary>The result of trying to rebuild one field's value from its artifact.</summary>
    public sealed record Result(Outcome Outcome, string Detail);

    /// <summary>Rebuild <paramref name="field"/> from <paramref name="artifact"/> and compare.</summary>
    public static Result Check(string field, SubmissionDocument document, string artifact) => field switch
    {
        DerivableField.Map => CompareMap(document, artifact),
        DerivableField.Storage => CompareStorage(document, artifact),
        DerivableField.ConflictEdges => CompareConflicts(document, artifact),
        DerivableField.ComputedConflicts => CompareComputedConflicts(document, artifact),
        _ => NotComparable($"nothing in the harness recomputes '{field}', so it is cited rather than checked."),
    };

    private static Result Matched(string detail) => new(Outcome.Matched, detail);
    private static Result NotComparable(string detail) => new(Outcome.NotComparable, detail);
    private static Result Differs(string detail) => new(Outcome.Differs, detail);

    /// <summary>
    /// Rebuild the observability map from the binding — <b>the same call the loop makes</b>
    /// (<c>LoopRun</c> uses <c>MirrorObservability.FromBindings</c> over the slots' result sources), so
    /// the deriver and the loop cannot disagree about what the copy layer provides.
    /// </summary>
    private static Result CompareMap(SubmissionDocument document, string artifact)
    {
        if (document.Map?.ProvidedFor is not { } authored)
            return NotComparable("the submission declares no providedFor, so there is nothing to compare the binding against.");

        BindingDocument binding;
        try
        {
            binding = BindingDocument.Read(artifact);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            return Differs($"the binding could not be read, so the map could not be recomputed: {ex.Message}");
        }

        var signals = (binding.Slots ?? new List<SlotBindingDocument>())
            .SelectMany(s => s.ResultSources ?? new List<MirroredSignalDocument>())
            .Select(GateCli.ToMirroredSignal);

        var computed = MirrorObservability.FromBindings(signals);

        var authoredSignals = authored.Keys.ToHashSet(StringComparer.Ordinal);
        var computedSignals = computed.ProvidedFor.Keys.ToHashSet(StringComparer.Ordinal);

        // 🔴 *** THE COMPARISON IS ASYMMETRIC, AND THE ASYMMETRY IS THE WHOLE CORRECTNESS ARGUMENT. ***
        //
        // OVER-CLAIMING IS DANGEROUS: a submission naming a signal the copy layer does not carry, or a
        // mode it does not provide, can admit a vector that observes something not actually mirrored -
        // which is precisely what gate 5 exists to prevent.
        //
        // UNDER-CLAIMING IS NOT: a binding that provides MORE than the submission lists means only that
        // the submission's map is incomplete. Nothing can rely on a signal it does not name, and the
        // gate takes its map from the binding regardless.
        //
        // Measured on a real submission: the binding provided ~70 signals the map did not list. Refusing
        // that would have refused every honest submission in the job over a difference that cannot cause
        // a wrong result. It is REPORTED instead, so an incomplete map is still visible.
        var overClaimed = authoredSignals.Except(computedSignals, StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToArray();
        var notListed = computedSignals.Except(authoredSignals, StringComparer.Ordinal).ToArray();

        var overClaimedModes = authoredSignals.Intersect(computedSignals, StringComparer.Ordinal)
            .Where(s => !Modes(authored[s]).IsSubsetOf(computed.ProvidedFor[s].Select(m => m.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase)))
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();

        if (overClaimed.Length > 0 || overClaimedModes.Length > 0)
        {
            // NAMED, never counted: "the map differs" is exactly the finding that has to be actionable
            // in one reading.
            var parts = new List<string>();
            if (overClaimed.Length > 0)
                parts.Add($"signals the copy layer does not carry: {string.Join(", ", overClaimed)}");
            if (overClaimedModes.Length > 0)
                parts.Add($"instrumentation the copy layer does not provide, on: {string.Join(", ", overClaimedModes)}");

            return Differs("the submission's map claims more than the binding provides - " + string.Join("; ", parts));
        }

        var incomplete = notListed.Length == 0
            ? string.Empty
            : $" (the binding also provides {notListed.Length} signal(s) the map does not list; incomplete, not wrong)";

        return Matched($"every signal and mode the submission's map claims is provided by the binding, over {authoredSignals.Count} signal(s).{incomplete}");
    }

    private static HashSet<string> Modes(IEnumerable<string>? modes) =>
        (modes ?? Enumerable.Empty<string>()).ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Every declared storage path must appear in the reachable-state closure. <b>A containment check
    /// rather than an equality one</b>, deliberately: the closure legitimately covers more than one
    /// submission observes, so demanding equality would refuse every honest submission.
    /// </summary>
    private static Result CompareStorage(SubmissionDocument document, string artifact)
    {
        if (document.Map?.Storage is not { Count: > 0 } declared)
            return NotComparable("the submission declares no storage, so there is nothing to check against the closure.");

        var read = ComposerArtifacts.ReadReachableState(artifact);
        if (read.Outcome != ComposerReadOutcome.Read)
            return Differs(read.Detail);

        // *** BOTH SIDES ARE INDEXED UNDER THEIR ROOTED FORM AND THEIR SUFFIX. *** The closure
        // canonicalises an `iDB.<suffix>` reference onto the owning FB, so a storage entry written as
        // `iDB_X.A.B` and a closure entry written as `FB_X|A.B` are the SAME LOCATION under two
        // spellings. Comparing the rooted strings alone finds nothing and blames the submission.
        var reachable = read.Entries.Values.SelectMany(v => v).SelectMany(Forms).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var paths = declared
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Value?.Path))
            .Select(kv => (Signal: kv.Key, Path: kv.Value!.Path!))
            .ToArray();

        if (paths.Length == 0)
            return NotComparable("the submission's storage entries declare no paths.");

        var absent = paths
            .Where(p => !Forms(p.Path).Any(f => reachable.Contains(f)))
            .Select(p => $"{p.Signal} -> {p.Path}")
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();

        // 🔴 *** A TOTAL MISS IS NOT 131 FINDINGS - IT IS THE WRONG CLOSURE. ***
        //
        // Measured on a real job: every declared path was reported absent, because the closure covered a
        // different part of the program entirely. Reported as a mismatch, that is a spurious accusation
        // against 131 correct declarations - the same class of false finding the drift-check pairing bug
        // produced. When the two documents share NO vocabulary at all, the honest verdict is that this
        // comparison did not run, said out loud, with the likely cause named.
        if (absent.Length == paths.Length)
        {
            return NotComparable(
                $"none of the {paths.Length} declared storage path(s) appear in this closure, and the two share no "
                + "vocabulary at all. That reads as the wrong closure for this submission rather than as every "
                + "declaration being wrong, so nothing is being asserted about them either way.");
        }

        return absent.Length == 0
            ? Matched($"all {paths.Length} declared storage path(s) appear in the reachable-state closure.")
            : Differs($"{absent.Length} of {paths.Length} declared storage path(s) do not appear in the reachable-state closure: {string.Join(", ", absent)}");
    }

    /// <summary>
    /// The spellings one storage location can legitimately wear: the path as written, and the same path
    /// with its root dropped — which is what makes an <c>iDB_X.A.B</c> declaration and an
    /// <c>FB_X|A.B</c> closure entry recognisable as one location.
    /// </summary>
    private static IEnumerable<string> Forms(string path)
    {
        yield return path;

        var bar = path.IndexOf('|');
        if (bar >= 0 && bar < path.Length - 1)
            yield return path[(bar + 1)..];

        var dot = path.IndexOf('.');
        if (dot >= 0 && dot < path.Length - 1)
            yield return path[(dot + 1)..];
    }

    /// <summary>Every declared conflict edge must appear in the graph the artifact carries.</summary>
    private static Result CompareConflicts(SubmissionDocument document, string artifact)
    {
        var declared = (document.ConflictEdges ?? new List<ConflictEdgeDocument>())
            .Select(e => (A: e.BlockA ?? string.Empty, B: e.BlockB ?? string.Empty))
            .Where(e => !string.IsNullOrWhiteSpace(e.A) && !string.IsNullOrWhiteSpace(e.B))
            .ToArray();

        var read = ComposerArtifacts.ReadConflictGraph(artifact);
        if (read.Outcome != ComposerReadOutcome.Read)
            return Differs(read.Detail);

        if (declared.Length == 0)
        {
            // An empty `conflictEdges` is the earned claim "the graph ran and found nothing", so the
            // check that MEANS something here is that the graph really did run — which the kind check
            // above has just established. There is no per-edge comparison left to make.
            return NotComparable("the submission declares no conflict edges; the artifact confirms a graph that ran.");
        }

        var edges = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (subject, others) in read.Entries)
            foreach (var other in others)
                edges.Add(Key(subject, other));

        var absent = declared
            .Where(e => !edges.Contains(Key(e.A, e.B)))
            .Select(e => $"{e.A}~{e.B}")
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();

        return absent.Length == 0
            ? Matched($"all {declared.Length} declared conflict edge(s) appear in the artifact's graph.")
            : Differs($"{absent.Length} declared conflict edge(s) are not in the graph the artifact carries: {string.Join(", ", absent)}");
    }

    /// <summary>
    /// The bare-name form. Each named block must appear in the graph as a subject.
    ///
    /// <para>An empty list is the earned "graph ran, found nothing" claim, exactly as for the edge
    /// form — and it is <see cref="Outcome.NotComparable"/> rather than a match, because confirming the
    /// graph ran is the kind check's job and nothing here was actually compared.</para>
    /// </summary>
    private static Result CompareComputedConflicts(SubmissionDocument document, string artifact)
    {
        var declared = (document.ComputedConflicts ?? new List<string>())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToArray();

        var read = ComposerArtifacts.ReadConflictGraph(artifact);
        if (read.Outcome != ComposerReadOutcome.Read)
            return Differs(read.Detail);

        if (declared.Length == 0)
            return NotComparable("the submission declares no computed conflicts; the artifact confirms a graph that ran.");

        var subjects = read.Entries.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var others in read.Entries.Values)
            foreach (var other in others)
                subjects.Add(other);

        var absent = declared
            .Where(n => !subjects.Contains(n))
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();

        return absent.Length == 0
            ? Matched($"all {declared.Length} named conflicting block(s) appear in the artifact's graph.")
            : Differs($"{absent.Length} named conflicting block(s) are not in the graph the artifact carries: {string.Join(", ", absent)}");
    }

    /// <summary>Undirected: a conflict between two blocks is one edge however it was written down.</summary>
    private static string Key(string a, string b) =>
        string.CompareOrdinal(a, b) <= 0 ? $"{a} {b}" : $"{b} {a}";
}
