using System;
using System.Collections.Generic;
using System.Linq;

namespace Ladder.Wave
{
    /// <summary>Why a model ordering could not be produced, or is wrong.</summary>
    public enum OrderingDefect
    {
        /// <summary>Not a defect.</summary>
        None = 0,

        /// <summary>
        /// A slot depends on a model that is not ready — see <see cref="ModelReadiness"/> for which
        /// flavour, because they call for different fixes.
        /// </summary>
        ConsumerDependsOnAModelThatIsNotReady = 1,

        /// <summary>
        /// A model slot and a slot that uses that model were placed in the SAME wave set. Reordering
        /// cannot fix this — they must be separated by the colouring, which is why the dependency is
        /// derived as a conflict edge.
        /// </summary>
        ModelAndConsumerShareAWaveSet = 2,

        /// <summary>
        /// *** THE MODEL'S WAVE SET RUNS AFTER ITS CONSUMER'S. *** The ordering is inverted, so the
        /// consumer's result is conditioned on a model that had not been tested when it ran.
        /// </summary>
        ConsumerRunsBeforeItsModel = 3,

        /// <summary>
        /// Two models depend on each other's slots. *** NO ORDER EXISTS *** — this is not a bad order
        /// but an unorderable set, and it is a real input rather than a hypothetical.
        /// </summary>
        CircularModelDependency = 4,

        /// <summary>A slot names a model that is in neither the plan nor the supplied model set.</summary>
        ModelNotSupplied = 5,

        /// <summary>Two slots claim to test the same model. Which result belongs to it is undecidable.</summary>
        TwoSlotsTestTheSameModel = 6,

        /// <summary>
        /// *** X-I READING (b) WAS USED AND THE STOP-ON-FAILED-WAVE-SET GATE IS NOT ESTABLISHED. ***
        /// A consumer admitted only because its model is tested earlier in the SAME plan depends on the
        /// run loop refusing to continue after a failed wave set. Without that gate the consumer's
        /// results would be produced and BELIEVED after its model failed — a wrong answer that looks
        /// like a result. Owner's ruling, 2026-08-13: (b) is permitted, but fails closed until the gate
        /// is real.
        /// </summary>
        ReadingBRefusedBecauseTheGateIsNotEstablished = 7,
    }

    /// <summary>Facts about ordering defects, pinned by a test over the whole enum.</summary>
    public static class OrderingDefects
    {
        /// <summary>
        /// TRUE for every defect except <see cref="OrderingDefect.None"/>. *** THERE IS NO
        /// INFORMATIONAL DEFECT, AND THERE MUST NOT BE ONE. *** A finding that is reported and does not
        /// gate is the "a warning is not a gate" shape, which gets skimmed; pinning this over the whole
        /// enum means a future member has to be classified deliberately rather than added quietly as an
        /// advisory.
        /// </summary>
        public static bool RefusesThePlan(OrderingDefect defect) => defect != OrderingDefect.None;
    }

    /// <summary>One thing wrong with a model ordering.</summary>
    public sealed class OrderingFinding
    {
        internal OrderingFinding(OrderingDefect defect, string subject, string detail)
        {
            Defect = defect;
            Subject = subject;
            Detail = detail;
        }

        /// <summary>Which defect.</summary>
        public OrderingDefect Defect { get; }

        /// <summary>The slot, model or pair it is about.</summary>
        public string Subject { get; }

        /// <summary>What is wrong.</summary>
        public string Detail { get; }

        /// <inheritdoc />
        public override string ToString() => Defect + " [" + Subject + "]: " + Detail;
    }

    /// <summary>The ordered wave sets, or the reasons there are none.</summary>
    public sealed class ModelOrderingPlan
    {
        /// <summary>
        /// *** X-I's OWN RESIDUAL, CARRIED WHERE A READER OF RESULTS MEETS IT RATHER THAN ONLY IN A
        /// LANE REPORT. *** Emitted by every plan, ordered or refused.
        /// </summary>
        public const string FidelityResidual =
            "X-I RESIDUAL: passing its own tests makes a model faithful to its SPECIFICATION, NOT TO " +
            "THE PLANT. The sign error, the unit error, the transposed coefficient are caught; THE " +
            "PHYSICS NOBODY THOUGHT TO INCLUDE IS NOT, and no amount of model testing will ever catch " +
            "it. M3/M4's fidelity declaration remains the only defence against it.";

