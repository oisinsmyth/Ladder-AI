using System;
using System.Collections.Generic;
using System.Linq;

namespace Ladder.Wave
{
    /// <summary>
    /// §7's four buckets. Every assertion lands in exactly one, and the wave reports all four.
    /// </summary>
    /// <remarks>
    /// <para>
    /// *** THERE IS NO `Unclassified` MEMBER, AND ITS ABSENCE IS THE ENFORCEMENT MECHANISM. *** Nobody
    /// ever writes "unclassified" — it is a SET DIFFERENCE:
    /// </para>
    /// <code>
    /// UNCLASSIFIED = enumerated − (COVERED ∪ OUT-OF-SCOPE ∪ DEFERRED ∪ UNTESTABLE-ON-RIG)
    /// </code>
    /// <para>
    /// An assertion cannot be left lying around in a state nobody noticed, because that state is not
    /// one anybody can write. Forgetting one does not produce a missing tick — *** it produces a
    /// non-zero residual and a failed gate. *** The precedent is exact: phase 2's `AddressesExamined`
    /// became a separate denominator because a rule that was correct and never established that it had
    /// run is not a check. A test pins by reflection that this enum has no member whose name mentions
    /// "unclassified", so a future edit cannot quietly make it writable.
    /// </para>
    /// <para>
    /// <see cref="NotStated"/> is the zero value and is REFUSED on any assignment. It is NOT the
    /// residual: the residual is a set of coverage units, computed elsewhere, and never a value of this
    /// enum.
    /// </para>
    /// </remarks>
    public enum CoverageBucket
    {
        /// <summary>
        /// Nobody stated a bucket. Refused on assignment — an assignment that names no bucket is not an
        /// assignment. The zero value, so a dropped or defaulted bucket cannot read as COVERED.
        /// </summary>
        NotStated = 0,

        /// <summary>A vector exists and cites this assertion.</summary>
        Covered = 1,

        /// <summary>
        /// Not a control-logic requirement at all — wiring correctness, HMI behaviour, operator
        /// procedure. These were out of scope for the functional reviewer too.
        /// </summary>
        OutOfScope = 2,

        /// <summary>
        /// Testable, but blocked or not yet written. Carries an owner and a date; count AND AGE are
        /// reported every wave, because ageing deferrals are the signal, not the classification.
        /// </summary>
        Deferred = 3,

        /// <summary>
        /// Genuinely untestable on the rig. *** SHOULD BE NEARLY EMPTY *** — a coil energising IS the
        /// output decision and the copy layer latches it, so a large population here is a finding to
        /// chase rather than a category to accept.
        /// </summary>
        UntestableOnRig = 4,
    }

    /// <summary>Why a bucket assignment was refused.</summary>
    public enum AssignmentDefect
    {
        /// <summary>Not a defect.</summary>
        None = 0,

        /// <summary>The assignment names no bucket.</summary>
        NoBucketStated = 1,

        /// <summary>The assignment names no coverage unit.</summary>
        NoUnitNamed = 2,

        /// <summary>The unit is not in the enumeration — a classification cannot extend the denominator.</summary>
        UnitNotInTheEnumeration = 3,

        /// <summary>The same unit is assigned to more than one bucket. Every assertion lands in EXACTLY one.</summary>
        AssignedToMoreThanOneBucket = 4,

        /// <summary>An escape-hatch bucket was assigned by the block's author or a vector's author.</summary>
        EscapeHatchAssignedByAnInterestedParty = 5,

        /// <summary>A DEFERRED assignment carries no owner, or no date.</summary>
        DeferralWithoutOwnerOrDate = 6,

        /// <summary>An assignment records no assigner at all, so the identity checks cannot run.</summary>
        NoAssignerRecorded = 7,
    }

