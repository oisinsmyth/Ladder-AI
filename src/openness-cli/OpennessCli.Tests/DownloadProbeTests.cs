using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DownloadProbe;
using Siemens.Engineering;
using Xunit;

// This file's namespace is OpennessCli.Tests, so a bare `Program` binds to OpennessCli's own entry
// point, not the probe's. Aliased rather than fully qualified everywhere so it stays obvious which
// binary is under test.
using ProbeProgram = DownloadProbe.Program;

namespace OpennessCli.Tests;

/// <summary>
/// `download-probe` - the one binary in this repository that calls
/// <c>DownloadProvider.Download</c>, built to answer whether <c>DataBlockReinitialization</c> is
/// raised when a download restructures a standard-access DB.
///
/// EVERY ASSURANCE THIS TOOL HAS IS HERE. It cannot be exercised end to end: running it is a real
/// device write, authorised separately, and "just checking it attaches" is already a Portal session
/// against a live project. So the properties that make it safe to hand over are asserted here or
/// they are not asserted at all:
///
///   1. The scratch-path guard refuses BEFORE Portal is contacted - proved by handing Run a session
///      delegate that throws if it is ever called, and by asserting no log file was even created.
///   2. The selection policy never chooses a denied selection. Checked against the ACTUAL selection
///      enums in the installed V20 assembly, not against a list someone typed here.
///   3. A configuration the tool does not recognise is logged in full rather than skipped.
///   4. An unhandled configuration surfaces as the abort exit code, distinct from success and from
///      an ordinary failure.
/// </summary>
public class DownloadProbeTests
{
    // Invented paths only. The scratch project the rig uses is a copy of a live engineering job, and
    // its name may not enter this repository (CLAUDE.md, "Live runs": use anything, commit nothing).
    //
    // These two are ARGUMENT-PARSER fixtures and nothing more. The fence itself is tested in
    // DownloadProbeAllowlistTests, against real files under a temporary allowlist, because since
    // 2026-08-13 it compares RESOLVED PATHS against an allowlist rather than matching a file name —
    // so a string that never exists on disk can no longer be a fixture for it.
    private const string ScratchProject = @"C:\work\Widget Line scratch.ap20";
    private const string RealProject = @"C:\work\Widget Line.ap20";

    [Fact]
    public void Guard_RefusalCodeIsDistinctFromEveryOtherOutcome()
    {
        var codes = new[]
        {
            ProbeExitCodes.Completed,
            ProbeExitCodes.UsageError,
            ProbeExitCodes.EnvironmentError,
            ProbeExitCodes.RefusedByPath,
            ProbeExitCodes.NoProvider,
            ProbeExitCodes.NoDownloadTarget,
            ProbeExitCodes.SafetyRefused,
            ProbeExitCodes.AbortedByUnhandledConfiguration,
            ProbeExitCodes.CompletedWithErrors,
            ProbeExitCodes.UnexpectedError,
            ProbeExitCodes.SelectionApplyFailed,
        };

        Assert.Equal(codes.Length, codes.Distinct().Count());
    }

    /// <summary>
    /// The pair this whole distinction rests on. "The guard held and the download was correctly
    /// prevented" and "the tool could not do what it decided to do, so the run means nothing" are
    /// opposite findings; a script that saw one code for both would read a broken tool as a success.
    /// </summary>
    [Fact]
    public void ARefusalAndAnApplyFailure_DoNotShareAnExitCode() =>
        Assert.NotEqual(ProbeExitCodes.AbortedByUnhandledConfiguration, ProbeExitCodes.SelectionApplyFailed);

    // ---- --to-folder: the non-destructive mode ---------------------------------------------------

    /// <summary>
    /// <c>--to-folder</c> uses <c>Download(DirectoryInfo, delegate)</c> — hardware and software
    /// written to a directory, no connection, nothing on the wire, no CPU stopped. It exists to reach
    /// THE COMPILE A DOWNLOAD RUNS, which is measurably not the compile any of `compile --block`, the
    /// device compile, `compile-all --force` or `sanity-check` runs (2026-08-13: all four clean, the
    /// download's own compile failed naming a block).
    ///
    /// The tests below are all about ONE property: a run is unambiguously either the folder mode or
    /// the device mode. Accepting a device-path flag alongside <c>--to-folder</c> and ignoring it
    /// would produce an invocation that reads like the safe one and is not.
    /// </summary>
    [Fact]
    public void ToFolder_IsAccepted_AndCarriesTheDirectory()
    {
        var result = ProbeArgumentParser.Parse(
            new[] { ScratchProject, "--options", "Software", "--to-folder", @"C:\out\image" },
            null,
            Path.GetTempPath());

        var success = Assert.IsType<ProbeParseResult.Success>(result);
        Assert.Equal(@"C:\out\image", success.Arguments.ToFolder);
    }

    [Fact]
    public void ToFolder_DefaultsToNull_SoTheDeviceModeIsUnchanged()
    {
        var result = ProbeArgumentParser.Parse(
            new[] { ScratchProject, "--options", "Software" }, null, Path.GetTempPath());

        Assert.Null(Assert.IsType<ProbeParseResult.Success>(result).Arguments.ToFolder);
    }

    [Theory]
    [InlineData("--pc-interface", "Some Adapter")]
    [InlineData("--target", "1 X1")]
    public void ToFolder_WithADeviceFlag_IsRefused_NotIgnored(string flag, string value)
    {
        var result = ProbeArgumentParser.Parse(
            new[] { ScratchProject, "--options", "Software", "--to-folder", @"C:\out", flag, value },
            null,
            Path.GetTempPath());

        var failure = Assert.IsType<ProbeParseResult.Failure>(result);
        Assert.Contains("--to-folder", failure.Message, StringComparison.Ordinal);
    }

