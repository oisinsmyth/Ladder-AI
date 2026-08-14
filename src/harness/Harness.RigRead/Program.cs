using System.Text;
using DeviceGuard;
using Harness.RigWrite;
using Harness.S7;

namespace Harness.RigRead;

/// <summary>
/// Reads the rig marker DB off a live PLC and reports what is actually there.
///
/// <para><b>Why this exists.</b> <see cref="MarkerDbLayout"/>'s offsets are COMPUTED from the S7
/// layout rules and have never been compared with a device. A SimaticML export states member names,
/// types and order and no offsets at all, so no amount of reading the project can settle them. One
/// whole-block read can: three <c>String[32]</c> headers landing at 2, 36 and 70 with plausible
/// <c>(declared, current)</c> pairs is three independent confirmations, and if they land elsewhere the
/// arithmetic is wrong and every address derived from it is wrong with it.</para>
///
/// <para><b>It also reads MARKER memory.</b> <c>--marker &lt;byteOffset&gt; --length &lt;bytes&gt;</c>,
/// mutually exclusive with <c>--db</c>. Added 2026-08-14 because the harness MIRROR lives in <c>%M</c>,
/// not in a DB — so the build stamp, the vector registers and the result registers were all
/// unreadable by any committed tool, and the only evidence a download had actually taken was the
/// download's own report of itself. <b>A manifest says what was sent; the stamp says what is
/// executing.</b> This adds no transport: <c>MBRead</c> was already being issued by
/// <see cref="DiagnoseFailedRead"/> as a CPU-wide-vs-block-specific discriminator, and was measured
/// working on this rig (<c>MBRead(0,1) -> ok</c>) the same morning a DB read on the same session
/// failed <c>0x00C00000</c>. What is new is a caller being able to say WHICH marker bytes.</para>
///
/// <para><b>It cannot write.</b> Not by policy — by construction. The only device operations it
/// performs are <c>ConnectTo</c>, <c>DBRead</c>, <c>MBRead</c>, <c>GetOrderCode</c> and
/// <c>PlcGetStatus</c> — the last asks the CPU for its mode and cannot change one; the calls that do
/// (<c>PlcStop</c>, <c>PlcHotStart</c>, <c>PlcColdStart</c>) are not on <see cref="IS7Client"/> at all
/// and are unreachable from here. <c>MBWrite</c> is never called and appears nowhere in this file.
/// See the project file for the assembly-level statement of the same property.</para>
///
/// <para><b>The fence runs before the socket.</b> <see cref="DeviceAccessGuard"/> is consulted first
/// and a refusal returns without a connection attempt. That ordering is the point of the fence — a
/// check performed after the bytes have moved authorizes nothing.</para>
/// </summary>
public static class Program
{
    private const int ExitOk = 0;
    private const int ExitRefused = 1;
    private const int ExitUsage = 2;
    private const int ExitConnectFailed = 3;
    private const int ExitReadFailed = 4;

    /// <summary>
    /// The fence itself threw. Distinct from <see cref="ExitRefused"/> because they are different
    /// facts — one is a decision, the other is the decision not having been reached — and a caller
    /// that could not tell them apart would report a broken allowlist as a refused device.
    ///
    /// <para>*** WHY THIS EXISTS: A CRASH IS LOUD WITHOUT BEING NAMED. *** Measured 2026-08-14 on
    /// <c>{"entries": [null]}</c>: the guard raised a NullReferenceException, the process exited
    /// -1073741819, and the word REFUSED appeared nowhere. The specific defect is fixed in
    /// <see cref="AllowlistFile"/>, but one hand-found instance implies a family, so the phase is
    /// wrapped as well. A harness cannot tell an unhandled exception from a refusal.</para>
    ///
    /// <para>⚠️ <b>KNOWN LIMIT: as of the 2026-08-14 fuzz sweep, NO INPUT REACHES THIS CATCH.</b>
    /// Twenty-one malformed documents — null/array/number/string/bool entries, a null or non-array
    /// <c>entries</c>, null members at three depths, a type mismatch, a 201-entry file — all produce
    /// a named refusal from <see cref="AllowlistFile"/> or <see cref="DeviceAccessGuard"/> instead.
    /// So this is currently <i>correct, wired and unfalsifiable in place</i>, and it is kept for the
    /// members of the family that sweep could not enumerate. Say so rather than let a future reader
    /// take its existence as evidence that the class was covered.</para>
    /// </summary>
    private const int ExitFenceFault = 5;

