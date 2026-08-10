using System.Text;

namespace DeviceFetch;

/// <summary>
/// A coarse guard against pulling safety-program content off a device (hard rule 2 / permanent
/// exclusion #1). It is deliberately conservative and NOT a substitute for the export-time safety
/// enforcement: over a live HTTP surface there is no F-block flag to read, so this can only match on
/// names and obvious markers. It errs toward refusal.
///
/// For HMI JavaScript trace logs this rarely triggers — but the guard exists so that pointing the
/// fetcher at, say, a safety-program artifact is refused rather than silently downloaded.
/// </summary>
public static class SafetyContentGuard
{
    // Path/name tokens that name safety content on Siemens systems.
    private static readonly string[] PathTokens =
    {
        "/safety", "failsafe", "fail-safe", "f-runtime", "f_runtime", "safetyprogram", "safety-program",
    };

    public static bool PathLooksLikeSafety(string resourcePath, out string reason)
    {
        var lower = resourcePath.ToLowerInvariant();
        foreach (var t in PathTokens)
        {
            if (lower.Contains(t))
            {
                reason = $"resource path '{resourcePath}' matches safety token '{t}'.";
                return true;
            }
        }
        reason = "";
        return false;
    }

    public static bool ContentLooksLikeSafety(string? contentType, byte[] body, out string reason)
    {
        // Only sniff text; binaries are not classifiable here and pass this coarse check.
        if (contentType is not null && !contentType.Contains("text") && !contentType.Contains("json")
            && !contentType.Contains("xml"))
        {
            reason = "";
            return false;
        }

        var sample = Encoding.UTF8.GetString(body, 0, Math.Min(body.Length, 4096)).ToLowerInvariant();
        // A conservative marker: F-DB / F-block naming as it appears in Siemens exports.
        if (sample.Contains("f-runtime") || sample.Contains("safety program") || sample.Contains("fail-safe"))
        {
            reason = "fetched content contains a safety-program marker.";
            return true;
        }

        reason = "";
        return false;
    }
}
