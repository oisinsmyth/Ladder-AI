using System;
using System.Linq;

namespace Ladder.Wave.Tests
{
    /// <summary>
    /// 6.2 / X-J — the reserved number range for harness objects, and the audit that says whether it
    /// is being honoured.
    /// </summary>
    public sealed class HarnessNumberTests
    {
        private static HarnessNumberRange Range(int first = 900, int last = 949, params string[] spaces) =>
            new HarnessNumberRange(
                first,
                last,
                spaces.Length == 0 ? new[] { "FB", "FC", "OB", "DB" } : spaces,
                "gate-1-signer",
                "X-J: harness objects recognisable by number, and DB-7 cleanup knows what it owns");

        private static NumberedBlock Harness(string name, ObjectKind kind, int number) =>
            new NumberedBlock(name, kind, number, BlockOwner.Harness);

        private static NumberedBlock Deliverable(string name, ObjectKind kind, int number) =>
            new NumberedBlock(name, kind, number, BlockOwner.Deliverable);

        // =============================================================================================
        // THE RANGE REFUSES — IT DOES NOT ADVISE
        // =============================================================================================

        [Fact]
        public void An_allocation_never_leaves_the_reserved_range()
        {
            var ledger = new HarnessNumberLedger(Range());
            var existing = Enumerable.Range(900, 50).Select(n => Harness("FC_Harness_" + n, ObjectKind.Function, n)).ToArray();

            var full = ledger.Allocate("FC", existing);

            // The refusal is the point: the alternative is handing out 950, which is outside the
            // reservation and is exactly where a deliverable may legitimately be.
            Assert.False(full.Granted);
            Assert.Equal(0, full.Number);
            Assert.Contains("FULL", full.Reason, StringComparison.Ordinal);
            Assert.Contains("would silently re-create the collision", full.Reason, StringComparison.Ordinal);
        }

        [Fact]
        public void An_allocation_takes_the_lowest_free_number_inside_the_range()
        {
            var ledger = new HarnessNumberLedger(Range());

            var allocation = ledger.Allocate("FC", new[]
            {
                Harness("FC_A", ObjectKind.Function, 900),
                Harness("FC_B", ObjectKind.Function, 901),
            });

            Assert.True(allocation.Granted);
            Assert.Equal(902, allocation.Number);
            Assert.Equal("FC 902", allocation.Slot);
        }

        [Fact]
        public void An_allocation_checks_EVERY_numbered_object_not_only_the_harness_own()
        {
            // Checking only the harness's own objects allocates straight onto a deliverable squatting in
            // the range — the collision this exists to prevent, arriving from inside.
            var ledger = new HarnessNumberLedger(Range());

            var allocation = ledger.Allocate("FC", new[]
            {
                Deliverable("FC_PlantLogic", ObjectKind.Function, 900),
            });

            Assert.True(allocation.Granted);
            Assert.Equal(901, allocation.Number);
        }

        [Fact]
        public void A_space_the_reservation_does_not_cover_gets_no_number_at_all()
        {
            var ledger = new HarnessNumberLedger(Range(900, 949, "FC"));

            var fc = ledger.Allocate("FC", null);
            var db = ledger.Allocate("DB", null);

            Assert.True(fc.Granted);
            Assert.False(db.Granted);
            Assert.Contains("does not cover the DB space", db.Reason, StringComparison.Ordinal);
        }

        [Fact]
        public void An_allocation_with_no_number_space_is_refused()
        {
            // FB, FC, OB and DB are numbered independently, so "the next free number" has no answer
            // without one.
            var refused = new HarnessNumberLedger(Range()).Allocate("  ", null);

            Assert.False(refused.Granted);
            Assert.Contains("No number space was named", refused.Reason, StringComparison.Ordinal);
        }

        // =============================================================================================
        // THE RESERVATION IS A DECLARATION, AND IT MUST BE MADE BY SOMEBODY
        // =============================================================================================

        [Fact]
        public void A_reservation_that_names_no_number_space_reserves_nothing_and_is_refused()
        {
            Assert.Throws<ArgumentException>(() => new HarnessNumberRange(900, 949, new string[0], "signer", "why"));
            Assert.Throws<ArgumentException>(() => new HarnessNumberRange(900, 949, new[] { "  " }, "signer", "why"));
        }

