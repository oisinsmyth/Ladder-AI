using System;
using System.Collections.Generic;
using System.Linq;

namespace DownloadProbe;

/// <summary>
/// Which of the three download options this run selects. Required on the command line, with NO
/// default: a destructive option must never be reachable by omission.
///
/// <c>None</c> from the Siemens enum is deliberately absent — it downloads nothing, so it is not an
/// experiment, and accepting it would give this tool a reassuring-looking run that transferred
/// nothing at all.
/// </summary>
internal enum DownloadOptionChoice
{
    Software,
    SoftwareOnlyChanges,
    Hardware,
}

internal static class DownloadOptionChoices
{
    /// <summary>
    /// The three accepted spellings, exact. The error message prints this list.
    /// </summary>
    internal static readonly IReadOnlyList<string> Literals =
        new[] { "Software", "SoftwareOnlyChanges", "Hardware" };

    /// <summary>
    /// Literal matching against the three names, and NOT <c>Enum.TryParse</c>.
    ///
    /// <c>Enum.TryParse&lt;DownloadOptionChoice&gt;("0", out _)</c> returns true and yields
    /// <c>Software</c>; "2" yields <c>Hardware</c>; and so does any other numeric string in range,
    /// because the parser accepts the underlying integer as readily as the name. On a selector where
    /// one value stops the CPU and another resets retentive data, a typo that lands on a number must
    /// be an error, not a selection.
    ///
    /// Case-insensitive, deliberately matching `openness-cli download-plan --options`: the same words
    /// are typed at both commands and a divergence there would be a trap, not a safeguard. Case is
    /// not the hazard — numeric and near-miss spellings are, and both are refused.
    /// </summary>
    internal static bool TryParseLiteral(string? text, out DownloadOptionChoice choice)
    {
        if (string.Equals(text, "Software", StringComparison.OrdinalIgnoreCase))
        {
            choice = DownloadOptionChoice.Software;
            return true;
        }

        if (string.Equals(text, "SoftwareOnlyChanges", StringComparison.OrdinalIgnoreCase))
        {
            choice = DownloadOptionChoice.SoftwareOnlyChanges;
            return true;
        }

        if (string.Equals(text, "Hardware", StringComparison.OrdinalIgnoreCase))
        {
            choice = DownloadOptionChoice.Hardware;
            return true;
        }

        choice = default;
        return false;
    }

    internal static string DescribeRejection(string? text) =>
        $"--options: '{text}' is not one of the three permitted values. " +
        $"Expected exactly one of: {string.Join(" | ", Literals)}. " +
        "There is no default: one of these values stops the CPU and another resets retentive data, " +
        "so the option is always stated explicitly. Numeric values are rejected on purpose — " +
        "Enum.TryParse would accept '0'/'1'/'2' and silently select one of them.";

    /// <summary>
    /// What this option does to the device, printed on EVERY run that selects it, before anything is
    /// contacted. Same posture as `openness-cli block-layout --set`: the consequence is stated where
    /// it cannot be missed, not buried in a README.
    /// </summary>
    internal static IReadOnlyList<string> ConsequenceWarning(DownloadOptionChoice choice) => choice switch
    {
        DownloadOptionChoice.Software => new[]
        {
            "!! DESTRUCTIVE OPTION SELECTED: Software — TIA's \"Software (all)\".",
            "!! Siemens, verbatim: \"The PLC program including all blocks, PLC data types and PLC tags",
            "!! is downloaded to the target and all values are reset to their initial values. Be aware",
            "!! that this also applies to retentive values.\"",
            "!! ==> THIS WIPES RETENTIVE DATA ON THE DEVICE.",
        },
        DownloadOptionChoice.Hardware => new[]
        {
            "!! DESTRUCTIVE OPTION SELECTED: Hardware.",
            "!! A hardware download ALWAYS STOPS THE CPU. Nothing in this project's tooling can return",
            "!! a CPU to RUN — that requires a person in TIA Portal.",
            "!! UNRESOLVED (external gap G3): whether a hardware download also wipes retentive data.",
            "!! Two Siemens editions contradict each other, so treat it as UNKNOWN, not as safe.",
        },
        DownloadOptionChoice.SoftwareOnlyChanges => new[]
        {
            "-- Option selected: SoftwareOnlyChanges — the only one the design wants for normal use.",
            "-- \"Only changes\" is TIA's own online-versus-offline comparison, decided at download",
            "-- time. It is NOT the list of blocks you edited, and it is not under this tool's control.",
        },
        _ => throw new ArgumentOutOfRangeException(nameof(choice), choice, "Unhandled download option."),
    };

    /// <summary>
    /// Printed alongside the warning on every run. It is a prediction, not a guard: because this
    /// tool answers <c>NoAction</c> to everything destructive, an option that cannot proceed without
    /// a destructive answer is expected to abort — and that abort is the finding.
    /// </summary>
    internal static IReadOnlyList<string> AbortPrediction(DownloadOptionChoice choice) =>
        PredictionFor(choice).Concat(AbortIsOnlyAResultWhenTheToolWorked).ToList();

    /// <summary>
    /// Printed under every prediction, because the two ways a download can fail to proceed look
    /// identical from outside and mean opposite things — and a live run demonstrated the second one:
    /// the policy chose <c>NoAction</c> for <c>StopModules</c>, exactly as designed, and the API
    /// refused the set.
    /// </summary>
    private static readonly IReadOnlyList<string> AbortIsOnlyAResultWhenTheToolWorked = new[]
    {
        string.Empty,
        "AND THE DISTINCTION THAT MAKES THAT READABLE: an abort is a RESULT only when this tool",
        $"ANSWERED everything it meant to answer (exit {ProbeExitCodes.AbortedByUnhandledConfiguration}). If a selection it DECIDED ON cannot be",
        $"APPLIED, the run exits {ProbeExitCodes.SelectionApplyFailed} and PROVES NOTHING — the download stopped at the tool's own",
        "failure, so a configuration that would have been raised later could not appear in the log.",
    };

    private static IReadOnlyList<string> PredictionFor(DownloadOptionChoice choice) => choice switch
    {
        DownloadOptionChoice.Hardware => new[]
        {
            "PREDICTION: this run is EXPECTED TO ABORT. Proceeding would require answering StopModules",
            "with StopAll, which is on the deny list and will never be chosen. An abort here is a",
            $"RESULT (exit {ProbeExitCodes.AbortedByUnhandledConfiguration}), not a failure: it means the fail-closed posture genuinely prevents",
            "the destructive option.",
        },
        DownloadOptionChoice.Software => new[]
        {
            "PREDICTION: this run MAY ABORT, for the same reason as Hardware — a full software",
            "download that needs StopModules=StopAll or DataBlockReinitialization=StopPlcAndReinitialize",
            $"cannot get either. That abort is a RESULT (exit {ProbeExitCodes.AbortedByUnhandledConfiguration}), not a failure.",
        },
        DownloadOptionChoice.SoftwareOnlyChanges => new[]
        {
            "NOTE: an abort is still possible and is still a result — the log names which",
            "configuration was left unhandled.",
        },
        _ => throw new ArgumentOutOfRangeException(nameof(choice), choice, "Unhandled download option."),
    };
}
