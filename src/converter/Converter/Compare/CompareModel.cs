namespace Converter.Compare;

// The judgement half of THE CONFIRM LOOP (docs/notes/test-environment-build-plan.md, owner
// 2026-08-12):
//
//     export ──► to-ir ──► to-xml ──► import ──► compile ──► export
//        └──────────────── compare THESE TWO ──────────────────┘
//
// Strictly stronger than `drift-check`, which never leaves the PC: this pair has been THROUGH TIA,
// so it also catches whatever TIA does to the content on import and on compile — the class of
// defect `MemoryLayout` belonged to, where every PC-side check stayed green.
//
// FI-24 keeps the converter a pure in-process file transformer, so the orchestration lives in
// `tools/confirm-roundtrip.ps1` and only the comparison lives here. That split is deliberate and it
// is not the boring half: a byte-compare is unusable (TIA reassigns Part/Wire/Access UIds
// unprompted) and a comparison that ignores too much is exactly how the `MemoryLayout` hole
// survived a `drift-check` that reported MATCH.

public enum CompareStatus
{
    /// <summary>The two documents are semantically equivalent under <see cref="SimaticMl.Normalizer"/>.</summary>
    Equivalent,

    /// <summary>A real difference was found, and it is localized in <see cref="CompareReport.Differences"/>.</summary>
    Differs,

    /// <summary>
    /// The question was not answered. A file missing or unparseable, two paths that are the same
    /// file, a document that is not a SimaticML export, or a premise of the comparison that did not
    /// hold. NEVER exit 0 — "empty is not clean" (FI-44): a comparison that compared nothing must
    /// not look like a comparison that passed.
    /// </summary>
    NotCompared,
}

public enum DifferenceKind
{
    /// <summary>Present in the first document, absent from the second.</summary>
    ElementMissing,

    /// <summary>Absent from the first document, present in the second.</summary>
    ElementAdded,

    /// <summary>Both documents have the element; its text content differs.</summary>
    ValueDiffers,

    /// <summary>An attribute present in the first document and absent from the second.</summary>
    AttributeMissing,

    /// <summary>An attribute absent from the first document and present in the second.</summary>
    AttributeAdded,

    /// <summary>Both documents carry the attribute; its value differs.</summary>
    AttributeDiffers,

    /// <summary>The two roots are not even the same element — nothing below them is comparable.</summary>
    RootElementDiffers,
}

/// <summary>
/// One localized difference. <see cref="Path"/> is a path in the NORMALIZED document (element local
/// names, 1-based sibling index when a name repeats, <c>/@attr</c> for an attribute), so it points
/// at the element TIA changed rather than merely asserting that something changed.
///
/// A caveat the formatter states out loud: a UId value in a reported difference is a
/// content-derived key the Normalizer substituted for the raw number, not the number in the file.
/// A raw UId is meaningless across two exports — that is the whole reason the Normalizer rewrites
/// them — so reporting the file's own number would be worse than useless.
/// </summary>
public sealed record CompareDifference(DifferenceKind Kind, string Path, string? First, string? Second);

/// <summary>
/// Which side declared a <c>&lt;MemoryLayout&gt;</c>, carried explicitly because the strength of the
/// whole comparison turns on it — see <see cref="CompareRunner"/>.
/// </summary>
public sealed record MemoryLayoutObservation(string? First, string? Second, bool Compared);

public sealed record CompareReport(
    string FirstPath,
    string SecondPath,
    CompareStatus Status,
    IReadOnlyList<CompareDifference> Differences,
    MemoryLayoutObservation MemoryLayout,
    string? Detail = null)
{
    public bool Equivalent => Status == CompareStatus.Equivalent;
}
