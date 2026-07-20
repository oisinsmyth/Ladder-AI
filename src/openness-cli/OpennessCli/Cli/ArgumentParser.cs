using System;
using System.Collections.Generic;

namespace OpennessCli.Cli;

// TagTables switches list's own output from blocks to PLC tag tables — a distinct object type
// (confirmed real 2026-07-14, grounding PlantAutoControl's own dependency closure: some referenced
// tags are bare, single-component Access references that never show up in the ordinary block
// enumeration at all). A separate view, not merged into the same listing, since a tag table has
// none of BlockInfo's own fields (Number/Language/Safety/Consistent).
public sealed record ListOptions(
    string ProjectIdentifier,
    bool Json,
    bool TagTables,
    string? TiaInstallOverride,
    int TimeoutConnectSeconds,
    int TimeoutOpenSeconds);

// Exactly one of BlockName/TypeName/TagTableName is set — --block/--type/--tagtable are mutually
// exclusive alternatives (a PLC data type/UDT and a PLC tag table, both confirmed real 2026-07-14,
// have no Number/ProgrammingLanguage the way a block does, so each needs its own distinct
// resolution path, not a shared "name" field).
public sealed record ExportCommandOptions(
    string ProjectIdentifier,
    string? BlockName,
    string? TypeName,
    string? TagTableName,
    string? Device,
    string OutPath,
    string? TiaInstallOverride,
    int TimeoutConnectSeconds,
    int TimeoutOpenSeconds);

// AsType/AsTagTable select which composition Import() targets (PlcTypeGroup.Types /
// PlcTagTableGroup.TagTables vs. PlcBlockGroup.Blocks) — both default to false (blocks), today's
// existing behavior, unchanged. Mutually exclusive, same as --block/--type/--tagtable on export.
public sealed record ImportCommandOptions(
    string ProjectIdentifier,
    string GroupPath,
    IReadOnlyList<string> Files,
    bool AsType,
    bool AsTagTable,
    string? TiaInstallOverride,
    int TimeoutConnectSeconds,
    int TimeoutOpenSeconds);

public sealed record CompileCommandOptions(
    string ProjectIdentifier,
    string? Device,
    string? Block,
    string? Type,
    bool Json,
    string? TiaInstallOverride,
    int TimeoutConnectSeconds,
    int TimeoutOpenSeconds);

public sealed record DeleteCommandOptions(
    string ProjectIdentifier,
    string BlockName,
    string? Device,
    bool Confirm,
    string? TiaInstallOverride,
    int TimeoutConnectSeconds,
    int TimeoutOpenSeconds);

// Grounding/scaffolding command (2026-07-14, `PlantAutoControl` round-trip plan Phase 0.2) — creates an
// instance DB backing an already-existing FB, for FBs imported standalone with no calling context.
// Not S6+ logic generation: invents no tag/address/DB number (DbName is engineer-supplied, the DB
// number itself is always auto-assigned by TIA).
public sealed record CreateInstanceDbCommandOptions(
    string ProjectIdentifier,
    string GroupPath,
    string DbName,
    string InstanceOfName,
    string? TiaInstallOverride,
    int TimeoutConnectSeconds,
    int TimeoutOpenSeconds);

// No <project> positional: portal-status inspects the running Portal *processes*, not a project,
// so it never connects or opens anything. Carries the common flags (--json + install/timeouts) only
// for uniformity with every other command; the timeout values are unused (it does no connect/open).
public sealed record PortalStatusOptions(
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

    public sealed record DeleteSuccess(DeleteCommandOptions Options) : ParseResult;

    public sealed record CreateInstanceDbSuccess(CreateInstanceDbCommandOptions Options) : ParseResult;

    public sealed record SanityCheckSuccess(ListOptions Options) : ParseResult;

    public sealed record PortalStatusSuccess(PortalStatusOptions Options) : ParseResult;

    public sealed record Failure(string Message) : ParseResult;
}

public static class ArgumentParser
{
    public const int DefaultTimeoutConnectSeconds = 180;
    public const int DefaultTimeoutOpenSeconds = 1800;

