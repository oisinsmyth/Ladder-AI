using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// Sidecar synthesis (2026-07-15) — SidecarSynthesizer.Synthesize produces a fresh NetworkSidecar
/// directly from an IrNetwork's own Expr tree, for networks that never existed in TIA before (no
/// real donor export to clone). Scope: plain COIL/SCOIL/RCOIL AND/OR/NOT chains only.
///
/// Assertions here check structural correctness (types, ascending order, negation) rather than
/// hardcoded UId literals — unlike GraphReducer's own tests (which read real fixtures with known,
/// fixed values), every number here is minted by the algorithm itself, so the meaningful property
/// is "internally consistent and correctly ordered," not "matches this specific literal." True
/// collision-freedom is verified by the semantic-fidelity round trip (SidecarSynthesizerFidelityTests)
/// and the live TIA import (SynthesizerLiveCheck) — a naive "every UId anywhere is globally
/// distinct" check doesn't hold here by design (CoilOperandAccessUId/OperandAccessUId are meant to
/// equal the AccessUIds entry they reference, and rail wires are meant to be shared).
/// </summary>
public class SidecarSynthesizerTests
{
    private static NetworkSidecar Synthesize(string readableNetworkText)
    {
        var network = IrParser.ParseNetworkOnly(readableNetworkText);
        return SidecarSynthesizer.Synthesize(network);
    }

    // Collects every step-owned UId (Contact/Or/Not Part UIds, wires) recursively, including
    // nested OrBranch/NotStep — used only for the "children mint before parent" ordering checks
    // below, not for any distinctness assertion (see class doc comment for why not).
    private static void CollectStepUIds(IReadOnlyList<ChainStepSidecar> steps, List<int> uids)
    {
        foreach (var step in steps)
        {
            switch (step)
            {
                case ChainStepSidecar.ContactStep contact:
                    uids.Add(contact.ContactUId);
                    uids.Add(contact.OperandAccessUId);
                    uids.Add(contact.OperandWireUId);
                    uids.Add(contact.OutgoingWireUId);
                    break;
                case ChainStepSidecar.OrStep orStep:
                    uids.Add(orStep.OrPartUId);
                    uids.Add(orStep.OutgoingWireUId);
                    foreach (var branch in orStep.Branches)
                    {
                        if (branch.RailWireUId is int branchRail)
                        {
                            uids.Add(branchRail);
                        }

                        CollectStepUIds(branch.Steps, uids);
                    }

                    break;
                case ChainStepSidecar.NotStep notStep:
                    uids.Add(notStep.NotPartUId);
                    uids.Add(notStep.OutgoingWireUId);
                    if (notStep.RailWireUId is int notRail)
                    {
                        uids.Add(notRail);
                    }

                    CollectStepUIds(notStep.Steps, uids);
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected step kind in test helper: {step.GetType().Name}");
            }
        }
    }

    [Fact]
    public void Synthesize_PlainAndChain_ProducesSequentialAscendingContacts()
    {
        var sidecar = Synthesize("NETWORK 1 \"Test\"\n  COIL X := A AND B AND C\n");

        var assignment = Assert.Single(sidecar.Assignments);
        Assert.Equal(3, assignment.Steps.Count);
        var contacts = assignment.Steps.Select(s => Assert.IsType<ChainStepSidecar.ContactStep>(s)).ToList();
        Assert.All(contacts, c => Assert.False(c.Negated));

        // Rail-to-coil order (A, B, C) must mint ascending ContactUId — FlgNetBuilder trusts
        // ascending Part UId to already mean signal-flow order.
        Assert.True(contacts[0].ContactUId < contacts[1].ContactUId);
        Assert.True(contacts[1].ContactUId < contacts[2].ContactUId);
        Assert.NotNull(assignment.RailWireUId);

        Assert.Equal(new[] { "A", "B", "C", "X" }, sidecar.AccessUIds.Select(a => a.TagPath));
        Assert.All(sidecar.AccessUIds, a => Assert.Equal("GlobalVariable", a.Scope));

    }

