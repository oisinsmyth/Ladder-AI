using System.Text.Json;

namespace Converter.ConflictGraph;

/// <summary>
/// WHERE a cited signal lives on the controller — the submission contract's <c>map.storage</c> entry.
/// </summary>
/// <param name="Owner">
/// The block or tag table that DECLARES the root. <b>Null for a global path</b> — a DB member, a PLC
/// tag, an <c>iDB_…</c> member or a physical address is already unique.
/// </param>
/// <param name="Path">The path within that owner, or the global path verbatim.</param>
/// <remarks>
/// 🔴 <b>TWO KEYS, DELIBERATELY NOT ONE DOTTED STRING</b>, and this type mirrors
/// <c>Harness.Results.SignalStorage</c> field for field. The contract's words: <i>an emitted string is
/// not a schema.</i> A consumer handed <c>A.B.C</c> cannot tell whether <c>A</c> is an owning block or a
/// DB without parsing — and a parse is a lookup, which is the thing this field exists to remove.
/// </remarks>
public sealed record DeclaredStorage(string? Owner, string Path)
{
    /// <summary>True when no owner is stated, i.e. the path is global and already unique.</summary>
    public bool IsGlobal => string.IsNullOrWhiteSpace(Owner);

    /// <summary>
    /// The identity two declarations must share to be the SAME storage. <b>Compared as a pair, never as
    /// a concatenation</b> — joining them with a dot would make <c>(owner "A", path "B.C")</c> and
    /// <c>(owner null, path "A.B.C")</c> collide, which is the parse this schema exists to avoid.
    /// </summary>
    public (string Owner, string Path) Identity => (Owner?.Trim() ?? string.Empty, Path?.Trim() ?? string.Empty);

    public override string ToString() => IsGlobal ? Path : $"{Owner}::{Path}";
}

/// <summary>
/// How one cited signal joins to controller storage <b>according to the submission's own declaration</b>.
/// Four states, and the middle two are claims rather than silences. Mirrors
/// <c>Harness.Results.StorageJoin</c>.
/// </summary>
public enum DeclaredJoin
{
    /// <summary>🔴 <b>NOBODY STATED THE JOIN.</b> The default, so it fails closed.</summary>
    NotStated = 0,

    /// <summary>The signal occupies that PLC storage. Resolvable; may produce edges.</summary>
    InStorage,

    /// <summary>
    /// <b>A POSITIVE CLAIM: it occupies no PLC storage at all</b> — a mirror-side logical name. No edge
    /// is possible, and that is a COMPUTED FACT rather than an absence.
    /// </summary>
    HarnessOnly,

    /// <summary>In BOTH. It cannot occupy storage and occupy none. <b>REFUSED, naming it.</b></summary>
    Contradiction,
}

/// <summary>One signal declared more than once, with genuinely different storage.</summary>
public sealed record DeclaredStorageAmbiguity(string Signal, IReadOnlyList<DeclaredStorage> Candidates);

