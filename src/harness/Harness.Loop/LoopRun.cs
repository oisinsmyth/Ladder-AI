using Harness.Map;
using Harness.Results;
using Harness.Wire;

namespace Harness.Loop;

/// <summary>Everything one turn of the inner loop needs that it cannot derive.</summary>
/// <param name="ProgramUnderTest">
/// The blocks and tag tables being tested. They feed the build stamp — so a change to the block under
/// test is a different download, and a result carrying the old stamp identifies itself as stale.
/// </param>
/// <param name="RuntimeCompression">
/// <b>The factor this turn actually runs at — ONE value, feeding both the gate and the wave.</b>
///
/// <para>It is the same number that decides whether a sampled window still clears the observability floor
/// and whether X-B's backstop is long enough, and those two must never be able to disagree. Before it was
/// threaded through, the gate was evaluated at the request's factor and the wave's backstops were computed
/// as though every vector ran at <c>comp = 1</c>: a submission admitted at comp=10 would then be bounded ten
/// times too generously, and a submission whose vectors declared comp=10 while the wave ran at 1 would be
/// bounded ten times too TIGHTLY and report TIMED-OUT on a healthy test.</para>
/// </param>
public sealed record LoopRequest(
    IReadOnlyList<SubmissionVector> Vectors,
    AssertionEnumeration Enumeration,
    FidelityDeclaration? Fidelity,
    AgentIdentity BlockAuthor,
    ConflictGraph? ComputedConflicts,
    MirrorGeometry Geometry,
    IReadOnlyList<SlotRequest> Slots,
    IReadOnlyList<SlotBinding> Bindings,
    CopyLayerNaming Naming,
    IReadOnlyList<HarnessObject> ProgramUnderTest,
    RuntimeCompression? RuntimeCompression = null,
    BlockCompressionInputs? CompressionInputs = null,

    // Contract 4.5. *** THE LOOP CANNOT SUPPLY THESE AND MUST NOT INVENT THEM. *** `s7Objects: []` is a
    // POSITIVE CLAIM - no classic-S7comm path in this deployment reaches a data block - and the loop
    // asserting it on the caller's behalf would be the caller-supplied verdict this project has killed
    // three times. Absent means nobody said, and gate 11 reports NOT CHECKED.
    DeploymentDeclaration? Deployment = null,
    TagMapReach? TagMapReach = null,

    // *** THE 32-BIT WORD ORDER, AND IT IS THE SAME TRANSFORM AS THE VERSION REGISTER'S. ***
    // A `Time` result is one %MD on the PLC and TWO holding registers on the wire, so reading it back
    // means choosing which half is the low word - and `RegisterWordOrder`'s default is an INFERENCE that
    // no measurement has yet distinguished from its mirror image. It is threaded through here rather than
    // open-coded at the decode site so there is ONE order in this system to calibrate, not two.
    //
    // ✅ MEASURED HIGH-WORD-FIRST, twice: 2026-08-13 against a known pattern and 2026-08-14 against the
    // deployed build stamp. It stays configurable because the presentation is MB_SERVER's rather than
    // ours - a measurement of one rig is not a property of the instruction. A Time read under the wrong
    // order would be out by 65 536 ms and look like a plausible timing bug, which is why the default is
    // now evidence rather than an inference.
    RegisterWordOrder WordOrder = RegisterWordOrder.HighWordFirst,

    // Contract 2.7's join: signal -> controller storage, or a positive `harnessOnly` claim. Gates 8 and
    // 8c are statements about STORAGE and are NOT CHECKED without it. Null means nobody declared it.
    SignalStorageMap? SignalStorage = null,

    // 🔴 *** THE DOCUMENT'S EXTRA FIELDS, AND NULL IS "NOBODY SAID" RATHER THAN "THERE WERE NONE". ***
    //
    // Generate used to pass `Array.Empty<string>()` here unconditionally, on the reasoning that the loop
    // composes from TYPED objects so no unknown field could arrive. That is true of a caller building a
    // LoopRequest by hand and FALSE of LoopCli, which PARSES two documents - so gate 0b passed
    // unconditionally under the loop while harness-gate refused the same submission by name.
    //
    // An empty list is still the right value for a typed caller, and it is now the CALLER'S CLAIM rather
    // than this class asserting it on their behalf - the same treatment Deployment gets four fields up,
    // and for the same reason. Absent is NOT CHECKED, which is loud; the alternative default fails silent.
    IReadOnlyList<string>? UnknownFields = null,
    IReadOnlyList<string>? AnnotationFields = null,

    // Gate 8 refuses an explicit `conflictEdges: null` by name - it is neither the earned `[]` claim nor
    // the honest omission, and a lenient deserializer turns it back into an empty list one layer down. A
    // typed caller cannot express it at all, so false is the computed answer there; a document CAN, and
    // LoopCli now carries what the document said.
    bool ConflictEdgesExplicitlyNull = false)
{
    /// <summary>The factor, defaulting to uncompressed only where the caller passed nothing at all.</summary>
    public RuntimeCompression Compression => RuntimeCompression ?? Harness.Wire.RuntimeCompression.Uncompressed;
}

