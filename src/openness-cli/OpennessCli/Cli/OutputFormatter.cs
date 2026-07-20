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
