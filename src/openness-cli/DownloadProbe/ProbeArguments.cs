using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace DownloadProbe;

internal sealed class ProbeArguments
{
    internal ProbeArguments(
        string projectPath,
        DownloadOptionChoice options,
        bool json,
        string? device,
        string? pcInterface,
        string? target,
        string logDirectory,
        int connectTimeoutSeconds,
        int openTimeoutSeconds,
        bool disruptive = false,
        string? toFolder = null)
    {
        ToFolder = toFolder;
        ProjectPath = projectPath;
        Options = options;
        Json = json;
        Device = device;
        PcInterface = pcInterface;
        Target = target;
        LogDirectory = logDirectory;
        ConnectTimeoutSeconds = connectTimeoutSeconds;
        OpenTimeoutSeconds = openTimeoutSeconds;
        Disruptive = disruptive;
    }

    internal string ProjectPath { get; }

    internal DownloadOptionChoice Options { get; }

    internal bool Json { get; }

    /// <summary>Narrows PLC device resolution when a project has more than one. Never widens it.</summary>
    internal string? Device { get; }

    /// <summary>
    /// The PC-side adapter to download through, by EXACT name, as the project's own
    /// <c>ConnectionConfiguration</c> spells it. Required whenever the project declares more than one
    /// — there is no default and no first-one-wins, because the candidate list on a normal
    /// engineering PC includes a PLCSIM virtual adapter and a wrong pick writes to the wrong thing.
    /// Narrowing only: it selects among what the project already declares and cannot introduce one.
    ///
    /// May carry a trailing <c>" #&lt;n&gt;"</c> naming <c>ConfigurationPcInterface.Number</c> as well,
    /// because a Name does not identify an adapter — see
    /// <see cref="ConnectionTargetSelector.TrySplitTrailingNumber"/> for the parse rule. Taken
    /// VERBATIM here; the whole of the matching, including the split, lives in the selector.
    /// </summary>
    internal string? PcInterface { get; }

    /// <summary>
    /// The target interface to download through, by EXACT name, when the chosen PC interface offers
    /// more than one. Narrowing only, same as <see cref="PcInterface"/>.
    ///
    /// This used to name a configured ADDRESS. It could not: every target interface's address
    /// collection is empty on the project this was built for (TIA's extended-download dialog scans
    /// and the human picks, and that never reaches the project model), so an address-keyed selection
    /// had nothing to select from.
    /// </summary>
    internal string? Target { get; }

    internal string LogDirectory { get; }

    internal int ConnectTimeoutSeconds { get; }

    internal int OpenTimeoutSeconds { get; }

    /// <summary>
    /// R8's SANCTIONED EXCEPTION, and the only thing in this program that can widen what gets
    /// answered. False unless <c>--disruptive</c> was spelled exactly; there is no environment
    /// variable, no default, no fallback and no retry path that can arrive here.
    ///
    /// What it changes is stated in one place only — <see cref="SelectionPolicyMode.Disruptive"/> in
    /// <see cref="NoActionFirstPolicy"/> — and it changes NOTHING ELSE: the scratch-path guard, the
    /// required <c>--options</c> literal, the exact-name interface matching and the verbatim logging
    /// are the same code on both paths.
    /// </summary>
    internal bool Disruptive { get; }

    /// <summary>
    /// *** THE NON-DESTRUCTIVE HALF OF THIS TOOL, AND THE ONLY ONE THAT TOUCHES NO DEVICE. ***
    /// <c>DownloadProvider.Download(DirectoryInfo, DownloadConfigurationDelegate)</c> — Openness's
    /// second download overload, which writes hardware and software to a FOLDER instead of a
    /// controller. No connection, no PC interface, no target, no CPU stop, nothing on the wire.
    ///
    /// It exists here for one reason: **a download runs its own compile first, and that compile is
    /// not the one any of our compiles run.** Measured 2026-08-13 — `compile --block`, the device
    /// compile, `compile-all --force` and `sanity-check` all reported clean while the download's
    /// internal compile failed and named a block. Folder-download reaches THAT compile without
    /// reaching the controller, so the check that was previously only available by writing to a live
    /// PLC becomes available offline.
    ///
    /// Whether the compile it runs is the SAME one is a measured claim, not an assumed one — see the
    /// evidence recorded in src/openness-cli/README.md. Mutually exclusive with the device path: a
    /// run either writes to a folder or attempts a controller, never both, so no invocation can be
    /// misread as the safe one.
    /// </summary>
    internal string? ToFolder { get; }

    internal SelectionPolicyMode PolicyMode =>
        Disruptive ? SelectionPolicyMode.Disruptive : SelectionPolicyMode.Normal;
}

internal abstract class ProbeParseResult
{
    internal sealed class Success : ProbeParseResult
    {
        internal Success(ProbeArguments arguments) => Arguments = arguments;

        internal ProbeArguments Arguments { get; }
    }

    internal sealed class Failure : ProbeParseResult
    {
        internal Failure(string message) => Message = message;

        internal string Message { get; }
    }
}

