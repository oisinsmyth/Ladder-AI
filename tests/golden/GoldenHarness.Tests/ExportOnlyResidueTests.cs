using Converter.DriftCheck;
using Xunit;

namespace GoldenHarness;

/// <summary>
/// Why an export-only name is tolerated. Same discipline as <see cref="DriftDisposition"/> and for the
/// same reason: a list that cannot say WHY a name is on it stops being a decision and becomes
/// furniture. The two dispositions differ in WHO OWES SOMETHING, which is the only distinction that
/// changes what anybody does next.
/// </summary>
public enum ExportOnlyDisposition
{
    /// <summary>
    /// The object is in the controller ON PURPOSE and no <c>.ir</c> is owed — scaffolding, a library
    /// object nobody in this repo authors, an object outside the delivered program. Never gates.
    ///
    /// <para>*** THIS IS THE DISPOSITION THAT HAS TO BE EARNED, because "it's just harness stuff" is
    /// what every unexplained object looks like from the outside. *** The claim being made is that the
    /// project's DESCRIPTION is complete without it — and
    /// <c>docs/notes/test-environment-contract.md:981-990</c> is unambiguous that an undescribed object
    /// in the controller is a gap in the description, not a non-event: <i>an unread object may hold the
    /// second writer that makes a signal a conflict.</i> So the Reason must say what the object writes,
    /// not only what it is.</para>
    /// </summary>
    NotOurs,

    /// <summary>
    /// An <c>.ir</c> IS owed — the object is (or may be) project content and nothing in this repo
    /// describes it. *** THIS GATES. *** It is the partial-corpus case, written down rather than
    /// counted, and it stays red until somebody converts the object or rules it <see cref="NotOurs"/>.
    /// </summary>
    DescriptionOwed,
}

public sealed record KnownExportOnly(string Export, ExportOnlyDisposition Disposition, string Reason);

