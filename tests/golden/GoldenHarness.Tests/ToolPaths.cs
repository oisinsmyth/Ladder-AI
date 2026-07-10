namespace GoldenHarness;

/// <summary>
/// Locates the built openness-cli.exe and converter.exe — env var override first (for CI or a
/// non-Debug build), else resolved relative to the repo root (found by walking up from the test
/// assembly's location looking for CLAUDE.md, so this doesn't hardcode bin/ subfolder depth).
/// </summary>
public static class ToolPaths
{
    public static string OpennessCliExe =>
        Environment.GetEnvironmentVariable("OPENNESS_CLI_EXE")
        ?? Path.Combine(RepoRoot(), "src", "openness-cli", "OpennessCli", "bin", "Debug", "net48", "openness-cli.exe");

    public static string ConverterExe =>
        Environment.GetEnvironmentVariable("CONVERTER_EXE")
        ?? Path.Combine(RepoRoot(), "src", "converter", "Converter", "bin", "Debug", "net8.0", "converter.exe");

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CLAUDE.md")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName
            ?? throw new InvalidOperationException("Could not locate repo root (no CLAUDE.md found in any parent of the test assembly's directory).");
    }
}
