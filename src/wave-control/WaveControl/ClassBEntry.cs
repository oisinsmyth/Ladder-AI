using System;

namespace Ladder.Wave
{
    /// <summary>
    /// One entry on the D32 CLASS B deny list — a (configuration, selection) pair that is never
    /// permitted in any mode, because it destroys BEYOND what the download option entails.
    /// </summary>
    /// <remarks>
    /// <para>
    /// KEYED ON THE PAIR, NOT ON THE CONFIGURATION NAME. The spec's two entries are
    /// <c>ResetModule -> DeleteAll</c> and <c>InitializeMemory -> AcceptAll</c>, and the selection is
    /// half the identity: it is the selection that destroys, not the raising of the configuration.
    /// A deny-listed configuration that did NOT offer its denied selection is a shape nobody has
    /// characterised, and the classifier sends it to Class C rather than treating the absence as
    /// evidence it became safe.
    /// </para>
    /// <para>
    /// A Class B entry needs no entailment argument, and asking for one would be incoherent — the
    /// entry exists precisely BECAUSE no option entails it. What it does need is a
    /// <see cref="DestroysBeyond"/>: the thing it destroys that the option does not already cost you.
    /// Both classes go to a human, so an error here is cheap in a way a Class A error is not; the
    /// field is required anyway so the log can say what was refused and why.
    /// </para>
    /// </remarks>
    public sealed class ClassBEntry
    {
        /// <param name="configurationName">The Openness configuration type name, e.g. <c>ResetModule</c>.</param>
        /// <param name="deniedSelection">The selection that destroys, e.g. <c>DeleteAll</c>.</param>
        /// <param name="destroysBeyond">What it destroys beyond what the download option already entails.</param>
        /// <param name="evidence">Where the claim comes from — [M] measured, [R] researched, plus a locator.</param>
        public ClassBEntry(string configurationName, string deniedSelection, string destroysBeyond, string evidence)
        {
            ConfigurationName = Required(configurationName, nameof(configurationName));
            DeniedSelection = Required(deniedSelection, nameof(deniedSelection));
            DestroysBeyond = Required(destroysBeyond, nameof(destroysBeyond));
            Evidence = Required(evidence, nameof(evidence));
        }

        /// <summary>The Openness configuration type name.</summary>
        public string ConfigurationName { get; }

        /// <summary>The selection that destroys.</summary>
        public string DeniedSelection { get; }

        /// <summary>What it destroys beyond what the download option already entails.</summary>
        public string DestroysBeyond { get; }

        /// <summary>Where the claim comes from.</summary>
        public string Evidence { get; }

        private static string Required(string value, string parameterName)
        {
            var trimmed = (value ?? string.Empty).Trim();
            if (trimmed.Length == 0)
            {
                throw new ArgumentException("A Class B entry requires '" + parameterName + "'.", parameterName);
            }

            return trimmed;
        }
    }
}
