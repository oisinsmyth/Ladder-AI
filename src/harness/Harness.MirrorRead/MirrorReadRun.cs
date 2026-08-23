using DeviceGuard;
using Harness.Wire;

namespace Harness.MirrorRead;

/// <summary>Exit codes. Every one names a distinct fact; none of them means "something went wrong".</summary>
public enum MirrorReadExit
{
    /// <summary>The area is exactly as wide as declared, and every check passed.</summary>
    Ok = 0,

    /// <summary>The device fence refused the target. No socket was opened.</summary>
    Refused = 1,

    /// <summary>The arguments do not describe a measurement. Nothing was contacted.</summary>
    Usage = 2,

    /// <summary>The session could not be opened.</summary>
    ConnectFailed = 3,

    /// <summary>
    /// A probe that had to answer did not — a timeout or a dropped socket rather than a refusal.
    /// *** THE RUN MEASURED NOTHING AND SAYS SO. *** Distinct from every verdict below, because those
    /// are conclusions and this is the absence of one.
    /// </summary>
    NotEstablished = 4,

    /// <summary>The fence itself threw. A fault in the fence, not a verdict about the device.</summary>
    FenceFault = 5,

    /// <summary>
    /// *** THE HEADLINE FAILURE. *** A register inside the declared area was refused by the server:
    /// the area on the device is NARROWER than the IR declares, so the widening did not reach the wire.
    /// </summary>
    NarrowerThanDeclared = 6,

    /// <summary>
    /// A register outside the declared area answered: the server exposes MORE than the IR declares.
    /// Reading the intended registers successfully is consistent with this too, which is why it is
    /// measured rather than assumed away.
    /// </summary>
    WiderThanDeclared = 7,

    /// <summary>The area is the declared width, but the scan counter did not advance between reads.</summary>
    ScanCounterNotRising = 8,
}

/// <summary>
/// The run: fence, connect, read, probe the declared edge, report.
///
/// <para><b>What this exists to settle.</b> A widening of <c>MB_HOLD_REG</c> from <c>WORD 35</c> to
/// <c>WORD 37</c> is a claim about what a Modbus client can see. An S7 marker read confirms the bytes
/// exist in <c>%M</c> and consults <c>MB_HOLD_REG</c> not at all — <c>MB_SERVER</c> is not in that
/// path — so it cannot settle the claim in either direction. This tool asks the server.</para>
///
/// <para><b>Two probes, not one, and the second is the one that matters.</b> Reading the new registers
/// proves the area is AT LEAST wide enough. It is equally consistent with a server exposing far more
/// than intended. The register one past the declared end being REFUSED is what closes it from the
/// other side, and only the pair pins the width.</para>
///
/// <para><b>The sweep carries its own control.</b> A refusal at the far end proves nothing unless
/// something nearby succeeded — "everything is refused" produces the same reading. So the sweep must
/// straddle the edge (enforced in <see cref="MirrorReadOptions.Refusals"/>) and the verdict refuses to
/// conclude unless at least one inside probe answered.</para>
/// </summary>
public static class MirrorReadRun
{
    /// <summary>
    /// Run it. <paramref name="factory"/> is the ONLY route to a socket, so a test that asserts it was
    /// never invoked has proved the fence held — as an observable consequence, not as an exit code a
    /// disconnected gate could also produce.
    /// </summary>
    public static MirrorReadExit Execute(MirrorReadOptions options, IRegisterSourceFactory factory, TextWriter output) =>
        ExecuteAndReport(options, factory, output).Exit;

