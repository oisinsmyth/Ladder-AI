using System.Xml.Linq;
using Converter.Ir;
using Converter.Sanitize;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// PLC tag table support — grounded 2026-07-14 against the real "Default tag table"
/// (`station_2/JOB9002_PLC`), found while confirming `PlantAutoControl`'s own exact dependency list
/// (S1 item 26's own follow-on work). Root element `SW.Tags.PlcTagTable`, genuinely simpler and
/// differently-shaped from a DB/UDT member: `DataTypeName`/`ExternalAccessible`/`ExternalVisible`/
/// `ExternalWritable`/`LogicalAddress`/`Name` are plain child elements (not `BooleanAttribute`-
/// wrapped), no `Retain`/`SetPoint`/`StartValue`/nested-structure concept, and each tag's own
/// `ObjectList` carries only a Comment (no Title). A tag's own real "path" as referenced elsewhere
/// is its bare name (a single-component `Access`, never `TableName.TagName`).
///
/// **Deliberately minimal, not a general tag-table framework** (project owner's own call,
/// 2026-07-14): covers only what `PlantAutoControl`'s own 10 real dependency tags need (plain scalar
/// tags, `Word`/`Bool` observed). Explicitly NOT covered — see `src/converter/README.md`'s own
/// "PLC tag table support" section for the full, dated list of intentional gaps.
/// </summary>
public class PlcTagTableTests
{
    private static XDocument LoadFixture(string name) => XDocument.Load(Path.Combine("Fixtures", name));

