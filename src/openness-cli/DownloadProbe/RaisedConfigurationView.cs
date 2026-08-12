using System;
using System.Collections.Generic;
using System.Linq;

namespace DownloadProbe;

/// <summary>
/// One download configuration as this tool sees it, with every Siemens type already read off.
///
/// The seam exists so the DECISION is testable without Portal. Every concrete configuration type in
/// <c>Siemens.Engineering.Download.Configurations</c> has an internal constructor and is produced
/// only by a live download, so a test can never build one — and "which selection did we make" is
/// exactly the question the tests have to answer, because nobody may run this tool to find out.
/// <see cref="SiemensConfigurationReader"/> is the one adapter that produces these from the real
/// objects.
/// </summary>
internal sealed class RaisedConfigurationView
{
    internal RaisedConfigurationView(
        string runtimeTypeName,
        IReadOnlyList<string> typeChain,
        string? message,
        string? messageReadError,
        string familyDescription,
        IConfigurationSelection? selection,
        IReadOnlyList<ConfigurationPropertyView> properties,
        IReadOnlyList<ConfigurationAttributeInfoView>? attributeInfos = null)
    {
        RuntimeTypeName = runtimeTypeName;
        TypeChain = typeChain;
        Message = message;
        MessageReadError = messageReadError;
        FamilyDescription = familyDescription;
        Selection = selection;
        Properties = properties;
        AttributeInfos = attributeInfos ?? Array.Empty<ConfigurationAttributeInfoView>();
    }

    /// <summary>The runtime type's full name. Not assumed to be a documented Siemens class.</summary>
    internal string RuntimeTypeName { get; }

    /// <summary>
    /// The whole base-type chain, most-derived first. Logged because an UNRECOGNISED configuration
    /// is the most valuable result this experiment can produce, and its family
    /// (DownloadSelectionConfiguration / DownloadCheckConfiguration / DownloadPasswordConfiguration)
    /// is the first thing a reader needs.
    /// </summary>
    internal IReadOnlyList<string> TypeChain { get; }

    /// <summary>The configuration's own Message, verbatim. Never trimmed, dedup'd or shortened.</summary>
    internal string? Message { get; }

    /// <summary>Set instead of <see cref="Message"/> when reading it threw.</summary>
    internal string? MessageReadError { get; }

    internal string FamilyDescription { get; }

    /// <summary>Null when the configuration exposes no CurrentSelection at all.</summary>
    internal IConfigurationSelection? Selection { get; }

    /// <summary>Every other readable property, so an unknown configuration is still logged in full.</summary>
    internal IReadOnlyList<ConfigurationPropertyView> Properties { get; }

    /// <summary>
    /// Openness' own self-description of this INSTANCE's attributes — name, access mode, create
    /// relevance — from <c>IEngineeringObject.GetAttributeInfos()</c>.
    ///
    /// Added because a selection that would not apply is otherwise unexplainable from the log: the
    /// CLR property is settable (that is compile-time metadata and says nothing about the live
    /// object), but Openness reports a per-object <c>AccessMode</c> of
    /// <c>None</c>/<c>Read</c>/<c>Write</c>/<c>ReadWrite</c>, and <c>CurrentSelection</c> reading
    /// <c>Read</c> here would answer "why did the set throw" outright. Cost is one read; it is never
    /// written to.
    /// </summary>
    internal IReadOnlyList<ConfigurationAttributeInfoView> AttributeInfos { get; }
}

/// <summary>One entry from <c>GetAttributeInfos()</c>, rendered.</summary>
internal sealed class ConfigurationAttributeInfoView
{
    internal ConfigurationAttributeInfoView(string name, string accessMode, string createRelevance)
    {
        Name = name;
        AccessMode = accessMode;
        CreateRelevance = createRelevance;
    }

    internal string Name { get; }

    /// <summary>None / Read / Write / ReadWrite, as Openness reports it for this live object.</summary>
    internal string AccessMode { get; }

    internal string CreateRelevance { get; }
}

internal sealed class ConfigurationPropertyView
{
    internal ConfigurationPropertyView(string name, string typeName, string value)
    {
        Name = name;
        TypeName = typeName;
        Value = value;
    }

    internal string Name { get; }

    internal string TypeName { get; }

    /// <summary>Rendered value, or the exception text if reading it threw. Never omitted.</summary>
    internal string Value { get; }
}

