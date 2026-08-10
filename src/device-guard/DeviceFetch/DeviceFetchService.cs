using DeviceGuard;

namespace DeviceFetch;

/// <summary>Outcome of a fetch attempt — the orchestration result, before anything is written.</summary>
public sealed record FetchOutcome(
    bool Success,
    string Message,
    int? HttpStatus = null,
    byte[]? Body = null,
    string? ContentType = null,
    string? FinalUrl = null);

/// <summary>
/// Orchestrates one read-only fetch: guard → safety check → GET → safety check on what came back.
/// It never writes to the device (there is no verb for it) and it consults <see cref="DeviceGuard"/>
/// FIRST, so a target the safeguard refuses is never contacted at all.
/// </summary>
public sealed class DeviceFetchService
{
    private readonly DeviceAccessGuard _guard;
    private readonly IHttpGetter _getter;

    public DeviceFetchService(DeviceAccessGuard guard, IHttpGetter getter)
    {
        _guard = guard;
        _getter = getter;
    }

    public async Task<FetchOutcome> FetchAsync(string target, string resourcePath, DeviceCredentials credentials, CancellationToken ct = default)
    {
        // 1. The safeguard decides whether we may touch this device AT ALL — before any network I/O.
        var decision = _guard.Check(target);
        if (!decision.Allowed)
            return new FetchOutcome(false, decision.Message);

        // 2. Refuse a safety-program resource by its path before fetching (hard rule 2, coarse pre-check).
        if (SafetyContentGuard.PathLooksLikeSafety(resourcePath, out var why))
            return new FetchOutcome(false, $"REFUSED (safety): {why}");

        if (!Uri.TryCreate($"https://{target}{Normalize(resourcePath)}", UriKind.Absolute, out var url))
            return new FetchOutcome(false, $"REFUSED: could not form a URL from target '{target}' and path '{resourcePath}'.");

        // 3. The single read-only GET.
        HttpFetchResult result;
        try
        {
            result = await _getter.GetAsync(url, credentials, ct);
        }
        catch (Exception ex)
        {
            return new FetchOutcome(false, $"FETCH FAILED: {ex.Message}");
        }

        // 4. An auth wall is the expected outcome for protected resources — report it, do not retry-guess.
        if (result.StatusCode is 401 or 403)
            return new FetchOutcome(false,
                $"AUTH REQUIRED: {result.StatusCode} from {result.FinalUrl}. Supply --cookie or --user/--password-env; this tool never guesses credentials.",
                result.StatusCode, null, result.ContentType, result.FinalUrl);

        if (result.StatusCode is >= 300)
            return new FetchOutcome(false, $"HTTP {result.StatusCode} from {result.FinalUrl}.",
                result.StatusCode, null, result.ContentType, result.FinalUrl);

        // 5. Refuse safety-tagged content that came back, by content type / sniff (coarse).
        if (SafetyContentGuard.ContentLooksLikeSafety(result.ContentType, result.Body, out var contentWhy))
            return new FetchOutcome(false, $"REFUSED (safety): {contentWhy}", result.StatusCode, null, result.ContentType, result.FinalUrl);

        return new FetchOutcome(true, $"OK: {result.Body.Length} bytes from {result.FinalUrl}",
            result.StatusCode, result.Body, result.ContentType, result.FinalUrl);
    }

    private static string Normalize(string path) => path.StartsWith('/') ? path : "/" + path;
}
