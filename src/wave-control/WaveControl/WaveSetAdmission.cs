using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Ladder.Wave
{
    /// <summary>Why a wave set could not be coloured.</summary>
    public enum ColouringDefect
    {
        /// <summary>Not a defect.</summary>
        None = 0,

        /// <summary>A slot with no id — nothing downstream could refer to it.</summary>
        SlotNotIdentified = 1,

        /// <summary>Two slots share an id.</summary>
        DuplicateSlotId = 2,

        /// <summary>
        /// *** A SLOT WHOSE REACHABLE-STATE CLOSURE WAS NEVER COMPUTED. *** Not the same as one whose
        /// closure is empty: both arrive as an empty set and call for opposite actions. D9's whole
        /// claim is that independence is COMPUTED rather than declared, so a slot with no provenance
        /// has not been shown independent of anything.
        /// </summary>
        ReachableStateNotComputed = 3,

        /// <summary>An edge naming a slot that is not in the set, or naming one slot twice.</summary>
        EdgeDoesNotConnectTwoAdmittedSlots = 4,

        /// <summary>An edge whose kind is the zero value — it cannot be held to the add-only rule.</summary>
        EdgeKindNotStated = 5,

        /// <summary>
        /// A blacklist entry with no recorded reason. §2.5's named failure mode is defensive
        /// over-blacklisting: concurrency collapses toward serial and nobody notices, because it still
        /// WORKS. A reason per entry is what makes it visible.
        /// </summary>
        BlacklistEntryWithoutAReason = 6,

        /// <summary>
        /// *** NO WIDTH CAP WAS SUPPLIED. *** D29's poll-bandwidth limit is not a pairwise edge but a
        /// width limit on any wave set, and it is derived from the measured round trip against the
        /// observation set. Without it the colouring is UNCHECKED against bandwidth — not passing it.
        /// </summary>
        WidthCapNotSupplied = 7,

        /// <summary>
        /// A single slot could not be placed: it conflicts with so much that no colour class within the
        /// width cap can hold it alongside anything. Reported rather than silently given its own wave.
        /// </summary>
        SlotCouldNotBePlaced = 8,
    }

    /// <summary>One thing wrong with a colouring input or its result.</summary>
    public sealed class ColouringFinding
    {
        internal ColouringFinding(ColouringDefect defect, string subject, string detail)
        {
            Defect = defect;
            Subject = subject;
            Detail = detail;
        }

        /// <summary>Which defect.</summary>
        public ColouringDefect Defect { get; }

        /// <summary>The slot or edge it is about.</summary>
        public string Subject { get; }

        /// <summary>What is wrong.</summary>
        public string Detail { get; }

        /// <inheritdoc />
        public override string ToString() => Defect + " [" + Subject + "]: " + Detail;
    }

    /// <summary>One colour class — i.e. ONE WAVE SET.</summary>
    public sealed class WaveSet
    {
        internal WaveSet(int colour, IReadOnlyList<TestSlot> slots)
        {
            Colour = colour;
            Slots = slots;
        }

        /// <summary>The colour index. Each colour class is a wave set.</summary>
        public int Colour { get; }

        /// <summary>The slots in it. They are pairwise conflict-free by construction, and re-verified.</summary>
        public IReadOnlyList<TestSlot> Slots { get; }

        /// <summary>How many slots share this wave.</summary>
        public int SlotCount => Slots.Count;

        /// <summary>The widest slot's width, in registers.</summary>
        public int WidestSlotRegisters => Slots.Count == 0 ? 0 : Slots.Max(s => s.WidthRegisters);

        /// <summary>The narrowest slot's width, in registers.</summary>
        public int NarrowestSlotRegisters => Slots.Count == 0 ? 0 : Slots.Min(s => s.WidthRegisters);

        /// <summary>One line for the plan an agent reads.</summary>
        public string ToLogLine() =>
            "WAVE SET " + Colour + ": " + SlotCount + " slot(s) [" +
            string.Join(", ", Slots.Select(s => s.Id).ToArray()) + "], widths " +
            NarrowestSlotRegisters + "-" + WidestSlotRegisters + " registers";

        /// <inheritdoc />
        public override string ToString() => ToLogLine();
    }

    /// <summary>The outcome of an admission attempt.</summary>
    public enum AdmissionPlanOutcome
    {
        /// <summary>
        /// *** NOTHING WAS SUBMITTED, SO NOTHING WAS CHECKED. *** The zero value. An empty wave set is
        /// not an admitted one: it is a colouring that ran over nothing, and reporting it as admitted
        /// would let a submission pipeline that has stopped feeding the coordinator look healthy.
        /// </summary>
        NothingToAdmit = 0,

        /// <summary>The inputs are unusable. Every reason is listed.</summary>
        Refused = 1,

        /// <summary>Slots were coloured into wave sets.</summary>
        Admitted = 2,
    }

    /// <summary>The admission plan: which slots may share a wave set.</summary>
    public sealed class WaveSetPlan
    {
        internal WaveSetPlan(
            AdmissionPlanOutcome outcome,
            IReadOnlyList<WaveSet> waveSets,
            IReadOnlyList<ColouringFinding> findings,
            int slotsExamined,
            int edgesApplied,
            string summary)
        {
            Outcome = outcome;
            WaveSets = waveSets;
            Findings = findings;
            SlotsExamined = slotsExamined;
            EdgesApplied = edgesApplied;
            Summary = summary;
        }

        /// <summary>Admitted, refused, or nothing to admit.</summary>
        public AdmissionPlanOutcome Outcome { get; }

        /// <summary>The colour classes. Each one is a wave set.</summary>
        public IReadOnlyList<WaveSet> WaveSets { get; }

        /// <summary>Every reason for a refusal.</summary>
        public IReadOnlyList<ColouringFinding> Findings { get; }

        /// <summary>How many slots were looked at. Reported alongside every outcome.</summary>
        public int SlotsExamined { get; }

        /// <summary>How many conflict edges were applied.</summary>
        public int EdgesApplied { get; }

        /// <summary>A sentence describing the outcome.</summary>
        public string Summary { get; }

        /// <summary>TRUE only for <see cref="AdmissionPlanOutcome.Admitted"/>.</summary>
        public bool Usable => Outcome == AdmissionPlanOutcome.Admitted;

        /// <summary>
        /// The number of WAVES this submission set costs. DB-13's actual currency: conflicting slots go
        /// into DIFFERENT wave sets, *** which costs an extra WAVE, not a longer one *** — wave length
        /// is max tensor length across the slots and nothing here can change it.
        /// </summary>
        public int WaveCount => WaveSets.Count;

        /// <summary>The plan, in full — what an agent reads to see what its submission cost.</summary>
        public string Describe()
        {
            var lines = new List<string>();

            switch (Outcome)
            {
                case AdmissionPlanOutcome.Admitted:
                    lines.Add("ADMITTED: " + SlotsExamined + " slot(s) coloured into " + WaveCount +
                              " wave set(s) over " + EdgesApplied + " conflict edge(s). " + Summary);
                    break;

                case AdmissionPlanOutcome.Refused:
                    lines.Add("ADMISSION REFUSED: " + Findings.Count + " finding(s) over " + SlotsExamined +
                              " slot(s). " + Summary);
                    break;

                default:
                    lines.Add("NOTHING TO ADMIT: " + Summary);
                    break;
            }

            lines.AddRange(WaveSets.Select(w => "  " + w.ToLogLine()));
            lines.AddRange(Findings.Select(f => "  - " + f));

            if (WaveSets.Any(w => w.WidestSlotRegisters != w.NarrowestSlotRegisters))
            {
                lines.Add("  NOTE: slot widths differ within a wave set. Whether admission should GROUP " +
                          "BY SLOT SIZE is F-6/F-2 and is UNRULED — reported here, not acted on.");
            }

            return string.Join(Environment.NewLine, lines.ToArray());
        }

        /// <inheritdoc />
        public override string ToString() => Describe();
    }

    /// <summary>
    /// DB-13 / 6.1 — WAVE-SET ADMISSION. *** IT DECIDES WHICH SLOTS MAY SHARE A WAVE SET, AND IT DOES
    /// NOT SET WAVE DURATION. ***
    /// </summary>
    /// <remarks>
    /// <para>
    /// DB-13 was written as "pack tests into the fewest tensors", on the understanding that tensor
    /// count set the wave's length. Under the corrected model (D26a) *** every slot runs at every
    /// index, so there is no packing decision inside a wave at all: WAVE LENGTH IS MAX TENSOR LENGTH
    /// ACROSS THE SLOTS, and nothing this component does can change it. *** The colouring survives,
    /// applied to a different thing — *** COLOUR THE SLOTS, AND EACH COLOUR CLASS IS A WAVE SET. ***
    /// Conflicting slots go into different wave sets, which costs an extra WAVE rather than a longer
    /// one. So the conflict graph is an ADMISSION mechanism, not a scheduler, and what it buys is
    /// SLOTS PER WAVE rather than seconds per wave.
    /// </para>
    /// <para>
    /// *** NO TIMING CONSTANT IS CHOSEN OR COMPUTED HERE, AND THAT IS DELIBERATE RATHER THAN
    /// INCIDENTAL. *** D29's poll-bandwidth cap is `floor(S x scan / RTT_p99)`, and `RTT_p99` MOVED
    /// FROM 173 TO 201 MS while this component was being written. Anything holding a baked-in `K_max`
    /// would now be over-admitting. So the cap is an INPUT with a required provenance: whoever derives
    /// it from §12a owns it, this component consumes it, and a re-measurement changes one caller
    /// rather than this file. A missing cap is <see cref="ColouringDefect.WidthCapNotSupplied"/> —
    /// UNCHECKED, never passed.
    /// </para>
    /// <para>
    /// GREEDY IS FINE, and DB-13 says so. What matters more is that it is DETERMINISTIC — slots are
    /// taken in id order and given the lowest admissible colour — because two runs of the same
    /// submission set that produce different wave sets cannot be compared.
    /// </para>
    /// <para>
    /// *** THE BLACKLIST MAY ONLY ADD EDGES (D22), AND THAT IS STRUCTURAL HERE: *** edges are a union
    /// and there is no route that removes one. An agent cannot declare itself compatible with
    /// something the reference graph says it conflicts with, because there is no argument that would
    /// express it.
    /// </para>
    /// </remarks>
    public static class WaveSetAdmission
    {
        /// <summary>Colour the slots into wave sets.</summary>
        /// <param name="slots">The submitted slots.</param>
        /// <param name="edges">
        /// The conflict edges — D9's overlapping reachable state, shared model instances, and blacklist
        /// entries. A union; nothing subtracts.
        /// </param>
        /// <param name="maxSlotsPerWaveSet">
        /// D29's width cap. REQUIRED — pass null and the colouring is refused as unchecked. Derived
        /// from §12a by the caller; never computed here.
        /// </param>
        /// <param name="capProvenance">Where the cap came from, so a stale one is traceable.</param>
        public static WaveSetPlan Admit(
            IEnumerable<TestSlot>? slots,
            IEnumerable<ConflictEdge>? edges,
            int? maxSlotsPerWaveSet,
            string? capProvenance = null)
        {
            var slotList = (slots ?? Enumerable.Empty<TestSlot>()).Where(s => s != null).ToArray();
            var edgeList = (edges ?? Enumerable.Empty<ConflictEdge>()).Where(e => e != null).ToArray();
            var findings = new List<ColouringFinding>();

            if (slotList.Length == 0)
            {
                return new WaveSetPlan(
                    AdmissionPlanOutcome.NothingToAdmit,
                    new WaveSet[0],
                    new ColouringFinding[0],
                    0,
                    0,
                    "No slots were submitted, so nothing was coloured and nothing was checked. An empty " +
                    "wave set is not an admitted one — reporting it as admitted would let a submission " +
                    "pipeline that has stopped feeding the coordinator look healthy (FI-44).");
            }

            // --- THE CAP. Missing means UNCHECKED, not passed. ---------------------------------------
            if (!maxSlotsPerWaveSet.HasValue || maxSlotsPerWaveSet.Value < 1)
            {
                findings.Add(new ColouringFinding(
                    ColouringDefect.WidthCapNotSupplied,
                    maxSlotsPerWaveSet.HasValue ? maxSlotsPerWaveSet.Value.ToString(CultureInfo.InvariantCulture) : "<none>",
                    "D29's poll-bandwidth cap is a WIDTH LIMIT on any wave set, derived from the measured " +
                    "round trip against the observation set the wave needs. Without it the colouring is " +
                    "UNCHECKED against bandwidth rather than passing it. It is not computed here on " +
                    "purpose: RTT_p99 moved from 173 to 201 ms during this component's own construction, " +
                    "and anything holding a baked-in K_max would now be over-admitting."));
            }
            else if (string.IsNullOrWhiteSpace(capProvenance))
            {
                findings.Add(new ColouringFinding(
                    ColouringDefect.WidthCapNotSupplied,
                    maxSlotsPerWaveSet.Value.ToString(CultureInfo.InvariantCulture),
                    "A cap was supplied with no provenance. It is a function of a measured constant that " +
                    "has already moved once, so a cap nobody can trace is one nobody can re-check."));
            }

            // --- SLOT WELL-FORMEDNESS. ---------------------------------------------------------------
            foreach (var slot in slotList)
            {
                if (slot.Id.Length == 0)
                {
                    findings.Add(new ColouringFinding(
                        ColouringDefect.SlotNotIdentified,
                        "<unnamed slot>",
                        "A slot carries no id, so no edge can name it and no plan can report it."));
                    continue;
                }

                if (slot.ReachableStateProvenance.Length == 0)
                {
                    findings.Add(new ColouringFinding(
                        ColouringDefect.ReachableStateNotComputed,
                        slot.Id,
                        "The slot's reachable-state closure has no provenance. D9's whole claim is that " +
                        "independence is COMPUTED from the reference graph rather than declared, so a " +
                        "slot that cannot say where its closure came from has not been shown independent " +
                        "of anything. 'Computed and found empty' and 'nobody computed it' arrive as the " +
                        "same empty set and call for opposite actions."));
                }
            }

            foreach (var duplicate in slotList
                .Where(s => s.Id.Length > 0)
                .GroupBy(s => s.Id, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1))
            {
                findings.Add(new ColouringFinding(
                    ColouringDefect.DuplicateSlotId,
                    duplicate.Key,
                    "Two slots share this id. Which one an edge refers to is undecidable."));
            }

            // --- EDGE WELL-FORMEDNESS. ---------------------------------------------------------------
            var known = new HashSet<string>(slotList.Where(s => s.Id.Length > 0).Select(s => s.Id), StringComparer.OrdinalIgnoreCase);

            foreach (var edge in edgeList)
            {
                if (!ConflictEdgeKinds.IsStated(edge.Kind))
                {
                    findings.Add(new ColouringFinding(
                        ColouringDefect.EdgeKindNotStated,
                        edge.ToString(),
                        "The edge does not say where it came from, so it cannot be held to D22's " +
                        "add-only rule and cannot be told apart from one an agent invented."));
                    continue;
                }

                if (!edge.IsWellFormed || !known.Contains(edge.FirstSlot) || !known.Contains(edge.SecondSlot))
                {
                    findings.Add(new ColouringFinding(
                        ColouringDefect.EdgeDoesNotConnectTwoAdmittedSlots,
                        edge.ToString(),
                        "An edge must connect two DIFFERENT slots that are both in this submission set. " +
                        "An edge naming a slot nobody submitted is a graph built from a stale set, and " +
                        "silently dropping it would make the colouring look less constrained than it is."));
                    continue;
                }

                if (edge.Kind == ConflictEdgeKind.Blacklist && edge.Reason.Length == 0)
                {
                    findings.Add(new ColouringFinding(
                        ColouringDefect.BlacklistEntryWithoutAReason,
                        edge.ToString(),
                        "A blacklist entry must record why. §2.5's named failure mode is defensive " +
                        "over-blacklisting — concurrency collapses toward serial and nobody notices, " +
                        "because it still WORKS — and a recorded reason per entry is what makes it " +
                        "visible."));
                }
            }

            if (findings.Count > 0)
            {
                return new WaveSetPlan(
                    AdmissionPlanOutcome.Refused,
                    new WaveSet[0],
                    findings,
                    slotList.Length,
                    edgeList.Length,
                    "The submission set cannot be coloured as it stands.");
            }

            // --- COLOUR. Deterministic: slots in id order, lowest admissible colour. ------------------
            var cap = maxSlotsPerWaveSet!.Value;
            var adjacency = BuildAdjacency(slotList, edgeList);
            var colourOf = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var classes = new List<List<TestSlot>>();

            foreach (var slot in slotList.OrderBy(s => s.Id, StringComparer.OrdinalIgnoreCase))
            {
                var placed = false;

                for (var colour = 0; colour < classes.Count; colour++)
                {
                    if (classes[colour].Count >= cap)
                    {
                        continue;
                    }

                    if (classes[colour].Any(other => adjacency[slot.Id].Contains(other.Id)))
                    {
                        continue;
                    }

                    classes[colour].Add(slot);
                    colourOf[slot.Id] = colour;
                    placed = true;
                    break;
                }

                if (!placed)
                {
                    classes.Add(new List<TestSlot> { slot });
                    colourOf[slot.Id] = classes.Count - 1;
                }
            }

            var provisional = classes
                .Select((members, index) => new WaveSet(index, members.OrderBy(s => s.Id, StringComparer.OrdinalIgnoreCase).ToArray()))
                .ToArray();

            // *** RE-VERIFIED BY SOMETHING THAT DID NOT BUILD IT. *** See Verify below on why it is a
            // separate, public method rather than a loop here.
            findings.AddRange(Verify(provisional, edgeList, cap));

            if (findings.Count > 0)
            {
                return new WaveSetPlan(
                    AdmissionPlanOutcome.Refused,
                    new WaveSet[0],
                    findings,
                    slotList.Length,
                    edgeList.Length,
                    "The colouring produced does not satisfy its own inputs. This is a defect in the " +
                    "colourer, not in the submission set.");
            }

            return new WaveSetPlan(
                AdmissionPlanOutcome.Admitted,
                provisional,
                new ColouringFinding[0],
                slotList.Length,
                edgeList.Length,
                "Conflicting slots are in DIFFERENT wave sets, which costs an extra WAVE rather than a " +
                "longer one — wave length is max tensor length across the slots and nothing here changes " +
                "it. Cap " + cap + " slot(s) per wave set (" + capProvenance!.Trim() + ").");
        }

        /// <summary>
        /// Check a colouring against the edge list and the cap — *** FROM THE DEFINITION, NOT FROM THE
        /// COLOURER'S OWN BOOKKEEPING. ***
        /// </summary>
        /// <remarks>
        /// <para>
        /// The colourer "knows" its classes are conflict-free by construction, and that conviction is
        /// exactly the blind spot to guard — the same separation <see cref="DependencyClosure"/> keeps
        /// from <see cref="WaveBoundaryBatchPlanner"/>'s packer.
        /// </para>
        /// <para>
        /// *** PUBLIC ON PURPOSE, AND THE REASON IS A TEST GAP A MUTATION FOUND. *** While this was a
        /// private loop inside <see cref="Admit"/>, deleting it left the whole suite GREEN: the colourer
        /// never produces a bad colouring, so nothing could reach the check. A guard that cannot be
        /// shown to fire is indistinguishable from a constant — the same finding this lane made about a
        /// permanently-true flag. Exposed, it can be pointed at a deliberately wrong colouring, which is
        /// the only evidence that it checks anything.
        /// </para>
        /// </remarks>
        public static IReadOnlyList<ColouringFinding> Verify(
            IEnumerable<WaveSet>? waveSets,
            IEnumerable<ConflictEdge>? edges,
            int maxSlotsPerWaveSet)
        {
            var sets = (waveSets ?? Enumerable.Empty<WaveSet>()).Where(w => w != null).ToArray();
            var edgeList = (edges ?? Enumerable.Empty<ConflictEdge>()).Where(e => e != null).ToArray();
            var findings = new List<ColouringFinding>();

            var colourOf = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var set in sets)
            {
                foreach (var slot in set.Slots)
                {
                    colourOf[slot.Id] = set.Colour;
                }
            }

            foreach (var edge in edgeList)
            {
                int first;
                int second;
                if (!colourOf.TryGetValue(edge.FirstSlot, out first) ||
                    !colourOf.TryGetValue(edge.SecondSlot, out second))
                {
                    findings.Add(new ColouringFinding(
                        ColouringDefect.EdgeDoesNotConnectTwoAdmittedSlots,
                        edge.ToString(),
                        "The colouring does not place both ends of this edge, so the constraint it " +
                        "expresses was not applied to anything."));
                    continue;
                }

                if (first == second)
                {
                    findings.Add(new ColouringFinding(
                        ColouringDefect.SlotCouldNotBePlaced,
                        edge.ToString(),
                        "Both ends were given colour " + first + ", so two conflicting slots would share " +
                        "a wave set. Checked from the edge list rather than from the colourer's own " +
                        "bookkeeping, which is what makes this a check rather than an echo."));
                }
            }

            foreach (var oversized in sets.Where(w => w.SlotCount > maxSlotsPerWaveSet))
            {
                findings.Add(new ColouringFinding(
                    ColouringDefect.SlotCouldNotBePlaced,
                    string.Join(", ", oversized.Slots.Select(s => s.Id).ToArray()),
                    "A wave set holds " + oversized.SlotCount + " slots against a cap of " +
                    maxSlotsPerWaveSet + "."));
            }

            foreach (var duplicated in sets
                .SelectMany(w => w.Slots.Select(s => s.Id))
                .GroupBy(id => id, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1))
            {
                findings.Add(new ColouringFinding(
                    ColouringDefect.DuplicateSlotId,
                    duplicated.Key,
                    "The slot appears in " + duplicated.Count() + " wave sets. Colour classes PARTITION " +
                    "the slots; running one twice would double-count its bandwidth and its conflicts."));
            }

            return findings;
        }

        /// <summary>Builds one wave set, for a caller checking a colouring it produced itself.</summary>
        public static WaveSet WaveSetOf(int colour, IEnumerable<TestSlot> slots) =>
            new WaveSet(colour, (slots ?? Enumerable.Empty<TestSlot>()).Where(s => s != null).ToArray());

        private static Dictionary<string, HashSet<string>> BuildAdjacency(
            IReadOnlyList<TestSlot> slots,
            IReadOnlyList<ConflictEdge> edges)
        {
            var adjacency = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var slot in slots)
            {
                adjacency[slot.Id] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            // A UNION, and there is no route that removes an entry. That is D22's add-only rule made
            // structural: an agent cannot declare itself compatible with something the graph conflicts
            // with, because no argument here expresses it.
            foreach (var edge in edges)
            {
                adjacency[edge.FirstSlot].Add(edge.SecondSlot);
                adjacency[edge.SecondSlot].Add(edge.FirstSlot);
            }

            return adjacency;
        }
    }
}
