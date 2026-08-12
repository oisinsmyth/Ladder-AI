using System;
using System.Collections.Generic;
using System.Linq;

namespace DownloadProbe;

/// <summary>
/// Which of the two selection policies is in force. There are exactly two, they are named, and the
/// second is reachable ONLY from <c>--disruptive</c> on the command line.
/// </summary>
internal enum SelectionPolicyMode
{
    /// <summary>
    /// The wave-loop policy, and the default in every constructor and every overload in this program.
    /// NoAction wherever it exists; never a denied selection; otherwise leave it unhandled and let the
    /// download abort. This is what six live runs exercised.
    /// </summary>
    Normal,

    /// <summary>
    /// R8's sanctioned exception: answer — and ONLY answer — the selections the chosen download option
    /// already entails, so that a download can actually complete. Everything else about the policy is
    /// unchanged, including two denials that survive here (see
    /// <see cref="NoActionFirstPolicy.DisruptiveDeniedSelections"/>).
    /// </summary>
    Disruptive,
}

internal enum ConfigurationDecisionKind
{
    /// <summary>A <c>NoAction</c> selection existed and was chosen. The normal, quiet outcome.</summary>
    AnsweredNoAction,

    /// <summary>
    /// *** ONLY REACHABLE UNDER <see cref="SelectionPolicyMode.Disruptive"/>. *** A selection on
    /// <see cref="NoActionFirstPolicy.DisruptiveAllowances"/> was offered and was chosen AHEAD of
    /// <c>NoAction</c>. Never blurred with <see cref="AnsweredNoAction"/>: this is the arm that stops
    /// the CPU, and a log in which it read the same as the normal policy would be worthless.
    /// </summary>
    AnsweredByDisruptiveAllowance,

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

    /// <summary>True for the outcomes a reader must not skim past.</summary>
    internal bool IsLoud =>
        Kind is ConfigurationDecisionKind.AnsweredOnlySelectionAvailable
             or ConfigurationDecisionKind.AnsweredByDisruptiveAllowance
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
///   0. ONLY UNDER <see cref="SelectionPolicyMode.Disruptive"/>: if the (configuration, selection)
///      pair is on <see cref="DisruptiveAllowances"/>, choose it — ahead of <c>NoAction</c>. In
///      <see cref="SelectionPolicyMode.Normal"/> this rule does not exist and cannot be reached; the
///      mode comes from <c>--disruptive</c> and from nothing else.
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
    /// <c>--disruptive</c> SHRINKS this list to <see cref="DisruptiveDeniedSelections"/>. It has never
    /// emptied it and no flag empties it: the last two entries are denied on both paths.
    /// </summary>
    internal static readonly IReadOnlyList<(string ConfigurationType, string Selection)> DeniedSelections =
        new[]
        {
            ("StopModules", "StopAll"),
            ("DataBlockReinitialization", "StopPlcAndReinitialize"),
            ("ResetModule", "DeleteAll"),
            ("InitializeMemory", "AcceptAll"),
        };

    /// <summary>
    /// *** THE HALF THAT MATTERS UNDER <c>--disruptive</c>: WHAT STAYS DENIED. ***
    ///
    /// The disruptive mode does not open the guard, it moves it. Two entries leave the deny list and
    /// TWO REMAIN, because the line is not "destructive / not destructive" — everything here is
    /// destructive — but *** WHETHER THE CHOSEN DOWNLOAD OPTION ALREADY ENTAILS IT. ***
    ///
    ///   ResetModule      / DeleteAll  -> deletes the module's contents. No download option entails
    ///                                    emptying the module; that is a different act from loading a
    ///                                    program into it.
    ///   InitializeMemory / AcceptAll  -> initialises memory. Likewise: beyond what loading entails.
    ///
    /// If either is raised, it is left UNHANDLED and the download aborts exactly as it does today —
    /// the disruptive mode contains no code path that answers them.
    /// </summary>
    internal static readonly IReadOnlyList<(string ConfigurationType, string Selection)> DisruptiveDeniedSelections =
        new[]
        {
            ("ResetModule", "DeleteAll"),
            ("InitializeMemory", "AcceptAll"),
        };

