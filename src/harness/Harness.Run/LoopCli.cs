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

        // 🔴 *** THE DENOMINATOR — WHAT THE DEPLOYMENT STAGED, AS OPPOSED TO WHAT --program HANDS THE
        // STAMP TO HASH. *** Emitted by `harness-batch` from the lane manifests, never typed by a person:
        // a corpus derived from --program would be the numerator measuring itself and could never report a
        // gap. Absent is a real and common state — a hand-driven run has no manifest behind it — and it
        // renders as NO DENOMINATOR, in words, rather than as a clean sheet.
        StagedCorpus? stagedCorpus;
        try
        {
            stagedCorpus = StagedRows(Values(args, "--staged"));
        }
        catch (FormatException ex)
        {
            output.WriteLine("NOTHING EXAMINED — " + ex.Message);
            output.WriteLine("*** REFUSED RATHER THAN SKIPPED: *** a row this tool cannot read is a row missing from the DENOMINATOR,");
            output.WriteLine("and a denominator that quietly shrinks is exactly how a stamp covering 8 of 9 objects reads as complete.");
            return LoopExit.NothingExamined;
        }

        // 🔴 *** WHETHER ANYTHING DERIVED WHO ELSE LIVES IN THE MIRROR'S %M AREA (workbench Y1). ***
        //
        // Supplied by `harness-batch`, which is the only party that can answer it: the neighbour list is
        // derived from the whole deployed corpus, and a lane's own two documents cannot testify about it.
        // Present means NOT DERIVED, and the sentence travels into every package this run writes — the
        // plan that said it is a terminal line, and the package is what a reviewer opens weeks later.
        //
        // Absent means DERIVED, or a caller that predates the question. It does NOT mean "no neighbours".
        var neighboursNotDerived = Option(args, "--neighbours-not-derived");

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
            return GenerateOnly(submissionPath, bindingPath, submission, binding, program, readFile, emitDir, output, writeFile, readBytes, stagedCorpus, neighboursNotDerived);

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
            request = Compose(submission, binding, program, readFile, readBytes, settleFromArgs, stagedCorpus, neighboursNotDerived);
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
        Func<string, byte[]>? readBytes,

        // The denominator, on this path too. A generate-only run computes the same stamp a deploying run
        // will, so it must report the same coverage — this is the cheapest place to discover that a lane
        // stages an object the program list does not name, and it costs no device.
        StagedCorpus? stagedCorpus = null,

        // Echoed on this path too. A generate-only run writes no package, so the report is the only
        // surface the sentence has here — and an absent line would read as a derivation that found
        // nothing, which is the whole failure class.
        string? neighboursNotDerived = null)
    {
        LoopRequest request;
        try
        {
            request = Compose(submission, binding, program, readFile, readBytes,
                stagedCorpus: stagedCorpus, neighboursNotDerived: neighboursNotDerived);
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

        // 🔴 PRINTED WHEREVER IT IS KNOWN, INCLUDING THE PATH THAT WRITES NO PACKAGE. A run whose mirror
        // was placed against a neighbour list nobody derived must not be readable as one that was
        // checked — and a generate-only run is precisely where a person eyeballs the layout.
        if (!string.IsNullOrWhiteSpace(request.NeighboursNotDerived))
            output.WriteLine("neighbours  : " + request.NeighboursNotDerived);

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

            // The lane's refusals are on a different object from the copy layer's, and printing only the
            // second would leave a NotGeneratable stop with its whole cause missing.
            foreach (var refusal in generation.Lane?.Refusals ?? Array.Empty<string>())
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
        WriteCoverage(generation.Manifest, output);
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

        // 🔴 *** THE NEIGHBOUR INVENTORY, PRINTED ON EVERY RUN INCLUDING THE EMPTY ONE — and the reason is
        // this exact defect. *** Until 2026-08-23 `reservedRegions` was parsed, validated and then ignored
        // on this path, and the report said NOTHING either way: a reader could not distinguish "checked and
        // clear" from "never looked", which is the same indistinguishability the guard exists to remove one
        // level down. A check that is silent when it passes is a check nobody can confirm ran.
        WriteNeighbours(map.Geometry, output);

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

        // 🔴 *** WHAT THE LANE GENERATED AND WHAT IT DID NOT — PRINTED ON EVERY RUN, INCLUDING THE ONE THAT
        // DECLARED NOTHING. *** A section that appears only when something was generated teaches a reader
        // that its absence means there was nothing left to generate, which is the opposite of the truth on
        // every lane that exists.
        WriteLane(generation.Lane, output);
        WriteProgram(generation.Program, output);

        output.WriteLine();

        foreach (var obj in generation.Objects
                     .Concat(generation.Lane?.Objects ?? Array.Empty<HarnessObject>())
                     .Concat(generation.Program?.Objects ?? Array.Empty<HarnessObject>()))
        {
            output.WriteLine($"----- {obj.Kind} {obj.Name} -----");
            output.WriteLine(obj.Ir.TrimEnd('\n'));
            output.WriteLine();
        }

        foreach (var fragment in generation.Lane?.Fragments ?? Array.Empty<StimShellFragment>())
        {
            output.WriteLine($"----- STIMULUS SHELL (networks 1..{fragment.Shell.Networks.Count} of {fragment.HeadName}) -----");
            output.WriteLine(fragment.Shell.Ir.TrimEnd('\n'));
            output.WriteLine();
        }

        if (emitDir is not null)
        {
            foreach (var obj in generation.Objects
                     .Concat(generation.Lane?.Objects ?? Array.Empty<HarnessObject>())
                     .Concat(generation.Program?.Objects ?? Array.Empty<HarnessObject>()))
            {
                var path = Path.Combine(emitDir, obj.Name + ".ir");
                writeFile(path, obj.Ir);
                output.WriteLine($"WRITTEN: {path}");
            }

            // 🔴 *** THE SHELL FRAGMENTS GO IN A SUBDIRECTORY, AND THAT IS NOT TIDINESS. *** Every loader
            // that turns an emit directory back into a --program list globs `*.ir` NON-RECURSIVELY. A
            // fragment sitting beside the deployables would be picked up as a block, handed to the
            // converter, and refused — or worse, counted. It is networks, not a block: no header, no
            // interface, no statics.
            foreach (var fragment in generation.Lane?.Fragments ?? Array.Empty<StimShellFragment>())
            {
                var shellPath = Path.Combine(emitDir, StimShellFragment.Subdirectory, fragment.FileName);
                writeFile(shellPath, fragment.Shell.Ir);
                output.WriteLine($"WRITTEN: {shellPath}  (A FRAGMENT — networks only, NOT an importable block)");

                var requiresPath = Path.Combine(emitDir, StimShellFragment.Subdirectory, fragment.RequirementsFileName);
                writeFile(requiresPath, fragment.Requirements());
                output.WriteLine($"WRITTEN: {requiresPath}");
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
    /// 🔴 <b>WHAT THE LANE GENERATED, WHAT IT DECLINED TO, AND WHAT NOBODY DECLARED — all three, on every
    /// run.</b>
    ///
    /// <para><b>The third is the one this exists for.</b> Standing a lane up meant hand-authoring eight
    /// <c>.ir</c> artifacts and the harness generated two of them, and nothing anywhere said so: a reader
    /// of a clean run could not tell a lane whose test side is generated from one where every block was
    /// typed. <c>ObjectOrigin</c> was built to answer <i>how much of this lane is still hand-built</i> and
    /// could only be reached from a document a person had written by hand.</para>
    ///
    /// <para><b>AUTHORED is printed as a positive line, never as an absence.</b> An undeclared object and a
    /// generated one must not be distinguishable only by which line is missing.</para>
    /// </summary>
    private static void WriteLane(LaneGenerationResult? lane, TextWriter output)
    {
        if (lane is null)
        {
            output.WriteLine("LANE        : NOT COMPUTED — the run stopped before any declaration was read. Nothing here says whether "
                           + "this lane's slot FC or stimulus head are generated or authored.");
            return;
        }

        output.WriteLine($"LANE        : {lane.Summary()}");

        foreach (var slot in lane.Slots)
        {
            if (slot.SlotFc is { } fc)
            {
                output.WriteLine($"  GENERATED  slot '{slot.SlotId}': FC {fc.BlockName} — calls {fc.StimulusHeadBlock} FIRST, "
                               + $"then {fc.BlockUnderTestBlock}. The order is the whole content.");
            }

            if (slot.StimShell is { } shell)
            {
                output.WriteLine($"  GENERATED  slot '{slot.SlotId}': {shell.HeadName} networks 1..{shell.Shell.Networks.Count} "
                               + $"(the index shell) — {shell.Shell.RequiredUdtMembers.Count} UDT member(s) and "
                               + $"{shell.Shell.RequiredStatics.Count} static(s) are REFERENCED and NOT created. "
                               + "IT IS A FRAGMENT, NOT A BLOCK.");
            }

            foreach (var absent in slot.NotDeclared)
                output.WriteLine("  AUTHORED   " + absent);
        }

        // 🔴 THE OBLIGATIONS, AND THE FIRST OF THEM IS WHY THIS WHOLE SEAM EXISTS. A hand-written slot FC
        // was deployed and called by nothing: every vector timed out while the start echo reported
        // "commanded, observed to run" throughout, because both halves of that echo live in the copy layer,
        // which IS called. It cost a wave and three hours. The generator emits its own obligation so the
        // omission is a thing somebody DECLINED to do rather than a thing nobody was told about — and this
        // is where it has to arrive, or it was emitted into a variable and dropped.
        if (lane.Obligations.Count == 0)
        {
            output.WriteLine("  (no obligations — nothing was generated, so nothing is owed by this run)");
            return;
        }

        output.WriteLine();
        output.WriteLine($"  🔴 OBLIGATIONS ({lane.Obligations.Count}) — THIS TOOL CANNOT DISCHARGE ANY OF THEM:");
        foreach (var obligation in lane.Obligations)
            output.WriteLine("     - " + obligation);
    }

    /// <summary>
    /// 🔴 <b>What the PROGRAM generated and what it did not — printed on every run, including the one that
    /// declared nothing.</b>
    ///
    /// <para>Same rule as <see cref="WriteLane"/> and for the same reason: a section that appears only when
    /// something was generated teaches a reader that its absence means there was nothing left to generate,
    /// which is the opposite of the truth on every lane that exists today. <b>An instance DB reported as
    /// AUTHORED is a projection of a moving interface being maintained by hand</b> — the committed
    /// <c>iDB_HopperBlockageStim.ir</c> is nine statics behind its own FB, which is what that costs.</para>
    /// </summary>
    private static void WriteProgram(ProgramGenerationResult? program, TextWriter output)
    {
        if (program is null)
        {
            output.WriteLine("PROGRAM     : NOT COMPUTED — the run stopped before any declaration was read. Nothing here says "
                           + "whether this program's cyclic OB or its instance DBs are generated or authored.");
            return;
        }

        output.WriteLine($"PROGRAM     : {program.Summary()}");

        if (program.CyclicOb is { } ob)
        {
            output.WriteLine($"  GENERATED  OB {ob.BlockName} — {ob.CalledBlocks.Count} call(s), in order: "
                           + string.Join(" → ", ob.CalledBlocks));
        }

        foreach (var db in program.InstanceDbs)
        {
            output.WriteLine($"  GENERATED  DB {db.DbName} — instance of {db.FbName}, members "
                           + (db.Members == InstanceDbMemberSource.ProjectedFromFb
                               ? "PROJECTED from that FB's interface. Every start value it carries was DECLARED."
                               : $"LEFT TO TIA, which fills them from the FB — INCLUDING its "
                                 + $"{db.InheritedStartValues.Count} start value(s)."));
        }

        foreach (var absent in program.NotDeclared)
            output.WriteLine("  AUTHORED   " + absent);

        if (program.Obligations.Count == 0)
            return;

        output.WriteLine();
        output.WriteLine($"  🔴 OBLIGATIONS ({program.Obligations.Count}) — THIS TOOL CANNOT DISCHARGE ANY OF THEM:");
        foreach (var obligation in program.Obligations)
            output.WriteLine("     - " + obligation);
    }

    /// <summary>
    /// 🔴 <b>What the mirror was checked against — the reserved regions, and the ceiling on what that
    /// proves.</b>
    ///
    /// <para><b>Printed whether or not any were declared</b>, because the empty case is the one that has
    /// to be said out loud: an empty reservation list is <i>the absence of a claim</i>, not the claim that
    /// the area is otherwise empty. The measured collision this whole mechanism exists for happened in a
    /// deployment where nobody had written the neighbour down, and it would have happened identically with
    /// this check installed. <b>The check can only ever see a neighbour somebody declared</b>, and the fix
    /// for an undeclared one is to declare it — never to strengthen the check.</para>
    /// </summary>
    private static void WriteNeighbours(MirrorGeometry geometry, TextWriter output)
    {
        var reserved = geometry.ReservedRegions;

        output.WriteLine($"NEIGHBOURS  : {reserved.Count} reserved region(s) declared by the binding; every mirror region was compared");
        output.WriteLine("              against every one of them, and none of them was entered.");

        foreach (var region in reserved)
            output.WriteLine($"  CLEAR OF  {region.Describe()}");

        if (reserved.Count == 0)
        {
            output.WriteLine("  (none declared — *** WHICH IS NOT THE SAME AS 'THE AREA IS OTHERWISE EMPTY'. *** This mirror was proved");
            output.WriteLine("   disjoint from ITSELF and bounded by the declared width; nothing here knows whether anything else in the");
            output.WriteLine("   deployed program holds registers in the same %M area. On 2026-08-23 something did, and 53 tags were");
            output.WriteLine("   overwritten every scan. Declare neighbours in `reservedRegions` — this check cannot find them for you.)");
        }
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
        Harness.Wire.InertSettle? inertSettle = null,

        // 🔴 WHAT THE DEPLOYMENT STAGED - the denominator the stamp's coverage is reported against. It
        // does NOT come from either of the two documents above, deliberately: a submission and a binding
        // describe the test, not the deployment, and a denominator taken from the same place as the
        // numerator can never report a gap. Null is "nobody supplied one" and stays loud.
        StagedCorpus? stagedCorpus = null,

        // 🔴 Whether anything DERIVED the mirror's neighbours — see LoopRequest.NeighboursNotDerived. It
        // is not read out of either document, deliberately: a binding's `reservedRegions` is the DECLARED
        // half, and no document can testify that something derived it.
        string? neighboursNotDerived = null)
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

        // 🔴 *** THE NEIGHBOURS THE BINDING DECLARED, AND WITHOUT THIS LINE `reservedRegions` PARSED,
        // VALIDATED AND WAS THEN SILENTLY IGNORED ON EVERY harness-run. *** Measured 2026-08-23: a binding
        // declaring the virtual panel's band produced a 318-register mirror straight through it,
        // --generate-only exit 4, clean — no refusal, no warning, no mention that a neighbour had been
        // declared at all. ReservedRegion exists because of a bit-for-bit collision on a running
        // controller (53 panel tags overwritten every scan, master enable among them); BatchPlanner was
        // wired to the guard the same day and this path was not, which left the guard binding on the batch
        // route and INERT on the one a person drives by hand.
        //
        // *** IT SITS IN Compose, NOT IN GenerateOnly. *** Every route out of this CLI — generate-only, the
        // refusing gateway, --verify — builds its geometry here, so the reservation cannot be reached past.
        // A guard installed on one entry point is the defect being fixed, not a smaller version of it.
        //
        // Malformed reservations are carried through and refused BY THE GEOMETRY rather than dropped: a
        // binding that declared a neighbour believes part of the area is off limits, and silently ignoring
        // a typo'd one restores exactly the silence this closes. DeclaredReservations owns that rule and
        // the empty-set branch, so the batch planner and this path cannot come to derive it differently.
        var geometry = DeclaredReservations.AppliedTo(
            MirrorGeometry.ForCpu1214C(
                retentiveBytes: binding.RetentiveBytes ?? 256,
                baseByte: binding.BaseByte ?? 1000,
                declaredRegisters: declaredRegisters),
            binding);

        return new LoopRequest(
            inputs.Vectors,
            inputs.Enumeration,
            inputs.Fidelity,
            inputs.BlockAuthor,
            inputs.Conflicts,
            geometry,
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
            InertSettle: inertSettle,
            StagedCorpus: stagedCorpus,
            NeighboursNotDerived: neighboursNotDerived,

            // 🔴 Gate 5c's second operand, read off the document rather than the derived slots — see
            // LoopRequest.MapAuthor for why it is document-level.
            MapAuthor: new AgentIdentity(binding.DeclaredBy ?? string.Empty),

            // 🔴 *** WHAT THE LANE GENERATES RATHER THAN HAS TYPED. *** One per slot, INCLUDING every slot
            // that declares nothing — see LoopRequest.LaneDeclarations for why the empty ones travel too.
            LaneDeclarations: (binding.Slots ?? new List<SlotBindingDocument>())
                .Select(ToLaneDeclaration)
                .ToArray(),

            // 🔴 *** AND WHAT THE PROGRAM GENERATES — the cyclic OB and the instance DBs. *** Absent leaves
            // it null, which every lane written before this field existed means, and the run then REPORTS
            // those artifacts as authored rather than passing over them.
            ProgramGeneration: ToProgramDeclaration(binding.GenerateProgram));
    }

    /// <summary>
    /// 🔴 <b>The <c>generateProgram</c> section, off the wire.</b> Every parse failure is a THROW, never a
    /// silently dropped field: a misspelt key here produces an artifact that is simply absent, and an absent
    /// instance DB preset is a ZERO on the controller — a plausible number rather than an error.
    /// </summary>
    private static ProgramDeclaration? ToProgramDeclaration(ProgramGenerationDocument? document)
    {
        if (document is null)
            return null;

        return new ProgramDeclaration(
            ToCyclicOb(document.CyclicOb),
            (document.InstanceDbs ?? new List<InstanceDbDocument>()).Select(ToInstanceDb).ToArray());
    }

    private static CyclicObDeclaration? ToCyclicOb(CyclicObDocument? document)
    {
        if (document is null)
            return null;

        if (document.BlockName is not { Length: > 0 } name)
            throw new InvalidDataException("`generateProgram.cyclicOb` declares no `blockName`. There is nothing to generate.");

        // The number and the event class travel together with the name or not at all. Hard rule 3 forbids
        // inventing a number, and an OB's is fixed by its event class rather than allocated — which is why
        // OBs are excluded from the 9000-9999 harness band.
        if (document.Number is not int number)
        {
            throw new InvalidDataException(
                $"`generateProgram.cyclicOb` declares `blockName` '{name}' and no `number`. It is REQUIRED and never "
                + "defaulted: an OB's number is fixed by its event class, so it is read off the project rather than chosen.");
        }

        if (document.SecondaryType is not { Length: > 0 } secondaryType)
        {
            throw new InvalidDataException(
                $"`generateProgram.cyclicOb` '{name}' declares no `secondaryType`. The event class decides which system "
                + "parameters TIA requires on the block, and the generator will not assume one.");
        }

        if (document.Title is not { Length: > 0 } title)
            throw new InvalidDataException($"`generateProgram.cyclicOb` '{name}' declares no `title`; the generator will not invent one.");

        var calls = (document.Calls ?? new List<ObCallDocument>()).Select((call, index) =>
        {
            if (call.Block is not { Length: > 0 } block)
                throw new InvalidDataException($"`generateProgram.cyclicOb.calls[{index}]` has no `block`. There is nothing to call.");

            if (call.NetworkTitle is not { Length: > 0 } networkTitle)
            {
                throw new InvalidDataException(
                    $"`generateProgram.cyclicOb.calls[{index}]` calls '{block}' with no `networkTitle`. Every network gets a "
                    + "title (C-201) and the generator will not invent one — least of all in the block whose entire content "
                    + "is the order its networks are in.");
            }

            // Absent `instance` is the CLAIM that this is an FC — stateless, with no instance at all.
            return new ObCall(
                block,
                string.IsNullOrWhiteSpace(call.Instance) ? null : call.Instance,
                networkTitle,
                string.IsNullOrWhiteSpace(call.NetworkComment) ? null : call.NetworkComment);
        }).ToArray();

        return new CyclicObDeclaration(new CyclicObNaming(name, number, secondaryType, title), calls);
    }

    private static InstanceDbDeclaration ToInstanceDb(InstanceDbDocument document, int index)
    {
        if (document.DbName is not { Length: > 0 } name)
            throw new InvalidDataException($"`generateProgram.instanceDbs[{index}]` declares no `dbName`. There is nothing to generate.");

        if (document.DbNumber is not int number)
        {
            throw new InvalidDataException(
                $"`generateProgram.instanceDbs[{index}]` ('{name}') declares no `dbNumber`. It is REQUIRED and never defaulted: "
                + "hard rule 3 forbids inventing one, and harness objects come from the reserved 9000-9999 range the caller "
                + "allocates from — `converter claim --allocate --kind block-number --type DB --floor 9000`.");
        }

        if (document.Fb is not { Length: > 0 } fb)
        {
            throw new InvalidDataException(
                $"`generateProgram.instanceDbs[{index}]` ('{name}') declares no `fb`. An instance DB is a projection of an FB's "
                + "interface; with no FB named there is no interface to project.");
        }

        if (document.Comment is not { Length: > 0 } comment)
        {
            throw new InvalidDataException(
                $"`generateProgram.instanceDbs[{index}]` ('{name}') declares no `comment`. Every DB in the committed corpus "
                + "carries one and the generator will not invent it.");
        }

        // 🔴 NO DEFAULT, AND A MISSPELT VALUE IS A THROW. The two shapes differ in what happens to the FB's
        // own start values — `projectedFromFb` drops every one of them unless a preset declares it, while
        // `leftToTia` inherits the lot. A default here would silently decide a question about presets.
        var members = document.Members switch
        {
            "projectedFromFb" => InstanceDbMemberSource.ProjectedFromFb,
            "leftToTia" => InstanceDbMemberSource.LeftToTia,
            null or "" => throw new InvalidDataException(
                $"`generateProgram.instanceDbs[{index}]` ('{name}') declares no `members`. Say `projectedFromFb` — the structure "
                + "is projected from the FB and every start value is dropped unless a preset declares it — or `leftToTia`, which "
                + "emits an empty MEMBERS section and lets TIA fill the instance from the FB INCLUDING its start values. There "
                + "is no default, because the two differ over exactly the presets."),
            var other => throw new InvalidDataException(
                $"`generateProgram.instanceDbs[{index}]` ('{name}') declares `members` '{other}'. The two values are "
                + "`projectedFromFb` and `leftToTia`."),
        };

        var presets = (document.Presets ?? new List<InstanceDbPresetDocument>()).Select((preset, j) =>
        {
            if (preset.Path is not { Length: > 0 } path)
                throw new InvalidDataException($"`generateProgram.instanceDbs[{index}].presets[{j}]` has no `path`. There is nothing to set.");

            return new InstanceDbPreset(path, preset.Value, preset.Cleared);
        }).ToArray();

        return new InstanceDbDeclaration(new InstanceDbNaming(name, number, fb, comment), members, presets);
    }

    /// <summary>
    /// 🔴 <b>One slot's <c>generate</c> section, off the wire — and an ABSENT section produces an EMPTY
    /// declaration rather than nothing at all.</b>
    ///
    /// <para>Dropping the slot would make "nobody declared anything for it" indistinguishable from "it does
    /// not exist", and the report's whole job here is to say which parts of a lane are still hand-built.
    /// This is the same rule the binding document already applies four times over — an absence is a fact to
    /// be carried, not a row to be skipped.</para>
    ///
    /// <para><b>Every parse failure is a THROW, never a silently dropped field.</b> A misspelt phase
    /// <c>kind</c> falling back to <c>Unstated</c> would produce a head whose reset pulse has no terms —
    /// which the generator refuses, but for the wrong reason and naming the wrong thing.</para>
    /// </summary>
    private static LaneDeclaration ToLaneDeclaration(SlotBindingDocument slot)
    {
        var id = slot.SlotId ?? string.Empty;
        var declared = slot.Generate;

        if (declared is null)
            return new LaneDeclaration(id);

        // The name and the number travel together or not at all. A name with no number would reach
        // SlotFcGenerator's own refusal, which is correct and says nothing about WHERE the number was
        // meant to come from; hard rule 3 forbids inventing one and the reserved range is 9000-9999.
        SlotFcNaming? naming = null;
        if (declared.SlotFcName is { Length: > 0 } || declared.SlotFcNumber is not null)
        {
            if (declared.SlotFcName is not { Length: > 0 } name)
            {
                throw new InvalidDataException(
                    $"slot '{id}' declares `slotFcNumber` and no `slotFcName`. A block number with no block names nothing.");
            }

            if (declared.SlotFcNumber is not int number)
            {
                throw new InvalidDataException(
                    $"slot '{id}' declares `slotFcName` '{name}' and no `slotFcNumber`. The number is REQUIRED and is never "
                    + "defaulted: hard rule 3 forbids inventing one, and harness objects come from the reserved 9000-9999 range "
                    + "the caller allocates from — `converter claim --allocate --kind block-number --type FC --floor 9000`.");
            }

            naming = new SlotFcNaming(name, number);
        }

        return new LaneDeclaration(
            id,
            naming,
            ToSlotCall(declared.StimulusHead, id, "stimulusHead"),
            ToSlotCall(declared.BlockUnderTest, id, "blockUnderTest"),
            ToStimHeadSpec(declared.StimHead, id));
    }

    private static SlotCall? ToSlotCall(CalledBlockDocument? call, string slotId, string field)
    {
        if (call is null)
            return null;

        if (call.Block is not { Length: > 0 } block)
            throw new InvalidDataException($"slot '{slotId}' declares `{field}` with no `block`. There is nothing to call.");

        // C-201: every network gets a title, and a generated one is no exception. Refused rather than
        // defaulted — a generator that invents "Network 1" produces a block that passes review and tells a
        // reader nothing about why the call is where it is.
        if (call.NetworkTitle is not { Length: > 0 } title)
        {
            throw new InvalidDataException(
                $"slot '{slotId}' declares `{field}.block` '{block}' with no `networkTitle`. Every network gets a title (C-201) "
                + "and the generator will not invent one: an invented title passes review and tells a reader nothing.");
        }

        // Absent `instance` is the CLAIM that this is an FC — stateless, with no instance at all — and the
        // emitted call then omits the leading positional argument. It is not a blank.
        return new SlotCall(block, string.IsNullOrWhiteSpace(call.Instance) ? null : call.Instance, title);
    }

    private static StimHeadSpec? ToStimHeadSpec(StimHeadSpecDocument? spec, string slotId)
    {
        if (spec is null)
            return null;

        if (spec.HeadName is not { Length: > 0 } head)
        {
            throw new InvalidDataException(
                $"slot '{slotId}' declares a `stimHead` with no `headName`. The emitted shell is named after it, and a fragment "
                + "nobody can attach to a head is a file with no owner.");
        }

        var phases = (spec.Phases ?? new List<StimPhaseDocument>())
            .Select(p => new StimPhase(
                p.Bit ?? string.Empty,
                p.End ?? string.Empty,
                p.Duration ?? string.Empty,
                ParsePhaseKind(p.Kind, p.Bit, slotId)))
            .ToArray();

        return new StimHeadSpec(
            head,
            spec.UutReset ?? string.Empty,
            spec.Watchdog ?? string.Empty,
            spec.Dwell ?? string.Empty,
            phases,
            spec.Causes?.ToArray() ?? Array.Empty<string>(),
            spec.CycleEdges?.ToArray() ?? Array.Empty<string>(),
            spec.OutcomeBits?.ToArray() ?? Array.Empty<string>());
    }

    /// <summary>
    /// 🔴 <b>An unrecognised phase kind is a THROW, exactly as an unrecognised <c>phaseTrigger</c> is.</b>
    ///
    /// <para><c>Reset</c> is the only kind the shell generator READS — it builds the reset pulse from the
    /// phases carrying it. So a typo'd <c>"rest"</c> falling back to <c>Unstated</c> yields a head with no
    /// reset phase, which the generator then refuses for having no reset pulse at all: a true refusal
    /// pointing at the wrong thing, sending its reader to redesign a phase list that was already right.</para>
    /// </summary>
    private static StimPhaseKind ParsePhaseKind(string? declared, string? bit, string slotId)
    {
        if (string.IsNullOrWhiteSpace(declared))
            return StimPhaseKind.Unstated;

        if (Enum.TryParse<StimPhaseKind>(declared, ignoreCase: true, out var parsed) && parsed != StimPhaseKind.Unstated)
            return parsed;

        throw new InvalidDataException(
            $"slot '{slotId}' declares phase '{bit}' with kind '{declared}', which is not one of Disarm, Reset, Verify, Scenario "
            + "or Settle. *** REFUSED RATHER THAN TREATED AS UNSTATED: *** Reset is the only kind the shell reads, so a typo here "
            + "silently produces a head that can never clear the block under test — and every cleardown outcome then reports "
            + "whatever was already standing.");
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

        // *** WHAT THE RUN IS WORTH, NOT ONLY WHETHER IT IS ALLOWED. *** The same block the gate report
        // prints: distinct assertions cited over the enumeration's size, per subject. A wave that passes
        // every gate can still buy no coverage at all, and that is precisely what five rig events did.
        foreach (var line in (gate?.Coverage ?? AssertionCoverage.NotComputed).Lines())
            output.WriteLine($"  {line}");

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

    /// <summary>
    /// 🔴 <b>HASHED <i>n</i> OF <i>m</i>, AND WHICH STAGED OBJECTS THE STAMP DID NOT COVER.</b>
    ///
    /// <para>*** MEASURED, IN <c>docs/18-project-workbench.md</c> §5 <b>"Phase 10 — Wave time"</b>, under
    /// <i>"THE BUILD STAMP DOES COVER THE PARAMETER DB"</i>: *** a wave's stamp was derived over a program
    /// set the parameter DB was not in, so <i>"compressing them changes the controller without changing the
    /// stamp"</i> — two result packages describing materially different programs, one stamp, and a
    /// verifying gateway that would not notice. ⚠️ <b>The eight-object figure that entry first carried was
    /// RETRACTED 2026-08-24: the deployed set was NINE</b> (stamp <c>622F3EB7</c>). The shape stands.</para>
    ///
    /// <para><b>Printed on every run, including the complete one and the one with no denominator.</b> Both
    /// of the other sentences are written by <see cref="StampCoverage.Line"/> itself, so this method never
    /// decides which case it is looking at — the only branch here is "no manifest was recorded at all",
    /// which is a run that stopped before the stamp was computed.</para>
    ///
    /// <para>🔴 <b>THE LABEL SAYS <i>WHICH</i> COVERAGE, AND THAT IS NOT COSMETIC.</b> It was the bare word
    /// <c>COVERAGE</c> while it was the only one on this path. It now shares an output stream with
    /// <see cref="AssertionCoverage"/>, which counts ASSERTIONS CITED over a third party's enumeration —
    /// a different unit, a different denominator, a different subject, and nothing to do with this number.
    /// Two things called "coverage" in one report is its own defect: the unqualified one reads as the
    /// general figure of which the other is a part, and it is not.</para>
    /// </summary>
    private static void WriteCoverage(ProgramManifest? manifest, TextWriter output)
    {
        if (manifest?.Coverage is not { } coverage)
        {
            output.WriteLine("STAMP COVERAGE: <not computed — this run stopped before the build stamp was derived, so it made no claim");
            output.WriteLine("                about what was hashed. That is NOT a stamp that covered everything.>");
            return;
        }

        output.WriteLine("STAMP COVERAGE: " + coverage.Line);
    }

    /// <summary>
    /// 🔴 <b><c>--staged <c>Name=source</c></c> rows into the corpus the build stamp is measured against.</b>
    ///
    /// <para><b>The source travels with the name, and that is not decoration.</b> A gap reported as
    /// <c>DB_Params</c> says something is missing; <c>DB_Params [lane 'vessel']</c> says which document to
    /// go and fix. <see cref="StagedCorpus.Union"/> keeps both sources when two lanes stage one object,
    /// for the same reason.</para>
    ///
    /// <para><b>No rows is NULL, never an empty corpus.</b> An empty one renders every run as
    /// <i>"hashed n of 0"</i> over an empty gap list — the shape of a check that examined nothing — and
    /// <see cref="StagedCorpus.Of"/> refuses it at construction.</para>
    /// </summary>
    private static StagedCorpus? StagedRows(IReadOnlyList<string> values)
    {
        if (values.Count == 0)
            return null;

        var rows = new List<StagedCorpusEntry>();

        foreach (var value in values)
        {
            var split = value.IndexOf('=', StringComparison.Ordinal);

            // A bare name would have to be filed under an invented source, and an invented source sends
            // the reader of a gap to a document that never mentioned the object.
            if (split <= 0 || split == value.Length - 1)
            {
                throw new FormatException(
                    $"--staged '{value}' is not a `Name=source` row. The NAME is what TIA matches an import on and what the "
                    + "build stamp's manifest records; the SOURCE names the document that staged it, e.g. "
                    + "--staged \"DB_Params=lane 'vessel'\". A row with no source produces a gap nobody can trace to a manifest.");
            }

            rows.Add(new StagedCorpusEntry(value[..split], value[(split + 1)..]));
        }

        return StagedCorpus.Of(rows);
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

        // 🔴 WHAT THE STAMP DID *NOT* HASH, ON THE CONSOLE OF EVERY RUN INCLUDING THE COMPLETE ONE. A line
        // that appears only when something is missing teaches a reader that its absence means everything
        // was covered — and a run with no denominator at all would then read like the cleanest of them.
        WriteCoverage(result.ProgramManifest, output);
        output.WriteLine();

        WriteInertRest(result.InertRest, result.Outcome.ToString(), output);

        // 🔴 *** WHAT THIS WAVE WAS WORTH, ON THE PATH THAT ACTUALLY SPENDS THE RIG. ***
        //
        // The numerator shipped with two renderings and NEITHER was here: `GateCli`, and `WriteGate`, whose
        // only caller is the `--generate-only` branch. In `harness-batch` the Generate step passes
        // `--generate-only` for lanes[0] ALONE while the Wave step is per-lane without it, so a two-lane
        // batch printed the fraction once, for lane 0, and never computed it for lane 1 — the exact blind
        // spot the count was built to close, still open for every lane but the first.
        //
        // It is taken off `result.Gate`, which the wave already carries, so it is the figure from the gate
        // THIS run was admitted by rather than a second computation that could disagree with it.
        //
        // IMMEDIATELY ABOVE THE VECTOR ACCOUNTING, because the two are read against each other: three
        // vectors that ran and bought two assertions is the shape nobody noticed for five days, and the
        // numbers that show it should not be separated by a screen.
        foreach (var line in (result.Gate?.Coverage ?? AssertionCoverage.NotComputed).Lines())
            output.WriteLine(line);

        output.WriteLine();

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

                // 🔴 *** AND WHAT IT DID NOT HASH. *** The objects above answer "what was this stamp
                // computed over"; they cannot answer "was that all of it", and that is the question a
                // verifying gateway silently got wrong — a stamp over 8 of 9 staged objects reads
                // identically to a complete one (docs/18-project-workbench.md §5 "Phase 10 — Wave time",
                // under "THE BUILD STAMP DOES COVER THE PARAMETER DB"). Rendered by the
                // SAME helper the per-package artifact uses, so the run document and the package
                // documents cannot come to say different things about one derivation.
                ["coverage"] = ResultPackageJson.CoverageOf(manifest.Coverage),
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

        // 🔴 *** THIS PROGRAM'S ACTUAL SCAN PERIOD, WITH ITS DENOMINATOR AND ITS SUBJECT. ***
        //
        // WireTiming.ScanPeriodMs is a compiled constant measured against ONE program, and thirty-odd
        // sites convert scans to milliseconds with it — the timeout backstop, the timer floor, the
        // observability floor. Its own doc comment says to re-measure whenever the program changes
        // materially, and nothing enforced that. Phase 4 made a lane something the tool GENERATES, so a
        // new lane is a new program inheriting the old figure silently.
        //
        // REPORTED AND COMPARED, NEVER SUBSTITUTED. Replacing the constant from a live sample would be
        // the worse failure: it would look measured while being just as capable of having been taken in
        // the wrong condition, which is how a 113-frame stationarity claim was once beautifully stable
        // and wrong. Emitted on EVERY run, including when it could not be measured, and saying which.
        var scan = result.Wave?.ScanPeriod is { } rate
            ? new JsonObject
            {
                ["measured"] = true,
                ["millisecondsPerScan"] = Math.Round(rate.MillisecondsPerScan, 4),
                ["scans"] = rate.Scans,
                ["windowSeconds"] = Math.Round(rate.Window.TotalSeconds, 3),
                ["buildStamp"] = $"16#{rate.BuildStamp:X8}",
                ["precondition"] = rate.Precondition,
                ["compiledConstantMs"] = WireTiming.ScanPeriodMs,
                ["deltaFraction"] = Math.Round(rate.DeltaFraction, 4),
                // The whole point of the comparison. A run that agrees says so; a run that does not is
                // the signal to re-measure the constant deliberately.
                ["disagreesWithConstant"] = rate.DisagreesWithConstant,
            }
            : new JsonObject
            {
                ["measured"] = false,
                ["reason"] = result.Wave?.ScanPeriodNotMeasured
                    ?? "no wave ran, so the scan counter was never sampled.",
                ["compiledConstantMs"] = WireTiming.ScanPeriodMs,
            };

        var document = new JsonObject
        {
            ["outcome"] = result.Outcome.ToString(),
            ["detail"] = result.Detail,
            ["programUnderTest"] = programUnderTest,
            ["postDownloadSettling"] = settling,
            ["scanPeriod"] = scan,

            // 🔴 *** WHAT THE SUBMISSION WAS WORTH, IN THE ARTIFACT THAT OUTLIVES THE RUN. ***
            //
            // It landed in NO artifact at all: `programUnderTest.coverage` above is StampCoverage — which
            // objects the build stamp hashed — and the assertion figure existed only as terminal output on
            // the `--generate-only` path. So a reviewer opening two lanes' result files could compare their
            // outcomes and not what either of them bought.
            //
            // 🔴 PER SUBJECT AND WITH NO AGGREGATE. See `ResultPackageJson.AssertionCoverageOf`: two
            // subjects summed is the denominator of neither, so nothing here is summable.
            //
            // A gate-less run renders `NotComputed` rather than being absent — an absent key reads as
            // "fine" to everyone who did not write the emitter, and 0 of 0 reads as a measurement.
            ["assertionCoverage"] = ResultPackageJson.AssertionCoverageOf(
                result.Gate?.Coverage ?? AssertionCoverage.NotComputed),

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
        output.WriteLine("                   [--staged \"<Name>=<source>\"...]");
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
        output.WriteLine("--neighbours-not-derived <sentence> records that NOTHING DERIVED who else occupies the mirror's %M area.");
        output.WriteLine("         Emitted by `harness-batch` when its derivation was declined or could not be obtained; it becomes a");
        output.WriteLine("         CAVEAT on every result package this run writes. ABSENT MEANS DERIVED — never 'no neighbours'.");
        output.WriteLine("--staged names WHAT THE DEPLOYMENT STAGED — the DENOMINATOR the build stamp's coverage is reported against,");
        output.WriteLine("         one `Name=source` row per object, e.g. --staged \"DB_Params=lane 'vessel'\". It is NOT --program:");
        output.WriteLine("         --program is what gets HASHED, and a short --program list is indistinguishable from a complete one");
        output.WriteLine("         unless something else states what should have been in it. MEASURED: a wave's stamp was derived over a");
        output.WriteLine("         set the parameter DB was not in, so compressing it changed the controller and not the stamp. (The");
        output.WriteLine("         deployed set was NINE objects, stamp 622F3EB7 — an earlier `eight objects` figure was retracted on");
        output.WriteLine("         2026-08-24; docs/18-project-workbench.md, section 5, `Phase 10 - Wave time`.) `harness-batch` emits");
        output.WriteLine("         these rows from the lane manifests; nothing derives them from --program, which would be the numerator");
        output.WriteLine("         measuring itself. WITH NONE, the run reports NO DENOMINATOR in as many words — an empty gap list is");
        output.WriteLine("         not a clean sheet. THE CORPUS IS NOT A STAMP INPUT: the same objects give the same stamp with or");
        output.WriteLine("         without it, because nothing about what a lane staged is on the controller.");
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
        output.WriteLine("         A slot whose binding carries a `generate` section ALSO gets its SLOT FC and its STIMULUS SHELL");
        output.WriteLine("         generated here. The slot FC is a deployable block and IS in the build stamp; the shell is");
        output.WriteLine("         NETWORKS 1..N OF A HEAD AND NOT A BLOCK, so it is written to " + StimShellFragment.Subdirectory + "/ and is never a");
        output.WriteLine("         manifest object. A slot that declares nothing is REPORTED AS AUTHORED — absent never means");
        output.WriteLine("         generated, and generated is never reported for something a person wrote.");
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
