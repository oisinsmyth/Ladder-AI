using System;
using System.Collections.Generic;
using System.Linq;

namespace Ladder.Wave.Tests
{
    /// <summary>A closure checker, in the shape both the real one and the mutants below wear.</summary>
    internal delegate IReadOnlyList<ClosureViolation> ClosureChecker(
        IEnumerable<IEnumerable<string>> batches,
        IEnumerable<ChangedObject> changeSet,
        DeployedProgram deployed);

    /// <summary>
    /// The closure verifier's load-bearing assertions, factored out so the SAME assertions can be run
    /// against the real one (where they must pass) and against deliberately wrong ones (where they must
    /// go red).
    /// </summary>
    /// <remarks>
    /// A verifier that only ever sees correct plans has not been tested. Worse, the specific risk here
    /// is a check that shares its subject's blind spot: the packer builds batches from connected
    /// components and therefore "knows" they are closed, so a verifier phrased in the same terms would
    /// agree with it while both were wrong. These assertions are phrased in terms of the DEFINITION —
    /// where a dependency ended up — and the mutants are the proof that they bite.
    /// </remarks>
    internal static class ClosureContract
    {
        /// <summary>
        /// DB-4's own example: an FB, its instance DB and its UDT. Split across two batches they land in
        /// different downloads and the program is inconsistent in between.
        /// </summary>
        public static ChangedObject[] FbWithIdbAndUdt() => new[]
        {
            ChangeSets.Udt("UDT_Motor"),
            ChangeSets.Fb("FB_Motor", ChangeSets.Hash, "UDT_Motor"),
            ChangeSets.InstanceDb("iDB_Motor", "FB_Motor"),
        };

        /// <summary>THE GUARD: a dependency in another batch is a violation.</summary>
        public static void ASplitDependencyGroupMustBeCaught(ClosureChecker check)
        {
            var changeSet = FbWithIdbAndUdt();
            var split = new[]
            {
                new[] { "UDT_Motor", "FB_Motor" },
                new[] { "iDB_Motor" },
            };

            var violations = check(split, changeSet, ChangeSets.Deployed());

            Assert.Contains(
                violations,
                v => v.Kind == ClosureViolationKind.DependencyInAnotherBatch &&
                     string.Equals(v.ObjectName, "iDB_Motor", StringComparison.OrdinalIgnoreCase) &&
                     string.Equals(v.DependencyName, "FB_Motor", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>THE SECOND GUARD: a dependency that exists nowhere is a violation, and a DIFFERENT one.</summary>
        public static void ADependencyThatExistsNowhereMustBeCaught(ClosureChecker check)
        {
            var changeSet = new[] { ChangeSets.Fb("FB_Motor", ChangeSets.Hash, "UDT_Absent") };
            var batches = new[] { new[] { "FB_Motor" } };

            var violations = check(batches, changeSet, ChangeSets.Deployed());

            Assert.Contains(
                violations,
                v => v.Kind == ClosureViolationKind.DependencyPresentNowhere &&
                     string.Equals(v.DependencyName, "UDT_Absent", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>THE THIRD GUARD: a plan that silently drops an object is a violation.</summary>
        public static void AnObjectInNoBatchMustBeCaught(ClosureChecker check)
        {
            var changeSet = new[] { ChangeSets.Fb("FB_A"), ChangeSets.Fb("FB_B") };
            var batches = new[] { new[] { "FB_A" } };

            var violations = check(batches, changeSet, ChangeSets.Deployed());

            Assert.Contains(
                violations,
                v => v.Kind == ClosureViolationKind.ObjectInNoBatch &&
                     string.Equals(v.ObjectName, "FB_B", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>AND THE CONVERSE: a genuinely closed plan produces nothing.</summary>
        public static void AClosedPlanMustProduceNoViolations(ClosureChecker check)
        {
            var changeSet = FbWithIdbAndUdt();
            var together = new[] { new[] { "UDT_Motor", "FB_Motor", "iDB_Motor" } };

            Assert.Empty(check(together, changeSet, ChangeSets.Deployed()));
        }

        /// <summary>A dependency already on the device does NOT have to be in the batch.</summary>
        public static void ADeployedDependencyMustNotBeAViolation(ClosureChecker check)
        {
            var changeSet = new[] { ChangeSets.Fb("FB_Motor", ChangeSets.Hash, "UDT_Motor") };
            var batches = new[] { new[] { "FB_Motor" } };

            Assert.Empty(check(batches, changeSet, ChangeSets.Deployed("UDT_Motor")));
        }
    }

    /// <summary>
    /// Deliberately WRONG closure checkers, kept so the guards in <see cref="ClosureContract"/> can be
    /// shown to catch the mistakes they exist to catch.
    /// </summary>
    /// <remarks>
    /// *** NOTHING IN PRODUCTION CODE REFERENCES THESE. *** Same argument as
    /// <see cref="MutantClassifiers"/>: a test asserting "the split plan is rejected" passes just as
    /// green whether it is checking anything or not, and the only way to know is to point it at an
    /// implementation that gets it wrong and watch it fail.
    /// </remarks>
    internal static class MutantClosureCheckers
    {
        /// <summary>
        /// THE MISTAKE THIS COMPONENT EXISTS TO PREVENT: a "check" that only counts objects per batch.
        /// It is exactly what a reading of DB-4 that noticed the number twenty and not the word
        /// "dependency-closed" would produce, and it passes every split plan.
        /// </summary>
        public static IReadOnlyList<ClosureViolation> SizeOnly(
            IEnumerable<IEnumerable<string>> batches,
            IEnumerable<ChangedObject> changeSet,
            DeployedProgram deployed)
        {
            var violations = new List<ClosureViolation>();

            foreach (var batch in batches)
            {
                var names = batch.ToArray();
                if (names.Length > WaveBoundaryBatchPlanner.DefaultMaxObjectsPerBatch)
                {
                    violations.Add(new ClosureViolation(
                        ClosureViolationKind.ObjectInMoreThanOneBatch,
                        names[0],
                        string.Empty,
                        "[MUTANT] over the limit"));
                }
            }

            return violations;
        }

        /// <summary>
        /// The subtler mistake, and the one this design is actually exposed to: a checker that shares the
        /// packer's reasoning. It asks "is every dependency somewhere in the plan?" rather than "is it in
        /// THIS batch?" — which is true of every plan the packer could ever produce, split or not.
        /// </summary>
        public static IReadOnlyList<ClosureViolation> SomewhereInThePlanIsGoodEnough(
            IEnumerable<IEnumerable<string>> batches,
            IEnumerable<ChangedObject> changeSet,
            DeployedProgram deployed)
        {
            var batchList = batches.Select(b => b.ToArray()).ToArray();
            var everywhere = new HashSet<string>(batchList.SelectMany(b => b), ChangedObject.NameComparer);
            var violations = new List<ClosureViolation>();

            foreach (var o in changeSet)
            {
                foreach (var dependency in o.DependsOn)
                {
                    if (!everywhere.Contains(dependency) && !deployed.Contains(dependency))
                    {
                        violations.Add(new ClosureViolation(
                            ClosureViolationKind.DependencyPresentNowhere,
                            o.Name,
                            dependency,
                            "[MUTANT] not anywhere in the plan"));
                    }
                }
            }

            return violations;
        }
    }
}
