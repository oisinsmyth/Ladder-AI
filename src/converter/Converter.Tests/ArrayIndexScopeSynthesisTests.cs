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

    // FI-51 (2026-08-07). A subscript in the MIDDLE of a path — an array of structs, e.g.
    // `DB_Weigh.Silo[0].RawValue` — was inexpressible, and the two halves of the converter
    // disagreed about it in the worst possible way: the parser REFUSED a non-final indexed
    // component, while the writer silently emitted `<Component Name="Silo[0]" />`, a component
    // literally named "Silo[0]" that names no member and that TIA rejects on import.
    //
    // Real consequence on a live job: the four weighing devices' slices of the interface DB could
    // not be mapped, so no vessel had a weight, and every weight-derived judgement on the plant —
    // stability, trust, overfill defence, valve position inference — ran on zero.
    [Fact]
    public void MidPathArraySubscript_WritesTheRealSimaticMlShape_NotABracketInTheName()
    {
        var node = AccessNode.FromDottedPath(3, "GlobalVariable", "DB_Weigh.Silo[0].RawValue");

        Assert.Equal(new[] { "DB_Weigh", "Silo[0]", "RawValue" }, node.ComponentPath);
        Assert.Equal("DB_Weigh.Silo[0].RawValue", node.DottedPath);

        var (name, index) = AccessNode.SplitComponent(node.ComponentPath[1]);
        Assert.Equal("Silo", name);
        Assert.Equal(0, index);

        // The un-subscripted siblings must not acquire one.
        Assert.Null(AccessNode.SplitComponent(node.ComponentPath[0]).Index);
        Assert.Null(AccessNode.SplitComponent(node.ComponentPath[2]).Index);
    }

    // The trailing-subscript case that already worked must keep working — it is the shape the
    // original grounding confirmed (`CommsProcessData.Node_Error[1]`), and the generalization
    // retired the field that used to carry it.
    [Fact]
    public void TrailingArraySubscript_StillRoundTripsThroughTheDottedForm()
    {
        var node = AccessNode.FromDottedPath(4, "GlobalVariable", "CommsProcessData.Node_Error[1]");

        Assert.Equal(new[] { "CommsProcessData", "Node_Error[1]" }, node.ComponentPath);
        Assert.Equal("CommsProcessData.Node_Error[1]", node.DottedPath);
        Assert.Equal(1, AccessNode.SplitComponent(node.ComponentPath[1]).Index);
    }

    // A subscript and a bit-slice on the same access. The slice stays an access-level suffix
    // (it addresses a bit within whatever the path resolved to); the subscript belongs to its
    // component. Composing them must not let either eat the other.
    [Fact]
    public void SubscriptAndSlice_ComposeWithoutConsumingEachOther()
    {
        var node = AccessNode.FromDottedPath(5, "GlobalVariable", "DB_A.Word[2].%X15");

        Assert.Equal(new[] { "DB_A", "Word[2]" }, node.ComponentPath);
        Assert.Equal("x15", node.SliceAccessModifier);
        Assert.Equal("DB_A.Word[2].%X15", node.DottedPath);
    }

    // Several subscripts in one path — nested arrays of structs. Nothing in the generalized form
    // is positional, so this needs no additional handling; the test exists to say so.
    [Fact]
    public void MultipleSubscriptsInOnePath_AllSurvive()
    {
        var node = AccessNode.FromDottedPath(6, "GlobalVariable", "DB_A.Vessel[0].Sensor[3].Reading");

        Assert.Equal(new[] { "DB_A", "Vessel[0]", "Sensor[3]", "Reading" }, node.ComponentPath);
        Assert.Equal(0, AccessNode.SplitComponent(node.ComponentPath[1]).Index);
        Assert.Equal(3, AccessNode.SplitComponent(node.ComponentPath[2]).Index);
        Assert.Equal("DB_A.Vessel[0].Sensor[3].Reading", node.DottedPath);
    }
}