    /// <summary>
    /// The same run, handing back the document as well as the code.
    ///
    /// <para><b>Every path returns a report, including the ones that measured nothing</b> — see
    /// <see cref="MirrorReadReport.NotMeasured"/>. A consumer that got no file on a refusal could not
    /// tell it from a run nobody started, and those are the two states hardest to tell apart already.</para>
    /// </summary>
    public static MirrorReadOutcome ExecuteAndReport(MirrorReadOptions options, IRegisterSourceFactory factory, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(output);

        output.WriteLine("harness-mirror-read — READ-ONLY. Connect, read holding registers, print, disconnect.");
        output.WriteLine("  This binary contains no write path. That is not a claim resting on a flag: no method");
        output.WriteLine("  in this assembly names a write member, checked by an IL walk over the compiled");
        output.WriteLine("  assembly with a denominator and a live positive control (MirrorReadStructureTests).");
        output.WriteLine("  What it does NOT claim: the referenced Harness.Wire assembly does contain a write —");
        output.WriteLine("  the harness proper needs one. Nothing here reaches it.");
        output.WriteLine();
        output.WriteLine($"target      : {options.Address}:{options.Port} unit {options.UnitId}");
        output.WriteLine($"declared    : MB_HOLD_REG covers {options.DeclaredRegisters} register(s) — 0..{options.LastDeclaredRegister}");
        output.WriteLine($"boundary    : single-register probes {options.BoundaryFrom}..{options.BoundaryTo}");
        output.WriteLine($"allowlist   : {options.AllowlistPath ?? "<none configured>"}");
        output.WriteLine();

        var refusals = options.Refusals;
        if (refusals.Count > 0)
        {
            output.WriteLine("== usage ==");
            foreach (var refusal in refusals)
                output.WriteLine($"  - {refusal}");
            output.WriteLine("  Nothing was contacted.");
            return new MirrorReadOutcome(MirrorReadExit.Usage,
                MirrorReadReport.NotMeasured(options, MirrorReadExit.Usage, refusals));
        }

        // ---- 1. THE FENCE, ABOVE EVERY LINE THAT COULD OPEN A SOCKET ----
        //
        // Wrapped, and the claim it makes on the way out is exact: this block sits entirely above the
        // factory call below, so on a throw here "no connection was attempted" is true BY CONSTRUCTION.
        //
        // DeviceAccessGuard, never DeviceWriteGuard. The live allowlist entry reads writeEligible:false
        // and that is correct — this run is a read, reads are governed by the access guard, and asking
        // the write guard would refuse a device that is properly authorised for exactly what we are
        // doing. An over-firing gate decays into a warning.
        GuardDecision decision;
        try
        {
            decision = new DeviceAccessGuard(AllowlistFile.Load(options.AllowlistPath)).Check(options.Address);
        }
        catch (Exception ex)
        {
            output.WriteLine("== device fence (DeviceAccessGuard) ==");
            output.WriteLine($"  reason  : FenceFault ({ex.GetType().Name})");
            output.WriteLine($"  verdict : REFUSED — the fence could not reach a decision: {ex.Message}");
            output.WriteLine("  REFUSED — no connection attempted. This is a FAULT IN THE FENCE and not a verdict");
            output.WriteLine("  about the device: nothing examined the target at all.");
            return new MirrorReadOutcome(MirrorReadExit.FenceFault,
                MirrorReadReport.NotMeasured(options, MirrorReadExit.FenceFault, new[]
                {
                    $"the device fence could not reach a decision ({ex.GetType().Name}: {ex.Message}). That is a FAULT IN "
                    + "THE FENCE and not a verdict about the device: nothing examined the target at all.",
                }));
        }

        output.WriteLine("== device fence (DeviceAccessGuard — consulted BEFORE any socket) ==");
        output.WriteLine($"  reason  : {decision.Reason}");
        output.WriteLine($"  verdict : {decision.Message}");
        if (!decision.Allowed)
        {
            output.WriteLine("  REFUSED — no connection attempted.");
            return new MirrorReadOutcome(MirrorReadExit.Refused,
                MirrorReadReport.NotMeasured(options, MirrorReadExit.Refused, new[]
                {
                    $"the device fence REFUSED {options.Address} before any socket was opened ({decision.Reason}): "
                    + decision.Message,
                }));
        }

        output.WriteLine($"  entry   : {decision.MatchedEntry?.DisplayLabel ?? "<none>"}");
        output.WriteLine($"  note    : the entry's writeEligible={decision.MatchedEntry?.WriteEligible.ToString() ?? "?"} is not consulted here " +
                         "and must not be — that flag governs DeviceWriteGuard, and this is a read.");
        output.WriteLine("  (the connection below is opened only because this said ALLOWED)");
        output.WriteLine();

        // ---- 2. CONNECT ----
        output.WriteLine("== connect ==");
        IRegisterSource source;
        try
        {
            source = factory.Open(options.Address, options.Port, options.UnitId);
            output.WriteLine($"  open({options.Address}, {options.Port}, unit {options.UnitId}) -> ok");
        }
        catch (Exception ex)
        {
            output.WriteLine($"  open({options.Address}, {options.Port}, unit {options.UnitId}) -> FAILED");
            output.WriteLine($"  {ex.GetType().FullName}: {ex.Message}");
            output.WriteLine("  Nothing further attempted.");
            return new MirrorReadOutcome(MirrorReadExit.ConnectFailed,
                MirrorReadReport.NotMeasured(options, MirrorReadExit.ConnectFailed, new[]
                {
                    $"the session would not open ({ex.GetType().Name}: {ex.Message}), so nothing was read. An "
                    + "unreachable device is not a verified one.",
                }));
        }

        using (source)
        {
            output.WriteLine();

            // 🔴 THE ONE RETRY IN THIS TOOL, AND IT COVERS ONE CODE.
            //
            // A download stops the CPU, so a run placed after one meets a scan counter that has not
            // started again yet. That is exit 8 — a liveness fact, not a width verdict — and it clears by
            // itself. The wave answers the identical transient by ASKING AGAIN and reporting how many
            // attempts it needed (the blind 15 s wait it replaced was measured to be too short), so this
            // reuses that shape rather than inventing a second policy for one event.
            //
            // *** NOTHING ELSE IS RETRIED. *** A narrower or wider area is a CONCLUSION, and re-rolling a
            // conclusion until it changes is how a check becomes a random number generator. The loop
            // condition says so in one line, and the attempt count travels in the report either way.
            MirrorReadOutcome outcome;
            var attempt = 0;

            while (true)
            {
                attempt++;
                outcome = Measure(options, source, output, attempt);

                if (outcome.Exit != MirrorReadExit.ScanCounterNotRising || attempt > options.ScanRetries)
                    break;

                output.WriteLine();
                output.WriteLine($"== the scan counter has not moved — asking again, attempt {attempt + 1} of "
                    + $"{options.ScanRetries + 1}, {options.ScanRetryIntervalMs} ms apart ==");
                output.WriteLine("   A download stops the CPU, so a counter that has not started yet is EXPECTED here and");
                output.WriteLine("   is not a width finding. Only this code is retried: a width verdict is a conclusion.");
                output.WriteLine();

                if (options.ScanRetryIntervalMs > 0)
                    Thread.Sleep(options.ScanRetryIntervalMs);
            }

            return outcome;
        }
    }

