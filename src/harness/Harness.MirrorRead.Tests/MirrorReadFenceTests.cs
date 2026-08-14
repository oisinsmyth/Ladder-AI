namespace Harness.MirrorRead.Tests;

/// <summary>
/// THE FENCE, MUTATED IN BOTH DIRECTIONS.
///
/// <para>A fence has two ways to be wrong and they look nothing alike. <b>Disconnected</b>, it allows
/// everything and the run looks perfectly normal. <b>Over-firing</b>, it refuses everything, which
/// reads as safety right up until somebody removes it for being noise. Only one test catches each:</para>
/// <list type="bullet">
/// <item><see cref="AnApprovedTestRig_IsAllowedAndTheSessionIsOpened"/> goes red if the fence starts
/// refusing a properly authorised rig — including if somebody "hardens" it by consulting
/// <c>DeviceWriteGuard</c>, whose <c>writeEligible: false</c> on the live entry is correct and must
/// not block a read.</item>
/// <item>Every refusal test below goes red if the fence is deleted or moved below the connect, because
/// each asserts <c>Opens == 0</c> — the OBSERVABLE CONSEQUENCE, not the exit code. A disconnected gate
/// can still produce the right exit code by accident; it cannot leave the socket unopened.</item>
/// </list>
///
/// <para>These drive the REAL <c>AllowlistFile</c> and <c>DeviceAccessGuard</c> against real files on
/// disk. A fake fence would be a fence you can hand a permissive one to, which is the mutation rather
/// than the test.</para>
/// </summary>
public class MirrorReadFenceTests : IDisposable
{
    private readonly string _directory =
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "mirror-read-tests-" + Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp directory is not a test failure.
        }
    }

    private string WriteAllowlist(string name, string json)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, json);
        return path;
    }

    private const string RigAddress = "10.10.10.10";

    private static string RigEntry(string kind = "test-rig", bool writeEligible = false) =>
        $$"""
          {
            "entries": [
              {
                "address": "{{RigAddress}}",
                "label": "bench rig fixture",
                "kind": "{{kind}}",
                "writeEligible": {{(writeEligible ? "true" : "false")}}
              }
            ]
          }
          """;

    private static MirrorReadOptions Options(string? allowlist, string address = RigAddress) =>
        new(address, 503, 1, allowlist, DeclaredRegisters: 37, BoundaryFrom: 34, BoundaryTo: 38, IntervalMs: 0);

    // ---- the fence must ALLOW a properly authorised rig -------------------------------------------

    /// <summary>
    /// *** THE OVER-FIRING MUTATION'S DETECTOR. *** An approved test rig, whose entry says
    /// <c>writeEligible: false</c> exactly as the live one does, must be ALLOWED and the session must
    /// actually be opened. A fence that refuses this is a fence that refuses every real run.
    /// </summary>
    [Fact]
    public void AnApprovedTestRig_IsAllowedAndTheSessionIsOpened()
    {
        var allowlist = WriteAllowlist("ok.json", RigEntry(writeEligible: false));
        var factory = new RecordingFactory(() => new ScriptedSource(available: 37));
        var output = new StringWriter();

        var exit = MirrorReadRun.Execute(Options(allowlist), factory, output);

        Assert.Equal(1, factory.Opens);
        Assert.Equal(RigAddress, factory.LastHost);
        Assert.Equal(503, factory.LastPort);
        Assert.NotEqual(MirrorReadExit.Refused, exit);
        Assert.Contains("ALLOWED", output.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// The same, stated as its own fact because it is the one most likely to be "fixed" by somebody
    /// tightening the fence: <c>writeEligible: false</c> is the LIVE value and it must not refuse a read.
    /// Reads are governed by <c>DeviceAccessGuard</c>, writes by <c>DeviceWriteGuard</c>, and this
    /// binary consults only the first.
    /// </summary>
    [Fact]
    public void WriteEligibleFalse_DoesNotBlockARead()
    {
        var allowlist = WriteAllowlist("nowrite.json", RigEntry(writeEligible: false));
        var factory = new RecordingFactory(() => new ScriptedSource(available: 37));

        MirrorReadRun.Execute(Options(allowlist), factory, new StringWriter());

        Assert.Equal(1, factory.Opens);
    }

    // ---- the fence must REFUSE everything else, before any socket ---------------------------------

    [Fact]
    public void AnEntryThatIsNotATestRig_IsRefusedAndNothingConnects()
    {
        var allowlist = WriteAllowlist("production.json", RigEntry(kind: "production"));
        var factory = new RecordingFactory(() => new ScriptedSource(available: 37));
        var output = new StringWriter();

        var exit = MirrorReadRun.Execute(Options(allowlist), factory, output);

        Assert.Equal(MirrorReadExit.Refused, exit);
        Assert.Equal(0, factory.Opens);
        Assert.Contains("REFUSED", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void AnAddressThatIsNotListed_IsRefusedAndNothingConnects()
    {
        var allowlist = WriteAllowlist("other.json", RigEntry());
        var factory = new RecordingFactory(() => new ScriptedSource(available: 37));

        var exit = MirrorReadRun.Execute(Options(allowlist, address: "10.10.10.11"), factory, new StringWriter());

        Assert.Equal(MirrorReadExit.Refused, exit);
        Assert.Equal(0, factory.Opens);
    }

    [Fact]
    public void AMissingAllowlistFile_IsRefusedAndNothingConnects()
    {
        var factory = new RecordingFactory(() => new ScriptedSource(available: 37));
        var missing = Path.Combine(_directory, "does-not-exist.json");

        var exit = MirrorReadRun.Execute(Options(missing), factory, new StringWriter());

        Assert.Equal(MirrorReadExit.Refused, exit);
        Assert.Equal(0, factory.Opens);
    }

    /// <summary>
    /// *** NO ALLOWLIST IS A REFUSAL, NEVER A PASS. *** The CLI refuses this earlier with a distinct
    /// message, because "you configured nothing" and "that device is not approved" are different facts.
    /// This pins the layer underneath: even reached directly with a null path, nothing connects.
    /// </summary>
    [Fact]
    public void NoAllowlistConfiguredAtAll_IsRefusedAndNothingConnects()
    {
        var factory = new RecordingFactory(() => new ScriptedSource(available: 37));

        var exit = MirrorReadRun.Execute(Options(allowlist: null), factory, new StringWriter());

        Assert.Equal(MirrorReadExit.Refused, exit);
        Assert.Equal(0, factory.Opens);
    }

    [Fact]
    public void AnEmptyAllowlist_IsRefusedAndNothingConnects()
    {
        var allowlist = WriteAllowlist("empty.json", """{ "entries": [] }""");
        var factory = new RecordingFactory(() => new ScriptedSource(available: 37));

        var exit = MirrorReadRun.Execute(Options(allowlist), factory, new StringWriter());

        Assert.Equal(MirrorReadExit.Refused, exit);
        Assert.Equal(0, factory.Opens);
    }

    [Fact]
    public void AMalformedAllowlist_IsRefusedAndNothingConnects()
    {
        var allowlist = WriteAllowlist("broken.json", "{ this is not json");
        var factory = new RecordingFactory(() => new ScriptedSource(available: 37));

        var exit = MirrorReadRun.Execute(Options(allowlist), factory, new StringWriter());

        Assert.Equal(MirrorReadExit.Refused, exit);
        Assert.Equal(0, factory.Opens);
    }

    // ---- usage refusals happen before the fence, and before any socket ----------------------------

    /// <summary>
    /// A sweep that never reaches the first undeclared register would run, print, and prove nothing —
    /// it cannot tell an area of exactly the declared width from a wider one. Refused before connecting.
    /// </summary>
    [Fact]
    public void ASweepThatDoesNotStraddleTheEdge_IsAUsageErrorAndNothingConnects()
    {
        var allowlist = WriteAllowlist("ok.json", RigEntry());
        var factory = new RecordingFactory(() => new ScriptedSource(available: 37));
        var options = Options(allowlist) with { BoundaryFrom = 30, BoundaryTo = 35 };

        var exit = MirrorReadRun.Execute(options, factory, new StringWriter());

        Assert.Equal(MirrorReadExit.Usage, exit);
        Assert.Equal(0, factory.Opens);
    }

    /// <summary>
    /// And the other side of the same rule: a sweep entirely OUTSIDE the declared area has no register
    /// that must succeed, so a refusal at the far end is indistinguishable from a server refusing
    /// everything.
    /// </summary>
    [Fact]
    public void ASweepWithNoInsideControl_IsAUsageErrorAndNothingConnects()
    {
        var allowlist = WriteAllowlist("ok.json", RigEntry());
        var factory = new RecordingFactory(() => new ScriptedSource(available: 37));
        var options = Options(allowlist) with { BoundaryFrom = 37, BoundaryTo = 40 };

        var exit = MirrorReadRun.Execute(options, factory, new StringWriter());

        Assert.Equal(MirrorReadExit.Usage, exit);
        Assert.Equal(0, factory.Opens);
    }

    [Fact]
    public void AConnectFailure_IsItsOwnExitCode()
    {
        var allowlist = WriteAllowlist("ok.json", RigEntry());
        var factory = new FailingFactory();

        var exit = MirrorReadRun.Execute(Options(allowlist), factory, new StringWriter());

        Assert.Equal(MirrorReadExit.ConnectFailed, exit);
        Assert.Equal(1, factory.Opens);
    }
}
