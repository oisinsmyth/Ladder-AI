namespace Converter.TagStatus;

// One classified name: its extracted root and whether it resolves in the project export.
public sealed record TagStatusEntry(string Name, string Root, bool Exists);

// Mirrors the Review/Preflight/Digest "one record, two renderers" report shape.
public sealed record TagStatusReport(IReadOnlyList<TagStatusEntry> Entries, IReadOnlyList<string> IndexWarnings)
{
    // A proposed name is one the export doesn't contain yet — the thing the anti-laundering rule
    // (CLAUDE.md hard rule 3) exists to flag. Drives the non-zero exit so callers can gate on it.
    public bool HasProposed => Entries.Any(e => !e.Exists);
}
