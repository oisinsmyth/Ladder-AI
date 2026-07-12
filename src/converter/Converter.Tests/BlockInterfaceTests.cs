using System.Xml.Linq;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// FB Static/Temp Interface support (S1 item 7 Phase B, 2026-07-11) — grounded against real
/// exports (`MotorDOL`: a UDT-typed Static member with StartValue-bearing nested members, a
/// TON_TIME Static member, and a bare Temp member). Member-level parsing/writing is the same
/// <see cref="DbInterfaceMembers"/> code path DBs use — these tests focus on the block-level
/// wiring (Static absent vs. present-and-empty, Temp always present, Input/Output/InOut/Constant/
/// Return still hard-erroring on real content).
/// </summary>
public class BlockInterfaceTests
{
    private static XDocument LoadFixture(string name) => XDocument.Load(Path.Combine("Fixtures", name));

    [Fact]
    public void Parse_FcWithNoInterface_HasNullStaticAndEmptyTemp()
    {
        var block = BlockSourceParser.Parse(LoadFixture("SanitizeSource.xml"));

        Assert.Null(block.StaticMembers);
        Assert.Empty(block.TempMembers);
    }

    [Fact]
    public void Parse_FbWithStaticAndTemp_ReadsBothStructuredMemberKindsAndBareTempMember()
    {
        var block = BlockSourceParser.Parse(LoadFixture("FbWithStaticAndTemp.xml"));

        Assert.NotNull(block.StaticMembers);
        Assert.Equal(2, block.StaticMembers!.Count);

        var io = block.StaticMembers[0];
        Assert.Equal("IO", io.Name);
        Assert.Equal("\"TypeDOL\"", io.Datatype);
        Assert.True(io.Retain);
        Assert.NotNull(io.NestedMembers);
        Assert.Equal("FALSE", io.NestedMembers![0].StartValue);

        var timer = block.StaticMembers[1];
        Assert.Equal("TON_TIME", timer.Datatype);
        Assert.Equal("1.0", timer.Version);

        var temp = Assert.Single(block.TempMembers);
        Assert.Equal("Time", temp.Name);
        Assert.Equal("Real", temp.Datatype);
        Assert.Null(temp.NestedMembers);
    }

    [Fact]
    public void Parse_FbWithNonEmptyInput_HardErrors()
    {
        var ex = Assert.Throws<UnsupportedConstructException>(() => BlockSourceParser.Parse(LoadFixture("FbWithNonEmptyInput.xml")));
        Assert.Contains("Input", ex.Message);
    }

    [Fact]
    public void Parse_FbWithNonStandardReturn_HardErrors()
    {
        var xml = XDocument.Parse("""
            <Document>
              <Engineering version="V20" />
              <SW.Blocks.FB ID="0">
                <AttributeList>
                  <Interface><Sections xmlns="http://www.siemens.com/automation/Openness/SW/Interface/v5">
              <Section Name="Input" />
              <Section Name="Output" />
              <Section Name="InOut" />
              <Section Name="Temp" />
              <Section Name="Constant" />
              <Section Name="Return">
                <Member Name="Ret_Val" Datatype="Bool" Accessibility="Public" />
              </Section>
            </Sections></Interface>
                  <Name>RealFbName</Name>
                  <Namespace />
                  <Number>2</Number>
                  <ProgrammingLanguage>LAD</ProgrammingLanguage>
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
                  <SW.Blocks.CompileUnit ID="3" CompositionName="CompileUnits">
                    <AttributeList>
                      <NetworkSource />
                      <ProgrammingLanguage>LAD</ProgrammingLanguage>
                    </AttributeList>
                    <ObjectList>
                      <MultilingualText ID="4" CompositionName="Comment">
                        <ObjectList>
                          <MultilingualTextItem ID="5" CompositionName="Items">
                            <AttributeList>
                              <Culture>en-US</Culture>
                              <Text />
                            </AttributeList>
                          </MultilingualTextItem>
                        </ObjectList>
                      </MultilingualText>
                    </ObjectList>
                  </SW.Blocks.CompileUnit>
                </ObjectList>
              </SW.Blocks.FB>
            </Document>
            """);

        var ex = Assert.Throws<UnsupportedConstructException>(() => BlockSourceParser.Parse(xml));
        Assert.Contains("non-standard Return", ex.Message);
    }

    [Fact]
    public void RoundTrip_FbWithStaticAndTemp_ParseWriteParse_IsStable()
    {
        var original = BlockSourceParser.Parse(LoadFixture("FbWithStaticAndTemp.xml"));
        var networks = original.CompileUnits.Select(u => u.Network).ToList();
        var compileUnitUIds = original.CompileUnits.Select(u => u.UId).ToList();
        var networkTitles = original.CompileUnits.Select(u => u.Title).ToList();
        var networkComments = original.CompileUnits.Select(u => u.Comment).ToList();

        var written = BlockSourceWriter.Write(original, networks, compileUnitUIds, networkTitles, networkComments);
        var reparsed = BlockSourceParser.Parse(written);

        Assert.Equal(original.StaticMembers!.Count, reparsed.StaticMembers!.Count);
        Assert.Equal(original.StaticMembers.Select(m => m.Name), reparsed.StaticMembers.Select(m => m.Name));
        Assert.Equal(original.StaticMembers[0].NestedMembers!.Select(m => m.StartValue), reparsed.StaticMembers[0].NestedMembers!.Select(m => m.StartValue));
        Assert.Equal(original.TempMembers.Select(m => m.Name), reparsed.TempMembers.Select(m => m.Name));
    }

    [Fact]
    public void IrRoundTrip_FbWithStaticAndTemp_SerializeParse_IsStable()
    {
        var block = BlockSourceParser.Parse(LoadFixture("FbWithStaticAndTemp.xml"));
        var networks = block.CompileUnits.Select((u, idx) => GraphReducer.Reduce(u.Network, idx + 1, u.Comment ?? string.Empty, u.UId)).ToList();

        var irBlock = new IrBlock(
            block.RootUId,
            block.Kind,
            block.Name,
            block.Number,
            block.Language,
            block.Comment,
            networks.Select(r => r.Network).ToList(),
            block.StaticMembers,
            block.TempMembers);

        var text = IrSerializer.SerializeBlock(irBlock, networks.Select(r => r.Sidecar).ToList());
        var (parsedBlock, _) = IrParser.ParseBlock(text);
        var reserialized = IrSerializer.SerializeBlock(parsedBlock, networks.Select(r => r.Sidecar).ToList());

        Assert.Equal(text, reserialized);
        Assert.Contains("INTERFACE", text);
        Assert.Contains("  STATIC", text);
        Assert.Contains("    IO : \"TypeDOL\" RETAIN SETPOINT", text);
        Assert.Contains("      InHand : Bool = FALSE", text);
        Assert.Contains("  TEMP", text);
        Assert.Contains("    Time : Real", text);
    }

    [Fact]
    public void IrSerializer_FcWithNoInterface_OmitsInterfaceSection()
    {
        var block = BlockSourceParser.Parse(LoadFixture("SanitizeSource.xml"));
        var reduced = GraphReducer.Reduce(block.CompileUnits[0].Network, 1, "Test", block.CompileUnits[0].UId);
        var irBlock = new IrBlock(block.RootUId, block.Kind, block.Name, block.Number, block.Language, block.Comment, new[] { reduced.Network }, block.StaticMembers, block.TempMembers);

        var text = IrSerializer.SerializeBlock(irBlock, new[] { reduced.Sidecar });

        Assert.DoesNotContain("INTERFACE", text);
    }
}