    [Fact]
    public void Synthesize_OrOfTwoAnds_MatchesRealInputMappingPatternShape()
    {
        // The exact shape every input-mapping/output-mapping rung uses:
        // AlwaysTrue AND NOT Test[0] AND DI1 OR AlwaysTrue AND Test[1]
        var sidecar = Synthesize(
            "NETWORK 1 \"Test\"\n" +
            "  COIL Input.Foo := AlwaysTrue AND NOT DiscreteInputs.Test[0] AND DI1 OR AlwaysTrue AND DiscreteInputs.Test[1]\n");

        var assignment = Assert.Single(sidecar.Assignments);
        Assert.Null(assignment.RailWireUId); // terminates at the OrStep, same as every real OR-merge-fed coil
        var orStep = Assert.IsType<ChainStepSidecar.OrStep>(Assert.Single(assignment.Steps));
        Assert.Equal(2, orStep.Branches.Count);

        var branch0 = orStep.Branches[0].Steps;
        Assert.Equal(3, branch0.Count);
        Assert.False(Assert.IsType<ChainStepSidecar.ContactStep>(branch0[0]).Negated); // AlwaysTrue
        Assert.True(Assert.IsType<ChainStepSidecar.ContactStep>(branch0[1]).Negated);  // NOT Test[0]
        Assert.False(Assert.IsType<ChainStepSidecar.ContactStep>(branch0[2]).Negated); // DI1

        var branch1 = orStep.Branches[1].Steps;
        Assert.Equal(2, branch1.Count);
        Assert.False(Assert.IsType<ChainStepSidecar.ContactStep>(branch1[0]).Negated); // AlwaysTrue
        Assert.False(Assert.IsType<ChainStepSidecar.ContactStep>(branch1[1]).Negated); // Test[1]

        // Both branches share one rail wire — confirmed real shape (every OR-merge branch in every
        // grounded example this session shares its rail).
        Assert.Equal(orStep.Branches[0].RailWireUId, orStep.Branches[1].RailWireUId);
        Assert.NotNull(orStep.Branches[0].RailWireUId);

        // The O Part's own UId must be numerically greater than every UId minted for its branches
        // (children-before-self minting => ascending UId already means signal-flow order).
        var branchUIds = new List<int>();
        CollectStepUIds(orStep.Branches.SelectMany(b => b.Steps).ToList(), branchUIds);
        Assert.All(branchUIds, u => Assert.True(u < orStep.OrPartUId));

        Assert.Equal(
            new[] { "AlwaysTrue", "DiscreteInputs.Test[0]", "DI1", "AlwaysTrue", "DiscreteInputs.Test[1]", "Input.Foo" },
            sidecar.AccessUIds.Select(a => a.TagPath));

    }

    [Fact]
    public void Synthesize_NestedOr_ProducesNestedOrStep()
    {
        var sidecar = Synthesize("NETWORK 1 \"Test\"\n  COIL X := A OR (B OR C)\n");

        var assignment = Assert.Single(sidecar.Assignments);
        var outerOr = Assert.IsType<ChainStepSidecar.OrStep>(Assert.Single(assignment.Steps));
        Assert.Equal(2, outerOr.Branches.Count);

        var firstBranchStep = Assert.Single(outerOr.Branches[0].Steps);
        Assert.IsType<ChainStepSidecar.ContactStep>(firstBranchStep); // A

        var secondBranchStep = Assert.Single(outerOr.Branches[1].Steps);
        var innerOr = Assert.IsType<ChainStepSidecar.OrStep>(secondBranchStep);
        Assert.Equal(2, innerOr.Branches.Count);
        Assert.Null(outerOr.Branches[1].RailWireUId); // this branch terminates at the inner OrStep, not rail

        // Inner O's own UId must exceed everything inside it, and the outer O's own UId must
        // exceed the inner O's (and everything else) — same children-before-self invariant, one
        // level deeper.
        Assert.True(innerOr.Branches.SelectMany(b => AllStepUIdsForAssertion(b.Steps)).All(u => u < innerOr.OrPartUId));
        Assert.True(innerOr.OrPartUId < outerOr.OrPartUId);

    }

