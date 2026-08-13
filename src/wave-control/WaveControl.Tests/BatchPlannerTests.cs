using System;
using System.Collections.Generic;
using System.Linq;

namespace Ladder.Wave.Tests
{
    /// <summary>
    /// DB-4 — at most twenty DEPENDENCY-CLOSED objects per wave-boundary download.
    /// </summary>
    public sealed class BatchPlannerTests
    {
        // --- THE CLOSURE VERIFIER, AND THE PROOF THAT ITS GUARDS BITE --------------------------------

        [Fact]
        public void The_real_closure_checker_satisfies_the_contract()
        {
            ClosureChecker real = DependencyClosure.Verify;

            ClosureContract.ASplitDependencyGroupMustBeCaught(real);
            ClosureContract.ADependencyThatExistsNowhereMustBeCaught(real);
            ClosureContract.AnObjectInNoBatchMustBeCaught(real);
            ClosureContract.AClosedPlanMustProduceNoViolations(real);
            ClosureContract.ADeployedDependencyMustNotBeAViolation(real);
        }

        [Fact]
        public void A_checker_that_only_counts_objects_fails_the_contract()
        {
            // The reading of DB-4 that notices the number twenty and not the words "dependency-closed".
            Assert.ThrowsAny<Exception>(
                () => ClosureContract.ASplitDependencyGroupMustBeCaught(MutantClosureCheckers.SizeOnly));

            Assert.ThrowsAny<Exception>(
                () => ClosureContract.ADependencyThatExistsNowhereMustBeCaught(MutantClosureCheckers.SizeOnly));
        }

        [Fact]
        public void A_checker_that_asks_only_whether_the_dependency_is_somewhere_in_the_plan_fails_the_contract()
        {
            // The mutant that shares the packer's blind spot: true of every plan the packer can produce,
            // split or not, so it would agree with a broken packer forever.
            Assert.ThrowsAny<Exception>(
                () => ClosureContract.ASplitDependencyGroupMustBeCaught(MutantClosureCheckers.SomewhereInThePlanIsGoodEnough));

            Assert.ThrowsAny<Exception>(
                () => ClosureContract.AnObjectInNoBatchMustBeCaught(MutantClosureCheckers.SomewhereInThePlanIsGoodEnough));
        }

        // --- THE PLANNER -----------------------------------------------------------------------------

        [Fact]
        public void An_fb_its_instance_db_and_its_udt_land_in_one_batch()
        {
            var plan = WaveBoundaryBatchPlanner.Plan(ClosureContract.FbWithIdbAndUdt(), ChangeSets.Deployed());

            Assert.Equal(BatchPlanOutcome.Planned, plan.Outcome);
            Assert.Single(plan.Batches);
            Assert.Equal(3, plan.Batches[0].Count);
        }

