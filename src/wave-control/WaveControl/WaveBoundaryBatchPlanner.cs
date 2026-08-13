using System;
using System.Collections.Generic;
using System.Linq;

namespace Ladder.Wave
{
    /// <summary>
    /// DB-4 — batch a wave boundary's change set into AT MOST TWENTY DEPENDENCY-CLOSED OBJECTS per
    /// download. More than twenty changed objects cannot be integrated consistently in one program
    /// cycle [R], and "an FB, its instance DB and its UDT must land together or the program is
    /// inconsistent between downloads".
    /// </summary>
    /// <remarks>
    /// <para>
    /// *** WHAT DEPENDENCY-CLOSED MEANS, AND WHY IT MAKES THE LIMIT A LIMIT ON GROUPS RATHER THAN ON
    /// OBJECTS. *** A batch is closed when every declared dependency of every object in it is either in
    /// the same batch or already on the device. Batches partition the change set, so if A and B both
    /// depend on C, placing A and B in different batches leaves one of them without C — which means
    /// every WEAKLY-CONNECTED COMPONENT of the change set's dependency graph must land in ONE batch.
    /// The packer therefore packs components, not objects, and a component larger than the limit is
    /// *** A REFUSAL, NOT A WARNING AND NOT A SPLIT ***: there is no consistent download of it.
    /// </para>
    /// <para>
    /// That refusal is not hypothetical. DB-1's blast radius says a modified UDT is RUN (Init) on EVERY
    /// DB built on it, so one edit to a shared UDT with twenty dependent DBs produces a twenty-one
    /// object component. D16 already forbids revising a shared UDT mid-wave-set for a different reason;
    /// this is what the batcher does when one arrives anyway.
    /// </para>
    /// <para>
    /// *** THE PLANNER'S OWN OUTPUT IS CHECKED BY SOMETHING THAT DOES NOT SHARE ITS REASONING. *** The
    /// packer knows its batches are closed because it built them from components. That knowledge is
    /// exactly the blind spot to worry about, so the plan is verified by <see cref="DependencyClosure"/>,
    /// which re-derives closure from the declared dependency lists and the baseline alone and also
    /// checks that the batches partition the change set. A plan that fails that check is REFUSED — a
    /// plan whose closure could not be verified is as unusable as one known to be open.
    /// </para>
    /// <para>
    /// *** SCOPE: THE NON-DISRUPTIVE WAVE BOUNDARY, AND ONLY THAT. *** STOP-class objects are refused
    /// here rather than packed, because they do not flow through a wave boundary at all (D23) — they
    /// accumulate for the drain. Whether the twenty-object limit ALSO applies to the drain's full
    /// disruptive download is NOT settled by the spec: DB-4's mechanism is consistent integration "in
    /// one program cycle", which is a RUN-mode property, and a full download stops the CPU. That is an
    /// open question, deliberately not answered here and not silently assumed either way; the drain
    /// path does not call this planner.
    /// </para>
    /// <para>
    /// WHAT IT DOES NOT PREDICT: whether a batch stops the CPU. See <see cref="CpuStopRequirement"/> —
    /// measured 2026-08-13, a two-object download left a RUNNING CPU running while a nineteen-object
    /// one required the stop, so the stop is a function of what changed and the determining property is
    /// unmeasured. Every batch reports <see cref="CpuStopRequirement.Undetermined"/>, and no cost model
    /// here assumes uniform batch cost.
    /// </para>
    /// </remarks>
    public static class WaveBoundaryBatchPlanner
    {
        /// <summary>DB-4's limit [R]. More than this cannot be integrated consistently in one program cycle.</summary>
        public const int DefaultMaxObjectsPerBatch = 20;

        /// <summary>
        /// Plan the batches for one wave-boundary download.
        /// </summary>
        /// <param name="changeSet">The RUN-class objects to be downloaded.</param>
        /// <param name="deployed">What is already on the device — the baseline a dependency may be satisfied by.</param>
        /// <param name="maxObjectsPerBatch">DB-4's limit; overridable only so a test can exercise the boundary cheaply.</param>
        public static BatchPlan Plan(
            IEnumerable<ChangedObject>? changeSet,
            DeployedProgram deployed,
            int maxObjectsPerBatch = DefaultMaxObjectsPerBatch)
        {
            if (deployed == null)
            {
                throw new ArgumentNullException(
                    nameof(deployed),
                    "A batch plan needs a deployed-program baseline. Without one there is no way to tell " +
                    "a dependency that is already on the device from one that is missing, and assuming " +
                    "the generous answer is how a plan passes that could not be downloaded.");
            }

            if (maxObjectsPerBatch < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxObjectsPerBatch),
                    "The limit must be at least one object per batch.");
            }

