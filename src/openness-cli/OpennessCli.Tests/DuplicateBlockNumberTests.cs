using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using OpennessCli;
using OpennessCli.Cli;
using OpennessCli.Model;
using OpennessCli.Openness;
using Xunit;

namespace OpennessCli.Tests;

/// <summary>
/// Duplicate block numbers — the hole measured in the gate itself on 2026-08-13.
///
/// An artifact declaring <c>&lt;Number&gt;910&lt;/Number&gt;</c> was imported into a project where
/// another FC already held 910. TIA accepted it and the project then contained two blocks at FC 910.
/// Every gate in the chain passed: `import` exit 0, per-block `compile` exit 0 with *"successfully
/// compiled"* and <c>CONSISTENT: yes</c>, device compile <c>Success, errors=0</c>, and `sanity-check`
/// <c>OVERALL: HEALTHY</c>, <c>INCONSISTENT: 0</c>, exit 0. Hard rule 4 names `sanity-check` as THE
/// gate.
///
/// <b>What these tests are actually for.</b> The exit code was never the bug — the bug was a green
/// HEALTHY verdict printed over a broken project. So the assertions below are on the REPORT: that it
/// names the type, the number and EVERY block holding it, and that it stops saying HEALTHY. The exit
/// code is checked too, but it is the smaller half.
///
/// Everything runs against <see cref="FakeGateway"/>. That is the only way this can be tested at all:
/// a duplicate-number project is not a state a unit test can ask TIA to build, and the whole failure
/// mode is that nothing downstream — consistency, compile, drift-check — can see it.
/// </summary>
public class DuplicateBlockNumberTests
{
    private const string Device = "S7-1200 station_1";
    private const string Group = "S7-1200 station_1/PLC_1/Program blocks";

    private static BlockInfo Block(string name, BlockType type, int number, string path = Group, bool safety = false) =>
        new(name, type, number, safety ? "F_LAD" : "LAD", safety, path, IsConsistent: true);

    /// <summary>The scope `sanity-check` compiles at since 2026-08-13 — hardware and program.</summary>
    private const string StationScope = "station (hardware + program)";

    private static readonly CompileResult CleanCompile =
        new(CompileState.Success, ErrorCount: 0, WarningCount: 0, Messages: Array.Empty<CompileMessage>());

    // ---- the detector -------------------------------------------------------------------------

    [Fact]
    public void Find_TwoBlocksAtOneNumber_IsOneGroupNamingBoth()
    {
        var duplicates = DuplicateBlockNumberFinder.Find(new[]
        {
            Block("FC_MsTick", BlockType.FC, 910),
            Block("FC_HarnessCopyLayer", BlockType.FC, 910),
            Block("FC_Other", BlockType.FC, 911),
        });

        var group = Assert.Single(duplicates);
        Assert.Equal(BlockType.FC, group.Type);
        Assert.Equal(910, group.Number);
        Assert.Equal(Device, group.Device);

        // "There is a duplicate somewhere" is not actionable — every holder must be named.
        Assert.Equal(
            new[] { "FC_HarnessCopyLayer", "FC_MsTick" },
            group.Blocks.Select(b => b.Name).ToArray());
    }

    [Fact]
    public void Find_ThreeBlocksAtOneNumber_ReportsAllThreeInOneGroup()
    {
        var duplicates = DuplicateBlockNumberFinder.Find(new[]
        {
            Block("A", BlockType.FC, 910),
            Block("B", BlockType.FC, 910),
            Block("C", BlockType.FC, 910),
        });

        Assert.Equal(3, Assert.Single(duplicates).Blocks.Count);
    }

    [Fact]
    public void Find_CleanProject_FindsNothing()
    {
        var duplicates = DuplicateBlockNumberFinder.Find(new[]
        {
            Block("FC_MsTick", BlockType.FC, 910),
            Block("FC_Other", BlockType.FC, 911),
            Block("DB_State", BlockType.DB, 1),
        });

        Assert.Empty(duplicates);
    }

