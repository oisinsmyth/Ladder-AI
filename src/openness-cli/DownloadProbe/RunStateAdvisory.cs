using System;
using System.Collections.Generic;
using System.Linq;

namespace DownloadProbe;

/// <summary>
/// IS THE CPU GOING TO BE LEFT IN STOP?
///
/// Under <c>--disruptive</c> this tool answers <c>StopModules -> StopAll</c>, which stops the CPU. The
/// only route back it has is the post-download <c>StartModules</c> configuration — and that
/// configuration is raised BY TIA, if and when TIA decides to raise it. It may not be. So the honest
/// report has three shapes and this type is the one place that decides which one is printed:
///
///   - StartModules raised and answered  -> TIA was ASKED to start the modules. Not a reading of the
///                                          CPU: nothing in this program asks the controller anything.
///   - StartModules raised, not answered -> the request failed. The CPU may be in STOP.
///   - StartModules never raised         -> *** THE CPU MAY BE IN STOP AND A HUMAN MUST RESTART IT. ***
///
/// There is deliberately no fourth shape in which the tool invents a route. <c>IS7Client</c> exposes
/// no mode change, and <c>OnlineProvider</c> is SURVEYED and reported (see
/// <see cref="OnlineModeSurvey"/>) but never used — wiring a mode change is a separate decision that
/// has not been taken.
/// </summary>
internal static class RunStateAdvisory
{
    internal const string StartModulesConfiguration = "StartModules";

    internal const string StopModulesConfiguration = "StopModules";

    /// <summary>
    /// The lines that close the run. Loud when the CPU may be stopped, quiet when nothing this tool
    /// did could have stopped it — and never silent, because "no news" is the reading that leaves
    /// someone walking away from a stopped rig.
    /// </summary>
    internal static IReadOnlyList<string> Describe(
        SelectionPolicyMode mode, IReadOnlyList<RecordedConfiguration> configurations)
    {
        var stopped = configurations.FirstOrDefault(c =>
            string.Equals(c.TypeName, StopModulesConfiguration, StringComparison.Ordinal) &&
            string.Equals(c.ChosenSelection, "StopAll", StringComparison.Ordinal));

        var start = configurations.FirstOrDefault(c =>
            string.Equals(c.TypeName, StartModulesConfiguration, StringComparison.Ordinal));

        if (mode != SelectionPolicyMode.Disruptive)
        {
            return new[]
            {
                "CPU RUN STATE: nothing in this run could have stopped the CPU — StopModules -> StopAll is",
                "  on the deny list of the NORMAL policy and was never answered. (--disruptive was not given.)",
            };
        }

        var lines = new List<string>
        {
            "==== CPU RUN STATE — READ THIS BEFORE WALKING AWAY ============================",
            stopped is null
                ? "  StopModules -> StopAll was NOT answered on this run, so this tool did not stop the CPU."
                : "  *** StopModules -> StopAll WAS ANSWERED. THIS RUN ASKED FOR THE CPU TO BE STOPPED. ***",
        };

        if (start is null)
        {
            lines.Add("  *** StartModules WAS NEVER RAISED BY THE API ON THIS RUN. ***");
            lines.Add("  *** THERE WAS THEREFORE NO OPPORTUNITY TO ASK FOR A RESTART, AND THIS TOOL HAS NO  ***");
            lines.Add("  *** OTHER ROUTE TO RUN. THE CPU MAY BE LEFT IN STOP AND A HUMAN MUST RESTART IT.   ***");
            lines.Add("      IS7Client exposes no mode change. OnlineProvider is surveyed and reported by this");
            lines.Add("      tool but never used — nothing here changes an operating mode. Restart it in TIA");
            lines.Add("      Portal, or at the CPU, and confirm the mode before relying on the rig.");
            return lines;
        }

        if (string.Equals(start.ChosenSelection, NoActionFirstPolicy.StartModulesSelection, StringComparison.Ordinal))
        {
            lines.Add($"  StartModules was raised and answered '{start.ChosenSelection}' — TIA WAS ASKED to start");
            lines.Add("  the modules after loading, and accepted the request.");
            lines.Add("  *** THAT IS A REQUEST, NOT A READING. *** Nothing in this tool asks the controller what");
            lines.Add("  mode it is in, so confirm RUN at the device or in TIA Portal before relying on the rig.");
            return lines;
        }

        lines.Add($"  *** StartModules WAS RAISED AND WAS NOT ANSWERED WITH '{NoActionFirstPolicy.StartModulesSelection}'. ***");
        lines.Add($"      what happened instead : {start.Outcome} (policy said {start.Decision})");
        lines.Add($"      why                   : {start.WhyUnanswered}");
        lines.Add("  *** SO NOTHING ASKED FOR THE MODULES TO BE STARTED. THE CPU MAY BE LEFT IN STOP AND A ***");
        lines.Add("  *** HUMAN MUST RESTART IT. ***");
        return lines;
    }
}
