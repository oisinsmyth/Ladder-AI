using System.Collections.Generic;
using OpennessCli.Openness;
using Xunit;

namespace OpennessCli.Tests;

public class TiaInstallLocatorTests
{
    [Fact]
    public void Resolve_PrefersCliOverride_WhenPresent()
    {
        var probed = new List<string>();
        var result = TiaInstallLocator.Resolve(
            cliOverride: @"C:\cli-override",
            envVarValue: @"C:\env-var-dir",
            directoryHasDll: dir =>
            {
                probed.Add(dir);
                return dir == @"C:\cli-override";
            });

        Assert.Equal(@"C:\cli-override\Siemens.Engineering.dll", result);
        Assert.Equal(new[] { @"C:\cli-override" }, probed); // must not probe further once the first candidate hits
    }

    [Fact]
    public void Resolve_FallsBackToEnvVar_WhenNoCliOverride()
    {
        var result = TiaInstallLocator.Resolve(
            cliOverride: null,
            envVarValue: @"C:\env-var-dir",
            directoryHasDll: dir => dir == @"C:\env-var-dir");

        Assert.Equal(@"C:\env-var-dir\Siemens.Engineering.dll", result);
    }

    [Fact]
    public void Resolve_FallsBackToDefaultInstallDir_WhenNoOverrideOrEnvVar()
    {
        var result = TiaInstallLocator.Resolve(
            cliOverride: null,
            envVarValue: null,
            directoryHasDll: dir => dir == TiaInstallLocator.DefaultInstallDir);

        Assert.Equal(
            TiaInstallLocator.DefaultInstallDir + @"\Siemens.Engineering.dll",
            result);
    }

    [Fact]
    public void Resolve_TriesCliOverrideBeforeEnvVarBeforeDefault_InOrder()
    {
        var probed = new List<string>();
        Assert.Throws<TiaInstallNotFoundException>(() =>
            TiaInstallLocator.Resolve(
                cliOverride: @"C:\cli",
                envVarValue: @"C:\env",
                directoryHasDll: dir =>
                {
                    probed.Add(dir);
                    return false;
                }));

        Assert.Equal(new[] { @"C:\cli", @"C:\env", TiaInstallLocator.DefaultInstallDir }, probed);
    }

    [Fact]
    public void Resolve_ThrowsWithAllTriedPaths_WhenNothingFound()
    {
        var ex = Assert.Throws<TiaInstallNotFoundException>(() =>
            TiaInstallLocator.Resolve(cliOverride: null, envVarValue: null, directoryHasDll: _ => false));

        Assert.Contains(TiaInstallLocator.DefaultInstallDir, ex.Message);
        Assert.Contains(TiaInstallLocator.EnvVarName, ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_TreatsBlankCliOverride_AsAbsent(string blank)
    {
        var result = TiaInstallLocator.Resolve(
            cliOverride: blank,
            envVarValue: null,
            directoryHasDll: dir => dir == TiaInstallLocator.DefaultInstallDir);

        Assert.Equal(TiaInstallLocator.DefaultInstallDir + @"\Siemens.Engineering.dll", result);
    }
}
