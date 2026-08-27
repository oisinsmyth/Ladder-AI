using Harness.Map;

namespace Harness.Map.Tests;

/// <summary>
/// 🔴 <b>A GENERATOR NOTHING CALLS REMOVES NO WORK.</b>
///
/// <para>That sentence is written twice in this component already — in <see cref="LaneGenerator"/>'s
/// header and again at its call site in <c>LoopRun</c> — because <see cref="SlotFcGenerator"/> and
/// <see cref="StimShellGenerator"/> both existed, both passed their own tests, and both were reachable
/// only from those tests while the blocks they generate went on being hand-authored on every lane.</para>
///
/// <para>This file is the check that the last two artifacts did not land the same way. It asserts they
/// come out of the PRODUCTION composition points — <see cref="LaneGenerator"/> for the stimulus UDT and
/// <see cref="ProgramGenerator"/> for the comms FB — and that each carries its refusals through those
/// points rather than only through its own entry point.</para>
/// </summary>
public class LastTwoArtifactsReachProductionTests
{
    // --- the stimulus UDT, through the lane -----------------------------------------------------------

    private static StimHeadSpec Head() => new(
        "FB_WidgetJamStim", "Uut.FaultReset", "T#600S", "T#2S",
        new[]
        {
            new StimPhase("InHeadDisarm", "HeadDisarmEnd", "DisarmDwell", StimPhaseKind.Disarm),
            new StimPhase("InHeadReset", "HeadResetEnd", "ResetDwell", StimPhaseKind.Reset),
            new StimPhase("InHeadVerify", "HeadEnd", "VerifyDwell", StimPhaseKind.Verify),
            new StimPhase("InScenario", "ScenarioEnd", "Stim.RunFor", StimPhaseKind.Scenario),
            new StimPhase("InTailVerify", "TailEnd", "VerifyDwell", StimPhaseKind.Verify),
        },
        new[] { "Uut.JamAlarmLatched" },
        Array.Empty<string>(),
        new[]
        {
            "Stim.ArrivedDirty", "Stim.InertAtStart", "Stim.RecoverFailed",
            "Stim.InertAtEnd", "Stim.ScenarioDone",
        });

    private static StimUdtDeclaration Udt() => new(
        new StimUdtNaming("UDT_WidgetJamStim", "Widget-jam stimulus vocabulary. Synthetic test material."),
        new[] { new StimUdtMember("Phase", "Int"), new StimUdtMember("ResetMode", "Int") });