    public static int Main(string[] args)
    {
        var address = Option(args, "--address") ?? "10.10.10.10";

        // ---- 0. WHICH AREA, DECIDED BEFORE ANYTHING ELSE ----
        //
        // --marker and --db address different AREAS, and a tool that silently preferred one would be
        // reporting the wrong memory under a heading that reads correct. So the combination is a
        // NAMED usage error rather than a precedence rule, and so is --offset alongside --marker
        // (--marker IS the offset; accepting both would leave one of them silently ignored).
        var markerArg = Option(args, "--marker");
        var dbArg = Option(args, "--db");
        var offsetArg = Option(args, "--offset");
        var lengthArg = Option(args, "--length");
        var marker = markerArg is not null;

        if (marker && dbArg is not null)
            return Usage("--marker and --db name DIFFERENT AREAS (M memory vs a data block) and cannot " +
                         "both be read in one run. Pass one. There is deliberately no precedence rule.");
        if (marker && offsetArg is not null)
            return Usage("--offset belongs to --db. With --marker the byte offset IS the --marker value, " +
                         "so passing both would leave one of them silently ignored.");
        if (marker && lengthArg is null)
            return Usage("--marker requires --length. There is no natural default for a marker window, " +
                         "and the DB default (MarkerDbLayout.TotalBytes) is a BLOCK size — applying it to " +
                         "M memory would read a plausible-looking window nobody asked for.");

        if (!TryInt(args, "--rack", 0, out var rack) ||
            !TryInt(args, "--slot", 1, out var slot) ||
            !TryInt(args, "--db", MarkerDbLayout.DbNumber, out var db) ||
            !TryInt(args, "--offset", 0, out var dbOffset) ||
            !TryInt(args, "--marker", 0, out var markerOffset) ||
            !TryInt(args, "--length", MarkerDbLayout.TotalBytes, out var length))
            return ExitUsage;

        var offset = marker ? markerOffset : dbOffset;
        if (offset < 0)
            return Usage($"a byte offset cannot be negative; got {offset}.");
        if (length <= 0)
            return Usage($"--length must be at least 1 byte; got {length}. A zero-length read reports " +
                         "nothing and would exit 0 — empty is not clean.");

        var allowlistPath = AllowlistPath.Resolve(Option(args, "--allowlist"), Environment.GetEnvironmentVariable);
        if (allowlistPath is null)
        {
            Console.Error.WriteLine(
                $"No allowlist configured. Pass --allowlist <path> or set {AllowlistPath.EnvVar}. " +
                "With neither, every target is refused and no socket is opened.");
            return ExitUsage;
        }

        Console.WriteLine("rig-read — READ-ONLY. Connect, read, print. No write path exists in this binary.");
        Console.WriteLine($"target      : {address} rack {rack} slot {slot}");
        Console.WriteLine(marker
            ? $"read        : MARKER MEMORY %MB{offset}, {length} bytes (%M{offset}..%M{offset + length - 1})"
            : $"read        : DB{db}.DBB{offset}, {length} bytes");
        Console.WriteLine($"allowlist   : {allowlistPath}");
        Console.WriteLine();

        // ---- 1. THE FENCE, BEFORE ANY SOCKET ----
        //
        // Wrapped, and the claim it makes on the way out is EXACT. This block sits entirely above
        // the `new Sharp7Client()` below, so on any throw here "no connection was attempted" is true
        // BY CONSTRUCTION rather than by inspection — which is the only kind of claim a catch-all is
        // entitled to make about something it did not see.
        GuardDecision decision;
        try
        {
            var load = AllowlistFile.Load(allowlistPath);
            decision = new DeviceAccessGuard(load).Check(address);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("== read fence (device-guard) ==");
            Console.Error.WriteLine($"  reason  : FenceFault ({ex.GetType().Name})");
            Console.Error.WriteLine($"  verdict : REFUSED — the fence could not reach a decision: {ex.Message}");
            Console.Error.WriteLine($"  where   : {ex.StackTrace}");
            Console.Error.WriteLine("  REFUSED — no connection attempted. This is a FAULT IN THE FENCE, not a verdict");
            Console.Error.WriteLine("  about the device: nothing examined the target at all. Fix the allowlist or the");
            Console.Error.WriteLine("  guard before reading anything from this exit code about the device.");
            return ExitFenceFault;
        }

        Console.WriteLine("== read fence (device-guard) ==");
        Console.WriteLine($"  reason  : {decision.Reason}");
        Console.WriteLine($"  verdict : {decision.Message}");
        if (!decision.Allowed)
        {
            Console.WriteLine("  REFUSED — no connection attempted.");
            return ExitRefused;
        }
        Console.WriteLine("  (the socket below is opened only because this said ALLOWED)");
        Console.WriteLine();

        using var client = new Sharp7Client();

        // ---- 2. CONNECT ----
        Console.WriteLine("== connect ==");
        var status = client.Connect(address, rack, slot, 10_000);
        Console.WriteLine($"  ConnectTo({address}, {rack}, {slot}) -> {status}");
        if (!status.Ok)
        {
            Console.WriteLine("  connect FAILED — nothing further attempted.");
            return ExitConnectFailed;
        }
        Console.WriteLine($"  connected  : {client.Connected}");
        Console.WriteLine($"  PDU size   : requested {client.PduSizeRequested}, negotiated {client.PduSizeNegotiated}");
        if (client.PduSizeNegotiated <= 0)
            Console.WriteLine("  WARNING: no PDU was negotiated, so any read size Sharp7 computes from it is meaningless.");
        Console.WriteLine();

        // ---- 3. IDENTITY, BEFORE THE BYTES ARE TRUSTED ----
        //
        // Deliberately ahead of the DB read, for two reasons. Reading the right values off the wrong
        // device is a wrong measurement (S7Transport does it in this order for the same reason); and
        // the order code is an SZL request rather than a data read, so whether it answers is the
        // cleanest available evidence about whether the SESSION works at all when a data read does not.
        RunIdentityPath(client, decision.MatchedEntry);
        Console.WriteLine();

        // ---- 4. CPU RUN STATE ----
        //
        // Before the DB read, because it is the fact that explains a failed one. A CPU in STOP still
        // connects and still answers SZL, so nothing earlier in this program distinguishes it from a
        // running CPU, and "the DB read failed" reads very differently once the mode is on the page.
        ReportRunState(client);
        Console.WriteLine();

        // ---- 5. THE READ ----
        if (marker)
            return ReadMarkers(address, rack, slot, offset, length);

        Console.WriteLine("== read ==");
        var buffer = new byte[length];
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var read = client.ReadDataBlock(db, offset, buffer);
        clock.Stop();
        Console.WriteLine($"  DBRead(db={db}, start={offset}, size={length}) -> {read}");
        Console.WriteLine($"  elapsed         : {clock.ElapsedMilliseconds} ms");
        Console.WriteLine($"  raw Sharp7 code : {read.Code} (0x{read.Code:X8})");
        Console.WriteLine($"  raw Sharp7 text : {read.Text}");
        Console.WriteLine();

        if (!read.Ok)
        {
            DiagnoseFailedRead(address, rack, slot, db);
            return ExitReadFailed;
        }

        DumpHex(buffer, offset);
        Console.WriteLine();
        Measure(buffer, offset);

        Console.WriteLine();
        Console.WriteLine("Done. Operations performed on the device: ConnectTo, DBRead, GetOrderCode, " +
                          "PlcGetStatus. No write, no mode change.");
        return ExitOk;
    }

