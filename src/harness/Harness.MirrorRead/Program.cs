using DeviceGuard;

namespace Harness.MirrorRead;

/// <summary>
/// <c>harness-mirror-read</c> — read holding registers off the rig's Modbus mirror and say how wide
/// the area actually is.
///
/// <para><b>Why it exists.</b> <c>Harness.Wire</c> has had a working Modbus transport and a map-aware
/// client since phase 2, and no executable exposed a read. Every rig observation therefore went over
/// S7 (<c>rig-read --marker</c>), which addresses <c>%M</c> directly and never consults
/// <c>MB_HOLD_REG</c> — so it can confirm bytes exist in marker memory while saying nothing at all
/// about whether a Modbus client can see them. When the question is precisely "does the area pointer's
/// width reach the wire", the S7 path is the one transport that cannot answer it.</para>
///
/// <para><b>Nothing new on the wire.</b> The transport is <c>Harness.Wire.NModbusTransport</c>,
/// including its <c>ModbusPolicy</c> — retries at zero, timeouts at 3,000 ms, both derived from
/// measurements on this rig. A second client with its own settings would be an uncalibrated one.</para>
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Contains("--help") || args.Contains("-h"))
        {
            Usage(Console.Out);
            return (int)MirrorReadExit.Usage;
        }

        var address = Option(args, "--address");
        if (string.IsNullOrWhiteSpace(address))
            return Refuse("--address is required. There is no default target: a tool that guesses which device it is talking to has already made the mistake the fence exists to prevent.");

        // *** REQUIRED, NOT DEFAULTED. *** The declared width is the CLAIM under test and it comes from
        // the IR. A default would produce a fully plausible verdict against whatever width was last
        // true, on a run where nobody stated one — a flag whose omission yields a plausible artifact is
        // not optional, it is a defect with a default.
        var declaredRaw = Option(args, "--declared-registers");
        if (declaredRaw is null)
            return Refuse("--declared-registers is required. It is the width MB_HOLD_REG declares in the IR (WORD n -> n registers, 0..n-1) and it is the claim this run tests. Defaulting it would let a run conclude against a width nobody stated.");
        if (!int.TryParse(declaredRaw, out var declared))
            return Refuse($"--declared-registers takes a whole number of registers; got \"{declaredRaw}\".");

        if (!TryInt(args, "--port", 503, out var port)) return (int)MirrorReadExit.Usage;
        if (!TryInt(args, "--unit", 1, out var unit)) return (int)MirrorReadExit.Usage;
        if (unit is < 0 or > 255)
            return Refuse($"--unit {unit} is not a Modbus unit identifier (0..255).");

        // The sweep straddles the declared edge by construction: three registers in, two out. Both ends
        // are still overridable, and MirrorReadOptions refuses any pair that does not straddle — a sweep
        // with no inside control, or one that never reaches the first undeclared register, runs and
        // proves nothing.
        if (!TryInt(args, "--boundary-from", Math.Max(0, declared - 3), out var boundaryFrom)) return (int)MirrorReadExit.Usage;
        if (!TryInt(args, "--boundary-to", declared + 1, out var boundaryTo)) return (int)MirrorReadExit.Usage;
        if (!TryInt(args, "--interval-ms", 3000, out var interval)) return (int)MirrorReadExit.Usage;

        var allowlist = AllowlistPath.Resolve(Option(args, "--allowlist"), Environment.GetEnvironmentVariable);
        if (allowlist is null)
        {
            // Deliberately NOT passed through as a refusal from the guard. The guard would refuse it
            // correctly, but "you did not configure an allowlist" and "that device is not approved" are
            // different facts and a caller that could not tell them apart would read a setup mistake as
            // a governance decision.
            return Refuse($"No allowlist configured. Pass --allowlist <path> or set {AllowlistPath.EnvVar}. There is no default path, and with none every target is refused before a socket is opened.");
        }

        var options = new MirrorReadOptions(
            address, port, (byte)unit, allowlist, declared, boundaryFrom, boundaryTo, interval);

        return (int)MirrorReadRun.Execute(options, new ModbusRegisterSourceFactory(), Console.Out);
    }

    private static int Refuse(string message)
    {
        Console.Error.WriteLine($"usage error: {message}");
        Console.Error.WriteLine("  Nothing was read and no connection was attempted.");
        return (int)MirrorReadExit.Usage;
    }

    private static void Usage(TextWriter output)
    {
        output.WriteLine("harness-mirror-read — READ-ONLY Modbus holding-register reader for the rig mirror.");
        output.WriteLine();
        output.WriteLine("  --address <host>              required. The Modbus TCP server, and the address the fence is asked about.");
        output.WriteLine("  --declared-registers <n>      required. The width MB_HOLD_REG declares in the IR (WORD n -> registers 0..n-1).");
        output.WriteLine("  --allowlist <path>            required unless LADDER_DEVICE_ALLOWLIST is set. No default path exists.");
        output.WriteLine("  --port <n>                    default 503 (this rig; 502 is refused there).");
        output.WriteLine("  --unit <n>                    default 1.");
        output.WriteLine("  --boundary-from <reg>         default n-3. First single-register probe.");
        output.WriteLine("  --boundary-to <reg>           default n+1. Last one. The sweep MUST straddle the declared edge.");
        output.WriteLine("  --interval-ms <ms>            default 3000. Gap between the two control reads, for the scan counter.");
        output.WriteLine();
        output.WriteLine("exit codes: 0 exactly the declared width | 1 fence refused | 2 usage | 3 connect failed");
        output.WriteLine("            4 not established | 5 fence fault | 6 NARROWER than declared | 7 wider than declared");
        output.WriteLine("            8 scan counter not rising");
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
