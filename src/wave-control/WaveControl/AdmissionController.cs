using System;
using System.Collections.Generic;
using System.Linq;

namespace Ladder.Wave
{
    /// <summary>
    /// LOOP 2 — admission (§1.2, R6, R3/D17). A submission enters the test project only after every
    /// object in it has PASSED PREFLIGHT and COMPILED CLEAN IN ISOLATION, and only if every change in
    /// it can be routed into one of D23's two queues.
    /// </summary>
    /// <remarks>
    /// <para>
    /// *** WHY THIS GATE EXISTS AT ALL, AND IT IS NOT BUREAUCRACY. *** Openness cannot download a
    /// selection [M] — every download is whole-software — so THE WHOLE PROJECT MUST COMPILE BEFORE
    /// ANYONE'S DOWNLOAD. Without admission control one agent's broken block blocks every agent. A
    /// failing block is simply not in the next batch.
    /// </para>
    /// <para>
    /// *** ATOMIC, PER DB-9: A PARTIAL SUBMISSION IS A REFUSAL WITH REASONS, NEVER A PARTIAL
    /// ADMISSION. *** One bad object refuses the submission it is in. Every finding is reported, not
    /// just the first, because an agent that fixes one defect per round trip costs a wave per defect.
    /// </para>
    /// <para>
    /// *** THE RULING THIS COMPONENT MAKES, AND IT IS A READING OF THE SPEC RATHER THAN A QUOTE FROM
    /// IT. *** §1.4 routes CHANGES, one at a time, into two queues; DB-9 admits SUBMISSIONS whole.
    /// Those two are silent about the case where one submission's objects route BOTH ways. This
    /// component holds the whole submission to the LATER queue: if any object is STOP-class, none of
    /// its objects enters a wave-boundary batch and the submission waits for the drain, as a unit.
    /// </para>
    /// <para>
    /// The argument: the testable unit is the submission (DB-9), so splitting it across a wave boundary
    /// and a drain boundary would leave the test project holding half a test set — and the wave in
    /// between could run a test against it and return a green that means nothing. The cost of the
    /// other reading is a cost §1.4 has already accepted in words: "an agent whose work needs a
    /// STOP-class change waits longer, because it cannot be tested until the next disruptive boundary."
    /// So the reading taken pays a cost the spec has already priced, and the alternative risks a silent
    /// wrong green, which is the failure this whole design exists to avoid.
    /// </para>
    /// <para>
    /// WHAT THIS DOES NOT CHECK, deliberately: DEPENDENCY CLOSURE. A submission's dependency may be
    /// satisfied by another agent's submission admitted into the same wave, so closure is not a
    /// property of one submission in isolation — it is decided over the whole change set by
    /// <see cref="WaveBoundaryBatchPlanner"/>. Nor does it check the CPU-memory budget: X-L's ruling
    /// retired the work/load gates in favour of minimality, keeping only the RETAIN rule, which is
    /// checkable from the IR and belongs with the harness generator.
    /// </para>
    /// </remarks>
    public static class AdmissionController
    {
        /// <summary>
        /// Admit or refuse one submission.
        /// </summary>
        /// <param name="submission">The submission (DB-9).</param>
        /// <param name="evidence">
        /// The preflight and isolated-compile results, one per object. An object with no entry here is
        /// refused: the gate is over ATTESTED results, and silence is not an attestation.
        /// </param>
        public static AdmissionDecision Admit(Submission submission, IEnumerable<AdmissionEvidence>? evidence)
        {
            if (submission == null)
            {
                throw new ArgumentNullException(nameof(submission));
            }

            var findings = new List<AdmissionFinding>();
            var routing = new List<RoutingVerdict>();

            if (submission.Objects.Count == 0)
            {
                return AdmissionDecision.NothingSubmitted(
                    submission,
                    "The submission carried no objects. Nothing was examined, so nothing was admitted — " +
                    "an empty submission passes no gate and must not be recorded as one (FI-44).");
            }

            if (submission.Id.Length == 0)
            {
                findings.Add(new AdmissionFinding(
                    AdmissionFindingKind.SubmissionNotIdentified,
                    null,
                    "The submission has no id. Every queue entry, refusal and drain log line refers to " +
                    "it by id, so an unidentified submission cannot be tracked through a boundary."));
            }

            if (submission.Agent.Length == 0)
            {
                findings.Add(new AdmissionFinding(
                    AdmissionFindingKind.SubmissionNotIdentified,
                    null,
                    "The submission names no agent. Results are distributed per agent (§2.4) and there " +
                    "would be nobody to distribute these to."));
            }

            var duplicateNames = submission.Objects
                .Where(o => o.Name.Length > 0)
                .GroupBy(o => o.Name, ChangedObject.NameComparer)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToArray();

            foreach (var duplicate in duplicateNames)
            {
                findings.Add(new AdmissionFinding(
                    AdmissionFindingKind.DuplicateObjectName,
                    duplicate,
                    "Two or more changes in this submission name the same object. Which one would be " +
                    "downloaded is undecidable, and the object count DB-4 budgets against would be wrong."));
            }

            var evidenceByObject = BuildEvidenceIndex(evidence);

            foreach (var changedObject in submission.Objects)
            {
                var verdict = ChangeRouter.Route(changedObject);
                routing.Add(verdict);

                if (!verdict.Routed)
                {
                    findings.Add(new AdmissionFinding(
                        AdmissionFindingKind.Unroutable,
                        changedObject.Name,
                        verdict.Reason));
                }

                CheckEvidence(changedObject, evidenceByObject, findings);
            }

            if (findings.Count > 0)
            {
                return AdmissionDecision.Refuse(
                    submission,
                    findings,
                    routing,
                    "Refused whole (DB-9): a partial submission is a refusal with reasons, never a " +
                    "partial admission. " + findings.Count + " finding(s) across " +
                    submission.Objects.Count + " object(s).");
            }

            var deferred = routing.Where(r => r.Deferred).Select(r => r.ChangedObject.Name).ToArray();

            if (deferred.Length > 0)
            {
                return AdmissionDecision.Admit(
                    submission,
                    DownloadQueue.DeferredQueue,
                    routing,
                    "Admitted, and HELD AS A UNIT for the drain boundary: " + deferred.Length +
                    " of its " + submission.Objects.Count + " object(s) are STOP-class (" +
                    string.Join(", ", deferred) + "). Splitting the submission across a wave boundary " +
                    "and a drain would leave the test project holding half a test set, and a wave in " +
                    "between could test against it (D23 routes changes; DB-9 admits submissions whole).");
            }

            return AdmissionDecision.Admit(
                submission,
                DownloadQueue.RunQueue,
                routing,
                "Admitted: every object passed preflight and compiled clean in isolation against the " +
                "content submitted, and every change is RUN-class, so the submission flows through a " +
                "normal wave boundary (§1.2, R6, D23).");
        }