    // FC/FB/OB/DB are separate number spaces in S7 — an FC 910 and a DB 910 are not a collision, and
    // reporting one would be a finding manufactured out of a correct project.
    [Fact]
    public void Find_SameNumberDifferentBlockTypes_IsNotACollision()
    {
        var duplicates = DuplicateBlockNumberFinder.Find(new[]
        {
            Block("FC_MsTick", BlockType.FC, 910),
            Block("DB_MsTick", BlockType.DB, 910),
            Block("FB_MsTick", BlockType.FB, 910),
            Block("OB_MsTick", BlockType.OB, 910),
        });

        Assert.Empty(duplicates);
    }

    // Block numbers are scoped to a PLC. Two PLCs in one project may each legitimately hold FC 910,
    // and EnumerateBlocks walks every device in the project — so grouping without the device would
    // fail a perfectly correct multi-PLC project.
    [Fact]
    public void Find_SameNumberOnDifferentDevices_IsNotACollision()
    {
        var duplicates = DuplicateBlockNumberFinder.Find(new[]
        {
            Block("FC_MsTick", BlockType.FC, 910, "PLC_A/PLC_A 6ES7 214-1AG40-0XB0/Program blocks"),
            Block("FC_MsTick", BlockType.FC, 910, "PLC_B/PLC_B 6ES7 214-1AG40-0XB0/Program blocks"),
        });

        Assert.Empty(duplicates);
    }

    // The converse of the above: the same device reached through different GROUPS is still one
    // number space. A collision hidden by a folder would be the easiest one to create by accident.
    [Fact]
    public void Find_SameDeviceDifferentGroups_IsACollision()
    {
        var duplicates = DuplicateBlockNumberFinder.Find(new[]
        {
            Block("FC_MsTick", BlockType.FC, 910, "PLC_A/PLC_A/Program blocks"),
            Block("FC_Copy", BlockType.FC, 910, "PLC_A/PLC_A/Program blocks/Harness"),
        });

        Assert.Single(duplicates);
    }

    [Fact]
    public void Find_SafetyBlock_IsReportedAndFlagged()
    {
        var duplicates = DuplicateBlockNumberFinder.Find(new[]
        {
            Block("FC_MsTick", BlockType.FC, 910),
            Block("F_Guard", BlockType.FC, 910, safety: true),
        });

        var group = Assert.Single(duplicates);
        Assert.Contains(group.Blocks, b => b is { Name: "F_Guard", IsSafety: true });
    }

    [Fact]
    public void DeviceOf_TakesTheFirstPathSegment()
    {
        Assert.Equal("PLC1", DuplicateBlockNumberFinder.DeviceOf("PLC1/PLC1 6ES7 214-1AG40-0XB0/Program blocks"));
        Assert.Equal("PLC1", DuplicateBlockNumberFinder.DeviceOf("PLC1"));
    }

    // ---- the verdict --------------------------------------------------------------------------

    /// <summary>
    /// THE MEASURED SHAPE: every block consistent, every type consistent, the device compiling clean
    /// — and two blocks at FC 910. This is the exact state that reported OVERALL: HEALTHY.
    /// </summary>
    private static readonly SanityCheckResult MeasuredResult = new(
        TotalBlocks: 34,
        InconsistentBlocks: Array.Empty<BlockConsistencyIssue>(),
        DeviceCompiles: new[] { new DeviceCompileSummary("S7-1200 station_1/PLC_1", CleanCompile, StationScope) },
        TotalTypes: 7,
        InconsistentTypes: Array.Empty<TypeConsistencyIssue>(),
        DuplicateNumbers: new[]
        {
            new DuplicateBlockNumber(Device, BlockType.FC, 910, new[]
            {
                new DuplicateBlockNumberEntry("FC_HarnessCopyLayer", Group, "LAD", IsSafety: false),
                new DuplicateBlockNumberEntry("FC_MsTick", Group, "LAD", IsSafety: false),
            }),
        });