        [Fact]
        public void A_reservation_nobody_declared_is_a_convention_and_is_refused()
        {
            Assert.Throws<ArgumentException>(() => new HarnessNumberRange(900, 949, new[] { "FC" }, "   ", "why"));
        }

        [Fact]
        public void An_empty_or_impossible_range_is_refused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HarnessNumberRange(949, 900, new[] { "FC" }, "s", "w"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HarnessNumberRange(0, 949, new[] { "FC" }, "s", "w"));
        }

        [Fact]
        public void The_number_spaces_are_independent_so_FC_910_and_DB_910_are_not_a_collision()
        {
            var ledger = new HarnessNumberLedger(Range());

            var findings = ledger.Audit(new[]
            {
                Harness("FC_Copy", ObjectKind.Function, 910),
                Harness("DB_Mirror", ObjectKind.GlobalDataBlock, 910),
            }).Findings;

            Assert.DoesNotContain(findings, f => f.Defect == NumberDefect.DuplicateNumber);
        }

        [Fact]
        public void A_global_and_an_instance_DB_share_one_number_space()
        {
            var ledger = new HarnessNumberLedger(Range());

            var findings = ledger.Audit(new[]
            {
                Harness("DB_Mirror", ObjectKind.GlobalDataBlock, 910),
                Harness("iDB_Model", ObjectKind.InstanceDataBlock, 910),
            }).Findings;

            Assert.Contains(findings, f => f.Defect == NumberDefect.DuplicateNumber);
        }

        // =============================================================================================
        // THE AUDIT — AND THE FOUR FINDINGS THAT MUST NOT COLLAPSE
        // =============================================================================================

        [Fact]
        public void The_measured_duplicate_is_caught()
        {
            // MEASURED 2026-08-13: TIA accepted an import declaring FC 910 while another block held 910
            // and created both — import exit 0, per-block compile CONSISTENT: yes, device compile
            // Success, sanity-check OVERALL: HEALTHY.
            var ledger = new HarnessNumberLedger(Range());

            var findings = ledger.Audit(new[]
            {
                Harness("FC_CopyLayer", ObjectKind.Function, 910),
                Deliverable("FC_PlantLogic", ObjectKind.Function, 910),
            }).Findings;

            Assert.Contains(findings, f => f.Defect == NumberDefect.DuplicateNumber && f.Subject == "FC 910");
            Assert.Contains(findings, f => f.Defect == NumberDefect.RangeOccupiedByAnOutsider);
        }

        [Fact]
        public void A_harness_object_outside_the_range_is_the_state_that_makes_a_collision_possible()
        {
            var ledger = new HarnessNumberLedger(Range());

            var findings = ledger.Audit(new[] { Harness("FC_CopyLayer", ObjectKind.Function, 42) }).Findings;

            Assert.Contains(findings, f => f.Defect == NumberDefect.HarnessObjectOutsideTheRange);
            Assert.DoesNotContain(findings, f => f.Defect == NumberDefect.RangeOccupiedByAnOutsider);
        }

        [Fact]
        public void A_deliverable_inside_the_range_is_a_squatter_and_a_DIFFERENT_finding()
        {
            var ledger = new HarnessNumberLedger(Range());

            var findings = ledger.Audit(new[] { Deliverable("FC_PlantLogic", ObjectKind.Function, 910) }).Findings;

            Assert.Contains(findings, f => f.Defect == NumberDefect.RangeOccupiedByAnOutsider);
            Assert.DoesNotContain(findings, f => f.Defect == NumberDefect.HarnessObjectOutsideTheRange);
        }

        [Fact]
        public void A_reservation_for_a_vanished_object_is_NOT_the_same_finding_as_a_squatter()
        {
            // The fixes are opposite: release a reservation, versus move a block. `claims --check`
            // already draws this line and it must not be collapsed here.
            var ledger = new HarnessNumberLedger(Range());

            var findings = ledger.Audit(
                new[] { Harness("FC_CopyLayer", ObjectKind.Function, 900) },
                reservationsHeld: new[] { "FC 900", "FC 901" }).Findings;

            var vanished = findings.Where(f => f.Defect == NumberDefect.ReservationForAVanishedObject).ToArray();

            Assert.Single(vanished);
            Assert.Equal("FC 901", vanished[0].Subject);
            Assert.Contains("RELEASE", vanished[0].Detail, StringComparison.Ordinal);
            Assert.DoesNotContain(findings, f => f.Defect == NumberDefect.RangeOccupiedByAnOutsider);
        }

