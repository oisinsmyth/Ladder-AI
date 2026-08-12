using System;
using System.Linq;
using Xunit.Sdk;

namespace Ladder.Wave.Tests
{
    /// <summary>
    /// D32 caveat 3 — the Class A / B / C decision that says whether the escalation ladder's step 6
    /// is progress or a wasted CPU stop.
    /// </summary>
    public sealed class ConfigurationClassifierTests
    {
        // --- CLASS A: refused only because the mode is non-disruptive -----------------------------

        [Theory]
        [InlineData("StopModules", "StopAll", "NoAction", "StopAll")]
        [InlineData("DataBlockReinitialization", "StopPlcAndReinitialize", "NoAction", "StopPlcAndReinitialize")]
        [InlineData("StartModules", "StartModule", "NoAction", "StartModule")]
        public void The_three_allowance_list_configurations_are_class_A(
            string name, string expectedAnswer, string selectionA, string selectionB)
        {
            var verdict = ConfigurationClassifier.Classify(name, selectionA, selectionB);

            Assert.Equal(ConfigurationClass.RefusedOnlyBecauseNonDisruptive, verdict.Class);
            Assert.Equal('A', verdict.ClassLetter);
            Assert.Equal(LadderRung.Step6DisruptiveFullDownload, verdict.NextRung);
            Assert.True(verdict.DisruptiveDownloadWouldResolveIt);
            Assert.Equal(expectedAnswer, verdict.AnsweredSelection);
            Assert.Null(verdict.UnknownReason);
        }

        [Fact]
        public void Class_A_is_the_only_class_a_disruptive_download_is_spent_on()
        {
            var classA = ConfigurationClassifier.Classify("StopModules", "NoAction", "StopAll");
            var classB = ConfigurationClassifier.Classify("ResetModule", "NoAction", "DeleteAll");
            var classC = ConfigurationClassifier.Classify("SelectiveDeleteDownload", "AcceptAll", "DeleteSelected");

            Assert.True(classA.DisruptiveDownloadWouldResolveIt);
            Assert.False(classB.DisruptiveDownloadWouldResolveIt);
            Assert.False(classC.DisruptiveDownloadWouldResolveIt);
        }

        [Fact]
        public void StopAll_is_class_A_and_not_on_the_deny_list()
        {
            // Three passages of the spec cited a deny list containing StopAll; the list that exists
            // does not contain it. Putting it there would make R8's sanctioned disruptive mode
            // impossible and D25 unimplementable, so it is pinned here.
            Assert.DoesNotContain(
                ConfigurationClassifier.DenyList,
                e => string.Equals(e.DeniedSelection, "StopAll", StringComparison.OrdinalIgnoreCase));

            Assert.Equal(
                ConfigurationClass.RefusedOnlyBecauseNonDisruptive,
                ConfigurationClassifier.Classify("StopModules", "NoAction", "StopAll").Class);
        }

        // --- CLASS B: never permitted in any mode -------------------------------------------------

        [Theory]
        [InlineData("ResetModule", "DeleteAll")]
        [InlineData("InitializeMemory", "AcceptAll")]
        public void The_two_deny_list_pairs_are_class_B(string name, string deniedSelection)
        {
            var verdict = ConfigurationClassifier.Classify(name, "NoAction", deniedSelection);

            Assert.Equal(ConfigurationClass.NeverPermitted, verdict.Class);
            Assert.Equal('B', verdict.ClassLetter);
            Assert.Equal(LadderRung.Step7TotalTestAbort, verdict.NextRung);
            Assert.False(verdict.DisruptiveDownloadWouldResolveIt);
            Assert.Null(verdict.AnsweredSelection);
        }

        [Fact]
        public void A_deny_listed_configuration_that_did_not_offer_its_denied_selection_is_class_C_not_class_A()
        {
            // Not evidence that it became safe — evidence that this is a shape nobody characterised.
            var verdict = ConfigurationClassifier.Classify("ResetModule", "NoAction", "ResetRetainOnly");

            Assert.Equal(ConfigurationClass.Unknown, verdict.Class);
            Assert.Equal(UnknownReason.DeniedConfigurationInAnUnrecognisedShape, verdict.UnknownReason);
            Assert.Equal(LadderRung.Step7TotalTestAbort, verdict.NextRung);
        }

        // --- CLASS C: the branch that matters -----------------------------------------------------

        [Fact]
        public void An_unknown_configuration_lands_in_class_C()
        {
            ClassifierContract.UnknownConfigurationsMustLandInClassC(ConfigurationClassifier.Classify);
        }

        [Fact]
        public void An_allowance_list_name_in_an_unrecognised_shape_lands_in_class_C()
        {
            ClassifierContract.UnrecognisedShapesMustLandInClassC(ConfigurationClassifier.Classify);
        }

        [Fact]
        public void A_configuration_with_no_name_lands_in_class_C()
        {
            var verdict = ConfigurationClassifier.Classify(new RaisedConfiguration(null));

            Assert.Equal(ConfigurationClass.Unknown, verdict.Class);
            Assert.Equal(UnknownReason.NoConfigurationName, verdict.UnknownReason);
        }

        [Fact]
        public void Class_C_is_the_zero_value_so_a_defaulted_classification_fetches_a_human()
        {
            // The safety argument of the enum, pinned. R4a's lesson is that a zero value's DIRECTION
            // is load-bearing; here it is pointed at the conservative outcome deliberately.
            Assert.Equal(ConfigurationClass.Unknown, default(ConfigurationClass));
            Assert.Equal(LadderRung.Step7TotalTestAbort, default(LadderRung));
            Assert.Equal(EscalationOutcome.ReachedHumanWithoutAttemptingDownload, default(EscalationOutcome));
        }