        internal ModelOrderingPlan(
            IReadOnlyList<WaveSet> orderedWaveSets,
            IReadOnlyList<OrderingFinding> findings,
            IReadOnlyList<ModelReadinessVerdict> readiness,
            bool restsOnABetweenWaveSetGate,
            RunLoopGateState gateState,
            string summary)
        {
            OrderedWaveSets = orderedWaveSets;
            Findings = findings;
            Readiness = readiness;
            RestsOnABetweenWaveSetGate = restsOnABetweenWaveSetGate;
            GateState = gateState;
            Summary = summary;
        }

        /// <summary>The wave sets, in RUN ORDER. Empty when the ordering was refused.</summary>
        public IReadOnlyList<WaveSet> OrderedWaveSets { get; }

        /// <summary>Every reason for a refusal.</summary>
        public IReadOnlyList<OrderingFinding> Findings { get; }

        /// <summary>What was decided about each model depended on.</summary>
        public IReadOnlyList<ModelReadinessVerdict> Readiness { get; }

        /// <summary>
        /// 🔴 *** TRUE WHEN SOME CONSUMER IS ADMITTED ONLY BECAUSE ITS MODEL IS TESTED EARLIER IN THIS
        /// SAME PLAN — AND THE SPEC IS AMBIGUOUS ABOUT WHETHER THAT IS SUFFICIENT. ***
        /// </summary>
        /// <remarks>
        /// <para>
        /// X-I rule 2 says both of these, and they are not the same rule:
        /// </para>
        /// <list type="bullet">
        ///   <item><description>
        ///     "A MODEL MUST PASS BEFORE ANY SLOT THAT USES IT IS ADMITTED ... Enforced at admission
        ///     (R6)" — which reads as: the consumer is refused until a PASSING RESULT exists.
        ///   </description></item>
        ///   <item><description>
        ///     "model slots run in an EARLIER wave set than the slots depending on them" — which reads
        ///     as: they may be submitted together, and the ordering is what enforces it.
        ///   </description></item>
        /// </list>
        /// <para>
        /// *** THE SECOND READING HAS A HOLE THE FIRST DOES NOT: *** wave sets in one plan are ordered
        /// but still RUN sequentially, so if the model's wave set FAILS, the consumer's runs anyway
        /// unless something re-checks between wave sets. Nothing in the spec describes that gate, and
        /// nothing here builds it — this component decides admission, not the run loop.
        /// </para>
        /// <para>
        /// So both readings are supported, and this flag makes the dependence VISIBLE rather than
        /// picking one. A caller whose run loop does not stop on a failed wave set should treat a plan
        /// carrying this flag as unadmitted. *** RAISED FOR THE OWNER RATHER THAN DECIDED. ***
        /// </para>
        /// </remarks>
        public bool RestsOnABetweenWaveSetGate { get; }

        /// <summary>
        /// The state of the run loop's stop-on-failed-wave-set gate, as checked against the run loop in
        /// use. Only meaningful when <see cref="RestsOnABetweenWaveSetGate"/> is true — a plan that does
        /// not use reading (b) does not depend on it, and the gate is not a blanket requirement.
        /// </summary>
        public RunLoopGateState GateState { get; }

        /// <summary>A sentence describing the outcome.</summary>
        public string Summary { get; }

        /// <summary>TRUE when an order was produced.</summary>
        public bool Ordered => Findings.Count == 0 && OrderedWaveSets.Count > 0;

        /// <summary>The plan, in full.</summary>
        public string Describe()
        {
            var lines = new List<string>();

            lines.Add(Ordered
                ? "MODEL ORDER: " + OrderedWaveSets.Count + " wave set(s) in run order. " + Summary
                : "MODEL ORDERING REFUSED: " + Findings.Count + " finding(s). " + Summary);

            lines.AddRange(OrderedWaveSets.Select((w, i) => "  RUN " + i + ": " + w.ToLogLine()));
            lines.AddRange(Readiness.Select(r => "  MODEL " + r));
            lines.AddRange(Findings.Select(f => "  - " + f));

            if (RestsOnABetweenWaveSetGate)
            {
                lines.Add(GateState == RunLoopGateState.Established
                    ? "  RESTS ON THE BETWEEN-WAVE-SET GATE, WHICH IS ESTABLISHED: at least one consumer " +
                      "is admitted only because its model is tested EARLIER IN THIS PLAN (X-I reading b). " +
                      "That is permitted because the run loop is declared to STOP after a failed wave set " +
                      "— and the declaration is only as good as whoever supplied it."
                    : "  🔴 REFUSED — RESTS ON A BETWEEN-WAVE-SET GATE THAT IS " + GateState + ": a " +
                      "consumer is admitted only because its model is tested earlier in THIS plan, and " +
                      "wave sets run sequentially, so a failed model wave would not stop it. The results " +
                      "would be produced and BELIEVED after the model failed.");
            }

            lines.Add("  " + FidelityResidual);

            return string.Join(Environment.NewLine, lines.ToArray());
        }

