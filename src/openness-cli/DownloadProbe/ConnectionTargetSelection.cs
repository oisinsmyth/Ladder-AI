using System;
using System.Collections.Generic;
using System.Linq;

namespace DownloadProbe;

/// <summary>
/// One download target exactly as the project's own <c>ConnectionConfiguration</c> declares it:
/// a mode, a PC interface, and one of that interface's target interfaces.
///
/// Generic in the node type on purpose. In the program the node is the live
/// <c>ConfigurationTargetInterface</c> — which is <c>Siemens.Engineering.Connection.IConfiguration</c>,
/// measured by reflection on the installed V20 assembly, and therefore acceptable to the four-argument
/// <c>DownloadProvider.Download</c> overload directly. Every Siemens type here is sealed with no
/// public constructor, so a test could not build one; the generic parameter lets the selection rules
/// — the part that decides WHICH DEVICE GETS WRITTEN TO — be exercised with a stand-in node instead
/// of being the one part of this tool that is only ever proved by running it at a controller.
/// </summary>
internal sealed class ConnectionTarget<TNode>
{
    internal ConnectionTarget(
        string modeName,
        string pcInterfaceName,
        string targetInterfaceName,
        IReadOnlyList<string> configuredAddresses,
        string nodeTypeName,
        TNode node)
    {
        ModeName = modeName;
        PcInterfaceName = pcInterfaceName;
        TargetInterfaceName = targetInterfaceName;
        ConfiguredAddresses = configuredAddresses;
        NodeTypeName = nodeTypeName;
        Node = node;
    }

    internal string ModeName { get; }

    /// <summary>The PC-side adapter. This is what <c>--pc-interface</c> names, exactly.</summary>
    internal string PcInterfaceName { get; }

    internal string TargetInterfaceName { get; }

    /// <summary>
    /// The addresses this target interface declares, which on the project this tool was built for is
    /// EMPTY — the human downloads through TIA's "Extended download to device" dialog, and that scan
    /// result never lands in the project model. Recorded because an empty list is a finding, not a
    /// blank: it is the reason an address-keyed selection could not work here.
    /// </summary>
    internal IReadOnlyList<string> ConfiguredAddresses { get; }

    /// <summary>The CLR type actually handed to <c>Download</c>. Logged, so the route taken is visible.</summary>
    internal string NodeTypeName { get; }

    internal TNode Node { get; }

    internal string Label =>
        $"{ModeName} / {PcInterfaceName} / {TargetInterfaceName}" +
        (ConfiguredAddresses.Count == 0
            ? " [no configured address]"
            : $" [{string.Join(", ", ConfiguredAddresses)}]");
}

/// <summary>Either one target, or a refusal that names every candidate it was choosing between.</summary>
internal sealed class TargetSelection<TNode>
{
    private TargetSelection(ConnectionTarget<TNode>? chosen, IReadOnlyList<string> refusalLines)
    {
        Chosen = chosen;
        RefusalLines = refusalLines;
    }

    internal ConnectionTarget<TNode>? Chosen { get; }

    internal IReadOnlyList<string> RefusalLines { get; }

    internal bool IsRefusal => Chosen is null;

    internal static TargetSelection<TNode> Take(ConnectionTarget<TNode> chosen) =>
        new(chosen, Array.Empty<string>());

    internal static TargetSelection<TNode> Refuse(IEnumerable<string> lines) =>
        new(null, lines.ToList());
}

/// <summary>
/// Picks the download target, or refuses.
///
/// THE ONE RULE THIS TYPE EXISTS TO HOLD: it never picks a device on the user's behalf. The probe's
/// first live run refused at "NO CONNECTION TARGET" rather than guess, and that refusal is the
/// property being preserved here, not worked around — the candidate list on the machine this runs on
/// includes a PLCSIM virtual adapter, so a helpful default or a substring match could silently send a
/// download to a simulator, or to whichever adapter happened to be enumerated first.
///
/// So: <c>--pc-interface</c> matches by EXACT, ORDINAL name; it is REQUIRED whenever the project
/// declares more than one PC interface; and zero matches or several matches are both hard refusals
/// that print every candidate.
/// </summary>
internal static class ConnectionTargetSelector
{
    /// <summary>
    /// Ordinal, case-sensitive, whole-string. Not <c>OrdinalIgnoreCase</c> and not <c>Contains</c>:
    /// "Adapter" is a substring of all three adapters on the reference machine, and an adapter name
    /// differing only in case is still a different name that this tool has no business guessing at.
    /// </summary>
    internal const StringComparison NameComparison = StringComparison.Ordinal;