            var objects = (changeSet ?? Enumerable.Empty<ChangedObject>()).Where(o => o != null).ToArray();

            if (objects.Length == 0)
            {
                return BatchPlan.NothingToBatch(
                    maxObjectsPerBatch,
                    "The change set was empty, so no download is needed. Reported as its own outcome and " +
                    "NOT as a plan: an empty set is also what a broken change tracker produces, and the " +
                    "two must not share a verdict (FI-44).");
            }

            var refusals = new List<BatchRefusal>();

            // --- WELL-FORMEDNESS. ---------------------------------------------------------------
            var unnamed = objects.Count(o => o.Name.Length == 0);
            if (unnamed > 0)
            {
                refusals.Add(new BatchRefusal(
                    BatchRefusalReason.UnnamedObject,
                    new string[0],
                    unnamed + " object(s) in the change set have no name. An unnamed object cannot be " +
                    "placed in a batch, cross-checked against §9a's load manifest, or referred to in a " +
                    "refusal."));
            }

            foreach (var duplicate in objects
                .Where(o => o.Name.Length > 0)
                .GroupBy(o => o.Name, ChangedObject.NameComparer)
                .Where(g => g.Count() > 1))
            {
                refusals.Add(new BatchRefusal(
                    BatchRefusalReason.DuplicateObjectName,
                    new[] { duplicate.Key },
                    "The change set names '" + duplicate.Key + "' " + duplicate.Count() + " times. Which " +
                    "change would be downloaded is undecidable, and the object count the twenty-object " +
                    "limit is budgeted in would be wrong."));
            }

            // --- THE QUEUE BACKSTOP. --------------------------------------------------------------
            // This planner is the WAVE-BOUNDARY path. A STOP-class object here means a routing bug, and
            // the consequence of not catching it is a CPU stopped in the middle of a wave.
            var notRunClass = objects
                .Where(o => o.Name.Length > 0 && o.ChangeClass != ChangeClass.Run && o.ChangeClass != ChangeClass.RunInit)
                .ToArray();

            if (notRunClass.Length > 0)
            {
                refusals.Add(new BatchRefusal(
                    BatchRefusalReason.StopClassObjectInAWaveBoundaryBatch,
                    notRunClass.Select(o => o.Name).ToArray(),
                    "These are not RUN-class changes and must not be in a wave-boundary batch: " +
                    string.Join(", ", notRunClass.Select(o => o.Name + " (" + o.ChangeClass + ")").ToArray()) +
                    ". STOP-class changes accumulate in the deferred queue and land at a drain (D23/D24); " +
                    "an unclassified one is refused at admission. Reaching this planner means the router " +
                    "was bypassed."));
            }

            var byName = new Dictionary<string, ChangedObject>(ChangedObject.NameComparer);
            foreach (var o in objects.Where(o => o.Name.Length > 0))
            {
                byName[o.Name] = o;
            }

            // --- DEPENDENCIES THAT NOTHING WILL EVER SATISFY. -------------------------------------
            foreach (var o in objects.Where(o => o.Name.Length > 0))
            {
                foreach (var dependency in o.DependsOn.Where(d => !byName.ContainsKey(d) && !deployed.Contains(d)))
                {
                    refusals.Add(new BatchRefusal(
                        BatchRefusalReason.UnresolvedDependency,
                        new[] { o.Name, dependency },
                        "'" + o.Name + "' depends on '" + dependency + "', which is neither in the change " +
                        "set nor in the deployed baseline (" + deployed + "). No batch can be closed over " +
                        "it: either the change set is incomplete, or the baseline is stale."));
                }
            }

            if (refusals.Count > 0)
            {
                return BatchPlan.Refuse(
                    refusals,
                    maxObjectsPerBatch,
                    objects.Length,
                    "The change set cannot be batched as it stands.");
            }

            // --- COMPONENTS. Objects joined by a dependency edge in EITHER direction must land
            // together, because batches partition the change set (see DependencyClosure's remarks).
            var groups = WeaklyConnectedComponents(byName);