    private static MirrorReadOutcome Measure(MirrorReadOptions options, IRegisterSource source, TextWriter output, int attempt)
    {
        var findings = new List<string>();

        // ---- 3. THE CONTROL REGISTERS, FIRST READING ----
        output.WriteLine("== step 1: control registers 0..3, first reading ==");
        var controlA = RegisterRead.Perform(source, 0, 4);
        output.WriteLine($"  FC03(0, 4)  {controlA.Describe()}");
        ReportControl(controlA, output);
        output.WriteLine();

        // *** THE INTERVAL IS MEASURED, NOT THE REQUESTED WAIT. *** The scan advance is only a rate
        // when it is divided by the time that actually passed, and the requested wait is not that time:
        // the whole-area read and the boundary sweep happen in between, and each costs a round trip.
        // Dividing by the requested figure would report a per-scan period that is wrong by however long
        // the intervening probes took, which is exactly the kind of derived-then-quoted number this
        // project keeps having to retract.
        var betweenControls = System.Diagnostics.Stopwatch.StartNew();

        // ---- 4. THE WHOLE DECLARED AREA ----
        //
        // The full width, because that is the question in its plainest form: can a Modbus client see the
        // area the IR declares? A read that fails is reported as the failure it is. It is NOT narrowed
        // and retried into a success — the sweep below finds the real edge, and it reports it under a
        // FAILING verdict, never as a pass on a smaller question.
        //
        // 🔴 IT IS SPLIT ACROSS TRANSACTIONS WHERE IT MUST BE, AND THAT IS NOT THE NARROWING JUST RULED
        // OUT. FC03 carries at most 125 registers; this step used to issue one request whatever the width
        // and only worked because the live run declared 37. At 576 it would come back refused, and the
        // verdict would read as a boundary finding about the DEVICE when the request was simply illegal.
        // Splitting still asks about every register 0..n-1 — it changes the transport, not the question.
        // The boundary probe below stays UNPAGED, deliberately: it reads the first register past the area
        // and requires exception 2, and paging that would mask the refusal being measured.
        //
        // 🔴 WHY PAGING AND NOT "NOT APPLICABLE ABOVE 125", WHICH WAS THE OTHER WAY TO STOP AN
        // UNREACHABLE VERDICT. Skipping the step above the protocol limit would have left the boundary
        // sweep as the only thing that reads anything — and the sweep touches FIVE registers at the edge.
        // Registers 4..1020 would then be read by nothing at all, so a hole in the middle of the mirror
        // would pass every check this tool has. An exemption that removes the only coverage of 99.5% of
        // the area is not a narrower claim, it is the same closed check arriving by a different door.
        //
        // ⚠️ AND THE TRANSPORT'S REFUSAL TO SPLIT STILL STANDS — it is about a different span. Its words
        // are "splitting it here would hide a map that was derived wrong; the map refuses this at
        // derivation time", and both halves are about a span MapAllocator DERIVED: a slot read wider than
        // 125 means the map cannot be served, and hiding that at the transport would paper over a
        // design-time defect. Neither premise holds here. This span is not derived — it is
        // --declared-registers, a number the operator reads off MB_HOLD_REG in the IR, so there is no
        // derivation that could be wrong. And the split is not "here": it is one layer above the
        // transport, in the tool, counted and named in the output below. NModbusTransport keeps refusing;
        // this pages above it.
        var pageSize = Harness.Map.ModbusLimits.MaxReadRegisters;
        var pages = (options.DeclaredRegisters + pageSize - 1) / pageSize;
        output.WriteLine($"== step 2: the whole declared area, registers 0..{options.LastDeclaredRegister} in {pages} FC03(s) of at most {pageSize} ==");
        var wide = RegisterRead.PerformPaged(source, 0, options.DeclaredRegisters, pageSize);

        // *** THE LABEL NAMES THE TRANSACTION THAT WAS ACTUALLY ISSUED. *** It used to be hardcoded
        // "FC03(0, n)" whatever came back, so a page that failed at 1000 was printed as a 1,024-register
        // read from 0 — a request nobody made, at an address nobody asked about. PerformPaged already
        // returns the failing page's OWN Start and Count for exactly this reason; the printer was
        // throwing that away and reasserting the whole span, which is the false-attribution the paging
        // was introduced to remove, left standing in the line that reports it.
        var label = pages == 1
            ? $"FC03(0, {options.DeclaredRegisters})"
            : wide.Ok
                ? $"{pages} x FC03 -> 0..{options.LastDeclaredRegister}"
                : $"FC03({wide.Start}, {wide.Count})";
        output.WriteLine($"  {label}  {wide.Describe()}");
        output.WriteLine();

        if (wide.Ok)
        {
            DumpRegisters(wide.Values, output);
            output.WriteLine();
        }
        else
        {
            output.WriteLine("  *** THE DECLARED AREA COULD NOT BE READ WHOLE. *** The per-register sweep below");
            output.WriteLine("  locates the real edge. Whatever it finds, this run does NOT pass: a narrower read");
            output.WriteLine("  that succeeds answers a smaller question than the one asked.");
            output.WriteLine();

            // The finding names the failing transaction, not the whole area. "Could not be read in one
            // FC03" was true of the old unpaged read and is now simply false — at 1,024 it is nine, and a
            // finding that misdescribes the request it is complaining about sends a reader to the device
            // to explain something the tool did.
            var where = pages == 1
                ? $"the single FC03(0, {options.DeclaredRegisters})"
                : $"FC03({wide.Start}, {wide.Count}), page {wide.Start / pageSize + 1} of {pages}";
            findings.Add($"the whole declared area (0..{options.LastDeclaredRegister}) could not be read. " +
                         $"The transaction that failed was {where}: {wide.Describe()}");
        }

        // ---- 5. THE BOUNDARY SWEEP ----
        output.WriteLine($"== step 3: single-register probes across the declared edge ({options.BoundaryFrom}..{options.BoundaryTo}) ==");
        output.WriteLine($"  registers 0..{options.LastDeclaredRegister} are DECLARED; {options.FirstUndeclaredRegister} and above are not.");
        output.WriteLine("  reg   declared   result");

        var probes = new List<RegisterRead>();
        for (var register = options.BoundaryFrom; register <= options.BoundaryTo; register++)
        {
            var probe = RegisterRead.Perform(source, register, 1);
            probes.Add(probe);
            var declared = register <= options.LastDeclaredRegister ? "yes" : "NO ";
            output.WriteLine($"  {register,3}   {declared}        {probe.Describe()}");
        }
        output.WriteLine();

        // ---- 6. THE CONTROL REGISTERS, SECOND READING ----
        if (options.IntervalMs > 0)
        {
            output.WriteLine($"== step 4: waiting {options.IntervalMs} ms, then reading the control registers again ==");
            Thread.Sleep(options.IntervalMs);
        }
        else
        {
            output.WriteLine("== step 4: control registers again (no wait requested) ==");
        }

        var controlB = RegisterRead.Perform(source, 0, 4);
        betweenControls.Stop();
        output.WriteLine($"  FC03(0, 4)  {controlB.Describe()}");
        ReportControl(controlB, output);
        output.WriteLine();

        // ---- 7. VERDICTS ----
        // `pages` is passed, not recomputed. Two derivations of one number is how GateParityTests came to
        // exist in this repo — the copy drifts, and here the copy would decide what the report claims was
        // examined while the original decided what actually was.
        var exit = Verdict(options, controlA, controlB, wide, probes, betweenControls.ElapsedMilliseconds, pages, findings, output);

        output.WriteLine();
        output.WriteLine("Operations performed on the device: TCP connect, FC03 read holding registers, disconnect.");
        output.WriteLine("No write function code was issued. No FC05, FC06, FC15 or FC16 appears in this binary.");

        // *** THE DOCUMENT IS A SECOND RENDERING OF THE SAME MEASUREMENT, NEVER A SECOND MEASUREMENT. ***
        // Every value below is one the lines above already printed. Recomputing any of them here would be
        // two derivations of one number, which is how the parity tests in this repository came to exist —
        // and here the copy would decide what a reviewer reads while the original decided what happened.
        var report = new MirrorReadReport(
            new MirrorReadTarget(options.Address, options.Port, options.UnitId),
            options.DeclaredRegisters,
            options.LastDeclaredRegister,
            (int)exit,
            exit.ToString(),
            WidthVerdictOf(exit),
            Measured: true,
            Attempts: attempt,
            new MirrorReadExamined(
                wide.Ok ? wide.Values.Length : 0, pages, probes.Count, options.BoundaryFrom, options.BoundaryTo),
            probes.Select(p => new MirrorProbeRow(
                p.Start, p.Start <= options.LastDeclaredRegister, p.Outcome.ToString(), p.SlaveExceptionCode)).ToArray(),
            new MirrorControl(
                Reading(controlA), Reading(controlB), betweenControls.ElapsedMilliseconds, Advance(controlA, controlB)),
            findings,
            MirrorReadReport.NotSeenBy(options, pages));

        return new MirrorReadOutcome(exit, report);
    }

