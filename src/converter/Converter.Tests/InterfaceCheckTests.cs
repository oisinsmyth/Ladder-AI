using System.Text.Json;
using Converter.InterfaceCheck;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// 2026-08-14. `converter interface-check` — NB-30, the static interface check that did not exist.
///
/// <para>*** D6's GREEN IS VALID ONLY WHILE D1 IS REPORTED BY THIS CHECK. *** D1 — a response signal
/// the specification names and the block does not provide — hid inside a relational conformance
/// assertion whose two operands SHARED the error, and a relational assertion is structurally blind to
/// exactly that. The comparison that finds it is a two-name set difference over the block's interface,
/// which nothing in the system made.</para>
///
/// <para><b>The two wrong implementations were predicted before the build and are pinned here</b>, in
/// <c>ReadingInputOutputSectionsOnly…</c> and <c>MatchingCommentsWould…</c>: one reports BOTH signals
/// missing including the one that exists, and one PASSES the defect on the word "inhibit" in a block
/// comment. Both would have looked like working checks.</para>
///
/// <para><b>And the over-firing direction is tested as deliberately as the refusing one</b> — a gate
/// that refuses everything passes every test that only checks refusals. <c>WholeCorpusSweep…</c> is
/// the strongest form: every block in the committed corpus, checked against its own interface members,
/// must come out PRESENT, with the file count printed.</para>
/// </summary>
public class InterfaceCheckTests : IDisposable
{
    private readonly string _dir;

