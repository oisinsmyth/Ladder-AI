namespace Ladder.Wave.Tests
{
    /// <summary>
    /// Deliberately WRONG classifiers, kept so the guards in <see cref="ClassifierContract"/> can be
    /// shown to catch the mistakes they exist to catch.
    /// </summary>
    /// <remarks>
    /// *** NOTHING IN PRODUCTION CODE REFERENCES THESE. *** They exist because the absence of a guard
    /// is invisible: a test asserting "unknown lands in Class C" passes just as green whether it is
    /// checking anything or not, and the only way to know is to point it at an implementation that
    /// gets it wrong and watch it fail.
    /// </remarks>
    internal static class MutantClassifiers
    {
        /// <summary>
        /// THE MISTAKE THIS COMPONENT EXISTS TO PREVENT: an unrecognised configuration silently
        /// treated as Class A, which would escalate to a disruptive download against something that
        /// might destroy data.
        /// </summary>
        private static readonly ClassAEntry FabricatedEntry = new ClassAEntry(
            configurationName: "<fabricated>",
            answeredSelection: "WhateverItOffered",
            recognisedSelections: new[] { "WhateverItOffered" },
            entailedBy: DownloadOption.Software,
            entailment: "a fabricated entailment, existing only so this mutant can build a wrong verdict",
            evidence: "[MUTANT] not a real source");

        /// <summary>Classifies exactly as the real one does, except that the unknown branch returns Class A.</summary>
        public static ConfigurationVerdict UnknownIsClassA(RaisedConfiguration configuration)
        {
            var verdict = ConfigurationClassifier.Classify(configuration);

            return verdict.Class == ConfigurationClass.Unknown
                ? ConfigurationVerdict.ClassA(configuration, FabricatedEntry)
                : verdict;
        }

        /// <summary>
        /// The subtler mistake: the NAME is checked but the SHAPE is not, so a known configuration
        /// offering a selection nobody has characterised is answered anyway.
        /// </summary>
        public static ConfigurationVerdict ShapeIsNotChecked(RaisedConfiguration configuration)
        {
            var verdict = ConfigurationClassifier.Classify(configuration);

            if (verdict.UnknownReason != UnknownReason.AllowedConfigurationInAnUnrecognisedShape)
            {
                return verdict;
            }

            foreach (var entry in ConfigurationClassifier.AllowanceList)
            {
                if (ClassAEntry.NameComparer.Equals(entry.ConfigurationName, configuration.Name))
                {
                    return ConfigurationVerdict.ClassA(configuration, entry);
                }
            }

            return verdict;
        }
    }
}