        // --- THE NEGATIVE TESTS: proof the guards above are load-bearing ---------------------------

        [Fact]
        public void NEGATIVE_a_classifier_that_defaults_an_unknown_to_class_A_fails_the_unknown_guard()
        {
            // The real classifier passes...
            ClassifierContract.UnknownConfigurationsMustLandInClassC(ConfigurationClassifier.Classify);

            // ...and one that treats an unrecognised configuration as Class A does not. Without this,
            // the guard's absence would be invisible: the positive test would stay green either way.
            var failure = Assert.ThrowsAny<XunitException>(
                () => ClassifierContract.UnknownConfigurationsMustLandInClassC(MutantClassifiers.UnknownIsClassA));

            Assert.Contains("RefusedOnlyBecauseNonDisruptive", failure.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void NEGATIVE_a_classifier_that_checks_the_name_but_not_the_shape_fails_the_shape_guard()
        {
            ClassifierContract.UnrecognisedShapesMustLandInClassC(ConfigurationClassifier.Classify);

            Assert.ThrowsAny<XunitException>(
                () => ClassifierContract.UnrecognisedShapesMustLandInClassC(MutantClassifiers.ShapeIsNotChecked));
        }

        // --- THE ALLOWANCE LIST'S OWN GATE ---------------------------------------------------------

        [Fact]
        public void The_allowance_list_is_exactly_D32s_three_entries()
        {
            Assert.Equal(
                new[] { "StopModules", "DataBlockReinitialization", "StartModules" },
                ConfigurationClassifier.AllowanceList.Select(e => e.ConfigurationName).ToArray());

            Assert.Equal(
                new[] { "ResetModule -> DeleteAll", "InitializeMemory -> AcceptAll" },
                ConfigurationClassifier.DenyList.Select(e => e.ConfigurationName + " -> " + e.DeniedSelection).ToArray());
        }

        [Fact]
        public void No_class_A_entry_exists_without_a_documented_entailment()
        {
            ClassifierContract.EveryClassAEntryDocumentsItsEntailment(ConfigurationClassifier.AllowanceList);
        }

        [Fact]
        public void NEGATIVE_an_entry_added_without_doing_the_work_fails_the_entailment_pin()
        {
            var undocumented = new[]
            {
                new ClassAEntry(
                    configurationName: "SomethingSomebodyAddedInAHurry",
                    answeredSelection: "ProceedAnyway",
                    recognisedSelections: new[] { "ProceedAnyway" },
                    entailedBy: DownloadOption.Software,
                    entailment: "it seemed fine when we looked at it on the day",
                    evidence: "TODO"),
            };

            Assert.ThrowsAny<XunitException>(
                () => ClassifierContract.EveryClassAEntryDocumentsItsEntailment(undocumented));
        }

        [Fact]
        public void A_class_A_entry_cannot_be_built_without_naming_the_download_option()
        {
            var ex = Assert.Throws<ArgumentException>(() => new ClassAEntry(
                "Whatever", "Sel", new[] { "Sel" }, DownloadOption.None,
                "a perfectly well written entailment that names nothing", "[M] somewhere"));

            Assert.Equal("entailedBy", ex.ParamName);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("it's fine")]
        public void A_class_A_entry_cannot_be_built_without_stating_what_the_option_entails(string entailment)
        {
            var ex = Assert.Throws<ArgumentException>(() => new ClassAEntry(
                "Whatever", "Sel", new[] { "Sel" }, DownloadOption.Software, entailment, "[M] somewhere"));

            Assert.Equal("entailment", ex.ParamName);
        }

        [Fact]
        public void A_class_A_entry_cannot_be_built_without_evidence()
        {
            var ex = Assert.Throws<ArgumentException>(() => new ClassAEntry(
                "Whatever", "Sel", new[] { "Sel" }, DownloadOption.Software,
                "a perfectly well written entailment for the Software download", "  "));

            Assert.Equal("evidence", ex.ParamName);
        }

        [Fact]
        public void A_class_A_entry_cannot_claim_an_entailment_for_a_selection_it_has_never_seen_offered()
        {
            Assert.Throws<ArgumentException>(() => new ClassAEntry(
                "Whatever", "StopAll", new[] { "NoAction" }, DownloadOption.Software,
                "a perfectly well written entailment for the Software download", "[M] somewhere"));
        }

        // --- REPORTABILITY -------------------------------------------------------------------------

        [Fact]
        public void The_verdict_log_line_carries_the_class_the_rung_and_the_offered_selections()
        {
            var line = ConfigurationClassifier.Classify("ResetModule", "NoAction", "DeleteAll").ToLogLine();

            Assert.Contains("CONFIG-CLASS B", line, StringComparison.Ordinal);
            Assert.Contains("ResetModule", line, StringComparison.Ordinal);
            Assert.Contains("NoAction|DeleteAll", line, StringComparison.Ordinal);
            Assert.Contains("step 7", line, StringComparison.Ordinal);
        }

        [Fact]
        public void The_current_selection_is_carried_for_the_log_and_never_changes_the_class()
        {
            // R4a: the zero values differ in DIRECTION between the Siemens enums, so a classifier that
            // consulted the pre-selected value would invert silently on DataBlockReinitialization.
            var withSafeLooking = new RaisedConfiguration(
                "DataBlockReinitialization", new[] { "NoAction", "StopPlcAndReinitialize" }, currentSelection: "NoAction");
            var withDestructive = new RaisedConfiguration(
                "DataBlockReinitialization", new[] { "NoAction", "StopPlcAndReinitialize" }, currentSelection: "StopPlcAndReinitialize");

            Assert.Equal(
                ConfigurationClassifier.Classify(withSafeLooking).Class,
                ConfigurationClassifier.Classify(withDestructive).Class);
        }
    }
}