            var oversized = groups.Where(g => g.Count > maxObjectsPerBatch).ToArray();
            if (oversized.Length > 0)
            {
                foreach (var group in oversized)
                {
                    refusals.Add(new BatchRefusal(
                        BatchRefusalReason.DependencyGroupExceedsObjectLimit,
                        group.ToArray(),
                        group.Count + " objects must land in the same download because they are joined by " +
                        "declared dependencies, and the limit is " + maxObjectsPerBatch +
                        ". This cannot be split: closure over a partition puts a whole dependency group " +
                        "in one batch, so there is no consistent download of this set at all. The usual " +
                        "cause is DB-1's blast radius — a modified UDT is RUN (Init) on every DB built on " +
                        "it (D16 forbids revising a shared UDT mid-wave-set for a related reason)."));
                }

                return BatchPlan.Refuse(
                    refusals,
                    maxObjectsPerBatch,
                    objects.Length,
                    "At least one dependency group is larger than the " + maxObjectsPerBatch +
                    "-object limit, so it cannot be downloaded consistently at all.");
            }

            // --- PACK. First-fit-decreasing over the groups. Greedy is fine (DB-13 says so of the
            // colouring, and this is a strictly easier problem): the limit is a correctness constraint
            // and the batch COUNT is a cost, not a gate. Deterministic ordering matters more than
            // optimality — the same change set must produce the same plan, or two runs of the same wave
            // are not comparable.
            var bins = new List<List<string>>();
            foreach (var group in groups
                .OrderByDescending(g => g.Count)
                .ThenBy(g => g.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).First(), StringComparer.OrdinalIgnoreCase))
            {
                var bin = bins.FirstOrDefault(b => b.Count + group.Count <= maxObjectsPerBatch);
                if (bin == null)
                {
                    bin = new List<string>();
                    bins.Add(bin);
                }

                bin.AddRange(group);
            }

            // --- VERIFY THE PLAN WITH SOMETHING THAT DID NOT BUILD IT. ----------------------------
            var violations = DependencyClosure.Verify(bins.Select(b => (IEnumerable<string>)b), objects, deployed);
            if (violations.Count > 0)
            {
                refusals.Add(new BatchRefusal(
                    BatchRefusalReason.PlanFailedItsOwnClosureCheck,
                    violations.Select(v => v.ObjectName).Distinct(ChangedObject.NameComparer).ToArray(),
                    "The plan this packer produced does not satisfy the closure definition, checked " +
                    "independently: " + string.Join(" | ", violations.Select(v => v.ToString()).ToArray()) +
                    ". Refused rather than warned about — a plan whose closure cannot be verified is as " +
                    "unusable as one known to be open."));

                return BatchPlan.Refuse(
                    refusals,
                    maxObjectsPerBatch,
                    objects.Length,
                    "The packer's own output failed the independent closure check. This is a defect in " +
                    "the packer, not in the change set.");
            }

            var batches = bins
                .Select((names, index) => new DownloadBatch(
                    index,
                    names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).Select(n => byName[n]).ToArray()))
                .ToArray();

            return BatchPlan.Planned(
                batches,
                maxObjectsPerBatch,
                objects.Length,
                batches.Length + " dependency-closed batch(es) covering " + objects.Length +
                " object(s), each within the " + maxObjectsPerBatch + "-object limit and verified closed " +
                "independently of the packer. Whether any of them stops the CPU is UNDETERMINED and is " +
                "decided at download time (D32), not here.");
        }

        /// <summary>
        /// Weakly-connected components over the change set's declared dependencies, by union-find.
        /// Edges are undirected on purpose: an object and anything it depends on must land together,
        /// and because batches partition the set, that forces the whole component into one batch.
        /// </summary>
        private static List<List<string>> WeaklyConnectedComponents(Dictionary<string, ChangedObject> byName)
        {
            var parent = new Dictionary<string, string>(ChangedObject.NameComparer);
            foreach (var name in byName.Keys)
            {
                parent[name] = name;
            }

            Func<string, string> find = null!;
            find = name =>
            {
                var root = name;
                while (!ChangedObject.NameComparer.Equals(parent[root], root))
                {
                    root = parent[root];
                }

                // Path compression, so a long chain of dependencies does not make this quadratic.
                var walk = name;
                while (!ChangedObject.NameComparer.Equals(walk, root))
                {
                    var next = parent[walk];
                    parent[walk] = root;
                    walk = next;
                }

                return root;
            };

            foreach (var o in byName.Values)
            {
                foreach (var dependency in o.DependsOn.Where(byName.ContainsKey))
                {
                    var a = find(o.Name);
                    var b = find(dependency);
                    if (!ChangedObject.NameComparer.Equals(a, b))
                    {
                        parent[a] = b;
                    }
                }
            }

            var groups = new Dictionary<string, List<string>>(ChangedObject.NameComparer);
            foreach (var name in byName.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                var root = find(name);
                List<string> group;
                if (!groups.TryGetValue(root, out group))
                {
                    group = new List<string>();
                    groups[root] = group;
                }

                group.Add(name);
            }

            return groups.Values.ToList();
        }
    }
}