    private const string Usage =
        "Usage:\n" +
        "  openness-cli list          <project> [--json] [--tagtables] [--tia-install <path>] [--timeout-connect <s>] [--timeout-open <s>]\n" +
        "  openness-cli export        <project> (--block <name> | --type <name> | --tagtable <name>) --out <path> [--device <name>] [--tia-install <path>] [--timeout-connect <s>] [--timeout-open <s>]\n" +
        "  openness-cli import        <project> --group <device>/<path> [--type | --tagtable] <files...> [--tia-install <path>] [--timeout-connect <s>] [--timeout-open <s>]\n" +
        "  openness-cli compile       <project> [--device <name>] [--block <name> | --type <name>] [--json] [--tia-install <path>] [--timeout-connect <s>] [--timeout-open <s>]\n" +
        "  openness-cli delete        <project> --block <name> [--device <name>] --yes [--tia-install <path>] [--timeout-connect <s>] [--timeout-open <s>]\n" +
        "  openness-cli create-instance-db <project> --group <device>/<path> --name <name> --instance-of <FBName> [--tia-install <path>] [--timeout-connect <s>] [--timeout-open <s>]\n" +
        "  openness-cli sanity-check  <project> [--json] [--tia-install <path>] [--timeout-connect <s>] [--timeout-open <s>]\n" +
        "  openness-cli portal-status [--json] [--tia-install <path>]\n" +
        "  <project> is either the name of a project already open in TIA Portal, or a path to a .apNN file.\n" +
        "  --type selects a PLC data type (UDT) instead of a block; on import it's a switch (no value) applying to all files.\n" +
        "  --tagtable selects a PLC tag table instead of a block; on export it takes a name, on import it's a switch (no value) applying to all files.\n" +
        "  list --tagtables enumerates tag tables instead of blocks.";

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
            "delete" => ParseDelete(args),
            "create-instance-db" => ParseCreateInstanceDb(args),
            "sanity-check" => ParseSanityCheck(args),
            "portal-status" => ParsePortalStatus(args),
            var other => new ParseResult.Failure(
                $"Unknown subcommand '{other}'. Supported subcommands: list, export, import, compile, delete, create-instance-db, sanity-check, portal-status.{Environment.NewLine}{Usage}"),
        };
    }

    private static ParseResult ParseList(string[] args)
    {
        string? projectIdentifier = null;
        var json = false;
        var tagTables = false;
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
                case "--tagtables":
                    tagTables = true;
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

        return new ParseResult.ListSuccess(new ListOptions(projectIdentifier, json, tagTables, tiaInstall, timeoutConnect, timeoutOpen));
    }

    private static ParseResult ParseSanityCheck(string[] args)
    {
        // Same flag shape as `list` (project + --json + common flags, no --device — checks
        // every device in the project), so reuse its parsing directly rather than duplicating it.
        var result = ParseList(args);
        return result is ParseResult.ListSuccess success ? new ParseResult.SanityCheckSuccess(success.Options) : result;
    }

    private static ParseResult ParsePortalStatus(string[] args)
    {
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
                    // No <project> positional here — portal-status inspects Portal processes, not a
                    // project. Anything not a recognised flag is a mistake, flagged rather than swallowed.
                    return new ParseResult.Failure(
                        $"Unexpected argument '{args[i]}'. `portal-status` takes no <project> and no positional arguments — it inspects running Portal processes.{Environment.NewLine}{Usage}");
            }
        }

        return new ParseResult.PortalStatusSuccess(new PortalStatusOptions(json, tiaInstall, timeoutConnect, timeoutOpen));
    }

    private static ParseResult ParseExport(string[] args)
    {
        string? projectIdentifier = null;
        string? block = null;
        string? type = null;
        string? tagTable = null;
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
                case "--type":
                    if (!TryTakeValue(args, ref i, "--type", out type, out var typeErr))
                    {
                        return new ParseResult.Failure(typeErr);
                    }

                    break;
                case "--tagtable":
                    if (!TryTakeValue(args, ref i, "--tagtable", out tagTable, out var tagTableErr))
                    {
                        return new ParseResult.Failure(tagTableErr);
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

        var selectedCount = (block is not null ? 1 : 0) + (type is not null ? 1 : 0) + (tagTable is not null ? 1 : 0);
        if (selectedCount == 0)
        {
            return new ParseResult.Failure($"Missing required flag: --block <name>, --type <name>, or --tagtable <name>.{Environment.NewLine}{Usage}");
        }

        if (selectedCount > 1)
        {
            return new ParseResult.Failure($"--block, --type, and --tagtable are mutually exclusive.{Environment.NewLine}{Usage}");
        }

        if (outPath is null)
        {
            return new ParseResult.Failure($"Missing required flag: --out <path>.{Environment.NewLine}{Usage}");
        }

        return new ParseResult.ExportSuccess(new ExportCommandOptions(projectIdentifier, block, type, tagTable, device, outPath, tiaInstall, timeoutConnect, timeoutOpen));
    }

    private static ParseResult ParseImport(string[] args)
    {
        string? projectIdentifier = null;
        string? group = null;
        var files = new List<string>();
        var asType = false;
        var asTagTable = false;
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
                case "--type":
                    asType = true;
                    break;
                case "--tagtable":
                    asTagTable = true;
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

        if (asType && asTagTable)
        {
            return new ParseResult.Failure($"--type and --tagtable are mutually exclusive.{Environment.NewLine}{Usage}");
        }

        return new ParseResult.ImportSuccess(new ImportCommandOptions(projectIdentifier, group, files, asType, asTagTable, tiaInstall, timeoutConnect, timeoutOpen));
    }

    private static ParseResult ParseCompile(string[] args)
    {
        string? projectIdentifier = null;
        string? device = null;
        string? block = null;
        string? type = null;
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
                case "--type":
                    if (!TryTakeValue(args, ref i, "--type", out type, out var typeErr))
                    {
                        return new ParseResult.Failure(typeErr);
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

        if (block is not null && type is not null)
        {
            return new ParseResult.Failure($"--block and --type are mutually exclusive.{Environment.NewLine}{Usage}");
        }

        return new ParseResult.CompileSuccess(new CompileCommandOptions(projectIdentifier, device, block, type, json, tiaInstall, timeoutConnect, timeoutOpen));
    }

    private static ParseResult ParseDelete(string[] args)
    {
        string? projectIdentifier = null;
        string? block = null;
        string? device = null;
        var confirm = false;
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
                case "--device":
                    if (!TryTakeValue(args, ref i, "--device", out device, out var deviceErr))
                    {
                        return new ParseResult.Failure(deviceErr);
                    }

                    break;
                case "--yes":
                    confirm = true;
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

        return new ParseResult.DeleteSuccess(new DeleteCommandOptions(projectIdentifier, block, device, confirm, tiaInstall, timeoutConnect, timeoutOpen));
    }

    private static ParseResult ParseCreateInstanceDb(string[] args)
    {
        string? projectIdentifier = null;
        string? group = null;
        string? name = null;
        string? instanceOf = null;
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
                case "--name":
                    if (!TryTakeValue(args, ref i, "--name", out name, out var nameErr))
                    {
                        return new ParseResult.Failure(nameErr);
                    }

                    break;
                case "--instance-of":
                    if (!TryTakeValue(args, ref i, "--instance-of", out instanceOf, out var instanceOfErr))
                    {
                        return new ParseResult.Failure(instanceOfErr);
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

        if (group is null)
        {
            return new ParseResult.Failure($"Missing required flag: --group <device>/<path>.{Environment.NewLine}{Usage}");
        }

        if (name is null)
        {
            return new ParseResult.Failure($"Missing required flag: --name <name>.{Environment.NewLine}{Usage}");
        }

        if (instanceOf is null)
        {
            return new ParseResult.Failure($"Missing required flag: --instance-of <FBName>.{Environment.NewLine}{Usage}");
        }

        return new ParseResult.CreateInstanceDbSuccess(new CreateInstanceDbCommandOptions(projectIdentifier, group, name, instanceOf, tiaInstall, timeoutConnect, timeoutOpen));
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