    // ------------------------------------------------------------------ marker memory

    /// <summary>
    /// Read a window of <c>%M</c> marker memory and print it three ways: raw bytes, 16-bit holding
    /// registers, and 32-bit doublewords.
    ///
    /// <para><b>Why a second session.</b> <see cref="IS7Client"/> has no marker read and this lane is
    /// fenced to <c>Harness.RigRead</c>, so the call is made on a Sharp7 client owned here — which is
    /// exactly what <see cref="DiagnoseFailedRead"/> already does, on the same overlap with the main
    /// client, and is the configuration <c>MBRead(0,1) -> ok</c> was measured in. Widening
    /// <c>IS7Client</c> is the tidier home for this and belongs to whoever owns that assembly; it is
    /// not a prerequisite, and doing it from here would touch every fake that implements it.</para>
    ///
    /// <para><b>The decode states its convention and shows its working.</b> Bytes are printed before
    /// any interpretation, so a wrong word order is visible in the report rather than baked into it.
    /// No expected VALUES are compiled in — same reason <see cref="Measure"/> has none: what a stamp
    /// or a register should contain belongs to whoever authored the program, not to a general reader,
    /// and a tool that knows the answer cannot be used to find out.</para>
    /// </summary>
    private static int ReadMarkers(string address, int rack, int slot, int offset, int length)
    {
        Console.WriteLine("== read (MARKER MEMORY) ==");
        Console.WriteLine("  a second Sharp7 session, because IS7Client carries no marker read; the same");
        Console.WriteLine("  overlap the DB-failure diagnostic below uses, and the one MBRead was measured in.");

        var probe = new Sharp7.S7Client { ConnTimeout = 10_000 };
        try
        {
            var rc = probe.ConnectTo(address, rack, slot);
            Console.WriteLine($"  ConnectTo({address}, {rack}, {slot}) -> {Say(probe, rc)}");
            if (rc != 0)
            {
                Console.WriteLine("  connect FAILED — nothing read.");
                return ExitConnectFailed;
            }

            var buffer = new byte[length];
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var read = probe.MBRead(offset, length, buffer);
            clock.Stop();
            Console.WriteLine($"  MBRead(start={offset}, size={length}) -> {Say(probe, read)}");
            Console.WriteLine($"  elapsed         : {clock.ElapsedMilliseconds} ms");
            Console.WriteLine($"  raw Sharp7 code : {read} (0x{read:X8})");
            Console.WriteLine();

            if (read != 0)
            {
                DiagnoseFailedMarkerRead(probe, offset, length);
                return ExitReadFailed;
            }

            DumpHex(buffer, offset);
            Console.WriteLine();
            MeasureMarkers(buffer, offset);

            Console.WriteLine();
            Console.WriteLine("Done. Operations performed on the device: ConnectTo, MBRead, GetOrderCode, " +
                              "PlcGetStatus. No write, no mode change.");
            return ExitOk;
        }
        finally
        {
            probe.Disconnect();
        }
    }