/// <summary>
/// Build-plan 5.3 — <b>the inner loop: an admitted submission in, a result package per vector out.</b>
///
/// <para>The plan's phase 5 gate is that <i>a block goes from request to tested without a human in the
/// inner loop, AND the result package tells the authoring agent something it could act on</i>. The
/// pieces for both halves existed; what did not was the thing that drives them in order.</para>
///
/// <para><b>The order, and what each step costs:</b></para>
/// <list type="number">
/// <item><b>DERIVE the map</b> — free, PC-side. It comes first because the gate needs it: the
/// observability floor is a property of the wave set's width, and the map is where that lives.</item>
/// <item><b>GATE the submission</b> — free. <b>An inadmissible submission costs nothing beyond this
/// point:</b> no copy layer is generated, no deployment is attempted, no transport is opened.</item>
/// <item><b>GENERATE the copy layer</b> and the build stamp — free.</item>
/// <item><b>ASSERT 0.1b</b> over the generated objects — free, and it is the last thing that is.</item>
/// <item><b>DEPLOY</b> — the device boundary, behind <see cref="IDeviceGateway"/>.</item>
/// <item><b>CONFIRM</b> the version register — what is RUNNING, read from the device.</item>
/// <item><b>RUN the wave</b>.</item>
/// <item><b>PACKAGE</b> per vector, with liveness evidence assembled from the run rather than assumed.</item>
/// </list>
///
/// <para><b>THE LOOP NEVER DECIDES WHETHER A RESULT IS GOOD.</b> DB-8 owns that, in a precedence this
/// class honours rather than re-derives — admissibility, then liveness, then the run outcome, then
/// settling, then content last, because content is the only one a frozen mirror can satisfy. Everything
/// here does is assemble the evidence each of those steps needs and hand the package back unmodified.</para>
/// </summary>
public static class LoopRun
{
    /// <summary>Run one turn.</summary>
    /// <param name="nowMs">Monotonic milliseconds, injected so the backstop is testable without waiting.</param>
    public static LoopResult Execute(LoopRequest request, IDeviceGateway gateway, Func<long>? nowMs = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(gateway);

        // Steps 1-4 are the same code the generate-and-stop entry point runs, called rather than
        // duplicated: a second copy of the derive/gate/generate sequence is how the artifact somebody
        // INSPECTS stops being the artifact that gets DEPLOYED.
        var generation = Generate(request);

        if (!generation.Generated)
        {
            return new LoopResult(generation.Stopped!.Value, generation.Gate, generation.SizeReport,
                generation.Retention, null, null, null,
                Array.Empty<ResultPackage>(), generation.Caveats, generation.Detail);
        }

        var map = generation.Map!;
        var gate = generation.Gate;
        var stamp = generation.Stamp;
        var copyLayer = generation.CopyLayer!;
        var retention = generation.Retention!;
        var caveats = generation.Caveats;
        var compression = request.Compression;

        // ---- 4b. THE MERGED ORDER, BEFORE THE DEVICE BOUNDARY ---------------------------------------
        //
        // 🔴 *** A MANY-TO-ONE SLOT MAP MERGES SEVERAL GROUPS' VECTORS INTO ONE TENSOR, AND THE SUBMISSION
        // CARRIES NO TOTAL ORDER. *** Measured on the deliverable: every group restarts `index` at 0, so 27
        // vectors carry six distinct index values. `Package` below reads distribution.Results[ordinal] — so
        // merging without a major key gives one vector per group reading Results[0] and 21 of 27 packages
        // carrying another vector's run, with no error raised anywhere.
        //
        // It is checked HERE rather than inside Generate because the order is a property of the WAVE: the
        // copy layer is a pure function of the binding and generating it is free. That split is what lets
        // `--generate-only` print the IR AND this refusal, while a deploying run stops before the device.
        var order = generation.Order;

        if (order is null || !order.Ordered)
        {
            // *** TWO OUTCOMES, BECAUSE THE REMEDIES DIFFER. *** An unstated order is fixed by stating
            // one; a boundary-spanning group is fixed by running it around the download boundary it
            // needs, which is not a thing this loop can do. Collapsing them would send a reader to state
            // an order that would not have helped.
            var spanning = order?.BoundarySpanningVectors ?? Array.Empty<string>();

            return new LoopResult(
                spanning.Count > 0 ? LoopOutcome.NotSchedulable : LoopOutcome.NotOrdered,
                gate, generation.SizeReport, retention, null, null, null,
                Array.Empty<ResultPackage>(), caveats,
                (spanning.Count > 0
                    ? $"{spanning.Count} vector(s) cannot be scheduled in this wave, so nothing was deployed and no wave was run: "
                    : "the vectors could not be put in one order, so nothing was deployed and no wave was run: ")
                + (order?.Detail ?? "no order was computed at all")
                + (order is null ? string.Empty : " " + string.Join(" | ", order.Refusals)));
        }

        var ordinalOf = order.OrdinalOf;

        // ---- 5. DEPLOY — the device boundary --------------------------------------------------------
        var deployment = gateway.Deploy(copyLayer.Objects.Concat(request.ProgramUnderTest).ToArray(), stamp);
        if (!deployment.Attempted || !deployment.Loaded)
        {
            return new LoopResult(LoopOutcome.NotDeployed, gate, generation.SizeReport, retention, deployment, null, null,
                Array.Empty<ResultPackage>(), caveats,
                (deployment.Attempted ? "the deployment was attempted and the device did not load everything: " : "no deployment was attempted: ")
                + deployment.Detail);
        }

        using var transport = gateway.Open();
        var client = new MirrorClient(map, transport, stamp);

        // ---- 6. CONFIRM what is RUNNING -------------------------------------------------------------
        var version = VersionCheck.Confirm(client, stamp);
        if (!version.Confirmed)
        {
            return new LoopResult(LoopOutcome.NotConfirmed, gate, generation.SizeReport, retention, deployment, version, null,
                Array.Empty<ResultPackage>(), caveats,
                "the version register did not confirm the build this loop generated, so the wave was not run: " + version.Detail);
        }

        // ---- 7. RUN ---------------------------------------------------------------------------------
        // *** GROUPED BY THE MIRROR SLOT AND ORDERED BY THE MERGED ORDINAL, NOT BY THE CITED ID AND NOT BY
        // THE VECTOR'S OWN INDEX. *** Six ids serving one slot produce ONE tensor here, laid out in the
        // coordinator's stated group order — which is the whole reason the ordinal exists.
        var tensors = request.Vectors
            .GroupBy(v => SlotIndexOf(map, request.Bindings, v.Slot))
            .Select(g => new SlotTensor(
                g.Key,
                g.OrderBy(v => ordinalOf[v.Id]).Select(v => ToWireVector(v, request.Bindings, map, request.WordOrder)).ToArray()))
            .OrderBy(t => t.SlotIndex)
            .ToArray();

        var roundTripsBefore = client.RoundTrips;
        var wave = WaveRun.Run(client, compression, tensors, nowMs);

        // ---- 8. PACKAGE -----------------------------------------------------------------------------
        var packages = Package(request, map, stamp, client, wave, deployment, version, roundTripsBefore, ordinalOf);

        return new LoopResult(LoopOutcome.Ran, gate, generation.SizeReport, retention, deployment, version, wave,
            packages, caveats,
            $"the wave ran to {wave.Length} index(es) over {tensors.Length} slot(s), costing {wave.RoundTrips} round trip(s).");
    }

