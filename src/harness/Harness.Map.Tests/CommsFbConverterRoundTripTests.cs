using Harness.Map;

namespace Harness.Map.Tests;

/// <summary>
/// 🔴 <b>THE GENERATED COMMS FB, THROUGH THE REAL CONVERTER — AND THEN READ BACK BY THE PROVEN READ SIDE.</b>
///
/// <para><see cref="CommsFbAgainstTheCommittedCorpusTests"/> shows the generator reproduces a committed
/// file. This asks two different parties two different questions.</para>
///
/// <list type="number">
/// <item><b><c>to-xml</c>: does the emitted document convert?</b> A generated block the converter refuses
/// is worth nothing however well it reads, and this component's own tests cannot fail that way — they
/// compare the generator's output against what its author expected.</item>
/// <item><b><c>served-area</c>: does the width the block SERVES come back as the width the geometry
/// DECLARED?</b> That producer reads the area pointer AND the sidecar constant behind it, joined by the
/// port's UId, and refuses on any disagreement. It is the reason the sidecar in the emitted block is not
/// decoration: the two homes of that number are what it reconciles, and this generator writes both from
/// one expression.</item>
/// </list>
///
/// <para>🔴 <b>AND IT IS WHY THIS GENERATOR EMITS A SIDECAR AT ALL — measured here, not assumed.</b>
/// <see cref="A_READABLE_ONLY_COMMS_BLOCK_DOES_NOT_CONVERT"/> pins the finding that made the decision:
/// <c>SidecarSynthesizer</c> refuses fixed-shape instructions outright, so the readable-only form every
/// other generator in this component emits is, for this one block, a document the converter will not
/// convert.</para>
///
/// <para><b>Neither question is the compile gate</b> (hard rule 4). <c>to-xml</c> proves the document is
/// well-formed to the converter; only TIA's own import proves it is acceptable to TIA.</para>
/// </summary>
public class CommsFbConverterRoundTripTests
{
    /// <summary>
    /// A wholly invented server on an invented mirror — no name here appears in any project, and the
    /// numbers are chosen to differ from every committed one so a pass cannot come from a coincidence.
    /// </summary>
    private static MirrorGeometry Geometry() => MirrorGeometry.ForCpu1214C(16, baseByte: 2400, declaredRegisters: 41);

    private static CommsFbDeclaration Declaration() => new(
        new CommsFbNaming(
            "FB_Comms_WidgetMirrorServer",
            9411,
            "Widget Mirror Server",
            $"Serves the widget rig's mirror to a Modbus TCP client: {CommsFbGenerator.RegistersPlaceholder} holding "
            + $"registers from M{CommsFbGenerator.BasePlaceholder}.0. Synthetic test material."),
        new CommsEndpoint(LocalPort: 1502, ConnectionId: "16#0021", InterfaceId: 64),
        new CommsNetworkText(
            "Serve The Widget Mirror",
            $"Runs the server every scan over {CommsFbGenerator.RegistersPlaceholder} words from "
            + $"M{CommsFbGenerator.BasePlaceholder}.0. Synthetic test material."),
        CommsConnectionShape.PassiveAnyClient,
        MemoryLayout: "Optimized");

    private static string Emit(DirectoryInfo work)
    {
        var generated = CommsFbGenerator.Generate(Declaration(), Geometry());
        var path = Path.Combine(work.FullName, generated.BlockName + ".ir");
        File.WriteAllText(path, generated.Ir);
        return path;
    }

    [Fact]
    public void THE_GENERATED_COMMS_FB_CONVERTS_TO_SIMATICML()
    {
        var work = Directory.CreateTempSubdirectory("commsfb-roundtrip-");
        try
        {
            var path = Emit(work);
            var (exit, output) = ConverterProcess.Run("to-xml", path);

            Assert.True(exit == 0, $"converter to-xml refused the generated comms FB (exit {exit}): {output}");
            Assert.True(File.Exists(Path.ChangeExtension(path, ".xml")),
                "converter to-xml reported success and wrote no XML.");
        }
        finally
        {
            work.Delete(recursive: true);
        }
    }

    /// <summary>
    /// 🔴 <b>THE READ SIDE AGREES WITH THE GEOMETRY — and `served-area` reads BOTH homes of the number,
    /// so this passing means the statement and the sidecar constant say the same thing.</b>
    /// </summary>
    [Fact]
    public void SERVED_AREA_DERIVES_THE_DECLARED_WINDOW_FROM_THE_GENERATED_BLOCK()
    {
        var work = Directory.CreateTempSubdirectory("commsfb-servedarea-");
        try
        {
            Emit(work);
            var (exit, output) = ConverterProcess.Run("served-area", "--project", work.FullName);

            // Exit 2 is NOT DERIVED and is never a pass — the producer's own rule, restated here so a
            // future reader cannot mistake a 2 for a green.
            Assert.True(exit == 0, $"converter served-area did not derive the window (exit {exit}): {output}");
            Assert.Contains("2400", output, StringComparison.Ordinal);
            Assert.Contains("41", output, StringComparison.Ordinal);
        }
        finally
        {
            work.Delete(recursive: true);
        }
    }

    /// <summary>
    /// 🔴 <b>THE MEASUREMENT BEHIND THE ONE PLACE THIS COMPONENT DEPARTS FROM ADR-0005.</b>
    ///
    /// <para>Every other generator here emits readable-only IR and lets the converter synthesize the
    /// machine half. Strip the SIDECAR off a comms block and <c>to-xml</c> fails:
    /// <c>UnsupportedSynthesisConstructException: Network 1: sidecar synthesis does not support:
    /// FixedShapes</c>. <b>Pinned as a test so that if the converter ever grows fixed-shape synthesis,
    /// this fails and someone re-reads the decision</b> rather than the generator carrying a hand-written
    /// sidecar forever for a reason that stopped being true.</para>
    /// </summary>
    [Fact]
    public void A_READABLE_ONLY_COMMS_BLOCK_DOES_NOT_CONVERT()
    {
        var work = Directory.CreateTempSubdirectory("commsfb-nosidecar-");
        try
        {
            var generated = CommsFbGenerator.Generate(Declaration(), Geometry());
            var path = Path.Combine(work.FullName, generated.BlockName + ".ir");
            File.WriteAllText(path, generated.Ir.Split("\nSIDECAR\n")[0] + "\n");

            var (exit, output) = ConverterProcess.Run("to-xml", path);

            Assert.True(exit != 0,
                "converter to-xml ACCEPTED a readable-only comms block. If sidecar synthesis has grown fixed-shape "
                + "support, CommsFbGenerator should stop emitting a hand-built SIDECAR and let the converter derive it "
                + "(ADR-0005), which is what every other generator in this component does.");
            Assert.Contains("FixedShapes", output, StringComparison.Ordinal);
        }
        finally
        {
            work.Delete(recursive: true);
        }
    }
}