    private static readonly SanityCheckResult CleanResult = new(
        TotalBlocks: 34,
        InconsistentBlocks: Array.Empty<BlockConsistencyIssue>(),
        DeviceCompiles: new[] { new DeviceCompileSummary("S7-1200 station_1/PLC_1", CleanCompile, StationScope) },
        TotalTypes: 7,
        InconsistentTypes: Array.Empty<TypeConsistencyIssue>(),
        DuplicateNumbers: Array.Empty<DuplicateBlockNumber>());

    [Fact]
    public void SanityCheckResult_IsNotHealthy_WhenEverythingIsConsistentAndTwoBlocksShareANumber()
    {
        Assert.False(MeasuredResult.IsHealthy);
    }

    // ---- the report ---------------------------------------------------------------------------

    [Fact]
    public void SanityCheckTable_DoesNotSayHealthy_OverADuplicate()
    {
        var text = OutputFormatter.FormatSanityCheckTable(MeasuredResult);

        Assert.DoesNotContain("OVERALL: HEALTHY", text);
        Assert.Contains("OVERALL: ISSUES FOUND", text);
    }

    [Fact]
    public void SanityCheckTable_NamesTheTypeTheNumberAndEveryBlockHoldingIt()
    {
        var text = OutputFormatter.FormatSanityCheckTable(MeasuredResult);

        Assert.Contains("FC 910", text);
        Assert.Contains("FC_MsTick", text);
        Assert.Contains("FC_HarnessCopyLayer", text);
        Assert.Contains(Device, text);
    }

    [Fact]
    public void SanityCheckTable_SaysNothingHereClearsItAndNamesTheRemedy()
    {
        var text = OutputFormatter.FormatSanityCheckTable(MeasuredResult);

        // The two facts a reader must not have to infer: that the green compile below means nothing
        // about this, and that no amount of re-running or compiling will clear it.
        Assert.Contains("TIA ACCEPTS THIS", text);
        Assert.Contains("nothing here clears it", text);
        Assert.Contains("delete", text, StringComparison.OrdinalIgnoreCase);
    }

    // FI-62's rule applied to a new question: a count that appears only when non-zero cannot be told
    // apart from a check that does not exist — and "a check that does not exist" is this defect.
    [Fact]
    public void SanityCheckTable_ReportsTheDuplicateCountEvenAtZero()
    {
        var text = OutputFormatter.FormatSanityCheckTable(CleanResult);

        Assert.Contains("DUPLICATE NUMBERS: 0", text);
        Assert.Contains("OVERALL: HEALTHY", text);
    }

    [Fact]
    public void SanityCheckJson_CarriesEveryCollidingBlock()
    {
        var json = OutputFormatter.FormatSanityCheckJson(MeasuredResult);
        var root = JsonDocument.Parse(json).RootElement;

        Assert.False(root.GetProperty("healthy").GetBoolean());

        var duplicates = root.GetProperty("duplicateBlockNumbers");
        Assert.Equal(1, duplicates.GetArrayLength());
        Assert.Equal("FC", duplicates[0].GetProperty("type").GetString());
        Assert.Equal(910, duplicates[0].GetProperty("number").GetInt32());

        var blocks = duplicates[0].GetProperty("blocks");
        Assert.Equal(2, blocks.GetArrayLength());
        Assert.Equal(
            new[] { "FC_HarnessCopyLayer", "FC_MsTick" },
            blocks.EnumerateArray().Select(b => b.GetProperty("name").GetString()).ToArray());
    }

    // ---- sanity-check's exit code -------------------------------------------------------------

    private static ListOptions SanityOptions(bool json = false) =>
        new("C:\\proj\\My.ap20", json, TagTables: false, TiaInstallOverride: null,
            TimeoutConnectSeconds: 1, TimeoutOpenSeconds: 1);

