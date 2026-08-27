using Harness.Gate;
using Harness.Map;
using Harness.Run;

namespace Harness.Loop.Tests;

/// <summary>
/// 🔴 <b>THE COMMS FB AND THE STIMULUS UDT, FROM A BINDING DOCUMENT TO THE STAMPED IR — AND A COMMIT
/// MESSAGE SAID THIS ALREADY WORKED.</b>
///
/// <para><c>d61652c</c> claimed <i>"Both wired to production: <c>LaneDeclaration.StimUdt</c> through
/// <c>LaneGenerator</c>, and <c>ProgramDeclaration.CommsFb</c> through <c>ProgramGenerator</c>."</i> Both
/// halves of that sentence were true and neither was the claim that mattered. The two generators were
/// reachable from the two <i>Map-level composition points</i> — which is what
/// <c>Harness.Map.Tests.LastTwoArtifactsReachProductionTests</c> asserts — and unreachable from the only
/// artifact a coordinator writes: <c>LoopCli.ToProgramDeclaration</c> constructed a TWO-argument
/// <c>ProgramDeclaration</c>, <c>ToLaneDeclaration</c> constructed a FIVE-argument
/// <c>LaneDeclaration</c>, and neither wire document carried the key.</para>
///
/// <para><b>The symptom was the dangerous one.</b> A binding that declared both ran and reported
/// <c>AUTHORED — no comms FB was declared, so none was generated</c>: a true sentence about a document
/// that plainly asked for generation, which <c>BindingDocument</c>'s own gate-0b note already names as the
/// quietest failure of any level in that file.</para>
///
/// <para><b>So these tests deliberately start at the JSON</b>, not at a typed declaration. A test that
/// builds a <c>ProgramDeclaration</c> in C# and asserts the generator honours it passes on the broken
/// build — that is exactly what the existing suite did, 449 green, while the capability was unreachable.
/// <b>Every name here is INVENTED.</b></para>
/// </summary>
public class TheLastTwoArtifactsReachABindingTests
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

    /// <summary>
    /// A lane declaring the stimulus head (which the UDT is derived FROM) and the stimulus UDT, and a
    /// program declaring the cyclic OB and the comms FB. <b>The served window is deliberately absent from
    /// the <c>commsFb</c> section</b>: it is <c>baseByte</c> and <c>declaredRegisters</c> at the top of this
    /// document, handed to the generator as the map's own geometry, so the emitted area pointer cannot
    /// disagree with the area this run allocated against.
    /// </summary>
    private const string Binding = """
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
                            "inertRest": { "value": "false", "basis": "the coil is ANDed with the start condition" } }],
        "generate": {
          "stimHead": {
            "headName": "FB_WidgetStim",
            "uutReset": "iDB_WidgetUnderTest.Cmd.FaultReset",
            "watchdog": "T#5M",
            "dwell": "T#200MS",
            "phases": [
              { "bit": "InHeadDisarm", "end": "HeadDisarmEnd", "duration": "DisarmDwell", "kind": "Disarm" },
              { "bit": "InHeadReset",  "end": "HeadResetEnd",  "duration": "ResetDwell",  "kind": "Reset" },
              { "bit": "InHeadVerify", "end": "HeadEnd",       "duration": "VerifyDwell", "kind": "Verify" },
              { "bit": "InScenario",   "end": "ScenEnd",       "duration": "Stim.EndAt",  "kind": "Scenario" },
              { "bit": "InTailVerify", "end": "TailEnd",       "duration": "VerifyDwell", "kind": "Verify" }
            ],
            "causes": ["iDB_WidgetUnderTest.Status.JamLatched"],
            "cycleEdges": [],
            "outcomeBits": [
              "Stim.ArrivedDirty", "Stim.InertAtStart", "Stim.RecoverFailed",
              "Stim.InertAtEnd", "Stim.ScenarioDone"
            ]
          },
          "stimUdt": {
            "typeName": "UDT_WidgetStim",
            "comment": "The vocabulary the widget plant model is commanded in. Invented test material.",
            "members": [
              { "name": "Phase",     "datatype": "Int" },
              { "name": "ResetMode", "datatype": "Int" }
            ]
          }
        }
      }],
      "generateProgram": {
        "cyclicOb": {
          "blockName": "OB_HarnessCycle",
          "number": 1,
          "secondaryType": "ProgramCycle",
          "title": "Harness Program Sweep",
          "comment": "Sweeps the widget lane once per scan, then publishes the mirror. Invented test material.",
          "calls": [
            { "block": "FB_Comms_WidgetServer", "instance": "iDB_Comms_WidgetServer", "networkTitle": "Serve The Widget Mirror" },
            { "block": "FC_HarnessCopyLayer", "networkTitle": "Harness Mirror Copy Layer" }
          ]
        },
        "commsFb": {
          "blockName": "FB_Comms_WidgetServer",
          "blockNumber": 9411,
          "title": "Widget Mirror Server",
          "comment": "Serves {registers} registers of the widget mirror from %M{base}. Invented test material.",
          "localPort": 1502,
          "connectionId": "16#0021",
          "interfaceId": 64,
          "networkTitle": "Serve The Widget Mirror",
          "networkComment": "Runs the server every scan over {registers} registers. Invented test material."
        }
      }
    }
    """;

    private static LoopRequest Compose(string binding) =>
        LoopCli.Compose(SubmissionDocument.Read(Submission), BindingDocument.Read(binding), Array.Empty<HarnessObject>());

    private static LoopGeneration Generate(string binding) =>
        LoopRun.Generate(Compose(binding), stopWhenInadmissible: false);

    // ---------------------------------------------------------------------------------------------
    // THE WIRE — the seam that was missing
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>The document reaches the DOMAIN.</b> Before the fix both of these were <c>null</c> however the
    /// document was written, because the composition constructed declarations with the fields left off.
    /// </summary>
    [Fact]
    public void BOTH_DECLARATIONS_REACH_THE_DOMAIN_OFF_THE_WIRE()
    {
        var request = Compose(Binding);

        var comms = request.ProgramGeneration!.CommsFb;
        Assert.NotNull(comms);
        Assert.Equal("FB_Comms_WidgetServer", comms!.Naming.BlockName);
        Assert.Equal(9411, comms.Naming.BlockNumber);
        Assert.Equal(1502, comms.Endpoint!.LocalPort);
        Assert.Equal("16#0021", comms.Endpoint.ConnectionId);
        Assert.Equal(64, comms.Endpoint.InterfaceId);

        // 🔴 ABSENT, AND THAT IS THE DECISION RATHER THAN AN OMISSION — see GeneratedLayoutObligation.
        Assert.Null(comms.MemoryLayout);

        var udt = Assert.Single(request.LaneDeclarations!).StimUdt;
        Assert.NotNull(udt);
        Assert.Equal("UDT_WidgetStim", udt!.Naming.TypeName);
        Assert.Equal(2, udt.Members!.Count);
        Assert.Equal("Int", udt.Members.Single(m => m.Name == "Phase").Datatype);
    }

    /// <summary>
    /// 🔴 <b>The three endpoint numbers are named TOGETHER when any is missing</b> — all three identify one
    /// server instance on one CPU, none is defaulted, and an author who omitted the section omitted all
    /// three.
    /// </summary>
    [Fact]
    public void A_COMMS_FB_WITH_NO_ENDPOINT_IS_REFUSED_AT_PARSE_NAMING_ALL_THREE_FIELDS()
    {
        var mangled = Binding
            .Replace("\"localPort\": 1502,", string.Empty, StringComparison.Ordinal)
            .Replace("\"connectionId\": \"16#0021\",", string.Empty, StringComparison.Ordinal)
            .Replace("\"interfaceId\": 64,", string.Empty, StringComparison.Ordinal);

        var error = Assert.Throws<InvalidDataException>(() => Compose(mangled));

        Assert.Contains("localPort", error.Message, StringComparison.Ordinal);
        Assert.Contains("connectionId", error.Message, StringComparison.Ordinal);
        Assert.Contains("interfaceId", error.Message, StringComparison.Ordinal);
        Assert.Contains("hard rule 3", error.Message, StringComparison.Ordinal);
    }

    /// <summary>A UDT is the vocabulary a model is commanded in, so its comment is a description of the plant and is refused rather than invented.</summary>
    [Fact]
    public void A_STIMULUS_UDT_WITH_NO_COMMENT_IS_REFUSED_AT_PARSE()
    {
        var mangled = Binding.Replace(
            "\"comment\": \"The vocabulary the widget plant model is commanded in. Invented test material.\",",
            string.Empty, StringComparison.Ordinal);

        var error = Assert.Throws<InvalidDataException>(() => Compose(mangled));
        Assert.Contains("UDT_WidgetStim", error.Message, StringComparison.Ordinal);
        Assert.Contains("COMMANDED IN", error.Message, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // THE RUN — the artifacts a declaring binding now actually gets
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>END TO END: a binding declaring both gets BOTH, and the server serves the window THIS RUN
    /// allocated rather than one anybody typed.</b>
    /// </summary>
    [Fact]
    public void A_BINDING_THAT_DECLARES_BOTH_ACTUALLY_PRODUCES_BOTH()
    {
        var generation = Generate(Binding);

        Assert.True(generation.Generated, generation.Detail);

        // --- the comms FB ---
        var comms = generation.Program!.CommsFb;
        Assert.NotNull(comms);
        Assert.Equal("FB_Comms_WidgetServer", comms!.BlockName);

        // The area pointer is the MAP's geometry, not a number in the document: `commsFb` carries no window.
        Assert.Equal($"P#M{generation.Map!.Geometry.BaseByte}.0 WORD {generation.Map.Geometry.DeclaredRegisters}", comms.AreaPointer);
        Assert.Equal("P#M1000.0 WORD 576", comms.AreaPointer);

        // And the prose placeholders were filled from that same geometry, so the sentence cannot fall
        // behind the pointer beside it.
        Assert.Contains("Serves 576 registers of the widget mirror from %M1000.", comms.Ir, StringComparison.Ordinal);
        Assert.Contains("MB_SERVER", comms.Ir, StringComparison.Ordinal);

        Assert.Contains(generation.Program.Objects,
            o => o.Name == "FB_Comms_WidgetServer" && o.Kind == HarnessObjectKind.Block);

        // --- the stimulus UDT ---
        var slot = Assert.Single(generation.Lane!.Slots);
        Assert.NotNull(slot.StimUdt);
        Assert.Equal("UDT_WidgetStim", slot.StimUdt!.TypeName);
        Assert.StartsWith("TYPE UDT_WidgetStim\n", slot.StimUdt.Ir, StringComparison.Ordinal);

        // 🔴 A TYPE, NOT A DATA BLOCK — HarnessObjectKind.DataType exists so a UDT is never handed a data
        // block's retention rules on its way into the stamp.
        Assert.Contains(generation.Lane.Objects,
            o => o.Name == "UDT_WidgetStim" && o.Kind == HarnessObjectKind.DataType);

        // The half the RUNGS typed is what this generator is worth; the declared half is the part nothing
        // checked, and the two are counted apart rather than totalled.
        Assert.True(slot.StimUdt.DerivedCount > 0,
            "no member's type was fixed by the emitted rungs, so nothing was derived and the type is a transcription.");
        Assert.Equal(2, slot.StimUdt.DeclaredCount);
    }

    /// <summary>
    /// 🔴 <b>AND BOTH ARE IN THE BUILD STAMP</b> — the server EXECUTES and the UDT is LOADED, so a changed
    /// declaration must move the number the device is confirmed against. Neither embeds the stamp, so
    /// hashing them is not circular the way the copy layer is.
    /// </summary>
    [Fact]
    public void BOTH_GENERATED_ARTIFACTS_MOVE_THE_BUILD_STAMP()
    {
        var declared = Generate(Binding);

        var withoutComms = LoopRun.Generate(
            Compose(Binding) with
            {
                ProgramGeneration = Compose(Binding).ProgramGeneration! with { CommsFb = null },
            },
            stopWhenInadmissible: false);

        Assert.True(withoutComms.Generated, withoutComms.Detail);
        Assert.NotEqual(declared.Stamp.Value, withoutComms.Stamp.Value);

        var withoutUdt = LoopRun.Generate(
            Compose(Binding) with
            {
                LaneDeclarations = Compose(Binding).LaneDeclarations!.Select(d => d with { StimUdt = null }).ToArray(),
            },
            stopWhenInadmissible: false);

        Assert.True(withoutUdt.Generated, withoutUdt.Detail);
        Assert.NotEqual(declared.Stamp.Value, withoutUdt.Stamp.Value);
    }

    /// <summary>
    /// A binding that declares NEITHER is reported as AUTHORED, in words, on a positive line — which is the
    /// reading that must survive, because it is the honest one for every lane written before these keys.
    /// </summary>
    [Fact]
    public void A_BINDING_THAT_DECLARES_NEITHER_IS_STILL_REPORTED_AS_AUTHORED()
    {
        var stripped = Binding
            .Replace("\"commsFb\": {", "\"_commsFb\": {", StringComparison.Ordinal)
            .Replace("\"stimUdt\": {", "\"_stimUdt\": {", StringComparison.Ordinal)
            // The OB may not call a server that is no longer generated by this pass.
            .Replace("{ \"block\": \"FB_Comms_WidgetServer\", \"instance\": \"iDB_Comms_WidgetServer\", \"networkTitle\": \"Serve The Widget Mirror\" },",
                     string.Empty, StringComparison.Ordinal);

        var generation = Generate(stripped);

        Assert.True(generation.Generated, generation.Detail);
        Assert.Null(generation.Program!.CommsFb);
        Assert.Contains(generation.Program.NotDeclared, n => n.Contains("no comms FB was declared", StringComparison.Ordinal));
        Assert.Contains(generation.Lane!.NotDeclared, n => n.Contains("no stimulus UDT was declared", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------------------------------------
    // THE QUIET FAILURE, MADE LOUD
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>A KEY INSIDE A GENERATION DECLARATION THAT REACHES NO GENERATOR NOW REFUSES, INSTEAD OF BEING
    /// REPORTED AS AUTHORED.</b>
    ///
    /// <para>Gate 0b already names such a key — checked, it does, at every level — and on a DEPLOYING run it
    /// stops before generation. But <c>--generate-only</c> runs the gate with <c>stopWhenInadmissible:
    /// false</c> deliberately, and then prints the AUTHORED line anyway: two statements about one document,
    /// with the reassuring one false. This refusal happens at composition, so no path can reach that
    /// sentence.</para>
    /// </summary>
    [Theory]
    // The exact shape of this defect: a `commsFb` key against a build whose schema cannot read it.
    [InlineData("\"commsFb\": {", "\"commFb\": {", "binding.generateProgram.commFb")]
    // The per-slot half.
    [InlineData("\"stimUdt\": {", "\"stimUdtt\": {", "binding.slots[0].generate.stimUdtt")]
    // And one level deeper, where the key is inside an object the schema DOES read.
    [InlineData("\"localPort\": 1502,", "\"localPortt\": 1502, \"localPort\": 1502,", "binding.generateProgram.commsFb.localPortt")]
    public void A_STRAY_KEY_INSIDE_A_GENERATION_DECLARATION_REFUSES_RATHER_THAN_REPORTING_AUTHORED(
        string find, string replace, string expectedPath)
    {
        var mangled = Binding.Replace(find, replace, StringComparison.Ordinal);
        Assert.NotEqual(Binding, mangled);

        var error = Assert.Throws<InvalidDataException>(() => Compose(mangled));

        Assert.Contains(expectedPath, error.Message, StringComparison.Ordinal);
        Assert.Contains("read by no schema", error.Message, StringComparison.Ordinal);
        Assert.Contains("AUTHORED", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 <b>THE ANNOTATION ESCAPE SURVIVES.</b> A leading <c>_</c> is the document-wide convention for a
    /// deliberate comment; refusing one here would make this the only level of the binding where a note is
    /// illegal — and the test above uses exactly that escape to disable a section.
    /// </summary>
    [Fact]
    public void AN_UNDERSCORE_ANNOTATION_INSIDE_A_GENERATION_DECLARATION_IS_NOT_A_STRAY_KEY()
    {
        var annotated = Binding.Replace(
            "\"blockNumber\": 9411,",
            "\"blockNumber\": 9411, \"_why\": \"claimed 2026-08-27 from the 9000-9999 harness band.\",",
            StringComparison.Ordinal);

        var request = Compose(annotated);
        Assert.NotNull(request.ProgramGeneration!.CommsFb);
    }

    /// <summary>
    /// A stray key OUTSIDE a generation declaration is left to gate 0b, which names it. This refusal is
    /// narrow on purpose: elsewhere a dropped key costs a VALUE, which other checks can still catch; only
    /// here does it cost a whole ARTIFACT and report the loss as a deliberate authoring choice.
    /// </summary>
    [Fact]
    public void A_STRAY_KEY_OUTSIDE_A_GENERATION_DECLARATION_IS_LEFT_TO_GATE_0b()
    {
        var mangled = Binding.Replace("\"declaredBy\":", "\"declaredBuy\":", StringComparison.Ordinal);

        var request = Compose(mangled);
        Assert.Contains("binding.declaredBuy", request.UnknownFields!);
    }

    // ---------------------------------------------------------------------------------------------
    // C-201 ON THE GENERATED OB — the second defect, at the wire
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>The OB's header comment reaches the emitted block, and a binding that omits it is REFUSED.</b>
    ///
    /// <para><c>converter preflight</c> refuses a generated OB with
    /// <c>[review:C-201] [Error] … has no header comment</c>, and the <c>review:harness-scope</c> exemption
    /// that covers the slot FC does not cover an OB — its number is fixed by its event class, so it is
    /// excluded from the 9000–9999 band. Before this key existed, <b>no binding could produce a compliant
    /// OB at all</b>.</para>
    /// </summary>
    [Fact]
    public void THE_CYCLIC_OBS_HEADER_COMMENT_REACHES_THE_BLOCK_AND_ITS_ABSENCE_REFUSES()
    {
        var ir = Generate(Binding).Program!.CyclicOb!.Ir;

        Assert.Contains(
            "COMMENT \"Sweeps the widget lane once per scan, then publishes the mirror. Invented test material.\"\n",
            ir, StringComparison.Ordinal);

        var mangled = Binding.Replace(
            "\"comment\": \"Sweeps the widget lane once per scan, then publishes the mirror. Invented test material.\",",
            string.Empty, StringComparison.Ordinal);

        var error = Assert.Throws<InvalidDataException>(() => Compose(mangled));
        Assert.Contains("C-201", error.Message, StringComparison.Ordinal);
        Assert.Contains("harness-scope", error.Message, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // THE LAYOUT DECISION, CARRIED RATHER THAN REMEMBERED
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>NO GENERATED OBJECT DECLARES A <c>MEMORYLAYOUT</c>, AND THE RUN SAYS SO ALOUD.</b>
    ///
    /// <para>See <see cref="GeneratedLayoutObligation"/> for the decision. The consequence — <c>converter
    /// compare</c> exit 2 against a TIA re-export — is not a fault in the block, and
    /// <c>--allow-silent-layout</c> is the converter's own documented escape for that input pair. What was
    /// wrong is that a caller had to REMEMBER it; now it travels in the run's obligation list.</para>
    /// </summary>
    [Fact]
    public void THE_GENERATED_OBJECTS_DECLARE_NO_LAYOUT_AND_THE_RUN_SAYS_WHAT_THAT_COSTS_THE_ROUND_TRIP()
    {
        var generation = Generate(Binding);

        foreach (var obj in generation.Program!.Objects.Concat(generation.Lane!.Objects))
            Assert.DoesNotContain("MEMORYLAYOUT", obj.Ir, StringComparison.Ordinal);

        Assert.Contains(generation.Program.Obligations, o => o.Contains("--allow-silent-layout", StringComparison.Ordinal));
        Assert.Contains(generation.Lane.Obligations, o => o.Contains("--allow-silent-layout", StringComparison.Ordinal));

        // Exit 2 is never a pass, and the obligation has to say that rather than only naming the flag.
        Assert.Contains(generation.Program.Obligations, o => o.Contains("exit 2", StringComparison.Ordinal));
    }
}
