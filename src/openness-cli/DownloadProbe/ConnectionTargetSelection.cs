using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// <summary>
    /// The <c>Number</c> stood in for when <c>ConfigurationPcInterface.Number</c> could not be read.
    /// Deliberately a value no real ordinal can take, so it can never accidentally MATCH a requested
    /// number — an unreadable number narrows nothing and the ambiguity refusal still fires.
    /// </summary>
    internal const int UnknownPcInterfaceNumber = int.MinValue;

    internal ConnectionTarget(
        string modeName,
        string pcInterfaceName,
        int pcInterfaceNumber,
        string targetInterfaceName,
        IReadOnlyList<string> configuredAddresses,
        string nodeTypeName,
        TNode node)
    {
        ModeName = modeName;
        PcInterfaceName = pcInterfaceName;
        PcInterfaceNumber = pcInterfaceNumber;
        TargetInterfaceName = targetInterfaceName;
        ConfiguredAddresses = configuredAddresses;
        NodeTypeName = nodeTypeName;
        Node = node;
    }

    internal string ModeName { get; }

    /// <summary>The PC-side adapter's <c>Name</c>. Note that this is the name ALONE.</summary>
    internal string PcInterfaceName { get; }

    /// <summary>
    /// <c>ConfigurationPcInterface.Number</c> (<c>System.Int32</c>, read-only, declared on the type
    /// itself — measured by reflection on the installed V20 assembly). MEASURED 2026-08-12: two
    /// Hyper-V adapters on one machine share a <c>Name</c> BYTE FOR BYTE and differ only here, so the
    /// name alone does not identify an adapter and matching on it pooled two adapters that can reach
    /// different networks. It is not part of <c>Name</c>: the <c>#N</c> in
    /// <c>openness-cli download-plan</c>'s output is this property, rendered.
    /// </summary>
    internal int PcInterfaceNumber { get; }

    /// <summary>
    /// The adapter as <c>--pc-interface</c> must spell it when the number is needed — the same
    /// <c>Name #Number</c> form <c>download-plan</c> prints, so a refusal is copy-pasteable.
    /// </summary>
    internal string PcInterfaceLabel =>
        PcInterfaceName + " #" + (PcInterfaceNumber == UnknownPcInterfaceNumber
            ? "(unreadable)"
            : PcInterfaceNumber.ToString(CultureInfo.InvariantCulture));

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
        $"{ModeName} / {PcInterfaceLabel} / {TargetInterfaceName}" +
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
///
/// SINCE 2026-08-12 the name alone may not identify an adapter. Two Hyper-V adapters on the machine
/// this runs on share a <c>Name</c> byte for byte and differ only in <c>Number</c>, so a name-only
/// match POOLED two adapters that can reach different networks and the run refused at
/// "--target IS REQUIRED ... has 2 target interfaces" — the right refusal for the wrong reason, and
/// with no way for the operator to say which one. So <c>--pc-interface</c> also accepts the
/// <c>Name #Number</c> form <c>openness-cli download-plan</c> already prints. The exactness rule is
/// untouched: the name part is still matched whole, ordinal and case-sensitive, and a number that
/// names no adapter is a refusal rather than a quiet fall back to matching on the name alone.
/// </summary>
internal static class ConnectionTargetSelector
{
    /// <summary>
    /// Ordinal, case-sensitive, whole-string. Not <c>OrdinalIgnoreCase</c> and not <c>Contains</c>:
    /// "Adapter" is a substring of all three adapters on the reference machine, and an adapter name
    /// differing only in case is still a different name that this tool has no business guessing at.
    /// </summary>
    internal const StringComparison NameComparison = StringComparison.Ordinal;