    private static void AssertPlcTagTableSourcesEqual(PlcTagTableSource expected, PlcTagTableSource actual)
    {
        Assert.Equal(expected.RootUId, actual.RootUId);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Tags.Count, actual.Tags.Count);
        for (var i = 0; i < expected.Tags.Count; i++)
        {
            AssertPlcTagSourcesEqual(expected.Tags[i], actual.Tags[i]);
        }
    }

    private static void AssertPlcTagSourcesEqual(PlcTagSource expected, PlcTagSource actual)
    {
        Assert.Equal(expected.RootUId, actual.RootUId);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.DataTypeName, actual.DataTypeName);
        Assert.Equal(expected.LogicalAddress, actual.LogicalAddress);
        Assert.Equal(expected.ExternalAccessible, actual.ExternalAccessible);
        Assert.Equal(expected.ExternalVisible, actual.ExternalVisible);
        Assert.Equal(expected.ExternalWritable, actual.ExternalWritable);
        Assert.Equal(expected.Comment, actual.Comment);
    }

    [Fact]
    public void Parse_PlcTagTable_ReadsNameAndAllTags()
    {
        var tagTable = PlcTagTableSourceParser.Parse(LoadFixture("PlcTagTableSource.xml"));

        Assert.Equal("DefaultTagTable", tagTable.Name);
        Assert.Equal("0", tagTable.RootUId);
        Assert.Equal(3, tagTable.Tags.Count);

        Assert.Equal("Tag_1", tagTable.Tags[0].Name);
        Assert.Equal("Word", tagTable.Tags[0].DataTypeName);
        Assert.Equal("%IW64", tagTable.Tags[0].LogicalAddress);
        Assert.True(tagTable.Tags[0].ExternalAccessible);
        Assert.True(tagTable.Tags[0].ExternalVisible);
        Assert.True(tagTable.Tags[0].ExternalWritable);
        Assert.Null(tagTable.Tags[0].Comment);

        Assert.Equal("Tag_2", tagTable.Tags[1].Name);
        Assert.Equal("Bool", tagTable.Tags[1].DataTypeName);
        Assert.Equal("%I500.0", tagTable.Tags[1].LogicalAddress);
        Assert.Equal("Some real comment text", tagTable.Tags[1].Comment);

        Assert.Equal("Tag_3", tagTable.Tags[2].Name);
        Assert.Equal("Word", tagTable.Tags[2].DataTypeName);
        Assert.Equal("%QW64", tagTable.Tags[2].LogicalAddress);
    }

    [Fact]
    public void RoundTrip_ParseWriteParse_IsStable()
    {
        var original = PlcTagTableSourceParser.Parse(LoadFixture("PlcTagTableSource.xml"));

        var written = PlcTagTableSourceWriter.Write(original);
        var reparsed = PlcTagTableSourceParser.Parse(written);

        AssertPlcTagTableSourcesEqual(original, reparsed);
    }

    [Fact]
    public void IrRoundTrip_SerializeParse_IsStable()
    {
        var tagTable = PlcTagTableSourceParser.Parse(LoadFixture("PlcTagTableSource.xml"));

        var irText = TagTableIrSerializer.Serialize(tagTable);
        var reparsed = TagTableIrParser.ParseTagTable(irText);

        AssertPlcTagTableSourcesEqual(tagTable, reparsed);
    }

    [Fact]
    public void IrSerializer_WritesReadableTagLines()
    {
        var tagTable = PlcTagTableSourceParser.Parse(LoadFixture("PlcTagTableSource.xml"));

        var irText = TagTableIrSerializer.Serialize(tagTable);

        Assert.Contains("TAGTABLE DefaultTagTable", irText);
        Assert.Contains("  ROOTID 0", irText);
        Assert.Contains("Tag_1 1 : Word @ %IW64 ACCESSIBLE VISIBLE WRITABLE", irText);
        Assert.Contains("Tag_2 4 : Bool @ %I500.0 ACCESSIBLE VISIBLE WRITABLE COMMENT \"Some real comment text\"", irText);
    }

    [Fact]
    public void FullRoundTrip_XmlToIrToXml_PreservesEverything()
    {
        var original = PlcTagTableSourceParser.Parse(LoadFixture("PlcTagTableSource.xml"));

        var irText = TagTableIrSerializer.Serialize(original);
        var fromIr = TagTableIrParser.ParseTagTable(irText);
        var regeneratedXml = PlcTagTableSourceWriter.Write(fromIr);
        var reparsedFromXml = PlcTagTableSourceParser.Parse(regeneratedXml);

        AssertPlcTagTableSourcesEqual(original, reparsedFromXml);
    }

    [Fact]
    public void Sanitize_RenamesTagTableAndTags()
    {
        var tagTable = PlcTagTableSourceParser.Parse(LoadFixture("PlcTagTableSource.xml"));
        var map = new SanitizationMap
        {
            Names = { ["DefaultTagTable"] = "SanitizedTagTable" },
            Tags =
            {
                ["Tag_1"] = "SanitizedTag1",
                ["Tag_2"] = "SanitizedTag2",
                ["Tag_3"] = "SanitizedTag3",
            },
            Comments = { ["Tag_2"] = "Sanitized comment text" },
        };

        var sanitized = Sanitizer.ApplyToTagTable(tagTable, map);

        Assert.Equal("SanitizedTagTable", sanitized.Name);
        Assert.Equal("SanitizedTag1", sanitized.Tags[0].Name);
        Assert.Equal("SanitizedTag2", sanitized.Tags[1].Name);
        Assert.Equal("Sanitized comment text", sanitized.Tags[1].Comment);
        Assert.Equal("SanitizedTag3", sanitized.Tags[2].Name);

        // DataTypeName/LogicalAddress are structural, never sanitized.
        Assert.Equal("Word", sanitized.Tags[0].DataTypeName);
        Assert.Equal("%IW64", sanitized.Tags[0].LogicalAddress);
    }

    [Fact]
    public void Sanitize_MissingMapEntry_ThrowsListingWhatsMissing()
    {
        var tagTable = PlcTagTableSourceParser.Parse(LoadFixture("PlcTagTableSource.xml"));
        var map = new SanitizationMap();

        var ex = Assert.Throws<SanitizationMapException>(() => Sanitizer.ApplyToTagTable(tagTable, map));
        Assert.Contains("DefaultTagTable", ex.Message);
        Assert.Contains("Tag_1", ex.Message);
    }
}
