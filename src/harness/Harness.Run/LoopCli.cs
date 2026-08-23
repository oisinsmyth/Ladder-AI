using System.Text.Json;
using System.Text.Json.Nodes;
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

    /// <summary>
    /// 🔴 <c>--generate-only</c>: <b>the IR was produced AND NO WAVE COULD BE BUILT FROM THESE TWO
    /// DOCUMENTS.</b>
    ///
    /// <para>Its own code because the copy layer being right says nothing about the run being possible.
    /// The two live at different levels: the layer is a pure function of the BINDING, while the order is a
    /// property of merging the VECTORS into it. A caller that reads 0 here would conclude the pair is
    /// ready to deploy, and a deploying run would stop at <c>NotOrdered</c> or <c>NotSchedulable</c>.</para>
    /// </summary>
    public const int GeneratedNotRunnable = 5;
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
        Func<string, string?>? env = null,
        Func<string, IReadOnlyList<string>>? expandProgramPath = null,
        Func<string, IMirrorFeedPublisher>? openFeed = null,

        // Passed to Compose so the loop re-hashes a byte-stamped provenance record the same way
        // harness-gate does. Absent means the loop can only verify text-stamped records, which it
        // reports as NOT CHECKED rather than as a pass.
        Func<string, byte[]>? readBytes = null,

        // Retry the inert phase at index 0. Supplied by a caller that has just deployed - see InertSettle.
        Harness.Wire.InertSettle? inertSettle = null)
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
        var unit = byte.TryParse(Option(args, "--unit"), out var u) ? u : (byte)1;

        // 🔴 RETRY THE INERT PHASE AT INDEX 0, rather than sleeping past a transient nobody measured.
        // Supplied by a caller that has just downloaded - harness-batch does. Absent means no retry,
        // which is right for every run that did not just deploy: a slot that is not quiescent then has
        // been disturbed by something, and retrying would paper over the finding.
        var inertRetries = int.TryParse(Option(args, "--inert-retry"), out var ir) && ir > 0 ? ir : 1;
        var inertGapSeconds = double.TryParse(Option(args, "--inert-retry-interval"), out var ig) && ig > 0 ? ig : 5;
        var settleFromArgs = inertRetries > 1
            ? Harness.Wire.InertSettle.Retry(inertRetries, TimeSpan.FromSeconds(inertGapSeconds))
            : null;
        var outPath = Option(args, "--out");
        var verify = args.Contains("--verify");
        var generateOnly = args.Contains("--generate-only");
        var emitDir = Option(args, "--emit");
        var programPaths = Values(args, "--program");
        var noProgram = args.Contains("--no-program-under-test");

        // 🔴 *** OPT-IN, NEVER ON BY DEFAULT. *** A run that silently wrote a file somewhere is a surprise,
        // and the feed is a real artifact on disk with a real (small) cost per read. Stating the path is
        // also what keeps two concurrent runs from publishing over each other.
        var publishPath = Option(args, "--publish");

        // *** THE TWO MODES ARE MUTUALLY EXCLUSIVE, AND THE REFUSAL NAMES WHY. *** --verify reads a build
        // stamp OFF A DEVICE; --generate-only stops before any gateway is constructed. A run that claimed
        // both would have to open a socket to satisfy one of them.
        if (generateOnly && verify)
        {
            output.WriteLine("NOTHING EXAMINED — --generate-only and --verify are mutually exclusive. --generate-only stops after the");
            output.WriteLine("copy layer is generated and constructs no gateway at all; --verify reads the build stamp OFF THE DEVICE.");
            return LoopExit.NothingExamined;
        }

        // *** A FEED IS A RECORD OF READS, AND A GENERATE-ONLY RUN MAKES NONE. *** Accepting the flag and
        // writing an empty feed would put a file on disk that a viewer would report as "a publisher is
        // running and no read has landed yet" — a live-looking state for a run that will never read
        // anything. Refused by name rather than ignored: a flag that silently does nothing is one somebody
        // will believe was honoured.
        if (publishPath is not null && generateOnly)
        {
            output.WriteLine("NOTHING EXAMINED — --publish and --generate-only contradict each other. --publish forwards the reads a");
            output.WriteLine("wave makes so a viewer can watch them; --generate-only constructs no gateway, opens no socket and makes");
            output.WriteLine("no reads. A feed from it would show a publisher that is running and will never read anything.");
            return LoopExit.NothingExamined;
        }

        if (publishPath is not null && string.IsNullOrWhiteSpace(publishPath))
        {
            output.WriteLine("NOTHING EXAMINED — --publish was given an empty path. There is no default feed location: two runs");
            output.WriteLine("publishing to one guessed path would overwrite each other and a viewer could not tell which it was watching.");
            return LoopExit.NothingExamined;
        }

        if (bindingPath is null)
        {
            output.WriteLine("NOTHING EXAMINED — --binding is required. The bindings say which signal each register carries, and");
            output.WriteLine("their TYPES decide both the mirror tag and the rung shape. There is no default: a guessed binding");
            output.WriteLine("produces a copy layer that compiles and mirrors the wrong things.");
            return LoopExit.NothingExamined;
        }

        // 🔴 *** THE PROGRAM UNDER TEST IS DECLARED OR ITS ABSENCE IS, AND THERE IS NO THIRD OPTION. ***
        // It used to be `Array.Empty<HarnessObject>()`, hard-coded, with no flag that could change it. Two
        // consequences, and neither announced itself: the BUILD STAMP was computed over an empty program
        // set, so it could never match a rig carrying the block under test — the one thing the stamp
        // exists to establish — and ManifestOf returned NotAvailable, putting a permanent caveat on every
        // result package. `--no-program-under-test` is the same shape as `s7Objects: []`: a POSITIVE claim
        // somebody typed, not a silence the tool filled in.
        if (programPaths.Count > 0 && noProgram)
        {
            output.WriteLine("NOTHING EXAMINED — --program and --no-program-under-test contradict each other. One names the blocks under");
            output.WriteLine("test; the other claims there are none. Choosing between them would be this tool deciding what you meant.");
            return LoopExit.NothingExamined;
        }

        if (programPaths.Count == 0 && !noProgram)
        {
            output.WriteLine("NOTHING EXAMINED — no program under test was declared. Pass --program <file-or-dir>... , or");
            output.WriteLine("--no-program-under-test to claim there is none.");
            output.WriteLine("*** THERE IS NO DEFAULT, BECAUSE THE EMPTY SET IS NOT A SAFE ONE. *** The BUILD STAMP is a hash of what is");
            output.WriteLine("about to run, and a stamp taken over no program CANNOT MATCH a rig carrying the block under test — the");
            output.WriteLine("verify path would then refuse every healthy device, and the deploy path would publish a stamp naming a");
            output.WriteLine("program that is not the one loaded. The load-manifest check also reports NotAvailable over an empty set,");
            output.WriteLine("which makes every result package non-conclusive. An empty program set is a real and sayable claim; it is");
            output.WriteLine("just not one this tool may make on your behalf.");
            return LoopExit.NothingExamined;
        }

        IReadOnlyList<HarnessObject> program;
        try
        {
            program = ProgramUnderTest.Load(programPaths, readFile, expandProgramPath ?? DefaultExpand);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            output.WriteLine($"NOTHING EXAMINED — the PROGRAM UNDER TEST could not be loaded: {ex.Message}");
            output.WriteLine("Nothing was gated, generated or deployed. An object the loader could not classify is a REFUSAL naming it,");
            output.WriteLine("never a skip: a file quietly left out of the stamp is a program the version register would confirm wrongly.");
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

        WriteProgramInventory(program, noProgram, binding.BlockName, binding.TagTableName, output);

        // ---- GENERATE AND STOP ----------------------------------------------------------------------
        // Before the fence, because there is nothing to fence: no gateway is constructed on this path and
        // no host is read. It sits here rather than after the fence so that the absence is structural
        // rather than a flag somebody could reorder past.
        if (generateOnly)
            return GenerateOnly(submissionPath, bindingPath, submission, binding, program, readFile, emitDir, output, writeFile, readBytes);

        // ---- THE FENCE, BEFORE ANYTHING OPENS A SOCKET ----------------------------------------------
        var port = 0;

        if (verify)
        {
            if (host is null)
            {
                output.WriteLine("NOTHING EXAMINED — --verify needs --host. Verification reads the build stamp OFF THE DEVICE; there is");
                output.WriteLine("no offline form of it, because a stamp nobody read is not evidence.");
                return LoopExit.NothingExamined;
            }

            // 🔴 *** --port HAS NO DEFAULT, AND THE ONE IT HAD WAS WRONG FOR THE ONLY RIG THAT EXISTS. ***
            // It defaulted to 502 — the IANA Modbus port — and this rig serves 503 with :502 REFUSED
            // (measured). A refused connect is loud, so a wrong default looks survivable; what makes it
            // not survivable is that 502 is the port EVERY OTHER Modbus device on a network answers on, so
            // the quiet failure is reading a DIFFERENT DEVICE and believing it. Defaulting to 503 instead
            // would only bake one rig's measurement into the tool and move the same hazard elsewhere.
            //
            // So it is stated, like --host and --allowlist, and for the same reason: nothing about which
            // endpoint gets opened is inferred.
            if (!int.TryParse(Option(args, "--port"), out port) || port is < 1 or > 65535)
            {
                output.WriteLine($"NOTHING EXAMINED — --verify needs --port <1-65535>, and there is no default. (Read: '{Option(args, "--port") ?? "<absent>"}'.)");
                output.WriteLine("*** THE OLD DEFAULT OF 502 WAS WRONG FOR THIS RIG: it serves 503, and :502 is REFUSED — measured. *** A");
                output.WriteLine("wrong port is not reliably a loud failure either: 502 is the port every other Modbus device on a network");
                output.WriteLine("answers on, so the failure that is not loud is reading the WRONG DEVICE and believing what it says. The");
                output.WriteLine("port an endpoint is opened on is stated here, never inferred — as with --host and --allowlist.");
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
            request = Compose(submission, binding, program, readFile, readBytes, settleFromArgs);
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

        // 🔴 *** THE FEED, AND IT IS DISPOSED WHATEVER HAPPENS. *** `End()` is what turns "the wave
        // finished" into a fact a viewer can read; a publisher that simply stopped writing is
        // indistinguishable from one that died, and the viewer reports those as different states
        // precisely because they are. So the End must survive an exception out of the run.
        using var feed = publishPath is null
            ? null
            : (openFeed ?? DefaultFeed)(publishPath);

        if (feed is not null)
        {
            output.WriteLine($"feed        : PUBLISHING every read to {feed.Destination}");
            output.WriteLine("              Watch it live with:  harness-mirror-view --follow <that path> --map <tags.ir> --area <server.ir>");
            output.WriteLine("              THE VIEWER OPENS NO SOCKET. MB_SERVER accepts one connection per instance, so a viewer");
            output.WriteLine("              connecting directly while this runs would take this run's connection away — and would in");
            output.WriteLine("              any case be a second sample at a second instant. The page shows the bytes THIS run read.");
            output.WriteLine();
        }

        var result = LoopRun.Execute(request, gateway, () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), feed);

        Write(result, output);

        // *** REPORTED WHATEVER THE COUNT, INCLUDING ZERO. *** A publisher that cannot write must not stop
        // the wave — but a feed that quietly stopped writing is indistinguishable from a wave that quietly
        // stopped reading, and a line that appears only on failure teaches a reader that its absence means
        // everything was written.
        if (feed is not null)
        {
            output.WriteLine();
            output.WriteLine($"FEED: {feed.Published} document(s) published to {feed.Destination}, {feed.Failures} publish(es) FAILED.");

            if (feed.LastFailure is not null)
                output.WriteLine($"  last failure: {feed.LastFailure}");
        }

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

    /// <summary>The real feed. Behind a factory so no test above writes to a real path.</summary>
    private static IMirrorFeedPublisher DefaultFeed(string path) => new MirrorFeedPublisher(path);

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
        IReadOnlyList<HarnessObject> program,
        Func<string, string> readFile,
        string? emitDir,
        TextWriter output,
        Action<string, string> writeFile,

        // Carried through here too: this path runs the same gate, so it must be able to verify a
        // byte-stamped provenance record rather than reporting it NOT CHECKED.
        Func<string, byte[]>? readBytes)
    {
        LoopRequest request;
        try
        {
            request = Compose(submission, binding, program, readFile, readBytes);
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

        // *** THE GATE'S VERDICT IS PRINTED BEFORE ANY STOP, NOT AFTER THE ONE THAT DID NOT HAPPEN. ***
        // It used to be reported only on the path where generation SUCCEEDED, so a copy layer that failed
        // to generate — for a reason that has nothing to do with the vectors, such as a slot id the tag
        // namer will not take — threw away a gate report that had already been computed. A reader then had
        // no way to learn what the gate said about a submission short of making the unrelated fault go
        // away first, and no way at all to compare this gate against `harness-gate`'s.
        var admissible = WriteGate(generation.Gate, generation.Stopped, output);

        output.WriteLine();

        // *** IT IS PRINTED HERE EVEN THOUGH THIS PATH NEVER RUNS A WAVE. *** The resting values are a
        // property of the BINDING, so `--generate-only` is exactly where a coordinator can see what their
        // document does and does not declare — without spending a deployment to find out.
        WriteInertRest(generation.InertRest, generation.Stopped?.ToString() ?? "an unreported stage", output);

        if (!generation.Generated)
        {
            output.WriteLine();
            output.WriteLine($"NOT GENERATED: {generation.Stopped}");
            output.WriteLine($"  {generation.Detail}");

            foreach (var refusal in generation.Refusals)
                output.WriteLine("  - " + refusal);

            // *** THE 0.1b FINDINGS THEMSELVES, NOT ONLY THEIR COUNT. *** `REFUSED: 159 finding(s) across
            // 45 object(s)` is a number a reader cannot act on, and the objects are named in the findings
            // and nowhere else — so the one path that could tell you WHICH object and WHICH address was
            // printing a total. Grouped by object, because 159 findings over 45 objects read one at a time
            // is a refusal nobody finishes, and a refusal nobody finishes gets skimmed.
            foreach (var byObject in (generation.Retention?.Findings ?? Array.Empty<RetentionFinding>())
                         .GroupBy(f => f.Object, StringComparer.Ordinal)
                         .OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                output.WriteLine($"  - {byObject.Key}: {byObject.Count()} finding(s)");
                foreach (var finding in byObject.Take(3))
                    output.WriteLine($"      {finding.Detail}");

                if (byObject.Count() > 3)
                    output.WriteLine($"      ... and {byObject.Count() - 3} more on this object.");
            }

            return LoopExit.DidNotRun;
        }

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

        // 🔴 *** THE HASH RESTS ON A NUMBER NOBODY MEASURED, AND A READER OF THE HASH HAS TO KNOW THAT. ***
        // `retentiveBytes` is a property of the PROGRAM, not of the CPU, and MirrorGeometry gives it no
        // default deliberately — "a caller that does not know the program's retentive M extent must go and
        // read it". `Compose` supplies 256 when the binding is silent, and that value is a MAP HASH INPUT
        // and therefore a BUILD STAMP input: two runs disagreeing about it produce two identities for one
        // program. It is not refused here because no party in this loop can measure it and a gate nobody
        // can satisfy blocks its own recovery path — so it is PRINTED, on every run, beside the number it
        // decides.
        // 🔴 *** THE NUMBER COMES FROM THE GEOMETRY THE MAP WAS DERIVED WITH, AND THE LABEL COMES FROM THE
        // DOCUMENT. *** This printed `binding.RetentiveBytes` for an hour, which is a SECOND, independent
        // reading of the same field — so a `Compose` that stopped honouring it would print
        // `16 byte(s), STATED` beside a hash computed at 256. MEASURED by mutation: reverting Compose to a
        // hard-coded 256 left this line, and every test of it, perfectly green. *** A VALUE WITH TWO
        // READERS HAS AS MANY TRUTHS AS IT HAS READERS *** — so the value is read once, from the object
        // that actually decided the hash, and the document is asked only whether anybody stated it.
        output.WriteLine(binding.RetentiveBytes is not null
            ? $"  retentive M : {map.Geometry.RetentiveBytes} byte(s), STATED by the binding."
            : $"  retentive M : {map.Geometry.RetentiveBytes} byte(s), DEFAULTED — the binding does not state it. It is an input to the map"
              + Environment.NewLine
              + "                hash above and therefore to the build stamp, so this identity is only as measured as that number is.");

        output.WriteLine();

        // *** THE MERGED ORDER, PRINTED HERE AND GATED IN THE DEPLOYING RUN. *** Not a warning/gate split
        // but a scope one: the order decides how vectors merge into a tensor and decides nothing about the
        // IR below. Both paths read the same report object, so they cannot come to disagree.
        var order = generation.Order;
        var runnable = order is not null && order.Ordered;

        output.WriteLine($"WAVE ORDER  : {order?.Detail ?? "<not computed>"}");

        foreach (var refusal in order?.Refusals ?? Array.Empty<string>())
            output.WriteLine("  REFUSED   " + refusal);

        if (!runnable)
        {
            output.WriteLine("  *** NOTHING MAY BE RUN FROM THESE TWO DOCUMENTS AS THEY STAND. *** The IR below is correct and is a pure");
            output.WriteLine("      function of the BINDING; what is missing is above, and it is a property of merging the VECTORS into it.");
        }

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

        // *** THE GATE'S VERDICT WINS THE EXIT CODE WHEN BOTH ARE BAD. *** An inadmissible submission is
        // the stronger statement — the vectors themselves are not accepted — and reporting the weaker one
        // would send a reader to fix the order of a submission that would still be refused.
        return admissible
            ? runnable ? LoopExit.Generated : LoopExit.GeneratedNotRunnable
            : LoopExit.GeneratedNotAdmissible;
    }

    /// <summary>
    /// 🔴 <b>The submission and the binding, as the request the loop runs — and <c>public</c> so the
    /// gate-parity test can compare this against <c>harness-gate</c>'s own evaluation of the same pair.</b>
    ///
    /// <para>*** EVERY DOCUMENT-SOURCED GATE INPUT COMES FROM <see cref="GateCli.InputsOf"/>. *** It used
    /// to re-derive four of them here, pass <c>null</c> for four more and hard-code the last two, which
    /// made the loop's gate both weaker and stronger than the standalone one in different places. A
    /// submission could pass here and fail there, and this is the path that spends rig time.</para>
    /// </summary>
    /// <param name="program">
    /// The blocks and tag tables under test. <b>It feeds the BUILD STAMP</b>, which is the whole reason it
    /// must not be inferred: a stamp over an empty set names a program nobody downloaded.
    /// </param>
    /// <param name="readFile">
    /// Used only to read the tag map named by the submission's <c>tagMapPath</c>, for gate 11. Omitting it
    /// leaves that gate NOT CHECKED rather than comparing against an empty reachable set.
    /// </param>
    public static LoopRequest Compose(
        SubmissionDocument submission,
        BindingDocument binding,
        IReadOnlyList<HarnessObject> program,
        Func<string, string>? readFile = null,

        // 🔴 *** THE BYTE READER GOES THROUGH TOO, OR THE LOOP'S GATE IS WEAKER THAN THE CLI'S AGAIN. ***
        // A provenance record stamped over BYTES can only be re-hashed over bytes. Without this the loop
        // would report every derived field NOT CHECKED where harness-gate verified it - the precise
        // asymmetry GateParityTests exists to catch, and the loop is the path that spends rig time.
        Func<string, byte[]>? readBytes = null,

        // Retry the inert phase at index 0. Supplied by a caller that has just deployed - see InertSettle.
        Harness.Wire.InertSettle? inertSettle = null)
    {
        ArgumentNullException.ThrowIfNull(submission);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(program);

        var inputs = GateCli.InputsOf(submission, binding, readFile, readBytes);

        var bindings = (binding.Slots ?? new List<SlotBindingDocument>())
            .Select(s => new SlotBinding(
                s.SlotId ?? string.Empty,
                Signals(s.VectorTargets),
                s.StartCondition,
                Signals(s.ResultSources))
            {
                // The many-to-one map, off the wire. Absent stays absent: an empty `serves` is a slot that
                // answers to its own id, which is what every binding written before this meant.
                Serves = s.Serves?.ToArray() ?? Array.Empty<string>(),
                ServesRunInOrder = s.ServesRunInOrder,
                BoundarySpanning = s.BoundarySpanning?.ToArray() ?? Array.Empty<string>(),

                // The inert-rest migration claim, off the wire. A field the domain model has and the
                // composition drops is a field that does not exist — three of those are recorded in
                // BindingDocument, all of them found only after a run had already gone wrong.
                AssumedZeroRest = s.AssumedZeroRest,
                AssumedZeroRestBasis = s.AssumedZeroRestBasis,

                // 🔴 *** THE INERT CHECK'S WAIT, OFF THE WIRE. *** `InertRestPlan.For` was called with two
                // arguments and its third defaulted to 1, so every slot in every submission waited ONE
                // SCAN before deciding the model was at rest — for a model that takes ~9 seconds after a
                // contents step, the check reliably caught it mid-integration and blamed the block.
                // Absent stays 1, which is the floor rather than a blank.
                QuiescenceScans = s.QuiescenceScans ?? 1,

                // 🔴 *** THE PHASE CONDITION, AND AN UNRECOGNISED TRIGGER IS A THROW RATHER THAN Unstated.
                // *** Falling back to Unstated would silently drop the whole declaration: `InertRestPlan`
                // would then see a signal with no trigger and refuse for the WRONG reason, or — if the
                // signal were also absent — say nothing at all and run the wave unaligned while the
                // document plainly asked for alignment. A typo'd trigger must name itself.
                PhaseSignal = s.PhaseSignal,
                PhaseTrigger = ParsePhaseTrigger(s.PhaseTrigger, s.SlotId),
                PhaseThreshold = s.PhaseThreshold,
                PhaseGuardSignal = s.PhaseGuardSignal,
            })
            .ToArray();

        if (bindings.Length == 0)
            throw new InvalidDataException("the binding document names no slots. Empty is not clean: a loop with nothing bound would generate a copy layer that mirrors nothing.");

        static Harness.Map.PhaseTrigger ParsePhaseTrigger(string? declared, string? slotId)
        {
            if (string.IsNullOrWhiteSpace(declared))
                return Harness.Map.PhaseTrigger.Unstated;

            if (Enum.TryParse<Harness.Map.PhaseTrigger>(declared, ignoreCase: true, out var parsed)
                && parsed != Harness.Map.PhaseTrigger.Unstated)
            {
                return parsed;
            }

            throw new InvalidDataException(
                $"slot '{slotId}' declares phaseTrigger '{declared}', which is not one of Decreases, Below or AtOrAbove. "
                + "*** REFUSED RATHER THAN TREATED AS UNSTATED: *** a dropped trigger runs the wave with no phase alignment at all "
                + "while the document plainly asked for it, and the resulting wave looks exactly like an aligned one.");
        }

        var slots = bindings
            .Select(b => new SlotRequest(
                b.SlotId,
                Math.Max(1, b.VectorRegistersNeeded),
                Math.Max(1, b.ResultRegistersNeeded)))
            .ToArray();

        // 🔴 REQUIRED, WHERE baseByte AND retentiveBytes BELOW ARE DEFAULTED, AND THE ASYMMETRY IS THE
        // POINT. Those two have obvious rig values and a wrong one shows up at once as addresses that do
        // not line up. This number decides whether the map FITS INSIDE WHAT MODBUS CAN REACH, and a
        // default would be a value this code invented, silently answering that question for a submission
        // that never stated it. Same rule as --declared-registers in harness-mirror-read.
        if (binding.DeclaredRegisters is not int declaredRegisters)
        {
            throw new InvalidDataException(
                "the binding document does not state `declaredRegisters`: the width MB_HOLD_REG declares in the comms block's area "
                + "pointer (P#M<base>.0 WORD n), in registers. It is REQUIRED and is not defaulted, because it is the ceiling a map is "
                + "checked against — registers past it exist in %M but cannot be read over Modbus at all, so a map that overflows it "
                + "produces reads REFUSED BY THE SERVER mid-wave rather than a refusal here. On the rig it is 576.");
        }

        return new LoopRequest(
            inputs.Vectors,
            inputs.Enumeration,
            inputs.Fidelity,
            inputs.BlockAuthor,
            inputs.Conflicts,
            MirrorGeometry.ForCpu1214C(
                retentiveBytes: binding.RetentiveBytes ?? 256,
                baseByte: binding.BaseByte ?? 1000,
                declaredRegisters: declaredRegisters),
            slots,
            bindings,
            new CopyLayerNaming(
                binding.BlockName ?? "FC_HarnessCopyLayer",
                binding.BlockNumber ?? 0,
                binding.TagTableName ?? "HarnessMirror",
                binding.TagPrefix ?? "HX_"),

            // *** THE STAMP IS COMPUTED OVER WHAT IS ACTUALLY DEPLOYED. *** That is the entire point of
            // it: a manifest says what TIA reported sending, the stamp says what is EXECUTING. This was
            // Array.Empty<HarnessObject>() with no way to change it.
            program,
            RuntimeCompression: new RuntimeCompression(inputs.RuntimeCompression),
            CompressionInputs: inputs.CompressionInputs,
            Deployment: inputs.Deployment,
            TagMapReach: inputs.TagMapReach,
            SignalStorage: inputs.Storage,
            UnknownFields: inputs.UnknownFields,
            AnnotationFields: inputs.AnnotationFields,
            ConflictEdgesExplicitlyNull: inputs.ConflictEdgesExplicitlyNull,
            Derivation: inputs.Derivation,
            ScenarioEndInput: inputs.ScenarioEndInput,
            MaxIndexScans: inputs.MaxIndexScans,
            ScenarioTimeInputs: inputs.ScenarioTimeInputs,
            InertSettle: inertSettle);
    }

    private static IReadOnlyList<MirroredSignal> Signals(List<MirroredSignalDocument>? rows) =>
        (rows ?? new List<MirroredSignalDocument>())
        .Select(GateCli.ToMirroredSignal)
        .ToArray();

    /// <summary>
    /// The gate's verdict and every refusal and NOT CHECKED, <b>printed whatever happens next</b>. Returns
    /// whether it admitted the submission.
    ///
    /// <para><b>When there is no verdict it reports WHICH STAGE STOPPED, and never a cause it cannot
    /// know.</b> This line first read <i>"the map could not be derived, so the gate never ran"</i> — true
    /// of the only pre-gate stop that existed when it was written, and false the moment the slot join
    /// landed above the gate, where the map derives perfectly. <b>An error message that asserts a
    /// conclusion its code cannot reach is a false claim shipped in the product</b>, and this project has
    /// now been bitten three times by one naming the wrong artifact — most recently a failed BINDING read
    /// printing the SUBMISSION's filename, which sends its reader to inspect a healthy file.</para>
    /// </summary>
    private static bool WriteGate(SubmissionReport? gate, LoopOutcome? stopped, TextWriter output)
    {
        output.WriteLine($"SUBMISSION GATE: {gate?.Verdict.ToString() ?? $"<not evaluated — the run stopped at {stopped?.ToString() ?? "an unreported stage"}, which is BEFORE the gate>"}"
                         + (gate is null ? string.Empty : $"  ({gate.Gates.Count} gate(s) run, {gate.Refused.Count} refused, {gate.NotChecked.Count} could not run)"));

        foreach (var refused in gate?.Refused ?? Array.Empty<GateResult>())
            output.WriteLine($"  REFUSED     {refused.Gate}: {refused.Detail}");

        foreach (var notChecked in gate?.NotChecked ?? Array.Empty<GateResult>())
            output.WriteLine($"  NOT CHECKED {notChecked.Gate}: {notChecked.Detail}");

        return gate?.Verdict == SubmissionVerdict.AdmissibleSubjectToJudgement;
    }

    /// <summary>
    /// What the stamp was taken over, <b>on every run including the empty one</b> — a report that appears
    /// only when there is something to say teaches its reader that absence means it did not run.
    /// </summary>
    private static void WriteProgramInventory(
        IReadOnlyList<HarnessObject> program,
        bool noProgram,
        string? copyLayerBlockName,
        string? mirrorTagTableName,
        TextWriter output)
    {
        if (noProgram)
        {
            output.WriteLine("program     : NONE — `--no-program-under-test` was declared. *** THIS IS A POSITIVE CLAIM AND IT HAS TEETH: ***");
            output.WriteLine("              the build stamp is taken over the copy layer alone, so it will only confirm against a device");
            output.WriteLine("              carrying no block under test, and the load-manifest check reports NotAvailable — which makes");
            output.WriteLine("              every result package non-conclusive about any block.");
            output.WriteLine();
            return;
        }

        // *** THE HARNESS'S OWN TWO OBJECTS ARE NAMED HERE AND SUBTRACTED FROM THE HEADLINE COUNT. *** This
        // line used to read "N object(s) under test, and THE BUILD STAMP IS TAKEN OVER THEM" over a set that
        // included the copy layer being stamped — which was both the defect and the reason it survived, since
        // the report asserted the very scope it had wrong. See BuildStamp.Derive for the measurement.
        bool SelfReferential(HarnessObject o) =>
            string.Equals(o.Name, copyLayerBlockName, StringComparison.Ordinal) ||
            string.Equals(o.Name, mirrorTagTableName, StringComparison.Ordinal);

        var stamped = program.Count(o => !SelfReferential(o));

        output.WriteLine($"program     : {program.Count} object(s) under test; THE BUILD STAMP IS TAKEN OVER {stamped} OF THEM.");

        foreach (var obj in program)
        {
            var note = SelfReferential(obj)
                ? "  <- NOT STAMPED: the harness generates this one, so hashing it would stamp the stamp"
                : string.Empty;

            output.WriteLine($"              {obj.Kind,-9} {obj.Name}  ({obj.Ir.Length} chars of IR){note}");
        }

        if (stamped == program.Count && (copyLayerBlockName is not null || mirrorTagTableName is not null))
        {
            output.WriteLine($"              (no supplied object is the harness's own: expected `{copyLayerBlockName}` and");
            output.WriteLine($"              `{mirrorTagTableName}` in the program set and neither was there. That is legitimate when the");
            output.WriteLine("              copy layer has not been promoted into this tree yet — and it is worth noticing, because");
            output.WriteLine("              once it IS promoted the set changes and only the exclusion keeps the stamp stable.)");
        }

        // *** THE PROGRAM'S CLAIM ON RETAIN — REPORTED, GATING NOTHING, AND PRINTED WHEN IT IS ZERO. ***
        // 0.1b constrains the objects the harness GENERATES; the plant's retain is the plant's, and at
        // least one retentive member here exists precisely so it survives the CPU restart the
        // boundary-spanning vectors ride. But retain is one shared budget and the tightest on this rig, so
        // a harness that says nothing about the program's share of it is hiding a real number.
        var retain = RetentionCheck.RetainDeclarations(program);

        output.WriteLine($"              RETAIN in the program under test: {retain.Count} declaration(s) — REPORTED, NOT GATED. 0.1b is about the");
        output.WriteLine("              objects this harness GENERATES; the program's retain is not the harness's to refuse. It is a claim on the");
        output.WriteLine("              same budget, which is why it is counted.");

        foreach (var byObject in retain.GroupBy(f => f.Object, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
            output.WriteLine($"                {byObject.Key}: {byObject.Count()}");

        output.WriteLine();
    }

    /// <summary>Every value after <paramref name="name"/>, for a flag that may repeat or take a list.</summary>
    private static IReadOnlyList<string> Values(IReadOnlyList<string> args, string name)
    {
        var values = new List<string>();

        for (var i = 0; i < args.Count; i++)
        {
            if (!string.Equals(args[i], name, StringComparison.Ordinal))
                continue;

            for (var j = i + 1; j < args.Count && !args[j].StartsWith("--", StringComparison.Ordinal); j++)
                values.Add(args[j]);
        }

        return values;
    }

    /// <summary>A directory becomes its <c>.ir</c> files, in a stable order; anything else is itself.</summary>
    private static IReadOnlyList<string> DefaultExpand(string path) =>
        Directory.Exists(path)
            ? Directory.GetFiles(path, "*.ir").OrderBy(p => p, StringComparer.Ordinal).ToArray()
            : new[] { path };

    /// <summary>
    /// 🔴 <b>The run report — and every count in it reads against the SUBMITTED vectors.</b>
    ///
    /// <para><b>The defect this closes, verbatim from the first wave that has ever run:</b>
    /// <c>the wave ran to 22 index(es) over 1 slot(s)</c>, two packages, then
    /// <c>0 of 2 package(s) say anything about the block at all</c>. <b>Nothing anywhere stated what
    /// became of vectors 3 to 22.</b> The denominator was the number of packages PRODUCED, which is the
    /// one number guaranteed to make an incomplete run look complete.</para>
    /// </summary>
    /// <summary>
    /// 🔴 <b>WHAT THE INERT START STATE WAS GATED ON, ON EVERY RUN — declared, excluded, defaulted or
    /// derived, per register.</b>
    ///
    /// <para>*** THE DEFECT THIS SECTION EXISTS FOR PRINTED NOTHING AT ALL. *** Every result register was
    /// asserted to rest at zero by a hardcoded <c>ToDictionary</c> in the wave builder, so a run gated on
    /// twenty-three assumptions and a run gated on twenty-three declarations produced identical output.
    /// <b>A defaulted expectation that reads like a declared one is the whole shape of it</b>, which is why
    /// the per-register lines carry their provenance rather than only the totals.</para>
    ///
    /// <para><b>Printed on the clean run too.</b> A section that appears only when something is wrong
    /// teaches its reader that its absence means everything was declared.</para>
    /// </summary>
    internal static void WriteInertRest(InertRestReport? report, string stoppedAt, TextWriter output)
    {
        if (report is null)
        {
            output.WriteLine("INERT REST: NOT COMPUTED — the run stopped at " + stoppedAt + ", which is before the bindings were examined.");
            output.WriteLine("  *** THIS IS NOT 'nothing to report'. *** No register's resting value was established either way.");
            output.WriteLine();
            return;
        }

        output.WriteLine(report.Summary());

        foreach (var plan in report.Plans)
        {
            output.WriteLine($"  {plan.Summary()}");

            // Only the registers a reader has to WEIGH. A declared value is the ordinary case and printing
            // twenty of them buries the two that nobody stated — and a section nobody finishes gets skimmed.
            foreach (var register in plan.Registers.Where(r =>
                         r.Provenance is InertRestProvenance.Defaulted or InertRestProvenance.Excluded))
            {
                output.WriteLine($"    {register}");
            }

            foreach (var refusal in plan.Refusals)
                output.WriteLine($"    REFUSED  {refusal}");

            foreach (var note in plan.Notes)
                output.WriteLine($"    NOTE     {note}");
        }

        if (report.Plans.Sum(p => p.DefaultedCount) > 0)
        {
            output.WriteLine("  🔴 A DEFAULTED REGISTER IS ONE NOBODY DECLARED. Its expectation of 0 was supplied by the slot's");
            output.WriteLine("     assumedZeroRest claim, not by anyone who knows what the signal does — so an inert failure on one");
            output.WriteLine("     of these may be the expectation rather than the program, and a PASS on one proves less than it looks.");
        }

        output.WriteLine();
    }

    internal static void Write(LoopResult result, TextWriter output)
    {
        var account = result.Account;

        output.WriteLine($"OUTCOME: {result.Outcome}");
        output.WriteLine($"  {result.Detail}");
        output.WriteLine();

        WriteInertRest(result.InertRest, result.Outcome.ToString(), output);

        // *** THE DENOMINATOR, PRINTED ON EVERY RUN INCLUDING THE COMPLETE ONE. *** A section that appears
        // only when something went short teaches a reader that its absence means everything ran.
        output.WriteLine($"VECTORS SUBMITTED: {account.Submitted}");
        output.WriteLine($"  RAN            {account.Ran}");
        output.WriteLine($"  NEVER ATTEMPTED {account.NeverAttempted}");

        foreach (var group in account.Vectors
                     .Where(v => v.Disposition != VectorDisposition.Ran)
                     .GroupBy(v => v.Disposition)
                     .OrderBy(g => g.Key.ToString(), StringComparer.Ordinal))
        {
            output.WriteLine($"    {Label(group.Key),-18} {group.Count()}");
        }

        output.WriteLine();

        // *** WHICH INDEX A SLOT STOPPED AT, AND WHAT IT COST IN VECTORS. *** "A slot exited early" without
        // the index and the count is a fact nobody can act on.
        if (account.SlotExits.Count > 0)
        {
            output.WriteLine("SLOTS THAT STOPPED SHORT OF THEIR OWN TENSOR");
            foreach (var exit in account.SlotExits)
            {
                output.WriteLine(
                    $"  slot {exit.SlotIndex}: ran {exit.IndicesRun} of {exit.TensorLength} index(es), stopping at index "
                    + $"{exit.LastIndex} with {exit.LastOutcome}. {exit.VectorsNeverAttempted} vector(s) were consequently NEVER ATTEMPTED.");
                output.WriteLine($"      the stopping index said: {exit.LastDetail}");
            }

            output.WriteLine();
        }

        output.WriteLine("DISPOSITION PER SUBMITTED VECTOR");
        foreach (var vector in account.Vectors)
        {
            output.WriteLine($"  {Label(vector.Disposition),-18} {vector.VectorId,-16} slot {vector.SlotIndex} index {vector.WaveIndex}");

            if (vector.Disposition != VectorDisposition.Ran)
                output.WriteLine($"      {vector.Detail}");
        }

        output.WriteLine();

        if (result.Packages.Count == 0)
        {
            output.WriteLine("NO PACKAGES — nothing about the block was tested. That is a statement about the RUN, not about the block.");
            output.WriteLine($"*** EMPTY IS NOT CLEAN: {account.Submitted} vector(s) were submitted and NONE of them produced a result. ***");
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

        // *** AGAINST THE SUBMITTED TOTAL, NEVER AGAINST THE PACKAGES. *** Both numbers are printed, so a
        // reader can see the gap rather than having to notice it.
        output.WriteLine(
            $"{conclusive} of {account.Submitted} SUBMITTED vector(s) say anything about the block at all "
            + $"({result.Packages.Count} produced a package; {account.NeverAttempted} were never attempted).");
    }

    /// <summary>Fixed-width labels for the console. The JSON carries the enum name, which is the machine-readable one.</summary>
    private static string Label(VectorDisposition disposition) => disposition switch
    {
        VectorDisposition.Ran => "RAN",
        VectorDisposition.SlotExitedFirst => "SLOT EXITED FIRST",
        VectorDisposition.WaveDidNotRun => "WAVE DID NOT RUN",
        VectorDisposition.NotDistributed => "NOT DISTRIBUTED",
        _ => disposition.ToString().ToUpperInvariant(),
    };

    /// <summary>
    /// 🔴 <b>The artifact. A consumer reads THIS, never the rendering above</b> — so everything the report
    /// above can conclude must be computable from here alone.
    ///
    /// <para><b>Two defects closed at once, and they are the same defect at two levels.</b> Each package
    /// now carries all seven of DB-8's named contents (see <see cref="ResultPackageJson"/>), and the
    /// document now carries a disposition for every SUBMITTED vector rather than only for the ones that
    /// produced a package. <c>dispositions.length</c> is the denominator; coverage is computable from this
    /// file without reference to any console output.</para>
    /// </summary>
    internal static string Render(LoopResult result)
    {
        var account = result.Account;

        var packages = new JsonArray();
        foreach (var package in result.Packages)
            packages.Add(ResultPackageJson.Of(package));

        var dispositions = new JsonArray();
        foreach (var vector in account.Vectors)
        {
            dispositions.Add(new JsonObject
            {
                ["vector"] = vector.VectorId,
                ["citedSlot"] = vector.CitedSlotId,
                ["slotIndex"] = vector.SlotIndex,
                ["waveIndex"] = vector.WaveIndex,
                ["disposition"] = vector.Disposition.ToString(),
                ["attempted"] = vector.Attempted,
                ["detail"] = vector.Detail,
            });
        }

        var slotExits = new JsonArray();
        foreach (var exit in account.SlotExits)
        {
            slotExits.Add(new JsonObject
            {
                ["slotIndex"] = exit.SlotIndex,
                ["indicesRun"] = exit.IndicesRun,
                ["tensorLength"] = exit.TensorLength,
                ["lastIndexReached"] = exit.LastIndex,
                ["lastOutcome"] = exit.LastOutcome,
                ["lastDetail"] = exit.LastDetail,
                ["vectorsNeverAttempted"] = exit.VectorsNeverAttempted,
            });
        }

        var loopCaveats = new JsonArray();
        foreach (var caveat in result.Caveats)
            loopCaveats.Add(new JsonObject { ["id"] = caveat.Id, ["detail"] = caveat.Detail });

        // 🔴 *** WHAT THE BUILD STAMP WAS COMPUTED OVER - SO THIS RUN CAN BE RE-RUN. ***
        //
        // Measured 2026-08-21: a wave that had gone green could not be re-run. The verifying gateway
        // refused a later attempt on a stamp mismatch, and NOTHING RECORDED WHICH PROGRAM SET the
        // successful run had stamped. Two candidate sets were tried and gave two different stamps,
        // neither the device's. A hash cannot be inverted, so the run became unreproducible the moment
        // its command line was gone - and this file, the artifact meant to OUTLIVE the run, had kept the
        // outcome and not the input.
        //
        // `null` here is "the run stopped before the stamp was computed", which is not the same as a run
        // that hashed nothing - that one says so through `hashedNothing`.
        JsonNode? programUnderTest = null;
        if (result.ProgramManifest is { } manifest)
        {
            var objects = new JsonArray();
            foreach (var o in manifest.Objects)
                objects.Add(new JsonObject { ["kind"] = o.Kind, ["name"] = o.Name, ["sha256"] = o.Sha256 });

            var excluded = new JsonArray();
            foreach (var e in manifest.ExcludedAsSelfReferential)
                excluded.Add(e);

            programUnderTest = new JsonObject
            {
                ["stamp"] = $"16#{manifest.Stamp:X8}",
                ["objectCount"] = manifest.Objects.Count,

                // A run declaring no program under test hashed no objects. That is a REAL state and a
                // different one from a manifest nobody recorded, so it is said rather than inferred from
                // an empty array.
                ["hashedNothing"] = manifest.HashedNothing,
                ["objects"] = objects,

                // Named, never dropped: a caller who passed a whole IR directory has no way to know it
                // also handed over the copy layer.
                ["excludedAsSelfReferential"] = excluded,
            };
        }

        // 🔴 *** THE POST-DOWNLOAD SETTLING TIME, AS A NUMBER THAT OUTLIVES THE TERMINAL. ***
        //
        // The wave already measured it and only ever rendered it as prose on the headline — and the prose
        // was written ONLY when the retry loop had to work, so the run that settled instantly reported
        // exactly like the run where no retry was licensed. Both were silence.
        //
        // EMITTED ON EVERY RUN, INCLUDING WHEN THERE IS NOTHING TO REPORT, and it says which of the two
        // nothings it is. A key that simply disappears when unmeasured is how "nobody asked" and "the
        // answer was zero" became the same document.
        var settling = result.Wave?.InertSettle is { } measured
            ? new JsonObject
            {
                ["measured"] = true,
                ["attempts"] = measured.Attempts,
                ["waitedSeconds"] = Math.Round(measured.Waited.TotalSeconds, 3),

                // False is a RESULT — the transient outlasted the licence — not a missing measurement.
                ["quiescent"] = measured.Quiescent,
            }
            : new JsonObject
            {
                ["measured"] = false,
                ["reason"] = result.Wave is null
                    ? "no wave ran, so the plant was never observed settling."
                    : "no inert retry was licensed — this run did not follow a download, so there was no "
                      + "transient to measure. NOT a measurement of zero.",
            };

        var document = new JsonObject
        {
            ["outcome"] = result.Outcome.ToString(),
            ["detail"] = result.Detail,
            ["programUnderTest"] = programUnderTest,
            ["postDownloadSettling"] = settling,

            // The coverage arithmetic, stated rather than left to be derived — and derivable anyway from
            // `dispositions`, which is what makes these three numbers checkable against the rows below.
            ["vectorsSubmitted"] = account.Submitted,
            ["vectorsRan"] = account.Ran,
            ["vectorsNeverAttempted"] = account.NeverAttempted,
            ["packagesProduced"] = result.Packages.Count,
            ["conclusiveAboutTheBlock"] = result.Packages.Count(p => p.ConclusiveAboutTheBlock),
            ["indicesPlanned"] = account.IndicesPlanned,
            ["indicesRun"] = account.IndicesRun,
            ["slotExits"] = slotExits,
            ["dispositions"] = dispositions,
            ["packages"] = packages,
            ["caveats"] = loopCaveats,
        };

        return document.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static string? Option(IReadOnlyList<string> args, string name)
    {
        var index = args.ToList().IndexOf(name);
        return index >= 0 && index + 1 < args.Count ? args[index + 1] : null;
    }

    private static void Usage(TextWriter output)
    {
        output.WriteLine("usage: harness-run --submission <submission.json> --binding <binding.json>");
        output.WriteLine("                   (--program <file-or-dir>... | --no-program-under-test)");
        output.WriteLine("                   [--generate-only [--emit <dir>]]");
        output.WriteLine("                   [--verify --host <ip> --port <n> [--unit 1] [--allowlist <path>]]");
        output.WriteLine("                   [--publish <feed.mirrorfeed>]");
        output.WriteLine("                   [--out <result.json>]");
        output.WriteLine();
        output.WriteLine("Runs the phase 5.3 inner loop: map -> gate -> copy layer -> 0.1b -> gateway -> version -> wave -> packages.");
        output.WriteLine();
        output.WriteLine("--program names the blocks and tag tables UNDER TEST, as .ir files (a directory contributes its *.ir).");
        output.WriteLine("         THE BUILD STAMP IS COMPUTED OVER THEM — a manifest says what TIA reported sending, the stamp says");
        output.WriteLine("         what is EXECUTING — so a stamp taken over an empty set cannot match a rig carrying the block. There");
        output.WriteLine("         is no default; --no-program-under-test is the positive claim that there is none, and it is checked");
        output.WriteLine("         rather than assumed. An .ir this loader cannot classify is a REFUSAL naming the file, never a skip.");
        output.WriteLine();
        output.WriteLine("--port has NO DEFAULT and is required with --verify. It defaulted to 502; this rig serves 503 and refuses 502");
        output.WriteLine("         (measured). 502 is also the port every other Modbus device answers on, so the failure that is NOT");
        output.WriteLine("         loud is reading a different device and believing it.");
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
        output.WriteLine("--publish FORWARDS EVERY READ THIS RUN MAKES to a feed file, so `harness-mirror-view --follow <path>`");
        output.WriteLine("         can show the wave live WITHOUT OPENING A SECOND SOCKET. That matters twice over. MB_SERVER accepts");
        output.WriteLine("         ONE connection per instance, so a viewer connected directly while a wave runs takes the wave's");
        output.WriteLine("         connection away - measured: SocketException 'actively refused', 0 of 22 vectors attempted. And two");
        output.WriteLine("         sockets would be TWO SAMPLES AT TWO INSTANTS, so the page could show a value this run never acted");
        output.WriteLine("         on. The feed carries the RAW REGISTERS as read, each stamped with the moment the read RETURNED;");
        output.WriteLine("         the viewer does the decoding, so there is exactly one decoder and the two cannot drift.");
        output.WriteLine("         OFF unless stated: a run that silently writes a file somewhere is a surprise. A failure to publish");
        output.WriteLine("         never stops the wave, and the failures are COUNTED and reported at the end of the run.");
        output.WriteLine();
        output.WriteLine("Without --verify the gateway REFUSES and the loop stops at deployment. That is a real outcome, not a stub.");
        output.WriteLine();
        output.WriteLine($"exit {LoopExit.Ran} = the wave RAN            (not 'the block passed' — read the packages)");
        output.WriteLine($"exit {LoopExit.DidNotRun} = the loop stopped early  (it names where; nothing about the block was tested)");
        output.WriteLine($"exit {LoopExit.NothingExamined} = nothing examined       (usage, or an input that could not be read)");
        output.WriteLine($"exit {LoopExit.Refused} = the device fence refused the target — NO SOCKET WAS OPENED");
    }
}
