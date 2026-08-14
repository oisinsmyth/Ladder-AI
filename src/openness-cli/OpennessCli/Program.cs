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
    private static int Main(string[] args) => Run(args, () => new OpennessGateway());

    /// <summary>
    /// The whole program, with the Portal half injected — the same seam <c>download-probe</c>'s
    /// <c>Program.Run</c> uses, and for the same reason: <b>everything that decides WHETHER Portal is
    /// contacted sits before <see cref="IOpennessGateway.Connect"/>, and none of it was reachable
    /// from a test.</b>
    ///
    /// 🔴 <b>Why this exists (2026-08-14).</b> The pre-Connect refusals below — the unconfirmed HMI
    /// writes, the <c>import-all</c> dry run, and above all <c>block-layout --set</c> without
    /// <c>--yes</c> — were each tested by calling their refusal helper DIRECTLY. That tests the
    /// MESSAGE, not the ROUTING. Measured: commenting out the <c>block-layout</c> arm left all 712
    /// tests green, and <see cref="RunBlockLayout"/> does not re-check <c>Confirm</c>, so that one
    /// <c>if</c> is the only thing between a missing <c>--yes</c> and a write that destroys the
    /// block's retained data. A guard that can be silently disconnected is decoration.
    ///
    /// <paramref name="gatewayFactory"/> is injected ONLY for that: a test supplies a gateway whose
    /// <c>Connect</c> records that it was called, so "Portal was not contacted" is OBSERVED rather
    /// than inferred from an exit code. <see cref="Main"/> passes the real one and there is no
    /// argument, flag or environment variable that reaches this parameter.
    /// </summary>
    internal static int Run(string[] args, Func<IOpennessGateway> gatewayFactory)
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

        using IOpennessGateway gateway = gatewayFactory();
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

            // Same seam, same reason: a `block-layout --set` without `--yes` is answered from the
            // arguments alone. It is here rather than in RunBlockLayout so that the refusal cannot
            // be reached AFTER a Portal session exists — the read path and the write path of this
            // command share a subcommand name, and the write is destructive.
            if (parseResult is ParseResult.BlockLayoutSuccess { Options: { Set: not null, Confirm: false } } unconfirmedLayout)
            {
                return RefuseUnconfirmedBlockLayout(unconfirmedLayout.Options);
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
                case ParseResult.CompileAllSuccess compileAll:
                    return RunCompileAll(gateway, compileAll.Options, timeoutOpenSeconds);
                case ParseResult.DeleteSuccess delete:
                    return RunDelete(gateway, delete.Options, timeoutOpenSeconds);
                case ParseResult.BlockLayoutSuccess blockLayout:
                    return RunBlockLayout(gateway, blockLayout.Options, timeoutOpenSeconds);
                case ParseResult.DownloadPlanSuccess downloadPlan:
                    return RunDownloadPlan(gateway, downloadPlan.Options, timeoutOpenSeconds);
                case ParseResult.CreateInstanceDbSuccess createInstanceDb:
                    return RunCreateInstanceDb(gateway, createInstanceDb.Options, timeoutOpenSeconds);
                case ParseResult.SanityCheckSuccess sanityCheck:
                    return RunSanityCheck(gateway, sanityCheck.Options, timeoutOpenSeconds);
                case ParseResult.CompileScopesSuccess compileScopes:
                    return RunCompileScopes(gateway, compileScopes.Options, timeoutOpenSeconds);
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
    // consistency flag to cross-examine a green result with. Confirmed 2026-08-12 against the V20
    // API surface: `IsConsistent` exists on exactly four types, all of them PLC-side
    // (PlcBlock, PlcType, PlcForceTable, PlcWatchTable), and on nothing in Siemens.Engineering.Hmi.*
    // or Siemens.Engineering.HmiUnified.*. So an HMI compile reporting Success means only that the
    // compiler said so, and nothing here can strengthen it — an asymmetry with `compile` that is a
    // fact about the API, not an omission to be filled in with an always-null field.
    internal static int RunHmiCompile(IOpennessGateway gateway, CompileCommandOptions options, int timeoutOpenSeconds)
    {
        gateway.OpenProject(options.ProjectIdentifier, TimeSpan.FromSeconds(timeoutOpenSeconds));
        var result = gateway.CompileHmi(options.Device);
        Console.WriteLine(options.Json ? OutputFormatter.FormatCompileJson(result) : OutputFormatter.FormatCompileTable(result));

        // THE VERDICT KEYS ON ERRORS, NEVER ON State — the same rule as `compile` (see RunCompile),
        // applied here 2026-08-12. `compile` earned it the expensive way: on a project carrying a
        // permanent hardware warning, EVERY clean per-block compile returned a non-Success State and
        // so exited 8 with `errors: 0`, and a caller branching on the exit code read every success as
        // a failure. `hmi-compile` shares the same Compile()/CompileResult machinery and had the same
        // defect; it was left alone at the time only because no HMI project had yet demonstrated it.
        // Leaving a known defect because it has not bitten yet is how it bites later.
        //
        // State and the warning count are still REPORTED — "compiled with warnings" and "compiled
        // clean" are different facts. Only the exit code changed.
        if (EffectiveErrorCount(result) > 0)
        {
            return ExitCodes.CompileFailed;
        }

        if (result.State != Model.CompileState.Success)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"PASSED WITH WARNINGS: 0 errors, so this is a pass. State is {result.State}, which is reported " +
                "and not decisive — measured on the PLC side, one pre-existing project-wide warning returns a " +
                "non-Success state on every otherwise-clean compile. Read the messages above before treating the " +
                "warnings as noise.");
            Console.WriteLine(
                "There is no consistency read-back to fall back on here: `IsConsistent` is PLC-only, so an HMI " +
                "compile's verdict rests entirely on what the compiler reported.");
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

    internal static int RunImport(IOpennessGateway gateway, ImportCommandOptions options, int timeoutOpenSeconds)
    {
        gateway.OpenProject(options.ProjectIdentifier, TimeSpan.FromSeconds(timeoutOpenSeconds));
        if (options.AsType)
        {
            var importedTypes = gateway.ImportTypes(options.GroupPath, options.Files);
            foreach (var name in importedTypes)
            {
                Console.WriteLine(name);
            }

            // Types and tag tables carry no number, so there is nothing here to collide. Returning
            // early rather than running an EnumerateBlocks that could not find anything this command
            // caused: a check that cannot fail on this path would only be there to look thorough.
            return ExitCodes.Success;
        }

        if (options.AsTagTable)
        {
            var importedTagTables = gateway.ImportTagTables(options.GroupPath, options.Files);
            foreach (var name in importedTagTables)
            {
                Console.WriteLine(name);
            }

            return ExitCodes.Success;
        }

        var imported = gateway.ImportBlocks(options.GroupPath, options.Files);
        Console.WriteLine(OutputFormatter.FormatTable(imported));
        return ReportDuplicateBlockNumbersAfterWrite(
            gateway, imported.Select(b => b.Name).ToList(), "imported and SAVED");
    }

    /// <summary>
    /// The post-write duplicate-number scan (2026-08-13).
    ///
    /// <b>Why this is a post-condition and not a refusal.</b> Refusing before the write would mean
    /// deciding from the FILE that its number is already taken, and that check is wrong in the common
    /// case: the overwhelmingly normal thing `import` does is put back a block that is already in the
    /// project, whose number is therefore already held — by itself. A guard that fires on the
    /// commonest correct operation is a guard people learn to route around, and then it protects
    /// nothing. The honest question is not "is this number taken" but "does the project now hold two
    /// blocks at one number", and only the project can answer it, only afterwards.
    ///
    /// <b>Why it does not roll back.</b> `import` has no delete-what-I-just-wrote path, and adding
    /// one on a failure path is precisely the seam FI-63 closed when the renumber repair was retired
    /// — mutating further to rescue a mutation that already went wrong. The collision may also be
    /// older than this command. So it reports, exactly, and stops.
    ///
    /// <b>Why it is nevertheless a non-zero exit and not a warning.</b> The measured chain was import
    /// exit 0 -> compile exit 0 -> sanity-check exit 0, and a warning inside a green chain is a
    /// warning that gets skimmed. The output states plainly that the files DID go in, so the non-zero
    /// code can never be read as "nothing was imported".
    /// </summary>
    private static int ReportDuplicateBlockNumbersAfterWrite(
        IOpennessGateway gateway, IReadOnlyList<string> justWritten, string whatHappened)
    {
        var duplicates = Model.DuplicateBlockNumberFinder.Find(gateway.EnumerateBlocks());
        if (duplicates.Count == 0)
        {
            return ExitCodes.Success;
        }

        Console.Error.WriteLine();
        Console.Error.WriteLine(
            $"The files above were {whatHappened} — this exit code is NOT a failed import. What it reports is " +
            "that the project now holds more than one block at the same number, which TIA accepts silently and " +
            "no compile or consistency check will ever mention:");
        Console.Error.WriteLine();
        Console.Error.Write(OutputFormatter.FormatDuplicateBlockNumbers(duplicates, justWritten));
        return ExitCodes.DuplicateBlockNumber;
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
    internal static int RunImportAll(IOpennessGateway gateway, ImportAllCommandOptions options, int timeoutOpenSeconds)
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

                // Counted WITHIN the phase, not across the run. A global counter made every type
                // report "imported on pass 2" and every block "pass 3" purely because they were in
                // the second and third phases — 98 files claiming to have been retried when not one
                // of them had failed even once. A retry is worth reporting precisely because it is
                // unusual; a label that fires for almost everything reports nothing, and would hide
                // the real retry it exists to show. Measured on a live 101-file restore.
                var phasePass = 0;

                while (remaining.Count > 0)
                {
                    passes++;
                    phasePass++;
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
                                Model.ImportAllOutcome.Imported, phasePass, Detail: null));
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
                                Model.ImportAllOutcome.Failed, phasePass,
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

        // Same post-write scan the single-file path runs, for the same reason, and ranked above
        // ImportIncomplete: 13's remedy is to work out why a file did not land and import again,
        // which on a duplicate adds a third block rather than fixing anything. A restore is exactly
        // where this bites — the dump can legitimately contain two files claiming one number, and
        // nothing between here and the controller would ever say so.
        var duplicateVerdict = ReportDuplicateBlockNumbersAfterWrite(
            gateway,
            entries.Where(e => e.Outcome == Model.ImportAllOutcome.Imported).Select(e => e.Name).ToList(),
            "imported and SAVED");
        if (duplicateVerdict != ExitCodes.Success)
        {
            return duplicateVerdict;
        }

        // Deliberately NOT a compile gate, and it does not pretend to be one: every block that goes
        // in through Import() is flagged inconsistent, and clearing that is `sanity-check`'s job
        // (hard rule 4, FI-52). This exit code answers one question — did every file land.
        return result.IsComplete ? ExitCodes.Success : ExitCodes.ImportIncomplete;
    }

    /// <summary>
    /// Compiles every item the project reports as inconsistent, types first, in one session.
    ///
    /// This exists because of FI-52's other half. A whole-device compile does not clear the
    /// inconsistent flag a freshly-imported block carries, so the only thing that clears it is a
    /// per-block or per-type compile — and after a bulk restore that is ninety-odd of them. Run as
    /// separate CLI invocations that is ninety-odd Portal attaches; run here it is one.
    ///
    /// Types before blocks, then a re-read and a retry while progress is being made — the same
    /// fixpoint argument as `import-all`, for the same reason: compiling one item can clear another,
    /// and the order in which that happens is not worth deriving when a second pass settles it.
    /// </summary>
    private static int RunCompileAll(IOpennessGateway gateway, CompileAllCommandOptions options, int timeoutOpenSeconds)
    {
        gateway.OpenProject(options.ProjectIdentifier, TimeSpan.FromSeconds(timeoutOpenSeconds));

        // Keyed by name so an item compiled on more than one pass reports its LAST outcome. A first
        // pass that failed on "callee has not been compiled" and a second that succeeded are one
        // item that is fine, not two lines of which one is alarming.
        var entries = new Dictionary<string, Model.CompileAllEntry>(StringComparer.Ordinal);
        var passes = 0;

        try
        {
            var previousOutstanding = int.MaxValue;

            while (true)
            {
                var sanity = gateway.RunSanityCheck();

                // Inconsistency is not the only thing a further pass can fix. `Block "X" that is
                // accessed has not been compiled` is an ORDERING artefact — the caller was compiled
                // before its callee — and it clears on a re-run now that the callee is done.
                // Measured on the first live whole-program restore: 15 items reported errors of that
                // shape while every one of them was already flagged consistent, so a loop that
                // watched only consistency stopped one pass short of clean and reported 15 failures
                // that were not failures. Errors are part of the work set for the same reason
                // import-all retries: the dependency order is not worth deriving when a pass settles it.
                var work = new List<(string Name, string Kind)>();
                if (options.Force && passes == 0)
                {
                    // --force exists because the default work set cannot see the one thing a restore
                    // most needs re-checked. An item that compiled WITH ERRORS is still flagged
                    // CONSISTENT, so on the next invocation it is invisible: the run examines
                    // nothing and reports clean. Errors do not survive the process; consistency does.
                    work.AddRange(gateway.EnumerateTypes().Select(t => (t.Name, "Type")));
                    work.AddRange(gateway.EnumerateBlocks().Where(b => !b.IsSafety).Select(b => (b.Name, "Block")));
                }
                else
                {
                    work.AddRange(sanity.InconsistentTypes.Select(t => (t.Name, "Type")));
                    work.AddRange(sanity.InconsistentBlocks.Select(b => (b.Name, "Block")));
                }

                var alreadyQueued = new HashSet<string>(work.Select(w => w.Name), StringComparer.Ordinal);
                foreach (var errored in entries.Values.Where(e => e.ErrorCount > 0))
                {
                    if (alreadyQueued.Add(errored.Name))
                    {
                        work.Add((errored.Name, errored.Kind));
                    }
                }

                if (work.Count == 0)
                {
                    break;
                }

                // No progress since the last pass means another identical pass cannot help: what is
                // left is blocked on something compiling cannot fix.
                if (work.Count >= previousOutstanding)
                {
                    foreach (var (name, kind) in work)
                    {
                        if (!entries.ContainsKey(name))
                        {
                            entries[name] = new Model.CompileAllEntry(name, kind, Model.CompileState.Error, 0, 0,
                                StillInconsistent: true, "still inconsistent after a pass that cleared nothing");
                        }
                    }

                    break;
                }

                previousOutstanding = work.Count;
                passes++;

                // Types first, then data blocks, then everything else. A UDT is a dependency of the
                // DBs shaped by it, and a DB is a dependency of the logic that accesses it, so this
                // is callee-before-caller for the two cases that are knowable from what sanity-check
                // reports. It only saves passes — the loop above is what makes the result correct.
                foreach (var (name, kind) in work.OrderBy(w => w.Kind == "Type" ? 0 : IsDataBlock(sanity, w.Name) ? 1 : 2))
                {
                    entries[name] = kind == "Type"
                        ? CompileOne(() => gateway.CompileType(name, options.Device), name, kind)
                        : CompileOne(() => gateway.CompileBlock(name, options.Device), name, kind);
                }
            }

            // Re-read once at the end so StillInconsistent reflects the final state rather than the
            // state at the moment each item was compiled.
            var final = gateway.RunSanityCheck();
            var stillOut = new HashSet<string>(
                final.InconsistentTypes.Select(t => t.Name).Concat(final.InconsistentBlocks.Select(b => b.Name)),
                StringComparer.Ordinal);

            foreach (var name in entries.Keys.ToList())
            {
                entries[name] = entries[name] with { StillInconsistent = stillOut.Contains(name) };
            }
        }
        finally
        {
            gateway.Save();
        }

        var result = new Model.CompileAllResult(entries.Values.OrderBy(e => e.Name, StringComparer.Ordinal).ToList(), passes);
        Console.WriteLine(options.Json
            ? OutputFormatter.FormatCompileAllJson(result)
            : OutputFormatter.FormatCompileAllTable(result));

        if (result.WithErrorsCount > 0)
        {
            return ExitCodes.CompileFailed;
        }

        if (result.StillInconsistentCount > 0)
        {
            return ExitCodes.CompileIncomplete;
        }

        // Examining nothing is not passing. Without this the command prints "every item compiled
        // without errors" after compiling zero items — measured, on a project where the previous run
        // had left 15 items compiling with errors and every one of them flagged consistent. Nothing
        // was wrong with the project state it read; the report simply described a pass it had not
        // earned. `--force` is the answer when you need the question actually asked.
        return result.CompiledCount == 0 ? ExitCodes.NothingExamined : ExitCodes.Success;
    }

    // sanity-check reports a block's LANGUAGE, and a data block's is the literal "DB" — the only
    // handle available here for "this is a callee, compile it first". Ordering only; being wrong
    // costs a pass.
    private static bool IsDataBlock(Model.SanityCheckResult sanity, string name) =>
        sanity.InconsistentBlocks.Any(b =>
            string.Equals(b.Name, name, StringComparison.Ordinal) &&
            string.Equals(b.Language, "DB", StringComparison.OrdinalIgnoreCase));

    private static Model.CompileAllEntry CompileOne(Func<Model.CompileResult> compile, string name, string kind)
    {
        try
        {
            var r = compile();
            return new Model.CompileAllEntry(name, kind, r.State, r.ErrorCount, r.WarningCount, StillInconsistent: false,
                r.ErrorCount > 0
                    ? string.Join("; ", r.Messages.Where(m => !string.IsNullOrWhiteSpace(m.Description)).Select(m => m.Description).Take(3))
                    : null);
        }
        catch (Exception ex)
        {
            // Recorded and carried on, like export-all: "which of ninety items threw, and why" is
            // the whole value of the report, and one throw must not end the run.
            return new Model.CompileAllEntry(name, kind, Model.CompileState.Error, 1, 0, StillInconsistent: true,
                $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// The number of errors a compile result is judged on. Fail-closed: the compiler's own
    /// <c>ErrorCount</c> OR the count of Error messages in its message tree, whichever is larger.
    ///
    /// Two counts exist because they can disagree — <see cref="OutputFormatter.FormatCompileTable"/>
    /// already prints a NOTE when they do, calling the aggregates unreliable. A verdict that trusted
    /// only one of them would be talked out of failing by the other.
    /// </summary>
    internal static int EffectiveErrorCount(Model.CompileResult result) =>
        Math.Max(result.ErrorCount, result.Messages.Count(m => m.State == Model.CompileState.Error));

    internal static int RunCompile(IOpennessGateway gateway, CompileCommandOptions options, int timeoutOpenSeconds)
    {
        gateway.OpenProject(options.ProjectIdentifier, TimeSpan.FromSeconds(timeoutOpenSeconds));
        // *** THE DEFAULT IS THE STATION SCOPE (changed 2026-08-13). ***
        //
        // It used to be the DeviceItem scope, which compiles the HARDWARE and nothing else —
        // measured: its entire message tree is `Hardware configuration`, and it reported
        // `Success, errors=0` on a project whose FC8 failed with `Tag "DB_Example".DataStore not
        // defined`. Every caller in this repository invokes a bare `compile` INTENDING a program
        // check (the lad-coder agent, the gen-block-new skill, docs 03/05/08/11/15, hard rule 4),
        // and not one of them wanted the hardware-only compile they were getting.
        //
        // Station rather than software because it is a SUPERSET of the old behaviour: it still runs
        // the hardware compile, so nothing that depended on that half loses it, and it is the
        // nearest equivalent of TIA's `Compile → Hardware and software`. The old scope stays
        // reachable, by name, as `--hardware` — a default that cannot be asked for by its own name
        // is a behaviour nobody can reason about.
        var result = (options.Block, options.Type, options.Software, options.Station, options.Hardware) switch
        {
            (null, null, false, false, false) => gateway.CompileStation(options.Device),
            (null, null, false, true, false) => gateway.CompileStation(options.Device),
            (null, null, true, false, false) => gateway.CompileSoftware(options.Device),
            (null, null, false, false, true) => gateway.Compile(options.Device),
            (not null, null, false, false, false) => gateway.CompileBlock(options.Block, options.Device),
            (null, not null, false, false, false) => gateway.CompileType(options.Type, options.Device),
            _ => throw new InvalidOperationException("--block, --type, --software, --station and --hardware are mutually exclusive."),
        };
        Console.WriteLine(options.Json ? OutputFormatter.FormatCompileJson(result) : OutputFormatter.FormatCompileTable(result));

        // THE VERDICT KEYS ON ERRORS, NEVER ON State (2026-08-12) — the same rule `compile-all` has
        // had since it was written, arrived at here the expensive way. Measured live: EVERY per-block
        // compile against the scratch project exited 8 with `errors: 0`, because the project carries a
        // permanent hardware warning ("Inputs or outputs are used that do not exist in the configured
        // hardware") and Compile() returns a non-Success STATE for it — on every block, regardless of
        // the block. A caller branching on the exit code reads every clean compile as a failure, and
        // would reasonably stop.
        //
        // State is still REPORTED, and so is the warning count: "compiled with warnings" and "compiled
        // clean" are different facts and the output must keep saying which. What changed is only which
        // of them decides the exit code.
        var errorCount = EffectiveErrorCount(result);
        if (errorCount > 0)
        {
            return ExitCodes.CompileFailed;
        }

        if (result.State != Model.CompileState.Success)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"PASSED WITH WARNINGS: 0 errors, so this is a pass. State is {result.State}, which is reported " +
                "and not decisive — a pre-existing hardware warning anywhere in the project returns a " +
                "non-Success state on a perfectly clean block. Read the messages above before treating the " +
                "warnings as noise.");
        }

        // The CONVERSE of FI-52 (measured 2026-08-12). A PER-BLOCK compile can report "Block was
        // successfully compiled" with errors: 0 and leave ITS OWN block flagged IsConsistent=false —
        // seen when a block it referenced did not exist. TIA then refuses to export it, and until now
        // only `sanity-check` could tell you why. So a per-item compile re-resolves the item and reads
        // the flag back (OpennessGateway.CompileBlock/CompileType), and this gates on it: a block that
        // cannot be exported must never leave a clean-looking exit behind.
        if (options.Block is not null || options.Type is not null)
        {
            var what = options.Block is not null ? $"Block '{options.Block}'" : $"Type '{options.Type}'";
            if (result.ConsistentAfterCompile == false)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine(
                    $"COMPILE INCOMPLETE: {what} compiled without errors and is STILL flagged " +
                    "IsConsistent=false. TIA will REFUSE to export it (\"Inconsistent blocks and PLC data " +
                    "types (UDT) cannot be exported\"), so this compile is not the gate it looks like.");
                Console.Error.WriteLine(
                    "The usual cause is a block or type it references that does not exist yet, or has not " +
                    "itself been compiled — compile the callees first, then this. `openness-cli sanity-check` " +
                    "lists everything in that state; `compile-all` compiles them in one session.");
                return ExitCodes.CompileIncomplete;
            }

            if (result.ConsistentAfterCompile is null)
            {
                // Empty is not clean (FI-44). The compile ran, and the one question that would have
                // made it a gate went unanswered — that is not a pass.
                Console.Error.WriteLine();
                Console.Error.WriteLine(
                    $"COMPILE INCOMPLETE: {what} compiled without errors, but its consistency flag could not " +
                    "be read back afterwards (the item did not re-resolve to exactly one match). Nothing here " +
                    "proves it is exportable. Run `openness-cli sanity-check` before treating this as a pass.");
                return ExitCodes.CompileIncomplete;
            }
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

    internal static int RefuseUnconfirmedBlockLayout(BlockLayoutCommandOptions options)
    {
        Console.Error.WriteLine(
            $"Would set the memory layout of block '{options.BlockName}' to {options.Set} in project " +
            $"'{options.ProjectIdentifier}'.");
        Console.Error.WriteLine(OutputFormatter.RetainedDataWarning);
        Console.Error.WriteLine(OutputFormatter.ReimportHazardWarning);
        Console.Error.WriteLine(
            "Nothing was changed, and Portal was not contacted. Re-run with --yes to proceed.");
        return ExitCodes.NotConfirmed;
    }

    /// <summary>
    /// `block-layout` — read, assert, or set a block's optimized/standard block access.
    ///
    /// <c>internal</c> rather than <c>private</c> so the read/set/verify decisions can be tested
    /// against a fake gateway with no Portal session. That is not incidental: the single most
    /// important property of this command is that a set whose read-back disagrees with the request
    /// FAILS, and there is no other way to observe that without a controller.
    ///
    /// The verification compares the read-back against THIS INVOCATION'S OWN requested value, not
    /// against the value the result reports having been asked for. The two are normally identical;
    /// gating on the caller's own value is what makes the check fail closed if they ever are not.
    /// </summary>
    internal static int RunBlockLayout(IOpennessGateway gateway, BlockLayoutCommandOptions options, int timeoutOpenSeconds)
    {
        gateway.OpenProject(options.ProjectIdentifier, TimeSpan.FromSeconds(timeoutOpenSeconds));

        if (options.Set is not { } requested)
        {
            var read = gateway.GetBlockMemoryLayout(options.BlockName, options.Device);
            Console.WriteLine(options.Json
                ? OutputFormatter.FormatBlockLayoutJson(read)
                : OutputFormatter.FormatBlockLayoutTable(read));

            // --expect turns the read into a gate. It exists because setting a layout is not known
            // to survive a re-import of the same block, and no other check in this toolchain can
            // see the attribute at all — the converter never emits it and Normalizer ignores it, so
            // drift-check is structurally blind to a change in either direction.
            if (options.Expect is { } expected && read.Layout != expected)
            {
                Console.Error.WriteLine(
                    $"LAYOUT MISMATCH: expected {expected}, but '{read.Name}' is {read.Layout}.");
                return ExitCodes.LayoutMismatch;
            }

            return ExitCodes.Success;
        }

        var result = gateway.SetBlockMemoryLayout(options.BlockName, options.Device, requested);
        Console.WriteLine(options.Json
            ? OutputFormatter.FormatBlockLayoutJson(result)
            : OutputFormatter.FormatBlockLayoutTable(result));

        if (result.Layout != requested)
        {
            Console.Error.WriteLine(
                $"SET NOT APPLIED: asked for {requested}, but '{result.Name}' read back as {result.Layout} after saving. " +
                "The block's layout is NOT what was requested.");
            return ExitCodes.LayoutMismatch;
        }

        return ExitCodes.Success;
    }

    /// <summary>
    /// `download-plan` — report what a download WOULD comprise. It cannot download.
    ///
    /// <c>internal</c> for the same reason <see cref="RunBlockLayout"/> is: the property that matters
    /// most here can only be observed against a fake gateway. That property is that NO PATH THROUGH
    /// THIS METHOD INVOKES A DOWNLOAD — the tests assert it by counting calls on the fake, across
    /// every option value, every output mode and every failure mode.
    ///
    /// Note what this method does NOT contain: any branch, flag or condition under which it would
    /// call <see cref="IOpennessGateway.PerformDownload"/>. That method exists only so the fake has
    /// something to count, and both implementations of it throw.
    /// </summary>
    internal static int RunDownloadPlan(IOpennessGateway gateway, DownloadPlanCommandOptions options, int timeoutOpenSeconds)
    {
        gateway.OpenProject(options.ProjectIdentifier, TimeSpan.FromSeconds(timeoutOpenSeconds));

        var plan = gateway.BuildDownloadPlan(options.Device, options.Options);
        Console.WriteLine(options.Json
            ? OutputFormatter.FormatDownloadPlanJson(plan)
            : OutputFormatter.FormatDownloadPlanTable(plan));

        // Empty is not clean (FI-44). A plan that could not obtain a DownloadProvider has answered
        // none of the questions it was asked — where a download would come from, what it would use,
        // whether it is even possible on this device — and exiting 0 would present that silence as a
        // clean bill of health. The report already says so on stdout; this is the half a script reads.
        if (!plan.Provider.Found)
        {
            Console.Error.WriteLine(
                "DOWNLOAD PLAN INCOMPLETE: no DownloadProvider could be obtained from any object in this " +
                "device's tree, so nothing in this report describes what a download would actually do. " +
                "The objects asked, and the services each advertises, are listed above.");
            return ExitCodes.DownloadPlanIncomplete;
        }

        return ExitCodes.Success;
    }

    /// <summary>
    /// `create-instance-db` — scaffold an instance DB for an already-existing FB.
    ///
    /// <c>internal</c> for the same reason <see cref="RunBlockLayout"/> and
    /// <see cref="RunDownloadPlan"/> are: the property that matters here is observable only against a
    /// fake gateway. That property is that a run which does NOT return a usable block leaves the
    /// project unchanged — asserted by the tests as "the fake's block list is back where it started
    /// and its save counter is still zero", not merely as an exit code. The exit code was never the
    /// bug; the committed `DB0` was.
    /// </summary>
    internal static int RunCreateInstanceDb(IOpennessGateway gateway, CreateInstanceDbCommandOptions options, int timeoutOpenSeconds)
    {
        gateway.OpenProject(options.ProjectIdentifier, TimeSpan.FromSeconds(timeoutOpenSeconds));
        var info = gateway.CreateInstanceDb(options.GroupPath, options.DbName, options.InstanceOfName);
        Console.WriteLine($"Created '{info.Name}' (DB{info.Number}, instance of '{options.InstanceOfName}') at {info.Path}.");
        return ExitCodes.Success;
    }

    internal static int RunSanityCheck(IOpennessGateway gateway, ListOptions options, int timeoutOpenSeconds)
    {
        gateway.OpenProject(options.ProjectIdentifier, TimeSpan.FromSeconds(timeoutOpenSeconds));
        var result = gateway.RunSanityCheck();
        Console.WriteLine(options.Json ? OutputFormatter.FormatSanityCheckJson(result) : OutputFormatter.FormatSanityCheckTable(result));

        // Checked BEFORE the health verdict, not folded into it. 9's documented remedy is "compile
        // the listed blocks", and on a duplicate that is the move which DESTROYS the only symptom
        // while leaving the defect — measured 2026-08-13. See ExitCodes.DuplicateBlockNumber.
        if (result.DuplicateNumbers.Count > 0)
        {
            return ExitCodes.DuplicateBlockNumber;
        }

        return result.IsHealthy ? ExitCodes.Success : ExitCodes.SanityCheckFailed;
    }

    /// <summary>
    /// Read-only. Reports every compile entry point and how many DISTINCT compilers they resolve to.
    ///
    /// Always exits 0: this is a description of the API surface, not a gate. Whether having more than
    /// one distinct compiler is a PROBLEM depends on what the extra one finds, and that question is
    /// answered by running it (`compile --software`), never by counting.
    /// </summary>
    internal static int RunCompileScopes(IOpennessGateway gateway, ListOptions options, int timeoutOpenSeconds)
    {
        gateway.OpenProject(options.ProjectIdentifier, TimeSpan.FromSeconds(timeoutOpenSeconds));
        var survey = gateway.SurveyCompileScopes(deviceFilter: null);
        Console.WriteLine(options.Json
            ? OutputFormatter.FormatCompileScopesJson(survey)
            : OutputFormatter.FormatCompileScopesTable(survey));
        return ExitCodes.Success;
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
    /// A compile reported no errors and something it should have verified is still unverified. Two
    /// ways to earn it, and they are the same fact from either end:
    ///
    /// 1. FI-52 — a WHOLE-DEVICE compile cleared no block's flag: other blocks remain inconsistent.
    /// 2. 2026-08-12, the converse — a PER-BLOCK/PER-TYPE compile left ITS OWN item inconsistent.
    ///    Measured: "Block was successfully compiled", errors 0, and the block still
    ///    <c>IsConsistent=false</c> because a block it referenced did not exist; TIA then refused to
    ///    export it. Also earned when the post-compile consistency read-back could not be performed at
    ///    all — an unanswered question is not a pass (FI-44, empty is not clean).
    ///
    /// Distinct from <see cref="CompileFailed"/> — nothing reported an error; the gate simply did not
    /// prove what it appears to have proved, which is the failure mode that matters most because it
    /// looks like a pass. One code rather than two because the caller's response is identical: do not
    /// treat this as the hard-rule-4 gate, and go and look at what was left unverified.
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
    /// The command ran, nothing went wrong, and it examined NOTHING — so its silence is not
    /// evidence about the project. `compile-all` earns this when no type or block is flagged
    /// inconsistent: there is nothing in its default work set, and a report saying "every item
    /// compiled without errors" after compiling zero items describes a pass it did not earn.
    ///
    /// It is its own code rather than a success because of how the gap arises. An item that compiled
    /// WITH ERRORS is still flagged CONSISTENT, and errors do not survive the process while
    /// consistency does — so the run immediately after a failed one is exactly the run that examines
    /// nothing and looks cleanest. Same reasoning as the converter's own "exit 2 = a check was
    /// compared against nothing" (FI-44): empty is not clean.
    /// </summary>
    public const int NothingExamined = 14;

    /// <summary>
    /// `block-layout`: the block's memory layout is NOT the one asked for. Two ways to earn it, and
    /// they are the same fact from either end — a `--set` whose read-back after saving disagrees
    /// with the request, or a `--expect` assertion that does not hold.
    ///
    /// Its own code rather than <see cref="CommandError"/> because nothing was named wrongly: the
    /// block resolved, the command ran, and the project's answer is not the one required. Re-running
    /// with a different argument does not fix it. And it must never be
    /// <see cref="Success"/>-with-a-note, because the failure it reports is invisible everywhere
    /// else — an optimized block is not an error to a classic-S7comm reader, it is simply absent,
    /// and no compile, drift-check or sanity-check in this toolchain can see the attribute at all.
    /// </summary>
    public const int LayoutMismatch = 15;

    /// <summary>
    /// `download-plan` ran, nothing went wrong, and it could not obtain a <c>DownloadProvider</c> from
    /// any object in the device's tree — so the report answers none of the questions the command
    /// exists to answer, and its calm appearance is not evidence about anything.
    ///
    /// Same family as <see cref="CompileIncomplete"/>/<see cref="ExportIncomplete"/>/
    /// <see cref="ImportIncomplete"/>/<see cref="NothingExamined"/>, and its own code for the same
    /// reason: it is not a failure (nothing threw, no argument was wrong, re-running changes nothing)
    /// and it is emphatically not a success. A caller must be able to tell "here is what a download
    /// would do" from "this tool could not find out" without parsing the report.
    ///
    /// It says nothing whatsoever about whether a download would be permitted — that is the write
    /// fence's question, and this command never asks it.
    /// </summary>
    public const int DownloadPlanIncomplete = 16;

    /// <summary>
    /// A write command failed and <b>the project is unchanged</b> — nothing was saved, and the
    /// partial mutation was removed from the open session. Earned by `create-instance-db` when the
    /// new instance DB comes back with an invalid block number (FI-63) or its number cannot be read.
    ///
    /// Its own code rather than <see cref="CommandError"/> because nothing was named wrongly and
    /// re-running with a different argument does not fix it, and rather than
    /// <see cref="UnexpectedError"/> because it is a modelled, expected outcome with a known
    /// recovery. What the caller reads off it is the half that matters: <b>there is nothing to clean
    /// up.</b> Before 2026-08-13 the same condition exited 5 having ALREADY SAVED the broken block,
    /// so the exit code and the project disagreed about whether anything had happened.
    /// </summary>
    public const int ChangeAbandoned = 17;

    /// <summary>
    /// Same failure as <see cref="ChangeAbandoned"/> with the cleanup half missing: nothing was
    /// saved, so <b>nothing reached disk</b>, but the partial mutation could not be removed from the
    /// in-memory project model either.
    ///
    /// A separate code because the caller's response differs, which is this table's rule for when to
    /// split one (cf. <see cref="CompileIncomplete"/>, which does not). On 17 a retry is immediately
    /// safe. On 18 the open Portal session holds a block that exists nowhere on disk, and if that
    /// session belongs to a person rather than to this process, the recovery is to close it WITHOUT
    /// saving — advice that would be actively wrong on a 17.
    /// </summary>
    public const int RollbackIncomplete = 18;

    /// <summary>
    /// The project contains <b>two or more blocks holding the same number</b> on one device
    /// (2026-08-13). Earned by `sanity-check`, and by `import` when the project holds a collision
    /// after the files went in.
    ///
    /// It is NOT <see cref="SanityCheckFailed"/>, and that separation is the whole point. 9 means
    /// "something is inconsistent or a device failed to compile", and the measured project was
    /// <b>perfectly consistent and compiled clean</b> while holding two blocks at FC 910 — so folding
    /// this into 9 would put a real defect behind a code whose documented remedy (compile the listed
    /// blocks) is exactly what ERASES the only signal there was. Measured: the colliding import made
    /// `sanity-check` report <c>INCONSISTENT: 1</c>, one per-block compile cleared it, and the check
    /// returned HEALTHY with the duplicate still present.
    ///
    /// It is not <see cref="CommandError"/> either: nothing was named wrongly and re-running with a
    /// different argument does not fix it. The fix is destructive — delete or renumber one of the
    /// blocks — which is why this tool reports and never repairs.
    ///
    /// <b>It outranks every other non-error verdict here.</b> When a run could return this or a
    /// 9/13, it returns this: every other verdict either clears itself on the next pass or announces
    /// itself again, and a duplicate does neither. Both reports are printed in full regardless, so
    /// ranking the codes hides nothing.
    /// </summary>
    public const int DuplicateBlockNumber = 19;

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

        // `create-instance-db` refused to keep what it made (2026-08-13). Two codes, split on
        // whether the in-memory session was left clean, because that is the only thing the caller
        // has to do differently. Order matters: the more specific pattern must come first.
        // Neither is UnexpectedError — the predecessor of this pair WAS unclassified, and reporting
        // the single most destructive failure this CLI had as an unmodelled internal fault is the
        // audit-F-09 defect all over again.
        InstanceDbCreationAbandonedException { RollbackCompleted: false } => RollbackIncomplete,
        InstanceDbCreationAbandonedException => ChangeAbandoned,

        // Deliberately an internal fault, not a user error. `PerformDownload` is unreachable by
        // design — no argument reaches it and `download-plan` never calls it — so if this ever
        // escapes, something in this program called a method that exists only to be uncallable.
        // That is a bug in the tool, and it should print the full chain and say so, not be dressed
        // up as something the caller mistyped.
        Model.DownloadNotEnabledException => UnexpectedError,

        // Same family: the block resolved but does not expose an access mode, and the correction is
        // to name a different block. Reporting it as an internal fault would be the audit-F-09
        // defect again.
        Model.BlockMemoryLayoutUnavailableException => CommandError,

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
