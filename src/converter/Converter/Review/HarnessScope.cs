using Converter.Ir;
using Converter.SimaticMl;

namespace Converter.Review;

/// <summary>
/// What kind of object a reviewed file is, for the purpose of doc 06's NAMING conventions.
///
/// Ruled 2026-08-13 by the owner: *** doc 06's naming conventions govern PLC program content
/// AUTHORED FOR THE PLANT. A harness-generated object is neither plant nor hand-authored. ***
/// A generated test-harness copy layer therefore drew 20+ C-001 findings and a C-201 on every run —
/// findings that were correct against the letter of the rule and wrong about their subject.
///
/// *** THE REQUIREMENT IS NOT "SUPPRESS THE FINDINGS". *** A silent exemption and a correct pass are
/// indistinguishable, which is this repository's most-repeated defect; and twenty standing findings
/// per run is how a reviewer learns to skim, which is the same defect running backwards (an
/// over-firing gate decays into a warning). So the reviewer must KNOW an object is harness-generated
/// and SAY SO, in a counted, labelled, non-gating bucket — never by dropping anything.
/// </summary>
public enum HarnessClass
{
    /// <summary>Plant content. Every rule gates exactly as it always has.</summary>
    Plant,

    /// <summary>
    /// Harness-generated. C-001 and C-201 findings are REPORTED under the harness bucket and do not
    /// gate. Every other rule still gates — a harness object is exempt from a NAMING convention, not
    /// from behaving correctly (C-103 is the live example and stays a finding).
    /// </summary>
    Harness,

    /// <summary>
    /// *** COULD NOT BE DECIDED FROM THE ARTIFACT. *** Behaves exactly like <see cref="Plant"/> for
    /// gating — every finding fires — and differs only in what the report SAYS. It exists because
    /// "this is plant content" and "no derivable property of this file could tell me" are different
    /// facts, and collapsing them is how a classifier that silently stopped running would look like
    /// a classifier that examined everything and found no harness objects.
    /// </summary>
    Unclassified,
}

/// <param name="Class">The verdict.</param>
/// <param name="Basis">
/// *** WHY — always populated, for every class including Plant. *** A verdict with no stated basis
/// cannot be checked by a reader, and a Plant verdict printed with its reason is the only thing that
/// makes a DISCONNECTED classifier visible: if this line ever reads "no NUMBER was available" on a
/// file that plainly has one, the derivation stopped running.
/// </param>
public sealed record HarnessVerdict(HarnessClass Class, string Basis);

/// <summary>
/// Decides, from properties DERIVED FROM THE ARTIFACTS THEMSELVES, whether reviewed content is
/// harness-generated. *** THERE IS DELIBERATELY NO `--harness` FLAG AND NO `IsHarness` FIELD IN THE
/// IR. *** A caller assertion is forgotten exactly when it matters, and a declaration is a
/// transferred responsibility rather than a verification.
///
/// <para>THE TWO DERIVATIONS, AND THEIR DIFFERENT STRENGTHS:</para>
/// <list type="bullet">
/// <item><b>Blocks and DBs — the reserved number band.</b> Block numbers 9000–9999 are reserved for
/// harness-generated objects, independently in each number space (FC, FB, DB); declared in
/// docs/notes/test-environment-build-plan.md, 2026-08-13. A block's number is structural, is written
/// in its own IR, and is not a name: renumbering a plant FC into the band is a real change that
/// collides with the harness's own allocation. <b>OBs are excluded</b> — an OB's number is fixed by
/// its event class, so the band cannot classify one in EITHER direction; an OB is therefore
/// <see cref="HarnessClass.Unclassified"/> and its findings gate.</item>
/// <item><b>Tags — who references them.</b> ⚠️ A TAG TABLE HAS NO NUMBER, and its only table-level
/// property is its NAME. *** A NAME IS EXACTLY WHAT MUST NOT BE LOAD-BEARING HERE: "rename a plant
/// tag table to HarnessMirror" must not be a route out of review. *** So the table's name is NEVER
/// READ, and neither is an `HX_` prefix. Classification is PER TAG, from the one derived property
/// available: the set of blocks that reference the tag, each of which is itself classified by the
/// number band. A tag is harness only when it has at least one referrer and EVERY referrer is a
/// harness block. Any plant referrer makes it plant; no referrer at all, or any unclassifiable
/// referrer, makes it Unclassified. To launder a plant tag under this rule you must move every
/// reference to it into blocks numbered 9000–9999 — which breaks the plant program, where a rename
/// costs nothing.</item>
/// </list>
///
/// <para>WHAT THIS DOES NOT COVER, STATED HERE RATHER THAN ONLY IN A REPORT:</para>
/// <list type="bullet">
/// <item>A UDT has no number and no referrer relation of this kind — <b>always Unclassified</b>.</item>
/// <item>The referrer relation is only as complete as the corpus it was built from. A scope built
/// from a two-file batch knows about two files. <see cref="CorpusDescription"/> is printed with the
/// verdicts so a reader sees WHICH corpus answered, and if ANY corpus file failed to parse no tag is
/// classified harness at all — an unread file could have held the plant referrer that makes a tag
/// plant, so a partial corpus fails closed rather than quietly exempting.</item>
/// </list>
/// </summary>
public sealed class HarnessScope
{
    /// <summary>
    /// The reserved band, both bounds inclusive. One constant, consumed everywhere, rather than a
    /// literal at a call site — the build plan's own follow-up condition when it declared the band.
    /// </summary>
    public const int ReservedBandLow = 9000;

