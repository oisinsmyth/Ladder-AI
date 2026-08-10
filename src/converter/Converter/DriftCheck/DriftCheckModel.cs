namespace Converter.DriftCheck;

// FI-26 (docs/16-future-ideas.md): the export-drift DETECTOR. Complements the committed round-trip
// tests (which TOLERATE drift via an own-sidecar oracle) by Normalizer-comparing each committed
// `ir/<proj>/<block>.ir` against its paired `simatic-ml/<proj>/<block>.xml` and flagging semantic
// divergence — turning silent drift (a fix never re-exported) into a visible signal / red build.
// One record set, two renderers, like TagStatus/Digest.

public enum DriftStatus
{
    Match,      // .ir rebuilds to a SimaticML semantically equivalent to the committed .xml
    Drifted,    // rebuilt XML diverges from the committed export — the drift this tool exists to catch
    Skipped,    // no paired .xml (an ir-only block, e.g. added post-export)
    Error,      // the .ir could not be converted to XML (parse/synthesis failure) — cannot be compared
    ExportOnly, // an .xml with no paired .ir — see the FI-70 note below; the tool used to never look
}

// IrPath is null for ExportOnly (there is no .ir — that is the finding); XmlPath is null for Skipped.
public sealed record DriftEntry(string Name, string? IrPath, string? XmlPath, DriftStatus Status, string? Detail);

// FI-70. The exports directory means two different things depending on what filled it, and the tool
// cannot tell them apart from the inside:
//
//   FI-26's use — a COMMITTED export corpus that may legitimately lag the .ir. An unpaired .ir is an
//                 ordinary "added since the last export"; nothing is wrong.
//   FI-70's use — a FRESH DUMP OF THE CONTROLLER. An unpaired .ir now means "this block is not in the
//                 controller", and an unpaired .xml means "this block is in the controller and no .ir
//                 describes it". Both are serious, and the second is the one nobody was ever shown:
//                 the runner enumerates .ir files, so a block added in TIA by hand could never appear
//                 in the report at all, whatever its status.
//
// So Complete is the caller's declaration that the exports directory is the whole picture. It never
// changes what is COMPARED — only whether an absence is allowed to pass. ExportOnly entries are now
// always REPORTED (silence about them was the actual defect); Complete decides whether they fail.
public sealed record DriftCheckReport(IReadOnlyList<DriftEntry> Entries, bool Complete = false)
{
    // Genuine divergence always fails. Error (unconvertible) never does — a non-derivable block is a
    // separate, already-tracked concern, not export drift.
    //
    // Absences fail only when the caller has declared the exports directory complete. Without that
    // declaration this tool has no way to know whether a missing file is a lagging corpus or a
    // missing block, and guessing in either direction is worse than saying so.
    public bool HasDrift =>
        Entries.Any(e => e.Status == DriftStatus.Drifted)
        || (Complete && Entries.Any(e => e.Status is DriftStatus.Skipped or DriftStatus.ExportOnly));
}
