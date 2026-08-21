using System.Text.Json;

namespace Harness.Results;

/// <summary>
/// What reading a composer's output produced — <b>and the three outcomes are deliberately distinct.</b>
///
/// <para>*** THE ONE THAT MATTERS IS <see cref="NotComputed"/>. *** A composer that could not do its
/// job writes a report SAYING so, and that document is readable, parseable and hashes perfectly. Folded
/// into "empty" it becomes a clean answer — measured on a real job, where a conflict graph that
/// resolved none of the submission's signals sat behind a submission declaring the edges anyway.</para>
/// </summary>
public enum ComposerReadOutcome
{
    /// <summary>The document parsed and carries content.</summary>
    Read = 0,

    /// <summary>
    /// <b>The document is the right kind and says it has no answer.</b> Not an error and not an empty
    /// result: the composer ran, refused, and recorded why.
    /// </summary>
    NotComputed = 1,

    /// <summary>Not this kind of document at all — malformed, or missing the keys that identify it.</summary>
    NotThisKind = 2,
}

/// <summary>One composer artifact, read.</summary>
/// <param name="Outcome">Which of the three states the document is in.</param>
/// <param name="Entries">
/// The content, keyed by subject (a block name, or an edge key). Empty whenever
/// <see cref="Outcome"/> is not <see cref="ComposerReadOutcome.Read"/> — <b>and callers must branch on
/// the outcome, never on the count</b>, which is the whole point of the enum.
/// </param>
/// <param name="Detail">Why, in the document's own words where it gave a reason.</param>
public sealed record ComposerRead(
    ComposerReadOutcome Outcome,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Entries,
    string Detail);

/// <summary>
/// 🔴 <b>READERS FOR THE TWO COMPOSER OUTPUTS THE HARNESS DOES NOT PRODUCE ITSELF.</b>
///
/// <para><b>A FILE CONTRACT, NOT A PROJECT REFERENCE, AND THAT IS DELIBERATE.</b> These documents are
/// written by <c>converter reachable-state</c> and by the wave-control slot-conflict derivation, which
/// live in a different solution — and <c>src/harness/Directory.Build.props</c> keeps this assembly
/// dependency-free on purpose, so that changing the harness never drags in the Openness approval cycle
/// or anything else. Referencing wave-control to reuse its reader would buy correctness at the cost of
/// the property the whole harness is built on.</para>
///
/// <para><b>The accepted cost is that this is a SECOND reader of one format.</b> It is mitigated the
/// only way that actually works: both sides key on the same <c>notComputed</c> convention, and this one
/// <b>refuses anything it does not recognise instead of returning empty</b>. A drifted format therefore
/// surfaces as NotThisKind — loud — rather than as a clean read of nothing.</para>
/// </summary>
public static class ComposerArtifacts
{
    /// <summary>
    /// Read a <c>converter reachable-state</c> document: block → the storage paths it reaches.
    ///
    /// <para><b>Both levels of <c>notComputed</c> are honoured</b>, matching the producer's own
    /// convention: the whole report can refuse (it writes <c>notComputed</c> INSTEAD OF <c>blocks</c>),
    /// and a single block's closure can be withheld by name while the rest are computed. A withheld
    /// block is not a block that reaches nothing.</para>
    /// </summary>
    public static ComposerRead ReadReachableState(string json) =>
        ReadKeyed(json, "blocks", "block", "reachableState", "reachable-state", emptyIsAnAnswer: false);

    /// <summary>
    /// Read a conflict-graph document: block → the blocks it conflicts with.
    ///
    /// <para>Accepts either <c>edges</c> (the derived form) or <c>blocks</c>, because the two spellings
    /// exist in the wild; an unrecognised shape is <see cref="ComposerReadOutcome.NotThisKind"/> rather
    /// than an empty graph — <b>an empty graph is the claim "there are no conflicts", which is exactly
    /// the false statement this reader must never manufacture.</b></para>
    /// </summary>
    public static ComposerRead ReadConflictGraph(string json)
    {
        // 🔴 *** THE DOCUMENT'S OWN `derivation.computed` FLAG OUTRANKS EVERY INFERENCE BELOW. *** A real
        // conflict graph states whether it was computed, with the corpus size and the resolved/unresolved
        // counts beside it. Guessing from the shape when the document says so outright is how a report of
        // failure gets read as a clean answer.
        var declared = DeclaredComputed(json);
        if (declared is { } notComputed)
            return notComputed;

        // Three spellings exist in the wild and all three are real. `conflictEdges` is what the live
        // producer emits and was WRONGLY refused as NotThisKind until a dry run over a real job's fully
        // computed graph - 36 signals examined, 36 resolved - reported it as neither an answer nor a
        // refusal. A valid artifact called unusable is as damaging as an invalid one accepted.
        foreach (var (arrayKey, subjectKey, valueKey) in new[]
                 {
                     ("conflictEdges", "blockA", "blockB"),
                     ("edges", "blockA", "blockB"),
                     ("blocks", "block", "conflictsWith"),
                 })
        {
            var read = ReadKeyed(json, arrayKey, subjectKey, valueKey, "conflict-graph", emptyIsAnAnswer: true);
            if (read.Outcome != ComposerReadOutcome.NotThisKind)
                return read;
        }

        return NotThisKind(
            "the conflict-graph document carries none of 'conflictEdges', 'edges' or 'blocks', and no 'notComputed' "
            + "reason, so it is neither an answer nor a refusal.");
    }

