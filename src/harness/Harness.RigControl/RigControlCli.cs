using System.Diagnostics;
using DeviceGuard;
using Harness.S7;

namespace Harness.RigControl;

/// <summary>
/// <c>rig-control --run</c> — ask an allowlisted bench rig to go to RUN, then PROVE it did.
///
/// <para><b>Why this exists — CORRECTED AGAINST THE ARTIFACT, 2026-08-14.</b> The brief for this tool
/// said <i>"download-probe requires --disruptive and therefore stops the CPU"</i>. That is half true
/// and the half it omits changes what this tool is for. <c>--disruptive</c> answers
/// <c>StopModules/StopAll</c> AND <c>StartModules/StartModule</c>, and
/// <c>NoActionFirstPolicy.DisruptiveAllowances</c> already calls the latter <i>"the only route to RUN
/// this tool has"</i>. Measured live on 2026-08-14: <c>StopModules+StartModules answered, exit 0</c>,
/// and <c>rig-read</c> six minutes later read <c>Running (8)</c>. *** SO AN ORDINARY DOWNLOAD DOES NOT
/// LEAVE THE CPU STOPPED, AND THIS TOOL IS NOT THE ROUTINE RECOVERY PATH. ***</para>
///
/// <para><b>What it IS for is narrower and is named in the download probe's own source:</b>
/// <c>StartModules</c> is raised in the POST delegate, <i>after</i> the download has already stopped
/// the modules, so an abort there leaves the CPU stopped <b>"with no route to start it from inside
/// that download"</b>. This is that route, from outside — together with the plainer case of a CPU a
/// person stopped. It is a NEW CLASS OF WRITE to physical hardware and it is fenced accordingly.</para>
///
/// <para><b>The shape of the thing, in order.</b> Fence (no device contacted) → plan → <c>--yes</c> →
/// connect → identity → run state BEFORE → request → run state AFTER. Every step above the connect is
/// a pure function of files and arguments, so on any refusal "nothing was contacted" is true BY
/// CONSTRUCTION rather than by inspection — which is the only claim a fence is entitled to make.</para>
///
/// <para>🔴 <b>THE READ-BACK IS NOT OPTIONAL AND THERE IS NO FLAG THAT SKIPS IT.</b> A silent no-op is
/// the whole failure mode: a PI-service request can be acknowledged by a CPU that then does nothing,
/// and an exit 0 on the strength of the acknowledgement would be a confident false green about the one
/// fact the caller needs. A read-back that disagrees with the request is
/// <see cref="ExitReadBackMismatch"/>, and so is a read-back that COULD NOT BE PERFORMED — empty is
/// not clean.</para>
/// </summary>
public static class RigControlCli
{
    public const int ExitOk = 0;

    /// <summary>A gate refused. No mode change was attempted.</summary>
    public const int ExitRefused = 1;

    public const int ExitUsage = 2;

    /// <summary>The fence allowed, and the session could not be established.</summary>
    public const int ExitConnectFailed = 3;

    /// <summary>The CPU was asked to run and the request itself failed or was refused on the wire.</summary>
    public const int ExitRequestFailed = 4;

    /// <summary>
    /// The fence itself threw. Distinct from <see cref="ExitRefused"/> because they are different facts
    /// — one is a decision, the other is the decision not having been reached.
    ///
    /// <para>*** A CRASH IS LOUD WITHOUT BEING NAMED. *** A harness cannot tell an unhandled exception
    /// from a refusal; measured on this repo's own allowlist loader, where a null entry produced exit
    /// -1073741819 and the word REFUSED appeared nowhere.</para>
    /// </summary>
    public const int ExitFenceFault = 5;

    /// <summary>
    /// <c>--yes</c> was absent. The plan was printed and NOTHING WAS CONTACTED — no transport was even
    /// constructed. Modelled on <c>openness-cli block-layout --set</c>, whose discipline is the
    /// reference for a destructive verb in this repository.
    /// </summary>
    public const int ExitNotConfirmed = 10;

    /// <summary>
    /// *** THE STATE READ BACK DOES NOT MATCH THE STATE REQUESTED, OR COULD NOT BE READ AT ALL. ***
    /// Its own code, never a success with a note. Numbered 15 to match <c>block-layout --set</c>, the
    /// other verb in this repository whose read-back is the point of it.
    /// </summary>
    public const int ExitReadBackMismatch = 15;

