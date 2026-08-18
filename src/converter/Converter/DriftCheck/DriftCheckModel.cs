namespace Converter.DriftCheck;

// FI-26 (docs/16-future-ideas.md): the export-drift DETECTOR. Complements the committed round-trip
// tests (which TOLERATE drift via an own-sidecar oracle) by Normalizer-comparing each committed
// `ir/<proj>/<block>.ir` against its paired export and flagging semantic divergence — turning silent
// drift (a fix never re-exported) into a visible signal / red build. One record set, two renderers,
// like TagStatus/Digest.

public enum DriftStatus
{
    Match,      // .ir rebuilds to a SimaticML semantically equivalent to the export
    Drifted,    // rebuilt XML diverges from the export — the drift this tool exists to catch
    Skipped,    // no paired export (an ir-only block, e.g. added post-export)
    Error,      // the .ir could not be converted, or the export could not be parsed — NOT compared
    ExportOnly, // an export with no paired .ir — see the FI-70 note below; the tool used to never look

    // Two files on one side claiming one object identity. Deliberately NOT an absence in either
    // direction: nothing can be compared, and calling it "missing" would be a guess about which of
    // them was meant. Always gates — an ambiguous corpus cannot answer the question that was asked.
    PairingFailure,
}

// IrPath is null for ExportOnly (there is no .ir — that is the finding); XmlPath is null for Skipped;
// both are null for PairingFailure, whose Detail names the colliding files.
//
// FromTia: whether the paired .xml carries TIA's own `<DocumentInfo>` block — see
// DriftCheckReport.TiaExportCount for why that one bit decides whether this tool answered a question
// at all. Null when there was no .xml to look at (Skipped, PairingFailure).
public sealed record DriftEntry(
    string Name, string? IrPath, string? XmlPath, DriftStatus Status, string? Detail, bool? FromTia = null);

// FI-70. The exports directory means two different things depending on what filled it, and the tool
// cannot tell them apart from the inside:
//
//   FI-26's use — a COMMITTED export corpus that may legitimately lag the .ir. An unpaired .ir is an
//                 ordinary "added since the last export"; nothing is wrong.
//   FI-70's use — a FRESH DUMP OF THE CONTROLLER. An unpaired .ir now means "this block is not in the
//                 controller", and an unpaired export means "this block is in the controller and no .ir
//                 describes it". Both are serious, and the second is the one nobody was ever shown:
//                 the runner enumerated .ir files, so a block added in TIA by hand could never appear
//                 in the report at all, whatever its status.
//
// So Complete is the caller's declaration that the exports directory is the whole picture. It never
// changes what is COMPARED — only whether an absence is allowed to pass. ExportOnly entries are now
// always REPORTED (silence about them was the actual defect); Complete decides whether they fail.
public sealed record DriftCheckReport(
    IReadOnlyList<DriftEntry> Entries,
    bool Complete = false,
    string? ProjectDir = null,
    string? ExportsDir = null)
{
    // The DENOMINATOR: how many objects were actually put through the Normalizer. Everything else in
    // the summary is a reason a comparison did NOT happen.
    public int ComparedCount => Entries.Count(e => e.Status is DriftStatus.Match or DriftStatus.Drifted);

    // 🔴 EMPTY IS NOT CLEAN (2026-08-14). Three measured ways this tool reported a clean pass having
    // compared nothing at all:
    //
    //   1. Both directories existing but EMPTY -> "0 drifted, 0 match", exit 0 — and with --complete
    //      the SCOPE line then declared "the 0 absence(s) above are findings".
    //   2. EVERY .ir unparseable (a zero-byte file, a truncated write) -> "1 error", exit 0, because
    //      Error never gated. --complete covers a MISSING FILE; it did not cover a comparison that
    //      could not RUN, which is the same absence one level in.
    //   3. Either directory pointed ONE LEVEL UP, with the real files in a subfolder — both walks are
    //      TopDirectoryOnly, so nothing is found. The likeliest real-world mistake of the three, and
    //      it was rewarded with a green.
    //
    // The old reasoning for (2) — "a non-derivable block is a separate, already-tracked concern, not
    // export drift" — holds for an UnsupportedConstructException on a block using an instruction the
    // converter cannot emit. It does not hold for an IrFormatException: a file that does not parse is
    // not a capability gap, it is a broken file, and the tool cannot say the block has not drifted
    // because it never looked. One catch clause, two meanings, one non-gating bucket.
    public bool ExaminedNothing => ComparedCount == 0;

    /// <summary>
    /// Of the objects actually compared, how many were compared against a document TIA WROTE — one
    /// carrying the <c>&lt;DocumentInfo&gt;</c> block every Openness export emits and the converter
    /// never does.
    ///
    /// <para>🔴 <b>THE FOURTH WAY OF EXAMINING NOTHING, AND THE ONLY ONE THAT REPORTS A FULL
    /// DENOMINATOR WHILE DOING IT — 2026-08-18.</b> The three recorded above all show up as
    /// <c>COMPARED: 0</c>. This one shows <c>COMPARED: 101</c>, a hundred green MATCH lines, and
    /// proves nothing whatsoever, because the "exports" were <b>this converter's own
    /// <c>to-xml</c> output</b>: <c>to-xml</c> writes BESIDE ITS INPUT by default (FI-72), so
    /// running it over an <c>ir/</c> directory silently replaces every real export sitting there,
    /// and from then on the tool compares the converter against itself. Same binary, same input,
    /// same output — MATCH is a tautology, not a measurement.</para>
    ///
    /// <para>MEASURED on a live job: <b>0 of 101</b> files in the directory being passed as
    /// <c>--exports</c> carried DocumentInfo, and <c>0 drifted / 101 match</c> had been recorded
    /// FOUR TIMES as evidence the corpus was in sync with the controller. It was evidence of
    /// nothing. The drift that then appeared was two real converter FIXES landing (wire-endpoint
    /// direction, the instance-DB InOut section) and the stale output not moving with them.</para>
    ///
    /// <para>WHY THE GATE IS "NONE" AND NOT "ALL": a real corpus can legitimately carry a
    /// hand-reduced fixture or a file predating an export setting — <c>simatic-ml/reference/</c> has
    /// four such out of fifteen — and failing those would be a gate firing outside its own question.
    /// ZERO is the different claim: not one document on the other side of the comparison came from
    /// the controller, so whatever this run proved, it was not about the project.</para>
    /// </summary>
    public int TiaExportCount => Entries.Count(e =>
        e.Status is DriftStatus.Match or DriftStatus.Drifted && e.FromTia == true);

    /// <summary>Compared against a document the converter itself could have written.</summary>
    public int NonTiaExportCount => ComparedCount - TiaExportCount;

    /// <summary>Every object compared, and not one of them against a TIA export.</summary>
    public bool ComparedNothingFromTia => ComparedCount > 0 && TiaExportCount == 0;

    // Genuine divergence always fails. So does a comparison that could not be made (Error), and an
    // ambiguous identity (PairingFailure).
    //
    // Absences still fail only when the caller has declared the exports directory complete: without
    // that declaration the tool cannot know whether a missing file is a lagging corpus or a missing
    // block, and guessing in either direction is worse than saying so.
    public bool HasDrift =>
        Entries.Any(e => e.Status is DriftStatus.Drifted or DriftStatus.Error or DriftStatus.PairingFailure)
        || ExaminedNothing
        || ComparedNothingFromTia
        || (Complete && Entries.Any(e => e.Status is DriftStatus.Skipped or DriftStatus.ExportOnly));
}
