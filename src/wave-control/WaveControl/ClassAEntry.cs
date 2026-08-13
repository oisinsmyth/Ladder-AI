using System;
using System.Collections.Generic;
using System.Linq;

namespace Ladder.Wave
{
    /// <summary>
    /// One entry on the D32 CLASS A allowance list — a configuration refused ONLY because the wave
    /// loop runs in a non-disruptive mode, which a disruptive full download can therefore ANSWER.
    /// </summary>
    /// <remarks>
    /// <para>
    /// *** THIS TYPE EXISTS TO MAKE IT IMPOSSIBLE TO ADD A CLASS A ENTRY WITHOUT NAMING WHAT THE
    /// DOWNLOAD OPTION ENTAILS. *** Class A is the only class that spends a CPU stop, so an entry
    /// added here on a hunch is the one mistake in this component that costs something real. The
    /// constructor therefore refuses an entry that does not carry, all four:
    /// </para>
    /// <list type="number">
    ///   <item><description>
    ///     <see cref="EntailedBy"/> — WHICH download option already entails it. Class A's definition
    ///     is "a consequence the chosen download option ALREADY entails"; without an option named,
    ///     there is no claim, only a preference.
    ///   </description></item>
    ///   <item><description>
    ///     <see cref="Entailment"/> — the argument in words, long enough to be an argument. A blank,
    ///     a placeholder or a one-word "yes" is rejected outright.
    ///   </description></item>
    ///   <item><description>
    ///     <see cref="Evidence"/> — where the claim comes from, measured [M] or researched [R]. The
    ///     spec's own audit found three passages citing a deny list that did not exist; an entry
    ///     that cannot say where it came from is the same failure waiting to happen.
    ///   </description></item>
    ///   <item><description>
    ///     <see cref="AnsweredSelection"/> and <see cref="RecognisedSelections"/> — the entailment is
    ///     claimed PER SELECTION, not per configuration name. A recognised configuration offering an
    ///     unrecognised selection is not this entry's configuration; the classifier demotes it to
    ///     Class C rather than answering it.
    ///   </description></item>
    /// </list>
    /// <para>
    /// There is no other constructor, no object initialiser path, and no setter. Every route to a
    /// Class A verdict runs through an instance of this type, so the checks above are the only gate
    /// there is and they cannot be walked around by a caller in a hurry.
    /// </para>
    /// </remarks>
    public sealed class ClassAEntry
    {
        /// <summary>
        /// The floor on <see cref="Entailment"/>. Arbitrary in the same way a lint rule is: its job
        /// is to stop "ok", "safe" and "n/a" from satisfying a field whose entire purpose is to make
        /// somebody write down the reasoning. Anything that genuinely names an option and what it
        /// entails clears it without trying.
        /// </summary>
        public const int MinimumEntailmentLength = 24;

        /// <summary>
        /// Matching is case-insensitive, matching the sibling <c>Ladder.Download</c> classifier and
        /// for the same reason: a change of NAME must fall through to Class C and be surfaced, and it
        /// still does. Only capitalisation is forgiven, because capitalisation is not semantics.
        /// </summary>
        internal static readonly StringComparer NameComparer = StringComparer.OrdinalIgnoreCase;