    /// <summary>
    /// Flags that are refused BY NAME rather than treated as unknown options.
    ///
    /// <para>"Unknown option" reads as a typo and sends somebody looking for the right spelling. Each
    /// of these is a thing a person might reasonably reach for, and each of them is a thing this binary
    /// will not do — so the refusal states the position instead of hiding behind a parser.</para>
    ///
    /// <para><c>--skip-readback</c> and friends are listed here precisely BECAUSE they do not exist:
    /// the absence of a skip flag is a property worth making visible at the moment somebody looks for
    /// one.</para>
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> RefusedFlags = new Dictionary<string, string>
    {
        ["--stop"] = "there is no stop. This binary performs the RUN transition and nothing else; a " +
                     "capability built because it is symmetrical is a capability nobody weighed. Stop " +
                     "the CPU in TIA Portal.",
        ["--halt"] = "there is no stop. See --stop.",
        ["--cold-start"] = "only the plain RUN transition is built. A cold start discards retentive " +
                           "data and nothing asked for one.",
        ["--force"] = "there is no override. Every gate is either satisfied in the allowlist by a " +
                      "person, or the answer is no.",
        ["--override"] = "there is no override. See --force.",
        ["--skip-readback"] = "the read-back cannot be skipped. A request that is acknowledged and " +
                              "does nothing is the failure this tool exists to detect, so verifying " +
                              "the state afterwards is the operation, not a check on it.",
        ["--no-verify"] = "the read-back cannot be skipped. See --skip-readback.",
        ["--assume-run"] = "the read-back cannot be skipped. See --skip-readback.",
        ["--write-eligible"] = "writeEligible is a human authorisation recorded in the allowlist. It " +
                               "cannot be supplied on a command line, and this binary never edits an " +
                               "allowlist.",
    };

    public static string Usage =>
        """
        rig-control --run --target <address> [options]

          Asks an allowlisted bench rig to go to RUN, then reads the state back and verifies it.

          --run                 required. The only transition this binary performs.
          --target <address>    the device to reach. A routing hint, never an authorization.
          --allowlist <path>    the device allowlist. Or set LADDER_DEVICE_ALLOWLIST.
                                With neither, every target is REFUSED and no socket is opened.
          --yes                 required to contact anything. Without it the plan is printed and
                                NOTHING is contacted (exit 10).
          --rack <n>            default 0.
          --slot <n>            default 1.
          --connect-timeout-ms  default 10000.
          --settle-ms <n>       how long to wait for the CPU to reach RUN before failing the
                                read-back. Default 15000, polled every 500 ms.

        THIS BINARY CAN START A CPU AND CANNOT STOP ONE. There is no --stop, no --force and no
        flag that skips the read-back. Reversal is a person's job, in TIA Portal.
        """;

