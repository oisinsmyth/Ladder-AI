using DeviceGuard;
using Harness.Gate;
using Harness.Loop;
using Harness.Map;
using Harness.Run;
using Harness.Wire;

namespace Harness.Verify;

/// <summary>What one <c>ReadControl()</c> call did. Admitted and refused are different facts, never a bool with a message.</summary>
/// <param name="Refusal">The refusal text, or null when the call was admitted.</param>
/// <param name="Snapshot">The control snapshot, or null when the call was refused.</param>
public sealed record ControlAttempt(string Label, uint ExpectedStamp, string? Refusal, ControlSnapshot? Snapshot)
{
    public bool Admitted => Refusal is null;

    /// <summary>Whether the refusal NAMES the rule it enforces. A guard that refuses anonymously is one nobody can act on.</summary>
    public bool NamesDb6 => Refusal?.Contains("DB-6", StringComparison.Ordinal) == true;
}

/// <summary>
/// 🔴 <b><c>harness-verify</c> — DB-6 exercised against a live device, READ-ONLY.</b>
///
/// <para><b>The gap it closes.</b> <c>MirrorClient</c> is constructed in exactly one production place
/// (<c>LoopRun.Execute</c>) and no executable reaches it: <c>harness-run --verify</c> stops at
/// <c>VerifyingDeviceGateway</c>, which reads the version registers through the raw transport and never
/// builds a client. So <c>VersionCheck.Confirm</c> and <c>MirrorClient.ReadControl</c>'s DB-6 guard had
/// been unit-tested against scripted transports and had <b>never executed against a real device</b> —
/// which is a different claim, and the one this binary makes.</para>
///
/// <para><b>THE COMPOSITION IS <c>harness-run --generate-only</c>'s, called rather than copied.</b>
/// <see cref="LoopCli.Compose"/> then <see cref="LoopRun.Generate"/>: the same documents, the same
/// loaders, the same map and the same stamp. A second parser here would be a second answer to the
/// question the whole tool turns on.</para>
///
/// <para>*** THREE CONTROLS, AND NONE OF THEM SUBSUMES THE OTHERS. ***
/// <list type="A">
/// <item><b>A — the composition's own client.</b> Whatever the device is carrying, this is the client a
/// real run would hold, and its verdict is the live one.</item>
/// <item><b>B — the POSITIVE CONTROL.</b> A client holding a DELIBERATELY WRONG stamp, derived here and
/// printed. It MUST be refused, and the refusal MUST name DB-6. <i>A guard only ever observed passing has
/// not been observed at all.</i></item>
/// <item><b>C — the ACCEPTANCE CONTROL.</b> A client holding the stamp READ OFF THE DEVICE THIS RUN —
/// measured, never a literal in this file. It MUST be admitted. Without it, "refused" is equally what a
/// guard that refuses everything produces, and <i>a gate that fires on cases outside its scope is noise,
/// and noise gets switched off.</i></item>
/// </list></para>
///
/// <para>⚠️ <b>CONTROL C IS NOT A CONFIRMATION OF THE DEPLOYMENT AND MUST NEVER BE READ AS ONE.</b> It
/// says the guard admits when the two stamps agree. It says nothing about whether the device is running
/// the program these documents describe — that is <see cref="VersionCheck"/>'s question, answered above
/// it, and the two are reported separately for exactly that reason.</para>
///
/// <para><b>It writes nothing.</b> The only device operations are TCP connect, FC03 and disconnect. The
/// claim is checked by an IL walk over the shipped assembly (<c>VerifyStructureTests</c>) with a
/// denominator and a live positive control, not by a constant somebody has to remember to change.</para>
/// </summary>
public static class VerifyRun
{
    /// <summary>
    /// Run it. Every collaborator arrives as a delegate, so the whole decision surface is exercisable
    /// with no filesystem and no socket — a decision only reachable through a process is one nobody tests.
    /// </summary>
    public static VerifyExit Execute(
        VerifyOptions options,
        Func<string, string> readFile,
        Func<string, IReadOnlyList<string>> expand,
        Func<string, int, byte, IRegisterTransport> connect,
        TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(readFile);
        ArgumentNullException.ThrowIfNull(expand);
        ArgumentNullException.ThrowIfNull(connect);
        ArgumentNullException.ThrowIfNull(output);

        output.WriteLine("harness-verify - DB-6 (\"ISOLATION BY CONSTRUCTION\") EXERCISED AGAINST A LIVE DEVICE.");
        output.WriteLine("READ-ONLY: TCP connect, FC03 read holding registers, disconnect. No write function code exists in this binary.");
        output.WriteLine();

        // ---- 1. THE INPUTS, READ SEPARATELY SO A FAILURE NAMES THE RIGHT FILE ------------------------
        IReadOnlyList<HarnessObject> program;
        try
        {
            program = ProgramUnderTest.Load(options.ProgramPaths, readFile, expand);
        }
        catch (Exception ex)
        {
            output.WriteLine($"NOTHING EXAMINED - the PROGRAM UNDER TEST could not be loaded: {ex.GetType().Name}: {Ascii.Of(ex.Message)}");
            return VerifyExit.NothingExamined;
        }

        SubmissionDocument submission;
        try
        {
            submission = SubmissionDocument.Read(readFile(options.SubmissionPath));
        }
        catch (Exception ex)
        {
            output.WriteLine($"NOTHING EXAMINED - could not read the SUBMISSION '{options.SubmissionPath}': {ex.GetType().Name}: {Ascii.Of(ex.Message)}");
            return VerifyExit.NothingExamined;
        }

        BindingDocument binding;
        try
        {
            binding = BindingDocument.Read(readFile(options.BindingPath));
        }
        catch (Exception ex)
        {
            output.WriteLine($"NOTHING EXAMINED - could not read the BINDING '{options.BindingPath}': {ex.GetType().Name}: {Ascii.Of(ex.Message)}");
            return VerifyExit.NothingExamined;
        }

        // ---- 2. COMPOSE — the same call harness-run --generate-only makes ----------------------------
        LoopRequest request;
        try
        {
            request = LoopCli.Compose(submission, binding, program, readFile);
        }
        catch (Exception ex)
        {
            output.WriteLine($"NOT COMPOSED - the submission and binding could not be composed: {ex.GetType().Name}: {Ascii.Of(ex.Message)}");
            output.WriteLine($"  submission: {options.SubmissionPath}");
            output.WriteLine($"  binding   : {options.BindingPath}");
            output.WriteLine("  There is no map and no build stamp, so there is nothing to check the device against. No socket was opened.");
            return VerifyExit.NotComposed;
        }

        // *** stopWhenInadmissible: false — DELIBERATE, AND THE SAME CHOICE --generate-only MAKES. *** The
        // gate is a verdict about the VECTORS; the map and the stamp are functions of the BINDING and the
        // program. This run neither deploys nor writes a vector, so an inadmissible submission costs it
        // nothing — and stopping on the gate would make DB-6 unexercisable whenever a submission is held
        // up on a declaration that has nothing to do with the version register.
        var generation = LoopRun.Generate(request, stopWhenInadmissible: false);

        if (!generation.Generated || generation.Map is null)
        {
            output.WriteLine($"NOT COMPOSED - generation stopped at {generation.Stopped}, so no map and no stamp exist.");
            output.WriteLine($"  {Ascii.Of(generation.Detail)}");
            foreach (var refusal in generation.Refusals)
                output.WriteLine("  - " + Ascii.Of(refusal));

            output.WriteLine("  No socket was opened. Empty is not clean: a run with no expected stamp cannot exercise a guard that compares one.");
            return VerifyExit.NotComposed;
        }

        var map = generation.Map;
        var stamp = generation.Stamp;

        WriteComposition(options, request, program, map, stamp, generation, output);

        if (stamp.Value == 0)
        {
            output.WriteLine("NOT COMPOSED - the composed build stamp is ZERO, which is what bit memory reads before anything writes it.");
            output.WriteLine("  A client holding one could not tell a running program from an absent one, and MirrorClient refuses to be");
            output.WriteLine("  constructed with it. No socket was opened.");
            return VerifyExit.NotComposed;
        }

        // ---- 3. THE FENCE, BEFORE ANY SOCKET --------------------------------------------------------
        //
        // *** THE REAL GUARD, NOT AN INTERFACE OVER IT. *** An IDeviceFence seam here would let the tests
        // exercise a fence that is not the one an operator runs — and this project's own rule is to test a
        // fence through the entry point a caller actually uses, asserting the OBSERVABLE CONSEQUENCE. The
        // consequence is `connect` never being called, and the tests assert exactly that against a real
        // allowlist file on disk.
        var decision = DeviceAccessGuard.FromPath(options.AllowlistPath).Check(options.Address);

        output.WriteLine("== device fence ==");
        if (!decision.Allowed)
        {
            output.WriteLine($"  REFUSED  {options.Address}: {decision.Reason} - {Ascii.Of(decision.Message)}");
            output.WriteLine("  NO SOCKET WAS OPENED. The fence runs before the connection, because a check performed after the bytes have");
            output.WriteLine("  moved authorizes nothing.");
            return VerifyExit.Refused;
        }

        output.WriteLine($"  ALLOWED  {options.Address}:{options.Port} unit {options.Unit}   (allowlist {options.AllowlistPath})");
        output.WriteLine();

        // ---- 4. THE WIRE ----------------------------------------------------------------------------
        IRegisterTransport transport;
        try
        {
            transport = connect(options.Address, options.Port, options.Unit);
        }
        catch (Exception ex)
        {
            output.WriteLine($"NOTHING READ - the transport would not open: {ex.GetType().Name}: {Ascii.Of(ex.Message)}");
            output.WriteLine("  *** AN UNREACHABLE DEVICE IS NOT A VERIFIED ONE, AND AN UNREACHED GUARD IS NOT A PASSING ONE. ***");
            return VerifyExit.NothingRead;
        }

        using (transport)
        {
            return AgainstTheDevice(options, map, stamp, transport, output);
        }
    }

