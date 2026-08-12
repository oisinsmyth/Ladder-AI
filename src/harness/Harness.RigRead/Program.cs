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
/// <para><b>It cannot write.</b> Not by policy — by construction. The only device operations it
/// performs are <c>ConnectTo</c>, <c>DBRead</c>, <c>MBRead</c>, <c>GetOrderCode</c> and
/// <c>PlcGetStatus</c> — the last asks the CPU for its mode and cannot change one; the calls that do
/// (<c>PlcStop</c>, <c>PlcHotStart</c>, <c>PlcColdStart</c>) are not on <see cref="IS7Client"/> at all
/// and are unreachable from here. See the project file for the assembly-level statement of the same
/// property.</para>
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

    public static int Main(string[] args)
    {
        var address = Option(args, "--address") ?? "10.10.10.10";
        var rack = int.Parse(Option(args, "--rack") ?? "0");
        var slot = int.Parse(Option(args, "--slot") ?? "1");
        var db = int.Parse(Option(args, "--db") ?? MarkerDbLayout.DbNumber.ToString());
        var offset = int.Parse(Option(args, "--offset") ?? "0");
        var length = int.Parse(Option(args, "--length") ?? MarkerDbLayout.TotalBytes.ToString());

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
        Console.WriteLine($"read        : DB{db}.DBB{offset}, {length} bytes");
        Console.WriteLine($"allowlist   : {allowlistPath}");
        Console.WriteLine();

        // ---- 1. THE FENCE, BEFORE ANY SOCKET ----
        var load = AllowlistFile.Load(allowlistPath);
        var decision = new DeviceAccessGuard(load).Check(address);

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
}
