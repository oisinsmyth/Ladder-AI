using System;
using System.Collections.Generic;
using System.Linq;

namespace Ladder.Wave.Tests
{
    /// <summary>
    /// The classifier's load-bearing assertions, factored out so the SAME assertions can be run
    /// against the real classifier (where they must pass) and against a deliberately wrong one (where
    /// they must go red).
    /// </summary>
    /// <remarks>
    /// This indirection is the only way to make the guard's absence visible. A test that merely
    /// asserts "unknown -> Class C" passes whether or not it is checking anything hard; running it
    /// against a mutant that gets it wrong proves it is.
    /// </remarks>
    internal static class ClassifierContract
    {
        /// <summary>
        /// Configurations no entry accounts for. The first two are real: <c>SelectiveDeleteDownload</c>
        /// is D32's named Class C shape, and <c>UserManagementDownload</c> is the one measured
        /// configuration that did NOT abort when left unhandled — TIA applied a default nobody chose
        /// and nobody can see. The rest stand in for "every configuration nobody has seen yet", which
        /// is what each of the last three real downloads produced.
        /// </summary>
        public static IEnumerable<RaisedConfiguration> UnknownConfigurations()
        {
            yield return new RaisedConfiguration("SelectiveDeleteDownload", new[] { "AcceptAll", "DeleteSelected" });
            yield return new RaisedConfiguration("UserManagementDownload", new[]
            {
                "KeepOnlineUserManagementData",
                "UpdateUserManagementDataButKeepOnlinePassword",
                "DownloadAllUserManagementDataResetToProject",
            });
            yield return new RaisedConfiguration("OverwriteSystemData", new[] { "NoAction", "Overwrite" });
            yield return new RaisedConfiguration("AConfigurationNobodyHasSeenYet", new[] { "NoAction", "ProceedAnyway" });
            yield return new RaisedConfiguration("AConfigurationWithNoSelectionsAtAll");
            yield return new RaisedConfiguration(string.Empty, new[] { "NoAction" });
        }

        /// <summary>
        /// A configuration on the allowance list wearing a shape no entry accounts for. Class A's
        /// entailment is claimed PER SELECTION, so an unclaimed selection has no argument behind it
        /// and the configuration must be demoted — answering it would be answering a selection nobody
        /// characterised.
        /// </summary>
        public static IEnumerable<RaisedConfiguration> AllowedNamesInUnrecognisedShapes()
        {
            yield return new RaisedConfiguration("StopModules", new[] { "NoAction", "StopAll", "DeleteAll" });
            yield return new RaisedConfiguration("DataBlockReinitialization", new[] { "NoAction", "ReinitializeAndWipeRetain" });
            yield return new RaisedConfiguration("StartModules", new[] { "NoAction", "StartModule", "StartAndClearMemory" });
        }

        /// <summary>THE GUARD. Everything unrecognised goes to Class C and to a human.</summary>
        public static void UnknownConfigurationsMustLandInClassC(Func<RaisedConfiguration, ConfigurationVerdict> classify)
        {
            foreach (var configuration in UnknownConfigurations())
            {
                var verdict = classify(configuration);

                Assert.Equal(ConfigurationClass.Unknown, verdict.Class);
                Assert.Equal('C', verdict.ClassLetter);
                Assert.Equal(LadderRung.Step7TotalTestAbort, verdict.NextRung);
                Assert.False(
                    verdict.DisruptiveDownloadWouldResolveIt,
                    "An unrecognised configuration must never authorise a disruptive download: " + configuration);
                Assert.Null(verdict.AnsweredSelection);
            }
        }

        /// <summary>THE SECOND GUARD. A known name in an unknown shape is also Class C.</summary>
        public static void UnrecognisedShapesMustLandInClassC(Func<RaisedConfiguration, ConfigurationVerdict> classify)
        {
            foreach (var configuration in AllowedNamesInUnrecognisedShapes())
            {
                var verdict = classify(configuration);

                Assert.Equal(ConfigurationClass.Unknown, verdict.Class);
                Assert.Equal(UnknownReason.AllowedConfigurationInAnUnrecognisedShape, verdict.UnknownReason);
                Assert.False(
                    verdict.DisruptiveDownloadWouldResolveIt,
                    "A recognised configuration offering an uncharacterised selection must not be answered: " + configuration);
            }
        }

        /// <summary>
        /// THE PIN: no Class A entry exists without a documented entailment. Applied to the live
        /// allowance list it must pass; applied to a list carrying an entry somebody added without
        /// doing the work, it must go red.
        /// </summary>
        public static void EveryClassAEntryDocumentsItsEntailment(IReadOnlyList<ClassAEntry> allowanceList)
        {
            Assert.NotEmpty(allowanceList);

            foreach (var entry in allowanceList)
            {
                Assert.True(
                    entry.EntailedBy != DownloadOption.None,
                    entry.ConfigurationName + " names no download option that entails it.");

                Assert.True(
                    entry.Entailment.Trim().Length >= ClassAEntry.MinimumEntailmentLength,
                    entry.ConfigurationName + " does not state what the option entails.");

                Assert.True(
                    entry.Evidence.StartsWith("[M]", StringComparison.Ordinal) ||
                    entry.Evidence.StartsWith("[R]", StringComparison.Ordinal),
                    entry.ConfigurationName + " cites no measured [M] or researched [R] source for its entailment; " +
                    "it carries '" + entry.Evidence + "'.");

                Assert.Contains(entry.AnsweredSelection, entry.RecognisedSelections);
            }
        }
    }
}