    private static IEnumerable<int> AllStepUIdsForAssertion(IReadOnlyList<ChainStepSidecar> steps)
    {
        var uids = new List<int>();
        CollectStepUIds(steps, uids);
        return uids;
    }

    [Fact]
    public void Synthesize_StandaloneNotOfCompound_ProducesNotStep()
    {
        var sidecar = Synthesize("NETWORK 1 \"Test\"\n  COIL X := NOT (A AND B)\n");

        var assignment = Assert.Single(sidecar.Assignments);
        var notStep = Assert.IsType<ChainStepSidecar.NotStep>(Assert.Single(assignment.Steps));
        Assert.Equal(2, notStep.Steps.Count);
        Assert.IsType<ChainStepSidecar.ContactStep>(notStep.Steps[0]);
        Assert.IsType<ChainStepSidecar.ContactStep>(notStep.Steps[1]);
        Assert.NotNull(notStep.RailWireUId); // its own nested chain is a plain 2-contact AND, rail-fed

    }

    [Fact]
    public void Synthesize_NegatedLeafMidChain_ProducesNegatedContactNotStandaloneNot()
    {
        var sidecar = Synthesize("NETWORK 1 \"Test\"\n  COIL X := A AND NOT B\n");

        var assignment = Assert.Single(sidecar.Assignments);
        Assert.Equal(2, assignment.Steps.Count);
        Assert.False(Assert.IsType<ChainStepSidecar.ContactStep>(assignment.Steps[0]).Negated);
        Assert.True(Assert.IsType<ChainStepSidecar.ContactStep>(assignment.Steps[1]).Negated);
    }

    [Fact]
    public void Synthesize_BareLeaf_ProducesSingleContact()
    {
        var sidecar = Synthesize("NETWORK 1 \"Test\"\n  COIL X := A\n");

        var assignment = Assert.Single(sidecar.Assignments);
        var contact = Assert.IsType<ChainStepSidecar.ContactStep>(Assert.Single(assignment.Steps));
        Assert.False(contact.Negated);
        Assert.NotNull(assignment.RailWireUId);
    }

    [Fact]
    public void Synthesize_TrueSentinel_ProducesEmptySteps()
    {
        var sidecar = Synthesize("NETWORK 1 \"Test\"\n  COIL X := TRUE\n");

        var assignment = Assert.Single(sidecar.Assignments);
        Assert.Empty(assignment.Steps);
        Assert.NotNull(assignment.RailWireUId); // wired directly to rail
        Assert.Single(sidecar.AccessUIds); // just the coil's own tag, no contacts
    }

    [Fact]
    public void Synthesize_SCoilAndRCoil_ShareOneRailWireWithPlainCoil()
    {
        var sidecar = Synthesize(
            "NETWORK 1 \"Test\"\n" +
            "  COIL Output.Run := StartCmd\n" +
            "  SCOIL Motor1.Latched := SetCmd\n" +
            "  RCOIL Motor1.Latched := ResetCmd\n");

        Assert.Equal(3, sidecar.Assignments.Count);
        var rails = sidecar.Assignments.Select(a => a.RailWireUId).ToList();
        Assert.All(rails, r => Assert.NotNull(r));
        Assert.Equal(rails[0], rails[1]);
        Assert.Equal(rails[1], rails[2]);

    }

