namespace DeviceFetch;

/// <summary>Result of a single read-only HTTP GET.</summary>
public sealed record HttpFetchResult(
    int StatusCode,
    string? ContentType,
    byte[] Body,
    string FinalUrl);

/// <summary>
/// The one network operation this tool performs: an HTTP GET. Abstracted behind an interface so the
/// orchestration (guarding, safety checks, output) is unit-testable without a live device, and so
/// that there is — by construction — no method here that writes to a device. There is no Post, Put,
/// Delete or upload anywhere in this assembly. Read-only is a property of the type, not a promise.
/// </summary>
public interface IHttpGetter
{
    Task<HttpFetchResult> GetAsync(Uri url, DeviceCredentials credentials, CancellationToken ct);
}

/// <summary>
/// Credentials for the device, supplied at call time — never hardcoded, never logged. A cookie (an
/// already-established session) or HTTP Basic. Full UMC token login is intentionally NOT implemented:
/// it needs real credentials regardless, and this tool refuses to guess or brute-force them.
/// </summary>
public sealed record DeviceCredentials(string? Cookie = null, string? BasicUser = null, string? BasicPassword = null)
{
    public static readonly DeviceCredentials None = new();

    public bool HasAny => Cookie is not null || BasicUser is not null;
}
