using System;
using System.Collections.Generic;
using System.Linq;

namespace DownloadProbe;

/// <summary>
/// WHAT ACTUALLY HAPPENED to one configuration — as distinct from what the policy DECIDED about it.
///
/// The two were conflated, and a live run showed exactly why that is not survivable. The log said
/// <c>FAILED to apply 'NoAction'</c>, and then the abort summary said
/// <c>why unhandled : a NoAction selection exists, so it is chosen</c> — the policy's reason,
/// printed for a configuration the policy had answered and the API had refused. The two readings are
/// opposites: one says the tool worked and correctly prevented a download, the other says the tool is
/// broken and the run means nothing. A reader had to notice the contradiction to tell which.
/// </summary>
internal enum ConfigurationOutcomeKind
{
    /// <summary>The policy chose a selection and the API took it. The normal, quiet outcome.</summary>
    Answered,

    /// <summary>
    /// The set was accepted and the value did not stick — the read-back differs from what was
    /// written. A silent no-op looks exactly like a success, so it is not filed as one.
    /// </summary>
    AnsweredButReadBackDiffers,

    /// <summary>
    /// *** THE TOOL WORKED. *** The policy declined to answer — every selection denied, or several
    /// permitted ones and no basis to choose. The download aborting on this is the fail-closed guard
    /// doing its job, and the abort is the result.
    /// </summary>
    RefusedByPolicy,

    /// <summary>
    /// The configuration exposes no selection at all (a check configuration's <c>Checked</c> flag, a
    /// password configuration, an unrecognised shape). Nothing was written to it — not a refusal and
    /// not a failure, because there was nothing to answer.
    /// </summary>
    NotAnswerable,

    /// <summary>
    /// *** THE TOOL IS BROKEN. *** The policy chose a permitted selection and the API would not
    /// accept it. Nothing about the download was tested: whatever the run reports afterwards is the
    /// consequence of the tool's own failure, never of the policy.
    /// </summary>
    FailedToApply,
}

/// <summary>One configuration raised, what it said, what we decided, and what the API did with it.</summary>
internal sealed class RecordedConfiguration
{
    internal RecordedConfiguration(
        string phase,
        int ordinal,
        string typeName,
        string? message,
        IReadOnlyList<string> availableSelections,
        string? chosenSelection,
        ConfigurationDecisionKind decision,
        string reason,
        ConfigurationOutcomeKind outcome,
        string? attemptedSelection = null,
        string? failureSummary = null,
        string? appliedByRoute = null)
    {
        Phase = phase;
        Ordinal = ordinal;
        TypeName = typeName;
        Message = message;
        AvailableSelections = availableSelections;
        ChosenSelection = chosenSelection;
        Decision = decision;
        Reason = reason;
        Outcome = outcome;
        AttemptedSelection = attemptedSelection;
        FailureSummary = failureSummary;
        AppliedByRoute = appliedByRoute;
    }

    internal string Phase { get; }

    internal int Ordinal { get; }

    internal string TypeName { get; }

    internal string? Message { get; }

    internal IReadOnlyList<string> AvailableSelections { get; }

    /// <summary>The selection that WENT IN. Null unless the API accepted it.</summary>
    internal string? ChosenSelection { get; }

    /// <summary>What the policy decided. Says nothing about whether it could be applied.</summary>
    internal ConfigurationDecisionKind Decision { get; }

    /// <summary>The POLICY's reason. Never the reason a configuration went unanswered by failure.</summary>
    internal string Reason { get; }

    internal ConfigurationOutcomeKind Outcome { get; }

    /// <summary>What the policy asked for, whether or not it was accepted.</summary>
    internal string? AttemptedSelection { get; }

    /// <summary>Set only on <see cref="ConfigurationOutcomeKind.FailedToApply"/>: every route and every inner exception.</summary>
    internal string? FailureSummary { get; }

    /// <summary>Which route wrote the value, when one did. A fallback route working is itself a finding.</summary>
    internal string? AppliedByRoute { get; }

    /// <summary>The configuration went into the download unanswered, for any reason.</summary>
    internal bool WasLeftUnhandled => ChosenSelection is null;

    /// <summary>
    /// The one-line reason THIS configuration went unanswered — the failure when it failed, the
    /// policy's reason when the policy declined. The line that used to contradict itself.
    /// </summary>
    internal string WhyUnanswered => Outcome switch
    {
        ConfigurationOutcomeKind.FailedToApply =>
            $"*** FAILED TO APPLY '{AttemptedSelection}' *** — {FailureSummary}",
        _ => Reason,
    };
}

