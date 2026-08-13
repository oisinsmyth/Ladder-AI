using System.Text;
using System.Text.Json;

namespace Converter.Compare;

// One record set, two renderers (the Digest/TagStatus/DriftCheck pattern).
public static class CompareOutputFormatter
{
    private const int ValueDisplayLimit = 160;

    public static string FormatText(CompareReport report, int maxDifferences)
    {
        var sb = new StringBuilder();

        sb.Append("FIRST : ").Append(report.FirstPath).Append('\n');
        sb.Append("SECOND: ").Append(report.SecondPath).Append('\n');

        if (report.Status == CompareStatus.NotCompared)
        {
            sb.Append("NOT COMPARED: ").Append(report.Detail).Append('\n');
            sb.Append("VERDICT: NOT COMPARED — this is not a pass. Nothing was proven about the round trip.\n");
            return sb.ToString().TrimEnd('\n', '\r');
        }

        // The parenthetical states WHY, and it used to lie whenever exactly one side was silent: it
        // printed "neither document declares one" directly beside a value the other document plainly
        // declared, so a reader skimming it concluded no layout existed anywhere. The skip was right;
        // the reason given was false. A diagnostic that misstates its own reasoning is worse than one
        // that says less — the Normalizer holds NEITHER side to the other's value when EITHER is
        // silent, which is the fact to print.
        sb.Append("MEMORYLAYOUT: ")
            .Append(Describe(report.MemoryLayout.First)).Append(" -> ").Append(Describe(report.MemoryLayout.Second))
            .Append(report.MemoryLayout.Compared
                ? "  (compared — both documents declare one)"
                : report.MemoryLayout.First is null && report.MemoryLayout.Second is null
                    ? "  (NOT compared — neither document declares one)"
                    : $"  (NOT compared — {(report.MemoryLayout.First is null ? "FIRST" : "SECOND")} declares none, " +
                      "so the other side's value is not asserted against it)")
            .Append('\n');

        if (report.Status == CompareStatus.Equivalent)
        {
            sb.Append("VERDICT: EQUIVALENT — semantically identical after normalization.\n");
            return sb.ToString().TrimEnd('\n', '\r');
        }

        var shown = maxDifferences <= 0
            ? report.Differences
            : report.Differences.Take(maxDifferences).ToList();

        foreach (var difference in shown)
        {
            sb.Append(Label(difference.Kind)).Append(": ").Append(difference.Path).Append('\n');

            // Without this line the two renderings below look like a rewiring rather than a
            // reversal — which is the whole reason this difference kind exists.
            if (difference.Kind == DifferenceKind.WireDirectionDiffers)
            {
                sb.Append("    the SAME endpoints with the producer and consumer roles REVERSED. A wire's FIRST\n")
                  .Append("    endpoint is its producer; nothing else in the document encodes direction.\n");
            }

            if (difference.First is not null)
            {
                sb.Append("    first : ").Append(Truncate(difference.First)).Append('\n');
            }

            if (difference.Second is not null)
            {
                sb.Append("    second: ").Append(Truncate(difference.Second)).Append('\n');
            }
        }

        if (shown.Count < report.Differences.Count)
        {
            sb.Append("... ").Append(report.Differences.Count - shown.Count)
                .Append(" further difference(s) not shown (--max-differences 0 for all)\n");
        }

        sb.Append("VERDICT: DIFFERS — ").Append(report.Differences.Count).Append(" difference(s).\n");
        sb.Append("NOTE: paths are in the NORMALIZED document. A UId value shown above is a content-derived key\n")
            .Append("      the Normalizer substituted for the file's own number — TIA reassigns raw UIds unprompted,\n")
            .Append("      so the number in the file identifies nothing across two exports.\n");

        return sb.ToString().TrimEnd('\n', '\r');
    }

    public static string FormatJson(CompareReport report)
    {
        var payload = new
        {
            first = report.FirstPath,
            second = report.SecondPath,
            status = report.Status.ToString(),
            detail = report.Detail,
            memoryLayout = new
            {
                first = report.MemoryLayout.First,
                second = report.MemoryLayout.Second,
                compared = report.MemoryLayout.Compared,
            },
            differenceCount = report.Differences.Count,
            differences = report.Differences.Select(d => new
            {
                kind = d.Kind.ToString(),
                path = d.Path,
                first = d.First,
                second = d.Second,
            }),
            equivalent = report.Equivalent,
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string Describe(string? layout) => layout ?? "(none declared)";

    private static string Label(DifferenceKind kind) => kind switch
    {
        DifferenceKind.ElementMissing => "ELEMENT-MISSING (in first, gone from second)",
        DifferenceKind.ElementAdded => "ELEMENT-ADDED   (not in first, present in second)",
        DifferenceKind.ValueDiffers => "VALUE-DIFFERS  ",
        DifferenceKind.AttributeMissing => "ATTR-MISSING   ",
        DifferenceKind.AttributeAdded => "ATTR-ADDED     ",
        DifferenceKind.AttributeDiffers => "ATTR-DIFFERS   ",
        DifferenceKind.RootElementDiffers => "ROOT-DIFFERS   ",
        DifferenceKind.WireDirectionDiffers => "WIRE-DIRECTION ",
        DifferenceKind.SectionOrderDiffers => "SECTION-ORDER  ",
        _ => kind.ToString(),
    };

    private static string Truncate(string value)
    {
        var flattened = value.Replace("\r", string.Empty).Replace('\n', ' ');
        return flattened.Length <= ValueDisplayLimit
            ? flattened
            : flattened[..ValueDisplayLimit] + "… (+" + (flattened.Length - ValueDisplayLimit) + " chars)";
    }
}
