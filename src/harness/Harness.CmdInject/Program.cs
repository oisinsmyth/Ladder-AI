using System.Globalization;
using DeviceGuard;

namespace Harness.CmdInject;

/// <summary>
/// <c>harness-cmd-inject</c> — the write half of the virtual panel.
///
/// <para>🔴 <b>THIS BUILD CAN OPEN A SOCKET AND WRITE.</b> Phase 1's binary could not; this one supplies
/// <see cref="ModbusInjectionTransportFactory"/> where it used to pass null. Everything that stood between
/// a caller and a write in phase 1 still does — the arming flag, the fence, the band containment — and two
/// gates that phase 1 could only declare are now made: the <b>build stamp is confirmed before any write</b>,
/// and the <b>command band is captured and put back on every exit path</b>.</para>
///
/// <para>The protocol lives here; the map lives in the job folder and loads at runtime. The verbs are
/// <c>map</c> (offline, no address possible) and <c>send</c> (dry-run by default).</para>
///
/// <para><b><c>--arm</c> is read BEFORE the verb is parsed</b>, so flag order cannot change the answer.</para>
///
/// <para><b>Ctrl-C is an exit path like any other.</b> The handler cancels the poll rather than killing the
/// process, so the session's restore still runs — a client that could be interrupted into leaving a live
/// command band is a client with no reversibility at all.</para>
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            Usage(Console.Out);
            return (int)CmdInjectExit.Usage;
        }

        // *** THE ARMING GATE, READ FIRST. *** Presence of --arm is decided here, before any verb dispatch,
        // so no verb parser can change what it means and no flag order can flip it.
        var armed = args.Contains("--arm");

        var verb = args[0];
        return verb switch
        {
            "map" => (int)RunMap(args),
            "send" => (int)RunSend(args, armed),
            _ => Refuse($"unknown verb '{verb}'. Verbs: map, send."),
        };
    }

    private static CmdInjectExit RunMap(string[] args)
    {
        var tags = Option(args, "--tags");
        var area = Option(args, "--area");
        var binding = Option(args, "--binding");

        if (tags is null || area is null || binding is null)
        {
            return (CmdInjectExit)Refuse("map requires --tags <path> --area <path> --binding <path>. " +
                                         "All three are files in the job folder; none is compiled in.");
        }

        return MapRun.Execute(tags, area, binding, Console.Out);
    }

    private static CmdInjectExit RunSend(string[] args, bool armed)
    {
        var tags = Option(args, "--tags");
        var area = Option(args, "--area");
        var binding = Option(args, "--binding");
        var channel = Option(args, "--channel");

        if (tags is null || area is null || binding is null || channel is null)
        {
            return (CmdInjectExit)Refuse("send requires --tags <path> --area <path> --binding <path> --channel <name>. " +
                                         "Operands are given with --set <role>=<value> (role: code, int1, int2, real1, real2).");
        }

        var operands = new Dictionary<InjectionRole, string>();
        foreach (var setting in Options(args, "--set"))
        {
            var eq = setting.IndexOf('=');
            if (eq <= 0)
                return (CmdInjectExit)Refuse($"--set '{setting}' is not <role>=<value>.");

            var roleText = setting[..eq].Trim();
            var value = setting[(eq + 1)..];

            if (!TryOperandRole(roleText, out var role))
                return (CmdInjectExit)Refuse($"--set names role '{roleText}', which is not an operand role (code, int1, int2, real1, real2).");

            operands[role] = value;
        }

        uint? expectedStamp = null;
        var stampText = Option(args, "--expect-stamp");
        if (stampText is not null)
        {
            if (!TryParseStamp(stampText, out var stamp))
                return (CmdInjectExit)Refuse($"--expect-stamp '{stampText}' is not a 32-bit value (decimal or 0x-hex).");
            expectedStamp = stamp;
        }

        if (!TryInt(args, "--port", 503, out var port)) return CmdInjectExit.Usage;
        if (!TryInt(args, "--unit", 1, out var unit)) return CmdInjectExit.Usage;
        if (unit is < 0 or > 255)
            return (CmdInjectExit)Refuse($"--unit {unit} is not a Modbus unit identifier (0..255).");

        if (!TryInt(args, "--poll-attempts", 20, out var pollAttempts)) return CmdInjectExit.Usage;
        if (pollAttempts < 1)
            return (CmdInjectExit)Refuse($"--poll-attempts {pollAttempts} would poll never. A command written and never looked at is the one thing this tool must not do.");

        if (!TryInt(args, "--poll-interval-ms", 250, out var pollInterval)) return CmdInjectExit.Usage;
        if (pollInterval < 0)
            return (CmdInjectExit)Refuse($"--poll-interval-ms {pollInterval} is not a wait.");

        var target = Option(args, "--target");
        var allowlist = AllowlistPath.Resolve(Option(args, "--allowlist"), Environment.GetEnvironmentVariable);

        var options = new SendOptions(
            tags, area, binding, channel, operands, armed,
            target, allowlist, expectedStamp, port, (byte)unit,
            RaiseEnable: args.Contains("--raise-enable"),
            PollAttempts: pollAttempts,
            PollIntervalMs: pollInterval);

        // *** THE COMPOSITION ROOT. *** This line is the whole of what makes this binary able to write, and
        // it is the only place a socket-capable factory is named. Everything below it takes the factory as a
        // parameter, which is what lets a test supply a recording double and assert `opens == 0` on every
        // refusal path — the fence holding as a fact about a counter rather than an exit code.
        using var cancellation = new CancellationTokenSource();

        // Ctrl-C CANCELS, it does not kill. e.Cancel = true stops the runtime terminating the process, so
        // the poll loop returns, the session's restore runs, and the band is put back. Without this the one
        // exit path a person is most likely to take is the one that leaves a live command surface.
        ConsoleCancelEventHandler onCancel = (_, e) =>
        {
            e.Cancel = true;
            Console.Error.WriteLine();
            Console.Error.WriteLine("interrupt received: stopping the poll and restoring the command band. The command is NOT resent.");
            cancellation.Cancel();
        };

        Console.CancelKeyPress += onCancel;
        try
        {
            return SendRun.Execute(
                options, new SequenceLedger(), new ModbusInjectionTransportFactory(), Console.Out,
                new RealInjectionClock(), cancellation.Token);
        }
        finally
        {
            Console.CancelKeyPress -= onCancel;
        }
    }

    private static bool TryOperandRole(string text, out InjectionRole role)
    {
        foreach (var candidate in Roles.OperandRoles)
        {
            if (string.Equals(candidate.ToString(), text, StringComparison.OrdinalIgnoreCase))
            {
                role = candidate;
                return true;
            }
        }

        role = default;
        return false;
    }

    private static bool TryParseStamp(string text, out uint stamp)
    {
        text = text.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return uint.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out stamp);

        return uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out stamp);
    }

    private static int Refuse(string message)
    {
        Console.Error.WriteLine($"usage error: {message}");
        Console.Error.WriteLine("  Nothing was read and no connection was attempted.");
        return (int)CmdInjectExit.Usage;
    }

    private static void Usage(TextWriter output)
    {
        output.WriteLine("harness-cmd-inject — Modbus command injection for the bench rig. Holds the PROTOCOL, not the MAP.");
        output.WriteLine();
        output.WriteLine("  map  --tags <path> --area <path> --binding <path>");
        output.WriteLine("       Resolve the binding against the mirror map and print it. OFFLINE — takes no address.");
        output.WriteLine();
        output.WriteLine("  send --tags <path> --area <path> --binding <path> --channel <name> [--set <role>=<value>]...");
        output.WriteLine("       [--target <host>] [--allowlist <path>] [--expect-stamp <v>] [--port n] [--unit n]");
        output.WriteLine("       [--raise-enable] [--poll-attempts n] [--poll-interval-ms n] [--arm]");
        output.WriteLine("       Without --arm: print the plan and the exact frames, construct nothing, exit 10.");
        output.WriteLine("       With --arm: run the fence, confirm the build stamp BEFORE any write, capture the");
        output.WriteLine("       command band as a restore point, write, poll for the acknowledgement, and put the");
        output.WriteLine("       band back — on every exit path, Ctrl-C included, verified by re-read.");
        output.WriteLine();
        output.WriteLine("  --expect-stamp is REQUIRED to write. It is the only identity this link carries; without it");
        output.WriteLine("  the address is a routing hint and nothing confirms which CPU answered.");
        output.WriteLine("  --raise-enable raises the master enable inside the session. The restore drops it again, so");
        output.WriteLine("  the enable cannot persist between invocations and a send that needs it must raise it here.");
        output.WriteLine();
        output.WriteLine("  operand roles for --set: code, int1, int2, real1, real2.");
        output.WriteLine();
        output.WriteLine("exit codes: 0 ok | 2 usage | 3 map/binding refused | 4 allowlist unusable | 5 not a test rig");
        output.WriteLine("            6 not write-eligible | 7 not isolated | 8 no expected stamp | 9 fence fault");
        output.WriteLine("            10 dry run (no --arm) | 11 frame refused | 12 no transport supplied");
        output.WriteLine("            13 connect failed | 14 build stamp mismatch | 15 band not restorable");
        output.WriteLine("            16 enable clear | 17 not acknowledged | 18 aborted (restart/read/Ctrl-C)");
        output.WriteLine("            19 RESTORE FAILED — the band may be dirty; this outranks every other outcome");
    }

    private static string? Option(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static IEnumerable<string> Options(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
                yield return args[i + 1];
        }
    }

    private static bool TryInt(string[] args, string name, int fallback, out int value)
    {
        var raw = Option(args, name);
        if (raw is null)
        {
            value = fallback;
            return true;
        }

        if (int.TryParse(raw, out value)) return true;

        Refuse($"{name} takes a whole number; got \"{raw}\".");
        return false;
    }
}
