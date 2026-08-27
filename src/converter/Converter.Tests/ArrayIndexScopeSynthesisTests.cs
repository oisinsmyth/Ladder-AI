using System;
using System.Linq;
using System.Xml.Linq;
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
    // `DB_Gauge.Bay[0].RawValue` — was inexpressible, and the two halves of the converter
    // disagreed about it in the worst possible way: the parser REFUSED a non-final indexed
    // component, while the writer silently emitted `<Component Name="Bay[0]" />`, a component
    // literally named "Bay[0]" that names no member and that TIA rejects on import.
    //
    // Real consequence on a live job: the four weighing devices' slices of the interface DB could
    // not be mapped, so no vessel had a weight, and every weight-derived judgement on the plant —
    // stability, trust, overfill defence, valve position inference — ran on zero.
    [Fact]
    public void MidPathArraySubscript_WritesTheRealSimaticMlShape_NotABracketInTheName()
    {
        var node = AccessNode.FromDottedPath(3, "GlobalVariable", "DB_Gauge.Bay[0].RawValue");

        Assert.Equal(new[] { "DB_Gauge", "Bay[0]", "RawValue" }, node.ComponentPath);
        Assert.Equal("DB_Gauge.Bay[0].RawValue", node.DottedPath);

        var (name, index) = AccessNode.SplitComponent(node.ComponentPath[1]);
        Assert.Equal("Bay", name);
        Assert.Equal("0", index);

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
        Assert.Equal("1", AccessNode.SplitComponent(node.ComponentPath[1]).Index);
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
        Assert.Equal("0", AccessNode.SplitComponent(node.ComponentPath[1]).Index);
        Assert.Equal("3", AccessNode.SplitComponent(node.ComponentPath[2]).Index);
        Assert.Equal("DB_A.Vessel[0].Sensor[3].Reading", node.DottedPath);
    }

    // --- the VARIABLE subscript, 2026-08-24 ------------------------------------------------------
    //
    // Same defect class as MidPathArraySubscript above, one input class over, and it survived that
    // fix: the pattern matched `\d+` and fell through to `(component, null)` on anything else, so
    // `Table[Selector]` was emitted as a component literally NAMED "Table[Selector]". The writer
    // accepted it and TIA cannot resolve it.
    //
    // Consequence where it was found: the access was the sole wire carrying a runtime-selected
    // parameter set into the block that consumes it. Emitted this way it never lands, and the
    // consuming sequence cannot start. `converter preflight` did not flag it — preflight resolves
    // only the ROOT of a dotted path, so a defect below the root is invisible to it, which is why
    // this needs a test rather than a check.
    //
    // ✅ THE REFUSAL WAS LIFTED 2026-08-27, AND ONLY BECAUSE THE SHAPE WAS MEASURED. An ADR-0011
    // confirm loop hand-authored a probe indexing an array by a variable, imported it, compiled it
    // clean and exported it back: TIA's canonical form keeps `AccessModifier="Array"` and puts the
    // index expression's OWN scope plus a <Symbol> in the nested <Access>. The sweep that justified
    // the refusal (64,878 array accesses, `LiteralConstant` on every one) was accurate about the
    // corpus and simply could not answer the question — which is why the answer came from TIA.
    [Theory]
    [InlineData("Table[Selector]", "Table", "Selector")]   // the case found live: a runtime-selected slot
    [InlineData("Vessel[i]", "Vessel", "i")]
    [InlineData("Recipe[DB_Settings.Slot]", "Recipe", "DB_Settings.Slot")]
    public void VariableArraySubscript_IsNowAccepted_AsIndexText(
        string component, string expectedName, string expectedIndex)
    {
        var (name, index) = AccessNode.SplitComponent(component);

        Assert.Equal(expectedName, name);
        Assert.Equal(expectedIndex, index);
        Assert.True(AccessNode.IsSymbolicIndex(index!));
        Assert.False(AccessNode.IsLiteralIndex(index!));
    }

    // 🔴 WHAT IS STILL REFUSED, AND THE LINE IS THE EVIDENCE, NOT THE SYNTAX. The confirm loop
    // established one shape: an index that is a SYMBOL. A computed index was explicitly NOT covered,
    // so it stays refused — widening to "anything in brackets" would be the guess the whole exercise
    // existed to avoid. `#local` is refused for a different reason: the `#` is TIA's editor prefix
    // for a local, and the export's own component Name carries no `#`, so an IR author writing one
    // means something the emit shape cannot express.
    [Theory]
    [InlineData("Buf[Index + 1]")]                // an expression, not merely a symbol
    [InlineData("Slot[#local]")]                  // an editor prefix, not an exported component name
    [InlineData("Grid[2,3]")]                     // multi-dimensional: unobserved, so unbuilt
    public void ComputedOrUnobservedSubscript_IsStillRefused(string component)
    {
        var ex = Assert.Throws<UnsupportedConstructException>(() => AccessNode.SplitComponent(component));

        // The message must name the construct, or the engineer cannot act on it.
        Assert.Contains("neither an integer", ex.Message);
        Assert.Contains("COMPUTED index", ex.Message);
    }

    // ✅ ACCEPTED SINCE 2026-08-27 — and this is the test that records the change of evidence the
    // previous version of it asked for. It read "no export on this machine contains a negative
    // subscript, so 'obviously correct' is exactly the untested assumption this project keeps getting
    // caught by. If a real export ever shows one, this test is the place that records the change."
    // The confirm loop produced one: `Array[-5..5]` indexed at `[-3]` compiles clean and emits the
    // ORDINARY literal shape with a negative <ConstantValue> — no new structure at all.
    //
    // It also closed a split-brain: `to-ir` already accepted a negative subscript (int.TryParse)
    // while `to-xml` refused it, so a TIA-valid block could be read INTO the IR and then could not be
    // written back out — the "no IR the AI cannot change" failure ADR-0010 forbids.
    [Theory]
    [InlineData("Window[-1]", "Window", "-1")]
    [InlineData("Window[-3]", "Window", "-3")]
    public void NegativeArraySubscript_IsNowAccepted_MeasuredNotAssumed(
        string component, string expectedName, string expectedIndex)
    {
        var (name, index) = AccessNode.SplitComponent(component);

        Assert.Equal(expectedName, name);
        Assert.Equal(expectedIndex, index);
        Assert.True(AccessNode.IsLiteralIndex(index!));
    }

    // The refusal must not swallow the cases that work. A bare name is not a subscript, and every
    // non-negative literal still splits — including 0, which a `.+` name group must not eat.
    [Theory]
    [InlineData("Recipe", "Recipe", null)]
    [InlineData("Recipe[0]", "Recipe", "0")]
    [InlineData("Recipe[50]", "Recipe", "50")]
    [InlineData("Clock_0.5Hz", "Clock_0.5Hz", null)]   // an embedded dot is not a subscript
    public void LiteralAndUnsubscriptedComponents_AreUnaffectedByTheRefusal(
        string component, string expectedName, string? expectedIndex)
    {
        var (name, index) = AccessNode.SplitComponent(component);

        Assert.Equal(expectedName, name);
        Assert.Equal(expectedIndex, index);
    }

    // --- the splitter defect the widening itself introduced, 2026-08-27 ---------------------------
    //
    // 🔴 MEASURED, NOT REASONED ABOUT. FromDottedPath split on '.' unconditionally, carrying a comment
    // that justified it: "an index contains no dot, so splitting on '.' cannot cut one in half". That
    // was true of every input it had ever seen and became false the same day IsSymbolicIndex began
    // accepting a DOTTED index — `Recipe[DB_Settings.Slot].Target` split to
    // ["Recipe[DB_Settings", "Slot]", "Target"], three components of which two name nothing that can
    // exist. The widening shipped with a committed test using exactly that path shape, and the test
    // passed, because it exercised SplitComponent on an already-separated component and never put the
    // path through the splitter. A true statement in a comment does not stay true when the input class
    // widens underneath it.
    [Theory]
    [InlineData("Recipe[DB_Settings.Slot].Target", new[] { "Recipe[DB_Settings.Slot]", "Target" })]
    [InlineData("DB_A.Recipe[DB_Settings.Slot]", new[] { "DB_A", "Recipe[DB_Settings.Slot]" })]
    [InlineData("Vessel[i].Sensor[DB_Cfg.Which].Reading",
        new[] { "Vessel[i]", "Sensor[DB_Cfg.Which]", "Reading" })]
    public void DottedSymbolicIndex_SurvivesTheComponentSplit(string dottedPath, string[] expected)
    {
        var node = AccessNode.FromDottedPath(9, "GlobalVariable", dottedPath);

        Assert.Equal(expected, node.ComponentPath);
        // And it rejoins to what it came from — the round trip is the check that nothing was eaten.
        Assert.Equal(dottedPath, node.DottedPath);
    }

    // --- the sidecar half: emitting a variable subscript from HAND-AUTHORED IR --------------------
    //
    // Until this landed, the widening could only ECHO a variable subscript back out of an export it
    // had just parsed: the writer needs the index's scope, only the enclosing block's declared members
    // resolve it, and FlgNetBuilder has no block context. So `to-xml` on hand-authored IR hit the
    // writer's refusal — a correct failure, and still a total block on authoring one.
    //
    // 🔴 THIS IS THE CASE THAT MAKES THE CARRIED SCOPE NECESSARY RATHER THAN CONVENIENT: a GLOBAL array
    // indexed by a LOCAL variable. The two scopes differ, so anything that reuses the enclosing
    // access's scope for its index emits `GlobalVariable` here and sends TIA hunting for a PLC tag
    // named "Slot" that does not exist. It would not be caught at import either — the 2026-08-27
    // confirm loop mislabelled an index scope deliberately and TIA IMPORTED IT AT EXIT 0, refusing
    // only at compile.
    [Fact]
    public void GlobalArrayIndexedByLocalVariable_EmitsTheIndexScopeSeparately()
    {
        var xml = EmitSingleNetwork(
            "NETWORK 1 \"N\"\n  COIL Out := DB_Recipe.Table[Slot]\n",
            new DbMember("Slot", "Int", Retain: false, StartValue: null),
            new DbMember("Out", "Bool", Retain: false, StartValue: null));

        var component = SubscriptedComponent(xml, "Table");
        Assert.Equal("Array", component.Attribute("AccessModifier")?.Value);

        var nested = Assert.Single(component.Elements().Where(e => e.Name.LocalName == "Access"));
        // The index is local; the array it indexes is not. Both facts survive to the XML.
        Assert.Equal("LocalVariable", nested.Attribute("Scope")?.Value);
        Assert.Equal("GlobalVariable", component.Ancestors()
            .First(e => e.Name.LocalName == "Access").Attribute("Scope")?.Value);

        var indexSymbol = Assert.Single(nested.Elements().Where(e => e.Name.LocalName == "Symbol"));
        var segment = Assert.Single(indexSymbol.Elements().Where(e => e.Name.LocalName == "Component"));
        Assert.Equal("Slot", segment.Attribute("Name")?.Value);
    }

    // The all-local case — the shape the plant wiring actually needs.
    [Fact]
    public void LocalArrayIndexedByLocalVariable_Emits()
    {
        var xml = EmitSingleNetwork(
            "NETWORK 1 \"N\"\n  COIL Out := Recipe[Slot]\n",
            new DbMember("Recipe", "Array[0..9] of Bool", Retain: false, StartValue: null),
            new DbMember("Slot", "Int", Retain: false, StartValue: null),
            new DbMember("Out", "Bool", Retain: false, StartValue: null));

        var nested = Assert.Single(
            SubscriptedComponent(xml, "Recipe").Elements().Where(e => e.Name.LocalName == "Access"));
        Assert.Equal("LocalVariable", nested.Attribute("Scope")?.Value);
    }

    // A LITERAL subscript must not acquire a scope entry — it emits as a constant and has none. This
    // guards the fast path from quietly changing the overwhelmingly common case.
    [Fact]
    public void LiteralSubscript_CarriesNoIndexScope()
    {
        var block = BlockWith(
            "NETWORK 1 \"N\"\n  COIL Out := RisingEdgeFlags[3]\n",
            new DbMember("RisingEdgeFlags", "Array[0..14] of Bool", Retain: false, StartValue: null),
            new DbMember("Out", "Bool", Retain: false, StartValue: null));

        var sidecar = SidecarSynthesizer.SynthesizeBlock(block).Single();

        Assert.Empty(sidecar.AccessUIds.Single(a => a.TagPath == "RisingEdgeFlags[3]").IndexScopes);
    }

    // 🔴 THE REFUSAL MUST SURVIVE THE FIX. Carrying the scope must not become a way to default it: an
    // AccessNode built WITHOUT the sidecar context still refuses rather than guessing, which is the
    // whole discipline the widening was built on.
    [Fact]
    public void VariableSubscriptWithNoResolvedScope_StillRefusesToWrite()
    {
        var orphan = AccessNode.FromDottedPath(1, "LocalVariable", "Recipe[Slot]");
        var net = new FlgNetwork(new[] { orphan }, Array.Empty<PartNode>(), Array.Empty<WireNode>());

        var ex = Assert.Throws<UnsupportedConstructException>(() => FlgNetWriter.Write(net));
        Assert.Contains("nothing resolved that index's scope", ex.Message);
    }

    // The sidecar is a TEXT format and it is what a hand-authored block is stored as, so a scope that
    // does not survive serialization is a scope the author does not have.
    [Fact]
    public void IndexScopes_SurviveTheSidecarTextRoundTrip()
    {
        var block = BlockWith(
            "NETWORK 1 \"N\"\n  COIL Out := DB_Recipe.Table[Slot]\n",
            new DbMember("Slot", "Int", Retain: false, StartValue: null),
            new DbMember("Out", "Bool", Retain: false, StartValue: null));

        var original = SidecarSynthesizer.SynthesizeBlock(block).Single();
        var text = IrSerializer.SerializeBlock(block, new[] { original });

        // The token is visible in the text, not just in the object graph — a reader can see it.
        Assert.Contains("idx1=LocalVariable", text);

        var (_, sidecars) = IrParser.ParseBlock(text);
        var entry = sidecars.Single().AccessUIds.Single(a => a.TagPath == "DB_Recipe.Table[Slot]");

        Assert.Equal("LocalVariable", Assert.Single(entry.IndexScopes).Value);
        Assert.Equal("GlobalVariable", entry.Scope);
    }

    // BACKWARD COMPATIBILITY, asserted rather than assumed: every sidecar on disk predates this
    // field, and they are the stored form of real blocks — a parse regression here would not be a
    // failing test, it would be an unreadable corpus.
    //
    // The legacy text is produced by stripping the new token out of a genuinely valid file rather
    // than by hand-writing one, so the test cannot pass against a shape the serializer never emits.
    [Fact]
    public void SidecarLineWrittenBeforeIndexScopesExisted_StillParses()
    {
        var block = BlockWith(
            "NETWORK 1 \"N\"\n  COIL Out := DB_Recipe.Table[Slot]\n",
            new DbMember("Slot", "Int", Retain: false, StartValue: null),
            new DbMember("Out", "Bool", Retain: false, StartValue: null));

        var current = IrSerializer.SerializeBlock(block, SidecarSynthesizer.SynthesizeBlock(block));
        var legacy = System.Text.RegularExpressions.Regex.Replace(current, @" idx\d+=\S+", string.Empty);
        Assert.DoesNotContain("idx", legacy);

        var (_, sidecars) = IrParser.ParseBlock(legacy);
        var entry = sidecars.Single().AccessUIds.Single(a => a.TagPath == "DB_Recipe.Table[Slot]");

        // Parses, keeps its own scope, and acquires NO default for the index.
        Assert.Equal("GlobalVariable", entry.Scope);
        Assert.Empty(entry.IndexScopes);
    }

    private static IrBlock BlockWith(string networkText, params DbMember[] statics) =>
        new("0", "FB", "T", 1, "LAD", null,
            new[] { IrParser.ParseNetworkOnly(networkText) }, StaticMembers: statics);

    private static XElement EmitSingleNetwork(string networkText, params DbMember[] statics)
    {
        var block = BlockWith(networkText, statics);
        var sidecar = SidecarSynthesizer.SynthesizeBlock(block).Single();
        return FlgNetWriter.Write(FlgNetBuilder.Build(block.Networks[0], sidecar));
    }

    /// <summary>The one <c>&lt;Component&gt;</c> named <paramref name="name"/> that carries a subscript.</summary>
    private static XElement SubscriptedComponent(XElement xml, string name) =>
        Assert.Single(xml.Descendants()
            .Where(e => e.Name.LocalName == "Component" && e.Attribute("Name")?.Value == name
                        && e.Attribute("AccessModifier") is not null));
}
