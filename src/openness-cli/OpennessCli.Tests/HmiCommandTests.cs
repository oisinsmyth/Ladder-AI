using System;
using System.Collections.Generic;
using OpennessCli.Cli;
using OpennessCli.Model;
using Xunit;

namespace OpennessCli.Tests;

/// <summary>
/// Parser + formatter only. The gateway walk itself needs a live Portal session with an HMI device
/// and is not unit-testable here — same position every other Openness call in this project is in.
/// </summary>
public class HmiCommandTests
{
    private static HmiScreenItemInfo Item(
        string name,
        string type,
        params HmiDynamizationInfo[] dynamizations) =>
        new(name, type, 10, 20, 100, 40, dynamizations, Array.Empty<HmiEventInfo>());

    private static HmiScreenItemInfo ItemWithEvents(
        string name,
        string type,
        params HmiEventInfo[] events) =>
        new(name, type, 10, 20, 100, 40, Array.Empty<HmiDynamizationInfo>(), events);

    // Screen-level events are exercised separately; most tests care about items, so this keeps the
    // call sites readable rather than trailing an empty array through every one of them.
    private static HmiScreenInfo ScreenInfo(
        string name,
        int? number,
        long? width,
        long? height,
        int itemCount,
        IReadOnlyList<HmiScreenItemInfo> items) =>
        new(name, number, width, height, itemCount, items, Array.Empty<HmiEventInfo>());

    private static HmiDeviceInfo UnifiedDevice(params HmiScreenInfo[] screens) =>
        new("station/Panel_1", "PanelRuntime", HmiFamily.Unified, screens.Length, 2, 300, 40, 5, 6, 3, screens);

    [Fact]
    public void Parse_HmiWithoutProjectIdentifier_Fails()
    {
        var result = ArgumentParser.Parse(new[] { "hmi", "--json" });
        Assert.IsType<ParseResult.Failure>(result);
    }

    [Fact]
    public void Parse_HmiWithProjectIdentifier_UsesDefaultsAndSummaryMode()
    {
        var result = ArgumentParser.Parse(new[] { "hmi", "MyProject" });
        var success = Assert.IsType<ParseResult.HmiSuccess>(result);
        Assert.Equal("MyProject", success.Options.ProjectIdentifier);
        // Summary is the default: reading every item on every screen is the expensive path and must
        // be opt-in.
        Assert.Null(success.Options.Screen);
        Assert.Equal(ArgumentParser.DefaultHmiMaxItems, success.Options.MaxItems);
        Assert.False(success.Options.Json);
    }

    [Fact]
    public void Parse_HmiWithScreenAndMaxItems_RoundTripsBoth()
    {
        var result = ArgumentParser.Parse(new[] { "hmi", "MyProject", "--screen", "Overview", "--max-items", "7", "--json" });
        var success = Assert.IsType<ParseResult.HmiSuccess>(result);
        Assert.Equal("Overview", success.Options.Screen);
        Assert.Equal(7, success.Options.MaxItems);
        Assert.True(success.Options.Json);
    }

    [Fact]
    public void Parse_HmiScreenFlagWithoutValue_Fails()
    {
        var result = ArgumentParser.Parse(new[] { "hmi", "MyProject", "--screen" });
        Assert.IsType<ParseResult.Failure>(result);
    }

    [Fact]
    public void Parse_HmiMaxItemsNonPositive_Fails()
    {
        var result = ArgumentParser.Parse(new[] { "hmi", "MyProject", "--max-items", "0" });
        Assert.IsType<ParseResult.Failure>(result);
    }

