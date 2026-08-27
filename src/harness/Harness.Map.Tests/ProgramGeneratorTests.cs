using Harness.Map;

namespace Harness.Map.Tests;

/// <summary>
/// 🔴 <b><see cref="ProgramGenerator"/> — the seam where <see cref="SlotFcGenerator"/>'s obligation stops
/// being a sentence and becomes a check.</b>
///
/// <para>The generator emits, with every slot FC, the words <i>"'X' MUST be called from the cyclic OB,
/// ahead of the copy layer"</i>. Until the OB was generated in the same pass, that sentence was the whole
/// mechanism against the orphan that cost a wave and three hours. These tests are about the SEAM: what it
/// generates, what it refuses, and what it says about the things nobody declared.</para>
///
/// <para><b>Every block here is INVENTED.</b> A green run of this file is not evidence about a real program.</para>
/// </summary>
public class ProgramGeneratorTests
{
    private const string Fb = """
        BLOCK FB FB_Widget
        ROOTID 0
        NUMBER 9100
        LANGUAGE LAD
        TITLE "Widget"

        INTERFACE
          INPUT
          OUTPUT
          STATIC
            Running : Bool
          CONSTANT

        NETWORK 1 "Run"
          COIL Running := Running
        """;

    private const string FbWithStartValues = """
        BLOCK FB FB_Listener
        ROOTID 0
        NUMBER 9101
        LANGUAGE LAD
        TITLE "Listener"

        INTERFACE
          INPUT
          OUTPUT
          STATIC
            ListenPort : UInt = 503
          CONSTANT

        NETWORK 1 "Listen"
          COIL Running := Running
        """;

    private static Dictionary<string, string> Sources() =>
        new(StringComparer.OrdinalIgnoreCase) { ["FB_Widget"] = Fb, ["FB_Listener"] = FbWithStartValues };

    private static InstanceDbDeclaration Db(
        string name = "iDB_Widget",
        int number = 9200,
        string fb = "FB_Widget",
        InstanceDbMemberSource members = InstanceDbMemberSource.ProjectedFromFb) =>
        new(new InstanceDbNaming(name, number, fb, "Synthetic test material."), members);

    private static CyclicObDeclaration Ob(params ObCall[] calls) =>
        new(new CyclicObNaming("OB_HarnessCycle", 1, "ProgramCycle", "Harness Program Sweep"), calls);

    private static LaneGenerationResult LaneWithSlotFc(string slotFc = "FC_HarnessWidgetSlot") =>
        LaneGenerator.Generate(new[]
        {
            new LaneDeclaration(
                "WIDGET",
                new SlotFcNaming(slotFc, 9010),
                new SlotCall("FB_WidgetStim", "iDB_WidgetStim", "Advance The Stimulus"),
                new SlotCall("FB_Widget", "iDB_Widget", "Run The Widget")),
        });

    // ---------------------------------------------------------------------------------------------
    // WHAT IT GENERATES
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_FULL_DECLARATION_GENERATES_THE_OB_AND_THE_INSTANCE_DBS()
    {
        var result = ProgramGenerator.Generate(
            new ProgramDeclaration(
                Ob(new ObCall("FB_Widget", "iDB_Widget", "Run The Widget"),
                   new ObCall("FC_HarnessCopyLayer", null, "Harness Mirror Copy Layer")),
                new[] { Db() }),
            Sources(),
            copyLayerBlock: "FC_HarnessCopyLayer");

        Assert.False(result.Refused);
        Assert.NotNull(result.CyclicOb);
        Assert.Single(result.InstanceDbs);

        // Both kinds reach the caller as deployable objects — and the DB is a DataBlock, which is the kind
        // the harness had never generated before.
        Assert.Equal(
            new[] { ("OB_HarnessCycle", HarnessObjectKind.Block), ("iDB_Widget", HarnessObjectKind.DataBlock) },
            result.Objects.Select(o => (o.Name, o.Kind)).ToArray());
    }

    /// <summary>
    /// 🔴 <b>NOT DECLARED IS SAID OUT LOUD, ON A POSITIVE LINE.</b> An absent line reads as a program with
    /// nothing left to generate, which is the opposite of the truth on every lane that exists today.
    /// </summary>
    [Fact]
    public void WHAT_WAS_NOT_DECLARED_IS_REPORTED_AS_AUTHORED()
    {
        var result = ProgramGenerator.Generate(new ProgramDeclaration(InstanceDbs: new[] { Db() }), Sources());

        Assert.False(result.Refused);
        Assert.Contains(result.NotDeclared, line => line.Contains("no cyclic OB was declared", StringComparison.Ordinal));

        var noDbs = ProgramGenerator.Generate(
            new ProgramDeclaration(Ob(new ObCall("FB_Widget", "iDB_Widget", "Run"))), Sources());

        Assert.Contains(noDbs.NotDeclared, line => line.Contains("no instance DB was declared", StringComparison.Ordinal));
    }

