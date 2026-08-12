using System;
using System.Collections.Generic;
using System.Linq;

namespace DownloadProbe;

internal enum ConfigurationDecisionKind
{
    /// <summary>A <c>NoAction</c> selection existed and was chosen. The normal, quiet outcome.</summary>
    AnsweredNoAction,

    /// <summary>
    /// No <c>NoAction</c> existed, exactly one non-denied selection did, and it was taken because
    /// there was nothing else to take. LOUD: the tool had no choice, and the log must say so.
    /// </summary>
    AnsweredOnlySelectionAvailable,

    /// <summary>
    /// No <c>NoAction</c>, and every selection on offer is on the deny list. Left unhandled on
    /// purpose; the download is expected to abort, and that abort is the result.
    /// </summary>
    LeftUnhandledOnlyDeniedSelections,

    /// <summary>
    /// No <c>NoAction</c>, and several permitted selections — so choosing would be guessing about
    /// something the caller never stated. Left unhandled. Loud, because it is the case most likely
    /// to need a rule adding.
    /// </summary>
    LeftUnhandledAmbiguousWithoutNoAction,

    /// <summary>
    /// The configuration exposes no selection at all (a check configuration's <c>Checked</c> flag, a
    /// password configuration, or an unrecognised shape). Nothing is written to it.
    /// </summary>
    LeftUnhandledNoSelectionProperty,
}

internal sealed class ConfigurationDecision
{
    private ConfigurationDecision(ConfigurationDecisionKind kind, string? selection, string reason)
    {
        Kind = kind;
        Selection = selection;
        Reason = reason;
    }

    internal ConfigurationDecisionKind Kind { get; }

    /// <summary>The selection to apply, or null to leave the configuration unhandled.</summary>
    internal string? Selection { get; }

    internal string Reason { get; }

    /// <summary>True for the two outcomes a reader must not skim past.</summary>
    internal bool IsLoud =>
        Kind is ConfigurationDecisionKind.AnsweredOnlySelectionAvailable
             or ConfigurationDecisionKind.LeftUnhandledOnlyDeniedSelections
             or ConfigurationDecisionKind.LeftUnhandledAmbiguousWithoutNoAction;

    internal static ConfigurationDecision Choose(ConfigurationDecisionKind kind, string selection, string reason) =>
        new(kind, selection, reason);

    internal static ConfigurationDecision LeaveUnhandled(ConfigurationDecisionKind kind, string reason) =>
        new(kind, null, reason);
}

/// <summary>
/// What this tool answers to a download configuration. Pure, and the only place a selection is ever
/// decided — <see cref="ConfigurationRecorder"/> applies whatever comes back and nothing else.
///
/// THE RULE, IN THE ORDER IT IS APPLIED:
///
///   1. If a <c>NoAction</c> selection exists, choose it. Always, with no exceptions and no
///      per-configuration special cases.
///   2. Otherwise, if exactly one selection remains after removing everything on
///      <see cref="DeniedSelections"/>, take it — and say loudly that there was no choice.
///   3. Otherwise leave the configuration unhandled and let the download abort. That covers both
///      "every option is destructive" and "several options, none obviously right".
///
/// WHY RULE 2 EXISTS AT ALL. The first draft of this tool left every configuration without a
/// <c>NoAction</c> unhandled. <c>ConsistentBlocksDownload</c> — "Download software to device" — has
/// exactly ONE selection, <c>ConsistentDownload</c>, and no <c>NoAction</c>. Under the stricter rule
/// every run of every option would have aborted on that entirely harmless bookkeeping prompt, before
/// <c>DataBlockReinitialization</c> — the only question this experiment exists to answer — was ever
/// reached. A guard that aborts on everything proves nothing.
///
/// WHY THE DENY LIST IS DATA. It is keyed on (configuration type, selection name) and written out
/// below so a reviewer reads it rather than reconstructs it. It is not a runtime judgement about
/// which names look dangerous: every entry is a documented consequence, and every one of them has a
/// <c>NoAction</c> sibling, so rule 1 already covers them and the deny list is the backstop for the
/// case where it somehow does not.
/// </summary>
internal static class NoActionFirstPolicy
{
    /// <summary>The only selection this tool ever prefers. There is no second preference.</summary>
    internal const string NoAction = "NoAction";