    /// <summary>
    /// The exit code as the width question's answer.
    ///
    /// <para><b>Everything that is not one of the three verdicts is
    /// <see cref="MirrorWidthVerdict.NotEstablished"/></b>, including exit 8: a scan counter that did not
    /// rise says the copy layer may not be executing, and a run that cannot establish liveness has not
    /// established a width either. Mapping it to "as declared" because the reads happened to succeed
    /// would be the tool concluding from a run it just said it could not trust.</para>
    /// </summary>
    private static MirrorWidthVerdict WidthVerdictOf(MirrorReadExit exit) => exit switch
    {
        MirrorReadExit.Ok => MirrorWidthVerdict.ExactlyAsDeclared,
        MirrorReadExit.NarrowerThanDeclared => MirrorWidthVerdict.NarrowerThanDeclared,
        MirrorReadExit.WiderThanDeclared => MirrorWidthVerdict.WiderThanDeclared,
        _ => MirrorWidthVerdict.NotEstablished,
    };

    /// <summary>A control read, decoded exactly as <see cref="ReportControl"/> prints it — or the fact that it failed.</summary>
    private static MirrorControlReading Reading(RegisterRead read) =>
        read.Ok
            ? new MirrorControlReading(true,
                $"16#{RegisterWords.To32(read.Values[0], read.Values[1], RegisterWordOrder.HighWordFirst):X8}",
                ScanCount.FromRegisters(read.Values[2], read.Values[3], RegisterWordOrder.HighWordFirst).Raw)
            : new MirrorControlReading(false, null, null);

