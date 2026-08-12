using System;

namespace Ladder.Wave
{
    /// <summary>
    /// The download options this project ever passes. Class A's whole definition is "a consequence
    /// the chosen download option ALREADY entails", so an entailment claim that does not name an
    /// option is not a claim at all — which is why <see cref="ClassAEntry"/> demands a non-empty set
    /// of these rather than a sentence a reader has to take on trust.
    /// </summary>
    [Flags]
    public enum DownloadOption
    {
        /// <summary>No option named. Never valid on a Class A entry; the constructor rejects it.</summary>
        None = 0,

        /// <summary>
        /// <c>SoftwareOnlyChanges</c> — the wave-boundary download. "The parts TIA finds different
        /// from the controller", decided by TIA at download time, NOT "the blocks you edited".
        /// </summary>
        SoftwareOnlyChanges = 1,

        /// <summary>
        /// <c>Software</c> (all) — the full download. R7/R8: reversed for the disruptive boundary,
        /// still forbidden at a wave boundary where it would reset retentive values for no reason.
        /// </summary>
        Software = 2,

        /// <summary><c>Hardware</c> — the hardware configuration download.</summary>
        Hardware = 4,
    }
}
