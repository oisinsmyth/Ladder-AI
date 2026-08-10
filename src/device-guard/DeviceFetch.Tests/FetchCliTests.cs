using System.Text;
using DeviceFetch;

namespace DeviceFetch.Tests;

public class FetchCliTests : IDisposable
{
    private readonly List<string> _temp = new();

    private string TempAllowlist(string content)
    {
        var p = Path.Combine(Path.GetTempPath(), "device-fetch-al-" + Guid.NewGuid() + ".json");
        File.WriteAllText(p, content);
        _temp.Add(p);
        return p;
    }

    private string TempOut()
    {
        var p = Path.Combine(Path.GetTempPath(), "device-fetch-out-" + Guid.NewGuid() + ".bin");
        _temp.Add(p);
        return p;
    }

    public void Dispose()
    {
        foreach (var p in _temp) if (File.Exists(p)) File.Delete(p);
    }

    private sealed class StubGetter : IHttpGetter, IDisposable
    {
        private readonly HttpFetchResult _r;
        public StubGetter(HttpFetchResult r) => _r = r;
        public Task<HttpFetchResult> GetAsync(Uri url, DeviceCredentials c, CancellationToken ct)
            => Task.FromResult(_r with { FinalUrl = url.ToString() });
        public void Dispose() { }
    }

    private const string OneRig =
        """{ "entries": [ { "address": "10.10.10.15", "label": "Bench HMI", "kind": "test-rig" } ] }""";

    private static Func<string, string?> NoEnv => _ => null;

    [Fact]
    public async Task Missing_required_args_is_usage_error()
    {
        var o = new StringWriter(); var e = new StringWriter();
        var code = await FetchCli.RunAsync(new[] { "get", "--target", "10.10.10.15" },
            NoEnv, () => new StubGetter(new HttpFetchResult(200, "text/plain", Array.Empty<byte>(), "")), o, e);
        Assert.Equal(FetchCli.ExitUsage, code);
    }

    [Fact]
    public async Task Unknown_command_is_usage_error()
    {
        var code = await FetchCli.RunAsync(new[] { "push" }, NoEnv,
            () => new StubGetter(new HttpFetchResult(200, "", Array.Empty<byte>(), "")));
        Assert.Equal(FetchCli.ExitUsage, code);
    }

    [Fact]
    public async Task Refused_target_exits_one_and_writes_no_file()
    {
        var outFile = TempOut();
        var code = await FetchCli.RunAsync(
            new[] { "get", "--target", "10.99.99.99", "--path", "/x", "--out", outFile },
            NoEnv, () => new StubGetter(new HttpFetchResult(200, "text/plain", Encoding.UTF8.GetBytes("x"), "")));

        Assert.Equal(FetchCli.ExitRefusedOrFailed, code);
        Assert.False(File.Exists(outFile));
    }

    [Fact]
    public async Task Successful_fetch_writes_the_file_and_exits_zero()
    {
        var al = TempAllowlist(OneRig);
        var outFile = TempOut();
        var body = "trace: hello";

        var code = await FetchCli.RunAsync(
            new[] { "get", "--target", "10.10.10.15", "--path", "/trace.log", "--out", outFile, "--allowlist", al },
            NoEnv, () => new StubGetter(new HttpFetchResult(200, "text/plain", Encoding.UTF8.GetBytes(body), "")));

        Assert.Equal(FetchCli.ExitOk, code);
        Assert.True(File.Exists(outFile));
        Assert.Equal(body, File.ReadAllText(outFile));
    }

    [Fact]
    public async Task Password_comes_only_from_the_named_environment_variable()
    {
        var al = TempAllowlist(OneRig);
        var outFile = TempOut();
        Func<string, string?> env = name =>
            name == DeviceGuard.AllowlistPath.EnvVar ? al :
            name == "RIG_PW" ? "s3cret" : null;

        // If password resolution worked, --user + resolved password is accepted and the fetch proceeds.
        var code = await FetchCli.RunAsync(
            new[] { "get", "--target", "10.10.10.15", "--path", "/trace.log", "--out", outFile,
                    "--user", "admin", "--password-env", "RIG_PW" },
            env, () => new StubGetter(new HttpFetchResult(200, "text/plain", Encoding.UTF8.GetBytes("ok"), "")));

        Assert.Equal(FetchCli.ExitOk, code);
    }

    [Fact]
    public async Task User_without_resolvable_password_is_usage_error()
    {
        var al = TempAllowlist(OneRig);
        var outFile = TempOut();
        Func<string, string?> env = name => name == DeviceGuard.AllowlistPath.EnvVar ? al : null;

        var code = await FetchCli.RunAsync(
            new[] { "get", "--target", "10.10.10.15", "--path", "/x", "--out", outFile,
                    "--user", "admin", "--password-env", "MISSING_PW" },
            env, () => new StubGetter(new HttpFetchResult(200, "", Array.Empty<byte>(), "")));

        Assert.Equal(FetchCli.ExitUsage, code);
    }
}
