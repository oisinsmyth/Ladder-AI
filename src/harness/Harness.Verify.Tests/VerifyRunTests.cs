using Harness.Map;
using Harness.Wire;

namespace Harness.Verify.Tests;

/// <summary>
/// <c>harness-verify</c> end to end, with a scripted mirror in place of the rig.
///
/// <para><b>What these tests can and cannot say.</b> They exercise the whole decision surface — compose,
/// fence, connect, version check, the three DB-6 attempts, the verdict — against a transport that answers
/// out of an array. What they cannot do is prove the tool works against a DEVICE; that claim belongs to
/// the recorded live run and to nothing here. <i>Knowing exactly what a green from a given tool does not
/// cover is what makes the green usable.</i></para>
/// </summary>
public class VerifyRunTests : IDisposable
{
    private readonly string _allowlist;

    public VerifyRunTests()
    {
        _allowlist = Path.Combine(Path.GetTempPath(), $"verify-allowlist-{Guid.NewGuid():N}.json");
        File.WriteAllText(_allowlist, """
        {
          "entries": [
            { "address": "10.0.0.1", "label": "scripted bench rig", "kind": "test-rig", "writeEligible": false }
          ]
        }
        """);
    }

    public void Dispose()
    {
        if (File.Exists(_allowlist)) File.Delete(_allowlist);
        GC.SuppressFinalize(this);
    }

    private (VerifyExit Exit, string Output, ScriptedTransport Transport, RecordingConnect Connect) Run(
        uint deviceStamp, string address = "10.0.0.1", string? allowlist = null)
    {
        var (map, _) = Fixtures.Composed();
        var transport = new ScriptedTransport(map, deviceStamp);
        var connect = new RecordingConnect(() => transport);
        var output = new StringWriter();

        var exit = VerifyRun.Execute(
            Fixtures.Options(allowlist ?? _allowlist, address),
            Fixtures.Files, Fixtures.Expand, connect.Factory, output);

        return (exit, output.ToString(), transport, connect);
    }

    // ---- the live shape: the device is carrying a DIFFERENT build --------------------------------

