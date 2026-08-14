namespace Harness.Results;

/// <summary>
/// <b>WHERE a signal lives on the controller</b> — contract §2.7.
/// </summary>
/// <param name="Owner">
/// The block or tag table that DECLARES the root. <b>Null for a global path</b> — a DB member, a PLC
/// tag, an <c>iDB_…</c> member or a physical address is already unique.
/// </param>
/// <param name="Path">The path within that owner, or the global path verbatim.</param>
/// <remarks>
/// 🔴 <b>TWO KEYS, DELIBERATELY NOT ONE DOTTED STRING.</b> The contract's words: <i>an emitted string is
/// not a schema.</i> A consumer handed <c>A.B.C</c> cannot tell whether <c>A</c> is an owning block or a
/// DB without parsing — and a parse is a lookup, which is the very thing this field exists to remove.
/// </remarks>
public sealed record SignalStorage(string? Owner, string Path)
{
    /// <summary>True when no owner is stated, i.e. the path is global and already unique.</summary>
    public bool IsGlobal => string.IsNullOrWhiteSpace(Owner);

    /// <summary>
    /// The identity two signals must share to be the SAME storage. <b>Compared as a pair, never as a
    /// concatenation</b> — joining them with a dot would make <c>(owner "A", path "B.C")</c> and
    /// <c>(owner null, path "A.B.C")</c> collide, which is the parse this schema exists to avoid.
    /// </summary>
    public (string Owner, string Path) Identity => (Owner?.Trim() ?? string.Empty, Path?.Trim() ?? string.Empty);

    public override string ToString() => IsGlobal ? Path : $"{Owner}::{Path}";
}

/// <summary>How one submission signal joins to controller storage. <b>Four states, and the middle two are claims rather than silences.</b></summary>
public enum StorageJoin
{
    /// <summary>
    /// 🔴 <b>NOBODY STATED THE JOIN.</b> Gates 8 and 8c are NOT CHECKED — the honest answer, and the
    /// default, so it fails closed.
    /// </summary>
    NotStated = 0,

    /// <summary>The signal occupies that PLC storage. Resolvable; may produce edges.</summary>
    InStorage,

    /// <summary>
    /// <b>A POSITIVE CLAIM: it occupies no PLC storage at all</b> — a mirror-side logical name.
    /// <b>No edge is possible, and that is a COMPUTED FACT rather than an absence.</b>
    ///
    /// <para>This state exists because the tooling names an ambiguity it cannot settle: an unresolved
    /// signal reports <i>"this may be a mirror-only signal, or the name may be wrong"</i> — <b>two
    /// entirely different repairs behind one silence.</b> Without the third state a legitimately
    /// mirror-only signal is indistinguishable from a typo for ever.</para>
    /// </summary>
    HarnessOnly,

    /// <summary>In BOTH. A contradiction: it cannot occupy storage and occupy none. <b>REFUSED, naming it.</b></summary>
    Contradiction,
}

/// <summary>One signal declared more than once, with genuinely different storage.</summary>
public sealed record StorageAmbiguity(string Signal, IReadOnlyList<SignalStorage> Candidates);