    /// <summary>Everything from the first FC03 onward. Split out so the socket's lifetime is one <c>using</c>.</summary>
    private static VerifyExit AgainstTheDevice(
        VerifyOptions options, RegisterMap map, BuildStamp stamp, IRegisterTransport transport, TextWriter output)
    {
        var client = new MirrorClient(map, transport, stamp);

        // ---- 5. VersionCheck.Confirm — NEVER RUN AGAINST A DEVICE UNTIL NOW -------------------------
        VersionReport report;
        try
        {
            report = VersionCheck.Confirm(client, stamp, options.StableReads, options.MaxReads);
        }
        catch (Exception ex)
        {
            output.WriteLine($"NOTHING READ - VersionCheck.Confirm threw: {ex.GetType().Name}: {Ascii.Of(ex.Message)}");
            output.WriteLine("  No verdict was reached, so nothing about the device or the guard was established.");
            return VerifyExit.NothingRead;
        }

        WriteVersionReport(options, report, output);

        // ---- 6. DB-6 RULE 1/2 — the client-side guard, three times ----------------------------------
        output.WriteLine("== DB-6 rule 1/2: MirrorClient.ReadControl() ==");
        output.WriteLine("  \"the version register is checked before EVERY transaction batch, not only at connect - a download can land");
        output.WriteLine("   mid-session\". Below, the SAME map and the SAME open socket, with three different expected stamps.");
        output.WriteLine();

        var a = Attempt("A  the composition's own client", client, output);

        // *** THE WRONG STAMP IS DERIVED HERE AND PRINTED, NEVER A MAGIC NUMBER. *** It must differ from
        // BOTH the composed stamp and whatever the device published, or the control tests nothing; and it
        // must be non-zero, because MirrorClient refuses a zero stamp at construction and the refusal
        // would then come from the wrong place entirely.
        var wrong = WrongStamp(stamp.Value, report.Observed);
        output.WriteLine($"  the wrong stamp is 16#{wrong:X8} - the composed stamp 16#{stamp.Value:X8} with its low bit(s) perturbed until it");
        output.WriteLine($"  differs from both it and the observed 16#{report.Observed:X8}, and is non-zero. Derived here and printed; nothing is hardcoded.");
        output.WriteLine();

        var wrongClient = new MirrorClient(map, transport, new BuildStamp(wrong));
        var b = Attempt($"B  POSITIVE CONTROL - a client holding a DELIBERATELY WRONG stamp 16#{wrong:X8}", wrongClient, output);

        // ---- 7. THE ACCEPTANCE CONTROL --------------------------------------------------------------
        ControlAttempt? c = null;
        MirrorClient? acceptClient = null;

        if (report.Observed == 0)
        {
            output.WriteLine("  C  ACCEPTANCE CONTROL - *** COULD NOT BE RUN. *** The device published 16#00000000, and MirrorClient refuses");
            output.WriteLine("     to be constructed with a zero expected stamp. So this run cannot show the guard ADMITTING anything, and");
            output.WriteLine("     every refusal above is therefore equally consistent with a guard that refuses everything.");
            output.WriteLine();
        }
        else
        {
            var note = report.Observed == stamp.Value
                ? " - the same value as A, because the device IS carrying this build; A and C are one client's verdict reported twice"
                : " - MEASURED off the device by the read above, never a literal in this tool";

            acceptClient = new MirrorClient(map, transport, new BuildStamp(report.Observed));
            c = Attempt($"C  ACCEPTANCE CONTROL - a client holding the stamp READ OFF THE DEVICE, 16#{report.Observed:X8}{note}",
                acceptClient, output);
        }

        WriteDenominators(map, report, client, wrongClient, acceptClient, output);

        return Verdict(report, a, b, c, output);
    }