/// <summary>
/// The selection half of a configuration: what could be chosen, what is chosen now, and the one
/// operation that changes it.
///
/// Openness gives no common base for this. <c>DownloadConfiguration</c> exposes only
/// <c>Message</c>/<c>Parent</c>; each concrete configuration declares its OWN
/// <c>CurrentSelection</c> property over its OWN enum type (verified by reflecting the installed
/// V20 <c>Siemens.Engineering.dll</c>). So there is no typed way to ask "what are the choices" —
/// the answer is <c>Enum.GetNames</c> on whatever enum that property happens to be, which is also
/// why an unfamiliar configuration is handled by the same code path as a familiar one.
/// </summary>
internal interface IConfigurationSelection
{
    string EnumTypeName { get; }

    /// <summary>
    /// Every selection the enum DECLARES, by name, in declaration order.
    ///
    /// Read as "the names this type could ever have", NOT as "the answers this instance will accept"
    /// — Openness offers no way to ask the second question, and the difference is not academic: a
    /// live run had <c>NoAction</c> in this list, chose it, and had the set rejected. So a name
    /// appearing here is a CANDIDATE. What the API actually accepted is
    /// <see cref="SelectionApplyResult"/>, and only that.
    /// </summary>
    IReadOnlyList<string> AvailableSelections { get; }

    /// <summary>
    /// The name of the enum member whose numeric value is zero, if any.
    ///
    /// Worth logging because it is the value an unset field reads as, so "current selection is
    /// <c>NoAction</c>" is NOT evidence that anything ever selected <c>NoAction</c> when
    /// <c>NoAction = 0</c> — which is exactly the case on <c>StopModulesSelections</c>. Null when no
    /// member is zero (<c>DataBlockReinitializationSelections</c> has <c>NoAction = 1</c>, so the
    /// same reading there WOULD mean something).
    /// </summary>
    string? DefaultValuedSelection { get; }

    string ReadCurrentSelection();

    /// <summary>
    /// Applies the named selection, by every route available, and reports what each route did.
    /// Never throws: a rejected selection is a finding this tool has to report precisely, not an
    /// exception for a caller to flatten into one line.
    /// </summary>
    SelectionApplyResult Apply(string selectionName);
}

/// <summary>One route tried when applying a selection, and what it did.</summary>
internal sealed class SelectionApplyAttempt
{
    internal SelectionApplyAttempt(string route, bool succeeded, Exception? error)
    {
        Route = route;
        Succeeded = succeeded;
        Error = error;
    }

    /// <summary>How the value was written — named exactly, because which route worked is a finding.</summary>
    internal string Route { get; }

    internal bool Succeeded { get; }

    /// <summary>
    /// The exception as thrown, whole. NOT a message string: the reason this type exists is that a
    /// message string is precisely what threw the real cause away last time.
    /// </summary>
    internal Exception? Error { get; }
}

/// <summary>
/// The outcome of trying to apply one selection: every route attempted, in order, and the read-back.
/// </summary>
internal sealed class SelectionApplyResult
{
    internal SelectionApplyResult(
        string requestedSelection,
        IReadOnlyList<SelectionApplyAttempt> attempts,
        string? readBack,
        Exception? readBackError = null)
    {
        RequestedSelection = requestedSelection;
        Attempts = attempts;
        ReadBack = readBack;
        ReadBackError = readBackError;
    }

    internal string RequestedSelection { get; }

    internal IReadOnlyList<SelectionApplyAttempt> Attempts { get; }

    /// <summary>What the property read back afterwards, or null if reading it also failed.</summary>
    internal string? ReadBack { get; }

    internal Exception? ReadBackError { get; }

    internal bool Succeeded => Attempts.Any(a => a.Succeeded);

    internal SelectionApplyAttempt? SuccessfulAttempt => Attempts.FirstOrDefault(a => a.Succeeded);

    internal IReadOnlyList<SelectionApplyAttempt> FailedAttempts =>
        Attempts.Where(a => !a.Succeeded).ToList();

    /// <summary>
    /// True when the value went in but did not stay — a silent no-op, the failure mode that looks
    /// exactly like a success.
    /// </summary>
    internal bool ReadBackDiffers =>
        Succeeded && !string.Equals(ReadBack, RequestedSelection, StringComparison.Ordinal);

    /// <summary>The whole failure, one line, with every inner exception named.</summary>
    internal string FailureSummary =>
        FailedAttempts.Count == 0
            ? "(nothing failed)"
            : string.Join(
                " | ",
                FailedAttempts.Select(a => $"{a.Route}: {ExceptionReport.Summarise(a.Error)}"));
}