    /// <summary>
    /// Is the M area refused, or was it this WINDOW? One byte at <c>%MB0</c> discriminates: a CPU that
    /// refuses variable access refuses that too, while a window running off the end of bit memory (or
    /// past what one PDU carries) does not affect it. Without this, a failed marker read has the same
    /// two candidate causes a failed DB read has, and neither is distinguishable from the other.
    /// </summary>
    private static void DiagnoseFailedMarkerRead(Sharp7.S7Client probe, int offset, int length)
    {
        Console.WriteLine($"== diagnostic: is M memory refused, or was it the window %M{offset}..%M{offset + length - 1}? ==");

        var one = new byte[1];
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var mZero = probe.MBRead(0, 1, one);
        clock.Stop();
        Console.WriteLine($"  MBRead(0, 1) -> {Say(probe, mZero)}  [{clock.ElapsedMilliseconds} ms]");
        Console.WriteLine();

        if (mZero == 0)
        {
            Console.WriteLine($"  IMPLICATION: M memory IS readable (byte 0 = {one[0]:X2}), so variable access is");
            Console.WriteLine($"               permitted and the failure is about THIS WINDOW — %M{offset} plus");
            Console.WriteLine($"               {length} bytes is beyond the CPU's bit memory, or beyond what one");
            Console.WriteLine("               request carries. Check the offset and the length, not the CPU.");
        }
        else
        {
            Console.WriteLine("  IMPLICATION: the smallest possible marker read fails too, so the cause is not the");
            Console.WriteLine("               window: the CPU is refusing variable access (PUT/GET disabled, or");
            Console.WriteLine("               secure PG/HMI communication only). Nothing about the program is");
            Console.WriteLine("               implicated and no marker address will read until that changes.");
        }
    }

