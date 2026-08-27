using Harness.Gate;
using Harness.Map;
using Harness.Run;

namespace Harness.Loop.Tests;

/// <summary>
/// 🔴 <b>FOLLOW THE PROGRAM-LEVEL GENERATION DECLARATION FROM THE BINDING FILE TO THE STAMPED, DEPLOYED
/// IR.</b>
///
/// <para><see cref="LaneGenerationReachesTheRunTests"/> did this for the per-slot half — the slot FC and the
/// stimulus shell. This is the other half: <b>the cyclic OB and the instance DBs</b>. Four hand-authored
/// <c>.ir</c> artifacts, three of which (the instance DBs) are mechanical projections of an FB's interface
/// and one of which (the OB) is an ordered call list whose ORDER is the only part that is not.</para>
///
/// <para>These tests cross the seams no assertion about the generators can enter: the JSON to the domain,
/// the domain to <see cref="LoopRun.Generate"/>, and the generation to the build stamp — <b>which is the one
/// that matters, because a generated block that is not stamped can change the deployed program without
/// moving the number the device is confirmed against.</b></para>
///
/// <para><b>Every block name here is INVENTED.</b> A green run establishes that the wire is connected, not
/// anything about a real program.</para>
/// </summary>
public class ProgramGenerationReachesTheRunTests
{
    private const string Submission = """
    {
      "blockAuthor": "agent-a",
      "runtimeCompression": 1,
      "slotsInWaveSet": 1,
      "maxIndexScans": 200,
      "resultRegistersPerSlot": 4,
      "computedConflicts": [],
      "enumeration": { "clauses": ["REQ-1"], "assertions": ["REQ-1:aaaaaa"] },
      "vectors": []
    }
    """;

    private const string BindingUndeclared = """
    {
      "declaredBy": "agent-k",
      "blockNumber": 9001,
      "baseByte": 1000,
      "declaredRegisters": 576,
      "slots": [{
        "slotId": "WIDGET",
        "vectorTargets": [{ "tag": "Stim_Total", "type": "Time" }],
        "startCondition": "Stim_Start",
        "resultSources": [{ "tag": "Alarm", "type": "Bool",
                            "inertRest": { "value": "false", "basis": "the coil is ANDed with the start condition" } }]
      }]
    }
    """;

    /// <summary>
    /// 🔴 <b>THE WHOLE PROGRAM-LEVEL DECLARATION SURFACE, AND ITS SIZE IS THE POINT.</b> One instance DB
    /// costs six lines plus one per preset; the OB costs four lines plus one per call. The 33-line
    /// <c>.ir</c> file the DB replaces was typed by hand and had to be retyped whenever its FB moved.
    /// </summary>
    private const string BindingDeclared = """
    {
      "declaredBy": "agent-k",
      "blockNumber": 9001,
      "baseByte": 1000,
      "declaredRegisters": 576,
      "slots": [{
        "slotId": "WIDGET",
        "vectorTargets": [{ "tag": "Stim_Total", "type": "Time" }],
        "startCondition": "Stim_Start",
        "resultSources": [{ "tag": "Alarm", "type": "Bool",
                            "inertRest": { "value": "false", "basis": "the coil is ANDed with the start condition" } }]
      }],
      "generateProgram": {
        "cyclicOb": {
          "blockName": "OB_HarnessCycle",
          "number": 1,
          "secondaryType": "ProgramCycle",
          "title": "Harness Program Sweep",
          "comment": "Runs the widget, then publishes the mirror. Invented test material.",
          "calls": [
            { "block": "FB_Widget", "instance": "iDB_Widget", "networkTitle": "Run The Widget",
              "networkComment": "Ahead of the copy layer so the mirror publishes THIS scan's outputs." },
            { "block": "FC_HarnessCopyLayer", "networkTitle": "Harness Mirror Copy Layer" }
          ]
        },
        "instanceDbs": [
          {
            "dbName": "iDB_Widget",
            "dbNumber": 9200,
            "fb": "FB_Widget",
            "comment": "The widget instance the lane exercises. Invented test material.",
            "members": "projectedFromFb",
            "presets": [{ "path": "DwellTime", "value": "T#12S" }]
          }
        ]
      }
    }
    """;

