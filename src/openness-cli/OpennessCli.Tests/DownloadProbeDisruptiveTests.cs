using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DownloadProbe;
using Xunit;

using ProbeProgram = DownloadProbe.Program;

namespace OpennessCli.Tests;

/// <summary>
/// `download-probe --disruptive` — the mode that lets a download actually COMPLETE, by answering the
/// selections that stop the CPU.
///
/// WHY IT EXISTS. Six live download attempts against the real device transferred NOTHING: five
/// aborted on a configuration this tool refuses on purpose, and the one that returned
/// <c>Success</c> reported "The software has not been loaded, because it is up-to-date". A full
/// download raises <c>StopModules</c>, and the API REJECTS <c>NoAction</c> on it — so the stop is a
/// precondition, not a prompt, and a full download is impossible unless <c>StopAll</c> is answered.
///
/// WHAT THESE TESTS HAVE TO ESTABLISH, because the tool cannot be run to find out:
///   1. Without the flag, the two destructive selections stay unreachable — the existing suite's
///      assertions are untouched and this file adds the negative controls beside the new behaviour.
///   2. With the flag, those two become reachable AND <c>ResetModule -> DeleteAll</c> /
///      <c>InitializeMemory -> AcceptAll</c> DO NOT. Asserted against the deny list itself, not only
///      through behaviour: "the guard shrank to a named smaller set" is the claim, and a behavioural
///      test alone cannot distinguish it from "the guard was emptied and nothing raised those two".
///   3. The flag is unreachable by accident.
///   4. A <c>Success</c> result that says "up-to-date" is reported as NOTHING TRANSFERRED. That is
///      the single most important assertion in this file: the previous run's green result was the
///      thing that most needed disbelieving.
/// </summary>
public class DownloadProbeDisruptiveTests
{
    // Invented paths only — CLAUDE.md, "Live runs": use anything, commit nothing.
    private const string ScratchProject = @"C:\work\Widget Line scratch.ap20";
    private const string RealProject = @"C:\work\Widget Line.ap20";

    // ---- (3) the flag is unreachable by accident -------------------------------------------------

    [Fact]
    public void Disruptive_IsOff_UnlessTheExactFlagIsGiven()
    {
        var argumentSets = new[]
        {
            new[] { ScratchProject, "--options", "Software" },
            new[] { ScratchProject, "--options", "Hardware", "--json" },
            new[] { ScratchProject, "--options", "SoftwareOnlyChanges", "--pc-interface", "Some Adapter" },
            new[] { ScratchProject, "--options", "Software", "--device", "PLC1", "--target", "1 X1" },
            new[] { ScratchProject, "--options", "Software", "--timeout-connect", "60", "--timeout-open", "60" },
        };

        foreach (var args in argumentSets)
        {
            var success = Assert.IsType<ProbeParseResult.Success>(
                ProbeArgumentParser.Parse(args, null, Path.GetTempPath()));

            Assert.False(success.Arguments.Disruptive, $"'{string.Join(" ", args)}' enabled the disruptive mode.");
            Assert.Equal(SelectionPolicyMode.Normal, success.Arguments.PolicyMode);
        }
    }

    /// <summary>
    /// A near miss must be a USAGE ERROR, never a silent enable and never a silent ignore. A silent
    /// ignore is the worse of the two failures here: the operator believes the CPU will be restarted
    /// and it will not be.
    /// </summary>
    [Theory]
    [InlineData("--disrupt")]
    [InlineData("--disruptive=true")]
    [InlineData("--Disruptive")]
    [InlineData("--DISRUPTIVE")]
    [InlineData("--disruptives")]
    [InlineData("-disruptive")]
    public void Disruptive_NearMissSpellings_AreUsageErrors(string spelling)
    {
        var result = ProbeArgumentParser.Parse(
            new[] { ScratchProject, "--options", "Software", spelling }, null, Path.GetTempPath());

        if (result is ProbeParseResult.Success success)
        {
            Assert.False(success.Arguments.Disruptive, $"'{spelling}' silently enabled the disruptive mode.");
            Assert.Fail($"'{spelling}' was accepted as an argument instead of being rejected.");
        }

        Assert.IsType<ProbeParseResult.Failure>(result);
    }

    [Fact]
    public void Disruptive_TheExactFlag_TurnsItOn_AndSelectsTheDisruptivePolicy()
    {
        var success = Assert.IsType<ProbeParseResult.Success>(ProbeArgumentParser.Parse(
            new[] { ScratchProject, "--options", "Software", "--disruptive" }, null, Path.GetTempPath()));

        Assert.True(success.Arguments.Disruptive);
        Assert.Equal(SelectionPolicyMode.Disruptive, success.Arguments.PolicyMode);
    }

    /// <summary>
    /// Every default in the program picks the refusing policy. A recorder or a decision built without
    /// stating a mode must never acquire the disruptive one by omission.
    /// </summary>
    [Fact]
    public void EveryDefault_IsTheNormalPolicy()
    {
        Assert.Equal(SelectionPolicyMode.Normal, default(SelectionPolicyMode));

        // Decide's and IsDenied's optional parameters.
        Assert.Null(NoActionFirstPolicy.Decide("StopModules", new[] { "StopAll" }).Selection);
        Assert.True(NoActionFirstPolicy.IsDenied("StopModules", "StopAll"));

        // And a recorder built the two-argument way applies the refusing policy.
        using var log = new ProbeLog(new StringWriter(), filePath: null);
        var recorder = new ConfigurationRecorder(log, "PRE");
        var selection = new StubSelection("StopModulesSelections", new[] { "StopAll" });

        recorder.Record(ViewFor("StopModules", "The modules are stopped.", selection));

        Assert.Empty(selection.Applied);
    }

    /// <summary>
    /// 🔴 <b>THE INVERSE OF THE TEST THAT USED TO BE HERE, AND IT IS DELIBERATELY NOT DELETED.</b>
    ///
    /// <para>This asserted that <c>--disruptive</c> could not skip the device-download fence, using a
    /// project that EXISTS beside the allowlisted one — the mistyped-path case. The fence was removed
    /// on 2026-08-28 (ADR-0013) on the owner's instruction, so that guarantee is gone, and a deleted
    /// test would leave nothing recording that it went. <b>This one now proves the opposite holds:</b>
    /// a project nobody named reaches the session.</para>
    ///
    /// <para>The stub is the instrument — it records the call rather than throwing, so "Portal was
    /// reached" is asserted positively instead of being inferred from an absence. If a fence is ever
    /// reinstated, this test fails and names the ADR, which is the outcome worth having.</para>
    /// </summary>
    [Fact]
    public void AProjectNamedInNoAllowlist_NowReachesTheSession_BecauseTheFenceWasRemoved()
    {
        var logDir = NewTempDir();
        using var repo = ProbeProjectRepo.Create();

        var neighbour = Path.Combine(Path.GetDirectoryName(repo.ProjectPath)!, "NamedByNobody.ap20");
        File.WriteAllText(neighbour, "(a project no allowlist ever mentioned)");

        var reached = false;

        ProbeProgram.Run(
            new[] { neighbour, "--options", "Software", "--disruptive", "--log-dir", logDir },
            new StringWriter(),
            new StringWriter(),
            (_, _) =>
            {
                reached = true;
                return new ProbeOutcome(ProbeExitCodes.Completed, "stub — the session is not the subject of this test");
            },
            repo.BinaryDirectory);

        Assert.True(reached, "The session was never reached, which would mean something still fences the project.");

        // And the disclosure that replaced the refusal is in the artifact, naming what was opened.
        var log = string.Concat(Directory.GetFiles(logDir).Select(File.ReadAllText));
        Assert.Contains("NO PROJECT FENCE", log, StringComparison.Ordinal);
        Assert.Contains("NamedByNobody.ap20", log, StringComparison.Ordinal);
    }