    internal static TargetSelection<TNode> Select<TNode>(
        IReadOnlyList<ConnectionTarget<TNode>> candidates,
        string? requestedPcInterface,
        string? requestedTargetInterface)
    {
        if (candidates.Count == 0)
        {
            return TargetSelection<TNode>.Refuse(new[]
            {
                "NO CONNECTION TARGET: the project's ConnectionConfiguration declares no mode /",
                "PC interface / target interface at all, so there is nothing to download through.",
                "Nothing was guessed and no scan was performed.",
            });
        }

        var pcInterfaceNames = candidates
            .Select(c => c.PcInterfaceName)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (requestedPcInterface is null)
        {
            if (pcInterfaceNames.Count > 1)
            {
                return TargetSelection<TNode>.Refuse(
                    new[]
                    {
                        $"--pc-interface IS REQUIRED: this project declares {pcInterfaceNames.Count} PC interfaces.",
                        "There is no default and no first-one-wins. Picking the wrong adapter means writing to",
                        "the wrong device — and one of the candidates below may be a simulator.",
                        string.Empty,
                        "Candidates (exact names, quote them verbatim):",
                    }
                    .Concat(DescribeCandidates(candidates)));
            }

            // Exactly one PC interface exists, so naming it would add nothing: there is no choice to
            // make and therefore no choice being made on the user's behalf.
        }
        else
        {
            var matched = candidates.Where(c => string.Equals(c.PcInterfaceName, requestedPcInterface, NameComparison)).ToList();

            if (matched.Count == 0)
            {
                var lines = new List<string>
                {
                    $"NO PC INTERFACE NAMED '{requestedPcInterface}'.",
                    "Matching is exact and case-sensitive, on the whole name — a substring or a differently",
                    "cased spelling is refused rather than resolved to whichever adapter it is nearest.",
                };

                var nearMisses = candidates
                    .Where(c => string.Equals(c.PcInterfaceName, requestedPcInterface, StringComparison.OrdinalIgnoreCase))
                    .Select(c => c.PcInterfaceName)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                if (nearMisses.Count > 0)
                {
                    lines.Add(string.Empty);
                    lines.Add("Differs only in case from: " + string.Join(", ", nearMisses.Select(n => $"'{n}'")));
                    lines.Add("Re-run with that exact spelling if it is the one you mean.");
                }

                lines.Add(string.Empty);
                lines.Add("Candidates (exact names, quote them verbatim):");
                return TargetSelection<TNode>.Refuse(lines.Concat(DescribeCandidates(candidates)));
            }

            var matchedPcNames = matched.Select(c => c.PcInterfaceName).Distinct(StringComparer.Ordinal).ToList();
            if (matchedPcNames.Count > 1)
            {
                // Unreachable through an exact-name filter unless the project genuinely declares the
                // same adapter name twice. Kept because "cannot happen" is not a reason to pick one.
                return TargetSelection<TNode>.Refuse(
                    new[]
                    {
                        $"AMBIGUOUS: '{requestedPcInterface}' matched {matchedPcNames.Count} distinct PC interfaces.",
                        string.Empty,
                        "Candidates:",
                    }
                    .Concat(DescribeCandidates(candidates)));
            }

            candidates = matched;
        }

        var pcInterfaceName = candidates[0].PcInterfaceName;

        if (candidates.Count == 1 && requestedTargetInterface is null)
        {
            return TargetSelection<TNode>.Take(candidates[0]);
        }

        if (requestedTargetInterface is null)
        {
            return TargetSelection<TNode>.Refuse(
                new[]
                {
                    $"--target IS REQUIRED: PC interface '{pcInterfaceName}' has {candidates.Count} target interfaces.",
                    "Name one exactly. Nothing is chosen for you at this level either.",
                    string.Empty,
                    "Candidates:",
                }
                .Concat(DescribeCandidates(candidates)));
        }

        var targets = candidates
            .Where(c => string.Equals(c.TargetInterfaceName, requestedTargetInterface, NameComparison))
            .ToList();

        if (targets.Count == 1)
        {
            return TargetSelection<TNode>.Take(targets[0]);
        }

        return TargetSelection<TNode>.Refuse(
            new[]
            {
                targets.Count == 0
                    ? $"NO TARGET INTERFACE NAMED '{requestedTargetInterface}' under PC interface '{pcInterfaceName}'."
                    : $"AMBIGUOUS: '{requestedTargetInterface}' matched {targets.Count} target interfaces under '{pcInterfaceName}'.",
                "Matching is exact and case-sensitive, on the whole name.",
                string.Empty,
                "Candidates:",
            }
            .Concat(DescribeCandidates(candidates)));
    }

