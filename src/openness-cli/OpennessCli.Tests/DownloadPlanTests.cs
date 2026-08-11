using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using OpennessCli;
using OpennessCli.Cli;
using OpennessCli.Model;
using OpennessCli.Openness;
using Xunit;

namespace OpennessCli.Tests;

/// <summary>
/// `download-plan` — the safe half of a download capability: everything up to, and deliberately not
/// including, transferring anything to a device.
///
/// Two properties are load-bearing here and neither can be checked against a live Portal, because
/// checking them against a live Portal would mean risking the thing being prevented:
///
///   1. NO PATH THROUGH THIS COMMAND PERFORMS A DOWNLOAD. Asserted by counting calls on the fake
///      gateway across every option value, both output modes, and each failure mode — plus a
///      reflection check over the shipped assembly that no method anywhere in it references
///      <c>DownloadProvider.Download</c>. The counter catches a call this command might make; the
///      reflection catches one any other part of the binary might make.
///
///   2. THE REPORT STATES THE GRANULARITY. Openness's download API is device-level, so "download my
///      new DB" is not a thing it does. That misconception is what the command exists to prevent,
///      and a report that stated it only sometimes would fail exactly when someone was in a hurry.
/// </summary>
public class DownloadPlanTests
{
    // ---- argument parsing ------------------------------------------------------------------

    [Fact]
    public void Parse_Minimal_DefaultsToWholeSoftware()
    {
        var result = ArgumentParser.Parse(new[] { "download-plan", "C:\\proj\\My.ap20" });

        var success = Assert.IsType<ParseResult.DownloadPlanSuccess>(result);
        Assert.Null(success.Options.Device);
        Assert.False(success.Options.Json);

        // Not SoftwareOnlyChanges. That value is the one most easily misread as "only my changed
        // block", so inheriting it by default would hand the misconception to anyone who left the
        // flag off.
        Assert.Equal(DownloadOptionKind.Software, success.Options.Options);
    }

    [Theory]
    [InlineData("Software", DownloadOptionKind.Software)]
    [InlineData("software", DownloadOptionKind.Software)]
    [InlineData("SoftwareOnlyChanges", DownloadOptionKind.SoftwareOnlyChanges)]
    [InlineData("softwareonlychanges", DownloadOptionKind.SoftwareOnlyChanges)]
    [InlineData("Hardware", DownloadOptionKind.Hardware)]
    [InlineData("HARDWARE", DownloadOptionKind.Hardware)]
    public void Parse_Options_IsCaseInsensitive(string raw, DownloadOptionKind expected)
    {
        var result = ArgumentParser.Parse(new[] { "download-plan", "C:\\proj\\My.ap20", "--options", raw });

        var success = Assert.IsType<ParseResult.DownloadPlanSuccess>(result);
        Assert.Equal(expected, success.Options.Options);
    }