        /// <inheritdoc />
        public override string ToString() => Describe();
    }

    /// <summary>
    /// X-I / 6.5 — *** MODELS ARE UNTESTED CODE THAT EVERY RESULT DEPENDS ON. *** The model-before-
    /// consumer ordering over wave sets.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A model stands in for equipment a test cannot drive directly, so a consumer's result is only
    /// meaningful if the model it depends on has itself been tested. X-I's answer needs NO NEW
    /// MACHINERY — a model is IR with inputs and outputs, which is exactly what a slot tests, so the
    /// self-checks simply BECOME TEST VECTORS. Three of its four rules are existing rules applied:
    /// D6 (the model's author does not write its vectors) is the same authorship check; §7's coverage
    /// transfers unchanged with the physical analysis as the specification; inert, start bools and the
    /// result package are identical because a model under test IS a block under test.
    /// </para>
    /// <para>
    /// *** ONLY RULE 2 IS NEW, AND IT IS AN ORDERING CONSTRAINT ON WAVE SETS. *** Run concurrently, a
    /// red result is ambiguous between the block and the model — §7a cause 1 versus cause 2, the exact
    /// ambiguity §7a exists to remove. So the dependency becomes TWO things:
    /// </para>
    /// <list type="number">
    ///   <item><description>
    ///     A CONFLICT EDGE, so the colouring separates them into different wave sets. Derived here by
    ///     <see cref="EdgesFor"/> — *** COMPUTED FROM THE DEPENDENCY, NEVER DECLARED BY A SUBMITTER. ***
    ///   </description></item>
    ///   <item><description>
    ///     A DIRECTION, so the model's wave set RUNS FIRST. Separation alone is not enough: a consumer
    ///     running before its model gets a result conditioned on code nobody has tested.
    ///   </description></item>
    /// </list>
    /// <para>
    /// *** AND THE RESIDUAL X-I ITSELF NAMES, REPEATED HERE BECAUSE IT LIMITS EVERYTHING ABOVE:
    /// PASSING ITS OWN TESTS MAKES A MODEL FAITHFUL TO ITS SPECIFICATION, NOT TO THE PLANT. *** Model
    /// testing catches the sign error, the unit error, the transposed coefficient. It cannot catch the
    /// physics nobody thought to include — the fill model with no in-flight mass correctly implements a
    /// specification that omits in-flight mass, passes every test anyone would write from it, and still
    /// engineers a correct pre-act offset out of the block under test. M3/M4's fidelity declaration
    /// stays the only defence against that, and nothing here weakens or replaces it.
    /// </para>
    /// </remarks>
    public static class ModelOrdering
    {
        /// <summary>
        /// Derive the conflict edges X-I rule 2 implies. *** COMPUTED, NEVER DECLARED. ***
        /// </summary>
        /// <remarks>
        /// A caller-supplied "these two are ordered" is a verdict, and this component has had that
        /// shape fail before. What a submitter declares is a FACT about its own vector — which models
        /// it uses, and which model it tests. The edges follow from those.
        /// </remarks>
        public static IReadOnlyList<ConflictEdge> EdgesFor(IEnumerable<TestSlot>? slots)
        {
            var slotList = (slots ?? Enumerable.Empty<TestSlot>()).Where(s => s != null && s.Id.Length > 0).ToArray();
            var edges = new List<ConflictEdge>();

            var modelSlots = slotList
                .Where(s => s.IsModelSlot)
                .GroupBy(s => s.TestsModel, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.OrdinalIgnoreCase);

            foreach (var consumer in slotList.OrderBy(s => s.Id, StringComparer.OrdinalIgnoreCase))
            {
                foreach (var modelName in consumer.ModelInstances)
                {
                    TestSlot[] testers;
                    if (!modelSlots.TryGetValue(modelName, out testers))
                    {
                        continue;
                    }

                    foreach (var tester in testers.Where(t => !string.Equals(t.Id, consumer.Id, StringComparison.OrdinalIgnoreCase)))
                    {
                        edges.Add(new ConflictEdge(
                            tester.Id,
                            consumer.Id,
                            ConflictEdgeKind.ModelUnderTestByAnotherSlot,
                            "'" + consumer.Id + "' uses model '" + modelName + "', which '" + tester.Id +
                            "' is the test of. Run together, a red is ambiguous between the block and " +
                            "the model (X-I rule 2)."));
                    }
                }
            }

            return edges;
        }