    // ---- (1) without the flag, nothing changed ---------------------------------------------------

    [Theory]
    [InlineData("StopModules", "StopAll")]
    [InlineData("DataBlockReinitialization", "StopPlcAndReinitialize")]
    public void WithoutDisruptive_TheDestructiveSelections_AreStillUnreachable(string type, string selection)
    {
        Assert.True(NoActionFirstPolicy.IsDenied(type, selection));
        Assert.True(NoActionFirstPolicy.IsDenied(type, selection, SelectionPolicyMode.Normal));

        // Sole option: left unhandled. With NoAction beside it: NoAction. Neither picks the selection.
        Assert.Null(NoActionFirstPolicy.Decide(type, new[] { selection }, SelectionPolicyMode.Normal).Selection);
        Assert.Equal(
            "NoAction",
            NoActionFirstPolicy.Decide(type, new[] { "NoAction", selection }, SelectionPolicyMode.Normal).Selection);

        Assert.False(NoActionFirstPolicy.IsDisruptiveAllowance(type, selection, SelectionPolicyMode.Normal));
    }

    [Fact]
    public void WithoutDisruptive_StartModules_IsAnsweredNoAction_AndTheCpuIsNotStarted()
    {
        var decision = NoActionFirstPolicy.Decide(
            "StartModules", new[] { "NoAction", NoActionFirstPolicy.StartModulesSelection }, SelectionPolicyMode.Normal);

        Assert.Equal("NoAction", decision.Selection);
        Assert.Equal(ConfigurationDecisionKind.AnsweredNoAction, decision.Kind);
    }

    // ---- (2) THE HALF THAT MATTERS: the deny list SHRINKS, it is not emptied ----------------------

    /// <summary>
    /// The assertion the task turns on, made against the LIST rather than against behaviour. A purely
    /// behavioural test ("DeleteAll is still not chosen") passes just as happily against an emptied
    /// deny list, because nothing in a unit test raises a ResetModule configuration.
    /// </summary>
    [Fact]
    public void Disruptive_ShrinksTheDenyList_ToANamedSmallerSet_AndNeverEmptiesIt()
    {
        var normal = NoActionFirstPolicy.DeniedSelectionsFor(SelectionPolicyMode.Normal);
        var disruptive = NoActionFirstPolicy.DeniedSelectionsFor(SelectionPolicyMode.Disruptive);

        Assert.Same(NoActionFirstPolicy.DeniedSelections, normal);
        Assert.Same(NoActionFirstPolicy.DisruptiveDeniedSelections, disruptive);

        // NOT EMPTY.
        Assert.NotEmpty(disruptive);

        // SMALLER.
        Assert.True(disruptive.Count < normal.Count, "The disruptive deny list is not smaller than the normal one.");

        // A SUBSET — every survivor was already denied, so nothing new was invented here either.
        Assert.All(disruptive, entry => Assert.Contains(entry, normal));

        // AND EXACTLY THESE TWO, BY NAME.
        Assert.Equal(
            new[] { ("ResetModule", "DeleteAll"), ("InitializeMemory", "AcceptAll") },
            disruptive.ToArray());

        // Exactly two entries left the list, and they are these two by name — no third entry quietly
        // dropped out. (The allowance list is LONGER than this: an allowance need not have been on the
        // deny list at all — OverwriteSystemData never was — so the two sets are checked separately
        // and never derived from one another.)
        var removed = normal.Where(n => !disruptive.Contains(n)).ToList();
        Assert.Equal(
            new[] { ("StopModules", "StopAll"), ("DataBlockReinitialization", "StopPlcAndReinitialize") },
            removed.ToArray());
    }

    [Theory]
    [InlineData("ResetModule", "DeleteAll")]
    [InlineData("InitializeMemory", "AcceptAll")]
    public void Disruptive_StillRefuses_WhatDestroysBeyondWhatTheOptionEntails(string type, string selection)
    {
        Assert.True(NoActionFirstPolicy.IsDenied(type, selection, SelectionPolicyMode.Disruptive));
        Assert.False(NoActionFirstPolicy.IsDisruptiveAllowance(type, selection, SelectionPolicyMode.Disruptive));

        // Sole option: left UNHANDLED, so the download aborts exactly as it does today.
        var sole = NoActionFirstPolicy.Decide(type, new[] { selection }, SelectionPolicyMode.Disruptive);
        Assert.Null(sole.Selection);
        Assert.Equal(ConfigurationDecisionKind.LeftUnhandledOnlyDeniedSelections, sole.Kind);

        // Beside NoAction: NoAction, same as always.
        Assert.Equal(
            "NoAction",
            NoActionFirstPolicy.Decide(type, new[] { "NoAction", selection }, SelectionPolicyMode.Disruptive).Selection);
    }

    [Fact]
    public void Disruptive_TheAllowanceList_IsExactlyFourNamedPairs()
    {
        Assert.Equal(
            new[]
            {
                ("StopModules", "StopAll"),
                ("DataBlockReinitialization", "StopPlcAndReinitialize"),
                ("OverwriteSystemData", "Overwrite"),
                ("StartModules", "StartModule"),
            },
            NoActionFirstPolicy.DisruptiveAllowances.ToArray());

        // Nothing else is an allowance, in either direction: a selection name is not transferable
        // between configurations.
        Assert.False(NoActionFirstPolicy.IsDisruptiveAllowance("ResetModule", "StopAll", SelectionPolicyMode.Disruptive));
        Assert.False(NoActionFirstPolicy.IsDisruptiveAllowance("StopModules", "DeleteAll", SelectionPolicyMode.Disruptive));
        Assert.False(NoActionFirstPolicy.IsDisruptiveAllowance("StopHSystem", "StopHSystem", SelectionPolicyMode.Disruptive));
        Assert.False(NoActionFirstPolicy.IsDisruptiveAllowance("OverwriteOnMemoryCard", "Overwrite", SelectionPolicyMode.Disruptive));
        Assert.False(NoActionFirstPolicy.IsDisruptiveAllowance("OverwriteSystemData", "Load", SelectionPolicyMode.Disruptive));
    }