    /// <summary>
    /// 🔴 <b>STEPS 1–4 ONLY: derive, gate, width, GENERATE, assert 0.1b — and then STOP.</b>
    ///
    /// <para><b>This seam did not exist, and its absence was itself a defect.</b> The only way to obtain
    /// a copy layer was <see cref="Execute"/>, which goes on to hand it to a gateway — so the artifact
    /// could not be INSPECTED without the machinery that DEPLOYS it being in the call. Reviewing generated
    /// IR, diffing two generations, or measuring a mirror's width all needed a device fence to be
    /// satisfied first, for work that touches no device at all.</para>
    ///
    /// <para><b>It is the same code, called rather than copied.</b> <see cref="Execute"/> runs exactly
    /// this and continues; a second implementation would be the way the inspected artifact and the
    /// deployed one quietly stop being the same thing.</para>
    ///
    /// <para><b>Nothing here touches a device</b>, and nothing here can: no <see cref="IDeviceGateway"/>
    /// is a parameter, so there is no gateway to call.</para>
    /// </summary>
    /// <param name="stopWhenInadmissible">
    /// 🔴 <b>Whether an inadmissible submission stops generation. TRUE for anything that will DEPLOY, and
    /// there is no caller that passes false on the way to a device.</b>
    ///
    /// <para>The gate's job is that <i>an inadmissible submission costs nothing beyond this point</i> — no
    /// copy layer, no deployment, no transport. That is a statement about SPENDING, and a generate-and-stop
    /// run spends nothing: it constructs no gateway and opens no socket. The gate is also a statement about
    /// the VECTORS, while the copy layer is a function of the BINDING alone, so a submission held up on a
    /// NOT-CHECKED declaration says nothing about whether the emitted IR is right.</para>
    ///
    /// <para><b>Passing false costs the caller its argument.</b> The gate still runs, its verdict is still
    /// on the result, and a caller that suppresses the stop becomes the only remaining check — which is
    /// why <c>harness-run --generate-only</c> prints the whole verdict and exits with a code that a
    /// deployable run cannot produce.</para>
    /// </param>
    public static LoopGeneration Generate(LoopRequest request, bool stopWhenInadmissible = true)
    {
        ArgumentNullException.ThrowIfNull(request);

        var caveats = Caveats(request);

        // ---- 1. DERIVE ------------------------------------------------------------------------------
        var mapResult = MapAllocator.Allocate(new WaveSetRequest(request.Geometry, request.Slots));
        if (!mapResult.Allocated)
        {
            return LoopGeneration.Stop(LoopOutcome.NotDerivable, null, mapResult.SizeReport, null, caveats,
                "the map could not be derived, so the gate could not run and nothing was generated: "
                + string.Join(" | ", mapResult.Refusals));
        }

        var map = mapResult.Map!;
        caveats = caveats.Append(CollapseSeam(mapResult.SizeReport!)).ToArray();

        // ---- 1b. THE JOIN BETWEEN THE TWO DOCUMENTS, BEFORE ANYTHING IS SPENT -----------------------
        //
        // 🔴 *** THE SLOT ID IS THE ONLY THING TYING A VECTOR TO A BINDING, AND NOTHING COMPARED THEM
        // UNTIL THE WAVE WAS ALREADY RUNNING. *** Measured 2026-08-14 against the committed hopper files:
        // a vector naming an unbound slot passed map derivation, the gate, the width check, generation
        // and 0.1b; the program was DEPLOYED; the version register was CONFIRMED; and only then did
        // SlotIndexOf throw out of step 7 with one deployment and one open transport already spent.
        //
        // The exception was never the defect - the POSITION was. It sits here, above the gate, because
        // the gate is a statement about the VECTORS and this is a statement about whether the two
        // documents refer to the same wave set at all. The decision procedure is Harness.Results.SlotJoin
        // (unit-tested there); this is only the call site, and SlotIndexOf's throw stays where it is,
        // now unreachable - the same shape as the phase-armed-latch throw further down.
        //
        // *** THE BOUND SET IS THE CITABLE IDS, NOT THE MAP'S SLOT IDS. *** With a many-to-one map a slot
        // answers to the specification ids it SERVES, and its own id is an internal key — the tag fragment
        // and a map-hash input. Comparing against the map's ids would refuse every vector of a served
        // group, which is exactly what the committed deliverable did before `serves` existed.
        var citable = request.Bindings.SelectMany(b => b.CitableSlotIds).ToArray();

        var join = SlotJoin.Check(
            request.Vectors.Select(v => (v.Id, v.Slot)),
            citable);

        if (join.Any)
        {
            return LoopGeneration.Stop(LoopOutcome.NotBound, null, mapResult.SizeReport, null, caveats,
                join.Detail + " Nothing was generated and nothing was deployed.");
        }

        // ---- 2. GATE — before anything is spent -----------------------------------------------------
        // The floor is a property of the WAVE SET, so it is computed from the map rather than declared:
        // a slot is polled once per read cycle, and a read cycle is ceil(K/R) round trips.
        var readsPerCycle = map.ReadPlan(Enumerable.Range(0, map.Slots.Count)).Count;
        var floor = WireTiming.ObservabilityFloorScans(readsPerCycle);

        // What the copy layer WILL provide, derived from the bindings — available before it is generated,
        // which is what lets the observability gate run before anything is spent.
        // The COORDINATOR'S bindings, which is what makes gate 5 a verdict here rather than the vector
        // author checking their own homework - and keyed on the SPECIFICATION's names, with the modes
        // DERIVED. Keying on the block tag is the assumption that failed 16 times in 17 in one run.
        var mirror = MirrorObservability.FromBindings(request.Bindings.SelectMany(b => b.ResultSources));

        // *** ONE COMPRESSION VALUE, READ ONCE. *** It goes to the gate below and to the wave at step 7,
        // and the local is what makes it impossible for the two to be different numbers.
        var compression = request.Compression;

        var gate = SubmissionGate.Check(
            request.Vectors, request.Enumeration, request.Fidelity, request.BlockAuthor,
            mirror, floor, compression.Factor, request.ComputedConflicts, request.CompressionInputs,
            request.Deployment, request.TagMapReach, request.SignalStorage,

            // 🔴 *** CARRIED FROM THE REQUEST, NEVER ASSERTED HERE. *** These two lines used to read
            // `false` and `Array.Empty<string>()`, justified by "the loop composes from TYPED objects, so
            // no unknown field can arrive". That is true of a hand-built request and false of LoopCli,
            // which parses a submission AND a binding - so gate 0b passed unconditionally under the loop
            // while the standalone gate refused the same document by name, and gate 8's explicit-null
            // refusal was unreachable from the loop entirely. A caller with no document channel says so by
            // passing an empty list; nobody says it for them.
            conflictEdgesExplicitlyNull: request.ConflictEdgesExplicitlyNull,
            unknownFields: request.UnknownFields,
            annotationFields: request.AnnotationFields);

        if (stopWhenInadmissible && gate.Verdict != SubmissionVerdict.AdmissibleSubjectToJudgement)
        {
            return LoopGeneration.Stop(LoopOutcome.NotAdmissible, gate, mapResult.SizeReport, null, caveats,
                $"the submission was {gate.Verdict}, so no copy layer was generated, nothing was deployed and no wave was run. "
                + $"{gate.Refused.Count} gate(s) refused, {gate.NotChecked.Count} could not run.");
        }

        // ---- 2b. WIDTH — every value must survive the element declared to carry it -------------------
        //
        // 🔴 *** BEFORE ANYTHING IS GENERATED OR DEPLOYED, AND SEPARATE FROM THE GATE. *** A value that
        // overflows its mirror element does not error on the controller: 75 000 ms in a single register
        // arrives as 9 464 ms, every boundary keyed on it fires early, and the run returns a plausible
        // FAIL against a block that did nothing wrong. Measured on the deliverable vector set — 81
        // duration values exceed 65 535 ms — so this is load-bearing, not defensive.
        //
        // The order hazard is already loud (a swapped word makes 75 s read as ~7 days and the scenario
        // times out); the width hazard is the quiet one, so it is the one that refuses.
        var tooWide = WidthRefusals(request);
        if (tooWide.Count > 0)
        {
            return LoopGeneration.Stop(LoopOutcome.NotRepresentable, gate, mapResult.SizeReport, null, caveats,
                $"{tooWide.Count} vector value(s) do not fit the mirror element declared to carry them, so no copy layer was "
                + "generated and nothing was deployed. *** THIS IS A REFUSAL RATHER THAN A TRUNCATION: *** a value silently "
                + "narrowed produces a confident wrong answer, not an error. " + string.Join(" | ", tooWide));
        }

        // ---- 3. GENERATE ----------------------------------------------------------------------------
        var stamp = BuildStamp.Of(map, request.Bindings, request.Naming, request.ProgramUnderTest);
        var copyLayer = CopyLayerGenerator.Generate(map, request.Bindings, request.Naming, stamp);

        if (!copyLayer.Generated)
        {
            return LoopGeneration.Stop(LoopOutcome.NotDerivable, gate, mapResult.SizeReport, null, caveats,
                "the copy layer could not be generated: " + string.Join(" | ", copyLayer.Refusals),
                copyLayer);
        }

        // ---- 4. ASSERT 0.1b -------------------------------------------------------------------------
        //
        // 🔴 *** OVER THE GENERATED OBJECTS ALONE, AND THAT IS A CORRECTION. *** This read
        // `copyLayer.Objects.Concat(request.ProgramUnderTest)` — the same set that gets DEPLOYED — which
        // conflated "what this download contains" with "what this rule is about". Build-plan 0.1b is
        // `every HARNESS object is asserted non-retentive`, and RetentionCheck's own summary says
        // `Run the assertion over every generated harness object`. The program under test is neither
        // generated nor the harness's to constrain.
        //
        // *** MEASURED THE MOMENT `--program` COULD NAME THE REAL PROGRAM: 159 FINDINGS OVER 45 OBJECTS,
        // AND EVERY ONE OF THEM WAS A PROPERTY OF CORRECT PLANT CODE. *** Plant DBs declare no
        // MEMORYLAYOUT (they are not mirrors), `DB_Settings` legitimately RETAINs 22 commissioning
        // setpoints, and the default tag table legitimately carries 101 `%I`/`%Q` addresses, which the
        // rule reads as "not a bit-memory address".
        //
        // AND THE DECISIVE CASE, because it is not a matter of taste: `FB_HopperBlockageStim` declares
        // `PreBoundaryDone : Bool RETAIN` *deliberately* — its own comment says clearing it there rather
        // than on the start edge `is what lets it survive the CPU restart it exists for`. That retentive
        // member is exactly what the boundary-spanning STARTUP vectors depend on. A 0.1b applied to the
        // program under test would refuse the deliverable for containing the thing the deliverable needs.
        //
        // The program's retain is still a real claim on a shared budget, so it is REPORTED — see
        // ProgramRetain below — counted, labelled and gating nothing.
        var retention = RetentionCheck.Check(copyLayer.Objects, request.Geometry);
        if (!retention.Passed)
        {
            return LoopGeneration.Stop(LoopOutcome.NotAssertable, gate, mapResult.SizeReport, retention, caveats,
                "the generated objects failed the non-retentive assertion, so nothing was deployed: " + retention.Summary(),
                copyLayer);
        }

        return new LoopGeneration(null, gate, mapResult.SizeReport, map, stamp, copyLayer, retention, caveats,
            $"the copy layer was generated: {copyLayer.Objects.Count} object(s), {copyLayer.Require().Networks.Count} network(s), "
            + $"{copyLayer.Require().Tags.Count} mirror tag(s), {map.TotalRegisters} register(s) of mirror. NOTHING WAS DEPLOYED.",
            OrderOf(request));
    }

