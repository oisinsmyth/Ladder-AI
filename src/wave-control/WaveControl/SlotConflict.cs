using System;
using System.Collections.Generic;
using System.Linq;

namespace Ladder.Wave
{
    /// <summary>Where a conflict edge came from. DB-13 names exactly three sources.</summary>
    /// <remarks>
    /// <para>
    /// *** THE ZERO VALUE IS <see cref="Unstated"/> AND IT IS REFUSED. *** An edge that cannot say
    /// where it came from cannot be held to D22's add-only rule, and cannot be told apart from one an
    /// agent invented.
    /// </para>
    /// <para>
    /// *** AND EVERY MEMBER IS PINNED INTO EXACTLY ONE BUCKET BY A TEST OVER THE WHOLE ENUM. *** This
    /// lane has already had two properties defined as "not the one exception" silently acquire a new
    /// meaning when an enum grew, with nobody editing them — and the second was found by the test
    /// written for the first. This enum WILL grow (D9's closure, shared models, the blacklist, and
    /// whatever the first real submission set turns up), so the partition is asserted rather than
    /// implied.
    /// </para>
    /// </remarks>
    public enum ConflictEdgeKind
    {
        /// <summary>Not stated. Refused.</summary>
        Unstated = 0,

        /// <summary>
        /// D9 — OVERLAPPING REACHABLE STATE, the transitive closure through each block's call tree,
        /// COMPUTED from the reference graph (`converter cross-check`) and never declared. Already the
        /// definition of "independent".
        /// </summary>
        OverlappingReachableState = 1,

        /// <summary>
        /// Two tests parametrising the SAME model of the SAME equipment (D28). DB-13 calls this the
        /// dominant source and the one that is fixable — instancing the models removes these edges.
        /// </summary>
        SharedModelInstance = 2,

        /// <summary>
        /// §2.5 / D22 — a manual blacklist entry. *** MAY ONLY ADD AN EXCLUSION, NEVER REMOVE ONE. ***
        /// An agent must not be able to declare itself compatible with something the reference graph
        /// says it conflicts with; computed disjointness is the FLOOR and the blacklist sits on top.
        /// </summary>
        Blacklist = 3,

        /// <summary>
        /// X-I rule 2 — a slot and the MODEL SLOT that tests a model it uses. *** COMPUTED FROM THE
        /// DEPENDENCY, NEVER DECLARED. *** Run concurrently, a red result is ambiguous between the
        /// block and the model (§7a cause 1 versus cause 2), which is the exact ambiguity §7a exists to
        /// remove — so they cannot share a wave set, and the model's must come FIRST.
        /// </summary>
        ModelUnderTestByAnotherSlot = 4,
    }

    /// <summary>Facts about conflict-edge kinds, pinned by a test over the whole enum.</summary>
    public static class ConflictEdgeKinds
    {
        /// <summary>
        /// TRUE for edges DERIVED from the reference graph or the model set — nobody chooses them, so
        /// nobody can choose to omit them.
        /// </summary>
        public static bool IsComputed(ConflictEdgeKind kind) =>
            kind == ConflictEdgeKind.OverlappingReachableState ||
            kind == ConflictEdgeKind.SharedModelInstance ||
            kind == ConflictEdgeKind.ModelUnderTestByAnotherSlot;

        /// <summary>TRUE for edges a person or agent added by hand. Exactly one kind, and it is add-only.</summary>
        public static bool IsManual(ConflictEdgeKind kind) => kind == ConflictEdgeKind.Blacklist;

        /// <summary>TRUE when the kind is usable at all — i.e. it is computed or manual, and not the zero value.</summary>
        public static bool IsStated(ConflictEdgeKind kind) => IsComputed(kind) || IsManual(kind);

        /// <summary>
        /// TRUE for a kind that also carries a DIRECTION — the two ends must not merely be separated,
        /// one must come FIRST. Only X-I's model dependency does.
        /// </summary>
        /// <remarks>
        /// Separating an ordered pair is not enough: a consumer running BEFORE the model that stands in
        /// for its equipment gets a result conditioned on a model nobody has tested yet. Every other
        /// kind is symmetric — "these two cannot run together" says nothing about which runs first.
        /// </remarks>
        public static bool IsOrdered(ConflictEdgeKind kind) => kind == ConflictEdgeKind.ModelUnderTestByAnotherSlot;
    }

