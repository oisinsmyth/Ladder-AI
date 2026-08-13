using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Ladder.Wave.Tests
{
    /// <summary>
    /// 6.3 — the four buckets, the residual, and the gate that is `UNCLASSIFIED = 0`.
    /// </summary>
    public sealed class CoverageTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);

        private const string OpenText = "WHEN the level is above setpoint THEN the fill valve opens WITHIN 2 s";
        private const string CloseText = "WHEN the level falls below the deadband THEN the fill valve closes";
        private const string NeverText = "NEVER the conveyor runs with the guard open";

        private static EnumeratedAssertion Open() =>
            new EnumeratedAssertion("REQ-014", OpenText, AssertionForm.When, "FillValve_Open");

        private static EnumeratedAssertion Close() =>
            new EnumeratedAssertion("REQ-014", CloseText, AssertionForm.When, "FillValve_Open");

        private static EnumeratedAssertion Never() =>
            new EnumeratedAssertion("REQ-021", NeverText, AssertionForm.Never, "Conveyor_Run");

        private static AssertionEnumeration Enumeration(params EnumeratedClause[] clauses) =>
            new AssertionEnumeration(clauses, "assertion-enumerator", "gen/x/requirements.md");

        private static AssertionEnumeration TwoAssertionClause() =>
            Enumeration(new EnumeratedClause("REQ-014", new[] { Open(), Close() }));

        private static BucketAssignment Covered(string unit) =>
            new BucketAssignment(unit, CoverageBucket.Covered, "gate-1-signer", "a vector cites it");

        private static VectorCitation Cites(string assertionId, string signal = "FillValve_Open", AssertionForm form = AssertionForm.When) =>
            new VectorCitation("V1", "vector-author", "REQ-014", assertionId, null, form, new[] { signal });

        // =============================================================================================
        // THE MECHANISM: UNCLASSIFIED IS COMPUTED, NEVER WRITTEN
        // =============================================================================================

        [Fact]
        public void The_bucket_enum_has_no_unclassified_member_and_that_absence_is_the_enforcement()
        {
            // *** IF THIS BECOMES WRITABLE, THE GATE BECOMES ASPIRATIONAL. *** An assertion could then
            // be LEFT in a state somebody chose, and forgetting one would produce a missing tick rather
            // than a non-zero residual.
            var names = Enum.GetNames(typeof(CoverageBucket));

            Assert.DoesNotContain(names, n => n.IndexOf("unclassified", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.Equal(CoverageBucket.NotStated, default(CoverageBucket));
        }

        [Fact]
        public void A_forgotten_assertion_produces_a_non_zero_residual_and_a_failed_gate()
        {
            var enumeration = TwoAssertionClause();
            var classification = new CoverageClassification(new[] { Covered(Open().Id) });

            var report = CoverageAnalyser.Analyse(enumeration, classification, new[] { Cites(Open().Id) }, Now);

            Assert.Equal(1, report.Figure.Unclassified);
            Assert.Equal(new[] { Close().Id }, report.UnclassifiedUnits.ToArray());
            Assert.Contains(report.Findings, f => f.Defect == CoverageDefect.UnclassifiedResidual);
            Assert.False(report.MayClaim);
            Assert.Equal(ClaimVerdict.MayNotClaimRequirementsVerified, report.Verdict);
        }

        [Fact]
        public void Everything_classified_lets_the_wave_claim_and_not_every_assertion_needs_a_test()
        {
            var enumeration = TwoAssertionClause();
            var classification = new CoverageClassification(new[]
            {
                Covered(Open().Id),
                new BucketAssignment(Close().Id, CoverageBucket.Deferred, "gate-1-signer", "no vector yet", "owner-a", Now.AddDays(-3)),
            });

            var report = CoverageAnalyser.Analyse(enumeration, classification, new[] { Cites(Open().Id) }, Now);

            Assert.Equal(0, report.Figure.Unclassified);
            Assert.True(report.MayClaim);
            Assert.Equal(1, report.Figure.Covered);
            Assert.Equal(1, report.Figure.Deferred);
        }

        [Fact]
        public void A_classification_cannot_extend_the_denominator()
        {
            // Driving the residual to zero by classifying things that do not exist.
            var enumeration = TwoAssertionClause();
            var classification = new CoverageClassification(new[]
            {
                Covered(Open().Id),
                Covered("REQ-014:ffffff"),
                Covered("REQ-999:aaaaaa"),
            });

            var report = CoverageAnalyser.Analyse(enumeration, classification, null, Now);

            Assert.Contains(report.AssignmentFindings, f => f.Defect == AssignmentDefect.UnitNotInTheEnumeration);
            Assert.Equal(1, report.Figure.Unclassified);
            Assert.False(report.MayClaim);
        }

        [Fact]
        public void A_unit_in_two_buckets_is_refused_rather_than_resolved_by_taking_the_last()
        {
            var enumeration = Enumeration(new EnumeratedClause("REQ-014", new[] { Open() }));
            var classification = new CoverageClassification(new[]
            {
                Covered(Open().Id),
                new BucketAssignment(Open().Id, CoverageBucket.OutOfScope, "gate-1-signer", "reconsidered"),
            });

            var report = CoverageAnalyser.Analyse(enumeration, classification, null, Now);

            Assert.Contains(report.AssignmentFindings, f => f.Defect == AssignmentDefect.AssignedToMoreThanOneBucket);
        }

        [Fact]
        public void An_assignment_naming_no_bucket_is_refused()
        {
            var enumeration = Enumeration(new EnumeratedClause("REQ-014", new[] { Open() }));
            var classification = new CoverageClassification(new[]
            {
                new BucketAssignment(Open().Id, CoverageBucket.NotStated, "gate-1-signer"),
            });

            var report = CoverageAnalyser.Analyse(enumeration, classification, null, Now);

            Assert.Contains(report.AssignmentFindings, f => f.Defect == AssignmentDefect.NoBucketStated);
        }

        // =============================================================================================
        // EMPTY IS NOT CLEAN, AND "NOT ENUMERABLE" IS NOT A NUMBER
        // =============================================================================================

        [Fact]
        public void An_empty_enumeration_is_not_enumerable_never_a_hundred_percent()
        {
            // An empty denominator makes every suite complete by construction — the exact shape FI-44
            // keeps catching.
            var report = CoverageAnalyser.Analyse(
                Enumeration(),
                new CoverageClassification(null),
                null,
                Now);

            Assert.Equal(ClaimVerdict.NotEnumerable, report.Verdict);
            Assert.Contains(report.EnumerationFindings, f => f.Defect == EnumerationDefect.Empty);
            Assert.Contains("NOT ENUMERABLE", report.Describe(), StringComparison.Ordinal);
            Assert.DoesNotContain("COVERAGE", report.Describe(), StringComparison.Ordinal);
        }

        [Fact]
        public void A_clause_that_decomposed_to_nothing_is_an_error_in_the_decomposition()
        {
            var enumeration = Enumeration(
                new EnumeratedClause("REQ-014", new[] { Open() }),
                new EnumeratedClause("REQ-015", new EnumeratedAssertion[0]));

            var report = CoverageAnalyser.Analyse(enumeration, new CoverageClassification(null), null, Now);

            Assert.Contains(report.EnumerationFindings, f => f.Defect == EnumerationDefect.ClauseWithNoAssertions);
            Assert.Equal(ClaimVerdict.NotEnumerable, report.Verdict);
        }

        [Fact]
        public void A_clause_with_no_stable_id_makes_the_denominator_unenumerable()
        {
            var enumeration = Enumeration(new EnumeratedClause("  ", new[] { Open() }));

            Assert.Contains(enumeration.Validate(), f => f.Defect == EnumerationDefect.ClauseNotStablyIdentified);
        }

        [Fact]
        public void Not_evaluated_is_the_zero_value_of_the_claim_verdict()
        {
            Assert.Equal(ClaimVerdict.NotEvaluated, default(ClaimVerdict));
        }

        // =============================================================================================
        // A BARE PERCENTAGE IS NOT AN AVAILABLE OUTPUT
        // =============================================================================================

        [Fact]
        public void No_type_here_offers_a_bare_percentage_to_quote()
        {
            // §5 row 6 is the one inflation route that closes CLEANLY — but only if the closure is
            // structural. A `double Percent { get; }` anywhere makes it a convention again.
            foreach (var type in new[] { typeof(CoverageFigure), typeof(CoverageReport) })
            {
                foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance))
                {
                    var returns = (member as PropertyInfo)?.PropertyType ?? (member as MethodInfo)?.ReturnType;
                    if (returns == null || !(returns == typeof(double) || returns == typeof(float) || returns == typeof(decimal)))
                    {
                        continue;
                    }

                    Assert.False(
                        member.Name.IndexOf("percent", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        member.Name.IndexOf("ratio", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        member.Name.IndexOf("fraction", StringComparison.OrdinalIgnoreCase) >= 0,
                        type.Name + "." + member.Name + " offers a bare coverage number that could be quoted alone.");
                }
            }
        }

        [Fact]
        public void The_rendered_figure_always_carries_all_four_buckets_the_residual_and_the_oldest_deferral()
        {
            var enumeration = TwoAssertionClause();
            var classification = new CoverageClassification(new[]
            {
                Covered(Open().Id),
                new BucketAssignment(Close().Id, CoverageBucket.Deferred, "gate-1-signer", "no vector yet", "owner-a", Now.AddDays(-41)),
            });

            var rendered = CoverageAnalyser.Analyse(enumeration, classification, new[] { Cites(Open().Id) }, Now).Figure.Render();

            Assert.Contains("COVERED 1", rendered, StringComparison.Ordinal);
            Assert.Contains("OUT-OF-SCOPE 0", rendered, StringComparison.Ordinal);
            Assert.Contains("DEFERRED 1", rendered, StringComparison.Ordinal);
            Assert.Contains("oldest 41d", rendered, StringComparison.Ordinal);
            Assert.Contains("UNTESTABLE-ON-RIG 0", rendered, StringComparison.Ordinal);
            Assert.Contains("UNCLASSIFIED 0", rendered, StringComparison.Ordinal);
            Assert.Contains("50.0%", rendered, StringComparison.Ordinal);
        }

        // =============================================================================================
        // THE ID SCHEME — AND STALE IS NOT MISSING
        // =============================================================================================

        [Fact]
        public void An_id_depends_only_on_clause_identity_and_content_and_nothing_positional()
        {
            var first = AssertionId.Compute("REQ-014", OpenText);

            // Same text, computed again anywhere in any order.
            Assert.Equal(first, AssertionId.Compute("REQ-014", "  WHEN the level is above setpoint   THEN the fill valve opens WITHIN 2 s.  "));

            // Different clause, same text: a different assertion.
            Assert.NotEqual(first, AssertionId.Compute("REQ-015", OpenText));

            // Different text: a different ID, deliberately.
            Assert.NotEqual(first, AssertionId.Compute("REQ-014", CloseText));
        }

        [Fact]
        public void A_hand_edited_id_is_caught_by_rehashing()
        {
            var tampered = new EnumeratedAssertion("REQ-014", OpenText, AssertionForm.When, "FillValve_Open", declaredId: "REQ-014:000000");
            var enumeration = Enumeration(new EnumeratedClause("REQ-014", new[] { tampered }));

            Assert.Contains(enumeration.Validate(), f => f.Defect == EnumerationDefect.IdDoesNotRecompute);
        }

        [Fact]
        public void A_reworded_assertion_makes_prior_citations_STALE_and_not_missing()
        {
            // *** THE TWO STATES MUST NOT COLLAPSE. *** They call for opposite fixes: a missing ID sends
            // an author hunting a typo; a stale one sends them to re-read the vector against new words.
            var oldId = AssertionId.Compute("REQ-014", OpenText);
            var reworded = new EnumeratedAssertion(
                "REQ-014",
                "WHEN the level is above setpoint THEN the fill valve opens WITHIN 5 s",
                AssertionForm.When,
                "FillValve_Open",
                supersedes: oldId);

            var enumeration = Enumeration(new EnumeratedClause("REQ-014", new[] { reworded }));

            var report = CoverageAnalyser.Analyse(
                enumeration,
                new CoverageClassification(new[] { Covered(reworded.Id) }),
                new[] { Cites(oldId) },
                Now);

            Assert.Contains(report.Findings, f => f.Defect == CoverageDefect.CitationIsStale);
            Assert.DoesNotContain(report.Findings, f => f.Defect == CoverageDefect.CitationIsMissing);
        }

        [Fact]
        public void An_id_nothing_superseded_is_MISSING_and_not_stale()
        {
            var enumeration = Enumeration(new EnumeratedClause("REQ-014", new[] { Open() }));

            var report = CoverageAnalyser.Analyse(
                enumeration,
                new CoverageClassification(new[] { Covered(Open().Id) }),
                new[] { Cites("REQ-014:abcdef") },
                Now);

            Assert.Contains(report.Findings, f => f.Defect == CoverageDefect.CitationIsMissing);
            Assert.DoesNotContain(report.Findings, f => f.Defect == CoverageDefect.CitationIsStale);
        }

        [Theory]
        [InlineData("REQ-014.A2", true)]
        [InlineData("REQ-014.a10", true)]
        [InlineData("REQ-014:3f9a1c", false)]
        [InlineData("REQ-014.Alpha", false)]
        public void The_display_ordinal_form_is_detected_by_shape(string citation, bool isOrdinal)
        {
            Assert.Equal(isOrdinal, AssertionId.IsDisplayOrdinalForm(citation));
        }

        [Fact]
        public void A_citation_in_the_display_ordinal_form_is_rejected()
        {
            var enumeration = Enumeration(new EnumeratedClause("REQ-014", new[] { Open() }));

            var report = CoverageAnalyser.Analyse(
                enumeration,
                new CoverageClassification(new[] { Covered(Open().Id) }),
                new[] { Cites("REQ-014.A1") },
                Now);

            Assert.Contains(report.Findings, f => f.Defect == CoverageDefect.CitationUsesTheDisplayOrdinalForm);
        }

        [Fact]
        public void Two_assertions_in_one_clause_that_normalise_identically_are_a_duplicate_not_a_collision()
        {
            var enumeration = Enumeration(new EnumeratedClause("REQ-014", new[]
            {
                Open(),
                new EnumeratedAssertion("REQ-014", OpenText + ".", AssertionForm.When, "FillValve_Open"),
            }));

            Assert.Contains(enumeration.Validate(), f => f.Defect == EnumerationDefect.DuplicateAssertion);
        }

        // =============================================================================================
        // THE FORM AUTHORITY GAP — CLOSED
        // =============================================================================================

        [Fact]
        public void A_vector_citing_a_NEVER_while_declaring_a_When_is_caught()
        {
            // The gap the enumeration's own producer could not close: the gate's enumeration was flat
            // strings, so the vector's declared form had no authority to be checked against. It does now.
            var never = Never();
            var enumeration = Enumeration(new EnumeratedClause("REQ-021", new[] { never }));

            var report = CoverageAnalyser.Analyse(
                enumeration,
                new CoverageClassification(new[] { Covered(never.Id) }),
                new[] { new VectorCitation("V1", "vector-author", "REQ-021", never.Id, null, AssertionForm.When, new[] { "Conveyor_Run" }) },
                Now);

            Assert.Contains(report.Findings, f => f.Defect == CoverageDefect.DeclaredFormDisagreesWithTheEnumeration);
        }

        [Fact]
        public void A_vector_that_declares_no_form_at_all_is_caught_by_the_same_check()
        {
            var enumeration = Enumeration(new EnumeratedClause("REQ-014", new[] { Open() }));

            var report = CoverageAnalyser.Analyse(
                enumeration,
                new CoverageClassification(new[] { Covered(Open().Id) }),
                new[] { Cites(Open().Id, form: AssertionForm.Unstated) },
                Now);

            Assert.Contains(report.Findings, f => f.Defect == CoverageDefect.DeclaredFormDisagreesWithTheEnumeration);
            Assert.Equal(AssertionForm.Unstated, default(AssertionForm));
        }

        [Fact]
        public void An_assertion_whose_text_does_not_wear_its_declared_form_is_an_enumeration_defect()
        {
            var wrong = new EnumeratedAssertion("REQ-014", "the valve shall open when the level is high", AssertionForm.When, "V");
            var enumeration = Enumeration(new EnumeratedClause("REQ-014", new[] { wrong }));

            Assert.Contains(enumeration.Validate(), f => f.Defect == EnumerationDefect.TextDoesNotMatchItsForm);
        }

        // =============================================================================================
        // THE INFLATION ROUTES WITH MECHANICAL CLOSURES
        // =============================================================================================

        [Fact]
        public void Citing_an_assertion_whose_response_signal_the_vector_never_mentions_is_caught()
        {
            var enumeration = Enumeration(new EnumeratedClause("REQ-014", new[] { Open() }));

            var report = CoverageAnalyser.Analyse(
                enumeration,
                new CoverageClassification(new[] { Covered(Open().Id) }),
                new[] { Cites(Open().Id, signal: "SomeOtherTag") },
                Now);

            Assert.Contains(report.Findings, f => f.Defect == CoverageDefect.ResponseSignalAbsentFromExpectations);
        }

        [Fact]
        public void An_escape_hatch_bucket_assigned_by_an_interested_party_is_refused()
        {
            // Bucket laundering: pushing a hard assertion into OUT-OF-SCOPE or UNTESTABLE-ON-RIG.
            var enumeration = Enumeration(new EnumeratedClause("REQ-014", new[] { Open(), Close() }));

            var byBlockAuthor = new CoverageClassification(new[]
            {
                new BucketAssignment(Open().Id, CoverageBucket.OutOfScope, "block-author", "not control logic"),
                Covered(Close().Id),
            });

            var report = CoverageAnalyser.Analyse(enumeration, byBlockAuthor, new[] { Cites(Close().Id) }, Now, blockAuthor: "block-author");

            Assert.Contains(report.AssignmentFindings, f => f.Defect == AssignmentDefect.EscapeHatchAssignedByAnInterestedParty);
        }

        [Fact]
        public void An_escape_hatch_bucket_assigned_by_a_vector_author_is_refused_too()
        {
            var enumeration = Enumeration(new EnumeratedClause("REQ-014", new[] { Open(), Close() }));

            var classification = new CoverageClassification(new[]
            {
                new BucketAssignment(Open().Id, CoverageBucket.UntestableOnRig, "vector-author", "cannot see it"),
                Covered(Close().Id),
            });

            var report = CoverageAnalyser.Analyse(enumeration, classification, new[] { Cites(Close().Id) }, Now);

            Assert.Contains(report.AssignmentFindings, f => f.Defect == AssignmentDefect.EscapeHatchAssignedByAnInterestedParty);
        }

        [Fact]
        public void A_deferral_without_an_owner_or_a_date_is_refused()
        {
            var enumeration = Enumeration(new EnumeratedClause("REQ-014", new[] { Open() }));

            var noOwner = new CoverageClassification(new[]
            {
                new BucketAssignment(Open().Id, CoverageBucket.Deferred, "gate-1-signer", "later", null, Now),
            });

            var noDate = new CoverageClassification(new[]
            {
                new BucketAssignment(Open().Id, CoverageBucket.Deferred, "gate-1-signer", "later", "owner-a"),
            });

            Assert.Contains(noOwner.Validate(enumeration), f => f.Defect == AssignmentDefect.DeferralWithoutOwnerOrDate);
            Assert.Contains(noDate.Validate(enumeration), f => f.Defect == AssignmentDefect.DeferralWithoutOwnerOrDate);
        }

        [Fact]
        public void An_assignment_with_no_recorded_assigner_is_refused_because_the_identity_check_cannot_run()
        {
            var enumeration = Enumeration(new EnumeratedClause("REQ-014", new[] { Open() }));
            var classification = new CoverageClassification(new[]
            {
                new BucketAssignment(Open().Id, CoverageBucket.OutOfScope, null, "not control logic"),
            });

            Assert.Contains(classification.Validate(enumeration), f => f.Defect == AssignmentDefect.NoAssignerRecorded);
        }

        [Fact]
        public void An_enumerator_who_is_also_the_block_author_loses_D6_at_the_denominator()
        {
            // §7's open item 3, and the one that would undo most of the design.
            var enumeration = new AssertionEnumeration(
                new[] { new EnumeratedClause("REQ-014", new[] { Open() }) },
                "block-author",
                "gen/x/requirements.md");

            Assert.Contains(
                enumeration.Validate(blockAuthor: "block-author"),
                f => f.Defect == EnumerationDefect.EnumeratorIsNotIndependent);
        }

        [Fact]
        public void An_enumeration_recording_no_enumerator_is_not_an_independent_one()
        {
            var enumeration = new AssertionEnumeration(
                new[] { new EnumeratedClause("REQ-014", new[] { Open() }) },
                null,
                "gen/x/requirements.md");

            Assert.Contains(
                enumeration.Validate(blockAuthor: "block-author"),
                f => f.Defect == EnumerationDefect.EnumeratorIsNotIndependent);
        }

        [Fact]
        public void A_vector_authored_by_the_block_author_is_caught()
        {
            var enumeration = Enumeration(new EnumeratedClause("REQ-014", new[] { Open() }));

            var report = CoverageAnalyser.Analyse(
                enumeration,
                new CoverageClassification(new[] { Covered(Open().Id) }),
                new[] { new VectorCitation("V1", "block-author", "REQ-014", Open().Id, null, AssertionForm.When, new[] { "FillValve_Open" }) },
                Now,
                blockAuthor: "block-author");

            Assert.Contains(report.Findings, f => f.Defect == CoverageDefect.VectorAuthorIsTheBlockAuthor);
        }

        [Fact]
        public void Covered_without_a_citation_is_the_classification_claiming_the_numerators_fact()
        {
            var enumeration = Enumeration(new EnumeratedClause("REQ-014", new[] { Open() }));

            var report = CoverageAnalyser.Analyse(
                enumeration,
                new CoverageClassification(new[] { Covered(Open().Id) }),
                null,
                Now);

            Assert.Contains(report.Findings, f => f.Defect == CoverageDefect.CoveredWithoutACitation);
        }

        [Fact]
        public void A_cited_assertion_bucketed_into_an_escape_hatch_is_caught()
        {
            var enumeration = Enumeration(new EnumeratedClause("REQ-014", new[] { Open() }));

            var report = CoverageAnalyser.Analyse(
                enumeration,
                new CoverageClassification(new[]
                {
                    new BucketAssignment(Open().Id, CoverageBucket.UntestableOnRig, "gate-1-signer", "cannot see it"),
                }),
                new[] { Cites(Open().Id) },
                Now);

            Assert.Contains(report.Findings, f => f.Defect == CoverageDefect.CitedButNotBucketedCovered);
        }

        // =============================================================================================
        // INSTANCES ARE SPEC-SIDE
        // =============================================================================================

        [Fact]
        public void Instances_multiply_the_denominator_and_come_from_the_clause_not_the_citations()
        {
            // If a citation could add an instance, an instance nobody wrote a vector for could not be
            // missing — the self-referential trap arriving by a side door.
            var enumeration = Enumeration(
                new EnumeratedClause("REQ-014", new[] { Open() }, new[] { "Feeder_01", "Feeder_02", "Feeder_03" }));

            Assert.Equal(3, enumeration.Units.Count);
            Assert.Contains(enumeration.Units, u => u.Key == Open().Id + "@Feeder_02");

            var report = CoverageAnalyser.Analyse(
                enumeration,
                new CoverageClassification(new[] { Covered(Open().Id + "@Feeder_01") }),
                null,
                Now);

            Assert.Equal(2, report.Figure.Unclassified);
        }

        [Fact]
        public void A_citation_naming_an_instance_the_clause_does_not_declare_is_caught()
        {
            var enumeration = Enumeration(
                new EnumeratedClause("REQ-014", new[] { Open() }, new[] { "Feeder_01" }));

            var report = CoverageAnalyser.Analyse(
                enumeration,
                new CoverageClassification(new[] { Covered(Open().Id + "@Feeder_01") }),
                new[] { new VectorCitation("V1", "vector-author", "REQ-014", Open().Id, "Feeder_99", AssertionForm.When, new[] { "FillValve_Open" }) },
                Now);

            Assert.Contains(report.Findings, f => f.Defect == CoverageDefect.InstanceNotDeclaredByTheClause);
        }

        // =============================================================================================
        // RE-DECOMPOSITION IS ITS OWN EVENT
        // =============================================================================================

        [Fact]
        public void A_redecomposition_that_changes_the_count_is_reported_as_its_own_event()
        {
            // A clause previously read as one assertion that now decomposes into two produces a NEW,
            // UNCLASSIFIED assertion — explicitly not absorbed as a classification change.
            var enumeration = TwoAssertionClause();

            var report = CoverageAnalyser.Analyse(
                enumeration,
                new CoverageClassification(new[] { Covered(Open().Id), Covered(Close().Id) }),
                new[] { Cites(Open().Id), Cites(Close().Id) },
                Now,
                previousAssertionCount: 1);

            Assert.Contains(report.Findings, f => f.Defect == CoverageDefect.RedecompositionChangedTheCount);
            Assert.False(report.MayClaim);
        }

        [Fact]
        public void An_unchanged_count_raises_no_redecomposition_event()
        {
            var enumeration = TwoAssertionClause();

            var report = CoverageAnalyser.Analyse(
                enumeration,
                new CoverageClassification(new[] { Covered(Open().Id), Covered(Close().Id) }),
                new[] { Cites(Open().Id), Cites(Close().Id) },
                Now,
                previousAssertionCount: 2);

            Assert.DoesNotContain(report.Findings, f => f.Defect == CoverageDefect.RedecompositionChangedTheCount);
            Assert.True(report.MayClaim);
        }

        [Fact]
        public void The_assertions_per_clause_ratio_is_reported_and_not_judged()
        {
            var enumeration = Enumeration(
                new EnumeratedClause("REQ-014", new[] { Open(), Close() }),
                new EnumeratedClause("REQ-021", new[] { Never() }));

            var described = CoverageAnalyser.Analyse(enumeration, new CoverageClassification(null), null, Now).Describe();

            Assert.Contains("REQ-014=2", described, StringComparison.Ordinal);
            Assert.Contains("REQ-021=1", described, StringComparison.Ordinal);
            Assert.Contains("no corpus norm exists yet", described, StringComparison.Ordinal);
        }
    }
}
