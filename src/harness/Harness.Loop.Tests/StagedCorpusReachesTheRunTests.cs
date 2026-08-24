using System.Text.Json;
using Harness.Loop;
using Harness.Map;
using Harness.Run;
using Harness.Wire;

namespace Harness.Loop.Tests;

/// <summary>
/// 🔴 <b>THE COVERAGE LINE ON A REAL RUN — <i>n</i> of <i>m</i>, not <c>NO STAGED CORPUS WAS SUPPLIED</c>.</b>
///
/// <para>Phase 6 Y3 gave <c>BuildStamp.Derive</c> a denominator and left it unfed on purpose: nothing
/// supplied a <see cref="StagedCorpus"/>, so every real run printed the no-corpus sentence. That was the
/// honest state and not a closed item — <b>a stamp with the parameter DB missing reads exactly like a
/// complete one</b> (docs/18-project-workbench.md §5 "Phase 10 — Wave time", under <i>"THE BUILD STAMP
/// DOES COVER THE PARAMETER DB"</i>; ⚠️ its original eight-object figure was retracted 2026-08-24 — the
/// deployed set was nine, stamp <c>622F3EB7</c>), which is how a changed controller kept an unchanged
/// stamp.</para>
///
/// <para><b>Three things are asserted together, and the third is the one that could quietly rot.</b> The
/// corpus reaches the derivation; the no-corpus sentence is STILL reachable from a hand-driven run; and
/// <b>the stamp does not move</b>. Wiring the corpus into the canonical form would have moved every stamp
/// already computed — including the literal compiled into the copy layer currently deployed — for no fact
/// on the controller.</para>
/// </summary>
public class StagedCorpusReachesTheRunTests
{
    // -------------------------------------------------------------------------------------------------
    // A. Through harness-run's own command line — the surface the batch actually drives.
    // -------------------------------------------------------------------------------------------------

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
      "blockNumber": 9001,
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

    private const string BlockIr = "BLOCK FC FC_DemoRamp\n  NUMBER 901\n  NETWORK 1 \"ramp\"\n";

    private static (int Exit, string Output) Cli(params string[] args)
    {
        var writer = new StringWriter();

        var exit = LoopCli.Run(args, writer,
            path => path switch
            {
                "sub.json" => Submission,
                "binding.json" => Binding,
                "ramp.ir" => BlockIr,
                _ => throw new FileNotFoundException(path),
            },
            (_, _) => { },
            null,
            _ => null,
            path => new[] { path });

        return (exit, writer.ToString());
    }

    private static string[] GenerateOnly(params string[] extra) =>
        new[] { "--submission", "sub.json", "--binding", "binding.json", "--generate-only", "--program", "ramp.ir" }
            .Concat(extra).ToArray();

    /// <summary>The one line a reader sees, for this fixture, spelled out whole.</summary>
    private const string RealCoverageLine =
        "stamp over 1 of 2 object(s) in the staged corpus; "
        + "0 excluded as self-referential (none); "
        + "1 present and not hashed (DB_Params [lane 'valve']); "
        + "0 hashed that no corpus entry names (none)";