    /// <summary>
    /// The merged <c>(group, index)</c> order for this request — <b>one computation, read by generation
    /// and by the run.</b>
    ///
    /// <para>Kept out of <see cref="Generate"/>'s stop sequence deliberately: a missing group order does
    /// not make the copy layer wrong, and refusing to EMIT the IR over it would deny a reader the artifact
    /// while telling them nothing extra. <see cref="Execute"/> refuses on the same object, before the
    /// device boundary.</para>
    /// </summary>
    private static WaveOrderReport OrderOf(LoopRequest request) =>
        WaveOrder.Of(
            request.Vectors.Select(v => (v.Id, v.Slot, v.Index)),
            request.Bindings
                .Select(b => new WaveSlotGroups(b.SlotId, b.CitableSlotIds, b.ServesRunInOrder, b.BoundarySpanning))
                .ToArray());


    // -------------------------------------------------------------------------------------------------
    // Packaging — evidence assembled, never asserted
    // -------------------------------------------------------------------------------------------------

    private static IReadOnlyList<ResultPackage> Package(
        LoopRequest request, RegisterMap map, BuildStamp stamp, MirrorClient client,
        WaveResult wave, DeploymentOutcome deployment, VersionReport version, int roundTripsBefore,
        IReadOnlyDictionary<string, int> ordinalOf)
    {
        var packages = new List<ResultPackage>();
        var slotsCoveredByOneRead = map.ReadPlan(Enumerable.Range(0, map.Slots.Count)).Max(r => r.SlotCount);

        foreach (var vector in request.Vectors)
        {
            var slotIndex = SlotIndexOf(map, request.Bindings, vector.Slot);

            // 🔴 *** THE MERGED ORDINAL, NEVER `vector.Index`. *** The index is the vector's position
            // WITHIN ITS GROUP and every group restarts at 0, so on a many-to-one slot map several vectors
            // share one index — and reading Results[index] hands each of them the first vector's run.
            var waveIndex = ordinalOf[vector.Id];

            var distribution = wave.Distributions.SingleOrDefault(d => d.SlotIndex == slotIndex);
            var run = distribution is not null && waveIndex < distribution.Results.Count
                ? distribution.Results[waveIndex]
                : null;

            if (run is null)
            {
                // The wave stopped before this index. No package is fabricated for it: an absent package
                // is unambiguous, and one built on a run that did not happen would not be.
                continue;
            }

            var logIndex = wave.Log.Indices.ElementAtOrDefault(waveIndex);
            var binding = BindingFor(request.Bindings, vector.Slot);

            var stimulus = new StimulusEvidence(
                Commanded: logIndex?.Commanded.Contains(slotIndex) ?? false,
                Executed: logIndex?.Executed.Contains(slotIndex) ?? false,
                ScanAdvance: run.ElapsedScans,
                RoundTrips: Math.Max(1, client.RoundTrips - roundTripsBefore),
                Manifest: ManifestOf(deployment, request.ProgramUnderTest),
                Version: version);

            packages.Add(ResultPackageBuilder.Build(
                new VectorDeclaration(vector.Id, vector.Basis, request.Fidelity, vector.Settling,
                    vector.AssertedBehaviours, vector.CompletionSignal, vector.Author, request.BlockAuthor,
                    ObservabilityCheck.Evaluate(vector.Expectations, vector.Form,
                        MirrorObservability.FromBindings(binding.ResultSources),
                        WireTiming.ObservabilityFloorScans(1), vector.CompressionFactor, request.Compression.Factor)),
                request.Enumeration,
                run,
                slotIndex,
                waveIndex,
                stimulus,
                StimulusExpectation.AtLeastOneScanPerRoundTrip(Math.Max(1, run.PollRounds)),
                Settling(client, vector, slotIndex, waveIndex, run, distribution!),
                Assertions(vector, binding, run, request.WordOrder),
                distribution!.CoRunning.FirstOrDefault(c => c.WaveIndex == waveIndex).CoRunners ?? Array.Empty<int>(),
                map,
                stamp,
                slotsCoveredByOneRead));
        }

        return packages;
    }

