using System;
using System.Collections.Generic;
using System.Linq;
using OpennessCli;
using OpennessCli.Cli;
using OpennessCli.Model;
using OpennessCli.Openness;
using Xunit;

namespace OpennessCli.Tests;

/// <summary>
/// `hw-identifiers` — reading a hardware identifier off the device's OWN configuration.
///
/// <para><b>Why it exists.</b> <c>Harness.Map/CommsFbGenerator.cs</c> says of the <c>HW_ANY</c>
/// interface identifier a harness comms block carries: "A hardware address: read it off the
/// device's own configuration, never from another project's block." Until this command there was
/// no way in this CLI to do that — no system-constant command, no hardware-identifier command —
/// so on a real job the value was carried across from a sibling project and shipped flagged as
/// unverified. A wrong identifier is not a compile error; it is a connection that never
/// establishes.</para>
///
/// <para><b>The two properties worth testing are both about what it must NOT do.</b> It must not
/// contact a device, and it must not report "no identifiers" and "I looked nowhere" as the same
/// thing.</para>
/// </summary>
public class HardwareIdentifierTests
{
    private static HardwareIdentifierResult Result(
        int walked, params HardwareIdentifierItem[] items) =>
        new(items, walked, items.Count(i => i.Attributes.Count > 0), DeviceFilter: null);

    private static HardwareIdentifierItem Item(string path, params (string Name, string Value)[] attributes) =>
        new(path, "DeviceItemImpl",
            attributes.Select(a => new HardwareIdentifierAttribute(a.Name, a.Value)).ToList());

    // -------------------------------------------------------------------------------------------
    // EMPTY IS NOT CLEAN — the distinction this command exists to keep
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>WALKING NOTHING IS EXIT 14, NOT EXIT 0.</b>
    ///
    /// <para>A <c>--device</c> filter that matched nothing produces exactly the same empty item list
    /// as a device tree carrying no identifiers. Returning success for the first would report the
    /// FILTER's silence as a fact about the hardware — the project's standing exit-2/14 contract,
    /// and the same reasoning as <c>converter</c>'s "a check compared against nothing".</para>
    /// </summary>
    [Fact]
    public void WalkingNoItemsIsNothingExamined_NotSuccess()
    {
        var gateway = new FakeGateway
        {
            HardwareIdentifiers = new HardwareIdentifierResult(
                Array.Empty<HardwareIdentifierItem>(), ItemsWalked: 0, ItemsWithIdentifiers: 0,
                DeviceFilter: "no-such-device"),
        };

        var exit = Program.RunHardwareIdentifiers(
            gateway,
            new HardwareIdentifierCommandOptions("p", "no-such-device", Json: false, AllAttributes: false, null, 1, 1),
            timeoutOpenSeconds: 1);

        Assert.Equal(ExitCodes.NothingExamined, exit);
    }

    /// <summary>
    /// 🔴 <b>THE CONTROL, and without it the case above could be satisfied by refusing everything.</b>
    ///
    /// <para>Items walked, none carrying an identifier, is an EARNED zero and must exit 0. Conflating
    /// it with the refusal would make the command useless on exactly the device trees where "nothing
    /// declares one" is the true and useful answer.</para>
    /// </summary>
    [Fact]
    public void ItemsWalkedButNoIdentifiersIsAnEarnedZero_AndSucceeds()
    {
        var gateway = new FakeGateway { HardwareIdentifiers = Result(walked: 12) };

        var exit = Program.RunHardwareIdentifiers(
            gateway,
            new HardwareIdentifierCommandOptions("p", null, Json: false, AllAttributes: false, null, 1, 1),
            timeoutOpenSeconds: 1);

        Assert.Equal(ExitCodes.Success, exit);
    }

