namespace DeviceGuard.Tests;

/// <summary>End-to-end over the CLI surface: exit codes and env/flag resolution, with writers and
/// environment injected so nothing touches the real process environment.</summary>
public class CliTests : IDisposable
{
    private readonly List<string> _temp = new();

    private string WriteAllowlist(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), "device-guard-cli-" + Guid.NewGuid() + ".json");
        File.WriteAllText(path, content);
        _temp.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var p in _temp)
            if (File.Exists(p)) File.Delete(p);
    }

    private static Func<string, string?> NoEnv => _ => null;

    private static (int code, string @out, string err) Run(string[] args, Func<string, string?>? env = null)
    {
        var o = new StringWriter();
        var e = new StringWriter();
        var code = Cli.Run(args, env ?? NoEnv, o, e);
        return (code, o.ToString(), e.ToString());
    }

    private const string OneRig =
        """{ "entries": [ { "address": "10.10.10.15", "label": "Bench HMI", "kind": "test-rig" } ] }""";

    [Fact]
    public void No_args_is_usage_error()
    {
        var (code, _, err) = Run(Array.Empty<string>());
        Assert.Equal(Cli.ExitUsage, code);
        Assert.Contains("Usage", err);
    }

    [Fact]
    public void Unknown_command_is_usage_error()
    {
        var (code, _, _) = Run(new[] { "frobnicate" });
        Assert.Equal(Cli.ExitUsage, code);
    }

    [Fact]
    public void Check_without_address_is_usage_error()
    {
        var path = WriteAllowlist(OneRig);
        var (code, _, _) = Run(new[] { "check", "--allowlist", path });
        Assert.Equal(Cli.ExitUsage, code);
    }

    [Fact]
    public void Check_allowed_exits_zero()
    {
        var path = WriteAllowlist(OneRig);
        var (code, @out, _) = Run(new[] { "check", "10.10.10.15", "--allowlist", path });

        Assert.Equal(Cli.ExitAllowed, code);
        Assert.Contains("ALLOWED", @out);
    }

    [Fact]
    public void Check_refused_when_not_listed_exits_one()
    {
        var path = WriteAllowlist(OneRig);
        var (code, @out, _) = Run(new[] { "check", "10.10.10.99", "--allowlist", path });

        Assert.Equal(Cli.ExitRefused, code);
        Assert.Contains("REFUSED", @out);
    }

    [Fact]
    public void Check_refused_when_no_allowlist_configured_exits_one()
    {
        var (code, @out, _) = Run(new[] { "check", "10.10.10.15" });
        Assert.Equal(Cli.ExitRefused, code);
        Assert.Contains("REFUSED", @out);
    }

    [Fact]
    public void Allowlist_path_resolves_from_environment_variable()
    {
        var path = WriteAllowlist(OneRig);
        Func<string, string?> env = name => name == AllowlistPath.EnvVar ? path : null;

        var (code, _, _) = Run(new[] { "check", "10.10.10.15" }, env);
        Assert.Equal(Cli.ExitAllowed, code);
    }

    [Fact]
    public void Flag_overrides_environment_variable()
    {
        var envPath = WriteAllowlist("""{ "entries": [] }"""); // empty → would refuse
        var flagPath = WriteAllowlist(OneRig);                  // has the rig → allows
        Func<string, string?> env = name => name == AllowlistPath.EnvVar ? envPath : null;

        var (code, _, _) = Run(new[] { "check", "10.10.10.15", "--allowlist", flagPath }, env);
        Assert.Equal(Cli.ExitAllowed, code);
    }

    [Fact]
    public void Check_json_output_is_valid_json()
    {
        var path = WriteAllowlist(OneRig);
        var (code, @out, _) = Run(new[] { "check", "10.10.10.15", "--allowlist", path, "--json" });

        Assert.Equal(Cli.ExitAllowed, code);
        using var doc = System.Text.Json.JsonDocument.Parse(@out);
        Assert.True(doc.RootElement.GetProperty("allowed").GetBoolean());
    }

    [Fact]
    public void List_reports_the_rigs()
    {
        var path = WriteAllowlist(OneRig);
        var (code, @out, _) = Run(new[] { "list", "--allowlist", path });

        Assert.Equal(Cli.ExitAllowed, code);
        Assert.Contains("10.10.10.15", @out);
    }

    [Fact]
    public void Where_without_config_says_refused()
    {
        var (code, @out, _) = Run(new[] { "where" });
        Assert.Equal(Cli.ExitAllowed, code);
        Assert.Contains("No allowlist configured", @out);
    }
}