    [Fact]
    public void Synthesize_CompoundOperandNotRailMost_ThrowsUnsupportedSynthesisConstruct()
    {
        var network = IrParser.ParseNetworkOnly("NETWORK 1 \"Test\"\n  COIL X := A AND (B OR C)\n");

        var ex = Assert.Throws<UnsupportedSynthesisConstructException>(() => SidecarSynthesizer.Synthesize(network));
        Assert.Contains("first operand", ex.Message);
    }

    [Fact]
    public void Synthesize_Compare_BuildsCompareStepAndFeedsFlgNetBuilderWithoutError()
    {
        var network = IrParser.ParseNetworkOnly("NETWORK 1 \"Test\"\n  COIL X := A = 1\n");
        var sidecar = SidecarSynthesizer.Synthesize(network);

        var step = Assert.Single(sidecar.Assignments.Single().Steps);
        var compare = Assert.IsType<ChainStepSidecar.CompareStep>(step);
        Assert.Equal("Eq", compare.PartName);

        var flgNetwork = FlgNetBuilder.Build(network, sidecar);
        Assert.NotEmpty(flgNetwork.Parts);
    }

    [Fact]
    public void Synthesize_OutOfScopeInstructionList_NamesTheConstruct()
    {
        var network = IrParser.ParseNetworkOnly(
            "NETWORK 1 \"Test\"\n" +
            "  WAND(EN := TRUE, IN1 := A, IN2 := B) => C\n");

        var ex = Assert.Throws<UnsupportedSynthesisConstructException>(() => SidecarSynthesizer.Synthesize(network));
        Assert.Contains("WordAnds", ex.Message);
    }

    [Fact]
    public void SynthesizeBlock_MintsDistinctCompileUnitUIdsPerNetwork()
    {
        var network1 = IrParser.ParseNetworkOnly("NETWORK 1 \"First\"\n  COIL X := A\n");
        var network2 = IrParser.ParseNetworkOnly("NETWORK 2 \"Second\"\n  COIL Y := B\n") with { Number = 2 };
        var block = new IrBlock("0", "FC", "Test", 1, "LAD", null, new[] { network1, network2 });

        var sidecars = SidecarSynthesizer.SynthesizeBlock(block);

        Assert.Equal(2, sidecars.Count);
        Assert.NotEqual(sidecars[0].CompileUnitUId, sidecars[1].CompileUnitUId);
    }

    [Fact]
    public void Synthesize_OrOfTwoAnds_FeedsFlgNetBuilderWithoutError()
    {
        // Direct smoke test: synthesizer output must satisfy FlgNetBuilder's own pre-existing
        // leaf-count cross-check for real, not just look plausible in isolation.
        var network = IrParser.ParseNetworkOnly(
            "NETWORK 1 \"Test\"\n" +
            "  COIL Input.Foo := AlwaysTrue AND NOT DiscreteInputs.Test[0] AND DI1 OR AlwaysTrue AND DiscreteInputs.Test[1]\n");
        var sidecar = SidecarSynthesizer.Synthesize(network);

        var flgNetwork = FlgNetBuilder.Build(network, sidecar);

        Assert.NotEmpty(flgNetwork.Parts);
        Assert.NotEmpty(flgNetwork.Wires);
        // AlwaysTrue, Test[0], DI1, AlwaysTrue (again — fresh per reference, no dedup), Test[1], Input.Foo
        Assert.Equal(6, flgNetwork.AccessNodes.Count);
    }

    // Regression test for a real bug caught only by live TIA import (never by the in-memory
    // fidelity round trip, which doesn't validate XML-level UId uniqueness the way TIA's own
    // Import() does): a TON/CALL's own instance reference must NOT also appear in AccessUIds — its
    // UId is embedded directly into the Part's own <Instance> sub-element by FlgNetBuilder
    // (BuildTimer/BuildCall), so a duplicate top-level Access entry for the same UId produced a
    // genuine "There are at least two definitions for UIds" rejection on real TIA import. Found
    // 2026-07-15 building the Kestrel Shredder System's FB_PusherControl.
    [Fact]
    public void Synthesize_Timer_InstanceUIdNotDuplicatedInAccessEntries()
    {
        var network = IrParser.ParseNetworkOnly(
            "NETWORK 1 \"Test\"\n" +
            "  TON(SomeTimer, IN := Start, PT := PresetMS)\n");
        var sidecar = SidecarSynthesizer.Synthesize(network);

        var instanceUId = sidecar.Timers.Single().InstanceUId;
        Assert.DoesNotContain(sidecar.AccessUIds, a => a.UId == instanceUId);
    }

