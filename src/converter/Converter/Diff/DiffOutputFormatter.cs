using System.Text;
using System.Text.Json;

namespace Converter.Diff;

// Mirrors ReviewOutputFormatter/PreflightOutputFormatter/TagStatusOutputFormatter's
// "one record, two renderers" pattern.
public static class DiffOutputFormatter
{
    public static string FormatText(DiffReport report)
    {
        var sb = new StringBuilder();

        if (report.BlockNameMismatch)
        {
            sb.Append("WARNING: block name changed: '").Append(report.OtherBlockName)
              .Append("' -> '").Append(report.BlockName).Append("' (diffing by network number regardless)\n");
        }

        sb.Append("SUMMARY: ").Append(report.Networks.Count).Append(" network(s): ")
          .Append(report.ChangedCount).Append(" changed, ")
          .Append(report.AddedCount).Append(" added, ")
          .Append(report.RemovedCount).Append(" removed, ")
          .Append(report.IdenticalCount).Append(" identical\n");

        if (report.Header.Changed)
        {
            sb.Append("HEADER changed:");
            if (report.Header.TitleChanged)
            {
                sb.Append(" title(").Append(report.Header.TitleBefore ?? "<none>").Append(" -> ")
                  .Append(report.Header.TitleAfter ?? "<none>").Append(')');
            }

            if (report.Header.CommentChanged)
            {
                sb.Append(" comment");
            }

            if (report.Header.InterfaceChanged)
            {
                sb.Append(" interface");
            }

            sb.Append('\n');
        }

        foreach (var network in report.Networks.Where(n => n.Kind != NetworkChangeKind.Identical))
        {
            sb.Append('\n').Append(KindLabel(network.Kind)).Append(" network ").Append(network.Number);
            if (!string.IsNullOrEmpty(network.Title))
            {
                sb.Append(" \"").Append(network.Title).Append('"');
            }

            sb.Append('\n');

            if (network.BeforeText is not null)
            {
                AppendIndented(sb, "- ", network.BeforeText);
            }

            if (network.AfterText is not null)
            {
                AppendIndented(sb, "+ ", network.AfterText);
            }
        }

        if (report.AllowedNetworks.Count > 0)
        {
            if (report.HasInvarianceViolation)
            {
                sb.Append("\nINVARIANCE VIOLATION: change(s) outside --only {")
                  .Append(string.Join(", ", report.AllowedNetworks.OrderBy(n => n)))
                  .Append("}: network(s) ")
                  .Append(string.Join(", ", report.InvarianceViolations.Select(v => v.Number)))
                  .Append('\n');
            }
            else
            {
                sb.Append("\nINVARIANCE OK: all changes confined to --only {")
                  .Append(string.Join(", ", report.AllowedNetworks.OrderBy(n => n)))
                  .Append("}\n");
            }
        }

        return sb.ToString().TrimEnd('\n', '\r');
    }

    public static string FormatJson(DiffReport report)
    {
        var payload = new
        {
            blockName = report.BlockName,
            blockNameMismatch = report.BlockNameMismatch,
            otherBlockName = report.OtherBlockName,
            hasAnyChange = report.HasAnyChange,
            header = new
            {
                changed = report.Header.Changed,
                titleChanged = report.Header.TitleChanged,
                titleBefore = report.Header.TitleBefore,
                titleAfter = report.Header.TitleAfter,
                commentChanged = report.Header.CommentChanged,
                commentBefore = report.Header.CommentBefore,
                commentAfter = report.Header.CommentAfter,
                interfaceChanged = report.Header.InterfaceChanged,
            },
            summary = new
            {
                total = report.Networks.Count,
                changed = report.ChangedCount,
                added = report.AddedCount,
                removed = report.RemovedCount,
                identical = report.IdenticalCount,
            },
            networks = report.Networks.Select(n => new
            {
                number = n.Number,
                kind = n.Kind.ToString(),
                title = n.Title,
                beforeText = n.BeforeText,
                afterText = n.AfterText,
            }),
            allowedNetworks = report.AllowedNetworks,
            invarianceViolations = report.InvarianceViolations.Select(n => n.Number),
            hasInvarianceViolation = report.HasInvarianceViolation,
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string KindLabel(NetworkChangeKind kind) => kind switch
    {
        NetworkChangeKind.Changed => "CHANGED",
        NetworkChangeKind.Added => "ADDED",
        NetworkChangeKind.Removed => "REMOVED",
        NetworkChangeKind.Identical => "IDENTICAL",
        _ => kind.ToString().ToUpperInvariant(),
    };

    private static void AppendIndented(StringBuilder sb, string marker, string text)
    {
        foreach (var line in text.Split('\n'))
        {
            if (line.Length == 0)
            {
                continue;
            }

            sb.Append("  ").Append(marker).Append(line).Append('\n');
        }
    }
}