    /// <summary>
    /// The two lists are checked against each other exactly once, here, and the claim is NOT that they
    /// partition anything: an allowance need not have been denied (OverwriteSystemData never was, on
    /// either path). What must hold is that nothing is on BOTH lists at once under the disruptive mode
    /// — a pair that was permitted and denied simultaneously would make the guard meaningless.
    /// </summary>
    [Fact]
    public void Disruptive_NoPairIsBothAllowedAndDenied()
    {
        foreach (var allowance in NoActionFirstPolicy.DisruptiveAllowances)
        {
            Assert.DoesNotContain(allowance, NoActionFirstPolicy.DisruptiveDeniedSelections);
            Assert.False(
                NoActionFirstPolicy.IsDenied(
                    allowance.ConfigurationType, allowance.Selection, SelectionPolicyMode.Disruptive),
                $"{allowance.ConfigurationType} -> {allowance.Selection} is allowed and denied at once.");
        }

        Assert.Equal(4, NoActionFirstPolicy.DisruptiveAllowances.Count);
        Assert.Equal(2, NoActionFirstPolicy.DisruptiveDeniedSelections.Count);
    }

    // ---- with the flag, the three become reachable — ahead of NoAction ---------------------------

    [Theory]
    [InlineData("StopModules", "StopAll")]
    [InlineData("DataBlockReinitialization", "StopPlcAndReinitialize")]
    public void Disruptive_AnswersTheEntailedSelection_AHEAD_OfNoAction(string type, string selection)
    {
        var decision = NoActionFirstPolicy.Decide(
            type, new[] { "NoAction", selection }, SelectionPolicyMode.Disruptive);

        // Ahead of NoAction is the point. Measured: NoAction cannot be applied to either of these, so
        // a mode that merely un-denied them would answer NoAction, be refused, and abort — i.e. it
        // would do nothing at all, while looking like it had been enabled.
        Assert.Equal(selection, decision.Selection);
        Assert.Equal(ConfigurationDecisionKind.AnsweredByDisruptiveAllowance, decision.Kind);
        Assert.True(decision.IsLoud);
        Assert.Contains("DISRUPTIVE MODE", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Disruptive_AnswersStopAll_EvenWhenItIsTheOnlySelectionOffered()
    {
        var decision = NoActionFirstPolicy.Decide("StopModules", new[] { "StopAll" }, SelectionPolicyMode.Disruptive);

        Assert.Equal("StopAll", decision.Selection);
        Assert.Equal(ConfigurationDecisionKind.AnsweredByDisruptiveAllowance, decision.Kind);
    }

    /// <summary>
    /// <c>StartModules</c> is answered ONLY in the disruptive mode — and in the normal mode the
    /// answer is <c>NoAction</c>, whose value is 0, so the configuration would read as answered while
    /// leaving the CPU stopped. That is the quiet failure this pair of assertions pins down.
    /// </summary>
    [Fact]
    public void StartModules_IsAnsweredWithStartModule_OnlyInDisruptiveMode()
    {
        var offered = new[] { "NoAction", NoActionFirstPolicy.StartModulesSelection };

        Assert.Equal("NoAction", NoActionFirstPolicy.Decide("StartModules", offered, SelectionPolicyMode.Normal).Selection);
        Assert.Equal("NoAction", NoActionFirstPolicy.Decide("StartModules", offered).Selection);

        var disruptive = NoActionFirstPolicy.Decide("StartModules", offered, SelectionPolicyMode.Disruptive);
        Assert.Equal("StartModule", disruptive.Selection);
        Assert.Equal(ConfigurationDecisionKind.AnsweredByDisruptiveAllowance, disruptive.Kind);
    }

    /// <summary>
    /// <c>OverwriteSystemData</c>, the third instance of the pattern: the enum DECLARES
    /// <c>NoAction</c>, the instance REJECTS it once the chosen option entails the overwrite, so the
    /// normal policy's answer aborts a hardware download. Measured on a live rig 2026-08-12 — the
    /// configuration was raised with <c>Overwrite</c> already selected and <c>NoAction</c> failed to
    /// apply by BOTH routes.
    ///
    /// Both halves are asserted together because the normal path must NOT be widened by this change:
    /// without <c>--disruptive</c> a hardware download still answers <c>NoAction</c> and still aborts.
    /// </summary>
    [Fact]
    public void OverwriteSystemData_IsAnsweredWithOverwrite_OnlyInDisruptiveMode()
    {
        var offered = new[] { "NoAction", "Overwrite" };

        // NORMAL — unchanged, and unchanged is the requirement.
        Assert.Equal("NoAction", NoActionFirstPolicy.Decide("OverwriteSystemData", offered, SelectionPolicyMode.Normal).Selection);
        Assert.Equal("NoAction", NoActionFirstPolicy.Decide("OverwriteSystemData", offered).Selection);
        Assert.False(NoActionFirstPolicy.IsDisruptiveAllowance("OverwriteSystemData", "Overwrite", SelectionPolicyMode.Normal));

        // DISRUPTIVE — chosen, and chosen AHEAD of NoAction, which is the only thing that helps: a
        // mode that merely stopped denying it would still answer NoAction, still be refused by the
        // API, and still abort.
        var disruptive = NoActionFirstPolicy.Decide("OverwriteSystemData", offered, SelectionPolicyMode.Disruptive);
        Assert.Equal("Overwrite", disruptive.Selection);
        Assert.Equal(ConfigurationDecisionKind.AnsweredByDisruptiveAllowance, disruptive.Kind);
        Assert.True(disruptive.IsLoud);
        Assert.Contains("DISRUPTIVE MODE", disruptive.Reason, StringComparison.Ordinal);
        Assert.True(NoActionFirstPolicy.IsDisruptiveAllowance("OverwriteSystemData", "Overwrite", SelectionPolicyMode.Disruptive));
    }

    /// <summary>
    /// And when it is the only selection offered, both modes still differ: disruptive answers it, and
    /// the normal mode takes it only under the LOUD "there was nothing else to take" arm — never
    /// quietly, and never as an allowance.
    /// </summary>
    [Fact]
    public void OverwriteSystemData_AsTheSoleSelection_IsAnsweredByTheAllowanceOnlyInDisruptiveMode()
    {
        var disruptive = NoActionFirstPolicy.Decide("OverwriteSystemData", new[] { "Overwrite" }, SelectionPolicyMode.Disruptive);
        Assert.Equal("Overwrite", disruptive.Selection);
        Assert.Equal(ConfigurationDecisionKind.AnsweredByDisruptiveAllowance, disruptive.Kind);

        var normal = NoActionFirstPolicy.Decide("OverwriteSystemData", new[] { "Overwrite" }, SelectionPolicyMode.Normal);
        Assert.NotEqual(ConfigurationDecisionKind.AnsweredByDisruptiveAllowance, normal.Kind);
        Assert.Equal(ConfigurationDecisionKind.AnsweredOnlySelectionAvailable, normal.Kind);
        Assert.True(normal.IsLoud);
    }

    [Fact]
    public void Recorder_InDisruptiveMode_AppliesOverwrite_AndSaysItCameFromTheAllowance()
    {
        using var log = new ProbeLog(new StringWriter(), filePath: null);
        var recorder = new ConfigurationRecorder(log, "PRE", SelectionPolicyMode.Disruptive);
        var selection = new StubSelection("OverwriteSystemDataSelections", new[] { "NoAction", "Overwrite" });

        var record = recorder.Record(
            ViewFor("OverwriteSystemData", "Delete and replace system data in target", selection));

        Assert.Equal(new[] { "Overwrite" }, selection.Applied);
        Assert.Equal("Overwrite", record.ChosenSelection);
        Assert.Equal(ConfigurationDecisionKind.AnsweredByDisruptiveAllowance, record.Decision);
        Assert.Equal(ConfigurationOutcomeKind.Answered, record.Outcome);
        Assert.Contains("PERMITTED ONLY BY --disruptive", string.Join("\n", log.Lines), StringComparison.Ordinal);
    }

    /// <summary>The negative control on the identical configuration: the normal path is untouched.</summary>
    [Fact]
    public void Recorder_WithoutDisruptive_AppliesNoActionToOverwriteSystemData()
    {
        using var log = new ProbeLog(new StringWriter(), filePath: null);
        var recorder = new ConfigurationRecorder(log, "PRE");
        var selection = new StubSelection("OverwriteSystemDataSelections", new[] { "NoAction", "Overwrite" });

        var record = recorder.Record(
            ViewFor("OverwriteSystemData", "Delete and replace system data in target", selection));

        Assert.Equal(new[] { "NoAction" }, selection.Applied);
        Assert.DoesNotContain("Overwrite", selection.Applied);
        Assert.Equal(ConfigurationDecisionKind.AnsweredNoAction, record.Decision);
    }

    /// <summary>
    /// The four allowance names, checked against the enums the installed V20 assembly actually
    /// declares rather than against four strings someone typed. A Siemens rename would otherwise
    /// leave the allowance matching nothing — which fails SAFE on StopModules (abort) and fails
    /// SILENTLY on StartModules (the CPU simply stays stopped), so it has to fail here instead.
    /// </summary>
    [Fact]
    public void TheAllowanceNames_ExistInTheRealV20SelectionEnums()
    {
        Assert.Contains(
            "StopAll",
            Enum.GetNames(typeof(Siemens.Engineering.Download.Configurations.StopModulesSelections)));
        Assert.Contains(
            "StopPlcAndReinitialize",
            Enum.GetNames(typeof(Siemens.Engineering.Download.Configurations.DataBlockReinitializationSelections)));
        Assert.Contains(
            "Overwrite",
            Enum.GetNames(typeof(Siemens.Engineering.Download.Configurations.OverwriteSystemDataSelections)));
        Assert.Contains(
            NoActionFirstPolicy.StartModulesSelection,
            Enum.GetNames(typeof(Siemens.Engineering.Download.Configurations.StartModulesSelections)));

        // OverwriteSystemDataSelections declares exactly two members, NoAction = 0 and Overwrite = 1 —
        // read off the installed V20 assembly rather than off a log transcription. The zero value is
        // again the decline, which is the value the API refused on the live rig.
        Assert.Equal(
            new[] { "NoAction", "Overwrite" },
            Enum.GetNames(typeof(Siemens.Engineering.Download.Configurations.OverwriteSystemDataSelections))
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray());
        Assert.Equal(0, (int)Siemens.Engineering.Download.Configurations.OverwriteSystemDataSelections.NoAction);
        Assert.Equal(1, (int)Siemens.Engineering.Download.Configurations.OverwriteSystemDataSelections.Overwrite);

        // And the reason StartModules must be answered explicitly rather than left alone: the enum's
        // ZERO value — what an untouched field reads as — is NoAction, which does not start anything.
        Assert.Equal(0, (int)Siemens.Engineering.Download.Configurations.StartModulesSelections.NoAction);
        Assert.Equal(
            2,
            Enum.GetNames(typeof(Siemens.Engineering.Download.Configurations.StartModulesSelections)).Length);
    }

    /// <summary>
    /// The disruptive twin of the existing whole-assembly sweep: run the policy over EVERY selection
    /// configuration V20 declares, in disruptive mode, and assert it never answers something that is
    /// still denied. If Siemens adds a configuration, this sees it.
    /// </summary>
    [Fact]
    public void Disruptive_AgainstEveryRealV20Configuration_NeverChoosesSomethingStillDenied()
    {
        var examined = 0;
        var answeredByAllowance = new List<string>();

        foreach (var (typeName, selections) in RealSelectionConfigurations())
        {
            var decision = NoActionFirstPolicy.Decide(typeName, selections, SelectionPolicyMode.Disruptive);
            examined++;

            if (decision.Selection is not { } chosen)
            {
                continue;
            }

            Assert.False(
                NoActionFirstPolicy.IsDenied(typeName, chosen, SelectionPolicyMode.Disruptive),
                $"{typeName} would be answered with the still-denied selection '{chosen}'.");
            Assert.Contains(chosen, selections);

            if (decision.Kind == ConfigurationDecisionKind.AnsweredByDisruptiveAllowance)
            {
                answeredByAllowance.Add($"{typeName} -> {chosen}");
            }
        }

        // Empty is not clean: a reflection walk matching nothing would pass vacuously.
        Assert.True(examined >= 20, $"Only {examined} selection configurations were examined.");

        // And the allowance fired on EXACTLY the four configurations it names — not on a fifth that
        // happens to share a selection name. `OverwriteOnMemoryCard` is the live control for that: it
        // is a real V20 configuration in this sweep and it is NOT answered by the allowance.
        Assert.Equal(
            new[]
            {
                "DataBlockReinitialization -> StopPlcAndReinitialize",
                "OverwriteSystemData -> Overwrite",
                "StartModules -> StartModule",
                "StopModules -> StopAll",
            },
            answeredByAllowance.OrderBy(a => a, StringComparer.Ordinal).ToArray());
    }

    // ---- the recorder: what actually gets written to the live configuration ----------------------

    [Fact]
    public void Recorder_InDisruptiveMode_AppliesStopAll_AndSaysItCameFromTheAllowance()
    {
        using var log = new ProbeLog(new StringWriter(), filePath: null);
        var recorder = new ConfigurationRecorder(log, "PRE", SelectionPolicyMode.Disruptive);
        var selection = new StubSelection("StopModulesSelections", new[] { "NoAction", "StopAll" });

        var record = recorder.Record(ViewFor("StopModules", "The modules are stopped for downloading.", selection));

        Assert.Equal(new[] { "StopAll" }, selection.Applied);
        Assert.Equal("StopAll", record.ChosenSelection);
        Assert.Equal(ConfigurationDecisionKind.AnsweredByDisruptiveAllowance, record.Decision);
        Assert.Equal(ConfigurationOutcomeKind.Answered, record.Outcome);

        var text = string.Join("\n", log.Lines);
        Assert.Contains("BY THE DISRUPTIVE ALLOWANCE, NOT BY THE NORMAL POLICY", text, StringComparison.Ordinal);
        Assert.Contains("PERMITTED ONLY BY --disruptive", text, StringComparison.Ordinal);
    }

    /// <summary>The negative control for the test above, on the identical configuration.</summary>
    [Fact]
    public void Recorder_WithoutDisruptive_AppliesNoActionToTheSameConfiguration()
    {
        using var log = new ProbeLog(new StringWriter(), filePath: null);
        var recorder = new ConfigurationRecorder(log, "PRE");
        var selection = new StubSelection("StopModulesSelections", new[] { "NoAction", "StopAll" });

        var record = recorder.Record(ViewFor("StopModules", "The modules are stopped for downloading.", selection));

        Assert.Equal(new[] { "NoAction" }, selection.Applied);
        Assert.DoesNotContain("StopAll", selection.Applied);
        Assert.Equal(ConfigurationDecisionKind.AnsweredNoAction, record.Decision);

        var text = string.Join("\n", log.Lines);
        Assert.Contains("by the NORMAL policy", text, StringComparison.Ordinal);
        Assert.DoesNotContain("DISRUPTIVE ALLOWANCE", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Recorder_InDisruptiveMode_WritesNothingForResetModule()
    {
        using var log = new ProbeLog(new StringWriter(), filePath: null);
        var recorder = new ConfigurationRecorder(log, "PRE", SelectionPolicyMode.Disruptive);
        var selection = new StubSelection("ResetModuleSelections", new[] { "DeleteAll" });

        var record = recorder.Record(ViewFor("ResetModule", "Reset module.", selection));

        Assert.Empty(selection.Applied);
        Assert.Null(record.ChosenSelection);
        Assert.Equal(ConfigurationOutcomeKind.RefusedByPolicy, record.Outcome);
        Assert.Contains("ON THE DENY LIST, NEVER CHOSEN", string.Join("\n", log.Lines), StringComparison.Ordinal);
    }

    [Fact]
    public void Recorder_InDisruptiveMode_AnswersStartModules_WithStartModule()
    {
        using var log = new ProbeLog(new StringWriter(), filePath: null);
        var recorder = new ConfigurationRecorder(log, "PRE", SelectionPolicyMode.Disruptive);
        var selection = new StubSelection("StartModulesSelections", new[] { "NoAction", "StartModule" });

        var record = recorder.Record(ViewFor("StartModules", "Start modules after downloading to device.", selection));

        Assert.Equal(new[] { "StartModule" }, selection.Applied);
        Assert.Equal("StartModule", record.ChosenSelection);
    }

    // ---- the banner ------------------------------------------------------------------------------

    [Fact]
    public void Banner_NamesBothSets_BeforeAnythingIsContacted()
    {
        var log = RunWithStubSession(disruptive: true);
        var text = string.Join("\n", log);

        Assert.Contains("--disruptive: THIS RUN IS ALLOWED TO STOP THE CPU", text, StringComparison.Ordinal);

        // Set 1: permitted here, denied normally. Every pair, by name.
        foreach (var (type, selection) in NoActionFirstPolicy.DisruptiveAllowances)
        {
            Assert.Contains($"{type} -> {selection}", text, StringComparison.Ordinal);
        }

        // Set 2: still denied. Every pair, by name.
        foreach (var (type, selection) in NoActionFirstPolicy.DisruptiveDeniedSelections)
        {
            Assert.Contains($"{type} -> {selection}", text, StringComparison.Ordinal);
        }

        // Spelled out as a literal as well as through the loop above: the loop passes whatever the
        // list happens to hold, so it cannot show that THIS allowance reached the banner.
        Assert.Contains("OverwriteSystemData -> Overwrite", text, StringComparison.Ordinal);

        Assert.Contains("STILL DENIED, EVEN HERE", text, StringComparison.Ordinal);
        Assert.Contains("SHRUNK, NOT EMPTIED", text, StringComparison.Ordinal);
        Assert.Contains("BE AT THE MACHINE", text, StringComparison.Ordinal);
        Assert.Contains("policy mode    : Disruptive", text, StringComparison.Ordinal);

        // And the prediction is REVERSED, so nobody reads an abort here as the success the normal
        // mode's prediction trains them to expect.
        Assert.Contains("EXPECTED TO PROCEED, not to abort", text, StringComparison.Ordinal);
        Assert.Contains("AN ABORT IS A FAILURE OF THE EXPERIMENT, NOT ITS RESULT", text, StringComparison.Ordinal);
        Assert.DoesNotContain("EXPECTED TO ABORT", text, StringComparison.Ordinal);
    }

    [Fact]
    public void WithoutDisruptive_NoBannerIsPrinted_AndTheFullDenyListStillIs()
    {
        var text = string.Join("\n", RunWithStubSession(disruptive: false));

        Assert.DoesNotContain("ALLOWED TO STOP THE CPU", text, StringComparison.Ordinal);
        Assert.DoesNotContain("SHRUNK, NOT EMPTIED", text, StringComparison.Ordinal);
        Assert.Contains("(--disruptive was NOT given)", text, StringComparison.Ordinal);

        // The new allowance is not advertised at all without the flag. (The other three cannot be
        // asserted the same way: two of them are printed here as DENIED entries, spelled identically.)
        Assert.DoesNotContain("OverwriteSystemData", text, StringComparison.Ordinal);

        foreach (var (type, selection) in NoActionFirstPolicy.DeniedSelections)
        {
            Assert.Contains($"{type} -> {selection}", text, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The usage text is built FROM the policy's own lists rather than retyped, so what an operator
    /// reads before running this cannot drift from what the tool will do. Asserted because the two
    /// lists are the entire safety argument for the flag.
    /// </summary>
    [Fact]
    public void TheUsageText_NamesBothSets_AndTheirRealContents()
    {
        var usage = ProbeArgumentParser.Usage;

        Assert.Contains("--disruptive", usage, StringComparison.Ordinal);
        foreach (var (type, selection) in NoActionFirstPolicy.DisruptiveAllowances)
        {
            Assert.Contains($"{type} -> {selection}", usage, StringComparison.Ordinal);
        }

        foreach (var (type, selection) in NoActionFirstPolicy.DisruptiveDeniedSelections)
        {
            Assert.Contains($"{type} -> {selection}", usage, StringComparison.Ordinal);
        }

        Assert.Contains("stay denied even here", usage, StringComparison.OrdinalIgnoreCase);
    }

    // ---- (4) WAS ANYTHING ACTUALLY TRANSFERRED — the most important test in this file -------------

    /// <summary>
    /// THE ONE THAT MATTERS. The previous "successful" live run returned <c>Success</c> with zero
    /// errors and carried nothing; the only trace was a sentence in a message. A tool that reported
    /// that as a success would have concluded the tooling can deliver a program when it had never
    /// delivered a byte.
    /// </summary>
    [Fact]
    public void ASuccessCarryingUpToDate_IsReportedAsNOTHINGTRANSFERRED()
    {
        var messages = new[]
        {
            Message("Success", "Download to device completed (errors: 0; warnings: 0)."),
            Message("Success", "The software has not been loaded, because it is up-to-date."),
        };

        var verdict = TransferVerdicts.Classify("Success", errorCount: 0, messages);

        Assert.Equal(TransferVerdictKind.NothingTransferred, verdict.Kind);
        Assert.False(verdict.SoftwareLoaded);
        Assert.Contains("NOTHING WAS TRANSFERRED", verdict.Headline, StringComparison.Ordinal);
        Assert.DoesNotContain("YES", verdict.Headline, StringComparison.Ordinal);

        // The evidence quotes the sentence verbatim, so the verdict can be checked rather than trusted.
        Assert.Contains(
            verdict.Evidence,
            line => line.IndexOf("The software has not been loaded, because it is up-to-date.", StringComparison.Ordinal) >= 0);
    }

    /// <summary>
    /// 🔴 *** THIS TEST ASSERTED THE DEFECT, AND ITS EXPECTATION IS INVERTED (2026-08-14). ***
    ///
    /// It was called <c>ASuccessWithoutUpToDate_IsReportedAsTRANSFERRED</c> and it required
    /// <c>SoftwareLoaded = true</c> for a result that <b>names nothing loaded</b> — a verdict inferred
    /// from the ABSENCE of an up-to-date phrase. `Ladder.Download`'s parser was written later the same
    /// day to make exactly that inference impossible ("absence of a denial is not evidence of an
    /// act"), and the probe now derives its verdict from it.
    ///
    /// *** A DEFECT WRITTEN DOWN AS AN EXPECTATION IS THE MOST DURABLE KIND: *** while this test
    /// stood, the correct behaviour was a failing build. It is kept, renamed and inverted rather than
    /// deleted, so the change of mind is visible in the file rather than only in the history.
    /// </summary>
    [Fact]
    public void ASuccessThatNamesNothingLoaded_IsUNDETERMINED_NotTransferred()
    {
        var messages = new[]
        {
            Message("Success", "Start downloading to device."),
            Message("Success", "Download to device completed (errors: 0; warnings: 0)."),
        };

        var verdict = TransferVerdicts.Classify("Success", errorCount: 0, messages);

        Assert.Equal(TransferVerdictKind.Undetermined, verdict.Kind);

        // Tri-state, and this is the value that matters: not true, and NOT false either — nobody knows.
        Assert.Null(verdict.SoftwareLoaded);

        Assert.DoesNotContain("THE SOFTWARE WAS LOADED", verdict.Headline, StringComparison.Ordinal);
        Assert.Contains("NAMES NOTHING LOADED", verdict.Headline, StringComparison.Ordinal);
        Assert.Contains(
            verdict.Evidence,
            line => line.IndexOf("THAT IS NOT A NEGATIVE, AND IT IS NOT A POSITIVE", StringComparison.Ordinal) >= 0);
    }

    /// <summary>
    /// The positive half, restated on POSITIVE evidence: a load message, by name. This is what a real
    /// transfer looks like, and it is the only thing that now yields "loaded".
    /// </summary>
    [Fact]
    public void AResultNamingALoadedObject_IsReportedAsTRANSFERRED()
    {
        var messages = new[]
        {
            Message("Success", "Start downloading to device."),
            Message("Success", "'DB_Data01' was loaded successfully."),
        };

        var verdict = TransferVerdicts.Classify("Success", errorCount: 0, messages);

        Assert.Equal(TransferVerdictKind.SoftwareLoaded, verdict.Kind);
        Assert.True(verdict.SoftwareLoaded);
        Assert.Contains("YES", verdict.Headline, StringComparison.Ordinal);
        Assert.Contains("REPORTED LOADED BY NAME", verdict.Headline, StringComparison.Ordinal);

        // The basis is now POSITIVE evidence, and the evidence block says so where it used to say the
        // opposite ("THE BASIS IS AN ABSENCE").
        Assert.Contains(
            verdict.Evidence,
            line => line.IndexOf("THE BASIS IS POSITIVE EVIDENCE", StringComparison.Ordinal) >= 0);
    }

    /// <summary>
    /// The phrase can arrive nested — <c>DownloadResultMessage.Messages</c> recurses — and a flat read
    /// of the top level would report the opposite answer.
    /// </summary>
    [Fact]
    public void AnUpToDatePhrase_IsFound_InANestedChildMessage()
    {
        var messages = new[]
        {
            Message(
                "Success",
                "Download to device completed.",
                Message("Success", "Software"),
                Message("Success", "The software has not been loaded, because it is up-to-date.")),
        };

        Assert.Equal(TransferVerdictKind.NothingTransferred, TransferVerdicts.Classify("Success", 0, messages).Kind);
    }

    /// <summary>
    /// The MEASURED up-to-date sentence, in the casings TIA has actually produced. Recognition is
    /// case-insensitive, and this is the wording that made a green run carry nothing.
    /// </summary>
    [Theory]
    [InlineData("The software has not been loaded, because it is up-to-date.")]
    [InlineData("THE SOFTWARE HAS NOT BEEN LOADED, BECAUSE IT IS UP-TO-DATE.")]
    public void TheMeasuredUpToDateSentence_IsCaught_InAnyCasing(string text)
    {
        var verdict = TransferVerdicts.Classify("Success", 0, new[] { Message("Success", text) });

        Assert.Equal(TransferVerdictKind.NothingTransferred, verdict.Kind);
        Assert.NotNull(verdict.MatchedPhrase);
    }

    /// <summary>
    /// *** AN EXPECTATION THAT CHANGED, AND THE ARGUMENT BEHIND IT THAT DID NOT. ***
    ///
    /// This wording ("the hardware configuration is up to date", unhyphenated, and not a statement
    /// that anything was withheld) used to be a third row of the theory above, asserting
    /// <c>NothingTransferred</c> — because the probe matched three LOOSE substrings and the stated
    /// reason was that matching wide is the safe direction: *"a false 'nothing transferred' costs a
    /// re-run, a false 'transferred' costs a wrong conclusion."*
    ///
    /// <b>That argument is preserved exactly where it matters and is no longer served by guessing.</b>
    /// The verdict is now keyed on the load manifest, so an unrecognised sentence can never produce a
    /// false "transferred" — it produces <c>Undetermined</c>, the conservative answer — AND the
    /// message is reported as unrecognised, which is how `Ladder.Download` is designed to go out of
    /// date rather than silently wrong. A guess would have hidden that signal.
    /// </summary>
    [Fact]
    public void AnUnrecognisedUpToDateWording_IsUndetermined_AndIsSurfaced()
    {
        var text = "The hardware configuration is up to date.";
        var verdict = TransferVerdicts.Classify("Success", 0, new[] { Message("Success", text) });

        // Never "transferred" — the direction the old wide matching existed to protect.
        Assert.NotEqual(TransferVerdictKind.SoftwareLoaded, verdict.Kind);
        Assert.Equal(TransferVerdictKind.Undetermined, verdict.Kind);
        Assert.Null(verdict.SoftwareLoaded);

        // And it is not silently swallowed: the parser reports the shape it did not recognise.
        var feedback = Ladder.Download.DownloadFeedbackParser.Parse(
            new Ladder.Download.DownloadResultSummary(
                "Success", 0, 0,
                new[] { new Ladder.Download.DownloadMessageNode(text, "Success", 0, 0, null, null) }));

        Assert.Equal(1, feedback.UnrecognisedMessageCount);
        Assert.Contains(feedback.UnrecognisedMessages, m => m.Text == text);
    }

    /// <summary>Empty is not clean — the project's own rule, applied to the transfer question.</summary>
    [Fact]
    public void AResultWithNoMessagesAtAll_IsUndetermined_NotAPass()
    {
        var verdict = TransferVerdicts.Classify("Success", errorCount: 0, Array.Empty<DownloadMessageNode>());

        Assert.Equal(TransferVerdictKind.Undetermined, verdict.Kind);
        Assert.Null(verdict.SoftwareLoaded);
        Assert.Contains("UNDETERMINED", verdict.Headline, StringComparison.Ordinal);
        Assert.Contains(verdict.Evidence, line => line.IndexOf("EMPTY IS NOT CLEAN", StringComparison.Ordinal) >= 0);
    }

    [Fact]
    public void AnAbortedDownload_HasNoTransferVerdict_RatherThanANegativeOne()
    {
        var verdict = TransferVerdicts.NoResult("it aborted");

        Assert.Equal(TransferVerdictKind.Undetermined, verdict.Kind);

        // Tri-state: "we do not know" must never collapse into "nothing was transferred".
        Assert.Null(verdict.SoftwareLoaded);
        Assert.NotEqual(false, verdict.SoftwareLoaded);
    }

    // ---- the resulting run state -----------------------------------------------------------------

    [Fact]
    public void RunState_WhenTheMessagesDoNotDiscloseIt_SaysSoExplicitly()
    {
        var lines = TransferVerdicts.DescribeRunState(new[]
        {
            Message("Success", "Download to device completed (errors: 0; warnings: 0)."),
        });

        var text = string.Join("\n", lines);
        Assert.Contains("NOT DISCLOSED", text, StringComparison.Ordinal);
        Assert.Contains("says NOTHING about", text, StringComparison.Ordinal);
    }

    [Fact]
    public void RunState_WhenTheMessagesMentionIt_QuotesThemAndCallsThemMentions()
    {
        var lines = TransferVerdicts.DescribeRunState(new[]
        {
            Message("Success", "The modules were stopped for the download."),
        });

        var text = string.Join("\n", lines);
        Assert.Contains("The modules were stopped for the download.", text, StringComparison.Ordinal);
        Assert.Contains("MENTION", text, StringComparison.Ordinal);
        Assert.Contains("NOT a reading of the CPU's", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The word matching is on boundaries. "during", "running" and "runtime" contain "run" and must
    /// not be reported as a disclosure about the CPU.
    /// </summary>
    [Fact]
    public void RunState_DoesNotReportADisclosure_FromWordsThatMerelyContainRunOrStop()
    {
        var lines = TransferVerdicts.DescribeRunState(new[]
        {
            Message("Success", "Consistency check during the runtime download of the HMI."),
        });

        Assert.Contains("NOT DISCLOSED", string.Join("\n", lines), StringComparison.Ordinal);
    }

    // ---- the CPU-may-be-left-in-STOP advisory ----------------------------------------------------

    [Fact]
    public void Disruptive_WhenStartModulesWasNeverRaised_SaysLoudlyThatTheCpuMayBeLeftInStop()
    {
        var text = string.Join("\n", RunStateAdvisory.Describe(
            SelectionPolicyMode.Disruptive,
            new[] { Recorded("StopModules", "StopAll", ConfigurationOutcomeKind.Answered) }));

        Assert.Contains("StartModules WAS NEVER RAISED", text, StringComparison.Ordinal);
        Assert.Contains("THE CPU MAY BE LEFT IN STOP AND A HUMAN MUST RESTART IT", text, StringComparison.Ordinal);
        Assert.Contains("StopModules -> StopAll WAS ANSWERED", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Disruptive_WhenStartModulesWasAnswered_SaysItWasAskedForNotMeasured()
    {
        var text = string.Join("\n", RunStateAdvisory.Describe(
            SelectionPolicyMode.Disruptive,
            new[]
            {
                Recorded("StopModules", "StopAll", ConfigurationOutcomeKind.Answered),
                Recorded("StartModules", "StartModule", ConfigurationOutcomeKind.Answered),
            }));

        Assert.Contains("TIA WAS ASKED to start", text, StringComparison.Ordinal);
        Assert.Contains("THAT IS A REQUEST, NOT A READING", text, StringComparison.Ordinal);
        Assert.DoesNotContain("MUST RESTART IT", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Disruptive_WhenStartModulesWasRaisedButNotAnswered_SaysTheCpuMayBeLeftInStop()
    {
        var text = string.Join("\n", RunStateAdvisory.Describe(
            SelectionPolicyMode.Disruptive,
            new[]
            {
                Recorded("StopModules", "StopAll", ConfigurationOutcomeKind.Answered),
                Recorded("StartModules", chosen: null, ConfigurationOutcomeKind.FailedToApply, attempted: "StartModule"),
            }));

        Assert.Contains("WAS NOT ANSWERED WITH 'StartModule'", text, StringComparison.Ordinal);
        Assert.Contains("THE CPU MAY BE LEFT IN STOP AND A", text, StringComparison.Ordinal);
    }

    [Fact]
    public void WithoutDisruptive_TheAdvisorySaysNothingCouldHaveStoppedTheCpu()
    {
        var text = string.Join("\n", RunStateAdvisory.Describe(
            SelectionPolicyMode.Normal,
            new[] { Recorded("StopModules", chosen: null, ConfigurationOutcomeKind.RefusedByPolicy) }));

        Assert.Contains("nothing in this run could have stopped the CPU", text, StringComparison.Ordinal);
        Assert.DoesNotContain("MAY BE LEFT IN STOP", text, StringComparison.Ordinal);
    }

    // ---- OnlineProvider: surveyed, reported, never used -------------------------------------------

    /// <summary>
    /// The survey runs against the installed V20 assembly and reports what <c>OnlineProvider</c>
    /// exposes. The finding it records — that <c>OnlineState</c> is a CONNECTION state and there is no
    /// CPU operating mode anywhere on the type — is asserted here so that a Siemens change which
    /// introduced one would break this test and get looked at.
    /// </summary>
    [Fact]
    public void OnlineSurvey_ReportsOnlineProvider_AndFindsNoCpuOperatingMode()
    {
        var text = string.Join("\n", OnlineModeSurvey.Describe(
            typeof(Siemens.Engineering.Download.DownloadProvider).Assembly));

        Assert.Contains("OnlineProvider", text, StringComparison.Ordinal);
        Assert.Contains("GoOnline", text, StringComparison.Ordinal);
        Assert.Contains("OnlineProvider OFFERS NO CPU OPERATING MODE", text, StringComparison.Ordinal);
        Assert.Contains("CONNECTION state", text, StringComparison.Ordinal);
        Assert.Contains("public ctors    : 0", text, StringComparison.Ordinal);

        // The survey must not silently describe nothing.
        Assert.DoesNotContain("NO TYPES FOUND", text, StringComparison.Ordinal);
        Assert.DoesNotContain("NOT FOUND in this assembly", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// "Reports but does not act", asserted rather than promised. Nothing in this binary calls any
    /// member declared in <c>Siemens.Engineering.Online</c> — so the survey cannot have quietly grown
    /// a <c>GoOnline()</c>, and no mode change was wired up while nobody was looking.
    /// </summary>
    [Fact]
    public void TheProbe_CallsNothingDeclaredInTheOnlineNamespace()
    {
        var hits = FindCalls("Siemens.Engineering.Online", memberName: null);

        Assert.True(hits.Count == 0, $"download-probe calls into Siemens.Engineering.Online from: {string.Join(", ", hits)}");
    }

    /// <summary>
    /// The negative control that gives the assertion above its meaning. A scanner matching nothing
    /// produces exactly the reassuring zero it is looking for, so the same walk must FIND the one
    /// call that provably exists: this binary's single <c>DownloadProvider.Download</c>.
    /// </summary>
    [Fact]
    public void TheIlScanner_ReallyFindsTheDownloadCallThatIsThere()
    {
        var hits = FindCalls("Siemens.Engineering.Download", "Download");

        Assert.True(
            hits.Count > 0,
            "The IL scan found no call to DownloadProvider.Download, which this binary certainly makes. " +
            "The scanner is broken, so its zero results elsewhere prove nothing.");
    }

    // ---- helpers ---------------------------------------------------------------------------------

    private static DownloadMessageNode Message(string state, string text, params DownloadMessageNode[] children) =>
        new(state, 0, 0, text, children);

    private static RecordedConfiguration Recorded(
        string typeName,
        string? chosen,
        ConfigurationOutcomeKind outcome,
        string? attempted = null) =>
        new(
            "PRE", 1, typeName, "message", Array.Empty<string>(), chosen,
            ConfigurationDecisionKind.AnsweredByDisruptiveAllowance, "reason", outcome,
            attempted ?? chosen, outcome == ConfigurationOutcomeKind.FailedToApply ? "the API refused it" : null);

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
    /// ENTERED — i.e. the banner is asserted to exist BEFORE anything is contacted, not afterwards.
    /// </summary>
    private static IReadOnlyList<string> RunWithStubSession(bool disruptive)
    {
        var logDir = NewTempDir();
        using var repo = ProbeProjectRepo.Create();
        var args = new List<string> { repo.ProjectPath, "--options", "Software", "--log-dir", logDir };
        if (disruptive)
        {
            args.Add("--disruptive");
        }

        IReadOnlyList<string> captured = Array.Empty<string>();
        ProbeProgram.Run(
            args,
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

        // One type at a time, each guarded: some types here reference Siemens.Engineering.Contract,
        // which is not beside the test binary, and merely reading their Namespace throws. The
        // ">= 20 examined" assertion at the call site is what stops that turning into a vacuous pass.
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
            }
            catch (TypeLoadException)
            {
            }
        }

        return found;
    }

    /// <summary>
    /// Walks the compiled IL of every method in the probe assembly for a call into
    /// <paramref name="namespacePrefix"/>, optionally narrowed to one member name. Deliberately
    /// coarse — every 4-byte window is offered to the metadata resolver — because against an
    /// assertion of ZERO, over-reporting is the safe direction: a false positive fails a test and
    /// gets read, a false negative lets the call through unnoticed.
    /// </summary>
    private static IReadOnlyList<string> FindCalls(string namespacePrefix, string? memberName)
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
                    continue;
                }

                if (body is not null && References(body, method, namespacePrefix, memberName))
                {
                    hits.Add($"{type.FullName}.{method.Name}");
                }
            }
        }

        return hits;
    }

    private static bool References(byte[] il, MethodInfo owner, string namespacePrefix, string? memberName)
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

            if (resolved is null)
            {
                continue;
            }

            string? declaringNamespace;
            try
            {
                declaringNamespace = resolved.DeclaringType?.Namespace;
            }
            catch (Exception)
            {
                continue;
            }

            if ((declaringNamespace ?? string.Empty).StartsWith(namespacePrefix, StringComparison.Ordinal) &&
                (memberName is null || resolved.Name == memberName))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// A stand-in for a live configuration's selection. Every concrete Siemens configuration type has
    /// an internal constructor and exists only during a download, so this is the only way the applied
    /// value can be asserted without running the tool at a controller.
    /// </summary>
    private sealed class StubSelection : IConfigurationSelection
    {
        private readonly List<string> _applied = new();
        private string _current = "(unset)";

        internal StubSelection(string enumTypeName, IReadOnlyList<string> available)
        {
            EnumTypeName = enumTypeName;
            AvailableSelections = available;
        }

        /// <summary>Every selection name the recorder ASKED FOR, in order.</summary>
        internal IReadOnlyList<string> Applied => _applied;

        public string EnumTypeName { get; }

        public IReadOnlyList<string> AvailableSelections { get; }

        public string? DefaultValuedSelection => null;

        public string ReadCurrentSelection() => _current;

        public SelectionApplyResult Apply(string selectionName)
        {
            _applied.Add(selectionName);
            _current = selectionName;
            return new SelectionApplyResult(
                selectionName,
                new[] { new SelectionApplyAttempt("property set (stub)", true, null) },
                _current);
        }
    }
}