    /// <summary>
    /// Marker bytes as Modbus holding registers and as 32-bit doublewords.
    ///
    /// <para>The register geometry is <c>MB_HOLD_REG = P#M&lt;base&gt;.0 WORD n</c>, so register
    /// <c>r</c> is the word at <c>base + 2r</c> and the base is whatever <c>--marker</c> named. That
    /// mapping is only meaningful when <c>--marker</c> was pointed at the mirror base, so the heading
    /// says which assumption it is under rather than presenting register numbers as a fact about the
    /// bytes.</para>
    ///
    /// <para><b>The doubleword column is HIGH-WORD-FIRST</b> — measured on this rig twice (2026-08-13,
    /// 2026-08-14) off a build stamp with distinguishable halves. It is printed as a labelled
    /// convention next to the bytes it was computed from, never instead of them: an order that is
    /// stated and wrong is correctable from the report, an order that is silently applied is not.</para>
    /// </summary>
    private static void MeasureMarkers(byte[] buffer, int baseByte)
    {
        Console.WriteLine($"== registers (assuming %M{baseByte} is a mirror base: register r = the word at base + 2r) ==");
        Console.WriteLine("  reg   addr        bytes    u16 hex   u16 dec   dword (HIGH-WORD-FIRST, measured on this rig)");

        for (var r = 0; (2 * r) + 1 < buffer.Length; r++)
        {
            var at = 2 * r;
            var word = (buffer[at] << 8) | buffer[at + 1];

            // A doubleword is reported on its LOW register only, and only when both halves are in the
            // buffer — a half-read 32-bit value printed as a number is a wrong measurement wearing the
            // right shape.
            var dword = "";
            if (r % 2 == 0 && at + 3 < buffer.Length)
            {
                var high = word;
                var low = (buffer[at + 2] << 8) | buffer[at + 3];
                var value = ((uint)high << 16) | (uint)low;
                dword = $"r{r}:r{r + 1} = 16#{value:X8} ({value})";
            }

            Console.WriteLine($"  {r,3}   %MW{baseByte + at,-6}  {buffer[at]:X2} {buffer[at + 1]:X2}    16#{word:X4}   {word,7}   {dword}");
        }

        if (buffer.Length % 2 != 0)
            Console.WriteLine($"  (the last byte %M{baseByte + buffer.Length - 1} = {buffer[^1]:X2} is not part of a whole register — " +
                              "an odd --length leaves one, and half a register is not a register)");
    }

    // ------------------------------------------------------------------ reporting

    private static void DumpHex(byte[] buffer, int baseOffset)
    {
        Console.WriteLine($"== raw bytes ({buffer.Length}) ==");
        for (var i = 0; i < buffer.Length; i += 16)
        {
            var count = Math.Min(16, buffer.Length - i);
            var hex = string.Join(" ", buffer.Skip(i).Take(count).Select(b => b.ToString("X2")));
            var ascii = new string(buffer.Skip(i).Take(count)
                .Select(b => b is >= 0x20 and < 0x7F ? (char)b : '.').ToArray());
            Console.WriteLine($"  {baseOffset + i,3}  {hex,-47}  |{ascii}|");
        }
    }

