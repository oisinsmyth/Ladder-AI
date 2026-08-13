namespace OpennessCli.Model;

/// <summary>
/// One object in the project model that answers <c>GetService&lt;ICompilable&gt;()</c>, or one that
/// was asked and did not.
///
/// The point of recording the ones that did NOT is that this is a survey of where a compile can be
/// started FROM, and "the software object has no compilable of its own" is as much of an answer as
/// "it has one" — see <see cref="CompileScopeSurvey"/>.
/// </summary>
/// <param name="Label">Human path, e.g. <c>Device 'PLC_1'/DeviceItem 'PLC_1' -> PlcSoftware</c>.</param>
/// <param name="OwnerType">Runtime type of the object that was asked.</param>
/// <param name="HasCompilable">Whether <c>GetService&lt;ICompilable&gt;()</c> returned non-null.</param>
/// <param name="CompilableType">Runtime type of the returned compilable, or null.</param>
/// <param name="ParentType">The compilable's own <c>Parent</c> type — what scope it belongs to.</param>
/// <param name="ParentName">The compilable's own <c>Parent</c> name, where it has one.</param>
/// <param name="IdentityGroup">
/// Providers that compare equal share a group number. Two scopes in the SAME group are the same
/// compile reached by two routes; two scopes in DIFFERENT groups are different compiles, and only
/// the second case can hide errors from a gate that runs just one of them.
/// </param>
/// <param name="Attributes">
/// The compiler object's own <c>GetAttributeInfos()</c> — reported because <c>ICompilable.Compile()</c>
/// takes NO arguments, so if a compile can be steered at all (delta versus rebuild-all, most of all)
/// the knob has to be a name-based attribute or a named invocation. Measured emptiness is the answer
/// to "can we ask for a rebuild-all"; it is not a reason to skip asking.
/// </param>
/// <param name="Invocations">
/// The compiler object's own <c>GetInvocationInfos()</c> — the other place a parameterised compile
/// could exist, reachable through <c>IEngineeringObject.Invoke(name, parameters)</c>.
/// </param>
/// <param name="Error">Set when the question could not be asked or answered.</param>
public sealed record CompileScope(
    string Label,
    string OwnerType,
    bool HasCompilable,
    string? CompilableType,
    string? ParentType,
    string? ParentName,
    int? IdentityGroup,
    IReadOnlyList<string> Attributes,
    IReadOnlyList<string> Invocations,
    string? Error);

/// <summary>
/// Every compile entry point Openness exposes on a project, and which of them are the same object.
///
/// Exists because of a measured disagreement (2026-08-13, JOB9004 scratch): four separate checks —
/// per-block compile, whole-device compile, <c>compile-all --force</c> and <c>sanity-check</c> —
/// all reported clean, while the compile TIA runs INSIDE a download failed and named a specific
/// block. Hard rule 4's gate is built entirely on the checks that said clean, so the first question
/// worth answering is a structural one: how many distinct compiles are there, and is the one the
/// gate runs the same object as the one the download runs?
///
/// This command answers only the structural half. It compiles nothing.
/// </summary>
public sealed record CompileScopeSurvey(
    IReadOnlyList<CompileScope> Scopes,
    int DistinctCompilers);
