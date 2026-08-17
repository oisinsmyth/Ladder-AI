namespace Harness.Cleanup;

/// <summary>
/// Everything the run was told, after parsing and before anything is read.
///
/// <para><see cref="CrossCheckPath"/> and <see cref="DrainReportPath"/> are NULLABLE on purpose. An
/// omitted flag becomes a <c>null</c> handed to <see cref="Harness.Results.Cleanup.Plan"/>, so the
/// refusal is the library's own and its guard sits on the path a caller actually takes — rather than
/// being shadowed by an argument check that a later refactor could disconnect without anything going
/// red.</para>
/// </summary>
public sealed record CleanupOptions(
    string ProjectDir,
    string? CrossCheckPath,
    IReadOnlyList<string> TestArtifactPaths,
    string? DrainReportPath,
    string Authority,
    string? ClaimsJsonPath,
    IReadOnlyList<string> DeclaredModels);