    /// <inheritdoc cref="ReservedBandLow"/>
    public const int ReservedBandHigh = 9999;

    private readonly Dictionary<string, SortedSet<string>> _tagReferrers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HarnessVerdict> _corpusBlocks = new(StringComparer.Ordinal);
    private readonly List<string> _unreadable = new();
    private int _corpusFileCount;

    /// <summary>
    /// A scope built from nothing. Blocks and DBs still classify (their number is in their own file),
    /// but NO tag can be classified, so every tag-table finding gates — which is the pre-2026-08-13
    /// behaviour. *** THAT IS THE POINT: THE DEFAULT IS THE CONSERVATIVE ONE. *** A caller that
    /// forgets to build a scope gets today's noise, never a bypass.
    /// </summary>
    public static HarnessScope Empty { get; } = new();

    /// <summary>Files whose references were read.</summary>
    public int CorpusFileCount => _corpusFileCount;

    /// <summary>
    /// Corpus files that could NOT be parsed. Non-empty means no tag may be classified harness —
    /// see <see cref="ClassifyTag"/>.
    /// </summary>
    public IReadOnlyList<string> UnreadableCorpusFiles => _unreadable;

    /// <summary>One line naming what the tag classification was decided against. Always printed.</summary>
    public string CorpusDescription =>
        _corpusFileCount == 0
            ? "no corpus was supplied, so NO tag could be classified (pass the referencing blocks, or --project)"
            : _unreadable.Count > 0
                ? $"{_corpusFileCount} file(s), of which {_unreadable.Count} could NOT be parsed ({string.Join(", ", _unreadable.Select(Path.GetFileName))}) — so no tag is classified harness: an unread file could hold the plant reference that makes a tag plant"
                : $"{_corpusFileCount} file(s), all parsed";

