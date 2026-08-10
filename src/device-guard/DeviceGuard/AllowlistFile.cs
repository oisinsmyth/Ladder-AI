using System.Text.Json;

namespace DeviceGuard;

/// <summary>
/// Loads and parses the test-rig allowlist JSON file.
///
/// Fail-closed on every problem: a missing path, a missing file, an unreadable file or a malformed
/// document all produce a <see cref="Result"/> with <see cref="Result.Loaded"/> == false, which the
/// guard turns into a refusal. There is deliberately no "assume empty and carry on" path — a parse
/// error must never read as "no rigs, but fine".
/// </summary>
public static class AllowlistFile
{
    /// <summary>Outcome of a load attempt.</summary>
    public sealed record Result(
        bool Loaded,
        IReadOnlyList<AllowlistEntry> Entries,
        GuardReason FailureReason,
        string? Message,
        string? ResolvedPath)
    {
        public static Result Fail(GuardReason reason, string message, string? path) =>
            new(false, Array.Empty<AllowlistEntry>(), reason, message, path);

        public static Result Ok(IReadOnlyList<AllowlistEntry> entries, string path) =>
            new(true, entries, GuardReason.Allowed, null, path);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    // Expected shape: { "entries": [ { "address": "...", "label": "...", "kind": "test-rig", ... } ] }
    private sealed record FileShape(List<AllowlistEntry>? Entries);

    public static Result Load(string? resolvedPath)
    {
        if (string.IsNullOrWhiteSpace(resolvedPath))
        {
            return Result.Fail(GuardReason.NoAllowlistConfigured,
                $"No allowlist configured. Set --allowlist <path> or the {AllowlistPath.EnvVar} environment variable.",
                null);
        }

        if (!File.Exists(resolvedPath))
        {
            return Result.Fail(GuardReason.AllowlistFileMissing,
                $"Allowlist file not found: {resolvedPath}", resolvedPath);
        }

        string json;
        try
        {
            json = File.ReadAllText(resolvedPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Result.Fail(GuardReason.AllowlistUnreadable,
                $"Could not read allowlist '{resolvedPath}': {ex.Message}", resolvedPath);
        }

        FileShape? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<FileShape>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            return Result.Fail(GuardReason.AllowlistUnreadable,
                $"Malformed allowlist '{resolvedPath}': {ex.Message}", resolvedPath);
        }

        var entries = parsed?.Entries ?? new List<AllowlistEntry>();
        return Result.Ok(entries, resolvedPath);
    }
}
