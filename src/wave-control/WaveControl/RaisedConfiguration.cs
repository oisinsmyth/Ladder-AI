using System.Collections.Generic;
using System.Linq;

namespace Ladder.Wave
{
    /// <summary>
    /// A configuration raised by Openness during a download, reduced to the three facts the D32
    /// classifier is allowed to reason from: its name, the selections it offered, and (for the log
    /// only) whichever selection was sitting on it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// *** <see cref="CurrentSelection"/> IS CARRIED FOR THE LOG AND IS NEVER AN INPUT TO THE
    /// CLASSIFICATION — R4a. *** The zero values differ IN DIRECTION between Siemens enums:
    /// <c>StopModulesSelections.NoAction = 0</c> is safe while
    /// <c>DataBlockReinitializationSelections.StopPlcAndReinitialize = 0</c> is destructive, so an
    /// unanswered configuration reads as SAFE on one and DESTRUCTIVE on the other. Any classifier
    /// that consulted the current selection would invert silently on the second one. It is recorded
    /// because a later reader wants to know what TIA had pre-selected, and for nothing else.
    /// </para>
    /// <para>
    /// This type is a plain DTO with no Openness dependency, the same seam
    /// <c>DownloadResultAdapter</c> uses in the sibling library: the caller projects the real
    /// <c>ConfigurationBase</c> into it, which is what lets these tests run on a machine with no TIA
    /// Portal and keeps a change here out of the (Path,FileHash) re-approval cycle.
    /// </para>
    /// </remarks>
    public sealed class RaisedConfiguration
    {
        /// <param name="name">The configuration type name as Openness reports it, e.g. <c>StopModules</c>.</param>
        /// <param name="offeredSelections">Every selection the configuration offered. Order is not significant.</param>
        /// <param name="currentSelection">Whatever selection was sitting on it. LOG ONLY — see the remarks.</param>
        public RaisedConfiguration(string? name, IEnumerable<string?>? offeredSelections = null, string? currentSelection = null)
        {
            Name = (name ?? string.Empty).Trim();
            OfferedSelections = (offeredSelections ?? Enumerable.Empty<string?>())
                .Select(s => (s ?? string.Empty).Trim())
                .Where(s => s.Length > 0)
                .ToArray();
            CurrentSelection = string.IsNullOrWhiteSpace(currentSelection) ? null : currentSelection!.Trim();
        }

        /// <summary>The configuration type name. May be empty — that is itself a Class C outcome.</summary>
        public string Name { get; }

        /// <summary>Every selection the configuration offered.</summary>
        public IReadOnlyList<string> OfferedSelections { get; }

        /// <summary>Whatever selection was sitting on the configuration. Never consulted by the classifier.</summary>
        public string? CurrentSelection { get; }

        /// <summary>
        /// True when no selection named <c>NoAction</c> was offered. Part of Class C's stated
        /// definition ("no <c>NoAction</c> and not on either list") and reported so a log can show it,
        /// but note that it does not need to be a separate rule: anything on neither list is Class C
        /// whether or not it offered a way to decline.
        /// </summary>
        public bool OffersNoAction =>
            OfferedSelections.Contains("NoAction", ClassAEntry.NameComparer);

        /// <inheritdoc />
        public override string ToString() =>
            (Name.Length == 0 ? "<unnamed>" : Name) +
            " [" + string.Join("|", OfferedSelections.ToArray()) + "]";
    }
}
