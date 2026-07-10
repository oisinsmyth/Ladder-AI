using System;

namespace OpennessCli.Cli;

public sealed record ListOptions(
    string ProjectIdentifier,
    bool Json,
    string? TiaInstallOverride,
    int TimeoutConnectSeconds,
    int TimeoutOpenSeconds);

public abstract record ParseResult
{
    private ParseResult()
    {
    }

    public sealed record Success(ListOptions Options) : ParseResult;

    public sealed record Failure(string Message) : ParseResult;
}

public static class ArgumentParser
{
    public const int DefaultTimeoutConnectSeconds = 180;
    public const int DefaultTimeoutOpenSeconds = 1800;

    private const string Usage =
        "Usage: openness-cli list <project> [--json] [--tia-install <path>] " +
        "[--timeout-connect <seconds>] [--timeout-open <seconds>]\n" +
        "  <project> is either the name of a project already open in TIA Portal, or a path to a .apNN file.";

    public static ParseResult Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return new ParseResult.Failure(Usage);
        }

        var subcommand = args[0];
        if (subcommand != "list")
        {
            return new ParseResult.Failure($"Unknown subcommand '{subcommand}'. Supported subcommands: list.{Environment.NewLine}{Usage}");
        }

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
                    if (args[i].StartsWith("--", StringComparison.Ordinal))
                    {
                        return new ParseResult.Failure($"Unknown flag '{args[i]}'.{Environment.NewLine}{Usage}");
                    }

                    if (projectIdentifier is not null)
                    {
                        return new ParseResult.Failure($"Unexpected extra argument '{args[i]}'.{Environment.NewLine}{Usage}");
                    }

                    projectIdentifier = args[i];
                    break;
            }
        }

        if (projectIdentifier is null)
        {
            return new ParseResult.Failure($"Missing required argument: <project>.{Environment.NewLine}{Usage}");
        }

        return new ParseResult.Success(new ListOptions(projectIdentifier, json, tiaInstall, timeoutConnect, timeoutOpen));
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