    [Fact]
    public void A_DECLARED_STIMULUS_UDT_IS_GENERATED_BY_THE_LANE_AND_IS_A_DEPLOYABLE_OBJECT()
    {
        var lane = LaneGenerator.Generate(new[]
        {
            new LaneDeclaration("SLOT-A", StimHead: Head(), StimUdt: Udt()),
        });

        Assert.False(lane.Refused, string.Join(" | ", lane.Refusals));

        var udt = Assert.Single(lane.Slots).StimUdt;
        Assert.NotNull(udt);
        Assert.StartsWith("TYPE UDT_WidgetJamStim\n", udt!.Ir, StringComparison.Ordinal);

        // 🔴 A TYPE, NOT A DATA BLOCK. HarnessObjectKind.DataType exists precisely so a UDT is not handed
        // a data block's retention rules on its way into the build stamp.
        var obj = Assert.Single(lane.Objects.Where(o => o.Kind == HarnessObjectKind.DataType));
        Assert.Equal("UDT_WidgetJamStim", obj.Name);

        Assert.Contains("1 stimulus UDT(s) GENERATED", lane.Summary(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_SLOT_THAT_DECLARES_NO_UDT_IS_REPORTED_AS_AUTHORED_RATHER_THAN_PASSED_OVER()
    {
        var lane = LaneGenerator.Generate(new[] { new LaneDeclaration("SLOT-A") });

        Assert.Contains(lane.NotDeclared, n => n.Contains("no stimulus UDT was declared", StringComparison.Ordinal));
        Assert.Contains(lane.NotDeclared, n => n.Contains("is AUTHORED", StringComparison.Ordinal));
    }

    /// <summary>
    /// 🔴 <b>The UDT is derived from the EMITTED RUNGS, so a UDT declared with no head has nothing behind
    /// it</b> — and the lane refuses rather than deriving it from the spec instead.
    /// </summary>
    [Fact]
    public void A_UDT_DECLARED_WITHOUT_A_HEAD_IS_REFUSED()
    {
        var lane = LaneGenerator.Generate(new[] { new LaneDeclaration("SLOT-A", StimUdt: Udt()) });

        Assert.True(lane.Refused);
        Assert.Contains(lane.Refusals, r => r.Contains("no rungs to derive the type from", StringComparison.Ordinal));

        // A refused request yields nothing usable, including the parts that worked.
        Assert.Empty(lane.Slots);
        Assert.Empty(lane.Objects);
    }

    // --- the comms FB, through the program ------------------------------------------------------------

    private static MirrorGeometry Geometry() => MirrorGeometry.ForCpu1214C(16, baseByte: 2400, declaredRegisters: 41);

    private static CommsFbDeclaration Comms() => new(
        new CommsFbNaming("FB_Comms_WidgetMirrorServer", 9411, "Widget Mirror Server",
            $"Serves {CommsFbGenerator.RegistersPlaceholder} registers of the widget mirror. Synthetic test material."),
        new CommsEndpoint(1502, "16#0021", 64),
        new CommsNetworkText("Serve The Widget Mirror", "Runs the server every scan. Synthetic test material."));

    private static CyclicObDeclaration Ob(params ObCall[] calls) =>
        new(new CyclicObNaming("OB_HarnessCycle", 1, "ProgramCycle", "Harness Program Sweep",
            "Sweeps the widget lane once per scan. Synthetic test material."), calls);

    [Fact]
    public void A_DECLARED_COMMS_FB_IS_GENERATED_BY_THE_PROGRAM_AND_SERVES_THE_MAPS_OWN_WINDOW()
    {
        var result = ProgramGenerator.Generate(
            new ProgramDeclaration(
                Ob(new ObCall("FB_Comms_WidgetMirrorServer", "iDB_WidgetMirrorServer", "Serve The Mirror"),
                   new ObCall("FC_HarnessCopyLayer", null, "Harness Mirror Copy Layer")),
                CommsFb: Comms()),
            copyLayerBlock: "FC_HarnessCopyLayer",
            geometry: Geometry());

        Assert.False(result.Refused, string.Join(" | ", result.Refusals));
        Assert.NotNull(result.CommsFb);
        Assert.Equal("P#M2400.0 WORD 41", result.CommsFb!.AreaPointer);
        Assert.Contains(result.Objects, o => o.Name == "FB_Comms_WidgetMirrorServer" && o.Kind == HarnessObjectKind.Block);
        Assert.Contains("1 comms FB", result.Summary(), StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 <b>A SERVER NOTHING CALLS LISTENS ON NOTHING, and every client read then fails as a CONNECTION
    /// error — which reads as a cable, a firewall or a CPU.</b> The OB's call list is checked for it, the
    /// way the slot FC's call-site obligation is.
    /// </summary>
    [Fact]
    public void AN_OB_THAT_DOES_NOT_CALL_THE_GENERATED_SERVER_IS_REFUSED()
    {
        var result = ProgramGenerator.Generate(
            new ProgramDeclaration(
                Ob(new ObCall("FC_HarnessCopyLayer", null, "Harness Mirror Copy Layer")),
                CommsFb: Comms()),
            copyLayerBlock: "FC_HarnessCopyLayer",
            geometry: Geometry());

        Assert.True(result.Refused);
        Assert.Contains(result.Refusals, r => r.Contains("FB_Comms_WidgetMirrorServer", StringComparison.Ordinal));
        Assert.Null(result.CommsFb);
    }

    /// <summary>The served window IS the geometry; with none supplied there is nothing to point at.</summary>
    [Fact]
    public void A_COMMS_FB_DECLARED_WITH_NO_GEOMETRY_IS_REFUSED()
    {
        var result = ProgramGenerator.Generate(new ProgramDeclaration(CommsFb: Comms()));

        Assert.True(result.Refused);
        Assert.Contains(result.Refusals, r => r.Contains("no mirror geometry was supplied", StringComparison.Ordinal));
    }

    [Fact]
    public void A_PROGRAM_THAT_DECLARES_NO_COMMS_FB_IS_REPORTED_AS_AUTHORED()
    {
        var result = ProgramGenerator.Generate(
            new ProgramDeclaration(Ob(new ObCall("FC_HarnessCopyLayer", null, "Harness Mirror Copy Layer"))),
            copyLayerBlock: "FC_HarnessCopyLayer");

        Assert.False(result.Refused, string.Join(" | ", result.Refusals));
        Assert.Contains(result.NotDeclared, n => n.Contains("no comms FB was declared", StringComparison.Ordinal));
        Assert.Contains(result.NotDeclared, n => n.Contains("is AUTHORED", StringComparison.Ordinal));
    }

    /// <summary>
    /// 🔴 <b>THE GENERATED SERVER IS AN FB SOURCE FOR ITS OWN INSTANCE DB</b>, so a caller does not have
    /// to hand back a snapshot of a block this same pass emits. And because MB_SERVER and TCON_IP_v4 are
    /// system-type instances <see cref="InstanceDbGenerator"/> refuses to project, that instance DB is the
    /// one shape in this component that must be declared <c>leftToTia</c> — which the projection path
    /// refuses by name rather than by silence.
    /// </summary>
    [Fact]
    public void THE_GENERATED_SERVER_IS_ITS_OWN_INSTANCE_DBS_FB_SOURCE()
    {
        var declaration = new ProgramDeclaration(
            Ob(new ObCall("FB_Comms_WidgetMirrorServer", "iDB_WidgetMirrorServer", "Serve The Mirror"),
               new ObCall("FC_HarnessCopyLayer", null, "Harness Mirror Copy Layer")),
            new[]
            {
                new InstanceDbDeclaration(
                    new InstanceDbNaming("iDB_WidgetMirrorServer", 9411, "FB_Comms_WidgetMirrorServer",
                        "State of the widget mirror server. Synthetic test material."),
                    InstanceDbMemberSource.LeftToTia),
            },
            Comms());

        var result = ProgramGenerator.Generate(
            declaration, copyLayerBlock: "FC_HarnessCopyLayer", geometry: Geometry());

        Assert.False(result.Refused, string.Join(" | ", result.Refusals));

        var db = Assert.Single(result.InstanceDbs);
        Assert.Equal("FB_Comms_WidgetMirrorServer", db.FbName);

        // The listening port and the connection ID are FB start values, so the instance INHERITS them —
        // reported, never passed over, because they are what a second instance would silently collide on.
        Assert.Contains(db.InheritedStartValues, v => v.Contains("Comms.LocalPort", StringComparison.Ordinal));
        Assert.Contains(db.InheritedStartValues, v => v.Contains("Comms.ID", StringComparison.Ordinal));

        // Projecting it instead is refused by name — MB_SERVER carries a VERSION and is not one of the two
        // system types grounded in the committed corpus.
        var projected = ProgramGenerator.Generate(
            declaration with
            {
                InstanceDbs = new[]
                {
                    declaration.InstanceDbs![0] with { Members = InstanceDbMemberSource.ProjectedFromFb },
                },
            },
            copyLayerBlock: "FC_HarnessCopyLayer",
            geometry: Geometry());

        Assert.True(projected.Refused);
        Assert.Contains(projected.Refusals, r => r.Contains("MB_SERVER", StringComparison.Ordinal));
    }
}
