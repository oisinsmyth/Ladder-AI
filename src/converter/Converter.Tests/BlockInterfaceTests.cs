using System.Xml.Linq;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// FB Interface support: Static/Temp (S1 item 7 Phase B, 2026-07-11) and Input/Output/InOut/
/// Constant (S1 item 20, 2026-07-12) — grounded against real exports (`MotorDOL`: a UDT-typed
/// Static member with StartValue-bearing nested members, a TON_TIME Static member, and a bare
/// Temp member; `TomraControlSystem`: real Input/Output members, same shape as Static's own EXCEPT
/// missing the SetPoint BooleanAttribute Static's own AttributeList always carries; `MotorVSDSystem`/
/// `AirStar`: real Constant members, a genuinely distinct third shape — no AttributeList at all,
/// a required StartValue instead). Member-level parsing/writing is the same
/// <see cref="DbInterfaceMembers"/> code path DBs use — these tests focus on the block-level
/// wiring (Static absent vs. present-and-empty, Temp always present, Input/Output/Constant
/// present-vs-absent-vs-empty, Return still hard-erroring on non-standard content).
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

    // Repurposed from a hard-error test (S1 item 20, 2026-07-12) — same "an obsolete hard-error
    // test becomes a positive one once the real shape is modeled" pattern already used for TON's
    // direct-Q-wiring and block-level Title. The fixture's own content was also corrected to the
    // real grounded shape at the same time (the old synthetic `<Member Name="Enable"
    // Datatype="Bool" Accessibility="Public" />` was never sourced from a real export).
    [Fact]
    public void Parse_FbWithInputOutput_ReadsBothAsMemberLists()
    {
        var block = BlockSourceParser.Parse(LoadFixture("FbWithNonEmptyInput.xml"));

        Assert.NotNull(block.InputMembers);
        Assert.Equal(2, block.InputMembers!.Count);
        Assert.Equal("controlWord0", block.InputMembers[0].Name);
        Assert.Equal("Word", block.InputMembers[0].Datatype);
        Assert.False(block.InputMembers[0].Retain);

        Assert.NotNull(block.OutputMembers);
        var output = Assert.Single(block.OutputMembers!);
        Assert.Equal("statusWord0", output.Name);

        Assert.Empty(block.InOutMembers);
        // Constant is present-but-empty in this fixture (`<Section Name="Constant" />`) — a
        // real, must-preserve distinction from being entirely absent (null): both are confirmed
        // real shapes (TomraControlSystem's own Constant is present-but-empty; MotorVSDSystem/AirStar's own
        // is present-and-populated).
        Assert.NotNull(block.ConstantMembers);
        Assert.Empty(block.ConstantMembers!);
    }

    [Fact]
    public void Parse_FbWithConstant_ReadsRequiredStartValues()
    {
        var block = BlockSourceParser.Parse(LoadFixture("FbWithConstant.xml"));

        Assert.NotNull(block.ConstantMembers);
        Assert.Equal(2, block.ConstantMembers!.Count);
        Assert.Equal("MaxSpeedError", block.ConstantMembers[0].Name);
        Assert.Equal("Int", block.ConstantMembers[0].Datatype);
        Assert.Equal("50", block.ConstantMembers[0].StartValue);
        Assert.Equal("20.0", block.ConstantMembers[1].StartValue);

        // Input/Output are present-but-empty in this fixture, not absent.
        Assert.NotNull(block.InputMembers);
        Assert.Empty(block.InputMembers!);
        Assert.NotNull(block.OutputMembers);
        Assert.Empty(block.OutputMembers!);
    }

    [Fact]
    public void RoundTrip_FbWithInputOutput_ParseWriteParse_IsStable()
    {
        var original = BlockSourceParser.Parse(LoadFixture("FbWithNonEmptyInput.xml"));
        var networks = original.CompileUnits.Select(u => u.Network).ToList();
        var compileUnitUIds = original.CompileUnits.Select(u => u.UId).ToList();
        var networkTitles = original.CompileUnits.Select(u => u.Title).ToList();
        var networkComments = original.CompileUnits.Select(u => u.Comment).ToList();

        var written = BlockSourceWriter.Write(original, networks, compileUnitUIds, networkTitles, networkComments);
        var reparsed = BlockSourceParser.Parse(written);

        Assert.Equal(original.InputMembers!.Select(m => m.Name), reparsed.InputMembers!.Select(m => m.Name));
        Assert.Equal(original.OutputMembers!.Select(m => m.Name), reparsed.OutputMembers!.Select(m => m.Name));
    }

    [Fact]
    public void RoundTrip_FbWithConstant_ParseWriteParse_IsStable()
    {
        var original = BlockSourceParser.Parse(LoadFixture("FbWithConstant.xml"));
        var networks = original.CompileUnits.Select(u => u.Network).ToList();
        var compileUnitUIds = original.CompileUnits.Select(u => u.UId).ToList();
        var networkTitles = original.CompileUnits.Select(u => u.Title).ToList();
        var networkComments = original.CompileUnits.Select(u => u.Comment).ToList();

        var written = BlockSourceWriter.Write(original, networks, compileUnitUIds, networkTitles, networkComments);
        var reparsed = BlockSourceParser.Parse(written);

        Assert.Equal(original.ConstantMembers!.Select(m => (m.Name, m.StartValue)), reparsed.ConstantMembers!.Select(m => (m.Name, m.StartValue)));
    }

    [Fact]
    public void IrRoundTrip_FbWithInputOutput_SerializeParse_IsStable()
    {
        var block = BlockSourceParser.Parse(LoadFixture("FbWithNonEmptyInput.xml"));
        var networks = block.CompileUnits.Select((u, idx) => GraphReducer.Reduce(u.Network, idx + 1, u.Comment ?? string.Empty, u.UId)).ToList();

        var irBlock = new IrBlock(
            block.RootUId, block.Kind, block.Name, block.Number, block.Language, block.Comment,
            networks.Select(r => r.Network).ToList(), block.StaticMembers, block.TempMembers, null,
            block.InputMembers, block.OutputMembers, block.InOutMembers, block.ConstantMembers);

        var text = IrSerializer.SerializeBlock(irBlock, networks.Select(r => r.Sidecar).ToList());
        var (parsedBlock, _) = IrParser.ParseBlock(text);
        var reserialized = IrSerializer.SerializeBlock(parsedBlock, networks.Select(r => r.Sidecar).ToList());

        Assert.Equal(text, reserialized);
        Assert.Contains("  INPUT\n", text);
        Assert.Contains("    controlWord0 : Word\n", text);
        Assert.Contains("  OUTPUT\n", text);
        Assert.Contains("    statusWord0 : Word\n", text);
        // Constant is present-but-empty in this fixture — the header still shows (preserving
        // the null-vs-present-but-empty distinction), just with no member lines after it.
        Assert.Contains("  CONSTANT\n\n", text);
        Assert.Equal(2, parsedBlock.InputMembers!.Count);
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
