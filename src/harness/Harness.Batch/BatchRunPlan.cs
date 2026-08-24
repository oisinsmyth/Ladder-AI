using Harness.Device;
using Harness.Map;

namespace Harness.Batch;

/// <summary>What a run step is for, and — where it matters — what its failure means.</summary>
public enum BatchStepKind
{
    /// <summary><c>converter lease acquire</c>. Before anything touches Portal or the rig.</summary>
    LeaseAcquire,

    /// <summary><c>openness-cli export-all</c>: what the project actually contains, for the drift check.</summary>
    ExportAll,

    /// <summary>
    /// 🔴 <c>converter drift-check</c> — <b>does every object the lanes SUPPLIED still match what is in
    /// the project?</b>
    ///
    /// <para>The build stamp is computed over the supplied program and means "what is executing". Nothing
    /// verified that claim, and it was measured false: a lane supplied a <c>Main</c> that differed from
    /// the one on the controller, so the stamp described a program nobody was running — and the block it
    /// differed by was an uncalled slot FC whose whole lane then timed out.</para>
    ///
    /// <para>⚠️ <b>Deliberately WITHOUT <c>--complete</c>.</b> That flag declares the exports dir the whole
    /// picture, and the union is a SUBSET of a ~119-object project, so it would report every unsupplied
    /// object as "in the controller and no .ir describes it" — a hundred spurious findings that would get
    /// the check switched off. The paired question is the right one here, and drift-check's own SCOPE line
    /// says which question it answered.</para>
    /// </summary>
    DriftCheck,

    /// <summary>
    /// <c>openness-cli export-all --tagtables</c> — the WHOLE project, for the third leg.
    ///
    /// <para>Its own export, and not the one <see cref="ExportAll"/> takes, for one reason:
    /// <c>--tagtables</c> is opt-in there and the union pair does not want it (a tag table has no
    /// <c>.ir</c> counterpart to pair with in a subset comparison). Without it <c>DefaultTagTable</c> and
    /// <c>HarnessMirror</c> pair with nothing and come back SKIPPED — and <b><c>HarnessMirror</c> is the
    /// object where the register map lives</b>, the one most worth comparing against the controller. That
    /// is exactly what today's third-leg run missed.</para>
    /// </summary>
    WholeCorpusExport,

    /// <summary>
    /// 🔴 <c>converter drift-check --complete</c> over the WHOLE COMMITTED CORPUS — <b>the third leg: does
    /// the live project still match its own committed description?</b>
    ///
    /// <para><b>Not a new capability — the same pair as <see cref="DriftCheck"/> asking a different
    /// question, over a different population.</b> This repo has exactly one self-firing mechanism
    /// (<c>hooks/pre-commit</c>) and no CI, so "run this periodically" has never had anywhere to live. A
    /// step that runs whenever Portal is already held is the closest thing to a schedule available, and
    /// inventing a scheduler to do better would be a larger and less honest change.</para>
    ///
    /// <para><c>--complete</c> declares the exports dir the whole picture, so an object in the project
    /// that NO committed <c>.ir</c> describes is a finding rather than a silence. That is why it must not
    /// be pointed at the lane union, which is a subset of a ~119-object project and would report a
    /// hundred spurious findings — see <see cref="DriftCheck"/>'s note.</para>
    ///
    /// <para>⚠️ <b>MEANINGFUL FOR THE REFERENCE PROJECT AND NOT FOR A LIVE JOB.</b> A live job has no
    /// committed corpus to point <c>--project</c> at, so this step cannot exist there — and a step that
    /// cannot exist must never read like one that passed. Its absence is a NOTICE on the plan, in these
    /// words, for the same reason <see cref="DriftCheck"/>'s scope line exists.</para>
    /// </summary>
    WholeCorpusDrift,

    /// <summary><c>harness-run --generate-only --emit</c>: the merged copy layer, from the merged binding.</summary>
    Generate,

    /// <summary>A Portal or download step, planned by <see cref="DeploymentPlan"/>.</summary>
    Deploy,