    /// <summary>
    /// Where the String headers ACTUALLY are, stated before any expectation is applied.
    ///
    /// <para>The candidate scan is reported first and separately from the expected offsets, so the
    /// measurement is legible even when it contradicts <see cref="MarkerDbLayout"/>. 0x20 is also the
    /// ASCII space, so a candidate is evidence and not proof; three of them at 2/36/70 is.</para>
    /// </summary>
    private static void Measure(byte[] buffer, int baseOffset)
    {
        Console.WriteLine("== measured: where a String[32] header could be ==");
        Console.WriteLine("  (a byte 32 followed by a byte <= 32 — 0x20 is also ' ', so these are candidates)");
        var found = 0;
        for (var i = 0; i + 1 < buffer.Length; i++)
        {
            if (buffer[i] != MarkerDbLayout.StringDeclaredMax || buffer[i + 1] > MarkerDbLayout.StringDeclaredMax)
                continue;

            found++;
            var text = Ascii(buffer, i + 2, buffer[i + 1]);
            Console.WriteLine($"    @{baseOffset + i,3}: (max={buffer[i]}, current={buffer[i + 1]}) -> \"{text}\"");
        }
        if (found == 0) Console.WriteLine("    none");
        Console.WriteLine();

        Console.WriteLine("== decoded against the COMPUTED offsets ==");
        Console.WriteLine($"  {MarkerDbLayout.Describe()}");

        var format = buffer.Length >= 2 ? (buffer[0] << 8) | buffer[1] : -1;
        Console.WriteLine($"  Format       @DBW{MarkerDbLayout.FormatOffset,-3} = {format}   (expected 1)");

        // No expected VALUES are baked in. What this tool checks is that the bytes at the computed
        // offsets are shaped like S7 strings — a plausible (max, current) header and decodable text.
        // The values themselves are site data: they belong to whoever downloaded the block, not to a
        // general-purpose reader, and hardcoding one job's marker here would make the tool quietly
        // wrong everywhere else. Compare the printed text against the block you authored.
        DecodeString(buffer, MarkerDbLayout.RigMarkerOffset, "RigMarker");
        DecodeString(buffer, MarkerDbLayout.OrderNumberOffset, "OrderNumber");
        DecodeString(buffer, MarkerDbLayout.SerialNumberOffset, "SerialNumber");
    }

    private static void DecodeString(byte[] buffer, int at, string member)
    {
        if (at + 1 >= buffer.Length)
        {
            Console.WriteLine($"  {member,-12} @DBB{at,-3} : NOT READ (buffer is only {buffer.Length} bytes)");
            return;
        }

        // A header whose declared max is not the expected width, or whose current length exceeds it,
        // means these bytes are not a string at that offset — which is the offsets being wrong, not
        // the block being wrong. Say so rather than printing whatever the decode makes of it.
        int max = buffer[at], current = buffer[at + 1];
        var plausible = max == MarkerDbLayout.StringDeclaredMax && current <= max;
        Console.WriteLine($"  {member,-12} @DBB{at,-3} : header (max={max}, current={current})" +
                          (plausible
                              ? $" — shaped like String[{MarkerDbLayout.StringDeclaredMax}]"
                              : $" — NOT a String[{MarkerDbLayout.StringDeclaredMax}] header; the offset is wrong"));

        var slice = buffer.Skip(at).Take(S7StringCodec.SizeOf(MarkerDbLayout.StringDeclaredMax)).ToArray();
        try
        {
            var text = S7StringCodec.Decode(slice, MarkerDbLayout.StringDeclaredMax, $"DB{MarkerDbLayout.DbNumber}.DBB{at}");
            Console.WriteLine($"                        text \"{text}\"");
        }
        catch (Exception ex) when (ex is S7TransportException or S7ConfigurationException)
        {
            Console.WriteLine($"                        NOT AN S7 STRING AT THIS OFFSET: {ex.Message}");
        }
    }

    private static string Ascii(byte[] buffer, int at, int count)
    {
        if (at < 0 || count < 0 || at + count > buffer.Length) return "<out of range>";
        return Encoding.ASCII.GetString(buffer, at, count);
    }

    // ------------------------------------------------------------------ identity

