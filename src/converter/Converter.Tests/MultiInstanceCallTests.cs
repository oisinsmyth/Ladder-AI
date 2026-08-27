using System.Linq;
using System.Xml.Linq;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// MULTI-INSTANCE FB CALLS (2026-08-06). An FB called with one of the calling block's own STATICS as
/// its instance — `#ValveA` rather than a global instance DB. The converter could not express it
/// at all, in two independent ways, and the construct appears nowhere in the committed export corpus:
/// it had simply never been written.
///
///   1. `Remanence` was emitted on EVERY static member. TIA refuses it on an FB-typed one —
///      "The attribute 'Remanence' cannot be set." — because a multi-instance's retentivity is a
///      property of the CALLED block's members, not of the calling member.
///   2. A call's instance scope was hardcoded to GlobalVariable, with a comment saying multi-instance
///      was out of scope. TIA then resolved the name as a global DB and reported "Missing instance DB"
///      on a block that had imported cleanly. The TIMER path had always done this correctly, so the
///      two were asymmetric for no reason beyond nobody having needed it.
///
/// Both were found by a coding agent that stopped rather than working around them.
/// </summary>
public class MultiInstanceCallTests
{
    private static IrBlock BlockCalling(string instancePath, params DbMember[] statics)
    {
        // The IR carries no `#` — local-ness is DERIVED from whether the name is a static of this
        // block, and the scope is encoded in the XML rather than in the text (SidecarSynthesizer.
        // ScopeFor's own doc comment makes that a deliberate property of this format).
        var network = IrParser.ParseNetworkOnly(
            $"NETWORK 1 \"N\"\n  CALL FB_Valve({instancePath}, EN := TRUE)\n");
        return new IrBlock("0", "FB", "Caller", 1, "LAD", null, new[] { network }, StaticMembers: statics);
    }

    [Fact]
    public void Synthesize_CallOnLocalStaticInstance_ScopesLocalVariable()
    {
        var block = BlockCalling(
            "ValveA",
            new DbMember("ValveA", "\"FB_Valve\"", Retain: false, StartValue: null));

        var call = SidecarSynthesizer.SynthesizeBlock(block).Single().Calls.Single();

        Assert.Equal("LocalVariable", call.InstanceScope);
    }

    [Fact]
    public void Synthesize_CallOnGlobalInstanceDb_StillScopesGlobalVariable()
    {
        // The regression guard that matters: the ordinary single-instance call is the shape every
        // block on this project already uses, and it must not have moved.
        var block = BlockCalling("Pump1_DB");

        var call = SidecarSynthesizer.SynthesizeBlock(block).Single().Calls.Single();

        Assert.Equal("GlobalVariable", call.InstanceScope);
    }

    // REVISED 2026-08-12. A multi-instance now takes `omitRemanence` (the full member shape MINUS
    // Remanence) rather than `bareShape` (which ALSO drops Version and the whole AttributeList).
    // `Remanence` is the only thing TIA ever refused here; dropping the rest silently produced a
    // VERSIONLESS instance declaration from a source that stated `Version="5.3"` — see
    // WriteMember_MultiInstance_KeepsVersionAndAttributeList below.
    [Fact]
    public void WriteMember_MultiInstance_OmitsRemanence()
    {
        var member = new DbMember("ValveA", "\"FB_Valve\"", Retain: false, StartValue: null);

        var element = DbInterfaceMembers.WriteMember(member, omitRemanence: true);

        Assert.Null(element.Attribute("Remanence"));
        Assert.Equal("ValveA", element.Attribute("Name")!.Value);
        Assert.Equal("\"FB_Valve\"", element.Attribute("Datatype")!.Value);
    }

    // The GENUINELY minimal shape still exists — `FC Scale`'s own attribute-less parameters — and is
    // now selected by the member's own captured `IsBareParameter` rather than forced by a caller.
    // Keeping it distinct from `omitRemanence` is the whole point of the split.
    [Fact]
    public void WriteMember_GenuinelyBareParameter_StillOmitsRemanenceAndAttributeList()
    {
        var member = new DbMember("Input", "Real", Retain: false, StartValue: null, IsBareParameter: true);

        var element = DbInterfaceMembers.WriteMember(member);

        Assert.Null(element.Attribute("Remanence"));
        Assert.Empty(element.Elements().Where(e => e.Name.LocalName == "AttributeList"));
    }

