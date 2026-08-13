using System;
using System.Collections.Generic;
using System.Linq;

namespace Ladder.Wave
{
    /// <summary>
    /// Whether a model may be depended on yet. *** THE ZERO VALUE IS "NOT CHECKED", NOT "PASSED". ***
    /// </summary>
    /// <remarks>
    /// X-I: a wrong model does not produce one wrong test — it produces a consistent, plausible and
    /// wholly wrong picture of a piece of equipment. So the states below are kept apart because they
    /// call for different actions, and every one of them except the last two REFUSES the consumer.
    /// Pinned into exactly one bucket by a test over the whole enum.
    /// </remarks>
    public enum ModelReadiness
    {
        /// <summary>
        /// *** NOBODY LOOKED. *** No result was supplied for this model at all — not "there is no
        /// result", which is a fact somebody established, but "the question was never asked". The zero
        /// value, so a dropped or defaulted readiness never reads as passed.
        /// </summary>
        NotChecked = 0,

        /// <summary>The result set was consulted and contains nothing for this model. It has never been tested.</summary>
        NoResultExists = 1,

        /// <summary>
        /// A result exists but describes DIFFERENT CONTENT — the model changed since it was tested.
        /// Kept apart from <see cref="NoResultExists"/> deliberately: one calls for writing vectors,
        /// the other for re-running the ones that exist.
        /// </summary>
        ResultIsStale = 2,

        /// <summary>The model was tested against its current content and FAILED.</summary>
        ResultIsFailing = 3,

        /// <summary>
        /// The model is being tested BY A SLOT IN THIS SAME PLAN, ordered into an earlier wave set.
        /// *** ADMITS, BUT SEE <see cref="ModelOrderingPlan.RestsOnABetweenWaveSetGate"/> — THIS IS THE
        /// READING THE SPEC IS AMBIGUOUS ABOUT. ***
        /// </summary>
        TestedEarlierInThisPlan = 4,

        /// <summary>A current, passing result from an earlier wave. The unambiguous case.</summary>
        Passed = 5,
    }

    /// <summary>Facts about readiness states, pinned by a test over the whole enum.</summary>
    public static class ModelReadinessStates
    {
        /// <summary>TRUE for the two states that permit a consumer to be admitted.</summary>
        public static bool Admits(ModelReadiness readiness) =>
            readiness == ModelReadiness.Passed || readiness == ModelReadiness.TestedEarlierInThisPlan;

        /// <summary>TRUE for every state that refuses the consumer.</summary>
        public static bool Refuses(ModelReadiness readiness) => !Admits(readiness);

        /// <summary>
        /// TRUE when the refusal is because nobody established anything, as against a fact that WAS
        /// established and is bad. Different fixes: one is "run the check", the others are "fix the
        /// model" or "write vectors for it".
        /// </summary>
        public static bool IsAnAbsenceOfEvidence(ModelReadiness readiness) =>
            readiness == ModelReadiness.NotChecked || readiness == ModelReadiness.NoResultExists;
    }

    /// <summary>
    /// One model's test result, carrying the STAMP that makes it comparable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// *** A BOOLEAN "THE MODEL PASSED" IS A VERDICT SOMEBODY DECLARES; A STAMP IS A FACT THAT CAN BE
    /// COMPARED. *** This component has now had that distinction matter three times — the queue
    /// reload's hash re-gate, the coverage citation's content hash, and `layoutSetAfterImport` being
    /// made a stamp rather than a bool. The same trick applies here: the result carries the model's
    /// artifact hash AT THE TIME IT WAS TESTED, so "nobody tested it" and "tested a DIFFERENT version
    /// of it" come out as two facts rather than one missing tick.
    /// </para>
    /// <para>
    /// The verdict itself is the harness's; this type consumes it. Nothing here runs a model, computes
    /// a hash, or decides whether a model is faithful — M4's fidelity set-difference is the
    /// submission-side gate and lives with the design-for-testability skill.
    /// </para>
    /// </remarks>
    public sealed class ModelTestResult
    {
        /// <param name="modelName">Which model.</param>
        /// <param name="passed">Whether its own test wave passed.</param>
        /// <param name="testedArtifactHash">
        /// The model's content hash AT THE TIME IT WAS TESTED — `converter ir-hash` or equivalent. This
        /// is the stamp: it is compared against the model's hash NOW.
        /// </param>
        /// <param name="provenance">Which wave produced it, so a result can be traced.</param>
        public ModelTestResult(string? modelName, bool passed, string? testedArtifactHash, string? provenance)
        {
            ModelName = (modelName ?? string.Empty).Trim();
            Passed = passed;
            TestedArtifactHash = (testedArtifactHash ?? string.Empty).Trim();
            Provenance = (provenance ?? string.Empty).Trim();
        }

        /// <summary>Which model.</summary>
        public string ModelName { get; }

        /// <summary>Whether its own test wave passed.</summary>
        public bool Passed { get; }

        /// <summary>The content hash the result is about.</summary>
        public string TestedArtifactHash { get; }

        /// <summary>Which wave produced it.</summary>
        public string Provenance { get; }

        /// <inheritdoc />
        public override string ToString() =>
            ModelName + (Passed ? " PASSED" : " FAILED") + " at " +
            (TestedArtifactHash.Length == 0 ? "<no hash>" : TestedArtifactHash) +
            (Provenance.Length == 0 ? string.Empty : " [" + Provenance + "]");
    }

