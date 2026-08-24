using Harness.Gate;
using Harness.Run;

namespace Harness.Loop.Tests;

/// <summary>
/// 🔴 <b>THE SENTENCE REACHES <c>harness-run</c>, WHICH IS THE HOP THE PACKAGE'S CAVEAT DEPENDS ON
/// (workbench Y1).</b>
///
/// <para><c>harness-batch</c> derives who else occupies the mirror's <c>%M</c> area; when nothing
/// derived it, the batch puts the NOT DERIVED sentence on each lane's wave command so it lands in the
/// result package. <c>BatchRunPlan</c> is tested for emitting the flag and
/// <c>ResultPackageBuilder</c> for turning it into a caveat — <b>this is the piece between them</b>,
/// and without it both halves could pass while the flag was silently swallowed by an argument parser
/// that ignores what it does not recognise.</para>
///
/// <para><b>Absent means DERIVED, never "no neighbours".</b> A run that prints nothing here is one whose
/// neighbour list was obtained, and the negative control below pins that the line is not unconditional.</para>
/// </summary>
public class NeighboursNotDerivedWireTests
{
    /// <summary>
    /// Deliberately EMPTY OF VECTORS so the gate refuses and <c>--generate-only</c> stops early. The
    /// neighbour sentence is echoed from the composed request, above the gate, so this fixture keeps the
    /// verdict constant while the line under test varies.
    /// </summary>
    private const string Submission = """
    {
      "blockAuthor": "agent-a",
      "runtimeCompression": 1,
      "slotsInWaveSet": 1,
      "maxIndexScans": 200,
      "resultRegistersPerSlot": 1,
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
      "slots": [
        { "slotId": "S000",
          "vectorTargets": [{ "tag": "Stim_Total", "type": "Int" }],
          "startCondition": "Stim_Start",
          "resultSources": [{ "tag": "Alarm_000", "type": "Bool",
                              "inertRest": { "value": "false", "basis": "the alarm coil is ANDed with the start condition, so it is off at inert" } }] }
      ]
    }
    """;

    private static string Run(params string[] extraArgs)
    {
        var writer = new StringWriter();

        var args = new[] { "--submission", "sub.json", "--binding", "binding.json", "--no-program-under-test", "--generate-only" }
            .Concat(extraArgs)
            .ToArray();

        LoopCli.Run(
            args,
            writer,
            path => path switch
            {
                "sub.json" => Submission,
                "binding.json" => Binding,
                _ => throw new FileNotFoundException(path),
            },
            (_, _) => { },
            env: _ => null,
            expandProgramPath: path => new[] { path });

        return writer.ToString();
    }

    [Fact]
    public void The_flag_is_PARSED_and_the_run_says_the_neighbour_list_was_not_derived()
    {
        var output = Run("--neighbours-not-derived",
            "NEIGHBOURS: NOT DERIVED — the escape `--neighbours declared-only` was named.");

        Assert.Contains("NEIGHBOURS: NOT DERIVED", output, StringComparison.Ordinal);
        Assert.Contains("declared-only", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// The negative control. A line printed unconditionally would say nothing, and a reader would learn
    /// to ignore it — which is the failure mode of every caveat that fires on every run.
    /// </summary>
    [Fact]
    public void Without_the_flag_the_run_says_nothing_about_neighbours()
    {
        Assert.DoesNotContain("NEIGHBOURS: NOT DERIVED", Run(), StringComparison.Ordinal);
    }

    /// <summary>
    /// And it reaches the REQUEST, which is what carries it into every package a verifying run writes.
    /// Asserted on the composed object rather than only on the console, because the console line is a
    /// convenience and the request field is the mechanism.
    /// </summary>
    [Fact]
    public void And_it_reaches_the_composed_request_which_is_what_the_package_reads()
    {
        var request = LoopCli.Compose(
            SubmissionDocument.Read(Submission),
            BindingDocument.Read(Binding),
            Array.Empty<Harness.Map.HarnessObject>(),
            neighboursNotDerived: "NEIGHBOURS: NOT DERIVED — nobody derived one.");

        Assert.Equal("NEIGHBOURS: NOT DERIVED — nobody derived one.", request.NeighboursNotDerived);
        Assert.Null(LoopCli.Compose(
            SubmissionDocument.Read(Submission),
            BindingDocument.Read(Binding),
            Array.Empty<Harness.Map.HarnessObject>()).NeighboursNotDerived);
    }
}