internal static class ProbeArgumentParser
{
    // `static readonly`, not `const`: the disruptive allowance and deny lists are rendered from
    // NoActionFirstPolicy's own data rather than retyped here, so the usage text cannot drift from
    // the lists the policy actually applies.
    internal static readonly string Usage =
        "download-probe <project.ap20> --options Software|SoftwareOnlyChanges|Hardware [--json]\n" +
        "               [--device <name>] --pc-interface \"<exact adapter name>\"\n" +
        "               [--target <exact target-interface name>] [--log-dir <dir>]\n" +
        "               [--timeout-connect <seconds>] [--timeout-open <seconds>] [--disruptive]\n" +
        "\n" +
        "  <project.ap20>  MUST be the scratch project: a file name ending '" + ScratchProjectGuard.RequiredSuffix + "'.\n" +
        "  --options       REQUIRED, no default. Exactly one of the three literals above.\n" +
        "  --pc-interface  the PC adapter to download through, by EXACT name (case-sensitive, whole\n" +
        "                  string). REQUIRED whenever the project declares more than one; no default,\n" +
        "                  no substring matching. A refusal lists every candidate verbatim.\n" +
        "                  Two adapters can share a NAME and differ only in NUMBER, so the value may\n" +
        "                  also carry a trailing ' #<n>' — the exact form `openness-cli download-plan`\n" +
        "                  prints, e.g. \"Microsoft Hyper-V Network Adapter #2\". The value is tried\n" +
        "                  WHOLE as a name first, so a name containing a '#' is never split; a number\n" +
        "                  naming no adapter is refused, never resolved by name alone.\n" +
        "  --target        the target interface, by EXACT name, when the chosen PC interface has more\n" +
        "                  than one. Not an IP address: the project's address collections are empty.\n" +
        "  --log-dir       where the verbatim configuration log is written. Defaults to\n" +
        "                  %LADDER_PROBE_LOG_DIR% if set, otherwise %TEMP%\\download-probe.\n" +
        "  --to-folder     *** NON-DESTRUCTIVE. *** Writes the download IMAGE to a directory via the\n" +
        "                  Download(DirectoryInfo, delegate) overload: no connection, nothing on the\n" +
        "                  wire, no CPU stopped. It exists to reach THE COMPILE A DOWNLOAD RUNS —\n" +
        "                  measurably not the same compile as --block / device / compile-all /\n" +
        "                  sanity-check, all four of which reported clean while a download's own\n" +
        "                  compile failed naming a block. Refuses --pc-interface, --target and\n" +
        "                  --disruptive: those belong to the device path, and a run must never be\n" +
        "                  ambiguous about which of the two it was.\n" +
        "  --json          a machine-readable report on stdout; the verbatim log then goes to the\n" +
        "                  file and to stderr, so stdout stays parseable.\n" +
        "  --disruptive    *** LETS THE DOWNLOAD ACTUALLY COMPLETE, BY STOPPING THE CPU. *** R8's\n" +
        "                  sanctioned exception, and the only flag that widens what this tool will\n" +
        "                  answer. Without it every selection policy is byte-for-byte as before.\n" +
        "                  Permits EXACTLY the selections the chosen --options already entails:\n" +
        "                      " + NoActionFirstPolicy.DisruptiveAllowanceSummary + "\n" +
        "                  and NOTHING else. These stay denied even here:\n" +
        "                      " + NoActionFirstPolicy.DisruptiveDenySummary + "\n" +
        "                  Expect the CPU to be left STOPPED. Be at the machine.";

    /// <summary>Environment override for the log directory, so no job-specific path lives in the repo.</summary>
    internal const string LogDirectoryEnvVar = "LADDER_PROBE_LOG_DIR";

    internal static ProbeParseResult Parse(IReadOnlyList<string> args) =>
        Parse(args, Environment.GetEnvironmentVariable(LogDirectoryEnvVar), Path.GetTempPath());

