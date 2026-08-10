using System.Text.Json;

namespace DeviceGuard;

/// <summary>
/// Command-line surface for the safeguard. This is what a future read-only fetch client calls (or
/// links the library and calls <see cref="DeviceAccessGuard.Check"/> directly) before touching any
/// device.
///
/// Commands:
///   check &lt;address&gt; [--allowlist &lt;path&gt;] [--json]   exit 0 = allowed, 1 = refused, 2 = usage
///   list                [--allowlist &lt;path&gt;] [--json]   show the honored entries (or that there are none)
///   where               [--allowlist &lt;path&gt;] [--json]   show which allowlist path was resolved
/// </summary>
public static class Cli
{
    public const int ExitAllowed = 0;
    public const int ExitRefused = 1;
    public const int ExitUsage = 2;

    public const string Usage =
        """
        device-guard — fail-closed test-rig allowlist gate (ADR-0008)

        Usage:
          device-guard check <address> [--allowlist <path>] [--json]
          device-guard list            [--allowlist <path>] [--json]
          device-guard where           [--allowlist <path>] [--json]

        The allowlist path comes from --allowlist, else the LADDER_DEVICE_ALLOWLIST
        environment variable. With neither set there is NO allowlist and every target
        is refused. Only entries whose "kind" is exactly "test-rig" ever allow access.

        Exit codes:  0 allowed   1 refused   2 usage error
        """;

    public static int Run(string[] args, Func<string, string?> envLookup,
        TextWriter? stdout = null, TextWriter? stderr = null)
    {
        var @out = stdout ?? Console.Out;
        var err = stderr ?? Console.Error;

        if (args.Length == 0)
        {
            err.WriteLine(Usage);
            return ExitUsage;
        }

        var command = args[0];
        var rest = args.Skip(1).ToArray();

        if (command is "-h" or "--help" or "help")
        {
            @out.WriteLine(Usage);
            return ExitAllowed;
        }

        bool json = rest.Contains("--json");
        string? flagPath = GetOptionValue(rest, "--allowlist");
        string? resolvedPath = AllowlistPath.Resolve(flagPath, envLookup);

        switch (command)
        {
            case "check":
                return RunCheck(rest, resolvedPath, json, @out, err);
            case "list":
                return RunList(resolvedPath, json, @out);
            case "where":
                return RunWhere(resolvedPath, json, @out);
            default:
                err.WriteLine($"Unknown command '{command}'.");
                err.WriteLine(Usage);
                return ExitUsage;
        }
    }

    private static int RunCheck(string[] rest, string? resolvedPath, bool json, TextWriter @out, TextWriter err)
    {
        var target = FirstPositional(rest);
        if (target is null)
        {
            err.WriteLine("check requires a <address> argument.");
            err.WriteLine(Usage);
            return ExitUsage;
        }

        var guard = DeviceAccessGuard.FromPath(resolvedPath);
        var decision = guard.Check(target);

        if (json)
        {
            @out.WriteLine(JsonSerializer.Serialize(new
            {
                target,
                allowed = decision.Allowed,
                reason = decision.Reason.ToString(),
                message = decision.Message,
                allowlistPath = resolvedPath,
                matchedEntry = decision.MatchedEntry,
            }, PrettyJson));
        }
        else
        {
            @out.WriteLine(decision.Message);
        }

        return decision.Allowed ? ExitAllowed : ExitRefused;
    }

    private static int RunList(string? resolvedPath, bool json, TextWriter @out)
    {
        var load = AllowlistFile.Load(resolvedPath);
        var rigs = load.Entries.Where(e => e.HasAddress && e.IsTestRig).ToList();

        if (json)
        {
            @out.WriteLine(JsonSerializer.Serialize(new
            {
                allowlistPath = resolvedPath,
                loaded = load.Loaded,
                reason = load.Loaded ? "Loaded" : load.FailureReason.ToString(),
                message = load.Message,
                testRigCount = rigs.Count,
                testRigs = rigs,
                allEntries = load.Entries,
            }, PrettyJson));
            return ExitAllowed;
        }

        if (!load.Loaded)
        {
            @out.WriteLine(load.Message ?? "No allowlist available. Every target is refused.");
            return ExitAllowed;
        }

        if (rigs.Count == 0)
        {
            @out.WriteLine($"Allowlist '{resolvedPath}' has no approved test-rig entries. Every target is refused.");
            return ExitAllowed;
        }

        @out.WriteLine($"Approved test rigs ({rigs.Count}) from '{resolvedPath}':");
        foreach (var e in rigs)
            @out.WriteLine($"  {e.Address,-18} {e.DisplayLabel}");
        return ExitAllowed;
    }

    private static int RunWhere(string? resolvedPath, bool json, TextWriter @out)
    {
        if (json)
        {
            @out.WriteLine(JsonSerializer.Serialize(new
            {
                allowlistPath = resolvedPath,
                configured = resolvedPath is not null,
                envVar = AllowlistPath.EnvVar,
            }, PrettyJson));
            return ExitAllowed;
        }

        @out.WriteLine(resolvedPath is null
            ? $"No allowlist configured (set --allowlist or {AllowlistPath.EnvVar}). Every target is refused."
            : $"Allowlist path: {resolvedPath}");
        return ExitAllowed;
    }

    private static readonly JsonSerializerOptions PrettyJson = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    // --- tiny arg helpers (no dependency) ---

    /// <summary>Value following <paramref name="name"/>, or null if the flag is absent or has no value.</summary>
    private static string? GetOptionValue(string[] args, string name)
    {
        var idx = Array.IndexOf(args, name);
        if (idx < 0 || idx + 1 >= args.Length)
            return null;
        return args[idx + 1];
    }

    /// <summary>First bare argument that is neither a flag nor the value consumed by --allowlist.</summary>
    private static string? FirstPositional(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (a == "--allowlist")
            {
                i++; // skip its value
                continue;
            }
            if (a.StartsWith("--", StringComparison.Ordinal))
                continue;
            return a;
        }
        return null;
    }
}
