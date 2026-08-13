using System;
using System.Collections.Generic;
using System.Linq;

namespace Ladder.Wave
{
    /// <summary>
    /// What loop 2 decided about one submission. Three outcomes, and the zero value is the one that is
    /// NOT a pass.
    /// </summary>
    public enum AdmissionOutcome
    {
        /// <summary>
        /// *** THE SUBMISSION CARRIED NOTHING TO EXAMINE. *** Reported as its own outcome, never as an
        /// admission. `compile-all` once claimed a pass having examined nothing (FI-44) and this is the
        /// same shape: a submission with no objects has satisfied no gate, so calling it "admitted"
        /// would put a wave's worth of trust behind an empty set. The zero value, so a dropped or
        /// defaulted outcome reads as "nothing was examined" rather than "admitted".
        /// </summary>
        NothingSubmitted = 0,

        /// <summary>Refused, whole (DB-9). Every finding is listed, not merely the first.</summary>
        Refused = 1,

        /// <summary>Admitted, whole, into the queue named by <see cref="AdmissionDecision.Queue"/>.</summary>
        Admitted = 2,
    }

    /// <summary>Why a submission was refused. Each maps to something the submitting agent can fix.</summary>
    public enum AdmissionFindingKind
    {
        /// <summary>Not a finding.</summary>
        None = 0,

        /// <summary>The submission has no id, or no submitting agent.</summary>
        SubmissionNotIdentified = 1,

        /// <summary>Two objects in one submission share a name.</summary>
        DuplicateObjectName = 2,

        /// <summary>No admission evidence was supplied for an object at all.</summary>
        NoEvidence = 3,

        /// <summary>Evidence was supplied but the check was never run (<see cref="EvidenceOutcome.NotProvided"/>).</summary>
        CheckNotRun = 4,

        /// <summary>The check ran and failed. §1.2's whole point: a failing block is simply not in the next batch.</summary>
        CheckFailed = 5,

        /// <summary>
        /// The evidence is about different content from the object being submitted — the hashes
        /// disagree, or one of them is absent so no comparison was possible.
        /// </summary>
        EvidenceStale = 6,

        /// <summary>The change could not be routed into either queue (see <see cref="RoutingRefusal"/>).</summary>
        Unroutable = 7,
    }

    /// <summary>One reason a submission was refused, naming the object and what to do about it.</summary>
    public sealed class AdmissionFinding
    {
        /// <param name="kind">Which class of finding.</param>
        /// <param name="objectName">The object it is about, or empty for a submission-level finding.</param>
        /// <param name="detail">What is wrong, in a sentence an agent can act on.</param>
        public AdmissionFinding(AdmissionFindingKind kind, string? objectName, string detail)
        {
            if (kind == AdmissionFindingKind.None)
            {
                throw new ArgumentException(
                    "A finding must name a kind. A finding of kind None would be a refusal reason that " +
                    "reads as 'nothing wrong'.",
                    nameof(kind));
            }

            Kind = kind;
            ObjectName = (objectName ?? string.Empty).Trim();
            Detail = (detail ?? string.Empty).Trim();
        }

        /// <summary>Which class of finding.</summary>
        public AdmissionFindingKind Kind { get; }

        /// <summary>The object it is about; empty for a submission-level finding.</summary>
        public string ObjectName { get; }

        /// <summary>What is wrong.</summary>
        public string Detail { get; }

        /// <inheritdoc />
        public override string ToString() =>
            Kind + (ObjectName.Length == 0 ? string.Empty : " [" + ObjectName + "]") + ": " + Detail;
    }

    /// <summary>
    /// The result of loop 2 for one submission — admitted whole into one queue, refused whole with
    /// every reason, or nothing submitted at all.
    /// </summary>
    public sealed class AdmissionDecision
    {
        private AdmissionDecision(
            Submission submission,
            AdmissionOutcome outcome,
            DownloadQueue queue,
            IReadOnlyList<AdmissionFinding> findings,
            IReadOnlyList<RoutingVerdict> routing,
            string summary)
        {
            Submission = submission;
            Outcome = outcome;
            Queue = queue;
            Findings = findings;
            Routing = routing;
            Summary = summary;
        }

