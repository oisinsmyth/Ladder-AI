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
        foreach (var successType in SuccessVariants())
        {
            var result = UninitialisedVariant(successType);

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

    /// <summary>
    /// The companion to <see cref="CommonOptions_HandlesEverySuccessParseResultVariant"/>, added
    /// 2026-08-08. `ProjectIdentifier` has the SAME every-variant `throw` default as `CommonOptions`
    /// but was only spot-checked by three hardcoded asserts above — so a new subcommand that forgot
    /// it would build clean, pass the whole suite, and die at runtime exactly the way `hmi` did.
    /// One guard covering one of two identical switches is a guard that will be bypassed.
    /// </summary>
    [Fact]
    public void ProjectIdentifier_HandlesEverySuccessParseResultVariant()
    {
        foreach (var successType in SuccessVariants())
        {
            var result = UninitialisedVariant(successType);

            var exception = Record.Exception(() => ArgumentParser.ProjectIdentifier(result));

            Assert.True(
                exception is null,
                $"ArgumentParser.ProjectIdentifier does not handle {successType.Name} — add a case for it.");
        }
    }

    /// <summary>
    /// Every `Hmi*Exception` must classify to something other than `UnexpectedError`. Added
    /// 2026-08-08 after P1 measured two of them exiting 5: the whole family had been mapped to
    /// `CommandError` that morning, then six NEW exceptions were added the same afternoon without
    /// being mapped — recreating the exact defect within hours. The existing reflection guard covers
    /// `ParseResult` variants, not exception types, so nothing caught it.
    ///
    /// These are all user-fixable naming or usage mistakes whose messages already say how to fix
    /// them; reporting one as an internal fault with a full inner-exception dump is wrong, and the
    /// only durable defence is a guard that enumerates the family rather than a list someone must
    /// remember to extend.
    /// </summary>
    [Fact]
    public void HmiExceptionsAreClassified_NotLeftAsInternalFaults()
    {
        var hmiExceptions = typeof(OpennessCli.Openness.IOpennessGateway).Assembly.GetTypes()
            .Where(t => typeof(Exception).IsAssignableFrom(t) && t.IsPublic && !t.IsAbstract)
            .Where(t => t.Name.StartsWith("Hmi", StringComparison.Ordinal)
                || t.Name.StartsWith("NoUnifiedHmi", StringComparison.Ordinal)
                || t.Name.StartsWith("AmbiguousHmi", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(hmiExceptions);

        foreach (var type in hmiExceptions)
        {
            // Uninitialized: ExitCodes.ForException type-tests only, and constructing each would
            // couple this guard to every constructor's shape.
            var instance = (Exception)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);

            Assert.True(
                ExitCodes.ForException(instance) != ExitCodes.UnexpectedError,
                $"{type.Name} falls through to UnexpectedError(5). It is a user-fixable error and belongs in ExitCodes.ForException — add a case for it.");
        }
    }

    private static List<Type> SuccessVariants()
    {
        var found = new List<Type>();
        foreach (var nested in typeof(ParseResult).GetNestedTypes())
        {
            if (nested.Name.EndsWith("Success", StringComparison.Ordinal))
            {
                found.Add(nested);
            }
        }

        Assert.NotEmpty(found);
        return found;
    }

    // Uninitialized on purpose: both switches type-test the variant and read properties whose
    // default values they never interpret. Constructing real options for every record would couple
    // these guards to each record's shape, which is the coupling they exist to avoid.
    private static ParseResult UninitialisedVariant(Type successType)
    {
        var optionsType = successType.GetConstructors()[0].GetParameters()[0].ParameterType;
        var options = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(optionsType);
        return (ParseResult)Activator.CreateInstance(successType, options)!;
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
    public void Parse_HmiEditScreen_EventScriptFromFile()
    {
        // A real handler body is multi-line JavaScript; passing that as one shell argument is
        // miserable, so '@' loads it from a file.
        var path = System.IO.Path.GetTempFileName();
        try
        {
            System.IO.File.WriteAllText(path, "let a = 1;\nHMIRuntime.Trace(a);\n");
            var result = ArgumentParser.Parse(new[] { "hmi-edit-screen", "P", "--name", "S", "--event", $"Button_1:Tapped@{path}", "--yes" });
            var success = Assert.IsType<ParseResult.HmiEditScreenSuccess>(result);
            Assert.Equal("Button_1", success.Options.Events[0].Target);
            Assert.Equal("Tapped", success.Options.Events[0].EventType);
            Assert.Contains("HMIRuntime.Trace(a);", success.Options.Events[0].Script!, StringComparison.Ordinal);
        }
        finally
        {
            System.IO.File.Delete(path);
        }
    }

    [Fact]
    public void Parse_HmiEditScreen_MissingScriptFileIsRejected()
    {
        // Silently creating an empty handler because the path was wrong would be the worst outcome.
        var result = ArgumentParser.Parse(new[] { "hmi-edit-screen", "P", "--name", "S", "--event", @"Button_1:Tapped@C:\no\such\file.js", "--yes" });
        var failure = Assert.IsType<ParseResult.Failure>(result);
        Assert.Contains("script file not found", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_HmiEditScreen_InlineScriptMayContainAtSign()
    {
        // '=' comes first, so the '@' is part of the script, not a file marker.
        var result = ArgumentParser.Parse(new[] { "hmi-edit-screen", "P", "--name", "S", "--event", "B:Tapped=t('a@b');", "--yes" });
        var success = Assert.IsType<ParseResult.HmiEditScreenSuccess>(result);
        Assert.Equal("t('a@b');", success.Options.Events[0].Script);
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
    public void Parse_HmiEditScreen_NestedSetTargetKeepsTheWholePathAsTheTarget()
    {
        // The §4m tooling gap: a dynamization's own attributes were unreachable because the target
        // was resolved as an item NAME. The split is unchanged — last '.' before the first '=' — so
        // everything before the attribute must survive as one target path for the gateway to walk.
        var result = ArgumentParser.Parse(new[]
        {
            "hmi-edit-screen", "P", "--name", "S",
            "--set", "Rect_1.BackColor.FlashingRate=Fast",
            "--set", "Rect_1.BackColor.ValueConverter.MappingTable.Entries[0].Flashing=True",
            "--yes",
        });

        var success = Assert.IsType<ParseResult.HmiEditScreenSuccess>(result);
        Assert.Equal(("Rect_1.BackColor", "FlashingRate", "Fast"), success.Options.Sets[0]);
        Assert.Equal(
            ("Rect_1.BackColor.ValueConverter.MappingTable.Entries[0]", "Flashing", "True"),
            success.Options.Sets[1]);
    }

    [Fact]
    public void Parse_HmiEditScreen_MapEntrySpecSurvivesItsOwnEqualsSigns()
    {
        // An entry spec is "<EntryType>;<Attr>=<Value>;..." — it is full of '=' signs, and only the
        // FIRST one separates the dynamization path from the spec.
        var result = ArgumentParser.Parse(new[]
        {
            "hmi-edit-screen", "P", "--name", "S",
            "--map", "Rect_1.BackColor=Range;From=int:1;To=int:5;Value=color:#FF0000;Flashing=True",
            "--yes",
        });

        var success = Assert.IsType<ParseResult.HmiEditScreenSuccess>(result);
        var (target, property, entrySpec) = success.Options.Maps[0];
        Assert.Equal("Rect_1", target);
        Assert.Equal("BackColor", property);
        Assert.Equal("Range;From=int:1;To=int:5;Value=color:#FF0000;Flashing=True", entrySpec);
    }

    [Fact]
    public void Parse_HmiEditScreen_MapClearTakesTargetAndProperty()
    {
        var result = ArgumentParser.Parse(new[]
        {
            "hmi-edit-screen", "P", "--name", "S", "--map-clear", "Rect_1.BackColor", "--yes",
        });

        var success = Assert.IsType<ParseResult.HmiEditScreenSuccess>(result);
        Assert.Equal(("Rect_1", "BackColor"), success.Options.MapClears[0]);
    }

    [Fact]
    public void Parse_HmiEditScreen_MalformedMapClearIsRejected()
    {
        Assert.IsType<ParseResult.Failure>(
            ArgumentParser.Parse(new[] { "hmi-edit-screen", "P", "--name", "S", "--map-clear", "NoDot", "--yes" }));
    }

    [Fact]
    public void Parse_HmiEditScreen_MapAloneCountsAsSomethingToDo()
    {
        // The "nothing to do" guard is a list that has to be extended with every new change kind;
        // forgetting one turns a real command into a usage error.
        var result = ArgumentParser.Parse(new[]
        {
            "hmi-edit-screen", "P", "--name", "S", "--map", "Rect_1.BackColor=Simple", "--yes",
        });

        Assert.IsType<ParseResult.HmiEditScreenSuccess>(result);
    }

    [Fact]
    public void FormatHmiReport_RendersAMappingTableOnItsOwnLine()
    {
        // Read-back from a fresh process is the ONLY evidence a mapping table was written: Unified
        // has no screen export, so if the walker cannot show it, nothing can.
        var device = UnifiedDevice(ScreenInfo(
            "Overview", 1, 1920, 1080, 1,
            new[]
            {
                Item(
                    "Lamp",
                    "HmiRectangle",
                    new HmiDynamizationInfo(
                        "BackColor",
                        "Tag",
                        "ZZ_AI_Tag",
                        null,
                        "ConditionType=Range entries=1 { MappingTableEntryRange Flashing=True<Boolean> }")),
            }));

        var report = OutputFormatter.FormatHmiReport(new[] { device }, "Overview");

        Assert.Contains("mapping: ConditionType=Range entries=1", report, StringComparison.Ordinal);
        Assert.Contains("Flashing=True<Boolean>", report, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatHmiReport_PlainBindingGrowsNoMappingLine()
    {
        var device = UnifiedDevice(ScreenInfo(
            "Overview", 1, 1920, 1080, 1,
            new[] { Item("Lamp", "HmiRectangle", new HmiDynamizationInfo("Visible", "Tag", "ZZ_AI_Tag", null)) }));

        Assert.DoesNotContain("mapping:", OutputFormatter.FormatHmiReport(new[] { device }, "Overview"), StringComparison.Ordinal);
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
    public void FormatCompileTable_CountsFromMessagesAndFlagsCompilerDisagreement()
    {
        // Measured against a real HMI device: the compiler reported WARNINGS=0 alongside 156 warning
        // messages, and ERRORS=1 alongside 6 error messages. Its aggregates are wrong in both
        // directions, so they must not be the numbers shown or gated on.
        var result = new CompileResult(
            Model.CompileState.Error,
            ErrorCount: 1,
            WarningCount: 0,
            Messages: new[]
            {
                new CompileMessage(Model.CompileState.Error, "SyntaxError: bad", "Screen/Button"),
                new CompileMessage(Model.CompileState.Error, "another", "Screen"),
                new CompileMessage(Model.CompileState.Warning, "no release button", "Screen/Rect"),
            });

        var text = OutputFormatter.FormatCompileTable(result);

        // Counts come from the messages, not the compiler's aggregates.
        Assert.Contains("ERRORS: 2  WARNINGS: 1", text, StringComparison.Ordinal);
        // And the disagreement is surfaced rather than silently papered over.
        Assert.Contains("are not reliable", text, StringComparison.Ordinal);
        Assert.Contains("ErrorCount=1", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatCompileTable_AgreeingCountsProduceNoNote()
    {
        var result = new CompileResult(
            Model.CompileState.Success,
            ErrorCount: 0,
            WarningCount: 1,
            Messages: new[] { new CompileMessage(Model.CompileState.Warning, "w", "p") });

        Assert.DoesNotContain("are not reliable", OutputFormatter.FormatCompileTable(result), StringComparison.Ordinal);
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
    public void Parse_HmiNew_RequiresKindAndName()
    {
        Assert.IsType<ParseResult.Failure>(ArgumentParser.Parse(new[] { "hmi-new", "P", "--name", "ZZ_AI_X", "--yes" }));
        Assert.IsType<ParseResult.Failure>(ArgumentParser.Parse(new[] { "hmi-new", "P", "--kind", "Screens", "--yes" }));
    }

    [Fact]
    public void Parse_HmiNew_CarriesKindNameAndParent()
    {
        var result = ArgumentParser.Parse(new[] { "hmi-new", "P", "--kind", "Tags", "--name", "ZZ_AI_T", "--in", "ZZ_AI_Table", "--yes" });
        var success = Assert.IsType<ParseResult.HmiNewSuccess>(result);
        Assert.Equal("Tags", success.Options.Kind);
        Assert.Equal("ZZ_AI_T", success.Options.Name);
        Assert.Equal("ZZ_AI_Table", success.Options.Parent);
        Assert.True(success.Options.Confirm);
    }

    [Fact]
    public void Parse_HmiNewAndDelete_DefaultToUnconfirmed()
    {
        Assert.False(Assert.IsType<ParseResult.HmiNewSuccess>(
            ArgumentParser.Parse(new[] { "hmi-new", "P", "--kind", "Screens", "--name", "ZZ_AI_S" })).Options.Confirm);
        Assert.False(Assert.IsType<ParseResult.HmiDeleteSuccess>(
            ArgumentParser.Parse(new[] { "hmi-delete", "P", "--kind", "Screens", "--name", "ZZ_AI_S" })).Options.Confirm);
    }

    [Fact]
    public void Parse_HmiDelete_AllowAnyNameDefaultsOff()
    {
        // The prefix guard is the one place the data boundary is enforced in code rather than
        // procedurally, because deletion runs unattended. It must default to protecting.
        var guarded = Assert.IsType<ParseResult.HmiDeleteSuccess>(
            ArgumentParser.Parse(new[] { "hmi-delete", "P", "--kind", "Tags", "--name", "ZZ_AI_T", "--yes" }));
        Assert.False(guarded.Options.AllowAnyName);

        var overridden = Assert.IsType<ParseResult.HmiDeleteSuccess>(
            ArgumentParser.Parse(new[] { "hmi-delete", "P", "--kind", "Tags", "--name", "Real", "--yes", "--allow-any-name" }));
        Assert.True(overridden.Options.AllowAnyName);
    }

    [Fact]
    public void Parse_HmiInventory_NeedsNeitherKindNorConfirmation()
    {
        // Read-only: requiring --yes on a census would be noise, and requiring --kind would stop it
        // being usable as the end-of-programme "is anything of mine left?" check.
        var result = ArgumentParser.Parse(new[] { "hmi-inventory", "P" });
        var success = Assert.IsType<ParseResult.HmiInventorySuccess>(result);
        Assert.True(success.Options.Confirm);
        Assert.Equal(string.Empty, success.Options.Kind);
    }

    [Fact]
    public void FormatHmiInventory_CallsOutSurvivingProbeArtifacts()
    {
        var objects = new[]
        {
            new HmiObjectInfo("Screens", "RealScreen", "HmiScreen"),
            new HmiObjectInfo("Screens", "ZZ_AI_TestScreen", "HmiScreen"),
            new HmiObjectInfo("Tags", "ZZ_AI_TestTag", "HmiTag"),
        };

        var text = OutputFormatter.FormatHmiInventoryTable(objects);

        Assert.Contains("PROBE ARTIFACTS (ZZ_AI_*): 2", text, StringComparison.Ordinal);
        Assert.Contains("must be zero at end of programme", text, StringComparison.Ordinal);
        Assert.Contains("Screens / ZZ_AI_TestScreen", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatHmiInventory_CleanRunReportsZeroArtifacts()
    {
        var text = OutputFormatter.FormatHmiInventoryTable(new[] { new HmiObjectInfo("Screens", "RealScreen", "HmiScreen") });
        Assert.Contains("PROBE ARTIFACTS (ZZ_AI_*): 0", text, StringComparison.Ordinal);
        Assert.DoesNotContain("must be zero", text, StringComparison.Ordinal);
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

    // Guards the 2026-08-09 fix: refusals share the applied list with successes, so both the headline
    // count and the exit code have to subtract them. The live probes reported "changes applied: 5"
    // for two successes and three refusals, and exited 0 — a caller reading either signal would have
    // recorded work that never happened.
    [Fact]
    public void DescribeAppliedCount_ExcludesRefusalsAndSaysHowMany()
    {
        var applied = new[]
        {
            "dynamization ScriptDynamization created on HmiIOField.ProcessValue",
            "dynamization ExpressionDynamization created on HmiText.Visible",
            "dynamization FlashingDynamization on HmiRectangle_1.Visible -> REFUSED (…)",
            "dynamization ResourceListDynamization on HmiButton_4.Visible -> REFUSED (…)",
            "dynamization TagParameterDynamization on HmiCircle_5.Visible -> REFUSED (…)",
        };

        Assert.Equal("2 (3 REFUSED)", OutputFormatter.DescribeAppliedCount(applied));
        Assert.Equal(3, OutputFormatter.CountRefusals(applied));
    }

    [Fact]
    public void DescribeAppliedCount_IsABareNumberWhenNothingWasRefused()
    {
        var applied = new[] { "set AlarmClass", "set Priority" };

        Assert.Equal("2", OutputFormatter.DescribeAppliedCount(applied));
        Assert.Equal(0, OutputFormatter.CountRefusals(applied));
    }
}
