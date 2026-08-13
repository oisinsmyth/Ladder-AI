using System.Diagnostics;
using System.Text;

namespace Harness.Device;

/// <summary>What one invocation of an external binary produced.</summary>
/// <param name="Started">
/// Whether the process was launched at all. <b>False is not a non-zero exit code</b> — a binary that
/// was never started says nothing about the project, and the two must not collapse.
/// </param>
/// <param name="TimedOut">
/// Whether the wait expired. A timed-out Portal command has NOT necessarily failed — it may still be
/// holding the project — so this is carried separately from the exit code rather than folded into it.
/// </param>
public sealed record ProcessResult(bool Started, bool TimedOut, int ExitCode, string StandardOutput, string StandardError, string Detail)
{
    public static ProcessResult NotStarted(string detail) =>
        new(false, false, -1, string.Empty, string.Empty, detail);
}

/// <summary>
/// The seam between the gateway's ORDERING (pure, tested) and the act of running a binary (not).
///
/// <para>It exists so that the plan, the exit-code interpretation, the layout re-assertion sequence
/// and the manifest parse can all be exercised with no Portal, no TIA and no controller — while the
/// thing being exercised is the same argument vector a rig session would type.</para>
/// </summary>
public interface IProcessRunner
{
    ProcessResult Run(string executable, IReadOnlyList<string> arguments, TimeSpan timeout);
}

/// <summary>
/// Windows argument quoting, in one place, because getting it wrong turns a group path containing a
/// space — which every real one does, e.g. <c>PLC1 6ES7 214-1AG40-0XB0/Program blocks</c> — into
/// three arguments and a confusing "No device item found under '...'".
/// </summary>
public static class CommandLine
{
    /// <summary>Quote one argument for the Windows command line (CreateProcess rules).</summary>
    public static string Quote(string argument)
    {
        ArgumentNullException.ThrowIfNull(argument);

        if (argument.Length > 0 && argument.IndexOfAny(new[] { ' ', '\t', '"', '\n' }) < 0)
            return argument;

        var sb = new StringBuilder("\"");
        for (var i = 0; i < argument.Length; i++)
        {
            var backslashes = 0;
            while (i < argument.Length && argument[i] == '\\')
            {
                backslashes++;
                i++;
            }

            if (i == argument.Length)
            {
                sb.Append('\\', backslashes * 2);
                break;
            }

            if (argument[i] == '"')
            {
                sb.Append('\\', (backslashes * 2) + 1).Append('"');
            }
            else
            {
                sb.Append('\\', backslashes).Append(argument[i]);
            }
        }

        return sb.Append('"').ToString();
    }

    /// <summary>The whole command as one readable line — what the rig-session document prints.</summary>
    public static string Render(string executable, IReadOnlyList<string> arguments) =>
        string.Join(" ", new[] { Quote(executable) }.Concat(arguments.Select(Quote)));
}

/// <summary>
/// The real runner.
///
/// <para>*** IT REDIRECTS TO FILES, NEVER TO PIPES, AND THAT IS NOT A STYLE CHOICE. ***
/// <c>openness-cli</c> and <c>download-probe</c> launch TIA Portal as a CHILD PROCESS that inherits
/// their standard handles. A .NET <c>RedirectStandardOutput</c> is a PIPE, and the pipe outlives the
/// command that created it — so <c>WaitForExit</c>/<c>ReadToEnd</c> block until Portal itself exits,
/// which for an attached session is never. CLAUDE.md records this as "piping its output hangs
/// forever — redirect with <c>&gt;</c>/<c>Out-File</c> on the outer invocation, don't pipe".</para>
///
/// <para>A FILE handle carries no such coupling: the grandchild may keep it open and the parent's
/// wait still returns. So every invocation goes through <c>cmd.exe /c</c> with <c>&gt;</c> and
/// <c>2&gt;</c> onto temporary files, which are read back after the process exits.</para>
/// </summary>
public sealed class ProcessRunner : IProcessRunner
{
    private readonly string _scratchDirectory;

    public ProcessRunner(string scratchDirectory) =>
        _scratchDirectory = scratchDirectory ?? throw new ArgumentNullException(nameof(scratchDirectory));

    public ProcessResult Run(string executable, IReadOnlyList<string> arguments, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(executable);
        ArgumentNullException.ThrowIfNull(arguments);

        if (!File.Exists(executable))
        {
            return ProcessResult.NotStarted(
                $"the executable '{executable}' does not exist, so nothing was run. This is not a failed command: no command happened.");
        }

        Directory.CreateDirectory(_scratchDirectory);
        var stem = Path.Combine(_scratchDirectory, $"run-{Guid.NewGuid():N}");
        var outPath = stem + ".out.txt";
        var errPath = stem + ".err.txt";

        // The whole invocation is handed to cmd.exe as ONE string with the redirections appended.
        // The outer quoting is cmd's own rule for "the command line begins and ends with a quote".
        var inner = CommandLine.Render(executable, arguments)
            + " > " + CommandLine.Quote(outPath)
            + " 2> " + CommandLine.Quote(errPath);

        var startInfo = new ProcessStartInfo("cmd.exe")
        {
            // Deliberately NOT ArgumentList: /c takes the rest of the line verbatim, and the
            // redirection operators must reach cmd unquoted.
            Arguments = "/c \"" + inner + "\"",
            UseShellExecute = false,

            // *** NEVER TRUE. *** See the class remark: a pipe here is inherited by Portal and the
            // wait never returns.
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo);
        if (process is null)
            return ProcessResult.NotStarted($"cmd.exe did not start for '{executable}'.");

        var exited = process.WaitForExit((int)Math.Min(timeout.TotalMilliseconds, int.MaxValue));
        if (!exited)
        {
            return new ProcessResult(true, true, -1, ReadIfPresent(outPath), ReadIfPresent(errPath),
                $"'{Path.GetFileName(executable)}' did not exit within {timeout}. It may still hold the project — do NOT assume it failed, and do NOT start another Portal command against the same project.");
        }

        return new ProcessResult(true, false, process.ExitCode, ReadIfPresent(outPath), ReadIfPresent(errPath),
            $"exit {process.ExitCode}");
    }

    private static string ReadIfPresent(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        }
        catch (IOException ex)
        {
            return $"(the output file '{path}' could not be read: {ex.Message})";
        }
    }
}