    /// <summary>One assertion (qualified by instance) put into one bucket, by somebody, for a reason.</summary>
    public sealed class BucketAssignment
    {
        /// <param name="unitKey">The coverage unit — <c>REQ-014:3f9a1c</c> or <c>REQ-014:3f9a1c@Feeder_02</c>.</param>
        /// <param name="bucket">Which bucket.</param>
        /// <param name="assigner">Who assigned it. Required, so §4.3's check can run at all.</param>
        /// <param name="reason">Why.</param>
        /// <param name="deferredOwner">For DEFERRED: who owns it.</param>
        /// <param name="deferredSince">For DEFERRED: since when. The age is what carries the weight.</param>
        public BucketAssignment(
            string? unitKey,
            CoverageBucket bucket,
            string? assigner,
            string? reason = null,
            string? deferredOwner = null,
            DateTimeOffset? deferredSince = null)
        {
            UnitKey = (unitKey ?? string.Empty).Trim();
            Bucket = bucket;
            Assigner = (assigner ?? string.Empty).Trim();
            Reason = (reason ?? string.Empty).Trim();
            DeferredOwner = (deferredOwner ?? string.Empty).Trim();
            DeferredSince = deferredSince;
        }

        /// <summary>The coverage unit.</summary>
        public string UnitKey { get; }

        /// <summary>Which bucket.</summary>
        public CoverageBucket Bucket { get; }

        /// <summary>Who assigned it.</summary>
        public string Assigner { get; }

        /// <summary>Why.</summary>
        public string Reason { get; }

        /// <summary>For DEFERRED: the owner.</summary>
        public string DeferredOwner { get; }

        /// <summary>For DEFERRED: since when.</summary>
        public DateTimeOffset? DeferredSince { get; }

        /// <summary>
        /// TRUE for the two buckets §4.3 puts out of the interested parties' reach. Pushing a hard
        /// assertion into either is the laundering route, and it is the one that needs a disinterested
        /// signer.
        /// </summary>
        public bool IsEscapeHatch =>
            Bucket == CoverageBucket.OutOfScope || Bucket == CoverageBucket.UntestableOnRig;

        /// <summary>How long this deferral has been sitting, or null when it is not a deferral.</summary>
        public TimeSpan? Age(DateTimeOffset now) =>
            Bucket == CoverageBucket.Deferred && DeferredSince.HasValue
                ? now.ToUniversalTime() - DeferredSince.Value.ToUniversalTime()
                : (TimeSpan?)null;

        /// <inheritdoc />
        public override string ToString() =>
            UnitKey + " -> " + Bucket + " (by " + (Assigner.Length == 0 ? "<nobody>" : Assigner) + ")";
    }

    /// <summary>One thing wrong with a classification.</summary>
    public sealed class AssignmentFinding
    {
        internal AssignmentFinding(AssignmentDefect defect, string subject, string detail)
        {
            Defect = defect;
            Subject = subject;
            Detail = detail;
        }

        /// <summary>Which defect.</summary>
        public AssignmentDefect Defect { get; }

        /// <summary>The unit or assigner it is about.</summary>
        public string Subject { get; }

        /// <summary>What is wrong.</summary>
        public string Detail { get; }

        /// <inheritdoc />
        public override string ToString() => Defect + " [" + Subject + "]: " + Detail;
    }

    /// <summary>
    /// THE CLASSIFICATION — a SEPARATE ARTIFACT from the enumeration, on purpose.
    /// </summary>
    /// <remarks>
    /// The residual is computed ACROSS the two. Holding the bucket inside the enumeration would make
    /// "unclassified" a field that could be left blank, and a blank field is a state somebody can
    /// forget to fill in without anything noticing. Two artifacts differenced against each other have
    /// no such state.
    /// </remarks>
    public sealed class CoverageClassification
    {
        /// <param name="assignments">The bucket assignments.</param>
        public CoverageClassification(IEnumerable<BucketAssignment>? assignments)
        {
            Assignments = (assignments ?? Enumerable.Empty<BucketAssignment>()).Where(a => a != null).ToArray();
        }

        /// <summary>The assignments.</summary>
        public IReadOnlyList<BucketAssignment> Assignments { get; }

        /// <summary>The units this classification claims to have classified, whatever the bucket.</summary>
        public IReadOnlyCollection<string> ClassifiedUnitKeys =>
            new HashSet<string>(Assignments.Where(a => a.UnitKey.Length > 0).Select(a => a.UnitKey), StringComparer.Ordinal);