        [Fact]
        public void An_object_that_will_not_say_who_owns_it_is_refused_rather_than_assumed_deliverable()
        {
            var ledger = new HarnessNumberLedger(Range());

            var findings = ledger.Audit(new[] { new NumberedBlock("FC_Mystery", ObjectKind.Function, 910, BlockOwner.Unknown) }).Findings;

            Assert.Contains(findings, f => f.Defect == NumberDefect.OwnerNotStated);
            Assert.Equal(BlockOwner.Unknown, default(BlockOwner));

            // ... and it is not ALSO reported as a squatter, which would be guessing which side it is on.
            Assert.DoesNotContain(findings, f => f.Defect == NumberDefect.RangeOccupiedByAnOutsider);
        }

        [Fact]
        public void A_kind_that_carries_no_block_number_is_reported_rather_than_silently_skipped()
        {
            var ledger = new HarnessNumberLedger(Range());

            var findings = ledger.Audit(new[] { new NumberedBlock("UDT_Motor", ObjectKind.DataType, 910, BlockOwner.Harness) }).Findings;

            Assert.Contains(findings, f => f.Defect == NumberDefect.NotANumberedKind);
        }

        [Fact]
        public void A_correctly_numbered_project_produces_no_findings()
        {
            // The did-not-run case for the whole audit: without this, every assertion above would pass
            // against an implementation that simply reported everything.
            var ledger = new HarnessNumberLedger(Range());

            var findings = ledger.Audit(
                new[]
                {
                    Harness("FC_CopyLayer", ObjectKind.Function, 900),
                    Harness("DB_Mirror", ObjectKind.GlobalDataBlock, 900),
                    Deliverable("FC_PlantLogic", ObjectKind.Function, 42),
                    Deliverable("FB_Motor", ObjectKind.FunctionBlock, 7),
                },
                reservationsHeld: new[] { "FC 900", "DB 900" }).Findings;

            Assert.Empty(findings);
        }

        // =============================================================================================
        // THE DECLARED BAND, AND THE OB CARVE-OUT
        // =============================================================================================

        [Fact]
        public void The_declared_band_is_9000_to_9999_over_FB_FC_and_DB_only()
        {
            var declared = HarnessNumberRange.Declared();

            Assert.Equal(9000, declared.FirstNumber);
            Assert.Equal(9999, declared.LastNumber);
            Assert.Equal(1000, declared.Capacity);
            Assert.Equal(new[] { "DB", "FB", "FC" }, declared.NumberSpaces.ToArray());

            // *** OB IS EXCLUDED. *** An OB's number is fixed by its event class — identified, not
            // chosen — so a band applied to OBs would be violated by OB80, which the spec names as a
            // harness object.
            Assert.False(declared.CoversSpace("OB"));

            // It is a ruling with a declarer, which is the whole difference from a convention.
            Assert.Contains("coordinator", declared.DeclaredBy, StringComparison.Ordinal);
        }

        [Fact]
        public void The_band_is_consumed_from_one_place_so_overturning_it_is_one_line()
        {
            // Two calls must agree; a band hardcoded at call sites would not have to.
            var first = HarnessNumberRange.Declared();
            var second = HarnessNumberRange.Declared();

            Assert.Equal(first.Describe(), second.Describe());
            Assert.Equal(9000, first.AllocationFloor);
        }

        [Fact]
        public void A_CORRECT_project_containing_OB80_produces_NO_findings_and_names_the_exemption()
        {
            // *** THE FALSE-FINDING GUARD, AND IT IS THE POINT OF THE CARVE-OUT. *** The spec names
            // OB80 as a harness-generated object. Without the carve-out it would fall through to
            // HarnessObjectOutsideTheRange, and the first false finding is what gets an audit switched
            // off.
            var ledger = new HarnessNumberLedger(HarnessNumberRange.Declared());

            var report = ledger.Audit(new[]
            {
                Harness("OB80_CycleInterrupt", ObjectKind.OrganizationBlock, 80),
                Harness("FC_CopyLayer", ObjectKind.Function, 9000),
                Harness("DB_Mirror", ObjectKind.GlobalDataBlock, 9000),
                Deliverable("FC_PlantLogic", ObjectKind.Function, 42),
                Deliverable("OB1_Main", ObjectKind.OrganizationBlock, 1),
            });

            Assert.Empty(report.Findings);
            Assert.True(report.Clean);
        }