    internal static ProbeParseResult Parse(IReadOnlyList<string> args, string? logDirEnvValue, string tempPath)
    {
        if (args.Count == 0)
        {
            return new ProbeParseResult.Failure(Usage);
        }

        string? projectPath = null;
        string? optionsText = null;
        var json = false;
        string? device = null;
        string? pcInterface = null;
        string? target = null;
        string? logDir = null;
        var connectTimeout = 900;
        var openTimeout = 900;
        var optionsSeen = false;
        var disruptive = false;
        string? toFolder = null;

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--options":
                    if (optionsSeen)
                    {
                        return new ProbeParseResult.Failure(
                            "--options given more than once. One run selects one option, on purpose: " +
                            "the three are exercised as three separate runs with three separate logs.");
                    }

                    if (!TryTakeValue(args, ref i, out optionsText))
                    {
                        return new ProbeParseResult.Failure("--options requires a value. " + DownloadOptionChoices.DescribeRejection(null));
                    }

                    optionsSeen = true;
                    break;

                case "--json":
                    json = true;
                    break;

                case "--to-folder":
                    if (!TryTakeValue(args, ref i, out toFolder) || toFolder!.Length == 0)
                    {
                        return new ProbeParseResult.Failure(
                            "--to-folder requires a directory to write the download image into. It is the " +
                            "NON-DESTRUCTIVE mode: nothing is put on the wire and no CPU is stopped.");
                    }

                    break;

                // The ONLY place in this program that can set the disruptive mode. Bare literal,
                // matched by the same ordinal `switch` as every other flag, so `--disruptive=true`,
                // `--Disruptive` and `--disrupt` all fall through to the unknown-option arm below and
                // are USAGE ERRORS — never a silent enable and never a silent ignore.
                case "--disruptive":
                    disruptive = true;
                    break;

                case "--device":
                    if (!TryTakeValue(args, ref i, out device))
                    {
                        return new ProbeParseResult.Failure("--device requires a value.");
                    }

                    break;

                case "--pc-interface":
                    if (!TryTakeValue(args, ref i, out pcInterface))
                    {
                        return new ProbeParseResult.Failure(
                            "--pc-interface requires a value: the adapter's EXACT name, quoted, as the " +
                            "project spells it. Run `openness-cli download-plan` to read the list.");
                    }

                    if (pcInterface!.Length == 0)
                    {
                        return new ProbeParseResult.Failure(
                            "--pc-interface was given an empty name. An empty name matches no adapter, and " +
                            "this tool will not fall back to a default when the one you named is not there.");
                    }

                    break;

                case "--target":
                    if (!TryTakeValue(args, ref i, out target))
                    {
                        return new ProbeParseResult.Failure("--target requires a value.");
                    }

                    break;

                case "--log-dir":
                    if (!TryTakeValue(args, ref i, out logDir))
                    {
                        return new ProbeParseResult.Failure("--log-dir requires a value.");
                    }

                    break;

                case "--timeout-connect":
                    if (!TryTakeSeconds(args, ref i, out connectTimeout))
                    {
                        return new ProbeParseResult.Failure("--timeout-connect requires a positive whole number of seconds.");
                    }

                    break;

                case "--timeout-open":
                    if (!TryTakeSeconds(args, ref i, out openTimeout))
                    {
                        return new ProbeParseResult.Failure("--timeout-open requires a positive whole number of seconds.");
                    }

                    break;

                // No --yes, no --force, no --confirm. There is nothing here to escalate: this tool
                // has one job and every run of it is a real download attempt. The gate is the
                // scratch-path guard plus the owner's authorisation to run it at all.
                default:
                    if (arg.StartsWith("--", StringComparison.Ordinal))
                    {
                        return new ProbeParseResult.Failure($"Unknown option '{arg}'.\n\n{Usage}");
                    }

                    if (projectPath is not null)
                    {
                        return new ProbeParseResult.Failure(
                            $"Unexpected second positional argument '{arg}'. One project per run.\n\n{Usage}");
                    }

                    projectPath = arg;
                    break;
            }
        }

        if (projectPath is null)
        {
            return new ProbeParseResult.Failure("No project path given.\n\n" + Usage);
        }

        if (!optionsSeen)
        {
            return new ProbeParseResult.Failure(
                "--options is REQUIRED and has no default. " + DownloadOptionChoices.DescribeRejection(null) +
                "\n\n" + Usage);
        }

        if (!DownloadOptionChoices.TryParseLiteral(optionsText, out var options))
        {
            return new ProbeParseResult.Failure(DownloadOptionChoices.DescribeRejection(optionsText));
        }

        // Refused rather than ignored. The two modes write to different places, and a run that
        // silently dropped one of the flags would be exactly the invocation someone reads as
        // "this one was the safe one".
        if (toFolder is not null && (pcInterface is not null || target is not null))
        {
            return new ProbeParseResult.Failure(
                "--to-folder writes a download image to a directory and contacts no device, so it cannot " +
                "be combined with --pc-interface or --target. Drop those, or drop --to-folder.");
        }

        if (toFolder is not null && disruptive)
        {
            return new ProbeParseResult.Failure(
                "--to-folder cannot stop a CPU — there is no CPU in it — so --disruptive has nothing to " +
                "permit and is refused rather than accepted as a no-op.");
        }

        var resolvedLogDir = logDir
            ?? (string.IsNullOrWhiteSpace(logDirEnvValue) ? null : logDirEnvValue)
            ?? Path.Combine(tempPath, "download-probe");

        return new ProbeParseResult.Success(new ProbeArguments(
            projectPath, options, json, device, pcInterface, target, resolvedLogDir!, connectTimeout, openTimeout,
            disruptive, toFolder));
    }

    private static bool TryTakeValue(IReadOnlyList<string> args, ref int index, out string? value)
    {
        if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            value = null;
            return false;
        }

        value = args[++index];
        return true;
    }

    private static bool TryTakeSeconds(IReadOnlyList<string> args, ref int index, out int seconds)
    {
        seconds = 0;
        return TryTakeValue(args, ref index, out var text)
            && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out seconds)
            && seconds > 0;
    }
}