    /// <summary>
    /// The regression guard. `hmi` was added with a dispatch case in Program's own switch, built
    /// with zero warnings and passed the whole suite — then threw "Unhandled parse result:
    /// HmiSuccess" on first run, because a SECOND switch over the same type had not been updated.
    /// Enumerating the variants by reflection means the next subcommand cannot repeat it: this fails
    /// the moment a *Success variant exists that CommonOptions does not handle.
    /// </summary>
    [Fact]
    public void CommonOptions_HandlesEverySuccessParseResultVariant()
    {
        var successTypes = new List<Type>();
        foreach (var nested in typeof(ParseResult).GetNestedTypes())
        {
            if (nested.Name.EndsWith("Success", StringComparison.Ordinal))
            {
                successTypes.Add(nested);
            }
        }

        Assert.NotEmpty(successTypes);

        foreach (var successType in successTypes)
        {
            var optionsType = successType.GetConstructors()[0].GetParameters()[0].ParameterType;
            // Uninitialized is fine: CommonOptions type-tests the variant and reads properties whose
            // default values it never interprets. Constructing real options for nine records would
            // couple this guard to each one's shape, which is the coupling it exists to avoid.
            var options = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(optionsType);
            var result = (ParseResult)Activator.CreateInstance(successType, options)!;

            var exception = Record.Exception(() => ArgumentParser.CommonOptions(result));

            Assert.True(
                exception is null,
                $"ArgumentParser.CommonOptions does not handle {successType.Name} — add a case for it.");
        }
    }

    [Fact]
    public void Parse_HmiSchemaWithoutScreen_ImpliesAllScreens()
    {
        // --schema derives from the screens walked, so alone it must mean all of them; requiring
        // "--screen *" alongside would be a trap with no upside.
        var result = ArgumentParser.Parse(new[] { "hmi", "MyProject", "--schema" });
        var success = Assert.IsType<ParseResult.HmiSuccess>(result);
        Assert.True(success.Options.Schema);
        Assert.Equal("*", success.Options.Screen);
    }

    [Fact]
    public void Parse_HmiSchemaWithExplicitScreen_KeepsThatScreen()
    {
        var result = ArgumentParser.Parse(new[] { "hmi", "MyProject", "--schema", "--screen", "Overview" });
        var success = Assert.IsType<ParseResult.HmiSuccess>(result);
        Assert.True(success.Options.Schema);
        Assert.Equal("Overview", success.Options.Screen);
    }

    [Fact]
    public void ProjectIdentifier_IsReadableForEverySuccessVariantThatHasOne()
    {
        // Feeds Connect's process-preference. portal-status legitimately has no project.
        Assert.Equal("MyProject", ArgumentParser.ProjectIdentifier(ArgumentParser.Parse(new[] { "hmi", "MyProject" })));
        Assert.Equal("MyProject", ArgumentParser.ProjectIdentifier(ArgumentParser.Parse(new[] { "list", "MyProject" })));
        Assert.Null(ArgumentParser.ProjectIdentifier(ArgumentParser.Parse(new[] { "portal-status" })));
    }

