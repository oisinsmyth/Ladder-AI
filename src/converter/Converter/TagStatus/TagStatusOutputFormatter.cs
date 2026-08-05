using System.Text;
using System.Text.Json;

namespace Converter.TagStatus;

// Mirrors ReviewOutputFormatter/PreflightOutputFormatter's "one record, two renderers" pattern.
public static class TagStatusOutputFormatter
{
    public static string FormatText(TagStatusReport report)
    {
        var sb = new StringBuilder();

        foreach (var e in report.Entries)
        {
            sb.Append(e.Name).Append(" -> ").Append(Label(e.Status));
            if (e.Root != e.Name)
            {
                sb.Append(" (root: ").Append(e.Root).Append(')');
            }

            sb.Append('\n');
        }

        foreach (var warning in report.IndexWarnings)
        {
            sb.Append("INDEX WARNING: ").Append(warning).Append('\n');
        }

        var proposed = Count(report, TagStatusKind.Proposed);
        var memberNotFound = Count(report, TagStatusKind.MemberNotFound);
        var memberUnchecked = Count(report, TagStatusKind.MemberUnchecked);

        sb.Append("SUMMARY: ").Append(report.Entries.Count).Append(" name(s), ")
            .Append(proposed).Append(" proposed, ")
            .Append(memberNotFound).Append(" member-not-found, ")
            .Append(memberUnchecked).Append(" member-unchecked\n");

        return sb.ToString().TrimEnd('\n', '\r');
    }

    public static string FormatJson(TagStatusReport report)
    {
        var payload = new
        {
            names = report.Entries.Select(e => new
            {
                name = e.Name,
                root = e.Root,
                status = Label(e.Status),
                // Retained so existing consumers keep working; `status` is the precise field.
                exists = e.Exists,
                blocking = e.IsBlocking,
            }),
            indexWarnings = report.IndexWarnings,
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    private static int Count(TagStatusReport report, TagStatusKind kind) =>
        report.Entries.Count(e => e.Status == kind);

    private static string Label(TagStatusKind kind) => kind switch
    {
        TagStatusKind.Exists => "EXISTS",
        TagStatusKind.Proposed => "PROPOSED",
        TagStatusKind.MemberNotFound => "MEMBER-NOT-FOUND",
        TagStatusKind.MemberUnchecked => "MEMBER-UNCHECKED",
        _ => "UNKNOWN",
    };
}