    /// <summary>
    /// *** THE SELECTIONS <c>--disruptive</c> ADDS, IN FULL. NOTHING ELSE IS ADDED ANYWHERE. ***
    ///
    /// RATIONALE, per entry — each is a consequence THE CHOSEN <c>--options</c> VALUE ALREADY MEANS,
    /// so permitting it adds no destruction beyond what selecting that option already decided:
    ///
    ///   StopModules               / StopAll                 The modules must be stopped to load a
    ///       full program. Measured 2026-08-12: `Software` and `Hardware` both raise this and the API
    ///       REJECTS `NoAction` on it — so the stop is a PRECONDITION, not a prompt, and there is no
    ///       version of "download the whole program without stopping" for this tool to prefer.
    ///
    ///   DataBlockReinitialization / StopPlcAndReinitialize  `Software` (all) resets all values
    ///       including retentive ones by definition, and a differential download raises this only
    ///       because a DB was RESTRUCTURED — which reinitialises that DB by definition. The
    ///       configuration is reporting the consequence of the change, not proposing an extra one.
    ///
    ///   OverwriteSystemData       / Overwrite               `DownloadOptions.Hardware` MEANS
    ///       downloading a hardware configuration, and replacing the target's system data IS what a
    ///       hardware download does — the configuration reports the consequence of the option the
    ///       caller chose, it does not propose an extra one. That is exactly R8's test for membership
    ///       of this list. Measured 2026-08-12 on a live rig: a `Hardware` download raised this with
    ///       its current selection ALREADY `Overwrite`, and the normal policy's `NoAction` FAILED TO
    ///       APPLY by both routes (property set and SetAttribute), so the configuration went
    ///       unanswered and Openness aborted.
    ///
    ///   StartModules              / StartModule             Puts the modules back. This is the only
    ///       route to RUN this tool has: `IS7Client` exposes no mode change, and nothing else here
    ///       does either. Answering it is strictly LESS destructive than not answering it — the
    ///       alternative outcome is a CPU left in STOP.
    ///
    /// *** THE PATTERN, NOW SEEN THREE TIMES, AND WHY THE `NoAction` DECLINE IS LARGELY FICTIONAL. ***
    /// StopModules, DataBlockReinitialization and OverwriteSystemData each DECLARE `NoAction` in their
    /// selection enum and each REJECT IT ON THE INSTANCE when the chosen download option entails the
    /// action (`StopAll`, `StopPlcAndReinitialize`, `Overwrite` respectively). So the enum's decline
    /// option is not a guard: what the enum offers and what the instance accepts are different things,
    /// and only REFUSING TO ANSWER — leaving the configuration unhandled so the download aborts — is a
    /// real one. That is why this list must name what each option ENTAILS rather than what looks safe.
    ///
    /// *** THESE ARE CHOSEN AHEAD OF <c>NoAction</c>, WHICH IS THE WHOLE POINT AND MUST NOT BE READ
    /// AS AN OVERSIGHT. *** Every one of these configurations also offers `NoAction`, so a mode that
    /// merely removed them from the deny list would still answer `NoAction`, still be refused by the
    /// API (measured, both routes, on both StopModules and DataBlockReinitialization), and still
    /// abort — i.e. it would do nothing at all. On `StartModules` the failure would be quieter and
    /// worse: `NoAction` there applies perfectly well and simply leaves the CPU stopped.
    /// </summary>
    internal static readonly IReadOnlyList<(string ConfigurationType, string Selection)> DisruptiveAllowances =
        new[]
        {
            ("StopModules", "StopAll"),
            ("DataBlockReinitialization", "StopPlcAndReinitialize"),
            ("OverwriteSystemData", "Overwrite"),
            ("StartModules", StartModulesSelection),
        };

    /// <summary>
    /// The selection that STARTS the modules, read from the installed V20 assembly rather than
    /// guessed: <c>StartModulesSelections</c> declares exactly <c>NoAction</c> and
    /// <c>StartModule</c>, documented "Start module". A test pins this name against the real enum, so
    /// a Siemens rename fails a build here rather than silently leaving a CPU in STOP at a rig.
    /// </summary>
    internal const string StartModulesSelection = "StartModule";

    /// <summary>One line naming every allowance, for the banner and the usage text.</summary>
    internal static string DisruptiveAllowanceSummary => Render(DisruptiveAllowances);

    /// <summary>One line naming what stays denied even under <c>--disruptive</c>.</summary>
    internal static string DisruptiveDenySummary => Render(DisruptiveDeniedSelections);

    private static string Render(IReadOnlyList<(string ConfigurationType, string Selection)> pairs) =>
        string.Join(", ", pairs.Select(p => $"{p.ConfigurationType} -> {p.Selection}"));

