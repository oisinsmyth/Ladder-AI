using System;
using System.Collections.Generic;

namespace OpennessCli.Cli;

public sealed record ListOptions(
    string ProjectIdentifier,
    bool Json,
    string? TiaInstallOverride,
    int TimeoutConnectSeconds,
    int TimeoutOpenSeconds);

public sealed record ExportCommandOptions(
    string ProjectIdentifier,
    string BlockName,
    string? Device,
    string OutPath,
    string? TiaInstallOverride,
    int TimeoutConnectSeconds,
    int TimeoutOpenSeconds);

public sealed record ImportCommandOptions(
    string ProjectIdentifier,
    string GroupPath,
    IReadOnlyList<string> Files,
    string? TiaInstallOverride,
    int TimeoutConnectSeconds,
    int TimeoutOpenSeconds);

public sealed record CompileCommandOptions(
    string ProjectIdentifier,
    string? Device,
    string? Block,
    bool Json,
    string? TiaInstallOverride,
    int TimeoutConnectSeconds,
    int TimeoutOpenSeconds);

public abstract record ParseResult
{
    private ParseResult()
    {
    }

    public sealed record ListSuccess(ListOptions Options) : ParseResult;

    public sealed record ExportSuccess(ExportCommandOptions Options) : ParseResult;

    public sealed record ImportSuccess(ImportCommandOptions Options) : ParseResult;

    public sealed record CompileSuccess(CompileCommandOptions Options) : ParseResult;

    public sealed record SanityCheckSuccess(ListOptions Options) : ParseResult;

    public sealed record Failure(string Message) : ParseResult;
}

public static class ArgumentParser
{
    public const int DefaultTimeoutConnectSeconds = 180;
    public const int DefaultTimeoutOpenSeconds = 1800;

    private const string Usage =
        "Usage:\n" +
        "  openness-cli list          <project> [--json] [--tia-install <path>] [--timeout-connect <s>] [--timeout-open <s>]\n" +
        "  openness-cli export        <project> --block <name> --out <path> [--device <name>] [--tia-install <path>] [--timeout-connect <s>] [--timeout-open <s>]\n" +
        "  openness-cli import        <project> --group <device>/<path> <files...> [--tia-install <path>] [--timeout-connect <s>] [--timeout-open <s>]\n" +
        "  openness-cli compile       <project> [--device <name>] [--block <name>] [--json] [--tia-install <path>] [--timeout-connect <s>] [--timeout-open <s>]\n" +
        "  openness-cli sanity-check  <project> [--json] [--tia-install <path>] [--timeout-connect <s>] [--timeout-open <s>]\n" +
        "  <project> is either the name of a project already open in TIA Portal, or a path to a .apNN file.";