        /// <summary>The submission this decision is about.</summary>
        public Submission Submission { get; }

        /// <summary>Admitted, refused, or nothing submitted.</summary>
        public AdmissionOutcome Outcome { get; }

        /// <summary>
        /// Which queue an ADMITTED submission entered; <see cref="DownloadQueue.Unassigned"/> otherwise.
        /// </summary>
        public DownloadQueue Queue { get; }

        /// <summary>Every reason for a refusal — all of them, so an agent fixes them in one pass.</summary>
        public IReadOnlyList<AdmissionFinding> Findings { get; }

        /// <summary>The per-object routing verdicts, kept for the log even when the submission was refused.</summary>
        public IReadOnlyList<RoutingVerdict> Routing { get; }

        /// <summary>A sentence describing the decision.</summary>
        public string Summary { get; }

        /// <summary>TRUE only for <see cref="AdmissionOutcome.Admitted"/>.</summary>
        public bool Admitted => Outcome == AdmissionOutcome.Admitted;

        /// <summary>
        /// TRUE when the submission was admitted but held for a drain boundary because at least one of
        /// its objects is STOP-class.
        /// </summary>
        public bool DeferredAsAUnit => Admitted && Queue == DownloadQueue.DeferredQueue;

        /// <summary>The objects that routed to the deferred queue — the reason a submission is held.</summary>
        public IReadOnlyList<ChangedObject> StopClassObjects =>
            Routing.Where(r => r.Deferred).Select(r => r.ChangedObject).ToArray();

        /// <summary>One line for the log, carrying the outcome, the queue and the finding count.</summary>
        public string ToLogLine()
        {
            var id = Submission.Id.Length == 0 ? "<unidentified>" : Submission.Id;

            switch (Outcome)
            {
                case AdmissionOutcome.Admitted:
                    return "ADMITTED " + id + " -> " + Queue + " (" + Submission.Objects.Count +
                           " object(s)) :: " + Summary;

                case AdmissionOutcome.Refused:
                    return "REFUSED " + id + " (" + Findings.Count + " finding(s)) :: " + Summary;

                default:
                    return "NOTHING SUBMITTED " + id + " :: " + Summary;
            }
        }

        /// <summary>The log line plus every finding, one per line.</summary>
        public string Describe()
        {
            if (Findings.Count == 0)
            {
                return ToLogLine();
            }

            return ToLogLine() + Environment.NewLine +
                   string.Join(Environment.NewLine, Findings.Select(f => "  - " + f).ToArray());
        }

        /// <inheritdoc />
        public override string ToString() => ToLogLine();

        internal static AdmissionDecision NothingSubmitted(Submission submission, string summary) =>
            new AdmissionDecision(
                submission,
                AdmissionOutcome.NothingSubmitted,
                DownloadQueue.Unassigned,
                new AdmissionFinding[0],
                new RoutingVerdict[0],
                summary);

        internal static AdmissionDecision Refuse(
            Submission submission,
            IReadOnlyList<AdmissionFinding> findings,
            IReadOnlyList<RoutingVerdict> routing,
            string summary)
        {
            if (findings.Count == 0)
            {
                throw new InvalidOperationException(
                    "A refusal must carry at least one finding. A refusal with no findings is an agent " +
                    "being told 'no' with nothing to fix, and is indistinguishable in a log from a bug " +
                    "in the gate itself.");
            }

            return new AdmissionDecision(
                submission,
                AdmissionOutcome.Refused,
                DownloadQueue.Unassigned,
                findings,
                routing,
                summary);
        }

        internal static AdmissionDecision Admit(
            Submission submission,
            DownloadQueue queue,
            IReadOnlyList<RoutingVerdict> routing,
            string summary)
        {
            if (queue == DownloadQueue.Unassigned)
            {
                throw new InvalidOperationException(
                    "An admitted submission must name the queue it entered. Unreachable from " +
                    "AdmissionController; kept so an edit that forgets the queue cannot produce a " +
                    "decision that reads as admitted while belonging to neither queue.");
            }

            return new AdmissionDecision(submission, AdmissionOutcome.Admitted, queue, new AdmissionFinding[0], routing, summary);
        }
    }
}
