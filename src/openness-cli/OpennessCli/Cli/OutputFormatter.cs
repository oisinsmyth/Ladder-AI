using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using OpennessCli.Model;

namespace OpennessCli.Cli;

public static class OutputFormatter
{
    private static readonly string[] Headers = { "TYPE", "NUMBER", "NAME", "LANGUAGE", "SAFETY", "CONSISTENT", "PATH" };

    // A refusal is recorded in the same list as a success, so both the count and the exit code have
    // to subtract them. Measured 2026-08-09 (P3/P4): three refused dynamizations and a refused alarm
    // text were reported as "changes applied: 5" / "3 change(s)" with exit 0 — a scripted caller
    // reading the count or the exit code would have recorded work that never happened.
    // net48 has no string.Contains(string, StringComparison).
    public static bool IsRefusal(string line) => line.IndexOf("REFUSED", StringComparison.Ordinal) >= 0;

    public static int CountRefusals(IEnumerable<string> applied) => applied.Count(IsRefusal);

    // "2 (3 REFUSED)" rather than "5" — the headline number must mean what it says.
    public static string DescribeAppliedCount(IReadOnlyList<string> applied)
    {
        var refused = CountRefusals(applied);
        var succeeded = applied.Count - refused;
        var text = succeeded.ToString(CultureInfo.InvariantCulture);
        return refused == 0
            ? text
            : $"{text} ({refused.ToString(CultureInfo.InvariantCulture)} REFUSED)";
    }

    /// <summary>
    /// The CLR class name is printed for every type and every version, not just for the ones that
    /// look interesting. That is the whole point of the command: the question it answers is which
    /// class a Unified faceplate actually turns out to be, and a report that pretty-printed only the
    /// friendly name would answer nothing.
    /// </summary>
    public static string FormatLibraryReport(LibraryInventory inventory, bool includeMasterCopies)
    {
        var sb = new StringBuilder();
        sb.Append("PROJECT LIBRARY — types: ").Append(inventory.Types.Count.ToString(CultureInfo.InvariantCulture));
        if (includeMasterCopies)
        {
            sb.Append("  masterCopies: ").Append(inventory.MasterCopies.Count.ToString(CultureInfo.InvariantCulture));
        }

        sb.AppendLine();

        if (inventory.Types.Count == 0)
        {
            sb.AppendLine("  (no library types — the project library is empty)");
        }

        foreach (var type in inventory.Types)
        {
            sb.Append("  ").Append(type.FolderPath).Append("  ").Append(type.Name)
              .Append("  [").Append(type.ClrTypeName).Append(']');
            if (!string.IsNullOrEmpty(type.Namespace))
            {
                sb.Append("  ns=").Append(type.Namespace);
            }

            sb.Append("  status=").Append(type.Status).AppendLine();

            sb.Append("      exportFormats: ")
              .AppendLine(type.ExportFormats.Count == 0 ? "(NONE — no document round trip for this type)" : string.Join(", ", type.ExportFormats));

            foreach (var version in type.Versions)
            {
                sb.Append("      v").Append(version.VersionNumber)
                  .Append("  ").Append(version.State)
                  .Append(version.IsDefault ? "  (default)" : string.Empty)
                  .Append("  [").Append(version.ClrTypeName).Append(']')
                  .AppendLine();
            }
        }

        foreach (var copy in inventory.MasterCopies)
        {
            sb.Append("  MASTERCOPY  ").Append(copy.FolderPath).Append("  ").Append(copy.Name);
            if (copy.ContentTypes.Count > 0)
            {
                sb.Append("  contains: ").Append(string.Join(", ", copy.ContentTypes));
            }

            sb.AppendLine();
        }

        return sb.ToString().TrimEnd('\n', '\r');
    }

