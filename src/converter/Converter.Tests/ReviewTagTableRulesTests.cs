using Converter.Ir;
using Converter.Review;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// 2026-08-13. `converter review` against a TAG TABLE exited 0 while all 18 mechanized rules
/// reported "not applicable — TAGTABLE rule support not implemented in Phase 1": the file was
/// effectively unreviewed and the exit code and summary were indistinguishable from a clean review.
/// A tag table is where tag NAMES live, so it is the file kind C-001 applies to most, not least.
///
/// Every rule below is tested BOTH ways — a fixture that violates it and one that conforms. A rule
/// only ever exercised against conforming input has not been tested; it would pass just as happily
/// if its body were `yield break`, which is exactly the state this file exists to end.
///
/// Fixtures are shaped after the real corpus (`ir/test-project001/DefaultTagTable.ir`): C-001
/// physical-IO names with the equipment token in the MIDDLE (`DQ3_PSH_RunPowerPack`), TIA's
/// auto-generated `Tag_41` placeholders at real input addresses, `AirStarWord0IN`-style comms words
/// at `%IW`, and the two vendor defaults C-007 names by hand (`Clock_0.5Hz`, `Default tag table`).
/// </summary>
public class ReviewTagTableRulesTests
{
    private static PlcTagSource Tag(string name, string type, string address) =>
        new("1", name, type, address, true, true, true, null);

    private static IReadOnlyList<Finding> Review(string tableName, params PlcTagSource[] tags)
    {
        var table = new PlcTagTableSource("0", tableName, tags);
        return Rules.CheckC001TagNames(table)
            .Concat(Rules.CheckC005TagTableCharset(table))
            .Concat(Rules.CheckC406TagDataTypes(table))
            .ToList();
    }

    // ---- C-001, layer (a): the physical-IO name format ------------------------------------------

    [Theory]
    [InlineData("DI3_FCC_RunFb", "%I0.4")]        // doc 06's own worked example
    [InlineData("DQ3_PSH_RunPowerPack", "%Q0.2")] // equipment token in the middle, real corpus shape
    [InlineData("DI22_SPR_7", "%I2.7")]           // two-digit point number, numeric signal field
    [InlineData("AI2_TNK_Level", "%IW64")]        // analog input at a word address
    [InlineData("AQ1_VSD_SpeedRef", "%QW66")]     // analog output at a word address
    public void CheckC001TagNames_ConformingPhysicalIoTag_NoFinding(string name, string address)
    {
        Assert.DoesNotContain(Review("Tags", Tag(name, "Bool", address)), f => f.RuleId == "C-001");
    }

    [Theory]
    [InlineData("Tag_41", "%IW508")]          // TIA's auto-generated placeholder, real in the corpus
    [InlineData("AirStarWord0IN", "%IW80")]   // a comms word at a physical input address
    [InlineData("StartPB", "%I0.0")]          // a plausible-looking name that is not the format
    [InlineData("DI_FCC_RunFb", "%I0.4")]     // no point number
    [InlineData("DI3FCCRunFb", "%I0.4")]      // no field separators
    [InlineData("DI3_FCC", "%I0.4")]          // only one field after the number - no signal
    [InlineData("DI3_FCC_", "%I0.4")]         // trailing empty field
    [InlineData("XX3_FCC_RunFb", "%I0.4")]    // not one of DI/DQ/AI/AQ
    public void CheckC001TagNames_PhysicalIoTagOutsideTheFormat_FlagsError(string name, string address)
    {
        var findings = Review("Tags", Tag(name, "Bool", address));

        var finding = Assert.Single(findings, f => f.RuleId == "C-001");
        Assert.Equal(FindingSeverity.Error, finding.Severity);
        Assert.Contains(name, finding.Description);
        Assert.Contains("physical-IO", finding.Description);
    }

    // The cross-check a name alone could never give: the address sits right next to it.
    [Theory]
    [InlineData("DQ3_PSH_Run", "%I0.2")]  // names itself an output, addressed at an input
    [InlineData("DI3_PSH_Run", "%Q0.2")]  // names itself an input, addressed at an output
    [InlineData("AQ1_VSD_Ref", "%IW64")]
    public void CheckC001TagNames_DirectionDisagreesWithAddress_FlagsError(string name, string address)
    {
        var findings = Review("Tags", Tag(name, "Bool", address));

        Assert.Contains(findings, f => f.RuleId == "C-001"
            && f.Severity == FindingSeverity.Error
            && f.Description.Contains("disagree"));
    }

    [Theory]
    [InlineData("DI3_FCC_Speed", "%IW64")]  // a digital point named onto a word
    [InlineData("AI3_FCC_Level", "%I0.4")]  // an analog point named onto a single bit
    public void CheckC001TagNames_WidthDisagreesWithAddress_FlagsError(string name, string address)
    {
        var findings = Review("Tags", Tag(name, "Bool", address));

        Assert.Contains(findings, f => f.RuleId == "C-001"
            && f.Severity == FindingSeverity.Error
            && (f.Description.Contains("single bit") || f.Description.Contains("byte/word/dword")));
    }