    /// <summary>
    /// THE PARSE RULE, stated once: a <c>--pc-interface</c> value is taken WHOLE as a
    /// <c>ConfigurationPcInterface.Name</c> first, and only if that matches NOTHING is a trailing
    /// <c>" #&lt;digits&gt;"</c> — a space, a hash, one or more ASCII digits, and then the end of the
    /// string, with at least one character before the space — re-read as a <c>Number</c>.
    ///
    /// Whole-string-first is what keeps a name that legitimately contains a <c>#</c> unmangled: such a
    /// name matches on the first attempt and is never split. It also makes the suffix additive rather
    /// than a reinterpretation — every value that resolved before this change still resolves to the
    /// same adapter.
    /// </summary>
    internal static bool TrySplitTrailingNumber(string requested, out string namePart, out int number)
    {
        namePart = requested;
        number = 0;

        // LastIndexOf, so "Adapter #2 #3" splits at the LAST separator: name "Adapter #2", number 3.
        // That is the escape hatch if an adapter is ever genuinely named "... #2".
        var separator = requested.LastIndexOf(" #", StringComparison.Ordinal);
        if (separator <= 0)
        {
            // Absent, or with nothing before it — " #2" names no adapter, so it is not a suffix.
            return false;
        }

        var digits = requested.Substring(separator + 2);
        if (digits.Length == 0 || digits.Any(c => c < '0' || c > '9'))
        {
            // Explicit digits-only: NumberStyles.None would already refuse a sign or a space, but the
            // rule this documents is "digits to the end of the string", not "whatever int can parse".
            return false;
        }

        if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out number))
        {
            return false;
        }

        namePart = requested.Substring(0, separator);
        return true;
    }

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

        // Keyed on (Name, Number), never on Name alone: two adapters sharing a name are two adapters,
        // and counting them as one is exactly what pooled their target interfaces together.
        var pcInterfaces = DistinctPcInterfaces(candidates);

        if (requestedPcInterface is null)
        {
            if (pcInterfaces.Count > 1)
            {
                return TargetSelection<TNode>.Refuse(
                    new[]
                    {
                        $"--pc-interface IS REQUIRED: this project declares {pcInterfaces.Count} PC interfaces.",
                        "There is no default and no first-one-wins. Picking the wrong adapter means writing to",
                        "the wrong device — and one of the candidates below may be a simulator.",
                        string.Empty,
                        "Pass exactly one of these, quoted verbatim (the trailing ' #<n>' is the adapter's",
                        "Number and is only needed when two of them share a name):",
                    }
                    .Concat(DescribePcInterfaceChoices(candidates))
                    .Concat(new[] { string.Empty, "Candidates:" })
                    .Concat(DescribeCandidates(candidates)));
            }

            // Exactly one PC interface exists, so naming it would add nothing: there is no choice to
            // make and therefore no choice being made on the user's behalf.
        }
        else
        {
            List<ConnectionTarget<TNode>> matched;

            var byWholeName = candidates
                .Where(c => string.Equals(c.PcInterfaceName, requestedPcInterface, NameComparison))
                .ToList();

            if (byWholeName.Count > 0)
            {
                matched = byWholeName;
            }
            else if (TrySplitTrailingNumber(requestedPcInterface, out var namePart, out var requestedNumber))
            {
                var byName = candidates
                    .Where(c => string.Equals(c.PcInterfaceName, namePart, NameComparison))
                    .ToList();

                matched = byName.Where(c => c.PcInterfaceNumber == requestedNumber).ToList();

                if (matched.Count == 0)
                {
                    return TargetSelection<TNode>.Refuse(
                        byName.Count > 0
                            ? NoSuchNumber(namePart, requestedNumber, byName, candidates)
                            : NoSuchName(requestedPcInterface, namePart, candidates));
                }
            }
            else
            {
                matched = new List<ConnectionTarget<TNode>>();
            }

            if (matched.Count == 0)
            {
                return TargetSelection<TNode>.Refuse(NoSuchName(requestedPcInterface, null, candidates));
            }

            var matchedInterfaces = DistinctPcInterfaces(matched);
            if (matchedInterfaces.Count > 1)
            {
                // Reachable, and measured: two Hyper-V adapters with the same Name. The number is what
                // separates them, so the refusal prints it and says to pass it.
                return TargetSelection<TNode>.Refuse(
                    new[]
                    {
                        $"AMBIGUOUS: '{requestedPcInterface}' matched {matchedInterfaces.Count} PC interfaces that share that name.",
                        "They are different adapters and may reach different networks, so nothing is picked for",
                        "you. Re-run naming the Number too, by appending ' #<n>' exactly as printed here:",
                        string.Empty,
                    }
                    .Concat(DescribePcInterfaceChoices(matched))
                    .Concat(new[] { string.Empty, "Candidates:" })
                    .Concat(DescribeCandidates(matched)));
            }

            candidates = matched;
        }

        var pcInterfaceName = candidates[0].PcInterfaceLabel;

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

    /// <summary>
    /// The distinct PC interfaces, as the strings <c>--pc-interface</c> would take. A refusal that
    /// lists these tells the reader exactly what to pass; the old one listed bare names, which on two
    /// same-named adapters told them to pass the very string that had just been refused.
    /// </summary>
    internal static IEnumerable<string> DescribePcInterfaceChoices<TNode>(
        IReadOnlyList<ConnectionTarget<TNode>> candidates) =>
        DistinctPcInterfaces(candidates).Select(label => $"  - \"{label}\"");

    private static IReadOnlyList<string> DistinctPcInterfaces<TNode>(
        IReadOnlyList<ConnectionTarget<TNode>> candidates) =>
        candidates
            .Select(c => c.PcInterfaceLabel)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static IEnumerable<string> NoSuchName<TNode>(
        string requested,
        string? namePart,
        IReadOnlyList<ConnectionTarget<TNode>> candidates)
    {
        var lines = new List<string>
        {
            $"NO PC INTERFACE NAMED '{requested}'.",
            "Matching is exact and case-sensitive, on the whole name — a substring or a differently",
            "cased spelling is refused rather than resolved to whichever adapter it is nearest.",
        };

        // Near misses are looked for against BOTH readings of the value: the whole string, and the
        // name part left after a trailing ' #<n>'. Otherwise a case-wrong name carrying a correct
        // number would get no hint at all, which is the case an operator is most likely to hit.
        var nearMisses = candidates
            .Where(c =>
                string.Equals(c.PcInterfaceName, requested, StringComparison.OrdinalIgnoreCase) ||
                (namePart is not null &&
                 string.Equals(c.PcInterfaceName, namePart, StringComparison.OrdinalIgnoreCase)))
            .Select(c => c.PcInterfaceLabel)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (nearMisses.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Differs only in case from: " + string.Join(", ", nearMisses.Select(n => $"'{n}'")));
            lines.Add("Re-run with that exact spelling if it is the one you mean.");
        }

        lines.Add(string.Empty);
        lines.Add("Pass exactly one of these, quoted verbatim:");
        lines.AddRange(DescribePcInterfaceChoices(candidates));
        lines.Add(string.Empty);
        lines.Add("Candidates:");
        return lines.Concat(DescribeCandidates(candidates));
    }

    /// <summary>
    /// The name matched and the number did not. A HARD REFUSAL, never a fall back to matching on the
    /// name alone: falling back is precisely what pooled two adapters, and doing it after the operator
    /// took the trouble to name a number would silently ignore the one thing they said.
    /// </summary>
    private static IEnumerable<string> NoSuchNumber<TNode>(
        string namePart,
        int requestedNumber,
        IReadOnlyList<ConnectionTarget<TNode>> sameName,
        IReadOnlyList<ConnectionTarget<TNode>> candidates)
    {
        var lines = new List<string>
        {
            $"NO PC INTERFACE '{namePart}' WITH NUMBER #{requestedNumber.ToString(CultureInfo.InvariantCulture)}.",
            "The name matched; the number did not. This is refused rather than resolved by name alone —",
            "a name-only match is what pooled two different adapters together in the first place.",
            string.Empty,
            $"'{namePart}' exists with these numbers:",
        };

        lines.AddRange(DescribePcInterfaceChoices(sameName));
        lines.Add(string.Empty);
        lines.Add("Candidates:");
        return lines.Concat(DescribeCandidates(candidates));
    }
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
        log.Line($"  pc interface     : {chosen.PcInterfaceLabel}");
        log.Line($"  target interface : {chosen.TargetInterfaceName}");
        log.Line($"  configured addrs : {(chosen.ConfiguredAddresses.Count == 0 ? "(none — and none is needed for this overload)" : string.Join(", ", chosen.ConfiguredAddresses))}");
        log.Line($"  passed as        : {chosen.NodeTypeName}");
        log.Line("  overload         : Download(IConfiguration, pre, post, DownloadOptions) — the four-argument");
        log.Line("                     one, which takes no ConfigurationAddress. The target interface IS an");
        log.Line("                     IConfiguration, so the empty Addresses collection is not in the way.");

        return invoke(chosen);
    }
}