    /// <summary>
    /// The one settling form this harness can EVALUATE — and it is deliberately narrow.
    ///
    /// <para>Lane 5 recorded that nothing produced a settling state: <c>WaveRun</c> observes COMPLETION,
    /// not settling, and a completion flag is not a settling signal. This closes it for the form a
    /// declaration can state structurally — <i>unchanged across N scans</i> — by <b>comparing what was
    /// RECORDED against what is still true N scans later</b>. That directly catches phase 2's defective
    /// build, which raised <c>Done</c> at 10 and went on ramping to 15.</para>
    ///
    /// <para><b>Two limits, named rather than hidden.</b> It can only be done for a slot's LAST index —
    /// earlier ones have had an inert phase move the program on, so they report
    /// <see cref="SettlingState.NotEstablished"/>, which is not the same as settled. And it cannot see a
    /// value that moved and came back.</para>
    /// </summary>
    private static SettlingState Settling(MirrorClient client, SubmissionVector vector, int slotIndex, int waveIndex, SlotRunResult run, SlotDistribution distribution)
    {
        if (vector.Settling is not { UnchangedForScans: > 0 } settling)
            return SettlingState.NotEstablished;

        // The MERGED ordinal, matching CompletedAtIndex, which counts positions in the tensor the wave
        // actually ran. `vector.Index` counts positions within a GROUP, and on a many-to-one slot the two
        // are different numbers — so comparing the old one would call several vectors "last".
        if (waveIndex != distribution.CompletedAtIndex)
            return SettlingState.NotEstablished;

        if (run.Outcome != SlotOutcome.Completed)
            return SettlingState.NotEstablished;

        var from = client.ReadControl().ScanCounter;
        for (var poll = 0; poll < 200; poll++)
        {
            if (client.ReadControl().ScanCounter.Since(from) >= settling.UnchangedForScans)
            {
                return client.ReadResults(slotIndex).SequenceEqual(run.Results)
                    ? SettlingState.Settled
                    : SettlingState.NotSettled;
            }
        }

        return SettlingState.NotEstablished;
    }