        /// <param name="configurationName">The Openness configuration type name, e.g. <c>StopModules</c>.</param>
        /// <param name="answeredSelection">
        /// The selection a DISRUPTIVE download answers for this configuration — the one whose
        /// consequence the option already entails.
        /// </param>
        /// <param name="recognisedSelections">
        /// Every selection this configuration has been SEEN to offer, including
        /// <paramref name="answeredSelection"/> and any inert one such as <c>NoAction</c>. An offered
        /// selection outside this set demotes the configuration to Class C.
        /// </param>
        /// <param name="entailedBy">Which download option(s) already entail the consequence.</param>
        /// <param name="entailment">What that option entails, and why answering adds nothing to it.</param>
        /// <param name="evidence">Where the claim comes from — [M] measured, [R] researched, plus a locator.</param>
        /// <param name="raisedIn">
        /// WHICH DELEGATE raises it. *** REQUIRED SINCE 2026-08-13, AND IT IS WHAT MAKES THE RUNG
        /// PER-ENTRY. *** D32 gives one rung per CLASS; that rung is true for a PRE-transfer abort
        /// (which leaves the CPU running) and false for a POST-transfer one (which leaves it stopped
        /// with a complete program). An entry that cannot say which delegate raises it cannot have a
        /// rung derived for it, and Class A is the only class that spends anything.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when any of the above is missing, blank, or (for <paramref name="entailment"/>)
        /// too short to be an argument. This exception IS the guard; do not soften it.
        /// </exception>
        public ClassAEntry(
            string configurationName,
            string answeredSelection,
            IEnumerable<string> recognisedSelections,
            DownloadOption entailedBy,
            string entailment,
            string evidence,
            DelegateStage raisedIn)
        {
            ConfigurationName = Required(configurationName, nameof(configurationName));
            AnsweredSelection = Required(answeredSelection, nameof(answeredSelection));

            if (entailedBy == DownloadOption.None)
            {
                throw new ArgumentException(
                    "A Class A entry must name WHICH download option already entails the configuration. " +
                    "Class A is defined by that entailment; without an option named there is no entry, " +
                    "only a preference for not being stopped. (" + configurationName + ")",
                    nameof(entailedBy));
            }

            var prose = (entailment ?? string.Empty).Trim();
            if (prose.Length < MinimumEntailmentLength)
            {
                throw new ArgumentException(
                    "A Class A entry must state WHAT the download option entails, in at least " +
                    MinimumEntailmentLength + " characters. Escalating to a disruptive download is " +
                    "the only rung of D32's ladder that spends a CPU stop, and an entry nobody had to " +
                    "justify is how something destructive gets onto the allowance list. (" +
                    configurationName + ")",
                    nameof(entailment));
            }

            Entailment = prose;
            Evidence = Required(evidence, nameof(evidence));

            var selections = (recognisedSelections ?? Enumerable.Empty<string>())
                .Select(s => (s ?? string.Empty).Trim())
                .ToList();

            if (selections.Any(s => s.Length == 0))
            {
                throw new ArgumentException(
                    "A Class A entry's recognised-selection list must not contain a blank name: a blank " +
                    "matches nothing and hides that the shape was never established. (" +
                    configurationName + ")",
                    nameof(recognisedSelections));
            }

            if (selections.Distinct(NameComparer).Count() != selections.Count)
            {
                throw new ArgumentException(
                    "A Class A entry's recognised-selection list contains a duplicate. (" +
                    configurationName + ")",
                    nameof(recognisedSelections));
            }

            if (!selections.Contains(AnsweredSelection, NameComparer))
            {
                throw new ArgumentException(
                    "A Class A entry's answered selection '" + AnsweredSelection +
                    "' must appear in its recognised-selection list, or the entry claims an entailment " +
                    "for a selection it has never seen offered. (" + configurationName + ")",
                    nameof(recognisedSelections));
            }

            if (raisedIn == DelegateStage.Unknown)
            {
                throw new ArgumentException(
                    "A Class A entry must name WHICH DELEGATE raises the configuration. Since the " +
                    "2026-08-13 measurement the rung is derived from it: a PRE-transfer abort leaves the " +
                    "CPU RUNNING and D32's 'spend a disruptive download' rung is sound, while a " +
                    "POST-transfer abort leaves it STOPPED with a complete program and that rung is " +
                    "false. An entry that will not say which cannot be given either. (" +
                    configurationName + ")",
                    nameof(raisedIn));
            }

            RecognisedSelections = selections.ToArray();
            EntailedBy = entailedBy;
            RaisedIn = raisedIn;
        }

        /// <summary>The Openness configuration type name, e.g. <c>StopModules</c>.</summary>
        public string ConfigurationName { get; }

        /// <summary>The selection a disruptive download answers for this configuration.</summary>
        public string AnsweredSelection { get; }

        /// <summary>Every selection this configuration has been seen to offer.</summary>
        public IReadOnlyList<string> RecognisedSelections { get; }

        /// <summary>Which download option(s) already entail the consequence.</summary>
        public DownloadOption EntailedBy { get; }

        /// <summary>What that option entails, and why answering the configuration adds nothing to it.</summary>
        public string Entailment { get; }

        /// <summary>Where the claim comes from — [M] measured, [R] researched, plus a locator.</summary>
        public string Evidence { get; }

        /// <summary>Which delegate raises it — the fact the per-entry rung is derived from.</summary>
        public DelegateStage RaisedIn { get; }

        /// <summary>What a policy throw on this configuration leaves the controller in [M 2026-08-13].</summary>
        public AbortAftermath Aftermath => AbortAftermathTable.For(RaisedIn);

        /// <summary>
        /// *** THE PER-ENTRY RUNG. *** D32 gives one rung per class; this derives one per ENTRY, from
        /// the stage that raises it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>PRE-transfer</b> — the abort leaves the CPU RUNNING and the project unchanged, so nothing
        /// has been spent and D32's step 6 is exactly right: a disruptive download ANSWERS the
        /// configuration rather than hoping it does not appear.
        /// </para>
        /// <para>
        /// <b>POST-transfer</b> — *** "GO TO STEP 6" IS CIRCULAR HERE. *** The configuration is raised
        /// after the transfer, which means the download already happened and already stopped the CPU;
        /// fetching a fresh disruptive download to answer it would be fetching the thing we are already
        /// inside. Refusing it does not call for a NEW download — it calls for answering it WITHIN THE
        /// CURRENT ONE, or the CPU is left stopped. That was flagged when the classifier was built and
        /// is now measured.
        /// </para>
        /// </remarks>
        public LadderRung Rung =>
            RaisedIn == DelegateStage.PostTransfer
                ? LadderRung.AnswerWithinTheCurrentDownload
                : LadderRung.Step6DisruptiveFullDownload;

        /// <summary>True when every offered selection is one this entry recognises.</summary>
        internal bool RecognisesShape(IReadOnlyList<string> offeredSelections)
        {
            for (var i = 0; i < offeredSelections.Count; i++)
            {
                if (!RecognisedSelections.Contains(offeredSelections[i], NameComparer))
                {
                    return false;
                }
            }

            return true;
        }

        private static string Required(string value, string parameterName)
        {
            var trimmed = (value ?? string.Empty).Trim();
            if (trimmed.Length == 0)
            {
                throw new ArgumentException(
                    "A Class A entry requires '" + parameterName + "'. An entry that cannot say this " +
                    "is not one that should be authorising a CPU stop.",
                    parameterName);
            }

            return trimmed;
        }
    }
}
