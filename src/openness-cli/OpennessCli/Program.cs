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

        var (tiaInstallOverride, timeoutConnectSeconds, timeoutOpenSeconds) = ArgumentParser.CommonOptions(parseResult);

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

            // The unconfirmed dry run answers from the arguments alone, so it must not pay for a
            // Portal connect first — and it certainly must not FAIL on one. Checked here, before
            // Connect, for the same reason portal-status is: refusing to act needs no session.
            if (parseResult is ParseResult.HmiCreateScreenSuccess { Options.Confirm: false } unconfirmed)
            {
                return RefuseUnconfirmedHmiCreateScreen(unconfirmed.Options);
            }

            if (parseResult is ParseResult.HmiEditScreenSuccess { Options.Confirm: false } unconfirmedEdit)
            {
                return RefuseUnconfirmedHmiEditScreen(unconfirmedEdit.Options);
            }

            if (parseResult is ParseResult.HmiNewSuccess { Options.Confirm: false } unconfirmedNew)
            {
                Console.Error.WriteLine(
                    $"Would create {unconfirmedNew.Options.Kind} '{unconfirmedNew.Options.Name}'" +
                    (unconfirmedNew.Options.Parent is { } p ? $" in '{p}'" : string.Empty) +
                    $" in project '{unconfirmedNew.Options.ProjectIdentifier}'. Nothing was created, and Portal was not contacted. Re-run with --yes to proceed.");
                return ExitCodes.NotConfirmed;
            }

            if (parseResult is ParseResult.HmiDeleteSuccess { Options.Confirm: false } unconfirmedDel)
            {
                Console.Error.WriteLine(
                    $"Would DELETE {unconfirmedDel.Options.Kind} '{unconfirmedDel.Options.Name}' from project " +
                    $"'{unconfirmedDel.Options.ProjectIdentifier}'. Nothing was deleted, and Portal was not contacted. Re-run with --yes to proceed.");
                return ExitCodes.NotConfirmed;
            }

            if (parseResult is ParseResult.HmiCreateTagSuccess { Options.Confirm: false } unconfirmedTag)
            {
                Console.Error.WriteLine(
                    $"Would create HMI tag '{unconfirmedTag.Options.TagName}' (type {unconfirmedTag.Options.DataType}) in table " +
                    $"'{unconfirmedTag.Options.TableName}' of project '{unconfirmedTag.Options.ProjectIdentifier}'. " +
                    "Nothing was created, and Portal was not contacted. Re-run with --yes to proceed.");
                return ExitCodes.NotConfirmed;
            }

            gateway.Connect(TimeSpan.FromSeconds(timeoutConnectSeconds), ArgumentParser.ProjectIdentifier(parseResult));

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
                case ParseResult.HmiSuccess hmi:
                    return RunHmi(gateway, hmi.Options, timeoutOpenSeconds);
                case ParseResult.HmiCreateScreenSuccess hmiCreate:
                    return RunHmiCreateScreen(gateway, hmiCreate.Options, timeoutOpenSeconds);
                case ParseResult.HmiEditScreenSuccess hmiEdit:
                    return RunHmiEditScreen(gateway, hmiEdit.Options, timeoutOpenSeconds);
                case ParseResult.HmiCompileSuccess hmiCompile:
                    return RunHmiCompile(gateway, hmiCompile.Options, timeoutOpenSeconds);
                case ParseResult.HmiCreateTagSuccess hmiTag:
                    return RunHmiCreateTag(gateway, hmiTag.Options, timeoutOpenSeconds);
                case ParseResult.HmiNewSuccess hmiNew:
                    return RunHmiObject(gateway, hmiNew.Options, timeoutOpenSeconds, isDelete: false);
                case ParseResult.HmiDeleteSuccess hmiDel:
                    return RunHmiObject(gateway, hmiDel.Options, timeoutOpenSeconds, isDelete: true);
                case ParseResult.HmiInventorySuccess hmiInv:
                    return RunHmiInventory(gateway, hmiInv.Options, timeoutOpenSeconds);
                case ParseResult.HmiSetSuccess hmiSet:
                    return RunHmiSet(gateway, hmiSet.Options, timeoutOpenSeconds);
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

    // Read-only, and exits Success even when the project has no HMI device at all — "this project
    // has none" is a legitimate answer to the question the command asks, not a failure.
    private static int RunHmi(IOpennessGateway gateway, HmiOptions options, int timeoutOpenSeconds)
    {
        gateway.OpenProject(options.ProjectIdentifier, TimeSpan.FromSeconds(timeoutOpenSeconds));

        if (options.Schema)
        {
            var reports = gateway.EnumerateHmiSchema(options.Screen ?? "*", options.MaxItems);
            Console.WriteLine(options.Json
                ? OutputFormatter.FormatHmiSchemaJson(reports)
                : OutputFormatter.FormatHmiSchemaReport(reports));

            return ExitCodes.Success;
        }

        var devices = gateway.EnumerateHmi(options.Screen, options.MaxItems);
        Console.WriteLine(options.Json
            ? OutputFormatter.FormatHmiJson(devices)
            : OutputFormatter.FormatHmiReport(devices, options.Screen));

        return ExitCodes.Success;
    }

    private static int RefuseUnconfirmedHmiCreateScreen(HmiCreateScreenOptions options)
    {
        Console.Error.WriteLine(
            $"Would create screen '{options.ScreenName}' ({options.Width}x{options.Height})" +
            (options.ItemTypes.Count > 0 ? $" with items: {string.Join(", ", options.ItemTypes)}" : " with no items") +
            $" in project '{options.ProjectIdentifier}'. Nothing was created, and Portal was not contacted. Re-run with --yes to proceed.");
        return ExitCodes.NotConfirmed;
    }

    private static int RefuseUnconfirmedHmiEditScreen(HmiEditScreenOptions options)
    {
        // Lists every change, so the dry run is a reviewable plan rather than a count.
        Console.Error.WriteLine($"Would edit screen '{options.ScreenName}' in project '{options.ProjectIdentifier}':");
        foreach (var (target, attribute, value) in options.Sets)
        {
            Console.Error.WriteLine($"  set {target}.{attribute} = {value}");
        }

        foreach (var (target, eventType, script) in options.Events)
        {
            Console.Error.WriteLine($"  event {target}:{eventType}" + (script is null ? " (no script)" : " (with script)"));
        }

        foreach (var (target, property, tag) in options.Binds)
        {
            Console.Error.WriteLine($"  bind {target}.{property} <- tag '{tag}'");
        }

        foreach (var itemType in options.AddItems)
        {
            Console.Error.WriteLine($"  add item {itemType}");
        }

        foreach (var (what, target, detail) in options.Deletes)
        {
            Console.Error.WriteLine($"  DELETE {what} {target}{(detail is null ? string.Empty : $" ({detail})")}");
        }

        Console.Error.WriteLine("Nothing was changed, and Portal was not contacted. Re-run with --yes to proceed.");
        return ExitCodes.NotConfirmed;
    }

    private static int RunHmiObject(IOpennessGateway gateway, HmiObjectOptions options, int timeoutOpenSeconds, bool isDelete)
    {
        gateway.OpenProject(options.ProjectIdentifier, TimeSpan.FromSeconds(timeoutOpenSeconds));
        var message = isDelete
            ? gateway.DeleteHmiObject(options.Kind, options.Name, options.AllowAnyName)
            : gateway.CreateHmiObject(options.Kind, options.Name, options.Parent);
        Console.WriteLine(message);

        // The delete path re-reads to confirm absence; if the object survived, that is a failure of
        // the operation even though nothing threw.
        // net48 has no string.Contains(string, StringComparison).
        return message.IndexOf("STILL PRESENT", StringComparison.Ordinal) >= 0 ? ExitCodes.CommandError : ExitCodes.Success;
    }

    private static int RunHmiSet(IOpennessGateway gateway, HmiObjectOptions options, int timeoutOpenSeconds)
    {
        gateway.OpenProject(options.ProjectIdentifier, TimeSpan.FromSeconds(timeoutOpenSeconds));
        var applied = gateway.SetHmiObjectAttributes(options.Kind, options.Name, options.Sets, options.Texts);
        Console.WriteLine($"{options.Kind} '{options.Name}': {OutputFormatter.DescribeAppliedCount(applied)} change(s)");
        foreach (var line in applied)
        {
            Console.WriteLine("  " + line);
        }

        // ANY refusal exits non-zero. This used to exit 0 whenever at least one change landed, on the
        // reasoning that the probe wants every verdict rather than a bail-out — but the verdicts are
        // printed either way, and the exit code is the only thing a script reads. Measured 2026-08-09
        // (P4.4): the alarm's class was set, its bit number and its EventText were both refused, and
        // the command exited 0. Partial success is not success.
        return OutputFormatter.CountRefusals(applied) > 0 ? ExitCodes.CommandError : ExitCodes.Success;
    }

    private static int RunHmiInventory(IOpennessGateway gateway, HmiObjectOptions options, int timeoutOpenSeconds)
    {
        gateway.OpenProject(options.ProjectIdentifier, TimeSpan.FromSeconds(timeoutOpenSeconds));
        var objects = gateway.InventoryHmi(string.IsNullOrEmpty(options.Kind) ? null : options.Kind);
        Console.WriteLine(options.Json
            ? OutputFormatter.FormatHmiInventoryJson(objects)
            : OutputFormatter.FormatHmiInventoryTable(objects));

        return ExitCodes.Success;
    }

    private static int RunHmiCreateTag(IOpennessGateway gateway, HmiCreateTagOptions options, int timeoutOpenSeconds)
    {
        gateway.OpenProject(options.ProjectIdentifier, TimeSpan.FromSeconds(timeoutOpenSeconds));
        Console.WriteLine(gateway.CreateHmiTag(options.TagName, options.TableName, options.DataType));
        return ExitCodes.Success;
    }

    private static int RunHmiEditScreen(IOpennessGateway gateway, HmiEditScreenOptions options, int timeoutOpenSeconds)
    {
        gateway.OpenProject(options.ProjectIdentifier, TimeSpan.FromSeconds(timeoutOpenSeconds));
        var result = gateway.EditHmiScreen(options.ScreenName, options.Sets, options.Events, options.Binds, options.Deletes, options.AddItems, options.BindKinds);
        Console.WriteLine(options.Json
            ? OutputFormatter.FormatHmiEditScreenJson(result)
            : OutputFormatter.FormatHmiEditScreenResult(result));

        if (result.Validation.Any(m => m.Severity is "Error" or "ValidateThrew"))
        {
            return ExitCodes.CompileFailed;
        }

        // Same rule as hmi-set: a refused change is a failed change. Measured 2026-08-09 (P3.2) —
        // three of five dynamization kinds were refused and the command still exited 0.
        return OutputFormatter.CountRefusals(result.Applied) > 0 ? ExitCodes.CommandError : ExitCodes.Success;
    }

    // The only writing HMI path. Gated on --yes like `delete`; the unconfirmed case never reaches
    // here (handled before Connect), so by this point the write is authorised.
    private static int RunHmiCreateScreen(IOpennessGateway gateway, HmiCreateScreenOptions options, int timeoutOpenSeconds)
    {
        gateway.OpenProject(options.ProjectIdentifier, TimeSpan.FromSeconds(timeoutOpenSeconds));
        var result = gateway.CreateHmiScreen(options.ScreenName, options.Width, options.Height, options.ItemTypes);
        Console.WriteLine(options.Json
            ? OutputFormatter.FormatHmiCreateScreenJson(result)
            : OutputFormatter.FormatHmiCreateScreenResult(result));

        // Validation errors are a failed gate, not a successful create with commentary — this is the
        // HMI analogue of hard rule 4's "never present non-compiling logic as finished".
        return result.Validation.Any(m => m.Severity is "Error" or "ValidateThrew")
            ? ExitCodes.CompileFailed
            : ExitCodes.Success;
    }

    // Deliberately does NOT carry the FI-52 consistency backstop that `compile` has: that check is
    // built on PlcBlock.IsConsistent, which has no HMI equivalent — there is no per-screen
    // consistency flag to cross-examine a green result with. So an HMI compile reporting Success
    // means only that the compiler said so, and nothing here can strengthen it.
    private static int RunHmiCompile(IOpennessGateway gateway, CompileCommandOptions options, int timeoutOpenSeconds)
    {
        gateway.OpenProject(options.ProjectIdentifier, TimeSpan.FromSeconds(timeoutOpenSeconds));
        var result = gateway.CompileHmi(options.Device);
        Console.WriteLine(options.Json ? OutputFormatter.FormatCompileJson(result) : OutputFormatter.FormatCompileTable(result));

        return result.State != Model.CompileState.Success ? ExitCodes.CompileFailed : ExitCodes.Success;
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

        if (result.State != Model.CompileState.Success)
        {
            return ExitCodes.CompileFailed;
        }

        // FI-52 (2026-08-07). A WHOLE-DEVICE compile is not a whole-PROGRAM gate, and used to say
        // it was. Confirmed live: a device compile returned "STATE: Success, ERRORS: 0" while 19 of
        // 34 freshly-imported blocks were still flagged IsConsistent=false — and one of those, when
        // compiled individually, failed with 8 errors. The quirk itself was known since 2026-07-10
        // (see IOpennessGateway.CompileBlock's own doc comment: a block re-imported via Import()
        // gets flagged inconsistent, and device-level Compile() reports Success without clearing
        // it) — but it was recorded in a doc comment, while the top-level instruction still named
        // a bare `compile` as the gate. A known trap written down where the person about to walk
        // into it does not read.
        //
        // Hard rule 4 rests on this exit code, so it fails closed: a pass now REQUIRES that
        // nothing was left unverified. Safety blocks never trip it — EnumerateBlocks reports them
        // consistent by construction rather than reading their flag (hard rule 2).
        if (options.Block is null && options.Type is null)
        {
            var unverified = gateway.EnumerateBlocks().Where(b => !b.IsConsistent).ToList();
            if (unverified.Count > 0)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine(
                    $"COMPILE INCOMPLETE: the device compiled without errors, but {unverified.Count} block(s) " +
                    "were not compiled and remain inconsistent. Device-level compile does not clear the " +
                    "inconsistent flag a freshly-imported block carries, so this is NOT a whole-program gate.");
                Console.Error.WriteLine("Compile these individually (--block / --type), in dependency order:");
                foreach (var block in unverified.OrderBy(b => b.Name, StringComparer.Ordinal))
                {
                    Console.Error.WriteLine($"  {block.Name}  ({block.Path})");
                }

                Console.Error.WriteLine("Or run `openness-cli sanity-check`, which checks consistency and compiles.");
                return ExitCodes.CompileIncomplete;
            }
        }

        return ExitCodes.Success;
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
    /// FI-52. The device compiled cleanly but did not cover every block: blocks remain flagged
    /// inconsistent, so the run proved less than it appears to. Distinct from
    /// <see cref="CompileFailed"/> — nothing reported an error; the gate simply did not examine
    /// everything, which is the failure mode that matters most because it looks like a pass.
    /// </summary>
    public const int CompileIncomplete = 11;

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

        // The HMI family, added 2026-08-08. Every one of these was falling through to
        // UnexpectedError = 5 and printing a full inner-exception chain, even though each is a
        // user-fixable naming or usage mistake whose message already says exactly how to fix it —
        // the same defect audit F-09 fixed for the PLC family above. A wrong screen name should not
        // read as an internal fault.
        NoUnifiedHmiDeviceException => CommandError,
        AmbiguousHmiDeviceException => CommandError,
        HmiScreenNotFoundException => CommandError,
        HmiScreenItemNotFoundException => CommandError,
        HmiScreenAlreadyExistsException => CommandError,
        HmiTagAlreadyExistsException => CommandError,
        HmiUnknownScreenItemTypeException => CommandError,
        HmiUnknownAttributeException => CommandError,
        HmiAttributeNotWritableException => CommandError,
        HmiUnknownEventTypeException => CommandError,
        HmiEventsNotSupportedException => CommandError,
        HmiDynamizationsNotSupportedException => CommandError,

        // The metamodel-command family. Added after P1 measured them exiting 5: the fix above was
        // made in the morning and these six exceptions were introduced in the afternoon WITHOUT
        // being mapped, recreating the same defect within hours. The reflection guard covers
        // ParseResult variants, not exception types — so nothing caught it. `HmiExceptionsAreClassified`
        // now closes that hole.
        HmiUnknownKindException => CommandError,
        HmiKindNotCreatableException => CommandError,
        HmiKindNotDeletableException => CommandError,
        HmiObjectAlreadyExistsException => CommandError,
        HmiObjectNotFoundException => CommandError,
        HmiRefusedToDeleteRealObjectException => CommandError,

        _ => UnexpectedError,
    };
}