    /// <summary>
    /// 🔴 <b>THE ACCEPTANCE TEST. A run handed a corpus reports a REAL denominator and NAMES the gap.</b>
    ///
    /// <para>Asserted WHOLE rather than by substring: the wording is the deliverable, and a substring check
    /// cannot catch a clause quietly going missing — least of all the device residual, whose absence would
    /// turn this into a claim about the whole program.</para>
    /// </summary>
    [Fact]
    public void A_RUN_GIVEN_A_CORPUS_PRINTS_A_REAL_N_OF_M()
    {
        var (_, output) = Cli(GenerateOnly(
            "--staged", "FC_DemoRamp=lane 'valve'", "--staged", "DB_Params=lane 'valve'"));

        Assert.Contains(RealCoverageLine + " — " + StampCoverage.DeviceResidual, output, StringComparison.Ordinal);
        Assert.DoesNotContain("NO STAGED CORPUS WAS SUPPLIED", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 <b>AND THE NO-CORPUS SENTENCE IS STILL REACHABLE.</b> A hand-driven run with a bare
    /// <c>--program</c> list has no manifest behind it, and it must go on saying so out loud. Making this
    /// unreachable while wiring the happy path would convert a loud absence into a silent assumption —
    /// which is the failure the whole item is about.
    /// </summary>
    [Fact]
    public void A_RUN_WITH_NO_CORPUS_STILL_SAYS_THERE_IS_NO_DENOMINATOR()
    {
        var (_, output) = Cli(GenerateOnly());

        Assert.Contains("NO STAGED CORPUS WAS SUPPLIED", output, StringComparison.Ordinal);
        Assert.Contains("an empty gap list here is not a clean sheet", output, StringComparison.Ordinal);
        Assert.DoesNotContain("in the staged corpus", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 <b>SUPPLYING A CORPUS DOES NOT MOVE THE STAMP, THROUGH THE WHOLE CLI.</b>
    ///
    /// <para><c>StampCoverageTests</c> asserts this of the derivation. This asserts it of the BINARY, which
    /// is where a wiring change could reach the canonical form by mistake — and the consequence would be
    /// every deployed copy layer's literal going stale for no fact on the controller.</para>
    /// </summary>
    [Fact]
    public void SUPPLYING_A_CORPUS_ON_THE_COMMAND_LINE_DOES_NOT_MOVE_THE_STAMP()
    {
        var bare = Stamp(Cli(GenerateOnly()).Output);
        var withCorpus = Stamp(Cli(GenerateOnly("--staged", "FC_DemoRamp=lane 'valve'", "--staged", "DB_Params=lane 'valve'")).Output);
        var other = Stamp(Cli(GenerateOnly("--staged", "Something_Else=lane 'other'")).Output);

        Assert.False(string.IsNullOrWhiteSpace(bare));
        Assert.Equal(bare, withCorpus);
        Assert.Equal(bare, other);
    }

    private static string Stamp(string output) =>
        output.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.StartsWith("BUILD STAMP", StringComparison.Ordinal))
            .Select(l => l[(l.IndexOf(':', StringComparison.Ordinal) + 1)..].Trim())
            .FirstOrDefault() ?? string.Empty;

    /// <summary>
    /// 🔴 <b>THE PHANTOM GAP THAT WOULD HAVE APPEARED ON EVERY REAL RUN.</b>
    ///
    /// <para>Every lane manifest lists the copy layer it generated, while a <c>--program</c> list built
    /// from a pre-generation tree legitimately does not. Classifying by NAME rather than by "was it
    /// supplied" is what stops that reading as a coverage hole for ever — and a check carrying a permanent
    /// phantom gap is a check people learn to skim.</para>
    /// </summary>
    [Fact]
    public void THE_COPY_LAYER_IN_THE_CORPUS_IS_SELF_REFERENTIAL_even_though_program_never_supplied_it()
    {
        var (_, output) = Cli(GenerateOnly(
            "--staged", "FC_DemoRamp=lane 'valve'",
            "--staged", "FC_HarnessCopyLayer=lane 'valve'",
            "--staged", "HarnessMirror=lane 'valve'"));

        Assert.Contains(
            "stamp over 1 of 3 object(s) in the staged corpus; "
            + "2 excluded as self-referential (FC_HarnessCopyLayer [lane 'valve'], HarnessMirror [lane 'valve']); "
            + "0 present and not hashed (none); "
            + "0 hashed that no corpus entry names (none)",
            output, StringComparison.Ordinal);
    }

    /// <summary>
    /// A row this tool cannot read is NAMED, never dropped. A silently-skipped row shrinks the
    /// denominator, and a shrinking denominator is how a short stamp reads complete.
    /// </summary>
    [Fact]
    public void A_MALFORMED_STAGED_ROW_IS_REFUSED_BY_NAME_and_never_skipped()
    {
        var (exit, output) = Cli(GenerateOnly("--staged", "FC_DemoRamp"));

        Assert.Equal(LoopExit.NothingExamined, exit);
        Assert.Contains("FC_DemoRamp", output, StringComparison.Ordinal);
        Assert.Contains("--staged", output, StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------------------------------------
    // B. Through the loop and into the artifact a consumer actually reads.
    // -------------------------------------------------------------------------------------------------

    private static StagedCorpus Corpus() => StagedCorpus.Of(new[]
    {
        new StagedCorpusEntry("FC_DemoRamp", "lane 'ramp'"),
        new StagedCorpusEntry("DemoUnit", "lane 'ramp'"),
        new StagedCorpusEntry("DB_Params", "lane 'ramp'"),
    });

    private static LoopResult Ran() =>
        LoopRun.Execute(
            LoopRunTests.Request() with { StagedCorpus = Corpus() },
            new SimulatedGateway(LoopRunTests.Geometry()));

    /// <summary>
    /// 🔴 <b>EVERY RESULT PACKAGE CARRIES THE DENOMINATOR — the package is the artifact meant to outlive
    /// the run.</b> The manifest reached <c>LoopResult</c> at Y3 and stopped one argument short of the
    /// packages, so a consumer reading a package alone could not tell a complete stamp from a short one.
    /// </summary>
    [Fact]
    public void EVERY_PACKAGE_CARRIES_THE_STAMPS_COVERAGE()
    {
        var result = Ran();

        Assert.NotEmpty(result.Packages);

        foreach (var package in result.Packages)
        {
            var coverage = package.Program?.Coverage;

            Assert.NotNull(coverage);
            Assert.Equal(3, coverage!.CorpusSize);
            Assert.Equal(2, coverage.Hashed);
            Assert.Equal(new[] { "DB_Params [lane 'ramp']" }, coverage.PresentAndNotHashed);
            Assert.False(coverage.Complete);
        }
    }

    /// <summary>
    /// 🔴 <b>A STAGED OBJECT OUTSIDE THE STAMP IS A CAVEAT ON THE RESULT</b>, because it can change the
    /// controller without moving the value the validity stamp rests on. Quoted whole, so the caveat carries
    /// the device residual with it.
    /// </summary>
    [Fact]
    public void THE_GAP_BECOMES_A_CAVEAT_ON_THE_VALIDITY_STAMP()
    {
        var caveats = Ran().Packages[0].Stamp.Caveats;

        Assert.Contains(caveats, c => c.Contains("THE BUILD STAMP DOES NOT COVER EVERY STAGED OBJECT", StringComparison.Ordinal));
        Assert.Contains(caveats, c => c.Contains(StampCoverage.DeviceResidual, StringComparison.Ordinal));
    }

    /// <summary>
    /// The run's own JSON artifact carries the coverage too. A consumer reads THIS, never the console —
    /// and <c>programUnderTest</c> rendered the objects and no denominator.
    /// </summary>
    [Fact]
    public void THE_RUN_ARTIFACT_RENDERS_THE_COVERAGE_under_programUnderTest()
    {
        var artifact = JsonDocument.Parse(LoopCli.Render(Ran())).RootElement;
        var coverage = artifact.GetProperty("programUnderTest").GetProperty("coverage");

        Assert.Equal(3, coverage.GetProperty("stagedCorpusSize").GetInt32());
        Assert.Equal(2, coverage.GetProperty("hashed").GetInt32());
        Assert.True(coverage.GetProperty("stagedCorpusStated").GetBoolean());
        Assert.False(coverage.GetProperty("complete").GetBoolean());
        Assert.Equal(StampCoverage.DeviceResidual, coverage.GetProperty("residual").GetString());
    }

    /// <summary>
    /// <b>Null, not an absent key and not a zero.</b> A run with no corpus renders a coverage whose
    /// denominator is stated as unknown — because an absent key reads as "fine" to everyone who did not
    /// write it.
    /// </summary>
    [Fact]
    public void THE_ARTIFACT_SAYS_NO_DENOMINATOR_when_no_corpus_was_supplied()
    {
        var result = LoopRun.Execute(LoopRunTests.Request(), new SimulatedGateway(LoopRunTests.Geometry()));
        var coverage = JsonDocument.Parse(LoopCli.Render(result)).RootElement
            .GetProperty("programUnderTest").GetProperty("coverage");

        Assert.Equal(JsonValueKind.Null, coverage.GetProperty("stagedCorpusSize").ValueKind);
        Assert.False(coverage.GetProperty("stagedCorpusStated").GetBoolean());
        Assert.Contains("NO STAGED CORPUS WAS SUPPLIED", coverage.GetProperty("line").GetString()!, StringComparison.Ordinal);
    }

    /// <summary>The console report prints the line too, on every run, including the complete one.</summary>
    [Fact]
    public void THE_CONSOLE_REPORT_PRINTS_THE_COVERAGE_LINE()
    {
        var writer = new StringWriter();
        LoopCli.Write(Ran(), writer);

        Assert.Contains("stamp over 2 of 3 object(s) in the staged corpus", writer.ToString(), StringComparison.Ordinal);
    }
}