/// <summary>
/// *** THE EXPORT-ONLY RESIDUE, BY NAME. *** <c>docs/notes/test-environment-contract.md:981-990</c>:
/// <i>"An object in the controller with no <c>.ir</c> at all is not a drift, it is a <b>gap in the
/// project's description</b>… <b>Report them BY NAME, never as a count.</b> '3 export-only' is a
/// number; the names are what let a reader tell a forgotten harness object from a deliverable nobody
/// exported."</i> Until this file, <b>nothing implemented "by name"</b> — <c>DriftStatus.ExportOnly</c>
/// did not appear anywhere in <c>tests/golden/</c>.
///
/// <para><b>🔴 THERE IS A COMMITTED INSTANCE OF THE DEFECT THE RULE FORBIDS, and it is in the same
/// document that states the rule.</b> <c>test-environment-contract.md:960</c> records the first
/// whole-project <c>drift-check --complete</c> against a fresh controller dump as <i>4 DRIFTED / 3
/// EXPORT-ONLY</i> — and <b>the three export-only names were never written down.</b> The count
/// survived; the finding did not. That is not neglect, it is structural: nothing that ran had anywhere
/// to put a name.</para>
///
/// <para><b>⚠️ THIS IS NOT <see cref="CommittedBlocksRoundTripTests"/>' <c>KnownMissingExports</c>, and
/// conflating them loses the whole point.</b> Different population, different key, opposite direction:</para>
///
/// <list type="table">
/// <item><term>KnownMissingExports</term><description>an <c>.ir</c> with no export. Keyed on the
/// committed <c>.ir</c> filename, populated by enumerating <c>*.ir</c>. It covers 7 of the 17 unpaired
/// objects in <c>test-project001</c>; the other 10 (a sidecar-carrying block, ONE tag table
/// — <c>HarnessMirror</c>; <c>DefaultTagTable</c> is paired and compared — a UDT, and SEVEN harness
/// instance DBs) are dropped by that walk before the list is ever consulted — its prose at
/// <c>CommittedBlocksRoundTripTests.cs:58-62</c> covers them, its ASSERTION covers none of them.</description></item>
/// <item><term>This list</term><description>an EXPORT with no <c>.ir</c>. Keyed on the export's own
/// name as <c>drift-check</c> reports it. *** An object with no <c>.ir</c> cannot be named, tolerated
/// or missed by a list keyed on a committed <c>.ir</c>, *** because the enumeration that builds that
/// list never reaches it. That is why this had to be a second list and not a widening of the first.</description></item>
/// </list>
///
/// <para><b>🔴 AGAINST THE COMMITTED CORPUS THIS LIST IS EMPTY, AND THAT IS THE CORRECT ANSWER — BUT AN
/// EMPTY LIST MUST NOT READ AS AN UNASKED QUESTION.</b> Both committed projects are one-directional:
/// every committed export pairs with an <c>.ir</c> (41 of 41), so there is no export-only residue to
/// name. Three things keep that zero honest, because a zero from a probe that cannot see and a zero
/// from a clean corpus are identical in a run log:</para>
/// <list type="number">
/// <item><see cref="EveryExportOnlyObject_IsNamedNotCounted"/> asserts the size of the population it
/// examined — via <see cref="CorpusCensusTests"/>' recorded row — so the answer is inseparable from its
/// denominator. A corpus that lost its exports cannot produce a quiet "no export-only objects".</item>
/// <item><see cref="TheProbe_NamesAnExportOnlyObject_WhenOneIsThere"/> demonstrates the probe firing,
/// on a temp corpus with one <c>.ir</c> removed. Zero is then a measurement.</item>
/// <item>The examined corpus is NAMED in every assertion message, so the claim is always
/// <i>"no export-only objects in these two committed directories"</i> and never
/// <i>"no export-only objects".</i></item>
/// </list>
///
/// <para><b>WHAT THIS FILE CANNOT SEE, and it is the larger half.</b> The two export-only objects this
/// project actually knows about — <c>MotorIOSet</c> and <c>MotorVSDIOSet</c>, UDTs present in the live
/// <c>test-project001</c> controller and described by no committed <c>.ir</c> — are visible <b>only
/// against a LIVE export</b>, which needs Portal. This lane has none, so they are deliberately NOT
/// seeded into the list below: an entry that is not export-only against the corpus being examined would
/// fail <see cref="EveryKnownExportOnly_IsStillExportOnly"/> on the day it was added, and a staleness
/// guard that has to be suppressed on entry is not a staleness guard. They are recorded here in prose
/// and belong in the list the moment a whole-project <c>drift-check --complete</c> is run against a
/// controller dump — which is the run this file gives them somewhere to sit.</para>
///
/// <para>The same limit stated once more, plainly: <b>this file examines a committed corpus, never a
/// controller.</b> An object added to the PLC by hand is invisible to it, and that is precisely the
/// object <c>--complete</c> exists to surface.</para>
/// </summary>
public class ExportOnlyResidueTests
{
    /// <summary>
    /// Export-only objects that have been LOOKED AT and ruled on. Keyed on the export's own object name
    /// exactly as <c>DriftEntry.Name</c> reports it for a <see cref="DriftStatus.ExportOnly"/> row —
    /// i.e. the export FILE's basename, which is what the runner uses when there is no <c>.ir</c> to
    /// take a declared identity from.
    ///
    /// <para>⚠️ Note the key is a filename-derived name while <c>drift-check</c> PAIRS on the identity
    /// declared inside the document — TIA's own name for the default tag table is "Default tag table"
    /// while its file is <c>DefaultTagTable.xml</c>. For an export-only row the two can only differ in
    /// whitespace (nothing paired, so nothing was folded), but if a name here ever stops matching, take
    /// it from the tool's output rather than inventing one.</para>
    ///
    /// <para><b>EMPTY, 2026-08-23, and empty is the accurate state for the committed corpus</b> — see
    /// the class comment for why that is a measurement and not an absence, and for the two live UDTs
    /// that will populate it the first time this question is asked of a controller dump.</para>
    /// </summary>
    private static readonly KnownExportOnly[] Known = Array.Empty<KnownExportOnly>();

    public static IEnumerable<object[]> Projects() => CorpusCensusTests.Projects();

