using System;
using OpennessCli.Cli;
using OpennessCli.Openness;

namespace OpennessCli;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var parseResult = ArgumentParser.Parse(args);
        if (parseResult is ParseResult.Failure failure)
        {
            Console.Error.WriteLine(failure.Message);
            return ExitCodes.UsageError;
        }

        var (tiaInstallOverride, timeoutConnectSeconds, timeoutOpenSeconds) = CommonOptions(parseResult);

        try
        {
            // Fail fast with a clear message before touching Portal at all.
            TiaInstallLocator.Resolve(tiaInstallOverride);
        }
        catch (TiaInstallNotFoundException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCodes.EnvironmentError;
        }

        using IOpennessGateway gateway = new OpennessGateway();
        try
        {
            // portal-status is the one command that must NOT go through Connect(): attaching risks
            // the first-connect approval dialog, and launching a fresh Portal is the exact opposite
            // of a pileup diagnostic. It only reads TiaPortal.GetProcesses(), so it runs here —
            // before, and instead of, the Connect()-first flow every other command relies on. (It
            // still needs TiaInstallLocator.Resolve above, which already ran, so the Siemens
            // assembly can load for the static GetProcesses() call.)
            if (parseResult is ParseResult.PortalStatusSuccess portalStatus)
            {
                return RunPortalStatus(gateway, portalStatus.Options);
            }

            gateway.Connect(TimeSpan.FromSeconds(timeoutConnectSeconds));

            switch (parseResult)
            {
                case ParseResult.ListSuccess list:
                    return RunList(gateway, list.Options, timeoutOpenSeconds);
                case ParseResult.ExportSuccess export:
                    return RunExport(gateway, export.Options, timeoutOpenSeconds);
                case ParseResult.ImportSuccess import:
                    return RunImport(gateway, import.Options, timeoutOpenSeconds);
                case ParseResult.CompileSuccess compile:
                    return RunCompile(gateway, compile.Options, timeoutOpenSeconds);
                case ParseResult.DeleteSuccess delete:
                    return RunDelete(gateway, delete.Options, timeoutOpenSeconds);
                case ParseResult.CreateInstanceDbSuccess createInstanceDb:
                    return RunCreateInstanceDb(gateway, createInstanceDb.Options, timeoutOpenSeconds);
                case ParseResult.SanityCheckSuccess sanityCheck:
                    return RunSanityCheck(gateway, sanityCheck.Options, timeoutOpenSeconds);
                default:
                    throw new InvalidOperationException($"Unhandled parse result: {parseResult.GetType().Name}");
            }
        }
        catch (Exception ex)
        {
            // One catch, one classification (2026-08-05, audit F-09). The chain of typed catch
            // clauses this replaces had drifted: nine domain errors the user can act on were in no
            // clause at all and fell through to the catch-all, so a mis-typed --group reported as an
            // internal fault. ExitCodes.ForException is where the classification now lives, and it is
            // unit-tested — the old form could only be checked by running the real CLI.
            var exitCode = ExitCodes.ForException(ex);
            Console.Error.WriteLine(exitCode == ExitCodes.UnexpectedError
                ? $"openness-cli {args[0]} failed: {DescribeWithInnerExceptions(ex)}"
                : ex.Message);
            return exitCode;
        }
    }

    private static string DescribeWithInnerExceptions(Exception ex)
    {
        var messages = new List<string>();
        for (var current = ex; current is not null; current = current.InnerException)
        {
            messages.Add($"{current.GetType().Name}: {current.Message}");
        }

        return string.Join(" ---> ", messages);
    }

    private static int RunList(IOpennessGateway gateway, ListOptions options, int timeoutOpenSeconds)
    {
        gateway.OpenProject(options.ProjectIdentifier, TimeSpan.FromSeconds(timeoutOpenSeconds));
        if (options.TagTables)
        {
            var tagTables = gateway.EnumerateTagTables();
            Console.WriteLine(options.Json ? OutputFormatter.FormatTagTableJson(tagTables) : OutputFormatter.FormatTagTableTable(tagTables));
        }
        else
        {
            var blocks = gateway.EnumerateBlocks();
            Console.WriteLine(options.Json ? OutputFormatter.FormatJson(blocks) : OutputFormatter.FormatTable(blocks));
        }

        return ExitCodes.Success;
    }

    private static int RunExport(IOpennessGateway gateway, ExportCommandOptions options, int timeoutOpenSeconds)
    {
        gateway.OpenProject(options.ProjectIdentifier, TimeSpan.FromSeconds(timeoutOpenSeconds));
        if (options.TypeName is not null)
        {
            gateway.ExportType(options.TypeName, options.Device, options.OutPath);
            Console.WriteLine($"Exported '{options.TypeName}' -> {options.OutPath}");
        }
        else if (options.TagTableName is not null)
        {
            gateway.ExportTagTable(options.TagTableName, options.Device, options.OutPath);
            Console.WriteLine($"Exported '{options.TagTableName}' -> {options.OutPath}");
        }
        else
        {
            gateway.ExportBlock(options.BlockName!, options.Device, options.OutPath);
            Console.WriteLine($"Exported '{options.BlockName}' -> {options.OutPath}");
        }

        return ExitCodes.Success;
    }

    private static int RunImport(IOpennessGateway gateway, ImportCommandOptions options, int timeoutOpenSeconds)
    {
        gateway.OpenProject(options.ProjectIdentifier, TimeSpan.FromSeconds(timeoutOpenSeconds));
        if (options.AsType)
        {
            var importedTypes = gateway.ImportTypes(options.GroupPath, options.Files);
            foreach (var name in importedTypes)
            {
                Console.WriteLine(name);
            }
        }
        else if (options.AsTagTable)
        {
            var importedTagTables = gateway.ImportTagTables(options.GroupPath, options.Files);
            foreach (var name in importedTagTables)
            {
                Console.WriteLine(name);
            }
        }
        else
        {
            var imported = gateway.ImportBlocks(options.GroupPath, options.Files);
            Console.WriteLine(OutputFormatter.FormatTable(imported));
        }

        return ExitCodes.Success;
    }

    private static int RunCompile(IOpennessGateway gateway, CompileCommandOptions options, int timeoutOpenSeconds)
    {
        gateway.OpenProject(options.ProjectIdentifier, TimeSpan.FromSeconds(timeoutOpenSeconds));
        var result = (options.Block, options.Type) switch
        {
            (null, null) => gateway.Compile(options.Device),
            (not null, null) => gateway.CompileBlock(options.Block, options.Device),
            (null, not null) => gateway.CompileType(options.Type, options.Device),
            _ => throw new InvalidOperationException("--block and --type are mutually exclusive."),
        };
        Console.WriteLine(options.Json ? OutputFormatter.FormatCompileJson(result) : OutputFormatter.FormatCompileTable(result));
        return result.State == Model.CompileState.Success ? ExitCodes.Success : ExitCodes.CompileFailed;
    }

    private static int RunDelete(IOpennessGateway gateway, DeleteCommandOptions options, int timeoutOpenSeconds)
    {
        gateway.OpenProject(options.ProjectIdentifier, TimeSpan.FromSeconds(timeoutOpenSeconds));
        var info = gateway.DeleteBlock(options.BlockName, options.Device, options.Confirm);
        if (!options.Confirm)
        {
            Console.WriteLine($"Would delete '{info.Name}' ({info.Path}) — pass --yes to confirm.");
            return ExitCodes.NotConfirmed;
        }

        Console.WriteLine($"Deleted '{info.Name}' ({info.Path}).");
        return ExitCodes.Success;
    }

    private static int RunCreateInstanceDb(IOpennessGateway gateway, CreateInstanceDbCommandOptions options, int timeoutOpenSeconds)
    {
        gateway.OpenProject(options.ProjectIdentifier, TimeSpan.FromSeconds(timeoutOpenSeconds));
        var info = gateway.CreateInstanceDb(options.GroupPath, options.DbName, options.InstanceOfName);
        Console.WriteLine($"Created '{info.Name}' (DB{info.Number}, instance of '{options.InstanceOfName}') at {info.Path}.");
        return ExitCodes.Success;
    }

    private static int RunSanityCheck(IOpennessGateway gateway, ListOptions options, int timeoutOpenSeconds)
    {
        gateway.OpenProject(options.ProjectIdentifier, TimeSpan.FromSeconds(timeoutOpenSeconds));
        var result = gateway.RunSanityCheck();
        Console.WriteLine(options.Json ? OutputFormatter.FormatSanityCheckJson(result) : OutputFormatter.FormatSanityCheckTable(result));
        return result.IsHealthy ? ExitCodes.Success : ExitCodes.SanityCheckFailed;
    }

    private static int RunPortalStatus(IOpennessGateway gateway, PortalStatusOptions options)
    {
        // No Connect()/OpenProject() — see the seam comment in Main. Read-only diagnosis, so it's
        // purely informational: always exit 0. Strays are frequently legitimate human windows, so a
        // non-zero-on-stray gate would just be noise; this is not a compile-style gate.
        var processes = gateway.EnumeratePortalProcesses();
        var report = PortalStatusClassifier.Classify(processes);
        Console.WriteLine(options.Json
            ? OutputFormatter.FormatPortalStatusJson(report)
            : OutputFormatter.FormatPortalStatusTable(report));
        return ExitCodes.Success;
    }

    private static (string? TiaInstallOverride, int TimeoutConnectSeconds, int TimeoutOpenSeconds) CommonOptions(ParseResult result) => result switch
    {
        ParseResult.ListSuccess s => (s.Options.TiaInstallOverride, s.Options.TimeoutConnectSeconds, s.Options.TimeoutOpenSeconds),
        ParseResult.ExportSuccess s => (s.Options.TiaInstallOverride, s.Options.TimeoutConnectSeconds, s.Options.TimeoutOpenSeconds),
        ParseResult.ImportSuccess s => (s.Options.TiaInstallOverride, s.Options.TimeoutConnectSeconds, s.Options.TimeoutOpenSeconds),
        ParseResult.CompileSuccess s => (s.Options.TiaInstallOverride, s.Options.TimeoutConnectSeconds, s.Options.TimeoutOpenSeconds),
        ParseResult.DeleteSuccess s => (s.Options.TiaInstallOverride, s.Options.TimeoutConnectSeconds, s.Options.TimeoutOpenSeconds),
        ParseResult.CreateInstanceDbSuccess s => (s.Options.TiaInstallOverride, s.Options.TimeoutConnectSeconds, s.Options.TimeoutOpenSeconds),
        ParseResult.SanityCheckSuccess s => (s.Options.TiaInstallOverride, s.Options.TimeoutConnectSeconds, s.Options.TimeoutOpenSeconds),
        ParseResult.PortalStatusSuccess s => (s.Options.TiaInstallOverride, s.Options.TimeoutConnectSeconds, s.Options.TimeoutOpenSeconds),
        _ => throw new InvalidOperationException($"Unhandled parse result: {result.GetType().Name}"),
    };
}