    /// <param name="args">The command line.</param>
    /// <param name="envLookup">Environment reader, injected so the fence is testable without ambient state.</param>
    /// <param name="transportFactory">
    /// Constructs the device transport. It is called ONLY after the fence has allowed and
    /// <c>--yes</c> has been seen, which is what lets a test assert "nothing was contacted" by counting
    /// calls rather than by reading the code.
    /// </param>
    public static int Run(
        string[] args,
        Func<string, string?> envLookup,
        Func<IRunTransitionTransport> transportFactory,
        TextWriter stdout,
        TextWriter stderr)
    {
        // ---- 0. ARGUMENTS -------------------------------------------------------------------
        foreach (var arg in args)
        {
            if (RefusedFlags.TryGetValue(arg, out var why))
            {
                stderr.WriteLine($"REFUSED: {arg} — {why}");
                return ExitUsage;
            }
        }

        if (args.Length == 0 || !args.Contains("--run"))
        {
            stderr.WriteLine(Usage);
            return ExitUsage;
        }

        var target = Option(args, "--target");
        if (string.IsNullOrWhiteSpace(target))
        {
            stderr.WriteLine("--target is required. " + Usage);
            return ExitUsage;
        }

        if (!TryInt(args, "--rack", 0, stderr, out var rack) ||
            !TryInt(args, "--slot", 1, stderr, out var slot) ||
            !TryInt(args, "--connect-timeout-ms", 10_000, stderr, out var connectTimeoutMs) ||
            !TryInt(args, "--settle-ms", 15_000, stderr, out var settleMs))
        {
            return ExitUsage;
        }

        var confirmed = args.Contains("--yes");
        var allowlistPath = AllowlistPath.Resolve(Option(args, "--allowlist"), envLookup);

        stdout.WriteLine("rig-control --run — asks a CPU to go to RUN, then verifies it did.");
        stdout.WriteLine($"target      : {target} rack {rack} slot {slot}");
        stdout.WriteLine($"allowlist   : {allowlistPath ?? "<none configured>"}");
        stdout.WriteLine($"confirmed   : {confirmed} (--yes)");
        stdout.WriteLine();
        stdout.WriteLine("*** THIS BINARY CAN START THIS CPU AND CANNOT STOP IT. *** There is no --stop and");
        stdout.WriteLine("    no restore point for a mode change. If RUN turns out to be the wrong state, a");
        stdout.WriteLine("    person reverses it in TIA Portal. Decide that before passing --yes.");
        stdout.WriteLine();

        // ---- 1. THE FENCE, ENTIRELY ABOVE THE TRANSPORT ------------------------------------
        //
        // Wrapped, and the claim it makes on the way out is exact: this block sits above every call to
        // transportFactory, so "no device was contacted" holds on a throw by construction.
        RunTransitionDecision decision;
        try
        {
            decision = RunTransitionFence.Check(target, allowlistPath);
        }
        catch (Exception ex)
        {
            stderr.WriteLine("== run fence ==");
            stderr.WriteLine($"  gate    : FenceFault ({ex.GetType().Name})");
            stderr.WriteLine($"  verdict : REFUSED — the fence could not reach a decision: {ex.Message}");
            stderr.WriteLine($"  where   : {ex.StackTrace}");
            stderr.WriteLine("  NO DEVICE WAS CONTACTED. This is a FAULT IN THE FENCE, not a verdict about");
            stderr.WriteLine("  the device — nothing examined the target at all.");
            return ExitFenceFault;
        }

        stdout.WriteLine("== run fence ==");
        stdout.WriteLine($"  gate    : {decision.Gate}");
        stdout.WriteLine($"  verdict : {decision.Message}");
        if (!decision.Allowed)
        {
            stdout.WriteLine("  REFUSED — no connection attempted, no mode change requested.");
            return ExitRefused;
        }

        var entry = decision.MatchedEntry!;
        stdout.WriteLine("  (the socket below is opened only because this said ALLOWED)");
        stdout.WriteLine();

        // ---- 2. THE PLAN, AND THE --yes GATE ------------------------------------------------
        stdout.WriteLine("== plan ==");
        stdout.WriteLine($"  connect to      : {target} rack {rack} slot {slot}, {connectTimeoutMs} ms timeout");
        stdout.WriteLine($"  verify identity : orderNumber == '{entry.OrderNumber!.Trim()}'");
        stdout.WriteLine(DescribeIdentityNarrowing(entry));
        stdout.WriteLine("  read run state  : before, so a CPU already running is left alone");
        stdout.WriteLine("  request         : RUN");
        stdout.WriteLine($"  read back       : poll up to {settleMs} ms until the CPU answers RUN");
        stdout.WriteLine();

        if (!confirmed)
        {
            stdout.WriteLine("  --yes was NOT given. NOTHING WAS CONTACTED — no transport was constructed, no");
            stdout.WriteLine("  socket opened, no request sent. Re-run with --yes to perform the plan above.");
            return ExitNotConfirmed;
        }

        // ---- 3. EVERYTHING BELOW THIS LINE TOUCHES A DEVICE ---------------------------------
        try
        {
            using var transport = transportFactory();
            return Perform(transport, target!, rack, slot, connectTimeoutMs, settleMs, entry, stdout);
        }
        catch (Exception ex)
        {
            // A crash is loud without being NAMED. Everything above is a decision; this is the device
            // half, and a harness must be able to tell an unexpected fault here from a refusal above.
            stderr.WriteLine($"FAULT: the device phase threw {ex.GetType().Name}: {ex.Message}");
            stderr.WriteLine(ex.StackTrace);
            stderr.WriteLine("The fence had already ALLOWED, so a mode change may or may not have been");
            stderr.WriteLine("requested. Read the CPU's state before assuming either way.");
            return ExitRequestFailed;
        }
    }

    // ------------------------------------------------------------------ the device phase