    [Fact]
    public void Synthesize_Call_InstanceUIdNotDuplicatedInAccessEntries()
    {
        var network = IrParser.ParseNetworkOnly(
            "NETWORK 1 \"Test\"\n" +
            "  CALL SomeFb(iDB_SomeFb, EN := TRUE)\n");
        var sidecar = SidecarSynthesizer.Synthesize(network);

        var instanceUId = sidecar.Calls.Single().InstanceUId;
        Assert.DoesNotContain(sidecar.AccessUIds, a => a.UId == instanceUId);
    }

    // Regression test for the real bug that blocked live import entirely (174 "tag not defined"
    // errors covering every reference in FB_PusherControl, including its own Step): SynthesizeBlock
    // must mark a reference to the enclosing block's own declared Static/Temp/Input/Output/InOut
    // member as LocalVariable (the "#" a human types in the TIA editor for an internal reference,
    // encoded as Scope in the XML rather than shown in this IR's own text) — not GlobalVariable,
    // the synthesizer's own correct default for a genuinely external reference (a different DB's
    // member, a physical tag). Exhaustively grounded against MotorStarter.ir's own real sidecar
    // (every access in that whole fixture is LocalVariable — see ScopeFor's own doc comment).
    [Fact]
    public void SynthesizeBlock_ReferenceToOwnStaticMember_IsLocalVariableScope()
    {
        var network = IrParser.ParseNetworkOnly(
            "NETWORK 1 \"Test\"\n" +
            "  COIL LocalBit := ExternalDb.SomeTag\n");
        var block = new IrBlock("0", "FB", "Test", 1, "LAD", null, new[] { network },
            StaticMembers: new[] { new DbMember("LocalBit", "Bool", false, null) });

        var sidecar = SidecarSynthesizer.SynthesizeBlock(block).Single();

        var coilEntry = sidecar.AccessUIds.Single(a => a.TagPath == "LocalBit");
        Assert.Equal("LocalVariable", coilEntry.Scope);
        var externalEntry = sidecar.AccessUIds.Single(a => a.TagPath == "ExternalDb.SomeTag");
        Assert.Equal("GlobalVariable", externalEntry.Scope);
    }

    // Regression test for a real IrParser bug (not SidecarSynthesizer, but found by the same
    // FB_PusherControl build): a decimal literal like "1000.0" (MotorStarter's own real HMI-
    // seconds-to-milliseconds scale factor) fell through ParseLeaf's old integer-only regex and
    // was misparsed as a dotted tag path ("1000"."0") — every prior real fixture reaching this
    // exact literal text arrived via the *other* direction (GraphReducer reading real XML
    // directly, never exercising this text-shape guess at all), so the gap was invisible until
    // this build hand-authored the text form for the first time. TIA's own rejection ('Tag
    // "1000"."0" not defined') is what surfaced it.
    [Fact]
    public void ParseNetworkOnly_DecimalLiteral_ParsesAsLiteralNotTagRef()
    {
        var network = IrParser.ParseNetworkOnly(
            "NETWORK 1 \"Test\"\n" +
            "  MUL(EN := TRUE, IN1 := SomeTag, IN2 := 1000.0) => Dest\n");

        var mul = network.Muls.Single();
        Assert.Equal(new Expr.Literal("1000.0"), mul.Inputs[1]);
    }
}
