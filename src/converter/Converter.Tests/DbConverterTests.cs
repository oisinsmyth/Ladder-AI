using System.Xml.Linq;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

public class DbConverterTests
{
    private static XDocument LoadFixture(string name) => XDocument.Load(Path.Combine("Fixtures", name));

    // DbSource's record-generated Equals compares Members (List<DbMember>) by reference, not
    // structurally — List<T> doesn't override Equals. Assert.Equal(dbA, dbB) would use that
    // Equals and fail even for logically-identical lists; comparing Members as the top-level
    // argument instead triggers xUnit's own sequence-aware comparison.
    private static void AssertDbSourcesEqual(DbSource expected, DbSource actual)
    {
        Assert.Equal(expected.RootUId, actual.RootUId);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Number, actual.Number);
        Assert.Equal(expected.Comment, actual.Comment);
        Assert.Equal(expected.Members, actual.Members);
    }

    [Fact]
    public void Parse_GlobalDb_ReadsAllMemberShapes()
    {
        var db = DbSourceParser.Parse(LoadFixture("GlobalDbSource.xml"));

        Assert.Equal("RealDbName", db.Name);
        Assert.Equal(7, db.Number);
        Assert.Equal("Real comment mentioning RealSite.", db.Comment);
        Assert.Equal(4, db.Members.Count);

        var flag = db.Members[0];
        Assert.Equal("RealFlag", flag.Name);
        Assert.Equal("Bool", flag.Datatype);
        Assert.False(flag.Retain);
        Assert.Null(flag.StartValue);

        var word = db.Members[1];
        Assert.Equal("RealWord", word.Name);
        Assert.True(word.Retain);
        Assert.Equal("16#0041", word.StartValue);

        var array = db.Members[2];
        Assert.Equal("Array[0..2] of Int", array.Datatype);

        var stringMember = db.Members[3];
        Assert.Equal("'Real Site Name'", stringMember.StartValue);
    }

    [Fact]
    public void Parse_InstanceDb_HardErrors()
    {
        var ex = Assert.Throws<UnsupportedConstructException>(() => DbSourceParser.Parse(LoadFixture("InstanceDbSource.xml")));
        Assert.Contains("Instance DB", ex.Message);
    }

    [Fact]
    public void Parse_StructuredMember_HardErrors()
    {
        var ex = Assert.Throws<UnsupportedConstructException>(() => DbSourceParser.Parse(LoadFixture("GlobalDbWithStructuredMember.xml")));
        Assert.Contains("MyTimer", ex.Message);
    }

    [Fact]
    public void Parse_NonDefaultBooleanAttribute_HardErrors()
    {
        var ex = Assert.Throws<UnsupportedConstructException>(() => DbSourceParser.Parse(LoadFixture("GlobalDbWithNonDefaultAttribute.xml")));
        Assert.Contains("ExternalWritable", ex.Message);
        Assert.Contains("LockedFlag", ex.Message);
    }

    [Fact]
    public void RoundTrip_ParseWriteParse_IsStable()
    {
        var original = DbSourceParser.Parse(LoadFixture("GlobalDbSource.xml"));

        var written = DbSourceWriter.Write(original);
        var reparsed = DbSourceParser.Parse(written);

        AssertDbSourcesEqual(original, reparsed);
    }

    [Fact]
    public void IrRoundTrip_SerializeParse_IsStable()
    {
        var db = DbSourceParser.Parse(LoadFixture("GlobalDbSource.xml"));

        var irText = DbIrSerializer.Serialize(db);
        var reparsed = DbIrParser.ParseDb(irText);

        AssertDbSourcesEqual(db, reparsed);
    }

    [Fact]
    public void IrSerializer_WritesReadableMemberLines()
    {
        var db = DbSourceParser.Parse(LoadFixture("GlobalDbSource.xml"));

        var irText = DbIrSerializer.Serialize(db);

        Assert.Contains("DB RealDbName", irText);
        Assert.Contains("  ROOTID 0", irText);
        Assert.Contains("  NUMBER 7", irText);
        Assert.Contains("  COMMENT \"Real comment mentioning RealSite.\"", irText);
        Assert.Contains("    RealFlag : Bool", irText);
        Assert.Contains("    RealWord : Word RETAIN = 16#0041", irText);
        Assert.Contains("    RealArray : Array[0..2] of Int", irText);
        Assert.Contains("    RealName : String = 'Real Site Name'", irText);
    }

    [Fact]
    public void FullRoundTrip_XmlToIrToXml_PreservesEverything()
    {
        var original = DbSourceParser.Parse(LoadFixture("GlobalDbSource.xml"));

        var irText = DbIrSerializer.Serialize(original);
        var fromIr = DbIrParser.ParseDb(irText);
        var regeneratedXml = DbSourceWriter.Write(fromIr);
        var reparsedFromXml = DbSourceParser.Parse(regeneratedXml);

        AssertDbSourcesEqual(original, reparsedFromXml);
    }
}
