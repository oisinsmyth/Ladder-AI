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
    private static readonly string[] Headers = { "TYPE", "NUMBER", "NAME", "LANGUAGE", "SAFETY", "PATH" };

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
            path = b.Path,
        });

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string[] ToRow(BlockInfo b) => new[]
    {
        b.Type.ToString(),
        b.Number.ToString(CultureInfo.InvariantCulture),
        b.Name,
        b.Language,
        b.IsSafety ? "SAFETY" : string.Empty,
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