    /// <summary>
    /// The document's own verdict on whether it was computed, where it states one.
    ///
    /// <para>Returns a <see cref="ComposerReadOutcome.NotComputed"/> read when the document says it was
    /// NOT computed, and null when it says nothing or says it was — in which case the shape checks below
    /// decide. <b>An explicit false is authoritative and needs no inference.</b></para>
    /// </summary>
    private static ComposerRead? DeclaredComputed(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);

            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("derivation", out var derivation)
                || derivation.ValueKind != JsonValueKind.Object
                || !derivation.TryGetProperty("computed", out var computed)
                || computed.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                return null;
            }

            if (computed.ValueKind == JsonValueKind.True)
                return null;

            var unresolved = derivation.TryGetProperty("signalsUnresolved", out var u) ? u.ToString() : "?";
            return new ComposerRead(ComposerReadOutcome.NotComputed,
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal),
                $"the conflict-graph document states derivation.computed = false ({unresolved} signal(s) unresolved).");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// The shared shape: a root refusal, or an array of entries each naming a subject and a list.
    /// </summary>
    /// <param name="valueKey">
    /// The per-entry list. <b>When the value is a STRING rather than a list it is read as a one-element
    /// list</b>, which is how an edge document names its far end.
    /// </param>
    /// <param name="emptyIsAnAnswer">
    /// 🔴 <b>Whether an EMPTY array is a result or a non-result, and the two kinds genuinely differ.</b>
    ///
    /// <para>For a conflict graph, <c>[]</c> is the EARNED positive claim "the graph ran over the corpus
    /// and found no conflicts" — the same meaning the submission's own empty <c>conflictEdges</c>
    /// carries, and refusing it would refuse every clean project. For a reachable-state closure it is
    /// not: the producer withholds its key and exits non-zero rather than emitting zero blocks, so an
    /// empty array there means something went wrong quietly.</para>
    /// </param>
    private static ComposerRead ReadKeyed(string json, string arrayKey, string subjectKey, string valueKey, string kind, bool emptyIsAnAnswer)
    {
        ArgumentNullException.ThrowIfNull(json);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            return NotThisKind($"the {kind} document is not valid JSON: {ex.Message}");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return NotThisKind($"the {kind} document's root is {root.ValueKind}, not an object.");

            // *** THE ROOT REFUSAL, CHECKED BEFORE THE CONTENT. *** The producer writes `notComputed`
            // INSTEAD OF the array, so looking for content first would find none and call it empty.
            if (root.TryGetProperty("notComputed", out var why))
            {
                return new ComposerRead(ComposerReadOutcome.NotComputed,
                    new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal),
                    $"the {kind} document reports that it could not be computed: {Text(why)}");
            }

            if (!root.TryGetProperty(arrayKey, out var array) || array.ValueKind != JsonValueKind.Array)
                return NotThisKind($"the {kind} document carries no '{arrayKey}' array and no 'notComputed' reason, so it is neither an answer nor a refusal.");

            var entries = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            var withheld = new List<string>();

            foreach (var entry in array.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object)
                    return NotThisKind($"the {kind} document has a non-object entry in '{arrayKey}'.");

                if (!entry.TryGetProperty(subjectKey, out var subject) || subject.ValueKind != JsonValueKind.String)
                    return NotThisKind($"an entry in the {kind} document names no '{subjectKey}'.");

                var name = subject.GetString() ?? string.Empty;

                // Per-entry withholding: the producer computed the rest and named this one as absent.
                // An entry with no value key is WITHHELD, never "reaches nothing".
                if (entry.TryGetProperty("notComputed", out var entryWhy))
                {
                    withheld.Add($"{name} ({Text(entryWhy)})");
                    continue;
                }

                if (!entry.TryGetProperty(valueKey, out var value))
                {
                    withheld.Add($"{name} (no '{valueKey}' key and no reason)");
                    continue;
                }

                entries[name] = value.ValueKind switch
                {
                    JsonValueKind.String => new[] { value.GetString() ?? string.Empty },
                    JsonValueKind.Array => value.EnumerateArray()
                        .Where(v => v.ValueKind == JsonValueKind.String)
                        .Select(v => v.GetString() ?? string.Empty)
                        .ToArray(),
                    _ => Array.Empty<string>(),
                };
            }

            // 🔴 *** A PARTIALLY-WITHHELD REPORT IS NOT COMPUTED. *** Returning the entries that DID
            // compute would be an answer over a smaller set, and nothing downstream could tell it from a
            // whole one. The withheld subjects are named, because "some of it is missing" is unusable
            // without knowing which.
            if (withheld.Count > 0)
            {
                return new ComposerRead(ComposerReadOutcome.NotComputed,
                    new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal),
                    $"the {kind} document withholds {withheld.Count} of {withheld.Count + entries.Count} subject(s) by name: {string.Join(", ", withheld)}");
            }

            if (entries.Count == 0 && !emptyIsAnAnswer)
            {
                return new ComposerRead(ComposerReadOutcome.NotComputed,
                    entries,
                    $"the {kind} document's '{arrayKey}' array is empty. That is not the claim 'nothing was found' - "
                    + "a producer with nothing to say writes a reason, and this one said neither.");
            }

            return new ComposerRead(ComposerReadOutcome.Read, entries, $"{entries.Count} subject(s) read from the {kind} document.");
        }
    }

    private static ComposerRead NotThisKind(string detail) =>
        new(ComposerReadOutcome.NotThisKind, new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal), detail);

    private static string Text(JsonElement element) =>
        element.ValueKind == JsonValueKind.String ? element.GetString() ?? string.Empty : element.ToString();
}
