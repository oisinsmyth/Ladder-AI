using Harness.CmdInject;

namespace Harness.CmdInject.Tests;

/// <summary>
/// The fence, gate by gate. Each case drives the REAL <c>AllowlistFile</c> and <c>DeviceAccessGuard</c>
/// against real files on disk — a fake fence would be a fence you can hand a permissive one to, which is
/// the mutation rather than the test.
/// </summary>
public class InjectionFenceTests : IDisposable
{
    private readonly string _directory =
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "cmd-inject-fence-" + Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); }
        catch (IOException) { /* a leftover temp dir is not a test failure */ }
    }

    private const string Rig = "10.10.10.10";
    private const uint Stamp = 0xF52ECEAD;

    private string Allowlist(string name, string json)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, json);
        return path;
    }

    private static string Entry(
        string kind = "test-rig",
        bool writeEligible = true,
        bool outputsIsolated = true,
        string? isolationAssertedBy = "a-tester") =>
        $$"""
          {
            "entries": [
              {
                "address": "{{Rig}}",
                "label": "bench rig fixture",
                "kind": "{{kind}}",
                "writeEligible": {{(writeEligible ? "true" : "false")}},
                "outputsIsolated": {{(outputsIsolated ? "true" : "false")}}
                {{(isolationAssertedBy is null ? "" : $", \"isolationAssertedBy\": \"{isolationAssertedBy}\"")}}
              }
            ]
          }
          """;

    [Fact]
    public void NoTarget_IsNoTargetGate()
    {
        var decision = InjectionFence.Check(target: "", allowlistPath: Allowlist("a.json", Entry()), expectedBuildStamp: Stamp);
        Assert.False(decision.Allowed);
        Assert.Equal(InjectionGate.NoTarget, decision.Gate);
    }

    [Fact]
    public void NoAllowlistConfigured_IsAUsageGateNotARefusalOfTheDevice()
    {
        var decision = InjectionFence.Check(Rig, allowlistPath: null, expectedBuildStamp: Stamp);

        Assert.False(decision.Allowed);
        Assert.Equal(InjectionGate.NoAllowlistConfigured, decision.Gate);
        // It must read as a SETUP error, mapped to Usage — not a governance decision about the device.
        Assert.Equal(CmdInjectExit.Usage, CmdInjectExitMap.ForGate(decision.Gate));
        Assert.Contains("SETUP", decision.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AMissingAllowlistFile_IsAllowlistUnusable()
    {
        var missing = Path.Combine(_directory, "does-not-exist.json");
        var decision = InjectionFence.Check(Rig, missing, Stamp);

        Assert.False(decision.Allowed);
        Assert.Equal(InjectionGate.AllowlistUnusable, decision.Gate);
    }

    [Fact]
    public void AMalformedAllowlist_IsAllowlistUnusable()
    {
        var decision = InjectionFence.Check(Rig, Allowlist("bad.json", "{ not json"), Stamp);

        Assert.False(decision.Allowed);
        Assert.Equal(InjectionGate.AllowlistUnusable, decision.Gate);
    }

    [Fact]
    public void ANonTestRigEntry_IsNotAnApprovedTestRig()
    {
        var decision = InjectionFence.Check(Rig, Allowlist("prod.json", Entry(kind: "production")), Stamp);

        Assert.False(decision.Allowed);
        Assert.Equal(InjectionGate.NotAnApprovedTestRig, decision.Gate);
    }

    [Fact]
    public void AnUnlistedAddress_IsNotAnApprovedTestRig()
    {
        var decision = InjectionFence.Check("10.10.10.11", Allowlist("ok.json", Entry()), Stamp);

        Assert.False(decision.Allowed);
        Assert.Equal(InjectionGate.NotAnApprovedTestRig, decision.Gate);
    }

    [Fact]
    public void WriteEligibleFalse_IsNotWriteEligible()
    {
        var decision = InjectionFence.Check(Rig, Allowlist("nowrite.json", Entry(writeEligible: false)), Stamp);

        Assert.False(decision.Allowed);
        Assert.Equal(InjectionGate.NotWriteEligible, decision.Gate);
    }

    [Fact]
    public void OutputsNotIsolated_IsOutputsNotIsolated()
    {
        var decision = InjectionFence.Check(Rig, Allowlist("hot.json", Entry(outputsIsolated: false)), Stamp);

        Assert.False(decision.Allowed);
        Assert.Equal(InjectionGate.OutputsNotIsolated, decision.Gate);
    }

    [Fact]
    public void IsolationAssertedByNobody_IsIsolationUnattributed()
    {
        var decision = InjectionFence.Check(Rig, Allowlist("unattr.json", Entry(isolationAssertedBy: null)), Stamp);

        Assert.False(decision.Allowed);
        Assert.Equal(InjectionGate.IsolationUnattributed, decision.Gate);
    }

    [Fact]
    public void NoExpectedBuildStamp_IsNoExpectedBuildStamp()
    {
        var decision = InjectionFence.Check(Rig, Allowlist("ok.json", Entry()), expectedBuildStamp: null);

        Assert.False(decision.Allowed);
        Assert.Equal(InjectionGate.NoExpectedBuildStamp, decision.Gate);
    }

    [Fact]
    public void EverythingSatisfied_IsAllowed()
    {
        var decision = InjectionFence.Check(Rig, Allowlist("ok.json", Entry()), Stamp);

        Assert.True(decision.Allowed);
        Assert.Equal(InjectionGate.Allowed, decision.Gate);
        Assert.NotNull(decision.MatchedEntry);
    }
}