    /// <summary>Nothing declared at all is a legitimate program and is not a refusal.</summary>
    [Fact]
    public void AN_EMPTY_DECLARATION_IS_NOT_A_REFUSAL()
    {
        Assert.False(ProgramGenerator.Generate(null).Refused);
        Assert.False(ProgramGenerator.Generate(new ProgramDeclaration()).Refused);
        Assert.Empty(ProgramGenerator.Generate(null).Objects);
    }

    // ---------------------------------------------------------------------------------------------
    // THE OBLIGATION, MECHANISED
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>THE ORPHAN. A generated slot FC the declared OB does not call REFUSES THE WHOLE PROGRAM.</b>
    /// </summary>
    [Fact]
    public void AN_OB_THAT_DOES_NOT_CALL_A_GENERATED_SLOT_FC_IS_REFUSED()
    {
        var result = ProgramGenerator.Generate(
            new ProgramDeclaration(Ob(new ObCall("FC_HarnessCopyLayer", null, "Copy Layer"))),
            Sources(),
            LaneWithSlotFc(),
            copyLayerBlock: "FC_HarnessCopyLayer");

        Assert.True(result.Refused);
        Assert.Contains(result.Refusals, r => r.Contains("does not call 'FC_HarnessWidgetSlot'", StringComparison.Ordinal));

        // And the refusal carries the generator's own words about WHY, rather than a restatement of them.
        Assert.Contains(result.Refusals, r => r.Contains("never executes", StringComparison.Ordinal));
    }

    /// <summary>🔴 <b>And calling it AFTER the copy layer is refused too — the mirror would be a scan behind.</b></summary>
    [Fact]
    public void AN_OB_THAT_CALLS_THE_SLOT_FC_AFTER_THE_COPY_LAYER_IS_REFUSED()
    {
        var result = ProgramGenerator.Generate(
            new ProgramDeclaration(Ob(
                new ObCall("FC_HarnessCopyLayer", null, "Copy Layer"),
                new ObCall("FC_HarnessWidgetSlot", null, "Widget Slot"))),
            Sources(),
            LaneWithSlotFc(),
            copyLayerBlock: "FC_HarnessCopyLayer");

        Assert.True(result.Refused);
        Assert.Contains(result.Refusals, r => r.Contains("the wrong way round", StringComparison.Ordinal));
    }