    /// <summary>One <c>ReadControl()</c>, reported whichever way it goes.</summary>
    private static ControlAttempt Attempt(string label, MirrorClient client, TextWriter output)
    {
        ControlAttempt attempt;
        try
        {
            var snapshot = client.ReadControl();
            attempt = new ControlAttempt(label, client.Expected.Value, null, snapshot);
        }
        catch (WireException ex)
        {
            attempt = new ControlAttempt(label, client.Expected.Value, Ascii.Of(ex.Message), null);
        }

        output.WriteLine($"  {label}");

        if (attempt.Snapshot is { } s)
        {
            output.WriteLine($"     ADMITTED - version 16#{s.Version:X8}, scan counter {s.ScanCounter.Raw}, "
                             + $"start bools [{Hex(s.StartBools)}], start echo [{Hex(s.StartEcho)}]");
        }
        else
        {
            output.WriteLine($"     REFUSED  - WireException, and it {(attempt.NamesDb6 ? "NAMES DB-6" : "*** DOES NOT NAME DB-6 ***")}:");
            foreach (var line in Wrap(attempt.Refusal!, 110))
                output.WriteLine("       " + line);
        }

        output.WriteLine();
        return attempt;
    }

    /// <summary>
    /// The final verdict, decided by <see cref="GuardCheck"/> and only PRINTED here.
    ///
    /// <para><b><see cref="VerifyExit.GuardDidNotBehave"/> outranks everything</b>: a refusal produced by
    /// a guard that refuses everything is not a refusal.</para>
    /// </summary>
    private static VerifyExit Verdict(
        VersionReport report, ControlAttempt a, ControlAttempt b, ControlAttempt? c, TextWriter output)
    {
        var verdict = GuardCheck.Of(report, a, b, c);

        output.WriteLine("== verdict ==");

        if (!verdict.GuardBehaved)
        {
            output.WriteLine("  *** THE GUARD DID NOT BEHAVE. This is a finding about THE HARNESS, not about the device.");
            foreach (var fault in verdict.Faults)
                foreach (var line in Wrap(fault, 110))
                    output.WriteLine("     " + line);

            output.WriteLine("  Whatever the version register said above is not usable until this is fixed.");
            return verdict.Exit;
        }

        if (verdict.Exit == VerifyExit.NotEstablished)
        {
            output.WriteLine("  !! THE GUARD WAS NOT FULLY EXERCISED - which is not the same as it misbehaving.");
            foreach (var fault in verdict.Faults)
                foreach (var line in Wrap(fault, 110))
                    output.WriteLine("     " + line);

            output.WriteLine($"  VersionCheck: {report.Outcome.ToString().ToUpperInvariant()} - that part of the run stands and is reported above.");
            return verdict.Exit;
        }

        output.WriteLine("  DB-6 client-side guard: EXERCISED AGAINST THE DEVICE AND BEHAVED. The wrong stamp was refused BY NAME; "
                         + (c!.ExpectedStamp == report.Observed ? "the device's own stamp was admitted." : "the acceptance control was admitted."));

        if (verdict.Exit == VerifyExit.Confirmed)
        {
            output.WriteLine($"  VersionCheck: CONFIRMED. The device is running the build these documents compose to (16#{report.Expected:X8}).");
            output.WriteLine("  Control A was ADMITTED - the same fact reached through the client rather than through VersionCheck.");
            return verdict.Exit;
        }

        output.WriteLine($"  VersionCheck: {report.Outcome.ToString().ToUpperInvariant()} - the device is NOT running the composed build.");
        output.WriteLine("  *** THAT IS A STATEMENT ABOUT THE DEVICE, NOT ABOUT THE GUARD. *** DB-6 detected it, named it, and refused to");
        output.WriteLine("  read a control region through a map derived for a different program. That is the outcome DB-6 exists to produce.");
        output.WriteLine($"  Control A was {(a.Admitted ? "ADMITTED" : "REFUSED")}, consistent with the version check above.");

        return verdict.Exit;
    }