        [Fact]
        public void An_exempted_OB_is_ENUMERATED_rather_than_silently_skipped()
        {
            // A silent exemption and a correct pass produce the same empty finding list. The exemption
            // is therefore part of the result.
            var ledger = new HarnessNumberLedger(HarnessNumberRange.Declared());

            var report = ledger.Audit(new[]
            {
                Harness("OB80_CycleInterrupt", ObjectKind.OrganizationBlock, 80),
                Harness("FC_CopyLayer", ObjectKind.Function, 9000),
            });

            Assert.Single(report.ExemptedOrganizationBlocks);
            Assert.Contains("OB80_CycleInterrupt", report.ExemptedOrganizationBlocks[0], StringComparison.Ordinal);
            Assert.Contains("EXEMPT OB80_CycleInterrupt", report.Describe(), StringComparison.Ordinal);
            Assert.Contains("fixed by its event class", report.Describe(), StringComparison.Ordinal);
        }

        [Fact]
        public void Two_OBs_at_one_number_are_not_reported_by_this_audit_because_the_band_does_not_govern_them()
        {
            // Stated so the limit is visible rather than assumed: OB numbering is the event class's,
            // and a duplicate OB is a different problem for a different check.
            var ledger = new HarnessNumberLedger(HarnessNumberRange.Declared());

            var report = ledger.Audit(new[]
            {
                Harness("OB80_A", ObjectKind.OrganizationBlock, 80),
                Harness("OB80_B", ObjectKind.OrganizationBlock, 80),
            });

            Assert.Empty(report.Findings);
            Assert.Equal(2, report.ExemptedOrganizationBlocks.Count);
        }

        [Fact]
        public void An_audit_that_examined_nothing_says_so_and_is_not_clean()
        {
            var report = new HarnessNumberLedger(HarnessNumberRange.Declared()).Audit(null);

            Assert.True(report.NothingExamined);
            Assert.False(report.Clean);
            Assert.Empty(report.Findings);
            Assert.Contains("NOTHING EXAMINED", report.Describe(), StringComparison.Ordinal);
        }

        [Fact]
        public void A_harness_FC_outside_the_declared_band_is_still_caught()
        {
            // The did-not-run case for the carve-out: it must exempt OBs and NOTHING ELSE.
            var ledger = new HarnessNumberLedger(HarnessNumberRange.Declared());

            var report = ledger.Audit(new[] { Harness("FC_CopyLayer", ObjectKind.Function, 910) });

            Assert.Contains(report.Findings, f => f.Defect == NumberDefect.HarnessObjectOutsideTheRange);
            Assert.Empty(report.ExemptedOrganizationBlocks);
        }

        // =============================================================================================
        // INTEROPERATION WITH THE EXISTING CLAIM REGISTRY
        // =============================================================================================

        [Fact]
        public void The_range_renders_a_claim_invocation_rather_than_keeping_a_registry_of_its_own()
        {
            // `converter claim` already solves the multi-agent half, and its --claims dir is required
            // with no default because a per-worktree one grants every claim and looks like success.
            var range = Range();
            var arguments = range.ClaimArgumentsFor(Harness("FC_CopyLayer", ObjectKind.Function, 910), "coordinator-1");

            Assert.Contains("claim --agent coordinator-1", arguments, StringComparison.Ordinal);
            Assert.Contains("--kind block-number", arguments, StringComparison.Ordinal);
            Assert.Contains("--value FC910", arguments, StringComparison.Ordinal);
            Assert.Contains("FC_CopyLayer", arguments, StringComparison.Ordinal);
        }

        [Fact]
        public void A_claim_invocation_cannot_be_rendered_without_an_agent_id()
        {
            Assert.Throws<ArgumentException>(
                () => Range().ClaimArgumentsFor(Harness("FC_CopyLayer", ObjectKind.Function, 910), "  "));
        }

        [Fact]
        public void The_allocation_floor_is_exposed_for_the_claim_tools_own_allocate_path()
        {
            Assert.Equal(900, Range().AllocationFloor);
        }

        [Fact]
        public void The_reservation_describes_itself_for_a_listing_a_human_reads()
        {
            var described = Range(900, 949, "FC", "DB").Describe();

            Assert.Contains("DB/FC 900-949", described, StringComparison.Ordinal);
            Assert.Contains("gate-1-signer", described, StringComparison.Ordinal);
        }
    }
}
