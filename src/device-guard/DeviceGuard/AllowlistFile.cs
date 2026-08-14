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

        // *** A NULL ELEMENT IS A MALFORMED DOCUMENT, AND IT USED TO BE AN UNNAMED CRASH. ***
        // Measured 2026-08-14: `{"entries": [null]}` parses cleanly into a list containing a null,
        // so this method returned Loaded == true and the guard then dereferenced it —
        // NullReferenceException out of DeviceAccessGuard.Check, exit -1073741819, and NOT ONE WORD
        // of refusal printed.
        //
        // It did fail closed: the stack ended at the guard and no socket was ever opened. But an
        // exit code no caller vocabulary contains is unusable to a harness, which cannot tell an
        // unhandled exception from a refusal — so the fence held while saying nothing, and that is
        // the half being fixed. The class comment above already promised that "a malformed document"
        // produces Loaded == false; a null entry simply was not recognised as one.
        //
        // Refused rather than filtered out. Dropping the null would be a silent repair of a file
        // somebody wrote wrong, and the next reader would see a shorter list than they authored.
        var nullAt = new List<int>();
        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i] is null)
            {
                nullAt.Add(i);
            }
        }

        if (nullAt.Count > 0)
        {
            return Result.Fail(GuardReason.AllowlistUnreadable,
                $"Malformed allowlist '{resolvedPath}': entries[{string.Join("], entries[", nullAt)}] " +
                (nullAt.Count == 1 ? "is" : "are") + " null. An entry that names no device is not an " +
                "entry, and a document containing one is not readable. Nothing was refused or allowed " +
                "on the strength of it, and no connection was attempted.",
                resolvedPath);
        }

        return Result.Ok(entries, resolvedPath);
    }
}