    // -------------------------------------------------------------------------------------------------
    // Reporting
    // -------------------------------------------------------------------------------------------------

    private static void WriteComposition(
        VerifyOptions options,
        LoopRequest request,
        IReadOnlyList<HarnessObject> program,
        RegisterMap map,
        BuildStamp stamp,
        LoopGeneration generation,
        TextWriter output)
    {
        output.WriteLine("== composition - LoopCli.Compose + LoopRun.Generate, the same calls harness-run --generate-only makes ==");
        output.WriteLine($"  submission : {options.SubmissionPath}   ({request.Vectors.Count} vector(s))");
        output.WriteLine($"  binding    : {options.BindingPath}   ({request.Bindings.Count} slot(s), block {request.Naming.BlockName}, table {request.Naming.TagTableName})");

        // *** THE EXCLUSION LIST COMES FROM THE DERIVATION ITSELF, NOT FROM A SECOND READING OF THE
        // NAMING. *** BuildStamp.Derive reports what it refused to hash; re-deriving that here from
        // request.Naming would be a second reader of the same fact, and a value with two readers has as
        // many truths as it has readers. (Measured 2026-08-17: this exclusion is the whole reason a
        // promoted copy layer no longer invalidates the stamp it just wrote — commit 94ab188.)
        BuildStamp.Derive(map, request.Bindings, request.Naming, program, out var excluded);
        var excludedNames = excluded
            .Select(e => e.Contains(':', StringComparison.Ordinal) ? e[(e.IndexOf(':', StringComparison.Ordinal) + 1)..] : e)
            .ToHashSet(StringComparer.Ordinal);

        output.WriteLine($"  program    : {program.Count} object(s) supplied; THE BUILD STAMP IS TAKEN OVER {program.Count - excluded.Count} OF THEM.");
        foreach (var name in excluded)
            output.WriteLine($"               NOT STAMPED: {name}  <- the harness generates this one; hashing it would stamp the stamp");

        if (excluded.Count == 0)
        {
            output.WriteLine("               (no supplied object is the harness's own - legitimate when the copy layer has not been");
            output.WriteLine("               promoted into this tree, and worth noticing because the set changes the moment it is)");
        }

        output.WriteLine();
        output.WriteLine($"  map        : {map.TotalRegisters} register(s) at %M{map.Geometry.BaseByte}"
                         + $"  [control {map.Control.Length}, vectors {map.VectorBlock.Length}, results {map.ResultBlock.Length}]");
        output.WriteLine($"  version reg: {map.Version.Register}..{map.Version.End - 1}   scan counter: {map.ScanCounter.Register}..{map.ScanCounter.End - 1}");
        output.WriteLine($"  map hash   : {map.MapHash}");
        output.WriteLine($"  BUILD STAMP: {stamp.Literal}   <- computed from the artifacts above. THIS is what the device is checked against.");
        output.WriteLine($"  generation : {Ascii.Of(generation.Detail)}");
        output.WriteLine($"  gate       : {generation.Gate?.Verdict.ToString() ?? "<not evaluated>"} - REPORTED, and it does not stop this run:");
        output.WriteLine("               nothing is deployed and no vector is written here, so an inadmissible submission costs this run");
        output.WriteLine("               nothing, and stopping on it would make DB-6 unexercisable over a gate it has no bearing on.");
        output.WriteLine();
    }