        /// <summary>
        /// Order a colouring so every model slot runs before its consumers, and check every model
        /// depended on is ready.
        /// </summary>
        /// <param name="plan">A colouring from <see cref="WaveSetAdmission.Admit"/>.</param>
        /// <param name="models">The models, with their CURRENT content hashes.</param>
        /// <param name="results">Model results the caller holds. Null means nobody looked.</param>
        /// <param name="gate">
        /// The run loop's stop-on-failed-wave-set declaration. *** NULL REFUSES X-I READING (b). ***
        /// Owner's ruling 2026-08-13: (b) is permitted, but only once the gate demonstrably exists, and
        /// absence is the refusal. A plan that does not use (b) is unaffected by this.
        /// </param>
        /// <param name="runLoopVersionInUse">
        /// Which run loop will execute this plan, compared against the gate's declaration. A gate
        /// declared for a different loop is STALE, which is a different fact from having none.
        /// </param>
        public static ModelOrderingPlan Order(
            WaveSetPlan plan,
            IEnumerable<ModelUnderTest>? models,
            IEnumerable<ModelTestResult>? results,
            StopOnFailedWaveSetGate? gate = null,
            string? runLoopVersionInUse = null)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            if (!plan.Usable)
            {
                return new ModelOrderingPlan(
                    new WaveSet[0],
                    new[]
                    {
                        new OrderingFinding(
                            OrderingDefect.ConsumerDependsOnAModelThatIsNotReady,
                            plan.Outcome.ToString(),
                            "The colouring was not admitted (" + plan.Outcome + "), so there are no wave " +
                            "sets to order. Ordering an unadmitted plan would produce a run order for " +
                            "slots that were never admitted."),
                    },
                    new ModelReadinessVerdict[0],
                    false,
                    RunLoopGateState.NotDeclared,
                    "Nothing to order.");
            }

            var slots = plan.WaveSets.SelectMany(w => w.Slots).ToArray();
            var findings = new List<OrderingFinding>();

            foreach (var contested in slots
                .Where(s => s.IsModelSlot)
                .GroupBy(s => s.TestsModel, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1))
            {
                findings.Add(new OrderingFinding(
                    OrderingDefect.TwoSlotsTestTheSameModel,
                    contested.Key,
                    "Slots " + string.Join(", ", contested.Select(s => s.Id).ToArray()) + " all claim to " +
                    "test this model. Which result belongs to it is undecidable, and a consumer would be " +
                    "admitted on whichever happened to be found first."));
            }

            var colourOf = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var set in plan.WaveSets)
            {
                foreach (var slot in set.Slots)
                {
                    colourOf[slot.Id] = set.Colour;
                }
            }

            var modelSlotColour = slots
                .Where(s => s.IsModelSlot)
                .GroupBy(s => s.TestsModel, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => colourOf[g.First().Id], StringComparer.OrdinalIgnoreCase);

            // --- PRECEDENCE BETWEEN COLOUR CLASSES, DERIVED FROM THE DEPENDENCY. --------------------
            var mustPrecede = new Dictionary<int, HashSet<int>>();
            foreach (var set in plan.WaveSets)
            {
                mustPrecede[set.Colour] = new HashSet<int>();
            }

            foreach (var consumer in slots)
            {
                foreach (var modelName in consumer.ModelInstances)
                {
                    int modelColour;
                    if (!modelSlotColour.TryGetValue(modelName, out modelColour))
                    {
                        continue;
                    }

                    if (modelColour == colourOf[consumer.Id])
                    {
                        findings.Add(new OrderingFinding(
                            OrderingDefect.ModelAndConsumerShareAWaveSet,
                            consumer.Id + " / " + modelName,
                            "The model's slot and a slot using it are both in wave set " + modelColour +
                            ". *** REORDERING CANNOT FIX THIS *** — they have to be SEPARATED by the " +
                            "colouring, which is why the dependency is derived as a conflict edge. Pass " +
                            "ModelOrdering.EdgesFor(slots) to WaveSetAdmission.Admit."));
                        continue;
                    }

                    mustPrecede[modelColour].Add(colourOf[consumer.Id]);
                }
            }