    /// <summary>The FB the instance DB is projected from — supplied through <c>--program</c>, as production does.</summary>
    private const string WidgetFb = """
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
            DwellTime : Time
          CONSTANT

        NETWORK 1 "Run"
          COIL Running := Running
        """;

    private static HarnessObject Fb() => new("FB_Widget", HarnessObjectKind.Block, WidgetFb);

    private static LoopRequest Compose(string binding, params HarnessObject[] program) =>
        LoopCli.Compose(SubmissionDocument.Read(Submission), BindingDocument.Read(binding), program);

    private static LoopGeneration Generate(string binding, params HarnessObject[] program) =>
        LoopRun.Generate(Compose(binding, program), stopWhenInadmissible: false);

    // ---------------------------------------------------------------------------------------------
    // THE WIRE
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void THE_DECLARATION_REACHES_THE_DOMAIN_WHOLE()
    {
        var declaration = Compose(BindingDeclared).ProgramGeneration;

        Assert.NotNull(declaration);
        Assert.Equal("OB_HarnessCycle", declaration!.CyclicOb!.Naming.BlockName);
        Assert.Equal(1, declaration.CyclicOb.Naming.Number);
        Assert.Equal("ProgramCycle", declaration.CyclicOb.Naming.SecondaryType);
        Assert.Equal(2, declaration.CyclicOb.Calls.Count);
        Assert.Null(declaration.CyclicOb.Calls[1].InstancePath);

        var db = Assert.Single(declaration.InstanceDbs!);
        Assert.Equal("iDB_Widget", db.Naming.DbName);
        Assert.Equal(9200, db.Naming.DbNumber);
        Assert.Equal(InstanceDbMemberSource.ProjectedFromFb, db.Members);
        Assert.Equal("T#12S", Assert.Single(db.Presets!).Value);
    }

    /// <summary>
    /// 🔴 <b>NO DEFAULT FOR <c>members</c>, AND A MISSPELT ONE IS A THROW.</b> The two shapes differ in what
    /// happens to the FB's own start values, so a default here would silently decide a question about presets.
    /// </summary>
    [Fact]
    public void AN_INSTANCE_DB_WITH_NO_MEMBERS_SHAPE_IS_REFUSED_AT_PARSE()
    {
        var binding = BindingDeclared.Replace("\"members\": \"projectedFromFb\",", string.Empty, StringComparison.Ordinal);

        var error = Assert.Throws<InvalidDataException>(() => Compose(binding, Fb()));
        Assert.Contains("no `members`", error.Message, StringComparison.Ordinal);
    }

    /// <summary>An OB number with no name, or a name with no number, is a throw rather than a dropped field.</summary>
    [Fact]
    public void A_CYCLIC_OB_WITH_NO_NUMBER_IS_REFUSED_AT_PARSE()
    {
        var binding = BindingDeclared.Replace("\"number\": 1,", string.Empty, StringComparison.Ordinal);

        var error = Assert.Throws<InvalidDataException>(() => Compose(binding, Fb()));
        Assert.Contains("no `number`", error.Message, StringComparison.Ordinal);
    }

    /// <summary>Gate 0b reaches inside the new section: a misspelt key here produces an artifact that is simply absent.</summary>
    [Fact]
    public void AN_UNKNOWN_KEY_IN_THE_PROGRAM_SECTION_IS_REPORTED_BY_GATE_0b()
    {
        var binding = BindingDeclared.Replace("\"dbNumber\": 9200,", "\"dbNumber\": 9200, \"dbNumbr\": 1,", StringComparison.Ordinal);

        var (unknown, _) = BindingDocument.Read(binding).AllExtraFieldPaths();

        Assert.Contains("binding.generateProgram.instanceDbs[0].dbNumbr", unknown);
    }

