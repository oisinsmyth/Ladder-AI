using System.Xml.Linq;
using Converter.Sanitize;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

public class SanitizerTests
{
    private static BlockSource LoadFixture(string name)
    {
        var document = XDocument.Load(Path.Combine("Fixtures", name));
        return BlockSourceParser.Parse(document);
    }

    private static SanitizationMap FullMap() => new()
    {
        Names = new Dictionary<string, string> { ["RealBlockName"] = "SanitizedBlockName" },
        Comments = new Dictionary<string, string> { ["RealBlockName"] = "Sanitized block comment." },
        NetworkComments = new Dictionary<string, string> { ["RealBlockName#1"] = "Sanitized network comment." },
        Tags = new Dictionary<string, string>
        {
            ["RealProcess.RealTag"] = "SanitizedProcess.SanitizedTag",
            ["RealOutput"] = "SanitizedOutput",
        },
    };

    [Fact]
    public void Apply_WithCompleteMap_RenamesEverythingIdentifying()
    {
        var block = LoadFixture("SanitizeSource.xml");

        var sanitized = Sanitizer.Apply(block, FullMap());

        Assert.Equal("SanitizedBlockName", sanitized.Name);
        Assert.Equal("Sanitized block comment.", sanitized.Comment);
        Assert.Equal("Sanitized network comment.", sanitized.CompileUnits[0].Comment);

        var accessPaths = sanitized.CompileUnits[0].Network.AccessNodes.Select(a => a.DottedPath).ToList();
        Assert.Contains("SanitizedProcess.SanitizedTag", accessPaths);
        Assert.Contains("SanitizedOutput", accessPaths);
        Assert.DoesNotContain(accessPaths, p => p.Contains("Real", StringComparison.Ordinal));
    }

    [Fact]
    public void Apply_PreservesStructuralData_UIdsAndNumber()
    {
        var block = LoadFixture("SanitizeSource.xml");

        var sanitized = Sanitizer.Apply(block, FullMap());

        Assert.Equal(block.RootUId, sanitized.RootUId);
        Assert.Equal(block.Number, sanitized.Number);
        Assert.Equal(block.Kind, sanitized.Kind);
        Assert.Equal(block.CompileUnits[0].UId, sanitized.CompileUnits[0].UId);
    }

    [Fact]
    public void Apply_MissingTagMapping_HardErrorsListingIt()
    {
        var block = LoadFixture("SanitizeSource.xml");
        var map = FullMap();
        map.Tags.Remove("RealOutput");

        var ex = Assert.Throws<SanitizationMapException>(() => Sanitizer.Apply(block, map));
        Assert.Contains("RealOutput", ex.Message);
    }

    [Fact]
    public void Apply_MissingBlockName_HardErrors()
    {
        var block = LoadFixture("SanitizeSource.xml");
        var map = FullMap();
        map.Names.Clear();

        var ex = Assert.Throws<SanitizationMapException>(() => Sanitizer.Apply(block, map));
        Assert.Contains("Names[\"RealBlockName\"]", ex.Message);
    }

    [Fact]
    public void Apply_MissingNetworkComment_HardErrors()
    {
        var block = LoadFixture("SanitizeSource.xml");
        var map = FullMap();
        map.NetworkComments.Clear();

        var ex = Assert.Throws<SanitizationMapException>(() => Sanitizer.Apply(block, map));
        Assert.Contains("NetworkComments", ex.Message);
    }

    [Fact]
    public void Apply_CollectsAllMissingEntriesInOneError()
    {
        var block = LoadFixture("SanitizeSource.xml");
        var map = new SanitizationMap();

        var ex = Assert.Throws<SanitizationMapException>(() => Sanitizer.Apply(block, map));
        Assert.Contains("Names[\"RealBlockName\"]", ex.Message);
        Assert.Contains("Comments[\"RealBlockName\"]", ex.Message);
        Assert.Contains("NetworkComments[\"RealBlockName#1\"]", ex.Message);
        Assert.Contains("RealProcess.RealTag", ex.Message);
        Assert.Contains("RealOutput", ex.Message);
    }

    private static DbSource LoadDbFixture(string name)
    {
        var document = XDocument.Load(Path.Combine("Fixtures", name));
        return DbSourceParser.Parse(document);
    }

    private static SanitizationMap FullDbMap() => new()
    {
        Names = new Dictionary<string, string> { ["RealDbName"] = "SanitizedDbName" },
        Comments = new Dictionary<string, string> { ["RealDbName"] = "Sanitized DB comment." },
        Tags = new Dictionary<string, string>
        {
            ["RealDbName.RealFlag"] = "SanitizedDbName.SanitizedFlag",
            ["RealDbName.RealWord"] = "SanitizedDbName.SanitizedWord",
            ["RealDbName.RealArray"] = "SanitizedDbName.SanitizedArray",
            ["RealDbName.RealName"] = "SanitizedDbName.SanitizedName",
        },
        StartValues = new Dictionary<string, string>
        {
            ["RealDbName.RealName"] = "'Sanitized Site Name'",
        },
    };

    [Fact]
    public void ApplyToDb_WithCompleteMap_RenamesEverythingIdentifying()
    {
        var db = LoadDbFixture("GlobalDbSource.xml");

        var sanitized = Sanitizer.ApplyToDb(db, FullDbMap());

        Assert.Equal("SanitizedDbName", sanitized.Name);
        Assert.Equal("Sanitized DB comment.", sanitized.Comment);

        var memberNames = sanitized.Members.Select(m => m.Name).ToList();
        Assert.Equal(new[] { "SanitizedFlag", "SanitizedWord", "SanitizedArray", "SanitizedName" }, memberNames);

        var nameMember = sanitized.Members.Single(m => m.Name == "SanitizedName");
        Assert.Equal("'Sanitized Site Name'", nameMember.StartValue);
    }

    [Fact]
    public void ApplyToDb_PreservesStructuralData()
    {
        var db = LoadDbFixture("GlobalDbSource.xml");

        var sanitized = Sanitizer.ApplyToDb(db, FullDbMap());

        Assert.Equal(db.RootUId, sanitized.RootUId);
        Assert.Equal(db.Number, sanitized.Number);
        Assert.Equal(db.Members[0].Datatype, sanitized.Members[0].Datatype);
        Assert.Equal(db.Members[1].Retain, sanitized.Members[1].Retain);
        // Non-string start value (hex word) is structural, never mapped.
        Assert.Equal("16#0041", sanitized.Members[1].StartValue);
        Assert.Equal(db.Members[2].Datatype, sanitized.Members[2].Datatype);
    }

    [Fact]
    public void ApplyToDb_MissingStringStartValueMapping_HardErrors()
    {
        var db = LoadDbFixture("GlobalDbSource.xml");
        var map = FullDbMap();
        map.StartValues.Remove("RealDbName.RealName");

        var ex = Assert.Throws<SanitizationMapException>(() => Sanitizer.ApplyToDb(db, map));
        Assert.Contains("StartValues[\"RealDbName.RealName\"]", ex.Message);
    }

    [Fact]
    public void ApplyToDb_MissingMemberTagMapping_HardErrors()
    {
        var db = LoadDbFixture("GlobalDbSource.xml");
        var map = FullDbMap();
        map.Tags.Remove("RealDbName.RealFlag");

        var ex = Assert.Throws<SanitizationMapException>(() => Sanitizer.ApplyToDb(db, map));
        Assert.Contains("RealDbName.RealFlag", ex.Message);
    }
}
