using System.Xml.Linq;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// PLC data type (UDT) support — grounded 2026-07-14 against the real `TypeDOL` (`FB MotorDOL`'s
/// own dependency, found while attempting a full import+compile cycle test for `PlantAutoControl`'s
/// dependency FBs into `SampleProject`). Root element `SW.Types.PlcStruct` — genuinely simpler
/// than a DB (no `Number`, no `InstanceOfName`) but with real, confirmed differences from
/// <see cref="DbSourceParser"/>'s own shape: the Interface has exactly one `Section Name="None"`
/// (not `"Static"`), and each `&lt;Member&gt;` carries no `Remanence`/`Accessibility` attribute on
/// the tag itself (unlike a DB/FB Static member) even though its own `AttributeList` carries the
/// same four `BooleanAttribute`s. `ObjectList` carries both Comment and Title `MultilingualText`
/// elements (a DB's own only carries Comment) — confirmed real, not assumed.
/// </summary>
public class PlcTypeTests
{
    private static XDocument LoadFixture(string name) => XDocument.Load(Path.Combine("Fixtures", name));

    // PlcTypeSource/DbMember's record-generated Equals compares Members by reference, not
    // structurally (same reasoning as DbConverterTests.AssertDbSourcesEqual).
    private static void AssertPlcTypeSourcesEqual(PlcTypeSource expected, PlcTypeSource actual)
    {
        Assert.Equal(expected.RootUId, actual.RootUId);
        Assert.Equal(expected.Name, actual.Name);
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
        Assert.Null(actual.NestedMembers);
    }

    [Fact]
    public void Parse_PlcType_ReadsNameAndAllMembers()
    {
        var type = PlcTypeSourceParser.Parse(LoadFixture("PlcTypeSource.xml"));

        Assert.Equal("TypeDOL", type.Name);
        Assert.Equal("0", type.RootUId);
        Assert.Null(type.Comment);
        Assert.Equal(6, type.Members.Count);

        Assert.Equal("Run", type.Members[0].Name);
        Assert.Equal("Bool", type.Members[0].Datatype);
        Assert.False(type.Members[0].Retain);
        Assert.False(type.Members[0].SetPoint);
        Assert.Null(type.Members[0].StartValue);
        Assert.Null(type.Members[0].NestedMembers);

        Assert.Equal("SpeedSetpoint", type.Members[2].Name);
        Assert.Equal("Real", type.Members[2].Datatype);

        Assert.Equal("RunHours", type.Members[3].Name);
        Assert.Equal("UDInt", type.Members[3].Datatype);

        Assert.Equal("StatusWord", type.Members[4].Name);
        Assert.Equal("Word", type.Members[4].Datatype);

        Assert.Equal("EquipmentName", type.Members[5].Name);
        Assert.Equal("String", type.Members[5].Datatype);
    }

    [Fact]
    public void Parse_FailsafeCompliantType_HardErrors()
    {
        var xml = XDocument.Parse("""
            <Document>
              <Engineering version="V20" />
              <SW.Types.PlcStruct ID="0">
                <AttributeList>
                  <Interface><Sections xmlns="http://www.siemens.com/automation/Openness/SW/Interface/v5">
              <Section Name="None">
                <Member Name="Run" Datatype="Bool">
                  <AttributeList>
                    <BooleanAttribute Name="ExternalAccessible" SystemDefined="true">true</BooleanAttribute>
                    <BooleanAttribute Name="ExternalVisible" SystemDefined="true">true</BooleanAttribute>
                    <BooleanAttribute Name="ExternalWritable" SystemDefined="true">true</BooleanAttribute>
                    <BooleanAttribute Name="SetPoint" SystemDefined="true">false</BooleanAttribute>
                  </AttributeList>
                </Member>
              </Section>
            </Sections></Interface>
                  <IsFailsafeCompliant>true</IsFailsafeCompliant>
                  <Name>SomeType</Name>
                  <Namespace />
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
              </SW.Types.PlcStruct>
            </Document>
            """);

        var ex = Assert.Throws<UnsupportedConstructException>(() => PlcTypeSourceParser.Parse(xml));
        Assert.Contains("hard rule 2", ex.Message);
    }

    [Fact]
    public void Parse_NonNoneSectionName_HardErrors()
    {
        var xml = XDocument.Parse("""
            <Document>
              <Engineering version="V20" />
              <SW.Types.PlcStruct ID="0">
                <AttributeList>
                  <Interface><Sections xmlns="http://www.siemens.com/automation/Openness/SW/Interface/v5">
              <Section Name="Static">
                <Member Name="Run" Datatype="Bool">
                  <AttributeList>
                    <BooleanAttribute Name="ExternalAccessible" SystemDefined="true">true</BooleanAttribute>
                    <BooleanAttribute Name="ExternalVisible" SystemDefined="true">true</BooleanAttribute>
                    <BooleanAttribute Name="ExternalWritable" SystemDefined="true">true</BooleanAttribute>
                    <BooleanAttribute Name="SetPoint" SystemDefined="true">false</BooleanAttribute>
                  </AttributeList>
                </Member>
              </Section>
            </Sections></Interface>
                  <IsFailsafeCompliant>false</IsFailsafeCompliant>
                  <Name>SomeType</Name>
                  <Namespace />
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
              </SW.Types.PlcStruct>
            </Document>
            """);

        var ex = Assert.Throws<UnsupportedConstructException>(() => PlcTypeSourceParser.Parse(xml));
        Assert.Contains("Static", ex.Message);
    }

