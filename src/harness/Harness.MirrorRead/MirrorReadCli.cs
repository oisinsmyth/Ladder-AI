using DeviceGuard;

namespace Harness.MirrorRead;

/// <summary>
/// The command line, with the three things a process boundary owns injected: where the sockets come
/// from, where the two output channels go, and how a file gets written.
///
/// <para><b>Why it is not in <c>Program.Main</c>.</b> It was, and the consequence was that
/// <c>--declared-registers</c>'s parsing, the allowlist resolution and the exit codes for every refusal
/// could only be exercised by running the binary against a device. The measurement was thoroughly tested
/// and the argument vector reaching it was not — which matters more now that a BATCH composes that
/// vector, from a derived width, with no person reading it first.</para>
/// </summary>
public static class MirrorReadCli
{
    /// <param name="standardOutput">Where the prose transcript goes — unless <c>--json</c> moves it.</param>
    /// <param name="standardError">Refusals, and the prose transcript under <c>--json</c>.</param>
    /// <param name="writeFile">
    /// How <c>--out</c> writes. Injected so the file this tool produces is asserted rather than assumed:
    /// the batch reads that file as the run's evidence, and a report that silently did not get written is
    /// indistinguishable from a check that was never planned.
    /// </param>
    public static int Run(
        string[] args,
        IRegisterSourceFactory factory,
        TextWriter standardOutput,
        TextWriter standardError,
        Action<string, string> writeFile)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);
        ArgumentNullException.ThrowIfNull(writeFile);

        if (args.Contains("--help") || args.Contains("-h"))
        {
            Usage(standardOutput);
            return (int)MirrorReadExit.Usage;
        }

        // 🔴 --json PUTS THE DOCUMENT ON STDOUT AND THE PROSE ON STDERR, AND DISCARDS NEITHER.
        //
        // Every other tool here that grew a --json emits only the document, because their prose is a
        // rendering of the same facts. This tool's prose is not: it is the transcript of what was asked
        // of a device and what came back, and it is the artifact a person reads when the verdict is
        // surprising. So the two channels carry the two audiences, and a caller redirecting stdout still
        // gets something parseable.
        var json = args.Contains("--json");
        var prose = json ? standardError : standardOutput;

        var address = Option(args, "--address");
        if (string.IsNullOrWhiteSpace(address))
            return Refuse(standardError, "--address is required. There is no default target: a tool that guesses which device it is talking to has already made the mistake the fence exists to prevent.");

        // *** REQUIRED, NOT DEFAULTED. *** The declared width is the CLAIM under test and it comes from
        // the IR. A default would produce a fully plausible verdict against whatever width was last
        // true, on a run where nobody stated one — a flag whose omission yields a plausible artifact is
        // not optional, it is a defect with a default.
        var declaredRaw = Option(args, "--declared-registers");
        if (declaredRaw is null)
            return Refuse(standardError, "--declared-registers is required. It is the width MB_HOLD_REG declares in the IR (WORD n -> n registers, 0..n-1) and it is the claim this run tests. Defaulting it would let a run conclude against a width nobody stated.");
        if (!int.TryParse(declaredRaw, out var declared))
            return Refuse(standardError, $"--declared-registers takes a whole number of registers; got \"{declaredRaw}\".");

        if (!TryInt(args, standardError, "--port", 503, out var port)) return (int)MirrorReadExit.Usage;
        if (!TryInt(args, standardError, "--unit", 1, out var unit)) return (int)MirrorReadExit.Usage;
        if (unit is < 0 or > 255)
            return Refuse(standardError, $"--unit {unit} is not a Modbus unit identifier (0..255).");

        // The sweep straddles the declared edge by construction: three registers in, two out. Both ends
        // are still overridable, and MirrorReadOptions refuses any pair that does not straddle — a sweep
        // with no inside control, or one that never reaches the first undeclared register, runs and
        // proves nothing.
        if (!TryInt(args, standardError, "--boundary-from", Math.Max(0, declared - 3), out var boundaryFrom)) return (int)MirrorReadExit.Usage;
        if (!TryInt(args, standardError, "--boundary-to", declared + 1, out var boundaryTo)) return (int)MirrorReadExit.Usage;
        if (!TryInt(args, standardError, "--interval-ms", 3000, out var interval)) return (int)MirrorReadExit.Usage;

        // Zero by default: only a caller that has just downloaded knows the CPU was stopped, and only
        // that caller should pay for the wait.
        if (!TryInt(args, standardError, "--scan-retry", 0, out var scanRetries)) return (int)MirrorReadExit.Usage;
        if (!TryInt(args, standardError, "--scan-retry-interval-ms", 5000, out var scanRetryInterval)) return (int)MirrorReadExit.Usage;

        var allowlist = AllowlistPath.Resolve(Option(args, "--allowlist"), Environment.GetEnvironmentVariable);
        if (allowlist is null)
        {
            // Deliberately NOT passed through as a refusal from the guard. The guard would refuse it
            // correctly, but "you did not configure an allowlist" and "that device is not approved" are
            // different facts and a caller that could not tell them apart would read a setup mistake as
            // a governance decision.
            return Refuse(standardError, $"No allowlist configured. Pass --allowlist <path> or set {AllowlistPath.EnvVar}. There is no default path, and with none every target is refused before a socket is opened.");
        }

        var options = new MirrorReadOptions(
            address, port, (byte)unit, allowlist, declared, boundaryFrom, boundaryTo, interval,
            scanRetries, scanRetryInterval);

        var outcome = MirrorReadRun.ExecuteAndReport(options, factory, prose);

        // 🔴 THE REPORT IS WRITTEN ON EVERY PATH THAT GOT AS FAR AS HAVING ONE, INCLUDING THE REFUSALS.
        // A consumer that finds no file cannot tell a refusal from a run nobody started, and those are
        // already the two states hardest to tell apart. The refusals above this line are the exception,
        // and they are the ones with no target to describe yet.
        var outPath = Option(args, "--out");
        if (!string.IsNullOrWhiteSpace(outPath))
            writeFile(outPath!, outcome.Report.ToJson());

        if (json)
            standardOutput.WriteLine(outcome.Report.ToJson());

        return (int)outcome.Exit;
    }

    private static int Refuse(TextWriter standardError, string message)
    {
        standardError.WriteLine($"usage error: {message}");
        standardError.WriteLine("  Nothing was read and no connection was attempted, and NO REPORT WAS WRITTEN: this refusal "
            + "happened before there was a target to describe.");
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
        output.WriteLine("  --scan-retry <n>              default 0. EXTRA whole measurements, and ONLY when the scan counter did");
        output.WriteLine("                                not rise (exit 8) — the just-restarted CPU a download leaves behind.");
        output.WriteLine("                                A width verdict is a conclusion and is never retried.");
        output.WriteLine("  --scan-retry-interval-ms <ms> default 5000. Gap between those attempts.");
        output.WriteLine("  --out <path>                  write the JSON report there. Written on refusals too, saying it measured nothing.");
        output.WriteLine("  --json                        the report on stdout, the prose transcript on stderr. Neither is discarded.");
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

    private static bool TryInt(string[] args, TextWriter standardError, string name, int fallback, out int value)
    {
        var raw = Option(args, name);
        if (raw is null)
        {
            value = fallback;
            return true;
        }

        if (int.TryParse(raw, out value)) return true;

        Refuse(standardError, $"{name} takes a whole number; got \"{raw}\".");
        return false;
    }
}
