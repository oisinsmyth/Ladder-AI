using System;
using System.Collections.Generic;
using System.Text.Json;
using OpennessCli.Cli;
using OpennessCli.Model;
using OpennessCli.Openness;
using Xunit;

namespace OpennessCli.Tests;

// portal-status is split so the COM-free half is fully unit-testable: PortalStatusClassifier (pure,
// hand-built lists) + FormatPortalStatus (pure) + ParsePortalStatus (pure). The Siemens-touching
// enumeration on OpennessGateway is integration-only (needs a real Portal) and isn't exercised here.
public class PortalStatusTests
{
    private static PortalProcessInfo Proc(int pid, string? projectPath, bool marked, bool ui = true) =>
        new(pid, projectPath, new DateTime(2026, 7, 20, 9, 15, 0), ui, marked);

    // ---- Classifier: one bucket per case -------------------------------------------------------

    [Fact]
    public void Classify_ProcessWithProjectOpen_IsInUse()
    {
        var report = PortalStatusClassifier.Classify(new[] { Proc(100, @"C:\proj\GenProject1.ap20", marked: false) });

        var only = Assert.Single(report.Processes);
        Assert.Equal(PortalProcessClass.InUse, only.Class);
        Assert.Equal(1, report.InUseCount);
    }

    [Fact]
    public void Classify_EmptyProjectButMarked_IsSelfLaunchedOrphan()
    {
        var report = PortalStatusClassifier.Classify(new[] { Proc(101, projectPath: null, marked: true) });

        var only = Assert.Single(report.Processes);
        Assert.Equal(PortalProcessClass.SelfLaunchedOrphan, only.Class);
        Assert.Equal(1, report.SelfLaunchedOrphanCount);
    }

    [Fact]
    public void Classify_EmptyProjectAndNotMarked_IsStrayEmpty()
    {
        var report = PortalStatusClassifier.Classify(new[] { Proc(102, projectPath: null, marked: false) });

        var only = Assert.Single(report.Processes);
        Assert.Equal(PortalProcessClass.StrayEmpty, only.Class);
        Assert.Equal(1, report.StrayEmptyCount);
    }

    [Fact]
    public void Classify_ProjectOpenWinsOverMarked_IsStillInUse()
    {
        // A marked PID that has since had a project opened into it should read as in-use, not orphan
        // — the ProjectPath check comes first, matching the registry's own "unmark on open" semantics.
        var report = PortalStatusClassifier.Classify(new[] { Proc(103, @"C:\proj\X.ap20", marked: true) });

        Assert.Equal(PortalProcessClass.InUse, Assert.Single(report.Processes).Class);
    }

    [Fact]
    public void Classify_EmptyString_ProjectPathTreatedAsEmpty()
    {
        var report = PortalStatusClassifier.Classify(new[] { Proc(104, projectPath: string.Empty, marked: false) });

        Assert.Equal(PortalProcessClass.StrayEmpty, Assert.Single(report.Processes).Class);
    }

    [Fact]
    public void Classify_Mixed_CountsEachBucket()
    {
        var report = PortalStatusClassifier.Classify(new[]
        {
            Proc(1, @"C:\a.ap20", marked: false),
            Proc(2, null, marked: true),
            Proc(3, null, marked: false),
            Proc(4, null, marked: false),
        });

        Assert.Equal(4, report.Total);
        Assert.Equal(1, report.InUseCount);
        Assert.Equal(1, report.SelfLaunchedOrphanCount);
        Assert.Equal(2, report.StrayEmptyCount);
    }

    // ---- Classifier: the human note ------------------------------------------------------------

    [Fact]
    public void Classify_NoProcesses_NoteSaysNoneRunning()
    {
        var report = PortalStatusClassifier.Classify(Array.Empty<PortalProcessInfo>());

        Assert.Empty(report.Processes);
        Assert.Contains("No TIA Portal processes", report.Note);
    }

    [Fact]
    public void Classify_SingleStray_NoteMentionsFirstConnectDialog()
    {
        var report = PortalStatusClassifier.Classify(new[] { Proc(1, null, marked: false) });

        Assert.Contains("first-connect", report.Note);
    }

    [Fact]
    public void Classify_MultipleStrays_NoteMentionsPileup()
    {
        var report = PortalStatusClassifier.Classify(new[]
        {
            Proc(1, null, marked: false),
            Proc(2, null, marked: false),
            Proc(3, null, marked: false),
        });

        Assert.Contains("pileup", report.Note);
    }

    [Fact]
    public void Classify_OnlyInUse_NoteSaysNothingToCleanUp()
    {
        var report = PortalStatusClassifier.Classify(new[] { Proc(1, @"C:\a.ap20", marked: false) });

        Assert.Contains("nothing to clean up", report.Note);
    }

    [Fact]
    public void Classify_Orphan_NoteSaysNoActionNeeded()
    {
        var report = PortalStatusClassifier.Classify(new[] { Proc(1, null, marked: true) });

        Assert.Contains("no action is needed", report.Note);
    }

    // ---- ParsePortalStatus ---------------------------------------------------------------------

    [Fact]
    public void Parse_PortalStatus_NoFlags_UsesDefaults()
    {
        var result = ArgumentParser.Parse(new[] { "portal-status" });

        var success = Assert.IsType<ParseResult.PortalStatusSuccess>(result);
        Assert.False(success.Options.Json);
        Assert.Null(success.Options.TiaInstallOverride);
    }

