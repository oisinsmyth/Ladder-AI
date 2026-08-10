using System.Text;
using DeviceFetch;
using DeviceGuard;

namespace DeviceFetch.Tests;

public class DeviceFetchServiceTests
{
    /// <summary>Records whether it was called, so we can prove the guard blocks BEFORE any network I/O.</summary>
    private sealed class FakeGetter : IHttpGetter
    {
        public int Calls { get; private set; }
        public Uri? LastUrl { get; private set; }
        private readonly HttpFetchResult _result;
        public FakeGetter(HttpFetchResult result) => _result = result;

        public Task<HttpFetchResult> GetAsync(Uri url, DeviceCredentials credentials, CancellationToken ct)
        {
            Calls++;
            LastUrl = url;
            return Task.FromResult(_result with { FinalUrl = url.ToString() });
        }
    }

    private static DeviceAccessGuard GuardAllowing(string address)
    {
        var entry = new AllowlistEntry(address, "Bench", AllowlistEntry.TestRigKind);
        return new DeviceAccessGuard(AllowlistFile.Result.Ok(new[] { entry }, "test.json"));
    }

    private static DeviceAccessGuard GuardAllowingNothing() =>
        new(AllowlistFile.Result.Ok(Array.Empty<AllowlistEntry>(), "test.json"));

    private static HttpFetchResult Ok(string body, string type = "text/plain") =>
        new(200, type, Encoding.UTF8.GetBytes(body), "https://host/");

    [Fact]
    public async Task Refuses_and_never_contacts_a_device_the_guard_blocks()
    {
        var getter = new FakeGetter(Ok("secret"));
        var svc = new DeviceFetchService(GuardAllowingNothing(), getter);

        var outcome = await svc.FetchAsync("10.10.10.15", "/trace.log", DeviceCredentials.None);

        Assert.False(outcome.Success);
        Assert.Equal(0, getter.Calls); // the whole point: no network touch on a refused target
    }

    [Fact]
    public async Task Fetches_when_target_is_allowed()
    {
        var getter = new FakeGetter(Ok("trace line 1"));
        var svc = new DeviceFetchService(GuardAllowing("10.10.10.15"), getter);

        var outcome = await svc.FetchAsync("10.10.10.15", "/trace.log", DeviceCredentials.None);

        Assert.True(outcome.Success);
        Assert.Equal(1, getter.Calls);
        Assert.Equal("https://10.10.10.15/trace.log", getter.LastUrl!.ToString());
        Assert.Equal("trace line 1", Encoding.UTF8.GetString(outcome.Body!));
    }

    [Fact]
    public async Task Adds_leading_slash_to_a_bare_path()
    {
        var getter = new FakeGetter(Ok("x"));
        var svc = new DeviceFetchService(GuardAllowing("10.10.10.15"), getter);

        await svc.FetchAsync("10.10.10.15", "trace.log", DeviceCredentials.None);

        Assert.Equal("https://10.10.10.15/trace.log", getter.LastUrl!.ToString());
    }

    [Fact]
    public async Task Refuses_safety_path_before_fetching()
    {
        var getter = new FakeGetter(Ok("x"));
        var svc = new DeviceFetchService(GuardAllowing("10.10.10.15"), getter);

        var outcome = await svc.FetchAsync("10.10.10.15", "/device/safety/program.log", DeviceCredentials.None);

        Assert.False(outcome.Success);
        Assert.Contains("safety", outcome.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, getter.Calls); // refused before any network I/O
    }

    [Fact]
    public async Task Refuses_safety_tagged_content_after_fetching()
    {
        var getter = new FakeGetter(Ok("... this is the F-Runtime group ...", "text/plain"));
        var svc = new DeviceFetchService(GuardAllowing("10.10.10.15"), getter);

        var outcome = await svc.FetchAsync("10.10.10.15", "/some.log", DeviceCredentials.None);

        Assert.False(outcome.Success);
        Assert.Contains("safety", outcome.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(outcome.Body); // not handed back
    }

    [Fact]
    public async Task Reports_auth_required_on_401_without_guessing()
    {
        var getter = new FakeGetter(new HttpFetchResult(401, "text/html", Array.Empty<byte>(), "https://10.10.10.15/device/WebRH"));
        var svc = new DeviceFetchService(GuardAllowing("10.10.10.15"), getter);

        var outcome = await svc.FetchAsync("10.10.10.15", "/device/WebRH", DeviceCredentials.None);

        Assert.False(outcome.Success);
        Assert.Contains("AUTH REQUIRED", outcome.Message);
        Assert.Equal(401, outcome.HttpStatus);
    }
}
