using System.Xml.Linq;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

public class DbConverterTests
{
    private static XDocument LoadFixture(string name) => XDocument.Load(Path.Combine("Fixtures", name));

    // DbSource/DbMember's record-generated Equals compares list-typed properties (Members,
    // NestedMembers) by reference, not structurally — List<T> doesn't override Equals. A plain
    // Assert.Equal(dbA, dbB) would fail even for logically-identical DBs, and the same problem
    // recurs one level down for a structured member's own NestedMembers. Compare field-by-field,
    // recursing into NestedMembers explicitly, rather than relying on default record equality.
    private static void AssertDbSourcesEqual(DbSource expected, DbSource actual)
    {
        Assert.Equal(expected.RootUId, actual.RootUId);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Number, actual.Number);
        Assert.Equal(expected.InstanceOfName, actual.InstanceOfName);
        Assert.Equal(expected.Comment, actual.Comment);
        Assert.Equal(expected.Members.Count, actual.Members.Count);
        for (var i = 0; i < expected.Members.Count; i++)
        {
            AssertDbMembersEqual(expected.Members[i], actual.Members[i]);
        }
    }

    private static void AssertDbMembersEqual(DbMember expected, DbMember actual)
    {
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Datatype, actual.Datatype);
        Assert.Equal(expected.Retain, actual.Retain);
        Assert.Equal(expected.StartValue, actual.StartValue);
        Assert.Equal(expected.Version, actual.Version);
        Assert.Equal(expected.SetPoint, actual.SetPoint);

        if (expected.NestedMembers is null)
        {
            Assert.Null(actual.NestedMembers);
            return;
        }

        Assert.NotNull(actual.NestedMembers);
        Assert.Equal(expected.NestedMembers.Count, actual.NestedMembers!.Count);
        for (var i = 0; i < expected.NestedMembers.Count; i++)
        {
            AssertDbMembersEqual(expected.NestedMembers[i], actual.NestedMembers[i]);
        }
    }

    [Fact]
    public void Parse_GlobalDb_ReadsAllMemberShapes()
    {
        var db = DbSourceParser.Parse(LoadFixture("GlobalDbSource.xml"));

        Assert.Equal("RealDbName", db.Name);
        Assert.Equal(7, db.Number);
        Assert.Null(db.InstanceOfName);
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
    public void Parse_InstanceDb_ReadsInstanceOfNameAndMembers()
    {
        var db = DbSourceParser.Parse(LoadFixture("InstanceDbSource.xml"));

        Assert.Equal("SomeInstance", db.Name);
        Assert.Equal("SomeFB", db.InstanceOfName);
        var flag = Assert.Single(db.Members);
        Assert.Equal("Flag", flag.Name);
        Assert.Equal("Bool", flag.Datatype);
        Assert.Null(flag.NestedMembers);
    }

    [Fact]
    public void Parse_TimerStructuredMember_ReadsNestedMembersAndVersion()
    {
        var db = DbSourceParser.Parse(LoadFixture("GlobalDbWithStructuredMember.xml"));

        var timer = Assert.Single(db.Members);
        Assert.Equal("MyTimer", timer.Name);
        Assert.Equal("TON_TIME", timer.Datatype);
        Assert.Equal("1.0", timer.Version);
        Assert.False(timer.Retain);
        Assert.Null(timer.StartValue);
        Assert.NotNull(timer.NestedMembers);
        Assert.Equal(4, timer.NestedMembers!.Count);
        Assert.Equal(new[] { "PT", "ET", "IN", "Q" }, timer.NestedMembers.Select(m => m.Name));
        Assert.All(timer.NestedMembers, m => Assert.Null(m.StartValue));
    }

    [Fact]
    public void Parse_UdtTypedMember_ReadsNestedMembersWithStartValues()
    {
        var db = DbSourceParser.Parse(LoadFixture("GlobalDbWithUdtTypedMember.xml"));

        var io = Assert.Single(db.Members);
        Assert.Equal("IO", io.Name);
        Assert.Equal("\"TypeDOL\"", io.Datatype);
        Assert.True(io.Retain);
        Assert.Null(io.Version);
        Assert.NotNull(io.NestedMembers);
        Assert.Equal(3, io.NestedMembers!.Count);
        Assert.Equal("FALSE", io.NestedMembers[0].StartValue);
        Assert.Equal("FALSE", io.NestedMembers[1].StartValue);
        Assert.Null(io.NestedMembers[2].StartValue);
    }

    [Fact]
    public void Parse_AnonymousStructMember_ReadsNestedMembersWithFullAttributeList()
    {
        var db = DbSourceParser.Parse(LoadFixture("GlobalDbWithAnonymousStructMember.xml"));

        var inputs = Assert.Single(db.Members);
        Assert.Equal("Inputs", inputs.Name);
        Assert.Equal("Struct", inputs.Datatype);
        Assert.True(inputs.Retain);
        Assert.Null(inputs.Version);
        Assert.Null(inputs.StartValue);
        Assert.NotNull(inputs.NestedMembers);
        Assert.Equal(2, inputs.NestedMembers!.Count);

        Assert.Equal("InHand", inputs.NestedMembers[0].Name);
        Assert.Equal("Bool", inputs.NestedMembers[0].Datatype);
        Assert.Null(inputs.NestedMembers[0].StartValue);
        Assert.False(inputs.NestedMembers[0].SetPoint);

        Assert.Equal("RunTime", inputs.NestedMembers[1].Name);
        Assert.Equal("Real", inputs.NestedMembers[1].Datatype);
        Assert.Equal("0.0", inputs.NestedMembers[1].StartValue);
        Assert.True(inputs.NestedMembers[1].SetPoint);
    }

    [Fact]
    public void RoundTrip_AnonymousStructMember_ParseWriteParse_IsStable()
    {
        var original = DbSourceParser.Parse(LoadFixture("GlobalDbWithAnonymousStructMember.xml"));

        var written = DbSourceWriter.Write(original);
        var reparsed = DbSourceParser.Parse(written);

        AssertDbSourcesEqual(original, reparsed);
    }

    [Fact]
    public void IrRoundTrip_AnonymousStructMember_SerializeParse_IsStable()
    {
        var db = DbSourceParser.Parse(LoadFixture("GlobalDbWithAnonymousStructMember.xml"));

        var irText = DbIrSerializer.Serialize(db);
        var reparsed = DbIrParser.ParseDb(irText);

        AssertDbSourcesEqual(db, reparsed);
    }

    [Fact]
    public void Parse_InstanceDbWithBothStructuredMemberKinds_ReadsBoth()
    {
        var db = DbSourceParser.Parse(LoadFixture("InstanceDbWithStructuredMembers.xml"));

        Assert.Equal("MotorDOL", db.InstanceOfName);
        Assert.Equal(2, db.Members.Count);
        Assert.Equal("\"TypeDOL\"", db.Members[0].Datatype);
        Assert.Equal("TON_TIME", db.Members[1].Datatype);
        Assert.Equal("1.0", db.Members[1].Version);
    }

    [Fact]
    public void Parse_InstanceDbWithWrongInstanceOfType_HardErrors()
    {
        var xml = XDocument.Parse("""
            <Document>
              <Engineering version="V20" />
              <SW.Blocks.InstanceDB ID="0">
                <AttributeList>
                  <InstanceOfName>SomeFB</InstanceOfName>
                  <InstanceOfType>FC</InstanceOfType>
                  <Interface><Sections xmlns="http://www.siemens.com/automation/Openness/SW/Interface/v5">
              <Section Name="Static" />
            </Sections></Interface>
                  <Name>SomeInstance</Name>
                  <Namespace />
                  <Number>4</Number>
                  <ProgrammingLanguage>DB</ProgrammingLanguage>
                </AttributeList>
                <ObjectList>
                  <MultilingualText ID="1" CompositionName="Comment">
                    <ObjectList>
                      <MultilingualTextItem ID="2" CompositionName="Items">
                        <AttributeList>
                          <Culture>en-US</Culture>
                          <Text />
                        </AttributeList>
                      </MultilingualTextItem>
                    </ObjectList>
                  </MultilingualText>
                </ObjectList>
              </SW.Blocks.InstanceDB>
            </Document>
            """);

        var ex = Assert.Throws<UnsupportedConstructException>(() => DbSourceParser.Parse(xml));
        Assert.Contains("InstanceOfType", ex.Message);
    }

    [Fact]
    public void Parse_DoublyNestedMember_HardErrors()
    {
        var ex = Assert.Throws<UnsupportedConstructException>(() => DbSourceParser.Parse(LoadFixture("GlobalDbWithDoublyNestedMember.xml")));
        Assert.Contains("Inner", ex.Message);
    }

    [Fact]
    public void Parse_NonNoneNestedSectionName_HardErrors()
    {
        var ex = Assert.Throws<UnsupportedConstructException>(() => DbSourceParser.Parse(LoadFixture("GlobalDbWithNonNoneNestedSection.xml")));
        Assert.Contains("IO", ex.Message);
    }

    [Fact]
    public void Parse_StructuredMemberWithSetPointFalse_ReadsVerbatimNotAssumedDefault()
    {
        // Real counterexample, 2026-07-11 (ConveyorMotor1.RecentStartSignal): a structured (TOF_TIME)
        // member with SetPoint=false disproved the original "structured members always have
        // SetPoint=true" assumption — must be captured verbatim, not inferred from member kind.
        var xml = XDocument.Parse("""
            <Document>
              <Engineering version="V20" />
              <SW.Blocks.GlobalDB ID="0">
                <AttributeList>
                  <Interface><Sections xmlns="http://www.siemens.com/automation/Openness/SW/Interface/v5">
              <Section Name="Static">
                <Member Name="RecentStartSignal" Datatype="TOF_TIME" Version="1.0" Remanence="NonRetain" Accessibility="Public">
                  <AttributeList>
                    <BooleanAttribute Name="ExternalAccessible" SystemDefined="true">true</BooleanAttribute>
                    <BooleanAttribute Name="ExternalVisible" SystemDefined="true">true</BooleanAttribute>
                    <BooleanAttribute Name="ExternalWritable" SystemDefined="true">true</BooleanAttribute>
                    <BooleanAttribute Name="SetPoint" SystemDefined="true">false</BooleanAttribute>
                  </AttributeList>
                  <Sections>
                    <Section Name="None">
                      <Member Name="PT" Datatype="Time" />
                      <Member Name="ET" Datatype="Time" />
                      <Member Name="IN" Datatype="Bool" />
                      <Member Name="Q" Datatype="Bool" />
                    </Section>
                  </Sections>
                </Member>
              </Section>
            </Sections></Interface>
                  <Name>RealDbName</Name>
                  <Namespace />
                  <Number>7</Number>
                  <ProgrammingLanguage>DB</ProgrammingLanguage>
                </AttributeList>
                <ObjectList>
                  <MultilingualText ID="1" CompositionName="Comment">
                    <ObjectList>
                      <MultilingualTextItem ID="2" CompositionName="Items">
                        <AttributeList>
                          <Culture>en-US</Culture>
                          <Text />
                        </AttributeList>
                      </MultilingualTextItem>
                    </ObjectList>
                  </MultilingualText>
                </ObjectList>
              </SW.Blocks.GlobalDB>
            </Document>
            """);

        var db = DbSourceParser.Parse(xml);

        var timer = Assert.Single(db.Members);
        Assert.False(timer.SetPoint);
        Assert.NotNull(timer.NestedMembers);

        var written = DbSourceWriter.Write(db);
        var reparsed = DbSourceParser.Parse(written);
        AssertDbSourcesEqual(db, reparsed);
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
    public void RoundTrip_InstanceDbWithStructuredMembers_ParseWriteParse_IsStable()
    {
        var original = DbSourceParser.Parse(LoadFixture("InstanceDbWithStructuredMembers.xml"));

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
    public void IrRoundTrip_InstanceDbWithStructuredMembers_SerializeParse_IsStable()
    {
        var db = DbSourceParser.Parse(LoadFixture("InstanceDbWithStructuredMembers.xml"));

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
    public void IrSerializer_WritesInstanceOfAndStructuredMemberLines()
    {
        var db = DbSourceParser.Parse(LoadFixture("InstanceDbWithStructuredMembers.xml"));

        var irText = DbIrSerializer.Serialize(db);

        Assert.Contains("DB ConveyorMotor1", irText);
        Assert.Contains("  INSTANCEOF MotorDOL", irText);
        Assert.Contains("    IO : \"TypeDOL\" RETAIN", irText);
        Assert.Contains("      InHand : Bool = FALSE", irText);
        Assert.Contains("      Running : Bool", irText);
        Assert.Contains("    FaultTripTimer2 : TON_TIME VERSION 1.0", irText);
        Assert.Contains("      PT : Time", irText);
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

    [Fact]
    public void FullRoundTrip_InstanceDbWithStructuredMembers_XmlToIrToXml_PreservesEverything()
    {
        var original = DbSourceParser.Parse(LoadFixture("InstanceDbWithStructuredMembers.xml"));

        var irText = DbIrSerializer.Serialize(original);
        var fromIr = DbIrParser.ParseDb(irText);
        var regeneratedXml = DbSourceWriter.Write(fromIr);
        var reparsedFromXml = DbSourceParser.Parse(regeneratedXml);

        AssertDbSourcesEqual(original, reparsedFromXml);
    }
}