    private static IReadOnlyList<AssertionOutcome> Assertions(
        SubmissionVector vector, SlotBinding binding, SlotRunResult run, RegisterWordOrder wordOrder)
    {
        var assertionId = vector.Basis?.AssertionId ?? "<uncited>";

        return vector.Expectations.Select(e =>
        {
            // *** THE REGISTER OFFSET, NOT THE LIST INDEX. *** They diverge the moment a 32-bit element is
            // in the list: everything after a Time sits one register later than its position, and reading
            // by position would return the Time's SECOND HALF as the next signal's value — a plausible
            // number, silently wrong. -1 rather than 0 for "not here", because 0 is a real offset.
            var register = binding.ResultRegisterOf(e.Signal);
            var signal = binding.ResultSignal(e.Signal);

            return AssertionOutcome.Compare(assertionId, e.Signal, e.Expected ?? "<no predicate>",
                Observe(signal, register, run, wordOrder));
        }).ToArray();
    }

    /// <summary>
    /// Decode one result, <b>by the type the binding declared</b>.
    ///
    /// <para>Reading every result as a signed 16-bit word was true only while every element was an Int. A
    /// Bool would read 0/1 rather than false/true and never match its predicate; a Time would read its
    /// high half alone and be wrong by up to 65 536 ms.</para>
    ///
    /// <para><b>Null is "not observed", and it is returned rather than a zero</b> — an unread register is
    /// not a zero one, and <see cref="AssertionOutcome.Compare"/> is what turns null into NotObserved.</para>
    /// </summary>
    private static string? Observe(MirroredSignal? signal, int register, SlotRunResult run, RegisterWordOrder wordOrder)
    {
        if (signal is null || register < 0 || register >= run.Results.Length)
            return null;

        var element = MirrorElements.For(signal.Type);
        if (element is null)
            return null;

        // A 32-bit element needs BOTH its registers present. Half a value is not a value.
        if (register + element.Registers > run.Results.Length)
            return null;

        return element.Form switch
        {
            // Bit 0 of the register, matching what the copy layer's COIL writes and what
            // MirrorGeometry.BitAddressOf places there.
            MirrorAddressForm.Bit => ((run.Results[register] & 1) == 1).ToString().ToLowerInvariant(),

            MirrorAddressForm.Word => unchecked((short)run.Results[register]).ToString(),

            // The same order as the version register and the scan counter, on purpose: ONE calibration
            // for the whole system rather than two. Measured HighWordFirst (2026-08-13, 2026-08-14).
            MirrorAddressForm.DoubleWord => unchecked((int)RegisterWords.To32(
                run.Results[register], run.Results[register + 1], wordOrder)).ToString(),

            _ => null,
        };
    }

    /// <summary>
    /// Whether the program under test appears in the download's own load manifest.
    ///
    /// <para><b>Tag tables are excluded, and that is a correction rather than a convenience.</b> A PLC
    /// tag table carries no load message: the one measured 19-object manifest from this rig names an
    /// FC, an FB, its instance DB, OB1, <c>MB_SERVER</c> and nine <c>TCP_MB_*</c> helpers, and no tag
    /// table. Every program under test the harness has generates one, so a comparison that demanded it
    /// would report <see cref="ManifestPresence.Absent"/> on every healthy real download — and
    /// <c>Absent</c> makes every package non-conclusive. It passed unnoticed because the only gateway
    /// that existed put EVERY object name in its manifest, including the tag table, so the test and
    /// the device disagreed about what a manifest contains and only the test was ever consulted.</para>
    ///
    /// <para><b>An empty downloadable set is <see cref="ManifestPresence.NotAvailable"/>, not
    /// Loaded.</b> A manifest that was never asked about anything answers nothing.</para>
    /// </summary>
    private static ManifestPresence ManifestOf(DeploymentOutcome deployment, IReadOnlyList<HarnessObject> programUnderTest)
    {
        if (deployment.Manifest.Count == 0)
            return ManifestPresence.NotAvailable;

        // A PLC DATA TYPE is excluded on the same ground as a tag table and with the same [I] marker: it is
        // imported and compiled, and nothing has ever observed one in a load manifest. The one measured
        // 19-object manifest from this rig names an FC, an FB, its instance DB, OB1, MB_SERVER and nine
        // TCP_MB_* helpers — no tag table and no type. Including it would report Absent on every healthy
        // download carrying a UDT, and Absent makes every package non-conclusive, which is the failure
        // direction that gets a check switched off.
        var downloadable = programUnderTest
            .Where(o => o.Kind is not (HarnessObjectKind.TagTable or HarnessObjectKind.DataType))
            .ToArray();
        if (downloadable.Length == 0)
            return ManifestPresence.NotAvailable;

        return downloadable.All(o => deployment.Manifest.Contains(o.Name))
            ? ManifestPresence.Loaded
            : ManifestPresence.Absent;
    }

    /// <summary>
    /// 🔴 <b>The mirror slot a CITED specification id resolves to — through the many-to-one map, never by
    /// string equality with the map's own slot ids.</b>
    ///
    /// <para>Unreachable throws: <c>SlotJoin</c> refuses an unbound id above the gate, before anything is
    /// generated. They stay because the alternative to a throw here is a default, and a defaulted slot
    /// index is one agent's vector written into another slot's mirror region.</para>
    /// </summary>
    private static int SlotIndexOf(RegisterMap map, IReadOnlyList<SlotBinding> bindings, string citedSlotId)
    {
        var binding = BindingFor(bindings, citedSlotId);
        var slot = map.Slot(binding.SlotId);

        return slot?.Index ?? throw new ArgumentException(
            $"binding slot '{binding.SlotId}' (serving '{citedSlotId}') is not in this map.", nameof(citedSlotId));
    }

