using System;
using System.Collections.Generic;
using System.Linq;

namespace Ladder.Wave
{
    /// <summary>
    /// Derives the COMPUTED conflict edges from the facts slots already carry.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 🔴 *** THIS EXISTS BECAUSE TWO OF THE THREE COMPUTED EDGE KINDS HAD NO PRODUCER, WHICH MADE
    /// D9's OWN RULE ASPIRATIONAL IN THIS COMPONENT. *** D9 says independence is *"COMPUTED from the
    /// reference graph, not declared"*, and `ModelUnderTestByAnotherSlot` was the only kind anything
    /// actually computed — <see cref="ConflictEdgeKind.OverlappingReachableState"/> and
    /// <see cref="ConflictEdgeKind.SharedModelInstance"/> could only arrive as caller-supplied edges,
    /// which is the shape D9 forbids. <see cref="TestSlot"/> already carried both inputs
    /// (<see cref="TestSlot.ReachableState"/> and <see cref="TestSlot.ModelInstances"/>) and nothing
    /// read them.
    /// </para>
    /// <para>
    /// <see cref="ConflictEdgeKind.Blacklist"/> has no producer here and correctly never will: §2.5
    /// puts it in an author's hands, and it may only ADD to what these derive (D22). Computed
    /// disjointness is the FLOOR; the blacklist sits on top for what the graph cannot see.
    /// </para>
    /// <para>
    /// *** WHAT THIS DOES NOT DO: COMPUTE THE REACHABLE SET ITSELF. *** That is
    /// `converter cross-check`'s transitive closure through each block's call tree, and it is why
    /// <see cref="TestSlot.ReachableStateProvenance"/> is required — a slot whose closure has no
    /// provenance is refused at admission rather than treated as independent. This turns two SETS into
    /// EDGES; producing the sets is somebody else's job and is checked, not assumed.
    /// </para>
    /// </remarks>
    public static class SlotConflictDerivation
    {
        /// <summary>
        /// Every computed edge for a submission set: overlapping reachable state (D9), shared model
        /// instances (D28), and X-I's model-under-test precedence.
        /// </summary>
        /// <remarks>
        /// *** THE DOMINANT SOURCE IS THE MODEL ONE, AND DB-13 SAYS SO. *** If tests pack 1-2 wide
        /// because everything shares a plant model, instancing the models (D28) is the highest-value
        /// change available anywhere in the design — and that is visible only because the edges are
        /// LABELLED by kind rather than merged into one "conflicts" relation.
        /// </remarks>
        public static IReadOnlyList<ConflictEdge> AllComputedEdges(IEnumerable<TestSlot>? slots)
        {
            var list = (slots ?? Enumerable.Empty<TestSlot>()).Where(s => s != null && s.Id.Length > 0).ToArray();

            return OverlappingReachableState(list)
                .Concat(SharedModelInstances(list))
                .Concat(ModelOrdering.EdgesFor(list))
                .ToArray();
        }

        /// <summary>
        /// D9 — two slots whose reachable-state closures INTERSECT are not independent.
        /// </summary>
        /// <remarks>
        /// The edge names the overlapping members, because *"these two conflict"* is not actionable and
        /// *"these two both reach DB_Recipe.Setpoint"* is. Slots with no provenance are still paired
        /// here if their sets intersect — refusing them is admission's job, and dropping them here
        /// would make an unprovenanced slot look MORE independent than a provenanced one.
        /// </remarks>
        public static IReadOnlyList<ConflictEdge> OverlappingReachableState(IEnumerable<TestSlot>? slots)
        {
            return PairwiseOverlap(
                slots,
                s => s.ReachableState,
                ConflictEdgeKind.OverlappingReachableState,
                (a, b, shared) =>
                    "'" + a + "' and '" + b + "' both reach " + Describe(shared) +
                    ". D9: independent means NON-OVERLAPPING REACHABLE STATE — the transitive closure " +
                    "through each block's call tree, computed rather than declared.");
        }

        /// <summary>
        /// D28 — two tests parametrising the SAME model of the same equipment cannot run concurrently.
        /// </summary>
        public static IReadOnlyList<ConflictEdge> SharedModelInstances(IEnumerable<TestSlot>? slots)
        {
            return PairwiseOverlap(
                slots,
                s => s.ModelInstances,
                ConflictEdgeKind.SharedModelInstance,
                (a, b, shared) =>
                    "'" + a + "' and '" + b + "' both parametrise " + Describe(shared) +
                    ". DB-13 names this the DOMINANT source of conflict and the one that is FIXABLE: " +
                    "instancing the models (D28) removes the edge, where the D9 kind cannot be removed " +
                    "at all.");
        }

        private static IReadOnlyList<ConflictEdge> PairwiseOverlap(
            IEnumerable<TestSlot>? slots,
            Func<TestSlot, IReadOnlyList<string>> selector,
            ConflictEdgeKind kind,
            Func<string, string, IReadOnlyList<string>, string> reason)
        {
            var list = (slots ?? Enumerable.Empty<TestSlot>())
                .Where(s => s != null && s.Id.Length > 0)
                .OrderBy(s => s.Id, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var edges = new List<ConflictEdge>();

            for (var i = 0; i < list.Length; i++)
            {
                for (var j = i + 1; j < list.Length; j++)
                {
                    var shared = selector(list[i])
                        .Intersect(selector(list[j]), StringComparer.Ordinal)
                        .OrderBy(v => v, StringComparer.Ordinal)
                        .ToArray();

                    if (shared.Length == 0)
                    {
                        continue;
                    }

                    edges.Add(new ConflictEdge(
                        list[i].Id,
                        list[j].Id,
                        kind,
                        reason(list[i].Id, list[j].Id, shared)));
                }
            }

            return edges;
        }

        private static string Describe(IReadOnlyList<string> shared) =>
            shared.Count == 1
                ? "'" + shared[0] + "'"
                : shared.Count + " shared item(s): " + string.Join(", ", shared.ToArray());
    }
}
