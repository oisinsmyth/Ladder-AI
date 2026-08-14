using System.Text.Json;
using System.Text.Json.Serialization;
using DeviceGuard;
using Harness.Gate;
using Harness.Loop;
using Harness.Map;
using Harness.Results;
using Harness.Wire;

namespace Harness.Run;

/// <summary>Exit codes. <b>Read the report, not the code</b> — but a caller that only has the code must not be misled.</summary>
public static class LoopExit
{
    /// <summary>The wave ran. <b>Not "the block passed"</b> — read the packages.</summary>
    public const int Ran = 0;

    /// <summary>The loop stopped before the wave, for a reason it names. Nothing about the block was tested.</summary>
    public const int DidNotRun = 1;

    /// <summary>Usage, or an input that could not be read. <b>Nothing was examined.</b></summary>
    public const int NothingExamined = 2;

    /// <summary>The device fence refused the target. <b>No socket was opened.</b></summary>
    public const int Refused = 3;

    /// <summary>
    /// <c>--generate-only</c>: the copy layer was generated, the submission is ADMISSIBLE, and
    /// <b>nothing was deployed</b>.
    ///
    /// <para>Deliberately the same value as <see cref="Ran"/> — both mean "the thing you asked for
    /// happened" — but named separately, because a caller reading 0 from a generate-only run has NOT run
    /// a wave and has learned nothing about any block.</para>
    /// </summary>
    public const int Generated = 0;

    /// <summary>
    /// 🔴 <c>--generate-only</c>: <b>the IR was produced AND the submission is NOT ADMISSIBLE.</b>
    ///
    /// <para>Its own code, and not <see cref="Generated"/>, because the gate that would have stopped a
    /// deploying run did not stop this one. The IR is there to be READ; nothing may be deployed from a run
    /// that exits here, and a caller keying on 0 cannot reach this by accident.</para>
    /// </summary>
    public const int GeneratedNotAdmissible = 4;
}

