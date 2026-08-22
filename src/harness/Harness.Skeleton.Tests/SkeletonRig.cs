using Harness.Map;
using Harness.Skeleton;
using Harness.Wire;

namespace Harness.Skeleton.Tests;

/// <summary>
/// The whole walking skeleton assembled PC-side: map → copy layer → blocks under test → interpreter →
/// simulated bit memory → Modbus register view → the real client.
///
/// <para><b>Every piece here is the real one except the CPU.</b> The map is <c>MapAllocator</c>'s, the
/// copy layer is <c>CopyLayerGenerator</c>'s IR, the blocks under test are the generators' IR, the
/// sequence is <c>WaveRun</c>'s, and the verdicts are the models'. What is substituted is the device —
/// and the substitute EXECUTES THE GENERATED IR rather than re-implementing what it means, so a defect
/// or a coupling introduced in the IR reaches the result the same way it would on the rig.</para>
/// </summary>
internal sealed class SkeletonRig
{
    /// <summary>The ramp block's own %M region: above the retentive window, clear of the mirror.</summary>
    private const int RampBaseByte = 3000;

    /// <summary>The peak block's region. Disjoint from the ramp's — sharing one would be interference by address.</summary>
    private const int PeakBaseByte = 3100;

    private const int MirrorBaseByte = 4000;
    private const int RetentiveBytes = 256;

    /// <summary>Slot ordinal of the ramp block in a two-slot rig.</summary>
    public const int RampSlot = 0;

    /// <summary>Slot ordinal of the peak block in a two-slot rig.</summary>
    public const int PeakSlot = 1;

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

    /// <summary>The blocks under test and their tag tables.</summary>
    public IReadOnlyList<HarnessObject> ProgramObjects { get; }

    public MirrorGeometry Geometry => Map.Geometry;

    /// <summary>A one-slot rig carrying only the ramp block — phase 2's shape, kept as the solo baseline.</summary>
    public static SkeletonRig Build(TrivialBlockDefect defect = TrivialBlockDefect.None, int scansPerTransaction = 4) =>
        Assemble(
            new[] { new SlotRequest("S0", 2, 2) },
            TrivialBlock.Generate(RampBaseByte, blockNumber: 901, defect),
            new[] { TrivialBlock.Binding("S0") },
            scansPerTransaction);

    /// <summary>
    /// A two-slot rig: the ramp block in slot 0, the peak block in slot 1.
    /// </summary>
    /// <param name="coupling">
    /// Whether the peak block writes into the ramp block's accumulator. This is the ONLY difference
    /// between the interfering pair and the disjoint one.
    /// </param>
    public static SkeletonRig BuildPair(
        PeakBlockCoupling coupling = PeakBlockCoupling.None,
        TrivialBlockDefect defect = TrivialBlockDefect.None,
        int scansPerTransaction = 4)
    {
        var program = TrivialBlock.Generate(RampBaseByte, blockNumber: 901, defect)
            .Concat(PeakBlock.Generate(PeakBaseByte, blockNumber: 902, coupling))
            .ToArray();

        return Assemble(
            new[] { new SlotRequest("S0", 2, 2), new SlotRequest("S1", 2, 2) },
            program,
            new[] { TrivialBlock.Binding("S0"), PeakBlock.Binding("S1") },
            scansPerTransaction);
    }

    private static SkeletonRig Assemble(
        IReadOnlyList<SlotRequest> slots,
        IReadOnlyList<HarnessObject> program,
        IReadOnlyList<SlotBinding> bindings,
        int scansPerTransaction)
    {
        var geometry = MirrorGeometry.ForCpu1214C(RetentiveBytes, MirrorBaseByte, declaredRegisters: (MirrorGeometry.Cpu1214CBitMemoryBytes - MirrorBaseByte) / 2);
        var map = MapAllocator.Allocate(new WaveSetRequest(geometry, slots)).Require();
        var naming = new CopyLayerNaming(BlockNumber: 900);

        // The stamp is derived over the map, the bindings, the naming and the program under test — never
        // over the copy layer, which CONTAINS it. Coupling the second block changes its IR, so the coupled
        // and disjoint pairs are genuinely different downloads.
        var stamp = BuildStamp.Of(map, bindings, naming, program);
        var copyLayer = CopyLayerGenerator.Generate(map, bindings, naming, stamp).Objects;

        // Tag tables first, then blocks in OB1 call order: copy layer, then the blocks under test.
        var lad = new LadProgram();
        foreach (var table in copyLayer.Concat(program).Where(o => o.Kind == HarnessObjectKind.TagTable))
            lad.WithTagTable(table.Ir);

        lad.WithBlock(copyLayer.Single(o => o.Kind == HarnessObjectKind.Block).Ir);
        foreach (var block in program.Where(o => o.Kind == HarnessObjectKind.Block))
            lad.WithBlock(block.Ir);

        var plc = new SimulatedPlc(lad);
        var transport = new SimulatedTransport(plc, geometry, scansPerTransaction);

        return new SkeletonRig(map, stamp, plc, transport,
            new MirrorClient(map, transport, stamp), copyLayer, program);
    }

    /// <summary>One ramp vector, with its inert declaration.</summary>
    public static WireVector RampVector(int step, int limit) => new(
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
        Duration: new ScanBudget(TrivialBlockModel.Predict(step, limit).Scans, 1));

    /// <summary>One peak vector, with its inert declaration.</summary>
    public static WireVector PeakVector(int level, int trip) => new(
        Values: new[] { (ushort)level, (ushort)trip },
        Inert: new InertDeclaration(new Dictionary<int, ushort>
        {
            [PeakBlock.PeakRegister] = 0,
            [PeakBlock.AlarmRegister] = 0,
        }),
        CompletionRegister: PeakBlock.AlarmRegister,
        CompletionValue: 1,
        Duration: new ScanBudget(PeakBlockModel.Predict(level, trip).Scans, 1));

    /// <summary>Run one vector against slot 0 of a one-slot rig and judge it against the ramp model.</summary>
    public (SlotRunResult Run, TrivialBlockVerdict Verdict) RunAndJudge(int step, int limit)
    {
        var run = SlotRun.Run(Client, RuntimeCompression.Uncompressed, 0, RampVector(step, limit));
        return (run, TrivialBlockModel.Judge(step, limit, run.Results));
    }

    /// <summary>Run a wave over the given tensors on this rig.</summary>
    public WaveResult RunWave(IReadOnlyList<SlotTensor> tensors, Action<SlotDistribution>? onSlotComplete = null) =>
        WaveRun.Run(Client, RuntimeCompression.Uncompressed, tensors, onSlotComplete: onSlotComplete);
}
