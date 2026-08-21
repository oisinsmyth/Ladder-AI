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

    /// <summary>
    /// 🔴 <b>PLURAL SINCE 2026-08-18.</b> A single <c>AssertionEnumeration</c> converts implicitly to a set
    /// of one, so every construction site is unchanged and a single-subject wave behaves exactly as it did.
    /// It comes from <c>GateCli.InputsOf</c> like every other document-sourced input, which is what keeps
    /// the loop's gate and the standalone gate the same gate.
    /// </summary>
    AssertionEnumerationSet Enumeration,
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
    bool ConflictEdgesExplicitlyNull = false,

    // Gate 0c's evidence: which derivable fields the document carried, and the deriver's provenance for
    // each. Carried from the request for exactly the reason UnknownFields is - the loop is the path that
    // spends rig time, so a gate that is weaker here than in the standalone CLI is the weaker check
    // guarding the more expensive door. Null is NOT CHECKED, never a pass.
    DerivationEvidence? Derivation = null)
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
    /// <param name="feed">
    /// 🔴 <b>OPT-IN. When present, every read the wave makes is FORWARDED to it as it returns</b>, so
    /// <c>harness-mirror-view --follow</c> can show the run live without opening a second socket to the
    /// device. <c>MB_SERVER</c> accepts one connection per instance — measured, a viewer attached during a
    /// wave cost 0 of 22 vectors — and two connections would in any case be two samples at two instants.
    ///
    /// <para><b>It never causes a read and it can never fail the run.</b> The publisher forwards what a
    /// read already returned and counts its own failures; nothing in this loop consults it.</para>
    /// </param>
    public static LoopResult Execute(
        LoopRequest request, IDeviceGateway gateway, Func<long>? nowMs = null, IMirrorFeedPublisher? feed = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(gateway);

        // Steps 1-4 are the same code the generate-and-stop entry point runs, called rather than
        // duplicated: a second copy of the derive/gate/generate sequence is how the artifact somebody
        // INSPECTS stops being the artifact that gets DEPLOYED.
        var generation = Generate(request);

        if (!generation.Generated)
        {
            // *** EVERY SUBMITTED VECTOR IS ACCOUNTED FOR EVEN HERE, WHERE NOTHING RAN. *** A stop before
            // the wave is precisely the run most likely to be read as "no results yet" rather than as
            // "twenty-two vectors were never attempted", and an empty list says the first.
            return new LoopResult(generation.Stopped!.Value, generation.Gate, generation.SizeReport,
                generation.Retention, null, null, null,
                Array.Empty<ResultPackage>(), generation.Caveats, generation.Detail,
                NothingAttempted(request, generation.Stopped!.Value, generation.Detail, ordinalOf: null, generation.Map),
                generation.InertRest,
                generation.Manifest);
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

            var outcome = spanning.Count > 0 ? LoopOutcome.NotSchedulable : LoopOutcome.NotOrdered;

            var detail =
                (spanning.Count > 0
                    ? $"{spanning.Count} vector(s) cannot be scheduled in this wave, so nothing was deployed and no wave was run: "
                    : "the vectors could not be put in one order, so nothing was deployed and no wave was run: ")
                + (order?.Detail ?? "no order was computed at all")
                + (order is null ? string.Empty : " " + string.Join(" | ", order.Refusals));

            return new LoopResult(
                outcome,
                gate, generation.SizeReport, retention, null, null, null,
                Array.Empty<ResultPackage>(), caveats, detail,

                // No merged order exists on this path, so no vector HAS a wave index — the accounting
                // reports -1 rather than 0, since 0 is a real index and would read as "it was first".
                NothingAttempted(request, outcome, detail, ordinalOf: null, map),
                generation.InertRest,
                generation.Manifest);
        }

        var ordinalOf = order.OrdinalOf;

        // ---- 4c. THE INERT EXPECTATION, BEFORE THE DEVICE BOUNDARY ----------------------------------
        //
        // 🔴 *** EVERY RESULT REGISTER USED TO BE ASSERTED TO REST AT ZERO, HARDCODED IN ToWireVector. ***
        // That is not a declaration, it is an assumption wearing one's clothes — and on real hardware it
        // fails in BOTH directions. Loud: a signal resting at a -1 sentinel refuses a correct program.
        // SILENT, and this is the disqualifying half: where 0 is a measured PASS verdict, asserting 0
        // accepts the previous index's leftover result as an inert start state, which is D33's first check
        // passing over exactly the state it exists to catch.
        //
        // *** IT REFUSES HERE RATHER THAN INSIDE THE WAVE *** for the reason SlotJoin sits above the gate:
        // a cross-document fault found after the deployment costs a whole download. InertPhase carries the
        // same check against the band it actually read — that one is the backstop for a direct caller, and
        // the two are not redundant because only this one is free.
        var inertRest = generation.InertRest;

        if (inertRest is null || !inertRest.Planned)
        {
            var detail =
                (inertRest is null
                    ? "the inert expectation was never computed, so nothing was deployed and no wave was run"
                    : $"{inertRest.Refusals.Count} published signal(s) have no declared resting value, so nothing was deployed and "
                      + "no wave was run")
                + ". *** THE INERT PHASE IS D33's FIRST CHECK AND IT MUST BE DECLARED: a check with no expectation passes over "
                + "anything. *** " + string.Join(" | ", inertRest?.Refusals ?? Array.Empty<string>())
                + " " + (inertRest?.Summary() ?? "INERT REST: NOT COMPUTED.");

            return new LoopResult(LoopOutcome.RestNotDeclared, gate, generation.SizeReport, retention, null, null, null,
                Array.Empty<ResultPackage>(), caveats, detail,
                NothingAttempted(request, LoopOutcome.RestNotDeclared, detail, ordinalOf, map),
                inertRest,
                generation.Manifest);
        }

        // ---- 5. DEPLOY — the device boundary --------------------------------------------------------
        var deployment = gateway.Deploy(copyLayer.Objects.Concat(request.ProgramUnderTest).ToArray(), stamp);
        if (!deployment.Attempted || !deployment.Loaded)
        {
            var detail =
                (deployment.Attempted ? "the deployment was attempted and the device did not load everything: " : "no deployment was attempted: ")
                + deployment.Detail;

            return new LoopResult(LoopOutcome.NotDeployed, gate, generation.SizeReport, retention, deployment, null, null,
                Array.Empty<ResultPackage>(), caveats, detail,
                NothingAttempted(request, LoopOutcome.NotDeployed, detail, ordinalOf, map),
                inertRest,
                generation.Manifest);
        }

        using var transport = gateway.Open();

        // The feed is handed to the CLIENT, which is where every read in this system lands. Publishing
        // from anywhere higher would mean choosing which of the wave's reads a viewer is entitled to see,
        // and publishing on a separate clock would be the two-instants problem again minus the socket.
        //
        // ⚠️ `request.WordOrder` is now passed explicitly. It used to be left to the parameter default,
        // which is the same value TODAY (`HighWordFirst`, measured twice) and would have diverged the
        // moment anything set the request's order: the client decodes the version register and the scan
        // counter under ITS order, so a request declaring the other one would have had ReadControl compare
        // a mis-assembled stamp against Expected and refuse a healthy device. A no-op as things stand,
        // covered by the existing default-agreement test, and one fewer value with two readers.
        var client = new MirrorClient(map, transport, stamp, request.WordOrder, feed);

        // ---- 6. CONFIRM what is RUNNING -------------------------------------------------------------
        var version = VersionCheck.Confirm(client, stamp);
        if (!version.Confirmed)
        {
            var detail = "the version register did not confirm the build this loop generated, so the wave was not run: " + version.Detail;

            return new LoopResult(LoopOutcome.NotConfirmed, gate, generation.SizeReport, retention, deployment, version, null,
                Array.Empty<ResultPackage>(), caveats, detail,
                NothingAttempted(request, LoopOutcome.NotConfirmed, detail, ordinalOf, map),
                inertRest,
                generation.Manifest);
        }

        // ---- 7. RUN ---------------------------------------------------------------------------------
        // *** GROUPED BY THE MIRROR SLOT AND ORDERED BY THE MERGED ORDINAL, NOT BY THE CITED ID AND NOT BY
        // THE VECTOR'S OWN INDEX. *** Six ids serving one slot produce ONE tensor here, laid out in the
        // coordinator's stated group order — which is the whole reason the ordinal exists.
        var tensors = request.Vectors
            .GroupBy(v => SlotIndexOf(map, request.Bindings, v.Slot))
            .Select(g => new SlotTensor(
                g.Key,
                g.OrderBy(v => ordinalOf[v.Id]).Select(v => ToWireVector(v, request.Bindings, map, request.WordOrder, inertRest)).ToArray()))
            .OrderBy(t => t.SlotIndex)
            .ToArray();

        var roundTripsBefore = client.RoundTrips;
        var wave = WaveRun.Run(client, compression, tensors, nowMs);

        // ---- 7b. ACCOUNT FOR EVERY SUBMITTED VECTOR, BEFORE PACKAGING ANY OF THEM -------------------
        //
        // 🔴 *** THE DENOMINATOR IS THE SUBMITTED SET AND IT IS COMPUTED ONCE. *** Step 8 below builds a
        // package if and only if this says `Ran`, which is what stops the report and the packages from
        // disagreeing: the loop used to `continue` past a vector whose index the wave never reached, and
        // NOTHING ELSE IN THE SYSTEM LEARNED THAT IT HAD HAPPENED. Measured on the first wave ever run —
        // 22 vectors submitted, 2 packages produced, and the summary line then read `0 of 2`.
        var account = VectorAccounting.Of(
            Submitted(request, ordinalOf, map),
            VectorAccounting.ProgressOf(wave.Distributions),
            wave.Length);

        // ---- 8. PACKAGE -----------------------------------------------------------------------------
        // *** THE GATE'S OWN FLOOR IS HANDED IN, NEVER RE-DERIVED. *** See LoopGeneration.ObservabilityFloorScans.
        var packages = Package(request, map, stamp, client, wave, deployment, version, roundTripsBefore, ordinalOf, account,
            generation.ObservabilityFloorScans);

        // ---- 8b. RELEASE THE RIG — AFTER PACKAGING, NEVER BEFORE ------------------------------------
        //
        // 🔴 *** THE WAVE LEFT EVERY START BOOL RAISED, AND THE BLOCK UNDER TEST RUNNING, INDEFINITELY. ***
        // Nothing in `WaveRun` lowers them at the end: the reset level is written by the NEXT index's
        // inert phase, and after the last index there is no next index. Measured on JOB9004's vessel runs
        // 2026-08-18 — the final control frame of two complete waves reads StartBools = 0x0001,
        // StartEcho = 0x0001, taken minutes after the client had finished and closed.
        //
        // Two consequences, both real. The block under test goes on being commanded with nobody
        // observing it, for however long the rig is left alone; and the NEXT wave's index 0 then has only
        // its declared quiescence window to bring a fully-driven block back to rest, which is the
        // likeliest reading of the index-0 `NotInert` results in this job's earlier runs.
        //
        // *** IT IS HERE AND NOT IN `WaveRun` BECAUSE OF THE ORDERING. *** `Package` above evaluates
        // SETTLING, which re-reads the slot's results some scans after the run and compares them against
        // what completion recorded — and that comparison is only meaningful while the block is still
        // driven. Releasing inside the wave would reset the block first and report `NotSettled` on every
        // healthy last index. So: last read first, release second.
        //
        // A failure to release cannot be allowed to destroy a run that already happened, so it is
        // reported rather than thrown.
        var released = ReleaseTheRig(client);

        // *** WHAT WAS PLANNED AND WHAT WAS REACHED ARE TWO NUMBERS, AND THIS PRINTED ONLY THE FIRST. ***
        // `wave.Length` is the LONGEST TENSOR — what the wave set out to run — so a wave that stopped at
        // index 1 of 22 reported "the wave ran to 22 index(es)". True of the plan, false of the run, and
        // it is the headline line of the whole report.
        var reached = account.IndicesRun == account.IndicesPlanned
            ? $"the wave ran all {wave.Length} planned index(es)"
            : $"the wave ran {account.IndicesRun} of {wave.Length} planned index(es) and STOPPED EARLY";

        return new LoopResult(LoopOutcome.Ran, gate, generation.SizeReport, retention, deployment, version, wave,
            packages, caveats,
            $"{reached} over {tensors.Length} slot(s), costing {wave.RoundTrips} round trip(s). "
            + $"{account.Ran} of {account.Submitted} submitted vector(s) were attempted"
            + (account.NeverAttempted > 0 ? $"; {account.NeverAttempted} were NOT. " : ". ")
            // *** THE START STATE THIS RUN'S EVERY VERDICT RESTS ON, ON THE HEADLINE LINE. *** A run gated
            // on assumed zeros is not the same evidence as one gated on declared resting values, and the
            // difference has to be visible where a reader of RESULTS meets it.
            + inertRest.Summary()
            // 🔴 *** AND WHY AN INDEX REFUSED, WHICH USED TO REACH NOBODY. *** A NotInert index produces a
            // package whose every assertion reads `<never read>`; the InertReport that says WHICH check
            // refused and on which register is on `SlotRunResult.Inert` and is rendered nowhere. Two agents
            // spent a day on a `NotInert` whose stated cause was one field away.
            + InertRefusals(wave)
            + released,
            account,
            inertRest,
            generation.Manifest);
    }

    /// <summary>
    /// 🔴 <b>Lower every start bool once the run's last read is done — the rig is left INERT, not
    /// running.</b>
    ///
    /// <para>Returns what to append to the headline, because <b>a release that did not happen must be
    /// visible</b>: the failure mode of a silent one is a block left commanded on a bench rig, which is
    /// the state this exists to end. It never throws — a run that already produced results is not thrown
    /// away because the tidy-up write failed.</para>
    /// </summary>
    private static string ReleaseTheRig(MirrorClient client)
    {
        try
        {
            client.LowerAllStartBools();
            return " The rig was released: every start bool is LOW and no block under test is left commanded.";
        }
        catch (Exception error)
        {
            return " ⚠️ THE RIG WAS NOT RELEASED — the start bools could not be lowered after the run, so a block "
                + "under test may still be commanded on the device and the next wave's first index will start from a "
                + $"driven state: {error.Message}";
        }
    }

    /// <summary>
    /// 🔴 <b>Every index whose inert phase REFUSED, with the outcome and the reason the phase itself
    /// gave.</b>
    ///
    /// <para><b>Nothing rendered this.</b> A refused index yields a package whose run outcome is
    /// <c>NotInert</c>, whose assertions are all <c>&lt;never read&gt;</c> and whose stimulus says the
    /// experiment did not happen — none of which says WHICH of the inert checks refused, on which
    /// register, or against what declared value. That is on <see cref="SlotRunResult.Inert"/> and it is
    /// the one fact a reader needs, because the three plausible causes (a declaration that is wrong, a
    /// signal that is not quiescent by construction, a program that did not reset) have three different
    /// remedies and are indistinguishable without it.</para>
    /// </summary>
    private static string InertRefusals(WaveResult wave)
    {
        var refusals = wave.Distributions
            .SelectMany(d => d.Results.Select((r, ordinal) => (d.SlotIndex, Ordinal: ordinal, Run: r)))
            .Where(x => x.Run.Outcome == SlotOutcome.NotInert)
            .OrderBy(x => x.Ordinal).ThenBy(x => x.SlotIndex)
            .Select(x => $"index {x.Ordinal}, slot {x.SlotIndex}: {x.Run.Inert.Outcome} — {x.Run.Inert.Detail}")
            .ToArray();

        return refusals.Length == 0
            ? string.Empty
            : $" *** INERT REFUSED AT {refusals.Length} INDEX(ES), SO THOSE TESTS NEVER RAN AND SAY NOTHING ABOUT ANY BLOCK: "
              + string.Join(" | ", refusals);
    }

    /// <summary>
    /// Every submitted vector with the two keys the accounting needs: which mirror slot it resolves to and
    /// where it sits in that slot's MERGED run.
    /// </summary>
    private static IReadOnlyList<VectorAccounting.SubmittedVector> Submitted(
        LoopRequest request, IReadOnlyDictionary<string, int> ordinalOf, RegisterMap map) =>
        request.Vectors
            .Select(v => new VectorAccounting.SubmittedVector(
                v.Id, v.Slot, SlotIndexOf(map, request.Bindings, v.Slot), ordinalOf[v.Id]))
            .ToArray();

    /// <summary>
    /// The whole submitted set, accounted for as never attempted — <b>for every path that stops before
    /// the wave.</b>
    /// </summary>
    /// <param name="ordinalOf">
    /// The merged order, where one exists. <b>Absent means no vector has a wave index yet</b>, and the
    /// rows then carry <c>-1</c> rather than <c>0</c>: zero is a real index and would read as "it was
    /// first". Slot indices are likewise <c>-1</c> when the map cannot resolve them, which is the case on
    /// the paths that stop before the map is trusted.
    /// </param>
    private static RunAccount NothingAttempted(
        LoopRequest request, LoopOutcome outcome, string detail,
        IReadOnlyDictionary<string, int>? ordinalOf = null,
        RegisterMap? map = null) =>
        VectorAccounting.NothingAttempted(
            request.Vectors.Select(v => new VectorAccounting.SubmittedVector(
                v.Id,
                v.Slot,
                SlotIndexOrUnknown(request, map, v.Slot),
                ordinalOf is not null && ordinalOf.TryGetValue(v.Id, out var ordinal) ? ordinal : -1)),
            outcome,
            detail);

    /// <summary>
    /// The slot index a cited id resolves to, or <c>-1</c>.
    ///
    /// <para><b>It cannot throw, and that is the point of it existing beside
    /// <see cref="SlotIndexOf"/>.</b> The stop paths include the one where the two documents disagree
    /// about which slots exist at all, so an unresolvable id is an ordinary input to this accounting
    /// rather than a fault — and losing the whole account to an exception would leave the run reporting
    /// nothing about any vector, which is exactly the defect being fixed.</para>
    /// </summary>
    private static int SlotIndexOrUnknown(LoopRequest request, RegisterMap? map, string citedSlotId)
    {
        if (map is null)
            return -1;

        var matches = request.Bindings
            .Where(b => b.CitableSlotIds.Contains(citedSlotId, StringComparer.Ordinal))
            .ToArray();

        return matches.Length == 1 ? map.Slot(matches[0].SlotId)?.Index ?? -1 : -1;
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

        // ---- 1c. THE SECOND JOIN: THE SIGNAL NAMES --------------------------------------------------
        //
        // 🔴 *** THE SLOT ID IS NOT THE ONLY JOIN, AND THE ONE BELOW IT HAD NO CHECK AT ALL. *** A
        // vector's slot resolving says nothing about whether the SIGNALS it names resolve, and both are
        // consumed as REGISTER OFFSETS. Measured on JOB9004's first live wave, 2026-08-17: the wave ran both its
        // vectors against the rig, cost 7,912 round trips, read the whole result band every poll — and
        // returned `<never read>` for every declared assertion, because not one cited name reached a
        // register. Two causes, both this join, and only one of them was in the lookup.
        //
        // *** IT IS ABOVE THE GATE FOR THE SAME REASON SlotJoin IS: an unjoined name cannot be answered by
        // anything downstream, so the cost of finding it late is a whole deployment. *** And it is a
        // REFUSAL rather than a per-assertion NotObserved because of the completion signal, which had a
        // FALLBACK TO REGISTER 0 — a plausible run that finishes on the wrong register is worse than a
        // refusal that names the disagreement.
        var signalJoin = SignalJoin.Check(request.Vectors, slot => SlotBindingOrNull(request.Bindings, slot));

        if (signalJoin.Any)
        {
            return LoopGeneration.Stop(LoopOutcome.NotBound, null, mapResult.SizeReport, null, caveats,
                signalJoin.Detail + " Nothing was generated and nothing was deployed.");
        }

        // ---- 2. GATE — before anything is spent -----------------------------------------------------
        //
        // 🔴 *** THE FLOOR IS COMPUTED HERE, ONCE, AND CARRIED — IT USED TO BE COMPUTED TWICE AND THE TWO
        // DISAGREED. *** It is a property of the WAVE SET: a slot is re-read once per poll cycle, and a
        // poll cycle is ceil(K/R) round trips. This site derived it from the map; the site that builds the
        // DELIVERED RESULT PACKAGE passed a hardcoded 1. On any wave needing more than one read per cycle
        // the package's floor was too small by that factor, in the PERMISSIVE direction — so a window this
        // gate would refuse read as `Supportable` in the artifact the block author receives. A one-read
        // wave set is unaffected, which is why it went unseen.
        //
        // The value now travels on LoopGeneration and reaches Package as an argument, so there is nothing
        // left to keep in step. Same shape as the inert-rest plan and the wave order above it.
        var floor = WireTiming.ObservabilityFloorScans(map.ReadsPerPollCycle);

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
            annotationFields: request.AnnotationFields,
            derivation: request.Derivation);

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
        // The manifest comes out of the SAME derivation as the stamp. A run that cannot say what its
        // stamp was computed over cannot be re-run - measured, on a wave that had gone green.
        var stamp = BuildStamp.Derive(map, request.Bindings, request.Naming, request.ProgramUnderTest, out _, out var manifest);
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

        // ---- 4c. THE INERT EXPECTATION — computed from the bindings, REPORTED here, GATED in Execute ---
        //
        // 🔴 *** IT USED TO BE `Range(0, ResultRegistersNeeded).ToDictionary(i => i, _ => (ushort)0)`,
        // BUILT INSIDE THE WAVE BUILDER. *** Every result register asserted to rest at zero, hardcoded,
        // under a type whose own summary reads "D33's FIRST check, and it must be declared: a check with no
        // expectation passes over anything". See InertRestPlan for the ruling and the measured consequence.
        //
        // Computed here and refused in Execute, exactly as the wave order is: the resting values decide
        // nothing about the copy layer, so `--generate-only` prints the IR AND this report.
        var inertRest = InertRestOf(request);

        return new LoopGeneration(null, gate, mapResult.SizeReport, map, stamp, copyLayer, retention, caveats,
            $"the copy layer was generated: {copyLayer.Objects.Count} object(s), {copyLayer.Require().Networks.Count} network(s), "
            + $"{copyLayer.Require().Tags.Count} mirror tag(s), {map.TotalRegisters} register(s) of mirror. NOTHING WAS DEPLOYED. "
            + inertRest.Summary(),
            OrderOf(request),
            inertRest,
            floor,
            manifest);
    }

    /// <summary>
    /// One inert-rest plan per binding, <b>derived once and read by both the refusal and the wave
    /// builder.</b>
    ///
    /// <para>Two derivations of one expectation is how the thing that REFUSES and the thing that RUNS come
    /// to disagree — and this file already records three instances of exactly that, all of them on the
    /// signal join. One computation, two consumers.</para>
    /// </summary>
    private static InertRestReport InertRestOf(LoopRequest request) =>
        new(request.Bindings.Select(b => InertRestPlan.For(b, request.WordOrder)).ToArray());

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
        IReadOnlyDictionary<string, int> ordinalOf, RunAccount account,

        // 🔴 *** THE WAVE SET'S FLOOR, PASSED IN. *** This method used to call
        // `WireTiming.ObservabilityFloorScans(1)` inline while the gate computed the real one from the
        // map — see LoopGeneration.ObservabilityFloorScans for the measured consequence. It is a
        // parameter rather than a re-derivation so the two cannot be different numbers again.
        double observabilityFloorScans)
    {
        var packages = new List<ResultPackage>();
        var slotsCoveredByOneRead = map.ReadPlan(Enumerable.Range(0, map.Slots.Count)).Max(r => r.SlotCount);
        var accountOf = account.Vectors.ToDictionary(a => a.VectorId, StringComparer.Ordinal);

        foreach (var vector in request.Vectors)
        {
            var slotIndex = SlotIndexOf(map, request.Bindings, vector.Slot);

            // 🔴 *** THE MERGED ORDINAL, NEVER `vector.Index`. *** The index is the vector's position
            // WITHIN ITS GROUP and every group restarts at 0, so on a many-to-one slot map several vectors
            // share one index — and reading Results[index] hands each of them the first vector's run.
            var waveIndex = ordinalOf[vector.Id];

            // 🔴 *** ONE PREDICATE DECIDES BOTH THE PACKAGE AND THE DISPOSITION. *** This was a bare
            // `if (run is null) continue;` — correct in itself (a package built on a run that did not
            // happen would be worse) and SILENT, so the twenty vectors it skipped on JOB9004's first live wave
            // left no trace anywhere. The skip is still a skip; what changed is that the account was
            // computed first and every skipped vector is now a reported row with a reason.
            if (accountOf[vector.Id].Disposition != VectorDisposition.Ran)
                continue;

            // Guaranteed in range by the account's own definition of `Ran`, which is why the lookup below
            // is unconditional rather than re-testing what was just decided.
            var distribution = wave.Distributions.Single(d => d.SlotIndex == slotIndex);
            var run = distribution.Results[waveIndex];

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
                        observabilityFloorScans, vector.CompressionFactor, request.Compression.Factor)),
                request.Enumeration,
                run,
                slotIndex,
                waveIndex,
                stimulus,
                StimulusExpectation.AtLeastOneScanPerRoundTrip(Math.Max(1, run.PollRounds)),
                // No client, no waveIndex, no distribution: the dwell was already taken at this index's
                // close and travels on `run`. Those three parameters existed only to re-read the device
                // afterwards and to work out whether it was still safe to - which it only was for the
                // last index on the slot.
                Settling(vector, binding, slotIndex, run),
                Assertions(vector, binding, run, request.WordOrder),

                // 🔴 *** NULL WHEN NO SLICE WAS RECORDED, NOT AN EMPTY LIST. *** This read
                // `FirstOrDefault(...).CoRunners ?? Array.Empty<int>()`, so an index with no entry in the
                // co-running log rendered as the positive claim "ran alone" — a result obtained under
                // unrecorded co-runners was indistinguishable from one obtained in isolation.
                CoRunnersOf(distribution, waveIndex),
                map,
                stamp,
                slotsCoveredByOneRead));
        }

        return packages;
    }

    /// <summary>
    /// The measured co-running slice for one index, <b>or null when the log holds no entry for it.</b>
    ///
    /// <para>Written as an explicit lookup rather than <c>FirstOrDefault(...) ?? empty</c> because those
    /// two spellings differ only in what they say about an absence, and the convenient one says the
    /// dangerous thing.</para>
    /// </summary>
    private static IReadOnlyList<int>? CoRunnersOf(SlotDistribution distribution, int waveIndex)
    {
        foreach (var (index, coRunners) in distribution.CoRunning)
        {
            if (index == waveIndex)
                return coRunners;
        }

        return null;
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
    /// <para>🔴 <b>IT COMPARES THE REGISTERS THE DECLARATION NAMES, AND UNTIL 2026-08-20 IT COMPARED THE
    /// WHOLE SLOT BAND.</b> The line was
    /// <c>client.ReadResults(slotIndex).SequenceEqual(run.Results)</c>: <c>ReadResults</c> returns every
    /// register the slot publishes, so a vector saying <i>"settle on these three signals"</i> was answered
    /// with <i>"were all N registers bit-identical"</i>. <c>SettlingDeclaration.Signals</c> was resolved to
    /// a register BY NOTHING, in the whole system — <c>SignalJoin</c> recorded that as a named limit and
    /// named this change as the thing that would end it.</para>
    ///
    /// <para><b>MEASURED, ON A LIVE WAVE.</b> A slot whose binding declares five registers as having NO
    /// resting value — a presenter tick and a counter that run OUTSIDE the index, and a one-scan pulse —
    /// can never satisfy a whole-band comparison: the band differed on <b>54–69% of adjacent poll
    /// pairs</b>. The three declared signals had been stable for ~2,000 scans. <b>Sixteen of sixteen
    /// assertions held and every vector came back UNSETTLED</b> — a wave's worth of rig time spent
    /// answering a question nobody asked. The registers that moved are exactly the ones the binding
    /// already says have no resting value, which is why the whole-band form could not have been rescued
    /// by a tolerance.</para>
    ///
    /// <para><b>Every road that is not a comparison is <see cref="SettlingState.NotEstablished"/> WITH ITS
    /// REASON, and none of them may be <c>Settled</c>.</b> An empty signal list is one of them: a
    /// per-signal check over zero registers is satisfied by anything, and it would present as a clean
    /// pass. <c>SignalJoin</c> refuses an unjoinable settling name before the deployment is spent; the
    /// resolution below is the backstop for a caller that reached here anyway, and it names the signal
    /// rather than quietly comparing a shorter list.</para>
    ///
    /// <para><b>Two limits, named rather than hidden.</b> It can only be done for a slot's LAST index —
    /// earlier ones have had an inert phase move the program on, so they report
    /// <see cref="SettlingState.NotEstablished"/>, which is not the same as settled. And it cannot see a
    /// value that moved and came back.</para>
    ///
    /// <para>⚠️ <b>THE SAMPLE IS TAKEN AFTER THE INDEX'S WHOLE TAIL RECOVERY, NOT AT THE CLOSE OF THE
    /// OBSERVATION WINDOW</b>, which is what the declaration's own wording describes. That mismatch is
    /// unchanged by this fix and is recorded rather than quietly narrowed: <c>run.Observations</c> is the
    /// retained series and is already passed in, but it is DECIMATED (see <c>ObservationSeries.Stride</c>)
    /// — with a stride above 1 a change can fall between two retained frames, so reading settling off it
    /// could report <c>Settled</c> over a value that moved. Moving the sample there is a soundness
    /// question about the retention rule, not a re-pointing of this method.</para>
    /// </summary>
    private static SettlingReport Settling(
        SubmissionVector vector, SlotBinding binding, int slotIndex, SlotRunResult run)
    {
        // The declaration is resolved by the SAME method that built the wave's probe, so the explanation
        // and the measurement can never be about different registers.
        var resolution = ResolveSettling(vector, binding);
        if (resolution.Refusal is { } refusal)
            return SettlingReport.NotEstablished(refusal);

        if (run.Outcome != SlotOutcome.Completed)
        {
            return SettlingReport.NotEstablished(
                $"the run ended {run.Outcome}, so no observation was taken that could be asked whether it was final.");
        }

        // *** NULL IS "NOBODY TOOK THE SAMPLE", NEVER "IT SETTLED". *** Unreachable while the wave builds
        // a probe for every vector that declares one - which is exactly why it is stated rather than
        // assumed: a future path that forgets to pass the probe must surface here, not pass silently.
        if (run.Settling is not { } sample)
        {
            return SettlingReport.NotEstablished(
                $"no settling dwell was taken at the close of slot {slotIndex}'s index, so nothing established whether the observed "
                + "value was final. The declaration resolved, so this is a gap in the run rather than in the vector.");
        }

        if (!sample.Taken)
            return SettlingReport.NotEstablished(sample.Detail);

        var probe = resolution.Probe!;
        var denominator =
            $"{probe.Registers.Count} register(s) carrying {vector.Settling!.Signals!.Count} declared signal(s) "
            + $"({string.Join(", ", probe.Registers.Select(r => $"R{r.Register:000} '{r.Signal}'"))}), "
            + $"re-read {sample.ScansWaited} scan(s) after the close of this index";

        return sample.Unchanged
            ? SettlingReport.Settled(
                $"every declared settling signal held its recorded value: {denominator}. *** THIS IS A CLAIM ABOUT THE DECLARED SIGNALS "
                + $"AND NOT ABOUT SLOT {slotIndex}'s WHOLE BAND *** - other registers in the band may legitimately be moving, and several "
                + "of them are declared to have no resting value at all.")
            : SettlingReport.NotSettled(
                $"{sample.Detail} Compared over {denominator}. The observed value was still changing, so what was read may be mid-flight.");
    }

    /// <summary>
    /// 🔴 <b>THE OBSERVE PATH — REWRITTEN 2026-08-17 AFTER IT WAS MEASURED PRODUCING A CONFIDENT FAIL
    /// AGAINST A BLOCK THAT WAS PROVEN CORRECT ON THE DEVICE.</b>
    ///
    /// <para><b>What it used to do, in two lines:</b> resolve every expectation through
    /// <c>ResultRegisterOf</c> — the VALUE offsets — and compare it against <c>run.Results</c>, the single
    /// snapshot taken at the poll that recognised completion. <c>e.Mode</c> was never read by the code that
    /// performs the observation at all, so <c>Latched</c> and <c>Sampled</c> were the same thing at the
    /// wire, and the phase-armed latches that had been argued for, sized, generated, deployed and read off
    /// the wire every poll were <b>unconsumed</b>.</para>
    ///
    /// <para><b>Why the single snapshot is fatal rather than merely lossy.</b> A well-built stimulus model
    /// returns the block to inert BEFORE it raises its completion flag — that is required, and it is what
    /// stops a wave dying after one vector. So the one instant this code looked at was, by construction,
    /// the one instant at which every commanded member is inert and every latched cause has been reset.
    /// Measured: an assertion expecting a commanded state to be TRUE was read ~600 ms after the scenario ended,
    /// against a register that no block, correct or not, could have held true at that instant.</para>
    ///
    /// <para><b>What it does now, per expectation:</b></para>
    /// <list type="number">
    /// <item><b><c>Latched</c> resolves through the LATCH register</b> — the mode that is immune to the
    /// tail, because a latch says "this happened at some point inside the armed window". <b>An absent latch
    /// is a refusal naming the signal, never a fallback to the value register</b>: that fallback is
    /// precisely how <c>Latched</c> became a synonym for <c>Sampled</c>.</item>
    /// <item><b>Everything else is evaluated over the RETAINED SERIES</b>, frame by frame, with each frame
    /// stamped with its scan and with whether the signal's own declared arm window was open when it was
    /// taken. See <c>SeriesEvaluation</c> for the three-way fold and for why it does not consult
    /// <c>AssertionForm</c>.</item>
    /// </list>
    /// </summary>
    private static IReadOnlyList<AssertionOutcome> Assertions(
        SubmissionVector vector, SlotBinding binding, SlotRunResult run, RegisterWordOrder wordOrder)
    {
        var assertionId = vector.Basis?.AssertionId ?? "<uncited>";
        var series = run.Observations;

        // 🔴 *** THE STRIDE TRAVELS WITH THE DENOMINATORS, because the fold's decision on whether it may
        // convict from an ABSENCE turns on WHICH PART OF THE INDEX was retained — and `Truncated` alone
        // never answered that. It was true on all three of the rows that accused a site's block on
        // 2026-08-17. ***
        var accounting = new SeriesAccounting(
            series.PollsObserved, series.DistinctFrames, series.Frames.Count, series.Truncated)
        {
            Stride = series.Stride,
        };

        return vector.Expectations.Select(e =>
        {
            var expected = e.Expected ?? "<no predicate>";

            // *** THE REGISTER OFFSET, NOT THE LIST INDEX. *** They diverge the moment a 32-bit element is
            // in the list: everything after a Time sits one register later than its position, and reading
            // by position would return the Time's SECOND HALF as the next signal's value — a plausible
            // number, silently wrong. -1 rather than 0 for "not here", because 0 is a real offset.
            var register = binding.ResultRegisterOf(e.Signal);
            var signal = binding.ResultSignal(e.Signal);

            if (e.Mode == InstrumentationMode.Latched)
                return Latched(assertionId, e, expected, binding, series, accounting);

            var armRegister = binding.ArmRegisterOf(e.Signal);

            var frames = series.Frames
                .Select(f => new ObservedFrame(
                    f.Scan.Raw, f.PollRound,
                    Observe(signal, register, f.Registers, wordOrder),
                    WindowAt(armRegister, f.Registers)))
                .ToArray();

            // 🔴 *** THE DECLARED TEMPORAL SHAPE, PASSED THROUGH — THE LAST HOP OF THE OBSERVATION-WINDOW
            // MODEL. *** Without it every expectation reaches the evaluator as `Unstated`, the three-way
            // fold calls every mixed series Inconclusive, and a vector that ran and settled says nothing
            // about the block. Measured on the wave of 2026-08-18: three vectors, all Inconclusive,
            // `conclusiveAboutTheBlock: 0`, with 65 frames observed on two signals — every one of them
            // while the declared arm window was CLOSED.
            //
            // *** IT WIDENS WHAT CAN BE SAID AND NEVER WHAT PASSES. *** `Unstated` is still the default and
            // still reproduces the previous fold down to the text, so nothing already written changes
            // meaning; and the sharp shapes still refuse to accuse without a declared arm window, because a
            // disagreeing frame outside the phase may be the model's own inert tail
            // (docs/notes/observation-window-shapes.md §2, the accusation rule).
            return SeriesEvaluation.Evaluate(assertionId, e.Signal, expected, frames, accounting, e.Shape);
        }).ToArray();
    }

    /// <summary>
    /// A <c>Latched</c> expectation, read from <b>whichever register actually holds the latch</b> — and the
    /// two are different registers for the two kinds of latch, which is the whole point of
    /// <see cref="LatchSource"/>.
    ///
    /// <list type="bullet">
    /// <item><b>Generated</b> (the signal is declared <c>Transient</c>): the copy layer emits a sticky bit
    /// in the LATCH BAND, so the answer is in <c>LatchRegisterOf</c> and the value register is not
    /// consulted.</item>
    /// <item><b>Hand-authored</b> (the binding NAMES a block that latches it): the latching happens inside
    /// the block under test, so the VALUE register already carries the latched bit and there is no separate
    /// register to read. ⚠️ <b>This one is taken on trust and cannot be verified from here</b> — the
    /// harness did not emit that latch and cannot read the named block. A binding that names a block which
    /// does not in fact latch the signal reproduces the original defect exactly, and nothing mechanical
    /// will say so. It is admitted because refusing it would refuse a real deployed capability, and because
    /// <c>MirrorObservability.LatchProvenance</c> already carries the block name for a reviewer to check
    /// against the object set.</item>
    /// <item><b>None</b>: a refusal naming the signal. <b>Never a fallback to the value register</b> — an
    /// author declaring <c>Latched</c> is saying the value register cannot answer.</item>
    /// </list>
    ///
    /// <para><b>Either way it is read from the FINAL frame, and that is correct rather than convenient:</b>
    /// a latch is cleared only when the slot stops running an index, so it is still standing at completion
    /// — which is exactly the property that makes this mode the answer to a model with a tail recovery.
    /// Reading it from an earlier frame would ask "had it happened YET", which is a weaker question.</para>
    /// </summary>
    private static AssertionOutcome Latched(
        string assertionId, ObservabilityDeclaration e, string expected,
        SlotBinding binding, ObservationSeries series, SeriesAccounting accounting)
    {
        var signal = binding.ResultSignal(e.Signal);

        var register = signal?.LatchSource switch
        {
            LatchSource.Generated => binding.LatchRegisterOf(e.Signal),
            LatchSource.HandAuthored => binding.ResultRegisterOf(e.Signal),
            _ => -1,
        };

        if (signal is null || register < 0)
            return SeriesEvaluation.NoLatchFor(assertionId, e.Signal, expected, accounting);

        var final = series.Final;
        if (final is null || register >= final.Registers.Length)
            return AssertionOutcome.Compare(assertionId, e.Signal, expected, null);

        // A latch is a Bool in its own register, written by a set-coil — bit 0, the same placement
        // MirrorGeometry.BitAddressOf gives every mirrored bit. A hand-authored one is likewise a Bool
        // result source, so the decode is the same either way.
        var value = ((final.Registers[register] & 1) == 1).ToString().ToLowerInvariant();

        return SeriesEvaluation.FromLatch(
            assertionId, e.Signal, expected, value, final.Scan.Raw, accounting, signal.LatchSource);
    }

    /// <summary>
    /// Whether the arm window was open when a frame was taken.
    ///
    /// <para><b><see cref="WindowState.Unknown"/> is returned for "no arm register", and it is not
    /// "open".</b> A signal whose binding declared no window, or whose arm tag this slot does not publish,
    /// simply cannot have its frames classified — and treating that as armed would hand every
    /// under-declared signal the permissive reading, which is the assumption that made one arbitrary
    /// instant authoritative in the first place.</para>
    /// </summary>
    private static WindowState WindowAt(int armRegister, ushort[] registers)
    {
        if (armRegister < 0 || armRegister >= registers.Length)
            return WindowState.Unknown;

        return (registers[armRegister] & 1) == 1 ? WindowState.InWindow : WindowState.OutOfWindow;
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
    private static string? Observe(MirroredSignal? signal, int register, ushort[] registers, RegisterWordOrder wordOrder)
    {
        if (signal is null || register < 0 || register >= registers.Length)
            return null;

        var element = MirrorElements.For(signal.Type);
        if (element is null)
            return null;

        // A 32-bit element needs BOTH its registers present. Half a value is not a value.
        if (register + element.Registers > registers.Length)
            return null;

        return element.Form switch
        {
            // Bit 0 of the register, matching what the copy layer's COIL writes and what
            // MirrorGeometry.BitAddressOf places there.
            MirrorAddressForm.Bit => ((registers[register] & 1) == 1).ToString().ToLowerInvariant(),

            MirrorAddressForm.Word => unchecked((short)registers[register]).ToString(),

            // The same order as the version register and the scan counter, on purpose: ONE calibration
            // for the whole system rather than two. Measured HighWordFirst (2026-08-13, 2026-08-14).
            MirrorAddressForm.DoubleWord => unchecked((int)RegisterWords.To32(
                registers[register], registers[register + 1], wordOrder)).ToString(),

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

    /// <summary>
    /// The binding serving a cited slot id, or <b>null when none does or several do</b> — the non-throwing
    /// form, for the checks that run BEFORE <see cref="SlotJoin"/> has refused an unbound id.
    ///
    /// <para>Returning null on the ambiguous case rather than picking one is deliberate: the ambiguity is
    /// <c>WaveOrder</c>'s finding to report, and answering it here would attribute a signal disagreement to
    /// whichever binding happened to be listed first.</para>
    /// </summary>
    private static SlotBinding? SlotBindingOrNull(IReadOnlyList<SlotBinding> bindings, string citedSlotId)
    {
        var matches = bindings
            .Where(b => b.CitableSlotIds.Contains(citedSlotId, StringComparer.Ordinal))
            .ToArray();

        return matches.Length == 1 ? matches[0] : null;
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
    private static WireVector ToWireVector(
        SubmissionVector vector, IReadOnlyList<SlotBinding> bindings, RegisterMap map, RegisterWordOrder wordOrder, InertRestReport inertRest)
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

        // 🔴 *** THIS READ `completionRegister >= 0 ? completionRegister : 0` UNTIL 2026-08-17, AND THAT
        // FALLBACK IS THE WHOLE OF A MEASURED DEFECT. *** Register 0 is a real register holding some other
        // signal, so a completion name the binding does not carry did not fail — it silently watched
        // whatever sat first in the result band. Measured on JOB9004's first live wave: `SPEC.Scenario_Done`
        // matched neither the
        // tag nor the spec name, the poll watched result register 0 (the stimulus model's PHASE code), and
        // the wave declared a 41-second scenario COMPLETE after EIGHT SCANS because phase 1 equals the
        // completion value 1. It then read the result band at that instant and packaged it as the answer.
        //
        // A throw, never a default: step 1c refuses an unjoined completion signal above the gate, so
        // reaching here means the loop ran a submission it did not admit.
        var completionRegister = binding.ResultRegisterOf(vector.CompletionSignal);
        if (completionRegister < 0)
        {
            throw new InvalidOperationException(
                $"vector '{vector.Id}' reached the wave with completion signal '{vector.CompletionSignal}', which the binding for slot "
                + $"'{vector.Slot}' does not carry. THE SIGNAL JOIN REFUSES THAT BEFORE ANY WAVE IS GENERATED, so the loop has run a "
                + "submission it did not admit. There is deliberately no fallback register: watching register 0 instead is how a wave "
                + "reports a vector complete on a signal nobody asked about.");
        }

        return new WireVector(
            values,
            // 🔴 *** THIS READ `Enumerable.Range(0, binding.ResultRegistersNeeded).ToDictionary(i => i, _ =>
            // (ushort)0)` UNTIL 2026-08-18 — EVERY RESULT REGISTER ASSERTED TO REST AT ZERO, HARDCODED. ***
            // The comment beside it argued, correctly, that inert must be declared over every RESULT
            // REGISTER including the second half of a wide element — and then supplied the values itself.
            // The premise survives and is now discharged by InertRestPlan, which expands a PER-SIGNAL
            // declaration across the element's whole span and derives the latch band from the rungs this
            // harness emits, so no half is left unclaimed and no value is invented.
            //
            // *** MEASURED ON REAL HARDWARE: *** one slot passed its inert gate only because its published
            // signals happen to rest at zero; a second could not pass it at all, and every reason was a
            // legitimate resting value of a correctly-functioning program — a -1 sentinel where 0 means a
            // measured PASS, a commanded input resting at whatever the pending index declares, alarm bits
            // honestly true after a restart, and a one-scan pulse no single sample can be right about.
            //
            // Unreachable by construction: step 4c refuses an undeclared resting value before any device is
            // touched, so a throw out of `For` means the loop built a wave from a plan it never made.
            inertRest.For(binding.SlotId),
            completionRegister,
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
            new ScanBudget(vector.MaxDurationScans, Math.Max(1, vector.CompressionFactor)),

            // 🔴 *** THE SETTLING PROBE TRAVELS WITH THE VECTOR SO THE DWELL HAPPENS AT ITS OWN CLOSE. ***
            // Settling used to be decided after the whole wave, by re-reading the device - at which point
            // only the LAST index on a slot still had its state, and every other vector was NotEstablished
            // by construction.
            ResolveSettling(vector, binding).Probe);
    }

    /// <summary>One resolution of a vector's settling declaration: the probe, or why there is none.</summary>
    private sealed record SettlingResolution(SettlingProbe? Probe, string? Refusal);

    /// <summary>
    /// 🔴 <b>THE ONE PLACE A SETTLING DECLARATION IS TURNED INTO REGISTERS.</b>
    ///
    /// <para>Called twice — once to build the wave's probe, once to explain the outcome — and it is one
    /// METHOD rather than two copies deliberately. <c>ResultRegisterOf</c> is the single join key in this
    /// system, and the method this logic was lifted out of records it as having been re-derived wrongly
    /// three times already. It is pure: no device, no clock.</para>
    /// </summary>
    private static SettlingResolution ResolveSettling(SubmissionVector vector, SlotBinding binding)
    {
        if (vector.Settling is not { UnchangedForScans: > 0 } settling)
        {
            return new SettlingResolution(null,
                "the vector declares no `settlingUnchangedForScans`, so its settling condition is prose the runner cannot check. "
                + "That is NOT settled: nothing established whether the value was final when it was read.");
        }

        var span = new List<SettlingRegister>();
        var unresolved = new List<string>();

        foreach (var name in settling.Signals ?? Array.Empty<string>())
        {
            var cited = name ?? string.Empty;
            var register = binding.ResultRegisterOf(cited);

            // -1 is "this binding does not carry the name", and it may NOT be defaulted to 0 — 0 is a real
            // offset carrying some other signal.
            if (register < 0)
            {
                unresolved.Add(cited.Length == 0 ? "<blank>" : cited);
                continue;
            }

            // The WIDTH matters: a Time occupies two registers, and comparing only the first would miss a
            // value moving in its other half.
            var width = Math.Max(1, binding.ResultSignal(cited)?.Registers ?? 1);
            for (var w = 0; w < width; w++)
                span.Add(new SettlingRegister(cited, register + w));
        }

        if (unresolved.Count > 0)
        {
            return new SettlingResolution(null,
                $"{unresolved.Count} of the {settling.Signals?.Count ?? 0} declared settling signal(s) resolve to no register of the binding "
                + $"serving slot '{vector.Slot}': {string.Join(", ", unresolved.Select(u => $"'{u}'"))}. *** THIS IS NOT A SETTLED RESULT AND IT IS "
                + "NOT A NARROWER ONE. *** Comparing only the names that DID resolve would answer a weaker question than the one declared, and a "
                + "settling check over no registers at all is satisfied by anything. The binding states the specification's name as `specName` "
                + "beside the block's own tag.");
        }

        if (span.Count == 0)
        {
            return new SettlingResolution(null,
                "the settling declaration names no signal, so there is nothing to hold still. *** EMPTY IS NOT CLEAN: a per-signal settling "
                + "check over zero registers is satisfied by any program whatsoever, so this is NOT settled. *** Name the signals whose "
                + "stability makes the observed value final — they are not the block's completion flag.");
        }

        return new SettlingResolution(new SettlingProbe(span, settling.UnchangedForScans), null);
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