    /// <summary>
    /// 🔴 <b>THE SHAPE OF THE REAL RUN, and the one the rig produces today.</b> The device publishes a
    /// stamp that is not the composed one, so the version check reports <c>Stale</c>, control A is refused,
    /// the positive control is refused, and the acceptance control — holding the stamp the device itself
    /// published — is ADMITTED. Exit is <see cref="VerifyExit.NotConfirmed"/>: a statement about the
    /// DEVICE, made by a guard that was shown to work in both directions in the same run.
    /// </summary>
    [Fact]
    public void A_device_running_a_different_build_is_NotConfirmed_and_the_guard_behaves()
    {
        var (exit, output, transport, connect) = Run(deviceStamp: 0xF52ECEAD);

        Assert.Equal(VerifyExit.NotConfirmed, exit);
        Assert.Equal(1, connect.Calls);
        Assert.Equal(0, transport.Writes);
        Assert.True(transport.Reads > 3, $"only {transport.Reads} FC03(s) were served; the settling reads alone should exceed that.");

        Assert.Contains("Outcome       : Stale", output, StringComparison.Ordinal);
        Assert.Contains("Observed      : 16#F52ECEAD", output, StringComparison.Ordinal);
        Assert.Contains("POSITIVE CONTROL", output, StringComparison.Ordinal);
        Assert.Contains("ACCEPTANCE CONTROL", output, StringComparison.Ordinal);
        Assert.Contains("ADMITTED", output, StringComparison.Ordinal);
        Assert.Contains("DB-6", output, StringComparison.Ordinal);
        Assert.Contains("EXERCISED AGAINST THE DEVICE AND BEHAVED", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The converse, asserted as deliberately as the refusal.</b> A device carrying the composed build
    /// confirms, control A is admitted, and the positive control is STILL refused. A guard that refused
    /// every ordinary case would pass every test that only looks for refusals — and would be removed
    /// within a week, by someone who was right to.
    /// </summary>
    [Fact]
    public void A_device_running_the_composed_build_is_Confirmed_and_the_wrong_stamp_is_still_refused()
    {
        var (_, stamp) = Fixtures.Composed();
        var (exit, output, _, _) = Run(deviceStamp: stamp.Value);

        Assert.Equal(VerifyExit.Confirmed, exit);
        Assert.Contains("Outcome       : Confirmed", output, StringComparison.Ordinal);
        Assert.Contains("POSITIVE CONTROL", output, StringComparison.Ordinal);
        Assert.Contains("REFUSED", output, StringComparison.Ordinal);
        Assert.Contains("Control A was ADMITTED", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// A device publishing zero: <see cref="VerifyExit.NotEstablished"/>, <b>not</b>
    /// <see cref="VerifyExit.GuardDidNotBehave"/>. The acceptance control cannot be constructed, so the run
    /// establishes nothing about the guard — while establishing plenty about the device. Filing a device
    /// state under "the harness is broken" is a gate firing outside its scope.
    /// </summary>
    [Fact]
    public void A_device_publishing_zero_leaves_the_guard_NOT_ESTABLISHED_rather_than_accused()
    {
        var (exit, output, _, _) = Run(deviceStamp: 0);

        Assert.Equal(VerifyExit.NotEstablished, exit);
        Assert.Contains("Outcome       : Absent", output, StringComparison.Ordinal);
        Assert.Contains("NOT FULLY EXERCISED", output, StringComparison.Ordinal);
        Assert.DoesNotContain("THE GUARD DID NOT BEHAVE", output, StringComparison.Ordinal);
    }

    // ---- the fence, asserted on its observable consequence ---------------------------------------

    /// <summary>
    /// *** THE SENTINEL, NOT THE EXIT CODE. *** A disconnected fence can produce exit 3 by accident; only
    /// the connect counter says no socket was opened.
    /// </summary>
    [Fact]
    public void An_address_not_on_the_allowlist_is_refused_and_NOTHING_CONNECTS()
    {
        var (exit, output, _, connect) = Run(deviceStamp: 0xF52ECEAD, address: "192.168.0.99");

        Assert.Equal(VerifyExit.Refused, exit);
        Assert.Equal(0, connect.Calls);
        Assert.Contains("NO SOCKET WAS OPENED", output, StringComparison.Ordinal);
    }

    /// <summary>An allowlist file that does not exist grants nothing — and still opens nothing.</summary>
    [Fact]
    public void A_missing_allowlist_refuses_and_NOTHING_CONNECTS()
    {
        var (exit, _, _, connect) = Run(deviceStamp: 0xF52ECEAD, allowlist: Path.Combine(Path.GetTempPath(), "no-such-allowlist.json"));

        Assert.Equal(VerifyExit.Refused, exit);
        Assert.Equal(0, connect.Calls);
    }

    /// <summary>
    /// <b>The fence's own positive control.</b> Without it, every refusal above is equally what a fence
    /// that refuses everything produces — and a run that connects to nothing would satisfy the two tests
    /// above perfectly.
    /// </summary>
    [Fact]
    public void The_allowlisted_address_DOES_connect()
    {
        var (_, _, _, connect) = Run(deviceStamp: 0xF52ECEAD);

        Assert.Equal(1, connect.Calls);
    }

    // ---- inputs that stop the run before the wire ------------------------------------------------

    [Fact]
    public void A_transport_that_will_not_open_is_NothingRead_and_says_so()
    {
        var (map, _) = Fixtures.Composed();
        _ = map;
        var connect = new RecordingConnect();
        var output = new StringWriter();

        var exit = VerifyRun.Execute(
            Fixtures.Options(_allowlist), Fixtures.Files, Fixtures.Expand, connect.Factory, output);

        Assert.Equal(VerifyExit.NothingRead, exit);
        Assert.Equal(1, connect.Calls);
        Assert.Contains("AN UNREACHABLE DEVICE IS NOT A VERIFIED ONE", output.ToString(), StringComparison.Ordinal);
    }

    /// <summary>A binding that names no slots cannot produce a map, so there is nothing to check and no socket is opened.</summary>
    [Fact]
    public void A_binding_that_composes_to_nothing_is_NotComposed_and_NOTHING_CONNECTS()
    {
        var connect = new RecordingConnect();
        var output = new StringWriter();

        var exit = VerifyRun.Execute(
            Fixtures.Options(_allowlist),
            path => path == Fixtures.BindingPath ? """{ "blockName": "FC_X", "slots": [] }""" : Fixtures.Files(path),
            Fixtures.Expand, connect.Factory, output);

        Assert.Equal(VerifyExit.NotComposed, exit);
        Assert.Equal(0, connect.Calls);
    }

    [Fact]
    public void An_unreadable_program_file_is_NothingExamined_and_NOTHING_CONNECTS()
    {
        var connect = new RecordingConnect();
        var output = new StringWriter();

        var exit = VerifyRun.Execute(
            Fixtures.Options(_allowlist),
            path => path == Fixtures.ProgramPath ? "not an IR header at all" : Fixtures.Files(path),
            Fixtures.Expand, connect.Factory, output);

        Assert.Equal(VerifyExit.NothingExamined, exit);
        Assert.Equal(0, connect.Calls);
    }

    // ---- ReadsToSettle, the measurement -----------------------------------------------------------

    /// <summary>
    /// <b>A steady register settles at read 1 and the report says so.</b> This pins the ARITHMETIC of
    /// <c>ReadsToSettle</c> against a mirror that never changes, which is what the rig presents; the number
    /// the live run prints means "the value was already stable when read 1 was taken".
    /// </summary>
    [Fact]
    public void A_steady_register_reports_ReadsToSettle_of_one_and_the_run_prints_its_scope()
    {
        var (_, output, _, _) = Run(deviceStamp: 0xF52ECEAD);

        Assert.Contains("ReadsToSettle : 1", output, StringComparison.Ordinal);
        Assert.Contains("Reads         : 3", output, StringComparison.Ordinal);

        // 🔴 The measurement's LIMIT is printed where a reader of RESULTS meets it, not only in a report
        // somebody read once. A caveat that lives in a lane report has already failed the person it was
        // written for.
        Assert.Contains("THIS RUN DID NOT ENTER THE CONDITION", output, StringComparison.Ordinal);
    }

    // ---- the wrong stamp is derived, never a literal ----------------------------------------------

    [Theory]
    [InlineData(0x33434A68u, 0xF52ECEADu)]
    [InlineData(1u, 0u)]
    [InlineData(0u, 1u)]
    [InlineData(uint.MaxValue, uint.MaxValue - 1)]
    public void The_wrong_stamp_differs_from_both_inputs_and_is_never_zero(uint composed, uint observed)
    {
        var wrong = VerifyRun.WrongStamp(composed, observed);

        Assert.NotEqual(composed, wrong);
        Assert.NotEqual(observed, wrong);
        Assert.NotEqual(0u, wrong);
    }
}
