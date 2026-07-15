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
        Assert.Equal(3, block.StaticMembers!.Count);

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

    // Block-level STATIC parsing/writing goes through IrSerializer/IrParser, a separate code path
    // from a standalone DB's own DbIrSerializer/DbIrParser — both were found to have their own,
    // independently-broken, still-two-level-only nested-member handling (2026-07-14, `FB
    // ShredderControlSystem`'s own `ComsOutByte501`, an anonymous Struct member nesting a further
    // Struct-typed field). Fixed by sharing `DbMemberLineFormat.SerializeMemberRecursive`/
    // `ParseMemberRecursive` between both. This test exercises the block-level path specifically
    // (`ComsByte`/`SubByte` in the fixture), since `DbConverterTests` only covers the DB-level one.
    [Fact]
    public void Parse_FbWithAnonymousStructMember_RecursesArbitrarilyDeep()
    {
        var block = BlockSourceParser.Parse(LoadFixture("FbWithStaticAndTemp.xml"));

        var comsByte = block.StaticMembers![2];
        Assert.Equal("ComsByte", comsByte.Name);
        Assert.Equal("Struct", comsByte.Datatype);
        Assert.NotNull(comsByte.NestedMembers);
        Assert.Equal(2, comsByte.NestedMembers!.Count);

        Assert.Equal("Flag1", comsByte.NestedMembers[0].Name);
        Assert.Null(comsByte.NestedMembers[0].NestedMembers);

        var subByte = comsByte.NestedMembers[1];
        Assert.Equal("SubByte", subByte.Name);
        Assert.Equal("Struct", subByte.Datatype);
        var deepFlag = Assert.Single(subByte.NestedMembers!);
        Assert.Equal("Flag1", deepFlag.Name);
    }

    // A fourth, genuinely minimal Input/Output member shape, confirmed real 2026-07-14 (`FC
    // Scale`'s own Input/Output params, a small project utility FC grounding `FB MotorVSDSystem`'s own
    // dependency closure): no `Remanence` attribute at all (not merely an unrecognized value) and
    // no `<AttributeList>` — just `Name`/`Datatype`[/`Accessibility="Public"`]. Previously a hard
    // error (`"has unrecognized Remanence ''"` — a missing attribute reads back as null, printed
    // as an empty string). Distinct from `FB TomraControlSystem`'s own Input/Output shape (has both
    // `Remanence` and `AttributeList`, just missing `SetPoint`).
    [Fact]
    public void Parse_FcWithBareParameterMembers_ReadsWithoutRemanenceOrAttributeList()
    {
        var doc = LoadFixture("FcWithBareParameterMembers.xml");
        var block = BlockSourceParser.Parse(doc);

        var input = Assert.Single(block.InputMembers!);
        Assert.Equal("Input", input.Name);
        Assert.Equal("Real", input.Datatype);
        Assert.False(input.Retain);
        Assert.Null(input.StartValue);

        var output = Assert.Single(block.OutputMembers!);
        Assert.Equal("Output", output.Name);

        var networks = block.CompileUnits.Select(u => u.Network).ToList();
        var compileUnitUIds = block.CompileUnits.Select(u => u.UId).ToList();
        var networkTitles = block.CompileUnits.Select(u => u.Title).ToList();
        var networkComments = block.CompileUnits.Select(u => u.Comment).ToList();

        var written = BlockSourceWriter.Write(block, networks, compileUnitUIds, networkTitles, networkComments);
        var reparsed = BlockSourceParser.Parse(written);
        Assert.Equal("Input", Assert.Single(reparsed.InputMembers!).Name);
        Assert.Equal("Real", Assert.Single(reparsed.InputMembers!).Datatype);
    }

    // Real bug, found live 2026-07-14 (`AnalogScale`, the sanitized `FC Scale`, discovered via the
    // full export/convert/import/compile/re-export cycle run against every block already in the
    // scratch project): the XML round-trip above was tested and passed, but the IR *text* format
    // (`DbMemberLineFormat`, shared with a standalone DB's own `MEMBERS` section) never carried
    // `IsBareParameter` at all, so a bare parameter crossing the to-ir/to-xml boundary silently
    // reverted to the ordinary member shape — TIA's own Import() then refused the resulting
    // (wrong) `Remanence` attribute outright. Exercised here via a DB (the same shared line format
    // a block's own STATIC section uses), since that's the public surface — `DbMemberLineFormat`
    // itself is internal.
    [Fact]
    public void IrRoundTrip_BareParameterMember_PreservesBareShape()
    {
        var bareMember = new DbMember("Input", "Real", Retain: false, StartValue: null, IsBareParameter: true);
        var db = new DbSource("0", "SomeDb", 1, null, null, new[] { bareMember });

        var ir = DbIrSerializer.Serialize(db);
        Assert.Contains("BAREPARAM", ir);

        var reparsed = DbIrParser.ParseDb(ir);
        var reparsedMember = Assert.Single(reparsed.Members);
        Assert.True(reparsedMember.IsBareParameter);
        Assert.Equal("Input", reparsedMember.Name);
        Assert.Equal("Real", reparsedMember.Datatype);
    }

    // Real bug, found live 2026-07-14 (`OB1 Main`, discovered via the same full round-trip cycle):
    // an OB's own system-defined Input parameters (`Initial_Call`/`Remanence`) carry
    // `Informative="true"` plus a `<Comment><MultiLanguageText Lang="en-US">...</MultiLanguageText>
    // </Comment>` child — TIA's own Import() requires this specifically ("OB system parameters
    // must be informative"), and (like IsBareParameter above) the IR text format silently dropped
    // it crossing the to-ir/to-xml boundary.
    [Fact]
    public void IrRoundTrip_InformativeBareParameterMember_PreservesInformativeCommentText()
    {
        var informativeMember = new DbMember(
            "Initial_Call", "Bool", Retain: false, StartValue: null,
            IsBareParameter: true, Informative: true, InformativeComment: "Initial call of this OB");
        var db = new DbSource("0", "SomeDb", 1, null, null, new[] { informativeMember });

        var ir = DbIrSerializer.Serialize(db);
        Assert.Contains("INFORMATIVE \"Initial call of this OB\"", ir);

        var reparsed = DbIrParser.ParseDb(ir);
        var reparsedMember = Assert.Single(reparsed.Members);
        Assert.True(reparsedMember.Informative);
        Assert.Equal("Initial call of this OB", reparsedMember.InformativeComment);
    }

    // DbMember.Comment: a member's own "why" annotation, genuinely distinct from
    // InformativeComment above (that one is OB-bare-system-parameter-only). Confirmed real
    // 2026-07-15 by reusing Informative's own already-proven <Comment><MultiLanguageText
    // Lang="en-US">...</MultiLanguageText></Comment> shape on an *ordinary* Static member
    // (FB_PusherControl, ExtendDemand's own sibling PressureHold/PumpEverDemanded/etc.) and
    // confirming via a live TIA import + compile + export round-trip that it's accepted — see
    // DbModel.cs's own DbMember.Comment doc comment.
    [Fact]
    public void IrRoundTrip_OrdinaryMemberComment_PreservesCommentText()
    {
        var member = new DbMember("PumpEverDemanded", "Bool", Retain: false, StartValue: null, Comment: "Cold-start guard for the run-on off-delay.");
        var db = new DbSource("0", "SomeDb", 1, null, null, new[] { member });

        var ir = DbIrSerializer.Serialize(db);
        Assert.Contains("COMMENT \"Cold-start guard for the run-on off-delay.\"", ir);

        var reparsed = DbIrParser.ParseDb(ir);
        var reparsedMember = Assert.Single(reparsed.Members);
        Assert.Equal("Cold-start guard for the run-on off-delay.", reparsedMember.Comment);
    }

    // The IR text format appends COMMENT as the absolute last token specifically so it can be
    // peeled off *before* StartValue's own leftmost-" = "-search runs (DbMemberLineFormat's own
    // doc comments) — free-text comment prose routinely contains "=" itself (e.g. referencing a
    // Step number, as here). This test exercises exactly that combination: a member with both a
    // StartValue and a Comment whose text contains " = ", proving the peel order actually prevents
    // the corruption it was designed to prevent, not just by inspection.
    [Fact]
    public void IrRoundTrip_MemberCommentContainingEqualsSign_DoesNotCorruptStartValue()
    {
        var member = new DbMember(
            "OperatorPusherMode", "Int", Retain: true, StartValue: "1",
            Comment: "Only meaningful once IO.Step = 10 - ignored otherwise.");
        var db = new DbSource("0", "SomeDb", 1, null, null, new[] { member });

        var ir = DbIrSerializer.Serialize(db);
        var reparsed = DbIrParser.ParseDb(ir);
        var reparsedMember = Assert.Single(reparsed.Members);
        Assert.Equal("1", reparsedMember.StartValue);
        Assert.Equal("Only meaningful once IO.Step = 10 - ignored otherwise.", reparsedMember.Comment);
    }

    [Fact]
    public void XmlRoundTrip_OrdinaryMemberComment_PreservesCommentText()
    {
        var member = new DbMember("PumpEverDemanded", "Bool", Retain: false, StartValue: null, Comment: "Cold-start guard for the run-on off-delay.");
        var db = new DbSource("0", "SomeDb", 1, null, null, new[] { member });

        var xml = DbSourceWriter.Write(db);
        Assert.Contains("Cold-start guard for the run-on off-delay.", xml.ToString());

        var reparsed = DbSourceParser.Parse(xml);
        var reparsedMember = Assert.Single(reparsed.Members);
        Assert.Equal("Cold-start guard for the run-on off-delay.", reparsedMember.Comment);
    }

    // Comment is only confirmed real on the ordinary (top-level, non-bare) WriteMember shape —
    // a structured member's own *nested* fields go through WriteBareMember instead, which never
    // learned about Comment. Hard-erroring rather than silently dropping the text if one somehow
    // arrives there (e.g. hand-authored IR nesting a COMMENT under a UDT-typed Static member's own
    // field) — same "refused rather than guessed at" discipline as every other unconfirmed shape
    // in this converter.
    [Fact]
    public void Write_CommentOnNestedStructuredMember_ThrowsUnsupportedConstructException()
    {
        var nestedWithComment = new DbMember("Step", "Int", Retain: false, StartValue: null, Comment: "Not supported here.");
        var owner = new DbMember(
            "IO", "\"UDT_PusherIO\"", Retain: true, StartValue: null, SetPoint: true,
            NestedMembers: new[] { nestedWithComment });
        var db = new DbSource("0", "SomeDb", 1, null, null, new[] { owner });

        Assert.Throws<UnsupportedConstructException>(() => DbSourceWriter.Write(db));
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
