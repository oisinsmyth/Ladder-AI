namespace Converter.Digest;

// FI-15 (docs/16-future-ideas.md): a compact, deterministic, mechanically-derived structural
// summary of one .ir file — orientation ("what shape is this block?"), never judgment. Derived
// fresh from the IR on every run, never stored, so it cannot go stale the way a committed
// sidecar can. Mirrors ReviewModel's "one record set, two renderers" pattern.

/// <summary>One interface/member section: "INPUT", "STATIC", "MEMBERS", "TAGS", … with one
/// pre-formatted line per member.</summary>
public sealed record SectionDigest(string Section, IReadOnlyList<string> Members);

/// <summary>All CALLs of one callee across the whole block, with the distinct instance paths.</summary>
public sealed record CallSiteDigest(string BlockName, int CallCount, IReadOnlyList<string> Instances);

/// <summary>One network: number, title, and a "coil:2, timer:1" statement summary ("-" if the
/// network reduced to no statements). Statement-level only — comparisons/contacts live inside
/// condition expressions and are deliberately not counted.</summary>
public sealed record NetworkDigest(int Number, string Title, string Statements);

public sealed record FileDigest(
    string FilePath,
    string Kind,
    string? Name,
    int? Number,
    string? InstanceOfName,
    string? Title,
    IReadOnlyList<SectionDigest> Sections,
    IReadOnlyList<CallSiteDigest> Calls,
    IReadOnlyList<NetworkDigest> Networks,
    IReadOnlyList<string> TagRoots,
    string? FileError);

public sealed record DigestReport(IReadOnlyList<FileDigest> Files);

/// <summary>A file failed to parse and --ignore-errors was not given (same batch contract as
/// ReviewFileException).</summary>
public sealed class DigestFileException : Exception
{
    public DigestFileException(string message)
        : base(message)
    {
    }
}