        /// <summary>
        /// Validates the assignments against the enumeration and the interested parties.
        /// </summary>
        /// <param name="enumeration">The denominator, so an assignment cannot name a unit that is not in it.</param>
        /// <param name="blockAuthor">The block's author — may not assign an escape-hatch bucket.</param>
        /// <param name="vectorAuthors">The vector authors — likewise.</param>
        public IReadOnlyList<AssignmentFinding> Validate(
            AssertionEnumeration enumeration,
            string? blockAuthor = null,
            IEnumerable<string>? vectorAuthors = null)
        {
            if (enumeration == null)
            {
                throw new ArgumentNullException(nameof(enumeration));
            }

            var findings = new List<AssignmentFinding>();
            var known = new HashSet<string>(enumeration.Units.Select(u => u.Key), StringComparer.Ordinal);

            var interested = new List<string>();
            if (!string.IsNullOrWhiteSpace(blockAuthor))
            {
                interested.Add(blockAuthor!.Trim());
            }

            interested.AddRange((vectorAuthors ?? Enumerable.Empty<string>())
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Select(a => a!.Trim()));

            foreach (var assignment in Assignments)
            {
                var subject = assignment.UnitKey.Length == 0 ? "<no unit>" : assignment.UnitKey;

                if (assignment.UnitKey.Length == 0)
                {
                    findings.Add(new AssignmentFinding(
                        AssignmentDefect.NoUnitNamed,
                        subject,
                        "The assignment names no coverage unit, so it classifies nothing while appearing " +
                        "in the classification's own count."));
                    continue;
                }

                if (assignment.Bucket == CoverageBucket.NotStated)
                {
                    findings.Add(new AssignmentFinding(
                        AssignmentDefect.NoBucketStated,
                        subject,
                        "The assignment names no bucket. 'Not stated' is not a bucket and is not the " +
                        "residual either — the residual is computed by set difference and can never be " +
                        "written down."));
                }

                if (!known.Contains(assignment.UnitKey))
                {
                    findings.Add(new AssignmentFinding(
                        AssignmentDefect.UnitNotInTheEnumeration,
                        subject,
                        "The enumeration contains no such coverage unit. A classification cannot extend " +
                        "the denominator any more than a citation can — otherwise the residual could be " +
                        "driven to zero by classifying things that do not exist."));
                }

                if (assignment.Assigner.Length == 0)
                {
                    findings.Add(new AssignmentFinding(
                        AssignmentDefect.NoAssignerRecorded,
                        subject,
                        "The assignment records no assigner, so §4.3's check — that an escape-hatch " +
                        "bucket was not assigned by an interested party — cannot run. An unrecorded " +
                        "identity is not a disinterested one."));
                }
                else if (assignment.IsEscapeHatch &&
                         interested.Any(i => string.Equals(i, assignment.Assigner, StringComparison.OrdinalIgnoreCase)))
                {
                    findings.Add(new AssignmentFinding(
                        AssignmentDefect.EscapeHatchAssignedByAnInterestedParty,
                        subject,
                        "'" + assignment.Assigner + "' assigned " + assignment.Bucket + ", and is the " +
                        "block's author or a vector's author. Both have an incentive to reach for the " +
                        "escape hatches under pressure, which is why §4.3 puts them with whoever signs " +
                        "off the architecture."));
                }

                if (assignment.Bucket == CoverageBucket.Deferred &&
                    (assignment.DeferredOwner.Length == 0 || !assignment.DeferredSince.HasValue))
                {
                    findings.Add(new AssignmentFinding(
                        AssignmentDefect.DeferralWithoutOwnerOrDate,
                        subject,
                        "A deferral must carry an owner AND a date. §7 is explicit that under pressure " +
                        "everything becomes DEFERRED while the gate still passes, and that what carries " +
                        "real weight is the owner, the date, and the AGE reported every wave."));
                }
            }

            foreach (var contested in Assignments
                .Where(a => a.UnitKey.Length > 0)
                .GroupBy(a => a.UnitKey, StringComparer.Ordinal)
                .Where(g => g.Select(a => a.Bucket).Distinct().Count() > 1))
            {
                findings.Add(new AssignmentFinding(
                    AssignmentDefect.AssignedToMoreThanOneBucket,
                    contested.Key,
                    "Assigned to " + string.Join(" and ", contested.Select(a => a.Bucket.ToString()).Distinct().ToArray()) +
                    ". Every assertion lands in EXACTLY one bucket; resolving this by taking the last " +
                    "one would let a second assignment quietly launder the first."));
            }

            return findings;
        }
    }
}
