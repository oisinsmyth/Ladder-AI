using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpennessCli.Openness;

/// <summary>
/// Locates Siemens.Engineering.dll: --tia-install flag, then TIA_OPENNESS_PATH env var,
/// then the standard V20 install location. Never guesses beyond that — fails with a
/// message listing exactly what was tried.
/// </summary>
public static class TiaInstallLocator
{
    public const string EngineeringDllFileName = "Siemens.Engineering.dll";
    public const string EnvVarName = "TIA_OPENNESS_PATH";
    public const string DefaultInstallDir = @"C:\Program Files\Siemens\Automation\Portal V20\PublicAPI\V20";

    /// <summary>Pure resolution logic, unit-testable without touching the real filesystem.</summary>
    public static string Resolve(string? cliOverride, string? envVarValue, Func<string, bool> directoryHasDll)
    {
        var candidates = new List<(string Source, string Dir)>();

        if (!string.IsNullOrWhiteSpace(cliOverride))
        {
            candidates.Add(("--tia-install", cliOverride!));
        }

        if (!string.IsNullOrWhiteSpace(envVarValue))
        {
            candidates.Add(($"{EnvVarName} environment variable", envVarValue!));
        }

        candidates.Add(("default install path", DefaultInstallDir));

        foreach (var candidate in candidates)
        {
            if (directoryHasDll(candidate.Dir))
            {
                return Path.Combine(candidate.Dir, EngineeringDllFileName);
            }
        }

        var tried = string.Join(Environment.NewLine, candidates.Select(c => $"  - {c.Source}: {c.Dir}"));
        throw new TiaInstallNotFoundException(
            $"Could not find {EngineeringDllFileName}. Tried:{Environment.NewLine}{tried}{Environment.NewLine}" +
            $"Pass --tia-install <dir> or set {EnvVarName} to the directory containing {EngineeringDllFileName}.");
    }

    /// <summary>Real-filesystem convenience overload used by Program.cs.</summary>
    public static string Resolve(string? cliOverride)
    {
        var envVarValue = Environment.GetEnvironmentVariable(EnvVarName);
        return Resolve(cliOverride, envVarValue, dir => File.Exists(Path.Combine(dir, EngineeringDllFileName)));
    }
}

public sealed class TiaInstallNotFoundException : Exception
{
    public TiaInstallNotFoundException(string message)
        : base(message)
    {
    }
}
