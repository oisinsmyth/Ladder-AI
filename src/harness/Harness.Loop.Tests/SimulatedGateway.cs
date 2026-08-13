using Harness.Loop;
using Harness.Map;
using Harness.Skeleton;
using Harness.Wire;

namespace Harness.Loop.Tests;

/// <summary>
/// A device gateway that "deploys" by loading the generated IR into the LAD interpreter.
///
/// <para><b>It is the closest thing to a download available PC-side, and it is honest about which
/// half it is.</b> The objects it accepts are the ones the loop actually generated, it parses them
/// with the real interpreter — which refuses any construct it does not implement — and its manifest is
/// the set of object names that parsed. What it is not is a download: no import, no compile, no TIA,
/// no controller.</para>
///
/// <para>Every call is recorded, because the property that matters most about the loop's ordering is
/// that <b>an inadmissible submission reaches none of these methods at all</b>.</para>
/// </summary>
internal sealed class SimulatedGateway : IDeviceGateway
{
    private readonly MirrorGeometry _geometry;
    private SimulatedPlc? _plc;

    public SimulatedGateway(MirrorGeometry geometry, int scansPerTransaction = 4)
    {
        _geometry = geometry;
        ScansPerTransaction = scansPerTransaction;
    }

    public int ScansPerTransaction { get; set; }

    /// <summary>Deployments attempted. Zero is the assertion that matters when a submission is refused.</summary>
    public int Deployments { get; private set; }

    /// <summary>Transports opened. Also zero when nothing was admitted.</summary>
    public int Opens { get; private set; }

    /// <summary>Objects to omit from the manifest — a download that loaded less than it was given.</summary>
    public HashSet<string> OmitFromManifest { get; } = new(StringComparer.Ordinal);

    /// <summary>Report the deployment as never attempted, whatever it was handed.</summary>
    public bool Refuse { get; set; }

    /// <summary>Publish a different build stamp than the one deployed — a download that did not land.</summary>
    public uint? PublishVersionInstead { get; set; }

    public SimulatedPlc Plc => _plc ?? throw new InvalidOperationException("nothing has been deployed.");

    public DeploymentOutcome Deploy(IReadOnlyList<HarnessObject> objects, BuildStamp stamp)
    {
        Deployments++;

        if (Refuse)
        {
            return new DeploymentOutcome(false, false, new HashSet<string>(StringComparer.Ordinal),
                "this gateway was configured to refuse.");
        }

        var program = new LadProgram();
        foreach (var table in objects.Where(o => o.Kind == HarnessObjectKind.TagTable))
            program.WithTagTable(table.Ir);

        // OB1 call order: the copy layer first, then the blocks under test. The copy layer is the one
        // whose tag table carries the mirror, so it is identified by prefix rather than by position.
        foreach (var block in objects.Where(o => o.Kind == HarnessObjectKind.Block).OrderBy(o => o.Name.Contains("CopyLayer", StringComparison.Ordinal) ? 0 : 1))
            program.WithBlock(block.Ir);

        _plc = new SimulatedPlc(program);

        // *** THE MANIFEST OMITS TAG TABLES, BECAUSE A REAL ONE DOES. ***
        //
        // A PLC tag table carries no load message. The one measured 19-object manifest from this rig
        // names an FC, an FB, its instance DB, OB1, MB_SERVER and nine TCP_MB_* helpers — and no tag
        // table. This fake used to report every object name it was handed, which made the manifest
        // comparison in LoopRun agree with a shape no device produces: the check passed here and would
        // have reported ABSENT on every healthy real download.
        //
        // Keeping the fake faithful is what makes that comparison testable at all — revert LoopRun's
        // tag-table exclusion and these tests go red.
        var downloadable = objects.Where(o => o.Kind != HarnessObjectKind.TagTable).ToArray();
        var manifest = downloadable.Select(o => o.Name).Where(n => !OmitFromManifest.Contains(n)).ToHashSet(StringComparer.Ordinal);

        return new DeploymentOutcome(true, manifest.Count == downloadable.Length, manifest,
            $"{manifest.Count} of {downloadable.Length} downloadable object(s) parsed and loaded into the interpreter "
            + $"({objects.Count - downloadable.Length} tag table(s) carry no load message and are absent from the manifest, as on a real download).");
    }

    public IRegisterTransport Open()
    {
        Opens++;
        var transport = new SimulatedTransport(Plc, _geometry, ScansPerTransaction);

        if (PublishVersionInstead is { } other)
        {
            // Overwrite the published stamp AFTER the copy layer has run once, so the mirror looks like a
            // device running a different build rather than one that never ran at all.
            _plc!.Run(1);
            var words = RegisterWords.From32(other, RegisterWordOrder.HighWordFirst);
            var b = _geometry.ByteAddressOf(0);
            _plc.Memory[b] = (byte)(words[0] >> 8);
            _plc.Memory[b + 1] = (byte)words[0];
            _plc.Memory[b + 2] = (byte)(words[1] >> 8);
            _plc.Memory[b + 3] = (byte)words[1];
            _plc.Running = false;
        }

        return transport;
    }

    public void Dispose() { }
}
