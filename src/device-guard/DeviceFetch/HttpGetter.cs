using System.Net.Http.Headers;
using System.Text;

namespace DeviceFetch;

/// <summary>
/// The real HTTP GET, on .NET 8's HttpClient — whose TLS stack negotiates the HMI's modern TLS
/// (the Windows PowerShell / .NET Framework stack could not). Tolerates the device's self-signed
/// certificate: these are approved test rigs behind the guard, each with its own CA, so cert-chain
/// validation against the machine store would always fail. That tolerance is scoped to this tool.
///
/// Only GET is issued. The handler is configured once; there is no code path that sends any other
/// method.
/// </summary>
public sealed class HttpGetter : IHttpGetter, IDisposable
{
    private readonly HttpClient _client;

    public HttpGetter(TimeSpan? timeout = null)
    {
        var handler = new HttpClientHandler
        {
            // Test-rig scope: accept the device's self-signed cert. Documented and deliberate.
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
        };
        _client = new HttpClient(handler) { Timeout = timeout ?? TimeSpan.FromSeconds(20) };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("ladder-device-fetch/1.0 (read-only)");
    }

    public async Task<HttpFetchResult> GetAsync(Uri url, DeviceCredentials credentials, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);

        if (!string.IsNullOrEmpty(credentials.Cookie))
            req.Headers.TryAddWithoutValidation("Cookie", credentials.Cookie);

        if (credentials.BasicUser is not null)
        {
            var raw = $"{credentials.BasicUser}:{credentials.BasicPassword}";
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
        }

        using var resp = await _client.SendAsync(req, HttpCompletionOption.ResponseContentRead, ct);
        var body = await resp.Content.ReadAsByteArrayAsync(ct);

        return new HttpFetchResult(
            (int)resp.StatusCode,
            resp.Content.Headers.ContentType?.ToString(),
            body,
            (resp.RequestMessage?.RequestUri ?? url).ToString());
    }

    public void Dispose() => _client.Dispose();
}
