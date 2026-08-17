using System.Net;
using DeviceGuard;
using Harness.MirrorRead;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Harness.MirrorView;

/// <summary>Exit codes. Every one names a distinct fact; none of them means "something went wrong".</summary>
public enum MirrorViewExit
{
    /// <summary>The viewer ran and was shut down.</summary>
    Ok = 0,

    /// <summary>The arguments do not describe a viewer. Nothing was contacted.</summary>
    Usage = 2,

    /// <summary>
    /// *** THE MAP COULD NOT BE BUILT FROM THE COMMITTED ARTIFACTS. *** The viewer REFUSES to start.
    /// There is deliberately no built-in table to fall back on.
    /// </summary>
    MapRefused = 9,

    /// <summary>The HTTP listener could not be bound.</summary>
    ListenFailed = 10,
}

/// <summary>
/// <c>harness-mirror-view</c> — a locally-served page showing the Modbus register map and its current
/// values.
///
/// <para><b>READ-ONLY, structurally.</b> The only wire verb this assembly can name is
/// <c>Harness.MirrorRead.IRegisterSource.Read</c>, and that claim is checked by an IL walk over the
/// compiled assembly with a denominator and two live positive controls — not by a flag and not by this
/// comment. See <c>MirrorViewStructureTests</c>.</para>
///
/// <para><b>Bound to loopback only.</b> A view of a live controller has no business on a network
/// interface, and the structural walk asserts <c>IPAddress.Any</c>/<c>IPAddress.IPv6Any</c> appear
/// nowhere in the binary.</para>
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Contains("--help") || args.Contains("-h"))
        {
            Usage(Console.Out);
            return (int)MirrorViewExit.Usage;
        }

        // 🔴 *** TWO MODES, CHOSEN EXPLICITLY, NEVER INFERRED. *** DIRECT opens a socket to the device.
        // FOLLOW opens no socket at all and reads the feed a running wave publishes. They are mutually
        // exclusive because MB_SERVER accepts ONE connection per instance: a viewer that connected while a
        // wave was running took the wave's connection away, measured — a conformance wave reported 0 of 22
        // vectors attempted with `SocketException: actively refused`, and stopping the viewer fixed it.
        var follow = Option(args, "--follow");
        var address = Option(args, "--address");

        if (follow is not null && address is not null)
        {
            return Refuse("--follow and --address are mutually exclusive. --address OPENS A SOCKET to the device; " +
                          "--follow opens none and reads what a running wave already read. Choosing between them " +
                          "would be this tool deciding whether to take a wave's connection away.");
        }

        if (follow is null && string.IsNullOrWhiteSpace(address))
        {
            return Refuse("--address is required (or --follow <feed> to watch a running wave without opening a " +
                          "socket). There is no default target: a tool that guesses which device it is talking to " +
                          "has already made the mistake the fence exists to prevent.");
        }

        if (follow is not null && string.IsNullOrWhiteSpace(follow))
            return Refuse("--follow was given an empty path. There is no default feed location.");

        // *** REQUIRED, NOT DEFAULTED. *** The map is the artifact, and a default path would let the
        // viewer come up against whichever file happened to be at a guessed location — plausibly, and
        // wrongly. Both files are named because both are load-bearing: one carries the tags, the other
        // the MB_HOLD_REG pointer that turns a %M address into a register index.
        var mapPath = Option(args, "--map");
        if (mapPath is null)
        {
            return Refuse("--map is required: the mirror tag table, e.g. ir/test-project001/HarnessMirror.ir. " +
                          "This tool has no built-in register map and will not invent one.");
        }

        var areaPath = Option(args, "--area");
        if (areaPath is null)
        {
            return Refuse("--area is required: the block carrying the MB_HOLD_REG area pointer, e.g. " +
                          "ir/test-project001/FB_Comms_ModbusServer.ir. It states the base byte and the " +
                          "declared width, and without it a %M address cannot be placed on the register grid.");
        }

        if (!TryInt(args, "--port", 503, out var port)) return (int)MirrorViewExit.Usage;
        if (!TryInt(args, "--unit", 1, out var unit)) return (int)MirrorViewExit.Usage;
        if (unit is < 0 or > 255) return Refuse($"--unit {unit} is not a Modbus unit identifier (0..255).");
        if (!TryInt(args, "--http-port", 8137, out var httpPort)) return (int)MirrorViewExit.Usage;
        if (!TryInt(args, "--poll-ms", 1000, out var pollMs)) return (int)MirrorViewExit.Usage;
        if (!TryInt(args, "--stale-after-ms", MirrorViewOptions.DefaultStaleAfterMs(pollMs), out var staleMs))
            return (int)MirrorViewExit.Usage;

        // ---- THE MAP, FROM THE COMMITTED ARTIFACTS, OR NOTHING AT ALL ----
        var load = MirrorMapParser.Load(mapPath, areaPath);
        if (!load.Ok || load.Map is null)
        {
            Console.Error.WriteLine("REFUSED — the register map could not be built from the committed artifacts.");
            Console.Error.WriteLine($"  tag table   : {mapPath}");
            Console.Error.WriteLine($"  area pointer: {areaPath}");
            foreach (var refusal in load.Refusals)
                Console.Error.WriteLine($"  - {refusal}");
            Console.Error.WriteLine();
            Console.Error.WriteLine("  Nothing was started and no connection was attempted. There is no built-in");
            Console.Error.WriteLine("  fallback map on purpose: one would be wrong the first time the copy layer is");
            Console.Error.WriteLine("  regenerated, and would look right while being wrong.");
            return (int)MirrorViewExit.MapRefused;
        }

        var map = load.Map;

        string? allowlist = null;

        if (follow is null)
        {
            allowlist = AllowlistPath.Resolve(Option(args, "--allowlist"), Environment.GetEnvironmentVariable);
            if (allowlist is null)
            {
                // Deliberately NOT passed through as a refusal from the guard. "You did not configure an
                // allowlist" and "that device is not approved" are different facts, and a caller that could
                // not tell them apart would read a setup mistake as a governance decision.
                return Refuse($"No allowlist configured. Pass --allowlist <path> or set {AllowlistPath.EnvVar}. " +
                              "There is no default path, and with none every target is refused before a socket is opened.");
            }
        }

        var options = new MirrorViewOptions(
            address ?? string.Empty, port, (byte)unit, allowlist, map.DeclaredRegisters, pollMs, staleMs, httpPort,
            FollowPath: follow);

        var refusals = options.Refusals;
        if (refusals.Count > 0)
        {
            Console.Error.WriteLine("usage error:");
            foreach (var refusal in refusals) Console.Error.WriteLine($"  - {refusal}");
            Console.Error.WriteLine("  Nothing was started and no connection was attempted.");
            return (int)MirrorViewExit.Usage;
        }

        // *** THE FOLLOW PATH CONSTRUCTS NO FENCE AND NO CONNECTION FACTORY. *** Not "passes a permissive
        // one" — there is nothing to fence, because there is no socket on that path and no address to ask
        // about. The fence that matters governs the WAVE, which is the process holding the connection.
        return follow is not null
            ? RunFollowing(map, options, new FileMirrorFeedSource(follow))
            : Run(map, options, new AllowlistDeviceFence(allowlist), new ModbusRegisterSourceFactory());
    }

    private static int RunFollowing(MirrorMap map, MirrorViewOptions options, IMirrorFeedSource source)
    {
        var state = new MirrorState();
        using var poller = new MirrorFollowPoller(options, map, state, source, () => DateTimeOffset.UtcNow);

        Banner(map, options);
        poller.Start();
        return Serve(map, options, state);
    }

    private static int Run(MirrorMap map, MirrorViewOptions options, IDeviceFence fence, IRegisterSourceFactory factory)
    {
        var state = new MirrorState();
        using var poller = new MirrorPoller(options, state, fence, factory, () => DateTimeOffset.UtcNow);

        Banner(map, options);
        poller.Start();
        return Serve(map, options, state);
    }

    /// <summary>
    /// The HTTP half, shared by both modes. <b>One page, one JSON endpoint, one view model</b> — the two
    /// modes differ only in what fills the state, which is what keeps the page's guarantees identical.
    /// </summary>
    private static int Serve(MirrorMap map, MirrorViewOptions options, MirrorState state)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();

        // *** LOOPBACK ONLY, AND BOTH FAMILIES. *** "localhost" resolves to ::1 on this machine as often
        // as to 127.0.0.1, so binding one and not the other produces a page that works in one browser and
        // not another. IPAddress.Any appears nowhere in this assembly and a structural test asserts so:
        // a view of a live controller has no business on a network interface.
        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.Listen(IPAddress.Loopback, options.HttpPort);
            kestrel.Listen(IPAddress.IPv6Loopback, options.HttpPort);
        });

        var app = builder.Build();

        app.MapGet("/", () => Results.Content(MirrorPage.Html, MirrorPage.ContentType));
        app.MapGet("/api/mirror", () => Results.Content(
            MirrorJson.Serialize(MirrorView.Build(map, options, state, DateTimeOffset.UtcNow)),
            "application/json; charset=utf-8"));

        try
        {
            app.Run();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"could not serve on http://127.0.0.1:{options.HttpPort}/ — " +
                                    $"{ex.GetType().Name}: {ex.Message}");
            return (int)MirrorViewExit.ListenFailed;
        }

        return (int)MirrorViewExit.Ok;
    }

    private static void Banner(MirrorMap map, MirrorViewOptions options)
    {
        Console.WriteLine("harness-mirror-view — READ-ONLY live view of the Modbus mirror.");
        Console.WriteLine("  This binary contains no write path. Not a claim resting on a flag: no method in this");
        Console.WriteLine("  assembly names a write member of Harness.Wire, Harness.Map or NModbus, checked by an IL");
        Console.WriteLine("  walk with a denominator and live positive controls (MirrorViewStructureTests).");
        Console.WriteLine();

        if (options.IsFollowing)
        {
            Console.WriteLine("mode        : FOLLOW — THIS VIEWER OPENS NO SOCKET AND CONTACTS NO DEVICE.");
            Console.WriteLine("              It reads the feed a running wave publishes, so every value shown is a byte");
            Console.WriteLine("              THE HARNESS ITSELF READ, stamped with the instant that read returned. MB_SERVER");
            Console.WriteLine("              accepts ONE connection per instance, so a second socket would take the wave's");
            Console.WriteLine("              connection away — and would in any case be a second sample at a second instant.");
            Console.WriteLine("              Nothing here can cause a read: the page shows what the wave already read, or a");
            Console.WriteLine("              named reason it cannot say.");
            Console.WriteLine();
        }

        Console.WriteLine($"map         : {map.TagTableSource}  ({map.Tags.Count} tags)");
        Console.WriteLine($"area pointer: {map.AreaPointerSource}  (P#M{map.BaseByte}.0 WORD {map.DeclaredRegisters})");

        if (map.UnmappedRegisters.Count > 0)
        {
            Console.WriteLine($"              {map.UnmappedRegisters.Count} register(s) in the declared area are named " +
                              $"by no tag: {string.Join(", ", map.UnmappedRegisters)} — shown on the page as UNMAPPED.");
        }

        Console.WriteLine($"target      : {options.Describe}");

        Console.WriteLine(options.IsFollowing
            ? "allowlist   : NOT CONSULTED — no device is contacted on this path. The fence that matters governs the WAVE."
            : $"allowlist   : {options.AllowlistPath}");

        Console.WriteLine(options.IsFollowing
            ? $"poll        : the FEED is re-read every {options.PollIntervalMs} ms; a reading is STALE after {options.StaleAfterMs} ms"
            : $"poll        : every {options.PollIntervalMs} ms; a reading is STALE after {options.StaleAfterMs} ms");
        Console.WriteLine();
        Console.WriteLine($"  ->  http://127.0.0.1:{options.HttpPort}/          (loopback only)");
        Console.WriteLine($"  ->  http://127.0.0.1:{options.HttpPort}/api/mirror (the same data, as JSON)");
        Console.WriteLine();
        Console.WriteLine("Ctrl+C to stop.");
    }

    private static int Refuse(string message)
    {
        Console.Error.WriteLine($"usage error: {message}");
        Console.Error.WriteLine("  Nothing was started and no connection was attempted.");
        return (int)MirrorViewExit.Usage;
    }

    private static void Usage(TextWriter output)
    {
        output.WriteLine("harness-mirror-view — READ-ONLY browser view of the rig's Modbus register mirror.");
        output.WriteLine();
        output.WriteLine("TWO MODES, CHOSEN EXPLICITLY. --address opens a socket to the device. --follow opens NONE and reads");
        output.WriteLine("the feed a running wave publishes (harness-run --publish <path>), so the page shows exactly the bytes");
        output.WriteLine("the harness read, at the instants it read them. MB_SERVER accepts ONE connection per instance, so with");
        output.WriteLine("a wave running the direct mode would take its connection away — measured: 0 of 22 vectors attempted.");
        output.WriteLine();
        output.WriteLine("  --follow <path>           follow a wave's published feed. No socket, no --address, no allowlist.");
        output.WriteLine("  --address <host>          required unless --follow. The Modbus TCP server, and the address the fence is asked about.");
        output.WriteLine("  --map <path>              required. The mirror TAG TABLE ir file (names, addresses, types, comments).");
        output.WriteLine("  --area <path>             required. The ir file carrying MB_HOLD_REG := P#M<base>.0 WORD <n>.");
        output.WriteLine("  --allowlist <path>        required in DIRECT mode unless LADDER_DEVICE_ALLOWLIST is set. No default path exists.");
        output.WriteLine("  --port <n>                default 503 (this rig; 502 is refused there).");
        output.WriteLine("  --unit <n>                default 1.");
        output.WriteLine("  --http-port <n>           default 8137. Bound to LOOPBACK ONLY.");
        output.WriteLine("  --poll-ms <ms>            default 1000.");
        output.WriteLine("  --stale-after-ms <ms>     default 3x the poll interval (min 1000).");
        output.WriteLine();
        output.WriteLine("exit codes: 0 ran | 2 usage | 9 the MAP could not be built from the artifacts | 10 could not listen");
    }

    private static string? Option(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
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