            // --- READINESS, per model actually depended on. ------------------------------------------
            var modelSet = (models ?? Enumerable.Empty<ModelUnderTest>())
                .Where(m => m != null && m.Name.Length > 0)
                .GroupBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var readiness = new List<ModelReadinessVerdict>();
            var restsOnGate = false;
            var gateState = gate == null ? RunLoopGateState.NotDeclared : gate.CheckAgainst(runLoopVersionInUse);

            foreach (var modelName in slots
                .SelectMany(s => s.ModelInstances)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(m => m, StringComparer.OrdinalIgnoreCase))
            {
                ModelUnderTest model;
                if (!modelSet.TryGetValue(modelName, out model))
                {
                    findings.Add(new OrderingFinding(
                        OrderingDefect.ModelNotSupplied,
                        modelName,
                        "A slot depends on this model and it is in neither the plan nor the supplied " +
                        "model set, so its readiness could not even be asked about. Absent is not ready."));
                    continue;
                }

                // *** READING (a) IS TRIED FIRST, AND THAT PRECEDENCE MATTERS. *** A model with a
                // CURRENT PASSING RESULT satisfies X-I rule 2 outright; an in-plan model slot is then
                // just re-testing it, and the consumer does not rest on the between-wave-set gate at
                // all. Checking the plan first would refuse a submission that had both — over-refusing,
                // and making the gate bite on plans that do not need it. Found by the tests.
                var byPriorResult = ModelReadinessCheck.Check(model, results);

                var verdict = byPriorResult.Admits || !modelSlotColour.ContainsKey(modelName)
                    ? byPriorResult
                    : ModelReadinessCheck.Check(model, results, testedEarlierInThisPlan: true);

                readiness.Add(verdict);

                if (verdict.Readiness == ModelReadiness.TestedEarlierInThisPlan)
                {
                    restsOnGate = true;
                }

                if (!verdict.Admits)
                {
                    findings.Add(new OrderingFinding(
                        OrderingDefect.ConsumerDependsOnAModelThatIsNotReady,
                        modelName,
                        verdict.Readiness + " — " + verdict.Reason));
                }
            }

            // *** THE RULING, APPLIED: (b) FAILS CLOSED UNTIL THE GATE IS REAL. *** Only a plan that
            // actually uses reading (b) is held to it — the gate is not a blanket requirement, and
            // making it one would refuse every ordinary plan for a dependency it does not have.
            if (restsOnGate && !RunLoopGateStates.Establishes(gateState))
            {
                findings.Add(new OrderingFinding(
                    OrderingDefect.ReadingBRefusedBecauseTheGateIsNotEstablished,
                    gateState.ToString(),
                    "A consumer is admitted only because its model is tested EARLIER IN THIS PLAN (X-I " +
                    "reading b), and the run loop's stop-on-failed-wave-set gate is " + gateState +
                    ". Wave sets run sequentially, so a failed model wave would NOT stop the consumer's " +
                    "— its results would be produced and BELIEVED after its model failed, which is a " +
                    "wrong answer that looks like a result rather than a missing check. The gate is the " +
                    "RUN LOOP's requirement, not admission's: admission can only refuse to admit, it " +
                    "cannot stop a run already under way. Submit the model in an earlier wave (reading " +
                    "a), or establish the gate."));
            }

            if (findings.Count > 0)
            {
                return new ModelOrderingPlan(
                    new WaveSet[0],
                    findings,
                    readiness,
                    restsOnGate,
                    gateState,
                    "The wave sets cannot be ordered as they stand.");
            }

            // --- TOPOLOGICAL ORDER OVER THE COLOUR CLASSES. ------------------------------------------
            var ordered = TopologicallyOrder(plan.WaveSets, mustPrecede, findings);

            if (findings.Count > 0)
            {
                return new ModelOrderingPlan(new WaveSet[0], findings, readiness, restsOnGate, gateState, "No run order exists.");
            }

            findings.AddRange(Verify(ordered, slots));

            if (findings.Count > 0)
            {
                return new ModelOrderingPlan(
                    new WaveSet[0],
                    findings,
                    readiness,
                    restsOnGate,
                    gateState,
                    "The order produced does not satisfy its own inputs. This is a defect in the orderer.");
            }