/// <summary>
/// 🔴 <b>THE SUBMISSION-SIDE JOIN THIS TOOL NEVER READ: signal → controller storage, DECLARED.</b>
/// Contract §2.7.
///
/// <para><b>The defect this closes.</b> `conflict-graph --submission` took the submission's
/// <c>inputs</c> keys and <c>expectations[].signal</c> values — which are <b>the SPECIFICATION's names,
/// by design</b> (D8: agents speak tag names, never registers) — and fed them to a resolver expecting
/// STORAGE PATHS. On a real submission that returned <b>70 of 70 unresolved</b>, and the field carrying
/// the join <i>already existed and was never read.</i> This is the fifth instance in this codebase of
/// one seam: two documents naming the same thing differently, joined by nobody.</para>
///
/// <para>🔴 <b>NOTHING HERE RESOLVES A SIGNAL BY THE SHAPE OF ITS NAME.</b> <see cref="Resolve"/> is an
/// exact lookup on the declared key and returns nothing else — no suffix match, no leaf match, and no
/// API that could be used to build one. The shortcut was built, examined and declined by the contract,
/// because <b>two of the four cross-block multi-writer findings this project has ever recorded were
/// fiction produced exactly that way.</b></para>
///
/// <para><b>Only the OBJECT form is read, because that is what the consumer reads.</b>
/// <c>Harness.Gate.MapDocument.Storage</c> is a <c>Dictionary&lt;string, StorageDocument&gt;</c>.
/// Accepting a shape the gate would ignore would let an author write a map this tool honours and the
/// gate does not — a divergence between two readers of one document, which is the seam again.</para>
/// </summary>
public sealed record SubmissionSignalMap(
    IReadOnlyDictionary<string, DeclaredStorage> Storage,
    IReadOnlySet<string> HarnessOnly,
    IReadOnlyList<DeclaredStorageAmbiguity> Ambiguities,
    IReadOnlyList<string> Rejections)
{
    /// <summary>
    /// The author declared the join. <b>An AMBIGUOUS or REJECTED declaration counts</b>: a map whose
    /// every entry was refused has still been written, and reporting it as "nobody declared the join"
    /// would name the wrong repair — the author declared it, wrongly.
    /// </summary>
    public bool Declared => Storage.Count > 0 || HarnessOnly.Count > 0 || Ambiguities.Count > 0 || Rejections.Count > 0;

    /// <summary>
    /// How one signal joins. <b>An exact lookup on the declared key and nothing else</b> — see the type
    /// remarks for why there is no fallback here.
    /// </summary>
    public DeclaredJoin Resolve(string signal)
    {
        var key = (signal ?? string.Empty).Trim();
        var inStorage = Storage.ContainsKey(key);
        var mirrorOnly = HarnessOnly.Contains(key);

        return (inStorage, mirrorOnly) switch
        {
            (true, true) => DeclaredJoin.Contradiction,
            (true, false) => DeclaredJoin.InStorage,
            (false, true) => DeclaredJoin.HarnessOnly,
            _ => DeclaredJoin.NotStated,
        };
    }

    /// <summary>The storage a signal was declared to occupy, or null when it is not in <c>storage</c>.</summary>
    public DeclaredStorage? StorageOf(string signal) =>
        Storage.TryGetValue((signal ?? string.Empty).Trim(), out var s) ? s : null;

    /// <summary>The ambiguity recorded for a signal declared twice with different storage, if any.</summary>
    public DeclaredStorageAmbiguity? AmbiguityOf(string signal) =>
        Ambiguities.FirstOrDefault(a => string.Equals(a.Signal, (signal ?? string.Empty).Trim(), StringComparison.Ordinal));

    /// <summary>Nothing declared, so every join is NOT STATED.</summary>
    public static SubmissionSignalMap None { get; } = new(
        new Dictionary<string, DeclaredStorage>(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal),
        Array.Empty<DeclaredStorageAmbiguity>(),
        Array.Empty<string>());

    /// <summary>
    /// Build from declared entries. <b>Entries are a LIST rather than a dictionary</b> so that a signal
    /// declared twice is EXPRESSIBLE — and therefore refusable. A dictionary would have silently kept
    /// one of them, which is the resolution this contract forbids.
    /// </summary>
    public static SubmissionSignalMap Of(
        IEnumerable<(string Signal, DeclaredStorage Storage)> entries,
        IEnumerable<string>? harnessOnly = null,
        IEnumerable<string>? rejections = null)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var storage = new Dictionary<string, DeclaredStorage>(StringComparer.Ordinal);
        var ambiguities = new List<DeclaredStorageAmbiguity>();

        foreach (var group in entries
                     .Where(e => !string.IsNullOrWhiteSpace(e.Signal))
                     .GroupBy(e => e.Signal.Trim(), StringComparer.Ordinal))
        {
            var distinct = group.Select(e => e.Storage).DistinctBy(s => s.Identity).ToArray();
            if (distinct.Length > 1)
            {
                ambiguities.Add(new DeclaredStorageAmbiguity(group.Key, distinct));
                continue;
            }

            storage[group.Key] = distinct[0];
        }

        return new SubmissionSignalMap(
            storage,
            (harnessOnly ?? Array.Empty<string>()).Select(s => s.Trim()).Where(s => s.Length > 0)
                .ToHashSet(StringComparer.Ordinal),
            ambiguities,
            (rejections ?? Array.Empty<string>()).ToList());
    }

    /// <summary>
    /// Read <c>map.storage</c> and <c>map.harnessOnly</c> out of a submission document.
    ///
    /// <para><b>A malformed entry is a REJECTION carrying its reason, never a skip.</b> A skipped entry
    /// reads downstream as "the author did not declare this signal", which is a different fact with a
    /// different repair — and it is the reading that produced this defect in the first place. Rejections
    /// make the map non-empty, so the run reports NOT COMPUTED rather than quietly judging a smaller
    /// scope.</para>
    /// </summary>
    public static SubmissionSignalMap ReadFromSubmission(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        if (!doc.RootElement.TryGetProperty("map", out var map) || map.ValueKind != JsonValueKind.Object)
        {
            return None;
        }

        var entries = new List<(string, DeclaredStorage)>();
        var rejections = new List<string>();

        if (map.TryGetProperty("storage", out var storage))
        {
            if (storage.ValueKind != JsonValueKind.Object)
            {
                rejections.Add(
                    $"`map.storage` is a {storage.ValueKind} and the contract's shape — the one "
                    + "`Harness.Gate.MapDocument` deserializes — is an OBJECT keyed by signal name, each value "
                    + "{ owner?, path }. Reading any other shape here would honour a declaration the gate ignores.");
            }
            else
            {
                foreach (var property in storage.EnumerateObject())
                {
                    if (property.Value.ValueKind != JsonValueKind.Object)
                    {
                        rejections.Add(
                            $"`map.storage['{property.Name}']` is a {property.Value.ValueKind}, not an object. "
                            + "OWNER AND PATH ARE SEPARATE KEYS ON PURPOSE: a single dotted string cannot say whether "
                            + "its first segment is an owning block or a DB, and guessing is the lookup this field exists to remove.");
                        continue;
                    }

                    var owner = property.Value.TryGetProperty("owner", out var o) && o.ValueKind == JsonValueKind.String
                        ? o.GetString()
                        : null;
                    var storagePath = property.Value.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.String
                        ? p.GetString()
                        : null;

                    if (string.IsNullOrWhiteSpace(storagePath))
                    {
                        rejections.Add(
                            $"`map.storage['{property.Name}']` states no `path`. An owner alone names no location.");
                        continue;
                    }

                    entries.Add((property.Name, new DeclaredStorage(owner, storagePath)));
                }
            }
        }

        var harnessOnly = new List<string>();
        if (map.TryGetProperty("harnessOnly", out var mirrorOnly))
        {
            if (mirrorOnly.ValueKind != JsonValueKind.Array)
            {
                rejections.Add($"`map.harnessOnly` is a {mirrorOnly.ValueKind} and the contract's shape is an ARRAY of signal names.");
            }
            else
            {
                foreach (var element in mirrorOnly.EnumerateArray())
                {
                    if (element.ValueKind == JsonValueKind.String)
                    {
                        harnessOnly.Add(element.GetString()!);
                    }
                    else
                    {
                        rejections.Add($"`map.harnessOnly` carries a {element.ValueKind} element; every entry must be a signal NAME.");
                    }
                }
            }
        }

        return Of(entries, harnessOnly, rejections);
    }
}
