using System;
using System.Collections.Generic;
using OpennessCli.Cli;
using OpennessCli.Model;
using OpennessCli.Openness;
using Xunit;

namespace OpennessCli.Tests;

/// <summary>
/// The CLI surface and the report for getting CLASSIC HMI tag tables and text lists into a project
/// (2026-08-18).
///
/// These are PC-side checks only. What they cannot answer — whether the document round-trips, and
/// whether <c>ImportOptions.Override</c> replaces or merges — is a question for a real project and is
/// recorded in <c>src/openness-cli/README.md</c> from a measured run, not from these.
/// </summary>
public class HmiClassicTagTextListTests
{
    [Fact]
    public void Parse_Export_HmiTagTable_Succeeds()
    {
        var result = ArgumentParser.Parse(new[] { "export", "P", "--hmitagtable", "Tags_Panel", "--out", "C:\\out.xml" });

        var success = Assert.IsType<ParseResult.ExportSuccess>(result);
        Assert.Equal("Tags_Panel", success.Options.HmiTagTableName);
        Assert.Null(success.Options.TagTableName);
        Assert.Null(success.Options.BlockName);
    }

    [Fact]
    public void Parse_Export_TextList_Succeeds()
    {
        var result = ArgumentParser.Parse(new[] { "export", "P", "--textlist", "List_1", "--out", "C:\\out.xml" });

        var success = Assert.IsType<ParseResult.ExportSuccess>(result);
        Assert.Equal("List_1", success.Options.TextListName);
    }