    [Fact]
    public void SanityCheck_Exits19_OnTheMeasuredShape()
    {
        var gateway = new FakeGateway { SanityResult = MeasuredResult };

        var (exitCode, stdout, _) = CaptureConsole(() =>
            Program.RunSanityCheck(gateway, SanityOptions(), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.DuplicateBlockNumber, exitCode);
        Assert.NotEqual(ExitCodes.Success, exitCode);

        // The verdict, not just the code: this is what an operator reads.
        Assert.Contains("OVERALL: ISSUES FOUND", stdout);
        Assert.Contains("FC 910", stdout);
    }

    // 19 is NOT 9, and that is the point. 9 means "something is inconsistent or a device failed to
    // compile", whose documented remedy is a per-block compile — the exact move that ERASED the only
    // signal there was, leaving the duplicate behind a HEALTHY verdict.
    [Fact]
    public void SanityCheck_DuplicateCode_IsDistinctFromSanityCheckFailed()
    {
        Assert.NotEqual(ExitCodes.SanityCheckFailed, ExitCodes.DuplicateBlockNumber);
    }

    [Fact]
    public void SanityCheck_Exits9_WhenOnlyConsistencyIsWrong()
    {
        var gateway = new FakeGateway
        {
            SanityResult = CleanResult with
            {
                InconsistentBlocks = new[] { new BlockConsistencyIssue("FC_MsTick", Group, "LAD") },
            },
        };

        var (exitCode, _, _) = CaptureConsole(() =>
            Program.RunSanityCheck(gateway, SanityOptions(), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.SanityCheckFailed, exitCode);
    }

    // A project that is BOTH inconsistent and duplicated returns 19, and prints both lists. Ranked
    // that way because 9's remedy is actively wrong on a duplicate; nothing is hidden either way.
    [Fact]
    public void SanityCheck_DuplicateOutranksInconsistency_AndBothAreStillPrinted()
    {
        var gateway = new FakeGateway
        {
            SanityResult = MeasuredResult with
            {
                InconsistentBlocks = new[] { new BlockConsistencyIssue("FC_Elsewhere", Group, "LAD") },
            },
        };

        var (exitCode, stdout, _) = CaptureConsole(() =>
            Program.RunSanityCheck(gateway, SanityOptions(), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.DuplicateBlockNumber, exitCode);
        Assert.Contains("FC_Elsewhere", stdout);
        Assert.Contains("FC_HarnessCopyLayer", stdout);
    }

    [Fact]
    public void SanityCheck_Exits0_OnACleanProject()
    {
        var gateway = new FakeGateway { SanityResult = CleanResult };

        var (exitCode, _, _) = CaptureConsole(() =>
            Program.RunSanityCheck(gateway, SanityOptions(), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.Success, exitCode);
    }

    // ---- import -------------------------------------------------------------------------------

    private static ImportCommandOptions ImportOptions(bool asType = false) =>
        new("C:\\proj\\My.ap20", Group, new[] { "C:\\out\\FC_HarnessCopyLayer.xml" },
            AsType: asType, AsTagTable: false, AsScreen: false, Device: null, TiaInstallOverride: null,
            TimeoutConnectSeconds: 1, TimeoutOpenSeconds: 1);

    [Fact]
    public void Import_Exits19_WhenTheImportedBlockCollidesWithAnExistingNumber()
    {
        var gateway = new FakeGateway
        {
            ImportedBlocks = new[] { Block("FC_HarnessCopyLayer", BlockType.FC, 910) },
            BlocksForEnumeration = new[]
            {
                Block("FC_MsTick", BlockType.FC, 910),
                Block("FC_HarnessCopyLayer", BlockType.FC, 910),
            },
        };

        var (exitCode, _, stderr) = CaptureConsole(() =>
            Program.RunImport(gateway, ImportOptions(), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.DuplicateBlockNumber, exitCode);
        Assert.Contains("FC 910", stderr);
        Assert.Contains("FC_MsTick", stderr);

        // The non-zero code must never read as "nothing was imported" — the files DID go in, and the
        // recovery (delete or renumber) depends on knowing that.
        Assert.Contains("NOT a failed import", stderr);
        Assert.Contains("SAVED", stderr);

        // Which of the colliding blocks this command wrote: "I just broke it" and "it was already
        // broken" are different situations with different next moves.
        Assert.Contains("written by this command", stderr);
    }

    [Fact]
    public void Import_Exits0_WhenNothingCollides()
    {
        var gateway = new FakeGateway
        {
            ImportedBlocks = new[] { Block("FC_HarnessCopyLayer", BlockType.FC, 911) },
            BlocksForEnumeration = new[]
            {
                Block("FC_MsTick", BlockType.FC, 910),
                Block("FC_HarnessCopyLayer", BlockType.FC, 911),
            },
        };

        var (exitCode, _, stderr) = CaptureConsole(() =>
            Program.RunImport(gateway, ImportOptions(), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.DoesNotContain("DUPLICATE BLOCK NUMBERS", stderr);
    }

    // Re-importing a block that is already there, over itself, is the commonest thing `import` does.
    // It must not fire — a guard that fails on the normal case is a guard people route around.
    [Fact]
    public void Import_ReImportingABlockOverItself_IsNotACollision()
    {
        var gateway = new FakeGateway
        {
            ImportedBlocks = new[] { Block("FC_MsTick", BlockType.FC, 910) },
            BlocksForEnumeration = new[] { Block("FC_MsTick", BlockType.FC, 910) },
        };

        var (exitCode, _, _) = CaptureConsole(() =>
            Program.RunImport(gateway, ImportOptions(), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.Success, exitCode);
    }

    // A UDT has no number, so there is nothing on that path to collide. Asserted as "the enumeration
    // was never walked" rather than as an exit code, so the check cannot quietly become a no-op that
    // still costs a project walk on every type import.
    [Fact]
    public void Import_AsType_DoesNotScanForDuplicates()
    {
        var gateway = new FakeGateway { ImportedTypes = new[] { "UDT_Drum" } };

        var (exitCode, _, _) = CaptureConsole(() =>
            Program.RunImport(gateway, ImportOptions(asType: true), timeoutOpenSeconds: 1));

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(0, gateway.EnumerateBlocksCalls);
    }

    // ---- import-all ---------------------------------------------------------------------------

    private const string BlockXml =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?><Document><Engineering version=\"V20\" /><SW.Blocks.FC ID=\"0\" /></Document>";

    [Fact]
    public void ImportAll_Exits19_WhenTheRestoredCorpusHoldsTwoBlocksAtOneNumber()
    {
        var dir = Path.Combine(Path.GetTempPath(), "openness-cli-dupe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var file = Path.Combine(dir, "FC_HarnessCopyLayer.xml");
            File.WriteAllText(file, BlockXml);

            var gateway = new FakeGateway
            {
                BlocksForEnumeration = new[]
                {
                    Block("FC_MsTick", BlockType.FC, 910),
                    Block("FC_HarnessCopyLayer", BlockType.FC, 910),
                },
            };
            gateway.ImportedBlockFiles[file] = new[] { Block("FC_HarnessCopyLayer", BlockType.FC, 910) };

            var options = new ImportAllCommandOptions(
                "C:\\proj\\My.ap20", Group, new[] { dir }, Json: false, DryRun: false,
                TiaInstallOverride: null, TimeoutConnectSeconds: 1, TimeoutOpenSeconds: 1);

            var (exitCode, _, stderr) = CaptureConsole(() =>
                Program.RunImportAll(gateway, options, timeoutOpenSeconds: 1));

            Assert.Equal(ExitCodes.DuplicateBlockNumber, exitCode);
            Assert.Contains("FC 910", stderr);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
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