    /// <summary>
    /// The rule, implemented: <b>by name, never as a count.</b> Any export-only object not on the list
    /// fails, and the failure prints the NAMES — so the thing that reaches a human is the thing the
    /// contract asks for, and the "3 export-only" that got written down instead cannot happen from
    /// here.
    ///
    /// <para>The examined population is asserted first, against
    /// <see cref="CorpusCensusTests.Recorded"/>. That coupling is deliberate and is the answer to the
    /// obvious objection: <i>"no export-only objects" over an exports directory that lost half its
    /// files is a true sentence and a worthless one.</i> The count of exports examined is part of the
    /// claim.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(Projects))]
    public void EveryExportOnlyObject_IsNamedNotCounted(string project)
    {
        var census = CorpusCensusTests.For(project);
        var xmlDir = CommittedCorpus.XmlDir(project);
        var report = CommittedCorpus.Report(project);

        // The denominator is part of the answer. Asserted here as well as in CorpusCensusTests because
        // a zero finding is only as good as the population it was taken over, and the reader of THIS
        // failure should not have to go and find that out.
        var examined = CommittedCorpus.ExportFileCount(project);
        Assert.True(examined == census.Exports,
            $"{project}: this check examined {examined} export(s) in {xmlDir}, but the recorded census " +
            $"says {census.Exports}. Until that is reconciled, any export-only answer from this run is " +
            "over an unknown population — and 'no export-only objects' over a shrunken exports " +
            $"directory is a true sentence and a worthless one. Row measured: {census.Measured}");

        var found = report.Entries
            .Where(e => e.Status == DriftStatus.ExportOnly)
            .Select(e => e.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        var unnamed = found
            .Where(n => !Known.Any(k => string.Equals(k.Export, n, StringComparison.Ordinal)))
            .ToList();

        Assert.True(unnamed.Count == 0,
            $"{project}: export(s) present in {xmlDir} with NO committed .ir describing them:\n  " +
            string.Join("\n  ", unnamed) + "\n\n" +
            "This is not drift — it is a GAP IN THE PROJECT'S DESCRIPTION " +
            "(docs/notes/test-environment-contract.md:981-990). Nothing in this repo can say what such " +
            "an object writes, what it models, or whether it conflicts with something that is " +
            "described, and an unread object may hold the second writer that makes a signal a " +
            "conflict.\n" +
            "Add each NAME to ExportOnlyResidueTests.Known WITH a disposition and a reason that says " +
            "what the object WRITES, or commit an .ir for it. What must not happen is that it becomes a " +
            "number: 'N export-only' is exactly what was recorded at " +
            "test-environment-contract.md:960 for three objects whose names are now unrecoverable.");
    }

    /// <summary>
    /// *** THE GATE THE LIST OWES ITS EXISTENCE TO. *** <see cref="ExportOnlyDisposition.DescriptionOwed"/>
    /// means somebody has to write an <c>.ir</c>, and a list that holds that state quietly is a list
    /// that has converted an obligation into furniture — the same failure
    /// <see cref="ExportDriftDetectorTests.NoDriftEntry_IsAnOpenQuestion"/> exists to prevent one file
    /// over. <see cref="ExportOnlyDisposition.NotOurs"/> is an ANSWER and never reaches here.
    /// </summary>
    [Fact]
    public void NoExportOnlyEntry_IsAnUnpaidDescription()
    {
        var owed = Known
            .Where(k => k.Disposition == ExportOnlyDisposition.DescriptionOwed)
            .OrderBy(k => k.Export, StringComparer.Ordinal)
            .ToList();

        Assert.True(owed.Count == 0,
            "object(s) in the controller that this repo has agreed it cannot describe:\n  " +
            string.Join("\n  ", owed.Select(k => $"{k.Export} — {k.Reason}")) + "\n\n" +
            "Convert the object and commit its .ir, or — if the description genuinely is complete " +
            "without it — re-file as NotOurs with a reason that says what it WRITES. Leaving it here " +
            "makes the corpus partial while looking settled.");
    }

    /// <summary>
    /// Staleness guard, the mirror of <see cref="CommittedBlocksRoundTripTests.KnownMissingExport_IsStillMissing"/>:
    /// a listed name that has STOPPED being export-only must go, or the list starts describing a state
    /// of the world that is no longer true.
    ///
    /// <para>Two ways an entry goes stale, reported separately because they mean opposite things: an
    /// <c>.ir</c> was committed for it (the gap CLOSED — delete the entry, the object is compared now),
    /// or the export vanished from the corpus (the object is no longer THERE — a different fact, and
    /// the entry is now asserting nothing about anything).</para>
    ///
    /// <para>A <c>[Fact]</c>, not a <c>[Theory]</c>, and deliberately: a theory over a known-problem
    /// list fails with "No data found" the moment the list empties, which is the state being worked
    /// towards. It is empty today, so a theory here would have been born broken — the exact defect
    /// <c>CommittedBlocksRoundTripTests</c> records hitting twice on 2026-08-13.</para>
    /// </summary>
    [Fact]
    public void EveryKnownExportOnly_IsStillExportOnly()
    {
        var stale = new List<string>();

        foreach (var entry in Known)
        {
            var sightings = CommittedCorpus.Projects
                .Select(p => (Project: p, Entry: CommittedCorpus.Report(p).Entries
                    .FirstOrDefault(e => string.Equals(e.Name, entry.Export, StringComparison.Ordinal))))
                .Where(x => x.Entry is not null)
                .ToList();

            if (sightings.Count == 0)
            {
                stale.Add($"{entry.Export} — no object of that name is in either committed corpus at " +
                          "all any more, so this entry describes nothing. [" + entry.Disposition + "]");
                continue;
            }

            foreach (var (project, seen) in sightings.Where(x => x.Entry!.Status != DriftStatus.ExportOnly))
            {
                stale.Add($"{entry.Export} — in {project} it is now {seen!.Status}, not ExportOnly" +
                          (seen.Status is DriftStatus.Match or DriftStatus.Drifted
                              ? " (an .ir was committed for it and it IS being compared — the gap closed)"
                              : string.Empty) + ". [" + entry.Disposition + "]");
            }
        }

        Assert.True(stale.Count == 0,
            "ExportOnlyResidueTests.Known entr(ies) no longer describe the world:\n  " +
            string.Join("\n  ", stale) + "\n" +
            "Remove them. A tolerated gap that has closed, left on the list, is how a list stops being " +
            "read at all.");
    }

    /// <summary>
    /// *** THE PROBE, DEMONSTRATED FIRING — WITHOUT THIS THE EMPTY LIST ABOVE IS AN UNASKED QUESTION. ***
    ///
    /// <para>The committed corpus has no export-only object, so every assertion above passes over an
    /// empty set. A test that passes over an empty set and a test whose detection is broken produce the
    /// identical run log. So one export-only object is MANUFACTURED — a full copy of the
    /// <c>reference</c> corpus into a temp directory with a single <c>.ir</c> removed — and the probe
    /// must name it.</para>
    ///
    /// <para>What is asserted is the NAME, not the count: this is a check on the thing the contract
    /// actually requires (<i>"report them BY NAME, never as a count"</i>), and a probe that could only
    /// report "1 export-only" would satisfy a count assertion while failing the rule.</para>
    ///
    /// <para><c>HandAuthorSplitsMerges</c> is removed because nothing else in the reference corpus
    /// resolves against it: MEASURED, the remaining 14 objects still compare cleanly (0 error), so the
    /// export-only row is the only thing the deletion produces. Choosing a block others depend on would
    /// have turned neighbours into <c>Error</c> rows and made the probe prove something blurrier.</para>
    /// </summary>
    [Fact]
    public void TheProbe_NamesAnExportOnlyObject_WhenOneIsThere()
    {
        const string Orphaned = "HandAuthorSplitsMerges";

        var (irDir, xmlDir) = CorpusCensusTests.CopyCorpusToTemp(
            "reference", nameof(TheProbe_NamesAnExportOnlyObject_WhenOneIsThere));

        var irPath = Path.Combine(irDir, Orphaned + ".ir");
        Assert.True(File.Exists(irPath),
            $"the probe expected to orphan {Orphaned}.xml by deleting {Orphaned}.ir, and that .ir is not " +
            "in the reference corpus. Pick a block that is, or this control exercises nothing.");
        File.Delete(irPath);

        var report = DriftCheckRunner.Run(irDir, xmlDir);

        var exportOnly = report.Entries
            .Where(e => e.Status == DriftStatus.ExportOnly)
            .Select(e => e.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.True(exportOnly.SequenceEqual(new[] { Orphaned }, StringComparer.Ordinal),
            $"an export with no .ir must be reported as ExportOnly BY NAME. Expected exactly " +
            $"[{Orphaned}], got [{string.Join(", ", exportOnly)}]. If this is empty, the detection " +
            "above is inert and its zero over the committed corpus means nothing at all.");

        // The residue is a finding about the DESCRIPTION, not about the comparison: everything else in
        // the corpus is still compared. Asserted so that "export-only" cannot quietly become the bucket
        // a broken walk empties itself into.
        Assert.True(report.ComparedCount == CorpusCensusTests.For("reference").Compared - 1,
            $"orphaning one export should cost exactly one comparison; COMPARED is " +
            $"{report.ComparedCount} against a corpus of {CorpusCensusTests.For("reference").Compared}.");

        Assert.True(CommittedCorpus.Count(report, DriftStatus.Error) == 0,
            "the orphaning deletion broke something else in the corpus (Error rows present), so this " +
            "probe is no longer isolating the export-only case. Choose a block nothing else resolves " +
            "against.");
    }
}