/// <summary>
/// The instrumentation. Logs every configuration in full, asks <see cref="NoActionFirstPolicy"/>
/// what to answer, applies exactly that and nothing else, and keeps the record.
///
/// It applies a decision; it never makes one. That split is what makes the selection rule testable
/// without Portal, and it is the property a reviewer checks in one read: the only value written to a
/// configuration anywhere in this program is <c>decision.Selection</c>, and the only thing that can
/// produce a non-null <c>Selection</c> is <c>NoActionFirstPolicy.Decide</c>. A failed apply does not
/// change that — the tool never falls back to a DIFFERENT selection, only to a different way of
/// writing the same one.
/// </summary>
internal sealed class ConfigurationRecorder
{
    private readonly ProbeLog _log;
    private readonly string _phase;
    private readonly SelectionPolicyMode _mode;
    private readonly List<RecordedConfiguration> _recorded = new();
    private int _ordinal;

    /// <summary>
    /// <paramref name="mode"/> defaults to <see cref="SelectionPolicyMode.Normal"/> so that the
    /// disruptive policy is never acquired by omission — a recorder built without stating a mode gets
    /// the one that refuses.
    /// </summary>
    internal ConfigurationRecorder(ProbeLog log, string phase, SelectionPolicyMode mode = SelectionPolicyMode.Normal)
    {
        _log = log;
        _phase = phase;
        _mode = mode;
    }

    internal IReadOnlyList<RecordedConfiguration> Recorded => _recorded;

    /// <summary>Everything that went into the download unanswered, for any reason.</summary>
    internal IReadOnlyList<RecordedConfiguration> Unhandled =>
        _recorded.Where(r => r.WasLeftUnhandled).ToList();

    /// <summary>Left unanswered ON PURPOSE. An abort caused by these is the guard working.</summary>
    internal IReadOnlyList<RecordedConfiguration> RefusedByPolicy =>
        _recorded.Where(r => r.Outcome == ConfigurationOutcomeKind.RefusedByPolicy).ToList();

    /// <summary>
    /// Answered by the policy and REJECTED by the API. Any of these and the run proves nothing —
    /// this is the bucket that must never be summarised as a refusal.
    /// </summary>
    internal IReadOnlyList<RecordedConfiguration> FailedToApply =>
        _recorded.Where(r => r.Outcome == ConfigurationOutcomeKind.FailedToApply).ToList();

    /// <summary>Nothing to answer — no selection property at all.</summary>
    internal IReadOnlyList<RecordedConfiguration> NotAnswerable =>
        _recorded.Where(r => r.Outcome == ConfigurationOutcomeKind.NotAnswerable).ToList();

    internal RecordedConfiguration Record(RaisedConfigurationView view)
    {
        _ordinal++;
        var simpleTypeName = SimpleName(view.RuntimeTypeName);
        var available = view.Selection?.AvailableSelections ?? Array.Empty<string>();

        _log.Blank();
        _log.Line($"---- [{_phase} #{_ordinal}] CONFIGURATION RAISED ----------------------------------------");
        _log.Line($"  runtime type : {view.RuntimeTypeName}");
        _log.Line($"  type chain   : {string.Join(" <- ", view.TypeChain)}");
        _log.Line($"  family       : {view.FamilyDescription}");

        if (view.MessageReadError is not null)
        {
            _log.Line($"  message      : <<READ FAILED: {view.MessageReadError}>>");
        }
        else
        {
            _log.Verbatim("  message      : ", view.Message);
        }

        var currentBefore = "(not read)";
        if (view.Selection is { } selection)
        {
            _log.Line($"  selection enum : {selection.EnumTypeName}");
            _log.Line($"  selections available ({selection.AvailableSelections.Count}):");
            foreach (var name in selection.AvailableSelections)
            {
                // Three annotations that must never merge into one: denied under the mode in force,
                // permitted ONLY because --disruptive was given, and the enum's zero value.
                var denied = NoActionFirstPolicy.IsDenied(simpleTypeName, name, _mode)
                    ? "   <== ON THE DENY LIST, NEVER CHOSEN"
                    : string.Empty;
                var allowance = NoActionFirstPolicy.IsDisruptiveAllowance(simpleTypeName, name, _mode)
                    ? "   <== PERMITTED ONLY BY --disruptive; DENIED BY THE NORMAL POLICY"
                    : string.Empty;
                var isDefault = string.Equals(name, selection.DefaultValuedSelection, StringComparison.Ordinal)
                    ? "   (= 0, the value an unset field reads as)"
                    : string.Empty;
                _log.Line($"      - {name}{denied}{allowance}{isDefault}");
            }

            _log.Line("                 (these are the names the ENUM DECLARES. Openness exposes no way to ask");
            _log.Line("                  which of them THIS instance would accept — so they are candidates, and");
            _log.Line("                  the ANSWER line below is the only evidence of what was actually taken.)");

            currentBefore = ReadCurrent(selection);
            _log.Line($"  current selection before we answered : {currentBefore}");
            if (string.Equals(currentBefore, selection.DefaultValuedSelection, StringComparison.Ordinal))
            {
                _log.Line("                 NOTE: that is the enum's ZERO value, so it is also what an unanswered");
                _log.Line("                 configuration reads as. It is NOT evidence that anything selected it.");
            }
        }
        else
        {
            _log.Line("  selections available : (none — this configuration exposes no CurrentSelection)");
        }

        foreach (var property in view.Properties)
        {
            _log.Line($"  property     : {property.Name} ({property.TypeName}) = {property.Value}");
        }

        LogAttributeInfos(view);

        var decision = NoActionFirstPolicy.Decide(simpleTypeName, view.Selection is null ? null : available, _mode);
        var record = Apply(view, decision, simpleTypeName, available, currentBefore);
        _recorded.Add(record);
        return record;
    }

