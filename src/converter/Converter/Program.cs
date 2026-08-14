using System.Xml.Linq;
using Converter.CandidateScan;
using Converter.Claims;
using Converter.Compare;
using Converter.ConflictGraph;
using Converter.CrossCheck;
using Converter.Diff;
using Converter.Digest;
using Converter.DriftCheck;
using Converter.Ir;
using Converter.IrHash;
using Converter.Preflight;
using Converter.ReuseScan;
using Converter.RelationReconcile;
using Converter.Review;
using Converter.Sanitize;
using Converter.SignalSweep;
using Converter.SimaticMl;
using Converter.TagStatus;
using Converter.TargetScan;
using Converter.Trace;
using Converter.UndrivenScan;

namespace Converter;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length >= 1 && args[0] == "sanitize")
        {
            return RunSanitize(args[1..]);
        }

        if (args.Length >= 1 && args[0] == "review")
        {
            return RunReview(args[1..]);
        }

        if (args.Length >= 1 && args[0] == "digest")
        {
            return RunDigest(args[1..]);
        }

        if (args.Length >= 1 && args[0] == "preflight")
        {
            return RunPreflight(args[1..]);
        }

        if (args.Length >= 1 && args[0] == "tagstatus")
        {
            return RunTagStatus(args[1..]);
        }

        if (args.Length >= 1 && args[0] == "diff")
        {
            return RunDiff(args[1..]);
        }

        if (args.Length >= 1 && args[0] == "reuse-scan")
        {
            return RunReuseScan(args[1..]);
        }

        if (args.Length >= 1 && args[0] == "candidate-scan")
        {
            return RunCandidateScan(args[1..]);
        }

        if (args.Length >= 1 && args[0] == "undriven-scan")
        {
            return RunUndrivenScan(args[1..]);
        }

        if (args.Length >= 1 && args[0] == "relation-reconcile")
        {
            return RunRelationReconcile(args[1..]);
        }

        if (args.Length >= 1 && args[0] == "signal-sweep")
        {
            return RunSignalSweep(args[1..]);
        }

        if (args.Length >= 1 && args[0] == "ir-hash")
        {
            return RunIrHash(args[1..]);
        }

        if (args.Length >= 1 && args[0] == "target-scan")
        {
            return RunTargetScan(args[1..]);
        }

        if (args.Length >= 1 && args[0] == "drift-check")
        {
            return RunDriftCheck(args[1..]);
        }

        if (args.Length >= 1 && args[0] == "compare")
        {
            return RunCompare(args[1..]);
        }

        if (args.Length >= 1 && args[0] == "cross-check")
        {
            return RunCrossCheck(args[1..]);
        }

        if (args.Length >= 1 && args[0] == "conflict-graph")
        {
            return RunConflictGraph(args[1..]);
        }

        if (args.Length >= 1 && args[0] == "trace")
        {
            return RunTrace(args[1..]);
        }

        if (args.Length >= 1 && args[0] == "claim")
        {
            return RunClaim(args[1..]);
        }

        if (args.Length >= 1 && args[0] == "claims")
        {
            return RunClaims(args[1..]);
        }

        // FI-73. The convert path is its own method so its argument handling can be tested: an unknown
        // --flag used to be treated as a FILENAME here, and the only way to prove the refusal works is to
        // call it directly.
        return RunConvert(args);
    }

    internal static int RunConvert(string[] args)
    {
        if (args.Length < 2 || args[0] is not ("to-ir" or "to-xml"))
        {
            Console.Error.WriteLine("Usage: converter to-ir|to-xml <file> [<file> ...] [--project <ir-dir>] [--out <dir>]");
            Console.Error.WriteLine("                   --out <dir>  writes the result there instead of BESIDE THE INPUT (FI-72). Default is beside the input, which is right in the ordinary");
            Console.Error.WriteLine("                                export-and-read-back loop and destructive when the file beside it is hand-authored — an overwrite is now reported when it happens");
            Console.Error.WriteLine("                   to-xml REFUSES to emit XML with unresolved member types (FI-71). Pass --project <ir-dir>; --allow-blind-types converts anyway");
            Console.Error.WriteLine("       converter to-xml   # derives the sidecar when the input has none (ADR-0005); uses a stored SIDECAR if present. --project supplies callee/tag types for derivation");
            Console.Error.WriteLine("       converter to-ir    # keeps the stored SIDECAR by default (safe); --no-sidecar omits it for a block already verified derivable (errors if unsynthesizable)");
            Console.Error.WriteLine("       converter to-xml <file> --synthesize   # force the derive path (errors if a SIDECAR is present)");
            Console.Error.WriteLine("       converter sanitize <file> --map <mapping.json> --out <path>");
            Console.Error.WriteLine("       converter review <file> [<file> ...] [--project <ir-dir>] [--ignore-errors] [--json] [--allow-unchecked]   # --project enables cross-file rules (C-118 interface-UDT Step, FI-09). Exit 1 = error findings, 2 = REVIEW INCOMPLETE (a rule had a subject here and was not judged; --allow-unchecked accepts that deliberately)");
            Console.Error.WriteLine("       converter digest <file> [<file> ...] [--ignore-errors] [--json]   # compact structural summary of .ir content (FI-15)");
            Console.Error.WriteLine("       converter preflight <file> [<file> ...] --project <ir-dir> [--json]   # static checks before any Portal round trip (FI-13; not the compile gate)");
            Console.Error.WriteLine("       converter tagstatus <name> [<name> ...] --project <ir-dir> [--json] [--roots-only]   # classify names against the export (FI-24): EXISTS / PROPOSED (root absent) / MEMBER-NOT-FOUND (root resolves, member absent) / MEMBER-UNCHECKED (member namespace not enumerable); exit 1 if any proposed or member-not-found. --roots-only restores root-level-only classification");
            Console.Error.WriteLine("       converter diff <old.ir> <new.ir> [--only <network> ...] [--json]   # which networks changed, rest provably identical in IR (S7 invariance); with --only, exit 1 on any change outside the set");
            Console.Error.WriteLine("       converter reuse-scan --project <ir-dir> [--tag <tag> ...] [--kind <kind> ...] [--json]   # reuse-first: which blocks reference tag(s)/implement kind(s) (FI-29); exit 1 if any candidate found");
            Console.Error.WriteLine("       converter ir-hash <file> [<file> ...] [--json]   # stable readable-IR hash keying an explanation sidecar (FI-17); immune to SIDECAR/UId churn; exit 1 on any error");
            Console.Error.WriteLine("       converter target-scan --requirements <register.md> --project <ir-dir> [--json]   # S6 new-block target gap-hunter: REQ x tag-status x as-built (FI-30); exit 1 if no clean candidate");
            Console.Error.WriteLine("       converter drift-check --project <ir-dir> --exports <simatic-ml-dir> [--complete] [--json]   # detect ir<->simatic-ml export drift (FI-26); exit 1 if any block drifted");
            Console.Error.WriteLine("                    --complete: the exports dir is the WHOLE picture (e.g. a fresh controller dump, FI-70), so a missing .xml OR a .xml with no .ir also fails");
            Console.Error.WriteLine("       converter compare <first.xml> <second.xml> [--json] [--max-differences <n>] [--allow-silent-layout]   # the CONFIRM LOOP's judgement half: Normalizer-compare two SimaticML exports and report WHAT differs");
            Console.Error.WriteLine("                    exit 0 equivalent / 1 differs / 2 NOT COMPARED (missing, unparseable, not a block export, same file twice, or a MemoryLayout premise that did not hold)");
            Console.Error.WriteLine("       converter cross-check --project <ir-dir> [--json]   # whole-project cross-block reference-graph FACTS the reviewer reasons over (FI-22); never verdicts; exit 0");
            Console.Error.WriteLine("       converter conflict-graph --project <ir-dir> (--submission <file> | --signals <file>) [--json] [--allow-unresolved]   # SUBMISSION-SCOPED `conflictEdges` for harness gates 8/8c, in the exact shape ConflictEdgeDocument deserializes. NOT cross-check with a filter: that emits whole-project fact tables keyed on a storage path, this emits EDGES between BLOCKS with a provenance and a signal class. Only MultiWriter provenance is ever emitted - a CallGraph edge is about no signal, so it could only carry an Unstated class, and ProvenanceComplete is ALL-or-nothing, so ONE such edge would turn gate 8c to NOT CHECKED for the whole submission. `computedConflicts` is never emitted for the same reason (a bare name is Unstated provenance); gate 8's packing set derives from the edges. Exit 0 = computed (an EMPTY list is the EARNED claim that the graph ran and found nothing), 2 = NOT COMPUTED and the key is WITHHELD so the gate reports NOT CHECKED, 3 = emitted but an edge is unprovenanced");
            Console.Error.WriteLine("       converter trace --binding <bindings.json> --project <ir-dir> [--json]   # forward-pass REQ trace: per-hop facts over the reader/writer graph (FI-25); facts not verdicts; exit 0. Hops incl. guard-containment (FI-36-min): every spec-listed condition must appear in the coil's guard");
            Console.Error.WriteLine("       converter candidate-scan --project <ir-dir> --fb <FBName> [--scope <prefix> ...] [--type <T>] [--direction status|command|any] [--json]   # compute every signal that could satisfy a requirement (FI-39); exit 1 if the IO half has >1 candidate");
            Console.Error.WriteLine("       converter undriven-scan --project <ir-dir> --fb <FBName> [--instance <iDB> ...] [--caller <file.ir> ...] [--hints] [--json]   # per-instance interface drive states (FI-39); exit 1 on undriven/disarmed");
            Console.Error.WriteLine("       converter relation-reconcile --specs <dir> --ledger <code-structure.md> --register <requirements.md> [--project <ir-dir>] [--json]   # reconcile (instance, relation-id) sets across the spec artifacts + probative citations (FI-39); exit 1 on any difference");
            Console.Error.WriteLine("       converter signal-sweep --project <ir-dir> --specs <dir> [--register <file>] [--unclaimed <file>] [--json]   # project-level residual signal coverage (FI-39); exit 1 if any signal is in no spec and no disposition table");
            Console.Error.WriteLine("       converter claim  --project <ir-dir> --claims <dir> --agent <id> --kind <k> (--value <v> | --allocate [--type FB|FC|OB|DB] [--floor <n>] [--in <word|block>]) [--purpose <text>] [--json]   # reserve a shared resource BEFORE writing IR (FI-65); exit 1 refused, 2 unusable. X-J RESERVED BAND (2026-08-14): block numbers 9000-9999 are reserved for harness-generated objects per number space, FB/FC/DB, OB EXCLUDED (an OB's number is fixed by its event class, and applying a band to OBs emits a false finding on OB80, the first harness object the spec lists). A PLAIN --allocate CANNOT return a band number - the band is REMOVED from the candidate set, not deprioritised. --floor 9000 aims the search INTO the band, and that allocation is CONFINED to it: running out is `BandExhausted` naming the band, NEVER a quiet step past 9999 into deliverable numbers. An explicit --value inside the band is ACCEPTED and SAID SO in the outcome, not refused - the block does not exist yet (that is what an allocation claim means), so nothing derivable distinguishes a harness claim from a plant one, and a --harness flag would be a caller assertion forgotten exactly when it matters. The band is read from HarnessNumberRange.Declared(), never restated here");
            Console.Error.WriteLine("       converter claims --project <ir-dir> --claims <dir> [--check] [--release --agent <id> (--kind <k> --value <v> | --all) [--force]] [--agent <id>] [--json]   # list/verify/release claims (FI-65); exit 1 on conflict");
            return 1;
        }

        var mode = args[0];
        var rest = args[1..];
        var synthesize = rest.Contains("--synthesize");
        var noSidecar = rest.Contains("--no-sidecar");
        var allowBlindTypes = rest.Contains("--allow-blind-types");

        // --project <ir-dir> (optional, --synthesize only): supplies callee interfaces for wired-CALL
        // synthesis beyond the blocks in the batch itself.
        string? projectDir = null;
        string? outDir = null;
        var positional = new List<string>();
        for (var i = 0; i < rest.Length; i++)
        {
            if (rest[i] == "--synthesize" || rest[i] == "--no-sidecar" || rest[i] == "--allow-blind-types")
            {
                continue;
            }

            if (rest[i] == "--project")
            {
                if (i + 1 >= rest.Length)
                {
                    Console.Error.WriteLine("Flag '--project' requires a value.");
                    return 1;
                }

                projectDir = rest[++i];
                continue;
            }

            if (rest[i] == "--out")
            {
                if (i + 1 >= rest.Length)
                {
                    Console.Error.WriteLine("Flag '--out' requires a value.");
                    return 1;
                }

                outDir = rest[++i];
                continue;
            }

            // FI-73. An unrecognised --flag used to fall through to here and be treated as a FILENAME.
            // Measured on a build that predated --out: `to-ir x.xml --out dir` converted x.xml BESIDE ITS
            // INPUT (the destructive act this project had just fixed), then died with an unhandled
            // FileNotFoundException on a file literally called "--out". Any flag typo does the same, and
            // the damage is done before the crash. A leading "--" is never a path here, so refuse it.
            if (rest[i].StartsWith("--", StringComparison.Ordinal))
            {
                Console.Error.WriteLine(
                    $"Unknown flag '{rest[i]}' for '{mode}'. Valid flags: --project <ir-dir>, --out <dir>, " +
                    "--synthesize (to-xml), --no-sidecar (to-ir), --allow-blind-types (to-xml). " +
                    "Refusing rather than treating it as a file name, which would convert the other inputs first.");
                return 1;
            }

            positional.Add(rest[i]);
        }

        var files = positional.ToArray();

        if (synthesize && mode != "to-xml")
        {
            Console.Error.WriteLine("--synthesize is only valid with 'to-xml' — a real SimaticML export always has real sidecar data, so synthesis is meaningless for 'to-ir'.");
            return 1;
        }

        if (noSidecar && mode != "to-ir")
        {
            Console.Error.WriteLine("--no-sidecar is only valid with 'to-ir' — it omits the stored SIDECAR for a block already verified derivable.");
            return 1;
        }

        // Registries for synthesis. Built for both modes: `to-xml` derives the sidecar when the input
        // has none (ADR-0005), and `to-ir` uses the same synthesis to decide whether a block is
        // derivable (so it can omit the stored sidecar) — both need the callee interfaces (wired CALLs)
        // and tag/member types (typed boxes). A sidecar-carrying `to-xml` input ignores them.
        var callees = BuildCalleeRegistry(files, projectDir);
        var tagTypes = BuildTagTypeRegistry(files, projectDir);

        var blindRoots = WarnIfConvertingBlindToExternalTypes(mode, files, projectDir);

        // FI-71. The warning above was FI-57's remedy and it did not remedy: the same mistake has now
        // cost three separate import-and-compile cycles, the third on a file that was about to be
        // imported. A warning on stderr competes with the tool's own success line and loses.
        //
        // So `to-xml` FAILS CLOSED, and only `to-xml`: it is the direction whose output gets imported
        // into a controller, where a guessed member type is a defect waiting on a compile to find it.
        // `to-ir` reads an export and can produce nothing a PLC will execute, so it stays advisory.
        // Same family as FI-52/FI-62/FI-66 — except here the gate existed and merely asked nicely.
        if (mode == "to-xml" && blindRoots.Count > 0 && !allowBlindTypes)
        {
            Console.Error.WriteLine(
                "ERROR: refusing to emit XML with unresolved member types. This file is destined for import, " +
                "and a comparison against an unsigned member typed from the literal is rejected by TIA at " +
                "compile — after a full round trip. Re-run with --project <ir-dir>. If the referenced roots " +
                "genuinely are not in this project, pass --allow-blind-types to convert anyway.");
            return 1;
        }

        foreach (var file in files)
        {
            try
            {
                if (mode == "to-ir")
                {
                    ConvertToIr(file, noSidecar, callees, tagTypes, outDir);
                }
                else
                {
                    ConvertToXml(file, synthesize, callees, tagTypes, outDir);
                }
            }
            catch (Exception ex) when (ex is SimaticMlFormatException or UnsupportedConstructException or NonReducibleNetworkException or IrFormatException or UnsupportedSynthesisConstructException)
            {
                Console.Error.WriteLine($"{file}: {ex.GetType().Name}: {ex.Message}");
                return 1;
            }
        }

        return 0;
    }

    private static bool IsDbXml(XDocument document) =>
        document.Root?.Descendants().Any(e => e.Name.LocalName is "SW.Blocks.GlobalDB" or "SW.Blocks.InstanceDB") ?? false;

    private static bool IsTypeXml(XDocument document) =>
        document.Root?.Descendants().Any(e => e.Name.LocalName == "SW.Types.PlcStruct") ?? false;

    private static bool IsTagTableXml(XDocument document) =>
        document.Root?.Descendants().Any(e => e.Name.LocalName == "SW.Tags.PlcTagTable") ?? false;

    // FI-65 component 1. Exit codes: 0 acquired / clean, 1 refused or conflict, 2 unusable input.
    // The 1-vs-2 split matters to a calling agent: 1 is a real answer ("someone else has it, pick
    // another"), 2 means nothing was decided and retrying the same way will not help.
    private const int ExitUnusable = 2;

    // --claims is required, falling back only to LADDER_CLAIMS_DIR, and hard-errors if neither is set.
    // It deliberately does NOT default to anything worktree-relative: agents run in separate git
    // worktrees, so a per-worktree claims directory is always empty, always grants every claim, and
    // silently converts the whole registry into a no-op. FI-44's "empty is not clean" in its purest
    // form — the failure would look exactly like success.
    private static string? ResolveClaimsDir(string? flag)
    {
        if (!string.IsNullOrWhiteSpace(flag))
        {
            return flag;
        }

        var fromEnv = Environment.GetEnvironmentVariable("LADDER_CLAIMS_DIR");
        return string.IsNullOrWhiteSpace(fromEnv) ? null : fromEnv;
    }

    private static int RunClaim(string[] args)
    {
        string? projectDir = null, claimsDir = null, agent = null, kindToken = null;
        string? value = null, type = null, within = null, purpose = null;
        var allocate = false;
        var floor = 1;
        var json = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--project": projectDir = Next(args, ref i); break;
                case "--claims": claimsDir = Next(args, ref i); break;
                case "--agent": agent = Next(args, ref i); break;
                case "--kind": kindToken = Next(args, ref i); break;
                case "--value": value = Next(args, ref i); break;
                case "--type": type = Next(args, ref i); break;
                case "--in": within = Next(args, ref i); break;
                case "--purpose": purpose = Next(args, ref i); break;
                case "--floor":
                    if (!int.TryParse(Next(args, ref i), out floor))
                    {
                        Console.Error.WriteLine("--floor requires an integer");
                        return ExitUnusable;
                    }

                    break;
                case "--allocate": allocate = true; break;
                case "--json": json = true; break;
                default:
                    Console.Error.WriteLine($"Unexpected argument: {args[i]}");
                    return ExitUnusable;
            }
        }

        if (projectDir is null || agent is null || kindToken is null)
        {
            Console.Error.WriteLine("Usage: converter claim --project <ir-dir> --claims <dir> --agent <id> --kind <" + ClaimKinds.AllTokens + "> (--value <v> | --allocate [--type FB|FC|OB|DB] [--floor <n>] [--in <word|block>]) [--purpose <text>] [--json]");
            return ExitUnusable;
        }

        var resolved = ResolveClaimsDir(claimsDir);
        if (resolved is null)
        {
            Console.Error.WriteLine("--claims <dir> is required (or set LADDER_CLAIMS_DIR). It must be a directory SHARED by every agent working this project — a per-worktree path would grant every claim and coordinate nothing.");
            return ExitUnusable;
        }

        if (!Directory.Exists(projectDir))
        {
            Console.Error.WriteLine($"directory not found: {projectDir}");
            return ExitUnusable;
        }

        if (!ClaimKinds.TryParse(kindToken, out var kind))
        {
            Console.Error.WriteLine($"unknown --kind '{kindToken}' — expected one of: {ClaimKinds.AllTokens}");
            return ExitUnusable;
        }

        if (allocate == (value is not null))
        {
            Console.Error.WriteLine("pass exactly one of --value <v> or --allocate");
            return ExitUnusable;
        }

        try
        {
            var corpus = ClaimCorpus.Build(projectDir);
            var store = new ClaimStore(resolved, projectDir);

            var outcome = allocate
                ? ClaimsRunner.Allocate(corpus, store, projectDir, kind, type, floor, within, agent, purpose)
                : ClaimsRunner.Acquire(corpus, store, projectDir, kind, value!, agent, purpose);

            var text = json
                ? ClaimsOutputFormatter.FormatOutcomeJson(outcome, store.Directory)
                : ClaimsOutputFormatter.FormatOutcomeText(outcome, store.Directory);

            if (outcome.Ok)
            {
                Console.WriteLine(text);
                return 0;
            }

            Console.Error.WriteLine(text);
            return outcome.Result is ClaimResult.Invalid or ClaimResult.NothingExamined ? ExitUnusable : 1;
        }
        catch (ClaimFormatException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitUnusable;
        }
    }

    private static int RunClaims(string[] args)
    {
        string? projectDir = null, claimsDir = null, agent = null, kindToken = null, value = null;
        var check = false;
        var release = false;
        var all = false;
        var force = false;
        var json = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--project": projectDir = Next(args, ref i); break;
                case "--claims": claimsDir = Next(args, ref i); break;
                case "--agent": agent = Next(args, ref i); break;
                case "--kind": kindToken = Next(args, ref i); break;
                case "--value": value = Next(args, ref i); break;
                case "--check": check = true; break;
                case "--release": release = true; break;
                case "--all": all = true; break;
                case "--force": force = true; break;
                case "--json": json = true; break;
                default:
                    Console.Error.WriteLine($"Unexpected argument: {args[i]}");
                    return ExitUnusable;
            }
        }

        if (projectDir is null)
        {
            Console.Error.WriteLine("Usage: converter claims --project <ir-dir> --claims <dir> [--check] [--agent <id>] [--json]");
            Console.Error.WriteLine("       converter claims --project <ir-dir> --claims <dir> --release --agent <id> (--kind <k> --value <v> | --all) [--force]");
            return ExitUnusable;
        }

        var resolved = ResolveClaimsDir(claimsDir);
        if (resolved is null)
        {
            Console.Error.WriteLine("--claims <dir> is required (or set LADDER_CLAIMS_DIR).");
            return ExitUnusable;
        }

        // The doubled-root refusal lives in ClaimStore's constructor so every entry point inherits it;
        // this catch is what makes `claims` (list / --check / --release) report it the SAME WAY the
        // `claim` command does, instead of as an unhandled stack trace. A guard whose message is
        // legible from one command and a crash from the next is half a guard.
        ClaimStore store;
        try
        {
            store = new ClaimStore(resolved, projectDir);
        }
        catch (ClaimFormatException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitUnusable;
        }

        if (release)
        {
            return RunClaimsRelease(store, agent, kindToken, value, all, force);
        }

        if (!Directory.Exists(projectDir))
        {
            Console.Error.WriteLine($"directory not found: {projectDir}");
            return ExitUnusable;
        }

        var corpus = ClaimCorpus.Build(projectDir);
        var report = ClaimsRunner.Check(corpus, store, projectDir, DateTime.UtcNow);

        if (agent is not null)
        {
            report = report with { Claims = report.Claims.Where(c => c.Agent == agent).ToList() };
        }

        Console.WriteLine(json
            ? ClaimsOutputFormatter.FormatReportJson(report)
            : ClaimsOutputFormatter.FormatReportText(report));

        // Listing is informational; only --check gates. Separating them means an agent can look at the
        // board without a non-zero exit, and a pipeline step can gate without also having to parse it.
        return check && report.HasFindings ? 1 : 0;
    }

    private static int RunClaimsRelease(ClaimStore store, string? agent, string? kindToken, string? value, bool all, bool force)
    {
        if (agent is null)
        {
            Console.Error.WriteLine("--release requires --agent <id>");
            return ExitUnusable;
        }

        if (all)
        {
            var released = 0;
            foreach (var claim in store.All().Where(c => c.Agent == agent))
            {
                if (store.Release(claim.Kind, claim.Value, agent, force, out var why))
                {
                    Console.WriteLine(why);
                    released++;
                }
                else
                {
                    Console.Error.WriteLine(why);
                }
            }

            Console.WriteLine($"released {released} claim(s) for agent '{agent}'");
            return 0;
        }

        if (kindToken is null || value is null)
        {
            Console.Error.WriteLine("--release needs either --all or both --kind and --value");
            return ExitUnusable;
        }

        if (!ClaimKinds.TryParse(kindToken, out var kind))
        {
            Console.Error.WriteLine($"unknown --kind '{kindToken}' — expected one of: {ClaimKinds.AllTokens}");
            return ExitUnusable;
        }

        if (store.Release(kind, value, agent, force, out var reason))
        {
            Console.WriteLine(reason);
            return 0;
        }

        Console.Error.WriteLine(reason);
        return 1;
    }

    private static string? Next(string[] args, ref int i) => i + 1 < args.Length ? args[++i] : null;

    private static int RunSanitize(string[] args)
    {
        string? file = null;
        string? mapPath = null;
        string? outPath = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--map":
                    mapPath = RequireValue(args, ref i, "--map");
                    break;
                case "--out":
                    outPath = RequireValue(args, ref i, "--out");
                    break;
                default:
                    file ??= args[i];
                    break;
            }
        }

        if (file is null || mapPath is null || outPath is null)
        {
            Console.Error.WriteLine("Usage: converter sanitize <file> --map <mapping.json> --out <path>");
            return 1;
        }

        try
        {
            var document = XDocument.Load(file);
            var map = SanitizationMap.Load(mapPath);

            if (IsDbXml(document))
            {
                var db = DbSourceParser.Parse(document);
                var sanitizedDb = Sanitizer.ApplyToDb(db, map);
                var dbXml = DbSourceWriter.Write(sanitizedDb);
                dbXml.Save(outPath);
                Console.WriteLine($"{file} -> {outPath}");
                return 0;
            }

            if (IsTypeXml(document))
            {
                var type = PlcTypeSourceParser.Parse(document);
                var sanitizedType = Sanitizer.ApplyToType(type, map);
                var typeXml = PlcTypeSourceWriter.Write(sanitizedType);
                typeXml.Save(outPath);
                Console.WriteLine($"{file} -> {outPath}");
                return 0;
            }

            if (IsTagTableXml(document))
            {
                var tagTable = PlcTagTableSourceParser.Parse(document);
                var sanitizedTagTable = Sanitizer.ApplyToTagTable(tagTable, map);
                var tagTableXml = PlcTagTableSourceWriter.Write(sanitizedTagTable);
                tagTableXml.Save(outPath);
                Console.WriteLine($"{file} -> {outPath}");
                return 0;
            }

            var block = BlockSourceParser.Parse(document);
            var sanitized = Sanitizer.Apply(block, map);

            var flgNetworks = sanitized.CompileUnits.Select(u => u.Network).ToList();
            var compileUnitUIds = sanitized.CompileUnits.Select(u => u.UId).ToList();
            var networkTitles = sanitized.CompileUnits.Select(u => u.Title).ToList();
            var networkComments = sanitized.CompileUnits.Select(u => u.Comment).ToList();

            var xml = BlockSourceWriter.Write(sanitized, flgNetworks, compileUnitUIds, networkTitles, networkComments);
            xml.Save(outPath);
            Console.WriteLine($"{file} -> {outPath}");
            return 0;
        }
        catch (Exception ex) when (ex is SimaticMlFormatException or UnsupportedConstructException or SanitizationMapException)
        {
            Console.Error.WriteLine($"{file}: {ex.GetType().Name}: {ex.Message}");
            return 1;
        }
    }

    private static int RunReview(string[] args)
    {
        var files = new List<string>();
        var ignoreErrors = false;
        var json = false;
        var allowUnchecked = false;
        string? projectDir = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--ignore-errors":
                    ignoreErrors = true;
                    break;
                case "--json":
                    json = true;
                    break;
                case "--allow-unchecked":
                    allowUnchecked = true;
                    break;
                case "--project":
                    projectDir = RequireValue(args, ref i, "--project");
                    break;
                default:
                    files.Add(args[i]);
                    break;
            }
        }

        if (files.Count == 0)
        {
            Console.Error.WriteLine("Usage: converter review <file> [<file> ...] [--project <ir-dir>] [--ignore-errors] [--json] [--allow-unchecked]");
            return 1;
        }

        // --project (optional) enables the cross-file rules (C-118, FI-09): a UDT/DB/tag index over
        // the export, resolving the interface UDT a block's Step register lives in. Absent, C-118 is
        // reported NotApplicable per block — every other rule is single-file and unaffected.
        TagTypeRegistry? udtIndex = null;
        if (projectDir is not null)
        {
            if (!Directory.Exists(projectDir))
            {
                Console.Error.WriteLine($"--project directory not found: {projectDir}");
                return 1;
            }

            udtIndex = TagTypeRegistry.FromFiles(Directory.EnumerateFiles(projectDir, "*.ir"));
        }

        // The harness scope's corpus: the batch itself PLUS --project when given. The batch alone is
        // deliberately enough — reviewing a generated tag table together with the generated copy
        // layer that drives it is exactly how the harness invokes this — and --project widens it to
        // the whole export, which is the stronger question because it can see a PLANT reference the
        // batch omitted. There is no `--harness` flag and no way to assert a classification: see
        // HarnessScope for why a caller assertion was rejected outright.
        var harnessScope = HarnessScope.Build(
            projectDir is null ? files : files.Concat(Directory.EnumerateFiles(projectDir, "*.ir")));

        ReviewReport report;
        try
        {
            report = ReviewRunner.ReviewFiles(files, ignoreErrors, udtIndex, harnessScope);
        }
        catch (ReviewFileException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        Console.WriteLine(json ? ReviewOutputFormatter.FormatJson(report) : ReviewOutputFormatter.FormatTable(report));

        // Exit 2 = REVIEW INCOMPLETE: at least one rule had a subject in one of these files and was
        // not judged. Named on stderr as well as in the report, because the exit code is what a
        // caller acts on and the report is what a caller skims. See ReviewOutcome for why this is a
        // gate rather than a warning.
        var uncheckedRules = ReviewOutcome.UncheckedRules(report);
        if (uncheckedRules.Count > 0)
        {
            Console.Error.WriteLine(
                $"REVIEW INCOMPLETE: {uncheckedRules.Count} rule(s) were not checked ({string.Join(", ", uncheckedRules.Select(u => $"{u.RuleId} in {Path.GetFileName(u.FilePath)}").Distinct())}). "
                + "This is not a clean review. Supply what the rule needs (e.g. --project), or pass --allow-unchecked to accept the gap deliberately.");
        }

        return ReviewOutcome.ExitCode(report, allowUnchecked);
    }

    private static int RunDigest(string[] args)
    {
        var files = new List<string>();
        var ignoreErrors = false;
        var json = false;
        var fingerprint = false;

        foreach (var arg in args)
        {
            switch (arg)
            {
                case "--ignore-errors":
                    ignoreErrors = true;
                    break;
                case "--json":
                    json = true;
                    break;
                case "--fingerprint":
                    fingerprint = true;
                    break;
                default:
                    files.Add(arg);
                    break;
            }
        }

        if (files.Count == 0)
        {
            Console.Error.WriteLine("Usage: converter digest <file> [<file> ...] [--ignore-errors] [--json] [--fingerprint]");
            return 1;
        }

        DigestReport report;
        try
        {
            report = DigestBuilder.DigestFiles(files, ignoreErrors);
        }
        catch (DigestFileException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        Console.WriteLine(json ? DigestOutputFormatter.FormatJson(report) : DigestOutputFormatter.FormatText(report, fingerprint));

        return report.Files.Any(f => f.FileError is not null) ? 1 : 0;
    }

    private static int RunPreflight(string[] args)
    {
        var files = new List<string>();
        string? projectDir = null;
        var json = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--project":
                    projectDir = RequireValue(args, ref i, "--project");
                    break;
                case "--json":
                    json = true;
                    break;
                default:
                    files.Add(args[i]);
                    break;
            }
        }

        if (files.Count == 0 || projectDir is null)
        {
            Console.Error.WriteLine("Usage: converter preflight <file> [<file> ...] --project <ir-dir> [--json]");
            return 1;
        }

        if (!Directory.Exists(projectDir))
        {
            Console.Error.WriteLine($"--project directory not found: {projectDir}");
            return 1;
        }

        var report = PreflightRunner.Run(files, projectDir);
        Console.WriteLine(json ? PreflightOutputFormatter.FormatJson(report) : PreflightOutputFormatter.FormatText(report));

        return report.HasFindings ? 1 : 0;
    }

    private static int RunTagStatus(string[] args)
    {
        var names = new List<string>();
        string? projectDir = null;
        var json = false;
        var rootsOnly = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--project":
                    projectDir = RequireValue(args, ref i, "--project");
                    break;
                case "--json":
                    json = true;
                    break;
                case "--roots-only":
                    rootsOnly = true;
                    break;
                default:
                    names.Add(args[i]);
                    break;
            }
        }

        if (names.Count == 0 || projectDir is null)
        {
            Console.Error.WriteLine("Usage: converter tagstatus <name> [<name> ...] --project <ir-dir> [--json] [--roots-only]");
            return 1;
        }

        if (!Directory.Exists(projectDir))
        {
            Console.Error.WriteLine($"--project directory not found: {projectDir}");
            return 1;
        }

        var report = TagStatusRunner.Run(names, projectDir, rootsOnly);
        Console.WriteLine(json ? TagStatusOutputFormatter.FormatJson(report) : TagStatusOutputFormatter.FormatText(report));

        return report.HasBlocking ? 1 : 0;
    }

    private static int RunDiff(string[] args)
    {
        var paths = new List<string>();
        var onlyNetworks = new List<int>();
        var json = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--only":
                {
                    // Accept one or more network numbers after --only, space- and/or comma-separated
                    // (`--only 1 2`, `--only 1,2`), and --only may repeat. Consume following tokens while
                    // they parse as a network list; stop at the next flag or path. (Fixes the doc-vs-parser
                    // mismatch found in the gen-block-modify-fix validation, 2026-07-18 — the docs implied
                    // space-separated but the parser took only a single value.)
                    var consumedAny = false;
                    while (i + 1 < args.Length && TryParseNetworkList(args[i + 1], out var nets))
                    {
                        onlyNetworks.AddRange(nets);
                        i++;
                        consumedAny = true;
                    }

                    if (!consumedAny)
                    {
                        Console.Error.WriteLine("--only requires at least one network number (e.g. --only 1 2 or --only 1,2).");
                        return 1;
                    }

                    break;
                }
                case "--json":
                    json = true;
                    break;
                default:
                    paths.Add(args[i]);
                    break;
            }
        }

        if (paths.Count != 2)
        {
            Console.Error.WriteLine("Usage: converter diff <old.ir> <new.ir> [--only <network> ...] [--json]");
            return 1;
        }

        foreach (var path in paths)
        {
            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"File not found: {path}");
                return 1;
            }
        }

        DiffReport report;
        try
        {
            report = DiffRunner.Run(paths[0], paths[1], onlyNetworks);
        }
        catch (Exception ex) when (ex is IrFormatException or SimaticMlFormatException)
        {
            Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}");
            return 1;
        }

        Console.WriteLine(json ? DiffOutputFormatter.FormatJson(report) : DiffOutputFormatter.FormatText(report));

        // --only makes this an assertion: exit 1 if anything changed outside the declared set. With
        // no --only it's an informational report (exit 0) — a filter/inspection aid, never a gate on
        // its own.
        return report.HasInvarianceViolation ? 1 : 0;
    }

    private static int RunSignalSweep(string[] args)
    {
        string? projectDir = null;
        string? specs = null;
        string? register = null;
        string? unclaimed = null;
        var json = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--project":
                    projectDir = RequireValue(args, ref i, "--project");
                    break;
                case "--specs":
                    specs = RequireValue(args, ref i, "--specs");
                    break;
                case "--register":
                    register = RequireValue(args, ref i, "--register");
                    break;
                case "--unclaimed":
                    unclaimed = RequireValue(args, ref i, "--unclaimed");
                    break;
                case "--json":
                    json = true;
                    break;
                default:
                    Console.Error.WriteLine($"Unexpected argument: {args[i]}");
                    return 1;
            }
        }

        if (projectDir is null || specs is null)
        {
            Console.Error.WriteLine("Usage: converter signal-sweep --project <ir-dir> --specs <equipment-specs-dir> [--register <requirements.md>] [--unclaimed <unclaimed-signals.md>] [--json]");
            return 1;
        }

        if (!Directory.Exists(projectDir) || !Directory.Exists(specs))
        {
            Console.Error.WriteLine($"directory not found: {(Directory.Exists(projectDir) ? specs : projectDir)}");
            return 1;
        }

        foreach (var path in new[] { register, unclaimed }.Where(p => p is not null && !File.Exists(p)))
        {
            Console.Error.WriteLine($"file not found: {path}");
            return 1;
        }

        try
        {
            var report = SignalSweepRunner.Run(projectDir, specs, register, unclaimed);
            Console.WriteLine(json
                ? SignalSweepOutputFormatter.FormatJson(report)
                : SignalSweepOutputFormatter.FormatText(report));

            return report.HasFindings ? 1 : 0;
        }
        catch (SignalSweepFormatException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int RunRelationReconcile(string[] args)
    {
        string? specs = null;
        string? ledger = null;
        string? register = null;
        string? projectDir = null;
        var json = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--specs":
                    specs = RequireValue(args, ref i, "--specs");
                    break;
                case "--ledger":
                    ledger = RequireValue(args, ref i, "--ledger");
                    break;
                case "--register":
                    register = RequireValue(args, ref i, "--register");
                    break;
                case "--project":
                    projectDir = RequireValue(args, ref i, "--project");
                    break;
                case "--json":
                    json = true;
                    break;
                default:
                    Console.Error.WriteLine($"Unexpected argument: {args[i]}");
                    return 1;
            }
        }

        if (specs is null || ledger is null || register is null)
        {
            Console.Error.WriteLine("Usage: converter relation-reconcile --specs <equipment-specs-dir> --ledger <code-structure.md> --register <requirements.md> [--project <ir-dir>] [--json]");
            return 1;
        }

        if (!Directory.Exists(specs))
        {
            Console.Error.WriteLine($"--specs directory not found: {specs}");
            return 1;
        }

        foreach (var (label, path) in new[] { ("--ledger", ledger), ("--register", register) })
        {
            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"{label} file not found: {path}");
                return 1;
            }
        }

        if (projectDir is not null && !Directory.Exists(projectDir))
        {
            Console.Error.WriteLine($"--project directory not found: {projectDir}");
            return 1;
        }

        try
        {
            var report = RelationReconcileRunner.Run(specs, ledger, register, projectDir);
            Console.WriteLine(json
                ? RelationReconcileOutputFormatter.FormatJson(report)
                : RelationReconcileOutputFormatter.FormatText(report));

            // FI-44 — "empty is not clean". A leg that PARSED but shares no key with any other leg was
            // compared against nothing; that exits 2, distinct from both success and a real difference.
            // An ABSENT leg (a stopped D3) is a different thing and deliberately still does not gate.
            if (report.UncomparedLegs.Count > 0)
            {
                Console.Error.WriteLine(
                    "relation-reconcile: " +
                    string.Join(", ", report.UncomparedLegs.Select(u => u.Kind.ToString().ToLowerInvariant())) +
                    " leg(s) shared no relation key with any other leg - nothing was compared.");
                return 2;
            }

            return report.HasFindings ? 1 : 0;
        }
        catch (RelationReconcileFormatException ex)
        {
            // A leg that parsed nothing is a hard error, never a clean reconcile — a silent all-green
            // after format drift would make this check worse than not having it.
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int RunUndrivenScan(string[] args)
    {
        string? projectDir = null;
        string? fb = null;
        var instances = new List<string>();
        var callers = new List<string>();
        var json = false;
        var hints = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--project":
                    projectDir = RequireValue(args, ref i, "--project");
                    break;
                case "--fb":
                    fb = RequireValue(args, ref i, "--fb");
                    break;
                case "--instance":
                    var instance = RequireValue(args, ref i, "--instance");
                    if (instance is null)
                    {
                        return 1;
                    }

                    instances.Add(instance);
                    break;
                case "--caller":
                    var caller = RequireValue(args, ref i, "--caller");
                    if (caller is null)
                    {
                        return 1;
                    }

                    callers.Add(caller);
                    break;
                case "--hints":
                    hints = true;
                    break;
                case "--json":
                    json = true;
                    break;
                default:
                    Console.Error.WriteLine($"Unexpected argument: {args[i]}");
                    return 1;
            }
        }

        if (projectDir is null || fb is null)
        {
            Console.Error.WriteLine("Usage: converter undriven-scan --project <ir-dir> --fb <FBName> [--instance <iDB> ...] [--caller <file.ir> ...] [--hints] [--json]");
            return 1;
        }

        if (!Directory.Exists(projectDir))
        {
            Console.Error.WriteLine($"--project directory not found: {projectDir}");
            return 1;
        }

        foreach (var caller in callers.Where(c => !File.Exists(c)))
        {
            Console.Error.WriteLine($"--caller file not found: {caller}");
            return 1;
        }

        var report = UndrivenScanRunner.Run(projectDir, fb, instances, callers, hints);
        Console.WriteLine(json
            ? UndrivenScanOutputFormatter.FormatJson(report)
            : UndrivenScanOutputFormatter.FormatText(report));

        // FI-44 - "empty is not clean". A scan that examined nothing exits 2, distinct from both
        // success and from a real finding: the question was wrong, not the plant. Before this it
        // exited 0 and an FB that had never been written passed the check.
        if (report.ExaminedNothing)
        {
            Console.Error.WriteLine(report.Scope == ScanScope.UnknownBlock
                ? $"undriven-scan: no block named '{fb}' in {projectDir} - nothing was examined."
                : $"undriven-scan: block '{fb}' has no instances in {projectDir} - nothing was examined.");
            return 2;
        }

        // Hard facts only: an interface input with no armed writer. The name-join hints never gate.
        return report.HasFindings ? 1 : 0;
    }

    private static int RunCandidateScan(string[] args)
    {
        string? projectDir = null;
        string? fb = null;
        string? instance = null;
        string? type = null;
        var direction = "any";
        var scopes = new List<string>();
        var phrases = new List<string>();
        var json = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--project":
                    projectDir = RequireValue(args, ref i, "--project");
                    break;
                case "--fb":
                    fb = RequireValue(args, ref i, "--fb");
                    break;
                case "--instance":
                    instance = RequireValue(args, ref i, "--instance");
                    break;
                case "--type":
                    type = RequireValue(args, ref i, "--type");
                    break;
                case "--direction":
                    direction = RequireValue(args, ref i, "--direction") ?? "any";
                    break;
                case "--scope":
                    var scope = RequireValue(args, ref i, "--scope");
                    if (scope is null)
                    {
                        return 1;
                    }

                    scopes.Add(scope);
                    break;
                case "--phrase":
                    var phrase = RequireValue(args, ref i, "--phrase");
                    if (phrase is null)
                    {
                        return 1;
                    }

                    phrases.Add(phrase);
                    break;
                case "--json":
                    json = true;
                    break;
                default:
                    Console.Error.WriteLine($"Unexpected argument: {args[i]}");
                    return 1;
            }
        }

        if (projectDir is null || fb is null)
        {
            Console.Error.WriteLine("Usage: converter candidate-scan --project <ir-dir> --fb <FBName> [--instance <name>] [--scope <path-prefix> ...] [--type <TypeName>] [--direction status|command|any] [--phrase <word> ...] [--json]");
            return 1;
        }

        if (direction is not ("status" or "command" or "any"))
        {
            Console.Error.WriteLine($"--direction must be status, command or any (got '{direction}')");
            return 1;
        }

        if (!Directory.Exists(projectDir))
        {
            Console.Error.WriteLine($"--project directory not found: {projectDir}");
            return 1;
        }

        var report = CandidateScanRunner.Run(projectDir, fb, instance, scopes, type, direction, phrases);
        Console.WriteLine(json
            ? CandidateScanOutputFormatter.FormatJson(report)
            : CandidateScanOutputFormatter.FormatText(report));

        // FI-44 — a scope that matched nothing exits 2, distinct from both success and a real
        // finding. Before this it exited 0, and scoping to a piece of equipment on a plant whose
        // tags follow C-001 always matched nothing — so the check reported clean on the one binding
        // that was actually contested.
        if (report.ScopedButFoundNothing)
        {
            Console.Error.WriteLine(
                $"candidate-scan: scope [{string.Join(", ", report.Scopes)}] matched no IO signal in {projectDir} - nothing was examined. " +
                "A scope matching nothing is not evidence the binding is unambiguous.");
            return 2;
        }

        // Non-zero when the requirement could be satisfied by more than one signal — the mechanical
        // trigger that makes an ambiguous binding non-discretionary.
        return report.HasChoice ? 1 : 0;
    }

    private static int RunReuseScan(string[] args)
    {
        string? projectDir = null;
        var tags = new List<string>();
        var kinds = new List<string>();
        var json = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--project":
                    projectDir = RequireValue(args, ref i, "--project");
                    break;
                case "--tag":
                    var tag = RequireValue(args, ref i, "--tag");
                    if (tag is null)
                    {
                        return 1;
                    }

                    tags.Add(tag);
                    break;
                case "--kind":
                    var kind = RequireValue(args, ref i, "--kind");
                    if (kind is null)
                    {
                        return 1;
                    }

                    kinds.Add(kind);
                    break;
                case "--json":
                    json = true;
                    break;
                default:
                    Console.Error.WriteLine($"Unexpected argument: {args[i]}");
                    return 1;
            }
        }

        if (projectDir is null)
        {
            Console.Error.WriteLine("Usage: converter reuse-scan --project <ir-dir> [--tag <tag> ...] [--kind <kind> ...] [--json]");
            return 1;
        }

        if (!Directory.Exists(projectDir))
        {
            Console.Error.WriteLine($"--project directory not found: {projectDir}");
            return 1;
        }

        if (tags.Count == 0 && kinds.Count == 0)
        {
            Console.Error.WriteLine("reuse-scan needs at least one --tag or --kind to query for.");
            return 1;
        }

        var unknownKinds = kinds.Where(k => !ReuseScanRunner.KnownKinds.Contains(k)).ToList();
        if (unknownKinds.Count > 0)
        {
            Console.Error.WriteLine($"Unknown --kind value(s): {string.Join(", ", unknownKinds)}. Known kinds: {string.Join(", ", ReuseScanRunner.KnownKinds)}");
            return 1;
        }

        var report = ReuseScanRunner.Run(projectDir, tags, kinds);
        Console.WriteLine(json ? ReuseScanOutputFormatter.FormatJson(report) : ReuseScanOutputFormatter.FormatText(report));

        return report.HasMatches ? 1 : 0;
    }

    private static int RunIrHash(string[] args)
    {
        var json = false;
        var files = new List<string>();

        foreach (var arg in args)
        {
            if (arg == "--json")
            {
                json = true;
            }
            else if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                Console.Error.WriteLine($"Unexpected argument: {arg}");
                return 1;
            }
            else
            {
                files.Add(arg);
            }
        }

        if (files.Count == 0)
        {
            Console.Error.WriteLine("Usage: converter ir-hash <file> [<file> ...] [--json]");
            return 1;
        }

        var report = IrHashRunner.Run(files);
        Console.WriteLine(json ? IrHashOutputFormatter.FormatJson(report) : IrHashOutputFormatter.FormatText(report));

        return report.HasErrors ? 1 : 0;
    }

    private static int RunTargetScan(string[] args)
    {
        string? requirementsPath = null;
        string? projectDir = null;
        var json = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--requirements":
                    requirementsPath = RequireValue(args, ref i, "--requirements");
                    break;
                case "--project":
                    projectDir = RequireValue(args, ref i, "--project");
                    break;
                case "--json":
                    json = true;
                    break;
                default:
                    Console.Error.WriteLine($"Unexpected argument: {args[i]}");
                    return 1;
            }
        }

        if (requirementsPath is null || projectDir is null)
        {
            Console.Error.WriteLine("Usage: converter target-scan --requirements <register.md> --project <ir-dir> [--json]");
            return 1;
        }

        if (!File.Exists(requirementsPath))
        {
            Console.Error.WriteLine($"--requirements file not found: {requirementsPath}");
            return 1;
        }

        if (!Directory.Exists(projectDir))
        {
            Console.Error.WriteLine($"--project directory not found: {projectDir}");
            return 1;
        }

        var report = TargetScanRunner.Run(requirementsPath, projectDir);
        Console.WriteLine(json ? TargetScanOutputFormatter.FormatJson(report) : TargetScanOutputFormatter.FormatText(report));

        return report.HasCandidates ? 0 : 1;
    }

    // THE CONFIRM LOOP's judgement half (docs/notes/test-environment-build-plan.md, owner 2026-08-12).
    // Pure and in-process: `tools/confirm-roundtrip.ps1` owns Portal and the orchestration, FI-24 owns
    // the reason why.
    //
    // Exit codes follow the house convention already set by relation-reconcile/undriven-scan/
    // candidate-scan: 0 clean, 1 a real finding, 2 NOTHING WAS JUDGED. The third is the one that
    // matters here — a comparison that could not be made must never be indistinguishable from one
    // that passed (FI-44, "empty is not clean").
    internal static int RunCompare(string[] args)
    {
        var paths = new List<string>();
        var json = false;
        var allowSilentLayout = false;
        var maxDifferences = 50;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--json":
                    json = true;
                    break;
                case "--allow-silent-layout":
                    allowSilentLayout = true;
                    break;
                case "--max-differences":
                    var value = RequireValue(args, ref i, "--max-differences");
                    if (value is null)
                    {
                        return ExitUnusable;
                    }

                    if (!int.TryParse(value, out maxDifferences) || maxDifferences < 0)
                    {
                        Console.Error.WriteLine("--max-differences requires a non-negative integer (0 = show all).");
                        return ExitUnusable;
                    }

                    break;
                default:
                    if (args[i].StartsWith("--", StringComparison.Ordinal))
                    {
                        Console.Error.WriteLine($"Unknown flag '{args[i]}' for 'compare'. Valid flags: --json, --max-differences <n>, --allow-silent-layout.");
                        return ExitUnusable;
                    }

                    paths.Add(args[i]);
                    break;
            }
        }

        if (paths.Count != 2)
        {
            Console.Error.WriteLine("Usage: converter compare <first.xml> <second.xml> [--json] [--max-differences <n>] [--allow-silent-layout]");
            Console.Error.WriteLine("  Compares two SimaticML documents for semantic equivalence (Normalizer) and reports WHAT differs.");
            Console.Error.WriteLine("  Built for the confirm loop: the export BEFORE a round trip through TIA against the export AFTER it.");
            Console.Error.WriteLine("  exit 0 equivalent / 1 differs / 2 not compared.");
            return ExitUnusable;
        }

        var report = CompareRunner.Run(paths[0], paths[1], allowSilentLayout);
        var text = json
            ? CompareOutputFormatter.FormatJson(report)
            : CompareOutputFormatter.FormatText(report, maxDifferences);

        if (report.Status == CompareStatus.NotCompared)
        {
            Console.Error.WriteLine(text);
            return ExitUnusable;
        }

        Console.WriteLine(text);
        return report.Equivalent ? 0 : 1;
    }

    private static int RunCrossCheck(string[] args)
    {
        string? projectDir = null;
        var json = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--project":
                    projectDir = RequireValue(args, ref i, "--project");
                    break;
                case "--json":
                    json = true;
                    break;
                default:
                    Console.Error.WriteLine($"Unexpected argument: {args[i]}");
                    return 1;
            }
        }

        if (projectDir is null)
        {
            Console.Error.WriteLine("Usage: converter cross-check --project <ir-dir> [--json]");
            return 1;
        }

        if (!Directory.Exists(projectDir))
        {
            Console.Error.WriteLine($"--project directory not found: {projectDir}");
            return 1;
        }

        var report = CrossCheckRunner.Run(projectDir);
        Console.WriteLine(json ? CrossCheckOutputFormatter.FormatJson(report) : CrossCheckOutputFormatter.FormatText(report));

        // A facts provider, not a gate — always exit 0 (like the `converter review` dump the skills embed).
        return 0;
    }

    /// <summary>
    /// `conflict-graph` — the SUBMISSION-SCOPED emission `Harness.Results.SubmissionGate` gates 8 and
    /// 8c consume. Distinct from `cross-check` on purpose: that command emits whole-project FACT
    /// TABLES keyed on a storage path, and the gate consumes EDGES between BLOCKS carrying a
    /// provenance and a signal class. Reshaping one into the other was tried and correctly refused.
    ///
    /// <para>Exit codes are the refusal semantics, carried across from the consumer:
    /// <list type="bullet">
    /// <item><b>0</b> — computed. `conflictEdges` is present, and an EMPTY list is the earned claim
    /// that the graph ran and found nothing.</item>
    /// <item><b>1</b> — usage error.</item>
    /// <item><b>2</b> — NOT COMPUTED. The key is withheld entirely, so the gate reports NOT CHECKED
    /// and fails closed rather than reading an unearned empty list as a clean program.</item>
    /// <item><b>3</b> — computed, but at least one edge carries an Unstated provenance or class. The
    /// graph IS emitted; this exists because at the gate that state appears as a flat NOT CHECKED for
    /// the whole submission with nothing naming the cause, and the operator who can fix it is the one
    /// running this command.</item>
    /// </list></para>
    /// </summary>
    private static int RunConflictGraph(string[] args)
    {
        string? projectDir = null;
        string? submission = null;
        string? signalsFile = null;
        var json = false;
        var allowUnresolved = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--project":
                    projectDir = RequireValue(args, ref i, "--project");
                    break;
                case "--submission":
                    submission = RequireValue(args, ref i, "--submission");
                    break;
                case "--signals":
                    signalsFile = RequireValue(args, ref i, "--signals");
                    break;
                case "--allow-unresolved":
                    allowUnresolved = true;
                    break;
                case "--json":
                    json = true;
                    break;
                default:
                    Console.Error.WriteLine($"Unexpected argument: {args[i]}");
                    return 1;
            }
        }

        if (projectDir is null || (submission is null && signalsFile is null))
        {
            Console.Error.WriteLine("Usage: converter conflict-graph --project <ir-dir> (--submission <file> | --signals <file>) [--json] [--allow-unresolved]");
            return 1;
        }

        if (!Directory.Exists(projectDir))
        {
            Console.Error.WriteLine($"--project directory not found: {projectDir}");
            return 1;
        }

        List<string> signals = new();
        try
        {
            if (submission is not null)
            {
                signals.AddRange(ConflictGraphRunner.SignalsFromSubmission(submission));
            }

            if (signalsFile is not null)
            {
                signals.AddRange(ConflictGraphRunner.SignalsFromList(signalsFile));
            }
        }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException)
        {
            Console.Error.WriteLine($"could not read the signal set: {ex.GetType().Name}: {ex.Message}");
            return 1;
        }

        var report = ConflictGraphRunner.Run(projectDir, signals, allowUnresolved);
        Console.WriteLine(json ? ConflictGraphOutputFormatter.FormatJson(report) : ConflictGraphOutputFormatter.FormatText(report));

        if (!report.Computed)
        {
            Console.Error.WriteLine(
                "NOT COMPUTED — `conflictEdges` was NOT emitted, so gates 8/8c will report NOT CHECKED. "
                + "That is deliberate: an empty edge list is the positive claim that the graph ran and found nothing, and it has not been earned here. "
                + report.NotComputedReason);
            return 2;
        }

        if (report.WithoutRecordedProvenance.Count > 0)
        {
            Console.Error.WriteLine(
                $"{report.WithoutRecordedProvenance.Count} edge(s) carry an Unstated provenance or signal class. The graph IS emitted, but the consumer's "
                + "ProvenanceComplete is ALL-or-nothing, so gate 8c will report NOT CHECKED for the WHOLE submission and will not name which edge caused it.");
            return 3;
        }

        return 0;
    }

    private static int RunTrace(string[] args)
    {
        string? bindingPath = null;
        string? projectDir = null;
        var json = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--binding":
                    bindingPath = RequireValue(args, ref i, "--binding");
                    break;
                case "--project":
                    projectDir = RequireValue(args, ref i, "--project");
                    break;
                case "--json":
                    json = true;
                    break;
                default:
                    Console.Error.WriteLine($"Unexpected argument: {args[i]}");
                    return 1;
            }
        }

        if (bindingPath is null || projectDir is null)
        {
            Console.Error.WriteLine("Usage: converter trace --binding <bindings.json> --project <ir-dir> [--json]");
            return 1;
        }

        if (!File.Exists(bindingPath))
        {
            Console.Error.WriteLine($"--binding file not found: {bindingPath}");
            return 1;
        }

        if (!Directory.Exists(projectDir))
        {
            Console.Error.WriteLine($"--project directory not found: {projectDir}");
            return 1;
        }

        TraceReport report;
        try
        {
            report = TraceRunner.Run(bindingPath, projectDir);
        }
        catch (BindingFileException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        catch (System.Text.Json.JsonException ex)
        {
            Console.Error.WriteLine($"--binding file is not valid JSON: {ex.Message}");
            return 1;
        }

        Console.WriteLine(json ? TraceOutputFormatter.FormatJson(report) : TraceOutputFormatter.FormatText(report));

        // A facts provider, not a gate — exit 0 (candidate verdicts are for the reviewer to confirm).
        return 0;
    }

    private static int RunDriftCheck(string[] args)
    {
        string? projectDir = null;
        string? exportsDir = null;
        var json = false;
        var complete = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--project":
                    projectDir = RequireValue(args, ref i, "--project");
                    break;
                case "--exports":
                    exportsDir = RequireValue(args, ref i, "--exports");
                    break;
                case "--json":
                    json = true;
                    break;
                case "--complete":
                    complete = true;
                    break;
                default:
                    Console.Error.WriteLine($"Unexpected argument: {args[i]}");
                    return 1;
            }
        }

        if (projectDir is null || exportsDir is null)
        {
            Console.Error.WriteLine("Usage: converter drift-check --project <ir-dir> --exports <simatic-ml-dir> [--complete] [--json]");
            return 1;
        }

        if (!Directory.Exists(projectDir))
        {
            Console.Error.WriteLine($"--project directory not found: {projectDir}");
            return 1;
        }

        if (!Directory.Exists(exportsDir))
        {
            Console.Error.WriteLine($"--exports directory not found: {exportsDir}");
            return 1;
        }

        var report = DriftCheckRunner.Run(projectDir, exportsDir, complete);
        Console.WriteLine(json ? DriftCheckOutputFormatter.FormatJson(report) : DriftCheckOutputFormatter.FormatText(report));

        return report.HasDrift ? 1 : 0;
    }

    // Parses a single --only token: one network number or a comma-separated list ("1" or "1,2,3").
    // Returns false (so --only stops consuming) for anything not all-integer — the next flag or a path.
    private static bool TryParseNetworkList(string token, out List<int> networks)
    {
        networks = new List<int>();
        foreach (var part in token.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(part, out var n))
            {
                networks = new List<int>();
                return false;
            }

            networks.Add(n);
        }

        return networks.Count > 0;
    }

    private static string? RequireValue(string[] args, ref int i, string flag)
    {
        if (i + 1 >= args.Length)
        {
            Console.Error.WriteLine($"Flag '{flag}' requires a value.");
            return null;
        }

        i++;
        return args[i];
    }

    // FI-72. `--out <dir>` and the overwrite report exist because both converters write BESIDE their
    // input by default, so converting `X.xml` writes `X.ir` — which is right in the ordinary
    // export-and-read-back loop and destructive when the `.ir` beside it is hand-authored. It has now
    // silently overwritten hand-edited IR for two separate agents (one lost eight files, recovered
    // only because ir-hash could prove the readable content identical; the other lost its own review
    // snapshot mid-review).
    //
    // Not fixed by refusing to overwrite: overwriting is the normal case and the correct one, so a
    // refusal would break every routine loop and be turned off within a day. Fixed by making the
    // event VISIBLE at the moment it happens, and by giving the caller somewhere else to put the
    // output — which is what both agents actually needed and neither had.
    internal static string ResolveOutPath(string sourcePath, string extension, string? outDir)
    {
        var fileName = Path.GetFileNameWithoutExtension(sourcePath) + extension;
        if (outDir is null)
        {
            return Path.ChangeExtension(sourcePath, extension);
        }

        Directory.CreateDirectory(outDir);
        return Path.Combine(outDir, fileName);
    }

    private static void ReportWrite(string sourcePath, string outPath, bool overwrote) =>
        Console.WriteLine(overwrote
            ? $"{sourcePath} -> {outPath}  (OVERWROTE an existing file; pass --out <dir> to write elsewhere)"
            : $"{sourcePath} -> {outPath}");

    private static void ConvertToIr(
        string sourcePath, bool noSidecar, CalleeInterfaceRegistry callees, TagTypeRegistry tagTypes,
        string? outDir = null)
    {
        var document = XDocument.Load(sourcePath);

        if (IsDbXml(document))
        {
            var db = DbSourceParser.Parse(document);
            var dbIrText = DbIrSerializer.Serialize(db);
            var dbOutPath = ResolveOutPath(sourcePath, ".ir", outDir);
            var dbOutPathExisted = File.Exists(dbOutPath);
            File.WriteAllText(dbOutPath, dbIrText);
            ReportWrite(sourcePath, dbOutPath, dbOutPathExisted);
            return;
        }

        if (IsTypeXml(document))
        {
            var type = PlcTypeSourceParser.Parse(document);
            var typeIrText = TypeIrSerializer.Serialize(type);
            var typeOutPath = ResolveOutPath(sourcePath, ".ir", outDir);
            var typeOutPathExisted = File.Exists(typeOutPath);
            File.WriteAllText(typeOutPath, typeIrText);
            ReportWrite(sourcePath, typeOutPath, typeOutPathExisted);
            return;
        }

        if (IsTagTableXml(document))
        {
            var tagTable = PlcTagTableSourceParser.Parse(document);
            var tagTableIrText = TagTableIrSerializer.Serialize(tagTable);
            var tagTableOutPath = ResolveOutPath(sourcePath, ".ir", outDir);
            var tagTableOutPathExisted = File.Exists(tagTableOutPath);
            File.WriteAllText(tagTableOutPath, tagTableIrText);
            ReportWrite(sourcePath, tagTableOutPath, tagTableOutPathExisted);
            return;
        }

        var block = BlockSourceParser.Parse(document);

        var networks = new List<IrNetwork>();
        var sidecars = new List<NetworkSidecar>();

        foreach (var compileUnit in block.CompileUnits)
        {
            // Title drives the NETWORK line's own quoted label (S1 item 16, 2026-07-12) —
            // Comment is a genuinely separate field, carried through via `with` since
            // GraphReducer.Reduce doesn't need to know about it (it isn't consumed by reduction,
            // just threaded through to the output).
            var title = compileUnit.Title ?? string.Empty;
            var reduced = GraphReducer.Reduce(compileUnit.Network, networks.Count + 1, title, compileUnit.UId);
            networks.Add(reduced.Network with { Comment = compileUnit.Comment });
            sidecars.Add(reduced.Sidecar);
        }

        var irBlock = new IrBlock(
            block.RootUId, block.Kind, block.Name, block.Number, block.Language, block.Comment, networks, block.StaticMembers, block.TempMembers, block.Title,
            block.InputMembers, block.OutputMembers, block.InOutMembers, block.ConstantMembers, block.SecondaryType,
            block.MemoryLayout);

        // Derive-always (ADR-0005), corrected 2026-07-19: to-ir KEEPS the stored SIDECAR by default. A
        // real export can synthesise-but-diverge (e.g. array-index locals, Gap D — found in the
        // test-project001 FBs and MotorStarter), so "synthesis succeeds" is NOT proof the derived form
        // matches, and auto-omitting on that alone would silently corrupt such a block. Omitting is safe
        // only once the block is proven equivalent. `--no-sidecar` is the explicit opt-in — and, since the
        // ADR-0005 follow-on landed (2026-07-19), it VERIFIES that equivalence itself rather than trusting
        // the caller: it derives a sidecar from the readable form, rebuilds the SimaticML, and
        // Normalizer-compares it to the source export being converted, omitting the sidecar only if
        // semantically equivalent (else it errors and the block keeps its sidecar). Default (no flag) still
        // keeps the sidecar — flipping THAT to auto-omit-when-equivalent is a separate, deferred owner
        // decision.
        string irText;
        if (noSidecar)
        {
            irText = SynthesizeReadableVerified(irBlock, document, callees, tagTypes);
        }
        else
        {
            irText = IrSerializer.SerializeBlock(irBlock, sidecars);
        }

        var outPath = ResolveOutPath(sourcePath, ".ir", outDir);
        var overwrote = File.Exists(outPath);
        File.WriteAllText(outPath, irText);
        ReportWrite(sourcePath, outPath, overwrote);
    }

    // The verified `--no-sidecar` path (ADR-0005 follow-on, 2026-07-19). Produces the readable-only IR for a
    // block only after PROVING it safe to drop the stored sidecar: derive a sidecar from the readable form
    // exactly as `to-xml` later will (serialize readable -> re-parse -> synthesize), rebuild the SimaticML,
    // and semantically compare it (the Normalizer) to `sourceDocument` — the very export being converted, so
    // the comparison is self-consistent and staleness-immune. Equivalent => the readable-only text is safe to
    // return. Not even synthesizable, or synthesizes-but-diverges => throw, and the caller keeps the stored
    // sidecar. This is the safe replacement for the old IsSynthesizable guard, which proved only that
    // synthesis didn't throw — necessary but, per ADR-0005's CriticalCaveat, NOT sufficient (a real block can
    // synthesise-but-diverge on gaps the reference corpus never exercised).
    internal static string SynthesizeReadableVerified(
        IrBlock block, XDocument sourceDocument, CalleeInterfaceRegistry? callees, TagTypeRegistry? tagTypes)
    {
        var readableText = IrSerializer.SerializeBlockReadable(block);
        var reparsed = IrParser.ParseBlockWithoutSidecar(readableText);

        IReadOnlyList<NetworkSidecar> synthSidecars;
        try
        {
            synthSidecars = SidecarSynthesizer.SynthesizeBlock(reparsed, callees, tagTypes);
        }
        catch (UnsupportedSynthesisConstructException ex)
        {
            // Genuinely unsynthesizable (an unsupported construct or an unresolvable operand type) — the old
            // IsSynthesizable failure mode. Reframe with the --no-sidecar context, preserving the reason.
            throw new UnsupportedSynthesisConstructException(
                $"{block.Name}: --no-sidecar requested but the block is not synthesizable — cannot omit the sidecar (it would not round-trip). {ex.Message}");
        }

        // Compare on documents that have BOTH been through an XML parse. The source export was loaded from
        // disk, but BuildBlockXml yields an in-memory tree whose node shape (text/whitespace nodes) differs
        // from a freshly-parsed one — and XNode.DeepEquals (inside the Normalizer) is sensitive to that, so
        // comparing the in-memory tree directly reports a spurious mismatch. Serialize-then-parse the synth
        // so both sides match the trusted offline parity comparison exactly (SynthesisParityRunner loads the
        // export and the synth both from file).
        var synthXml = XDocument.Parse(BuildBlockXml(reparsed, synthSidecars).ToString());
        if (!Normalizer.AreSemanticallyEquivalent(sourceDocument, synthXml))
        {
            throw new UnsupportedSynthesisConstructException(
                $"{block.Name}: --no-sidecar requested but the derived form is NOT semantically equivalent to the " +
                "source export — the block is not yet fully derivable, so its sidecar cannot be safely omitted (keep it). " +
                "See ADR-0005 / docs/notes/converter-synthesis-gaps.md.");
        }

        return readableText;
    }

    // Builds the callee-interface registry for wired-CALL synthesis from the batch's own block .ir files
    // (only "BLOCK …" files — DBs/UDTs/tag-tables have no callable interface) plus any --project export.
    // A file that won't parse is skipped here; a wired CALL that actually needs it then fails with a
    // clear error at synthesis time.
    internal static CalleeInterfaceRegistry BuildCalleeRegistry(IEnumerable<string> files, string? projectDir)
    {
        var texts = new List<string>();
        foreach (var file in files)
        {
            if (File.Exists(file))
            {
                texts.Add(File.ReadAllText(file));
            }
        }

        if (projectDir is not null && Directory.Exists(projectDir))
        {
            foreach (var file in Directory.EnumerateFiles(projectDir, "*.ir"))
            {
                texts.Add(File.ReadAllText(file));
            }
        }

        var blocks = new List<IrBlock>();
        foreach (var text in texts)
        {
            if (!text.StartsWith("BLOCK ", StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                blocks.Add(IrParser.HasSidecarSection(text)
                    ? IrParser.ParseBlock(text).Block
                    : IrParser.ParseBlockWithoutSidecar(text));
            }
            catch (IrFormatException)
            {
                // Unparseable — skip; a wired CALL that needs this callee will error clearly at synthesis.
            }
        }

        return CalleeInterfaceRegistry.FromBlocks(blocks);
    }

    // FI-57 (2026-08-08). Converting a block WITHOUT `--project` silently mistypes every comparison
    // against a member of another DB.
    //
    // Measured: `DB_HmiCmd.Heartbeat <> 0`, where Heartbeat is a UInt, emits `SrcType=Int` with no
    // --project and `SrcType=UInt` with it. TIA then rejects the block:
    //     "The data type UInt of the actual parameter does not match the data type Int of the
    //      formal parameter"
    //
    // It is LOUD — the compile gate catches it — so nothing has ever shipped wrong on it. What it
    // costs is a whole import-and-compile cycle, every time, and it has now cost two separately.
    // Both agents reasonably concluded they had found a converter type-inference bug, because from
    // inside the block that is exactly what it looks like: the type is simply not knowable without
    // the other DB in scope, and nothing said so.
    //
    // So: say so. Warn when a block references a root this run cannot see, naming the roots. The
    // warning is advisory and never changes an exit code — a block that genuinely references
    // nothing external is silent, and one that does gets told what it is guessing about.
    internal static IReadOnlyCollection<string> WarnIfConvertingBlindToExternalTypes(string mode, string[] files, string? projectDir)
    {
        if (projectDir is not null)
        {
            return Array.Empty<string>();
        }

        var localRoots = new HashSet<string>(StringComparer.Ordinal);
        var referenced = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in files)
        {
            string text;
            try
            {
                text = File.ReadAllText(file);
            }
            catch (IOException)
            {
                continue;
            }

            // Everything this batch declares itself — its own blocks, DBs and types — is in scope
            // whatever else is missing.
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex
                         .Matches(text, @"^\s*(?:BLOCK\s+\w+|TYPE|DB|TAGTABLE)\s+""?([A-Za-z_]\w*)""?",
                             System.Text.RegularExpressions.RegexOptions.Multiline))
            {
                localRoots.Add(m.Groups[1].Value);
            }

            // A dotted reference whose root looks like a global container (a DB or an instance DB).
            // Deliberately narrow: roots that are plainly the block's own interface (IO, and
            // anything the block declares) are excluded below, so this does not fire on ordinary
            // local structure access.
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex
                         .Matches(text, @"\b((?:DB|iDB)_\w+)\."))
            {
                referenced.Add(m.Groups[1].Value);
            }
        }

        referenced.ExceptWith(localRoots);
        if (referenced.Count == 0)
        {
            return Array.Empty<string>();
        }

        var names = string.Join(", ", referenced.OrderBy(r => r, StringComparer.Ordinal).Take(6));
        var more = referenced.Count > 6 ? $" (+{referenced.Count - 6} more)" : string.Empty;

        Console.Error.WriteLine(
            $"WARNING: converted without --project, so member types in {names}{more} could not be " +
            "resolved. Comparisons against them fall back to a type inferred from the literal, which " +
            "TIA rejects when the real member is unsigned. Re-run with --project <ir-dir> to type them.");

        return referenced;
    }

    // Builds the tag/member-type registry for typed box/compare synthesis from the batch's own .ir
    // files plus any --project export. TagTypeRegistry.FromFiles picks out the DB/UDT/tag-table files
    // (a block file contributes no operand types) and skips anything unparseable — an operand whose
    // type can't be resolved simply falls back to the builder's default rather than erroring here.
    internal static TagTypeRegistry BuildTagTypeRegistry(IEnumerable<string> files, string? projectDir)
    {
        var paths = new List<string>();
        foreach (var file in files)
        {
            if (File.Exists(file))
            {
                paths.Add(file);
            }
        }

        if (projectDir is not null && Directory.Exists(projectDir))
        {
            paths.AddRange(Directory.EnumerateFiles(projectDir, "*.ir"));
        }

        return TagTypeRegistry.FromFiles(paths);
    }

    private static void ConvertToXml(
        string sourcePath, bool synthesize = false,
        CalleeInterfaceRegistry? callees = null, TagTypeRegistry? tagTypes = null, string? outDir = null)
    {
        var irText = File.ReadAllText(sourcePath);
        var xml = BuildXmlFromIrText(irText, synthesize, callees, tagTypes);

        var outPath = ResolveOutPath(sourcePath, ".xml", outDir);
        var overwrote = File.Exists(outPath);
        xml.Save(outPath);
        ReportWrite(sourcePath, outPath, overwrote);
    }

    // The full `.ir` text -> SimaticML XDocument dispatch, in-memory (no disk write). One code path for
    // every kind (DB / UDT / tag table / code block), shared by `to-xml` (which saves the result) and
    // `drift-check` (which Normalizer-compares it to a committed export). Block sidecar decision matches
    // ConvertToXml's original: `--synthesize` OR a sidecar-less input derives the sidecar (ADR-0005);
    // a stored SIDECAR is used as-is. Parse/synthesis exceptions propagate to the caller.
    internal static XDocument BuildXmlFromIrText(
        string irText, bool synthesize, CalleeInterfaceRegistry? callees, TagTypeRegistry? tagTypes)
    {
        if (irText.StartsWith("DB ", StringComparison.Ordinal))
        {
            return DbSourceWriter.Write(DbIrParser.ParseDb(irText));
        }

        if (irText.StartsWith("TYPE ", StringComparison.Ordinal))
        {
            return PlcTypeSourceWriter.Write(TypeIrParser.ParseType(irText));
        }

        if (irText.StartsWith("TAGTABLE ", StringComparison.Ordinal))
        {
            return PlcTagTableSourceWriter.Write(TagTableIrParser.ParseTagTable(irText));
        }

        IrBlock block;
        IReadOnlyList<NetworkSidecar> sidecars;
        if (synthesize || !IrParser.HasSidecarSection(irText))
        {
            block = IrParser.ParseBlockWithoutSidecar(irText);
            sidecars = SidecarSynthesizer.SynthesizeBlock(block, callees, tagTypes);
        }
        else
        {
            (block, sidecars) = IrParser.ParseBlock(irText);
        }

        return BuildBlockXml(block, sidecars);
    }

    // Rebuild the SimaticML XDocument for a block from its (stored or synthesized) sidecars — the shared
    // block->XML step used by both `to-xml` and the verified `--no-sidecar` equivalence check, so the two
    // paths produce byte-for-byte the same output from the same inputs.
    internal static XDocument BuildBlockXml(IrBlock block, IReadOnlyList<NetworkSidecar> sidecars)
    {
        var flgNetworks = new List<FlgNetwork>();
        var compileUnitUIds = new List<string>();
        var networkTitles = new List<string?>();
        var networkComments = new List<string?>();

        for (var i = 0; i < block.Networks.Count; i++)
        {
            var sidecar = sidecars[i];
            flgNetworks.Add(FlgNetBuilder.Build(block.Networks[i], sidecar));
            compileUnitUIds.Add(sidecar.CompileUnitUId);
            networkTitles.Add(string.IsNullOrEmpty(block.Networks[i].Title) ? null : block.Networks[i].Title);
            networkComments.Add(block.Networks[i].Comment);
        }

        // A static named as an INSTANCE is a MULTI-INSTANCE and never carries `Remanence` (TIA: "The
        // attribute 'Remanence' cannot be set"). Derived here because this is the last place the IR
        // networks and the interface are both in hand. Subscript stripped, same normalisation as
        // SidecarSynthesizer.ScopeFor. Timers are unaffected — they are TimerBindings, not Calls, and
        // their full shape (VERSION/SETPOINT/Remanence) is confirmed real.
        //
        // A CALL is not the only thing that names an instance (2026-08-12): every FIXED-SHAPE
        // instruction takes one too — `MB_SERVER`/`MB_MASTER`/`MB_COMM_LOAD` and the two older Modbus
        // spellings. Counting only Calls meant hand-authored IR declaring `MB_Server : MB_SERVER
        // VERSION 5.3` emitted `Remanence` and was refused at import — a mystery rejection rather than
        // a known consequence, on a block that converted and preflighted clean.
        var multiInstanceStatics = new HashSet<string>(StringComparer.Ordinal);

        void AddInstanceRoot(string? instancePath)
        {
            if (instancePath is not { } path || path.Length == 0)
            {
                return;
            }

            var head = path.Split('.')[0];
            var subscript = head.IndexOf('[');
            multiInstanceStatics.Add(subscript >= 0 ? head[..subscript] : head);
        }

        foreach (var call in block.Networks.SelectMany(n => n.Calls))
        {
            AddInstanceRoot(call.InstancePath);
        }

        foreach (var fixedShape in block.Networks.SelectMany(n => n.FixedShapes))
        {
            AddInstanceRoot(fixedShape.InstancePath);
        }

        foreach (var modbusMaster in block.Networks.SelectMany(n => n.ModbusMasters))
        {
            AddInstanceRoot(modbusMaster.InstancePath);
        }

        foreach (var commLoad in block.Networks.SelectMany(n => n.ModbusCommLoads))
        {
            AddInstanceRoot(commLoad.InstancePath);
        }

        var blockSource = new BlockSource(
            block.RootUId, block.Kind, block.Name, block.Number, block.Language, block.Comment, Array.Empty<CompileUnitSource>(), block.StaticMembers, block.TempMembers, block.Title,
            block.InputMembers, block.OutputMembers, block.InOutMembers, block.ConstantMembers, block.SecondaryType,
            multiInstanceStatics.ToList(), block.MemoryLayout);
        return BlockSourceWriter.Write(blockSource, flgNetworks, compileUnitUIds, networkTitles, networkComments);
    }
}
