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
    /// A deployment artifact is one of <b>THREE</b> shapes, and each is judged on the value it publishes
    /// rather than on how healthy it looks.
    ///
    /// <list type="bullet">
    /// <item><description><b><c>download-probe --json</c></b> — the first-hand report. Judged on
    /// <c>transferVerdict</c>. <b>This is the shape to capture</b>: it embeds the verbatim log AND the
    /// first-class <c>loadManifest</c>, so one redirected file satisfies this check and the deployment
    /// recomputation both.</description></item>
    /// <item><description><b>A <c>download-probe</c> LOG</b> — the same result after the probe's renderer
    /// has been at it. Judged on its rendered <c>TRANSFER VERDICT</c> line.</description></item>
    /// <item><description><b>A serialized loop result</b> — judged on <c>outcome</c>, where only
    /// <c>Ran</c> means the deployment actually happened.</description></item>
    /// </list>
    ///
    /// <para><c>NotDeployed</c> is the device refusing the load; <c>NotConfirmed</c> is a load whose
    /// version register did not match, which is worse than useless as provenance because the addresses
    /// the submission carries were derived for a build the device is not running. <b>An unrecognised
    /// outcome is rejected too — fail closed</b>, because a new outcome value will be added by someone
    /// who is not thinking about this check.</para>
    /// </summary>
    private static ArtifactVerdict Deployment(string content)
    {
        // 🔴 *** JSON IS TRIED FIRST, AND THE ORDER IS A MEASURED FIX RATHER THAN A TIDY-UP. ***
        //
        // The banner test below used to run first, and `download-probe --json` EMBEDS its verbatim log in
        // the report rather than pointing at it - so a real capture matches the banner, and `ProbeLog`
        // then reads the verdict token out of a JSON string literal and comes back with
        // `TRANSFERRED",` - closing quote, comma and all. MEASURED while wiring the deployment
        // recomputation: the FIRST-HAND artifact, the one the probe emits precisely so nothing has to
        // scrape its rendering, was refused by the check meant to admit it. A scrape of a rendering is
        // never preferred over a first-class field that says the same thing.
        JsonDocument? document = null;
        JsonException? parseFailure = null;

        try
        {
            document = JsonDocument.Parse(content);
        }
        catch (JsonException ex)
        {
            parseFailure = ex;
        }

        if (document is null)
        {
            // 🔴 *** A DOWNLOAD IS PERFORMED BY download-probe, WHICH EMITS A LOG - NOT A LoopResult. ***
            //
            // This check originally accepted only a serialized LoopResult, on the reasoning that the loop's
            // gateway performs the deployment. Measured on a real submission: that gateway has never run,
            // every actual download on this rig was done by `download-probe`, and its evidence is a text log.
            // So `deployment` was UNATTRIBUTABLE in practice - a field nothing could satisfy, which is a gate
            // that refuses correct work rather than one that catches anything.
            if (content.Contains("==== download-probe", StringComparison.Ordinal))
                return ProbeLog(content);

            return new ArtifactVerdict(false, $"WRONG KIND - the deployment artifact is neither a download-probe log nor valid JSON: {parseFailure!.Message}");
        }

        using (document)
        {
            // The probe's own report, keyed on the field it publishes for exactly this purpose.
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("transferVerdict", out var transferVerdict))
            {
                return ProbeJson(transferVerdict);
            }

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

    /// <summary>
    /// A <c>download-probe --json</c> report, judged on <b>its first-class <c>transferVerdict</c> key</b>.
    ///
    /// <para><b>The same rule as the log form, read off the value instead of the rendering.</b> The probe
    /// emits this key so a separate process never has to parse its own prose — and the harness gateway's
    /// notes record what parsing the prose costs: "anything the renderer drops is gone before this reader
    /// sees it".</para>
    ///
    /// <para><b>The whole token, never a substring, for the same reason as below:</b> the serialized enum
    /// values are <c>Transferred</c>, <c>NothingTransferred</c> and <c>Undetermined</c>, and the first is a
    /// substring of the second. <b>A null verdict is refused</b> — the probe writes null when no transfer
    /// classification exists at all (an abort, a throw, a run that never reached the download), which is
    /// "we cannot say" and never "it happened".</para>
    /// </summary>
    private static ArtifactVerdict ProbeJson(JsonElement verdict)
    {
        if (verdict.ValueKind != JsonValueKind.String)
        {
            return new ArtifactVerdict(false,
                "ARTIFACT REPORTS FAILURE - the download-probe report's `transferVerdict` is not a value, which is the probe "
                + "saying no transfer classification exists at all: an abort, a throw, or a run that never reached the "
                + "download. A run that cannot say whether anything reached the controller is not evidence that something did.");
        }

        var value = verdict.GetString() ?? string.Empty;

        return string.Equals(value, "Transferred", StringComparison.OrdinalIgnoreCase)
            ? new ArtifactVerdict(true, "a download-probe report whose transferVerdict is Transferred.")
            : new ArtifactVerdict(false,
                $"ARTIFACT REPORTS FAILURE - the download-probe report's own transferVerdict is '{value}', not "
                + "'Transferred'. The probe distinguishes a download that completed from one that moved anything, and only "
                + "the second is evidence of a deployment.");
    }

    /// <summary>
    /// A <c>download-probe</c> log, judged on <b>its own TRANSFER VERDICT and nothing else</b>.
    ///
    /// <para>*** THE PROBE'S OWN DOCUMENTATION SAYS WHY: "transfers NOTHING. Read the TRANSFER VERDICT,
    /// never the state." *** A download can report <c>state=Success</c> having moved nothing at all — the
    /// target was already up to date — so keying on the state would accept a deployment that did not
    /// happen.</para>
    ///
    /// <para><b>The whole token is matched, never a substring.</b> The three rendered values are
    /// <c>TRANSFERRED</c>, <c>NOTHINGTRANSFERRED</c> and <c>UNDETERMINED</c> — and <c>TRANSFERRED</c> is a
    /// substring of the failure case, so a <c>Contains</c> would read "nothing was transferred" as
    /// success. Anything that is not exactly the good token is refused: fail closed.</para>
    /// </summary>
    private static ArtifactVerdict ProbeLog(string content)
    {
        const string marker = "TRANSFER VERDICT : ";

        var at = content.IndexOf(marker, StringComparison.Ordinal);
        if (at < 0)
        {
            return new ArtifactVerdict(false,
                "ARTIFACT REPORTS FAILURE - the download-probe log carries no TRANSFER VERDICT line at all, so it "
                + "does not say whether anything reached the controller. A run that cannot say is not evidence that it did.");
        }

        var rest = content[(at + marker.Length)..];
        var end = rest.IndexOfAny(new[] { '\r', '\n', ' ' });
        var verdict = (end < 0 ? rest : rest[..end]).Trim();

        return string.Equals(verdict, "TRANSFERRED", StringComparison.Ordinal)
            ? new ArtifactVerdict(true, "a download-probe log whose TRANSFER VERDICT is TRANSFERRED.")
            : new ArtifactVerdict(false,
                $"ARTIFACT REPORTS FAILURE - the download-probe log's own TRANSFER VERDICT is '{verdict}', not "
                + "'TRANSFERRED'. The probe distinguishes a download that completed from one that moved anything, "
                + "and only the second is evidence of a deployment.");
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
        DerivableField.Deployment => CompareDeployment(document, artifact),
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

        // `default` author, stated rather than defaulted: this map is a SCRATCH VALUE for the set
        // comparison below and never reaches a gate — only `ProvidedFor`'s keys and modes are read from
        // it. Gate 5c adjudicates the map GateCli derives, which does carry `binding.DeclaredBy`.
        var computed = MirrorObservability.FromBindings(signals, default);

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

        var reachable = read.Entries.Values.SelectMany(v => v).Select(Canonical).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var paths = declared
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Value?.Path))
            .Select(kv => (Signal: kv.Key, Path: kv.Value!.Path!))
            .ToArray();

        if (paths.Length == 0)
            return NotComparable("the submission's storage entries declare no paths.");

        var absent = paths
            .Where(p => !reachable.Contains(Canonical(p.Path)))
            .Select(p => $"{p.Signal} -> {p.Path}")
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();

        // 🔴 *** A TOTAL MISS IS NOT N FINDINGS - IT IS THE WRONG CLOSURE, OR TWO VOCABULARIES. ***
        //
        // Measured on a real job twice over. Once where every declared path was absent because the
        // closure covered a different part of the program entirely; and once where the closure names
        // locations as `FB_X|A.B` while the submission declares them as `iDB_Y.A.B` - the same locations
        // under names no textual rule can join, because an instance DB's name does not contain its FB's.
        // Reported as a mismatch either way, that is a spurious accusation against correct declarations.
        if (absent.Length == paths.Length)
        {
            return NotComparable(
                $"none of the {paths.Length} declared storage path(s) appear in this closure. Either it was computed over "
                + "different blocks, or the two documents name locations differently - a closure says `FB_X|A.B` where a "
                + "submission may say `iDB_Y.A.B`, and an instance DB's name does not contain its FB's, so no textual rule "
                + "joins them. NOTHING IS BEING ASSERTED ABOUT THESE DECLARATIONS EITHER WAY: this field is cited, not checked.");
        }

        return absent.Length == 0
            ? Matched($"all {paths.Length} declared storage path(s) appear in the reachable-state closure.")
            : Differs($"{absent.Length} of {paths.Length} declared storage path(s) do not appear in the reachable-state closure: {string.Join(", ", absent)}");
    }

    /// <summary>
    /// 🔴 <b>ONE STORAGE PATH, NORMALISED ONLY FOR THE SEPARATOR — THE ROOT IS NEVER DROPPED.</b>
    ///
    /// <para>*** DROPPING THE ROOT WAS A FALSE-POSITIVE FACTORY, AND IT WAS MEASURED. *** A first cut
    /// also yielded the path with everything before the first dot removed, so that an
    /// <c>iDB_X.A.B</c> declaration could meet an <c>FB_X|A.B</c> closure entry. It did — and it also
    /// matched <c>iDB_Alpha.IO.Flag</c> against <c>FB_Beta|Sub.IO.Flag</c>, a
    /// completely different location in a closure that covered none of the block under test. Three of
    /// four declarations "matched" that way, against an artifact that mentions none of them.</para>
    ///
    /// <para><b>A suffix is not an identity.</b> Instance-DB names cannot be mapped to their FB textually
    /// — an <c>iDB_Alpha</c> may be an instance of some <c>FB_Gamma</c> — so where the two documents use
    /// different vocabularies the honest answer is that the comparison could not be made, which is what
    /// the caller reports. It is not a licence to match on whatever the tail happens to be.</para>
    /// </summary>
    private static string Canonical(string path) => path.Replace('|', '.');

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

    // -------------------------------------------------------------------------------------------------
    // deployment — the object manifest, rebuilt from the download that produced it
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>GATE 5b DIFFERENCES A LATCH BLOCK AGAINST THE DEPLOYMENT'S OBJECT MANIFEST. UNTIL NOW THAT
    /// MANIFEST WAS THE AUTHOR'S OWN LIST.</b>
    ///
    /// <para><c>deployment</c> fell to <see cref="Check"/>'s default arm — <b>cited, never compared</b>,
    /// stamped <c>Attributed</c>. So the set 5b looks a block up in was a self-report, and
    /// <c>DeploymentDocument.DeliverableObjects</c> says so in its own words: <i>"an author who omits a
    /// deliverable's name buys silence on that one row."</i> This arm rebuilds the set from the download's
    /// load manifest and differences the two, which is the difference between 5b returning a verdict and
    /// 5b confirming a claim against itself.</para>
    ///
    /// <para>🔴 <b>THE ASYMMETRY IS <see cref="CompareMap"/>'S, DELIBERATELY AND FOR THE SAME REASON.</b>
    /// OVER-CLAIMING IS DANGEROUS: a submission naming an object the download did not load makes 5b
    /// report that a latching block is present when nothing put it there — the exact reassurance 5b
    /// exists to stop being free. UNDER-CLAIMING IS NOT: a download that loaded MORE than the submission
    /// lists means the manifest is incomplete, and nothing can rely on an object it does not name.
    /// Refused in the first direction, REPORTED in the second.</para>
    ///
    /// <para>⚠️ <b>THE PRECONDITION, AND IT IS NOT ENFORCEABLE FROM HERE.</b> A captured manifest is only
    /// comparable against a submission describing <b>THE SAME PROGRAM</b>, loaded the same way. Two
    /// recorded downloads on this rig put DIFFERENT programs on the device; and a differential run
    /// (<c>SoftwareOnlyChanges</c>) loads only what changed, so its manifest is a legitimate SUBSET of the
    /// program on the controller. Compared across either boundary a correctly-authored manifest
    /// over-claims, and this arm refuses it. <b>Nothing in the two documents joins them</b> — an
    /// <c>importStamp</c> names an import, a probe log names a project path, and neither is the other —
    /// so the pairing is the operator's to get right, and the refusal text says so rather than letting a
    /// mispairing read as a finding about the submission.</para>
    /// </summary>
    private static Result CompareDeployment(SubmissionDocument document, string artifact)
    {
        if (document.Deployment is not { } deployment)
            return NotComparable("the submission declares no deployment, so there is no object manifest to compare against the download.");

        // The same two lists gate 5b differences a latch block against, built the same way — trimmed,
        // ordinal, unioned. Two constructions of one manifest is two manifests.
        var declared = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in deployment.S7Objects ?? new List<S7ObjectDocument>())
        {
            if (!string.IsNullOrWhiteSpace(row.HarnessObject))
                declared.Add(row.HarnessObject!.Trim());
        }

        foreach (var name in deployment.DeliverableObjects ?? new List<string>())
        {
            if (!string.IsNullOrWhiteSpace(name))
                declared.Add(name.Trim());
        }

        if (declared.Count == 0)
        {
            return NotComparable(
                "the submission's `deployment` names no object at all — neither an `s7Objects[].harnessObject` nor a "
                + "`deliverableObjects` entry — so there is nothing to look up in the download's load manifest. "
                + "`noS7Transport` and `s7Objects: []` are claims about the classic-S7comm WIRE, not about what was loaded.");
        }

        var (loaded, source, detail) = LoadedObjects(artifact);

        if (loaded is null)
        {
            return NotComparable(
                $"the deployment artifact carries no load manifest, so the {declared.Count} declared object(s) were compared "
                + $"against nothing: {detail} NOTHING IS BEING ASSERTED ABOUT THIS MANIFEST EITHER WAY — this field is cited, not checked.");
        }

        // *** AN EMPTY MANIFEST IS NOT A CLEAN ONE, AND GATE 5b ALREADY RULED THIS EXACT CASE. *** Its own
        // words: treating "absent from an empty list" as a refusal "would report the same finding for a
        // submission that named a real loaded block as for one that named a fiction. Both are unverified;
        // only one is wrong." A download whose result exists but reports no object load — every load a
        // hardware or connection one — can confirm and deny nothing about a program object.
        if (loaded.Count == 0)
        {
            return NotComparable(
                $"the download's result was read (via {source}) and names NO loaded program object, so the {declared.Count} "
                + $"declared object(s) were compared against an empty set: {detail} Absent from an empty manifest is the same "
                + "finding for a real object as for a fiction, which is not a finding.");
        }

        var overClaimed = declared.Except(loaded, StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToArray();
        var notListed = loaded.Except(declared, StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToArray();

        if (overClaimed.Length > 0)
        {
            // NAMED, never counted — the finding has to be actionable in one reading, and a case-only miss
            // is named as such so the refusal argues rather than merely walls (gate 5b's precedent; §4.5
            // object names are compared ORDINALLY throughout).
            var named = overClaimed.Select(o =>
            {
                var nearly = loaded.FirstOrDefault(l => string.Equals(l, o, StringComparison.OrdinalIgnoreCase));
                return nearly is null ? o : $"{o} (the manifest carries '{nearly}', differing only in case — compared ordinally, so this is a miss)";
            });

            return Differs(
                $"the submission's deployment names {overClaimed.Length} of its {declared.Count} object(s) that the download "
                + $"DID NOT LOAD: {string.Join(", ", named)}. Read off {loaded.Count} loaded object(s) via {source}. "
                + "*** BEFORE TREATING THIS AS THE SUBMISSION'S FAULT, CHECK THE PAIRING. *** A load manifest only speaks to "
                + "the submission that describes THE SAME PROGRAM, loaded the same way: a differential run "
                + "(`SoftwareOnlyChanges`) loads only what changed and its manifest is a legitimate subset, and a log from a "
                + "download of a different program shares no object names at all. Nothing in these two documents joins them, "
                + "so pairing them is the operator's act. If the pairing is right, this is a manifest asserting an object "
                + "nothing put on the device — and gate 5b would report a latching block present on the strength of it.");
        }

        var incomplete = notListed.Length == 0
            ? string.Empty
            : $" (the download also loaded {notListed.Length} object(s) the deployment does not name: {string.Join(", ", notListed)}; incomplete, not wrong)";

        return Matched(
            $"all {declared.Count} object(s) the submission's deployment names appear in the download's load manifest, "
            + $"read off {loaded.Count} loaded object(s) via {source}.{incomplete}");
    }

    /// <summary>
    /// The download's load manifest, and <b>WHICH AUTHORITY PRODUCED IT</b>.
    ///
    /// <para>🔴 <b>FIRST-HAND FIRST.</b> <c>download-probe --json</c> emits <c>loadManifest</c> built by
    /// <c>DownloadResultAdapter</c> straight off the live Openness <c>DownloadResult</c>; the probe LOG is
    /// that same result after the probe's renderer has been at it, and <c>ProbeLogReader</c>'s own
    /// documentation warns that "anything the renderer drops is gone before this reader sees it". Both are
    /// read here because both are things an operator can be holding, and <b>which one was used is carried
    /// into every verdict this produces</b> — <c>ProbeReport.Source</c> exists for exactly that, and a
    /// result that does not say which authority it used invites the stronger reading.</para>
    ///
    /// <para><b>A null manifest is NOBODY LOOKED, never "nothing was loaded".</b> An aborted run, a throw,
    /// a folder download and a loop-result JSON all produce no manifest at all, and reading any of them as
    /// an empty set would turn "we cannot say" into "the download loaded nothing" at the first consumer.
    /// <c>ProbeReport</c> keeps that distinction, and it is preserved here rather than re-decided.</para>
    /// </summary>
    private static (IReadOnlySet<string>? Loaded, string Source, string Detail) LoadedObjects(string artifact)
    {
        if (artifact.TrimStart().StartsWith("{", StringComparison.Ordinal))
        {
            // The probe's own stdout — `loadManifest` when the build emits one, its embedded `log` when it
            // does not. A serialized loop result lands here too and yields no manifest, which is correct:
            // `outcome: Ran` says a deployment happened and names not one object.
            var report = Harness.Device.ProbeReport.FromStdout(artifact);

            return report.ManifestAvailable
                ? (report.Manifest, report.Source, report.Detail)
                : (null, report.Source, report.Detail);
        }

        var feedback = Ladder.Download.DownloadFeedbackParser.Parse(Ladder.Download.ProbeLogReader.Read(artifact));

        if (!feedback.ResultPresent)
        {
            return (null, nameof(Ladder.Download.ProbeLogReader),
                "the log carries no `==== DOWNLOAD RESULT` section, so no result object ever existed — an abort or a throw. "
                + "That is UNDETERMINED, which is not the same claim as nothing having been transferred.");
        }

        return (new HashSet<string>(feedback.LoadedObjects, StringComparer.Ordinal), nameof(Ladder.Download.ProbeLogReader),
            $"{feedback.LoadedObjectCount} object(s), {feedback.NonObjectLoadCount} non-object load(s), "
            + $"{feedback.UnrecognisedMessageCount} unrecognised message(s). RE-DERIVED from the probe's rendering of the "
            + "result, not read off the live DownloadResult — anything that renderer dropped is already gone.");
    }

    /// <summary>Undirected: a conflict between two blocks is one edge however it was written down.</summary>
    private static string Key(string a, string b) =>
        string.CompareOrdinal(a, b) <= 0 ? $"{a} {b}" : $"{b} {a}";
}