    /// <summary>
    /// Openness' own account of what is writable on this live object. Printed next to the selection
    /// because it is the fact that decides whether a refused set is "the API says no" or "the tool
    /// asked wrongly" — <c>CurrentSelection</c> reporting <c>Read</c> here would settle it outright.
    /// </summary>
    private void LogAttributeInfos(RaisedConfigurationView view)
    {
        if (view.AttributeInfos.Count == 0)
        {
            return;
        }

        _log.Line($"  attribute infos (GetAttributeInfos(), {view.AttributeInfos.Count}) — Openness' own access modes for THIS object:");
        foreach (var info in view.AttributeInfos)
        {
            _log.Line($"      - {info.Name} : access={info.AccessMode} createRelevance={info.CreateRelevance}");
        }
    }

    private RecordedConfiguration Apply(
        RaisedConfigurationView view,
        ConfigurationDecision decision,
        string simpleTypeName,
        IReadOnlyList<string> available,
        string currentBefore)
    {
        if (decision.Selection is null)
        {
            _log.Line(decision.IsLoud
                ? $"  ANSWER       : *** LEFT UNHANDLED ON PURPOSE *** — {decision.Reason}"
                : $"  ANSWER       : left unhandled — {decision.Reason}");

            var refused = decision.Kind != ConfigurationDecisionKind.LeftUnhandledNoSelectionProperty;
            if (refused)
            {
                _log.Line("                 THE TOOL WORKED. An unhandled configuration that can prevent the");
                _log.Line("                 download aborts it (EngineeringTargetInvocationException), and that");
                _log.Line("                 abort is the result.");
            }

            return NewRecord(
                view, decision, simpleTypeName, available,
                chosen: null,
                outcome: refused ? ConfigurationOutcomeKind.RefusedByPolicy : ConfigurationOutcomeKind.NotAnswerable);
        }

        if (string.Equals(currentBefore, decision.Selection, StringComparison.Ordinal))
        {
            _log.Line($"  note         : the configuration ALREADY reads '{decision.Selection}'. The set is made anyway —");
            _log.Line("                 Openness' abort message speaks of a configuration being 'unhandled', which");
            _log.Line("                 reads as the ACT of answering being tracked, not merely the value present.");
        }

        var result = view.Selection!.Apply(decision.Selection);

        if (result.Succeeded)
        {
            // Three distinct sentences for three distinct reasons an answer was given. The disruptive
            // allowance gets its own, because "no choice was available" is exactly what it is NOT:
            // NoAction was available on every configuration this arm answers, and was passed over.
            if (decision.Kind == ConfigurationDecisionKind.AnsweredByDisruptiveAllowance)
            {
                _log.Line($"  ANSWER       : *** '{decision.Selection}' — BY THE DISRUPTIVE ALLOWANCE, NOT BY THE NORMAL POLICY ***");
                _log.Line($"                 {decision.Reason}");
            }
            else if (decision.IsLoud)
            {
                _log.Line($"  ANSWER       : *** '{decision.Selection}' — NO CHOICE WAS AVAILABLE ***");
                _log.Line($"                 {decision.Reason}");
            }
            else
            {
                _log.Line($"  ANSWER       : '{decision.Selection}' — by the NORMAL policy. {decision.Reason}");
            }

            var successful = result.SuccessfulAttempt!;
            _log.Line($"  applied via  : {successful.Route}");

            // A fallback that works is a finding in its own right: it means the primary route is
            // wrong, on a tool nobody may run casually to find that out.
            foreach (var failed in result.FailedAttempts)
            {
                _log.Line($"  *** NOTE     : an earlier route FAILED and a later one succeeded. The primary route is broken.");
                ExceptionReport.Write(_log, $"route that failed — {failed.Route}:", failed.Error, "                 ");
            }

            _log.Line($"  read back    : {result.ReadBack ?? "<<READ FAILED>>"}");
            if (result.ReadBackError is not null)
            {
                ExceptionReport.Write(_log, "read-back failure:", result.ReadBackError, "                 ");
            }

            if (result.ReadBackDiffers)
            {
                _log.Line("                 *** READ-BACK DOES NOT MATCH WHAT WAS SET — the selection did not stick.");
                return NewRecord(
                    view, decision, simpleTypeName, available,
                    chosen: decision.Selection,
                    outcome: ConfigurationOutcomeKind.AnsweredButReadBackDiffers,
                    attempted: decision.Selection,
                    appliedByRoute: successful.Route);
            }

            return NewRecord(
                view, decision, simpleTypeName, available,
                chosen: decision.Selection,
                outcome: ConfigurationOutcomeKind.Answered,
                attempted: decision.Selection,
                appliedByRoute: successful.Route);
        }

        // Every route was refused. This is a TOOL FAILURE and is filed as one — never as a refusal,
        // and never with the policy's reason attached, which would read as though the tool meant to
        // leave it unanswered.
        _log.Line($"  ANSWER       : *** FAILED TO APPLY '{decision.Selection}' — THIS IS A TOOL FAILURE, NOT A REFUSAL ***");
        _log.Line($"                 The policy decided correctly ({decision.Reason}); the API would not accept");
        _log.Line("                 the decision. Nothing about the download has been tested here, and any");
        _log.Line("                 abort that follows is caused by this tool, not by the fail-closed guard.");
        _log.Line($"                 routes tried: {result.Attempts.Count}");

        var ordinal = 0;
        foreach (var attempt in result.Attempts)
        {
            ordinal++;
            _log.Line($"  attempt {ordinal}/{result.Attempts.Count} : {attempt.Route} — FAILED");
            ExceptionReport.Write(_log, "exception chain, in full:", attempt.Error, "                 ");
        }

        _log.Line($"  read back    : {result.ReadBack ?? "<<READ FAILED>>"}  (unchanged by a refused set)");
        if (result.ReadBackError is not null)
        {
            ExceptionReport.Write(_log, "read-back failure:", result.ReadBackError, "                 ");
        }

        return NewRecord(
            view, decision, simpleTypeName, available,
            chosen: null,
            outcome: ConfigurationOutcomeKind.FailedToApply,
            attempted: decision.Selection,
            failureSummary: result.FailureSummary);
    }

    private RecordedConfiguration NewRecord(
        RaisedConfigurationView view,
        ConfigurationDecision decision,
        string simpleTypeName,
        IReadOnlyList<string> available,
        string? chosen,
        ConfigurationOutcomeKind outcome,
        string? attempted = null,
        string? failureSummary = null,
        string? appliedByRoute = null) =>
        new(
            _phase, _ordinal, simpleTypeName, view.Message, available, chosen,
            decision.Kind, decision.Reason, outcome, attempted, failureSummary, appliedByRoute);

    private string ReadCurrent(IConfigurationSelection selection)
    {
        try
        {
            return selection.ReadCurrentSelection();
        }
        catch (Exception ex)
        {
            ExceptionReport.Write(_log, "reading the current selection FAILED:", ex, "                 ");
            return $"<<READ FAILED: {ExceptionReport.Summarise(ex)}>>";
        }
    }

    private static string SimpleName(string fullName)
    {
        var lastDot = fullName.LastIndexOf('.');
        return lastDot >= 0 ? fullName.Substring(lastDot + 1) : fullName;
    }
}