    internal static IEnumerable<string> DescribeCandidates<TNode>(IReadOnlyList<ConnectionTarget<TNode>> candidates) =>
        candidates.Select(c => "  - " + c.Label);
}

/// <summary>
/// Resolve the target, log what was resolved, and only then hand it to the caller's download call.
///
/// The download call itself is a parameter so that this — everything that decides WHAT GETS WRITTEN
/// TO — runs in a test with a fake in place of <c>DownloadProvider</c>, which cannot be constructed
/// outside Portal. A refusal returns without the delegate ever being invoked, and that is asserted.
/// </summary>
internal static class DownloadDispatch
{
    internal static ProbeOutcome Dispatch<TNode>(
        IReadOnlyList<ConnectionTarget<TNode>> candidates,
        string? requestedPcInterface,
        string? requestedTargetInterface,
        ProbeLog log,
        Func<ConnectionTarget<TNode>, ProbeOutcome> invoke)
    {
        log.Line($"candidate targets from the project's own configuration ({candidates.Count}):");
        foreach (var line in ConnectionTargetSelector.DescribeCandidates(candidates))
        {
            log.Line(line);
        }

        log.Line("  (read from the project model. GetAccessibleDevices() — the live scan — is never called,");
        log.Line("   and ApplyConfiguration() is never called: this tool does not change what it targets.)");

        var selection = ConnectionTargetSelector.Select(candidates, requestedPcInterface, requestedTargetInterface);
        if (selection.Chosen is null)
        {
            log.Blank();
            log.Block(selection.RefusalLines);
            log.Blank();
            log.Line("Refusing rather than guessing — picking the wrong one means writing to the wrong device,");
            log.Line("which no amount of re-reading undoes. Nothing was downloaded.");
            return new ProbeOutcome(
                ProbeExitCodes.NoDownloadTarget,
                "No unambiguous connection target in the project's own configuration.");
        }

        var chosen = selection.Chosen;
        log.Blank();
        log.Line("chosen target (selected, never created — no configuration was applied):");
        log.Line($"  mode             : {chosen.ModeName}");
        log.Line($"  pc interface     : {chosen.PcInterfaceName}");
        log.Line($"  target interface : {chosen.TargetInterfaceName}");
        log.Line($"  configured addrs : {(chosen.ConfiguredAddresses.Count == 0 ? "(none — and none is needed for this overload)" : string.Join(", ", chosen.ConfiguredAddresses))}");
        log.Line($"  passed as        : {chosen.NodeTypeName}");
        log.Line("  overload         : Download(IConfiguration, pre, post, DownloadOptions) — the four-argument");
        log.Line("                     one, which takes no ConfigurationAddress. The target interface IS an");
        log.Line("                     IConfiguration, so the empty Addresses collection is not in the way.");

        return invoke(chosen);
    }
}
