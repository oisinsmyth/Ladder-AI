using Harness.Device;
using Harness.Loop;
using Harness.Map;
using Harness.Run;
using System.Text;

namespace Harness.Batch;

/// <summary>Exit codes. 0 is a plan or an enqueue that happened; nothing else is.</summary>
public static class BatchExit
{
    public const int Ok = 0;

    /// <summary>A real answer about the world: refused, or already queued.</summary>
    public const int Refused = 1;

    /// <summary>Nothing was decided — a missing flag, an unreadable document.</summary>
    public const int Unusable = 2;

    /// <summary>
    /// 🔴 <b>The queue was empty, so the batch examined nothing.</b> Its own exit code because "planned a
    /// batch of zero lanes" would otherwise be reported with the same 0 as a real plan, and every number
    /// in the report would read as valid.
    /// </summary>
    public const int NothingBatched = 3;
}

/// <summary>
/// Every decision in <c>harness-batch</c>, so none of them needs a process to test. The composition root
/// in <c>Program.cs</c> decides nothing.
/// </summary>
public static class BatchCli
{
    /// <summary>
    /// The converter <c>run</c> uses when the caller names none. <b>One constant, because the served-area
    /// derivation and the run plan's own commands must not be able to name two different binaries</b> —
    /// a batch that derived its width with one converter and drift-checked with another would report two
    /// facts about two programs under one heading.
    /// </summary>
    private const string DefaultConverter = "converter";

    private const string Usage =
        "Usage: harness-batch enqueue --queue <dir> --lane <name> --binding <file> --submission <file>\n"
        + "                             (--manifest <file> | --program <path>...) [--purpose <text>]\n"
        + "                             # --manifest DERIVES the program set from what the lane emitted; --program is DECLARED by you\n"
        + "       harness-batch manifest --lane <name> --binding <file> --submission <file> --program <path>...\n"
        + "                             [--block-under-test <Name>] --emit <dir> --out <manifest.json>\n"
        + "       harness-batch manifest --lane <name> --binding <file> --submission <file> --check <manifest.json>\n"
        + "                             # WRITES the lane manifest from what the generator produced, so `enqueue --manifest`\n"
        + "                             # can DERIVE the program set instead of you typing it. Refuses to emit a manifest\n"
        + "                             # that names a different program from the one the build stamp hashed.\n"
        + "                             # --check re-derives the stamp over an EXISTING manifest's own paths and compares\n"
        + "       harness-batch plan    --queue <dir> [--out <merged-binding.json>] [--converter <exe>]\n"
        + "                             [--neighbours derive|declared-only]\n"
        + "                             # --converter DERIVES the served register width from the MB_SERVER call that serves it\n"
        + "                             # and refuses a binding that disagrees; without it the width is DECLARED and unchecked\n"
        + "                             # --neighbours derive DERIVES every %M occupant of that area from the corpus and refuses\n"
        + "                             # a mirror that would enter one. `declared-only` is the ONE named escape, and IT IS COUNTED\n"
        + "       harness-batch list    --queue <dir>\n"
        + "       harness-batch dequeue --queue <dir> --lane <name>\n"
        + "       harness-batch run     --queue <dir> --merged <file> --staging <dir> --leases <dir> --holder <id> --holder-pid <n>\n"
        + "                             --portal-project <path> --portal-evidence <file> --rig <address> [--port <n>] [--unit <n>]\n"
        + "                             [--converter <exe>] [--harness-run <exe>] [--allowlist <file>] [--ttl <minutes>]\n"
        + "                             [--deploy-config <file.json>] --neighbours derive|declared-only --yes\n"
        + "                             # WITHOUT --yes: prints every command, contacts NOTHING\n"
        + "                             # --converter DEFAULTS to `converter` here and the served width DERIVES on this path\n"
        + "                             # --neighbours is REQUIRED with --yes and has NO DEFAULT: the path that reaches a\n"
        + "                             # controller does not get to decide this by leaving a flag off";

    public static int Run(
        string[] args, TextWriter output, Func<string, string> readFile, Action<string, string> writeFile,
        IProcessRunner? runner = null, Func<IReadOnlyList<string>, DeploymentOutcome>? deploy = null,

        // Only `manifest` needs it: a provenance record stamped over BYTES can only be re-hashed over
        // bytes, and LoopCli.Compose takes the reader rather than opening files itself. Optional, and
        // absent leaves those gate inputs NOT CHECKED — which does not move the build stamp, the only
        // thing this verb derives.
        Func<string, byte[]>? readBytes = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);

        if (args.Length == 0)
        {
            output.WriteLine(Usage);
            return BatchExit.Unusable;
        }

        var verb = args[0];
        if (verb is not ("enqueue" or "plan" or "list" or "dequeue" or "run" or "manifest"))
        {
            output.WriteLine($"unknown sub-command '{verb}' — expected one of: enqueue, manifest, plan, list, dequeue, run");
            output.WriteLine(Usage);
            return BatchExit.Unusable;
        }

        string? queue = null, lane = null, binding = null, submission = null, outPath = null, purpose = null;
        string? merged = null, staging = null, leases = null, holder = null, portalProject = null;
        string? portalEvidence = null, rig = null, converterExe = null, harnessRunExe = null, allowlist = null;
        string? opennessCliExe = null, manifest = null, neighboursMode = null;
        string? mirrorReadExe = null, committedCorpus = null;
        string? blockUnderTest = null, emitDir = null, checkPath = null;
        int holderPid = 0, rigPort = 503, rigUnit = 1, ttlMinutes = 60;
        var settleSeconds = -1;   // -1 = not stated, use the default
        string? attestation = null;
        var confirmed = false;
        var programs = new List<string>();

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--queue": queue = Next(args, ref i); break;
                case "--lane": lane = Next(args, ref i); break;
                case "--binding": binding = Next(args, ref i); break;
                case "--submission": submission = Next(args, ref i); break;
                case "--out": outPath = Next(args, ref i); break;
                case "--purpose": purpose = Next(args, ref i); break;
                // The lane's own emitted object list. Where present the program set is DERIVED from it
                // rather than typed, which is what stops the build stamp describing a program nobody
                // deployed. See LaneManifest.
                case "--manifest": manifest = Next(args, ref i); break;