    /// <summary>One conflict edge between two slots.</summary>
    public sealed class ConflictEdge
    {
        /// <param name="firstSlot">One end.</param>
        /// <param name="secondSlot">The other.</param>
        /// <param name="kind">Where it came from.</param>
        /// <param name="reason">
        /// Why. REQUIRED for a <see cref="ConflictEdgeKind.Blacklist"/> edge — §2.5's named failure
        /// mode is defensive over-blacklisting, where concurrency collapses toward serial and nobody
        /// notices because it still WORKS, and a recorded reason per entry is what makes it visible.
        /// </param>
        public ConflictEdge(string? firstSlot, string? secondSlot, ConflictEdgeKind kind, string? reason = null)
        {
            FirstSlot = (firstSlot ?? string.Empty).Trim();
            SecondSlot = (secondSlot ?? string.Empty).Trim();
            Kind = kind;
            Reason = (reason ?? string.Empty).Trim();
        }

        /// <summary>One end.</summary>
        public string FirstSlot { get; }

        /// <summary>The other.</summary>
        public string SecondSlot { get; }

        /// <summary>Where it came from.</summary>
        public ConflictEdgeKind Kind { get; }

        /// <summary>Why.</summary>
        public string Reason { get; }

        /// <summary>TRUE when both ends name a slot and they are different slots.</summary>
        public bool IsWellFormed =>
            FirstSlot.Length > 0 &&
            SecondSlot.Length > 0 &&
            !string.Equals(FirstSlot, SecondSlot, StringComparison.OrdinalIgnoreCase);

        /// <inheritdoc />
        public override string ToString() =>
            FirstSlot + " <-> " + SecondSlot + " (" + Kind + ")" +
            (Reason.Length == 0 ? string.Empty : ": " + Reason);
    }

    /// <summary>
    /// One slot offered for admission — a (agent, methodology) pair (D26b), with what the colouring
    /// needs to know about it.
    /// </summary>
    public sealed class TestSlot
    {
        /// <param name="id">Stable slot id.</param>
        /// <param name="agent">Whose slot it is. An agent may hold several (D26b).</param>
        /// <param name="reachableState">
        /// The transitive closure of state this slot can reach (D9), as computed by
        /// `converter cross-check`.
        /// </param>
        /// <param name="reachableStateProvenance">
        /// WHERE that closure came from. *** REQUIRED, AND IT IS WHAT SEPARATES "COMPUTED AND FOUND
        /// EMPTY" FROM "NOBODY COMPUTED IT". *** Both arrive as an empty set and call for opposite
        /// actions; the same distinction `DeployedProgram.From` refuses an empty list to preserve.
        /// </param>
        /// <param name="modelInstances">The model instances this slot parametrises (D28).</param>
        /// <param name="widthRegisters">
        /// The slot's width on the wire. Reported per wave set and NOT acted on — see
        /// <see cref="WaveSetPlan"/> on F-6.
        /// </param>
        /// <param name="testsModel">
        /// The model this slot IS THE TEST OF, when it is a model slot (X-I). Empty for an ordinary
        /// block slot. A model-testing slot is SIMPLER than a block-testing one — it needs no model
        /// instance of its own, because its inputs ARE the vector.
        /// </param>
        public TestSlot(
            string? id,
            string? agent,
            IEnumerable<string>? reachableState,
            string? reachableStateProvenance,
            IEnumerable<string>? modelInstances = null,
            int widthRegisters = 0,
            string? testsModel = null)
        {
            TestsModel = (testsModel ?? string.Empty).Trim();
            Id = (id ?? string.Empty).Trim();
            Agent = (agent ?? string.Empty).Trim();
            ReachableState = Clean(reachableState);
            ReachableStateProvenance = (reachableStateProvenance ?? string.Empty).Trim();
            ModelInstances = Clean(modelInstances);
            WidthRegisters = widthRegisters;
        }

        /// <summary>Stable slot id.</summary>
        public string Id { get; }

        /// <summary>Whose slot it is.</summary>
        public string Agent { get; }

        /// <summary>The reachable-state closure (D9).</summary>
        public IReadOnlyList<string> ReachableState { get; }

        /// <summary>Where that closure came from. Empty means nobody computed it.</summary>
        public string ReachableStateProvenance { get; }

        /// <summary>The model instances this slot parametrises.</summary>
        public IReadOnlyList<string> ModelInstances { get; }

        /// <summary>The slot's width on the wire, in registers.</summary>
        public int WidthRegisters { get; }

        /// <summary>The model this slot is the test OF, or empty for an ordinary block slot.</summary>
        public string TestsModel { get; }

        /// <summary>TRUE when this slot is a model slot (X-I).</summary>
        public bool IsModelSlot => TestsModel.Length > 0;

        /// <inheritdoc />
        public override string ToString() =>
            (Id.Length == 0 ? "<unnamed slot>" : Id) + " (" + (Agent.Length == 0 ? "<no agent>" : Agent) + ")";

        private static IReadOnlyList<string> Clean(IEnumerable<string>? values) =>
            (values ?? Enumerable.Empty<string>())
                .Select(v => (v ?? string.Empty).Trim())
                .Where(v => v.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(v => v, StringComparer.Ordinal)
                .ToArray();
    }
}