    /// <summary>The binding whose <see cref="SlotBinding.CitableSlotIds"/> contain the id a vector cited.</summary>
    private static SlotBinding BindingFor(IReadOnlyList<SlotBinding> bindings, string citedSlotId)
    {
        var matches = bindings
            .Where(b => b.CitableSlotIds.Contains(citedSlotId, StringComparer.Ordinal))
            .ToArray();

        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new ArgumentException($"no binding serves slot '{citedSlotId}'.", nameof(citedSlotId)),

            // WaveOrder refuses a group served twice by name and before the device boundary; this is the
            // backstop, and it throws rather than taking the first because "the first" is decided by the
            // order the bindings happen to be listed in.
            _ => throw new ArgumentException(
                $"slot '{citedSlotId}' is served by {matches.Length} bindings ({string.Join(", ", matches.Select(m => m.SlotId))}).",
                nameof(citedSlotId)),
        };
    }

    /// <summary>
    /// Every vector value that does not survive its declared element, named.
    ///
    /// <para><b>Checked here rather than at the write, because a refusal has to happen BEFORE anything is
    /// generated or deployed.</b> Catching it at the write would mean the copy layer was built, the
    /// program downloaded and the CPU loaded before anybody noticed the stimulus could not be expressed.</para>
    ///
    /// <para><b>And it covers the COMPLETION VALUE as well as the inputs</b>, since a completion signal is
    /// just another mirrored element — see the gap named on <see cref="ToWireVector"/>.</para>
    /// </summary>
    private static IReadOnlyList<string> WidthRefusals(LoopRequest request)
    {
        var refusals = new List<string>();

        foreach (var vector in request.Vectors)
        {
            var binding = request.Bindings.FirstOrDefault(b => b.CitableSlotIds.Contains(vector.Slot, StringComparer.Ordinal));
            if (binding is null)
                continue;

            foreach (var refusal in MirrorValueFit.CheckAll(binding.VectorTargets, vector.Inputs))
                refusals.Add($"vector '{vector.Id}': {refusal}");

            // The completion value travels in the completion signal's own element, so it is held to the
            // same rule as everything else the mirror carries.
            var completion = binding.ResultSignal(vector.CompletionSignal);
            if (completion is not null && vector.CompletionValue is { } expected)
            {
                var fit = MirrorValueFit.Check(vector.CompletionSignal, completion.Type, expected.ToString());
                if (!fit.Fits)
                    refusals.Add($"vector '{vector.Id}' completion value: {fit.Refusal}");
            }

            // ⚠️ AND THE GAP ITSELF, REFUSED RATHER THAN MIS-COMPARED. WireVector compares ONE register
            // against a ushort, so a 32-bit completion signal cannot be expressed at all. Comparing anyway
            // would test its high half and report TIMED-OUT forever on a block that finished.
            if (completion is not null && (MirrorElements.For(completion.Type)?.Registers ?? 1) > 1)
            {
                refusals.Add(
                    $"vector '{vector.Id}': completion signal '{vector.CompletionSignal}' is declared {completion.Type}, which occupies "
                    + $"{MirrorElements.Require(completion.Type).Registers} registers — and completion is compared against ONE register "
                    + "holding a 16-bit value. *** THIS IS INEXPRESSIBLE, NOT MIS-EXPRESSED. *** Comparing anyway would test the high half "
                    + "alone and report TIMED-OUT forever on a block that finished. Use a single-register completion signal, or widen "
                    + "WireVector.CompletionValue to carry an element width the way every other mirrored value now does.");
            }
        }

        return refusals;
    }

    /// <summary>
    /// ⚠️ <b>KNOWN GAP, ONE FIELD OVER: <c>CompletionValue</c> IS COMPARED AGAINST A SINGLE REGISTER.</b>
    ///
    /// <para><c>WireVector.CompletionValue</c> is a <c>ushort</c> and <c>SlotRun</c> compares it against
    /// <c>results[CompletionRegister]</c> — one register. <b>So a 32-bit completion signal is not
    /// mis-compared, it is INEXPRESSIBLE</b>: the submission's <c>completionValue</c> is an <c>int?</c>
    /// bounded 0..65535, which was right for a 16-bit register and wrong for the type system that now
    /// surrounds it.</para>
    ///
    /// <para><b>The fix belongs with the MIRROR GEOMETRY, not the result package</b>, and the reason is
    /// that a completion signal is not a special kind of thing — <i>it is just another mirrored element</i>.
    /// It should carry a width like every other one: declared type, width derived from the address form,
    /// value range-checked by <see cref="MirrorValueFit"/>, and the comparison made across the element's
    /// full register span rather than its first register. Putting it in the result package instead would
    /// give completion a second, private notion of width that could disagree with the mirror's.</para>
    ///
    /// <para><b>Until then the loop refuses rather than mis-comparing:</b> a completion signal whose
    /// element is wider than one register cannot be honoured here, and pretending otherwise would compare
    /// against its high half and report TIMED-OUT forever on a block that finished.</para>
    /// </summary>
    private static WireVector ToWireVector(SubmissionVector vector, IReadOnlyList<SlotBinding> bindings, RegisterMap map, RegisterWordOrder wordOrder)
    {
        var binding = BindingFor(bindings, vector.Slot);

        // *** THE VECTOR IS LAID OUT BY REGISTER WIDTH, NOT ONE WORD PER SIGNAL. *** A 32-bit input takes
        // two registers, so a naive one-word-per-target array would put every later input at the wrong
        // address — and would silently write half of the wide one.
        var values = new ushort[binding.VectorRegistersNeeded];
        var targetOffsets = binding.VectorRegisterOffsets;

        for (var i = 0; i < binding.VectorTargets.Count; i++)
        {
            var target = binding.VectorTargets[i];
            var offset = targetOffsets[i];

            // 🔴 *** THE KEY A VECTOR ACTUALLY WRITES ITS INPUTS UNDER — AND THIS LINE READ `target.Tag`
            // UNTIL 2026-08-14, WHICH ON THE DELIVERABLE MATCHED NOTHING AT ALL. *** A vector cites the
            // SPECIFICATION's name; `Tag` is what the block calls the member, and on this deliverable the
            // two differ for all ten stimulus inputs. So every stimulus register stayed at ZERO and the
            // block ran a scenario nobody asked for — while `MirrorValueFit`, corrected the same day, was
            // checking the very values that were then not written. *** A GATE THAT EXAMINES THE RIGHT
            // THING BESIDE A WRITER THAT WRITES THE WRONG ONE IS WORSE THAN BOTH BEING WRONG, *** because
            // the gate's green then reads as evidence about the writer. One definition, on the signal.
            if (!vector.Inputs.TryGetValue(target.JoinKey, out var text))
                continue;

            // ONE parse, ONE encode and ONE range check, shared with the refusal above. A second, laxer
            // path here is how a value refused at step 2b could still be written narrowed — so there isn't
            // one, and the ENCODING travels with it for the same reason.
            var fit = MirrorValueFit.Check(target.JoinKey, target.Type, text, target.Encoding);
            if (!fit.Fits)
            {
                // Unreachable: step 2b refuses the whole run before any wave is built. A throw rather than
                // a silent narrowing, because narrowing here is the exact defect being prevented.
                throw new InvalidOperationException(
                    $"vector '{vector.Id}' reached the wave with a value that does not fit its mirror element. The width check "
                    + $"refuses that before generation, so the loop has run a submission it did not admit. {fit.Refusal}");
            }

            var element = MirrorElements.Require(target.Type);

            if (element.Form == MirrorAddressForm.DoubleWord)
            {
                // The SAME order value as the read side. Writing under one order and reading under the
                // other would cancel out on our own loopback and disagree only against the device — the
                // exact shape of self-agreement this project distrusts, which is why the order was settled
                // by reading the DEVICE (2026-08-13, 2026-08-14) and not by a round trip through us.
                var words = RegisterWords.From32(unchecked((uint)(int)fit.Value), wordOrder);
                values[offset] = words[0];
                values[offset + 1] = words[1];
            }
            else
            {
                values[offset] = unchecked((ushort)(short)fit.Value);
            }
        }

        var completionRegister = binding.ResultRegisterOf(vector.CompletionSignal);

        return new WireVector(
            values,
            // Inert is declared over every RESULT REGISTER, including the second half of a wide element.
            // Declaring it per signal would leave those halves unclaimed, and an unclaimed register is one
            // nothing checks is quiet.
            new InertDeclaration(Enumerable.Range(0, binding.ResultRegistersNeeded).ToDictionary(i => i, _ => (ushort)0)),
            completionRegister >= 0 ? completionRegister : 0,
            // The completion VALUE comes from the vector. It used to be a literal 1 here, which was the
            // loop inventing a convention contract section 2 does not state — and then a DEFAULT of 1 on
            // the field, which was the same invention one layer up. Unreachable by construction: the
            // schema gate refuses a null before any wave is generated, so a throw here would mean the
            // loop had run an inadmissible submission.
            unchecked((ushort)(vector.CompletionValue
                ?? throw new InvalidOperationException(
                    $"vector '{vector.Id}' reached the wave with no completion value. The schema gate refuses that, so the loop has run a submission it did not admit."))),
            // *** THE DURATION CARRIES ITS OWN comp. *** The declaration is in scans at the AUTHOR's
            // factor, and the wave re-expresses it at the factor it runs — see ScanBudget. The schema gate
            // has already refused a MaxDuration below 1, so the construction cannot throw here.
            new ScanBudget(vector.MaxDurationScans, Math.Max(1, vector.CompressionFactor)));
    }

    // -------------------------------------------------------------------------------------------------
    // The gaps this run rests on — carried, not closed
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// The known gaps, on <b>every</b> run rather than only the bad ones.
    ///
    /// <para>A report that appears only on bad news teaches a reader that its absence means it was not
    /// run — the same lesson as F-6's collapse report, which prints its no-collapse line too.</para>
    /// </summary>
    private static LoopCaveat[] Caveats(LoopRequest request)
    {
        var sampled = request.Vectors.SelectMany(v => v.Expectations).Count(e => e.Mode == InstrumentationMode.Sampled);
        var total = request.Vectors.Sum(v => v.Expectations.Count);

        return new[]
        {
            new LoopCaveat("F-3-authority",
                "THE ASSERTION FORM HAS NO AUTHORITY BEHIND IT. F-3 refuses a SAMPLED observation of a NEVER assertion, "
                + "and the form is declared BY THE VECTOR — nothing checks it against the enumeration, because the "
                + "enumeration still has no producer. So F-3 is enforced against what a vector CLAIMS, not against what "
                + "the assertion IS, and an author who cites a NEVER and declares WHEN gets the permissive path."),

            new LoopCaveat("DB-8-saw-nothing",
                $"DB-8 CANNOT DISTINGUISH 'SAW NOTHING' FROM 'NOTHING HAPPENED'. A sampled assertion that observed no "
                + $"disagreement is AssertionState.Held, identical to one that saw nothing because nothing occurred. "
                + $"F-3 prevents the case where that is dangerous (a NEVER assertion); it does not fix the package. "
                + $"This submission declares {sampled} sampled expectation(s) of {total}."),
        };
    }

    /// <summary>
    /// F-6's collapse report, carried onto the result <b>because nothing else emits it</b>.
    ///
    /// <para>It is on <c>MapResult</c> and no path printed it: the gate never allocates a map, and
    /// <c>WaveRun</c> receives one already derived. The coordinator is its natural owner; until there is
    /// one, this is the seam, and it is a REPORT here exactly as it is there — nothing refuses on it.</para>
    /// </summary>
    private static LoopCaveat CollapseSeam(SlotSizeReport report) =>
        new("F-6-collapse-seam", report.Describe()
            + " (Carried here because nothing else emits it — the coordinator is its natural owner. It is a report at "
            + "this level too: no part of this loop consults it.)");
}
