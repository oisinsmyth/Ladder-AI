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

    public static string FormatCompileTable(CompileResult result)
    {
        var sb = new StringBuilder();
        sb.Append("STATE: ").Append(result.State).Append('\n');
        sb.Append("ERRORS: ").Append(result.ErrorCount).Append("  WARNINGS: ").Append(result.WarningCount).Append('\n');

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

        if (result.InconsistentBlocks.Count > 0)
        {
            sb.Append('\n').Append("Inconsistent blocks:\n");
            foreach (var block in result.InconsistentBlocks)
            {
                sb.Append("  ").Append(block.Path).Append('/').Append(block.Name).Append(" (").Append(block.Language).Append(")\n");
            }
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
