namespace Harness.Map;

/// <summary>
/// One object the deployment STAGED — a row of the denominator the build stamp is measured against.
/// </summary>
/// <param name="Name">
/// The object's name in the project. <b>The key, because TIA matches an import by NAME</b> and that is
/// the same key <c>Harness.Batch.ManifestObject</c> records — which is what lets a lane manifest become a
/// corpus row without this assembly knowing that type exists.
/// </param>
/// <param name="Source">
/// Where the row came from, in words a reader can act on — <c>"lane 'vessel'"</c>, <c>"staged union"</c>.
/// <b>A gap nobody can trace to a source is a gap nobody can close</b>: the corpus is a UNION over lane
/// manifests and the staged set, so a bare name does not say which document to go and fix.
/// </param>
/// <param name="Kind">
/// The object's kind if the source knows it, else null. <b>Not part of the key.</b> A lane manifest
/// records no kind, and requiring one would mean either inventing it or dropping the row.
/// </param>
public sealed record StagedCorpusEntry(string Name, string Source, string? Kind = null)
{
    /// <summary>How this row is NAMED in a coverage line: the name, and the document that staged it.</summary>
    public override string ToString() =>
        Kind is null ? $"{Name} [{Source}]" : $"{Kind}:{Name} [{Source}]";
}

/// <summary>
/// 🔴 <b>THE DENOMINATOR: EVERY OBJECT THE DEPLOYMENT STAGED, whether the build stamp hashed it or not.</b>
///
/// <para><b>Why it is a separate thing from the program under test.</b> <c>--program</c> is the NUMERATOR
/// — the list a caller typed, or a lane declared, of what to hash. The defect this closes is that a SHORT
/// numerator is indistinguishable from a complete one when nothing states the denominator. So the corpus
/// is supplied from somewhere ELSE: the staged union and the lane manifests, which are emitted by whatever
/// built the lane rather than typed beside the stamp.</para>
///
/// <para><b>It is plain data on purpose.</b> <c>Harness.Map</c> has no project references at all — that is
/// what makes it testable with no device, no rig and no TIA session — so this type takes names and source
/// labels, never <c>Harness.Batch.LaneManifest</c>. The caller does the projection; the dependency arrow
/// stays pointing the way it always has.</para>
///
/// <para>🔴 <b>AND IT IS NOT THE DEVICE.</b> See <see cref="StampCoverage.DeviceResidual"/>. A corpus is a
/// record of what a deployment MEANT to put on a controller; an object already running there that appears
/// in no corpus and no lane manifest is outside this denominator entirely.</para>
/// </summary>
public sealed record StagedCorpus
{
    private StagedCorpus(IReadOnlyList<StagedCorpusEntry> entries) => Entries = entries;

    /// <summary>The rows, deduplicated by name, in first-seen order.</summary>
    public IReadOnlyList<StagedCorpusEntry> Entries { get; }

    /// <summary><i>m</i>. Distinct staged objects.</summary>
    public int Count => Entries.Count;

    /// <summary>
    /// Build a corpus from rows.
    ///
    /// <para><b>An empty corpus is REFUSED, not accepted as zero.</b> EMPTY IS NOT CLEAN: a corpus of no
    /// objects makes every run read <i>"hashed n of 0"</i> with an empty gap list, which is the exact shape
    /// of a green that examined nothing. A caller with nothing to stage passes <c>null</c> instead, and
    /// that renders as NO DENOMINATOR rather than as a clean sheet.</para>
    /// </summary>
    public static StagedCorpus Of(IEnumerable<StagedCorpusEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var rows = Deduplicate(entries.ToArray());

        if (rows.Count == 0)
        {
            throw new ArgumentException(
                "a staged corpus naming NO objects would make every coverage line read 'hashed n of 0' over an empty gap "
                + "list — the shape of a check that examined nothing. Pass null for 'no corpus was supplied', which says so.",
                nameof(entries));
        }

        return new StagedCorpus(rows);
    }

    /// <summary>The same, from bare names out of one document.</summary>
    public static StagedCorpus FromNames(string source, params string[] names)
    {
        ArgumentNullException.ThrowIfNull(names);
        return Of(names.Select(n => new StagedCorpusEntry(n, source)));
    }

    /// <summary>
    /// The union of two corpora — how several lane manifests plus the staged set become one denominator.
    ///
    /// <para><b>An object staged by two lanes keeps BOTH sources.</b> One lane's copy silently winning
    /// would make a gap report name the wrong document to go and fix.</para>
    /// </summary>
    public StagedCorpus Union(StagedCorpus other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new StagedCorpus(Deduplicate(Entries.Concat(other.Entries).ToArray()));
    }