    // A width that cannot be established is not guessed at — `%I5` carries no size letter and no
    // bit offset, so only the direction is cross-checked. Guessing here would manufacture a finding.
    [Fact]
    public void CheckC001TagNames_UnclassifiableWidth_ChecksDirectionOnly()
    {
        Assert.DoesNotContain(Review("Tags", Tag("DI3_FCC_RunFb", "Bool", "%I5")), f => f.RuleId == "C-001");

        Assert.Contains(Review("Tags", Tag("DQ3_FCC_RunFb", "Bool", "%I5")),
            f => f.RuleId == "C-001" && f.Description.Contains("disagree"));
    }

    // ---- C-001, layer (b): non-process-image tags are variables ----------------------------------

    [Theory]
    [InlineData("FirstScan", "%M1.0")]
    [InlineData("AlwaysTrue", "%M1.2")]
    [InlineData("CycleActive", "%M10.0")]
    public void CheckC001TagNames_ConformingFlagTag_NoFinding(string name, string address)
    {
        Assert.DoesNotContain(Review("Tags", Tag(name, "Bool", address)), f => f.RuleId == "C-001");
    }

    [Theory]
    [InlineData("cycle_active", "%M10.0")]
    [InlineData("HMI_Cmd_Start", "%M10.1")]
    [InlineData("1stScan", "%M10.2")]
    public void CheckC001TagNames_NonPascalCaseFlagTag_FlagsError(string name, string address)
    {
        var finding = Assert.Single(Review("Tags", Tag(name, "Bool", address)), f => f.RuleId == "C-001");
        Assert.Equal(FindingSeverity.Error, finding.Severity);
        Assert.Contains("variables layer", finding.Description);
    }

    // ---- C-007: the vendor-default exception, REPORTED rather than silently applied ---------------

    [Fact]
    public void CheckC001TagNames_VendorDefaultClockBit_ReportedAsInfoNotError()
    {
        var findings = Review("Tags", Tag("Clock_0.5Hz", "Bool", "%M0.7"));

        var c001 = Assert.Single(findings, f => f.RuleId == "C-001");
        Assert.Equal(FindingSeverity.Info, c001.Severity);
        Assert.Contains("C-007", c001.Description);

        var c005 = Assert.Single(findings, f => f.RuleId == "C-005");
        Assert.Equal(FindingSeverity.Info, c005.Severity);
        Assert.Contains("C-007", c005.Description);
    }

    // The exception must not be a hole. `Tag_1` is TIA's auto-generated placeholder, not a
    // vendor-supplied system object — an unnamed tag is exactly what a naming review should catch —
    // and a project tag merely SHAPED like a clock bit is not covered either.
    [Theory]
    [InlineData("Tag_1", "%M10.0")]
    [InlineData("Clock_Enable", "%M10.1")]   // Clock_ prefix, but no frequency and no Hz
    [InlineData("Motor_1Hz", "%M10.2")]      // ends in Hz, but is not a clock bit
    public void CheckC001TagNames_NotAVendorDefault_StillFlagsError(string name, string address)
    {
        var finding = Assert.Single(Review("Tags", Tag(name, "Bool", address)), f => f.RuleId == "C-001");
        Assert.Equal(FindingSeverity.Error, finding.Severity);
    }

    // ---- C-005: charset, including the table's own name ------------------------------------------

    [Fact]
    public void CheckC005TagTableCharset_ConformingNames_NoFinding()
    {
        Assert.DoesNotContain(
            Review("IO_Unit1", Tag("DI3_FCC_RunFb", "Bool", "%I0.4")),
            f => f.RuleId == "C-005");
    }

    [Theory]
    [InlineData("DI3_FCC_Run-Fb")]
    [InlineData("DI3_FCC_Run Fb")]
    [InlineData("3DI_FCC_RunFb")]
    public void CheckC005TagTableCharset_BadTagCharset_FlagsError(string name)
    {
        var finding = Assert.Single(Review("Tags", Tag(name, "Bool", "%I0.4")), f => f.RuleId == "C-005");
        Assert.Equal(FindingSeverity.Error, finding.Severity);
    }

    [Fact]
    public void CheckC005TagTableCharset_BadTableName_FlagsError()
    {
        var finding = Assert.Single(
            Review("Unit 1 tags", Tag("DI3_FCC_RunFb", "Bool", "%I0.4")),
            f => f.RuleId == "C-005");

        Assert.Equal(FindingSeverity.Error, finding.Severity);
        Assert.Contains("Tag table name", finding.Description);
    }

    // `Default tag table` is named verbatim in C-007 as tolerated — but it is REPORTED as tolerated,
    // not passed over in silence, and the severity keeps it out of the exit-code gate.
    [Fact]
    public void CheckC005TagTableCharset_VendorDefaultTableName_ReportedAsInfoNotError()
    {
        var finding = Assert.Single(
            Review("Default tag table", Tag("DI3_FCC_RunFb", "Bool", "%I0.4")),
            f => f.RuleId == "C-005");

        Assert.Equal(FindingSeverity.Info, finding.Severity);
        Assert.Contains("C-007", finding.Description);
    }