    // -------------------------------------------------------------------------------------------
    // IT READS. IT DOES NOT REACH A DEVICE.
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// The command performs no download and no device write on ANY path — asserted by call count on
    /// the fake gateway, in both output modes and through the refusal path, rather than by reading
    /// the implementation and believing it.
    /// </summary>
    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 0)]
    [InlineData(true, 5)]
    [InlineData(false, 5)]
    public void NoPathDownloadsOrPlansADownload(bool json, int walked)
    {
        var gateway = new FakeGateway { HardwareIdentifiers = Result(walked) };

        Program.RunHardwareIdentifiers(
            gateway,
            new HardwareIdentifierCommandOptions("p", null, json, AllAttributes: false, null, 1, 1),
            timeoutOpenSeconds: 1);

        Assert.Equal(0, gateway.BuildDownloadPlanCalls);
        Assert.Equal(1, gateway.ReadHardwareIdentifiersCalls);
    }

    /// <summary>The device filter reaches the gateway rather than being silently dropped.</summary>
    [Fact]
    public void TheDeviceFilterIsPassedThrough()
    {
        var gateway = new FakeGateway { HardwareIdentifiers = Result(walked: 3) };

        Program.RunHardwareIdentifiers(
            gateway,
            new HardwareIdentifierCommandOptions("p", "PLC1", Json: false, AllAttributes: false, null, 1, 1),
            timeoutOpenSeconds: 1);

        Assert.Equal("PLC1", gateway.LastHardwareIdentifierFilter);
    }

    // -------------------------------------------------------------------------------------------
    // THE REPORT SAYS WHAT IT CANNOT SEE
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 Both renderings must carry the stale-project caveat. This value is consumed by someone
    /// deciding whether a download will connect, and a confident number with no statement of its
    /// own limits is the shape that gets quoted as a measurement of the CONTROLLER.
    /// </summary>
    [Fact]
    public void BothRenderingsSayTheyReadTheProjectAndNotTheController()
    {
        var result = Result(walked: 2, Item("Station/PLC1", ("HwIdentifier", "64")));

        var table = OutputFormatter.FormatHardwareIdentifiersTable(result);
        var json = OutputFormatter.FormatHardwareIdentifiersJson(result);

        Assert.Contains("NOT FROM A CONTROLLER", table, StringComparison.Ordinal);
        Assert.Contains("cannotSee", json, StringComparison.Ordinal);
        Assert.Contains("never the CPU", json, StringComparison.Ordinal);
    }

    /// <summary>A walked-nothing report says so IN THE BODY, not only in the exit code — the exit
    /// code is invisible to somebody reading a saved transcript.</summary>
    [Fact]
    public void TheNothingExaminedReportSaysSoInItsOwnText()
    {
        var result = new HardwareIdentifierResult(
            Array.Empty<HardwareIdentifierItem>(), ItemsWalked: 0, ItemsWithIdentifiers: 0, DeviceFilter: "x");

        var table = OutputFormatter.FormatHardwareIdentifiersTable(result);

        Assert.Contains("NOTHING EXAMINED", table, StringComparison.Ordinal);
        Assert.Contains("Empty is not clean", table, StringComparison.Ordinal);
    }

    /// <summary>And an earned zero says it is one, with the denominator it earned it against.</summary>
    [Fact]
    public void AnEarnedZeroNamesWhatItExamined()
    {
        var table = OutputFormatter.FormatHardwareIdentifiersTable(Result(walked: 9));

        Assert.Contains("EARNED ZERO", table, StringComparison.Ordinal);
        Assert.Contains("9 item(s) were examined", table, StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------------------------------
    // PARSING
    // -------------------------------------------------------------------------------------------

    [Fact]
    public void ParsesProjectAndFilter()
    {
        var parsed = ArgumentParser.Parse(new[] { "hw-identifiers", "proj.ap20", "--device", "PLC1", "--json" });

        var success = Assert.IsType<ParseResult.HardwareIdentifierSuccess>(parsed);
        Assert.Equal("proj.ap20", success.Options.ProjectIdentifier);
        Assert.Equal("PLC1", success.Options.Device);
        Assert.True(success.Options.Json);
    }

    [Fact]
    public void MissingProjectIsAUsageFailure()
    {
        Assert.IsType<ParseResult.Failure>(ArgumentParser.Parse(new[] { "hw-identifiers" }));
    }

    /// <summary>
    /// `--yes` and `--force` are refused BY NAME, exactly as `download-plan` refuses them. Somebody
    /// typing one has concluded this command can be talked into touching a device; contradicting
    /// that belief is more useful than ignoring an unknown flag.
    /// </summary>
    [Theory]
    [InlineData("--yes")]
    [InlineData("--force")]
    public void ConfirmationFlagsAreRefusedByName(string flag)
    {
        var parsed = ArgumentParser.Parse(new[] { "hw-identifiers", "proj.ap20", flag });

        var failure = Assert.IsType<ParseResult.Failure>(parsed);
        Assert.Contains(flag, failure.Message, StringComparison.Ordinal);
        Assert.Contains("no device contact", failure.Message, StringComparison.Ordinal);
    }
}