    /// <summary>The advance between the two control reads, or null when there was no pair to subtract.</summary>
    private static long? Advance(RegisterRead first, RegisterRead second) =>
        first.Ok && second.Ok
            ? ScanCount.FromRegisters(second.Values[2], second.Values[3], RegisterWordOrder.HighWordFirst)
                .Since(ScanCount.FromRegisters(first.Values[2], first.Values[3], RegisterWordOrder.HighWordFirst))
            : null;

    /// <summary>The build stamp and the scan counter, printed from the raw words they were built out of.</summary>
    private static void ReportControl(RegisterRead read, TextWriter output)
    {
        if (!read.Ok) return;

        var stamp = RegisterWords.To32(read.Values[0], read.Values[1], RegisterWordOrder.HighWordFirst);
        var scan = ScanCount.FromRegisters(read.Values[2], read.Values[3], RegisterWordOrder.HighWordFirst);

        output.WriteLine($"    r0 = 16#{read.Values[0]:X4}   r1 = 16#{read.Values[1]:X4}   " +
                         $"-> build stamp 16#{stamp:X8}  (HIGH-WORD-FIRST, measured on this rig 2026-08-13 and 2026-08-14)");
        output.WriteLine($"    r2 = 16#{read.Values[2]:X4}   r3 = 16#{read.Values[3]:X4}   " +
                         $"-> scan counter {scan.Raw}");
    }