    /// <summary>
    /// Read every supplied .ir path and record which blocks reference which tag roots. Paths that do
    /// not parse are RECORDED, never skipped silently.
    /// </summary>
    public static HarnessScope Build(IEnumerable<string> paths)
    {
        var scope = new HarnessScope();
        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p, StringComparer.Ordinal))
        {
            scope.AddFile(path);
        }

        return scope;
    }

    /// <summary>
    /// Classify an FC/FB/OB by its own number. Nothing outside the file is consulted.
    /// </summary>
    public static HarnessVerdict ClassifyBlock(IrBlock block) =>
        string.Equals(block.Kind, "OB", StringComparison.OrdinalIgnoreCase)
            // The carve-out, and the rule breaks without it: an OB's number is FIXED BY ITS EVENT
            // CLASS — identified, not chosen — so OB80 being a harness object cannot be read off its
            // number, and neither can a plant OB1 being plant. Reading the band on an OB would either
            // emit a false finding on a correct harness OB or exempt every OB in the 9000s. Neither
            // is acceptable, so an OB is simply not decidable this way and its findings gate.
            ? new HarnessVerdict(
                HarnessClass.Unclassified,
                $"{block.Kind} {block.Number}: an OB's number is fixed by its event class, so the reserved band {ReservedBandLow}-{ReservedBandHigh} cannot classify it in either direction. If this IS a harness OB, that is not derivable from the artifact — an honest gap, and its C-001/C-201 findings gate as normal")
            : ClassifyNumbered(block.Kind, block.Number);

    /// <summary>Classify a DB by its own number. Instance DBs included — an iDB carries a number too.</summary>
    public static HarnessVerdict ClassifyDb(DbSource db) => ClassifyNumbered("DB", db.Number);

    /// <summary>
    /// A UDT has no number, and the referrer relation used for tags does not apply to a type (a type
    /// is instantiated, not referenced by path). *** NO DERIVABLE PROPERTY, SO NO VERDICT. ***
    /// </summary>
    public static HarnessVerdict ClassifyType(PlcTypeSource type) =>
        new(
            HarnessClass.Unclassified,
            $"'{type.Name}' is a PLC data type: a UDT carries NO number, so the reserved band {ReservedBandLow}-{ReservedBandHigh} has nothing to read, and its NAME is the only other table-level property — which is exactly what must not be load-bearing. No derivable property distinguishes a harness-generated UDT from a plant one, so this is UNCLASSIFIED and every finding gates");

    /// <summary>
    /// Classify ONE tag by the blocks that reference it. See the type doc for why this is per-tag and
    /// why the table's name is never consulted.
    /// </summary>
    public HarnessVerdict ClassifyTag(string tagName)
    {
        if (_corpusFileCount == 0)
        {
            return new HarnessVerdict(
                HarnessClass.Unclassified,
                $"'{tagName}': a tag table has no number, so a tag is classified by WHO REFERENCES IT — and no corpus of blocks was supplied to answer that. Findings gate. (Supply the referencing blocks in the same review batch, or pass --project.)");
        }

        if (!_tagReferrers.TryGetValue(tagName, out var referrers) || referrers.Count == 0)
        {
            return new HarnessVerdict(
                HarnessClass.Unclassified,
                $"'{tagName}' is referenced by NO block in the corpus ({CorpusDescription}), so there is nothing to classify it from. An unreferenced tag is not thereby harness — findings gate");
        }

        var plant = referrers.Where(r => VerdictFor(r).Class == HarnessClass.Plant).ToList();
        if (plant.Count > 0)
        {
            return new HarnessVerdict(
                HarnessClass.Plant,
                $"'{tagName}' is referenced by plant block(s) {string.Join(", ", plant)} — one plant reader or writer makes a tag plant content, whatever the table it sits in is called");
        }

        var undecided = referrers.Where(r => VerdictFor(r).Class == HarnessClass.Unclassified).ToList();
        if (undecided.Count > 0)
        {
            return new HarnessVerdict(
                HarnessClass.Unclassified,
                $"'{tagName}' is referenced by {string.Join(", ", undecided)}, which could not themselves be classified — so this tag cannot be either. Findings gate");
        }

        // *** THE PARTIAL-CORPUS FENCE. *** Every known referrer is a harness block, but a file that
        // failed to parse could have been the plant referrer that changes this answer. Refuse rather
        // than mis-classify: a comparison that could not be made must not be reported as one that
        // came out negative.
        if (_unreadable.Count > 0)
        {
            return new HarnessVerdict(
                HarnessClass.Unclassified,
                $"'{tagName}' is referenced only by harness block(s) {string.Join(", ", referrers)} — but {_unreadable.Count} corpus file(s) could not be parsed ({string.Join(", ", _unreadable.Select(Path.GetFileName))}), and an unread file could hold a plant reference. Not classified harness on a partial corpus; findings gate");
        }

        return new HarnessVerdict(
            HarnessClass.Harness,
            $"'{tagName}' is referenced only by harness block(s) {string.Join(", ", referrers.Select(r => $"{r} ({VerdictFor(r).Basis})"))}");
    }

    /// <summary>
    /// The whole tag table's verdict, derived from its tags' own verdicts — a summary line for the
    /// report, never the thing that decides a finding. Deliberately has no independent existence:
    /// there is no table-level property to derive it from.
    /// </summary>
    public HarnessVerdict ClassifyTagTable(PlcTagTableSource table)
    {
        if (table.Tags.Count == 0)
        {
            return new HarnessVerdict(
                HarnessClass.Unclassified,
                $"tag table '{table.Name}' declares no tags; a table's own NAME is never read for this (renaming a table must buy nothing), so there is nothing to classify");
        }

        var verdicts = table.Tags.Select(t => ClassifyTag(t.Name).Class).ToList();
        var harness = verdicts.Count(v => v == HarnessClass.Harness);
        var plant = verdicts.Count(v => v == HarnessClass.Plant);
        var undecided = verdicts.Count(v => v == HarnessClass.Unclassified);
        var summary = $"{harness} harness / {plant} plant / {undecided} unclassified of {verdicts.Count} tag(s); decided PER TAG from referencing blocks, never from the table name. Corpus: {CorpusDescription}";

        if (harness == verdicts.Count)
        {
            return new HarnessVerdict(HarnessClass.Harness, summary);
        }

        if (plant == verdicts.Count)
        {
            return new HarnessVerdict(HarnessClass.Plant, summary);
        }

        // A MIXED table is Unclassified at file level and that is not a fudge: the file-level verdict
        // decides nothing (each finding is partitioned by its own tag's verdict), and calling a table
        // holding both "plant" would misdescribe the tags that are not.
        return new HarnessVerdict(HarnessClass.Unclassified, summary);
    }

    private HarnessVerdict VerdictFor(string blockName) =>
        _corpusBlocks.TryGetValue(blockName, out var v)
            ? v
            : new HarnessVerdict(HarnessClass.Unclassified, $"'{blockName}' was not resolvable to a block in the corpus");

    private static HarnessVerdict ClassifyNumbered(string kind, int number) =>
        number >= ReservedBandLow && number <= ReservedBandHigh
            ? new HarnessVerdict(
                HarnessClass.Harness,
                $"{kind} {number} is inside the reserved harness band {ReservedBandLow}-{ReservedBandHigh} (declared docs/notes/test-environment-build-plan.md, 2026-08-13)")
            : new HarnessVerdict(
                HarnessClass.Plant,
                $"{kind} {number} is outside the reserved harness band {ReservedBandLow}-{ReservedBandHigh}, so this is plant content and every rule gates");

    private void AddFile(string path)
    {
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (IOException ex)
        {
            _corpusFileCount++;
            _unreadable.Add($"{path} ({ex.GetType().Name})");
            return;
        }

        _corpusFileCount++;

        // Only BLOCK files carry references. A DB/TYPE/TAGTABLE in the corpus contributes nothing to
        // the referrer relation and is not an error — but it is still COUNTED, so the corpus
        // description reports what was actually looked at.
        if (text.StartsWith("DB ", StringComparison.Ordinal)
            || text.StartsWith("TYPE ", StringComparison.Ordinal)
            || text.StartsWith("TAGTABLE ", StringComparison.Ordinal))
        {
            return;
        }

        IrBlock block;
        try
        {
            block = IrParser.HasSidecarSection(text)
                ? IrParser.ParseBlock(text).Block
                : IrParser.ParseBlockWithoutSidecar(text);
        }
        catch (Exception ex) when (ex is IrFormatException or SimaticMlFormatException or UnsupportedConstructException or NonReducibleNetworkException)
        {
            _unreadable.Add($"{path} ({ex.GetType().Name})");
            return;
        }

        _corpusBlocks[block.Name] = ClassifyBlock(block);

        // A block's OWN interface member names are excluded: a bare `Start` in a network is that
        // block's input, not the tag table's `Start`, and letting one shadow the other would let a
        // harness block's interface member vouch for an identically-named plant tag.
        var ownNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in (block.InputMembers ?? Array.Empty<DbMember>())
                     .Concat(block.OutputMembers ?? Array.Empty<DbMember>())
                     .Concat(block.InOutMembers)
                     .Concat(block.StaticMembers ?? Array.Empty<DbMember>())
                     .Concat(block.TempMembers)
                     .Concat(block.ConstantMembers ?? Array.Empty<DbMember>()))
        {
            ownNames.Add(member.Name);
        }

        foreach (var network in block.Networks)
        {
            foreach (var path_ in TagReferences.AllTagPaths(network))
            {
                var root = RootOf(path_);
                if (root.Length == 0 || ownNames.Contains(root))
                {
                    continue;
                }

                if (!_tagReferrers.TryGetValue(root, out var set))
                {
                    set = new SortedSet<string>(StringComparer.Ordinal);
                    _tagReferrers[root] = set;
                }

                set.Add(block.Name);
            }
        }
    }

    // First path segment, unquoted and unsubscripted: `"DB_X".Member[3].Y` -> `DB_X`, `HX_Scan` ->
    // `HX_Scan`. A tag-table tag is always a bare root, so anything with a dot can only match a tag
    // whose root shares the name — which is the correct reading (`Tag.%X0` is still that tag).
    private static string RootOf(string path)
    {
        var cut = path.IndexOfAny(new[] { '.', '[' });
        var root = cut < 0 ? path : path[..cut];
        return root.Trim('"');
    }
}
