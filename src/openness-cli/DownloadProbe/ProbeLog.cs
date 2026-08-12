using System;
using System.Collections.Generic;
using System.IO;

namespace DownloadProbe;

/// <summary>
/// Tees every line to a console writer and to a file, and never abbreviates.
///
/// The file matters more than the console here. This tool's whole product is the list of
/// configurations the API raised; a run that aborts mid-way still produced the answer, and console
/// scrollback is not an artifact. Nothing is deduplicated, summarised or truncated — a repeated
/// configuration is a fact about the download, not noise.
/// </summary>
internal sealed class ProbeLog : IDisposable
{
    private readonly TextWriter _console;
    private readonly StreamWriter? _file;
    private readonly List<string> _lines = new();

    internal ProbeLog(TextWriter console, string? filePath)
    {
        _console = console;
        FilePath = filePath;

        if (filePath is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            // New file, never append to someone else's run: two runs' configuration lists merged into
            // one file would be indistinguishable from one run raising everything twice.
            _file = new StreamWriter(new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            {
                AutoFlush = true,
            };
        }
    }

    internal string? FilePath { get; }

    /// <summary>Everything written, in order. The JSON report embeds it verbatim.</summary>
    internal IReadOnlyList<string> Lines => _lines;

    internal void Line(string text)
    {
        _lines.Add(text);
        _console.WriteLine(text);
        _file?.WriteLine(text);
    }

    internal void Block(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            Line(line);
        }
    }

    internal void Blank() => Line(string.Empty);

    internal void Rule(string title) => Line("==== " + title + " " + new string('=', Math.Max(0, 74 - title.Length)));

    /// <summary>
    /// Writes a multi-line block one line at a time, so an embedded newline in a Siemens message
    /// cannot break the file's line structure while still printing every character of it.
    /// </summary>
    internal void Verbatim(string prefix, string? text)
    {
        if (text is null)
        {
            Line(prefix + "(null)");
            return;
        }

        var parts = text.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < parts.Length; i++)
        {
            Line(prefix + (i == 0 ? string.Empty : "  | ") + parts[i]);
        }
    }

    public void Dispose() => _file?.Dispose();

    /// <summary>
    /// A per-run file name that cannot collide: option and timestamp, created with
    /// <c>FileMode.CreateNew</c> so an accidental reuse is an error rather than an overwrite.
    /// </summary>
    internal static string BuildFileName(DownloadOptionChoice options, DateTime nowUtc) =>
        $"download-probe-{options}-{nowUtc:yyyyMMdd-HHmmss}Z.log";
}