    private static void WriteVersionReport(VerifyOptions options, VersionReport report, TextWriter output)
    {
        output.WriteLine("== VersionCheck.Confirm - section 9's version register, read off the device ==");
        output.WriteLine($"  Outcome       : {report.Outcome}");
        output.WriteLine($"  Observed      : 16#{report.Observed:X8}");
        output.WriteLine($"  Expected      : 16#{report.Expected:X8}");
        output.WriteLine($"  Reads         : {report.Reads}   (bound {options.MaxReads}; {options.StableReads} identical consecutive reads count as settled)");
        output.WriteLine($"  ReadsToSettle : {report.ReadsToSettle}");
        output.WriteLine();

        // 🔴 *** THE MEASUREMENT'S SCOPE, PRINTED WHERE A READER OF RESULTS MEETS IT. *** §9 says the value
        // "flaps or reads old until integration completes", and THAT window opens at a download. This run
        // opens no download and cannot; so what is measured below is the STEADY STATE of a device that has
        // been running for however long it has, not the post-download transient §9 is about. Saying which
        // condition a run actually entered is the difference between a measurement and a background reading.
        output.WriteLine("  *** WHAT ReadsToSettle DOES AND DOES NOT MEASURE - read this before quoting the number. ***");
        output.WriteLine($"  It is the read at which the settled run BEGAN: {report.ReadsToSettle} means the value was already stable when read");
        output.WriteLine($"  {report.ReadsToSettle} of {report.Reads} was taken, and stayed so for the {options.StableReads} reads that decided it.");
        output.WriteLine("  *** THIS RUN DID NOT ENTER THE CONDITION section 9 DESCRIBES. That window - \"flaps or reads old until integration");
        output.WriteLine("  completes\" - opens at a DOWNLOAD, and this binary cannot perform one. So this is the STEADY-STATE settling of a");
        output.WriteLine("  device that has been running for some time, which is a real measurement of the read path and is NOT evidence");
        output.WriteLine("  about the post-download transient. A non-recurrence is not a demonstration.");
        output.WriteLine();
        output.WriteLine("  Detail:");
        foreach (var line in Wrap(Ascii.Of(report.Detail), 110))
            output.WriteLine("    " + line);

        output.WriteLine();
    }