                // 🔴 `manifest` only. The SUBJECT, by name — the one object every per-block check has to be
                // pointed at, and the one docs/18-project-workbench.md §5 "Phase 10 — Wave time" records as
                // absent from the stamp: search the phrase "appears only as its instance DB".
                //
                // The citation here read `docs/18:1166-1167` and was WRONG IN THE COMMIT THAT WROTE IT —
                // those lines are the compression-ceiling paragraph, nothing to do with the subject. Third
                // failure of the same class in two days, hence: re-point by SYMBOL AND SECTION, never a
                // bare line number.
                //
                // It must be a Block. `LaneManifest.Derive` refuses an instance DB, which this accepted.
                case "--block-under-test": blockUnderTest = Next(args, ref i); break;
                case "--emit": emitDir = Next(args, ref i); break;
                case "--check": checkPath = Next(args, ref i); break;
                case "--program":
                    // Multi-valued, the same shape harness-run's --program uses: consume until the next flag.
                    while (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                        programs.Add(args[++i]);
                    break;
                case "--merged": merged = Next(args, ref i); break;
                case "--staging": staging = Next(args, ref i); break;
                case "--leases": leases = Next(args, ref i); break;
                case "--holder": holder = Next(args, ref i); break;
                case "--portal-project": portalProject = Next(args, ref i); break;
                case "--portal-evidence": portalEvidence = Next(args, ref i); break;
                case "--rig": rig = Next(args, ref i); break;
                case "--converter": converterExe = Next(args, ref i); break;
                case "--neighbours": neighboursMode = Next(args, ref i); break;
                case "--harness-run": harnessRunExe = Next(args, ref i); break;
                case "--openness-cli": opennessCliExe = Next(args, ref i); break;
                case "--mirror-read": mirrorReadExe = Next(args, ref i); break;

                // The COMMITTED corpus, for the whole-project third leg — deliberately not the lane
                // union, which is a subset and would report every unsupplied object as export-only.
                // Absent on a live job, where no committed corpus exists; the plan says so rather than
                // letting the step's absence read as a pass.
                case "--corpus": committedCorpus = Next(args, ref i); break;
                case "--allowlist": allowlist = Next(args, ref i); break;
                // Read by Program.cs, which builds the gateway and generates the copy layer in-process.
                // They still have to be ACCEPTED here or the parser's unknown-argument arm rejects them —
                // which is exactly what happened on the first real rig run, at exit 2 before any gate was
                // taken. Listed together so the next one added does not repeat it.
                case "--deploy-config":
                case "--deploy-submission": _ = Next(args, ref i); break;
                case "--attest-portal-unjudgeable": attestation = Next(args, ref i); break;
                case "--yes": confirmed = true; break;
                case "--settle-seconds": if (!Number(args, ref i, "--settle-seconds", output, out settleSeconds, allowZero: true)) return BatchExit.Unusable; break;
                case "--holder-pid": if (!Number(args, ref i, "--holder-pid", output, out holderPid)) return BatchExit.Unusable; break;
                case "--port": if (!Number(args, ref i, "--port", output, out rigPort)) return BatchExit.Unusable; break;
                case "--unit": if (!Number(args, ref i, "--unit", output, out rigUnit)) return BatchExit.Unusable; break;
                case "--ttl": if (!Number(args, ref i, "--ttl", output, out ttlMinutes)) return BatchExit.Unusable; break;
                default:
                    output.WriteLine($"Unexpected argument: {args[i]}");
                    return BatchExit.Unusable;
            }
        }

        // 🔴 BEFORE the --queue requirement, because `manifest` touches no queue. It runs at BUILD time —
        // the lane does not exist yet, and demanding the shared root here would make the producer harder
        // to reach than the hand-typed list it replaces.
        if (verb == "manifest")
        {
            return Manifest(
                output, readFile, readBytes, writeFile,
                new ManifestArgs(lane, binding, submission, programs, blockUnderTest, emitDir, outPath, checkPath));
        }

        if (string.IsNullOrWhiteSpace(queue))
        {
            // No default, and the reason is the one --claims and --leases already carry: agents work in
            // separate worktrees, so a per-worktree queue is always empty, accepts every lane, and
            // produces a "batch" of one that looks exactly like success.
            output.WriteLine("--queue <dir> is required. It must be a directory SHARED by every agent on this machine — a per-worktree "
                + "queue would accept every lane and batch nothing with anything. The shared root is C:\\ProgramData\\Ladder-AI\\batch.");
            return BatchExit.Unusable;
        }

        // 🔴 A PURE ARGUMENT CHECK, TAKEN BEFORE ANYTHING COSTS ANYTHING — and `derive` without a
        // converter is REFUSED rather than quietly downgraded, because the quiet downgrade IS the
        // fallback shape this guard exists to avoid.
        NeighbourMode neighbours;
        switch (neighboursMode)
        {
            case null:
                neighbours = NeighbourMode.NotAsked;
                break;

            case "derive":
                // 🔴 ON `run`, THE CONVERTER DEFAULTS, SO `derive` NEEDS NO SECOND FLAG — and that is not a
                // convenience, it is the fix for a nudge this pair of changes would otherwise create.
                // `run --yes` now REQUIRES a --neighbours decision; if `derive` then cost an EXTRA flag
                // that `declared-only` does not, THE ESCAPE WOULD BE STRICTLY CHEAPER TO TYPE THAN THE
                // GUARD, which is the last shape you want on the choice a counter exists to watch.
                //
                // It is NOT the downgrade this check was written against. A defaulted `converter` that is
                // not on PATH still fails CLOSED: NeighbourProbe reports "the converter did not start",
                // that is a NotDerived, and NotDerived GATES the batch. What stays refused is the case
                // where nothing can supply an exe at all — `plan`, which deliberately does not default one
                // (see Plan() below) and whose guard is
                // NeighbourDerivationTests.Neighbours_derive_without_a_converter_is_refused_rather_than_downgraded.
                if (string.IsNullOrWhiteSpace(converterExe) && verb != "run")
                {
                    output.WriteLine("--neighbours derive needs --converter <exe>: the %M occupants of the served area are derived by "
                        + "`converter neighbours`, and there is no second way to obtain them. REFUSED rather than downgraded to "
                        + "declared-only — a derivation that silently becomes a weaker check is the exact shape this guard exists to "
                        + "prevent. Pass --converter, or say `--neighbours declared-only`, which is counted. (`run` defaults the "
                        + "converter and needs no flag; `plan` starts no process unless you name one.)");
                    return BatchExit.Unusable;
                }

                neighbours = NeighbourMode.Derive;
                break;

            case "declared-only":
                neighbours = NeighbourMode.DeclaredOnly;
                break;

            default:
                output.WriteLine($"--neighbours '{neighboursMode}' is not a mode. It takes `derive` (read the corpus and refuse a mirror "
                    + "that would enter somebody else's registers) or `declared-only` (the ONE named escape, which is counted). An "
                    + "unrecognised value is refused rather than treated as the safe default: the safe default here is the one that "
                    + "costs a subprocess, and guessing it for you would be a subprocess nobody asked for.");
                return BatchExit.Unusable;
        }

        var store = new LaneQueue(queue);

        // Echoed on every act. Two agents passing two different roots fork the queue, and no process can
        // detect that from inside — each queue is well-formed and legitimately holds what it holds.
        output.WriteLine($"queue={store.Root}");

        return verb switch
        {
            "enqueue" => Enqueue(store, output, readFile, lane, binding, submission, programs, purpose, manifest),
            "plan" => Plan(store, output, readFile, writeFile, outPath, converterExe, runner, neighbours),
            "list" => List(store, output),
            "dequeue" => Dequeue(store, output, lane),
            _ => Run(store, output, readFile, runner, deploy, new RunArgs(
                merged, staging, leases, holder, holderPid, portalProject, portalEvidence,
                rig, rigPort, rigUnit, converterExe, harnessRunExe, allowlist, ttlMinutes, confirmed, attestation,
                settleSeconds, opennessCliExe, neighbours, mirrorReadExe, committedCorpus)),
        };
    }

    private sealed record RunArgs(
        string? Merged, string? Staging, string? Leases, string? Holder, int HolderPid,
        string? PortalProject, string? PortalEvidence, string? Rig, int RigPort, int RigUnit,
        string? ConverterExe, string? HarnessRunExe, string? Allowlist, int TtlMinutes, bool Confirmed,
        string? PortalAttestation, int SettleSeconds, string? OpennessCliExe, NeighbourMode Neighbours,
        string? MirrorReadExe, string? CommittedCorpus);

    /// <summary>
    /// 🔴 <b>WHO ELSE IS IN THE AREA — derived, declined by the one named escape, or never asked.</b>
    ///
    /// <para>The area comes from <see cref="ServedAreaFact"/> and never from the binding: two
    /// derivations describing two different windows would let this clear registers the mirror does not
    /// live in, which is a closed check wearing a green.</para>
    ///
    /// <para><b>Every failure to obtain it is a <see cref="NeighbourState.NotDerived"/>, which gates in
    /// the planner.</b> That is the one place this deliberately differs from <see cref="UnionPreflight"/>
    /// and <see cref="ServedAreaProbe"/>: they report what they could not consult, because their findings
    /// are reports. This one is an input to a refusal.</para>
    /// </summary>
    private static NeighbourFact Neighbours(
        NeighbourMode mode, LaneQueue store, TextWriter output, string? converterExe,
        IReadOnlyList<Lane> lanes, ServedAreaFact served, IProcessRunner? runner, string verb)
    {
        switch (mode)
        {
            case NeighbourMode.DeclaredOnly:
                // Recorded BEFORE the plan, so a batch that then refuses for an unrelated reason still
                // counted the decline. The tally is about how often the guard is switched off, not about
                // how often doing so was followed by a successful deployment.
                output.WriteLine("  " + NeighbourEscapeLog.Record(store.Root, verb).Line);
                return NeighbourFact.DeclaredOnly;

            case NeighbourMode.Derive:
                if (runner is null)
                {
                    return NeighbourFact.NotDerivedBecause(
                        "this build has no process runner wired in, so `converter neighbours` could not be consulted at all.");
                }

                if (!served.Derived)
                {
                    return NeighbourFact.NotDerivedBecause(
                        "the served AREA itself was not derived, so there is no window to look for occupants inside — and asking "
                        + "about an AUTHORED area would check registers the mirror may not even live in. " + served.Denominator);
                }

                return NeighbourProbe.Derive(
                    converterExe!,
                    lanes.SelectMany(l => l.ProgramPaths).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                    served.BaseByte,
                    served.Registers,
                    runner);

            default:
                return NeighbourFact.NotAsked;
        }
    }

    /// <summary>
    /// 🔴 <b>ON THE PATH THAT REACHES A CONTROLLER, THE NEIGHBOUR DECISION IS MADE — NOT DEFAULTED, AND
    /// NOT DECIDABLE BY OMISSION.</b>
    ///
    /// <para><b>A blanket default was investigated and rejected on evidence, and the reasoning is kept
    /// here so nobody re-proposes it.</b> <see cref="NeighbourFact.Gates"/> is deliberately a REFUSAL
    /// INPUT rather than a report — the break from <see cref="UnionPreflight"/>'s weaker-fallback shape —
    /// so defaulting <c>derive</c> on would turn two ORDINARY, non-error situations into batch refusals:
    /// a corpus with no <c>MB_SERVER</c> call (every batch whose lanes do not stage the comms block), and
    /// one unparseable file anywhere in the union (the producer emits no list at all, by design).
    /// Defaulting <c>declared-only</c> on is worse still: it switches the guard off silently and the
    /// escape counter — the one thing that would ever surface it — would tick on every run and become
    /// unreadable.</para>
    ///
    /// <para><b>Requiring the choice costs neither.</b> No plan that succeeds today begins refusing; the
    /// deployment path simply stops being decidable by leaving a flag off. This is
    /// <c>feedback_a_warning_is_not_a_gate</c> applied where it belongs — a check that only warns gets
    /// skimmed, so fail closed on the path that reaches production.</para>
    ///
    /// <para>🔴 <b>THE RUNNING ESCAPE TOTAL IS IN THE REFUSAL TEXT, and that is not decoration.</b> Making
    /// the choice mandatory makes <c>declared-only</c> the routine opt-out, which is exactly the "escape
    /// becomes routine" failure <see cref="NeighbourEscapeLog"/> exists to surface. A counter nobody is
    /// shown at the moment of choosing is a counter that never gets read.</para>
    /// </summary>
    /// <remarks>
    /// <b>Scoped to <c>run --yes</c>, and to a run that could otherwise happen.</b>
    /// <list type="bullet">
    /// <item><c>plan</c> and dry runs keep today's behaviour exactly — neither contacts a controller, and
    /// <c>NeighbourDerivationTests.Naming_no_mode_at_all_still_says_NOT_DERIVED…</c> is a guard on the
    /// <c>plan</c> half of that.</item>
    /// <item><b>A missing deploy gateway wins the tie.</b> "<c>--yes</c> with nothing to deploy with
    /// starts NOTHING" is the older, stronger promise with its own test, and a run that cannot happen
    /// should not be made to answer a question about a run that cannot happen.</item>
    /// <item><b>An empty queue wins it too.</b> Exit 3 says the batch examined nothing, which is the more
    /// basic fact; asking for a decision about zero lanes would bury it.</item>
    /// </list>
    /// </remarks>
    private static bool RequiresANeighbourDecision(
        LaneQueue store, TextWriter output, Func<IReadOnlyList<string>, DeploymentOutcome>? deploy,
        RunArgs args, IReadOnlyList<Lane> lanes)
    {
        if (!args.Confirmed || args.Neighbours != NeighbourMode.NotAsked || deploy is null || lanes.Count == 0)
            return false;

        var escapes = NeighbourEscapeLog.Count(store.Root);

        output.WriteLine("REFUSED  `run --yes` needs an explicit --neighbours decision. There is no default, because both");
        output.WriteLine("         possible defaults are wrong here and the choice belongs to whoever is deploying.");
        output.WriteLine();
        output.WriteLine("           --neighbours derive          DERIVES every %M occupant of the served area from the program");
        output.WriteLine("                                        corpus and REFUSES a mirror that would enter one. Costs one");
        output.WriteLine("                                        converter subprocess, and it GATES: an unparseable file in the");
        output.WriteLine("                                        union, or a corpus with no MB_SERVER call, stops the batch");
        output.WriteLine("                                        rather than passing quietly. Needs --converter <exe>.");
        output.WriteLine();
        output.WriteLine("           --neighbours declared-only   deploys on whatever the bindings DECLARED. Nothing derives the");
        output.WriteLine("                                        occupants, so an UNDECLARED one is invisible to this run — the");
        output.WriteLine("                                        exact shape that put a generated mirror and a hand-authored");
        output.WriteLine("                                        panel in the same 53 registers. It is THE ONE NAMED ESCAPE AND");
        output.WriteLine($"                                        IT IS COUNTED: taken {escapes} time(s) against this queue already.");
        output.WriteLine();
        output.WriteLine("         That count is the point. If `declared-only` becomes the routine answer the guard is back to");
        output.WriteLine("         declared-only and nobody notices — so it is printed where the choice is made, not in a log");
        output.WriteLine("         somebody would have to go and read.");
        output.WriteLine();
        output.WriteLine("         NO GATE WAS TAKEN, NOTHING WAS WRITTEN and PORTAL WAS NOT CONTACTED.");

        return true;
    }

    /// <summary>
    /// 🔴 <b><c>--yes</c> is required, and without it Portal is NEVER CONTACTED.</b>
    ///
    /// <para>The same shape <c>download-probe</c>, <c>block-layout --set</c> and
    /// <c>hmi-create-screen</c> already use, and for a stronger reason than any of them: this takes two
    /// gates, writes a program into a project and downloads it to a controller. The dry run prints every
    /// command in order, which is worth having on its own — the deploy is otherwise about seven
    /// hand-assembled Portal-touching steps.</para>
    /// </summary>
    private static int Run(
        LaneQueue store, TextWriter output, Func<string, string> readFile,
        IProcessRunner? runner, Func<IReadOnlyList<string>, DeploymentOutcome>? deploy, RunArgs args)
    {
        var lanes = store.All();

        if (RequiresANeighbourDecision(store, output, deploy, args, lanes))
            return BatchExit.Unusable;

        // 🔴 THE SERVED WIDTH, DERIVED ON THE DEFAULT DEPLOYMENT PATH — and every precondition left on it
        // is one of this command's existing contracts rather than a new caution.
        //
        //   --yes            the dry run's contract is that it starts NO PROCESS AT ALL. Same trade the
        //                    reachability parity check already makes: eroding a clean invariant to gain a
        //                    convenience is how invariants stop being checkable. The dry run SAYS the
        //                    width was declared rather than implying it was checked.
        //   a deploy gateway "--yes with nothing to deploy with starts NOTHING" is a stated promise with
        //                    its own test, and the refusal for it sits below the planner. Deriving here
        //                    unconditionally would start a process before that refusal.
        //
        // 🔴 --converter IS NOW DEFAULTED, 2026-08-23, and this is the flip the previous version of this
        // comment named and priced: "the DEFAULT deployment path does not derive. Flipping that is one
        // line here plus the subprocess count in BatchCliRunTests." Both were paid. The old reasoning was
        // that a run which did not name a converter should not acquire a subprocess nobody asked for —
        // true of a check that was NEW, and no longer true of one that has run on the rig. A guard whose
        // default is off is a guard that exists rather than one that runs, and the path this default
        // governs is the one that reaches a controller. `run` already defaults every other binary it
        // invokes (ConverterExe/HarnessRunExe/OpennessCliExe, below), so this stops being the odd one out.
        //
        // ⚠️ `plan` IS DELIBERATELY NOT FLIPPED WITH IT. `plan` starts no process at all today, and
        // acquiring that by default would surprise every existing caller for no deployment safety —
        // nothing downloads off a plan. ServedAreaGateTests.Plan_without_a_converter_says_the_width_was
        // _declared_and_starts_no_process is the guard on that asymmetry; see Plan() below.
        //
        // The corpus is the lanes' own program paths, which need no staging directory — so the check
        // lands before anything is materialised, imported or downloaded.
        var served = args.Confirmed && runner is not null && deploy is not null
            ? ServedAreaProbe.Derive(
                args.ConverterExe ?? DefaultConverter,
                lanes.SelectMany(l => l.ProgramPaths).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                runner)
            : ServedAreaFact.NotDerived(
                (args.Confirmed ? string.Empty : "a dry run starts NO process, and ")
                + "the program corpus was not read. The width is DECLARED, not derived: pass --yes to "
                + "check it against the MB_SERVER call that serves it.");

        // 🔴 THE DRY RUN CANNOT DERIVE, AND IT SAYS SO AS A USAGE ERROR RATHER THAN DERIVING NOTHING.
        // The dry run's contract is that it starts NO process at all — a promise with its own test — so
        // `--neighbours derive` under it is a contradiction. Refused by name, because the alternative
        // (silently behaving as declared-only) is the downgrade this whole item exists to prevent, and it
        // would be invisible in the report.
        if (args.Neighbours == NeighbourMode.Derive && !args.Confirmed)
        {
            output.WriteLine("REFUSED  --neighbours derive needs --yes. A dry run starts NO PROCESS AT ALL, so it cannot consult "
                + "`converter neighbours`, and quietly proceeding as though it had is the silent downgrade this check exists to "
                + "prevent. Drop the flag, or say `--neighbours declared-only` — which is counted. NOTHING WAS RUN.");
            return BatchExit.Unusable;
        }

        // The SAME defaulted exe the width was derived with — never a second resolution. Two derivations
        // about one area reached through two different binaries would be two facts under one heading.
        var neighbours = Neighbours(
            args.Neighbours, store, output, args.ConverterExe ?? DefaultConverter, lanes, served, runner, "run");

        var batch = BatchPlanner.Plan(lanes, readFile, served, neighbours);

        if (!batch.Planned)
        {
            output.Write(BatchPlanner.Describe(batch));
            output.WriteLine("  NOTHING WAS RUN: a batch that could not be planned is not a batch that can be deployed.");
            return lanes.Count == 0 ? BatchExit.NothingBatched : BatchExit.Refused;
        }

        // 🔴 DEFAULTS TO THIS PROCESS, AND THE CONVERTER'S OPPOSITE RULE DOES NOT APPLY HERE.
        //
        // `converter lease acquire` REFUSES to default --pid to itself, because a converter invocation
        // exits the moment it returns: the lease would be held by a dead process from birth and every
        // reclaim decision would fall back to the TTL alone.
        //
        // `harness-batch run` is the other case. It SPANS the whole lease - it takes the gates, deploys,
        // runs every wave and releases - so it is exactly "the process that holds the gate for the
        // lease's lifetime", which is what that flag asks for. Requiring an operator to supply one
        // instead produced the failure this comment came from: a pid copied from an earlier shell that
        // had since exited, refused as not running, on the real rig.
        //
        // Still overridable, for a wrapper that genuinely outlives this process.
        var holderPid = args.HolderPid > 0 ? args.HolderPid : Environment.ProcessId;

        var options = new BatchRunOptions(
            ConverterExe: args.ConverterExe ?? DefaultConverter,
            HarnessRunExe: args.HarnessRunExe ?? "harness-run",
            OpennessCliExe: args.OpennessCliExe ?? "openness-cli",
            LeasesDirectory: args.Leases ?? string.Empty,
            PortalProject: args.PortalProject ?? string.Empty,
            RigAddress: args.Rig ?? string.Empty,
            Holder: args.Holder ?? string.Empty,
            HolderPid: holderPid,
            StagingDirectory: args.Staging ?? string.Empty,
            MergedBindingPath: args.Merged ?? string.Empty,
            PortalEvidencePath: args.PortalEvidence ?? string.Empty,
            RigPort: args.RigPort,
            RigUnit: args.RigUnit,
            DeviceAllowlistPath: args.Allowlist,
            LeaseTtlMinutes: args.TtlMinutes,
            PortalAttestation: args.PortalAttestation,

            // Defaulted like the other three invokes. Without a binary the width step is ABSENT, and
            // its absence is a Notice rather than a silence - the derived width simply goes unchecked
            // against the device, which is where every batch before this one already stood.
            MirrorReadExe: args.MirrorReadExe ?? "harness-mirror-read",
            CommittedCorpusDirectory: args.CommittedCorpus);

        var unionIr = MaterialiseUnionIr(batch.ProgramPaths, args.Staging, output);

        // 🔴 *** WHAT THE BUILD STAMP WILL BE MEASURED AGAINST, ON EVERY RUN INCLUDING THE ONE WITH NO
        // ANSWER. *** The union above is what gets HASHED; this is what SHOULD have been. Until they were
        // both stated, a short program set produced a stamp indistinguishable from a complete one —
        // measured in docs/18-project-workbench.md §5 "Phase 10 — Wave time", under "THE BUILD STAMP DOES
        // COVER THE PARAMETER DB", where the parameter DB was staged, unhashed, and "compressing them
        // changes the controller without changing the stamp". (That entry's original eight-object figure
        // was retracted 2026-08-24; the deployed set was nine.)
        //
        // Derived by the SAME call the plan uses, so the line and the command lines cannot disagree.
        output.WriteLine("  corpus    " + LaneCorpus.Of(lanes).Detail);

        // 🔴 REFUSED BEFORE ANY PROCESS STARTS, because it is a pure argument check and everything below
        // costs something. It used to sit after planning; moving it up is what keeps "--yes with nothing
        // to deploy with starts NOTHING" true now that a check runs down there.
        if (args.Confirmed && deploy is null)
        {
            output.WriteLine("REFUSED  --yes needs --deploy-config <file.json>, which supplies the Portal project, the group path, the "
                + "binaries and the download target (DeviceGatewayOptions). Without it there is nothing to deploy with, and taking the "
                + "gates first would lock another agent out of a run that cannot happen. NO GATE WAS TAKEN.");
            return BatchExit.Unusable;
        }

        // 🔴 WHERE THE MIRROR'S WIDTH CAME FROM, ON EVERY RUN. `BatchPlanner.Describe` carries this line
        // too, but `run` only prints that report when the plan FAILED — so on the path that actually
        // deploys, the provenance of the number the map was allocated against would otherwise appear
        // nowhere at all. That is precisely the state this item exists to end.
        if (batch.ServedArea is { } servedArea)
        {
            output.WriteLine("  width     " + servedArea.Denominator);

            if (servedArea.Derived)
            {
                output.WriteLine("            ^ read from the program CORPUS, not the controller. It cannot see whether the block");
                output.WriteLine("              it read is the block running on the CPU — agreement with the STAGED program is the");
                output.WriteLine("              whole of the claim, and the 1024-register widening was proven by probing the device.");
            }
        }

        // 🔴 WHO ELSE IS IN THE AREA, ON THE PATH THAT ACTUALLY DEPLOYS. `BatchPlanner.Describe` carries
        // this line too, and `run` only prints that report when the plan FAILED — so on a successful
        // deploy the provenance of the neighbour list would otherwise appear nowhere at all.
        if (batch.NeighbourReconciliation is { } area)
        {
            output.WriteLine("  area      " + area.Denominator);

            foreach (var report in area.Reports)
                output.WriteLine("            REPORTED  " + report);
        }

        // 🔴 *** THE PARITY CHECK, RUN ON THE CORPUS ACTUALLY IN FRONT OF IT. ***
        //
        // Reachability is derived TWICE — once here by a text walk, once by the converter's real parser —
        // and it must be, because the harness is deliberately dependency-free and referencing the
        // converter would give away a property this project paid for. So the two are compared instead,
        // over real input, every time a batch actually runs. A disagreement REFUSES: when two derivations
        // differ at least one is wrong and neither knows which, and continuing would pick a winner by
        // accident of code path. Being unable to consult the second one is reported, never refused.
        //
        // 🔴 UNDER --yes ONLY, AND THAT IS A DELIBERATE TRADE. It would be useful in a dry run, but the
        // dry run's contract is that it starts NO PROCESS AT ALL — a promise with its own test and its own
        // reason — and eroding a clean invariant to gain a convenience is how invariants stop being
        // checkable. The dry run says the check was not performed instead of implying it passed.
        if (!args.Confirmed)
        {
            output.WriteLine("  parity    NOT PERFORMED in a dry run — it would start a converter process, and a dry run starts none.");
        }
        else if (unionIr is not null && runner is not null)
        {
            // 🔴 THE UNION CHECK, RUN — over the UNION, before the lease, and no longer printed for
            // somebody to type. `harness-batch plan` used to emit one `cross-check` PER LANE PATH, which
            // is the per-lane filter and not the gate, directly under a sentence correctly arguing that
            // only a union check sees a cross-lane conflict. ONE subprocess serves both this and the
            // reachability parity below.
            var preflight = UnionPreflight.Run(options.ConverterExe, unionIr, runner);
            output.WriteLine("  " + preflight.Summary);

            foreach (var finding in preflight.Findings)
                output.WriteLine($"            {finding.Kind}: {finding.Detail}");

            if (preflight.Findings.Count > 0)
            {
                // Report-only, and the reason is stated where a reader meets the findings rather than
                // buried in a design note: cross-check emits FACTS, and a refusal set chosen without
                // evidence gets the gate switched off — after which it still appears in the list.
                output.WriteLine("            ^ FACTS, NOT VERDICTS — reported, not gating. Read them before deploying.");
            }

            if (batch.Reachability is { } mine)
            {
                // No re-run when the pre-flight could not read the converter: the second call would be
                // the identical subprocess over the identical corpus and would fail the identical way.
                // Reporting the pre-flight's own reason is both cheaper and more honest than a second
                // NotCompared with less information in it.
                var parity = preflight.CrossCheckJson is { } json
                    ? ReachabilityParity.CheckAgainst(mine, json)
                    : new ReachabilityParityResult(ParityOutcome.NotCompared,
                        "the union pre-flight could not consult the converter, so there is only one derivation "
                        + "to go on and the text walk stands alone: " + preflight.Detail);

                output.WriteLine($"  parity    {parity.Outcome}: {parity.Detail}");

                if (parity.Outcome == ParityOutcome.Disagreed)
                {
                    output.WriteLine();
                    output.WriteLine("REFUSED, and NO GATE WAS TAKEN.");
                    return BatchExit.Unusable;
                }
            }
        }

        var plan = BatchRunPlan.For(batch, lanes, options with
        {
            UnionIrDirectory = unionIr,
            ProjectExportDirectory = string.IsNullOrWhiteSpace(args.Staging) ? null : Path.Combine(args.Staging!, "project-xml"),

            // 🔴 A DIFFERENT DIRECTORY FROM project-xml, AND THE PLANNER REFUSES IF THEY ARE THE SAME.
            // The union export deliberately omits --tagtables and answers the PAIRED question; the
            // whole-corpus export passes --tagtables and `--complete` declares its directory to BE the
            // whole project. One directory serving both would make each answer a question about the
            // other's contents.
            WholeCorpusExportDirectory = string.IsNullOrWhiteSpace(args.Staging) ? null : Path.Combine(args.Staging!, "whole-project-xml"),
        });

        output.WriteLine($"lanes {lanes.Count}: {string.Join(", ", batch.LanesBatched)}");
        output.WriteLine();

        foreach (var step in plan.Steps)
            output.WriteLine($"  [{step.Kind}]{(step.Lane is null ? "" : " " + step.Lane)}  {step.CommandLineText}");

        foreach (var refusal in plan.Refusals)
            output.WriteLine($"  REFUSED  {refusal}");

        // 🔴 A STEP THAT IS ABSENT MUST SAY SO. Without this loop the width comparison and the
        // whole-corpus third leg simply do not appear in the plan, and a reader cannot tell a check
        // that ran clean from one that was never emitted - which is the exact indistinguishability
        // both steps exist to remove one level down.
        foreach (var notice in plan.Notices)
            output.WriteLine($"  NOT CHECKED  {notice}");

        output.WriteLine();

        if (!plan.Planned)
            return BatchExit.Unusable;

        if (!args.Confirmed)
        {
            // Portal has not been contacted, no lease has been taken, and nothing has been written. Said
            // explicitly rather than left to be inferred from the absence of output.
            output.WriteLine("DRY RUN — --yes was not passed, so NO GATE WAS TAKEN, NOTHING WAS WRITTEN and PORTAL WAS NOT CONTACTED.");
            output.WriteLine("The commands above are the ones that would run, in that order.");
            return BatchExit.Ok;
        }

        if (runner is null)
        {
            output.WriteLine("REFUSED  --yes was passed but this build has no process runner wired in, so nothing could be executed. "
                + "Nothing was written and no gate was taken.");
            return BatchExit.Unusable;
        }

        // The program union goes to the deployment, so the stamp it writes to the device is computed
        // over the SAME objects every lane's wave will compute over. Passing none - which this did -
        // stamps the device with a value no wave can reproduce, and the wave then refuses with a
        // version mismatch that reads as a failed download. Measured on the rig: device 16#CBE1D692,
        // staged 16#679E7923, and the download had in fact succeeded.
        var result = BatchRunner.Execute(plan, runner, () => deploy!(batch.ProgramPaths),
            // 🔴 THE BLIND WAIT NOW DEFAULTS OFF. The wave retries its own inert phase instead, which
            // MEASURES readiness rather than guessing at it — and the fixed 15 s was measured to be too
            // short anyway, so keeping it as the default would be paying for a wait that does not work.
            // --settle-seconds survives for a caller who wants a wait as well.
            settleAfterDownload: TimeSpan.FromSeconds(args.SettleSeconds >= 0 ? args.SettleSeconds : 0),

            // So the settling MEASUREMENT reaches the headline. The lanes share one download, so their
            // samples are repeated observations of one transient — the number nobody has ever read back.
            readFile: readFile);

        output.WriteLine(result.Headline);
        output.WriteLine();

        foreach (var step in result.Steps)
            output.WriteLine($"  {(step.Ok ? "ok    " : step.Verdict.ToString().ToUpperInvariant())}  [{step.Step.Kind}]  {step.Reason}");

        return result.Outcome == BatchRunOutcome.Ran ? BatchExit.Ok : BatchExit.Refused;
    }

    /// <param name="allowZero">
    /// Zero is meaningful for <c>--settle-seconds</c> — it disables the wait — and meaningless for a
    /// port or a TTL, so it is opted into rather than allowed everywhere.
    /// </param>
    /// <summary>
    /// Copy the lanes' program IR into one directory so <c>drift-check</c> has a <c>--project</c> to
    /// point at. Basename collisions were already refused at plan time, so a clash here would be a bug.
    ///
    /// <para>Returns null when there is nowhere to put it — and the plan then omits the drift steps and
    /// says so, rather than proceeding as though the comparison had passed.</para>
    /// </summary>
    private static string? MaterialiseUnionIr(IReadOnlyList<string> programPaths, string? staging, TextWriter output)
    {
        // 🔴 *** SAID, NOT SKIPPED. *** Without a staging directory there is nowhere to put the union, so
        // the drift check cannot run — and until now that produced NO LINE AT ALL. The steps were simply
        // absent from the plan, while the comment at their construction site claimed "the run SAYS so
        // rather than passing quietly". It did not. The most important gate this batch has could be
        // dropped by omitting one flag, and the report read exactly like a run that had passed it.
        if (string.IsNullOrWhiteSpace(staging))
        {
            output.WriteLine("  union     NOT STAGED — no --staging, so THE SUPPLIED PROGRAM WAS NOT COMPARED AGAINST THE PROJECT.");
            output.WriteLine("            The build stamp still claims the supplied program is what executes; nothing here checked it.");
            return null;
        }

        var union = Path.Combine(staging, "union-ir");

        try
        {
            if (Directory.Exists(union))
                Directory.Delete(union, recursive: true);

            Directory.CreateDirectory(union);

            // One enumeration, shared with the planner — see ProgramFiles. A basename collision between
            // two lanes has already been refused at plan time (BatchPlanner's union-corpus check), so the
            // overwrite:false below is a backstop against a case that should be unreachable, not the
            // place that finding is made.
            var contributions = ProgramFiles.Resolve(programPaths);
            var copied = 0;
            foreach (var file in contributions.SelectMany(c => c.Files).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                File.Copy(file, Path.Combine(union, Path.GetFileName(file)), overwrite: false);
                copied++;
            }

            // THE DENOMINATOR. "12 files staged" cannot be told from "12 of 15"; only the second says
            // whether the comparison about to run covers what was asked for.
            output.WriteLine($"  union     {copied} staged at {union} for the drift check — {ProgramFiles.Summary(contributions)}");

            foreach (var empty in contributions.Where(c => c.ContributedNothing))
                output.WriteLine($"            CONTRIBUTED NOTHING: {empty.Requested} ({empty.Kind})");

            return union;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Reported, and the drift steps are then omitted. A batch that silently skipped the check
            // because a copy failed would be claiming a comparison it never made.
            output.WriteLine($"  union     COULD NOT STAGE the program union, so the drift check will NOT run: {error.Message}");
            return null;
        }
    }

    private static bool Number(string[] args, ref int i, string flag, TextWriter output, out int value, bool allowZero = false)
    {
        if (int.TryParse(Next(args, ref i), out value) && (value > 0 || (allowZero && value == 0)))
            return true;

        output.WriteLine($"{flag} requires a positive whole number.");
        return false;
    }

    private sealed record ManifestArgs(
        string? Lane, string? Binding, string? Submission, IReadOnlyList<string> Programs,
        string? BlockUnderTest, string? EmitDir, string? OutPath, string? CheckPath);

    /// <summary>
    /// 🔴 <b>THE PRODUCER <c>LaneManifest</c> was written for and never had.</b>
    ///
    /// <para><c>LaneManifest</c> opens by saying the program set is <i>"EMITTED BY WHATEVER BUILT THE LANE
    /// — not typed on a command line"</i>, and every manifest in existence has been hand-authored. So the
    /// stronger DERIVED wording in <see cref="Enqueue"/> has been unreachable in practice, and a nine-object
    /// set was deployed on 2026-08-22 that survives only as a shell command.</para>
    ///
    /// <para><b>It generates the copy layer to obtain the stamp, not as a side errand.</b> The build stamp
    /// is what the manifest is checked against, and the only thing that knows it is the derivation inside
    /// <c>LoopRun.Generate</c>. Two entry points computing it two ways is the drift
    /// <c>GateParityTests</c> exists to stop, so this one goes through the same composition
    /// <c>harness-run</c> does.</para>
    ///
    /// <para>🔴 <b><c>--check</c> is the other half, and it is the arm that catches a STALE manifest.</b>
    /// It re-derives the stamp over the manifest's OWN paths and compares. Same paths, so the two agree by
    /// construction — <i>unless the files changed underneath</i>, which is precisely the case that produced
    /// a stamp naming a pre-fix <c>Main</c>: the manifest still says one object name and the file at that
    /// path now declares another.</para>
    /// </summary>
    private static int Manifest(
        TextWriter output, Func<string, string> readFile, Func<string, byte[]>? readBytes,
        Action<string, string> writeFile, ManifestArgs args)
    {
        if (string.IsNullOrWhiteSpace(args.Lane) || string.IsNullOrWhiteSpace(args.Binding) || string.IsNullOrWhiteSpace(args.Submission))
        {
            output.WriteLine("manifest needs --lane <name>, --binding <file> and --submission <file>: the copy layer, and so the build "
                + "stamp the manifest is checked against, is a function of those two documents.");
            return BatchExit.Unusable;
        }

        var checking = !string.IsNullOrWhiteSpace(args.CheckPath);

        LaneManifest? existing = null;
        IReadOnlyList<string> programPaths = args.Programs;

        if (checking)
        {
            if (args.Programs.Count > 0 || args.OutPath is not null || args.EmitDir is not null)
            {
                output.WriteLine("--check re-examines a manifest that already exists; it does not build one. Drop --program, --emit and "
                    + "--out, or drop --check. Accepting both would let this command report on one manifest and write another.");
                return BatchExit.Unusable;
            }

            try
            {
                existing = LaneManifest.Read(args.CheckPath!, readFile);
            }
            catch (Exception error) when (error is InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                output.WriteLine("REFUSED  " + error.Message);
                return BatchExit.Unusable;
            }

            programPaths = existing.ProgramPaths;
        }
        else
        {
            if (args.Programs.Count == 0 || string.IsNullOrWhiteSpace(args.OutPath) || string.IsNullOrWhiteSpace(args.EmitDir))
            {
                output.WriteLine("manifest needs --program <path>..., --emit <dir> and --out <manifest.json>. --emit is not optional: a "
                    + "manifest records a PATH per object so it can be turned back into a --program list, and the generated copy layer has "
                    + "no path until something writes it.");
                return BatchExit.Unusable;
            }
        }

        IReadOnlyList<LoadedProgramObject> loaded;
        LoopGeneration generation;
        try
        {
            loaded = ProgramUnderTest.LoadWithPaths(programPaths, readFile, ManifestExpand);

            var request = LoopCli.Compose(
                Harness.Gate.SubmissionDocument.Read(readFile(args.Submission!)),
                Harness.Gate.BindingDocument.Read(readFile(args.Binding!)),
                loaded.Select(l => l.Object).ToArray(),
                readFile,
                readBytes);

            // stopWhenInadmissible: false — the stamp is a function of the map, the binding, the naming and
            // the program, none of which the gate's verdict touches. Refusing to write a manifest because a
            // VECTOR was inadmissible would withhold the record of what was built from the person fixing it.
            generation = LoopRun.Generate(request, stopWhenInadmissible: false);
        }
        catch (Exception error) when (error is InvalidDataException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            output.WriteLine("REFUSED  the program set or the two documents could not be composed, so nothing was derived and nothing was "
                + $"written: {error.Message}");
            return BatchExit.Unusable;
        }

        if (!generation.Generated || generation.Manifest is not { } hashedSet)
        {
            output.WriteLine("REFUSED  no copy layer was generated, so there is no build stamp for a manifest to be checked against and "
                + $"nothing was written: {generation.Detail}");
            return BatchExit.Refused;
        }

        output.WriteLine($"  stamp     {generation.Stamp.Literal} over {hashedSet.Objects.Count} object(s): "
                       + string.Join(", ", hashedSet.Objects.Select(o => $"{o.Kind}:{o.Name}")));

        if (hashedSet.ExcludedAsSelfReferential.Count > 0)
        {
            output.WriteLine($"            EXCLUDED as the harness's own output ({hashedSet.ExcludedAsSelfReferential.Count}): "
                           + string.Join(", ", hashedSet.ExcludedAsSelfReferential));
        }

        // 🔴 *** EXIT 2 = EXAMINED NOTHING, AND IT IS NEVER A PASS. *** `StampAgreement`'s own docstring
        // names this shape as the disease — "a bare AGREES over a manifest of zero objects and a stamp of
        // zero objects is the shape of a check that compared nothing" — and the mitigation chosen was to
        // PRINT the counts. `ProgramManifest.HashedNothing` was built to tell the case apart and nothing
        // consulted it, so the emit-directory case that `LaneManifest.Derive`'s own docstring contemplates
        // produced `manifest AGREES` at exit 0 over a denominator of zero.
        //
        // Taken BEFORE the copy layer is written, so a refused invocation leaves nothing behind.
        //
        // The value matches the converter's mechanical floor — `candidate-scan`, `undriven-scan`,
        // `reuse-scan`, `relation-reconcile`, `signal-sweep` all read 2 as "examined nothing" — because a
        // reader who has learned it there must not have to learn a second convention here.
        if (hashedSet.HashedNothing)
        {
            output.WriteLine("REFUSED  the build stamp hashed NOTHING, so there is no program for a manifest to describe and no comparison "
                + "to make. Every object supplied as the program under test was excluded as the harness's OWN output"
                + (hashedSet.ExcludedAsSelfReferential.Count > 0
                    ? $" ({string.Join(", ", hashedSet.ExcludedAsSelfReferential)})"
                    : string.Empty)
                + " — which is what happens when --program is pointed at the --emit directory of an earlier run instead of at the lane's "
                + "IR. A manifest written here would AGREE with the stamp over a denominator of zero and read exactly like a lane that "
                + "had been fully checked. EXIT 2 IS \"EXAMINED NOTHING\", the same reading it has across the converter's mechanical "
                + "floor, and it is never a pass.");
            return BatchExit.Unusable;
        }

        if (checking)
        {
            var verdict = existing!.AgreesWithStamp(hashedSet);
            output.WriteLine((verdict.Agrees ? "OK       " : "REFUSED  ") + verdict.Detail);
            return verdict.Agrees ? BatchExit.Ok : BatchExit.Refused;
        }

        // The copy layer is WRITTEN, not merely named. A manifest whose paths do not exist turns into a
        // --program list that contributes nothing, and `ProgramFiles` would then report the absence from
        // three steps away rather than here.
        var emitted = new List<EmittedObject>();
        try
        {
            Directory.CreateDirectory(args.EmitDir!);

            foreach (var obj in generation.Objects)
            {
                var path = Path.Combine(args.EmitDir!, obj.Name + ".ir");
                writeFile(path, obj.Ir);
                emitted.Add(new EmittedObject(obj.Name, path, obj.Kind));
                output.WriteLine($"  written   {path}");
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            output.WriteLine($"REFUSED  the generated copy layer could not be written to {args.EmitDir}, so no manifest was written "
                + $"either — a manifest naming files that do not exist is worse than none: {error.Message}");
            return BatchExit.Unusable;
        }

        LaneManifest derived;
        try
        {
            derived = LaneManifest.Derive(
                args.Lane!,
                // 🔴 THE KIND TRAVELS. `LoadedProgramObject` classified it from the IR's own header; dropping
                // it here is what let `--block-under-test` name an instance DB. See LaneManifest.Derive.
                loaded.Select(l => new EmittedObject(l.Object.Name, l.Path, l.Object.Kind)).ToArray(),
                emitted,
                args.BlockUnderTest,
                hashedSet,

                // Derived, not typed: the generator knows the copy layer needs a call site and knows what
                // it is called. It cannot create one, so it says so — see LaneManifest.Obligations.
                generation.Objects
                    .Where(o => o.Kind == HarnessObjectKind.Block)
                    .Select(o => $"'{o.Name}' MUST be called from the cyclic OB. A generated FC nothing calls is deployed, loaded, "
                               + "healthy in every artifact, and never runs.")
                    .ToArray());
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException)
        {
            output.WriteLine("REFUSED  " + error.Message);
            return BatchExit.Refused;
        }

        writeFile(args.OutPath!, derived.ToJson());

        var subject = derived.BlockUnderTest;
        output.WriteLine($"  manifest  DERIVED: {derived.Objects.Count} object(s) across {derived.ProgramPaths.Count} path(s), written to {args.OutPath}");
        output.WriteLine("            " + derived.AgreesWithStamp(hashedSet).Detail);
        // 🔴 A LANE THAT NAMES NO SUBJECT STILL RUNS — owner's ruling 2026-08-24, REPORT DO NOT GATE — so
        // the report is the only thing standing between it and a lane with a verified subject. One
        // sentence read as an invitation; the banner cannot.
        if (subject is null)
        {
            output.WriteLine("            BLOCK UNDER TEST: none stated.");
            output.WriteLine("            *** SO NO PER-BLOCK CHECK APPLIES: the stamp moves if ANY object changes,");
            output.WriteLine("                and nothing here says which one is the subject. ***");
            output.WriteLine("            Pass --block-under-test <Name> to name it.");
        }
        else
        {
            output.WriteLine($"            BLOCK UNDER TEST: {subject} — a Block, IN the stamped set, so changing it changes the stamp.");
        }
        output.WriteLine($"            Enqueue it with:  harness-batch enqueue --queue <dir> --lane {args.Lane} --binding {args.Binding} "
                       + $"--submission {args.Submission} --manifest {args.OutPath}");

        return BatchExit.Ok;
    }

    /// <summary>A directory becomes its <c>.ir</c> files in a stable order; anything else is itself.</summary>
    private static IReadOnlyList<string> ManifestExpand(string path) =>
        Directory.Exists(path)
            ? Directory.GetFiles(path, "*.ir").OrderBy(p => p, StringComparer.Ordinal).ToArray()
            : new[] { path };

    private static int Enqueue(
        LaneQueue store, TextWriter output, Func<string, string> readFile,
        string? lane, string? binding, string? submission, List<string> programs, string? purpose, string? manifestPath)
    {
        if (string.IsNullOrWhiteSpace(lane) || string.IsNullOrWhiteSpace(binding) || string.IsNullOrWhiteSpace(submission))
        {
            output.WriteLine("enqueue needs --lane <name>, --binding <file> and --submission <file>.");
            return BatchExit.Unusable;
        }

        // 🔴 *** THE PROGRAM SET IS DERIVED OR IT IS DECLARED, AND THE REPORT SAYS WHICH. ***
        //
        // Declared is the old path and still works. It is the weaker one: the stamp is computed over this
        // set and means "what is executing", and a hand-typed list is how a lane once got pointed at a
        // pre-fix Main with the stamp following it. Saying which of the two happened costs one line and
        // is the difference between a reader knowing and a reader assuming.
        // 🔴 THE DENOMINATOR THE BUILD STAMP WILL BE MEASURED AGAINST, or the honest absence of one.
        // Derived from the manifest below; empty where there is no manifest, which is what makes the run
        // say NO STAGED CORPUS WAS SUPPLIED rather than measure itself against a set nobody stated.
        var staged = Array.Empty<string>().ToList();

        if (!string.IsNullOrWhiteSpace(manifestPath))
        {
            LaneManifest manifest;
            try
            {
                manifest = LaneManifest.Read(manifestPath, readFile);
            }
            catch (Exception error) when (error is InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                output.WriteLine("REFUSED  " + error.Message);
                return BatchExit.Unusable;
            }

            // Never chooses a winner. The caller meant something by --program, and silently overriding it
            // would swap one unexamined program set for another.
            if (manifest.Disagreement(programs) is { } disagreement)
            {
                output.WriteLine("REFUSED  " + disagreement);
                return BatchExit.Unusable;
            }

            // 🔴 *** DERIVED HAS TO BE EARNED, AND UNTIL 2026-08-24 IT WAS KEYED ON WHICH FLAG WAS
            // PASSED. *** A hand-written manifest naming TWO NONEXISTENT FILES with fabricated origins
            // reported as "program set DERIVED from the manifest" and as "the DENOMINATOR every wave's
            // build stamp will be reported against". Nothing had emitted it and nothing looked.
            //
            // `AgreesWithStamp` had two production call sites and both were inside the `manifest` verb;
            // this path called only `Read`. It still cannot re-derive a stamp — it holds no copy layer —
            // but a manifest that WAS emitted records the stamp and a hash per object, so the document can
            // be checked against the files it names. That is the tie, at the cost of one read per object.
            if (manifest.Stamp is null)
            {
                output.WriteLine("REFUSED  the lane manifest at " + manifestPath + " records NO build stamp, so nothing emitted it — it was "
                    + "typed. THE WHOLE VALUE OF --manifest IS THAT THE PROGRAM SET WAS EMITTED BY WHATEVER BUILT THE LANE rather than "
                    + "typed on a command line, and a hand-written document reported as DERIVED is the weaker case wearing the stronger "
                    + "case's word. Produce one with `harness-batch manifest --lane ... --program ... --emit ... --out ...`, which ties it "
                    + "to the stamp at birth; or pass --program instead and accept the DECLARED wording, which says out loud that nothing "
                    + "checked the list.");
                return BatchExit.Unusable;
            }

            // Re-read every path the manifest names and re-hash it, with the stamp's own hash rule. The
            // stamp VALUE cannot be recomputed here, so this is the arm that is available: same names AND
            // same bytes, or the document is stale.
            var content = manifest.ContentStillMatches(readFile);
            if (!content.Holds)
            {
                output.WriteLine($"REFUSED  the lane manifest at {manifestPath} no longer describes the files it names. {content.Detail} "
                    + "The build stamp recorded in this manifest was computed over the OLD content, so enqueueing it would queue a lane "
                    + "whose stamp claims a program that is not on disk — which is how a lane gets pointed at a pre-fix Main and the stamp "
                    + "goes with it. Re-run `harness-batch manifest` to emit a current one, or restore the files.");
                return BatchExit.Refused;
            }

            programs = manifest.ProgramPaths.ToList();

            // 🔴 *** THE NAMES, NOT ONLY THE PATHS — AND THE NAMES WERE READ AND THROWN AWAY. *** The
            // paths become `--program`, which the build stamp HASHES; the names become the corpus it is
            // MEASURED AGAINST. Deriving the second from the first cannot work: a path is a file and the
            // stamp matches on the object name, and a denominator taken from the same list as the
            // numerator can never report a gap. Measured consequence in docs/18-project-workbench.md §5
            // "Phase 10 — Wave time", under "THE BUILD STAMP DOES COVER THE PARAMETER DB".
            staged = manifest.Objects.Select(o => o.Name).ToList();

            // 🔴 THREE BUCKETS, NOT TWO. This counted Generated and called EVERY remaining object
            // "authored" — including an Unstated one, which is what `LaneManifest.Derive` records for a
            // program read off disk, because the harness genuinely cannot tell who wrote an .ir file.
            // Folding "nobody said" into "a person wrote it" answers the question the origin field exists
            // for — how much of this lane is still hand-built — with a number nothing established.
            var generated = manifest.Objects.Count(o => o.Origin == ObjectOrigin.Generated);
            var authored = manifest.Objects.Count(o => o.Origin == ObjectOrigin.Authored);
            output.WriteLine($"  program set DERIVED from the manifest: {manifest.Objects.Count} object(s) "
                           + $"({generated} generated, {authored} authored, {manifest.Objects.Count - generated - authored} origin unstated) "
                           + $"across {programs.Count} path(s).");

            // 🔴 THE DENOMINATOR BEHIND THE WORD "DERIVED". Printed on the passing path, because "the
            // manifest checked out" is true of a manifest that had nothing to check.
            output.WriteLine($"  manifest TIED to build stamp 16#{manifest.Stamp.Value:X8}; content re-verified: {content.Detail}");

            // The subject, or the honest absence of one. Two objects claiming the role reads as none here,
            // deliberately: a lane tests one block and guessing which would be worse than declining.
            //
            // 🔴 NOTHING GATES ON A SUBJECT — owner's ruling 2026-08-24, report do not gate, and the
            // intended consumer (`converter undriven-scan --fb`) is NOT wired. See
            // LaneManifest.BlockUnderTest. So a subjectless lane must be impossible to mistake for a lane
            // with a verified one, and one apologetic clause was not doing that.
            if (manifest.BlockUnderTest is { } subject)
            {
                output.WriteLine($"  block under test DERIVED from the manifest: {subject}.");
            }
            else
            {
                output.WriteLine("  BLOCK UNDER TEST: none stated.");
                output.WriteLine("  *** SO NO PER-BLOCK CHECK APPLIES: the stamp moves if ANY object changes,");
                output.WriteLine("      and nothing here says which one is the subject. ***");
            }
            output.WriteLine($"  staged corpus DERIVED from the manifest: {staged.Count} object name(s) — the DENOMINATOR "
                           + "every wave's build stamp will be reported against.");

            // Printed, never enforced: this tool cannot create a call site, and a generated FC nothing
            // calls is deployed, loaded, healthy in every artifact, and never runs.
            foreach (var obligation in manifest.Obligations)
                output.WriteLine("  OBLIGATION: " + obligation);
        }
        else
        {
            output.WriteLine($"  program set DECLARED by the caller: {programs.Count} path(s), from --program. "
                           + "Nothing emitted this list, so nothing checks it against what the lane actually built.");
            output.WriteLine("  staged corpus NONE — with no manifest there is no list of what this lane staged, so its waves "
                           + "will report the build stamp with NO DENOMINATOR. That is said rather than guessed: a corpus "
                           + "derived from --program would be the numerator measuring itself and could never report a gap.");
        }

        var outcome = store.Enqueue(new Lane(lane, binding, submission, programs, purpose ?? string.Empty, staged));
        output.WriteLine((outcome.Ok ? "QUEUED   " : "REFUSED  ") + outcome.Detail);

        return outcome.Result switch
        {
            EnqueueResult.Queued => BatchExit.Ok,
            EnqueueResult.AlreadyQueued => BatchExit.Refused,
            _ => BatchExit.Unusable,
        };
    }

    /// <summary>
    /// 🔴 <b><c>--converter</c> is what turns the width from AUTHORED into DERIVED here.</b> Without it
    /// the plan still runs and still says, on its own report, that the width was declared and nothing
    /// corroborated it — which is the state every plan was in before 2026-08-23. It is not defaulted to
    /// <c>"converter"</c> the way <c>run</c> does, because <c>plan</c> starts no process today and
    /// acquiring that behaviour by default would surprise every existing caller; asking for it is one
    /// flag, and the report names the flag by naming what was not done.
    /// </summary>
    private static int Plan(
        LaneQueue store, TextWriter output, Func<string, string> readFile, Action<string, string> writeFile,
        string? outPath, string? converterExe, IProcessRunner? runner, NeighbourMode neighbourMode)
    {
        var lanes = store.All();

        var served = converterExe is not null && runner is not null
            ? ServedAreaProbe.Derive(
                converterExe,
                lanes.SelectMany(l => l.ProgramPaths).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                runner)
            : ServedAreaFact.NotDerived(
                "`plan` was given no --converter <exe>, so the program corpus was not read and the served width "
                + "was not established. The width below is DECLARED, not derived. Pass --converter to check it "
                + "against the MB_SERVER call that serves it.");

        var neighbours = Neighbours(neighbourMode, store, output, converterExe, lanes, served, runner, "plan");

        var result = BatchPlanner.Plan(lanes, readFile, served, neighbours);

        output.Write(BatchPlanner.Describe(result));

        if (!result.Planned)
            return lanes.Count == 0 ? BatchExit.NothingBatched : BatchExit.Refused;

        if (outPath is not null)
        {
            writeFile(outPath, result.MergedBindingJson!);
            output.WriteLine($"  merged binding written to {outPath}");
        }

        // The union pre-flight is a SEPARATE act and it is named rather than implied. Printing the
        // commands rather than running them is deliberate: this tool has no business deciding that a
        // cross-check finding is acceptable, and a batch that swallowed one would be the correlated
        // check this project exists to avoid.
        output.WriteLine();
        // 🔴 THESE ARE PER-LANE CHECKS AND THEY ARE NOT THE UNION CHECK. Until 2026-08-23 this block
        // printed exactly the same commands under a heading calling them "UNION PRE-FLIGHT", directly
        // above a sentence correctly explaining that only a union check finds a cross-lane conflict — so
        // the claim and the command contradicted each other on adjacent lines. A closed check: it names
        // something real (each lane's own corpus) that is not the thing it claimed.
        //
        // `plan` cannot run the union check, because the union corpus is materialised by `run`. So it
        // says what these are, and where the real one happens, rather than overclaiming.
        output.WriteLine("  PER-LANE pre-flight — optional, and NOT the union check:");
        foreach (var path in result.ProgramPaths.Distinct())
            output.WriteLine($"    converter cross-check --project {path}");

        output.WriteLine();
        output.WriteLine("  THE UNION CHECK IS RUN BY `harness-batch run --yes`, over the merged corpus, before the lease.");
        output.WriteLine("  It is the step a batch earns: two blocks that each compiled clean in isolation can still");
        output.WriteLine("  conflict, and ONLY a check over the union sees it. Per-lane runs cannot, by construction.");

        return BatchExit.Ok;
    }

    private static int List(LaneQueue store, TextWriter output)
    {
        var lanes = store.All();
        output.WriteLine($"lanes {lanes.Count}");

        foreach (var lane in lanes)
        {
            output.WriteLine($"  {lane.Name}");
            output.WriteLine($"    binding    {lane.BindingPath}");
            output.WriteLine($"    submission {lane.SubmissionPath}");
            output.WriteLine($"    program    {string.Join(", ", lane.ProgramPaths)}");
            if (lane.Purpose.Length > 0)
                output.WriteLine($"    purpose    {lane.Purpose}");
        }

        if (lanes.Count == 0)
            output.WriteLine("  (none) — an empty queue and a mistyped --queue path look identical from here; the queue line above is the one to check.");

        return BatchExit.Ok;
    }

    private static int Dequeue(LaneQueue store, TextWriter output, string? lane)
    {
        if (string.IsNullOrWhiteSpace(lane))
        {
            output.WriteLine("dequeue needs --lane <name>.");
            return BatchExit.Unusable;
        }

        if (store.Dequeue(lane))
        {
            output.WriteLine($"REMOVED  lane '{lane}' is no longer queued.");
            return BatchExit.Ok;
        }

        output.WriteLine($"REFUSED  lane '{lane}' is not in this queue.");
        return BatchExit.Refused;
    }

    private static string? Next(string[] args, ref int i) => i + 1 < args.Length ? args[++i] : null;
}
