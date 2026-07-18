using System.Linq;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// Gap D (2026-07-19): an array-indexed local member (`RisingEdgeFlags[3]`) must scope to
/// LocalVariable — its member is `RisingEdgeFlags`, which the block declares. Before the fix,
/// ScopeFor kept the `[3]` subscript on the first path component, so the lookup missed and the access
/// mis-scoped to GlobalVariable (TIA then rejects it as an undefined global tag). Surfaced by the
/// derive-always migration on MotorStarter / the test-project001 FBs.
/// </summary>
public class ArrayIndexScopeSynthesisTests
{
    [Fact]
    public void Synthesize_ArrayIndexedLocalMember_ScopesLocalVariable()
    {
        var network = IrParser.ParseNetworkOnly("NETWORK 1 \"N\"\n  COIL Out := RisingEdgeFlags[3]\n");
        var block = new IrBlock(
            "0", "FB", "T", 1, "LAD", null, new[] { network },
            StaticMembers: new[]
            {
                new DbMember("RisingEdgeFlags", "Array[0..14] of Bool", Retain: false, StartValue: null),
                new DbMember("Out", "Bool", Retain: false, StartValue: null),
            });

        var sidecar = SidecarSynthesizer.SynthesizeBlock(block).Single();

        var access = sidecar.AccessUIds.Single(a => a.TagPath == "RisingEdgeFlags[3]");
        Assert.Equal("LocalVariable", access.Scope);
        // The sibling scalar local scopes the same way (guards the non-subscript path stays correct).
        Assert.Equal("LocalVariable", sidecar.AccessUIds.Single(a => a.TagPath == "Out").Scope);
    }
}