    [Fact]
    public void Parse_MemberWithNestedSections_HardErrors()
    {
        var xml = XDocument.Parse("""
            <Document>
              <Engineering version="V20" />
              <SW.Types.PlcStruct ID="0">
                <AttributeList>
                  <Interface><Sections xmlns="http://www.siemens.com/automation/Openness/SW/Interface/v5">
              <Section Name="None">
                <Member Name="Nested" Datatype="&quot;SomeOtherType&quot;">
                  <AttributeList>
                    <BooleanAttribute Name="ExternalAccessible" SystemDefined="true">true</BooleanAttribute>
                    <BooleanAttribute Name="ExternalVisible" SystemDefined="true">true</BooleanAttribute>
                    <BooleanAttribute Name="ExternalWritable" SystemDefined="true">true</BooleanAttribute>
                    <BooleanAttribute Name="SetPoint" SystemDefined="true">false</BooleanAttribute>
                  </AttributeList>
                  <Sections>
                    <Section Name="None">
                      <Member Name="Inner" Datatype="Bool" />
                    </Section>
                  </Sections>
                </Member>
              </Section>
            </Sections></Interface>
                  <IsFailsafeCompliant>false</IsFailsafeCompliant>
                  <Name>SomeType</Name>
                  <Namespace />
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
              </SW.Types.PlcStruct>
            </Document>
            """);

        var ex = Assert.Throws<UnsupportedConstructException>(() => PlcTypeSourceParser.Parse(xml));
        Assert.Contains("Nested", ex.Message);
    }

    [Fact]
    public void RoundTrip_ParseWriteParse_IsStable()
    {
        var original = PlcTypeSourceParser.Parse(LoadFixture("PlcTypeSource.xml"));

        var written = PlcTypeSourceWriter.Write(original);
        var reparsed = PlcTypeSourceParser.Parse(written);

        AssertPlcTypeSourcesEqual(original, reparsed);
    }

    [Fact]
    public void IrRoundTrip_SerializeParse_IsStable()
    {
        var type = PlcTypeSourceParser.Parse(LoadFixture("PlcTypeSource.xml"));

        var irText = TypeIrSerializer.Serialize(type);
        var reparsed = TypeIrParser.ParseType(irText);

        AssertPlcTypeSourcesEqual(type, reparsed);
    }

    [Fact]
    public void IrSerializer_WritesReadableMemberLines()
    {
        var type = PlcTypeSourceParser.Parse(LoadFixture("PlcTypeSource.xml"));

        var irText = TypeIrSerializer.Serialize(type);

        Assert.Contains("TYPE TypeDOL", irText);
        Assert.Contains("  ROOTID 0", irText);
        Assert.DoesNotContain("NUMBER", irText);
        Assert.DoesNotContain("INSTANCEOF", irText);
        Assert.Contains("    Run : Bool", irText);
        Assert.Contains("    SpeedSetpoint : Real", irText);
        Assert.Contains("    RunHours : UDInt", irText);
        Assert.Contains("    StatusWord : Word", irText);
        Assert.Contains("    EquipmentName : String", irText);
    }

    [Fact]
    public void FullRoundTrip_XmlToIrToXml_PreservesEverything()
    {
        var original = PlcTypeSourceParser.Parse(LoadFixture("PlcTypeSource.xml"));

        var irText = TypeIrSerializer.Serialize(original);
        var fromIr = TypeIrParser.ParseType(irText);
        var regeneratedXml = PlcTypeSourceWriter.Write(fromIr);
        var reparsedFromXml = PlcTypeSourceParser.Parse(regeneratedXml);

        AssertPlcTypeSourcesEqual(original, reparsedFromXml);
    }

    [Fact]
    public void Sanitize_RenamesTypeAndMembers()
    {
        var type = PlcTypeSourceParser.Parse(LoadFixture("PlcTypeSource.xml"));
        var map = new Converter.Sanitize.SanitizationMap
        {
            Names = { ["TypeDOL"] = "MotorIOSet" },
            Tags =
            {
                ["MotorIOSet.Run"] = "MotorIOSet.Run",
                ["MotorIOSet.FaultActive"] = "MotorIOSet.FaultActive",
                ["TypeDOL.SpeedSetpoint"] = "MotorIOSet.SpeedSetpoint",
                ["TypeDOL.RunHours"] = "MotorIOSet.RunHours",
                ["TypeDOL.StatusWord"] = "MotorIOSet.StatusWord",
                ["TypeDOL.EquipmentName"] = "MotorIOSet.EquipmentName",
            },
        };

        var sanitized = Converter.Sanitize.Sanitizer.ApplyToType(type, map);

        Assert.Equal("MotorIOSet", sanitized.Name);
        Assert.Equal("Run", sanitized.Members[0].Name);
    }

    [Fact]
    public void Sanitize_MissingMapEntry_ThrowsListingWhatsMissing()
    {
        var type = PlcTypeSourceParser.Parse(LoadFixture("PlcTypeSource.xml"));
        var map = new Converter.Sanitize.SanitizationMap();

        var ex = Assert.Throws<Converter.Sanitize.SanitizationMapException>(() => Converter.Sanitize.Sanitizer.ApplyToType(type, map));
        Assert.Contains("TypeDOL", ex.Message);
    }
}
