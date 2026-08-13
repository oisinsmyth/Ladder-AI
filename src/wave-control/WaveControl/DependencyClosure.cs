using System;
using System.Collections.Generic;
using System.Linq;

namespace Ladder.Wave
{
    /// <summary>What kind of closure violation was found.</summary>
    public enum ClosureViolationKind
    {
        /// <summary>Not a violation.</summary>
        None = 0,

        /// <summary>
        /// The dependency is IN THE CHANGE SET but in a DIFFERENT batch, so the two would land in
        /// different downloads. DB-4: "an FB, its instance DB and its UDT must land together or the
        /// program is inconsistent between downloads."
        /// </summary>
        DependencyInAnotherBatch = 1,

        /// <summary>
        /// The dependency is in NO batch and NOT on the device — it exists nowhere the download can
        /// reach. This is a different fault from the one above and calls for a different fix: the first
        /// is a packing bug, this is a change set that was assembled wrong (or a baseline that is
        /// wrong). Conflating them is the mistake D32 caveat 1 warns about in its own domain.
        /// </summary>
        DependencyPresentNowhere = 2,

        /// <summary>A changed object appears in no batch at all — it would silently not be downloaded.</summary>
        ObjectInNoBatch = 3,

        /// <summary>A changed object appears in more than one batch.</summary>
        ObjectInMoreThanOneBatch = 4,
    }

    /// <summary>One closure violation, naming the object, the dependency and what kind of fault it is.</summary>
    public sealed class ClosureViolation
    {
        internal ClosureViolation(ClosureViolationKind kind, string objectName, string dependencyName, string detail)
        {
            Kind = kind;
            ObjectName = objectName;
            DependencyName = dependencyName;
            Detail = detail;
        }

        /// <summary>Which kind of violation.</summary>
        public ClosureViolationKind Kind { get; }

        /// <summary>The object whose batch is not closed.</summary>
        public string ObjectName { get; }

        /// <summary>The dependency that is not in it; empty for the whole-object violations.</summary>
        public string DependencyName { get; }

        /// <summary>What is wrong.</summary>
        public string Detail { get; }

        /// <inheritdoc />
        public override string ToString() =>
            Kind + " [" + ObjectName + (DependencyName.Length == 0 ? string.Empty : " -> " + DependencyName) + "]: " + Detail;
    }

    /// <summary>
    /// THE DEFINITION OF DEPENDENCY-CLOSED, APPLIED TO A BATCH PLAN THAT ALREADY EXISTS.
    /// </summary>
    /// <remarks>
    /// <para>
    /// *** THIS TYPE IS WRITTEN FROM THE DEFINITION, NOT FROM THE PACKER'S REASONING, AND THAT
    /// SEPARATION IS THE POINT. *** The packer groups objects by connected component and then knows,
    /// by construction, that its batches are closed — so a check phrased in terms of components would
    /// share the packer's blind spot exactly, and the two would agree while both being wrong. This one
    /// re-derives the answer from the declared dependency lists and the deployed baseline alone. It
    /// would catch a packer that split a component, dropped an object, or emitted one twice.
    /// </para>
    /// <para>
    /// WHAT "DEPENDENCY-CLOSED" MEANS HERE, CONCRETELY. A batch is a set of CHANGED objects that will
    /// land in one download. It is closed when, for every object in it, every declared dependency is
    /// EITHER in the same batch OR already on the device. A dependency that is in the change set but a
    /// different batch breaks it — the two land in different downloads and the program is inconsistent
    /// in between. A dependency that is in neither breaks it more seriously, because nothing in the
    /// plan will ever satisfy it.
    /// </para>
    /// <para>
    /// AND THE CONSEQUENCE THAT FALLS OUT OF IT AND IS WORTH STATING PLAINLY: because batches PARTITION
    /// the change set, downward closure per batch forces every WEAKLY-CONNECTED COMPONENT of the change
    /// set's dependency graph into a single batch. If A and B both depend on C and A is placed
    /// separately from B, one of the two batches is missing C. So DB-4's twenty-object limit is a limit
    /// on COMPONENTS, not on objects that can be sliced freely — and a component larger than twenty is
    /// not a packing problem but a refusal.
    /// </para>
    /// </remarks>
    public static class DependencyClosure
    {
        /// <summary>
        /// Verify a whole plan: every changed object in exactly one batch, and every batch closed.
        /// </summary>
        /// <param name="batches">The proposed batches, as name sets.</param>
        /// <param name="changeSet">Every changed object, indexed by name by this method.</param>
        /// <param name="deployed">What is already on the device.</param>
        /// <returns>Every violation found. An empty list means the plan is closed.</returns>
        public static IReadOnlyList<ClosureViolation> Verify(
            IEnumerable<IEnumerable<string>> batches,
            IEnumerable<ChangedObject> changeSet,
            DeployedProgram deployed)
        {
            if (deployed == null)
            {
                throw new ArgumentNullException(nameof(deployed));
            }

            var objects = (changeSet ?? Enumerable.Empty<ChangedObject>()).Where(o => o != null).ToArray();
            var byName = new Dictionary<string, ChangedObject>(ChangedObject.NameComparer);
            foreach (var o in objects.Where(o => o.Name.Length > 0))
            {
                byName[o.Name] = o;
            }

            var batchList = (batches ?? Enumerable.Empty<IEnumerable<string>>())
                .Select(b => new HashSet<string>(
                    (b ?? Enumerable.Empty<string>()).Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()),
                    ChangedObject.NameComparer))
                .ToArray();