/// <summary>
/// 🔴 <b>THE SUBMISSION-SIDE JOIN: signal → controller storage, DECLARED, and unambiguous by
/// construction.</b> Contract §2.7.
///
/// <para><b>Why this is blocking rather than tidy.</b> A conflict graph is a statement about STORAGE —
/// two blocks conflict because they write the same location — and <c>map.providedFor</c> carries
/// observability modes only: it says HOW a signal is watched and never WHERE it is. *** MEASURED ON A
/// LIVE RUN: 1 OF 17 SUBMISSION SIGNALS RESOLVED TO A STORAGE PATH, AND THAT ONE RESOLVED ONLY BECAUSE
/// ITS SPEC NAME AND BLOCK TAG HAPPEN TO BE THE SAME STRING. *** Gates 8 and 8c were not merely
/// unsupplied — there was no expressible way to supply them.</para>
///
/// <para>🔴 <b>NOTHING HERE RESOLVES A SIGNAL BY THE SHAPE OF ITS NAME, AND THE TYPE MAKES THAT
/// IMPOSSIBLE RATHER THAN DISCOURAGED.</b> There is no suffix match, no leaf match, no "ends with", and
/// no API that could be used to build one: <see cref="Resolve"/> is an exact lookup on the declared key
/// and returns nothing else. The shortcut — find the storage whose path ENDS WITH the signal's name —
/// was built, examined and declined, because <b>two of the four cross-block multi-writer findings this
/// project has ever recorded were fiction produced exactly that way</b> (<c>IO.Step</c> declared in two
/// different UDTs; a <c>Time</c> temp declared separately in three FBs). <b>The declared join REPLACES
/// name matching; it does not supplement it.</b></para>
///
/// <para><b>Ambiguity is REFUSED NAMING EVERY CANDIDATE, never resolved to one</b> — picking a candidate
/// is the fiction, not the fix.</para>
/// </summary>
public sealed record SignalStorageMap(
    IReadOnlyDictionary<string, SignalStorage> Storage,
    IReadOnlySet<string> HarnessOnly,
    IReadOnlyList<StorageAmbiguity> Ambiguities)
{
    /// <summary>
    /// Nothing was declared at all — every signal is <see cref="StorageJoin.NotStated"/>.
    ///
    /// <para><b>An AMBIGUOUS declaration is not an absent one</b>, so ambiguities count: a map whose every
    /// entry was refused for ambiguity has still been written, and reporting it as "nobody declared the
    /// join" would name the wrong repair — the author declared it twice, differently.</para>
    /// </summary>
    public bool IsEmpty => Storage.Count == 0 && HarnessOnly.Count == 0 && Ambiguities.Count == 0;

    /// <summary>
    /// How one signal joins. <b>An exact lookup on the declared key and nothing else</b> — see the type
    /// remarks for why there is no fallback.
    /// </summary>
    public StorageJoin Resolve(string signal)
    {
        var key = (signal ?? string.Empty).Trim();

        var inStorage = Storage.ContainsKey(key);
        var mirrorOnly = HarnessOnly.Contains(key);

        return (inStorage, mirrorOnly) switch
        {
            (true, true) => StorageJoin.Contradiction,
            (true, false) => StorageJoin.InStorage,
            (false, true) => StorageJoin.HarnessOnly,
            _ => StorageJoin.NotStated,
        };
    }

    /// <summary>The storage a signal occupies, or null when it is not in <c>storage</c>.</summary>
    public SignalStorage? StorageOf(string signal) =>
        Storage.TryGetValue((signal ?? string.Empty).Trim(), out var s) ? s : null;

    /// <summary>
    /// Build the map from declared entries. <b>Entries are a LIST rather than a dictionary</b> so that a
    /// signal declared twice is EXPRESSIBLE — and therefore refusable. A dictionary would have silently
    /// kept one of them, which is the resolution this contract forbids.
    /// </summary>
    public static SignalStorageMap Of(
        IEnumerable<(string Signal, SignalStorage Storage)> entries,
        IEnumerable<string>? harnessOnly = null)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var grouped = entries
            .Where(e => !string.IsNullOrWhiteSpace(e.Signal))
            .GroupBy(e => e.Signal.Trim(), StringComparer.Ordinal)
            .ToArray();

        var storage = new Dictionary<string, SignalStorage>(StringComparer.Ordinal);
        var ambiguities = new List<StorageAmbiguity>();

        foreach (var group in grouped)
        {
            // Instance aliases of ONE storage collapse first, so what survives is genuinely different
            // storage — which is the only thing worth refusing over.
            var distinct = group.Select(e => e.Storage).DistinctBy(s => s.Identity).ToArray();

            if (distinct.Length > 1)
            {
                ambiguities.Add(new StorageAmbiguity(group.Key, distinct));
                continue;
            }

            storage[group.Key] = distinct[0];
        }

        return new SignalStorageMap(
            storage,
            (harnessOnly ?? Array.Empty<string>()).Select(s => s.Trim()).ToHashSet(StringComparer.Ordinal),
            ambiguities);
    }

    /// <summary>The empty map: nothing declared, so every join is NOT STATED.</summary>
    public static SignalStorageMap None { get; } = new(
        new Dictionary<string, SignalStorage>(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal),
        Array.Empty<StorageAmbiguity>());
}
