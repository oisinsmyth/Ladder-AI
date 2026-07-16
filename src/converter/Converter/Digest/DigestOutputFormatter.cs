using System.Text;
using System.Text.Json;

namespace Converter.Digest;

// Mirrors ReviewOutputFormatter's "one record, two renderers" pattern.
public static class DigestOutputFormatter
{
    public static string FormatText(DigestReport report)
    {
        var sb = new StringBuilder();

        foreach (var file in report.Files)
        {
            sb.Append("FILE: ").Append(file.FilePath).Append('\n');

            if (file.FileError is not null)
            {
                sb.Append("  COULD NOT DIGEST: ").Append(file.FileError).Append('\n');
                sb.Append('\n');
                continue;
            }

            sb.Append("  KIND: ").Append(file.Kind).Append("  NAME: ").Append(file.Name);
            if (file.Number is int number)
            {
                sb.Append("  NUMBER: ").Append(number);
            }

            if (file.InstanceOfName is not null)
            {
                sb.Append("  INSTANCEOF: ").Append(file.InstanceOfName);
            }

            sb.Append('\n');

            if (!string.IsNullOrEmpty(file.Title))
            {
                sb.Append("  TITLE: ").Append(file.Title).Append('\n');
            }

            foreach (var section in file.Sections)
            {
                sb.Append("  ").Append(section.Section).Append(" (").Append(section.Members.Count).Append(")\n");
                foreach (var member in section.Members)
                {
                    sb.Append("    ").Append(member).Append('\n');
                }
            }

            if (file.Calls.Count > 0)
            {
                sb.Append("  CALLS\n");
                foreach (var call in file.Calls)
                {
                    sb.Append("    ").Append(call.BlockName).Append(" x").Append(call.CallCount);
                    if (call.Instances.Count > 0)
                    {
                        sb.Append(" (instances: ").Append(string.Join(", ", call.Instances)).Append(')');
                    }

                    sb.Append('\n');
                }
            }

            if (file.Networks.Count > 0)
            {
                sb.Append("  NETWORKS (").Append(file.Networks.Count).Append(")\n");
                foreach (var network in file.Networks)
                {
                    sb.Append("    ").Append(network.Number).Append(" \"").Append(network.Title).Append("\" ").Append(network.Statements).Append('\n');
                }
            }

            if (file.TagRoots.Count > 0)
            {
                sb.Append("  TAG ROOTS (").Append(file.TagRoots.Count).Append("): ").Append(string.Join(", ", file.TagRoots)).Append('\n');
            }

            sb.Append('\n');
        }

        var couldNotDigest = report.Files.Count(f => f.FileError is not null);
        sb.Append("SUMMARY: ").Append(report.Files.Count).Append(" file(s)");
        if (couldNotDigest > 0)
        {
            sb.Append(", ").Append(couldNotDigest).Append(" file(s) could not be digested");
        }

        sb.Append('\n');

        return sb.ToString().TrimEnd('\n', '\r');
    }

    public static string FormatJson(DigestReport report)
    {
        var payload = new
        {
            files = report.Files.Select(f => new
            {
                filePath = f.FilePath,
                kind = f.Kind,
                name = f.Name,
                number = f.Number,
                instanceOfName = f.InstanceOfName,
                title = f.Title,
                fileError = f.FileError,
                sections = f.Sections.Select(s => new { section = s.Section, members = s.Members }),
                calls = f.Calls.Select(c => new { blockName = c.BlockName, callCount = c.CallCount, instances = c.Instances }),
                networks = f.Networks.Select(n => new { number = n.Number, title = n.Title, statements = n.Statements }),
                tagRoots = f.TagRoots,
            }),
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }
}