    public static string FormatLibraryJson(LibraryInventory inventory)
    {
        var payload = new
        {
            types = inventory.Types.Select(t => new
            {
                folderPath = t.FolderPath,
                name = t.Name,
                clrTypeName = t.ClrTypeName,
                @namespace = t.Namespace,
                status = t.Status,
                exportFormats = t.ExportFormats,
                versions = t.Versions.Select(v => new
                {
                    versionNumber = v.VersionNumber,
                    state = v.State,
                    clrTypeName = v.ClrTypeName,
                    isDefault = v.IsDefault,
                }),
            }),
            masterCopies = inventory.MasterCopies.Select(m => new
            {
                folderPath = m.FolderPath,
                name = m.Name,
                contentTypes = m.ContentTypes,
            }),
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    public static string FormatTable(IReadOnlyList<BlockInfo> blocks)
    {
        if (blocks.Count == 0)
        {
            return "(no blocks found)";
        }

        var rows = blocks.Select(ToRow).ToList();
        var widths = Enumerable.Range(0, Headers.Length)
            .Select(col => Math.Max(Headers[col].Length, rows.Max(r => r[col].Length)))
            .ToArray();

        var sb = new StringBuilder();
        AppendRow(sb, Headers, widths);
        AppendSeparator(sb, widths);
        foreach (var row in rows)
        {
            AppendRow(sb, row, widths);
        }

        return sb.ToString().TrimEnd('\n', '\r');
    }

    public static string FormatJson(IReadOnlyList<BlockInfo> blocks)
    {
        var payload = blocks.Select(b => new
        {
            type = b.Type.ToString(),
            number = b.Number,
            name = b.Name,
            language = b.Language,
            safety = b.IsSafety,
            consistent = b.IsConsistent,
            path = b.Path,
        });

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Report shape is deliberately not a flat table: an HMI device is a small tree (device →
    /// screen → item → dynamization) and flattening it loses the containment that makes the output
    /// readable. The family line is the most important thing on the page — it is what tells the
    /// reader whether screen contents were unavailable or merely not requested.
    /// </summary>
    public static string FormatHmiReport(IReadOnlyList<HmiDeviceInfo> devices, string? screenFilter)
    {
        if (devices.Count == 0)
        {
            return "(no HMI devices found)";
        }

        var sb = new StringBuilder();
        foreach (var device in devices)
        {
            sb.Append("HMI DEVICE  ").Append(device.Path).Append("  [").Append(device.Family).AppendLine("]");

            if (device.Family == HmiFamily.Unified)
            {
                sb.Append("  screens=").Append(device.ScreenCount)
                  .Append("  screenGroups=").Append(device.ScreenGroupCount)
                  .Append("  tags=").Append(device.TagCount)
                  .Append("  discreteAlarms=").Append(device.DiscreteAlarmCount)
                  .Append("  analogAlarms=").Append(device.AnalogAlarmCount)
                  .Append("  alarmClasses=").Append(device.AlarmClassCount)
                  .Append("  scripts=").Append(device.ScriptCount)
                  .AppendLine();
            }
            else
            {
                sb.Append("  screens=").Append(device.ScreenCount).AppendLine();
                sb.AppendLine("  (classic: Openness exposes no screen contents — Screen has no ScreenItems, so items cannot be listed)");
            }

            foreach (var screen in device.Screens)
            {
                sb.Append("  SCREEN  ").Append(screen.Name);
                if (screen.ScreenNumber is { } number)
                {
                    sb.Append("  #").Append(number.ToString(CultureInfo.InvariantCulture));
                }

                if (screen.Width is { } w && screen.Height is { } h)
                {
                    sb.Append("  ").Append(w.ToString(CultureInfo.InvariantCulture)).Append('x').Append(h.ToString(CultureInfo.InvariantCulture));
                }

                if (device.Family == HmiFamily.Unified)
                {
                    sb.Append("  items=").Append(screen.ItemCount.ToString(CultureInfo.InvariantCulture));
                }

                sb.AppendLine();

                foreach (var handler in screen.Events)
                {
                    sb.Append("    on ").Append(handler.EventType);
                    sb.Append(handler.HasScript ? "  script: " : "  (no script)");
                    if (handler.HasScript)
                    {
                        sb.Append(handler.ScriptPreview);
                    }

                    sb.AppendLine();
                }

                if (screen.Items.Count < screen.ItemCount)
                {
                    // Never let a cap read as a complete listing (docs/04 design philosophy: no
                    // silent truncation).
                    sb.Append("    (showing ").Append(screen.Items.Count.ToString(CultureInfo.InvariantCulture))
                      .Append(" of ").Append(screen.ItemCount.ToString(CultureInfo.InvariantCulture));
                    sb.AppendLine(screen.Items.Count == 0 ? " items — pass --screen to read them)" : " items — raise --max-items for the rest)");
                }

                foreach (var item in screen.Items)
                {
                    sb.Append("    ").Append(item.ItemType).Append("  ").Append(item.Name);
                    if (item.Left is { } left && item.Top is { } top)
                    {
                        sb.Append("  @").Append(left.ToString(CultureInfo.InvariantCulture)).Append(',').Append(top.ToString(CultureInfo.InvariantCulture));
                    }

                    if (item.Width is { } iw && item.Height is { } ih)
                    {
                        sb.Append("  ").Append(iw.ToString(CultureInfo.InvariantCulture)).Append('x').Append(ih.ToString(CultureInfo.InvariantCulture));
                    }

                    sb.AppendLine();

                    foreach (var handler in item.Events)
                    {
                        sb.Append("      on ").Append(handler.EventType);
                        sb.Append(handler.HasScript ? "  script: " : "  (no script)");
                        if (handler.HasScript)
                        {
                            sb.Append(handler.ScriptPreview);
                        }

                        sb.AppendLine();
                    }

                    foreach (var dynamization in item.Dynamizations)
                    {
                        sb.Append("      ").Append(dynamization.PropertyName).Append(" <- ").Append(dynamization.Kind);
                        if (!string.IsNullOrEmpty(dynamization.Tag))
                        {
                            sb.Append("  tag=").Append(dynamization.Tag);
                        }

                        if (!string.IsNullOrEmpty(dynamization.PlcTag))
                        {
                            sb.Append("  plcTag=").Append(dynamization.PlcTag);
                        }

                        sb.AppendLine();

                        // On its own line: a mapping table is several entries wide and inlining it
                        // would push the tag name off the end of the row it belongs to.
                        if (!string.IsNullOrEmpty(dynamization.Mapping))
                        {
                            sb.Append("        mapping: ").AppendLine(dynamization.Mapping);
                        }
                    }
                }
            }
        }

        if (screenFilter is null && devices.Any(d => d.Family == HmiFamily.Unified))
        {
            sb.AppendLine("(summary only — pass --screen <name> or --screen * to read screen items and dynamizations)");
        }

        return sb.ToString().TrimEnd('\n', '\r');
    }

    /// <summary>
    /// The schema report — what stands in for a screen XML on Unified, where no screen export
    /// exists. Attributes are ordered by create-relevance first (Mandatory, then Relevant, then the
    /// rest) because that is the order someone authoring a screen needs them in: what must be set,
    /// then what is worth setting, then everything else.
    /// </summary>
    public static string FormatHmiSchemaReport(IReadOnlyList<HmiSchemaReport> reports)
    {
        if (reports.Count == 0)
        {
            return "(no Unified HMI devices found — schema is a Unified-only concept; classic exposes no screen items)";
        }

        var sb = new StringBuilder();
        foreach (var report in reports)
        {
            sb.Append("HMI DEVICE  ").AppendLine(report.DevicePath);

            sb.Append("  CREATABLE SCREEN-ITEM TYPES (").Append(report.CreatableScreenItemTypes.Count).AppendLine("):");
            if (report.CreatableScreenItemTypes.Count == 0)
            {
                sb.AppendLine("    (none reported — GetCreationInfos returned nothing for ScreenItems)");
            }

            foreach (var type in report.CreatableScreenItemTypes)
            {
                sb.Append("    ").AppendLine(type);
            }

            foreach (var schema in report.ItemSchemas)
            {
                sb.Append("  TYPE  ").Append(schema.TypeName)
                  .Append("  attributes=").Append(schema.Attributes.Count.ToString(CultureInfo.InvariantCulture))
                  .AppendLine();

                if (schema.Compositions.Count > 0)
                {
                    sb.Append("    compositions: ").AppendLine(string.Join(", ", schema.Compositions));
                }

                foreach (var attribute in schema.Attributes.OrderBy(RelevanceRank).ThenBy(a => a.Name, StringComparer.Ordinal))
                {
                    sb.Append("    ").Append(attribute.Name)
                      .Append("  [").Append(attribute.AccessMode).Append('/').Append(attribute.CreateRelevance).Append(']');
                    if (!string.IsNullOrEmpty(attribute.SupportedType))
                    {
                        sb.Append("  : ").Append(attribute.SupportedType);
                    }

                    if (!string.IsNullOrEmpty(attribute.SampleValue))
                    {
                        sb.Append("  = ").Append(attribute.SampleValue);
                    }

                    sb.AppendLine();
                }
            }
        }

        return sb.ToString().TrimEnd('\n', '\r');
    }

    private static int RelevanceRank(HmiAttributeSchema attribute) => attribute.CreateRelevance switch
    {
        "Mandatory" => 0,
        "Relevant" => 1,
        _ => 2,
    };

    /// <summary>
    /// Reports what was created and, more importantly, what <c>Validate()</c> said. A clean validate
    /// is stated explicitly rather than shown as silence — "no messages" and "never ran" look
    /// identical otherwise, and this is the first time this project has invoked it at all.
    /// </summary>
    public static string FormatHmiCreateScreenResult(HmiCreateScreenResult result)
    {
        var sb = new StringBuilder();
        sb.Append("CREATED screen '").Append(result.ScreenName).Append("' on ").AppendLine(result.DevicePath);
        sb.Append("  size: ").Append(result.Width.ToString(CultureInfo.InvariantCulture))
          .Append('x').Append(result.Height.ToString(CultureInfo.InvariantCulture)).AppendLine();
        sb.Append("  items created: ").Append(result.CreatedItems.Count.ToString(CultureInfo.InvariantCulture)).AppendLine();
        foreach (var item in result.CreatedItems)
        {
            sb.Append("    ").AppendLine(item);
        }

        AppendValidation(sb, result.Validation);

        // Saved is reported explicitly because an unsaved create survives only as long as the Portal
        // process holding it — the trap SaveProject's own comment records.
        sb.Append("  project saved: ").Append(result.Saved ? "yes" : "NO");
        return sb.ToString().TrimEnd('\n', '\r');
    }

    public static string FormatHmiEditScreenResult(HmiEditScreenResult result)
    {
        var sb = new StringBuilder();
        sb.Append("EDITED screen '").Append(result.ScreenName).Append("' on ").AppendLine(result.DevicePath);
        sb.Append("  changes applied: ").Append(DescribeAppliedCount(result.Applied)).AppendLine();
        foreach (var change in result.Applied)
        {
            sb.Append("    ").AppendLine(change);
        }

        AppendValidation(sb, result.Validation);
        sb.Append("  project saved: ").Append(result.Saved ? "yes" : "NO");
        return sb.ToString().TrimEnd('\n', '\r');
    }

    // Shared by create and edit so the two cannot drift in how they report a gate result.
    private static void AppendValidation(StringBuilder sb, IReadOnlyList<HmiValidationMessage> validation)
    {
        var errors = validation.Count(m => m.Severity == "Error");
        var warnings = validation.Count(m => m.Severity == "Warning");
        var threw = validation.Count(m => m.Severity == "ValidateThrew");

        sb.Append("  Validate(): ");
        if (threw > 0)
        {
            sb.AppendLine("THREW — see below (this is a finding, not a pass)");
        }
        else if (validation.Count == 0)
        {
            sb.AppendLine("ran, returned no errors and no warnings");
        }
        else
        {
            sb.Append(errors.ToString(CultureInfo.InvariantCulture)).Append(" error(s), ")
              .Append(warnings.ToString(CultureInfo.InvariantCulture)).AppendLine(" warning(s)");
        }

        foreach (var message in validation)
        {
            sb.Append("    [").Append(message.Severity).Append("] ");
            if (!string.IsNullOrEmpty(message.PropertyName))
            {
                sb.Append(message.PropertyName).Append(": ");
            }

            sb.AppendLine(message.Message);
        }
    }

    public static string FormatHmiEditScreenJson(HmiEditScreenResult result) =>
        JsonSerializer.Serialize(
            new
            {
                devicePath = result.DevicePath,
                screenName = result.ScreenName,
                applied = result.Applied,
                validation = result.Validation.Select(m => new { property = m.PropertyName, severity = m.Severity, message = m.Message }),
                saved = result.Saved,
            },
            new JsonSerializerOptions { WriteIndented = true });

    public static string FormatHmiCreateScreenJson(HmiCreateScreenResult result) =>
        JsonSerializer.Serialize(
            new
            {
                devicePath = result.DevicePath,
                screenName = result.ScreenName,
                width = result.Width,
                height = result.Height,
                createdItems = result.CreatedItems,
                validation = result.Validation.Select(m => new { property = m.PropertyName, severity = m.Severity, message = m.Message }),
                saved = result.Saved,
            },
            new JsonSerializerOptions { WriteIndented = true });

    /// <summary>
    /// The census. Grouped by kind with counts, and it calls out surviving `ZZ_AI_*` probe artifacts
    /// explicitly — the end-of-programme check is "zero of these left", and a listing that buried
    /// them among 233 real tags would not answer it.
    /// </summary>
    public static string FormatHmiInventoryTable(IReadOnlyList<HmiObjectInfo> objects)
    {
        if (objects.Count == 0)
        {
            return "(no HMI objects found)";
        }

        var sb = new StringBuilder();
        foreach (var group in objects.GroupBy(o => o.Kind).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            sb.Append(group.Key).Append("  (").Append(group.Count().ToString(CultureInfo.InvariantCulture)).AppendLine(")");
            foreach (var item in group.OrderBy(o => o.Name, StringComparer.Ordinal))
            {
                sb.Append("    ").Append(item.Name).Append("  [").Append(item.TypeName).AppendLine("]");
            }
        }

        var probeArtifacts = objects.Where(o => o.Name.StartsWith("ZZ_AI_", StringComparison.Ordinal)).ToList();
        sb.AppendLine();
        sb.Append("PROBE ARTIFACTS (ZZ_AI_*): ").Append(probeArtifacts.Count.ToString(CultureInfo.InvariantCulture));
        if (probeArtifacts.Count > 0)
        {
            sb.AppendLine(" — these are this tool's own and must be zero at end of programme:");
            foreach (var item in probeArtifacts.OrderBy(o => o.Kind, StringComparer.Ordinal).ThenBy(o => o.Name, StringComparer.Ordinal))
            {
                sb.Append("    ").Append(item.Kind).Append(" / ").AppendLine(item.Name);
            }
        }

        return sb.ToString().TrimEnd('\n', '\r');
    }

    public static string FormatHmiInventoryJson(IReadOnlyList<HmiObjectInfo> objects) =>
        JsonSerializer.Serialize(
            new
            {
                total = objects.Count,
                probeArtifacts = objects.Count(o => o.Name.StartsWith("ZZ_AI_", StringComparison.Ordinal)),
                objects = objects.Select(o => new { kind = o.Kind, name = o.Name, typeName = o.TypeName }),
            },
            new JsonSerializerOptions { WriteIndented = true });

    public static string FormatHmiSchemaJson(IReadOnlyList<HmiSchemaReport> reports)
    {
        var payload = reports.Select(r => new
        {
            devicePath = r.DevicePath,
            creatableScreenItemTypes = r.CreatableScreenItemTypes,
            itemSchemas = r.ItemSchemas.Select(s => new
            {
                typeName = s.TypeName,
                compositions = s.Compositions,
                attributes = s.Attributes.Select(a => new
                {
                    name = a.Name,
                    accessMode = a.AccessMode,
                    createRelevance = a.CreateRelevance,
                    supportedType = a.SupportedType,
                    sampleValue = a.SampleValue,
                }),
            }),
        });

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    public static string FormatHmiJson(IReadOnlyList<HmiDeviceInfo> devices)
    {
        var payload = devices.Select(d => new
        {
            path = d.Path,
            software = d.SoftwareName,
            family = d.Family.ToString(),
            screenCount = d.ScreenCount,
            screenGroupCount = d.ScreenGroupCount,
            tagCount = d.TagCount,
            discreteAlarmCount = d.DiscreteAlarmCount,
            analogAlarmCount = d.AnalogAlarmCount,
            alarmClassCount = d.AlarmClassCount,
            scriptCount = d.ScriptCount,
            screens = d.Screens.Select(s => new
            {
                name = s.Name,
                screenNumber = s.ScreenNumber,
                width = s.Width,
                height = s.Height,
                itemCount = s.ItemCount,
                events = s.Events.Select(e => new { eventType = e.EventType, hasScript = e.HasScript, scriptPreview = e.ScriptPreview }),
                items = s.Items.Select(i => new
                {
                    name = i.Name,
                    itemType = i.ItemType,
                    left = i.Left,
                    top = i.Top,
                    width = i.Width,
                    height = i.Height,
                    dynamizations = i.Dynamizations.Select(dyn => new
                    {
                        propertyName = dyn.PropertyName,
                        kind = dyn.Kind,
                        tag = dyn.Tag,
                        plcTag = dyn.PlcTag,
                        mapping = dyn.Mapping,
                    }),
                    events = i.Events.Select(e => new
                    {
                        eventType = e.EventType,
                        hasScript = e.HasScript,
                        scriptPreview = e.ScriptPreview,
                    }),
                }),
            }),
        });

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    private static readonly string[] TagTableHeaders = { "NAME", "PATH" };

    public static string FormatTagTableTable(IReadOnlyList<TagTableInfo> tagTables)
    {
        if (tagTables.Count == 0)
        {
            return "(no tag tables found)";
        }

        var rows = tagTables.Select(t => new[] { t.Name, t.Path }).ToList();
        var widths = Enumerable.Range(0, TagTableHeaders.Length)
            .Select(col => Math.Max(TagTableHeaders[col].Length, rows.Max(r => r[col].Length)))
            .ToArray();

        var sb = new StringBuilder();
        AppendRow(sb, TagTableHeaders, widths);
        AppendSeparator(sb, widths);
        foreach (var row in rows)
        {
            AppendRow(sb, row, widths);
        }

        return sb.ToString().TrimEnd('\n', '\r');
    }

    public static string FormatTagTableJson(IReadOnlyList<TagTableInfo> tagTables)
    {
        var payload = tagTables.Select(t => new { name = t.Name, path = t.Path });
        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Counts recomputed from the message tree, NOT taken from the compiler's own aggregates.
    /// Measured 2026-08-08 against a real HMI device: <c>CompilerResult</c> reported
    /// <c>WARNINGS: 0</c> alongside 156 warning messages, and on a failing run <c>ERRORS: 1</c>
    /// alongside 6 error messages. The aggregates are wrong in both directions, so anything gating
    /// or reporting on them is wrong too. <c>State</c> remains trustworthy and is what gates.
    /// </summary>
    private static (int Errors, int Warnings) CountFromMessages(CompileResult result) =>
        (result.Messages.Count(m => m.State == Model.CompileState.Error),
         result.Messages.Count(m => m.State == Model.CompileState.Warning));

    public static string FormatCompileTable(CompileResult result)
    {
        var sb = new StringBuilder();
        var (errors, warnings) = CountFromMessages(result);
        sb.Append("STATE: ").Append(result.State).Append('\n');
        sb.Append("ERRORS: ").Append(errors).Append("  WARNINGS: ").Append(warnings).Append('\n');

        // The compiler's own aggregates are shown only when they DISAGREE with the messages, so the
        // discrepancy is visible rather than silently papered over.
        if (result.ErrorCount != errors || result.WarningCount != warnings)
        {
            sb.Append("NOTE: compiler reported ErrorCount=").Append(result.ErrorCount)
              .Append(", WarningCount=").Append(result.WarningCount)
              .Append(" — these disagree with the messages above and are not reliable; counts shown are from the message tree.\n");
        }

        if (result.Messages.Count > 0)
        {
            sb.Append('\n');
            foreach (var message in result.Messages)
            {
                sb.Append('[').Append(message.State).Append("] ");
                if (!string.IsNullOrEmpty(message.Path))
                {
                    sb.Append(message.Path).Append(": ");
                }

                sb.Append(message.Description).Append('\n');
            }
        }

        return sb.ToString().TrimEnd('\n', '\r');
    }

    public static string FormatCompileJson(CompileResult result)
    {
        var payload = new
        {
            state = result.State.ToString(),
            errors = result.ErrorCount,
            warnings = result.WarningCount,
            messages = result.Messages.Select(m => new
            {
                state = m.State.ToString(),
                description = m.Description,
                path = m.Path,
            }),
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    public static string FormatSanityCheckTable(SanityCheckResult result)
    {
        var sb = new StringBuilder();
        sb.Append("OVERALL: ").Append(result.IsHealthy ? "HEALTHY" : "ISSUES FOUND").Append('\n');
        sb.Append("BLOCKS: ").Append(result.TotalBlocks).Append("  INCONSISTENT: ").Append(result.InconsistentBlocks.Count).Append('\n');

        // FI-62: types get their own line, always printed even at zero. A reader who has only ever
        // seen BLOCKS/INCONSISTENT needs to see that types were actually looked at — a silent
        // absence is what let an inconsistent UDT pass a HEALTHY gate in the first place.
        sb.Append("TYPES: ").Append(result.TotalTypes).Append("  INCONSISTENT: ").Append(result.InconsistentTypes.Count).Append('\n');

        if (result.InconsistentBlocks.Count > 0)
        {
            sb.Append('\n').Append("Inconsistent blocks:\n");
            foreach (var block in result.InconsistentBlocks)
            {
                sb.Append("  ").Append(block.Path).Append('/').Append(block.Name).Append(" (").Append(block.Language).Append(")\n");
            }
        }

        if (result.InconsistentTypes.Count > 0)
        {
            sb.Append('\n').Append("Inconsistent PLC data types:\n");
            foreach (var type in result.InconsistentTypes)
            {
                sb.Append("  ").Append(type.Path).Append('/').Append(type.Name).Append('\n');
            }

            sb.Append("  -> clear with: openness-cli compile <project> --type <name>\n");
        }

        sb.Append('\n').Append("Device compiles:\n");
        foreach (var device in result.DeviceCompiles)
        {
            sb.Append("  ").Append(device.DevicePath).Append(": ").Append(device.Compile.State)
                .Append(" (errors=").Append(device.Compile.ErrorCount)
                .Append(", warnings=").Append(device.Compile.WarningCount).Append(")\n");
        }

        return sb.ToString().TrimEnd('\n', '\r');
    }

    public static string FormatSanityCheckJson(SanityCheckResult result)
    {
        var payload = new
        {
            healthy = result.IsHealthy,
            totalBlocks = result.TotalBlocks,
            inconsistentBlocks = result.InconsistentBlocks.Select(b => new { name = b.Name, path = b.Path, language = b.Language }),
            totalTypes = result.TotalTypes,
            inconsistentTypes = result.InconsistentTypes.Select(t => new { name = t.Name, path = t.Path }),
            deviceCompiles = result.DeviceCompiles.Select(d => new
            {
                device = d.DevicePath,
                state = d.Compile.State.ToString(),
                errors = d.Compile.ErrorCount,
                warnings = d.Compile.WarningCount,
            }),
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    private static readonly string[] PortalStatusHeaders = { "PID", "CLASS", "PROJECT", "UI", "ACQUIRED" };

    public static string FormatPortalStatusTable(PortalStatusReport report)
    {
        var sb = new StringBuilder();
        sb.Append("PROCESSES: ").Append(report.Total)
            .Append("  IN-USE: ").Append(report.InUseCount)
            .Append("  SELF-LAUNCHED ORPHANS: ").Append(report.SelfLaunchedOrphanCount)
            .Append("  STRAYS: ").Append(report.StrayEmptyCount).Append('\n');
        sb.Append("NOTE: ").Append(report.Note).Append('\n');

        if (report.Processes.Count == 0)
        {
            return sb.ToString().TrimEnd('\n', '\r');
        }

        var rows = report.Processes.Select(ToPortalRow).ToList();
        var widths = Enumerable.Range(0, PortalStatusHeaders.Length)
            .Select(col => Math.Max(PortalStatusHeaders[col].Length, rows.Max(r => r[col].Length)))
            .ToArray();

        sb.Append('\n');
        AppendRow(sb, PortalStatusHeaders, widths);
        AppendSeparator(sb, widths);
        foreach (var row in rows)
        {
            AppendRow(sb, row, widths);
        }

        return sb.ToString().TrimEnd('\n', '\r');
    }

    public static string FormatPortalStatusJson(PortalStatusReport report)
    {
        var payload = new
        {
            processes = report.Processes.Select(p => new
            {
                pid = p.Process.Pid,
                @class = ClassLabel(p.Class),
                projectPath = p.Process.ProjectPath,
                hasUserInterface = p.Process.HasUserInterface,
                acquired = p.Process.Acquired,
                markedByThisTool = p.Process.MarkedByThisTool,
            }),
            counts = new
            {
                total = report.Total,
                inUse = report.InUseCount,
                selfLaunchedOrphans = report.SelfLaunchedOrphanCount,
                strayEmpty = report.StrayEmptyCount,
            },
            note = report.Note,
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string ClassLabel(PortalProcessClass c) => c switch
    {
        PortalProcessClass.InUse => "in-use",
        PortalProcessClass.SelfLaunchedOrphan => "self-launched-orphan",
        PortalProcessClass.StrayEmpty => "stray-empty",
        _ => c.ToString(),
    };

    private static string[] ToPortalRow(ClassifiedPortalProcess p) => new[]
    {
        p.Process.Pid.ToString(CultureInfo.InvariantCulture),
        ClassLabel(p.Class),
        string.IsNullOrEmpty(p.Process.ProjectPath) ? "(none)" : p.Process.ProjectPath!,
        p.Process.HasUserInterface ? "yes" : "no",
        p.Process.Acquired.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
    };

    private static string[] ToRow(BlockInfo b) => new[]
    {
        b.Type.ToString(),
        b.Number.ToString(CultureInfo.InvariantCulture),
        b.Name,
        b.Language,
        b.IsSafety ? "SAFETY" : string.Empty,
        b.IsConsistent ? string.Empty : "INCONSISTENT",
        b.Path,
    };

    private static void AppendRow(StringBuilder sb, IReadOnlyList<string> cells, IReadOnlyList<int> widths)
    {
        for (var i = 0; i < cells.Count; i++)
        {
            sb.Append(cells[i].PadRight(widths[i]));
            if (i < cells.Count - 1)
            {
                sb.Append("  ");
            }
        }

        sb.Append('\n');
    }

    private static void AppendSeparator(StringBuilder sb, IReadOnlyList<int> widths)
    {
        for (var i = 0; i < widths.Count; i++)
        {
            sb.Append(new string('-', widths[i]));
            if (i < widths.Count - 1)
            {
                sb.Append("  ");
            }
        }

        sb.Append('\n');
    }
}