    // ---- C-406: the declaration form, against a tag's own data type -------------------------------

    [Theory]
    [InlineData("TOF_TIME")]
    [InlineData("TONR_TIME")]
    public void CheckC406TagDataTypes_ForbiddenTimerType_FlagsError(string dataType)
    {
        var finding = Assert.Single(Review("Tags", Tag("DwellTimer", dataType, "%M20.0")), f => f.RuleId == "C-406");
        Assert.Equal(FindingSeverity.Error, finding.Severity);
    }

    [Theory]
    [InlineData("Bool")]
    [InlineData("Word")]
    [InlineData("TON_TIME")]
    public void CheckC406TagDataTypes_PermittedType_NoFinding(string dataType)
    {
        Assert.DoesNotContain(Review("Tags", Tag("DwellTimer", dataType, "%M20.0")), f => f.RuleId == "C-406");
    }

    // ---- The real corpus shape, end to end through the runner ------------------------------------

    // A tag table modelled on ir/test-project001/DefaultTagTable.ir. The point is the SHAPE of the
    // outcome: the three checked rules run, nothing is left unjudged, and the conforming physical-IO
    // tags are silent while the placeholder and the comms word are not.
    [Fact]
    public void ReviewFiles_RealisticTagTable_ChecksNamingAndLeavesNothingUnjudged()
    {
        var table = new PlcTagTableSource("0", "Default tag table", new[]
        {
            Tag("Tag_41", "Word", "%IW508"),
            Tag("AirStarWord0IN", "Word", "%IW80"),
            Tag("Clock_0.5Hz", "Bool", "%M0.7"),
            Tag("FirstScan", "Bool", "%M1.0"),
            Tag("DI1_SYS_ControlHealthy", "Bool", "%I0.0"),
            Tag("DQ3_PSH_RunPowerPack", "Bool", "%Q0.2"),
        });

        var path = Path.Combine(Path.GetTempPath(), $"review-tagtable-{Guid.NewGuid():N}.ir");
        File.WriteAllText(path, TagTableIrSerializer.Serialize(table));

        try
        {
            var report = ReviewRunner.ReviewFiles(new[] { path }, ignoreErrors: false);
            var file = Assert.Single(report.Files);

            Assert.Equal("Default tag table", file.BlockName);

            foreach (var id in new[] { "C-001", "C-005", "C-406" })
            {
                Assert.Contains(file.RuleStatuses, s => s.RuleId == id && s.Status == RuleCheckStatus.Checked);
            }

            // The two genuine C-001 defects, and only those two at error severity.
            var errors = file.Findings.Where(f => f.Severity == FindingSeverity.Error).ToList();
            Assert.Equal(2, errors.Count);
            Assert.Contains(errors, f => f.Description.Contains("Tag_41"));
            Assert.Contains(errors, f => f.Description.Contains("AirStarWord0IN"));

            // Nothing left unjudged: the whole file is now genuinely reviewed, so the run gates on
            // its findings rather than on a gap.
            Assert.Empty(ReviewOutcome.UncheckedRules(report));
            Assert.Equal(ReviewOutcome.Findings, ReviewOutcome.ExitCode(report, allowUnchecked: false));
        }
        finally
        {
            File.Delete(path);
        }
    }

    // The regression this whole file guards: the pre-2026-08-13 run over a tag table reported all 18
    // rules "not applicable" and exited 0. Whatever else changes, a fully-conforming tag table must
    // exit 0 *because it was checked*, not because nothing ran — so at least one rule must be
    // Checked and no rule may be Skipped.
    [Fact]
    public void ReviewFiles_CleanTagTable_ExitsZeroWithRulesActuallyChecked()
    {
        var table = new PlcTagTableSource("0", "IO_Unit1", new[]
        {
            Tag("DI1_SYS_ControlHealthy", "Bool", "%I0.0"),
            Tag("DQ3_PSH_RunPowerPack", "Bool", "%Q0.2"),
        });

        var path = Path.Combine(Path.GetTempPath(), $"review-tagtable-{Guid.NewGuid():N}.ir");
        File.WriteAllText(path, TagTableIrSerializer.Serialize(table));

        try
        {
            var report = ReviewRunner.ReviewFiles(new[] { path }, ignoreErrors: false);
            var file = Assert.Single(report.Files);

            Assert.Empty(file.Findings);
            Assert.Empty(ReviewOutcome.UncheckedRules(report));
            Assert.Equal(ReviewOutcome.Clean, ReviewOutcome.ExitCode(report, allowUnchecked: false));
            Assert.Contains(file.RuleStatuses, s => s.Status == RuleCheckStatus.Checked);
            Assert.DoesNotContain(file.RuleStatuses, s => s.Status == RuleCheckStatus.Skipped);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