        [Fact]
        public void Unrelated_objects_may_be_spread_across_batches_but_a_group_may_not_be()
        {
            // Six independent objects, limit two: three batches. Then the same six with one dependency
            // edge joining two of them — that pair must stay together in whichever batch it lands in.
            var independent = Enumerable.Range(1, 6).Select(i => ChangeSets.Fb("FB_" + i)).ToArray();

            var plan = WaveBoundaryBatchPlanner.Plan(independent, ChangeSets.Deployed(), maxObjectsPerBatch: 2);

            Assert.Equal(BatchPlanOutcome.Planned, plan.Outcome);
            Assert.Equal(3, plan.Batches.Count);
            Assert.All(plan.Batches, b => Assert.True(b.Count <= 2));
            Assert.Equal(6, plan.PlannedObjectCount);

            var joined = independent.Take(4)
                .Concat(new[] { ChangeSets.Fb("FB_5", ChangeSets.Hash, "FB_6"), ChangeSets.Fb("FB_6") })
                .ToArray();

            var joinedPlan = WaveBoundaryBatchPlanner.Plan(joined, ChangeSets.Deployed(), maxObjectsPerBatch: 2);

            Assert.Equal(BatchPlanOutcome.Planned, joinedPlan.Outcome);
            var pairBatch = joinedPlan.Batches.Single(b => b.ObjectNames.Contains("FB_5", StringComparer.OrdinalIgnoreCase));
            Assert.Contains("FB_6", pairBatch.ObjectNames, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void Every_planned_batch_is_within_the_limit_and_the_plan_covers_every_object()
        {
            var objects = Enumerable.Range(1, 47).Select(i => ChangeSets.Fb("FB_" + i)).ToArray();

            var plan = WaveBoundaryBatchPlanner.Plan(objects, ChangeSets.Deployed());

            Assert.Equal(BatchPlanOutcome.Planned, plan.Outcome);
            Assert.All(plan.Batches, b => Assert.True(b.Count <= WaveBoundaryBatchPlanner.DefaultMaxObjectsPerBatch));
            Assert.Equal(47, plan.PlannedObjectCount);
            Assert.Equal(47, plan.Batches.SelectMany(b => b.ObjectNames).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }

        [Fact]
        public void A_dependency_group_larger_than_the_limit_is_a_refusal_not_a_split()
        {
            // DB-1's blast radius: a modified UDT is RUN (Init) on EVERY DB built on it. One UDT with
            // twenty dependent DBs is a twenty-one object group from a single edit, and closure over a
            // partition means there is no consistent download of it at all.
            var udt = ChangeSets.Udt("UDT_Shared");
            var dbs = Enumerable.Range(1, 20).Select(i => ChangeSets.GlobalDb("DB_" + i, ChangeSets.Hash, "UDT_Shared"));

            var plan = WaveBoundaryBatchPlanner.Plan(new[] { udt }.Concat(dbs), ChangeSets.Deployed());

            Assert.Equal(BatchPlanOutcome.Refused, plan.Outcome);
            Assert.Contains(plan.Refusals, r => r.Reason == BatchRefusalReason.DependencyGroupExceedsObjectLimit);
            Assert.Empty(plan.Batches);
        }

        [Fact]
        public void A_group_exactly_at_the_limit_is_planned()
        {
            var udt = ChangeSets.Udt("UDT_Shared");
            var dbs = Enumerable.Range(1, 19).Select(i => ChangeSets.GlobalDb("DB_" + i, ChangeSets.Hash, "UDT_Shared"));

            var plan = WaveBoundaryBatchPlanner.Plan(new[] { udt }.Concat(dbs), ChangeSets.Deployed());

            Assert.Equal(BatchPlanOutcome.Planned, plan.Outcome);
            Assert.Single(plan.Batches);
            Assert.Equal(20, plan.Batches[0].Count);
        }

        [Fact]
        public void A_dependency_that_is_neither_changed_nor_deployed_is_refused()
        {
            var plan = WaveBoundaryBatchPlanner.Plan(
                new[] { ChangeSets.Fb("FB_Motor", ChangeSets.Hash, "UDT_Absent") },
                ChangeSets.Deployed("UDT_Something_Else"));

            Assert.Equal(BatchPlanOutcome.Refused, plan.Outcome);
            Assert.Contains(plan.Refusals, r => r.Reason == BatchRefusalReason.UnresolvedDependency);
        }

        [Fact]
        public void A_dependency_already_on_the_device_does_not_have_to_be_in_the_batch()
        {
            var plan = WaveBoundaryBatchPlanner.Plan(
                new[] { ChangeSets.Fb("FB_Motor", ChangeSets.Hash, "UDT_Motor") },
                ChangeSets.Deployed("UDT_Motor"));

            Assert.Equal(BatchPlanOutcome.Planned, plan.Outcome);
            Assert.Single(plan.Batches);
            Assert.Equal(new[] { "FB_Motor" }, plan.Batches[0].ObjectNames.ToArray());
        }

        [Fact]
        public void A_stop_class_object_in_a_wave_boundary_batch_is_refused()
        {
            // The backstop for a routing bug. Its consequence, uncaught, is a CPU stopped mid-wave.
            var plan = WaveBoundaryBatchPlanner.Plan(
                new[] { ChangeSets.Fb("FB_Motor"), ChangeSets.Ob("OB_Cyclic") },
                ChangeSets.Deployed());

            Assert.Equal(BatchPlanOutcome.Refused, plan.Outcome);
            Assert.Contains(plan.Refusals, r => r.Reason == BatchRefusalReason.StopClassObjectInAWaveBoundaryBatch);
        }

        [Fact]
        public void An_unclassified_object_never_reaches_a_batch_either()
        {
            var plan = WaveBoundaryBatchPlanner.Plan(
                new[] { new ChangedObject("FB_Mystery", ObjectKind.FunctionBlock, ChangeClass.Unknown) },
                ChangeSets.Deployed());

            Assert.Equal(BatchPlanOutcome.Refused, plan.Outcome);
            Assert.Contains(plan.Refusals, r => r.Reason == BatchRefusalReason.StopClassObjectInAWaveBoundaryBatch);
        }

        [Fact]
        public void A_duplicate_or_unnamed_object_is_refused()
        {
            var duplicated = WaveBoundaryBatchPlanner.Plan(
                new[] { ChangeSets.Fb("FB_A"), ChangeSets.Fb("fb_a") },
                ChangeSets.Deployed());

            Assert.Equal(BatchPlanOutcome.Refused, duplicated.Outcome);
            Assert.Contains(duplicated.Refusals, r => r.Reason == BatchRefusalReason.DuplicateObjectName);

            var unnamed = WaveBoundaryBatchPlanner.Plan(
                new[] { new ChangedObject("  ", ObjectKind.FunctionBlock, ChangeClass.Run) },
                ChangeSets.Deployed());

            Assert.Equal(BatchPlanOutcome.Refused, unnamed.Outcome);
            Assert.Contains(unnamed.Refusals, r => r.Reason == BatchRefusalReason.UnnamedObject);
        }

        // --- NOTHING EXAMINED IS NOT A PASS ----------------------------------------------------------

        [Fact]
        public void An_empty_change_set_is_reported_as_nothing_to_batch_never_as_a_plan()
        {
            var plan = WaveBoundaryBatchPlanner.Plan(new ChangedObject[0], ChangeSets.Deployed());

            Assert.Equal(BatchPlanOutcome.NothingToBatch, plan.Outcome);
            Assert.False(plan.Usable);
            Assert.Empty(plan.Batches);
            Assert.Equal(0, plan.ObjectsExamined);
            Assert.Contains("NOTHING TO BATCH", plan.ToLogLine(), StringComparison.Ordinal);
        }

        [Fact]
        public void Nothing_to_batch_is_the_zero_value_of_the_outcome()
        {
            Assert.Equal(BatchPlanOutcome.NothingToBatch, default(BatchPlanOutcome));
        }

        [Fact]
        public void A_plan_can_never_be_built_with_zero_batches()
        {
            Assert.Throws<InvalidOperationException>(
                () => BatchPlan.Planned(new DownloadBatch[0], 20, 3, "should be impossible"));
        }

        [Fact]
        public void A_refusal_can_never_be_built_with_no_reasons()
        {
            Assert.Throws<InvalidOperationException>(
                () => BatchPlan.Refuse(new BatchRefusal[0], 20, 3, "should be impossible"));
        }

        // --- AN EMPTY BASELINE MUST BE DECLARED ------------------------------------------------------

        [Fact]
        public void An_empty_deployed_baseline_must_be_declared_rather_than_handed_over_as_an_empty_list()
        {
            // "The device holds nothing" and "we could not read what the device holds" are the same
            // empty list and opposite decisions.
            Assert.Throws<ArgumentException>(() => DeployedProgram.From(new string[0], "a read that returned nothing"));
            Assert.Throws<ArgumentException>(() => DeployedProgram.From(new[] { "FB_A" }, "   "));
            Assert.Throws<ArgumentException>(() => DeployedProgram.Empty("  "));

            var declared = DeployedProgram.Empty("a bare test project");
            Assert.True(declared.DeclaredEmpty);
            Assert.Equal(0, declared.Count);
        }

        [Fact]
        public void The_planner_refuses_to_run_without_a_baseline()
        {
            Assert.Throws<ArgumentNullException>(
                () => WaveBoundaryBatchPlanner.Plan(new[] { ChangeSets.Fb("FB_A") }, null!));
        }

        // --- COST IS NOT UNIFORM, AND THIS LIBRARY DOES NOT PREDICT IT -------------------------------

        [Fact]
        public void No_planned_batch_claims_to_know_whether_it_stops_the_cpu()
        {
            // Measured 2026-08-13: a two-object download loaded into a RUNNING CPU with no stop/start
            // cycle at all, while an earlier nineteen-object download required the stop. So the stop is
            // a function of WHAT changed — not of downloading, and not of object count. The determining
            // property is unmeasured, so no batch here predicts it in either direction.
            var small = WaveBoundaryBatchPlanner.Plan(new[] { ChangeSets.Fb("FB_A"), ChangeSets.Fb("FB_B") }, ChangeSets.Deployed());
            var large = WaveBoundaryBatchPlanner.Plan(
                Enumerable.Range(1, 19).Select(i => ChangeSets.Fb("FB_" + i)).ToArray(),
                ChangeSets.Deployed());

            foreach (var batch in small.Batches.Concat(large.Batches))
            {
                Assert.Equal(CpuStopRequirement.Undetermined, batch.CpuStop);
            }

            Assert.Equal(CpuStopRequirement.Undetermined, default(CpuStopRequirement));
        }

        [Fact]
        public void A_batch_carrying_a_run_init_change_says_so_because_it_invalidates_earlier_results()
        {
            var plan = WaveBoundaryBatchPlanner.Plan(
                new[] { ChangeSets.GlobalDb("DB_Recipe") },
                ChangeSets.Deployed());

            Assert.True(plan.Batches[0].ResetsData);
            Assert.Contains("RESETS DATA", plan.Batches[0].ToLogLine(), StringComparison.Ordinal);
        }

        // --- DETERMINISM -----------------------------------------------------------------------------

        [Fact]
        public void The_same_change_set_produces_the_same_plan()
        {
            // Two runs of the same wave must be comparable, which they are not if the packer's output
            // depends on hash-table ordering.
            var objects = Enumerable.Range(1, 25).Select(i => ChangeSets.Fb("FB_" + i)).ToArray();

            var first = WaveBoundaryBatchPlanner.Plan(objects, ChangeSets.Deployed());
            var second = WaveBoundaryBatchPlanner.Plan(objects.Reverse().ToArray(), ChangeSets.Deployed());

            Assert.Equal(
                first.Batches.Select(b => string.Join(",", b.ObjectNames.ToArray())).ToArray(),
                second.Batches.Select(b => string.Join(",", b.ObjectNames.ToArray())).ToArray());
        }
    }
}