    /// <summary>
    /// GAP 4, THE SILENT ONE (2026-08-12). Measured on a real TIA V20 export: a multi-instance
    /// `MB_Server : MB_SERVER Version="5.3"` converted WITHOUT ERROR and came back as
    /// <c>&lt;Member Name="MB_Server" Datatype="MB_SERVER" Accessibility="Public" /&gt;</c> — no
    /// Version, no AttributeList. An import would then declare a VERSIONLESS instance and NOTHING
    /// WARNED. The `TON_TIME` member beside it kept its `Version="1.0"` only because it carries
    /// `Remanence` and never took this path.
    /// </summary>
    [Fact]
    public void WriteMember_MultiInstance_KeepsVersionAndAttributeList()
    {
        var member = new DbMember(
            "MB_Server", "MB_SERVER", Retain: false, StartValue: null, Version: "5.3", SetPoint: false);

        var element = DbInterfaceMembers.WriteMember(member, omitRemanence: true);

        Assert.Equal("5.3", element.Attribute("Version")!.Value);
        Assert.Null(element.Attribute("Remanence"));
        var attributeList = Assert.Single(element.Elements().Where(e => e.Name.LocalName == "AttributeList"));
        Assert.Equal(
            new[] { "ExternalAccessible", "ExternalVisible", "ExternalWritable", "SetPoint" },
            attributeList.Elements().Select(e => (string)e.Attribute("Name")!));
    }

    [Fact]
    public void WriteMember_OrdinaryStatic_StillCarriesRemanence()
    {
        // The other half of the guard. A UDT-typed static — the C-132 interface member — is ALSO a
        // quoted datatype, and it genuinely does carry Remanence: that is where `RETAIN` on an FB's
        // whole interface is expressed. So the datatype cannot decide this, which is why the caller
        // supplies it from the block's own CALL statements instead.
        var member = new DbMember("IO", "\"UDT_Valve\"", Retain: true, StartValue: null);

        var element = DbInterfaceMembers.WriteMember(member);

        Assert.Equal("Retain", element.Attribute("Remanence")!.Value);
    }

    // INVERTED 2026-08-12. This asserted `IsBareParameter` — the read that CAUSED gap 4. The bare
    // shape has nowhere to put a Version or an AttributeList, so reading a multi-instance as one
    // discarded both on the way back out. A multi-instance is not a bare parameter: it is an
    // ordinary Static member that never carries `Remanence` (retention belongs to the CALLED
    // block's members), and that one difference now lives on the WRITE side instead.
    [Fact]
    public void ParseMember_MultiInstanceFromTia_ReadsBackAsAnOrdinaryMemberKeepingItsAttributes()
    {
        // The READ half, missed when the write half was fixed. TIA re-exports a multi-instance with
        // NO Remanence but WITH an AttributeList and a <Sections> child holding the callee's whole
        // interface expanded inline. The bare-shape branch required BOTH absent, so this fell through
        // to the Remanence switch and hard-errored — taking to-ir, drift-check and the round-trip
        // harness out for every block containing a multi-instance.
        var xml = XElement.Parse(
            """
            <Member xmlns="http://www.siemens.com/automation/Openness/SW/Interface/v5"
                    Name="ProbeValve" Datatype="&quot;FB_Valve&quot;" Accessibility="Public">
              <AttributeList>
                <BooleanAttribute Name="ExternalAccessible" SystemDefined="true">true</BooleanAttribute>
                <BooleanAttribute Name="ExternalVisible" SystemDefined="true">true</BooleanAttribute>
                <BooleanAttribute Name="ExternalWritable" SystemDefined="true">true</BooleanAttribute>
              </AttributeList>
              <Sections>
                <Section Name="Input">
                  <Member Name="InHand" Datatype="Bool" Remanence="NonRetain" Accessibility="Public" />
                </Section>
              </Sections>
            </Member>
            """);

        var member = DbInterfaceMembers.ParseMember(xml, "Block 'FB_Caller'");

        Assert.Equal("ProbeValve", member.Name);
        Assert.Equal("\"FB_Valve\"", member.Datatype);
        Assert.False(member.IsBareParameter);
        Assert.False(member.Retain);

        // The AttributeList is CAPTURED, not discarded — the half that was missing.
        Assert.True(member.ExternalAccessible);
        Assert.True(member.ExternalVisible);
        Assert.True(member.ExternalWritable);

        // The callee's expanded interface is DISCARDED, not adopted. It is the called block's own
        // declaration; duplicating it into the caller would make the two free to disagree.
        Assert.Null(member.NestedMembers);
    }
}
