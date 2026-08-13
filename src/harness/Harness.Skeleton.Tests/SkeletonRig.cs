using Harness.Map;
using Harness.Skeleton;
using Harness.Wire;

namespace Harness.Skeleton.Tests;

/// <summary>
/// The whole walking skeleton assembled PC-side: map → copy layer → block under test → interpreter →
/// simulated bit memory → Modbus register view → the real client.
///
/// <para><b>Every piece here is the real one except the CPU.</b> The map is <c>MapAllocator</c>'s, the
/// copy layer is <c>CopyLayerGenerator</c>'s IR, the block under test is <c>TrivialBlock</c>'s IR, the
/// sequence is <c>SlotRun</c>'s, and the verdict is <c>TrivialBlockModel</c>'s. What is substituted is
/// the device — and the substitute EXECUTES THE GENERATED IR rather than re-implementing what it means,
/// so a defect introduced in the IR reaches the result the same way it would on the rig.</para>
/// </summary>
internal sealed class SkeletonRig
{
    /// <summary>The program under test's own %M region: above the retentive window, clear of the mirror.</summary>
    private const int ProgramBaseByte = 3000;

    private const int MirrorBaseByte = 4000;
    private const int RetentiveBytes = 256;

    private SkeletonRig(RegisterMap map, BuildStamp stamp, SimulatedPlc plc, SimulatedTransport transport,
        MirrorClient client, IReadOnlyList<HarnessObject> harnessObjects, IReadOnlyList<HarnessObject> programObjects)
    {
        Map = map;
        Stamp = stamp;
        Plc = plc;
        Transport = transport;
        Client = client;
        HarnessObjects = harnessObjects;
        ProgramObjects = programObjects;
    }

    public RegisterMap Map { get; }
    public BuildStamp Stamp { get; }
    public SimulatedPlc Plc { get; }
    public SimulatedTransport Transport { get; }
    public MirrorClient Client { get; }

    /// <summary>What the harness generated — the objects <see cref="RetentionCheck"/> is run over.</summary>
    public IReadOnlyList<HarnessObject> HarnessObjects { get; }

    /// <summary>The block under test and its tag table.</summary>
    public IReadOnlyList<HarnessObject> ProgramObjects { get; }

    public MirrorGeometry Geometry => Map.Geometry;

    public static SkeletonRig Build(TrivialBlockDefect defect = TrivialBlockDefect.None, int scansPerTransaction = 4)
    {
        var geometry = MirrorGeometry.ForCpu1214C(RetentiveBytes, MirrorBaseByte);

        var map = MapAllocator.Allocate(new WaveSetRequest(geometry, new[]
        {
            new SlotRequest("S0", VectorRegisters: 2, ResultRegisters: 2),
        })).Require();

        var program = TrivialBlock.Generate(ProgramBaseByte, blockNumber: 901, defect);
        var binding = TrivialBlock.Binding();
        var naming = new CopyLayerNaming(BlockNumber: 900);

        // The stamp is derived over the map, the binding, the naming and the program under test — never
        // over the copy layer, which CONTAINS it. Changing the defect changes the block's IR, which
        // changes the stamp: the two builds are genuinely different downloads.
        var stamp = BuildStamp.Of(map, binding, naming, program);
        var copyLayer = CopyLayerGenerator.Generate(map, binding, naming, stamp).Objects;

        // Tag tables first, then blocks in OB1 call order: copy layer, then the block under test.
        var lad = new LadProgram();
        foreach (var table in copyLayer.Concat(program).Where(o => o.Kind == HarnessObjectKind.TagTable))
            lad.WithTagTable(table.Ir);

        lad.WithBlock(copyLayer.Single(o => o.Kind == HarnessObjectKind.Block).Ir);
        lad.WithBlock(program.Single(o => o.Kind == HarnessObjectKind.Block).Ir);

        var plc = new SimulatedPlc(lad);
        var transport = new SimulatedTransport(plc, geometry, scansPerTransaction);

        return new SkeletonRig(map, stamp, plc, transport,
            new MirrorClient(map, transport, stamp), copyLayer, program);
    }

    /// <summary>One vector for the block under test, with its inert declaration.</summary>
    public static WireVector Vector(int step, int limit) => new(
        Values: new[] { (ushort)step, (ushort)limit },
        Inert: new InertDeclaration(new Dictionary<int, ushort>
        {
            // With the start bool low the block holds both outputs at zero — that IS the reset, held as a
            // level for the whole inert period (D33), and it is what the two checks are made against.
            [TrivialBlock.CountRegister] = 0,
            [TrivialBlock.DoneRegister] = 0,
        }),
        CompletionRegister: TrivialBlock.DoneRegister,
        CompletionValue: 1,
        DeclaredScans: TrivialBlockModel.Predict(step, limit).Scans);

    /// <summary>Run one vector and judge it against the model. The whole loop, in one call.</summary>
    public (SlotRunResult Run, TrivialBlockVerdict Verdict) RunAndJudge(int step, int limit)
    {
        var run = SlotRun.Run(Client, 0, Vector(step, limit));
        return (run, TrivialBlockModel.Judge(step, limit, run.Results));
    }
}