    private static IReadOnlyList<StagedCorpusEntry> Deduplicate(IReadOnlyList<StagedCorpusEntry> rows)
    {
        var order = new List<string>();
        var byName = new Dictionary<string, StagedCorpusEntry>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Name))
            {
                throw new ArgumentException(
                    "a staged corpus row with no name cannot be matched against anything — TIA matches an import by NAME, "
                    + "so an unnamed row would be reported as a permanent gap nobody could close.",
                    nameof(rows));
            }

            if (!byName.TryGetValue(row.Name, out var seen))
            {
                order.Add(row.Name);
                byName[row.Name] = row;
                continue;
            }

            if (!seen.Source.Contains(row.Source, StringComparison.Ordinal))
                byName[row.Name] = seen with { Source = $"{seen.Source} + {row.Source}" };
        }

        return order.Select(n => byName[n]).ToArray();
    }
}

/// <summary>
/// 🔴 <b>WHAT THE BUILD STAMP DID <i>NOT</i> HASH — the half the manifest never stated.</b>
///
/// <para>*** MEASURED, AND WRITTEN DOWN IN <c>docs/18-project-workbench.md</c> §5 <b>"Phase 10 — Wave
/// time"</b>, under <i>"THE BUILD STAMP DOES COVER THE PARAMETER DB"</i>. *** A wave's stamp was derived
/// over a program set the parameter DB was not in: <i>"compressing them changes the controller without
/// changing the stamp."</i> Two result packages describing materially different programs would carry the
/// SAME stamp, and the verifying gateway — whose entire job is to refuse a program that is not the one
/// described — would not notice.</para>
///
/// <para>⚠️ <b>THE FIGURE IN THAT ENTRY WAS RETRACTED 2026-08-24 — read it before quoting one.</b> It
/// originally said <b>eight</b> objects; the deploy added the parameter DB 2 h 32 min later the same
/// afternoon and the build that ran is stamp <c>622F3EB7</c> over <b>nine</b>. The defect CLASS below is
/// what this type is for and it is unaffected.</para>
///
/// <para><b><see cref="ProgramManifest.HashedNothing"/> already told the ZERO case apart. Nothing told the
/// SHORT case apart</b>, and short is the case that happened. A manifest saying <i>"here are the n objects
/// I hashed"</i> is equally true whether <i>n</i> was the whole program or most of it.</para>
///
/// <para><b>Every count here is printed on every run, including when it is zero.</b>
/// <c>MirrorViewModel.RegistersStale</c> makes the argument and it applies unchanged: a count that appears
/// only when it is non-zero teaches a reader that its absence means everything was covered — and then a run
/// that counted NOTHING reads exactly like a run that covered everything.</para>
/// </summary>
/// <param name="Hashed"><i>n</i>. Objects the canonical form actually appended, from the manifest.</param>
/// <param name="CorpusSize">
/// <i>m</i>. Distinct objects the deployment staged. <b>Null is NOT zero</b>: it means nobody supplied a
/// corpus, so there is no denominator and this run cannot say what it did not hash. An empty gap list under
/// a null denominator is not a clean sheet, and <see cref="Line"/> says exactly that instead of a number.
/// </param>
/// <param name="ExcludedAsSelfReferential">
/// <i>k</i>. The harness's own generated objects, refused BY NAME — the copy layer and the mirror tag
/// table. <b>Correct, and kept SEPARATE from the gap.</b> Hashing the block the stamp is rendered into is
/// circular (the invariant <see cref="BuildStamp"/> opens by stating), so these are not something to close;
/// filing them under "not hashed" would create a permanent phantom gap, and a check carrying one of those
/// is a check people learn to skim.
/// </param>
/// <param name="PresentAndNotHashed">
/// <i>j</i>. 🔴 <b>THE DEFECT, NAMED.</b> Staged, not self-referential, and the stamp did not cover it —
/// so a change to it moves the controller and not the stamp.
/// </param>
/// <param name="HashedNotInCorpus">
/// The other direction, and it is a different fault: the stamp covers an object NO staged document names.
/// That is not a missing object, it is a <c>--program</c> the manifests disagree with — the disagreement
/// <c>LaneManifest.Disagreement</c> refuses to pick a winner in. Without it, a stamp over the wrong nine
/// objects could still read <i>"9 of 9"</i>.
/// </param>
public sealed record StampCoverage(
    int Hashed,
    int? CorpusSize,
    IReadOnlyList<string> ExcludedAsSelfReferential,
    IReadOnlyList<string> PresentAndNotHashed,
    IReadOnlyList<string> HashedNotInCorpus)
{
    /// <summary>
    /// 🔴 <b>WHAT THIS COUNT CANNOT POSSIBLY SEE, CARRIED IN THE LINE ITSELF.</b>
    ///
    /// <para>The corpus is a record of what a deployment staged. <b>An object EXECUTING ON THE DEVICE that
    /// appears in no corpus and no lane manifest is outside the denominator entirely</b> — the virtual
    /// panel is exactly that — and reading it needs Portal, which the item that built this does not use.</para>
    ///
    /// <para><b>It travels in the same sentence as the number, deliberately.</b> A residual recorded
    /// somewhere else is a residual nobody reads, and <i>"9 of 9"</i> with the caveat elsewhere reads as
    /// coverage of THE PROGRAM. That would make this a closed check, which is the one thing it must not be.</para>
    /// </summary>
    public const string DeviceResidual =
        "COVERAGE OF THE STAGED CORPUS, NOT OF THE PROGRAM: an object EXECUTING ON THE DEVICE that appears in no "
        + "corpus and no lane manifest is outside this denominator entirely, and nothing here evidences its absence.";

    /// <summary>Whether a denominator was supplied at all. False means no coverage claim can be made.</summary>
    public bool CorpusStated => CorpusSize is not null;

    /// <summary>
    /// <b>True only when a corpus was stated AND both gap lists are empty.</b> Exposed so no caller reaches
    /// for <c>PresentAndNotHashed.Count == 0</c>, which is also true of the run that had no denominator.
    /// </summary>
    public bool Complete =>
        CorpusStated && PresentAndNotHashed.Count == 0 && HashedNotInCorpus.Count == 0;

    /// <summary>
    /// The one line a reader sees. <b>Printed on the passing case too</b>, for the reason on the type.
    /// </summary>
    public string Line =>
        (CorpusStated
            ? $"stamp over {Hashed} of {CorpusSize} object(s) in the staged corpus"
            : $"stamp over {Hashed} object(s); NO STAGED CORPUS WAS SUPPLIED, so there is no denominator and this run "
              + "CANNOT say what it did not hash — an empty gap list here is not a clean sheet")
        + $"; {ExcludedAsSelfReferential.Count} excluded as self-referential ({Named(ExcludedAsSelfReferential)})"
        + (CorpusStated
            ? $"; {PresentAndNotHashed.Count} present and not hashed ({Named(PresentAndNotHashed)})"
              + $"; {HashedNotInCorpus.Count} hashed that no corpus entry names ({Named(HashedNotInCorpus)})"
            : string.Empty)
        + " — " + DeviceResidual;

    private static string Named(IReadOnlyList<string> names) =>
        names.Count == 0 ? "none" : string.Join(", ", names);

    /// <summary>
    /// Compute the coverage of one derivation.
    ///
    /// <para><b>Names are matched ORDINALLY, the same way <see cref="BuildStamp.Derive"/> matches its own
    /// self-referential exclusion.</b> A looser match here would let the coverage report disagree with the
    /// exclusion it is reporting on — and it would err towards calling an object covered. Ordinal errs the
    /// other way, towards naming a gap that is only a spelling difference, and that is the safe direction:
    /// an over-reported gap gets looked at, an under-reported one does not.</para>
    ///
    /// <para><b>A corpus row matching the copy layer or the mirror tag table is classified self-referential
    /// EVEN IF the caller never handed it over.</b> Every lane manifest lists the copy layer it generated,
    /// while a <c>--program</c> list built from the pre-generation tree legitimately does not; classifying
    /// by name rather than by "was it supplied" is what stops that reading as a coverage hole on every run.</para>
    /// </summary>
    internal static StampCoverage Compute(
        IReadOnlyList<string> hashedNames,
        IReadOnlyList<string> selfReferentialSupplied,
        CopyLayerNaming naming,
        StagedCorpus? corpus)
    {
        if (corpus is null)
        {
            return new StampCoverage(
                hashedNames.Count,
                null,
                selfReferentialSupplied,
                Array.Empty<string>(),
                Array.Empty<string>());
        }

        var hashed = hashedNames.ToHashSet(StringComparer.Ordinal);
        var selfReferential = new List<string>(selfReferentialSupplied);
        var gaps = new List<string>();

        foreach (var row in corpus.Entries)
        {
            if (hashed.Contains(row.Name))
                continue;

            var isHarnessOwn =
                string.Equals(row.Name, naming.BlockName, StringComparison.Ordinal) ||
                string.Equals(row.Name, naming.TagTableName, StringComparison.Ordinal);

            if (isHarnessOwn)
            {
                // Already named by the derivation if it was supplied; named from the corpus if it was not.
                if (!selfReferential.Any(e => e.EndsWith($":{row.Name}", StringComparison.Ordinal)))
                    selfReferential.Add(row.ToString());

                continue;
            }

            gaps.Add(row.ToString());
        }

        var staged = corpus.Entries.Select(e => e.Name).ToHashSet(StringComparer.Ordinal);

        return new StampCoverage(
            hashedNames.Count,
            corpus.Count,
            selfReferential,
            gaps,
            hashedNames.Where(n => !staged.Contains(n)).ToArray());
    }
}