    // --disruptive permits selections that stop a CPU. There is no CPU in a directory, so accepting
    // it here would be accepting a dangerous-looking flag as a no-op — and a reader who saw it
    // accepted once would reasonably expect it to mean something.
    [Fact]
    public void ToFolder_WithDisruptive_IsRefused()
    {
        var result = ProbeArgumentParser.Parse(
            new[] { ScratchProject, "--options", "Software", "--to-folder", @"C:\out", "--disruptive" },
            null,
            Path.GetTempPath());

        var failure = Assert.IsType<ProbeParseResult.Failure>(result);
        Assert.Contains("--disruptive", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ToFolder_WithoutAValue_IsRefused()
    {
        var result = ProbeArgumentParser.Parse(
            new[] { ScratchProject, "--options", "Software", "--to-folder" }, null, Path.GetTempPath());

        Assert.IsType<ProbeParseResult.Failure>(result);
    }

    // ---- --options: required, literal, never numeric --------------------------------------------

    [Fact]
    public void Options_IsRequired_WithNoDefault()
    {
        var result = ProbeArgumentParser.Parse(new[] { ScratchProject }, null, Path.GetTempPath());

        var failure = Assert.IsType<ProbeParseResult.Failure>(result);
        Assert.Contains("--options is REQUIRED", failure.Message, StringComparison.Ordinal);
        foreach (var literal in DownloadOptionChoices.Literals)
        {
            Assert.Contains(literal, failure.Message, StringComparison.Ordinal);
        }
    }

    // The expected value is a string, not the enum: DownloadOptionChoice is internal to the probe,
    // and a public xUnit theory cannot take an internal parameter type.
    [Theory]
    [InlineData("Software", "Software")]
    [InlineData("software", "Software")]
    [InlineData("SoftwareOnlyChanges", "SoftwareOnlyChanges")]
    [InlineData("Hardware", "Hardware")]
    [InlineData("HARDWARE", "Hardware")]
    public void Options_AcceptsTheThreeLiterals(string raw, string expected)
    {
        var result = ProbeArgumentParser.Parse(
            new[] { ScratchProject, "--options", raw }, null, Path.GetTempPath());

        Assert.Equal(expected, Assert.IsType<ProbeParseResult.Success>(result).Arguments.Options.ToString());
    }

    /// <summary>
    /// The reason the parser is hand-written. <c>Enum.TryParse</c> accepts the underlying integer as
    /// readily as the name, so "0" would silently select <c>Software</c> - the option that resets
    /// retentive values - from what is obviously a typo.
    /// </summary>
    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("2")]
    public void Options_RejectsNumericStrings_ThatEnumTryParseWouldHaveAccepted(string raw)
    {
        // The negative control: Enum.TryParse really does accept these, which is why it is not used.
        Assert.True(Enum.TryParse<DownloadOptionChoice>(raw, out _));

        Assert.False(DownloadOptionChoices.TryParseLiteral(raw, out _));

        var result = ProbeArgumentParser.Parse(
            new[] { ScratchProject, "--options", raw }, null, Path.GetTempPath());
        var failure = Assert.IsType<ProbeParseResult.Failure>(result);
        Assert.Contains("Numeric values are rejected on purpose", failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("3")]
    [InlineData("-1")]
    [InlineData("SoftwareOnly")]
    [InlineData("Soft ware")]
    [InlineData("None")]
    [InlineData("HardwareAndSoftware")]
    [InlineData("")]
    public void Options_RejectsAnythingElse_NamingTheThree(string raw)
    {
        Assert.False(DownloadOptionChoices.TryParseLiteral(raw, out _));

        var result = ProbeArgumentParser.Parse(
            new[] { ScratchProject, "--options", raw }, null, Path.GetTempPath());

        var failure = Assert.IsType<ProbeParseResult.Failure>(result);
        foreach (var literal in DownloadOptionChoices.Literals)
        {
            Assert.Contains(literal, failure.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Options_CannotBeGivenTwice()
    {
        var result = ProbeArgumentParser.Parse(
            new[] { ScratchProject, "--options", "Software", "--options", "Hardware" }, null, Path.GetTempPath());

        Assert.IsType<ProbeParseResult.Failure>(result);
    }

    /// <summary>
    /// <c>None</c> exists in the Siemens enum and is deliberately unreachable here: it downloads
    /// nothing, so it would produce the most reassuring possible report having transferred nothing.
    /// </summary>
    [Fact]
    public void Options_CannotSelectNone()
    {
        Assert.False(DownloadOptionChoices.TryParseLiteral("None", out _));
        Assert.Equal(3, Enum.GetValues(typeof(DownloadOptionChoice)).Length);
    }

    // ---- the consequence warnings ---------------------------------------------------------------

    [Fact]
    public void Software_PrintsTheRetentiveWipeWarning_BeforeActing()
    {
        var log = RunWithStubSession(DownloadOptionChoice.Software);

        Assert.Contains(log, l => l.Contains("DESTRUCTIVE OPTION SELECTED: Software"));
        Assert.Contains(log, l => l.Contains("this also applies to retentive values"));
        Assert.Contains(log, l => l.Contains("THIS WIPES RETENTIVE DATA ON THE DEVICE"));
    }

    [Fact]
    public void Hardware_PrintsTheStopWarningAndTheUnresolvedRetentiveGap()
    {
        var log = RunWithStubSession(DownloadOptionChoice.Hardware);

        Assert.Contains(log, l => l.Contains("DESTRUCTIVE OPTION SELECTED: Hardware"));
        Assert.Contains(log, l => l.Contains("ALWAYS STOPS THE CPU"));
        Assert.Contains(log, l => l.Contains("requires a person in TIA Portal"));
        Assert.Contains(log, l => l.Contains("G3"));
        Assert.Contains(log, l => l.Contains("UNKNOWN, not as safe"));
        Assert.Contains(log, l => l.Contains("EXPECTED TO ABORT"));
    }

    [Fact]
    public void SoftwareOnlyChanges_SaysWhatOnlyChangesActuallyMeans()
    {
        var log = RunWithStubSession(DownloadOptionChoice.SoftwareOnlyChanges);

        Assert.Contains(log, l => l.Contains("Option selected: SoftwareOnlyChanges"));
        Assert.Contains(log, l => l.Contains("NOT the list of blocks you edited"));
        Assert.DoesNotContain(log, l => l.Contains("DESTRUCTIVE OPTION SELECTED"));
    }

    /// <summary>
    /// Every run states, before it starts, that an abort only counts as a result if the tool managed
    /// to answer what it decided to answer. The live run that prompted this could not, and its log
    /// summarised the failure using the policy's own reasoning.
    /// </summary>
    [Theory]
    [InlineData("Software")]
    [InlineData("SoftwareOnlyChanges")]
    [InlineData("Hardware")]
    public void EveryRun_WarnsUpFront_ThatAFailedApplyIsNotAnAbortResult(string option)
    {
        Assert.True(DownloadOptionChoices.TryParseLiteral(option, out var choice));
        var log = RunWithStubSession(choice);

        Assert.Contains(log, l => l.Contains("an abort is a RESULT only when this tool"));
        Assert.Contains(log, l => l.Contains($"exits {ProbeExitCodes.SelectionApplyFailed} and PROVES NOTHING"));
    }

    [Fact]
    public void EveryRun_PrintsTheDenyListAndThatItIsARealDownload()
    {
        var log = RunWithStubSession(DownloadOptionChoice.SoftwareOnlyChanges);

        Assert.Contains(log, l => l.Contains("PERFORMS A REAL DEVICE DOWNLOAD"));
        foreach (var (configurationType, selection) in NoActionFirstPolicy.DeniedSelections)
        {
            Assert.Contains(log, l => l.Contains($"{configurationType} -> {selection}"));
        }
    }

    // ---- the selection policy -------------------------------------------------------------------

    [Fact]
    public void Policy_ChoosesNoAction_WheneverItExists()
    {
        var decision = NoActionFirstPolicy.Decide("StopModules", new[] { "NoAction", "StopAll" });

        Assert.Equal(ConfigurationDecisionKind.AnsweredNoAction, decision.Kind);
        Assert.Equal("NoAction", decision.Selection);
    }

    /// <summary>
    /// The rule that had to be added. <c>ConsistentBlocksDownload</c> - "Download software to
    /// device" - offers exactly one selection and no <c>NoAction</c>. Leaving it unhandled aborted
    /// every run of every option on an entirely harmless bookkeeping prompt, before
    /// <c>DataBlockReinitialization</c> could ever be raised.
    /// </summary>
    [Fact]
    public void Policy_TakesTheSoleSelection_AndSaysItHadNoChoice()
    {
        var decision = NoActionFirstPolicy.Decide("ConsistentBlocksDownload", new[] { "ConsistentDownload" });

        Assert.Equal(ConfigurationDecisionKind.AnsweredOnlySelectionAvailable, decision.Kind);
        Assert.Equal("ConsistentDownload", decision.Selection);
        Assert.True(decision.IsLoud);
        Assert.Contains("only permitted selection", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Policy_LeavesUnhandled_WhenTheOnlySelectionIsDenied()
    {
        var decision = NoActionFirstPolicy.Decide("StopModules", new[] { "StopAll" });

        Assert.Equal(ConfigurationDecisionKind.LeftUnhandledOnlyDeniedSelections, decision.Kind);
        Assert.Null(decision.Selection);
        Assert.True(decision.IsLoud);
    }

    [Fact]
    public void Policy_LeavesUnhandled_RatherThanGuessBetweenSeveralPermittedSelections()
    {
        var decision = NoActionFirstPolicy.Decide("LoadIdentificationData", new[] { "LoadData", "LoadNothing" });

        Assert.Equal(ConfigurationDecisionKind.LeftUnhandledAmbiguousWithoutNoAction, decision.Kind);
        Assert.Null(decision.Selection);
    }

    [Fact]
    public void Policy_LeavesUnhandled_WhenThereIsNoSelectionPropertyAtAll()
    {
        var decision = NoActionFirstPolicy.Decide("CheckBeforeDownload", availableSelections: null);

        Assert.Equal(ConfigurationDecisionKind.LeftUnhandledNoSelectionProperty, decision.Kind);
        Assert.Null(decision.Selection);
    }

    [Theory]
    [InlineData("StopModules", "StopAll")]
    [InlineData("DataBlockReinitialization", "StopPlcAndReinitialize")]
    [InlineData("ResetModule", "DeleteAll")]
    [InlineData("InitializeMemory", "AcceptAll")]
    public void Policy_NeverChoosesADeniedSelection_EvenAsTheSoleOption(string type, string denied)
    {
        Assert.True(NoActionFirstPolicy.IsDenied(type, denied));
        Assert.Null(NoActionFirstPolicy.Decide(type, new[] { denied }).Selection);
        Assert.Equal("NoAction", NoActionFirstPolicy.Decide(type, new[] { "NoAction", denied }).Selection);
    }

    /// <summary>
    /// The strongest check available without Portal: run the policy over EVERY selection enum the
    /// installed V20 <c>Siemens.Engineering.dll</c> actually declares, rather than over a list
    /// someone typed into this file. If Siemens adds a configuration, this test sees it.
    /// </summary>
    [Fact]
    public void Policy_AgainstEveryRealV20Configuration_NeverChoosesSomethingDenied()
    {
        var configurations = RealSelectionConfigurations();

        var examined = 0;
        foreach (var (typeName, selections) in configurations)
        {
            var decision = NoActionFirstPolicy.Decide(typeName, selections);
            examined++;

            if (decision.Selection is { } chosen)
            {
                Assert.False(
                    NoActionFirstPolicy.IsDenied(typeName, chosen),
                    $"{typeName} would be answered with the denied selection '{chosen}'.");

                Assert.Contains(chosen, selections);
            }
        }

        // Empty is not clean: a reflection walk that silently matched nothing would pass vacuously.
        Assert.True(examined >= 20, $"Only {examined} selection configurations were examined.");
    }

    /// <summary>
    /// The three configurations this experiment turns on, checked by name against the real assembly:
    /// the two destructive ones must be answerable with <c>NoAction</c>, and the bookkeeping one must
    /// be the sole-selection case that rule 2 exists for.
    /// </summary>
    [Fact]
    public void Policy_AgainstTheThreeConfigurationsThatDecideTheExperiment()
    {
        var configurations = RealSelectionConfigurations().ToDictionary(c => c.TypeName, c => c.Selections);

        Assert.Equal("NoAction", NoActionFirstPolicy.Decide("StopModules", configurations["StopModules"]).Selection);
        Assert.Equal(
            "NoAction",
            NoActionFirstPolicy.Decide("DataBlockReinitialization", configurations["DataBlockReinitialization"]).Selection);

        var consistent = NoActionFirstPolicy.Decide("ConsistentBlocksDownload", configurations["ConsistentBlocksDownload"]);
        Assert.Equal(ConfigurationDecisionKind.AnsweredOnlySelectionAvailable, consistent.Kind);
        Assert.Equal("ConsistentDownload", consistent.Selection);
    }

    // ---- the recorder: logging, and applying nothing but what the policy said --------------------

    [Fact]
    public void Recorder_LogsAnUnrecognisedConfigurationInFull_RatherThanSkippingIt()
    {
        using var log = new ProbeLog(new StringWriter(), filePath: null);
        var recorder = new ConfigurationRecorder(log, "PRE");

        var view = new RaisedConfigurationView(
            runtimeTypeName: "Siemens.Engineering.Download.Configurations.SomethingNobodyListed",
            typeChain: new[] { "SomethingNobodyListed", "DownloadConfiguration" },
            message: "A configuration this tool has never seen.\nWith a second line.",
            messageReadError: null,
            familyDescription: "*** UNRECOGNISED CONFIGURATION FAMILY *** - logged in full, nothing written to it",
            selection: null,
            properties: new[]
            {
                new ConfigurationPropertyView("SomeFlag", "Boolean", "True"),
                new ConfigurationPropertyView("SomeCount", "Int32", "7"),
            });

        var record = recorder.Record(view);
        var text = string.Join("\n", log.Lines);

        Assert.Contains("SomethingNobodyListed", text, StringComparison.Ordinal);
        Assert.Contains("SomethingNobodyListed <- DownloadConfiguration", text, StringComparison.Ordinal);
        Assert.Contains("UNRECOGNISED CONFIGURATION FAMILY", text, StringComparison.Ordinal);

        // Verbatim, including the second line - never summarised, never truncated.
        Assert.Contains("A configuration this tool has never seen.", text, StringComparison.Ordinal);
        Assert.Contains("With a second line.", text, StringComparison.Ordinal);

        // Every readable property, not a chosen subset: the shape is unknown, so everything is shown.
        Assert.Contains("SomeFlag (Boolean) = True", text, StringComparison.Ordinal);
        Assert.Contains("SomeCount (Int32) = 7", text, StringComparison.Ordinal);

        Assert.Null(record.ChosenSelection);
        Assert.True(record.WasLeftUnhandled);
        Assert.Single(recorder.Recorded);
    }

    [Fact]
    public void Recorder_AppliesNoAction_AndNothingElse()
    {
        using var log = new ProbeLog(new StringWriter(), filePath: null);
        var recorder = new ConfigurationRecorder(log, "PRE");
        var selection = new FakeSelection("StopModulesSelections", new[] { "NoAction", "StopAll" });

        var record = recorder.Record(ViewFor("StopModules", "The modules are stopped for downloading to device.", selection));

        Assert.Equal(new[] { "NoAction" }, selection.Applied);
        Assert.Equal("NoAction", record.ChosenSelection);
        Assert.Equal(ConfigurationDecisionKind.AnsweredNoAction, record.Decision);
    }

    [Fact]
    public void Recorder_WritesNothingAtAll_WhenTheOnlySelectionIsDenied()
    {
        using var log = new ProbeLog(new StringWriter(), filePath: null);
        var recorder = new ConfigurationRecorder(log, "PRE");
        var selection = new FakeSelection("StopModulesSelections", new[] { "StopAll" });

        var record = recorder.Record(ViewFor("StopModules", "The modules are stopped.", selection));

        Assert.Empty(selection.Applied);
        Assert.Null(record.ChosenSelection);
        Assert.Contains("LEFT UNHANDLED", string.Join("\n", log.Lines), StringComparison.Ordinal);
        Assert.Contains("ON THE DENY LIST, NEVER CHOSEN", string.Join("\n", log.Lines), StringComparison.Ordinal);
    }

    [Fact]
    public void Recorder_TakesTheSoleSelection_AndTheLogSaysItHadNoChoice()
    {
        using var log = new ProbeLog(new StringWriter(), filePath: null);
        var recorder = new ConfigurationRecorder(log, "PRE");
        var selection = new FakeSelection("ConsistentBlocksDownloadSelections", new[] { "ConsistentDownload" });

        var record = recorder.Record(ViewFor("ConsistentBlocksDownload", "Download software to device.", selection));

        Assert.Equal(new[] { "ConsistentDownload" }, selection.Applied);
        Assert.Equal("ConsistentDownload", record.ChosenSelection);
        Assert.Contains("NO CHOICE WAS AVAILABLE", string.Join("\n", log.Lines), StringComparison.Ordinal);
    }

    [Fact]
    public void Recorder_ReportsAReadBackThatDoesNotMatchWhatWasSet()
    {
        using var log = new ProbeLog(new StringWriter(), filePath: null);
        var recorder = new ConfigurationRecorder(log, "PRE");
        var selection = new FakeSelection("StopModulesSelections", new[] { "NoAction", "StopAll" })
        {
            ReadBackOverride = "StopAll",
        };

        recorder.Record(ViewFor("StopModules", "The modules are stopped.", selection));

        Assert.Contains("READ-BACK DOES NOT MATCH", string.Join("\n", log.Lines), StringComparison.Ordinal);
    }

    // ---- the inner exception: the line that was being thrown away --------------------------------
    //
    // The live failure logged "TargetInvocationException: Exception has been thrown by the target of
    // an invocation." and nothing else. That sentence is emitted verbatim for EVERY failure
    // reflection has ever wrapped; the cause was in InnerException, and the log file contained
    // neither "Inner" nor "--->" nor an HResult anywhere.

    [Fact]
    public void ExceptionReport_UnwrapsAReflectionWrapper_AndNamesTheRealCause()
    {
        var wrapper = RealReflectionWrapper(
            new InvalidOperationException("THE ACTUAL REASON THE SET WAS REFUSED"));

        // The negative control that gives the assertions below their meaning: the wrapper's own
        // message really does say nothing, so a report built from it alone is worthless.
        Assert.DoesNotContain("THE ACTUAL REASON", wrapper.Message, StringComparison.Ordinal);

        var text = string.Join("\n", ExceptionReport.Describe(wrapper));

        Assert.Contains("System.Reflection.TargetInvocationException", text, StringComparison.Ordinal);
        Assert.Contains("System.InvalidOperationException", text, StringComparison.Ordinal);
        Assert.Contains("THE ACTUAL REASON THE SET WAS REFUSED", text, StringComparison.Ordinal);
        Assert.Contains("(inner)", text, StringComparison.Ordinal);
        Assert.Contains("hresult", text, StringComparison.Ordinal);

        // And the one-line form, which is what a summary or a verdict carries.
        var summary = ExceptionReport.Summarise(wrapper);
        Assert.Contains("--->", summary, StringComparison.Ordinal);
        Assert.Contains("THE ACTUAL REASON THE SET WAS REFUSED", summary, StringComparison.Ordinal);

        Assert.True(ExceptionReport.IsReflectionWrapper(wrapper));
        Assert.IsType<InvalidOperationException>(ExceptionReport.Unwrap(wrapper));
    }

    [Fact]
    public void ExceptionReport_WalksTheWholeChain_NotJustOneLevel()
    {
        var deep = new InvalidOperationException(
            "level-1",
            new ArgumentException("level-2", new NotSupportedException("level-3-THE-CAUSE")));

        var text = string.Join("\n", ExceptionReport.Describe(deep));

        Assert.Contains("level-1", text, StringComparison.Ordinal);
        Assert.Contains("level-2", text, StringComparison.Ordinal);
        Assert.Contains("level-3-THE-CAUSE", text, StringComparison.Ordinal);
        Assert.Equal("level-3-THE-CAUSE", ExceptionReport.Unwrap(deep).Message);
    }

    /// <summary>
    /// The chain shapes that hang off something other than <c>InnerException</c>. An aggregate
    /// reports only its FIRST inner exception through <c>InnerException</c>, and
    /// <c>ReflectionTypeLoadException</c>'s loader exceptions hang off no inner exception at all — so
    /// a walker that followed only the obvious spine would lose them in exactly the way this class
    /// was written to stop.
    /// </summary>
    [Fact]
    public void ExceptionReport_FollowsAggregateAndLoaderExceptions_NotJustInnerException()
    {
        var aggregate = new AggregateException(
            new InvalidOperationException("first-cause"),
            new NotSupportedException("SECOND-CAUSE-THAT-INNEREXCEPTION-ALONE-WOULD-LOSE"));

        var aggregateText = string.Join("\n", ExceptionReport.Describe(aggregate));
        Assert.Contains("first-cause", aggregateText, StringComparison.Ordinal);
        Assert.Contains("SECOND-CAUSE-THAT-INNEREXCEPTION-ALONE-WOULD-LOSE", aggregateText, StringComparison.Ordinal);

        var typeLoad = new ReflectionTypeLoadException(
            new Type?[] { null },
            new Exception?[] { new FileNotFoundException("LOADER-EXCEPTION-TEXT") });

        var typeLoadText = string.Join("\n", ExceptionReport.Describe(typeLoad));
        Assert.Contains("LOADER-EXCEPTION-TEXT", typeLoadText, StringComparison.Ordinal);
    }

    [Fact]
    public void ExceptionReport_OnASingleException_ReportsExactlyOneLevel()
    {
        var text = string.Join("\n", ExceptionReport.Describe(new InvalidOperationException("only-one")));

        Assert.Contains("[1]", text, StringComparison.Ordinal);
        Assert.DoesNotContain("[2]", text, StringComparison.Ordinal);
        Assert.DoesNotContain("--->", ExceptionReport.Summarise(new InvalidOperationException("only-one")), StringComparison.Ordinal);
    }

    // ---- a refused selection: a TOOL FAILURE, never a refusal -------------------------------------

    [Fact]
    public void Recorder_WhenTheApiRefusesTheSelection_FilesItAsAToolFailure_AndLogsTheInnerException()
    {
        using var log = new ProbeLog(new StringWriter(), filePath: null);
        var recorder = new ConfigurationRecorder(log, "PRE");
        var selection = new FakeSelection("StopModulesSelections", new[] { "NoAction", "StopAll" }, current: "NoAction")
        {
            DefaultValuedSelection = "NoAction",
            PrimaryRouteError = RealReflectionWrapper(new InvalidOperationException("REFUSED-BY-THE-API")),
            FallbackRouteError = new InvalidOperationException("REFUSED-BY-THE-API-AGAIN"),
        };

        var record = recorder.Record(
            ViewFor("StopModules", "The modules are stopped for downloading to device.", selection));

        // The decision was still correct — and only the deny-list-safe selection was ever asked for.
        Assert.Equal(new[] { "NoAction" }, selection.Applied);
        Assert.Equal(ConfigurationDecisionKind.AnsweredNoAction, record.Decision);

        // But the OUTCOME is a failure, not a refusal, and nothing was applied.
        Assert.Equal(ConfigurationOutcomeKind.FailedToApply, record.Outcome);
        Assert.Null(record.ChosenSelection);
        Assert.Equal("NoAction", record.AttemptedSelection);
        Assert.Empty(recorder.RefusedByPolicy);
        Assert.Single(recorder.FailedToApply);

        // The line that used to contradict the ANSWER line above it now carries the failure.
        Assert.Contains("FAILED TO APPLY", record.WhyUnanswered, StringComparison.Ordinal);
        Assert.DoesNotContain("so it is chosen", record.WhyUnanswered, StringComparison.Ordinal);

        var text = string.Join("\n", log.Lines);
        Assert.Contains("THIS IS A TOOL FAILURE, NOT A REFUSAL", text, StringComparison.Ordinal);

        // The whole point: the cause reaches the log, from BOTH routes.
        Assert.Contains("REFUSED-BY-THE-API", text, StringComparison.Ordinal);
        Assert.Contains("REFUSED-BY-THE-API-AGAIN", text, StringComparison.Ordinal);

        // And the two facts that make the failure diagnosable next time, without a re-run.
        Assert.Contains("the value an unset field reads as", text, StringComparison.Ordinal);
        Assert.Contains("ALREADY reads 'NoAction'", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// A fallback route working is not a quiet success. It means the primary route is wrong on a
    /// binary nobody may run casually to find that out, so the log says so and keeps the failure.
    /// </summary>
    [Fact]
    public void Recorder_WhenTheFallbackRouteApplies_SaysThePrimaryRouteIsBroken()
    {
        using var log = new ProbeLog(new StringWriter(), filePath: null);
        var recorder = new ConfigurationRecorder(log, "PRE");
        var selection = new FakeSelection("StopModulesSelections", new[] { "NoAction", "StopAll" })
        {
            PrimaryRouteError = RealReflectionWrapper(new InvalidOperationException("PRIMARY-ROUTE-CAUSE")),
        };

        var record = recorder.Record(ViewFor("StopModules", "The modules are stopped.", selection));

        Assert.Equal(ConfigurationOutcomeKind.Answered, record.Outcome);
        Assert.Equal("NoAction", record.ChosenSelection);
        Assert.Equal("SetAttribute (fake)", record.AppliedByRoute);
        Assert.Empty(recorder.FailedToApply);

        var text = string.Join("\n", log.Lines);
        Assert.Contains("The primary route is broken", text, StringComparison.Ordinal);
        Assert.Contains("PRIMARY-ROUTE-CAUSE", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The safety property under the retry: a refused selection is never escalated to a different
    /// one. Only the value the policy chose is ever asked for, however many routes are tried.
    /// </summary>
    [Fact]
    public void Recorder_OnARefusedSelection_NeverAsksForADifferentOne()
    {
        using var log = new ProbeLog(new StringWriter(), filePath: null);
        var recorder = new ConfigurationRecorder(log, "PRE");
        var selection = new FakeSelection("StopModulesSelections", new[] { "NoAction", "StopAll" })
        {
            PrimaryRouteError = new InvalidOperationException("no"),
            FallbackRouteError = new InvalidOperationException("still no"),
        };

        recorder.Record(ViewFor("StopModules", "The modules are stopped.", selection));

        Assert.Equal(new[] { "NoAction" }, selection.Applied);
        Assert.DoesNotContain("StopAll", selection.Applied);
    }

    [Fact]
    public void Recorder_ReportsTheAttributeAccessModesOpennessDeclaresForTheLiveObject()
    {
        using var log = new ProbeLog(new StringWriter(), filePath: null);
        var recorder = new ConfigurationRecorder(log, "PRE");

        recorder.Record(new RaisedConfigurationView(
            runtimeTypeName: "Siemens.Engineering.Download.Configurations.StopModules",
            typeChain: new[] { "StopModules" },
            message: "The modules are stopped.",
            messageReadError: null,
            familyDescription: "DownloadSelectionConfiguration",
            selection: new FakeSelection("StopModulesSelections", new[] { "NoAction", "StopAll" }),
            properties: Array.Empty<ConfigurationPropertyView>(),
            attributeInfos: new[] { new ConfigurationAttributeInfoView("CurrentSelection", "Read", "None") }));

        var text = string.Join("\n", log.Lines);
        Assert.Contains("CurrentSelection : access=Read", text, StringComparison.Ordinal);
    }

    // ---- the abort: a result, not a failure ------------------------------------------------------

    [Fact]
    public void Abort_WithAnUnhandledConfiguration_GetsItsOwnExitCode_AndNamesTheConfiguration()
    {
        using var log = new ProbeLog(new StringWriter(), filePath: null);
        var pre = new ConfigurationRecorder(log, "PRE");
        var post = new ConfigurationRecorder(log, "POST");

        pre.Record(ViewFor("StopModules", "The modules are stopped.", new FakeSelection("S", new[] { "StopAll" })));

        var outcome = ProbeSession.ClassifyAbort(
            new EngineeringTargetInvocationException("Unhandled configuration prevented the download."), pre, post, log);

        Assert.Equal(ProbeExitCodes.AbortedByUnhandledConfiguration, outcome.ExitCode);
        Assert.NotEqual(ProbeExitCodes.Completed, outcome.ExitCode);
        Assert.NotEqual(ProbeExitCodes.UnexpectedError, outcome.ExitCode);
        Assert.Contains("StopModules", outcome.Verdict, StringComparison.Ordinal);

        var text = string.Join("\n", log.Lines);
        Assert.Contains("DOWNLOAD ABORTED - THIS IS A RESULT", text, StringComparison.Ordinal);
        Assert.Contains("1 CONFIGURATION(S) REFUSED BY POLICY — THE TOOL WORKED", text, StringComparison.Ordinal);
        Assert.Contains("StopModules", text, StringComparison.Ordinal);

        // And it is filed as a refusal, not as a failure — the whole point of the two buckets.
        Assert.Empty(pre.FailedToApply);
        Assert.Single(pre.RefusedByPolicy);
    }

    /// <summary>
    /// The honesty check on the one above: the same exception with nothing left unhandled is NOT a
    /// refusal, and must not be reported as one. Otherwise every unexplained download failure would
    /// read as the guard working.
    /// </summary>
    [Fact]
    public void Abort_WithNothingLeftUnhandled_IsNotReportedAsARefusal()
    {
        using var log = new ProbeLog(new StringWriter(), filePath: null);
        var pre = new ConfigurationRecorder(log, "PRE");
        var post = new ConfigurationRecorder(log, "POST");

        pre.Record(ViewFor("StopModules", "The modules are stopped.", new FakeSelection("S", new[] { "NoAction", "StopAll" })));

        var outcome = ProbeSession.ClassifyAbort(
            new EngineeringTargetInvocationException("Something else went wrong."), pre, post, log);

        Assert.Equal(ProbeExitCodes.UnexpectedError, outcome.ExitCode);
        Assert.Contains("NOT caused by this", string.Join("\n", log.Lines), StringComparison.Ordinal);
    }

    /// <summary>
    /// THE BUG THIS TASK EXISTS FOR, at the summary level. A configuration the tool FAILED to answer
    /// aborted the download and was reported with the heading "DOWNLOAD ABORTED - THIS IS A RESULT",
    /// exit code 7, and the policy's own reason — "a NoAction selection exists, so it is chosen" —
    /// printed as the explanation of why it went unanswered, directly contradicting the ANSWER line
    /// above it. The run proved nothing and read as a success.
    /// </summary>
    [Fact]
    public void Abort_AfterAFailedApply_IsAToolFailure_NotARefusal()
    {
        using var log = new ProbeLog(new StringWriter(), filePath: null);
        var pre = new ConfigurationRecorder(log, "PRE");
        var post = new ConfigurationRecorder(log, "POST");

        pre.Record(ViewFor(
            "StopModules",
            "The modules are stopped for downloading to device.",
            new FakeSelection("StopModulesSelections", new[] { "NoAction", "StopAll" })
            {
                PrimaryRouteError = RealReflectionWrapper(new InvalidOperationException("REFUSED-BY-THE-API")),
                FallbackRouteError = new InvalidOperationException("REFUSED-BY-THE-API"),
            }));

        var outcome = ProbeSession.ClassifyAbort(
            new EngineeringTargetInvocationException("Download configuration 'StopModules' was unhandled."), pre, post, log);

        Assert.Equal(ProbeExitCodes.SelectionApplyFailed, outcome.ExitCode);
        Assert.NotEqual(ProbeExitCodes.AbortedByUnhandledConfiguration, outcome.ExitCode);
        Assert.Contains("TOOL FAILURE", outcome.Verdict, StringComparison.Ordinal);
        Assert.Contains("means nothing", outcome.Verdict, StringComparison.Ordinal);

        var text = string.Join("\n", log.Lines);
        Assert.Contains("DOWNLOAD ABORTED - THE TOOL FAILED TO ANSWER A CONFIGURATION", text, StringComparison.Ordinal);
        Assert.DoesNotContain("DOWNLOAD ABORTED - THIS IS A RESULT", text, StringComparison.Ordinal);

        // The per-configuration line carries the FAILURE, never the policy's reason.
        Assert.Contains("why unanswered     : *** FAILED TO APPLY 'NoAction' ***", text, StringComparison.Ordinal);
        Assert.DoesNotContain("why unanswered     : a NoAction selection exists, so it is chosen", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Both at once: a configuration refused on purpose AND one that could not be applied. The
    /// failure takes precedence — a run containing one is not evidence of anything — but both are
    /// reported, under their own headings, because they are different findings.
    /// </summary>
    [Fact]
    public void Abort_WithBothARefusalAndAFailure_ReportsThemSeparately_AndTheFailureWins()
    {
        using var log = new ProbeLog(new StringWriter(), filePath: null);
        var pre = new ConfigurationRecorder(log, "PRE");
        var post = new ConfigurationRecorder(log, "POST");

        pre.Record(ViewFor("StopModules", "Stopped.", new FakeSelection("S", new[] { "NoAction", "StopAll" })
        {
            PrimaryRouteError = new InvalidOperationException("refused"),
            FallbackRouteError = new InvalidOperationException("refused"),
        }));
        pre.Record(ViewFor("ResetModule", "Reset.", new FakeSelection("R", new[] { "DeleteAll" })));

        var outcome = ProbeSession.ClassifyAbort(
            new EngineeringTargetInvocationException("unhandled"), pre, post, log);

        Assert.Equal(ProbeExitCodes.SelectionApplyFailed, outcome.ExitCode);

        var text = string.Join("\n", log.Lines);
        Assert.Contains("THIS TOOL FAILED TO ANSWER — THE TOOL IS BROKEN", text, StringComparison.Ordinal);
        Assert.Contains("REFUSED BY POLICY — THE TOOL WORKED", text, StringComparison.Ordinal);
        Assert.Contains("StopModules", text, StringComparison.Ordinal);
        Assert.Contains("ResetModule", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// MEASURED, and it changes how an Openness failure has to be read:
    /// <c>EngineeringException</c> DOES NOT USE <c>InnerException</c>. Its public
    /// <c>(text, exception)</c> constructor folds the exception's message into its own
    /// <c>Message</c> as extra lines and discards the object — so <c>InnerException</c> is null on
    /// the abort this tool exists to interpret, and code that checked only <c>InnerException</c>
    /// (which is what the probe used to do) would report "no further detail" on an exception that
    /// carries plenty.
    ///
    /// Two consequences, both asserted: the folded text must survive because the whole multi-line
    /// Message is rendered, and Openness' own <c>DetailMessageData</c> — the channel it uses instead
    /// — must be read, which nothing in this program did before.
    /// </summary>
    [Fact]
    public void Abort_OpennessDiscardsInnerException_SoTheFoldedTextAndItsDetailChannelAreLoggedInstead()
    {
        var folded = new EngineeringTargetInvocationException(
            "outer text", new InvalidOperationException("INNER-ABORT-CAUSE"));

        // The negative control: this is NOT how .NET exceptions normally behave, and it is why the
        // old `if (ex.InnerException is { } inner)` branch could never have fired here.
        Assert.Null(folded.InnerException);
        Assert.Contains("INNER-ABORT-CAUSE", folded.Message, StringComparison.Ordinal);

        using var log = new ProbeLog(new StringWriter(), filePath: null);
        var pre = new ConfigurationRecorder(log, "PRE");
        var post = new ConfigurationRecorder(log, "POST");

        pre.Record(ViewFor("StopModules", "Stopped.", new FakeSelection("S", new[] { "StopAll" })));
        ProbeSession.ClassifyAbort(folded, pre, post, log);

        // The folded second line reaches the log because the whole Message is rendered, not its head.
        Assert.Contains("INNER-ABORT-CAUSE", string.Join("\n", log.Lines), StringComparison.Ordinal);

        var withDetail = new EngineeringTargetInvocationException(
            "the configuration was unhandled", new[] { "OPENNESS-DETAIL-ONE", "OPENNESS-DETAIL-TWO" });

        var rendered = string.Join("\n", ExceptionReport.Describe(withDetail));
        Assert.Contains("OPENNESS-DETAIL-ONE", rendered, StringComparison.Ordinal);
        Assert.Contains("OPENNESS-DETAIL-TWO", rendered, StringComparison.Ordinal);
        Assert.Contains("substitute for InnerException", rendered, StringComparison.Ordinal);
    }

    // ---- the measured shape of the API this all binds to -----------------------------------------

    /// <summary>
    /// The leading hypothesis for the refused set was a type mismatch in the reflective write:
    /// <c>CurrentSelection</c> declared on a closed generic base, set with a boxed <c>int</c> or the
    /// wrong enum type. This RULES THAT OUT against the installed V20 assembly, and pins the shape so
    /// that a Siemens change to it fails here rather than at a controller:
    /// <c>CurrentSelection</c> is declared on <c>StopModules</c> ITSELF, its type is the concrete
    /// enum <c>StopModulesSelections</c>, and it has a public setter. There is no generic base — the
    /// non-generic <c>DownloadSelectionConfiguration</c> declares no such property at all.
    /// </summary>
    [Fact]
    public void CurrentSelection_IsDeclaredOnTheConcreteConfiguration_OverItsOwnEnum()
    {
        var stopModules = typeof(Siemens.Engineering.Download.Configurations.StopModules);

        var property = stopModules.GetProperty(
            "CurrentSelection", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Assert.NotNull(property);
        Assert.Equal(stopModules, property!.DeclaringType);
        Assert.Equal("StopModulesSelections", property.PropertyType.Name);
        Assert.True(property.PropertyType.IsEnum);
        Assert.True(property.CanRead);
        Assert.True(property.CanWrite);
        Assert.NotNull(property.GetSetMethod());

        // No generic base to bind to the wrong closed form of.
        var selectionBase = typeof(Siemens.Engineering.Download.Configurations.DownloadSelectionConfiguration);
        Assert.False(selectionBase.IsGenericType);
        Assert.Null(selectionBase.GetProperty(
            "CurrentSelection", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));

        // The fallback route exists on the type, and takes (name, object) — the same call the
        // compiled property setter makes internally.
        var setAttribute = typeof(Siemens.Engineering.Download.Configurations.DownloadConfiguration)
            .GetMethod("SetAttribute", new[] { typeof(string), typeof(object) });
        Assert.NotNull(setAttribute);
    }

    /// <summary>
    /// Why "current selection before we answered : NoAction" is not the evidence it looks like.
    /// <c>StopModulesSelections.NoAction</c> is ZERO, so it is also what an untouched field reads as
    /// — whereas <c>DataBlockReinitializationSelections.NoAction</c> is 1, where the same reading
    /// really would mean something. The log now says which case it is looking at.
    /// </summary>
    [Fact]
    public void NoAction_IsTheZeroValue_OnStopModulesButNotOnDataBlockReinitialization()
    {
        Assert.Equal(0, (int)Siemens.Engineering.Download.Configurations.StopModulesSelections.NoAction);
        Assert.NotEqual(0, (int)Siemens.Engineering.Download.Configurations.DataBlockReinitializationSelections.NoAction);
    }

    [Fact]
    public void Run_PropagatesTheSessionExitCode_AndTheFooterCallsAnAbortAResult()
    {
        var logDir = NewTempDir();
        using var repo = ProbeFenceRepo.Permitting();

        var exit = ProbeProgram.Run(
            new[] { repo.ProjectPath, "--options", "Hardware", "--log-dir", logDir },
            new StringWriter(),
            new StringWriter(),
            (_, log) =>
            {
                log.Line("(stub session - Portal not contacted)");
                return new ProbeOutcome(
                    ProbeExitCodes.AbortedByUnhandledConfiguration,
                    "Aborted by 1 unhandled configuration(s): StopModules.");
            },
            repo.BinaryDirectory);

        Assert.Equal(ProbeExitCodes.AbortedByUnhandledConfiguration, exit);

        var written = Assert.Single(Directory.GetFiles(logDir));
        var text = File.ReadAllText(written);
        Assert.Contains("A RESULT, NOT A FAILURE", text, StringComparison.Ordinal);
        Assert.Contains("StopModules", text, StringComparison.Ordinal);
    }

    // ---- the log is the artifact -----------------------------------------------------------------

    [Fact]
    public void Run_WritesTheWholeLogToAFile_AsWellAsToStdout()
    {
        var logDir = NewTempDir();
        var stdout = new StringWriter();
        using var repo = ProbeFenceRepo.Permitting();

        ProbeProgram.Run(
            new[] { repo.ProjectPath, "--options", "SoftwareOnlyChanges", "--log-dir", logDir },
            stdout,
            new StringWriter(),
            (_, log) =>
            {
                log.Line("MARKER-LINE-IN-THE-SESSION");
                return new ProbeOutcome(ProbeExitCodes.Completed, "done");
            },
            repo.BinaryDirectory);

        var written = Assert.Single(Directory.GetFiles(logDir));
        Assert.Contains("MARKER-LINE-IN-THE-SESSION", File.ReadAllText(written), StringComparison.Ordinal);
        Assert.Contains("MARKER-LINE-IN-THE-SESSION", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Run_WithJson_KeepsStdoutParseable_AndEmbedsTheLogRatherThanSummarisingIt()
    {
        var logDir = NewTempDir();
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        using var repo = ProbeFenceRepo.Permitting();

        ProbeProgram.Run(
            new[] { repo.ProjectPath, "--options", "SoftwareOnlyChanges", "--json", "--log-dir", logDir },
            stdout,
            stderr,
            (_, log) =>
            {
                log.Line("MARKER-LINE-IN-THE-SESSION");
                return new ProbeOutcome(ProbeExitCodes.Completed, "done");
            },
            repo.BinaryDirectory);

        var json = stdout.ToString();
        using var document = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal("download-probe", document.RootElement.GetProperty("tool").GetString());
        Assert.Equal("SoftwareOnlyChanges", document.RootElement.GetProperty("options").GetString());
        Assert.Contains("MARKER-LINE-IN-THE-SESSION", json, StringComparison.Ordinal);

        // The human log went to stderr so stdout stayed a single JSON document.
        Assert.Contains("MARKER-LINE-IN-THE-SESSION", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Run_RefusesRatherThanRunningUnlogged_WhenTheLogFileCannotBeOpened()
    {
        var stderr = new StringWriter();
        using var repo = ProbeFenceRepo.Permitting();

        // A path that cannot be a directory: an existing FILE stands where the log directory would go.
        var blocker = Path.Combine(Path.GetTempPath(), "download-probe-blocker-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(blocker, "not a directory");

        try
        {
            var exit = ProbeProgram.Run(
                new[] { repo.ProjectPath, "--options", "SoftwareOnlyChanges", "--log-dir", blocker },
                new StringWriter(),
                stderr,
                (_, _) => throw new InvalidOperationException("The session must not run without a log."),
                repo.BinaryDirectory);

            Assert.Equal(ProbeExitCodes.EnvironmentError, exit);
            Assert.Contains("refused rather than run un-logged", stderr.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(blocker);
        }
    }

    // ---- the download target: never picked for you ----------------------------------------------
    //
    // The three strings below are the adapters on the engineering PC this tool runs on AS
    // `openness-cli download-plan` RENDERS THEM. One of them is a PLCSIM virtual adapter, which is
    // exactly why none of the matching here is allowed to be helpful: a substring or a case-folded
    // match could send a real download to a simulator, or to a VPN TAP adapter, without saying so.
    //
    // CORRECTION, 2026-08-12: the trailing "#N" is NOT part of ConfigurationPcInterface.Name — it is
    // the separate Number property, which download-plan renders into one display string. These
    // constants therefore keep their #N as PART OF THE NAME on purpose, which makes them the fixture
    // for the "a name legitimately containing a '#' is not mangled" rule. The real shape — a shared
    // Name plus distinct Numbers — is SameNamedAdapterPair() below.

    private const string HyperV = "Microsoft Hyper-V Network Adapter #2";
    private const string Plcsim = "Siemens PLCSIM Virtual Ethernet Adapter #1";
    private const string Tap = "TAP-Windows Adapter V9 #2";

    /// <summary>The Name the two Hyper-V adapters actually share, with the #N stripped off.</summary>
    private const string HyperVName = "Microsoft Hyper-V Network Adapter";

    /// <summary>
    /// The real shape: three PC interfaces under one mode, each with a single target interface named
    /// "1 X1", and — the finding that forced this whole change — NO configured addresses anywhere.
    /// </summary>
    private static IReadOnlyList<ConnectionTarget<string>> RealAdapterSet() => new[]
    {
        Target(HyperV, "1 X1", "node-hyperv"),
        Target(Plcsim, "1 X1", "node-plcsim"),
        Target(Tap, "1 X1", "node-tap"),
    };

    /// <summary>
    /// MEASURED 2026-08-12, and the reason --pc-interface had to grow a number: a second Hyper-V
    /// adapter appeared on the machine between sessions, and BOTH report the same <c>Name</c>. Only
    /// <c>Number</c> tells them apart, and they can reach different networks.
    /// </summary>
    private static IReadOnlyList<ConnectionTarget<string>> SameNamedAdapterPair() => new[]
    {
        Target(HyperVName, "1 X1", "node-hyperv-1", number: 1),
        Target(HyperVName, "1 X1", "node-hyperv-2", number: 2),
    };

    private static ConnectionTarget<string> Target(
        string pcInterface, string targetInterface, string node, int number = 1) =>
        new("PN/IE", pcInterface, number, targetInterface, Array.Empty<string>(), "StandInNode", node);

    [Fact]
    public void Target_WithSeveralPcInterfaces_AndNoneNamed_RefusesAndNamesThemAll()
    {
        var selection = ConnectionTargetSelector.Select(RealAdapterSet(), requestedPcInterface: null, requestedTargetInterface: null);

        Assert.True(selection.IsRefusal);
        var text = string.Join("\n", selection.RefusalLines);
        Assert.Contains("--pc-interface IS REQUIRED", text, StringComparison.Ordinal);
        Assert.Contains(HyperV, text, StringComparison.Ordinal);
        Assert.Contains(Plcsim, text, StringComparison.Ordinal);
        Assert.Contains(Tap, text, StringComparison.Ordinal);
    }

    [Fact]
    public void Target_WithAnUnmatchedName_RefusesAndNamesEveryCandidate()
    {
        var selection = ConnectionTargetSelector.Select(RealAdapterSet(), "Some Other Adapter", null);

        Assert.True(selection.IsRefusal);
        var text = string.Join("\n", selection.RefusalLines);
        Assert.Contains("NO PC INTERFACE NAMED 'Some Other Adapter'", text, StringComparison.Ordinal);
        Assert.Contains(HyperV, text, StringComparison.Ordinal);
        Assert.Contains(Plcsim, text, StringComparison.Ordinal);
        Assert.Contains(Tap, text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The exactness test, run against all three real adapter names at once. A substring shared by
    /// every candidate, a prefix, a suffix and a case variant must each refuse — and in particular
    /// must never resolve to a DIFFERENT adapter from the one the operator typed.
    /// </summary>
    [Theory]
    [InlineData("Adapter")]                                     // substring of all three
    [InlineData("Microsoft")]                                   // prefix of one
    [InlineData("Network Adapter #2")]                          // suffix of one
    [InlineData("microsoft hyper-v network adapter #2")]        // case variant of the intended one
    [InlineData("MICROSOFT HYPER-V NETWORK ADAPTER #2")]
    [InlineData("Siemens PLCSIM Virtual Ethernet Adapter")]     // prefix of the SIMULATOR
    [InlineData(" Microsoft Hyper-V Network Adapter #2")]       // stray leading space
    [InlineData("Microsoft Hyper-V Network Adapter #2 ")]
    public void Target_SelectsByExactName_AndNeverSilentlyResolvesANearMiss(string requested)
    {
        var selection = ConnectionTargetSelector.Select(RealAdapterSet(), requested, null);

        Assert.True(selection.IsRefusal);
        Assert.Null(selection.Chosen);
    }

    [Fact]
    public void Target_ACaseVariant_IsRefusedButTheRefusalSaysWhy()
    {
        var selection = ConnectionTargetSelector.Select(RealAdapterSet(), HyperV.ToLowerInvariant(), null);

        Assert.True(selection.IsRefusal);
        var text = string.Join("\n", selection.RefusalLines);
        Assert.Contains("Differs only in case from", text, StringComparison.Ordinal);
        Assert.Contains(HyperV, text, StringComparison.Ordinal);
    }

    [Fact]
    public void Target_TheExactName_SelectsThatAdapterAndNoOther()
    {
        var selection = ConnectionTargetSelector.Select(RealAdapterSet(), HyperV, null);

        var chosen = Assert.IsType<ConnectionTarget<string>>(selection.Chosen);
        Assert.Equal(HyperV, chosen.PcInterfaceName);
        Assert.Equal("node-hyperv", chosen.Node);
    }

    /// <summary>
    /// The sole-candidate case. Naming the only adapter there is would not be a choice, so it is not
    /// demanded — but nothing is being picked between, which is the property that matters.
    /// </summary>
    [Fact]
    public void Target_WithExactlyOnePcInterface_NeedsNoName()
    {
        var only = new[] { Target(HyperV, "1 X1", "node-hyperv") };

        var selection = ConnectionTargetSelector.Select(only, null, null);

        Assert.Equal("node-hyperv", Assert.IsType<ConnectionTarget<string>>(selection.Chosen).Node);
    }

    [Fact]
    public void Target_WithNoCandidatesAtAll_Refuses()
    {
        var selection = ConnectionTargetSelector.Select(Array.Empty<ConnectionTarget<string>>(), null, null);

        Assert.True(selection.IsRefusal);
        Assert.Contains("NO CONNECTION TARGET", string.Join("\n", selection.RefusalLines), StringComparison.Ordinal);
    }

    [Fact]
    public void Target_WithSeveralTargetInterfaces_RequiresTargetAndRefusesWithout()
    {
        var many = new[]
        {
            Target(HyperV, "1 X1", "node-x1"),
            Target(HyperV, "1 X2", "node-x2"),
        };

        var refused = ConnectionTargetSelector.Select(many, HyperV, null);
        Assert.True(refused.IsRefusal);
        Assert.Contains("--target IS REQUIRED", string.Join("\n", refused.RefusalLines), StringComparison.Ordinal);
        Assert.Contains("1 X2", string.Join("\n", refused.RefusalLines), StringComparison.Ordinal);

        Assert.Equal("node-x2", Assert.IsType<ConnectionTarget<string>>(ConnectionTargetSelector.Select(many, HyperV, "1 X2").Chosen).Node);

        // Exact here too: a target interface is no more guessable than an adapter.
        Assert.True(ConnectionTargetSelector.Select(many, HyperV, "1 x2").IsRefusal);
        Assert.True(ConnectionTargetSelector.Select(many, HyperV, "1 X").IsRefusal);
    }

    // ---- two adapters, one name: --pc-interface can say WHICH ----------------------------------
    //
    // MEASURED 2026-08-12. A second Hyper-V adapter appeared on the machine between sessions and
    // both report the SAME ConfigurationPcInterface.Name; the "#N" that download-plan prints is the
    // separate Number property. A name-only match therefore pooled two adapters' target interfaces
    // and refused with "--target IS REQUIRED ... has 2 target interfaces" — the right refusal for
    // the wrong reason, and with nothing the operator could type to resolve it.

    /// <summary>
    /// The API fact the whole change rests on, read from the installed V20 assembly rather than
    /// inferred from the rendered string: <c>Number</c> exists, it is an <c>Int32</c>, it is separate
    /// from <c>Name</c>, and it is read-only. A Siemens change to any of that fails here.
    /// </summary>
    [Fact]
    public void ConfigurationPcInterface_ExposesNumber_AsAnInt32SeparateFromName()
    {
        var pcInterface = typeof(Siemens.Engineering.Connection.ConfigurationPcInterface);

        var number = pcInterface.GetProperty(
            "Number", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Assert.NotNull(number);
        Assert.Equal(typeof(int), number!.PropertyType);
        Assert.True(number.CanRead);
        Assert.False(number.CanWrite);

        // And Name really is only the name — the "#N" is not in it, which is why matching on Name
        // alone could not distinguish the two adapters.
        var name = pcInterface.GetProperty(
            "Name", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.NotNull(name);
        Assert.Equal(typeof(string), name!.PropertyType);
    }

    /// <summary>
    /// THE BUG. The bare shared name matches both adapters, and that is still a refusal — but the
    /// refusal now prints the numbers, so it says exactly what to pass instead of repeating the
    /// string that was just refused.
    /// </summary>
    [Fact]
    public void PcInterface_TwoAdaptersSharingAName_RefuseTheBareName_AndPrintBothNumbers()
    {
        var selection = ConnectionTargetSelector.Select(SameNamedAdapterPair(), HyperVName, null);

        Assert.True(selection.IsRefusal);
        Assert.Null(selection.Chosen);

        var text = string.Join("\n", selection.RefusalLines);
        Assert.Contains("AMBIGUOUS", text, StringComparison.Ordinal);
        Assert.Contains($"\"{HyperVName} #1\"", text, StringComparison.Ordinal);
        Assert.Contains($"\"{HyperVName} #2\"", text, StringComparison.Ordinal);

        // NOT the old message, which blamed the target interfaces and named no way out.
        Assert.DoesNotContain("--target IS REQUIRED", text, StringComparison.Ordinal);
    }

    [Fact]
    public void PcInterface_TheNumberedForm_ResolvesToExactlyThatAdapter()
    {
        var chosen = Assert.IsType<ConnectionTarget<string>>(
            ConnectionTargetSelector.Select(SameNamedAdapterPair(), HyperVName + " #2", null).Chosen);

        Assert.Equal("node-hyperv-2", chosen.Node);
        Assert.Equal(2, chosen.PcInterfaceNumber);
        Assert.Equal(HyperVName, chosen.PcInterfaceName);

        // And the other one is reachable the same way — the number selects, it does not merely
        // disambiguate towards whichever was enumerated first.
        Assert.Equal(
            "node-hyperv-1",
            Assert.IsType<ConnectionTarget<string>>(
                ConnectionTargetSelector.Select(SameNamedAdapterPair(), HyperVName + " #1", null).Chosen).Node);
    }

    /// <summary>
    /// A number naming nothing is a HARD REFUSAL. Falling back to the name would silently ignore the
    /// one thing the operator said, and would re-pool the two adapters the number exists to separate.
    /// </summary>
    [Fact]
    public void PcInterface_ANumberThatNamesNoAdapter_IsRefused_NeverFallenBackToNameOnly()
    {
        var selection = ConnectionTargetSelector.Select(SameNamedAdapterPair(), HyperVName + " #9", null);

        Assert.True(selection.IsRefusal);
        Assert.Null(selection.Chosen);

        var text = string.Join("\n", selection.RefusalLines);
        Assert.Contains("NO PC INTERFACE", text, StringComparison.Ordinal);
        Assert.Contains("#9", text, StringComparison.Ordinal);
        Assert.Contains($"\"{HyperVName} #1\"", text, StringComparison.Ordinal);
        Assert.Contains($"\"{HyperVName} #2\"", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The parse rule, stated as a test: the value is tried WHOLE as a Name first, so a name that
    /// legitimately ends in " #2" resolves on its own name and is never split. RealAdapterSet's
    /// fixture names carry their "#N" inside the Name for exactly this reason.
    /// </summary>
    [Fact]
    public void PcInterface_ANameThatLegitimatelyContainsAHash_IsNotMangled()
    {
        var chosen = Assert.IsType<ConnectionTarget<string>>(
            ConnectionTargetSelector.Select(RealAdapterSet(), HyperV, null).Chosen);

        Assert.Equal(HyperV, chosen.PcInterfaceName);
        Assert.Equal("node-hyperv", chosen.Node);

        // And when such a name is itself duplicated, the number is still reachable — the LAST " #<n>"
        // is the one read, so "<name ending in #2> #7" means name "...#2", number 7.
        var awkward = new[]
        {
            Target(HyperV, "1 X1", "node-a", number: 7),
            Target(HyperV, "1 X1", "node-b", number: 8),
        };

        Assert.True(ConnectionTargetSelector.Select(awkward, HyperV, null).IsRefusal);
        Assert.Equal(
            "node-b",
            Assert.IsType<ConnectionTarget<string>>(
                ConnectionTargetSelector.Select(awkward, HyperV + " #8", null).Chosen).Node);
    }

    [Theory]
    // Only a trailing ' #<digits>' at the very end, with at least one character before the space.
    [InlineData("Adapter #2", true, "Adapter", 2)]
    [InlineData("Adapter #0", true, "Adapter", 0)]
    [InlineData("Adapter #10", true, "Adapter", 10)]
    [InlineData("Adapter #2 #3", true, "Adapter #2", 3)]
    [InlineData("Adapter", false, null, 0)]
    [InlineData("Adapter #", false, null, 0)]
    [InlineData("Adapter #2x", false, null, 0)]
    [InlineData("Adapter #2 ", false, null, 0)]
    [InlineData("Adapter#2", false, null, 0)]          // no space: '#' is just a character in a name
    [InlineData("Adapter #-1", false, null, 0)]
    [InlineData("Adapter # 2", false, null, 0)]
    [InlineData(" #2", false, null, 0)]                // nothing before the separator names no adapter
    [InlineData("#2", false, null, 0)]
    [InlineData("", false, null, 0)]
    public void PcInterface_TheTrailingNumberParseRule(string requested, bool split, string? name, int number)
    {
        Assert.Equal(split, ConnectionTargetSelector.TrySplitTrailingNumber(requested, out var actualName, out var actualNumber));

        if (split)
        {
            Assert.Equal(name, actualName);
            Assert.Equal(number, actualNumber);
        }
        else
        {
            // On a refusal to split the value is left whole, so the caller's name-only path sees it
            // unchanged rather than a truncated guess.
            Assert.Equal(requested, actualName);
        }
    }

    /// <summary>
    /// The null-request path counts adapters by (Name, Number) too. Counting by name alone made two
    /// same-named adapters look like one, so the "you must name it" refusal never fired and the run
    /// went on to pool their target interfaces.
    /// </summary>
    [Fact]
    public void PcInterface_WithNothingRequested_TwoSameNamedAdaptersStillRequireAName()
    {
        var selection = ConnectionTargetSelector.Select(SameNamedAdapterPair(), null, null);

        Assert.True(selection.IsRefusal);
        var text = string.Join("\n", selection.RefusalLines);
        Assert.Contains("--pc-interface IS REQUIRED", text, StringComparison.Ordinal);
        Assert.Contains("2 PC interfaces", text, StringComparison.Ordinal);
        Assert.Contains($"\"{HyperVName} #1\"", text, StringComparison.Ordinal);
        Assert.Contains($"\"{HyperVName} #2\"", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The suffix is ADDITIVE: everything that resolved before still resolves, unchanged. A single
    /// adapter takes its bare name, and takes no name at all.
    /// </summary>
    [Fact]
    public void PcInterface_TheSingleAdapterCase_StillWorksWithTheBareName()
    {
        var only = new[] { Target(HyperVName, "1 X1", "node-only", number: 5) };

        Assert.Equal("node-only", Assert.IsType<ConnectionTarget<string>>(
            ConnectionTargetSelector.Select(only, HyperVName, null).Chosen).Node);

        Assert.Equal("node-only", Assert.IsType<ConnectionTarget<string>>(
            ConnectionTargetSelector.Select(only, HyperVName + " #5", null).Chosen).Node);

        Assert.Equal("node-only", Assert.IsType<ConnectionTarget<string>>(
            ConnectionTargetSelector.Select(only, null, null).Chosen).Node);

        // Still exact, even with only one candidate: a wrong number is refused, not rounded off.
        Assert.True(ConnectionTargetSelector.Select(only, HyperVName + " #6", null).IsRefusal);
        Assert.True(ConnectionTargetSelector.Select(only, HyperVName.ToLowerInvariant() + " #5", null).IsRefusal);
    }

    /// <summary>
    /// The node that was resolved is the node that reaches <c>Download</c>. Asserted on a fake,
    /// because <c>DownloadProvider</c> and every <c>Siemens.Engineering.Connection</c> type are sealed
    /// with no public constructor and cannot exist outside a Portal session.
    /// </summary>
    [Fact]
    public void Dispatch_HandsTheResolvedNodeToTheDownloadCall()
    {
        using var log = new ProbeLog(new StringWriter(), filePath: null);
        ConnectionTarget<string>? received = null;

        var outcome = DownloadDispatch.Dispatch(
            RealAdapterSet(),
            HyperV,
            null,
            log,
            chosen =>
            {
                received = chosen;
                return new ProbeOutcome(ProbeExitCodes.Completed, "fake download");
            });

        Assert.Equal(ProbeExitCodes.Completed, outcome.ExitCode);
        Assert.NotNull(received);
        Assert.Equal("node-hyperv", received!.Node);
        Assert.Equal(HyperV, received.PcInterfaceName);

        // And the log says which node, by CLR type and by name, rather than just "chosen".
        var text = string.Join("\n", log.Lines);
        Assert.Contains("StandInNode", text, StringComparison.Ordinal);
        Assert.Contains(HyperV, text, StringComparison.Ordinal);
        Assert.Contains("Download(IConfiguration, pre, post, DownloadOptions)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Dispatch_OnARefusal_NeverReachesTheDownloadCall()
    {
        using var log = new ProbeLog(new StringWriter(), filePath: null);

        var outcome = DownloadDispatch.Dispatch<string>(
            RealAdapterSet(),
            requestedPcInterface: null,
            requestedTargetInterface: null,
            log,
            _ => throw new InvalidOperationException("Download was reached despite an ambiguous target."));

        Assert.Equal(ProbeExitCodes.NoDownloadTarget, outcome.ExitCode);

        var text = string.Join("\n", log.Lines);
        Assert.Contains("--pc-interface IS REQUIRED", text, StringComparison.Ordinal);
        Assert.Contains("Nothing was downloaded.", text, StringComparison.Ordinal);
        Assert.Contains(Plcsim, text, StringComparison.Ordinal);
    }

    [Fact]
    public void PcInterface_IsParsedVerbatim_IncludingSpacesAndTheHashSuffix()
    {
        var result = ProbeArgumentParser.Parse(
            new[] { ScratchProject, "--options", "Software", "--pc-interface", HyperV }, null, Path.GetTempPath());

        Assert.Equal(HyperV, Assert.IsType<ProbeParseResult.Success>(result).Arguments.PcInterface);
    }

    [Theory]
    [InlineData("")]
    public void PcInterface_RejectsAnEmptyName_RatherThanFallingBackToADefault(string raw)
    {
        var result = ProbeArgumentParser.Parse(
            new[] { ScratchProject, "--options", "Software", "--pc-interface", raw }, null, Path.GetTempPath());

        Assert.Contains("empty name", Assert.IsType<ProbeParseResult.Failure>(result).Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Route (c), the reason no <c>ApplyConfiguration</c> path was built: the node this tool selects
    /// really is acceptable to the four-argument overload. Read from the installed V20 assembly, so
    /// a Siemens change to either the interface list or the overload set fails here rather than at
    /// the controller.
    /// </summary>
    [Fact]
    public void TheSelectedNodeType_IsAcceptableToTheFourArgumentDownloadOverload()
    {
        var configuration = typeof(Siemens.Engineering.Connection.IConfiguration);
        var targetInterface = typeof(Siemens.Engineering.Connection.ConfigurationTargetInterface);

        Assert.True(
            configuration.IsAssignableFrom(targetInterface),
            "ConfigurationTargetInterface no longer implements IConfiguration; the selection route is gone.");

        // ConfigurationPcInterface deliberately does NOT — which is why the walk descends one level
        // past the adapter rather than passing the adapter itself.
        Assert.False(configuration.IsAssignableFrom(typeof(Siemens.Engineering.Connection.ConfigurationPcInterface)));

        var overload = typeof(Siemens.Engineering.Download.DownloadProvider)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == "Download")
            .Single(m => m.GetParameters().Length == 4);

        Assert.Equal(configuration, overload.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(Siemens.Engineering.Download.DownloadOptions), overload.GetParameters()[3].ParameterType);
    }

    /// <summary>
    /// The guarantee the probe was built with, restated as a test now that a target CAN be selected:
    /// selecting an existing node is a READ. Nothing in this binary calls <c>ApplyConfiguration</c>
    /// (which would write the choice into the project — the mutation an address-based route would
    /// have required) or <c>GetAccessibleDevices</c> (which would scan the network). Asserted over
    /// the compiled IL of every method, the same technique that keeps `openness-cli` provably unable
    /// to download, so a call added inside an untested branch still fails here.
    ///
    /// The fallback the task pre-authorised was therefore NOT built: it was only ever needed if the
    /// four-argument overload could not take the target interface, and it can.
    /// </summary>
    [Theory]
    [InlineData("ApplyConfiguration")]
    [InlineData("GetAccessibleDevices")]
    public void TheProbe_NeverCallsAProjectMutatingOrScanningConnectionMethod(string forbidden)
    {
        var hits = FindConnectionCalls(forbidden);

        Assert.True(hits.Count == 0, $"download-probe calls {forbidden} from: {string.Join(", ", hits)}");
    }

    /// <summary>
    /// The negative control for the test above, and the reason it means anything. A scanner that
    /// silently matches nothing produces exactly the reassuring result the assertion is looking for —
    /// so the same walk, over the same assembly, with the same namespace filter, is required to FIND
    /// a call that provably exists: the probe reads <c>ConfigurationPcInterface.TargetInterfaces</c>
    /// while enumerating candidates.
    /// </summary>
    [Fact]
    public void TheIlScanner_ReallyDetectsAConnectionCallThatIsThere()
    {
        var hits = FindConnectionCalls("get_TargetInterfaces");

        Assert.True(
            hits.Count > 0,
            "The IL scan found no call to ConfigurationPcInterface.TargetInterfaces, which the probe " +
            "certainly makes. The scanner is broken, so its zero results prove nothing.");
    }

    private static IReadOnlyList<string> FindConnectionCalls(string memberName)
    {
        var assembly = typeof(ProbeProgram).Assembly;
        var hits = new List<string>();

        foreach (var type in assembly.GetTypes())
        {
            foreach (var method in type.GetMethods(
                         BindingFlags.Public | BindingFlags.NonPublic |
                         BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                byte[]? body;
                try
                {
                    body = method.GetMethodBody()?.GetILAsByteArray();
                }
                catch (Exception)
                {
                    // Abstract, extern and compiler-generated members have no readable body.
                    continue;
                }

                if (body is null)
                {
                    continue;
                }

                if (ReferencesConnectionMember(body, method, memberName))
                {
                    hits.Add($"{type.FullName}.{method.Name}");
                }
            }
        }

        return hits;
    }

    /// <summary>
    /// Deliberately coarse, exactly as <c>DownloadPlanTests</c>' equivalent: every 4-byte window is
    /// offered to the metadata resolver, which over-reports candidate tokens and under-reports
    /// nothing. Against an assertion of ZERO, over-reporting is the safe direction — a false positive
    /// fails a test and gets read; a false negative would let the mutation through unnoticed.
    /// </summary>
    private static bool ReferencesConnectionMember(byte[] il, MethodInfo owner, string memberName)
    {
        var module = owner.Module;
        var genericTypeArgs = owner.DeclaringType is { IsGenericTypeDefinition: true } declaring
            ? declaring.GetGenericArguments()
            : Type.EmptyTypes;
        var genericMethodArgs = owner.IsGenericMethodDefinition ? owner.GetGenericArguments() : Type.EmptyTypes;

        for (var i = 0; i + 4 <= il.Length; i++)
        {
            MethodBase? resolved;
            try
            {
                resolved = module.ResolveMethod(BitConverter.ToInt32(il, i), genericTypeArgs, genericMethodArgs);
            }
            catch (Exception)
            {
                continue;
            }

            if (resolved?.Name == memberName &&
                (resolved.DeclaringType?.Namespace ?? string.Empty)
                    .StartsWith("Siemens.Engineering.Connection", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    // ---- helpers ---------------------------------------------------------------------------------

    private static RaisedConfigurationView ViewFor(string typeName, string message, IConfigurationSelection? selection) =>
        new(
            runtimeTypeName: "Siemens.Engineering.Download.Configurations." + typeName,
            typeChain: new[] { typeName, "DownloadSelectionConfiguration", "DownloadConfiguration" },
            message: message,
            messageReadError: null,
            familyDescription: "DownloadSelectionConfiguration",
            selection: selection,
            properties: Array.Empty<ConfigurationPropertyView>());

    /// <summary>
    /// Runs the program with a stub session and returns the log AS IT STOOD WHEN THE SESSION WAS
    /// ENTERED. That is the assertion: the header and the consequence warning are written before
    /// anything is contacted, not afterwards.
    /// </summary>
    private static IReadOnlyList<string> RunWithStubSession(DownloadOptionChoice options)
    {
        var logDir = NewTempDir();
        IReadOnlyList<string> captured = Array.Empty<string>();
        using var repo = ProbeFenceRepo.Permitting();

        ProbeProgram.Run(
            new[] { repo.ProjectPath, "--options", options.ToString(), "--log-dir", logDir },
            new StringWriter(),
            new StringWriter(),
            (_, log) =>
            {
                captured = log.Lines.ToArray();
                return new ProbeOutcome(ProbeExitCodes.Completed, "stub");
            },
            repo.BinaryDirectory);

        return captured;
    }

    private static string NewTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "download-probe-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// Every configuration in the installed V20 assembly that exposes a <c>CurrentSelection</c> enum,
    /// with its selection names. Read from Siemens' own metadata rather than transcribed.
    /// </summary>
    private static IReadOnlyList<(string TypeName, IReadOnlyList<string> Selections)> RealSelectionConfigurations()
    {
        var assembly = typeof(Siemens.Engineering.Download.Configurations.DownloadConfiguration).Assembly;

        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t is not null).Select(t => t!).ToArray();
        }

        // Inspected one type at a time, each guarded. Some types in this assembly reference
        // Siemens.Engineering.Contract, which is not beside the test binary, and merely READING
        // their Namespace throws FileNotFoundException. Those are not download configurations, so
        // skipping them loses nothing - and the caller's ">= 20 examined" assertion is what stops a
        // swallowed exception turning this into a vacuous pass.
        var found = new List<(string TypeName, IReadOnlyList<string> Selections)>();
        foreach (var type in types)
        {
            try
            {
                if (type.Namespace != "Siemens.Engineering.Download.Configurations" || type.IsEnum || type.IsAbstract)
                {
                    continue;
                }

                var property = type.GetProperty("CurrentSelection", BindingFlags.Public | BindingFlags.Instance);
                if (property is null || !property.PropertyType.IsEnum)
                {
                    continue;
                }

                found.Add((type.Name, Enum.GetNames(property.PropertyType)));
            }
            catch (FileNotFoundException)
            {
                // A type whose dependencies are not present. Not a download configuration.
            }
            catch (TypeLoadException)
            {
            }
        }

        return found;
    }

    private sealed class FakeSelection : IConfigurationSelection
    {
        private readonly List<string> _applied = new();
        private string _current;

        internal FakeSelection(string enumTypeName, IReadOnlyList<string> available, string current = "(unset)")
        {
            EnumTypeName = enumTypeName;
            AvailableSelections = available;
            _current = current;
        }

        /// <summary>Every selection name the recorder ASKED FOR, accepted or not.</summary>
        internal IReadOnlyList<string> Applied => _applied;

        internal string? ReadBackOverride { get; set; }

        /// <summary>Set to make the primary (property-set) route refuse, as the live run's did.</summary>
        internal Exception? PrimaryRouteError { get; set; }

        /// <summary>Set to make the fallback route refuse too, i.e. the selection is simply not accepted.</summary>
        internal Exception? FallbackRouteError { get; set; }

        public string EnumTypeName { get; }

        public IReadOnlyList<string> AvailableSelections { get; }

        public string? DefaultValuedSelection { get; set; }

        public string ReadCurrentSelection() => _current;

        public SelectionApplyResult Apply(string selectionName)
        {
            _applied.Add(selectionName);
            var attempts = new List<SelectionApplyAttempt>();

            if (PrimaryRouteError is null)
            {
                attempts.Add(new SelectionApplyAttempt("property set (fake)", true, null));
                _current = ReadBackOverride ?? selectionName;
                return new SelectionApplyResult(selectionName, attempts, _current);
            }

            attempts.Add(new SelectionApplyAttempt("property set (fake)", false, PrimaryRouteError));

            if (FallbackRouteError is null)
            {
                attempts.Add(new SelectionApplyAttempt("SetAttribute (fake)", true, null));
                _current = ReadBackOverride ?? selectionName;
                return new SelectionApplyResult(selectionName, attempts, _current);
            }

            attempts.Add(new SelectionApplyAttempt("SetAttribute (fake)", false, FallbackRouteError));
            return new SelectionApplyResult(selectionName, attempts, _current);
        }
    }

    /// <summary>
    /// A property whose setter throws, so a test can obtain a REAL
    /// <see cref="TargetInvocationException"/> from a real <c>PropertyInfo.SetValue</c> — the exact
    /// shape the live run produced — rather than a hand-constructed one that might not behave the
    /// same way.
    /// </summary>
    private sealed class ThrowingTarget
    {
        private readonly Exception _error;

        internal ThrowingTarget(Exception error) => _error = error;

        internal int Value
        {
            get => 0;
            set => throw _error;
        }
    }

    private static TargetInvocationException RealReflectionWrapper(Exception cause)
    {
        var property = typeof(ThrowingTarget).GetProperty(
            nameof(ThrowingTarget.Value), BindingFlags.NonPublic | BindingFlags.Instance)!;

        try
        {
            property.SetValue(new ThrowingTarget(cause), 1);
        }
        catch (TargetInvocationException ex)
        {
            return ex;
        }

        throw new InvalidOperationException("PropertyInfo.SetValue no longer wraps setter exceptions.");
    }
}
