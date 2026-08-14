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
    public static MirrorReadExit Execute(MirrorReadOptions options, IRegisterSourceFactory factory, TextWriter output)
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
            return MirrorReadExit.Usage;
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
            return MirrorReadExit.FenceFault;
        }

        output.WriteLine("== device fence (DeviceAccessGuard — consulted BEFORE any socket) ==");
        output.WriteLine($"  reason  : {decision.Reason}");
        output.WriteLine($"  verdict : {decision.Message}");
        if (!decision.Allowed)
        {
            output.WriteLine("  REFUSED — no connection attempted.");
            return MirrorReadExit.Refused;
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
            return MirrorReadExit.ConnectFailed;
        }

        using (source)
        {
            output.WriteLine();
            return Measure(options, source, output);
        }
    }

    private static MirrorReadExit Measure(MirrorReadOptions options, IRegisterSource source, TextWriter output)
    {
        var findings = new List<string>();

        // ---- 3. THE CONTROL REGISTERS, FIRST READING ----
        output.WriteLine("== step 1: control registers 0..3, first reading ==");
        var controlA = RegisterRead.Perform(source, 0, 4);
        output.WriteLine($"  FC03(0, 4)  {controlA.Describe()}");
        ReportControl(controlA, output);
        output.WriteLine();

        // ---- 4. THE WHOLE DECLARED AREA, IN ONE TRANSACTION ----
        //
        // One request over the full width, because that is the question in its plainest form: can a
        // Modbus client see the area the IR declares? A wide read that fails is reported as the failure
        // it is. It is NOT narrowed and retried into a success — the sweep below finds the real edge,
        // and it reports it under a FAILING verdict, never as a pass on a smaller question.
        output.WriteLine($"== step 2: the whole declared area, one FC03 over registers 0..{options.LastDeclaredRegister} ==");
        var wide = RegisterRead.Perform(source, 0, options.DeclaredRegisters);
        output.WriteLine($"  FC03(0, {options.DeclaredRegisters})  {wide.Describe()}");
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
            findings.Add($"the whole declared area (0..{options.LastDeclaredRegister}) could not be read in one FC03: {wide.Describe()}");
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
        output.WriteLine($"  FC03(0, 4)  {controlB.Describe()}");
        ReportControl(controlB, output);
        output.WriteLine();

        // ---- 7. VERDICTS ----
        var exit = Verdict(options, controlA, controlB, wide, probes, findings, output);

        output.WriteLine();
        output.WriteLine("Operations performed on the device: TCP connect, FC03 read holding registers, disconnect.");
        output.WriteLine("No write function code was issued. No FC05, FC06, FC15 or FC16 appears in this binary.");
        return exit;
    }

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
                output.WriteLine($"  scan counter: {scanA.Raw} -> {scanB.Raw}, advance {advance} scan(s) over " +
                                 $"{options.IntervalMs} ms of requested wait. The copy layer is EXECUTING, over " +
                                 "Modbus — not a stale value in memory.");
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
