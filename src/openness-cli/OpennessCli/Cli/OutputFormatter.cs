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
    /// <summary>
    /// Reports a <c>LibraryTypeVersion.Export</c> attempt, including how the call was BOUND.
    /// </summary>
    /// <remarks>
    /// A bare "0 files produced" would be another unexplained empty answer, and this project has
    /// been misled by one of those before. Printing the overload's parameter type and the options
    /// value passed makes a negative result diagnosable: it separates "this type has no document
    /// form" from "the wrong overload was bound".
    /// </remarks>
    public static string FormatLibraryExport(LibraryExportResult export)
    {
        var sb = new StringBuilder();
        sb.Append("LIBRARY TYPE EXPORT  ").Append(export.TypeName)
          .Append("  version=").Append(string.IsNullOrEmpty(export.Version) ? "(default)" : export.Version)
          .Append("  [").Append(export.ClrTypeName).AppendLine("]");
        sb.Append("  bound: Export(").Append(export.FirstParameterType).Append(", ")
          .Append(export.OptionsTypeName).Append(") with ").AppendLine(export.OptionsValue);

        if (export.ProducedPaths.Count == 0)
        {
            sb.AppendLine("  PRODUCED NOTHING - the call returned without throwing and wrote no file.");
            sb.AppendLine("  That is a FAILED export, not a quiet success. Exit 7.");
            return sb.ToString().TrimEnd();
        }

        sb.Append("  produced ").Append(export.ProducedPaths.Count.ToString(CultureInfo.InvariantCulture))
          .AppendLine(export.ProducedPaths.Count == 1 ? " file/directory:" : " files/directories:");
        foreach (var path in export.ProducedPaths)
        {
            sb.Append("    ").AppendLine(path);
        }

        return sb.ToString().TrimEnd();
    }

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
    public static string FormatHmiReport(IReadOnlyList<HmiDeviceInfo> devices, string? screenFilter, bool includeScripts = false)
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

                    // A container's contained type is the single most load-bearing fact about it:
                    // unset, the item is an empty box the compiler rejects; set, the type supplies
                    // the geometry, the visuals and the parameter list.
                    if (!string.IsNullOrEmpty(item.ContainedType))
                    {
                        sb.Append("  contains: ").Append(item.ContainedType);
                    }

                    sb.AppendLine();

                    if (item.Interface is { } faceplateInterface)
                    {
                        sb.Append("      interface: ")
                          .Append(faceplateInterface.Count.ToString(CultureInfo.InvariantCulture))
                          .AppendLine(faceplateInterface.Count == 1 ? " parameter" : " parameters");

                        for (var p = 0; p < faceplateInterface.Count; p++)
                        {
                            var parameter = faceplateInterface[p];
                            sb.Append("        [").Append(p.ToString(CultureInfo.InvariantCulture)).Append("] ")
                              .Append(string.IsNullOrEmpty(parameter.PropertyName) ? "(unnamed)" : parameter.PropertyName);

                            sb.Append(" = ").Append(string.IsNullOrEmpty(parameter.Value) ? "(unset)" : parameter.Value);

                            if (!string.IsNullOrEmpty(parameter.DataType))
                            {
                                sb.Append(" (").Append(parameter.DataType).Append(')');
                            }

                            sb.AppendLine();

                            foreach (var dynamization in parameter.Dynamizations)
                            {
                                sb.Append("            ").Append(dynamization.PropertyName)
                                  .Append(" <- ").Append(dynamization.Kind);
                                if (!string.IsNullOrEmpty(dynamization.Tag))
                                {
                                    sb.Append("  tag: ").Append(dynamization.Tag);
                                }

                                sb.AppendLine();
                            }
                        }
                    }

                    foreach (var handler in item.Events)
                    {
                        sb.Append("      on ").Append(handler.EventType);
                        sb.Append(handler.HasScript ? "  script: " : "  (no script)");
                        if (handler.HasScript)
                        {
                            sb.Append(handler.ScriptPreview);
                        }

                        sb.AppendLine();

                        if (includeScripts && handler.HasScript && !string.IsNullOrEmpty(handler.Script))
                        {
                            foreach (var scriptLine in handler.Script!.Replace("\r\n", "\n").Split('\n'))
                            {
                                sb.Append("        | ").AppendLine(scriptLine.TrimEnd());
                            }
                        }
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

    /// <summary>
    /// The sentence a `--set` must print in both its dry run and its confirmed run. It is the one
    /// consequence of this command that cannot be undone by running it again, and it is invisible
    /// afterwards — unlike a deleted block, a block whose retained data was wiped looks entirely
    /// normal until someone reads a value that should have survived a restart.
    /// </summary>
    public const string RetainedDataWarning =
        "DESTRUCTIVE: changing a block's memory layout DESTROYS ITS RETAINED DATA on the next download.\n" +
        "  Every retentive value in this block is lost — there is no migration and no warning at download time.\n" +
        "  This is correct only for a NEW, purpose-built block. For a block in service, it is not.";

    /// <summary>
    /// The hazard that outlives the command. Printed on every `--set`, because the moment it bites
    /// is a LATER import, by which point nobody is looking at this output any more.
    /// </summary>
    public const string ReimportHazardWarning =
        "NOT DURABLE — MEASURED: RE-IMPORTING THIS BLOCK REVERTS THE LAYOUT TO Optimized. Confirmed against a real\n" +
        "  project by TIA export, and the revert happens AT IMPORT, before any compile. The exported .xml carries no\n" +
        "  MemoryLayout element at all, so the import states no opinion and TIA applies the S7-1200 default.\n" +
        "  Nothing downstream notices: Normalizer ignores the attribute, so `converter drift-check` cannot detect the\n" +
        "  change in EITHER direction, and a block read over classic S7comm simply goes absent from the wire.\n" +
        "  RE-ASSERT AFTER EVERY IMPORT OF THIS BLOCK — this is required, not precautionary:\n" +
        "    openness-cli block-layout <project> --block <name> --set Standard --yes\n" +
        "    openness-cli block-layout <project> --block <name> --expect Standard";

    /// <summary>
    /// A block-layout read or set. Deliberately spells out BEFORE / REQUESTED / AFTER as three
    /// separate lines on a set: the value that was asked for and the value the project actually
    /// holds afterwards are different facts, and a report that collapsed them would be unable to
    /// show the exact failure this command exists to catch.
    /// </summary>
    public static string FormatBlockLayoutTable(BlockLayoutResult result)
    {
        var sb = new StringBuilder();
        sb.Append("BLOCK: ").Append(result.Name).Append("  (").Append(result.Path).Append(")\n");
        sb.Append("TYPE: ").Append(result.Type).Append('\n');

        if (!result.IsSet)
        {
            sb.Append("LAYOUT: ").Append(result.Layout).Append('\n');
            if (result.Layout == MemoryLayoutKind.Optimized)
            {
                sb.Append("NOTE: an OPTIMIZED block is invisible to classic S7comm — a PC-side reader sees no such block\n")
                  .Append("  at all, and fails at the first data read rather than at connect.\n");
            }

            return sb.ToString().TrimEnd('\n', '\r');
        }

        sb.Append("BEFORE: ").Append(result.PreviousLayout).Append('\n');
        sb.Append("REQUESTED: ").Append(result.RequestedLayout).Append('\n');
        sb.Append("AFTER: ").Append(result.Layout).Append('\n');

        if (result.Verified)
        {
            sb.Append(result.Changed
                ? "VERIFIED: saved, the block re-resolved from the project, and the layout read back matches the request.\n"
                : "VERIFIED: the layout read back matches the request. It was already that value — nothing changed.\n");
            sb.Append(RetainedDataWarning).Append('\n');
            sb.Append(ReimportHazardWarning).Append('\n');
        }
        else
        {
            sb.Append("NOT APPLIED: the set did not take. Requested ").Append(result.RequestedLayout)
              .Append(", but the block re-read ").Append(result.Layout).Append(" after saving.\n")
              .Append("  Nothing here reports this as a success: a silent no-op is the exact failure this command exists to catch.\n");
        }

        return sb.ToString().TrimEnd('\n', '\r');
    }

    public static string FormatBlockLayoutJson(BlockLayoutResult result)
    {
        var payload = new
        {
            name = result.Name,
            path = result.Path,
            type = result.Type.ToString(),
            layout = result.Layout.ToString(),
            previousLayout = result.PreviousLayout?.ToString(),
            requestedLayout = result.RequestedLayout?.ToString(),
            isSet = result.IsSet,
            changed = result.Changed,
            verified = result.Verified,
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// The sentence this whole command exists to make unmissable. Printed on EVERY <c>download-plan</c>
    /// run, in table and JSON alike, whatever the options — because the misconception it corrects
    /// ("I'll download my one new DB") is not triggered by any particular flag, it is what a reader
    /// arrives already believing.
    /// </summary>
    public const string DownloadGranularityWarning =
        "GRANULARITY: THE WHOLE PLC SOFTWARE. There is NO per-block download.\n" +
        "  DownloadProvider.Download takes a connection, two configuration callbacks and a DownloadOptions —\n" +
        "  no block, no group, no selection, no exclusion. The smallest real unit is the whole program.\n" +
        "  'SoftwareOnlyChanges' does NOT mean \"the blocks you edited\": it means the parts TIA finds\n" +
        "  different from the controller, decided by TIA at download time, and neither chosen by nor\n" +
        "  visible to this tool beforehand.";

    /// <summary>
    /// Printed on every run too, and separately from the granularity note. They are different facts —
    /// one is about what a download would carry, the other about whether this binary can do it — and
    /// a reader who took in only one of them should still be left with the right conclusion.
    /// </summary>
    public const string DownloadDisabledNotice =
        "THIS COMMAND CANNOT DOWNLOAD. It plans, and stops. There is no --yes, no --force and no\n" +
        "  confirmed form; no argument, environment variable or build configuration in this binary\n" +
        "  reaches DownloadProvider.Download. Performing one would be new code, governed by the write\n" +
        "  fence (ADR-0009).";

    /// <summary>
    /// A <c>download-plan</c> report.
    ///
    /// Leads with the two constant notices rather than with the device, deliberately. The device and
    /// the options are what was asked; the granularity and the refusal are what the reader most needs
    /// and least expects, and a report that buried them under a tidy summary would be read as
    /// confirmation that the download is ready to go.
    /// </summary>
    public static string FormatDownloadPlanTable(DownloadPlanResult plan)
    {
        var sb = new StringBuilder();
        sb.Append("DOWNLOAD PLAN — NOTHING WAS DOWNLOADED, AND NOTHING CAN BE.\n");
        sb.Append(DownloadDisabledNotice).Append('\n');
        sb.Append(DownloadGranularityWarning).Append('\n');
        sb.Append('\n');

        sb.Append("DEVICE: ").Append(plan.DeviceName).Append("  (").Append(plan.DevicePath).Append(")\n");
        sb.Append("OPTIONS: ").Append(plan.Options).Append('\n');
        sb.Append("WOULD CARRY: ").Append(plan.BlockCount.ToString(CultureInfo.InvariantCulture))
          .Append(" block(s) + ").Append(plan.TypeCount.ToString(CultureInfo.InvariantCulture))
          .Append(" PLC data type(s) — the device's whole program, not a selection.\n");

        if (plan.HasUnverifiedContent)
        {
            // Not a gate — this command gates nothing, it reports. But a plan that omitted this would
            // be describing the transfer of content no compile has ever examined (hard rule 4, FI-52).
            sb.Append("UNVERIFIED CONTENT: ").Append(plan.InconsistentBlocks.Count.ToString(CultureInfo.InvariantCulture))
              .Append(" block(s) are flagged inconsistent — never compiled, and a download would carry them anyway.\n");
            foreach (var name in plan.InconsistentBlocks.Take(10))
            {
                sb.Append("  ").Append(name).Append('\n');
            }

            if (plan.InconsistentBlocks.Count > 10)
            {
                sb.Append("  ... and ").Append((plan.InconsistentBlocks.Count - 10).ToString(CultureInfo.InvariantCulture))
                  .Append(" more.\n");
            }

            // The remediation prints whenever there IS unverified content, not only when the list is
            // long enough to be truncated. Tying advice to a truncation threshold means the person
            // with two unverified blocks — the one most likely to shrug and carry on — is the only
            // one who never sees it.
            sb.Append("  Run `openness-cli compile-all <project>` before treating this program as verified (hard rule 4).\n");
        }

        sb.Append('\n');
        sb.Append(FormatDownloadProviderSource(plan.Provider));
        sb.Append('\n');
        sb.Append(FormatDownloadConnection(plan.Connection));

        return sb.ToString().TrimEnd('\n', '\r');
    }

    private static string FormatDownloadProviderSource(DownloadProviderSource provider)
    {
        var sb = new StringBuilder();
        sb.Append("DOWNLOAD PROVIDER\n");

        if (provider.Found)
        {
            sb.Append("  obtained from: ").Append(provider.SourcePath)
              .Append("  [").Append(provider.SourceClrType).Append("]\n");
            sb.Append("  via: IEngineeringServiceProvider.GetService<DownloadProvider>() — the only route there is\n")
              .Append("    (DownloadProvider's sole constructor is internal, and it implements IEngineeringService)\n");
            if (provider.ProviderParentClrType is { } parent)
            {
                sb.Append("  provider.Parent: ").Append(parent).Append('\n');
            }
        }
        else
        {
            // Reported as a finding, not as a shrug. "No provider" is an answer about this project
            // that a reader needs to be able to act on, and the list of what was asked is what makes
            // it actionable rather than merely disappointing.
            sb.Append("  NOT OBTAINABLE from any object in this device's tree. Nothing below describes what a\n")
              .Append("    download would do, because there is no provider to describe it with.\n");
        }

        sb.Append("  asked, outward from the software-bearing item:\n");
        foreach (var attempt in provider.Attempts)
        {
            sb.Append("    ").Append(attempt.Outcome.PadRight(24)).Append(attempt.ClrType)
              .Append("  ").Append(attempt.ObjectPath).Append('\n');
            if (!string.IsNullOrEmpty(attempt.Detail))
            {
                sb.Append("      services: ").Append(attempt.Detail).Append('\n');
            }
        }

        return sb.ToString();
    }

    private static string FormatDownloadConnection(DownloadConnectionPlan? connection)
    {
        var sb = new StringBuilder();
        sb.Append("CONNECTION THE DOWNLOAD WOULD USE\n");

        if (connection is null)
        {
            sb.Append("  (not read — no DownloadProvider was obtained)\n");
            return sb.ToString();
        }

        // Stated before the addresses, not after. Everything below is what the PROJECT declares; a
        // reader who takes an address here as evidence that something answers at it has drawn the
        // one wrong conclusion this section can produce.
        sb.Append("  READ FROM THE PROJECT, NOT FROM THE NETWORK. These are configured addresses, not\n")
          .Append("    observed ones — nothing here says a device answers at any of them.\n")
          .Append("    ConfigurationPcInterface.GetAccessibleDevices() (a live scan) is never called.\n");
        sb.Append("  IsConfigured: ").Append(connection.IsConfigured).Append('\n');
        sb.Append("  EnableLegacyCommunication: ").Append(connection.EnableLegacyCommunication).Append('\n');

        if (!connection.IsConfigured)
        {
            sb.Append("  NOTE: the project has no online connection configured, so a download would have no\n")
              .Append("    route to take. Configure it in TIA Portal before a download would be possible.\n");
        }

        if (connection.Modes.Count == 0)
        {
            sb.Append("  (no connection modes)\n");
            return sb.ToString();
        }

        foreach (var mode in connection.Modes)
        {
            sb.Append("  mode: ").Append(mode.Name).Append('\n');
            foreach (var pc in mode.PcInterfaces)
            {
                sb.Append("    pc-interface: ").Append(pc.Name)
                  .Append(" #").Append(pc.Number.ToString(CultureInfo.InvariantCulture)).Append('\n');
                foreach (var address in pc.Addresses)
                {
                    sb.Append("      pc address: ").Append(address.Address).Append("  (").Append(address.Name).Append(")\n");
                }

                foreach (var subnet in pc.Subnets)
                {
                    sb.Append("      subnet: ").Append(subnet).Append('\n');
                }

                foreach (var target in pc.TargetInterfaces)
                {
                    sb.Append("      target: ").Append(target.Name).Append('\n');
                    foreach (var address in target.Addresses)
                    {
                        sb.Append("        TARGET ADDRESS: ").Append(address.Address)
                          .Append("  (").Append(address.Name).Append(")\n");
                    }
                }
            }
        }

        return sb.ToString();
    }

    public static string FormatDownloadPlanJson(DownloadPlanResult plan)
    {
        var payload = new
        {
            // First two fields on purpose: a consumer reading the head of the document learns the
            // two things it must not get wrong before it learns anything else.
            canDownload = plan.CanDownload,
            granularity = plan.Granularity,
            cannotDownloadReason = plan.CannotDownloadReason,
            devicePath = plan.DevicePath,
            deviceName = plan.DeviceName,
            options = plan.Options.ToString(),
            blockCount = plan.BlockCount,
            typeCount = plan.TypeCount,
            hasUnverifiedContent = plan.HasUnverifiedContent,
            inconsistentBlocks = plan.InconsistentBlocks,
            provider = new
            {
                found = plan.Provider.Found,
                sourcePath = plan.Provider.SourcePath,
                sourceClrType = plan.Provider.SourceClrType,
                providerParentClrType = plan.Provider.ProviderParentClrType,
                acquisition = "IEngineeringServiceProvider.GetService<DownloadProvider>()",
                attempts = plan.Provider.Attempts.Select(a => new
                {
                    objectPath = a.ObjectPath,
                    clrType = a.ClrType,
                    outcome = a.Outcome,
                    detail = a.Detail,
                }),
            },
            connection = plan.Connection is null ? null : new
            {
                probedWithoutConnecting = plan.Connection.ProbedWithoutConnecting,
                isConfigured = plan.Connection.IsConfigured,
                enableLegacyCommunication = plan.Connection.EnableLegacyCommunication,
                allTargetAddresses = plan.Connection.AllTargetAddresses,
                modes = plan.Connection.Modes.Select(m => new
                {
                    name = m.Name,
                    pcInterfaces = m.PcInterfaces.Select(p => new
                    {
                        name = p.Name,
                        number = p.Number,
                        addresses = p.Addresses.Select(a => new { name = a.Name, address = a.Address }),
                        subnets = p.Subnets,
                        targetInterfaces = p.TargetInterfaces.Select(t => new
                        {
                            name = t.Name,
                            addresses = t.Addresses.Select(a => new { name = a.Name, address = a.Address }),
                        }),
                    }),
                }),
            },
        };

        // The relaxed encoder, uniquely in this file. The default one escapes `<` and `>` as
        // </>, which would render this payload's single most important field —
        // `GetService<DownloadProvider>()`, the acquisition path this whole command exists to
        // establish — as unreadable mojibake. Still valid JSON, and this output is a report read by
        // people and scripts, never embedded in HTML, so the escaping bought nothing here.
        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
    }

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

        // The consistency read-back (2026-08-12). Printed for a per-item compile whether it passed or
        // failed, because "compiled clean" and "consistent" are different facts and the gap between
        // them is invisible: TIA refuses to EXPORT an inconsistent block, long after and far from the
        // compile that said it was fine. Absent for a whole-device/HMI compile, where there is no one
        // item to ask about.
        if (result.ConsistentAfterCompile is { } consistent)
        {
            sb.Append("CONSISTENT: ").Append(consistent ? "yes" : "NO").Append(
                consistent
                    ? "  (re-read after the compile; TIA will export this item)\n"
                    : "  — this item is STILL flagged inconsistent after compiling clean. TIA will REFUSE to\n" +
                      "  export it (\"Inconsistent blocks and PLC data types (UDT) cannot be exported\"). The usual\n" +
                      "  cause is something it references that does not exist yet; compile that first, then this.\n");
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

            // Always emitted, null included: a consumer keying on a missing property could not tell
            // "this build does not read consistency back" from "this compile had no single item to
            // ask about". Same reasoning as download-plan's `granularity` field.
            consistentAfterCompile = result.ConsistentAfterCompile,
            messages = result.Messages.Select(m => new
            {
                state = m.State.ToString(),
                description = m.Description,
                path = m.Path,
            }),
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    // FI-70. The one thing this report must never do is let a partial dump read as a whole one — the
    // directory is about to be handed to `converter drift-check --complete`, which treats a missing
    // file as "this block is not in the controller". So what was NOT produced leads, and the summary
    // states the conclusion in words rather than leaving it to be inferred from three counts.
    public static string FormatExportAllTable(ExportAllResult result)
    {
        var sb = new StringBuilder();
        sb.Append("OUT: ").Append(result.OutDir).Append('\n');

        foreach (var entry in result.Entries.Where(e => e.Outcome == ExportAllOutcome.Failed))
        {
            sb.Append("FAILED:  ").Append(entry.Name).Append("  (").Append(entry.Detail).Append(")\n");
        }

        foreach (var entry in result.Entries.Where(e => e.Outcome == ExportAllOutcome.Refused))
        {
            sb.Append("REFUSED: ").Append(entry.Name).Append("  (").Append(entry.Detail).Append(")\n");
        }

        sb.Append("SUMMARY: ").Append(result.ExportedCount).Append(" exported, ")
            .Append(result.RefusedCount).Append(" refused, ")
            .Append(result.FailedCount).Append(" failed\n");

        if (result.IsComplete)
        {
            sb.Append("COMPLETE: every block and type in the project was exported. Safe to compare against with\n")
                .Append("          converter drift-check --project <ir-dir> --exports ").Append(result.OutDir).Append(" --complete\n");
        }
        else
        {
            sb.Append("INCOMPLETE: this directory is NOT the whole project. Do not pass it to drift-check --complete\n")
                .Append("            as-is — that reads a missing file as 'this block is not in the controller', so the\n")
                .Append("            ").Append(result.RefusedCount + result.FailedCount)
                .Append(" item(s) above would come back as findings about the controller that are really\n")
                .Append("            findings about this dump.\n");
        }

        return sb.ToString().TrimEnd('\n', '\r');
    }

    public static string FormatExportAllJson(ExportAllResult result)
    {
        var payload = new
        {
            outDir = result.OutDir,
            complete = result.IsComplete,
            exported = result.ExportedCount,
            refused = result.RefusedCount,
            failed = result.FailedCount,
            entries = result.Entries.Select(e => new
            {
                name = e.Name,
                kind = e.Kind,
                path = e.Path,
                outPath = e.OutPath,
                outcome = e.Outcome.ToString(),
                detail = e.Detail,
            }),
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    // Mirrors FormatExportAllTable, and for the same reason: the failure mode of a restore is a
    // project that comes back looking whole. So what did NOT go in leads, and the multi-pass detail
    // is summarised rather than replayed — which pass a file landed on is only interesting for the
    // ones that needed more than the first.
    public static string FormatImportAllTable(ImportAllResult result)
    {
        var sb = new StringBuilder();
        sb.Append("GROUP: ").Append(result.GroupPath).Append('\n');

        foreach (var entry in result.Entries.Where(e => e.Outcome == ImportAllOutcome.Rejected))
        {
            sb.Append("REJECTED: ").Append(entry.Path).Append("  (").Append(entry.Detail).Append(")\n");
        }

        foreach (var entry in result.Entries.Where(e => e.Outcome == ImportAllOutcome.Failed))
        {
            sb.Append("FAILED:   ").Append(entry.Name).Append("  (").Append(entry.Detail).Append(")\n");
        }

        var late = result.Entries.Where(e => e.Outcome == ImportAllOutcome.Imported && e.Pass > 1).ToList();
        foreach (var entry in late)
        {
            sb.Append("RETRIED:  ").Append(entry.Name).Append("  (imported on pass ").Append(entry.Pass).Append(")\n");
        }

        sb.Append("SUMMARY: ").Append(result.ImportedCount).Append(" imported, ")
            .Append(result.FailedCount).Append(" failed, ")
            .Append(result.RejectedCount).Append(" rejected, in ")
            .Append(result.Passes).Append(" pass(es)\n");

        if (result.IsComplete)
        {
            sb.Append("COMPLETE: every file supplied is now in the project. This is NOT a compile gate — every\n")
                .Append("          imported block is flagged inconsistent until compiled. Run:\n")
                .Append("          openness-cli sanity-check <project>\n");
        }
        else
        {
            sb.Append("INCOMPLETE: the project does NOT contain everything that was supplied. A project missing a\n")
                .Append("            block opens, lists and can even pass a device compile, so this will not\n")
                .Append("            announce itself later — resolve the ")
                .Append(result.FailedCount + result.RejectedCount).Append(" item(s) above first.\n");
        }

        return sb.ToString().TrimEnd('\n', '\r');
    }

    public static string FormatImportAllJson(ImportAllResult result)
    {
        var payload = new
        {
            groupPath = result.GroupPath,
            complete = result.IsComplete,
            imported = result.ImportedCount,
            failed = result.FailedCount,
            rejected = result.RejectedCount,
            passes = result.Passes,
            entries = result.Entries.Select(e => new
            {
                name = e.Name,
                kind = e.Kind,
                path = e.Path,
                outcome = e.Outcome.ToString(),
                pass = e.Pass,
                detail = e.Detail,
            }),
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    public static string FormatImportAllPlanTable(ImportAllPlanner.Plan plan, string groupPath)
    {
        var sb = new StringBuilder();
        sb.Append("GROUP: ").Append(groupPath).Append('\n');
        sb.Append("PLAN (import order):\n");

        var order = 0;
        foreach (var file in plan.Files)
        {
            order++;
            sb.Append("  ").Append(order.ToString().PadLeft(3)).Append("  ")
                .Append(file.Kind.ToString().PadRight(10))
                .Append(file.RootElement.PadRight(24))
                .Append(file.Name).Append('\n');
        }

        foreach (var rejection in plan.Rejections)
        {
            sb.Append("REJECTED: ").Append(rejection.Path).Append("  (").Append(rejection.Reason).Append(")\n");
        }

        sb.Append("SUMMARY: ").Append(plan.Files.Count).Append(" file(s) would be imported, ")
            .Append(plan.Rejections.Count).Append(" rejected\n");

        return sb.ToString().TrimEnd('\n', '\r');
    }

    public static string FormatImportAllPlanJson(ImportAllPlanner.Plan plan, string groupPath)
    {
        var payload = new
        {
            groupPath,
            usable = plan.IsUsable,
            files = plan.Files.Select(f => new
            {
                path = f.Path,
                name = f.Name,
                kind = f.Kind.ToString(),
                rootElement = f.RootElement,
                phase = f.Phase,
            }),
            rejections = plan.Rejections.Select(r => new { path = r.Path, reason = r.Reason }),
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    // Errors and inconsistency get their own lines because they are different failures: compiled and
    // wrong, versus never examined. A single "failed" count would let the second hide in the first —
    // and the second is the one that looks like success (FI-52).
    public static string FormatCompileAllTable(CompileAllResult result)
    {
        var sb = new StringBuilder();

        foreach (var entry in result.Entries.Where(e => e.ErrorCount > 0))
        {
            sb.Append("ERRORS:   ").Append(entry.Name).Append("  (").Append(entry.ErrorCount).Append(" error(s)");
            if (!string.IsNullOrWhiteSpace(entry.Detail))
            {
                sb.Append(": ").Append(entry.Detail);
            }

            sb.Append(")\n");
        }

        foreach (var entry in result.Entries.Where(e => e.StillInconsistent && e.ErrorCount == 0))
        {
            sb.Append("UNCLEARED: ").Append(entry.Name).Append("  (compiled, still flagged inconsistent)\n");
        }

        sb.Append("SUMMARY: ").Append(result.CompiledCount).Append(" compiled, ")
            .Append(result.WithErrorsCount).Append(" with errors, ")
            .Append(result.StillInconsistentCount).Append(" still inconsistent, in ")
            .Append(result.Passes).Append(" pass(es)\n");

        // "Nothing was examined" must never render as "everything passed". An item that compiled with
        // errors is still flagged CONSISTENT, so the run right after a failed one has an empty work
        // set — the moment this report is most likely to be believed and least entitled to be.
        if (result.CompiledCount == 0)
        {
            sb.Append("NOTHING EXAMINED: no type or block is flagged inconsistent, so this run compiled nothing and\n")
                .Append("                 proves nothing about the project. Note that an item which compiled WITH ERRORS is\n")
                .Append("                 still flagged consistent, and errors do not survive the process — so a failed run\n")
                .Append("                 is followed by exactly this. Re-run with --force to compile every item.\n");
        }
        else
        {
            sb.Append(result.IsClean
                ? "CLEAN: every item compiled without errors and nothing is left flagged inconsistent.\n"
                : "NOT CLEAN: the items above were not cleared. An inconsistent block is one the gate did NOT examine,\n" +
                  "           which is the failure mode that looks like a pass (FI-52).\n");
        }

        return sb.ToString().TrimEnd('\n', '\r');
    }

    public static string FormatCompileAllJson(CompileAllResult result)
    {
        var payload = new
        {
            clean = result.IsClean,
            compiled = result.CompiledCount,
            withErrors = result.WithErrorsCount,
            stillInconsistent = result.StillInconsistentCount,
            passes = result.Passes,
            entries = result.Entries.Select(e => new
            {
                name = e.Name,
                kind = e.Kind,
                state = e.State.ToString(),
                errorCount = e.ErrorCount,
                warningCount = e.WarningCount,
                stillInconsistent = e.StillInconsistent,
                detail = e.Detail,
            }),
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    public static string FormatSanityCheckTable(SanityCheckResult result)
    {
        var sb = new StringBuilder();
        sb.Append("OVERALL: ").Append(result.IsHealthy ? "HEALTHY" : "ISSUES FOUND").Append('\n');
        sb.Append("BLOCKS: ").Append(result.TotalBlocks)
            .Append("  INCONSISTENT: ").Append(result.InconsistentBlocks.Count)
            // Printed ALWAYS, including at zero, for FI-62's reason applied to a new question: a
            // reader has to be able to see that duplicate numbers were examined. A count that only
            // appears when it is non-zero is indistinguishable from a check that does not exist —
            // and this check's whole history is that its absence looked exactly like a pass.
            .Append("  DUPLICATE NUMBERS: ").Append(result.DuplicateNumbers.Count).Append('\n');

        // FI-62: types get their own line, always printed even at zero. A reader who has only ever
        // seen BLOCKS/INCONSISTENT needs to see that types were actually looked at — a silent
        // absence is what let an inconsistent UDT pass a HEALTHY gate in the first place.
        sb.Append("TYPES: ").Append(result.TotalTypes).Append("  INCONSISTENT: ").Append(result.InconsistentTypes.Count).Append('\n');

        // First, ahead of the consistency lists, because it is the finding that outranks them. An
        // inconsistent block announces itself again on the next read; a duplicate number does not,
        // and the remedy for one is not the remedy for the other.
        if (result.DuplicateNumbers.Count > 0)
        {
            sb.Append('\n').Append(FormatDuplicateBlockNumbers(result.DuplicateNumbers, Array.Empty<string>()));
        }

        if (result.InconsistentBlocks.Count > 0)
        {
            sb.Append('\n').Append("Inconsistent blocks:\n");
            foreach (var block in result.InconsistentBlocks)
            {
                sb.Append("  ").Append(block.Path).Append('/').Append(block.Name).Append(" (").Append(block.Language).Append(")\n");
            }

            // FI-66 (2026-08-09). RE-READING IS NOT A FIXPOINT, AND THE OUTPUT USED TO IMPLY IT WAS.
            //
            // The working assumption on a live job was "run sanity-check again and the count drops to
            // 0". That holds for a block the PREVIOUS pass compiled, and is false for one that nothing
            // has compiled — measured: a block sat inconsistent across THREE consecutive reads while
            // never appearing in any import list. An agent following the documented loop can re-read
            // for ever and never reach INCONSISTENT: 0, with nothing saying why, and no prompt to
            // compile it because it is not in the import list.
            //
            // The device compile below does NOT clear these — that is FI-52's whole finding. So the
            // remedy has to be named here, next to the count, the way the types list already names
            // its own. A check that reports a number the documented remedy cannot reduce is the same
            // family as FI-52 and FI-62: output that implies an action which does not work.
            sb.Append("  -> a device compile does NOT clear these, and re-running sanity-check will not either.\n");
            sb.Append("     clear with: openness-cli compile <project> --block <name>   (per block, in dependency order)\n");
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

    /// <summary>
    /// The duplicate-number report, shared verbatim by `sanity-check` and by the post-write scan on
    /// the import path. One renderer rather than two, because the two must not be able to describe
    /// the same project differently — and because "there is a duplicate somewhere" is not actionable:
    /// what a reader needs is the type, the number, and EVERY block holding it.
    /// </summary>
    /// <param name="justWritten">
    /// Names written by the command that is reporting (empty for `sanity-check`, which wrote
    /// nothing). A group containing one of these is marked, so an operator can tell "the import I
    /// just ran collided" from "this project was already broken before I touched it" — two different
    /// situations with two different next moves.
    /// </param>
    public static string FormatDuplicateBlockNumbers(
        IReadOnlyList<DuplicateBlockNumber> duplicates, IReadOnlyList<string> justWritten)
    {
        var written = new HashSet<string>(justWritten, StringComparer.Ordinal);
        var sb = new StringBuilder();

        sb.Append("DUPLICATE BLOCK NUMBERS: ").Append(duplicates.Count)
            .Append(duplicates.Count == 1 ? " collision\n" : " collisions\n");

        foreach (var duplicate in duplicates)
        {
            sb.Append("  ").Append(duplicate.Device).Append(": ")
                .Append(duplicate.Type).Append(' ').Append(duplicate.Number)
                .Append(" is held by ").Append(duplicate.Blocks.Count).Append(" blocks:\n");

            foreach (var block in duplicate.Blocks)
            {
                sb.Append("      ").Append(block.Name)
                    .Append("  (").Append(block.Path).Append(", ").Append(block.Language).Append(')');
                if (block.IsSafety)
                {
                    sb.Append("  [SAFETY]");
                }

                if (written.Contains(block.Name))
                {
                    sb.Append("  <- written by this command");
                }

                sb.Append('\n');
            }
        }

        // Both halves are load-bearing. The first says why no other output has mentioned this; the
        // second says what to do, because nothing this tool runs automatically will clear it.
        sb.Append("  -> TIA ACCEPTS THIS. Measured: a colliding import exited 0, the per-block compile reported\n")
            .Append("     \"successfully compiled\" with CONSISTENT: yes, and the device compile reported Success,\n")
            .Append("     errors=0. Consistency and compilation do not answer this question.\n");
        sb.Append("  -> nothing here clears it, and re-running will not. Delete or renumber one of the blocks\n")
            .Append("     (`openness-cli delete <project> --block <name> --yes`, or renumber in TIA Portal), then\n")
            .Append("     re-import and re-run this check.\n");

        return sb.ToString();
    }

    /// <summary>
    /// The survey's whole payload is the IDENTITY column, so the table leads with it. A reader
    /// scanning this needs one number — how many distinct compilers — and then which routes reach
    /// which; the CLR type names are supporting evidence and sit underneath.
    /// </summary>
    public static string FormatCompileScopesTable(CompileScopeSurvey survey)
    {
        var sb = new StringBuilder();
        sb.Append($"DISTINCT COMPILERS: {survey.DistinctCompilers}   ENTRY POINTS OFFERING ONE: ")
            .Append(survey.Scopes.Count(s => s.HasCompilable))
            .Append($"   OBJECTS ASKED: {survey.Scopes.Count}\n");
        sb.Append("READ-ONLY: nothing was compiled.\n\n");

        var width = Math.Max(5, survey.Scopes.Max(s => s.Label.Length));
        sb.Append("GROUP  ").Append("SCOPE".PadRight(width)).Append("  COMPILER / WHY NOT\n");
        sb.Append("-----  ").Append(new string('-', width)).Append("  ------------------\n");
        foreach (var scope in survey.Scopes)
        {
            var group = scope.IdentityGroup is int g ? $"#{g}".PadRight(5) : "  -  ";
            var right = scope.HasCompilable
                ? $"{Short(scope.CompilableType)} (Parent: {Short(scope.ParentType)}{(scope.ParentName is null ? string.Empty : $" '{scope.ParentName}'")})"
                : scope.Error ?? "no ICompilable service";
            sb.Append(group).Append("  ").Append(scope.Label.PadRight(width)).Append("  ").Append(right).Append('\n');
            if (!scope.HasCompilable)
            {
                continue;
            }

            // Printed even when empty, and labelled as a measurement. Compile() takes no arguments,
            // so an empty list here is the evidence that a rebuild-all cannot be REQUESTED through
            // this object — which is a finding, not an absence of one.
            sb.Append("       ").Append("attributes : ")
                .Append(scope.Attributes.Count == 0 ? "(none — measured, not assumed)" : string.Join(", ", scope.Attributes)).Append('\n');
            sb.Append("       ").Append("invocations: ")
                .Append(scope.Invocations.Count == 0 ? "(none — measured, not assumed)" : string.Join(", ", scope.Invocations)).Append('\n');
        }

        sb.Append('\n');
        if (survey.DistinctCompilers <= 1)
        {
            sb.Append("Every route reaches the SAME compiler, so there is no scope this tool could be missing.\n");
            return sb.ToString();
        }

        // The point of the whole command. Stated as a question to answer, not as a defect: a second
        // compiler is only a gap if it finds something the first does not, and counting cannot say.
        sb.Append($"*** {survey.DistinctCompilers} DIFFERENT COMPILERS. Same GROUP = same object reached two ways; different\n")
            .Append("    GROUPS = different compiles. `openness-cli compile` uses the DeviceItem's, and reaches\n")
            .Append("    the PlcSoftware's only if the DeviceItem has none — which, where both exist, is never.\n")
            .Append("    Whether the other one finds anything this one misses is answered by RUNNING it\n")
            .Append("    (`openness-cli compile <project> --software`), never by this table. ***\n");
        return sb.ToString();

        static string Short(string? clrType) => clrType is null
            ? "(none)"
            : clrType.Substring(clrType.LastIndexOf('.') + 1);
    }

    public static string FormatCompileScopesJson(CompileScopeSurvey survey)
    {
        var payload = new
        {
            distinctCompilers = survey.DistinctCompilers,
            objectsAsked = survey.Scopes.Count,
            entryPoints = survey.Scopes.Count(s => s.HasCompilable),
            scopes = survey.Scopes.Select(s => new
            {
                label = s.Label,
                ownerType = s.OwnerType,
                hasCompilable = s.HasCompilable,
                compilableType = s.CompilableType,
                parentType = s.ParentType,
                parentName = s.ParentName,
                identityGroup = s.IdentityGroup,
                attributes = s.Attributes,
                invocations = s.Invocations,
                error = s.Error,
            }),
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
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
            duplicateBlockNumbers = result.DuplicateNumbers.Select(d => new
            {
                device = d.Device,
                type = d.Type.ToString(),
                number = d.Number,
                blocks = d.Blocks.Select(b => new { name = b.Name, path = b.Path, language = b.Language, isSafety = b.IsSafety }),
            }),
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