    [Fact]
    public void FormatHmiSchemaReport_NoDevices_SaysSchemaIsUnifiedOnly()
    {
        var text = OutputFormatter.FormatHmiSchemaReport(Array.Empty<HmiSchemaReport>());
        Assert.Contains("Unified-only", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatHmiSchemaReport_OrdersMandatoryAttributesFirst()
    {
        // The authoring order: what you must set, then what is worth setting, then the rest.
        var report = new HmiSchemaReport(
            "station/Panel_1",
            new[] { "HmiButton", "HmiIOField" },
            new[]
            {
                new HmiTypeSchema(
                    "HmiIOField",
                    new[] { "Dynamizations" },
                    new[]
                    {
                        new HmiAttributeSchema("Zzz", "ReadWrite", "None", "Boolean", "False"),
                        new HmiAttributeSchema("Aaa", "ReadWrite", "Relevant", "String", "x"),
                        new HmiAttributeSchema("Mmm", "ReadWrite", "Mandatory", "String", "y"),
                    }),
            });

        var text = OutputFormatter.FormatHmiSchemaReport(new[] { report });

        var mandatory = text.IndexOf("Mmm", StringComparison.Ordinal);
        var relevant = text.IndexOf("Aaa", StringComparison.Ordinal);
        var none = text.IndexOf("Zzz", StringComparison.Ordinal);
        Assert.True(mandatory < relevant && relevant < none, "attributes must sort Mandatory -> Relevant -> None");
        Assert.Contains("CREATABLE SCREEN-ITEM TYPES (2)", text, StringComparison.Ordinal);
        Assert.Contains("[ReadWrite/Mandatory]", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_HmiCreateScreen_RequiresName()
    {
        var result = ArgumentParser.Parse(new[] { "hmi-create-screen", "MyProject", "--yes" });
        Assert.IsType<ParseResult.Failure>(result);
    }

    [Fact]
    public void Parse_HmiCreateScreen_DefaultsToUnconfirmed()
    {
        // The gate must default closed: the one HMI command that writes should never write because
        // a flag was forgotten.
        var result = ArgumentParser.Parse(new[] { "hmi-create-screen", "MyProject", "--name", "TestScreen" });
        var success = Assert.IsType<ParseResult.HmiCreateScreenSuccess>(result);
        Assert.False(success.Options.Confirm);
        Assert.Equal("TestScreen", success.Options.ScreenName);
        Assert.Equal(ArgumentParser.DefaultHmiScreenWidth, success.Options.Width);
        Assert.Equal(ArgumentParser.DefaultHmiScreenHeight, success.Options.Height);
        Assert.Empty(success.Options.ItemTypes);
    }

    [Fact]
    public void Parse_HmiCreateScreen_CollectsRepeatedItemFlags()
    {
        var result = ArgumentParser.Parse(new[]
        {
            "hmi-create-screen", "MyProject", "--name", "TestScreen",
            "--item", "HmiRectangle", "--item", "HmiText", "--width", "800", "--height", "480", "--yes",
        });

        var success = Assert.IsType<ParseResult.HmiCreateScreenSuccess>(result);
        Assert.True(success.Options.Confirm);
        Assert.Equal(new[] { "HmiRectangle", "HmiText" }, success.Options.ItemTypes);
        Assert.Equal(800, success.Options.Width);
        Assert.Equal(480, success.Options.Height);
    }

    [Fact]
    public void FormatHmiCreateScreenResult_CleanValidate_SaysItRanRatherThanStayingSilent()
    {
        // "no messages" and "never ran" must not look the same — this is the first time the project
        // has ever invoked Validate().
        var result = new HmiCreateScreenResult(
            "station/Panel_1", "TestScreen", 1280, 615,
            new[] { "HmiRectangle HmiRectangle_1" },
            Array.Empty<HmiValidationMessage>(),
            true);

        var text = OutputFormatter.FormatHmiCreateScreenResult(result);

        Assert.Contains("ran, returned no errors and no warnings", text, StringComparison.Ordinal);
        Assert.Contains("project saved: yes", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatHmiCreateScreenResult_ValidateThrew_IsNotPresentedAsAPass()
    {
        var result = new HmiCreateScreenResult(
            "station/Panel_1", "TestScreen", 1280, 615,
            Array.Empty<string>(),
            new[] { new HmiValidationMessage(string.Empty, "ValidateThrew", "NotSupportedException: nope") },
            true);

        var text = OutputFormatter.FormatHmiCreateScreenResult(result);

        Assert.Contains("THREW", text, StringComparison.Ordinal);
        Assert.Contains("not a pass", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatHmiCreateScreenResult_UnsavedIsLoud()
    {
        var result = new HmiCreateScreenResult(
            "station/Panel_1", "TestScreen", 1280, 615,
            Array.Empty<string>(), Array.Empty<HmiValidationMessage>(), false);

        Assert.Contains("project saved: NO", OutputFormatter.FormatHmiCreateScreenResult(result), StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_HmiEditScreen_RequiresAtLeastOneChange()
    {
        // An edit with no edits would open the project, change nothing, save, and report success.
        var result = ArgumentParser.Parse(new[] { "hmi-edit-screen", "P", "--name", "S", "--yes" });
        var failure = Assert.IsType<ParseResult.Failure>(result);
        Assert.Contains("Nothing to do", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_HmiEditScreen_ParsesSetsAndEvents()
    {
        var result = ArgumentParser.Parse(new[]
        {
            "hmi-edit-screen", "P", "--name", "S",
            "--set", "Screen.Width=800",
            "--set", "Button_1.Left=42",
            "--event", "Button_1:Tapped=HMIRuntime.Trace('hi');",
            "--event", "Screen:Loaded",
            "--yes",
        });

        var success = Assert.IsType<ParseResult.HmiEditScreenSuccess>(result);
        Assert.Equal(("Screen", "Width", "800"), success.Options.Sets[0]);
        Assert.Equal(("Button_1", "Left", "42"), success.Options.Sets[1]);
        Assert.Equal(("Button_1", "Tapped", "HMIRuntime.Trace('hi');"), success.Options.Events[0]);
        // An event handler with no script is legitimate, and must stay distinguishable from "".
        Assert.Equal(("Screen", "Loaded", (string?)null), success.Options.Events[1]);
    }

    [Fact]
    public void Parse_HmiEditScreen_SetValueMayContainEquals()
    {
        var result = ArgumentParser.Parse(new[] { "hmi-edit-screen", "P", "--name", "S", "--set", "Screen.OutputFormat={D,@dd=MM}", "--yes" });
        var success = Assert.IsType<ParseResult.HmiEditScreenSuccess>(result);
        Assert.Equal("{D,@dd=MM}", success.Options.Sets[0].Value);
    }

    [Theory]
    [InlineData("NoDotOrEquals")]
    [InlineData("Screen.Width")]
    [InlineData("=42")]
    public void Parse_HmiEditScreen_MalformedSetIsRejected(string raw)
    {
        var result = ArgumentParser.Parse(new[] { "hmi-edit-screen", "P", "--name", "S", "--set", raw, "--yes" });
        Assert.IsType<ParseResult.Failure>(result);
    }

    [Fact]
    public void Parse_HmiEditScreen_DefaultsToUnconfirmed()
    {
        var result = ArgumentParser.Parse(new[] { "hmi-edit-screen", "P", "--name", "S", "--set", "Screen.Width=800" });
        Assert.False(Assert.IsType<ParseResult.HmiEditScreenSuccess>(result).Options.Confirm);
    }

    [Fact]
    public void FormatHmiReport_RendersEventsWithAndWithoutScripts()
    {
        // The gap this closes: a button with no dynamizations is not unbound, its behaviour is here.
        var device = UnifiedDevice(ScreenInfo(
            "Overview", 1, 1920, 1080, 1,
            new[]
            {
                ItemWithEvents(
                    "Start",
                    "HmiButton",
                    new HmiEventInfo("Tapped", true, "HMIRuntime.Trace('go');"),
                    new HmiEventInfo("Up", false, null)),
            }));

        var report = OutputFormatter.FormatHmiReport(new[] { device }, "Overview");

        Assert.Contains("on Tapped  script: HMIRuntime.Trace('go');", report, StringComparison.Ordinal);
        Assert.Contains("on Up  (no script)", report, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatHmiReport_RendersScreenLevelEvents()
    {
        // Screens carry their own handlers (Loaded/Unloaded), on a different composition from their
        // items'. Omitting them made a created screen-level event impossible to verify by read-back.
        var device = UnifiedDevice(new HmiScreenInfo(
            "Overview", 1, 1920, 1080, 0,
            Array.Empty<HmiScreenItemInfo>(),
            new[] { new HmiEventInfo("Loaded", false, null) }));

        var report = OutputFormatter.FormatHmiReport(new[] { device }, "Overview");

        Assert.Contains("on Loaded  (no script)", report, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatHmiEditScreenResult_ListsAppliedChangesAndValidation()
    {
        var result = new HmiEditScreenResult(
            "station/Panel_1",
            "TestScreen",
            new[] { "set Screen.Width = 800 (UInt32)", "event HmiButton.Tapped (script set)" },
            Array.Empty<HmiValidationMessage>(),
            true);

        var text = OutputFormatter.FormatHmiEditScreenResult(result);

        Assert.Contains("changes applied: 2", text, StringComparison.Ordinal);
        Assert.Contains("(UInt32)", text, StringComparison.Ordinal);
        Assert.Contains("ran, returned no errors and no warnings", text, StringComparison.Ordinal);
        Assert.Contains("project saved: yes", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatHmiReport_NoDevices_SaysSo()
    {
        Assert.Equal("(no HMI devices found)", OutputFormatter.FormatHmiReport(Array.Empty<HmiDeviceInfo>(), null));
    }

    [Fact]
    public void FormatHmiReport_ClassicDevice_StatesScreenContentsAreNotExposed()
    {
        var device = new HmiDeviceInfo(
            "station/Panel_1",
            "Panel_1",
            HmiFamily.Classic,
            1,
            0, 0, 0, 0, 0, 0,
            new[] { ScreenInfo("Overview", null, null, null, 0, Array.Empty<HmiScreenItemInfo>()) });

        var report = OutputFormatter.FormatHmiReport(new[] { device }, null);

        Assert.Contains("[Classic]", report, StringComparison.Ordinal);
        // The distinction that matters: unavailable, not merely unrequested.
        Assert.Contains("no screen contents", report, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatHmiReport_UnifiedSummary_InvitesTheDetailPass()
    {
        var device = UnifiedDevice(ScreenInfo("Overview", 1, 1920, 1080, 12, Array.Empty<HmiScreenItemInfo>()));

        var report = OutputFormatter.FormatHmiReport(new[] { device }, null);

        Assert.Contains("[Unified]", report, StringComparison.Ordinal);
        Assert.Contains("items=12", report, StringComparison.Ordinal);
        Assert.Contains("--screen", report, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatHmiReport_RendersItemsAndTagDynamizations()
    {
        var device = UnifiedDevice(ScreenInfo(
            "Overview",
            1,
            1920,
            1080,
            1,
            new[] { Item("Pump1Status", "HmiIOField", new HmiDynamizationInfo("ProcessValue", "Tag", "PumpSpeed", "DB_X.Speed")) }));

        var report = OutputFormatter.FormatHmiReport(new[] { device }, "Overview");

        Assert.Contains("HmiIOField", report, StringComparison.Ordinal);
        Assert.Contains("Pump1Status", report, StringComparison.Ordinal);
        Assert.Contains("ProcessValue <- Tag", report, StringComparison.Ordinal);
        Assert.Contains("tag=PumpSpeed", report, StringComparison.Ordinal);
        Assert.Contains("plcTag=DB_X.Speed", report, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatHmiReport_NonTagDynamization_ReportsKindWithoutInventingATag()
    {
        var device = UnifiedDevice(ScreenInfo(
            "Overview",
            1,
            1920,
            1080,
            1,
            new[] { Item("Lamp", "HmiRectangle", new HmiDynamizationInfo("BackColor", "Flashing", null, null)) }));

        var report = OutputFormatter.FormatHmiReport(new[] { device }, "Overview");

        Assert.Contains("BackColor <- Flashing", report, StringComparison.Ordinal);
        Assert.DoesNotContain("tag=", report, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatHmiReport_TruncatedItemList_NeverReadsAsComplete()
    {
        // ItemCount is the truth; Items is what was read. A cap must never present as a full listing.
        var device = UnifiedDevice(ScreenInfo(
            "Overview",
            1,
            1920,
            1080,
            50,
            new[] { Item("Only", "HmiButton") }));

        var report = OutputFormatter.FormatHmiReport(new[] { device }, "Overview");

        Assert.Contains("showing 1 of 50", report, StringComparison.Ordinal);
        Assert.Contains("--max-items", report, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatHmiJson_EmitsFamilyAndDynamizations()
    {
        var device = UnifiedDevice(ScreenInfo(
            "Overview",
            1,
            1920,
            1080,
            1,
            new[] { Item("Pump1Status", "HmiIOField", new HmiDynamizationInfo("ProcessValue", "Tag", "PumpSpeed", "DB_X.Speed")) }));

        var json = OutputFormatter.FormatHmiJson(new[] { device });

        Assert.Contains("\"family\": \"Unified\"", json, StringComparison.Ordinal);
        Assert.Contains("\"itemType\": \"HmiIOField\"", json, StringComparison.Ordinal);
        Assert.Contains("\"plcTag\": \"DB_X.Speed\"", json, StringComparison.Ordinal);
    }
}
