using System.Text.Json;
using Harness.Run;

namespace Harness.Loop.Tests;

/// <summary>
/// 🔴 <b>A RUN THAT PRODUCED A RESULT MUST BE ABLE TO WRITE IT — and for one shape of run it could not.</b>
///
/// <para><b>MEASURED 2026-09-02, on a real wave against the bench rig.</b> The loop connected, the
/// verifying gateway matched the build stamp on the device, the copy layer deployed and all seven indexes
/// executed. Then <c>Render</c> threw:</para>
///
/// <code>
/// System.InvalidOperationException: JsonSerializerOptions instance must specify a TypeInfoResolver
///   at System.Text.Json.Nodes.JsonValueCustomized`1.WriteTo(...)
///   at Harness.Run.LoopCli.Render(LoopResult result)
/// </code>
///
/// <para><b>THE CAUSE IS THE ONE <c>ResultPackageJson.Strings</c> ALREADY DOCUMENTS.</b>
/// <c>JsonArray.Add(string)</c> binds to the generic overload and boxes a
/// <c>JsonValueCustomized&lt;string&gt;</c>, which has no <c>TypeInfoResolver</c> to write itself with.
/// That renderer was converted to <c>JsonValue.Create</c>; the <c>excludedAsSelfReferential</c> array in
/// <c>LoopCli</c> was not, and now is.</para>
///
/// <para><b>THE CONSEQUENCE IS WORSE THAN A LOST FILE.</b> <c>Render</c> produces the text written to
/// <c>--out</c>, so the throw means the file is never created and <b>the path keeps whatever was there
/// before</b> — on the run that found this, an earlier <c>NotAdmissible</c> result. The only
/// machine-readable account of a wave that ran was a document describing a wave that did not, and this
/// project's evidence files cite result JSON by path.</para>
///
/// <para>
/// 🔴 <b>WHAT THESE TESTS DO NOT COVER, STATED BECAUSE THE GAP IS THE INTERESTING PART.</b>
/// <b>They do not reach the line that threw.</b> It sits inside <c>if (result.ProgramManifest is { }
/// manifest)</c>, and the array is empty unless an object was excluded as self-referential — which needs
/// the caller to hand the copy layer or the mirror to <c>--program</c>. But a run WITH a program under
/// test never reaches <c>Render</c> without <c>--verify</c> and a host: it is refused at the device fence
/// first. And <c>--verify</c> needs a <c>connect</c> stub no fixture in this project provides. The two
/// conditions — a populated manifest, and reaching <c>Render</c> — cannot currently be met together off a
/// device.
/// </para>
///
/// <para>What is asserted below is therefore the weaker, TRUE claim: <b>a run that reaches <c>Render</c>
/// writes a file, and that file parses.</b> It guards the serialise path in general and would catch a
/// regression on any array a no-program run does build. <b>It would NOT have caught the defect it was
/// written for.</b> Closing that needs a fake gateway answering a build stamp — recorded as the follow-up
/// in <c>docs/16-future-ideas.md</c> FI-86 rather than left implied by a test name.</para>
/// </summary>
public class RenderWritesTheResultOfARunThatRanTests
{
    private const string Submission = """
    {
      "blockAuthor": "agent-a",
      "runtimeCompression": 1,
      "slotsInWaveSet": 1,
      "maxIndexScans": 200,
      "resultRegistersPerSlot": 4,
      "computedConflicts": [],
      "model": { "id": "M", "represents": ["ramp"], "validatedAgainstPlantData": true },
      "enumeration": { "clauses": ["REQ-1"], "assertions": ["REQ-1:aaaaaa"] },
      "map": { "providedFor": { "Demo_Count": ["Sampled"] } },
      "vectors": []
    }
    """;

    private const string Binding = """
    {
      "declaredBy": "agent-k",
      "blockName": "FC_HarnessCopyLayer",
      "tagTableName": "HarnessMirror",
      "blockNumber": 9000,
      "baseByte": 1000,
      "declaredRegisters": 576,
      "slots": [{
        "slotId": "HBA",
        "vectorTargets": [{ "tag": "Stim_Total", "type": "Time" }],
        "startCondition": "Stim_Start",
        "resultSources": [{ "tag": "Alarm", "type": "Bool",
                           "inertRest": { "value": "false", "basis": "the alarm coil is ANDed with the start condition, so it is off at inert" } }]
      }]
    }
    """;

    private static Dictionary<string, string> ARunThatReachesRender()
    {
        var written = new Dictionary<string, string>(StringComparer.Ordinal);

        LoopCli.Run(
            new[]
            {
                "--submission", "sub.json", "--binding", "binding.json",
                "--no-program-under-test", "--out", "out.json",
            },
            new StringWriter(),
            path => path switch
            {
                "sub.json" => Submission,
                "binding.json" => Binding,
                _ => throw new FileNotFoundException(path),
            },
            (path, text) => written[path] = text,
            connect: null,
            env: _ => null,
            expandProgramPath: path => new[] { path });

        return written;
    }

    /// <summary>A run that got as far as producing a result leaves one behind. Red if <c>Render</c> throws
    /// on anything in the document it builds.</summary>
    [Fact]
    public void ARunThatReachesRender_WritesItsResult()
    {
        Assert.True(ARunThatReachesRender().ContainsKey("out.json"),
            "Render threw, so --out was never written and the path keeps whatever was there before.");
    }

    /// <summary>And what it wrote parses. The failure was on SERIALISE, so "a file exists" is not the claim
    /// that matters — a renderer can build a correct object graph that no serialiser can write.</summary>
    [Fact]
    public void TheWrittenResultParses()
    {
        var parsed = JsonDocument.Parse(ARunThatReachesRender()["out.json"]);

        Assert.Equal(JsonValueKind.Object, parsed.RootElement.ValueKind);
    }

    /// <summary>
    /// 🔴 THE HONEST BOUNDARY, ASSERTED SO IT CANNOT ROT INTO A FALSE CLAIM OF COVERAGE. This fixture
    /// declares no program under test, so the document carries no <c>programUnderTest</c> object and the
    /// <c>excludedAsSelfReferential</c> array that actually threw is never built. If a later change makes
    /// a no-program run emit that array, this goes red — and whoever sees it should WIDEN the fixture to
    /// assert the array serialises, not delete the assertion.
    /// </summary>
    [Fact]
    public void TheseTestsDoNotReachTheArrayThatThrew_AndSayThatOutLoud()
    {
        var root = JsonDocument.Parse(ARunThatReachesRender()["out.json"]).RootElement;

        var reachesIt =
            root.TryGetProperty("programUnderTest", out var manifest)
            && manifest.ValueKind == JsonValueKind.Object
            && manifest.TryGetProperty("excludedAsSelfReferential", out _);

        Assert.False(reachesIt,
            "This fixture now DOES build the excluded array - widen it to assert that array serialises, "
            + "because the coverage gap these tests document has just closed.");
    }
}
