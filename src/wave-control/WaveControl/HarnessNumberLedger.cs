using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Ladder.Wave
{
    /// <summary>What an audit of the project's block numbers found.</summary>
    public enum NumberDefect
    {
        /// <summary>Not a defect.</summary>
        None = 0,

        /// <summary>
        /// *** TWO OBJECTS AT THE SAME NUMBER IN THE SAME SPACE. *** The measured collision: TIA
        /// accepted an import declaring FC 910 while another block held 910 and created both, with
        /// import, per-block compile, device compile and `sanity-check` all green.
        /// </summary>
        DuplicateNumber = 1,

        /// <summary>
        /// A HARNESS object numbered OUTSIDE the reserved range. *** THIS IS THE STATE THAT MAKES A
        /// COLLISION POSSIBLE *** — the reservation only separates the two populations while both
        /// respect it.
        /// </summary>
        HarnessObjectOutsideTheRange = 2,

        /// <summary>
        /// A number inside the reserved range held by something that is NOT a harness object. The
        /// reservation is being squatted on, and the next harness allocation will land on it.
        /// </summary>
        RangeOccupiedByAnOutsider = 3,

        /// <summary>
        /// *** A RESERVATION HELD FOR AN OBJECT THAT NO LONGER EXISTS. *** A DIFFERENT FINDING from
        /// the two above and deliberately not collapsed into them: this one calls for RELEASING a
        /// reservation, while an outsider in the range calls for MOVING A BLOCK. `claims --check`
        /// already draws this line — an exclusive claim on a vanished block is what gates there.
        /// </summary>
        ReservationForAVanishedObject = 4,

        /// <summary>An object that will not say whether it is harness or deliverable. Checkable against nothing.</summary>
        OwnerNotStated = 5,

        /// <summary>An object whose kind carries no block number at all, offered as though it had one.</summary>
        NotANumberedKind = 6,
    }

    /// <summary>One finding from a number audit.</summary>
    public sealed class NumberFinding
    {
        internal NumberFinding(NumberDefect defect, string subject, string detail)
        {
            Defect = defect;
            Subject = subject;
            Detail = detail;
        }

        /// <summary>Which defect.</summary>
        public NumberDefect Defect { get; }

        /// <summary>The block or slot it is about.</summary>
        public string Subject { get; }

        /// <summary>What is wrong, and what would fix it.</summary>
        public string Detail { get; }

        /// <inheritdoc />
        public override string ToString() => Defect + " [" + Subject + "]: " + Detail;
    }

    /// <summary>
    /// What an audit found, AND what it exempted. *** THE EXEMPTIONS ARE PART OF THE RESULT, NOT A
    /// BRANCH THAT QUIETLY RETURNS EARLY. ***
    /// </summary>
    /// <remarks>
    /// A silent exemption and a correct pass produce the same empty finding list, which is the shape
    /// this component already refuses for an unowned object. So an exempted OB is ENUMERATED BY NAME:
    /// a reader can see that the audit looked at it and decided the band does not apply, rather than
    /// having to trust that it did.
    /// </remarks>
    public sealed class NumberAuditReport
    {
        internal NumberAuditReport(
            IReadOnlyList<NumberFinding> findings,
            IReadOnlyList<string> exemptedOrganizationBlocks,
            int examined)
        {
            Findings = findings;
            ExemptedOrganizationBlocks = exemptedOrganizationBlocks;
            Examined = examined;
        }

        /// <summary>What is wrong.</summary>
        public IReadOnlyList<NumberFinding> Findings { get; }

        /// <summary>
        /// The organisation blocks the band does not apply to, BY NAME. An OB's number is fixed by its
        /// event class — identified, not chosen — so OB80 is not a band violation and must not be
        /// reported as one; the spec names it as a harness object, so the very first correct project
        /// would otherwise produce a false finding.
        /// </summary>
        public IReadOnlyList<string> ExemptedOrganizationBlocks { get; }

        /// <summary>How many objects were looked at. Zero is reported, never read as clean.</summary>
        public int Examined { get; }

        /// <summary>TRUE when nothing is wrong AND something was actually examined.</summary>
        public bool Clean => Findings.Count == 0 && Examined > 0;

        /// <summary>TRUE when the audit was handed nothing. Its own state — never a pass.</summary>
        public bool NothingExamined => Examined == 0;

        /// <summary>The report, in full.</summary>
        public string Describe()
        {
            var lines = new List<string>();

            lines.Add(NothingExamined
                ? "NUMBER AUDIT: NOTHING EXAMINED — no numbered objects were supplied. That is not a " +
                  "clean project, it is an audit that ran over nothing (FI-44)."
                : "NUMBER AUDIT: " + Examined + " object(s) examined, " + Findings.Count + " finding(s), " +
                  ExemptedOrganizationBlocks.Count + " OB(s) exempted.");

            lines.AddRange(ExemptedOrganizationBlocks.Select(
                ob => "  EXEMPT " + ob + " — an OB's number is fixed by its event class, so the band " +
                      "does not apply to it. Listed rather than skipped."));

            lines.AddRange(Findings.Select(f => "  " + f));

            return string.Join(Environment.NewLine, lines.ToArray());
        }

        /// <inheritdoc />
        public override string ToString() => Describe();
    }

    /// <summary>The outcome of asking for a harness number.</summary>
    public sealed class NumberAllocation
    {
        private NumberAllocation(bool granted, int number, string slot, string reason)
        {
            Granted = granted;
            Number = number;
            Slot = slot;
            Reason = reason;
        }

        /// <summary>TRUE when a number was granted.</summary>
        public bool Granted { get; }

        /// <summary>The number, when granted. Zero otherwise — and zero is not a legal block number.</summary>
        public int Number { get; }

        /// <summary>The slot, e.g. <c>FC 910</c>.</summary>
        public string Slot { get; }

        /// <summary>Why it was granted or refused.</summary>
        public string Reason { get; }

        /// <inheritdoc />
        public override string ToString() =>
            (Granted ? "ALLOCATED " + Slot : "ALLOCATION REFUSED") + " :: " + Reason;

        internal static NumberAllocation Grant(string slot, int number, string reason) =>
            new NumberAllocation(true, number, slot, reason);

        internal static NumberAllocation Refuse(string reason) =>
            new NumberAllocation(false, 0, string.Empty, reason);
    }

    /// <summary>
    /// X-J's RUNTIME half — allocation inside the reserved range, and the audit that says whether the
    /// reservation is being honoured.
    /// </summary>
    /// <remarks>
    /// <para>
    /// *** THIS REFUSES; IT DOES NOT ADVISE. *** A convention that harness objects "use 900+" is a
    /// convention. Every route through this type is a refusal when the rule is broken:
    /// <see cref="Allocate"/> hands out numbers ONLY from inside the range and only ones nothing else
    /// holds; <see cref="Audit"/> reports a harness object outside the range and a deliverable inside
    /// it as findings, not warnings. Where the answer is "no", nothing is returned that a caller could
    /// mistake for a number.
    /// </para>
    /// <para>
    /// *** AND THE HONEST BOUNDARY OF THAT CLAIM. *** It refuses AT THIS COMPONENT. It cannot refuse
    /// what never comes through it: a hand-authored `.ir` carrying a number, or a `converter claim
    /// --allocate` run without the floor, both bypass it. The system-wide property is "unable to
    /// collide" only for objects that are allocated here and audited here before import — which is why
    /// <see cref="Audit"/> takes the WHOLE project's numbers rather than only the harness's, and is
    /// meant to run before the import that hard rule 4's gate has been measured to wave through.
    /// </para>
    /// </remarks>
    public sealed class HarnessNumberLedger
    {
        private readonly HarnessNumberRange _range;

        /// <param name="range">The spec-side reservation.</param>
        public HarnessNumberLedger(HarnessNumberRange range)
        {
            _range = range ?? throw new ArgumentNullException(nameof(range));
        }

        /// <summary>The reservation this ledger allocates within.</summary>
        public HarnessNumberRange Range => _range;

        /// <summary>
        /// Allocate the lowest free harness number in <paramref name="numberSpace"/>.
        /// </summary>
        /// <param name="numberSpace">FB, FC, OB or DB.</param>
        /// <param name="existing">
        /// Everything already numbered in the project — HARNESS AND DELIVERABLE ALIKE. Passing only the
        /// harness's own objects would allocate straight onto a deliverable squatting in the range,
        /// which is the collision this exists to prevent.
        /// </param>
        public NumberAllocation Allocate(string? numberSpace, IEnumerable<NumberedBlock>? existing)
        {
            var space = (numberSpace ?? string.Empty).Trim().ToUpperInvariant();

            if (space.Length == 0)
            {
                return NumberAllocation.Refuse(
                    "No number space was named. FB, FC, OB and DB are numbered independently, so " +
                    "'the next free number' is not a question that has an answer without one.");
            }

            if (!_range.CoversSpace(space))
            {
                return NumberAllocation.Refuse(
                    "The reservation " + _range.Describe() + " does not cover the " + space + " space, so " +
                    "there is no reserved number to hand out. Allocating outside the reservation is the " +
                    "thing this refuses to do.");
            }

            var taken = new HashSet<int>(
                (existing ?? Enumerable.Empty<NumberedBlock>())
                    .Where(b => b != null && string.Equals(b.NumberSpace, space, StringComparison.Ordinal))
                    .Select(b => b.Number));

            for (var candidate = _range.FirstNumber; candidate <= _range.LastNumber; candidate++)
            {
                if (taken.Contains(candidate))
                {
                    continue;
                }

                return NumberAllocation.Grant(
                    space + " " + candidate.ToString(CultureInfo.InvariantCulture),
                    candidate,
                    "The lowest free number inside " + _range.Describe() + ". Checked against EVERY " +
                    "numbered object in the project, harness and deliverable alike — checking only the " +
                    "harness's own would allocate straight onto a squatter.");
            }

            return NumberAllocation.Refuse(
                "The reserved range " + _range.Describe() + " is FULL in the " + space + " space (" +
                _range.Capacity + " number(s), all held). Widening it is a spec-side decision and is not " +
                "this type's to make — and allocating outside it would silently re-create the collision " +
                "the reservation exists to prevent.");
        }

        /// <summary>
        /// Audit the project's numbering against the reservation.
        /// </summary>
        /// <param name="blocks">Every numbered object in the project.</param>
        /// <param name="reservationsHeld">
        /// Slots this ledger believes are reserved — e.g. from the shared claims registry. A
        /// reservation whose object has vanished is its own finding.
        /// </param>
        public NumberAuditReport Audit(
            IEnumerable<NumberedBlock>? blocks,
            IEnumerable<string>? reservationsHeld = null)
        {
            var all = (blocks ?? Enumerable.Empty<NumberedBlock>()).Where(b => b != null).ToArray();
            var findings = new List<NumberFinding>();
            var exempted = new List<string>();

            foreach (var block in all)
            {
                var subject = block.ToString();

                // *** THE OB CARVE-OUT, AND IT COMES FIRST BECAUSE OTHERWISE THE VERY FIRST CORRECT
                // PROJECT PRODUCES A FALSE FINDING. *** The band omits the OB space, so without this an
                // OB80 generated by the harness would fall straight through to
                // HarnessObjectOutsideTheRange. An OB's number is fixed by its event class — it is
                // IDENTIFIED, not chosen — so it cannot be moved into a band and is not in violation of
                // one. It is EXEMPTED BY NAME rather than skipped, because a silent exemption and a
                // correct pass are indistinguishable.
                if (string.Equals(block.NumberSpace, "OB", StringComparison.Ordinal))
                {
                    exempted.Add(subject);
                    continue;
                }

                if (block.NumberSpace.Length == 0)
                {
                    findings.Add(new NumberFinding(
                        NumberDefect.NotANumberedKind,
                        subject,
                        "A " + block.Kind + " carries no block number, so it cannot be audited against a " +
                        "number reservation. Offering it as numbered hides whatever it really is."));
                    continue;
                }

                if (block.Owner == BlockOwner.Unknown)
                {
                    findings.Add(new NumberFinding(
                        NumberDefect.OwnerNotStated,
                        subject,
                        "The object does not say whether it is a harness object or a deliverable block. " +
                        "The reservation separates exactly those two populations, so an object in neither " +
                        "is checkable against nothing."));
                    continue;
                }

                var inside = _range.Contains(block);

                if (block.Owner == BlockOwner.Harness && !inside)
                {
                    findings.Add(new NumberFinding(
                        NumberDefect.HarnessObjectOutsideTheRange,
                        subject,
                        "A harness object at " + block.Slot + " sits outside " + _range.Describe() +
                        ". *** THIS IS THE STATE THAT MAKES A COLLISION POSSIBLE *** — the reservation " +
                        "separates the two populations only while both respect it, and a deliverable may " +
                        "legitimately be allocated this number."));
                }

                if (block.Owner == BlockOwner.Deliverable && inside)
                {
                    findings.Add(new NumberFinding(
                        NumberDefect.RangeOccupiedByAnOutsider,
                        subject,
                        "A deliverable block holds " + block.Slot + ", inside " + _range.Describe() +
                        ". The reservation is being squatted on, and the fix is to MOVE THE BLOCK — which " +
                        "is the opposite of the fix for a reservation whose object vanished."));
                }
            }

            foreach (var duplicate in all
                .Where(b => b.Slot.Length > 0 && !string.Equals(b.NumberSpace, "OB", StringComparison.Ordinal))
                .GroupBy(b => b.Slot, StringComparer.Ordinal)
                .Where(g => g.Count() > 1))
            {
                findings.Add(new NumberFinding(
                    NumberDefect.DuplicateNumber,
                    duplicate.Key,
                    "Held by " + duplicate.Count() + " objects: " +
                    string.Join(", ", duplicate.Select(b => b.Name.Length == 0 ? "<unnamed>" : b.Name).ToArray()) +
                    ". MEASURED 2026-08-13: TIA accepted exactly this and created both blocks — import " +
                    "exit 0, per-block compile CONSISTENT: yes, device compile Success, sanity-check " +
                    "OVERALL: HEALTHY. The hard-rule-4 gate passed green over it."));
            }

            var occupied = new HashSet<string>(
                all.Where(b => b.Slot.Length > 0 && !string.Equals(b.NumberSpace, "OB", StringComparison.Ordinal))
                   .Select(b => b.Slot),
                StringComparer.Ordinal);

            foreach (var slot in (reservationsHeld ?? Enumerable.Empty<string>())
                .Select(s => (s ?? string.Empty).Trim())
                .Where(s => s.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(s => s, StringComparer.Ordinal))
            {
                if (!occupied.Contains(slot))
                {
                    findings.Add(new NumberFinding(
                        NumberDefect.ReservationForAVanishedObject,
                        slot,
                        "A reservation is held for " + slot + " but nothing in the project occupies it. " +
                        "*** THIS IS NOT THE SAME FINDING AS A SQUATTER *** — the fix here is to RELEASE " +
                        "the reservation, where a squatter's fix is to move a block. `claims --check` " +
                        "draws the same line, and stale claims are reported there rather than " +
                        "auto-released."));
                }
            }

            return new NumberAuditReport(findings, exempted, all.Length);
        }
    }
}
