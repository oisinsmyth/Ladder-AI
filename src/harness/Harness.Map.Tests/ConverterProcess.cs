using System.Diagnostics;

namespace Harness.Map.Tests;

/// <summary>
/// The real <c>converter</c> binary, as a second authority over what this component emits. A generated
/// document the converter refuses is worth nothing however well it reads, and this component's own tests
/// cannot fail that way — they compare the generator's output against what its author expected.
///
/// <para><b>Not the compile gate</b> (hard rule 4): <c>to-xml</c> proves the document is well-formed to
/// the converter; only TIA's own import proves it is acceptable to TIA.</para>
/// </summary>
internal static class ConverterProcess
{
    /// <summary>
    /// Release first (what the skills invoke), Debug second. <b>Absent is a FAILURE, not a skip</b> — an
    /// optional check is one that stops running.
    /// </summary>
    public static string Exe
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);

            while (dir is not null)
            {
                foreach (var configuration in new[] { "Release", "Debug" })
                {
                    var candidate = Path.Combine(
                        dir.FullName, "src", "converter", "Converter", "bin", configuration, "net8.0", "converter.exe");
                    if (File.Exists(candidate))
                        return candidate;
                }

                dir = dir.Parent;
            }

            throw new InvalidOperationException(
                "converter.exe was not found by walking up from " + AppContext.BaseDirectory
                + ". *** THIS IS A FAILURE, NOT A REASON TO SKIP. *** Build it with: "
                + "dotnet build -c Release src/converter/converter.sln (free and safe at any time — the converter never "
                + "touches Portal). Without it the only authority left in this component's loop is the component itself.");
        }
    }

    /// <summary>
    /// Redirected, never piped — and safe here: the converter is a pure in-process file transformer with
    /// no child process, so it carries none of <c>openness-cli</c>'s inherited-handle hazard.
    /// </summary>
    public static (int Exit, string Output) Run(params string[] args)
    {
        var info = new ProcessStartInfo(Exe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var arg in args)
            info.ArgumentList.Add(arg);

        using var process = Process.Start(info) ?? throw new InvalidOperationException("converter.exe did not start.");
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, output);
    }
}