    /// <summary>
    /// (configuration type name, selection name) pairs that must never be chosen, whatever else is
    /// or is not available. Type name is the simple name, matched ordinally.
    ///
    /// Each entry, with the documented consequence:
    ///   StopModules               / StopAll                -> stops the CPU.
    ///   DataBlockReinitialization / StopPlcAndReinitialize -> "All data values, including retain
    ///                                                         data, will be initialized with their
    ///                                                         defined start values during loading.
    ///                                                         Set the PLC to STOP before loading."
    ///   ResetModule               / DeleteAll              -> deletes the module's contents.
    ///   InitializeMemory          / AcceptAll              -> initialises memory.
    ///
    /// There is deliberately no flag, argument or environment variable that empties this list.
    /// </summary>
    internal static readonly IReadOnlyList<(string ConfigurationType, string Selection)> DeniedSelections =
        new[]
        {
            ("StopModules", "StopAll"),
            ("DataBlockReinitialization", "StopPlcAndReinitialize"),
            ("ResetModule", "DeleteAll"),
            ("InitializeMemory", "AcceptAll"),
        };

    internal static bool IsDenied(string configurationTypeName, string selectionName) =>
        DeniedSelections.Any(d =>
            string.Equals(d.ConfigurationType, configurationTypeName, StringComparison.Ordinal) &&
            string.Equals(d.Selection, selectionName, StringComparison.Ordinal));

    /// <summary>
    /// Decides from the configuration's SIMPLE type name and the selections on offer. Takes the
    /// names rather than the view so the rule can be exercised over shapes that do not exist in this
    /// V20 assembly — the unknown configuration is the one worth testing.
    /// </summary>
    internal static ConfigurationDecision Decide(string configurationTypeName, IReadOnlyList<string>? availableSelections)
    {
        if (availableSelections is null)
        {
            return ConfigurationDecision.LeaveUnhandled(
                ConfigurationDecisionKind.LeftUnhandledNoSelectionProperty,
                "the configuration exposes no CurrentSelection property; nothing was written to it");
        }

        if (availableSelections.Contains(NoAction, StringComparer.Ordinal))
        {
            return ConfigurationDecision.Choose(
                ConfigurationDecisionKind.AnsweredNoAction,
                NoAction,
                "a NoAction selection exists, so it is chosen");
        }

        var denied = availableSelections.Where(s => IsDenied(configurationTypeName, s)).ToList();
        var permitted = availableSelections.Where(s => !IsDenied(configurationTypeName, s)).ToList();

        if (permitted.Count == 1)
        {
            return ConfigurationDecision.Choose(
                ConfigurationDecisionKind.AnsweredOnlySelectionAvailable,
                permitted[0],
                $"no NoAction selection exists on '{configurationTypeName}'; '{permitted[0]}' is the only " +
                "permitted selection offered, so it was taken because there was nothing else to take" +
                (denied.Count > 0 ? $" (denied and not chosen: {string.Join(", ", denied)})" : string.Empty));
        }

        if (permitted.Count == 0)
        {
            return ConfigurationDecision.LeaveUnhandled(
                ConfigurationDecisionKind.LeftUnhandledOnlyDeniedSelections,
                $"no NoAction selection exists on '{configurationTypeName}' and every selection offered is " +
                $"on the deny list ({string.Join(", ", denied)}); left UNHANDLED on purpose, so the download " +
                "aborts rather than proceeds destructively");
        }

        return ConfigurationDecision.LeaveUnhandled(
            ConfigurationDecisionKind.LeftUnhandledAmbiguousWithoutNoAction,
            $"no NoAction selection exists on '{configurationTypeName}' and {permitted.Count} permitted " +
            $"selections are offered ({string.Join(", ", permitted)}); choosing between them would be a guess " +
            "this tool is not entitled to make, so it is left UNHANDLED");
    }
}