    [Fact]
    public void Parse_PortalStatus_JsonAndInstall_Parsed()
    {
        var result = ArgumentParser.Parse(new[] { "portal-status", "--json", "--tia-install", "C:\\custom\\dir" });

        var success = Assert.IsType<ParseResult.PortalStatusSuccess>(result);
        Assert.True(success.Options.Json);
        Assert.Equal("C:\\custom\\dir", success.Options.TiaInstallOverride);
    }

    [Fact]
    public void Parse_PortalStatus_RejectsPositionalArgument()
    {
        // No <project> here — a stray positional is a mistake, not a project to open.
        var result = ArgumentParser.Parse(new[] { "portal-status", "C:\\proj\\My.ap20" });

        var failure = Assert.IsType<ParseResult.Failure>(result);
        Assert.Contains("no <project>", failure.Message);
    }

    [Fact]
    public void Parse_PortalStatus_UnknownFlag_Fails()
    {
        var result = ArgumentParser.Parse(new[] { "portal-status", "--bogus" });

        Assert.IsType<ParseResult.Failure>(result);
    }

    [Fact]
    public void Parse_PortalStatus_TiaInstallMissingValue_Fails()
    {
        var result = ArgumentParser.Parse(new[] { "portal-status", "--tia-install" });

        var failure = Assert.IsType<ParseResult.Failure>(result);
        Assert.Contains("--tia-install", failure.Message);
    }

    [Fact]
    public void Parse_UnknownSubcommandListing_IncludesPortalStatus()
    {
        var result = ArgumentParser.Parse(new[] { "nonsense" });

        var failure = Assert.IsType<ParseResult.Failure>(result);
        Assert.Contains("portal-status", failure.Message);
    }

    // ---- FormatPortalStatus --------------------------------------------------------------------

    private static readonly PortalStatusReport SampleReport = PortalStatusClassifier.Classify(new[]
    {
        new PortalProcessInfo(1111, @"C:\proj\GenProject1.ap20", new DateTime(2026, 7, 20, 9, 15, 0), true, false),
        new PortalProcessInfo(2222, null, new DateTime(2026, 7, 20, 9, 20, 0), true, true),
        new PortalProcessInfo(3333, null, new DateTime(2026, 7, 20, 9, 25, 0), false, false),
    });

    [Fact]
    public void FormatTable_ShowsCountsHeaderNoteAndEachPid()
    {
        var table = OutputFormatter.FormatPortalStatusTable(SampleReport);

        Assert.Contains("PROCESSES: 3", table);
        Assert.Contains("IN-USE: 1", table);
        Assert.Contains("SELF-LAUNCHED ORPHANS: 1", table);
        Assert.Contains("STRAYS: 1", table);
        Assert.Contains("NOTE:", table);
        Assert.Contains("1111", table);
        Assert.Contains("2222", table);
        Assert.Contains("3333", table);
        Assert.Contains("self-launched-orphan", table);
        Assert.Contains("stray-empty", table);
        Assert.Contains("in-use", table);
        Assert.Contains("GenProject1.ap20", table);
        Assert.Contains("(none)", table);
    }

    [Fact]
    public void FormatTable_NoProcesses_ShowsCountsAndNoteWithoutTable()
    {
        var empty = PortalStatusClassifier.Classify(Array.Empty<PortalProcessInfo>());
        var table = OutputFormatter.FormatPortalStatusTable(empty);

        Assert.Contains("PROCESSES: 0", table);
        Assert.Contains("No TIA Portal processes", table);
        Assert.DoesNotContain("PID", table);
    }

    [Fact]
    public void FormatJson_IsValidAndRoundTripsClassesCountsAndNote()
    {
        var json = OutputFormatter.FormatPortalStatusJson(SampleReport);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var processes = root.GetProperty("processes");
        Assert.Equal(3, processes.GetArrayLength());
        Assert.Equal(1111, processes[0].GetProperty("pid").GetInt32());
        Assert.Equal("in-use", processes[0].GetProperty("class").GetString());
        Assert.Equal("self-launched-orphan", processes[1].GetProperty("class").GetString());
        Assert.True(processes[1].GetProperty("markedByThisTool").GetBoolean());
        Assert.Equal("stray-empty", processes[2].GetProperty("class").GetString());
        Assert.False(processes[2].GetProperty("hasUserInterface").GetBoolean());

        var counts = root.GetProperty("counts");
        Assert.Equal(3, counts.GetProperty("total").GetInt32());
        Assert.Equal(1, counts.GetProperty("inUse").GetInt32());
        Assert.Equal(1, counts.GetProperty("selfLaunchedOrphans").GetInt32());
        Assert.Equal(1, counts.GetProperty("strayEmpty").GetInt32());

        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("note").GetString()));
    }

    [Fact]
    public void FormatJson_EmptyReport_HasEmptyProcessesArray()
    {
        var empty = PortalStatusClassifier.Classify(Array.Empty<PortalProcessInfo>());
        var json = OutputFormatter.FormatPortalStatusJson(empty);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(0, doc.RootElement.GetProperty("processes").GetArrayLength());
    }
}