    private static int Perform(
        IRunTransitionTransport transport,
        string target,
        int rack,
        int slot,
        int connectTimeoutMs,
        int settleMs,
        AllowlistEntry entry,
        TextWriter stdout)
    {
        stdout.WriteLine("== connect ==");
        var connect = transport.Connect(target, rack, slot, connectTimeoutMs);
        stdout.WriteLine($"  Connect({target}, {rack}, {slot}) -> {connect}");
        if (!connect.Ok)
        {
            stdout.WriteLine("  connect FAILED — nothing further attempted.");
            return ExitConnectFailed;
        }
        stdout.WriteLine();

        // ---- identity, before anything is asked of the CPU -----------------------------------
        stdout.WriteLine("== identity ==");
        var clock = Stopwatch.StartNew();
        var orderStatus = transport.ReadOrderCode(out var orderCode);
        clock.Stop();
        stdout.WriteLine($"  ReadOrderCode -> {orderStatus}  [{clock.ElapsedMilliseconds} ms]");

        if (!orderStatus.Ok)
        {
            stdout.WriteLine("  REFUSED — the order code could not be read, so the device answering this");
            stdout.WriteLine("  address is unidentified. An unread identifier is a mismatch, never a pass.");
            return ExitRefused;
        }

        var expected = entry.OrderNumber!.Trim();
        var observed = (orderCode ?? string.Empty).Trim();
        stdout.WriteLine($"  expected      : '{expected}'");
        stdout.WriteLine($"  observed      : '{observed}'");
        if (!string.Equals(expected, observed, StringComparison.OrdinalIgnoreCase))
        {
            stdout.WriteLine("  REFUSED — this is NOT the approved device. No mode change requested.");
            return ExitRefused;
        }
        stdout.WriteLine("  order code MATCHES.");
        stdout.WriteLine(DescribeIdentityNarrowing(entry));
        stdout.WriteLine();

        // ---- run state BEFORE ----------------------------------------------------------------
        stdout.WriteLine("== run state, before ==");
        var beforeStatus = transport.ReadRunState(out var before);
        stdout.WriteLine($"  status : {beforeStatus}");
        stdout.WriteLine($"  state  : {before}");

        if (!beforeStatus.Ok)
        {
            // Not fatal to the operation, but it must not be reported as "not running": a failed read
            // says nothing about the CPU. Named, and the run continues to the request, because the
            // read-back afterwards is what decides the verdict either way.
            stdout.WriteLine("  the CPU was not asked successfully — this says NOTHING about whether it is");
            stdout.WriteLine("  running. Continuing to the request; the read-back below is what decides.");
        }
        else if (before.Running)
        {
            stdout.WriteLine();
            stdout.WriteLine("== nothing to do ==");
            stdout.WriteLine("  The CPU is ALREADY RUNNING. No request was sent — this tool exists to reach");
            stdout.WriteLine("  that state, not to re-assert it, and an unnecessary PI-service request to a");
            stdout.WriteLine("  running CPU is a change nobody asked for.");
            return ExitOk;
        }
        stdout.WriteLine();

        // ---- THE REQUEST ---------------------------------------------------------------------
        stdout.WriteLine("== request RUN ==");
        clock.Restart();
        var request = transport.RequestRun();
        clock.Stop();
        stdout.WriteLine($"  RequestRun() -> {request}  [{clock.ElapsedMilliseconds} ms]");
        if (!request.Ok)
        {
            stdout.WriteLine("  the request FAILED. Compare the elapsed time against a known-good round trip:");
            stdout.WriteLine("  a failure that cost a full round trip was ANSWERED by the CPU (a refusal); one");
            stdout.WriteLine("  that returned immediately was rejected locally and never reached it.");
            stdout.WriteLine("  Reading the state back anyway, because a failed request is not evidence that");
            stdout.WriteLine("  nothing happened.");
            var (afterFailedState, afterFailedStatus) = PollForRun(transport, settleMs, stdout);
            stdout.WriteLine($"  state after the failed request: {afterFailedState} (status {afterFailedStatus})");
            return ExitRequestFailed;
        }
        stdout.WriteLine("  the CPU ACKNOWLEDGED the request. That is not the same as having done it.");
        stdout.WriteLine();

        // ---- THE READ-BACK. NOT OPTIONAL. -----------------------------------------------------
        stdout.WriteLine("== read back ==");
        var (after, afterStatus) = PollForRun(transport, settleMs, stdout);
        stdout.WriteLine($"  status : {afterStatus}");
        stdout.WriteLine($"  state  : {after}");

        if (!afterStatus.Ok)
        {
            stdout.WriteLine("  MISMATCH — the read-back COULD NOT BE PERFORMED. That is not a pass: nothing");
            stdout.WriteLine("  was established about the CPU's mode, and 'no error seen' is not 'running'.");
            return ExitReadBackMismatch;
        }

        if (!after.Running)
        {
            stdout.WriteLine("  MISMATCH — the request was acknowledged and the CPU is NOT running. This is");
            stdout.WriteLine("  the silent no-op the read-back exists to catch. Which non-running mode it is");
            stdout.WriteLine("  in is not knowable from here; look in TIA Portal.");
            return ExitReadBackMismatch;
        }

        stdout.WriteLine();
        stdout.WriteLine("== done ==");
        stdout.WriteLine("  The CPU answered RUN on a read AFTER the request. Operations performed on the");
        stdout.WriteLine("  device: Connect, ReadOrderCode, ReadRunState, RequestRun, ReadRunState.");
        return ExitOk;
    }

