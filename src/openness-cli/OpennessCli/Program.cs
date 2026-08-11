using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpennessCli.Cli;
using OpennessCli.Openness;
using Siemens.Engineering;

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

            // A dry run of import-all is decided entirely from the files on disk — what each one is
            // and what order they would go in. Same reasoning as the unconfirmed writes above:
            // answering from the arguments alone must not pay for, or fail on, a Portal connect.
            if (parseResult is ParseResult.ImportAllSuccess { Options.DryRun: true } dryRunImport)
            {
                return RunImportAllDryRun(dryRunImport.Options);
            }

            if (parseResult is ParseResult.HmiCreateTagSuccess { Options.Confirm: false } unconfirmedTag)
            {
                Console.Error.WriteLine(
                    $"Would create HMI tag '{unconfirmedTag.Options.TagName}' (type {unconfirmedTag.Options.DataType}) in table " +
                    $"'{unconfirmedTag.Options.TableName}' of project '{unconfirmedTag.Options.ProjectIdentifier}'. " +
                    "Nothing was created, and Portal was not contacted. Re-run with --yes to proceed.");
                return ExitCodes.NotConfirmed;
            }

            // FI-61: warn BEFORE the attach, because an unapproved binary is refused silently and
            // the attach then burns the whole --timeout-connect with nothing on screen to explain
            // it. Advisory only — never blocks (see OpennessWhitelist's class comment for why).
            //
            // Merged 2026-08-09: this arrived on master while this branch independently hit the same
            // wall from the other side (an unapproved `library` build exiting 5 with a raw
            // "Security error", then exiting 3 on a silent 15-minute timeout). The two halves are
            // complementary and BOTH are kept — this one warns before the attach, and
            // ExitCodes.DescribeSecurityRefusal explains it afterwards when the refusal is thrown
            // rather than silent. Neither subsumes the other: the pre-check cannot see a refusal
            // that only materialises on connect, and the post-hoc message cannot fire when the
            // attach hangs instead of throwing.
            var approval = OpennessWhitelist.CheckRunningExecutable();
            var approvalWarning = OpennessWhitelist.DescribeIfNotApproved(
                approval, System.Reflection.Assembly.GetEntryAssembly()?.Location ?? "(unknown)");
            if (approvalWarning is not null)
            {
                Console.Error.WriteLine(approvalWarning);
            }

            // The project-preference argument is this branch's: it steers Connect toward a Portal
            // process that already has this project open instead of attaching to GetProcesses()[0].
            gateway.Connect(TimeSpan.FromSeconds(timeoutConnectSeconds), ArgumentParser.ProjectIdentifier(parseResult));

            switch (parseResult)
            {
                case ParseResult.ListSuccess list:
                    return RunList(gateway, list.Options, timeoutOpenSeconds);
                case ParseResult.ExportSuccess export:
                    return RunExport(gateway, export.Options, timeoutOpenSeconds);
                case ParseResult.ExportAllSuccess exportAll:
                    return RunExportAll(gateway, exportAll.Options, timeoutOpenSeconds);
                case ParseResult.ImportSuccess import:
                    return RunImport(gateway, import.Options, timeoutOpenSeconds);
                case ParseResult.ImportAllSuccess importAll:
                    return RunImportAll(gateway, importAll.Options, timeoutOpenSeconds);
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
                case ParseResult.LibrarySuccess library:
                    return RunLibrary(gateway, library.Options, timeoutOpenSeconds);
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

            // The approval refusal gets its own message: the exception's own text is "Security
            // error.", which is worse than useless here because the reader's next move is a click
            // inside Portal, not a change to what they typed.
            var securityHelp = ExitCodes.DescribeSecurityRefusal(ex);
            Console.Error.WriteLine(securityHelp ?? (exitCode == ExitCodes.UnexpectedError
                ? $"openness-cli {args[0]} failed: {DescribeWithInnerExceptions(ex)}"
                : ex.Message));
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
            : OutputFormatter.FormatHmiReport(devices, options.Screen, options.Scripts));

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

        foreach (var (target, property, kind) in options.BindKinds)
        {
            Console.Error.WriteLine($"  dynamization {kind} on {target}.{property}");
        }

        foreach (var (target, property) in options.MapClears)
        {
            Console.Error.WriteLine($"  CLEAR mapping table on {target}.{property}");
        }

        foreach (var (target, property, entrySpec) in options.Maps)
        {
            Console.Error.WriteLine($"  map entry on {target}.{property}: {entrySpec}");
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

    private static int RunLibrary(IOpennessGateway gateway, LibraryOptions options, int timeoutOpenSeconds)
    {
        gateway.OpenProject(options.ProjectIdentifier, TimeSpan.FromSeconds(timeoutOpenSeconds));

        if (options.ProbeDocumentsTypeName is { } probeType && options.OutDirectory is { } probeOut)
        {
            foreach (var line in gateway.ProbeExportAsDocuments(probeType, probeOut))
            {
                Console.WriteLine(line);
            }

            // A probe reports; it does not pass or fail. Its whole output is the finding.
            return ExitCodes.Success;
        }

        if (options.ExportTypeName is { } exportTypeName && options.OutDirectory is { } outDirectory)
        {
            var export = gateway.ExportLibraryTypeVersion(exportTypeName, options.ExportVersion, outDirectory);
            Console.WriteLine(OutputFormatter.FormatLibraryExport(export));

            // An export that produced nothing is a FAILED export, not a quiet success — the whole
            // point of the call is whether a document exists.
            return export.ProducedPaths.Count > 0 ? ExitCodes.Success : ExitCodes.CommandError;
        }

        var inventory = gateway.InventoryLibrary(options.IncludeMasterCopies);
        Console.WriteLine(options.Json
            ? OutputFormatter.FormatLibraryJson(inventory)
            : OutputFormatter.FormatLibraryReport(inventory, options.IncludeMasterCopies));
        return ExitCodes.Success;
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
        var result = gateway.EditHmiScreen(
            options.ScreenName, options.Sets, options.Events, options.Binds, options.Deletes,
            options.AddItems, options.BindKinds, options.MapClears, options.Maps);
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

    // FI-70. Produces the thing there was never anything to compare against: a directory holding the
    // controller's own copy of every block and PLC data type, for
    // `converter drift-check --project <ir-dir> --exports <dir> --complete`.
    //
    // Two properties this command must have, both because of what the directory is FOR:
    //   - a refusal is REPORTED, never a silent omission. The completeness check reads a missing file
    //     as "this block is not in the controller", so quietly skipping a safety block would turn a
    //     correct refusal into a false finding about the controller.
    //   - one failure does not abort the rest. A partial dump that names its own holes is useful; a
    //     dump that stopped at the first problem tells you nothing about the other 90 blocks.
    private static int RunExportAll(IOpennessGateway gateway, ExportAllCommandOptions options, int timeoutOpenSeconds)
    {
        gateway.OpenProject(options.ProjectIdentifier, TimeSpan.FromSeconds(timeoutOpenSeconds));
        Directory.CreateDirectory(options.OutDir);

        var plan = ExportAllPlanner.Build(
            gateway.EnumerateBlocks(),
            gateway.EnumerateTypes(),
            options.IncludeTagTables ? gateway.EnumerateTagTables() : Array.Empty<Model.TagTableInfo>(),
            options.OutDir,
            options.IncludeTagTables);

        var entries = new List<Model.ExportAllEntry>();
        foreach (var item in plan.Items)
        {
            if (!item.WillExport)
            {
                entries.Add(new Model.ExportAllEntry(
                    item.Name, item.Kind.ToString(), item.Path, OutPath: null,
                    Model.ExportAllOutcome.Refused, item.RefusedReason));
                continue;
            }

            try
            {
                switch (item.Kind)
                {
                    case ExportAllPlanner.ItemKind.Block:
                        gateway.ExportBlock(item.Name, options.Device, item.OutPath!);
                        break;
                    case ExportAllPlanner.ItemKind.Type:
                        gateway.ExportType(item.Name, options.Device, item.OutPath!);
                        break;
                    default:
                        gateway.ExportTagTable(item.Name, options.Device, item.OutPath!);
                        break;
                }

                entries.Add(new Model.ExportAllEntry(
                    item.Name, item.Kind.ToString(), item.Path, item.OutPath,
                    Model.ExportAllOutcome.Exported, Detail: null));
            }
            catch (Exception ex)
            {
                // Recorded and carried on: see the header. The exception type is included because
                // "which of 90 blocks failed and why" is the whole value of the report.
                entries.Add(new Model.ExportAllEntry(
                    item.Name, item.Kind.ToString(), item.Path, item.OutPath,
                    Model.ExportAllOutcome.Failed, $"{ex.GetType().Name}: {ex.Message}"));
            }
        }

        var result = new Model.ExportAllResult(options.OutDir, entries);
        Console.WriteLine(options.Json
            ? OutputFormatter.FormatExportAllJson(result)
            : OutputFormatter.FormatExportAllTable(result));

        if (result.FailedCount > 0)
        {
            return ExitCodes.CommandError;
        }

        return result.IsComplete ? ExitCodes.Success : ExitCodes.ExportIncomplete;
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

    private static int RunImportAllDryRun(ImportAllCommandOptions options)
    {
        var plan = ImportAllPlanner.Build(options.Paths);
        Console.WriteLine(options.Json
            ? OutputFormatter.FormatImportAllPlanJson(plan, options.GroupPath)
            : OutputFormatter.FormatImportAllPlanTable(plan, options.GroupPath));
        Console.Error.WriteLine("Nothing was imported, and Portal was not contacted. Re-run without --dry-run to proceed.");
        return plan.IsUsable ? ExitCodes.NotConfirmed : ExitCodes.ImportIncomplete;
    }

    /// <summary>
    /// Bulk import with fixpoint retry. The order a whole program has to go in is a dependency
    /// order — a DB needs its UDT, an instance DB needs its FB, a UDT can need another UDT — and it
    /// is not derivable from the filenames. Rather than compute that graph (and be wrong at the
    /// edges), this attempts everything, keeps the failures, and goes round again: any order that
    /// CAN work converges, because every pass that imports at least one file unblocks strictly more
    /// than the last. It stops when a pass imports nothing, at which point the remaining failures
    /// are real ones and their last error is the honest one to report.
    /// </summary>
    private static int RunImportAll(IOpennessGateway gateway, ImportAllCommandOptions options, int timeoutOpenSeconds)
    {
        var plan = ImportAllPlanner.Build(options.Paths);
        var entries = new List<Model.ImportAllEntry>();

        foreach (var rejection in plan.Rejections)
        {
            entries.Add(new Model.ImportAllEntry(
                rejection.Path, System.IO.Path.GetFileNameWithoutExtension(rejection.Path), "?",
                Model.ImportAllOutcome.Rejected, Pass: 0, rejection.Reason));
        }

        gateway.OpenProject(options.ProjectIdentifier, TimeSpan.FromSeconds(timeoutOpenSeconds));

        var passes = 0;
        try
        {
            // Phases are separated rather than thrown into one pool because the dependency across
            // them is one-directional and known (a block may use a UDT; a UDT never uses a block).
            // Retrying a block against a type that has not been imported yet would still converge,
            // but it would spend a pass over the whole corpus to learn what the ordering already
            // knows.
            foreach (var phase in plan.Files.Select(f => f.Phase).Distinct().OrderBy(p => p))
            {
                var remaining = plan.Files.Where(f => f.Phase == phase).ToList();
                var lastError = new Dictionary<string, string>(StringComparer.Ordinal);

                while (remaining.Count > 0)
                {
                    passes++;
                    var importedThisPass = new List<ImportAllPlanner.PlannedFile>();

                    foreach (var file in remaining)
                    {
                        try
                        {
                            switch (file.Kind)
                            {
                                case ImportAllPlanner.ItemKind.TagTable:
                                    gateway.ImportTagTableFile(options.GroupPath, file.Path);
                                    break;
                                case ImportAllPlanner.ItemKind.Type:
                                    gateway.ImportTypeFile(options.GroupPath, file.Path);
                                    break;
                                default:
                                    gateway.ImportBlockFile(options.GroupPath, file.Path);
                                    break;
                            }

                            importedThisPass.Add(file);
                            entries.Add(new Model.ImportAllEntry(
                                file.Path, file.Name, file.Kind.ToString(),
                                Model.ImportAllOutcome.Imported, passes, Detail: null));
                        }
                        catch (SafetyContentRefusedException)
                        {
                            // Never retried, never downgraded to a per-file failure line: safety
                            // content must stop the command (hard rule 2), not be reported at the
                            // bottom of a mostly-successful summary.
                            throw;
                        }
                        catch (Exception ex)
                        {
                            lastError[file.Path] = $"{ex.GetType().Name}: {ex.Message}";
                        }
                    }

                    if (importedThisPass.Count == 0)
                    {
                        // A pass that imported nothing cannot be improved on by another identical
                        // pass — whatever is left is blocked on something that is not in this set.
                        foreach (var file in remaining)
                        {
                            entries.Add(new Model.ImportAllEntry(
                                file.Path, file.Name, file.Kind.ToString(),
                                Model.ImportAllOutcome.Failed, passes,
                                lastError.TryGetValue(file.Path, out var err) ? err : "failed with no error recorded"));
                        }

                        break;
                    }

                    remaining = remaining.Where(f => !importedThisPass.Contains(f)).ToList();
                }
            }
        }
        finally
        {
            // One save for the whole run — the reason the per-file gateway calls do not save. In a
            // finally so a mid-run abort still keeps what went in, which is what the per-call save
            // it replaces was there to guarantee.
            gateway.Save();
        }

        var result = new Model.ImportAllResult(options.GroupPath, entries, passes);
        Console.WriteLine(options.Json
            ? OutputFormatter.FormatImportAllJson(result)
            : OutputFormatter.FormatImportAllTable(result));

        // Deliberately NOT a compile gate, and it does not pretend to be one: every block that goes
        // in through Import() is flagged inconsistent, and clearing that is `sanity-check`'s job
        // (hard rule 4, FI-52). This exit code answers one question — did every file land.
        return result.IsComplete ? ExitCodes.Success : ExitCodes.ImportIncomplete;
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
    /// FI-70, and deliberately the same shape as <see cref="CompileIncomplete"/>. Every export that
    /// was attempted succeeded, but the directory is NOT a complete picture of the project — some
    /// content was refused (safety, or a basename collision). Distinct from
    /// <see cref="CommandError"/>: nothing went wrong, the dump is simply not whole.
    ///
    /// It has its own code because of what the directory is FOR. `converter drift-check --complete`
    /// reads a missing file as "this block is not in the controller", so comparing against a partial
    /// dump manufactures findings about the controller that are really findings about the dump. A
    /// caller has to be able to tell the two apart without parsing the report.
    /// </summary>
    public const int ExportIncomplete = 12;

    /// <summary>
    /// Same shape as <see cref="ExportIncomplete"/>, one step earlier in the loop: `import-all` ran,
    /// but the project does not now contain everything that was handed to it. Distinct from
    /// <see cref="CommandError"/> because the usual cause is not an error at all — a file that never
    /// resolved its dependencies, or one that was never attempted because it could not be classified.
    ///
    /// It has its own code because of what a restore is FOR. A project that is missing a block looks
    /// exactly like a project that is not: it opens, it lists, and a device compile can even pass on
    /// it. The caller has to be able to tell "everything went in" from "most of it did" without
    /// reading the report.
    /// </summary>
    public const int ImportIncomplete = 13;

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

        // The nested-target and mapping-table family, added 2026-08-09 with the --map work. Same
        // class as everything above: a path or an entry type the caller can correct.
        HmiTargetPathNotResolvableException => CommandError,
        HmiMappingTableNotAvailableException => CommandError,
        HmiUnknownMappingEntryTypeException => CommandError,

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

        // The Openness approval refusal, added 2026-08-09. Measured: a freshly-rebuilt `library`
        // binary exited 5 with "AggregateException ... ---> EngineeringSecurityException: Security
        // error." and a full inner-exception dump. That is not an internal fault — it is the single
        // most routine environmental condition this tool has, because TIA's whitelist keys on the
        // CLIENT BINARY'S HASH, so EVERY REBUILD needs a fresh human approval. Reporting the most
        // predictable consequence of editing this program as an unexpected error is exactly the
        // defect audit F-09 fixed for the naming families above.
        // It arrives wrapped (Task machinery), so the chain has to be walked rather than matched.
        _ when FindInChain<EngineeringSecurityException>(ex) is not null => EnvironmentError,

        _ => UnexpectedError,
    };

    /// <summary>
    /// Walks an exception chain — inner exceptions and every branch of an
    /// <see cref="AggregateException"/> — for the first exception of type <typeparamref name="T"/>.
    /// The connect path runs through <c>Task</c>, so the interesting exception is never the top one.
    /// </summary>
    public static T? FindInChain<T>(Exception? ex) where T : Exception
    {
        while (ex is not null)
        {
            if (ex is T match)
            {
                return match;
            }

            if (ex is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions)
                {
                    if (FindInChain<T>(inner) is { } found)
                    {
                        return found;
                    }
                }

                return null;
            }

            ex = ex.InnerException;
        }

        return null;
    }

    /// <summary>
    /// The actionable message for an Openness approval refusal. `EngineeringSecurityException`'s own
    /// message is the two-word "Security error.", which tells the reader nothing and — because the
    /// cause is a rebuild rather than anything they typed — is very easily misread as contention or
    /// as a broken install.
    /// </summary>
    public static string? DescribeSecurityRefusal(Exception ex) =>
        FindInChain<EngineeringSecurityException>(ex) is null
            ? null
            : "Openness refused this client: EngineeringSecurityException (\"Security error\").\n" +
              "  This is almost always the APPROVAL WHITELIST, not a broken install and not contention.\n" +
              "  TIA keys the whitelist on the CLIENT BINARY'S HASH, so every rebuild of openness-cli is\n" +
              "  a client TIA has never seen and needs a one-time human approval:\n" +
              "    1. Open TIA Portal.\n" +
              "    2. Re-run this command; a dialog titled 'Openness access' appears inside Portal.\n" +
              "    3. Click Yes. The approval then persists for THIS build only.\n" +
              "  Also confirm Windows group membership of 'Siemens TIA Openness'.\n" +
              "  See docs/notes/openness-quirks.md ('the approval whitelist keys on the binary's HASH').";
}