        private static Dictionary<string, List<AdmissionEvidence>> BuildEvidenceIndex(IEnumerable<AdmissionEvidence>? evidence)
        {
            var index = new Dictionary<string, List<AdmissionEvidence>>(ChangedObject.NameComparer);

            foreach (var item in (evidence ?? Enumerable.Empty<AdmissionEvidence>()).Where(e => e != null))
            {
                if (item.ObjectName.Length == 0)
                {
                    continue;
                }

                List<AdmissionEvidence> forObject;
                if (!index.TryGetValue(item.ObjectName, out forObject))
                {
                    forObject = new List<AdmissionEvidence>();
                    index[item.ObjectName] = forObject;
                }

                forObject.Add(item);
            }

            return index;
        }

        private static void CheckEvidence(
            ChangedObject changedObject,
            Dictionary<string, List<AdmissionEvidence>> evidenceByObject,
            List<AdmissionFinding> findings)
        {
            if (changedObject.Name.Length == 0)
            {
                // Already reported as unroutable; there is no key to look evidence up by, and a second
                // finding saying "no evidence for <unnamed>" would add noise rather than information.
                return;
            }

            List<AdmissionEvidence> candidates;
            if (!evidenceByObject.TryGetValue(changedObject.Name, out candidates) || candidates.Count == 0)
            {
                findings.Add(new AdmissionFinding(
                    AdmissionFindingKind.NoEvidence,
                    changedObject.Name,
                    "No admission evidence was supplied. Loop 2 admits on ATTESTED results — " +
                    "`converter preflight` and a per-block compile of this object — and an object with " +
                    "no evidence has passed no gate. Absence is not a pass (FI-44)."));
                return;
            }

            // MORE THAN ONE piece of evidence for one object is not resolved by picking the best of
            // them. It means two runs are being offered for the same object and the caller has not said
            // which describes the submitted content, so the hash check below decides — and if none of
            // them matches, the object is refused as stale.
            var matching = candidates
                .Where(e => e.ArtifactHash.Length > 0 &&
                            changedObject.ArtifactHash.Length > 0 &&
                            string.Equals(e.ArtifactHash, changedObject.ArtifactHash, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (matching.Length == 0)
            {
                findings.Add(new AdmissionFinding(
                    AdmissionFindingKind.EvidenceStale,
                    changedObject.Name,
                    DescribeHashMismatch(changedObject, candidates)));
                return;
            }

            foreach (var attested in matching)
            {
                CheckOne(changedObject.Name, "preflight (`converter preflight`, zero findings)", attested.Preflight, attested, findings);
                CheckOne(changedObject.Name, "compiled clean in isolation (per-block compile, errors 0)", attested.CompiledCleanInIsolation, attested, findings);
            }
        }

        private static void CheckOne(
            string objectName,
            string checkName,
            EvidenceOutcome outcome,
            AdmissionEvidence attested,
            List<AdmissionFinding> findings)
        {
            switch (outcome)
            {
                case EvidenceOutcome.Passed:
                    return;

                case EvidenceOutcome.Failed:
                    findings.Add(new AdmissionFinding(
                        AdmissionFindingKind.CheckFailed,
                        objectName,
                        "Failed " + checkName + ". §1.2: a failing block is simply not in the next batch — " +
                        "it would otherwise stop every other agent's download, because Openness cannot " +
                        "download a selection. Evidence: " + attested));
                    return;

                default:
                    findings.Add(new AdmissionFinding(
                        AdmissionFindingKind.CheckNotRun,
                        objectName,
                        "The check '" + checkName + "' was never run, or its result was never recorded. " +
                        "That is not the same as passing it, and it must not be admitted as though it " +
                        "were. Evidence: " + attested));
                    return;
            }
        }

        private static string DescribeHashMismatch(ChangedObject changedObject, List<AdmissionEvidence> candidates)
        {
            if (changedObject.ArtifactHash.Length == 0)
            {
                return "The submitted object carries no artifact hash, so no evidence can be shown to be " +
                       "about the content being submitted. `converter ir-hash` produces one and is immune " +
                       "to SIDECAR/UId churn. Without it, evidence from before an edit is indistinguishable " +
                       "from evidence about what is being submitted.";
            }

            var offered = candidates
                .Select(c => c.ArtifactHash.Length == 0 ? "<no hash>" : c.ArtifactHash)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return "The evidence is about different content. The object submits hash '" +
                   changedObject.ArtifactHash + "'; the evidence carries " +
                   string.Join(", ", offered.Select(o => "'" + o + "'").ToArray()) +
                   ". A clean compile of a previous edit is not evidence about this one.";
        }
    }
}
