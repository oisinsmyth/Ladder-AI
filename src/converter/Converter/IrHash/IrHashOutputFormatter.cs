using System.Text;
using System.Text.Json;

namespace Converter.IrHash;

// Mirrors Digest/TagStatus/ReuseScan "one record, two renderers". Text is one `<file> -> <hash>`
// line per input (errors as `<file> -> ERROR: ...`); JSON is the machine form a sidecar-validation
// step consumes.
public static class IrHashOutputFormatter
{
    public static string FormatText(IrHashReport report)
    {
        var sb = new StringBuilder();
        foreach (var entry in report.Entries)
        {
            sb.Append(entry.File).Append(" -> ");
            sb.Append(entry.IsError ? $"ERROR: {entry.Error}" : entry.Hash);
            sb.Append('\n');
        }

        return sb.ToString().TrimEnd('\n', '\r');
    }

    public static string FormatJson(IrHashReport report)
    {
        var payload = new
        {
            hashes = report.Entries.Select(e => new
            {
                file = e.File,
                hash = e.Hash,
                error = e.Error,
            }),
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }
}
