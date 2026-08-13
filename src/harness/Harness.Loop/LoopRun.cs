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
    BlockCompressionInputs? CompressionInputs = null)
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

        var caveats = Caveats(request);

        // ---- 1. DERIVE ------------------------------------------------------------------------------
        var mapResult = MapAllocator.Allocate(new WaveSetRequest(request.Geometry, request.Slots));
        if (!mapResult.Allocated)
        {
            return new LoopResult(LoopOutcome.NotDerivable, null, mapResult.SizeReport, null, null, null, null,
                Array.Empty<ResultPackage>(), caveats,
                "the map could not be derived, so the gate could not run and nothing was generated: "
                + string.Join(" | ", mapResult.Refusals));
        }

        var map = mapResult.Map!;
        caveats = caveats.Append(CollapseSeam(mapResult.SizeReport!)).ToArray();

        // ---- 2. GATE — before anything is spent -----------------------------------------------------
        // The floor is a property of the WAVE SET, so it is computed from the map rather than declared:
        // a slot is polled once per read cycle, and a read cycle is ceil(K/R) round trips.
        var readsPerCycle = map.ReadPlan(Enumerable.Range(0, map.Slots.Count)).Count;
        var floor = WireTiming.ObservabilityFloorScans(readsPerCycle);

        // What the copy layer WILL provide, derived from the bindings — available before it is generated,
        // which is what lets the observability gate run before anything is spent.
        var mirror = MirrorObservability.FromMinimalCopyLayer(request.Bindings.SelectMany(b => b.ResultSources));

        // *** ONE COMPRESSION VALUE, READ ONCE. *** It goes to the gate below and to the wave at step 7,
        // and the local is what makes it impossible for the two to be different numbers.
        var compression = request.Compression;

        var gate = SubmissionGate.Check(
            request.Vectors, request.Enumeration, request.Fidelity, request.BlockAuthor,
            mirror, floor, compression.Factor, request.ComputedConflicts, request.CompressionInputs);

        if (gate.Verdict != SubmissionVerdict.AdmissibleSubjectToJudgement)
        {
            return new LoopResult(LoopOutcome.NotAdmissible, gate, mapResult.SizeReport, null, null, null, null,
                Array.Empty<ResultPackage>(), caveats,
                $"the submission was {gate.Verdict}, so no copy layer was generated, nothing was deployed and no wave was run. "
                + $"{gate.Refused.Count} gate(s) refused, {gate.NotChecked.Count} could not run.");
        }

        // ---- 3. GENERATE ----------------------------------------------------------------------------
        var stamp = BuildStamp.Of(map, request.Bindings, request.Naming, request.ProgramUnderTest);
        var copyLayer = CopyLayerGenerator.Generate(map, request.Bindings, request.Naming, stamp);

        if (!copyLayer.Generated)
        {
            return new LoopResult(LoopOutcome.NotDerivable, gate, mapResult.SizeReport, null, null, null, null,
                Array.Empty<ResultPackage>(), caveats,
                "the copy layer could not be generated: " + string.Join(" | ", copyLayer.Refusals));
        }

        // ---- 4. ASSERT 0.1b -------------------------------------------------------------------------
        var retention = RetentionCheck.Check(copyLayer.Objects.Concat(request.ProgramUnderTest), request.Geometry);
        if (!retention.Passed)
        {
            return new LoopResult(LoopOutcome.NotAssertable, gate, mapResult.SizeReport, retention, null, null, null,
                Array.Empty<ResultPackage>(), caveats,
                "the generated objects failed the non-retentive assertion, so nothing was deployed: " + retention.Summary());
        }

        // ---- 5. DEPLOY — the device boundary --------------------------------------------------------
        var deployment = gateway.Deploy(copyLayer.Objects.Concat(request.ProgramUnderTest).ToArray(), stamp);
        if (!deployment.Attempted || !deployment.Loaded)
        {
            return new LoopResult(LoopOutcome.NotDeployed, gate, mapResult.SizeReport, retention, deployment, null, null,
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
            return new LoopResult(LoopOutcome.NotConfirmed, gate, mapResult.SizeReport, retention, deployment, version, null,
                Array.Empty<ResultPackage>(), caveats,
                "the version register did not confirm the build this loop generated, so the wave was not run: " + version.Detail);
        }

        // ---- 7. RUN ---------------------------------------------------------------------------------
        var tensors = request.Vectors
            .GroupBy(v => v.Slot, StringComparer.Ordinal)
            .Select(g => new SlotTensor(
                SlotIndexOf(map, g.Key),
                g.OrderBy(v => v.Index).Select(v => ToWireVector(v, request.Bindings, map)).ToArray()))
            .OrderBy(t => t.SlotIndex)
            .ToArray();

        var roundTripsBefore = client.RoundTrips;
        var wave = WaveRun.Run(client, compression, tensors, nowMs);

        // ---- 8. PACKAGE -----------------------------------------------------------------------------
        var packages = Package(request, map, stamp, client, wave, deployment, version, roundTripsBefore);

        return new LoopResult(LoopOutcome.Ran, gate, mapResult.SizeReport, retention, deployment, version, wave,
            packages, caveats,
            $"the wave ran to {wave.Length} index(es) over {tensors.Length} slot(s), costing {wave.RoundTrips} round trip(s).");
    }

    // -------------------------------------------------------------------------------------------------
    // Packaging — evidence assembled, never asserted
    // -------------------------------------------------------------------------------------------------

    private static IReadOnlyList<ResultPackage> Package(
        LoopRequest request, RegisterMap map, BuildStamp stamp, MirrorClient client,
        WaveResult wave, DeploymentOutcome deployment, VersionReport version, int roundTripsBefore)
    {
        var packages = new List<ResultPackage>();
        var slotsCoveredByOneRead = map.ReadPlan(Enumerable.Range(0, map.Slots.Count)).Max(r => r.SlotCount);

        foreach (var vector in request.Vectors)
        {
            var slotIndex = SlotIndexOf(map, vector.Slot);
            var distribution = wave.Distributions.SingleOrDefault(d => d.SlotIndex == slotIndex);
            var run = distribution is not null && vector.Index < distribution.Results.Count
                ? distribution.Results[vector.Index]
                : null;

            if (run is null)
            {
                // The wave stopped before this index. No package is fabricated for it: an absent package
                // is unambiguous, and one built on a run that did not happen would not be.
                continue;
            }

            var logIndex = wave.Log.Indices.ElementAtOrDefault(vector.Index);
            var binding = request.Bindings.Single(b => b.SlotId == vector.Slot);

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
                        MirrorObservability.FromMinimalCopyLayer(binding.ResultSources),
                        WireTiming.ObservabilityFloorScans(1), vector.CompressionFactor, request.Compression.Factor)),
                request.Enumeration,
                run,
                slotIndex,
                vector.Index,
                stimulus,
                StimulusExpectation.AtLeastOneScanPerRoundTrip(Math.Max(1, run.PollRounds)),
                Settling(client, vector, slotIndex, run, distribution!),
                Assertions(vector, binding, run),
                distribution!.CoRunning.FirstOrDefault(c => c.WaveIndex == vector.Index).CoRunners ?? Array.Empty<int>(),
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
    private static SettlingState Settling(MirrorClient client, SubmissionVector vector, int slotIndex, SlotRunResult run, SlotDistribution distribution)
    {
        if (vector.Settling is not { UnchangedForScans: > 0 } settling)
            return SettlingState.NotEstablished;

        if (vector.Index != distribution.CompletedAtIndex)
            return SettlingState.NotEstablished;

        if (run.Outcome != SlotOutcome.Completed)
            return SettlingState.NotEstablished;

        var from = client.ReadControl().ScanCounter;
        for (var poll = 0; poll < 200; poll++)
        {
            if (client.ReadControl().ScanCounter - from >= settling.UnchangedForScans)
            {
                return client.ReadResults(slotIndex).SequenceEqual(run.Results)
                    ? SettlingState.Settled
                    : SettlingState.NotSettled;
            }
        }

        return SettlingState.NotEstablished;
    }

    private static IReadOnlyList<AssertionOutcome> Assertions(SubmissionVector vector, SlotBinding binding, SlotRunResult run)
    {
        var assertionId = vector.Basis?.AssertionId ?? "<uncited>";

        return vector.Expectations.Select(e =>
        {
            var register = binding.ResultSources.ToList().IndexOf(e.Signal);
            var observed = register >= 0 && register < run.Results.Length
                ? unchecked((short)run.Results[register]).ToString()
                : null;

            return AssertionOutcome.Compare(assertionId, e.Signal, e.Expected ?? "<no predicate>", observed);
        }).ToArray();
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

        var downloadable = programUnderTest.Where(o => o.Kind != HarnessObjectKind.TagTable).ToArray();
        if (downloadable.Length == 0)
            return ManifestPresence.NotAvailable;

        return downloadable.All(o => deployment.Manifest.Contains(o.Name))
            ? ManifestPresence.Loaded
            : ManifestPresence.Absent;
    }

    private static int SlotIndexOf(RegisterMap map, string slotId)
    {
        var slot = map.Slot(slotId);
        return slot?.Index ?? throw new ArgumentException($"no slot '{slotId}' in this map.", nameof(slotId));
    }

    private static WireVector ToWireVector(SubmissionVector vector, IReadOnlyList<SlotBinding> bindings, RegisterMap map)
    {
        var binding = bindings.Single(b => b.SlotId == vector.Slot);
        var values = binding.VectorTargets
            .Select(t => vector.Inputs.TryGetValue(t, out var v) && short.TryParse(v, out var parsed) ? (ushort)parsed : (ushort)0)
            .ToArray();

        var completionRegister = binding.ResultSources.ToList().IndexOf(vector.CompletionSignal);

        return new WireVector(
            values,
            new InertDeclaration(binding.ResultSources.Select((_, i) => (i, (ushort)0)).ToDictionary(x => x.i, x => x.Item2)),
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
