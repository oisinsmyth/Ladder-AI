using System;
using System.Collections.Generic;
using System.Linq;

namespace Ladder.Wave
{
    /// <summary>
    /// DB-9's submission unit, in the part of it this component decides on: the objects a test set puts
    /// into the test project, submitted ATOMICALLY and admitted or refused WHOLE.
    /// </summary>
    /// <remarks>
    /// <para>
    /// DB-9's full unit is "the vectors, their observability declarations, and the blocks they target".
    /// The vectors and the observability declarations are NOT modelled here, because nothing in this
    /// component consumes them — the packer (DB-13) and the copy-layer generator do, and neither is
    /// built. What is modelled is the property that matters at admission: *** a partial submission is a
    /// REFUSAL with reasons, never a partial admission. ***
    /// </para>
    /// </remarks>
    public sealed class Submission
    {
        /// <param name="id">Stable identifier — it appears in every refusal, queue entry and drain log.</param>
        /// <param name="agent">Which agent submitted it, for the per-agent result routing (§2.4).</param>
        /// <param name="objects">
        /// The objects the submission puts into the test project. May be empty; that is reported as
        /// <see cref="AdmissionOutcome.NothingSubmitted"/> rather than admitted.
        /// </param>
        public Submission(string? id, string? agent, IEnumerable<ChangedObject>? objects)
        {
            Id = (id ?? string.Empty).Trim();
            Agent = (agent ?? string.Empty).Trim();
            Objects = (objects ?? Enumerable.Empty<ChangedObject>())
                .Where(o => o != null)
                .ToArray();
        }

        /// <summary>The submission's identifier. May be empty — refused at admission, with a reason.</summary>
        public string Id { get; }

        /// <summary>The submitting agent. May be empty — refused at admission, with a reason.</summary>
        public string Agent { get; }

        /// <summary>The objects it carries.</summary>
        public IReadOnlyList<ChangedObject> Objects { get; }

        /// <inheritdoc />
        public override string ToString() =>
            (Id.Length == 0 ? "<unidentified submission>" : Id) +
            " from " + (Agent.Length == 0 ? "<unnamed agent>" : Agent) +
            " carrying " + Objects.Count + " object(s)";
    }

    /// <summary>
    /// Whether a piece of admission evidence was produced, and what it said. The zero value is
    /// <see cref="NotProvided"/>, so silence never reads as a pass.
    /// </summary>
    public enum EvidenceOutcome
    {
        /// <summary>Nobody ran the check, or nobody recorded that they did. Refused.</summary>
        NotProvided = 0,

        /// <summary>The check ran and passed.</summary>
        Passed = 1,

        /// <summary>The check ran and failed.</summary>
        Failed = 2,
    }

    /// <summary>
    /// The evidence loop 2 admits on: this object PASSED PREFLIGHT and COMPILED CLEAN IN ISOLATION
    /// (§1.2, R6) — and both facts are about the content that is actually being submitted.
    /// </summary>
    /// <remarks>
    /// <para>
    /// *** THIS COMPONENT DOES NOT RUN THE CHECKS, AND THAT IS DELIBERATE. *** Preflight is
    /// `converter preflight`, and "compiles clean in isolation" is a per-block `openness-cli compile`
    /// against the test project — a Portal attach, a whitelist approval, and a device-side gate whose
    /// own defects (FI-52, the exit-code fix) are recorded elsewhere. What this library does is refuse
    /// to admit anything that cannot produce both answers. It is a GATE over attested results, and the
    /// only way through it is to attach the results.
    /// </para>
    /// <para>
    /// *** THE HASH IS WHAT MAKES THE EVIDENCE MEAN ANYTHING. *** "It compiled clean" is a claim about
    /// a state of the file, and an agent that compiles, then edits, then submits has evidence about
    /// something that no longer exists. The evidence therefore carries the hash of the artifact it was
    /// produced from — `converter ir-hash` is the tool the spec already names for this, and it is
    /// immune to SIDECAR and UId churn — and <see cref="AdmissionController"/> refuses when it does not
    /// match the object being submitted. Without that check the gate is a formality that a stale run
    /// satisfies.
    /// </para>
    /// </remarks>
    public sealed class AdmissionEvidence
    {
        /// <param name="objectName">Which object the evidence is about.</param>
        /// <param name="artifactHash">The content hash the checks were run against.</param>
        /// <param name="preflight">`converter preflight` — zero findings is <see cref="EvidenceOutcome.Passed"/>.</param>
        /// <param name="compiledCleanInIsolation">A per-block/per-type compile of THIS object, errors 0.</param>
        /// <param name="source">Where the evidence came from — a run id, a log path, a timestamp.</param>
        public AdmissionEvidence(
            string? objectName,
            string? artifactHash,
            EvidenceOutcome preflight,
            EvidenceOutcome compiledCleanInIsolation,
            string? source = null)
        {
            ObjectName = (objectName ?? string.Empty).Trim();
            ArtifactHash = (artifactHash ?? string.Empty).Trim();
            Preflight = preflight;
            CompiledCleanInIsolation = compiledCleanInIsolation;
            Source = (source ?? string.Empty).Trim();
        }

        /// <summary>Which object it is about.</summary>
        public string ObjectName { get; }

        /// <summary>The content hash the checks ran against.</summary>
        public string ArtifactHash { get; }

        /// <summary>The preflight result.</summary>
        public EvidenceOutcome Preflight { get; }

        /// <summary>The isolated-compile result.</summary>
        public EvidenceOutcome CompiledCleanInIsolation { get; }

        /// <summary>Where the evidence came from.</summary>
        public string Source { get; }

        /// <inheritdoc />
        public override string ToString() =>
            (ObjectName.Length == 0 ? "<unnamed>" : ObjectName) +
            ": preflight=" + Preflight + " compile=" + CompiledCleanInIsolation +
            " hash=" + (ArtifactHash.Length == 0 ? "<none>" : ArtifactHash) +
            (Source.Length == 0 ? string.Empty : " [" + Source + "]");
    }
}
