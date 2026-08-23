using System.Text.Json;
using Harness.Map;
using Harness.Results;
using Harness.Wire;

namespace Harness.Results.Tests;

/// <summary>
/// 🔴 <b>A RESULT OBTAINED WITH THE NEIGHBOUR LIST UNDERIVED SAYS SO, IN THE ARTIFACT THAT OUTLIVES THE
/// RUN (workbench Y1).</b>
///
/// <para><b>Why the plan's line is not enough.</b> The plan is printed to a terminal and gone with it.
/// The package is what a reviewer opens weeks later, and it is where the claim <i>"nothing else in the
/// area was written to"</i> would otherwise be made silently. A run whose mirror was allocated against
/// a neighbour list nobody derived rests on an unmeasured premise — which is
/// <see cref="ValidityStamp.Caveats"/>'s own definition of what belongs in it.</para>
///
/// <para><b>It fires only on NOT DERIVED, deliberately.</b> The builder's own rule for the program
/// manifest applies here word for word: <i>"a caveat that fires on all of them is a caveat nobody
/// reads."</i> A derived batch adds nothing.</para>
/// </summary>
public class NeighboursNotDerivedCaveatTests
{
    private static readonly CopyLayerNaming Naming =
        new(BlockName: "FC_TheCopyLayer", BlockNumber: 900, TagTableName: "TheMirrorTable");

    private static readonly BuildStamp Build = new(0xA93F2C71);

    private static readonly AssertionEnumeration Enumeration =
        AssertionEnumeration.Of(new[] { "REQ-14" }, new[] { "REQ-14.a", "REQ-14.b" });

    private static readonly ObservabilityReport Supportable = ObservabilityCheck.Evaluate(
        new[] { new ObservabilityDeclaration("Demo_Count", SignalNature.PersistentState, InstrumentationMode.Latched, 0) },
        AssertionForm.When,
        MirrorObservability.Of(("Demo_Count", new[] { InstrumentationMode.Latched })), 9, 1, 1);

    private static RegisterMap Map() =>
        MapAllocator.Allocate(new WaveSetRequest(
            MirrorGeometry.ForCpu1214C(256, 4000, declaredRegisters: (MirrorGeometry.Cpu1214CBitMemoryBytes - 4000) / 2),
            new[] { new SlotRequest("S0", 2, 2) })).Require();

    private static SlotRunResult Run() =>
        new(SlotOutcome.Completed, new ushort[] { 10, 1 }, new ScanCount(100), new ScanCount(140), 2, 8,
            new InertReport(InertOutcome.Established, new ScanCount(90), Array.Empty<ushort>(), Array.Empty<ushort>(), "established"),
            "stub",
            ObservationSeries.OfSingleFrame(new ushort[] { 10, 1 }, new ScanCount(140)));

    private static ResultPackage Package(string? neighboursNotDerived) =>
        ResultPackageBuilder.Build(
            new VectorDeclaration("V-1",
                new Basis("REQ-14", "REQ-14.a"),
                FidelityDeclaration.Of("M_Weigher", new[] { "fill-to-setpoint" }, new[] { "in-flight-mass" },
                    validatedAgainstPlantData: true, declaredBy: "agent-m"),
                new SettlingDeclaration("count unchanged across 3 consecutive scans", new[] { "Demo_Count" }),
                new[] { "fill-to-setpoint" },
                "Demo_Done",
                new AgentIdentity("agent-b"),
                new AgentIdentity("agent-a"),
                Supportable,
                BoundsCurrencyCheck.Evaluate("V-1",
                    new Dictionary<string, string>(StringComparer.Ordinal) { ["fill_setpoint"] = "500" },
                    new Dictionary<string, string>(StringComparer.Ordinal) { ["fill_setpoint"] = "500" },
                    AssertionBoundsExpectation.NotStated("this fixture declares a bound"))),
            Enumeration,
            Run(),
            slotIndex: 3,
            waveIndex: 7,
            new StimulusEvidence(true, true, 40, 8, ManifestPresence.Loaded,
                new VersionReport(VersionOutcome.Confirmed, Build.Value, Build.Value, 3, 1, "confirmed")),
            StimulusExpectation.AtLeastOneScanPerRoundTrip(8),
            new SettlingReport(SettlingState.Settled, "stated by a test fixture."),
            new[] { AssertionOutcome.Compare("REQ-14.a", "Demo_Count", "10", "10") },
            new[] { 1, 4 },
            Map(),
            Build,
            slotsCoveredByOneRead: 1,
            program: null,
            neighboursNotDerived: neighboursNotDerived);

    [Fact]
    public void A_NOT_DERIVED_NEIGHBOUR_LIST_BECOMES_A_CAVEAT_ON_THE_PACKAGE()
    {
        var package = Package(
            "NEIGHBOURS: NOT DERIVED — the escape `--neighbours declared-only` was named.");

        Assert.Contains(package.Stamp.Caveats,
            c => c.Contains("NEIGHBOURS: NOT DERIVED", StringComparison.Ordinal));
        Assert.Contains(package.Stamp.Caveats,
            c => c.Contains("declared-only", StringComparison.Ordinal));
    }

    /// <summary>
    /// 🔴 <b>Through a real serialise and parse.</b> A consumer reads the FILE, never a rendering, and
    /// anything the renderer drops is gone before a reviewer sees it.
    /// </summary>
    [Fact]
    public void AND_IT_SURVIVES_INTO_THE_ARTIFACT()
    {
        var text = ResultPackageJson.Of(Package("NEIGHBOURS: NOT DERIVED — nobody derived one."))
            .ToJsonString(new JsonSerializerOptions { WriteIndented = true });

        Assert.Contains("NEIGHBOURS: NOT DERIVED", text, StringComparison.Ordinal);
    }

    /// <summary>A derived batch supplies nothing, and the package grows no caveat — see the class remark.</summary>
    [Fact]
    public void A_DERIVED_BATCH_ADDS_NO_CAVEAT()
    {
        Assert.DoesNotContain(Package(null).Stamp.Caveats,
            c => c.Contains("NEIGHBOURS", StringComparison.Ordinal));
    }
}