    /// <summary>
    /// The deny list in force for a mode. <see cref="SelectionPolicyMode.Disruptive"/> returns a
    /// SMALLER, NAMED list — never an empty one, and never a list computed by removing "whatever the
    /// allowances mention", which would silently empty itself if an allowance were ever added.
    /// </summary>
    internal static IReadOnlyList<(string ConfigurationType, string Selection)> DeniedSelectionsFor(
        SelectionPolicyMode mode) =>
        mode == SelectionPolicyMode.Disruptive ? DisruptiveDeniedSelections : DeniedSelections;

    internal static bool IsDenied(
        string configurationTypeName,
        string selectionName,
        SelectionPolicyMode mode = SelectionPolicyMode.Normal) =>
        DeniedSelectionsFor(mode).Any(d =>
            string.Equals(d.ConfigurationType, configurationTypeName, StringComparison.Ordinal) &&
            string.Equals(d.Selection, selectionName, StringComparison.Ordinal));

    /// <summary>
    /// True only under <see cref="SelectionPolicyMode.Disruptive"/>, and only for a pair spelled out
    /// in <see cref="DisruptiveAllowances"/>. Both halves are matched ordinally: an allowance for
    /// <c>StopModules</c> says nothing about any other configuration that happens to offer a
    /// selection of the same name.
    /// </summary>
    internal static bool IsDisruptiveAllowance(
        string configurationTypeName,
        string selectionName,
        SelectionPolicyMode mode) =>
        mode == SelectionPolicyMode.Disruptive &&
        DisruptiveAllowances.Any(a =>
            string.Equals(a.ConfigurationType, configurationTypeName, StringComparison.Ordinal) &&
            string.Equals(a.Selection, selectionName, StringComparison.Ordinal));

    /// <summary>
    /// Decides from the configuration's SIMPLE type name and the selections on offer. Takes the
    /// names rather than the view so the rule can be exercised over shapes that do not exist in this
    /// V20 assembly — the unknown configuration is the one worth testing.
    /// </summary>
    internal static ConfigurationDecision Decide(
        string configurationTypeName,
        IReadOnlyList<string>? availableSelections,
        SelectionPolicyMode mode = SelectionPolicyMode.Normal)
    {
        if (availableSelections is null)
        {
            return ConfigurationDecision.LeaveUnhandled(
                ConfigurationDecisionKind.LeftUnhandledNoSelectionProperty,
                "the configuration exposes no CurrentSelection property; nothing was written to it");
        }

        // RULE 0, and it exists only under --disruptive. It sits AHEAD of NoAction because every
        // configuration it covers also offers NoAction: answering NoAction is what six live runs did,
        // and it is what aborted all six. `mode` is the only thing that can reach this arm.
        var allowed = availableSelections
            .Where(s => IsDisruptiveAllowance(configurationTypeName, s, mode))
            .ToList();

        if (allowed.Count == 1)
        {
            return ConfigurationDecision.Choose(
                ConfigurationDecisionKind.AnsweredByDisruptiveAllowance,
                allowed[0],
                $"*** DISRUPTIVE MODE *** '{allowed[0]}' is answered on '{configurationTypeName}' because " +
                "--disruptive was given and this exact (configuration, selection) pair is on the allowance " +
                "list — a consequence the chosen download option already entails. The NORMAL policy would " +
                "have answered NoAction here and the download would have aborted");
        }

        if (allowed.Count > 1)
        {
            // Unreachable while the allowance list holds one selection per configuration, and kept
            // because "cannot happen" is not a reason to pick one of two ways to stop a CPU.
            return ConfigurationDecision.LeaveUnhandled(
                ConfigurationDecisionKind.LeftUnhandledAmbiguousWithoutNoAction,
                $"the allowance list offers {allowed.Count} selections for '{configurationTypeName}' " +
                $"({string.Join(", ", allowed)}); choosing between them is not this tool's call, so it is " +
                "left UNHANDLED even in disruptive mode");
        }

        if (availableSelections.Contains(NoAction, StringComparer.Ordinal))
        {
            return ConfigurationDecision.Choose(
                ConfigurationDecisionKind.AnsweredNoAction,
                NoAction,
                "a NoAction selection exists, so it is chosen");
        }

        var denied = availableSelections.Where(s => IsDenied(configurationTypeName, s, mode)).ToList();
        var permitted = availableSelections.Where(s => !IsDenied(configurationTypeName, s, mode)).ToList();

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