/// <summary>
/// The process exit codes, and the exception -> exit-code classification that chooses between them.
/// Public (rather than internal, as it was until 2026-08-05) so the classification can be unit-tested
/// without a live Portal session — see <c>ExitCodeMappingTests</c>.
/// </summary>
public static class ExitCodes
{
    public const int Success = 0;
    public const int UsageError = 1;
    public const int EnvironmentError = 2;
    public const int ConnectTimeout = 3;
    public const int ProjectOpenTimeout = 4;
    public const int UnexpectedError = 5;
    public const int SafetyRefused = 6;
    public const int CommandError = 7;
    public const int CompileFailed = 8;
    public const int SanityCheckFailed = 9;
    public const int NotConfirmed = 10;

    /// <summary>
    /// Which exit code an escaping exception earns (2026-08-05, audit F-09).
    ///
    /// CommandError (7) means "you named something that isn't there, or named it ambiguously" — the
    /// user can fix it by re-running with a different argument, and the bare exception message is a
    /// useful thing to print. Everything else is UnexpectedError (5), printed with its inner-exception
    /// chain because it describes a state this tool did not expect to be in.
    ///
    /// The default is deliberately UnexpectedError: an unrecognized exception IS an unexpected one.
    /// Defaulting the other way would silently reclassify genuine bugs — including the 15 "X must be
    /// called before Y" precondition guards and the Openness-API invariant checks in OpennessGateway,
    /// all of which still throw a bare InvalidOperationException and all of which should keep landing
    /// here as faults rather than as user error.
    /// </summary>
    public static int ForException(Exception ex) => ex switch
    {
        ConnectTimeoutException => ConnectTimeout,
        ProjectOpenTimeoutException => ProjectOpenTimeout,
        SafetyContentRefusedException => SafetyRefused,

        // Named-thing-not-found / named-thing-ambiguous. The type, tag-table and group members of
        // this family were missing from the old catch clause and reported as internal faults.
        BlockNotFoundException => CommandError,
        AmbiguousBlockException => CommandError,
        TypeNotFoundException => CommandError,
        AmbiguousTypeException => CommandError,
        TagTableNotFoundException => CommandError,
        AmbiguousTagTableException => CommandError,
        DeviceNotFoundException => CommandError,
        GroupNotFoundException => CommandError,
        NotAPlcSoftwareContainerException => CommandError,

        // Not a naming mistake, but it was already classified this way and the message is actionable
        // (it names the path and points at the quirks note).
        ExportProducedNoFileException => CommandError,

        _ => UnexpectedError,
    };
}