    private static void DumpRegisters(ushort[] values, TextWriter output)
    {
        output.WriteLine($"== raw registers ({values.Length}) — words as read, before any interpretation ==");
        for (var i = 0; i < values.Length; i += 8)
        {
            var count = Math.Min(8, values.Length - i);
            var hex = string.Join(" ", values.Skip(i).Take(count).Select(v => v.ToString("X4")));
            output.WriteLine($"  r{i,3}  {hex}");
        }
    }

    /// <summary>
    /// What the readings license, and what they do not.
    ///
    /// <para>Ordered by severity, and every finding is printed even when an earlier one has already
    /// decided the exit code — a run that reports one problem and hides two is a worse artifact than
    /// one that reports three.</para>
    /// </summary>
    private static MirrorReadExit Verdict(
        MirrorReadOptions options,
        RegisterRead controlA,
        RegisterRead controlB,
        RegisterRead wide,
        IReadOnlyList<RegisterRead> probes,
        long measuredIntervalMs,
        int pages,
        List<string> findings,
        TextWriter output)
    {
        output.WriteLine("== verdict ==");

        var inside = probes.Where(p => p.Start <= options.LastDeclaredRegister).ToList();
        var outside = probes.Where(p => p.Start >= options.FirstUndeclaredRegister).ToList();

        var insideOk = inside.Where(p => p.Ok).ToList();
        var insideRefused = inside.Where(p => p.Outcome == ReadOutcome.RefusedByServer).ToList();
        var outsideOk = outside.Where(p => p.Ok).ToList();
        var outsideRefused = outside.Where(p => p.Outcome == ReadOutcome.RefusedByServer).ToList();
        var unanswered = probes.Where(p => p.Outcome == ReadOutcome.TransportFailed).ToList();

        // (a) The control for the whole sweep. Without an inside register that ANSWERED, a refusal
        //     outside is not evidence about a boundary — it is what a server refusing everything looks
        //     like, and the two are indistinguishable from here.
        if (insideOk.Count == 0)
        {
            findings.Add("NO register inside the declared area answered, so nothing in this sweep is " +
                         "evidence about the boundary: a refusal past the edge reads identically to a " +
                         "server refusing every request.");
        }
        else
        {
            output.WriteLine($"  control     : {insideOk.Count} declared register(s) answered " +
                             $"({string.Join(", ", insideOk.Select(p => "r" + p.Start))}), so the server is " +
                             "answering single-register reads at all.");
        }

        // (b) The two registers the widening was FOR.
        var latchLow = options.LastDeclaredRegister - 1;
        var latchHigh = options.LastDeclaredRegister;
        var latchesRead = wide.Ok
            || (Answered(probes, latchLow) && Answered(probes, latchHigh));

        if (latchesRead)
        {
            output.WriteLine($"  new area    : registers {latchLow} and {latchHigh} WERE READ. The area on the " +
                             "device is AT LEAST as wide as declared.");
        }

        if (insideRefused.Count > 0)
        {
            findings.Add($"*** THE AREA IS NARROWER THAN DECLARED. *** The server REFUSED " +
                         $"{string.Join(", ", insideRefused.Select(p => "r" + p.Start))}, which the IR declares as " +
                         $"inside MB_HOLD_REG (0..{options.LastDeclaredRegister}). The widening did not reach the " +
                         "wire: those registers are invisible to every Modbus client, including the harness.");
        }

        // (c) The other side of the boundary — the reason a successful read is not on its own enough.
        if (outsideOk.Count > 0)
        {
            findings.Add($"*** THE AREA IS WIDER THAN DECLARED. *** Register(s) " +
                         $"{string.Join(", ", outsideOk.Select(p => "r" + p.Start))} answered, and the IR declares " +
                         $"only 0..{options.LastDeclaredRegister}. Reading the intended registers is consistent " +
                         "with this, which is why the far side was probed.");
        }
        else if (outsideRefused.Count > 0 && insideOk.Count > 0)
        {
            output.WriteLine($"  edge        : register(s) {string.Join(", ", outsideRefused.Select(p => "r" + p.Start))} " +
                             "were REFUSED BY THE SERVER (a Modbus exception response, not a timeout), so the area " +
                             "is NOT wider than declared.");
        }

        if (unanswered.Count > 0)
        {
            findings.Add($"probe(s) {string.Join(", ", unanswered.Select(p => "r" + p.Start))} produced no answer " +
                         "from the server at all. That is a failed measurement, not a refusal, and no boundary " +
                         "conclusion may rest on it.");
        }

        // (d) Liveness. The stamp says WHICH program; the counter says it is EXECUTING.
        if (controlA.Ok && controlB.Ok)
        {
            var scanA = ScanCount.FromRegisters(controlA.Values[2], controlA.Values[3], RegisterWordOrder.HighWordFirst);
            var scanB = ScanCount.FromRegisters(controlB.Values[2], controlB.Values[3], RegisterWordOrder.HighWordFirst);
            var advance = scanB.Since(scanA);

            if (advance == 0)
            {
                findings.Add($"the scan counter did not advance between the two control reads (both " +
                             $"{scanA.Raw}). Either the CPU is not executing the copy layer or the two reads were " +
                             "not separated in time.");
            }
            else if (!scanB.IsPlausibleAdvanceFrom(scanA))
            {
                findings.Add($"the scan counter went from {scanA.Raw} to {scanB.Raw}, a modular advance of " +
                             $"{advance} — past the plausible ceiling, so it most likely went BACKWARDS " +
                             "(a restart or a different program) rather than forward.");
            }
            else
            {
                output.WriteLine($"  scan counter: {scanA.Raw} -> {scanB.Raw}, advance {advance} scan(s) over a " +
                                 $"MEASURED {measuredIntervalMs} ms between the two reads ({options.IntervalMs} ms " +
                                 "of it a requested wait, the rest the intervening probes). The copy layer is " +
                                 "EXECUTING, over Modbus — not a stale value in memory.");

                // Reported as a DERIVED figure with both of its inputs beside it, and deliberately not
                // compared against any compiled-in expectation. This tool does not know what the scan
                // period ought to be, and one that did could not be used to find out; a reader who has a
                // recorded figure can compare it against this and see a disagreement, which is the whole
                // value of printing it.
                if (advance > 0)
                {
                    output.WriteLine($"                derived: {measuredIntervalMs / (double)advance:F2} ms per scan " +
                                     $"({measuredIntervalMs} ms / {advance} scans). DERIVED, from two reads in one " +
                                     "session — not a scan-time measurement, and it includes whatever the counter " +
                                     "actually counts, which this tool does not know.");
                }
            }

            if (controlA.Values[0] != controlB.Values[0] || controlA.Values[1] != controlB.Values[1])
            {
                findings.Add($"the build stamp CHANGED between the two reads " +
                             $"(16#{controlA.Values[0]:X4}{controlA.Values[1]:X4} -> " +
                             $"16#{controlB.Values[0]:X4}{controlB.Values[1]:X4}). A download landed mid-run.");
            }
        }
        else
        {
            findings.Add("one or both control reads did not succeed, so neither the build stamp nor the " +
                         "scan counter was established.");
        }

        output.WriteLine();
        if (findings.Count == 0)
        {
            output.WriteLine($"  RESULT: the area is EXACTLY {options.DeclaredRegisters} register(s) wide " +
                             $"(0..{options.LastDeclaredRegister}), measured from BOTH sides — the declared " +
                             "registers answered and the first undeclared one was refused by the server.");

            // *** THE DENOMINATOR, ON EVERY PASSING RUN. *** Same shape as drift-check's COMPARED line:
            // every other number in this report is a reason something did not happen, and this is the one
            // that says how much was looked at. A pass over a small denominator is still a pass, but a
            // reader has to be able to see which one they got.
            output.WriteLine($"  EXAMINED    : {wide.Values.Length} register(s) read whole in {pages} FC03 " +
                             $"transaction(s), plus {probes.Count} single-register probe(s) across the edge " +
                             $"({options.BoundaryFrom}..{options.BoundaryTo}).");

            // WHAT THIS PASS CANNOT SEE, and it is new with paging rather than inherited.
            if (pages > 1)
            {
                output.WriteLine($"  NOT SEEN    : those {pages} transactions were {pages} separate moments on a " +
                                 "LIVE mirror — the scan counter moved between them — so the words above are a " +
                                 "reassembly and NOT one snapshot. Registers in different pages may never have " +
                                 "held those values at the same instant. This step measures REACHABILITY, which " +
                                 "is per-register and survives that; nothing here licenses reading two registers " +
                                 "from different pages as a coherent pair.");
            }

            return MirrorReadExit.Ok;
        }

        output.WriteLine($"  RESULT: {findings.Count} finding(s). This run does NOT pass.");
        foreach (var finding in findings)
            output.WriteLine($"    - {finding}");

        // Precedence: the headline defect first; then the opposite defect; then the absence of a
        // measurement; then liveness. A run with several gets the most consequential code, and every
        // finding is on the page above regardless.
        if (insideRefused.Count > 0) return MirrorReadExit.NarrowerThanDeclared;
        if (outsideOk.Count > 0) return MirrorReadExit.WiderThanDeclared;
        if (unanswered.Count > 0 || insideOk.Count == 0 || !controlA.Ok || !controlB.Ok || !wide.Ok)
            return MirrorReadExit.NotEstablished;
        return MirrorReadExit.ScanCounterNotRising;
    }

    private static bool Answered(IEnumerable<RegisterRead> probes, int register) =>
        probes.Any(p => p.Start == register && p.Count == 1 && p.Ok);
}
