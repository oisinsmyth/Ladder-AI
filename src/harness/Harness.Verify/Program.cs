using DeviceGuard;
using Harness.Wire;

namespace Harness.Verify;

/// <summary>
/// <c>harness-verify</c> — the process. <b>It parses arguments and owns nothing else</b>: every decision
/// lives in <see cref="VerifyRun"/>, which takes its filesystem and its transport as delegates, so the
/// whole decision surface is exercised in tests with no process and no socket.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        // 🔴 *** THE EMITTED TEXT IS ASCII-ONLY, AND THAT IS PINNED BY A TEST (OutputIsAscii). *** Measured
        // on this tool's own first live run: the repo's notation - em dashes, the section sign, the
        // red-circle marker - came back through the Windows console as replacement characters in the text
        // that then gets pasted into a lane report. `Console.OutputEncoding = UTF8` fixes the console and
        // was tried first; it was removed because it can THROW on a handle that will not take it, is caught,
        // and then produces a mangled report SILENTLY - a plausible artifact from a silent failure, which is
        // the shape this project keeps being bitten by. Making the text ASCII cannot fail that way.
        //
        // The XML DOC COMMENTS keep the repo's notation, because nothing prints them. Only literals are
        // constrained, and only because they are read by a person on a terminal.
        // (Sibling: Harness.Cleanup reached the same conclusion the same day, independently.)
        if (args.Contains("--help") || args.Contains("-h") || args.Length == 0)
        {
            Usage(Console.Out);
            return (int)VerifyExit.NothingExamined;
        }

        var submission = Option(args, "--submission");
        var binding = Option(args, "--binding");
        var programs = Values(args, "--program");
        var address = Option(args, "--address");

        if (submission is null) return Refuse("--submission is required. It is one half of what the build stamp is computed from.");
        if (binding is null) return Refuse("--binding is required. The bindings decide the map, and the map decides where the version register is.");
        if (programs.Count == 0)
        {
            return Refuse(
                "--program <file-or-dir>... is required. THE BUILD STAMP IS A HASH OF WHAT IS RUNNING, so a stamp taken over an "
                + "empty program set cannot match a rig carrying the block under test - this tool would then report Stale against "
                + "every healthy device and the report would be about its own inputs. There is deliberately no default and no "
                + "--no-program-under-test here: the empty claim is legitimate for harness-run and is never what this tool is for.");
        }

        if (address is null) return Refuse("--address is required. There is no default target: a tool that guesses which device it is talking to has already made the mistake the fence exists to prevent.");

        // *** --port HAS NO DEFAULT, FOR harness-run's REASON. *** 502 is the port every other Modbus
        // device on a network answers on, so a wrong default is not reliably a loud failure; the quiet
        // failure is reading a DIFFERENT DEVICE and believing it. This rig serves 503 and refuses 502.
        if (!int.TryParse(Option(args, "--port"), out var port) || port is < 1 or > 65535)
        {
            return Refuse(
                $"--port <1-65535> is required and has no default (read: '{Option(args, "--port") ?? "<absent>"}'). 502 is the port every "
                + "other Modbus device answers on, so the failure that is NOT loud is reading a different device and believing it. "
                + "This rig serves 503, with 502 refused - measured.");
        }

        if (!TryInt(args, "--unit", 1, out var unit) || unit is < 0 or > 255)
            return Refuse($"--unit takes a Modbus unit identifier 0..255; got '{Option(args, "--unit") ?? "<absent>"}'.");

        if (!TryInt(args, "--stable-reads", 3, out var stableReads) || stableReads < 2)
            return Refuse($"--stable-reads must be at least 2: one read is not stability, it is a sample. Got '{Option(args, "--stable-reads") ?? "<absent>"}'.");

        if (!TryInt(args, "--max-reads", 60, out var maxReads) || maxReads < stableReads)
            return Refuse($"--max-reads must be at least --stable-reads ({stableReads}), so the register has at least one chance to settle. Got '{Option(args, "--max-reads") ?? "<absent>"}'.");

        var allowlist = AllowlistPath.Resolve(Option(args, "--allowlist"), Environment.GetEnvironmentVariable);
        if (allowlist is null)
        {
            // Deliberately NOT passed through as a refusal from the guard. It would refuse correctly, but
            // "you did not configure an allowlist" and "that device is not approved" are different facts,
            // and a caller who cannot tell them apart reads a setup mistake as a governance decision.
            return Refuse($"No allowlist configured. Pass --allowlist <path> or set {AllowlistPath.EnvVar}. There is no default path, and with none every target is refused before a socket is opened.");
        }

        // ⚠️ A LITERAL `%USERPROFILE%` REACHING HERE IS A SHELL MISTAKE, AND THIS TOOL EXPANDS NOTHING.
        // Measured cost in this project: a documented `cmd` invocation run in PowerShell handed a tool a
        // literal path, which then refused at the WRONG GATE and never reached the fence the run existed
        // to exercise. A fence that repairs its own input is one you cannot tell what it actually read —
        // so it is reported, and only when the path ALSO does not resolve.
        if (!File.Exists(allowlist) && allowlist.Contains('%'))
        {
            Console.Error.WriteLine($"usage error: the allowlist path '{allowlist}' does not exist AND contains a '%'.");
            Console.Error.WriteLine("  Nothing expanded it. cmd.exe expands %USERPROFILE%; PowerShell and POSIX shells do not - use");
            Console.Error.WriteLine("  $env:USERPROFILE (PowerShell) or $USERPROFILE (bash). This tool expands nothing on purpose.");
            Console.Error.WriteLine("  No connection was attempted.");
            return (int)VerifyExit.NothingExamined;
        }

        var options = new VerifyOptions(
            submission, binding, programs, address, port, (byte)unit, allowlist, stableReads, maxReads);

        return (int)VerifyRun.Execute(
            options,
            File.ReadAllText,
            DefaultExpand,
            DefaultConnect,
            Console.Out);
    }

    /// <summary>The real wire — <c>Harness.Wire</c>'s own transport and its own measured <c>ModbusPolicy</c>. A second client with its own settings would be an uncalibrated one.</summary>
    private static IRegisterTransport DefaultConnect(string host, int port, byte unit) =>
        NModbusTransport.Connect(host, port, unit);

    /// <summary>A directory becomes its <c>.ir</c> files in a stable order; anything else is itself. <c>harness-run</c>'s rule, so the two compose the same set.</summary>
    private static IReadOnlyList<string> DefaultExpand(string path) =>
        Directory.Exists(path)
            ? Directory.GetFiles(path, "*.ir").OrderBy(p => p, StringComparer.Ordinal).ToArray()
            : new[] { path };

    private static int Refuse(string message)
    {
        Console.Error.WriteLine($"usage error: {message}");
        Console.Error.WriteLine("  Nothing was read and no connection was attempted.");
        return (int)VerifyExit.NothingExamined;
    }

    private static void Usage(TextWriter output)
    {
        output.WriteLine("harness-verify - exercise DB-6 (\"ISOLATION BY CONSTRUCTION\") against a live device. READ-ONLY.");
        output.WriteLine();
        output.WriteLine("Composes a submission + binding + program into a RegisterMap and a BuildStamp exactly as");
        output.WriteLine("`harness-run --generate-only` does, connects over Modbus, and then:");
        output.WriteLine("  1. runs VersionCheck.Confirm and prints the whole VersionReport, ReadsToSettle included;");
        output.WriteLine("  2. calls MirrorClient.ReadControl() on the composition's own client;");
        output.WriteLine("  3. POSITIVE CONTROL: the same call on a client holding a deliberately WRONG stamp, which must be");
        output.WriteLine("     refused by name;");
        output.WriteLine("  4. ACCEPTANCE CONTROL: the same call on a client holding the stamp READ OFF THE DEVICE, which must");
        output.WriteLine("     be admitted - a guard only ever observed refusing is evidence about nothing.");
        output.WriteLine();
        output.WriteLine("  --submission <file>         required. The conformance vectors.");
        output.WriteLine("  --binding <file>            required. The coordinator's harness binding.");
        output.WriteLine("  --program <file-or-dir>...  required. The program under test; THE BUILD STAMP IS TAKEN OVER IT.");
        output.WriteLine("  --address <host>            required. No default: a guessed target is the mistake the fence prevents.");
        output.WriteLine("  --port <n>                  required, NO DEFAULT. This rig serves 503; 502 is refused (measured).");
        output.WriteLine("  --allowlist <path>          required unless LADDER_DEVICE_ALLOWLIST is set. No default path exists.");
        output.WriteLine("  --unit <n>                  default 1.");
        output.WriteLine("  --stable-reads <n>          default 3. Identical consecutive reads that count as settled; min 2.");
        output.WriteLine("  --max-reads <n>             default 60. Bound, so a flapping register ends the check.");
        output.WriteLine();
        output.WriteLine("exit codes:");
        output.WriteLine($"  {(int)VerifyExit.Confirmed} the device IS running the composed build, and every DB-6 control behaved");
        output.WriteLine($"  {(int)VerifyExit.NotConfirmed} the device is NOT running it, and the guard behaved - a statement about the DEVICE");
        output.WriteLine($"  {(int)VerifyExit.NothingExamined} nothing examined (usage, or an input that could not be read)");
        output.WriteLine($"  {(int)VerifyExit.Refused} the device fence refused the target - NO SOCKET WAS OPENED");
        output.WriteLine($"  {(int)VerifyExit.NothingRead} nothing read - the transport would not open, or a control could not be constructed");
        output.WriteLine($"  {(int)VerifyExit.NotComposed} not composed - no map and no stamp exist, so no socket was opened");
        output.WriteLine($"  {(int)VerifyExit.GuardDidNotBehave} *** THE GUARD DID NOT BEHAVE - a finding about the HARNESS, and it outranks all of the above");
        output.WriteLine($"  {(int)VerifyExit.NotEstablished} the guard was NOT FULLY EXERCISED (the device published a zero stamp, so the acceptance");
        output.WriteLine("    control could not be constructed) - not the same as it misbehaving, and a device fact rather than a harness one");
    }

    private static string? Option(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    /// <summary>Every value after <paramref name="name"/>, for a flag that may repeat or take a list.</summary>
    private static IReadOnlyList<string> Values(string[] args, string name)
    {
        var values = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], name, StringComparison.Ordinal))
                continue;

            for (var j = i + 1; j < args.Length && !args[j].StartsWith("--", StringComparison.Ordinal); j++)
                values.Add(args[j]);
        }

        return values;
    }

    private static bool TryInt(string[] args, string name, int fallback, out int value)
    {
        var raw = Option(args, name);
        if (raw is null)
        {
            value = fallback;
            return true;
        }

        return int.TryParse(raw, out value);
    }
}