    /// <summary>
    /// 🔴 <c>harness-mirror-read --declared-registers &lt;DERIVED&gt;</c> — <b>the width the PROGRAM
    /// declares, asked of the CONTROLLER.</b>
    ///
    /// <para><b>The comparison is not new; the wiring is.</b> <c>served-area</c> derives the Modbus window
    /// from the program corpus and prints, on every run, that it cannot see whether the block it read is
    /// the block on the controller. <c>harness-mirror-read</c>'s whole contract is the other half:
    /// <c>--declared-registers</c> is required and deliberately not defaulted because <i>"it is the CLAIM
    /// under test and it comes from the IR"</i>, and its exit codes are the verdict — 0 exactly as wide,
    /// 6 NARROWER, 7 wider, 4 measured nothing. Feeding it the DERIVED number instead of a hand-typed one
    /// makes those codes the derived-vs-probed verdict.</para>
    ///
    /// <para><b>THE DERIVED VALUE, NEVER THE AUTHORED ONE.</b> The authored <c>declaredRegisters</c> is
    /// the field the derivation exists to check; feeding that back would have the run compare a number
    /// against itself and pass. Emitted only on the <c>Derived == true</c> path for the same reason.</para>
    ///
    /// <para>🔴 <b>BETWEEN <see cref="Deploy"/> AND THE FIRST <see cref="Wave"/>, AND NOWHERE ELSE.</b>
    /// <c>MB_SERVER</c> accepts ONE connection per instance — with a viewer attached, a conformance wave
    /// once reported 0 of 22 vectors attempted — so this step is strictly sequential with the waves. And
    /// it must follow the download, because the width it is asking about is the one just deployed.</para>
    ///
    /// <para>⚠️ <b>WHAT IT STILL CANNOT SEE.</b> Only the area the device can be ASKED about: a register
    /// that answers is reachable, which is not the same as correct, and nothing here says the mirror's
    /// CONTENTS are what the map intends. It also cannot see an occupant of the area — that is the
    /// neighbour derivation's question, and it is answered against the corpus, not the CPU.</para>
    /// </summary>
    MirrorWidth,

    /// <summary><c>harness-run --verify</c> for ONE lane, against the shared map.</summary>
    Wave,

    /// <summary><c>converter lease release</c>. Runs even when something above failed.</summary>
    LeaseRelease,
}

/// <summary>
/// One invocation: the executable, the argument vector, what it is for, and which lane it belongs to.
/// </summary>
/// <param name="Lane">
/// The lane this step serves, or null for a step the whole batch shares. <b>Carried so a failure can be
/// attributed</b> — "the batch failed" is not an actionable sentence when six lanes are in it.
/// </param>
public sealed record BatchStep(BatchStepKind Kind, string Executable, IReadOnlyList<string> Arguments, string Purpose, string? Lane = null)
{
    public string CommandLineText => CommandLine.Render(Executable, Arguments);
}

/// <summary>Where the binaries are and what the run is allowed to touch.</summary>
/// <param name="Holder">
/// Who holds the leases. <b>Must identify this agent specifically</b> — the recorded race on the text
/// file the lease replaces happened because two entries carried the same non-specific name.
/// </param>
/// <param name="HolderPid">
/// The process that holds the gate for the run's lifetime. <b>Not the converter's own pid</b>: a CLI
/// invocation exits the moment it returns, and a lease held by a dead process makes every reclaim
/// decision fall back to the TTL alone.
/// </param>
public sealed record BatchRunOptions(
    string ConverterExe,
    string HarnessRunExe,
    string OpennessCliExe,
    string LeasesDirectory,
    string PortalProject,
    string RigAddress,
    string Holder,
    int HolderPid,
    string StagingDirectory,
    string MergedBindingPath,
    string PortalEvidencePath,
    int RigPort = 503,
    int RigUnit = 1,
    string? DeviceAllowlistPath = null,
    int LeaseTtlMinutes = 60,

    /// <summary>
    /// 🔴 The operator's sentence for <c>--attest-portal-unjudgeable</c>, or null.
    ///
    /// <para>Carried through to the PORTAL acquire only. It covers the one verdict a tool cannot
    /// settle — a Portal process Openness cannot see, which reports the same <c>projectPath: null</c> as
    /// one with nothing open — and the lease records the sentence for as long as it is held. It cannot
    /// override a MEASURED holder, and the lease refuses that regardless of what is passed here.</para>
    /// </summary>
    string? PortalAttestation = null,

    /// <summary>
    /// Inert attempts at the first index of each lane's wave, including the first. <b>12 attempts 5 s
    /// apart is up to a minute of ASKING</b>, which ends as soon as the plant is quiescent — where the
    /// blind 15 s wait it replaces cost 15 s always and was measured to be too short anyway.
    /// </summary>
    int InertRetries = 12,

    /// <summary>Seconds between inert attempts. Roughly 200 scans at the measured 24.9 ms.</summary>
    int InertRetryIntervalSeconds = 5,

    /// <summary>Where the union of the lanes' program IR was materialised, for the drift check.</summary>
    string? UnionIrDirectory = null,

    /// <summary>Where the project's own objects are exported to, for the drift check.</summary>
    string? ProjectExportDirectory = null,

    /// <summary>
    /// 🔴 <c>harness-mirror-read</c>. <b>Null plans no width comparison, and the plan SAYS so</b> — the
    /// absence of a check must not be reachable by omitting an argument, which is exactly how the drift
    /// pair could once be dropped without a line in the report.
    /// </summary>
    string? MirrorReadExe = null,

    /// <summary>
    /// The WHOLE committed corpus (e.g. <c>ir/test-project001/</c>) for the third leg — not the lane
    /// union. <b>Null on a live job by construction</b>: there is no committed corpus there, which is a
    /// scope fact and not a configuration mistake, and the plan says which of the two it is.
    /// </summary>
    string? CommittedCorpusDirectory = null,

    /// <summary>
    /// Where the whole-project export (<c>--tagtables</c>) goes. <b>Its own directory</b>: see the
    /// refusal in <see cref="BatchRunPlan.For"/> — a <c>--complete</c> comparison against a directory
    /// another step also writes into cannot state what its denominator was.
    /// </summary>
    string? WholeCorpusExportDirectory = null);

