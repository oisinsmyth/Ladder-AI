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
        Assert.Equal(expected.Comment, actual.Comment);

        // Recursive since 2026-07-16 (nested anonymous-Struct TYPE members reach the IR layer
        // now) — previously asserted Null unconditionally, which would have masked a nested
        // round-trip loss as a pass for every flat fixture.
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

    // FI-64 (2026-08-09) REPOINTED THIS TEST. It used to assert that a member with Datatype
    // "SomeOtherType" — a NAMED TYPE REFERENCE — hard-errored on nested <Sections>. That was the
    // behaviour, and it was wrong: TIA expands a named type's members inline on re-export, so the
    // rule refused to read back any PLC data type carrying one, which cost the re-export round trip
    // (the only check that has caught defects every other gate passed). FI-56 had already fixed the
    // same shape on the bare-member path; this path was left behind.
    //
    // The test now guards the half that MUST still refuse: an ANONYMOUS structured member, whose
    // nested content is its only definition. Collapsing that would silently discard real members.
    // The named-type collapse is covered in NamedTypeExpansionReadBackTests.
    [Fact]
    public void Parse_AnonymousStructMemberWithNestedSections_HardErrors()
    {
        var xml = XDocument.Parse("""
            <Document>
              <Engineering version="V20" />
              <SW.Types.PlcStruct ID="0">
                <AttributeList>
                  <Interface><Sections xmlns="http://www.siemens.com/automation/Openness/SW/Interface/v5">
              <Section Name="None">
                <Member Name="Nested" Datatype="Struct">
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

    // Member-level Comment on the TYPE/UDT member shape (2026-07-16). The XML shape is mirrored
    // from the one proven Member-level Comment shape (ordinary Static members,
    // FB_PusherControl/FB_ShredderSequencer genuine TIA re-exports) — NOT yet proven on
    // SW.Types.PlcStruct by a live TIA import; see WriteTypeMember's own shape note and
    // src/converter/README.md ("Member-level Comment"). Element order is load-bearing: the real
    // re-exports put <Comment> directly after </AttributeList>, before <StartValue>/nested
    // members, so this asserts the sequence, not just presence.
    [Fact]
    public void Write_TypeMemberComment_EmitsMemberCommentShape()
    {
        var commented = new DbMember("Run", "Bool", Retain: false, StartValue: "FALSE", Comment: "Run command in - written by the sequencer each scan.");
        var type = new PlcTypeSource("0", "TypeMixerIO", null, new[] { commented });

        var written = PlcTypeSourceWriter.Write(type);

        var memberElement = written.Descendants().Single(e => e.Name.LocalName == "Member");
        Assert.Equal(
            new[] { "AttributeList", "Comment", "StartValue" },
            memberElement.Elements().Select(e => e.Name.LocalName));

        var textElement = memberElement.Elements().Single(e => e.Name.LocalName == "Comment").Elements().Single();
        Assert.Equal("MultiLanguageText", textElement.Name.LocalName);
        Assert.Equal("en-US", (string?)textElement.Attribute("Lang"));
        Assert.Equal("Run command in - written by the sequencer each scan.", textElement.Value);
    }

    // Regression for a real silent-loss bug (found 2026-07-16, planning this change):
    // ParseTypeMember never read the Member-level <Comment> at all, so a commented UDT member in
    // source XML silently lost its text crossing to-ir — no error, no marker, just gone.
    [Fact]
    public void Parse_TypeMemberComment_ReadsCommentText()
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
                  <Comment>
                    <MultiLanguageText Lang="en-US">Run command in - written by the sequencer each scan.</MultiLanguageText>
                  </Comment>
                </Member>
              </Section>
            </Sections></Interface>
                  <IsFailsafeCompliant>false</IsFailsafeCompliant>
                  <Name>TypeMixerIO</Name>
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

        var type = PlcTypeSourceParser.Parse(xml);

        var member = Assert.Single(type.Members);
        Assert.Equal("Run command in - written by the sequencer each scan.", member.Comment);
    }

    [Fact]
    public void XmlRoundTrip_TypeMemberComment_IsStable()
    {
        var commented = new DbMember("Run", "Bool", Retain: false, StartValue: null, Comment: "Run command in - written by the sequencer each scan.");
        var plain = new DbMember("StatusWord", "Word", Retain: false, StartValue: null);
        var type = new PlcTypeSource("0", "TypeMixerIO", null, new[] { commented, plain });

        var written = PlcTypeSourceWriter.Write(type);
        var reparsed = PlcTypeSourceParser.Parse(written);

        AssertPlcTypeSourcesEqual(type, reparsed);
    }

    [Fact]
    public void IrRoundTrip_TypeMemberComment_PreservesCommentText()
    {
        var commented = new DbMember("Run", "Bool", Retain: false, StartValue: null, Comment: "Run command in - written by the sequencer each scan.");
        var type = new PlcTypeSource("0", "TypeMixerIO", null, new[] { commented });

        var irText = TypeIrSerializer.Serialize(type);
        Assert.Contains("    Run : Bool COMMENT \"Run command in - written by the sequencer each scan.\"", irText);

        var reparsed = TypeIrParser.ParseType(irText);
        AssertPlcTypeSourcesEqual(type, reparsed);
    }

    // Mirror of BlockInterfaceTests.IrRoundTrip_MemberCommentContainingEqualsSign_...: COMMENT is
    // the absolute last line token specifically so it's peeled before StartValue's leftmost-" = "
    // search — prose containing "=" must not corrupt the start value on the TYPE line grammar
    // either. Also exercises the \" and \\ escaping, which the DB-side sibling doesn't.
    [Fact]
    public void IrRoundTrip_TypeMemberCommentContainingEqualsSign_DoesNotCorruptStartValue()
    {
        var member = new DbMember(
            "SpeedSetpoint", "Real", Retain: false, StartValue: "5.0",
            Comment: "Raw = operator value x 10 - see \"MIX-02\" sheet, folder \\HMI.");
        var type = new PlcTypeSource("0", "TypeMixerIO", null, new[] { member });

        var irText = TypeIrSerializer.Serialize(type);
        var reparsed = TypeIrParser.ParseType(irText);

        var reparsedMember = Assert.Single(reparsed.Members);
        Assert.Equal("5.0", reparsedMember.StartValue);
        Assert.Equal("Raw = operator value x 10 - see \"MIX-02\" sheet, folder \\HMI.", reparsedMember.Comment);
    }

    [Fact]
    public void FullRoundTrip_XmlToIrToXml_WithMemberComments_PreservesEverything()
    {
        var original = PlcTypeSourceParser.Parse(LoadFixture("PlcTypeWithMemberComments.xml"));

        // Self-check: the fixture genuinely carries member comments — a comment-free fixture
        // would make every assertion below vacuously comment-blind.
        Assert.Equal(2, original.Members.Count(m => m.Comment is not null));

        var irText = TypeIrSerializer.Serialize(original);
        var fromIr = TypeIrParser.ParseType(irText);
        var regeneratedXml = PlcTypeSourceWriter.Write(fromIr);
        var reparsedFromXml = PlcTypeSourceParser.Parse(regeneratedXml);

        AssertPlcTypeSourcesEqual(original, reparsedFromXml);
    }

    // The TypeIr text layer was flat until 2026-07-16 even though the XML side had recursed since
    // 2026-07-14 — serialize dropped NestedMembers entirely (silent loss), parse mangled a nested
    // line into a top-level member (see the dedicated regression below). Two levels of nesting
    // plus comments at both depths, so the recursion and the comment token are proven together.
    [Fact]
    public void IrRoundTrip_NestedStructTypeMembers_PreservesStructureAndComments()
    {
        var deepFlag = new DbMember("Flag1", "Bool", Retain: false, StartValue: null, Comment: "Deepest level - two Structs down.");
        var subByte = new DbMember("SubByte", "Struct", Retain: false, StartValue: null, NestedMembers: new[] { deepFlag });
        var flag0 = new DbMember("Flag0", "Bool", Retain: false, StartValue: "TRUE");
        var coms = new DbMember(
            "ComsByte", "Struct", Retain: false, StartValue: null,
            NestedMembers: new[] { flag0, subByte }, Comment: "Anonymous inline struct - packed coms image.");
        var trailingScalar = new DbMember("StatusWord", "Word", Retain: false, StartValue: null);
        var type = new PlcTypeSource("0", "TypeMixerIO", null, new[] { coms, trailingScalar });

        var irText = TypeIrSerializer.Serialize(type);
        var reparsed = TypeIrParser.ParseType(irText);

        AssertPlcTypeSourcesEqual(type, reparsed);
    }

    // Regression for the flat parser's exact failure shape: a 6-space nested line under a 4-space
    // Struct parent was parsed with a fixed 4-space indent, yielding a *top-level* member whose
    // name still carried the extra two spaces ("  Flag1") — silently wrong structure and a
    // corrupted name, not an error.
    [Fact]
    public void Parse_NestedIndentTypeIrLine_DoesNotMangleMemberName()
    {
        var irText = "TYPE TypeMixerIO\n" +
                     "  ROOTID 0\n" +
                     "  MEMBERS\n" +
                     "    ComsByte : Struct\n" +
                     "      Flag1 : Bool\n";

        var type = TypeIrParser.ParseType(irText);

        var coms = Assert.Single(type.Members);
        Assert.Equal("ComsByte", coms.Name);
        var nested = Assert.Single(coms.NestedMembers!);
        Assert.Equal("Flag1", nested.Name);
    }
}