    /// <summary>
    /// `--tagtable` is the PLC tag table and `--hmitagtable` is the classic HMI one. Two different
    /// compositions on two different devices, and this guard exists so nobody "simplifies" them into
    /// one flag whose meaning depends on which device answered first.
    /// </summary>
    [Fact]
    public void Parse_Export_PlcAndHmiTagTable_AreDifferentSelectors_AndCannotBeCombined()
    {
        var plc = Assert.IsType<ParseResult.ExportSuccess>(
            ArgumentParser.Parse(new[] { "export", "P", "--tagtable", "T", "--out", "C:\\o.xml" }));
        Assert.Equal("T", plc.Options.TagTableName);
        Assert.Null(plc.Options.HmiTagTableName);

        var both = ArgumentParser.Parse(new[]
        {
            "export", "P", "--tagtable", "T", "--hmitagtable", "T", "--out", "C:\\o.xml",
        });
        var failure = Assert.IsType<ParseResult.Failure>(both);
        Assert.Contains("mutually exclusive", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_Import_HmiTags_TakesNoGroup()
    {
        var ok = Assert.IsType<ParseResult.ImportSuccess>(
            ArgumentParser.Parse(new[] { "import", "P", "--hmitags", "C:\\t.xml" }));
        Assert.True(ok.Options.AsHmiTags);
        Assert.False(ok.Options.AsTextLists);
        Assert.Null(ok.Options.GroupPath);

        var withGroup = ArgumentParser.Parse(new[] { "import", "P", "--hmitags", "--group", "D/G", "C:\\t.xml" });
        var failure = Assert.IsType<ParseResult.Failure>(withGroup);
        Assert.Contains("--group does not apply to --hmitags", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_Import_TextLists_TakesNoGroup()
    {
        var ok = Assert.IsType<ParseResult.ImportSuccess>(
            ArgumentParser.Parse(new[] { "import", "P", "--textlists", "C:\\t.xml" }));
        Assert.True(ok.Options.AsTextLists);

        var withGroup = ArgumentParser.Parse(new[] { "import", "P", "--textlists", "--group", "D/G", "C:\\t.xml" });
        Assert.IsType<ParseResult.Failure>(withGroup);
    }

    [Fact]
    public void Parse_Import_HmiKindsAreMutuallyExclusive()
    {
        var result = ArgumentParser.Parse(new[] { "import", "P", "--hmitags", "--textlists", "C:\\t.xml" });
        var failure = Assert.IsType<ParseResult.Failure>(result);
        Assert.Contains("mutually exclusive", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_Import_HmiTags_RequiresAtLeastOneFile()
    {
        Assert.IsType<ParseResult.Failure>(ArgumentParser.Parse(new[] { "import", "P", "--hmitags" }));
    }

    /// <summary>
    /// Every exception this pair can raise must classify to something other than an internal fault.
    /// The `Hmi*` naming already puts most of them under
    /// <c>HmiExceptionsAreClassified_NotLeftAsInternalFaults</c>; this names them individually so a
    /// rename cannot quietly drop one out of that guard's prefix filter.
    /// </summary>
    /// <summary>
    /// The not-found messages must LIST WHAT IS THERE. Nothing in this CLI enumerates classic HMI tag
    /// tables or text lists, so a bare "not found" leaves the caller with no route to the right name —
    /// and TIA's own default table name contains spaces, which is not a name anyone guesses.
    /// </summary>
    [Fact]
    public void NotFoundMessages_ListWhatIsPresent_AndSayWhenNothingIs()
    {
        var tables = new HmiTagTableNotFoundException("Nope", new[] { "Default tag table [P/HMI_RT_1]" });
        Assert.Contains("PRESENT: Default tag table [P/HMI_RT_1]", tables.Message, StringComparison.Ordinal);

        var lists = new HmiTextListNotFoundException("Nope", Array.Empty<string>());
        Assert.Contains("(none", lists.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(typeof(HmiTagTableNotFoundException))]
    [InlineData(typeof(AmbiguousHmiTagTableException))]
    [InlineData(typeof(HmiTextListNotFoundException))]
    [InlineData(typeof(AmbiguousHmiTextListException))]
    [InlineData(typeof(HmiClassicOnlyObjectException))]
    [InlineData(typeof(HmiClassicDeviceNotResolvedException))]
    [InlineData(typeof(HmiClassicImportFailedException))]
    [InlineData(typeof(ScreenNotFoundException))]
    [InlineData(typeof(AmbiguousScreenException))]
    [InlineData(typeof(ScreenExportNotSupportedOnUnifiedException))]
    [InlineData(typeof(ScreenImportFailedException))]
    [InlineData(typeof(ScreenNumberCollisionException))]
    public void ClassicHmiExceptions_AreNotInternalFaults(Type type)
    {
        var instance = (Exception)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);
        Assert.NotEqual(ExitCodes.UnexpectedError, ExitCodes.ForException(instance));
    }

    /// <summary>
    /// A tag count that did not move is a REPORTED finding, not an omission — it is the difference
    /// between "the table was merged into" and "the table was replaced by an identical one".
    /// </summary>
    [Fact]
    public void FormatHmiClassicImport_ReportsBothDenominatorsAndTheDelta()
    {
        var text = OutputFormatter.FormatHmiClassicImport(new HmiClassicImportOutcome(
            "HMI tag table", "Panel/HMI_1", ContainersBefore: 2, ContainersAfter: 2,
            MembersBefore: 40, MembersAfter: 41,
            ReturnedByImport: new[] { "Tags_A" },
            PresentAfter: new[] { "Default tag table", "Tags_A" },
            Files: new[] { "C:\\t.xml" }));

        Assert.Contains("BEFORE 2  ->  AFTER 2", text, StringComparison.Ordinal);
        Assert.Contains("unchanged", text, StringComparison.Ordinal);
        Assert.Contains("BEFORE 40  ->  AFTER 41", text, StringComparison.Ordinal);
        Assert.Contains("+1", text, StringComparison.Ordinal);
        Assert.Contains("PRESENT Tags_A", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Null members print as NOT EXPOSED BY THE API. A zero would be a claim about the project; the
    /// truth is a claim about the API, and the two are different findings.
    /// </summary>
    [Fact]
    public void FormatHmiClassicImport_NullMemberCount_SaysNotExposed_NeverZero()
    {
        var text = OutputFormatter.FormatHmiClassicImport(new HmiClassicImportOutcome(
            "Text list", "Panel/HMI_1", ContainersBefore: 1, ContainersAfter: 2,
            MembersBefore: null, MembersAfter: null,
            ReturnedByImport: new[] { "List_1" },
            PresentAfter: new[] { "List_1", "List_2" },
            Files: new[] { "C:\\t.xml" }));

        Assert.Contains("NOT EXPOSED BY THE API", text, StringComparison.Ordinal);
        Assert.DoesNotContain("BEFORE 0", text, StringComparison.Ordinal);
        Assert.Contains("+1", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// An object <c>Import()</c> named but that a re-read does not find must be visible in the report,
    /// because that is the case the exit code refuses on.
    /// </summary>
    [Fact]
    public void FormatHmiClassicImport_NamesAnObjectThatIsNotThereAfterwards()
    {
        var text = OutputFormatter.FormatHmiClassicImport(new HmiClassicImportOutcome(
            "HMI tag table", "Panel/HMI_1", ContainersBefore: 1, ContainersAfter: 1,
            MembersBefore: 3, MembersAfter: 3,
            ReturnedByImport: new[] { "Ghost" },
            PresentAfter: new[] { "Default tag table" },
            Files: new[] { "C:\\t.xml" }));

        Assert.Contains("ABSENT  Ghost", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The device-resolution refusal must say which of the three situations it is in, because the
    /// correction is different in each: none at all, more than one, or Unified-only.
    /// </summary>
    [Fact]
    public void HmiClassicDeviceNotResolved_StatesWhichSituation()
    {
        var ambiguous = new HmiClassicDeviceNotResolvedException(
            null, new[] { "A/HMI_1", "B/HMI_2" }, Array.Empty<string>());
        Assert.Contains("More than one classic HMI device", ambiguous.Message, StringComparison.Ordinal);

        var unifiedOnly = new HmiClassicDeviceNotResolvedException(
            null, Array.Empty<string>(), new[] { "U/HMI_RT" });
        Assert.Contains("Unified HMI device", unifiedOnly.Message, StringComparison.Ordinal);
        Assert.Contains("U/HMI_RT", unifiedOnly.Message, StringComparison.Ordinal);

        var none = new HmiClassicDeviceNotResolvedException(
            "nope", Array.Empty<string>(), Array.Empty<string>());
        Assert.Contains("no Unified HMI device either", none.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The Unified refusal must name the device and the object. An empty result would read downstream
    /// as "no such object", which is the wrong conclusion about a project that plainly has one.
    /// </summary>
    [Fact]
    public void HmiClassicOnlyObject_NamesTheObjectAndTheUnifiedDevice()
    {
        var ex = new HmiClassicOnlyObjectException("HMI tag table", "Tags_A", "U/HMI_RT");
        Assert.Contains("Tags_A", ex.Message, StringComparison.Ordinal);
        Assert.Contains("U/HMI_RT", ex.Message, StringComparison.Ordinal);
        Assert.Contains("CLASSIC-only", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Usage_MentionsBothNewSelectorsAndBothNewImportFlags()
    {
        var failure = Assert.IsType<ParseResult.Failure>(ArgumentParser.Parse(new[] { "no-such-subcommand" }));
        foreach (var flag in new[] { "--hmitagtable", "--textlist", "--hmitags", "--textlists" })
        {
            Assert.Contains(flag, failure.Message, StringComparison.Ordinal);
        }
    }
}