    // ---------------------------------------------------------------------------------------------
    // THE RUN
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void GENERATE_EMITS_THE_OB_AND_THE_INSTANCE_DB_ALONGSIDE_THE_COPY_LAYER()
    {
        var generation = Generate(BindingDeclared, Fb());

        Assert.True(generation.Generated);
        Assert.NotNull(generation.Program);

        Assert.Equal("OB_HarnessCycle", generation.Program!.CyclicOb!.BlockName);

        var db = Assert.Single(generation.Program.InstanceDbs);
        Assert.Equal("iDB_Widget", db.DbName);

        // The projection, and the ONE declared preset landing on it.
        var ir = Assert.Single(generation.Program.Objects, o => o.Kind == HarnessObjectKind.DataBlock).Ir;
        Assert.Contains("INSTANCEOF FB_Widget", ir, StringComparison.Ordinal);
        Assert.Contains("    DwellTime : Time = T#12S\n", ir, StringComparison.Ordinal);
        Assert.Contains("    Running : Bool\n", ir, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 <b>THE FB COMES FROM <c>--program</c>, AND WITHOUT IT THE RUN REFUSES RATHER THAN EMITTING AN
    /// <c>INSTANCEOF</c> POINTING AT A BLOCK NOBODY SUPPLIED.</b>
    /// </summary>
    [Fact]
    public void AN_INSTANCE_DB_WHOSE_FB_IS_NOT_IN_THE_PROGRAM_SET_STOPS_THE_RUN()
    {
        var generation = Generate(BindingDeclared);

        Assert.False(generation.Generated);
        Assert.Equal(LoopOutcome.NotGeneratable, generation.Stopped);
        Assert.Contains("was not supplied", generation.Detail, StringComparison.Ordinal);

        // And the report survives the stop: a run refused here must still be able to print WHY.
        Assert.NotNull(generation.Program);
        Assert.True(generation.Program!.Refused);
    }

    /// <summary>
    /// 🔴 <b>THE BACKWARD-COMPATIBILITY ASSERTION, AND IT IS ABOUT THE STAMP.</b> Every lane deployed today
    /// declares nothing here. If merely adding the seam moved their stamp, each would report Stale — whose
    /// text says the download "aborted, was refused, or never reached it" — and send somebody to re-download
    /// a device that was already right.
    /// </summary>
    [Fact]
    public void AN_UNDECLARED_PROGRAM_GENERATES_NOTHING_AND_THE_STAMP_IS_BYTE_FOR_BYTE_UNMOVED()
    {
        var withSeam = Generate(BindingUndeclared, Fb());

        Assert.True(withSeam.Generated);
        Assert.Empty(withSeam.Program!.Objects);

        var withoutSeam = LoopRun.Generate(
            Compose(BindingUndeclared, Fb()) with { ProgramGeneration = null },
            stopWhenInadmissible: false);

        Assert.Equal(withoutSeam.Stamp.Value, withSeam.Stamp.Value);
    }

    /// <summary>
    /// 🔴 <b>THE GENERATED OB AND INSTANCE DB ARE IN THE BUILD STAMP, because one EXECUTES and the other is
    /// LOADED.</b>
    ///
    /// <para>The copy layer is the one legitimate exclusion — it embeds the stamp, so hashing it would be
    /// circular. Neither of these embeds anything, so leaving them out would let the declaration change the
    /// deployed program without moving the number the device is confirmed against.</para>
    /// </summary>
    [Fact]
    public void THE_GENERATED_PROGRAM_OBJECTS_ARE_IN_THE_BUILD_STAMP()
    {
        var declared = Generate(BindingDeclared, Fb());
        var undeclared = Generate(BindingUndeclared, Fb());

        Assert.Contains(declared.Manifest!.Objects, o => o.Name == "OB_HarnessCycle");
        Assert.Contains(declared.Manifest.Objects, o => o.Name == "iDB_Widget");
        Assert.DoesNotContain(undeclared.Manifest!.Objects, o => o.Name == "OB_HarnessCycle");
        Assert.NotEqual(undeclared.Stamp.Value, declared.Stamp.Value);

        // And the converse, without which the above would pass against a stamp that is simply unstable.
        Assert.Equal(declared.Stamp.Value, Generate(BindingDeclared, Fb()).Stamp.Value);
    }

    /// <summary>
    /// 🔴 <b>A CHANGED PRESET MOVES THE STAMP.</b> A preset is what the block STARTS FROM; two deployments
    /// whose instance DBs differ only in a threshold are materially different programs, and a stamp that
    /// could not tell them apart would confirm one while the other ran.
    /// </summary>
    [Fact]
    public void A_CHANGED_PRESET_MOVES_THE_STAMP()
    {
        var original = Generate(BindingDeclared, Fb());
        var changed = Generate(BindingDeclared.Replace("T#12S", "T#13S", StringComparison.Ordinal), Fb());

        Assert.NotEqual(original.Stamp.Value, changed.Stamp.Value);
    }
}