    private static void RunIdentityPath(IS7Client client, AllowlistEntry? entry)
    {
        Console.WriteLine("== identity path (IdentitySourcePlan + DeviceIdentityReader) ==");
        if (entry is null)
        {
            Console.WriteLine("  no matched allowlist entry — nothing to plan against.");
            return;
        }

        var plan = IdentitySourcePlan.ForEntry(entry);
        Console.WriteLine($"  sources : {plan.Describe()}");
        Console.WriteLine($"  usable  : {plan.IsUsable}");
        if (plan.Problem is not null)
            Console.WriteLine($"  problem : {plan.Problem}");

        // Run the read regardless of the plan's verdict: the sources are all read-only and what they
        // actually return is the fact worth having. An unusable plan is a CONFIGURATION diagnosis, not
        // a reason to leave the device unasked.
        // Timed, and reported even on the failure path. This is the reference round trip: the order
        // code is a request the CPU is known to answer, so its elapsed time is what a real exchange
        // with this device costs. A later request that fails in a small fraction of that time did not
        // reach the CPU; one that takes about as long did, and came back refused.
        var clock = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var identity = DeviceIdentityReader.Read(client, plan.Sources);
            clock.Stop();
            Console.WriteLine($"  read    : {identity.Describe()}");
            Console.WriteLine($"  elapsed : {clock.ElapsedMilliseconds} ms (reference round trip)");

            var comparison = DeviceIdentity.Compare(entry, identity);
            Console.WriteLine($"  declared: {comparison.WasDeclared}, matched: {comparison.Matched}");
            if (comparison.Problem is not null)
                Console.WriteLine($"  mismatch: {comparison.Problem}");
        }
        catch (Exception ex) when (ex is S7TransportException or S7ConfigurationException)
        {
            clock.Stop();
            Console.WriteLine($"  read    : FAILED after {clock.ElapsedMilliseconds} ms — {ex.Message}");
        }
    }

    // ------------------------------------------------------------------ run state

    /// <summary>
    /// What the CPU says about its own mode, with the value it was decoded from.
    ///
    /// <para>Both are printed because they answer different questions and the second is not derivable
    /// from the first: <c>NotRunning</c> covers STOP, STARTUP, HOLD and anything Sharp7 did not
    /// recognise, so the value is what a later reader has to go on. Timed alongside the identity read
    /// above for the same reason that one is — a failure that costs a full round trip was answered.</para>
    /// </summary>
    private static void ReportRunState(IS7Client client)
    {
        Console.WriteLine("== CPU run state (PlcGetStatus — a status request; this binary cannot change a mode) ==");

        var clock = System.Diagnostics.Stopwatch.StartNew();
        var status = client.ReadRunState(out var runState);
        clock.Stop();

        Console.WriteLine($"  status  : {status}");
        Console.WriteLine($"  state   : {runState}");
        Console.WriteLine($"  value   : {runState.Sharp7Value} (Sharp7's mapped value, not the CPU's byte)");
        Console.WriteLine($"  elapsed : {clock.ElapsedMilliseconds} ms");

        if (!status.Ok)
            Console.WriteLine("  the CPU was not asked successfully — this says nothing about whether it is running.");
        else if (!runState.Running)
            Console.WriteLine("  the CPU did not answer RUN. Which non-running mode it is in is not knowable here.");
    }

    // ------------------------------------------------------------------ diagnosis

    /// <summary>
    /// The one permitted follow-up read: a few bytes of M memory on a fresh session.
    ///
    /// <para>Connect succeeding and data failing has four candidate causes — PUT/GET disabled, secure
    /// PG/HMI communication only, the block being OPTIMIZED (invisible to classic S7comm), or the block
    /// not being on the CPU. All four look identical at the connect. A different AREA discriminates:
    /// if M memory also fails the cause is CPU-wide (the first two); if it succeeds the cause is
    /// specific to that DB (the last two).</para>
    /// </summary>
    private static void DiagnoseFailedRead(string address, int rack, int slot, int db)
    {
        Console.WriteLine("== diagnostic: is the refusal CPU-wide or specific to DB" + db + "? ==");

        var probe = new Sharp7.S7Client { ConnTimeout = 10_000 };
        try
        {
            var rc = probe.ConnectTo(address, rack, slot);
            Console.WriteLine($"  ConnectTo -> {Say(probe, rc)}");
            if (rc != 0) return;

            Console.WriteLine($"  PDU size  : requested {probe.PduSizeRequested}, negotiated {probe.PduSizeNegotiated}");

            // Smallest possible request of the same block. A size-related failure changes here; a
            // refusal does not.
            var one = new byte[1];
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var dbOne = probe.DBRead(db, 0, 1, one);
            clock.Stop();
            var dbOneMs = clock.ElapsedMilliseconds;
            Console.WriteLine($"  DBRead(db={db}, start=0, size=1) -> {Say(probe, dbOne)}  [{dbOneMs} ms]");

            // A different AREA entirely: the CPU-wide vs block-specific discriminator.
            var m = new byte[1];
            clock.Restart();
            var mOne = probe.MBRead(0, 1, m);
            clock.Stop();
            Console.WriteLine($"  MBRead(0, 1)                     -> {Say(probe, mOne)}  [{clock.ElapsedMilliseconds} ms]");

            // The same SZL request the identity path made, on this session, for a like-for-like
            // reference round trip against the two above.
            var oc = new Sharp7.S7Client.S7OrderCode();
            clock.Restart();
            var ocRc = probe.GetOrderCode(ref oc);
            clock.Stop();
            Console.WriteLine($"  GetOrderCode (SZL)               -> {Say(probe, ocRc)}  [{clock.ElapsedMilliseconds} ms]");

            Console.WriteLine();
            if (mOne == 0 && dbOne != 0)
            {
                Console.WriteLine($"  IMPLICATION: general data access WORKS (M read {m[0]:X2}), so PUT/GET is permitted");
                Console.WriteLine($"               and the refusal is specific to DB{db} — it is OPTIMIZED (invisible to");
                Console.WriteLine("               classic S7comm) or it is not on the CPU at all.");
            }
            else if (mOne != 0 && dbOne != 0)
            {
                Console.WriteLine("  IMPLICATION: data access fails CPU-WIDE and in the same way for a block and for");
                Console.WriteLine("               M memory, so the cause is NOT the block: an optimized or absent block");
                Console.WriteLine("               cannot affect M memory, and would in any case come back as a full-length");
                Console.WriteLine("               reply carrying an item return code (errCliItemNotAvailable 0x00C00000 or");
                Console.WriteLine("               errCliAddressOutOfRange 0x00900000). The CPU refuses variable access.");
                Console.WriteLine();
                Console.WriteLine("               On 0x00040000 (errIsoInvalidDataSize) specifically: this is NOT a bad");
                Console.WriteLine("               buffer on our side. Verified against Sharp7's own IL — in ReadArea it is");
                Console.WriteLine("               set by 'if (Length < 25)' AFTER RecvIsoPacket() has already succeeded, so");
                Console.WriteLine("               a well-formed reply arrived that is too short to be a read response. That");
                Console.WriteLine("               is a negative acknowledgement from the CPU. Compare the elapsed times: a");
                Console.WriteLine("               failure that cost a full round trip was answered, not rejected locally.");
            }
            else if (dbOne == 0)
            {
                Console.WriteLine($"  IMPLICATION: a 1-byte read of DB{db} SUCCEEDED where the {MarkerDbLayout.TotalBytes}-byte read did not,");
                Console.WriteLine("               so the block is present and readable and the failure is about the SIZE");
                Console.WriteLine("               of the request, not about access.");
            }
        }
        finally
        {
            probe.Disconnect();
        }
    }

    /// <summary>A Sharp7 return code with its code, its hex form and its own prose — all three,
    /// because the hex form is what an error table is indexed by and the prose is what is readable.</summary>
    private static string Say(Sharp7.S7Client probe, int rc) =>
        rc == 0 ? "ok" : $"rc={rc} (0x{rc:X8}): {probe.ErrorText(rc)}";

    // ------------------------------------------------------------------ args

    private static string? Option(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    /// <summary>
    /// A numeric option, or a NAMED usage error. These were <c>int.Parse</c>, which on a typo threw an
    /// unhandled FormatException — the exact shape hammer finding 27 objected to elsewhere in this
    /// file: fail closed, yes, but a harness cannot tell an unhandled exception from a refusal.
    /// </summary>
    private static bool TryInt(string[] args, string name, int fallback, out int value)
    {
        var raw = Option(args, name);
        if (raw is null)
        {
            value = fallback;
            return true;
        }

        if (int.TryParse(raw, out value)) return true;

        Usage($"{name} takes a whole number of {(name == "--length" ? "bytes" : "units")}; got \"{raw}\".");
        return false;
    }

    private static int Usage(string message)
    {
        Console.Error.WriteLine($"usage error: {message}");
        Console.Error.WriteLine("  Nothing was read and no connection was attempted.");
        return ExitUsage;
    }
}
