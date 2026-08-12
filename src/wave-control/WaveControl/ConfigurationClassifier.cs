using System;
using System.Collections.Generic;
using System.Linq;

namespace Ladder.Wave
{
    /// <summary>
    /// D32 caveat 3 — classify an unhandled download configuration as A, B or C, and thereby decide
    /// whether the escalation ladder's step 6 (a disruptive full download) is progress or a wasted
    /// CPU stop.
    /// </summary>
    /// <remarks>
    /// <para>
    /// *** THE UNKNOWN BRANCH IS THE POINT OF THIS TYPE, NOT AN EDGE CASE. *** Each of the last three
    /// real downloads produced a configuration nobody predicted, so an unrecognised name is the
    /// EXPECTED input, not the exceptional one. A classifier that quietly treated an unrecognised
    /// configuration as Class A would escalate to a disruptive download against something that might
    /// destroy data — which is why:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>
    ///     Class C is <see cref="ConfigurationClass.Unknown"/> = 0, so every default, every zeroed
    ///     field and every dropped value lands on the branch that fetches a human.
    ///   </description></item>
    ///   <item><description>
    ///     There is ONE fallthrough and it returns Class C. Class A and Class B are reached only by a
    ///     POSITIVE match against a table entry; the method has no "otherwise it is probably fine".
    ///   </description></item>
    ///   <item><description>
    ///     The deny list is consulted FIRST. If a configuration somehow appeared on both lists, the
    ///     answer would be "never permitted", not "escalate".
    ///   </description></item>
    ///   <item><description>
    ///     A recognised configuration in an UNRECOGNISED SHAPE — an offered selection no entry
    ///     accounts for — is demoted to Class C rather than answered. The entailment argument is made
    ///     per selection; a selection nobody characterised has no argument behind it.
    ///   </description></item>
    ///   <item><description>
    ///     Adding a Class A entry is gated by <see cref="ClassAEntry"/>'s constructor, which refuses
    ///     an entry that does not name the download option, the entailment and the evidence.
    ///   </description></item>
    /// </list>
    /// <para>
    /// This type answers ONE question and takes no action. It does not download, does not throw the
    /// delegate abort (D32 step 1), does not talk to Openness and holds no state. What the caller
    /// does with a verdict — and, crucially, what it RECORDS — is <see cref="EscalationRecord"/>.
    /// </para>
    /// </remarks>
    public static class ConfigurationClassifier
    {
        /// <summary>
        /// CLASS A — the allowance list. Three entries, and D32 names exactly these three. Every one
        /// carries the download option that already entails it, the entailment, and the evidence,
        /// because <see cref="ClassAEntry"/>'s constructor will not build one that does not.
        /// </summary>
        public static readonly IReadOnlyList<ClassAEntry> AllowanceList = new[]
        {
            new ClassAEntry(
                configurationName: "StopModules",
                answeredSelection: "StopAll",
                recognisedSelections: new[] { "NoAction", "StopAll" },
                entailedBy: DownloadOption.Software | DownloadOption.Hardware,
                entailment:
                    "a full Software or Hardware download loads the whole program and the CPU must be " +
                    "stopped to accept it; TIA rejects NoAction on this configuration outright, so the " +
                    "stop is not a consequence of answering but of choosing the option",
                evidence: "[M] 2026-08-12 spec R8/O3 — full and hardware downloads both raised StopModules and both rejected NoAction"),

            new ClassAEntry(
                configurationName: "DataBlockReinitialization",
                answeredSelection: "StopPlcAndReinitialize",
                recognisedSelections: new[] { "NoAction", "StopPlcAndReinitialize" },
                entailedBy: DownloadOption.Software,
                entailment:
                    "a full Software download resets data values including retentive ones anyway, so " +
                    "reinitialising the restructured data blocks costs nothing the chosen option had " +
                    "not already spent",
                evidence: "[R] spec R4/R8 — Software (all) resets values including retentive; [M] 2026-08-12 the configuration is raised when a download restructures a DB"),

            new ClassAEntry(
                configurationName: "StartModules",
                answeredSelection: "StartModule",
                recognisedSelections: new[] { "NoAction", "StartModule" },
                entailedBy: DownloadOption.Software | DownloadOption.Hardware,
                entailment:
                    "it is raised in the POST delegate only when the download stopped the modules, so " +
                    "it is the return leg of a stop the option already entailed; leaving it unanswered " +
                    "would stop the CPU and then decline to start it again",
                evidence: "[M] 2026-08-12 spec O3 — StartModule applied and read back; device reported 'stopped.' then 'started.', no human required"),
        };

