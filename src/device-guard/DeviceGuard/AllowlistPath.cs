namespace DeviceGuard;

/// <summary>
/// Resolves where the allowlist lives. There is deliberately NO built-in default path: if neither
/// the flag nor the environment variable is set, the resolved path is null and the guard fails
/// closed (refuses everything). A default file location would be a place a production device could
/// quietly accumulate; requiring an explicit path keeps the assertion conscious.
/// </summary>
public static class AllowlistPath
{
    public const string EnvVar = "LADDER_DEVICE_ALLOWLIST";

    /// <summary>Flag wins over the environment variable; absent both, returns null (→ fail closed).</summary>
    public static string? Resolve(string? flagPath, Func<string, string?> envLookup)
    {
        if (!string.IsNullOrWhiteSpace(flagPath))
            return flagPath;

        var env = envLookup(EnvVar);
        return string.IsNullOrWhiteSpace(env) ? null : env;
    }
}