    /// <summary>The copy layer itself must be called: without it the mirror never moves.</summary>
    [Fact]
    public void AN_OB_THAT_DOES_NOT_CALL_THE_COPY_LAYER_IS_REFUSED()
    {
        var result = ProgramGenerator.Generate(
            new ProgramDeclaration(Ob(new ObCall("FB_Widget", "iDB_Widget", "Run The Widget"))),
            Sources(),
            copyLayerBlock: "FC_HarnessCopyLayer");

        Assert.True(result.Refused);
        Assert.Contains(result.Refusals, r => r.Contains("the mirror never moves", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------------------------------------
    // THE SILENT COLLISION
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>TWO <c>leftToTia</c> INSTANCES OF ONE FB ARE REFUSED, AND THE REFUSAL LISTS THE START VALUES
    /// THEY WOULD BOTH INHERIT.</b>
    ///
    /// <para>TIA fills each from the FB, so both start identically. If one of those values identifies the
    /// instance — a listening port, a connection ID — the two collide, and not at compile time.</para>
    /// </summary>
    [Fact]
    public void TWO_LEFT_TO_TIA_INSTANCES_OF_ONE_FB_ARE_REFUSED()
    {
        var result = ProgramGenerator.Generate(
            new ProgramDeclaration(InstanceDbs: new[]
            {
                Db("iDB_ListenerA", 9300, "FB_Listener", InstanceDbMemberSource.LeftToTia),
                Db("iDB_ListenerB", 9301, "FB_Listener", InstanceDbMemberSource.LeftToTia),
            }),
            Sources());

        Assert.True(result.Refused);
        Assert.Contains(result.Refusals, r => r.Contains("ListenPort (= 503)", StringComparison.Ordinal));
    }

    /// <summary>One of them is fine, and what it inherits is stated rather than left to be discovered.</summary>
    [Fact]
    public void ONE_LEFT_TO_TIA_INSTANCE_REPORTS_WHAT_IT_INHERITS()
    {
        var result = ProgramGenerator.Generate(
            new ProgramDeclaration(
                Ob(new ObCall("FB_Listener", "iDB_Listener", "Listen")),
                new[] { Db("iDB_Listener", 9300, "FB_Listener", InstanceDbMemberSource.LeftToTia) }),
            Sources());

        Assert.False(result.Refused);
        Assert.Contains(result.Obligations, o => o.Contains("ListenPort (= 503)", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------------------------------------
    // THE REST OF THE REFUSALS
    // ---------------------------------------------------------------------------------------------

    /// <summary>An FB whose IR was not supplied cannot be projected from — the name alone is not enough.</summary>
    [Fact]
    public void AN_INSTANCE_DB_WHOSE_FB_IS_NOT_SUPPLIED_IS_REFUSED()
    {
        var result = ProgramGenerator.Generate(
            new ProgramDeclaration(InstanceDbs: new[] { Db("iDB_Missing", 9400, "FB_Absent") }),
            Sources());

        Assert.True(result.Refused);
        Assert.Contains(result.Refusals, r => r.Contains("was not supplied", StringComparison.Ordinal));
    }

    /// <summary>TIA matches an import by name, so the second file would silently replace the first.</summary>
    [Fact]
    public void TWO_INSTANCE_DBS_WITH_ONE_NAME_ARE_REFUSED()
    {
        var result = ProgramGenerator.Generate(
            new ProgramDeclaration(InstanceDbs: new[] { Db(number: 9200), Db(number: 9201) }),
            Sources());

        Assert.True(result.Refused);
        Assert.Contains(result.Refusals, r => r.Contains("declared twice", StringComparison.Ordinal));
    }

    /// <summary>A number holds one block.</summary>
    [Fact]
    public void TWO_INSTANCE_DBS_WITH_ONE_NUMBER_ARE_REFUSED()
    {
        var result = ProgramGenerator.Generate(
            new ProgramDeclaration(InstanceDbs: new[] { Db("iDB_One"), Db("iDB_Two") }),
            Sources());

        Assert.True(result.Refused);
        Assert.Contains(result.Refusals, r => r.Contains("both declare DB number 9200", StringComparison.Ordinal));
    }

    /// <summary>
    /// 🔴 <b>THE ORPHAN, ONE ARTIFACT OVER: a generated instance DB nothing instantiates.</b> It is imported,
    /// loaded, counted in the manifest and never written, and every value read out of it is the zero it was
    /// loaded with — which reads exactly like a block that ran and did nothing.
    /// </summary>
    [Fact]
    public void AN_INSTANCE_DB_THAT_NOTHING_INSTANTIATES_IS_REFUSED()
    {
        var result = ProgramGenerator.Generate(
            new ProgramDeclaration(Ob(new ObCall("FC_Something", null, "Something")), new[] { Db() }),
            Sources());

        Assert.True(result.Refused);
        Assert.Contains(result.Refusals, r => r.Contains("nothing instantiates it", StringComparison.Ordinal));
    }

    /// <summary>A slot FC's own call counts as instantiating it — the OB is not the only caller in a lane.</summary>
    [Fact]
    public void AN_INSTANCE_DB_NAMED_BY_A_SLOT_FCS_CALL_IS_ACCEPTED()
    {
        var result = ProgramGenerator.Generate(
            new ProgramDeclaration(
                Ob(new ObCall("FC_HarnessWidgetSlot", null, "Widget Slot"),
                   new ObCall("FC_HarnessCopyLayer", null, "Copy Layer")),
                new[] { Db() }),
            Sources(),
            LaneWithSlotFc(),
            "FC_HarnessCopyLayer",
            new[] { "iDB_Widget" });

        Assert.False(result.Refused);
        Assert.Single(result.InstanceDbs);
    }

    /// <summary>
    /// 🔴 <b>A REFUSED REQUEST YIELDS NOTHING USABLE, INCLUDING THE PARTS THAT WORKED.</b> Two thirds of a
    /// program is the orphan with the paperwork filed.
    /// </summary>
    [Fact]
    public void A_REFUSAL_VOIDS_EVERY_OBJECT_IN_THE_RESULT()
    {
        var result = ProgramGenerator.Generate(
            new ProgramDeclaration(
                Ob(new ObCall("FB_Widget", "iDB_Widget", "Run The Widget")),
                new[] { Db(), Db("iDB_Missing", 9400, "FB_Absent") }),
            Sources());

        Assert.True(result.Refused);
        Assert.Empty(result.Objects);
        Assert.Null(result.CyclicOb);
    }
}