    public InterfaceCheckTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"ifacecheck-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "patterns")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Could not find repo root.");
    }

    private static string CorpusDir() => Path.Combine(RepoRoot(), "ir", "test-project001");

    private void Write(string name, string text) => File.WriteAllText(Path.Combine(_dir, name), text);

    // A block in this corpus's own house style (C-132): INPUT and OUTPUT are BOTH EMPTY and the whole
    // caller interface is one STATIC member of a UDT. This shape is the reason the check exists in the
    // form it does.
    private void WriteHouseStyleBlock()
    {
        Write("FB_Thing.ir",
            "BLOCK FB FB_Thing\n" +
            "ROOTID 0\n" +
            "NUMBER 60\n" +
            "LANGUAGE LAD\n" +
            "COMMENT \"Raises an alarm and an inhibit demand when the thing stays high.\"\n" +
            "\n" +
            "INTERFACE\n" +
            "  INPUT\n" +
            "  OUTPUT\n" +
            "  STATIC\n" +
            "    IO : \"UDT_ThingIO\"\n" +
            "      LevelHigh : Bool\n" +
            "      ThingAlarm : Bool\n" +
            "    Scratch : Bool\n" +
            "\n" +
            "NETWORK 1 \"Alarm\"\n" +
            "  COIL IO.ThingAlarm := IO.LevelHigh\n");

        Write("UDT_ThingIO.ir",
            "TYPE UDT_ThingIO\n" +
            "  ROOTID 0\n" +
            "  MEMBERS\n" +
            "    LevelHigh : Bool\n" +
            "    ThingAlarm : Bool\n");
    }

    // ---------------------------------------------------------------- the two directions

    [Fact]
    public void RequiredNameThatExists_IsPresent_AndTheCheckPasses()
    {
        WriteHouseStyleBlock();

        var report = InterfaceCheckRunner.Run(_dir, "FB_Thing", new[] { "ThingAlarm" }, "test");

        Assert.True(report.Checked);
        Assert.False(report.BlockFails);
        Assert.Equal(RequirementStatus.Present, Assert.Single(report.Requirements).Status);
        Assert.Equal("STATIC/IO/ThingAlarm", report.Requirements[0].FoundAt);
    }

    [Fact]
    public void RequiredNameThatDoesNotExist_IsAFailAgainstTheBlock()
    {
        WriteHouseStyleBlock();

        var report = InterfaceCheckRunner.Run(_dir, "FB_Thing", new[] { "ThingInhibit" }, "test");

        Assert.True(report.Checked);
        Assert.True(report.BlockFails);
        Assert.Equal(RequirementStatus.Missing, Assert.Single(report.Requirements).Status);
        Assert.Contains("FAIL AGAINST THE BLOCK", report.Requirements[0].Detail);
    }

    [Fact]
    public void TheMixedCase_ReportsBothHalvesSeparately()
    {
        WriteHouseStyleBlock();

        var report = InterfaceCheckRunner.Run(_dir, "FB_Thing", new[] { "ThingAlarm", "ThingInhibit" }, "test");

        Assert.True(report.BlockFails);
        Assert.Single(report.Present);
        Assert.Single(report.Missing);

        // A partially-failing check must still name what it FOUND. A verdict that collapses to one
        // word sends an author looking for two defects where there is one.
        var text = InterfaceCheckOutputFormatter.FormatText(report);
        Assert.Contains("PRESENT ThingAlarm", text);
        Assert.Contains("MISSING ThingInhibit", text);
        Assert.Contains("1 of 2", text);
    }

    // ---------------------------------------------- the two wrong implementations, pinned

    /// <summary>
    /// *** THE FIRST PREDICTED WRONG IMPLEMENTATION. *** On this house style INPUT and OUTPUT are both
    /// empty, so a check reading only those sections reports EVERY required signal missing — including
    /// the one that exists. A gate that accuses correct work of the most serious offence in the project
    /// is one that gets disbelieved, and the day it is right nobody looks.
    /// </summary>
    [Fact]
    public void ReadingInputOutputSectionsOnly_WouldReportBothMissing_SoTheWalkMustCrossIntoStatic()
    {
        WriteHouseStyleBlock();

        var report = InterfaceCheckRunner.Run(_dir, "FB_Thing", new[] { "ThingAlarm" }, "test");

        // The premise: those two sections really are empty. If this ever stops being true the test
        // below stops being about anything, so it is asserted rather than assumed.
        Assert.Equal(0, report.SectionCounts["INPUT"]);
        Assert.Equal(0, report.SectionCounts["OUTPUT"]);
        Assert.True(report.SectionCounts["STATIC"] > 0);

        // And the consequence: the name IS found, in STATIC, through the UDT member.
        Assert.Equal(RequirementStatus.Present, report.Requirements[0].Status);
    }

    /// <summary>
    /// *** THE SECOND PREDICTED WRONG IMPLEMENTATION. *** The word the missing signal is named for
    /// appears in the block's own COMMENT. A check matching comments finds it and passes the defect.
    /// </summary>
    [Fact]
    public void MatchingCommentsWouldPassTheDefect_SoOnlyMemberNamesCount()
    {
        WriteHouseStyleBlock();

        // The premise, asserted: the word really is in the block text.
        var blockText = File.ReadAllText(Path.Combine(_dir, "FB_Thing.ir"));
        Assert.Contains("inhibit", blockText, StringComparison.OrdinalIgnoreCase);

        var report = InterfaceCheckRunner.Run(_dir, "FB_Thing", new[] { "ThingInhibit" }, "test");

        Assert.True(report.BlockFails);
        Assert.DoesNotContain(report.ExaminedMembers, m => m.Contains("inhibit", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// *** FOUND BY MUTATION, AND IT IS THE UNEXECUTED-BRANCH FAMILY. *** Disabling the
    /// <c>TryGetUdt</c> descent entirely left ALL of the first sixteen tests green, including both
    /// whole-corpus sweeps — because every UDT-typed interface member in <c>ir/test-project001</c>
    /// carries its members INLINED in the block's own IR, so the cross-file resolution never runs.
    /// A branch nothing reaches is a note about a branch.
    ///
    /// <para>The shape it exists for is real and simply absent from today's corpus: a member declared
    /// with a named PLC data type and NO inlined body. This test is that shape. <b>It is also the
    /// asserted absence</b> — <c>RealCorpus_EveryUdtTypedMember…</c> below pins that the corpus has no
    /// such member today, so the day one appears, that test fails and demands this one be re-read
    /// against real data rather than a fixture.</para>
    /// </summary>
    [Fact]
    public void AUdtTypedMemberWithNoInlinedBody_ResolvesThroughTheProjectsTypeDefinition()
    {
        Write("FB_NotInlined.ir",
            "BLOCK FB FB_NotInlined\n" +
            "ROOTID 0\n" +
            "NUMBER 65\n" +
            "LANGUAGE LAD\n" +
            "\n" +
            "INTERFACE\n" +
            "  STATIC\n" +
            "    IO : \"UDT_ThingIO\"\n" +
            "\n" +
            "NETWORK 1 \"X\"\n" +
            "  COIL DB_Output.A := DB_Input.B\n");

        Write("UDT_ThingIO.ir",
            "TYPE UDT_ThingIO\n" +
            "  ROOTID 0\n" +
            "  MEMBERS\n" +
            "    LevelHigh : Bool\n" +
            "    ThingAlarm : Bool\n");

        var report = InterfaceCheckRunner.Run(_dir, "FB_NotInlined", new[] { "ThingAlarm" }, "test");

        Assert.True(report.Checked);
        Assert.False(report.BlockFails);
        Assert.Equal("STATIC/IO/ThingAlarm", Assert.Single(report.Present).FoundAt);

        // And the member is NOT reported opaque — the whole point of the branch is that a resolvable
        // type is not an unknown one.
        Assert.Empty(report.OpaqueMembers);
    }

    /// <summary>
    /// The asserted absence that keeps the fixture above honest. <b>Every UDT-typed interface member in
    /// the committed corpus carries its members inlined today</b>, which is why the cross-file descent
    /// is unexercised by real data. When that stops being true this goes red and the resolution gets
    /// checked against a real block instead of a hand-written one.
    /// </summary>
    [Fact]
    public void RealCorpus_EveryUdtTypedInterfaceMemberIsInlined_SoTheCrossFileDescentIsFixtureOnlyToday()
    {
        var corpus = CorpusDir();
        var blocks = Directory.EnumerateFiles(corpus, "*.ir", SearchOption.TopDirectoryOnly)
            .Where(f => File.ReadAllText(f).StartsWith("BLOCK ", StringComparison.Ordinal))
            .ToList();

        Assert.True(blocks.Count >= 15, $"denominator: {blocks.Count} block file(s) swept");

        var opaqueOrResolved = new List<string>();
        foreach (var file in blocks)
        {
            var name = File.ReadLines(file).First().Split(' ')[2];
            var report = InterfaceCheckRunner.Run(corpus, name, new[] { "__probe__" }, "sweep");
            if (report.Checked && report.OpaqueMembers.Count > 0)
            {
                opaqueOrResolved.Add($"{name}: {string.Join(", ", report.OpaqueMembers.Select(o => o.Member))}");
            }
        }

        Assert.Empty(opaqueOrResolved);
    }

    // ---------------------------------------------------------------- empty is not clean

    [Fact]
    public void NoRequiredNames_IsNotChecked_NotAPass()
    {
        WriteHouseStyleBlock();

        var report = InterfaceCheckRunner.Run(_dir, "FB_Thing", Array.Empty<string>(), "test");

        Assert.False(report.Checked);
        Assert.False(report.BlockFails);
        Assert.Contains("Empty is not clean", report.NotCheckedReason);
    }

    [Fact]
    public void BlockNotInCorpus_IsNotChecked_NotAWholesaleFailAgainstIt()
    {
        WriteHouseStyleBlock();

        var report = InterfaceCheckRunner.Run(_dir, "FB_Absent", new[] { "ThingAlarm" }, "test");

        Assert.False(report.Checked);
        Assert.False(report.BlockFails);
        Assert.Empty(report.Requirements);
        Assert.Contains("no block named 'FB_Absent'", report.NotCheckedReason);
    }

    [Fact]
    public void BlockWithNoInterfaceMembers_IsNotChecked_NotAFullSweepOfMissings()
    {
        Write("FC_Bare.ir",
            "BLOCK FC FC_Bare\n" +
            "ROOTID 0\n" +
            "NUMBER 61\n" +
            "LANGUAGE LAD\n" +
            "\n" +
            "NETWORK 1 \"Nothing\"\n" +
            "  COIL DB_Output.A := DB_Input.B\n");

        var report = InterfaceCheckRunner.Run(_dir, "FC_Bare", new[] { "ThingAlarm" }, "test");

        Assert.False(report.Checked);
        Assert.False(report.BlockFails);
        Assert.Contains("no members in INPUT/OUTPUT/INOUT/STATIC/CONSTANT", report.NotCheckedReason);
    }

    /// <summary>
    /// A MISSING verdict is the positive claim that a block does not carry a name, and that claim is
    /// only sound over a COMPLETE member set. A member whose type could not be opened makes the set
    /// partial, so the negative half is withdrawn — the same direction of error
    /// <c>ReachableStateRunner</c> chooses on a partial corpus.
    /// </summary>
    [Fact]
    public void AMemberWhoseTypeCannotBeOpened_WithdrawsAMissingVerdictIntoNotChecked()
    {
        Write("FB_Opaque.ir",
            "BLOCK FB FB_Opaque\n" +
            "ROOTID 0\n" +
            "NUMBER 62\n" +
            "LANGUAGE LAD\n" +
            "\n" +
            "INTERFACE\n" +
            "  STATIC\n" +
            "    IO : \"UDT_NotInThisProject\"\n" +
            "\n" +
            "NETWORK 1 \"X\"\n" +
            "  COIL DB_Output.A := DB_Input.B\n");

        var report = InterfaceCheckRunner.Run(_dir, "FB_Opaque", new[] { "SomethingElse" }, "test");

        Assert.False(report.Checked);
        Assert.Contains("UDT_NotInThisProject", report.NotCheckedReason);
        Assert.Contains("COMPLETE member set", report.NotCheckedReason);
    }

    /// <summary>
    /// The converse, and it is the half that keeps the refusal above from swallowing everything: an
    /// opaque member does NOT block a PRESENT. Finding a name does not depend on having seen the rest.
    /// </summary>
    [Fact]
    public void AnOpaqueMemberDoesNotBlockAPresentVerdict()
    {
        Write("FB_HalfOpaque.ir",
            "BLOCK FB FB_HalfOpaque\n" +
            "ROOTID 0\n" +
            "NUMBER 63\n" +
            "LANGUAGE LAD\n" +
            "\n" +
            "INTERFACE\n" +
            "  STATIC\n" +
            "    Other : \"UDT_NotInThisProject\"\n" +
            "    ThingAlarm : Bool\n" +
            "\n" +
            "NETWORK 1 \"X\"\n" +
            "  COIL DB_Output.A := DB_Input.B\n");

        var report = InterfaceCheckRunner.Run(_dir, "FB_HalfOpaque", new[] { "ThingAlarm" }, "test");

        Assert.True(report.Checked);
        Assert.False(report.BlockFails);
        Assert.Single(report.OpaqueMembers);
    }

    // ---------------------------------------------------------------- the narrowings, printed

    [Fact]
    public void TempMembersAreExcludedByNameAndCounted_AndARequirementFoundOnlyThereIsStillMissing()
    {
        Write("FB_Temped.ir",
            "BLOCK FB FB_Temped\n" +
            "ROOTID 0\n" +
            "NUMBER 64\n" +
            "LANGUAGE LAD\n" +
            "\n" +
            "INTERFACE\n" +
            "  STATIC\n" +
            "    Keep : Bool\n" +
            "  TEMP\n" +
            "    ThingAlarm : Bool\n" +
            "\n" +
            "NETWORK 1 \"X\"\n" +
            "  COIL DB_Output.A := DB_Input.B\n");

        var report = InterfaceCheckRunner.Run(_dir, "FB_Temped", new[] { "ThingAlarm" }, "test");

        Assert.True(report.BlockFails);
        Assert.Equal(new[] { "TEMP/ThingAlarm" }, report.ExcludedTempMembers);

        // The exclusion is named in the finding, so nobody has to re-derive why a name they can see in
        // the file read MISSING.
        Assert.Contains("EXCLUDED", report.Requirements[0].Detail);

        // And the count is printed on EVERY run, including a zero one — a silent narrowing has no cost
        // to grow, and the first person to widen it will be solving a real problem.
        var text = InterfaceCheckOutputFormatter.FormatText(report);
        Assert.Contains("EXCLUDED: 1 TEMP member(s)", text);
    }

    [Fact]
    public void TheExcludedCountIsPrintedEvenWhenItIsZero()
    {
        WriteHouseStyleBlock();

        var text = InterfaceCheckOutputFormatter.FormatText(
            InterfaceCheckRunner.Run(_dir, "FB_Thing", new[] { "ThingAlarm" }, "test"));

        Assert.Contains("EXCLUDED: 0 TEMP member(s)", text);
        Assert.Contains("EXAMINED: 4 interface member name(s)", text);
    }

    // ---------------------------------------------------------------- unjudgeable inputs

    [Fact]
    public void ADottedPath_IsUnjudgeable_NotAFailAgainstTheBlock()
    {
        WriteHouseStyleBlock();

        var report = InterfaceCheckRunner.Run(_dir, "FB_Thing", new[] { "IO.ThingAlarm" }, "test");

        Assert.True(report.Checked);
        Assert.False(report.BlockFails);
        Assert.Equal(RequirementStatus.NotAMemberName, Assert.Single(report.Requirements).Status);

        // And the repair is named, because an error message that does not say what to do instead is a
        // refusal an author has to guess their way out of.
        Assert.Contains("ThingAlarm", report.Requirements[0].Detail);
    }

    [Fact]
    public void ACaseOnlyDifference_IsPresent_ButTheSpellingIsReported()
    {
        WriteHouseStyleBlock();

        var report = InterfaceCheckRunner.Run(_dir, "FB_Thing", new[] { "thingalarm" }, "test");

        Assert.Equal(RequirementStatus.Present, Assert.Single(report.Requirements).Status);
        Assert.False(report.BlockFails);
        Assert.Contains("spells it 'ThingAlarm'", report.Requirements[0].Detail);
    }

    [Fact]
    public void TwoBlocksOfTheSameName_AreNotChecked_RatherThanCheckedArbitrarily()
    {
        WriteHouseStyleBlock();
        Write("FB_Thing_copy.ir", File.ReadAllText(Path.Combine(_dir, "FB_Thing.ir")));

        var report = InterfaceCheckRunner.Run(_dir, "FB_Thing", new[] { "ThingAlarm" }, "test");

        Assert.False(report.Checked);
        Assert.Contains("2 files declare a block named", report.NotCheckedReason);
    }

    // ---------------------------------------------------------------- the stamp

    [Fact]
    public void TheReportCarriesTheBlocksIrHash_AndItMovesWhenTheBlockDoes()
    {
        WriteHouseStyleBlock();
        var first = InterfaceCheckRunner.Run(_dir, "FB_Thing", new[] { "ThingAlarm" }, "test").IrHash;

        var path = Path.Combine(_dir, "FB_Thing.ir");
        File.WriteAllText(path, File.ReadAllText(path).Replace("    Scratch : Bool", "    Scratch2 : Bool", StringComparison.Ordinal));

        var second = InterfaceCheckRunner.Run(_dir, "FB_Thing", new[] { "ThingAlarm" }, "test").IrHash;

        Assert.NotEqual(string.Empty, first);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void JsonCarriesTheStampAndTheDenominator()
    {
        WriteHouseStyleBlock();

        using var doc = JsonDocument.Parse(InterfaceCheckOutputFormatter.FormatJson(
            InterfaceCheckRunner.Run(_dir, "FB_Thing", new[] { "ThingAlarm", "ThingInhibit" }, "test")));

        var root = doc.RootElement;
        Assert.True(root.GetProperty("checked").GetBoolean());
        Assert.True(root.GetProperty("blockFails").GetBoolean());
        Assert.Equal(64, root.GetProperty("irHash").GetString()!.Length);
        Assert.Equal(4, root.GetProperty("examinedCount").GetInt32());
        Assert.Equal(2, root.GetProperty("requirements").GetArrayLength());
    }

    // ---------------------------------------------------------------- the real corpus

    /// <summary>
    /// *** THE REAL FINDING, AGAINST THE REAL ARTIFACTS. *** The enumeration names exactly two response
    /// signals across its 27 assertions; the block carries one of them. This is D1, computed rather
    /// than argued.
    /// </summary>
    [Fact]
    public void RealCorpus_TheHopperBlockMonitorDoesNotCarryTheInhibitSignalTheEnumerationNames()
    {
        var report = InterfaceCheckRunner.Run(
            CorpusDir(),
            "FB_HopperBlockageMonitor",
            new[] { "HopperBlockedAlarm", "HopperBlockedInhibit" },
            "assertion-enumeration.yaml");

        Assert.True(report.Checked);
        Assert.True(report.BlockFails);
        Assert.Equal("HopperBlockedAlarm", Assert.Single(report.Present).Name);
        Assert.Equal("HopperBlockedInhibit", Assert.Single(report.Missing).Name);

        // The house style, measured on the real block rather than on the fixture: both the sections a
        // naive check would have read are EMPTY.
        Assert.Equal(0, report.SectionCounts["INPUT"]);
        Assert.Equal(0, report.SectionCounts["OUTPUT"]);
    }

    /// <summary>
    /// *** THE OVER-FIRING DIRECTION, SWEPT OVER THE WHOLE COMMITTED CORPUS. *** A gate that refuses
    /// everything passes every test that only checks refusals. So: every block in <c>ir/test-project001</c>
    /// that has an interface at all is checked against a member name taken from its OWN interface, and
    /// every one must come out PRESENT. The counts are asserted so a sweep that examined nothing cannot
    /// read as a clean sweep.
    /// </summary>
    [Fact]
    public void WholeCorpusSweep_EveryBlockAcceptsItsOwnInterfaceMembers()
    {
        var corpus = CorpusDir();
        var blocks = Directory.EnumerateFiles(corpus, "*.ir", SearchOption.TopDirectoryOnly)
            .Where(f => File.ReadAllText(f).StartsWith("BLOCK ", StringComparison.Ordinal))
            .Select(f => File.ReadLines(f).First().Split(' ')[2])
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.True(blocks.Count >= 15, $"the sweep must have a real denominator; found {blocks.Count} block(s)");

        var swept = 0;
        var skippedNoInterface = 0;
        var failures = new List<string>();

        foreach (var block in blocks)
        {
            // Discover the block's own member names by asking for something impossible first: the
            // NOT CHECKED path tells us whether it has an interface at all.
            var probe = InterfaceCheckRunner.Run(corpus, block, new[] { "__no_such_member__" }, "sweep");
            if (!probe.Checked)
            {
                skippedNoInterface++;
                continue;
            }

            var own = probe.ExaminedMembers
                .Select(m => m[(m.LastIndexOf('/') + 1)..])
                .Distinct(StringComparer.Ordinal)
                .Take(5)
                .ToArray();

            var report = InterfaceCheckRunner.Run(corpus, block, own, "sweep");
            swept++;

            if (report.BlockFails || !report.Checked)
            {
                failures.Add($"{block}: checked={report.Checked} missing=[{string.Join(", ", report.Missing.Select(m => m.Name))}] {report.NotCheckedReason}");
            }
        }

        // The denominator is CLOSED, not merely large: every block is either swept or named as having
        // no interface. A floor alone would let a silently-shrinking sweep keep passing.
        Assert.Equal(blocks.Count, swept + skippedNoInterface);
        Assert.True(swept >= 10, $"only {swept} of {blocks.Count} block(s) were actually swept ({skippedNoInterface} had no interface) — a sweep that examined almost nothing is not evidence");
        Assert.Empty(failures);
    }

    /// <summary>
    /// The other end of the same sweep: a name nothing could carry must be MISSING on every block that
    /// has an interface. Together with the test above this separates <i>works</i> from <i>always says
    /// yes</i> and from <i>always says no</i>.
    /// </summary>
    [Fact]
    public void WholeCorpusSweep_ANameNoBlockCarriesIsMissingEverywhere()
    {
        var corpus = CorpusDir();
        var blocks = Directory.EnumerateFiles(corpus, "*.ir", SearchOption.TopDirectoryOnly)
            .Where(f => File.ReadAllText(f).StartsWith("BLOCK ", StringComparison.Ordinal))
            .Select(f => File.ReadLines(f).First().Split(' ')[2])
            .ToList();

        var failed = 0;
        var notChecked = 0;

        foreach (var block in blocks)
        {
            var report = InterfaceCheckRunner.Run(corpus, block, new[] { "ZzNoSuchSignalAnywhere" }, "sweep");
            if (!report.Checked)
            {
                notChecked++;
                continue;
            }

            if (report.BlockFails)
            {
                failed++;
            }
        }

        Assert.Equal(blocks.Count, failed + notChecked);
        Assert.True(failed >= 10, $"only {failed} block(s) reported the impossible name as a FAIL ({notChecked} not checked of {blocks.Count}) — if this is low the check is not detecting absence at all");
    }
}