/// <summary>
/// 🔴 <b><c>harness-run</c> — the last link in <i>spec → map → IR → deploy → run → results</i>.</b>
///
/// <para><c>Harness.Loop</c> was a library with no entry point: the only executables were Gate, RigRead
/// and RigWrite, so the loop could be unit-tested and could not be RUN. This is the composition root
/// that takes a submission, a binding, a device and a mode, and writes the result package out.</para>
///
/// <para><b>Every decision lives here rather than in <c>Program</c></b>, and the transport arrives as a
/// factory, so the whole CLI is exercisable without a process and without a socket — a decision only
/// reachable through a process is a decision nobody tests.</para>
///
/// <para>*** THE FENCE RUNS BEFORE THE SOCKET. *** <see cref="DeviceAccessGuard"/> is consulted first
/// and a refusal returns without a connection attempt, because a check performed after the bytes have
/// moved authorizes nothing. With no allowlist resolved, every target is refused.</para>
///
/// <para><b>There is no fake gateway here and no flag that produces one.</b> The two modes are
/// <c>--verify</c> (<see cref="VerifyingDeviceGateway"/>: read the build stamp off the device, refuse on
/// any mismatch, change nothing) and the default refusal. A gateway that reports a deployment nobody
/// performed is one edit from a gateway that lies, so the only way to reach <c>Loaded</c> from this
/// binary is a measured stamp match.</para>
/// </summary>
public static class LoopCli
{
    public static int Run(
        IReadOnlyList<string> args,
        TextWriter output,
        Func<string, string> readFile,
        Action<string, string> writeFile,
        Func<string, int, byte, IRegisterTransport>? connect = null,
        Func<string, string?>? env = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(readFile);
        ArgumentNullException.ThrowIfNull(writeFile);

        if (args.Count == 0 || Option(args, "--submission") is null)
        {
            Usage(output);
            return LoopExit.NothingExamined;
        }

        var submissionPath = Option(args, "--submission")!;
        var bindingPath = Option(args, "--binding");
        var host = Option(args, "--host");
        var port = int.TryParse(Option(args, "--port"), out var p) ? p : 502;
        var unit = byte.TryParse(Option(args, "--unit"), out var u) ? u : (byte)1;
        var outPath = Option(args, "--out");
        var verify = args.Contains("--verify");
        var generateOnly = args.Contains("--generate-only");
        var emitDir = Option(args, "--emit");

        // *** THE TWO MODES ARE MUTUALLY EXCLUSIVE, AND THE REFUSAL NAMES WHY. *** --verify reads a build
        // stamp OFF A DEVICE; --generate-only stops before any gateway is constructed. A run that claimed
        // both would have to open a socket to satisfy one of them.
        if (generateOnly && verify)
        {
            output.WriteLine("NOTHING EXAMINED — --generate-only and --verify are mutually exclusive. --generate-only stops after the");
            output.WriteLine("copy layer is generated and constructs no gateway at all; --verify reads the build stamp OFF THE DEVICE.");
            return LoopExit.NothingExamined;
        }

        if (bindingPath is null)
        {
            output.WriteLine("NOTHING EXAMINED — --binding is required. The bindings say which signal each register carries, and");
            output.WriteLine("their TYPES decide both the mirror tag and the rung shape. There is no default: a guessed binding");
            output.WriteLine("produces a copy layer that compiles and mirrors the wrong things.");
            return LoopExit.NothingExamined;
        }

        // 🔴 *** READ SEPARATELY SO A FAILURE NAMES THE RIGHT FILE. *** Measured: a six-slot binding whose
        // slots repeat the same result tags threw from the MAP construction, and one shared try/catch
        // reported it as "could not read <the SUBMISSION>" — blaming the artifact that was fine while the
        // faulty one went unnamed. An operator then reads and re-reads a correct file.
        SubmissionDocument submission;
        try
        {
            submission = SubmissionDocument.Read(readFile(submissionPath));
        }
        catch (Exception ex)
        {
            output.WriteLine($"NOTHING EXAMINED — could not read the SUBMISSION '{submissionPath}': {ex.GetType().Name}: {ex.Message}");
            return LoopExit.NothingExamined;
        }

        BindingDocument binding;
        try
        {
            binding = BindingDocument.Read(readFile(bindingPath));
        }
        catch (Exception ex)
        {
            output.WriteLine($"NOTHING EXAMINED — could not read the BINDING '{bindingPath}': {ex.GetType().Name}: {ex.Message}");
            return LoopExit.NothingExamined;
        }

        // ---- GENERATE AND STOP ----------------------------------------------------------------------
        // Before the fence, because there is nothing to fence: no gateway is constructed on this path and
        // no host is read. It sits here rather than after the fence so that the absence is structural
        // rather than a flag somebody could reorder past.
        if (generateOnly)
            return GenerateOnly(submissionPath, bindingPath, submission, binding, emitDir, output, writeFile);

        // ---- THE FENCE, BEFORE ANYTHING OPENS A SOCKET ----------------------------------------------
        if (verify)
        {
            if (host is null)
            {
                output.WriteLine("NOTHING EXAMINED — --verify needs --host. Verification reads the build stamp OFF THE DEVICE; there is");
                output.WriteLine("no offline form of it, because a stamp nobody read is not evidence.");
                return LoopExit.NothingExamined;
            }

            var allowlist = AllowlistPath.Resolve(Option(args, "--allowlist"), env ?? Environment.GetEnvironmentVariable);
            if (allowlist is null)
            {
                output.WriteLine($"REFUSED — no allowlist configured. Pass --allowlist <path> or set {AllowlistPath.EnvVar}.");
                output.WriteLine("With neither, every target is refused and NO SOCKET IS OPENED. There is deliberately no default path:");
                output.WriteLine("a default file location is a place a production device quietly accumulates.");
                return LoopExit.Refused;
            }

            var decision = DeviceAccessGuard.FromPath(allowlist).Check(host);
            if (!decision.Allowed)
            {
                output.WriteLine($"REFUSED — the device fence refused '{host}': {decision.Reason} — {decision.Message}");
                output.WriteLine("No socket was opened. The fence runs BEFORE the connection, because a check performed after the bytes");
                output.WriteLine("have moved authorizes nothing.");
                return LoopExit.Refused;
            }

            output.WriteLine($"device fence: ALLOWED {host}:{port} unit {unit}  (allowlist {allowlist})");
        }

        // ---- BUILD THE REQUEST ----------------------------------------------------------------------
        LoopRequest request;
        try
        {
            request = Compose(submission, binding);
        }
        catch (Exception ex)
        {
            output.WriteLine($"NOTHING EXAMINED — the submission and binding could not be COMPOSED: {ex.GetType().Name}: {ex.Message}");
            output.WriteLine($"  submission: {submissionPath}");
            output.WriteLine($"  binding   : {bindingPath}");
            output.WriteLine("  Both files PARSED; what failed is building the map and request from them, so this is a fault in what they");
            output.WriteLine("  SAY rather than in how they are written. A known case: several slots declaring the SAME result tags — the");
            output.WriteLine("  map is one dictionary keyed by signal across all slots, so identical tags collide. That is a BINDING fault.");
            return LoopExit.NothingExamined;
        }

        // ---- THE GATEWAY, AND THERE ARE ONLY TWO ----------------------------------------------------
        using IDeviceGateway gateway = verify
            ? new VerifyingDeviceGateway(
                MapAllocator.Allocate(new WaveSetRequest(request.Geometry, request.Slots)).Require(),
                () => (connect ?? DefaultConnect)(host!, port, unit),
                request.WordOrder)
            : new RefusingDeviceGateway();

        output.WriteLine(verify
            ? "gateway     : VERIFYING — reads the build stamp off the device and refuses any mismatch. Imports nothing, compiles"
              + Environment.NewLine + "              nothing, downloads nothing, writes nothing. `Loaded` comes from the measurement and from nowhere else."
            : "gateway     : REFUSING — no device work is configured, so the loop will stop at deployment and no wave will run."
              + Environment.NewLine + "              Pass --verify --host <ip> to run against a device already carrying this build.");
        output.WriteLine();

        var result = LoopRun.Execute(request, gateway, () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        Write(result, output);

        if (outPath is not null)
        {
            writeFile(outPath, Render(result));
            output.WriteLine();
            output.WriteLine($"WRITTEN: {outPath}");
        }

        return result.Outcome == LoopOutcome.Ran ? LoopExit.Ran : LoopExit.DidNotRun;
    }

    /// <summary>The real transport. Behind a factory so every test above reaches none of it.</summary>
    private static IRegisterTransport DefaultConnect(string host, int port, byte unit) =>
        NModbusTransport.Connect(host, port, unit);

    /// <summary>
    /// 🔴 <b><c>--generate-only</c>: derive → gate → width → GENERATE → 0.1b, then STOP and print the IR.</b>
    ///
    /// <para><b>This path constructs no gateway and reads no host.</b> That is the point: until it
    /// existed, the only way to obtain a copy layer was through <c>LoopRun.Execute</c>, which hands what
    /// it generates straight to a device. Reviewing the emitted IR, comparing two generations, or
    /// measuring the mirror's width all needed a device fence satisfied first, for work that touches no
    /// device — <i>and a missing seam is a finding in its own right, not a convenience gap.</i></para>
    ///
    /// <para><b>It prints the MIRROR WIDTH and a LATCH INVENTORY</b>, because both are what a reader has
    /// to decide on: the width says whether a re-deploy is needed and how big, and the inventory says, per
    /// latch, whether the copy layer clears it or the client must.</para>
    /// </summary>
    private static int GenerateOnly(
        string submissionPath,
        string bindingPath,
        SubmissionDocument submission,
        BindingDocument binding,
        string? emitDir,
        TextWriter output,
        Action<string, string> writeFile)
    {
        LoopRequest request;
        try
        {
            request = Compose(submission, binding);
        }
        catch (Exception ex)
        {
            output.WriteLine($"NOTHING EXAMINED — the submission and binding could not be COMPOSED: {ex.GetType().Name}: {ex.Message}");
            output.WriteLine($"  submission: {submissionPath}");
            output.WriteLine($"  binding   : {bindingPath}");
            return LoopExit.NothingExamined;
        }

        output.WriteLine("mode        : GENERATE ONLY — derive, gate, width, generate, 0.1b. NO GATEWAY IS CONSTRUCTED, no host is read,");
        output.WriteLine("              nothing is imported, compiled or downloaded. The exit code says the IR was PRODUCED, never that");
        output.WriteLine("              anything ran.");
        output.WriteLine();

        // *** THE GATE STILL RUNS AND ITS VERDICT IS STILL REPORTED; WHAT IT DOES NOT DO IS STOP THIS. ***
        // The gate's contract is that an inadmissible submission COSTS NOTHING beyond it — no copy layer,
        // no deployment, no transport — and this path spends none of those. It is also a verdict about the
        // VECTORS, while the copy layer is a function of the BINDING alone. The price of suppressing the
        // stop is paid below: the whole verdict is printed, and the exit code is one a deployable run
        // cannot produce.
        var generation = LoopRun.Generate(request, stopWhenInadmissible: false);

        if (!generation.Generated)
        {
            output.WriteLine($"NOT GENERATED: {generation.Stopped}");
            output.WriteLine($"  {generation.Detail}");

            foreach (var refusal in generation.Refusals)
                output.WriteLine("  - " + refusal);

            return LoopExit.DidNotRun;
        }

        var admissible = generation.Gate?.Verdict == SubmissionVerdict.AdmissibleSubjectToJudgement;

        output.WriteLine($"SUBMISSION GATE: {generation.Gate?.Verdict.ToString() ?? "<not evaluated>"}"
                         + $"  ({generation.Gate?.Refused.Count ?? 0} refused, {generation.Gate?.NotChecked.Count ?? 0} could not run)");

        foreach (var refused in generation.Gate?.Refused ?? Array.Empty<GateResult>())
            output.WriteLine($"  REFUSED     {refused.Gate}: {refused.Detail}");

        foreach (var notChecked in generation.Gate?.NotChecked ?? Array.Empty<GateResult>())
            output.WriteLine($"  NOT CHECKED {notChecked.Gate}: {notChecked.Detail}");

        if (!admissible)
        {
            output.WriteLine();
            output.WriteLine("🔴 THE GATE DID NOT ADMIT THIS SUBMISSION, AND IT DID NOT STOP THIS GENERATION EITHER.");
            output.WriteLine("   NOTHING MAY BE DEPLOYED FROM THIS RUN. The IR below is for READING. A deploying run re-runs the");
            output.WriteLine("   gate with the stop in place and would not get this far, so the only remaining check on what follows");
            output.WriteLine("   is the person reading it. Exit code is 4, never 0.");
        }

        output.WriteLine();

        var plan = generation.Require();
        var map = generation.Map!;

        output.WriteLine($"GENERATED — {generation.Detail}");
        output.WriteLine();
        output.WriteLine($"MIRROR WIDTH: {map.TotalRegisters} register(s) at %M{map.Geometry.BaseByte}"
                         + $"  [control {map.Control.Length}, vectors {map.VectorBlock.Length}, results {map.ResultBlock.Length}]");
        output.WriteLine($"BUILD STAMP : {generation.Stamp.Literal}");
        output.WriteLine($"MAP HASH    : {map.MapHash}");
        output.WriteLine();

        // *** THE LATCH INVENTORY, PRINTED ON EVERY RUN INCLUDING THE EMPTY ONE. *** A report that appears
        // only when there is something to say teaches a reader that its absence means it was not run.
        var latches = plan.Networks
            .Where(n => n.Kind == CopyLayerNetworkKind.ResultLatch)
            .SelectMany(n => n.LatchRungs)
            .ToArray();

        output.WriteLine($"LATCHES: {latches.Length}");
        foreach (var latch in latches)
        {
            output.WriteLine(latch.PhaseArmed
                ? $"  {latch.LatchTag}  PHASE-ARMED  set on [{string.Join(" AND ", latch.ArmTerms)}] AND {latch.Signal}; reset on NOT {latch.ClearLevel}"
                : $"  {latch.LatchTag}  UNCONDITIONAL  set on {latch.Signal}; NOT reset by the copy layer — THE CLIENT must clear it during inert");
        }

        if (latches.Length == 0)
            output.WriteLine("  (none — no result source in this binding is declared transient)");

        output.WriteLine();

        foreach (var obj in generation.Objects)
        {
            output.WriteLine($"----- {obj.Kind} {obj.Name} -----");
            output.WriteLine(obj.Ir.TrimEnd('\n'));
            output.WriteLine();
        }

        if (emitDir is not null)
        {
            foreach (var obj in generation.Objects)
            {
                var path = Path.Combine(emitDir, obj.Name + ".ir");
                writeFile(path, obj.Ir);
                output.WriteLine($"WRITTEN: {path}");
            }
        }

        return admissible ? LoopExit.Generated : LoopExit.GeneratedNotAdmissible;
    }

    private static LoopRequest Compose(SubmissionDocument submission, BindingDocument binding)
    {
        var gateReport = GateCli.Evaluate(submission);

        var vectors = (submission.Vectors ?? new List<VectorDocument>()).Select(GateCli.ToSubmissionVector).ToArray();

        var bindings = (binding.Slots ?? new List<SlotBindingDocument>())
            .Select(s => new SlotBinding(
                s.SlotId ?? string.Empty,
                Signals(s.VectorTargets),
                s.StartCondition,
                Signals(s.ResultSources)))
            .ToArray();

        if (bindings.Length == 0)
            throw new InvalidDataException("the binding document names no slots. Empty is not clean: a loop with nothing bound would generate a copy layer that mirrors nothing.");

        var slots = bindings
            .Select(b => new SlotRequest(
                b.SlotId,
                Math.Max(1, b.VectorRegistersNeeded),
                Math.Max(1, b.ResultRegistersNeeded)))
            .ToArray();

        _ = gateReport; // the loop re-runs the gate itself; this only proves the document parses.

        return new LoopRequest(
            vectors,
            GateCli.ToEnumeration(submission),
            GateCli.ToFidelity(submission),
            new AgentIdentity(submission.BlockAuthor ?? string.Empty),
            GateCli.ToConflicts(submission),
            MirrorGeometry.ForCpu1214C(
                retentiveBytes: binding.RetentiveBytes ?? 256,
                baseByte: binding.BaseByte ?? 1000),
            slots,
            bindings,
            new CopyLayerNaming(
                binding.BlockName ?? "FC_HarnessCopyLayer",
                binding.BlockNumber ?? 0,
                binding.TagTableName ?? "HarnessMirror",
                binding.TagPrefix ?? "HX_"),
            Array.Empty<HarnessObject>(),
            RuntimeCompression: new RuntimeCompression(Math.Max(1, submission.RuntimeCompression)),
            CompressionInputs: null,
            Deployment: GateCli.ToDeploymentDeclaration(submission),
            TagMapReach: null);
    }

    private static IReadOnlyList<MirroredSignal> Signals(List<MirroredSignalDocument>? rows) =>
        (rows ?? new List<MirroredSignalDocument>())
        .Select(GateCli.ToMirroredSignal)
        .ToArray();

    private static void Write(LoopResult result, TextWriter output)
    {
        output.WriteLine($"OUTCOME: {result.Outcome}");
        output.WriteLine($"  {result.Detail}");
        output.WriteLine();

        if (result.Packages.Count == 0)
        {
            output.WriteLine("NO PACKAGES — nothing about the block was tested. That is a statement about the RUN, not about the block.");
            return;
        }

        output.WriteLine("PACKAGES");
        foreach (var package in result.Packages)
        {
            output.WriteLine("  " + package.Summary());
            output.WriteLine("      " + package.WhatToDoNext);
        }

        var conclusive = result.Packages.Count(p => p.ConclusiveAboutTheBlock);
        output.WriteLine();
        output.WriteLine($"{conclusive} of {result.Packages.Count} package(s) say anything about the block at all.");
    }

    private static string Render(LoopResult result) =>
        JsonSerializer.Serialize(new
        {
            outcome = result.Outcome.ToString(),
            detail = result.Detail,
            packages = result.Packages.Select(p => new
            {
                vector = p.VectorId,
                verdict = p.Verdict.ToString(),
                conclusive = p.ConclusiveAboutTheBlock,
                whatToDoNext = p.WhatToDoNext,
                caveats = p.Stamp.Caveats,
                assertions = p.Assertions.Select(a => new { a.AssertionId, a.Signal, a.Expected, a.Observed, state = a.State.ToString() }),
            }),
            caveats = result.Caveats.Select(c => new { c.Id, c.Detail }),
        }, new JsonSerializerOptions { WriteIndented = true });

    private static string? Option(IReadOnlyList<string> args, string name)
    {
        var index = args.ToList().IndexOf(name);
        return index >= 0 && index + 1 < args.Count ? args[index + 1] : null;
    }

    private static void Usage(TextWriter output)
    {
        output.WriteLine("usage: harness-run --submission <submission.json> --binding <binding.json>");
        output.WriteLine("                   [--generate-only [--emit <dir>]]");
        output.WriteLine("                   [--verify --host <ip> [--port 502] [--unit 1] [--allowlist <path>]]");
        output.WriteLine("                   [--out <result.json>]");
        output.WriteLine();
        output.WriteLine("Runs the phase 5.3 inner loop: map -> gate -> copy layer -> 0.1b -> gateway -> version -> wave -> packages.");
        output.WriteLine();
        output.WriteLine("--generate-only stops after the copy layer is generated and the 0.1b assertion passes, and prints the IR,");
        output.WriteLine("         the mirror width and the latch inventory. IT CONSTRUCTS NO GATEWAY AND READS NO HOST: nothing is");
        output.WriteLine("         imported, compiled or downloaded, and no socket exists on that path. Use it to review what would");
        output.WriteLine("         be deployed. --emit <dir> also writes one .ir file per generated object.");
        output.WriteLine();
        output.WriteLine("--verify uses the VERIFYING gateway: it reads the build stamp off the device and refuses any mismatch.");
        output.WriteLine("         It imports nothing, compiles nothing, downloads nothing and writes nothing. Use it to run vectors");
        output.WriteLine("         against a program that is ALREADY deployed. There is no flag that asserts a deployment instead of");
        output.WriteLine("         measuring one, because a gateway reporting Loaded without loading is one edit from one that lies.");
        output.WriteLine();
        output.WriteLine("Without --verify the gateway REFUSES and the loop stops at deployment. That is a real outcome, not a stub.");
        output.WriteLine();
        output.WriteLine($"exit {LoopExit.Ran} = the wave RAN            (not 'the block passed' — read the packages)");
        output.WriteLine($"exit {LoopExit.DidNotRun} = the loop stopped early  (it names where; nothing about the block was tested)");
        output.WriteLine($"exit {LoopExit.NothingExamined} = nothing examined       (usage, or an input that could not be read)");
        output.WriteLine($"exit {LoopExit.Refused} = the device fence refused the target — NO SOCKET WAS OPENED");
    }
}