        /// <summary>
        /// CLASS B — the deny list. Exactly two entries, keyed on the (configuration, selection) PAIR.
        /// </summary>
        /// <remarks>
        /// <c>StopAll</c> is NOT on this list, and the spec records three passages that wrongly said
        /// it was. It is Class A: refused in the wave loop, ANSWERED at a disruptive boundary. Adding
        /// it here would make R8's sanctioned disruptive mode impossible and D25 unimplementable.
        /// </remarks>
        public static readonly IReadOnlyList<ClassBEntry> DenyList = new[]
        {
            new ClassBEntry(
                configurationName: "ResetModule",
                deniedSelection: "DeleteAll",
                destroysBeyond: "deletes the module's contents wholesale, which no download option asks for",
                evidence: "[R] spec §13 / D32 Class B — must stay denied even in the disruptive mode"),

            new ClassBEntry(
                configurationName: "InitializeMemory",
                deniedSelection: "AcceptAll",
                destroysBeyond: "initialises memory beyond the data the chosen download would have rewritten",
                evidence: "[R] spec §13 / D32 Class B — must stay denied even in the disruptive mode"),
        };

        /// <summary>
        /// Classify one raised configuration. Never throws, never returns null, and returns Class C
        /// for everything it does not positively recognise.
        /// </summary>
        public static ConfigurationVerdict Classify(RaisedConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (configuration.Name.Length == 0)
            {
                return ConfigurationVerdict.ClassC(
                    configuration,
                    Ladder.Wave.UnknownReason.NoConfigurationName,
                    "The configuration carried no name, so it cannot be looked up on either list.");
            }

            // --- CLASS B FIRST. -------------------------------------------------------------
            // Deny beats allowance. If a name ever appeared on both lists the answer must be
            // "never permitted", not "escalate", and ordering is the cheapest way to guarantee it.
            var denyEntriesForName = DenyList
                .Where(e => ClassAEntry.NameComparer.Equals(e.ConfigurationName, configuration.Name))
                .ToList();

            if (denyEntriesForName.Count > 0)
            {
                var matched = denyEntriesForName
                    .FirstOrDefault(e => configuration.OfferedSelections.Contains(e.DeniedSelection, ClassAEntry.NameComparer));

                if (matched != null)
                {
                    return ConfigurationVerdict.ClassB(configuration, matched);
                }

                // On the deny list, but not offering the selection that put it there. Not evidence
                // that it became safe — evidence that this is a shape nobody has characterised.
                return ConfigurationVerdict.ClassC(
                    configuration,
                    Ladder.Wave.UnknownReason.DeniedConfigurationInAnUnrecognisedShape,
                    "'" + configuration.Name + "' is on the deny list but did not offer the denied selection (" +
                    string.Join(", ", denyEntriesForName.Select(e => e.DeniedSelection).ToArray()) +
                    "), so this is a shape nobody has characterised.");
            }

            // --- CLASS A, AND ONLY ON A POSITIVE MATCH OF NAME *AND* SHAPE. -------------------
            var allowed = AllowanceList
                .FirstOrDefault(e => ClassAEntry.NameComparer.Equals(e.ConfigurationName, configuration.Name));

            if (allowed != null)
            {
                if (allowed.RecognisesShape(configuration.OfferedSelections))
                {
                    return ConfigurationVerdict.ClassA(configuration, allowed);
                }

                var unrecognised = configuration.OfferedSelections
                    .Where(s => !allowed.RecognisedSelections.Contains(s, ClassAEntry.NameComparer))
                    .ToArray();

                return ConfigurationVerdict.ClassC(
                    configuration,
                    Ladder.Wave.UnknownReason.AllowedConfigurationInAnUnrecognisedShape,
                    "'" + configuration.Name + "' is on the allowance list but offered selection(s) no entry " +
                    "accounts for (" + string.Join(", ", unrecognised) + "). The entailment is claimed per " +
                    "SELECTION, so an unclaimed selection has no argument behind it.");
            }

            // --- THE ONE FALLTHROUGH, AND IT IS CLASS C. -------------------------------------
            // "Unknown, or no NoAction and not on either list." Both collapse here: anything not
            // positively matched above goes to a human. Do not add an "otherwise" branch below this.
            return ConfigurationVerdict.ClassC(
                configuration,
                Ladder.Wave.UnknownReason.OnNeitherList,
                "'" + configuration.Name + "' is on neither the allowance list nor the deny list" +
                (configuration.OffersNoAction ? "." : " and offered no NoAction selection."));
        }

        /// <summary>
        /// Convenience for the common call shape — a name and its offered selections.
        /// </summary>
        public static ConfigurationVerdict Classify(string? name, params string[] offeredSelections) =>
            Classify(new RaisedConfiguration(name, offeredSelections));
    }
}