            var violations = new List<ClosureViolation>();

            // --- THE PARTITION CHECK. A plan that quietly drops an object downloads less than it was
            // asked to, and every other check would still pass on what remains.
            foreach (var name in byName.Keys)
            {
                var occurrences = batchList.Count(b => b.Contains(name));

                if (occurrences == 0)
                {
                    violations.Add(new ClosureViolation(
                        ClosureViolationKind.ObjectInNoBatch,
                        name,
                        string.Empty,
                        "The object is in the change set but in no batch, so the plan would not download " +
                        "it. Every other check would still pass on the objects that remain."));
                }
                else if (occurrences > 1)
                {
                    violations.Add(new ClosureViolation(
                        ClosureViolationKind.ObjectInMoreThanOneBatch,
                        name,
                        string.Empty,
                        "The object appears in " + occurrences + " batches. Batches must partition the " +
                        "change set; downloading it twice makes the object counts DB-4 budgets against wrong."));
                }
            }

            // --- THE CLOSURE CHECK ITSELF, one object at a time, from the declared dependencies.
            foreach (var batch in batchList)
            {
                CheckOneBatch(batch, byName, deployed, violations);
            }

            return violations;
        }

        /// <summary>
        /// Verify ONE batch against a change set and a baseline, ignoring the partition question — for
        /// a caller checking a batch it built itself.
        /// </summary>
        public static IReadOnlyList<ClosureViolation> VerifyOneBatch(
            IEnumerable<string> batch,
            IEnumerable<ChangedObject> changeSet,
            DeployedProgram deployed)
        {
            if (deployed == null)
            {
                throw new ArgumentNullException(nameof(deployed));
            }

            var byName = new Dictionary<string, ChangedObject>(ChangedObject.NameComparer);
            foreach (var o in (changeSet ?? Enumerable.Empty<ChangedObject>()).Where(o => o != null && o.Name.Length > 0))
            {
                byName[o.Name] = o;
            }

            var batchNames = new HashSet<string>(
                (batch ?? Enumerable.Empty<string>()).Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()),
                ChangedObject.NameComparer);

            var violations = new List<ClosureViolation>();
            CheckOneBatch(batchNames, byName, deployed, violations);
            return violations;
        }

        private static void CheckOneBatch(
            HashSet<string> batch,
            Dictionary<string, ChangedObject> byName,
            DeployedProgram deployed,
            List<ClosureViolation> violations)
        {
            foreach (var name in batch)
            {
                ChangedObject changedObject;
                if (!byName.TryGetValue(name, out changedObject))
                {
                    // A name in a batch that is in no change set. Reported rather than ignored: a batch
                    // naming something nobody submitted is a plan built from a stale set, and skipping
                    // it would make the plan look cleaner the more wrong it was.
                    violations.Add(new ClosureViolation(
                        ClosureViolationKind.DependencyPresentNowhere,
                        name,
                        string.Empty,
                        "The batch names '" + name + "', which is in no change set. A batch built from " +
                        "names nobody submitted cannot be checked against anything."));
                    continue;
                }

                foreach (var dependency in changedObject.DependsOn)
                {
                    if (batch.Contains(dependency))
                    {
                        continue;
                    }

                    if (byName.ContainsKey(dependency))
                    {
                        violations.Add(new ClosureViolation(
                            ClosureViolationKind.DependencyInAnotherBatch,
                            name,
                            dependency,
                            "'" + dependency + "' is in the change set but not in this batch, so the two " +
                            "would land in different downloads and the program would be inconsistent " +
                            "between them (DB-4)."));
                        continue;
                    }

                    if (deployed.Contains(dependency))
                    {
                        continue;
                    }

                    violations.Add(new ClosureViolation(
                        ClosureViolationKind.DependencyPresentNowhere,
                        name,
                        dependency,
                        "'" + dependency + "' is in no batch and not in the deployed baseline (" +
                        deployed + "), so nothing in this plan will ever satisfy it. Either the change " +
                        "set is incomplete, or the baseline is."));
                }
            }
        }
    }
}
