namespace Converter.DriftCheck;

// FI-26 (docs/16-future-ideas.md): the export-drift DETECTOR. Complements the committed round-trip
// tests (which TOLERATE drift via an own-sidecar oracle) by Normalizer-comparing each committed
// `ir/<proj>/<block>.ir` against its paired `simatic-ml/<proj>/<block>.xml` and flagging semantic
// divergence — turning silent drift (a fix never re-exported) into a visible signal / red build.
// One record set, two renderers, like TagStatus/Digest.

public enum DriftStatus
{
    Match,     // .ir rebuilds to a SimaticML semantically equivalent to the committed .xml
    Drifted,   // rebuilt XML diverges from the committed export — the drift this tool exists to catch
    Skipped,   // no paired .xml (an ir-only block, e.g. added post-export)
    Error,     // the .ir could not be converted to XML (parse/synthesis failure) — cannot be compared
}

public sealed record DriftEntry(string Name, string IrPath, string? XmlPath, DriftStatus Status, string? Detail);

public sealed record DriftCheckReport(IReadOnlyList<DriftEntry> Entries)
{
    // Only genuine divergence fails the check. Skipped (unpaired) and Error (unconvertible) are
    // reported but do not fail — an unpaired block has nothing to compare, and a non-derivable block
    // is a separate, already-tracked concern (not export drift).
    public bool HasDrift => Entries.Any(e => e.Status == DriftStatus.Drifted);
}