    /// <summary>What a model is, as far as the ordering needs to know.</summary>
    public sealed class ModelUnderTest
    {
        /// <param name="name">The model's name, as slots refer to it.</param>
        /// <param name="currentArtifactHash">
        /// Its content hash NOW. Compared against a result's stamp; when they differ the result is
        /// STALE rather than absent.
        /// </param>
        public ModelUnderTest(string? name, string? currentArtifactHash)
        {
            Name = (name ?? string.Empty).Trim();
            CurrentArtifactHash = (currentArtifactHash ?? string.Empty).Trim();
        }

        /// <summary>The model's name.</summary>
        public string Name { get; }

        /// <summary>Its content hash now.</summary>
        public string CurrentArtifactHash { get; }

        /// <inheritdoc />
        public override string ToString() =>
            Name + " @ " + (CurrentArtifactHash.Length == 0 ? "<no hash>" : CurrentArtifactHash);
    }

    /// <summary>One model's readiness, with the reason.</summary>
    public sealed class ModelReadinessVerdict
    {
        internal ModelReadinessVerdict(string modelName, ModelReadiness readiness, string reason)
        {
            ModelName = modelName;
            Readiness = readiness;
            Reason = reason;
        }

        /// <summary>Which model.</summary>
        public string ModelName { get; }

        /// <summary>Its readiness.</summary>
        public ModelReadiness Readiness { get; }

        /// <summary>Why.</summary>
        public string Reason { get; }

        /// <summary>TRUE when a consumer may depend on it.</summary>
        public bool Admits => ModelReadinessStates.Admits(Readiness);

        /// <inheritdoc />
        public override string ToString() => ModelName + ": " + Readiness + " — " + Reason;
    }

    /// <summary>
    /// X-I — is this model ready to be depended on? *** THE ANSWER IS COMPUTED FROM A STAMP, NEVER
    /// TAKEN FROM A CALLER'S ASSERTION. ***
    /// </summary>
    public static class ModelReadinessCheck
    {
        /// <summary>Decide one model's readiness.</summary>
        /// <param name="model">The model, with its CURRENT content hash.</param>
        /// <param name="results">Every model result the caller holds. Null means nobody looked.</param>
        /// <param name="testedEarlierInThisPlan">
        /// TRUE when a model slot in this same plan tests it and was ordered into an earlier wave set.
        /// COMPUTED by <see cref="ModelOrdering"/> from the plan, never supplied by a submitter.
        /// </param>
        public static ModelReadinessVerdict Check(
            ModelUnderTest model,
            IEnumerable<ModelTestResult>? results,
            bool testedEarlierInThisPlan = false)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (testedEarlierInThisPlan)
            {
                return new ModelReadinessVerdict(
                    model.Name,
                    ModelReadiness.TestedEarlierInThisPlan,
                    "A model slot in this plan tests it and is ordered into an earlier wave set (X-I " +
                    "rule 2). NOTE: this rests on the run loop refusing to start a later wave set after " +
                    "an earlier one failed — see ModelOrderingPlan.RestsOnABetweenWaveSetGate.");
            }

            if (results == null)
            {
                return new ModelReadinessVerdict(
                    model.Name,
                    ModelReadiness.NotChecked,
                    "No result set was supplied, so the question was never asked. That is not the same " +
                    "as establishing that no result exists, and neither is a pass (FI-44).");
            }

            var forModel = results
                .Where(r => r != null && string.Equals(r.ModelName, model.Name, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (forModel.Length == 0)
            {
                return new ModelReadinessVerdict(
                    model.Name,
                    ModelReadiness.NoResultExists,
                    "The result set was consulted and contains nothing for this model. *** AN UNTESTED " +
                    "MODEL IS NOT A PASSING MODEL *** — every result that depends on it would be " +
                    "conditioned on code nobody has verified, and a wrong model produces a consistent, " +
                    "plausible and wholly wrong picture of a piece of equipment (X-I).");
            }

            // *** THE STAMP COMPARISON, AND IT IS WHY THIS IS NOT A BOOLEAN. *** A result describes the
            // content it was obtained against. If the model has changed since, the result is about
            // something that no longer exists — a different fact from never having tested it.
            var current = forModel
                .Where(r => r.TestedArtifactHash.Length > 0 &&
                            model.CurrentArtifactHash.Length > 0 &&
                            string.Equals(r.TestedArtifactHash, model.CurrentArtifactHash, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (current.Length == 0)
            {
                return new ModelReadinessVerdict(
                    model.Name,
                    ModelReadiness.ResultIsStale,
                    "Result(s) exist but describe different content" +
                    (model.CurrentArtifactHash.Length == 0
                        ? " — and the model itself carries no current hash, so nothing could be compared."
                        : ": the model is at '" + model.CurrentArtifactHash + "' and the result(s) are at " +
                          string.Join(", ", forModel.Select(r => "'" + (r.TestedArtifactHash.Length == 0 ? "<none>" : r.TestedArtifactHash) + "'").Distinct(StringComparer.OrdinalIgnoreCase).ToArray()) +
                          ". The model changed since it was tested; re-run its vectors rather than write new ones."));
            }

            if (current.Any(r => !r.Passed))
            {
                return new ModelReadinessVerdict(
                    model.Name,
                    ModelReadiness.ResultIsFailing,
                    "The model was tested against its current content and FAILED. A consumer depending " +
                    "on it would return a result conditioned on a model known to be wrong, and a red " +
                    "there would be blamed on the block (§7a cause 1 versus cause 2).");
            }

            return new ModelReadinessVerdict(
                model.Name,
                ModelReadiness.Passed,
                "A passing result against the model's current content" +
                (current[0].Provenance.Length == 0 ? "." : " (" + current[0].Provenance + ")."));
        }
    }
}
