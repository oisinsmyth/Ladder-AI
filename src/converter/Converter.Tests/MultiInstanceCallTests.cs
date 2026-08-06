using System.Linq;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// MULTI-INSTANCE FB CALLS (2026-08-06). An FB called with one of the calling block's own STATICS as
/// its instance — `#ValveWater` rather than a global instance DB. The converter could not express it
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
            "ValveWater",
            new DbMember("ValveWater", "\"FB_Valve\"", Retain: false, StartValue: null));

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

    [Fact]
    public void WriteMember_BareShape_OmitsRemanenceAndAttributeList()
    {
        var member = new DbMember("ValveWater", "\"FB_Valve\"", Retain: false, StartValue: null);

        var element = DbInterfaceMembers.WriteMember(member, bareShape: true);

        Assert.Null(element.Attribute("Remanence"));
        Assert.Empty(element.Elements().Where(e => e.Name.LocalName == "AttributeList"));
        Assert.Equal("ValveWater", element.Attribute("Name")!.Value);
        Assert.Equal("\"FB_Valve\"", element.Attribute("Datatype")!.Value);
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
}