            return new ModelOrderingPlan(
                ordered,
                findings,
                readiness,
                restsOnGate,
                gateState,
                "Every model slot runs before the slots depending on it. " + ModelOrderingPlan.FidelityResidual);
        }

        /// <summary>
        /// Check a RUN ORDER against the model dependencies — *** FROM THE DEFINITION, NOT FROM THE
        /// ORDERER'S OWN BOOKKEEPING. ***
        /// </summary>
        /// <remarks>
        /// *** PUBLIC AND SEPARATE FROM THE START, BECAUSE THE LAST VERIFIER IN THIS COMPONENT WAS
        /// UNREACHABLE AND A MUTATION HAD TO FIND IT. *** The orderer never emits an inverted order, so
        /// a check buried inside it could not be reached by any input and would be unfalsifiable in
        /// place. Exposed, it can be pointed at an order the producer would never generate — which is
        /// the only evidence that it checks anything. The question to ask of a guard is "could any input
        /// reach this line?", not "is this line called?".
        /// </remarks>
        public static IReadOnlyList<OrderingFinding> Verify(
            IReadOnlyList<WaveSet>? orderedWaveSets,
            IEnumerable<TestSlot>? slots)
        {
            var order = (orderedWaveSets ?? new WaveSet[0]).Where(w => w != null).ToArray();
            var slotList = (slots ?? Enumerable.Empty<TestSlot>()).Where(s => s != null).ToArray();
            var findings = new List<OrderingFinding>();

            var positionOf = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var position = 0; position < order.Count(); position++)
            {
                foreach (var slot in order[position].Slots)
                {
                    positionOf[slot.Id] = position;
                }
            }

            var modelPosition = slotList
                .Where(s => s.IsModelSlot && positionOf.ContainsKey(s.Id))
                .GroupBy(s => s.TestsModel, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => positionOf[g.First().Id], StringComparer.OrdinalIgnoreCase);

            foreach (var consumer in slotList.Where(s => positionOf.ContainsKey(s.Id)))
            {
                foreach (var modelName in consumer.ModelInstances)
                {
                    int modelAt;
                    if (!modelPosition.TryGetValue(modelName, out modelAt))
                    {
                        continue;
                    }

                    var consumerAt = positionOf[consumer.Id];

                    if (modelAt == consumerAt)
                    {
                        findings.Add(new OrderingFinding(
                            OrderingDefect.ModelAndConsumerShareAWaveSet,
                            consumer.Id + " / " + modelName,
                            "Both run in wave set position " + consumerAt + ", so a red result is " +
                            "ambiguous between the block and the model."));
                        continue;
                    }

                    if (modelAt > consumerAt)
                    {
                        findings.Add(new OrderingFinding(
                            OrderingDefect.ConsumerRunsBeforeItsModel,
                            consumer.Id + " / " + modelName,
                            "'" + consumer.Id + "' runs at position " + consumerAt + " and the model's " +
                            "slot at " + modelAt + ". *** THE CONSUMER'S RESULT WOULD BE CONDITIONED ON A " +
                            "MODEL NOBODY HAD TESTED YET. ***"));
                    }
                }
            }

            return findings;
        }

        private static IReadOnlyList<WaveSet> TopologicallyOrder(
            IReadOnlyList<WaveSet> waveSets,
            Dictionary<int, HashSet<int>> mustPrecede,
            List<OrderingFinding> findings)
        {
            var remaining = waveSets.ToDictionary(w => w.Colour, w => w);
            var inDegree = waveSets.ToDictionary(w => w.Colour, w => 0);

            foreach (var pair in mustPrecede)
            {
                foreach (var successor in pair.Value)
                {
                    inDegree[successor]++;
                }
            }

            var ordered = new List<WaveSet>();

            while (remaining.Count > 0)
            {
                // Lowest colour among the ready ones, so the order is deterministic.
                var ready = remaining.Keys.Where(c => inDegree[c] == 0).OrderBy(c => c).ToArray();

                if (ready.Length == 0)
                {
                    findings.Add(new OrderingFinding(
                        OrderingDefect.CircularModelDependency,
                        string.Join(", ", remaining.Keys.OrderBy(c => c).Select(c => "wave set " + c).ToArray()),
                        "These wave sets each require another of them to run first, so *** NO RUN ORDER " +
                        "EXISTS. *** This is not a bad order but an unorderable set — two models whose " +
                        "slots depend on each other. Splitting one across submissions is the fix; " +
                        "reordering is not."));
                    return new WaveSet[0];
                }

                var next = ready[0];
                ordered.Add(remaining[next]);
                remaining.Remove(next);

                foreach (var successor in mustPrecede[next])
                {
                    inDegree[successor]--;
                }
            }

            return ordered;
        }
    }
}