    public static ParseResult Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return new ParseResult.Failure(Usage);
        }

        return args[0] switch
        {
            "list" => ParseList(args),
            "export" => ParseExport(args),
            "import" => ParseImport(args),
            "compile" => ParseCompile(args),
            "sanity-check" => ParseSanityCheck(args),
            var other => new ParseResult.Failure(
                $"Unknown subcommand '{other}'. Supported subcommands: list, export, import, compile, sanity-check.{Environment.NewLine}{Usage}"),
        };
    }

    private static ParseResult ParseList(string[] args)
    {
        string? projectIdentifier = null;
        var json = false;
        string? tiaInstall = null;
        var timeoutConnect = DefaultTimeoutConnectSeconds;
        var timeoutOpen = DefaultTimeoutOpenSeconds;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--json":
                    json = true;
                    break;
                case "--tia-install":
                    if (!TryTakeValue(args, ref i, "--tia-install", out tiaInstall, out var installErr))
                    {
                        return new ParseResult.Failure(installErr);
                    }

                    break;
                case "--timeout-connect":
                    if (!TryTakeIntValue(args, ref i, "--timeout-connect", out timeoutConnect, out var connectErr))
                    {
                        return new ParseResult.Failure(connectErr);
                    }

                    break;
                case "--timeout-open":
                    if (!TryTakeIntValue(args, ref i, "--timeout-open", out timeoutOpen, out var openErr))
                    {
                        return new ParseResult.Failure(openErr);
                    }

                    break;
                default:
                    if (!TryTakePositional(args[i], ref projectIdentifier, out var posErr))
                    {
                        return new ParseResult.Failure(posErr);
                    }

                    break;
            }
        }

        if (projectIdentifier is null)
        {
            return new ParseResult.Failure($"Missing required argument: <project>.{Environment.NewLine}{Usage}");
        }

        return new ParseResult.ListSuccess(new ListOptions(projectIdentifier, json, tiaInstall, timeoutConnect, timeoutOpen));
    }

    private static ParseResult ParseSanityCheck(string[] args)
    {
        // Same flag shape as `list` (project + --json + common flags, no --device — checks
        // every device in the project), so reuse its parsing directly rather than duplicating it.
        var result = ParseList(args);
        return result is ParseResult.ListSuccess success ? new ParseResult.SanityCheckSuccess(success.Options) : result;
    }

    private static ParseResult ParseExport(string[] args)
    {
        string? projectIdentifier = null;
        string? block = null;
        string? device = null;
        string? outPath = null;
        string? tiaInstall = null;
        var timeoutConnect = DefaultTimeoutConnectSeconds;
        var timeoutOpen = DefaultTimeoutOpenSeconds;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--block":
                    if (!TryTakeValue(args, ref i, "--block", out block, out var blockErr))
                    {
                        return new ParseResult.Failure(blockErr);
                    }

                    break;
                case "--out":
                    if (!TryTakeValue(args, ref i, "--out", out outPath, out var outErr))
                    {
                        return new ParseResult.Failure(outErr);
                    }

                    break;
                case "--device":
                    if (!TryTakeValue(args, ref i, "--device", out device, out var deviceErr))
                    {
                        return new ParseResult.Failure(deviceErr);
                    }

                    break;
                case "--tia-install":
                    if (!TryTakeValue(args, ref i, "--tia-install", out tiaInstall, out var installErr))
                    {
                        return new ParseResult.Failure(installErr);
                    }

                    break;
                case "--timeout-connect":
                    if (!TryTakeIntValue(args, ref i, "--timeout-connect", out timeoutConnect, out var connectErr))
                    {
                        return new ParseResult.Failure(connectErr);
                    }

                    break;
                case "--timeout-open":
                    if (!TryTakeIntValue(args, ref i, "--timeout-open", out timeoutOpen, out var openErr))
                    {
                        return new ParseResult.Failure(openErr);
                    }

                    break;
                default:
                    if (!TryTakePositional(args[i], ref projectIdentifier, out var posErr))
                    {
                        return new ParseResult.Failure(posErr);
                    }

                    break;
            }
        }

        if (projectIdentifier is null)
        {
            return new ParseResult.Failure($"Missing required argument: <project>.{Environment.NewLine}{Usage}");
        }

        if (block is null)
        {
            return new ParseResult.Failure($"Missing required flag: --block <name>.{Environment.NewLine}{Usage}");
        }

        if (outPath is null)
        {
            return new ParseResult.Failure($"Missing required flag: --out <path>.{Environment.NewLine}{Usage}");
        }

        return new ParseResult.ExportSuccess(new ExportCommandOptions(projectIdentifier, block, device, outPath, tiaInstall, timeoutConnect, timeoutOpen));
    }

    private static ParseResult ParseImport(string[] args)
    {
        string? projectIdentifier = null;
        string? group = null;
        var files = new List<string>();
        string? tiaInstall = null;
        var timeoutConnect = DefaultTimeoutConnectSeconds;
        var timeoutOpen = DefaultTimeoutOpenSeconds;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--group":
                    if (!TryTakeValue(args, ref i, "--group", out group, out var groupErr))
                    {
                        return new ParseResult.Failure(groupErr);
                    }

                    break;
                case "--tia-install":
                    if (!TryTakeValue(args, ref i, "--tia-install", out tiaInstall, out var installErr))
                    {
                        return new ParseResult.Failure(installErr);
                    }

                    break;
                case "--timeout-connect":
                    if (!TryTakeIntValue(args, ref i, "--timeout-connect", out timeoutConnect, out var connectErr))
                    {
                        return new ParseResult.Failure(connectErr);
                    }

                    break;
                case "--timeout-open":
                    if (!TryTakeIntValue(args, ref i, "--timeout-open", out timeoutOpen, out var openErr))
                    {
                        return new ParseResult.Failure(openErr);
                    }

                    break;
                default:
                    if (args[i].StartsWith("--", StringComparison.Ordinal))
                    {
                        return new ParseResult.Failure($"Unknown flag '{args[i]}'.{Environment.NewLine}{Usage}");
                    }

                    if (projectIdentifier is null)
                    {
                        projectIdentifier = args[i];
                    }
                    else
                    {
                        files.Add(args[i]);
                    }

                    break;
            }
        }

        if (projectIdentifier is null)
        {
            return new ParseResult.Failure($"Missing required argument: <project>.{Environment.NewLine}{Usage}");
        }

        if (group is null)
        {
            return new ParseResult.Failure($"Missing required flag: --group <device>/<path>.{Environment.NewLine}{Usage}");
        }

        if (files.Count == 0)
        {
            return new ParseResult.Failure($"Missing required argument: at least one <file>.{Environment.NewLine}{Usage}");
        }

        return new ParseResult.ImportSuccess(new ImportCommandOptions(projectIdentifier, group, files, tiaInstall, timeoutConnect, timeoutOpen));
    }

    private static ParseResult ParseCompile(string[] args)
    {
        string? projectIdentifier = null;
        string? device = null;
        string? block = null;
        var json = false;
        string? tiaInstall = null;
        var timeoutConnect = DefaultTimeoutConnectSeconds;
        var timeoutOpen = DefaultTimeoutOpenSeconds;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--device":
                    if (!TryTakeValue(args, ref i, "--device", out device, out var deviceErr))
                    {
                        return new ParseResult.Failure(deviceErr);
                    }

                    break;
                case "--block":
                    if (!TryTakeValue(args, ref i, "--block", out block, out var blockErr))
                    {
                        return new ParseResult.Failure(blockErr);
                    }

                    break;
                case "--json":
                    json = true;
                    break;
                case "--tia-install":
                    if (!TryTakeValue(args, ref i, "--tia-install", out tiaInstall, out var installErr))
                    {
                        return new ParseResult.Failure(installErr);
                    }

                    break;
                case "--timeout-connect":
                    if (!TryTakeIntValue(args, ref i, "--timeout-connect", out timeoutConnect, out var connectErr))
                    {
                        return new ParseResult.Failure(connectErr);
                    }

                    break;
                case "--timeout-open":
                    if (!TryTakeIntValue(args, ref i, "--timeout-open", out timeoutOpen, out var openErr))
                    {
                        return new ParseResult.Failure(openErr);
                    }

                    break;
                default:
                    if (!TryTakePositional(args[i], ref projectIdentifier, out var posErr))
                    {
                        return new ParseResult.Failure(posErr);
                    }

                    break;
            }
        }

        if (projectIdentifier is null)
        {
            return new ParseResult.Failure($"Missing required argument: <project>.{Environment.NewLine}{Usage}");
        }

        return new ParseResult.CompileSuccess(new CompileCommandOptions(projectIdentifier, device, block, json, tiaInstall, timeoutConnect, timeoutOpen));
    }

    private static bool TryTakePositional(string arg, ref string? projectIdentifier, out string error)
    {
        if (arg.StartsWith("--", StringComparison.Ordinal))
        {
            error = $"Unknown flag '{arg}'.{Environment.NewLine}{Usage}";
            return false;
        }

        if (projectIdentifier is not null)
        {
            error = $"Unexpected extra argument '{arg}'.{Environment.NewLine}{Usage}";
            return false;
        }

        projectIdentifier = arg;
        error = string.Empty;
        return true;
    }

    private static bool TryTakeValue(string[] args, ref int i, string flag, out string? value, out string error)
    {
        if (i + 1 >= args.Length)
        {
            value = null;
            error = $"Flag '{flag}' requires a value.";
            return false;
        }

        i++;
        value = args[i];
        error = string.Empty;
        return true;
    }

    private static bool TryTakeIntValue(string[] args, ref int i, string flag, out int value, out string error)
    {
        if (!TryTakeValue(args, ref i, flag, out var raw, out error))
        {
            value = 0;
            return false;
        }

        if (!int.TryParse(raw, out value) || value <= 0)
        {
            error = $"Flag '{flag}' requires a positive integer number of seconds, got '{raw}'.";
            return false;
        }

        return true;
    }
}