    /// <summary>
    /// <b>What was actually read, per client.</b> A total agrees with a wrong derivation as readily as with
    /// a right one; per-item evidence does not — so each client's own counter is printed, and the reader can
    /// see that three control reads were made rather than infer it from a sum.
    /// </summary>
    private static void WriteDenominators(
        RegisterMap map, VersionReport report,
        MirrorClient a, MirrorClient b, MirrorClient? c, TextWriter output)
    {
        var controlReads = 1 + 1 + (c is null ? 0 : 1);

        output.WriteLine("== denominators - what was actually read ==");
        output.WriteLine($"  client A round trips : {a.RoundTrips}  ({report.Reads} VersionCheck settling read(s) + 1 ReadControl)");
        output.WriteLine($"  client B round trips : {b.RoundTrips}  (the positive control's one ReadControl)");
        output.WriteLine(c is null
            ? "  client C round trips : 0  *** THE ACCEPTANCE CONTROL WAS NEVER CONSTRUCTED - see the verdict below ***"
            : $"  client C round trips : {c.RoundTrips}  (the acceptance control's one ReadControl)");
        output.WriteLine($"  ReadControl calls    : {controlReads} of 3, each one FC03 over control registers {map.Control.Register}..{map.Control.End - 1}");
        output.WriteLine($"  FC03 total           : {a.RoundTrips + b.RoundTrips + (c?.RoundTrips ?? 0)}");
        output.WriteLine("  writes               : 0, and unaddressable - see the structural walk in VerifyStructureTests.");
        output.WriteLine();
    }

    // -------------------------------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------------------------------

    /// <summary>
    /// A stamp that is neither the composed one nor the observed one, and is not zero.
    ///
    /// <para>Perturbation rather than a literal, so the control cannot silently become a no-op the day the
    /// composed stamp happens to equal whatever constant somebody typed. The walk is bounded and
    /// deterministic; with three values to avoid out of 2^32 it cannot run out.</para>
    /// </summary>
    public static uint WrongStamp(uint composed, uint observed)
    {
        for (uint delta = 1; delta < 64; delta++)
        {
            var candidate = composed ^ delta;
            if (candidate != 0 && candidate != composed && candidate != observed)
                return candidate;
        }

        // Unreachable: 63 candidates cannot all collide with three values.
        throw new InvalidOperationException("no wrong stamp could be derived, which is arithmetically impossible for three excluded values.");
    }

    private static string Hex(ushort[] registers) => string.Join(" ", registers.Select(r => r.ToString("X4")));

    /// <summary>Wrap a long refusal so it is read rather than skimmed. A refusal nobody finishes gets skipped.</summary>
    private static IEnumerable<string> Wrap(string text, int width)
    {
        var line = new System.Text.StringBuilder();

        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > width)
            {
                yield return line.ToString();
                line.Clear();
            }

            if (line.Length > 0) line.Append(' ');
            line.Append(word);
        }

        if (line.Length > 0)
            yield return line.ToString();
    }
}
