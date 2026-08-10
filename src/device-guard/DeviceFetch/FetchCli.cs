using System.Text.Json;
using DeviceGuard;

namespace DeviceFetch;

/// <summary>
/// CLI for the read-only fetch client. Read-only by construction: the only verb is <c>get</c>.
///
///   device-fetch get --target &lt;addr&gt; --path &lt;/resource&gt; --out &lt;file&gt;
///                    [--allowlist &lt;path&gt;] [--cookie &lt;session&gt;] [--user &lt;u&gt; --password-env &lt;VAR&gt;] [--json]
///
/// The target is checked against device-guard first; an off-allowlist device is never contacted.
/// Credentials are never hardcoded or logged — a password comes only from an environment variable
/// named by --password-env.
/// </summary>
public static class FetchCli
{
    public const int ExitOk = 0;
    public const int ExitRefusedOrFailed = 1;
    public const int ExitUsage = 2;

    public const string Usage =
        """
        device-fetch — read-only device file fetch, gated by device-guard (ADR-0008)

        Usage:
          device-fetch get --target <addr> --path </resource> --out <file>
                           [--allowlist <path>] [--cookie <session>]
                           [--user <name> --password-env <ENVVAR>] [--json]

        - The target must be an approved test rig (device-guard allowlist), or it is refused
          before any network contact.
        - Only HTTP GET is ever issued. There is no write/upload path.
        - Credentials are optional; a protected resource returns AUTH REQUIRED rather than a guess.
          A password is read ONLY from the environment variable named by --password-env.

        Exit codes:  0 ok   1 refused/failed   2 usage error
        """;

    public static async Task<int> RunAsync(
        string[] args,
        Func<string, string?> envLookup,
        Func<IHttpGetter> getterFactory,
        TextWriter? stdout = null,
        TextWriter? stderr = null)
    {
        var @out = stdout ?? Console.Out;
        var err = stderr ?? Console.Error;

        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            @out.WriteLine(Usage);
            return args.Length == 0 ? ExitUsage : ExitOk;
        }

        if (args[0] != "get")
        {
            err.WriteLine($"Unknown command '{args[0]}'.");
            err.WriteLine(Usage);
            return ExitUsage;
        }

        var rest = args.Skip(1).ToArray();
        bool json = rest.Contains("--json");
        string? target = GetOpt(rest, "--target");
        string? path = GetOpt(rest, "--path");
        string? outFile = GetOpt(rest, "--out");
        string? allowlistFlag = GetOpt(rest, "--allowlist");
        string? cookie = GetOpt(rest, "--cookie");
        string? user = GetOpt(rest, "--user");
        string? passwordEnv = GetOpt(rest, "--password-env");

        if (target is null || path is null || outFile is null)
        {
            err.WriteLine("get requires --target, --path and --out.");
            err.WriteLine(Usage);
            return ExitUsage;
        }

        string? password = passwordEnv is null ? null : envLookup(passwordEnv);
        if (user is not null && password is null)
        {
            err.WriteLine($"--user given but --password-env '{passwordEnv}' resolved to nothing.");
            return ExitUsage;
        }

        var credentials = new DeviceCredentials(cookie, user, password);
        var resolvedAllowlist = AllowlistPath.Resolve(allowlistFlag, envLookup);
        var guard = DeviceAccessGuard.FromPath(resolvedAllowlist);

        using var getter = getterFactory() as IDisposable;
        var service = new DeviceFetchService(guard, (IHttpGetter)getter!);

        var outcome = await service.FetchAsync(target, path, credentials);

        if (outcome.Success && outcome.Body is not null)
        {
            try
            {
                var dir = Path.GetDirectoryName(Path.GetFullPath(outFile));
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                await File.WriteAllBytesAsync(outFile, outcome.Body);
            }
            catch (Exception ex)
            {
                err.WriteLine($"Fetched OK but could not write '{outFile}': {ex.Message}");
                return ExitRefusedOrFailed;
            }
        }

        if (json)
        {
            @out.WriteLine(JsonSerializer.Serialize(new
            {
                target,
                path,
                success = outcome.Success,
                message = outcome.Message,
                httpStatus = outcome.HttpStatus,
                contentType = outcome.ContentType,
                finalUrl = outcome.FinalUrl,
                bytes = outcome.Body?.Length,
                outFile = outcome.Success ? outFile : null,
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        else
        {
            @out.WriteLine(outcome.Message);
            if (outcome.Success) @out.WriteLine($"Written to {outFile}");
        }

        return outcome.Success ? ExitOk : ExitRefusedOrFailed;
    }

    private static string? GetOpt(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return (i >= 0 && i + 1 < args.Length) ? args[i + 1] : null;
    }
}
