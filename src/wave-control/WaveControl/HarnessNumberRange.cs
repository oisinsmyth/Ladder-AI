using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Ladder.Wave
{
    /// <summary>Who a numbered object belongs to.</summary>
    /// <remarks>
    /// The zero value is <see cref="Unknown"/> and it is REFUSED wherever it matters: the whole
    /// reserved-range argument is "a harness object and a deliverable block cannot collide", and an
    /// object that will not say which it is cannot be held to either side of that.
    /// </remarks>
    public enum BlockOwner
    {
        /// <summary>Not stated. Refused — an unowned object can be checked against nothing.</summary>
        Unknown = 0,

        /// <summary>Plant logic. What ships. Must live OUTSIDE the reserved range.</summary>
        Deliverable = 1,

        /// <summary>
        /// Coordinator-generated: the copy layer, the models, the `%MW` mirror scaffolding, OB80 and
        /// the per-test instance DBs. Must live INSIDE the reserved range.
        /// </summary>
        Harness = 2,
    }

    /// <summary>One numbered object in the project.</summary>
    public sealed class NumberedBlock
    {
        /// <param name="name">The block's name.</param>
        /// <param name="kind">What kind it is — decides which number space it occupies.</param>
        /// <param name="number">Its number.</param>
        /// <param name="owner">Harness or deliverable.</param>
        public NumberedBlock(string? name, ObjectKind kind, int number, BlockOwner owner)
        {
            Name = (name ?? string.Empty).Trim();
            Kind = kind;
            Number = number;
            Owner = owner;
        }

        /// <summary>The block's name.</summary>
        public string Name { get; }

        /// <summary>What kind it is.</summary>
        public ObjectKind Kind { get; }

        /// <summary>Its number.</summary>
        public int Number { get; }

        /// <summary>Harness or deliverable.</summary>
        public BlockOwner Owner { get; }

        /// <summary>
        /// Which number space it occupies, or empty when the kind is not numbered. FB, FC, OB and DB
        /// each have their OWN space — <c>FC 910</c> and <c>DB 910</c> coexist happily, and only
        /// <c>FC 910</c> against <c>FC 910</c> is a collision.
        /// </summary>
        public string NumberSpace => NumberSpaceOf(Kind);

        /// <summary>The collision key: number space plus number.</summary>
        public string Slot => NumberSpace.Length == 0
            ? string.Empty
            : NumberSpace + " " + Number.ToString(CultureInfo.InvariantCulture);

        /// <summary>Which number space a kind occupies; empty for kinds that carry no number.</summary>
        public static string NumberSpaceOf(ObjectKind kind)
        {
            switch (kind)
            {
                case ObjectKind.FunctionBlock: return "FB";
                case ObjectKind.Function: return "FC";
                case ObjectKind.OrganizationBlock: return "OB";

                // Global and instance DBs share ONE space. An instance DB at 910 collides with a global
                // DB at 910, which is exactly the shape of the measured duplicate.
                case ObjectKind.GlobalDataBlock:
                case ObjectKind.InstanceDataBlock:
                    return "DB";

                default: return string.Empty;
            }
        }

        /// <inheritdoc />
        public override string ToString() =>
            (Name.Length == 0 ? "<unnamed>" : Name) + " " + (Slot.Length == 0 ? Kind.ToString() : Slot) +
            " [" + Owner + "]";
    }

    /// <summary>
    /// X-J — *** A NUMBER RANGE RESERVED FOR HARNESS-GENERATED OBJECTS. *** The SPEC-SIDE statement:
    /// which numbers belong to the harness, who said so, and why.
    /// </summary>
    /// <remarks>
    /// <para>
    /// *** WHY THIS EXISTS, AND IT WAS MEASURED RATHER THAN FEARED (2026-08-13). *** TIA ACCEPTED AN
    /// IMPORT DECLARING FC 910 WHILE ANOTHER BLOCK ALREADY HELD 910, AND CREATED TWO BLOCKS AT THAT
    /// NUMBER — `import` exit 0, per-block compile exit 0 with `CONSISTENT: yes`, device compile
    /// `Success`, and `sanity-check` `OVERALL: HEALTHY`. *** THE HARD-RULE-4 GATE PASSED GREEN OVER A
    /// DUPLICATE. *** `sanity-check` now catches it at exit 19, and *** DETECTION IS NOT PREVENTION.
    /// *** This is the prevention.
    /// </para>
    /// <para>
    /// X-J offers two treatments — the coordinator claims like anybody else, or a range is reserved
    /// and the claim tool refuses allocations inside it. The reserved range is chosen for X-J's own
    /// second reason: *** harness objects become recognisable BY NUMBER in every listing ***, which is
    /// also what tells DB-7's cleanup what it owns.
    /// </para>
    /// <para>
    /// *** SPEC-SIDE HERE, RUNTIME ELSEWHERE. *** Declaring the range is a statement about the program
    /// and is immutable once made; ALLOCATING within it is a runtime act and lives in
    /// <see cref="HarnessNumberLedger"/>. Keeping them apart is the same separation that made the
    /// coverage denominator honest — a declaration that could be edited at allocation time would be a
    /// preference, not a reservation.
    /// </para>
    /// <para>
    /// *** WHAT THIS DOES NOT DO, STATED PLAINLY: IT DOES NOT RE-SOLVE THE MULTI-AGENT HALF. ***
    /// `converter claim` already does that, and its `--claims &lt;dir&gt;` is required with no default
    /// precisely because a per-worktree claims dir is always empty, grants every claim, and turns the
    /// registry into a no-op that looks like success. This type INTEROPERATES with it —
    /// <see cref="ClaimArgumentsFor"/> renders the exact invocation — and holds no registry of its own.
    /// Two agents racing for one number is that tool's problem; a harness object and a deliverable
    /// block occupying one number is this one's.
    /// </para>
    /// </remarks>
    public sealed class HarnessNumberRange
    {
        /// <param name="firstNumber">The first reserved number, inclusive.</param>
        /// <param name="lastNumber">The last reserved number, inclusive.</param>
        /// <param name="numberSpaces">
        /// Which number spaces the reservation covers — <c>FB</c>, <c>FC</c>, <c>OB</c>, <c>DB</c>.
        /// REQUIRED and non-empty: each space is numbered independently, so a range reserved in none of
        /// them reserves nothing while looking like a reservation.
        /// </param>
        /// <param name="declaredBy">Who reserved it. A reservation nobody made is a convention.</param>
        /// <param name="reason">Why, for the listing a human reads at 3am.</param>
        public HarnessNumberRange(
            int firstNumber,
            int lastNumber,
            IEnumerable<string>? numberSpaces,
            string? declaredBy,
            string? reason)
        {
            if (firstNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(firstNumber), "Block numbers start at 1.");
            }

            if (lastNumber < firstNumber)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lastNumber),
                    "The range must contain at least one number. An empty range refuses nothing while " +
                    "reading as a reservation.");
            }

            var spaces = (numberSpaces ?? Enumerable.Empty<string>())
                .Select(s => (s ?? string.Empty).Trim().ToUpperInvariant())
                .Where(s => s.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToArray();

            if (spaces.Length == 0)
            {
                throw new ArgumentException(
                    "A reservation must name the number space(s) it covers. FB, FC, OB and DB are " +
                    "numbered INDEPENDENTLY, so a range covering none of them reserves nothing — and it " +
                    "would read in every listing as though it did.",
                    nameof(numberSpaces));
            }

            var authority = (declaredBy ?? string.Empty).Trim();
            if (authority.Length == 0)
            {
                throw new ArgumentException(
                    "A reservation must say who made it. A range nobody declared is a convention, and a " +
                    "convention is what X-J exists to replace.",
                    nameof(declaredBy));
            }

            FirstNumber = firstNumber;
            LastNumber = lastNumber;
            NumberSpaces = spaces;
            DeclaredBy = authority;
            Reason = (reason ?? string.Empty).Trim();
        }

        /// <summary>The first reserved number, inclusive.</summary>
        public int FirstNumber { get; }

        /// <summary>The last reserved number, inclusive.</summary>
        public int LastNumber { get; }

        /// <summary>Which number spaces the reservation covers.</summary>
        public IReadOnlyList<string> NumberSpaces { get; }

        /// <summary>Who declared it.</summary>
        public string DeclaredBy { get; }

        /// <summary>Why.</summary>
        public string Reason { get; }

        /// <summary>How many numbers it holds, per space.</summary>
        public int Capacity => LastNumber - FirstNumber + 1;

        /// <summary>TRUE when this reservation covers <paramref name="numberSpace"/>.</summary>
        public bool CoversSpace(string? numberSpace) =>
            !string.IsNullOrWhiteSpace(numberSpace) &&
            NumberSpaces.Contains(numberSpace!.Trim().ToUpperInvariant(), StringComparer.Ordinal);

        /// <summary>TRUE when the number falls inside the reserved band, ignoring the space.</summary>
        public bool ContainsNumber(int number) => number >= FirstNumber && number <= LastNumber;

        /// <summary>TRUE when this exact block sits inside the reservation.</summary>
        public bool Contains(NumberedBlock block) =>
            block != null && CoversSpace(block.NumberSpace) && ContainsNumber(block.Number);

        /// <summary>
        /// The `converter claim` invocation that reserves one number in the EXISTING registry — the
        /// interoperation point. This type does not allocate across agents; that tool does.
        /// </summary>
        /// <remarks>
        /// Rendered rather than executed: this library references nothing and shells out to nothing
        /// (the converter is a pure in-process file transformer by owner ruling, and env-touching
        /// belongs elsewhere). The caller runs it, supplying the shared <c>--claims</c> directory that
        /// the registry requires and that has no default on purpose.
        /// </remarks>
        public string ClaimArgumentsFor(NumberedBlock block, string agentId)
        {
            if (block == null)
            {
                throw new ArgumentNullException(nameof(block));
            }

            if (string.IsNullOrWhiteSpace(agentId))
            {
                throw new ArgumentException(
                    "The claim registry keys reservations on an agent id; without one the claim cannot " +
                    "be attributed or released.",
                    nameof(agentId));
            }

            return "claim --agent " + agentId.Trim() +
                   " --kind block-number --value " + block.Slot.Replace(" ", string.Empty) +
                   " --purpose \"harness object " + block.Name + ", inside the X-J reserved range " +
                   Describe() + "\"";
        }

        /// <summary>The floor a harness ALLOCATION starts from, for `claim --allocate --floor`.</summary>
        public int AllocationFloor => FirstNumber;

        /// <summary>A one-line description for a listing.</summary>
        public string Describe() =>
            string.Join("/", NumberSpaces.ToArray()) + " " + FirstNumber + "-" + LastNumber +
            " reserved for harness objects by " + DeclaredBy +
            (Reason.Length == 0 ? string.Empty : " (" + Reason + ")");

        /// <inheritdoc />
        public override string ToString() => Describe();
    }
}