/// <summary>The ordered steps of one batch run, or every reason there are none.</summary>
/// <param name="Notices">
/// 🔴 <b>THE CHECKS THIS PLAN DOES NOT CONTAIN, AND WHY — in their own words.</b>
///
/// <para>Not refusals: a refusal empties the plan, and none of these is a reason to refuse. They are the
/// third state, and it is the one that looks most like a pass. A step that is simply absent is
/// indistinguishable from one that ran and found nothing — measured here already, where the export/drift
/// pair could be dropped by omitting <c>--staging</c> and the report was identical to a run that had
/// passed it.</para>
///
/// <para>⚠️ <b>They are only worth anything if a caller PRINTS them.</b> A notice nobody renders is the
/// silence it was written to replace.</para>
/// </param>
public sealed record BatchRunPlan(
    IReadOnlyList<BatchStep> Steps,
    IReadOnlyList<string> Refusals,
    IReadOnlyList<string> Notices)
{
    /// <summary>A plan with nothing to declare. Kept so every existing construction site still reads.</summary>
    public BatchRunPlan(IReadOnlyList<BatchStep> steps, IReadOnlyList<string> refusals)
        : this(steps, refusals, Array.Empty<string>())
    {
    }

    public bool Planned => Refusals.Count == 0 && Steps.Count > 0;

    /// <summary>The steps that must run even after a failure above them.</summary>
    public IEnumerable<BatchStep> Teardown => Steps.Where(s => s.Kind == BatchStepKind.LeaseRelease);

    /// <summary>
    /// Build the plan. <b>Pure — no filesystem, no process, no Portal</b> — so the argument vectors a rig
    /// session will execute are the ones a unit test asserts. That is the same standard
    /// <see cref="DeploymentPlan"/> already sets, and it is the only standard available to a component
    /// that cannot be run without a controller.
    /// </summary>
    public static BatchRunPlan For(BatchPlanResult batch, IReadOnlyList<Lane> lanes, BatchRunOptions options)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(lanes);
        ArgumentNullException.ThrowIfNull(options);

        var refusals = new List<string>();

        if (!batch.Planned)
        {
            refusals.Add("the batch was not planned, so there is nothing to run. Its refusals are the ones to read; this plan adds none of its own.");
            return new BatchRunPlan(Array.Empty<BatchStep>(), refusals);
        }

        if (lanes.Count == 0)
            refusals.Add("NOTHING TO RUN: no lanes. A run over an empty batch would acquire the gate, deploy nothing and report a clean pass.");

        foreach (var (value, name) in new[]
                 {
                     (options.Holder, "--holder"),
                     (options.LeasesDirectory, "--leases"),
                     (options.PortalProject, "--portal-project"),
                     (options.RigAddress, "--rig"),
                     (options.PortalEvidencePath, "--portal-evidence"),
                 })
        {
            if (string.IsNullOrWhiteSpace(value))
                refusals.Add($"{name} is required: a batch run takes the Portal and rig gates, and neither can be taken without it.");
        }

        if (options.HolderPid <= 0)
        {
            refusals.Add("--holder-pid is required and must be a live process that outlives this run. It is NOT this process: a "
                + "lease held by a pid that exits immediately makes every later reclaim decision fall back to the TTL alone, while "
                + "still looking evidence-based.");
        }

        // 🔴 THE TWO EXPORT DIRECTORIES MUST BE DIFFERENT, AND SHARING ONE IS REFUSED RATHER THAN COPED
        // WITH. The union export is taken deliberately WITHOUT --tagtables and the whole-corpus one WITH
        // it, so one directory would hold two different pictures written by two steps — and `--complete`
        // means "this directory IS the whole project". A comparison that cannot say what its denominator
        // was is the exact failure `--complete` exists to remove, so it is refused at planning time
        // instead of producing a report nobody can read.
        if (!string.IsNullOrWhiteSpace(options.WholeCorpusExportDirectory)
            && !string.IsNullOrWhiteSpace(options.ProjectExportDirectory)
            && string.Equals(Path.TrimEndingDirectorySeparator(options.WholeCorpusExportDirectory!),
                Path.TrimEndingDirectorySeparator(options.ProjectExportDirectory!), StringComparison.OrdinalIgnoreCase))
        {
            refusals.Add("--whole-corpus-exports and the union's export directory are the same path ("
                + options.WholeCorpusExportDirectory + "). They hold different pictures — the union export omits "
                + "--tagtables and the whole-corpus one requires it — and `drift-check --complete` declares its "
                + "exports directory to BE the whole project. Sharing one makes the complete run's denominator "
                + "unstatable, which is the only thing it is for. Point them at different directories.");
        }

        if (refusals.Count > 0)
            return new BatchRunPlan(Array.Empty<BatchStep>(), refusals);

        var steps = new List<BatchStep>();

        // What this plan does NOT check, and why. Populated beside each decision rather than collected at
        // the end, so a check that stops being emitted cannot stop being explained in the same edit.
        var notices = new List<string>();

        // ---- 1. THE GATES, BEFORE ANYTHING ELSE. ------------------------------------------------
        //
        // Portal first, because it is the one a human can take and the one whose refusal is not a
        // queue position. Taking the rig first would mean holding it while discovering a person is in
        // the project — locking an agent out of the rig for a run that was never going to happen.
        steps.Add(Lease(options, "acquire", "portal:" + options.PortalProject, withEvidence: true));
        steps.Add(Lease(options, "acquire", "rig:" + options.RigAddress, withEvidence: false));

        // ---- 2. THE MERGED COPY LAYER. ----------------------------------------------------------
        //
        // Generated from the MERGED binding, so the map covers every lane's slots. Any lane's
        // submission will do for this step — the copy layer is a function of the map, the bindings and
        // the naming, and of none of the vectors — but the first lane's is used rather than a synthetic
        // one, because a submission that exists is one somebody has already gated.
        // 🔴 *** THE UNION OF EVERY LANE'S PROGRAM, NOT THE GENERATOR LANE'S — AND THE BUILD STAMP IS WHY.
        // ***
        //
        // The stamp is computed over map + bindings + naming + PROGRAM UNDER TEST, and it means "what is
        // executing". A batch deploys every lane's blocks, so the stamp of what lands on the controller
        // covers all of them. A step that hashed one lane's program would stamp the device with a value
        // no lane could reproduce.
        //
        // The same union goes to every WAVE below, and that is the part worth stating plainly: a lane
        // verifying with only its OWN program computes a different stamp from the one deployed, the
        // version check fails, and the package comes back Stale — which reads as "the download never
        // reached it" and sends a reader to re-download a device that is already correct. Being batched
        // must not change a lane's verdict, and this is what that costs.
        var programUnion = lanes.SelectMany(l => l.ProgramPaths).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        // 🔴 *** AND THE DENOMINATOR THAT UNION IS MEASURED AGAINST — the other half of the same claim. ***
        //
        // The union above is what the stamp HASHES. Nothing stated what it SHOULD have hashed, so a short
        // union produced a stamp indistinguishable from a complete one: measured in
        // docs/18-project-workbench.md §5 "Phase 10 — Wave time", under "THE BUILD STAMP DOES COVER THE
        // PARAMETER DB", where a wave's stamp omitted the parameter DB and "compressing them changes the
        // controller without changing the stamp". (The eight-object figure in that entry was retracted
        // 2026-08-24: the deployed set was nine, stamp 622F3EB7.)
        //
        // It goes to the GENERATE step and to every WAVE for the identical reason the union does — a step
        // measured against a different denominator would report a different gap from the deployment it
        // belongs to.
        //
        // 🔴 NULL WHEN NO LANE STAGED ANYTHING NAMEABLE, and then NOTHING is passed. `harness-run` then
        // says NO STAGED CORPUS WAS SUPPLIED, which is the true statement about a batch of lanes enqueued
        // from bare --program lists. Inventing a denominator here would turn that loud absence into a
        // silent assumption, which is the failure the whole mechanism exists to prevent.
        //
        // 🔴 AND A PARTIAL ONE IS REFUSED THE SAME WAY — see LaneCorpus. The lanes share one deployment, so
        // a union over only the lanes that happen to declare a manifest is a denominator SHORTER than what
        // is downloaded, which is the defect being measured rather than a smaller version of the fix.
        var stagedCorpus = LaneCorpus.Of(lanes).Corpus;

        // ---- 2a. DOES THE SUPPLIED PROGRAM STILL MATCH THE PROJECT? -----------------------------
        //
        // After the gates (this reads Portal) and before generation, because a stamp computed over a
        // program that is not the one in the project describes nothing. Skipped only when the caller
        // supplied nowhere to put the export or nothing to compare — and then the run SAYS so rather
        // than passing quietly.
        if (!string.IsNullOrWhiteSpace(options.UnionIrDirectory) && !string.IsNullOrWhiteSpace(options.ProjectExportDirectory))
        {
            steps.Add(new BatchStep(
                BatchStepKind.ExportAll,
                options.OpennessCliExe,
                new[] { "export-all", options.PortalProject, "--out", options.ProjectExportDirectory! },
                "export what the project actually contains, so the supplied program can be compared against it"));

            steps.Add(new BatchStep(
                BatchStepKind.DriftCheck,
                options.ConverterExe,
                new[] { "drift-check", "--project", options.UnionIrDirectory!, "--exports", options.ProjectExportDirectory! },
                "every object the lanes supplied must match the project - the stamp claims it is what executes"));
        }

        // ---- 2b. THE THIRD LEG: DOES THE LIVE PROJECT STILL MATCH ITS OWN COMMITTED DESCRIPTION? ----
        //
        // Same two commands as the pair above, a different question, and a different population. The pair
        // above asks about the objects THE LANES SUPPLIED and is the precondition for the build stamp
        // meaning anything. This one asks about EVERY object in the project, against the whole committed
        // corpus, with `--complete` — so an object in the project that no committed .ir describes is a
        // finding rather than a silence.
        //
        // 🔴 IT RUNS HERE BECAUSE THIS IS WHERE PORTAL IS ALREADY HELD. There is no scheduler in this
        // repo — one self-firing mechanism (hooks/pre-commit), no CI, no timed hooks — and inventing one
        // would be a bigger change than the check. A step under an existing lease is the closest thing to
        // a schedule available, and it costs no rig time: it is before the deployment, so it never sits
        // between the download and the waves.
        //
        // ⚠️ AND IT IS SCOPED HONESTLY. A live job has no committed corpus, so on one this pair is absent
        // — stated as a NOTICE, because a step that cannot exist must not read like one that passed.
        if (!string.IsNullOrWhiteSpace(options.CommittedCorpusDirectory)
            && !string.IsNullOrWhiteSpace(options.WholeCorpusExportDirectory))
        {
            steps.Add(new BatchStep(
                BatchStepKind.WholeCorpusExport,
                options.OpennessCliExe,
                new[] { "export-all", options.PortalProject, "--out", options.WholeCorpusExportDirectory!, "--tagtables" },
                "export the WHOLE project INCLUDING tag tables - HarnessMirror is where the register map lives, and "
                + "without --tagtables it pairs with nothing and comes back SKIPPED"));

            steps.Add(new BatchStep(
                BatchStepKind.WholeCorpusDrift,
                options.ConverterExe,
                new[] { "drift-check", "--project", options.CommittedCorpusDirectory!, "--exports", options.WholeCorpusExportDirectory!, "--complete" },
                "THE THIRD LEG: the whole committed corpus against the whole project, --complete, so an object in the "
                + "project that no .ir describes is NAMED rather than passed over. Meaningful for the reference project "
                + "only - a live job has no committed corpus"));
        }
        else
        {
            notices.Add("THE LIVE PROJECT WAS NOT COMPARED AGAINST THE WHOLE COMMITTED CORPUS (the third leg). "
                + (string.IsNullOrWhiteSpace(options.CommittedCorpusDirectory)
                    ? "No committed corpus was supplied to point --project at. On a live job that is CORRECT AND PERMANENT: "
                      + "there is no committed corpus, so this leg cannot exist there and its absence is a scope fact, not a "
                      + "configuration mistake. On the reference project it means somebody omitted an argument."
                    : "No directory was supplied for the whole-project export, so there was nowhere to take the "
                      + "--tagtables dump this comparison reads.")
                + " The union pair above, where it ran, compared only what the lanes SUPPLIED - it says nothing about the "
                + "rest of the project.");
        }

        var generatorLane = lanes[0];
        var generateArgs = new List<string>
        {
            "--submission", generatorLane.SubmissionPath,
            "--binding", options.MergedBindingPath,
            "--generate-only",
            "--emit", options.StagingDirectory,
        };
        AddPrograms(generateArgs, programUnion);
        AddStaged(generateArgs, stagedCorpus);

        steps.Add(new BatchStep(
            BatchStepKind.Generate,
            options.HarnessRunExe,
            generateArgs,
            "generate the merged copy layer and mirror tag table for every lane's slots",
            generatorLane.Name));

        // 🔴 *** ASSERTION COVERAGE FOR THE OTHER LANES ARRIVES AT THEIR WAVE STEP, AND NOWHERE EARLIER. ***
        //
        // This step runs for lanes[0] alone — correctly, because the copy layer is MERGED and generating
        // it once per lane would be N conflicting emissions of one artifact — and it is the only step
        // above the waves that reports what a submission is worth. Each Wave step now emits its own lane's
        // figure, to its console and to its `--out` artifact, taken from the gate that lane was actually
        // admitted by; so the wired path covers every lane and NO `harness gate` step is planned here.
        // A second computation of one measurement is a second rendering to read, a second place for the
        // enumeration and binding arguments to diverge from what the wave gated on, and a figure that
        // could disagree with the submission that ran.
        //
        // WHAT THAT LEAVES is a batch that dies before its waves — at the deployment, say — having
        // reported coverage for the generator lane and for no other, which looks exactly like a batch
        // whose other lanes were measured and found fine.
        //
        // ⚠️ ONLY WHEN THERE IS MORE THAN ONE LANE. With a single lane the Generate step IS that lane and
        // there is no gap; a notice on every plan is a notice nobody reads, which is the rule this file
        // already applies to the package caveats.
        if (lanes.Count > 1)
        {
            notices.Add("ASSERTION COVERAGE FOR EVERY LANE BUT THE GENERATOR LANE IS COMPUTED ONLY AT ITS WAVE STEP. "
                + $"The Generate step runs for '{generatorLane.Name}' alone, so it reports that lane's distinct-assertions-cited "
                + $"figure and no other; the remaining {lanes.Count - 1} lane(s) report theirs when their own wave runs, into "
                + "that wave's console and its result artifact. A BATCH THAT STOPS BEFORE THE WAVES HAS MEASURED THE OTHER "
                + "LANES' COVERAGE NOT AT ALL — which is not the same as measuring it and finding it adequate, and there is "
                + "deliberately no threshold anywhere that would make it look like a check.");
        }

        // ---- 3. THE DEPLOYMENT. -----------------------------------------------------------------
        //
        // Deliberately NOT re-planned here. DeploymentPlan already encodes the order, the layout
        // re-assert after every import (required, not precautionary — a re-import silently reverts a
        // block to Optimized, where classic S7comm cannot see it at all), and the exit-code reading
        // where "ran, reported nothing, examined nothing" is NotProven rather than Ok. Re-deriving that
        // sequence here would be a second place for it to drift.
        steps.Add(new BatchStep(
            BatchStepKind.Deploy,
            "<deployment>",
            Array.Empty<string>(),
            "import-all, layout re-assert, compile-all, sanity-check and download — planned by DeploymentPlan over the staged IR"));

        // ---- 3a. IS THE AREA ON THE DEVICE AS WIDE AS THE PROGRAM SAYS? -------------------------
        //
        // 🔴 THE DERIVED WIDTH, NEVER THE AUTHORED ONE. `served-area` reads the window off the program
        // corpus and says, on every run, that it cannot see whether the block it read is the block on the
        // controller. `harness-mirror-read --declared-registers` is the other half of exactly that
        // sentence, and it has existed whole since phase 2 — what was missing was a caller feeding it the
        // DERIVED number. Handing it the authored `declaredRegisters` would have the tool agree with the
        // binding, which is the comparison the derivation exists to replace.
        //
        // 🔴 HERE AND NOWHERE ELSE. MB_SERVER accepts ONE connection per instance — with a second client
        // attached, a conformance wave once reported 0 of 22 vectors attempted — so this is strictly
        // sequential with the waves. And it must follow the download, because the width under test is the
        // one just deployed.
        //
        // A download stops the CPU, so this step meets a just-restarted scan counter and exit 8 is
        // reachable. It is NOT a width verdict, and the answer is the wave's own: ASK AGAIN, N times, and
        // report how many attempts it took — a measurement of the settling rather than a guess at it. The
        // same two numbers, so one transient is not described by two policies.
        //
        // No new transport, no second allowlist: "a second client with its own settings would be an
        // uncalibrated one."
        if (batch.ServedArea is { Derived: true } servedWidth && !string.IsNullOrWhiteSpace(options.MirrorReadExe))
        {
            var widthArguments = new List<string>
            {
                "--address", options.RigAddress,
                "--port", options.RigPort.ToString(),
                "--unit", options.RigUnit.ToString(),
                "--declared-registers", servedWidth.Registers.ToString(),
            };

            if (!string.IsNullOrWhiteSpace(options.DeviceAllowlistPath))
            {
                widthArguments.Add("--allowlist");
                widthArguments.Add(options.DeviceAllowlistPath);
            }

            // Seconds on the wave's flag, milliseconds on this one — converted here, once, rather than
            // giving one transient two units to be wrong in.
            widthArguments.Add("--scan-retry");
            widthArguments.Add(options.InertRetries.ToString());
            widthArguments.Add("--scan-retry-interval-ms");
            widthArguments.Add((options.InertRetryIntervalSeconds * 1000).ToString());

            // The verdict as an artifact, not as scrollback. A reviewer opens the staging directory weeks
            // later; nobody opens a terminal that has closed.
            //
            // Only where there IS a staging directory: Path.Combine over an empty one yields the bare
            // relative name, which would drop a mirror-width.json into whatever the working directory
            // happened to be. A run with no artifact is worse than one with a misplaced file only if
            // nobody says so, which is what the notice below is for.
            if (!string.IsNullOrWhiteSpace(options.StagingDirectory))
            {
                widthArguments.Add("--out");
                widthArguments.Add(Path.Combine(options.StagingDirectory, "mirror-width.json"));
            }
            else
            {
                notices.Add("THE WIDTH COMPARISON WILL LEAVE NO ARTIFACT: no staging directory was given, so the verdict "
                    + "exists only as console output and an exit code. The comparison still runs and still gates.");
            }

            steps.Add(new BatchStep(
                BatchStepKind.MirrorWidth,
                options.MirrorReadExe!,
                widthArguments,
                $"the DERIVED width ({servedWidth.Registers} register(s)) asked of the CONTROLLER, from both sides - "
                + "exit 6 is NARROWER than the program declares and stops the batch"));
        }
        else
        {
            notices.Add("THE DERIVED WIDTH WAS NOT COMPARED AGAINST THE DEVICE. "
                + (batch.ServedArea is { Derived: true }
                    ? "No harness-mirror-read binary was supplied, so there was nothing to ask the controller with."
                    : "No width was derived from the program corpus, so there is no claim to test: "
                      + (batch.ServedArea?.Denominator ?? "the planner was given no served-area fact at all."))
                + " Every register figure this run rests on therefore comes from the STAGED CORPUS, which cannot see "
                + "whether the block it was read from is the block on the controller.");
        }

        // ---- 4. ONE WAVE PER LANE, against the shared map. --------------------------------------
        //
        // Each lane runs its OWN submission against the MERGED binding. The other lanes' slots simply
        // have no vector at any index, and the wave already treats that as inert — the same mechanism
        // that covers an excised slot. So no lane's gate evaluation is touched by being batched.
        foreach (var lane in lanes)
        {
            var arguments = new List<string>
            {
                "--submission", lane.SubmissionPath,
                "--binding", options.MergedBindingPath,
                "--verify",
                "--host", options.RigAddress,
                "--port", options.RigPort.ToString(),
                "--unit", options.RigUnit.ToString(),
            };

            // The UNION, not this lane's own — see the note above the Generate step. A lane that hashed
            // only its own program would compute a stamp the device does not carry and report Stale.
            AddPrograms(arguments, programUnion);

            // The UNION again, for the same reason: every lane's wave reports its coverage against the
            // objects the SHARED deployment staged, not against its own lane's slice of them.
            AddStaged(arguments, stagedCorpus);

            if (!string.IsNullOrWhiteSpace(options.DeviceAllowlistPath))
            {
                arguments.Add("--allowlist");
                arguments.Add(options.DeviceAllowlistPath);
            }

            // 🔴 RETRY THE INERT PHASE AT INDEX 0 RATHER THAN SLEEP BEFORE THE WAVE.
            //
            // A download leaves the plant moving, and the first wave after one refuses NotQuiescent —
            // measured twice on the rig. The blind wait that used to sit here was a guess at a duration
            // NOBODY HAS MEASURED, and 15 s was measured to be too short. The inert phase already IS the
            // readiness test: two observations a scan apart, compared. So the wave asks again, and the
            // number of attempts it needed is REPORTED — which finally measures the settling time
            // instead of guessing it.
            //
            // Only the batch passes this, and only because it has just deployed.
            arguments.Add("--inert-retry");
            arguments.Add(options.InertRetries.ToString());
            arguments.Add("--inert-retry-interval");
            arguments.Add(options.InertRetryIntervalSeconds.ToString());

            // 🔴 *** THE NEIGHBOUR LIST'S STATE TRAVELS INTO THE RESULT PACKAGE (workbench Y1). ***
            //
            // The plan's line is printed to a terminal and gone with it; the package is what a reviewer
            // opens weeks later, and it is where "nothing else in this area was written to" would
            // otherwise be assumed silently. A run whose mirror was allocated against a neighbour list
            // NOBODY DERIVED rests on an unmeasured premise, which is ValidityStamp.Caveats' own
            // definition of what belongs in it.
            //
            // Only on the NOT DERIVED path, and that is the same rule the program manifest already
            // follows: a caveat that fires on every package is a caveat nobody reads.
            if (batch.Neighbours is { Derived: false } neighbours)
            {
                arguments.Add("--neighbours-not-derived");
                arguments.Add("NEIGHBOURS: NOT DERIVED — " + neighbours.Why);
            }

            arguments.Add("--out");
            arguments.Add(Path.Combine(options.StagingDirectory, $"{lane.Name}-result.json"));

            steps.Add(new BatchStep(BatchStepKind.Wave, options.HarnessRunExe, arguments,
                $"run lane '{lane.Name}' against the shared map", lane.Name));
        }

        // ---- 5. THE GATES BACK. -----------------------------------------------------------------
        //
        // Released in the reverse order they were taken, and they run WHATEVER happened above: a lease
        // left held by a finished run is the failure the TTL exists to bound, and bounding it is not
        // the same as avoiding it.
        steps.Add(Lease(options, "release", "rig:" + options.RigAddress, withEvidence: false));
        steps.Add(Lease(options, "release", "portal:" + options.PortalProject, withEvidence: false));

        return new BatchRunPlan(steps, Array.Empty<string>(), notices);
    }

    /// <summary>
    /// <c>harness-run --program</c> is multi-valued and consumes tokens until the next flag, so each path
    /// gets its own <c>--program</c>. Repeating the flag is what keeps a path containing a space from
    /// being read as two.
    /// </summary>
    private static void AddPrograms(List<string> arguments, IReadOnlyList<string> programs)
    {
        foreach (var program in programs)
        {
            arguments.Add("--program");
            arguments.Add(program);
        }
    }

    /// <summary>
    /// 🔴 <b>One <c>--staged <c>name=source</c></c> per corpus row, and NOTHING AT ALL when there is no
    /// corpus.</b>
    ///
    /// <para>The source travels with the name because <b>a gap nobody can trace to a document is a gap
    /// nobody can close</b>: <c>DB_Params</c> alone says something is missing, <c>DB_Params [lane 'vessel']</c>
    /// says which manifest to go and fix. It is one token per row — the label contains a space — which is
    /// also why the flag repeats rather than taking a list.</para>
    ///
    /// <para>Emitting an empty <c>--staged</c> for an absent corpus would be the worst of the three
    /// options: <c>harness-run</c> would read a corpus of nothing, and "hashed n of 0" over an empty gap
    /// list is the shape of a check that examined nothing.</para>
    /// </summary>
    private static void AddStaged(List<string> arguments, StagedCorpus? corpus)
    {
        foreach (var row in corpus?.Entries ?? Array.Empty<StagedCorpusEntry>())
        {
            arguments.Add("--staged");
            arguments.Add($"{row.Name}={row.Source}");
        }
    }

    private static BatchStep Lease(BatchRunOptions options, string verb, string resource, bool withEvidence)
    {
        var arguments = new List<string>
        {
            "lease", verb,
            "--resource", resource,
            "--leases", options.LeasesDirectory,
            "--holder", options.Holder,
        };

        if (verb == "acquire")
        {
            arguments.Add("--pid");
            arguments.Add(options.HolderPid.ToString());
            arguments.Add("--ttl");
            arguments.Add(options.LeaseTtlMinutes.ToString());
            arguments.Add("--purpose");
            arguments.Add("batch run");

            // Only the Portal lease takes evidence, and it REFUSES without it. Nothing can detect a rig
            // in use — MB_SERVER's one connection is discovered by failure — so passing evidence there
            // would imply an observation nobody made.
            if (withEvidence)
            {
                arguments.Add("--portal-evidence");
                arguments.Add(options.PortalEvidencePath);

                // On the Portal acquire only, and only when the operator supplied one. It rides beside
                // the evidence rather than replacing it: the evidence still has to be present and fresh,
                // and this speaks only to the process nothing could classify.
                if (!string.IsNullOrWhiteSpace(options.PortalAttestation))
                {
                    arguments.Add("--attest-portal-unjudgeable");
                    arguments.Add(options.PortalAttestation);
                }
            }
        }

        return new BatchStep(
            verb == "acquire" ? BatchStepKind.LeaseAcquire : BatchStepKind.LeaseRelease,
            options.ConverterExe,
            arguments,
            $"{verb} the {resource} gate");
    }
}