    /// <summary>
    /// An unrecognised <c>--options</c> value is a HARD ERROR naming all three valid ones.
    ///
    /// The awkward cases are in here on purpose. "0"/"2" would be accepted by <c>Enum.TryParse</c>,
    /// resolving a typo to a value nobody named. "None" is a real member of Siemens's own
    /// <c>DownloadOptions</c> and selects neither hardware nor software — accepting it would print a
    /// confident plan for a transfer of nothing, which is this command's most dangerous possible
    /// output because it reads as reassurance.
    /// </summary>
    [Theory]
    [InlineData("None")]
    [InlineData("none")]
    [InlineData("SoftwareOnly")]
    [InlineData("Changes")]
    [InlineData("0")]
    [InlineData("2")]
    [InlineData("")]
    public void Parse_UnrecognisedOptions_FailsNamingAllThreeValidValues(string bad)
    {
        var result = ArgumentParser.Parse(new[] { "download-plan", "C:\\proj\\My.ap20", "--options", bad });

        var failure = Assert.IsType<ParseResult.Failure>(result);
        Assert.Contains("Software", failure.Message, StringComparison.Ordinal);
        Assert.Contains("SoftwareOnlyChanges", failure.Message, StringComparison.Ordinal);
        Assert.Contains("Hardware", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_UnrecognisedOptions_ExplainsThatNoneOfThemIsPerBlock()
    {
        var result = ArgumentParser.Parse(new[] { "download-plan", "C:\\proj\\My.ap20", "--options", "MyBlock" });

        var failure = Assert.IsType<ParseResult.Failure>(result);
        Assert.Contains("per-block", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_OptionsWithNoValue_Fails()
    {
        var result = ArgumentParser.Parse(new[] { "download-plan", "C:\\proj\\My.ap20", "--options" });
        Assert.IsType<ParseResult.Failure>(result);
    }

    [Fact]
    public void Parse_MissingProject_Fails()
    {
        var result = ArgumentParser.Parse(new[] { "download-plan", "--options", "Software" });
        Assert.IsType<ParseResult.Failure>(result);
    }

    [Fact]
    public void Parse_Device_IsCarried()
    {
        var result = ArgumentParser.Parse(new[] { "download-plan", "C:\\proj\\My.ap20", "--device", "PLC_1" });

        var success = Assert.IsType<ParseResult.DownloadPlanSuccess>(result);
        Assert.Equal("PLC_1", success.Options.Device);
    }

    /// <summary>
    /// `--yes` and `--force` are refused BY NAME rather than ignored as unknown flags. Someone who
    /// types one has concluded this command can be talked into downloading, and the correction is
    /// worth more than the silence.
    /// </summary>
    [Theory]
    [InlineData("--yes")]
    [InlineData("--force")]
    public void Parse_ConfirmationFlags_AreRefusedByName(string flag)
    {
        var result = ArgumentParser.Parse(new[] { "download-plan", "C:\\proj\\My.ap20", flag });

        var failure = Assert.IsType<ParseResult.Failure>(result);
        Assert.Contains(flag, failure.Message, StringComparison.Ordinal);
        Assert.Contains("plans only", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_IsRegisteredInBothDispatchSwitches()
    {
        // The `hmi` regression this guards against: a subcommand shipped its own dispatch case,
        // built clean, and still died at runtime on one of these two accessors.
        var parsed = ArgumentParser.Parse(new[] { "download-plan", "C:\\proj\\My.ap20", "--timeout-open", "60" });

        Assert.Equal("C:\\proj\\My.ap20", ArgumentParser.ProjectIdentifier(parsed));
        Assert.Equal(60, ArgumentParser.CommonOptions(parsed).TimeoutOpenSeconds);
    }

    // ---- THE test: nothing downloads --------------------------------------------------------

    /// <summary>
    /// THE test this command exists for. Every option value, both output modes: the download call is
    /// never made.
    ///
    /// It counts on the gateway rather than checking for an exception because those are different
    /// claims. An exception test passes if a download was attempted and then refused; this one only
    /// passes if it was never attempted.
    /// </summary>
    [Theory]
    [InlineData(DownloadOptionKind.Software, false)]
    [InlineData(DownloadOptionKind.Software, true)]
    [InlineData(DownloadOptionKind.SoftwareOnlyChanges, false)]
    [InlineData(DownloadOptionKind.SoftwareOnlyChanges, true)]
    [InlineData(DownloadOptionKind.Hardware, false)]
    [InlineData(DownloadOptionKind.Hardware, true)]
    public void NoDownloadIsEverInvoked(DownloadOptionKind options, bool json)
    {
        var gateway = new FakeGateway { Plan = SamplePlan(options) };

        var (exitCode, stdout, _) = CaptureConsole(() =>
            Program.RunDownloadPlan(gateway, PlanOptions(options) with { Json = json }, timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(0, gateway.PerformDownloadCalls);
        Assert.Equal(1, gateway.BuildDownloadPlanCalls);
        Assert.Equal(0, gateway.SaveCalls);
        Assert.NotEmpty(stdout);
    }

    [Fact]
    public void NoDownloadIsInvoked_EvenWhenThePlanCannotBeBuilt()
    {
        var gateway = new FakeGateway { ThrowOnBuildDownloadPlan = new DeviceNotFoundException("PLC_9") };

        Assert.Throws<DeviceNotFoundException>(() => CaptureConsole(() =>
            Program.RunDownloadPlan(gateway, PlanOptions(), timeoutOpenSeconds: 1)));

        Assert.Equal(0, gateway.PerformDownloadCalls);
    }

    [Fact]
    public void NoDownloadIsInvoked_WhenNoProviderWasFound()
    {
        var gateway = new FakeGateway { Plan = SamplePlan() with { Provider = NoProvider() } };

        CaptureConsole(() => Program.RunDownloadPlan(gateway, PlanOptions(), timeoutOpenSeconds: 1));

        Assert.Equal(0, gateway.PerformDownloadCalls);
    }

    /// <summary>
    /// The gateway method that would download throws, in both implementations, unconditionally.
    /// </summary>
    [Fact]
    public void PerformDownload_AlwaysThrows_OnTheRealGateway()
    {
        using var gateway = new OpennessGateway();

        // Note this needs no Connect and no OpenProject: the refusal is not conditional on any
        // session state, so there is no way to reach a state in which it would proceed.
        var ex = Assert.Throws<DownloadNotEnabledException>(() =>
            gateway.PerformDownload(deviceFilter: null, DownloadOptionKind.Software));

        Assert.Contains(DownloadNotEnabledException.StandardMessage, ex.Message, StringComparison.Ordinal);
        Assert.IsAssignableFrom<NotSupportedException>(ex);
    }

    /// <summary>
    /// Reaching <c>PerformDownload</c> is a bug in this tool, not a user error, so it is classified
    /// as an internal fault and prints as one. Nobody can produce it by mistyping an argument.
    /// </summary>
    [Fact]
    public void DownloadNotEnabled_ClassifiesAsAnInternalFault()
    {
        Assert.Equal(ExitCodes.UnexpectedError, ExitCodes.ForException(new DownloadNotEnabledException()));
    }

    /// <summary>
    /// The assertion the counter cannot make: that NOTHING ANYWHERE in the shipped assembly calls
    /// <c>DownloadProvider.Download</c>.
    ///
    /// The fake gateway proves this command does not download. It cannot prove some other method
    /// does not. This reads the compiled IL of every method in the assembly and looks for a call to
    /// any member named <c>Download</c> on a Siemens download type — so it fails if such a call is
    /// added anywhere, including inside a branch no test exercises, and it is not fooled by a
    /// commented-out call being uncommented.
    ///
    /// NEGATIVE-TESTED, 2026-08-11. A check that has never been shown to detect anything is not a
    /// check — a green result from a scanner that silently matches nothing is the most reassuring
    /// output this file could produce and would mean nothing at all. So the predicate was
    /// temporarily retargeted at a call known to exist in this same assembly
    /// (<c>ICompilable.Compile</c>, in <c>Siemens.Engineering.Compiler</c>) and the test FAILED,
    /// naming <c>OpennessCli.Openness.OpennessGateway.RunCompile: calls
    /// Siemens.Engineering.Compiler.ICompilable.Compile</c>. The IL walk, the token resolution and
    /// the namespace match are therefore all confirmed working against a real Siemens interface call
    /// on a real method body; only the name being searched for was then changed back.
    /// </summary>
    [Fact]
    public void NoMethodInTheAssemblyReferencesDownloadProviderDownload()
    {
        var assembly = typeof(OpennessGateway).Assembly;
        var offenders = new List<string>();

        foreach (var type in assembly.GetTypes())
        {
            foreach (var method in type.GetMethods(
                         BindingFlags.Public | BindingFlags.NonPublic |
                         BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                var body = SafeGetBody(method);
                if (body is null)
                {
                    continue;
                }

                if (ReferencesSiemensDownloadCall(assembly, body, method, out var detail))
                {
                    offenders.Add($"{type.FullName}.{method.Name}: {detail}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "This binary must contain no call to DownloadProvider.Download anywhere. Found: " +
            string.Join("; ", offenders));
    }

    // ---- report content ---------------------------------------------------------------------

    /// <summary>
    /// The granularity statement appears on EVERY run, in both modes and for every option value.
    /// Not conditional on anything: the belief it corrects is what a reader arrives with, not
    /// something a particular flag triggers.
    /// </summary>
    [Theory]
    [InlineData(DownloadOptionKind.Software)]
    [InlineData(DownloadOptionKind.SoftwareOnlyChanges)]
    [InlineData(DownloadOptionKind.Hardware)]
    public void Table_AlwaysStatesThatTheUnitIsTheWholePlc(DownloadOptionKind options)
    {
        var text = OutputFormatter.FormatDownloadPlanTable(SamplePlan(options));

        Assert.Contains("WHOLE PLC SOFTWARE", text, StringComparison.Ordinal);
        Assert.Contains("NO per-block download", text, StringComparison.Ordinal);
        Assert.Contains("CANNOT DOWNLOAD", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Table_ExplainsThatSoftwareOnlyChangesIsNotPerBlock()
    {
        var text = OutputFormatter.FormatDownloadPlanTable(SamplePlan(DownloadOptionKind.SoftwareOnlyChanges));

        Assert.Contains("SoftwareOnlyChanges", text, StringComparison.Ordinal);
        Assert.Contains("the blocks you edited", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Table_NamesTheDeviceTheOptionsAndWhatWouldBeCarried()
    {
        var text = OutputFormatter.FormatDownloadPlanTable(SamplePlan());

        Assert.Contains("DEVICE: PLC_1", text, StringComparison.Ordinal);
        Assert.Contains("OPTIONS: Software", text, StringComparison.Ordinal);
        Assert.Contains("34 block(s)", text, StringComparison.Ordinal);
        Assert.Contains("5 PLC data type(s)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Table_ReportsWhereTheProviderCameFrom_AndEveryObjectAsked()
    {
        var text = OutputFormatter.FormatDownloadPlanTable(SamplePlan());

        Assert.Contains("GetService<DownloadProvider>()", text, StringComparison.Ordinal);
        Assert.Contains("DeviceItem", text, StringComparison.Ordinal);

        // The objects that REFUSED are reported too — "the CPU answered and the station did not" is
        // the finding, and a report showing only the winner would have to be re-derived by hand.
        Assert.Contains("no-provider", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The connection section must say, unmissably, that it read the project and not the network.
    /// A configured address and a reachable one look identical here, and mistaking the first for the
    /// second is the only wrong conclusion this section can produce.
    /// </summary>
    [Fact]
    public void Table_StatesThatTheConnectionWasReadFromTheProjectNotTheNetwork()
    {
        var text = OutputFormatter.FormatDownloadPlanTable(SamplePlan());

        Assert.Contains("READ FROM THE PROJECT, NOT FROM THE NETWORK", text, StringComparison.Ordinal);
        Assert.Contains("GetAccessibleDevices() (a live scan) is never called", text, StringComparison.Ordinal);
        Assert.Contains("TARGET ADDRESS: 192.0.2.10", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Table_UnconfiguredConnection_SaysADownloadWouldHaveNoRoute()
    {
        var plan = SamplePlan() with
        {
            Connection = new DownloadConnectionPlan(
                IsConfigured: false, EnableLegacyCommunication: false, Modes: Array.Empty<DownloadConnectionMode>()),
        };

        var text = OutputFormatter.FormatDownloadPlanTable(plan);

        Assert.Contains("no online connection configured", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Table_ReportsInconsistentBlocksAsUnverifiedContentADownloadWouldCarry()
    {
        var plan = SamplePlan() with { InconsistentBlocks = new[] { "FC_A", "FB_B" } };

        var text = OutputFormatter.FormatDownloadPlanTable(plan);

        Assert.Contains("UNVERIFIED CONTENT", text, StringComparison.Ordinal);
        Assert.Contains("FC_A", text, StringComparison.Ordinal);
        Assert.Contains("compile-all", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Table_NoProvider_SaysTheReportDescribesNothing()
    {
        var plan = SamplePlan() with { Provider = NoProvider(), Connection = null };

        var text = OutputFormatter.FormatDownloadPlanTable(plan);

        Assert.Contains("NOT OBTAINABLE", text, StringComparison.Ordinal);
        Assert.Contains("no DownloadProvider was obtained", text, StringComparison.Ordinal);
    }

    // ---- JSON shape --------------------------------------------------------------------------

    [Fact]
    public void Json_CarriesTheRefusalAndTheGranularityAsFIELDS()
    {
        // In the payload, not only in prose: a scripted consumer has to be able to read both without
        // parsing English, and a consumer keying on a MISSING property could not tell "this build
        // refuses" from "this build predates the flag".
        var json = OutputFormatter.FormatDownloadPlanJson(SamplePlan());

        Assert.Contains("\"canDownload\": false", json, StringComparison.Ordinal);
        Assert.Contains("\"granularity\"", json, StringComparison.Ordinal);
        Assert.Contains("WHOLE PLC SOFTWARE", json, StringComparison.Ordinal);
        Assert.Contains("\"cannotDownloadReason\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Json_CarriesTheProviderAcquisitionPathAndTheAttempts()
    {
        var json = OutputFormatter.FormatDownloadPlanJson(SamplePlan());

        Assert.Contains("\"acquisition\": \"IEngineeringServiceProvider.GetService<DownloadProvider>()\"", json, StringComparison.Ordinal);
        Assert.Contains("\"found\": true", json, StringComparison.Ordinal);
        Assert.Contains("\"attempts\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Json_MarksTheConnectionAsProbedWithoutConnecting()
    {
        var json = OutputFormatter.FormatDownloadPlanJson(SamplePlan());

        Assert.Contains("\"probedWithoutConnecting\": true", json, StringComparison.Ordinal);
        Assert.Contains("\"allTargetAddresses\"", json, StringComparison.Ordinal);
        Assert.Contains("192.0.2.10", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Json_NoProvider_CarriesANullConnectionRatherThanAnEmptyOne()
    {
        var json = OutputFormatter.FormatDownloadPlanJson(SamplePlan() with { Provider = NoProvider(), Connection = null });

        Assert.Contains("\"connection\": null", json, StringComparison.Ordinal);
        Assert.Contains("\"found\": false", json, StringComparison.Ordinal);
    }

    // ---- exit codes --------------------------------------------------------------------------

    [Fact]
    public void NoProvider_ExitsIncomplete_NotSuccess()
    {
        // Empty is not clean (FI-44). A plan that obtained no provider answered none of the questions
        // the command asks, and exiting 0 would present that silence as a clean bill of health.
        var gateway = new FakeGateway { Plan = SamplePlan() with { Provider = NoProvider(), Connection = null } };

        var (exitCode, _, stderr) = CaptureConsole(() =>
            Program.RunDownloadPlan(gateway, PlanOptions(), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.DownloadPlanIncomplete, exitCode);
        Assert.NotEqual(ExitCodes.Success, exitCode);
        Assert.Contains("DOWNLOAD PLAN INCOMPLETE", stderr, StringComparison.Ordinal);
        Assert.Equal(0, gateway.PerformDownloadCalls);
    }

    [Fact]
    public void DownloadPlanIncomplete_HasItsOwnCode()
    {
        Assert.Equal(16, ExitCodes.DownloadPlanIncomplete);
        Assert.NotEqual(ExitCodes.Success, ExitCodes.DownloadPlanIncomplete);
        Assert.NotEqual(ExitCodes.CommandError, ExitCodes.DownloadPlanIncomplete);
    }

    // ---- device resolution -------------------------------------------------------------------

    [Fact]
    public void DeviceNotFound_IsAUserFixableCommandError()
    {
        var gateway = new FakeGateway { ThrowOnBuildDownloadPlan = new DeviceNotFoundException("PLC_9") };

        var ex = Assert.Throws<DeviceNotFoundException>(() => CaptureConsole(() =>
            Program.RunDownloadPlan(gateway, PlanOptions() with { Device = "PLC_9" }, timeoutOpenSeconds: 1)));

        Assert.Equal(ExitCodes.CommandError, ExitCodes.ForException(ex));
    }

    /// <summary>
    /// Ambiguity is a refusal, never a guess. `BuildDownloadPlan` requires exactly one PLC device —
    /// planning against the wrong one would describe a download of a device nobody meant.
    /// </summary>
    [Fact]
    public void AmbiguousDevice_IsRefused_NotGuessed()
    {
        var gateway = new FakeGateway { ThrowOnBuildDownloadPlan = new DeviceNotFoundException(null) };

        var ex = Assert.Throws<DeviceNotFoundException>(() => CaptureConsole(() =>
            Program.RunDownloadPlan(gateway, PlanOptions(), timeoutOpenSeconds: 1)));

        Assert.Contains("more than one", ex.Message, StringComparison.Ordinal);
        Assert.Equal(0, gateway.PerformDownloadCalls);
    }

    [Fact]
    public void DeviceFilterAndOptions_ArePassedThroughToTheGateway()
    {
        var gateway = new FakeGateway { Plan = SamplePlan(DownloadOptionKind.Hardware) };

        CaptureConsole(() => Program.RunDownloadPlan(
            gateway,
            PlanOptions(DownloadOptionKind.Hardware) with { Device = "PLC_1" },
            timeoutOpenSeconds: 1));

        Assert.Equal("PLC_1", gateway.LastDeviceFilter);
        Assert.Equal(DownloadOptionKind.Hardware, gateway.LastRequestedOptions);
    }

    // ---- safety ------------------------------------------------------------------------------

    /// <summary>
    /// A device carrying safety content is refused at the PLAN, and the refusal says why it has to
    /// be: download granularity is device-level, so there is no download of that PLC which excludes
    /// its safety program. This is not the export path's refusal copied across — nobody named a
    /// safety block here, and the plan is refused anyway.
    /// </summary>
    [Fact]
    public void SafetyContent_IsRefusedAtThePlan_AndTheReasonNamesDeviceLevelGranularity()
    {
        var refusal = SafetyContentRefusedException.ForWholeDeviceDownload(
            "S7-1200 station/PLC_1", 3, "F_MainSafety", "F_LAD");

        Assert.Equal(ExitCodes.SafetyRefused, ExitCodes.ForException(refusal));
        Assert.Contains("DEVICE-LEVEL", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("F_MainSafety", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("hard rule 2", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SafetyRefusal_ReachesTheCallerAndNothingIsDownloaded()
    {
        var gateway = new FakeGateway
        {
            ThrowOnBuildDownloadPlan = SafetyContentRefusedException.ForWholeDeviceDownload(
                "S7-1200 station/PLC_1", 1, "F_MainSafety", "F_LAD"),
        };

        Assert.Throws<SafetyContentRefusedException>(() => CaptureConsole(() =>
            Program.RunDownloadPlan(gateway, PlanOptions(), timeoutOpenSeconds: 1)));

        Assert.Equal(0, gateway.PerformDownloadCalls);
        Assert.Equal(0, gateway.SaveCalls);
    }

    // ---- helpers -----------------------------------------------------------------------------

    private static DownloadPlanCommandOptions PlanOptions(DownloadOptionKind options = DownloadOptionKind.Software) => new(
        ProjectIdentifier: "C:\\proj\\My.ap20",
        Device: null,
        Options: options,
        Json: false,
        TiaInstallOverride: null,
        TimeoutConnectSeconds: ArgumentParser.DefaultTimeoutConnectSeconds,
        TimeoutOpenSeconds: ArgumentParser.DefaultTimeoutOpenSeconds);

    private static DownloadProviderSource NoProvider() => new(
        Found: false,
        SourcePath: null,
        SourceClrType: null,
        ProviderParentClrType: null,
        Attempts: new[]
        {
            new DownloadProviderAttempt("station/PLC_1", "DeviceItem", "no-provider", "(advertises no services)"),
        });

    private static DownloadPlanResult SamplePlan(DownloadOptionKind options = DownloadOptionKind.Software) => new(
        DevicePath: "S7-1200 G2 station_1/PLC_1",
        DeviceName: "PLC_1",
        Options: options,
        Provider: new DownloadProviderSource(
            Found: true,
            SourcePath: "S7-1200 G2 station_1/PLC_1",
            SourceClrType: "DeviceItem",
            ProviderParentClrType: "DeviceItem",
            Attempts: new[]
            {
                new DownloadProviderAttempt("S7-1200 G2 station_1/PLC_1", "DeviceItem", "provider", "DownloadProvider, OnlineProvider, SoftwareContainer"),
                new DownloadProviderAttempt("S7-1200 G2 station_1 <parent^1>", "Device", "no-provider", "(advertises no services)"),
            }),
        Connection: new DownloadConnectionPlan(
            IsConfigured: true,
            EnableLegacyCommunication: false,
            Modes: new[]
            {
                new DownloadConnectionMode("PN/IE", new[]
                {
                    new DownloadPcInterface(
                        "Example NIC",
                        1,
                        new[] { new DownloadConnectionAddress("PC", "192.0.2.1") },
                        new[] { "PN/IE_1" },
                        new[]
                        {
                            new DownloadTargetInterface(
                                "PROFINET interface_1",
                                new[] { new DownloadConnectionAddress("X1", "192.0.2.10") }),
                        }),
                }),
            }),
        BlockCount: 34,
        TypeCount: 5,
        InconsistentBlocks: Array.Empty<string>());

    private static byte[]? SafeGetBody(MethodInfo method)
    {
        try
        {
            return method.GetMethodBody()?.GetILAsByteArray();
        }
        catch (Exception)
        {
            // Abstract, extern and generated members have no readable body. Nothing to inspect is
            // not the same as nothing found, but it is also not an offender.
            return null;
        }
    }

    /// <summary>
    /// Scans a method's IL for a <c>call</c>/<c>callvirt</c> to a member named <c>Download</c> on a
    /// type in <c>Siemens.Engineering.Download</c>.
    ///
    /// Deliberately coarse. A precise IL walk would be longer and would have to be right about
    /// operand widths to be trusted; this reads every 4-byte window as a metadata token and asks the
    /// resolver what it is, which over-reports candidate tokens and under-reports nothing. Since the
    /// assertion is that the count is ZERO, over-reporting is the safe direction: a false positive
    /// fails a test and gets read, a false negative would let the one thing this guards against
    /// through unnoticed.
    /// </summary>
    private static bool ReferencesSiemensDownloadCall(Assembly assembly, byte[] il, MethodInfo owner, out string detail)
    {
        detail = string.Empty;
        var module = owner.Module;
        var genericTypeArgs = SafeGenericArguments(owner.DeclaringType);
        var genericMethodArgs = owner.IsGenericMethodDefinition ? owner.GetGenericArguments() : Type.EmptyTypes;

        for (var i = 0; i + 4 <= il.Length; i++)
        {
            var token = BitConverter.ToInt32(il, i);

            MethodBase? resolved;
            try
            {
                resolved = module.ResolveMethod(token, genericTypeArgs, genericMethodArgs);
            }
            catch (Exception)
            {
                continue;
            }

            if (resolved is null)
            {
                continue;
            }

            var declaringNamespace = resolved.DeclaringType?.Namespace ?? string.Empty;
            if (resolved.Name == "Download" &&
                declaringNamespace.StartsWith("Siemens.Engineering.Download", StringComparison.Ordinal))
            {
                detail = $"calls {resolved.DeclaringType?.FullName}.{resolved.Name}";
                return true;
            }
        }

        return false;
    }

    private static Type[] SafeGenericArguments(Type? type)
    {
        try
        {
            return type is { IsGenericTypeDefinition: true } ? type.GetGenericArguments() : Type.EmptyTypes;
        }
        catch (Exception)
        {
            return Type.EmptyTypes;
        }
    }

    private static (int ExitCode, string StdOut, string StdErr) CaptureConsole(Func<int> action)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            var exitCode = action();
            return (exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}
