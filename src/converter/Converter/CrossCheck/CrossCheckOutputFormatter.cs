using System.Text;
using System.Text.Json;

namespace Converter.CrossCheck;

// Facts-only dump the review skills embed verbatim (same register as the `converter review` output),
// then reason over. No verdicts, no severities. One record set, two renderers.
public static class CrossCheckOutputFormatter
{
    public static string FormatText(CrossCheckReport report)
    {
        var sb = new StringBuilder();

        sb.Append("== MULTI-WRITER PATHS (C-308: >1 writer — the AI judges legit S/R pairs vs conflicts) ==\n");
        foreach (var m in report.MultiWriters)
        {
            sb.Append("  ").Append(m.Path).Append(": ")
                .Append(string.Join(", ", m.Writers.Select(w => $"{w.Block} N{w.Network} ({w.Kind})"))).Append('\n');
        }

        sb.Append("== DEAD MEMBERS (dead-wiring: no writer and/or no reader — global-DB + FB interface-UDT) ==\n");
        foreach (var d in report.DeadMembers)
        {
            var scope = d.Scope == DeadMemberScope.InterfaceMember ? "interface" : "global-db";
            var dir = d.Writers.Count == 0 && d.Readers.Count == 0 ? "unused (no writer, no reader)"
                : d.Writers.Count == 0 ? "consumed-but-never-written; readers: " + string.Join(", ", d.Readers.Select(r => $"{r.Block} N{r.Network}"))
                : "written-but-never-consumed; writers: " + string.Join(", ", d.Writers.Select(w => $"{w.Block} N{w.Network}"));
            sb.Append("  ").Append(d.Path).Append(" [").Append(scope).Append("]: ").Append(dir).Append('\n');
        }

        sb.Append("== PHYSICAL-IO REFERENCES (C-304: logic touches IO only in Map FCs — AI excludes those) ==\n");
        foreach (var io in report.IoBoundary)
        {
            sb.Append("  ").Append(io.Block).Append(": ").Append(io.Path).Append(" (").Append(io.Direction).Append(")\n");
        }

        sb.Append("== SIBLING REFERENCES (C-127: a reusable equipment FB must contain none) ==\n");
        foreach (var s in report.SiblingRefs)
        {
            var parts = new List<string>();
            if (s.Calls.Count > 0)
            {
                parts.Add("calls: " + string.Join(", ", s.Calls));
            }

            if (s.InstanceDbRoots.Count > 0)
            {
                parts.Add("iDB refs: " + string.Join(", ", s.InstanceDbRoots));
            }

            sb.Append("  ").Append(s.Block).Append(": ").Append(string.Join("; ", parts)).Append('\n');
        }

        foreach (var w in report.Warnings)
        {
            sb.Append("INDEX WARNING: ").Append(w).Append('\n');
        }

        sb.Append("SUMMARY: ").Append(report.MultiWriters.Count).Append(" multi-writer, ")
            .Append(report.DeadMembers.Count).Append(" dead-member, ")
            .Append(report.IoBoundary.Count).Append(" io-ref, ")
            .Append(report.SiblingRefs.Count).Append(" block(s) with sibling refs\n");

        return sb.ToString().TrimEnd('\n', '\r');
    }

    public static string FormatJson(CrossCheckReport report)
    {
        var payload = new
        {
            multiWriters = report.MultiWriters.Select(m => new
            {
                path = m.Path,
                writers = m.Writers.Select(w => new { block = w.Block, network = w.Network, kind = w.Kind }),
            }),
            deadMembers = report.DeadMembers.Select(d => new
            {
                path = d.Path,
                scope = d.Scope.ToString(),
                writers = d.Writers.Select(w => new { block = w.Block, network = w.Network, kind = w.Kind }),
                readers = d.Readers.Select(r => new { block = r.Block, network = r.Network }),
            }),
            ioBoundary = report.IoBoundary.Select(io => new { block = io.Block, path = io.Path, direction = io.Direction }),
            siblingRefs = report.SiblingRefs.Select(s => new { block = s.Block, calls = s.Calls, instanceDbRoots = s.InstanceDbRoots }),
            warnings = report.Warnings,
            // FI-67: JSON ONLY, DELIBERATELY. Most members have exactly one writer, so this is the
            // largest table in the report by a wide margin — printing it in the human view would
            // drown the four fact tables a reader actually scans. It exists to be queried (which
            // members lose their only writer if I delete this?), and a query wants JSON.
            soleWriters = report.SoleWriters.Select(s => new
            {
                path = s.Path,
                writer = new { block = s.Writer.Block, network = s.Writer.Network, kind = s.Writer.Kind },
                readers = s.Readers.Select(r => new { block = r.Block, network = r.Network }),
            }),
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }
}