    /// <summary>
    /// Poll the run state until the CPU answers RUN or the budget expires.
    ///
    /// <para>A CPU asked to run passes through STARTUP, which decodes to <c>NotRunning</c> — so a single
    /// immediate read would report a mismatch on a transition that was going to succeed. Polling makes
    /// the check about the OUTCOME rather than about the timing. The budget expiring is a MISMATCH, not
    /// a timeout that closes the question: a FAIL invites an argument and somebody looks.</para>
    /// </summary>
    private static (S7RunStateReading Reading, S7Status Status) PollForRun(
        IRunTransitionTransport transport, int settleMs, TextWriter stdout)
    {
        const int intervalMs = 500;
        var clock = Stopwatch.StartNew();
        var attempts = 0;

        S7Status status;
        S7RunStateReading reading;
        while (true)
        {
            attempts++;
            status = transport.ReadRunState(out reading);
            if (status.Ok && reading.Running) break;
            if (clock.ElapsedMilliseconds >= settleMs) break;
            Thread.Sleep(intervalMs);
        }

        clock.Stop();
        stdout.WriteLine($"  polled {attempts} time(s) over {clock.ElapsedMilliseconds} ms (budget {settleMs} ms)");
        return (reading, status);
    }

    // ------------------------------------------------------------------ narrowing, printed always

    /// <summary>
    /// What the identity check does NOT cover, printed on EVERY run — including the runs where it
    /// excludes nothing.
    ///
    /// <para>*** A NARROWING NOBODY CAN SEE BECOMES A PLACE TO HIDE, and a check that only speaks when
    /// it exempts something cannot be told from one that has stopped working. *** The serial number is
    /// the stronger identifier and this transport cannot read one: SZL 0x001C was refused at every
    /// index on this CPU, and the marker-DB route needs S7 variable access, which this rig refuses
    /// CPU-wide in both RUN and STOP. Refusing on it would make the gate permanently unsatisfiable —
    /// and a gate that refuses every legitimate run is removed within a week, by somebody right to
    /// remove it. So it is EXCLUDED, BY NAME, OUT LOUD.</para>
    /// </summary>
    private static string DescribeIdentityNarrowing(AllowlistEntry entry)
    {
        var excluded = new List<string>();
        if (!string.IsNullOrWhiteSpace(entry.SerialNumber))
            excluded.Add($"serialNumber ('{entry.SerialNumber!.Trim()}')");
        if (!string.IsNullOrWhiteSpace(entry.MacAddress))
            excluded.Add($"macAddress ('{entry.MacAddress!.Trim()}')");

        return excluded.Count == 0
            ? "  narrowing     : 0 declared identifier(s) EXCLUDED — the order code is the whole check."
            : $"  narrowing     : {excluded.Count} declared identifier(s) EXCLUDED and NOT CHECKED — " +
              string.Join(", ", excluded) + ". This transport cannot read them.";
    }

    // ------------------------------------------------------------------ args

    private static string? Option(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    /// <summary>
    /// Parse an integer option, REFUSING a malformed one rather than silently falling back to the
    /// default — a default applied to a value somebody meant to set addresses something other than
    /// what they asked for.
    /// </summary>
    private static bool TryInt(string[] args, string name, int fallback, TextWriter stderr, out int value)
    {
        var raw = Option(args, name);
        if (raw is null)
        {
            value = fallback;
            return true;
        }

        if (int.TryParse(raw, out value) && value >= 0) return true;

        stderr.WriteLine($"{name} '{raw}' is not a whole number of at least 0.");
        value = fallback;
        return false;
    }
}
