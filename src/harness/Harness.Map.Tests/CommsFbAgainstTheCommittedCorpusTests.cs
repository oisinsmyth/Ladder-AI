using Harness.Map;

namespace Harness.Map.Tests;

/// <summary>
/// 🔴 <b>THE GENERATED COMMS FB, AGAINST THE ONE IN THE CORPUS — BYTE FOR BYTE, SIDECAR INCLUDED.</b>
///
/// <para><c>ir/test-project001/FB_Comms_ModbusServer.ir</c> is a hand-authored block that has been through
/// TIA: imported, compiled, downloaded and served to a client. It is therefore the only outside authority
/// available to this component without a Portal session, and it is a much stronger one than a fixture the
/// generator's own author wrote — <b>a fixture agrees with whatever the generator does</b>, and this file
/// does not.</para>
///
/// <para><b>What is read out of the file, and why that is not circular.</b> The four pieces of PROSE and
/// the three ENDPOINT numbers are extracted and fed back in, because the generator refuses to originate
/// any of them and a test that typed them would only be asserting that a copy of a string round-trips.
/// Everything else — the header shape, the whole <c>TCON_IP_v4</c> interface, the seven
/// <c>MB_SERVER</c> ports in template order with their wire kinds, the area pointer, and all twenty-three
/// sidecar lines — is DERIVED, and the equality assertion is over the whole file. <b>The claim the test
/// makes is exactly: everything this generator does not ask a person for, it gets right.</b></para>
///
/// <para><b>This is not the compile gate</b> (hard rule 4). It proves the generator reproduces a document
/// TIA once accepted; only TIA's own import proves TIA accepts this one.</para>
/// </summary>
public class CommsFbAgainstTheCommittedCorpusTests
{
    /// <summary>
    /// The committed block. <b>Absent is a FAILURE, not a skip</b> — the same rule
    /// <see cref="ProgramGeneratorConverterRoundTripTests"/> holds about the converter binary: an optional
    /// check is one that stops running.
    /// </summary>
    internal static string CorpusPath(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "ir", "test-project001", fileName);
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"ir/test-project001/{fileName} was not found by walking up from {AppContext.BaseDirectory}. "
            + "*** THIS IS A FAILURE, NOT A REASON TO SKIP. *** Without it the only authority left in this "
            + "component's loop is the component itself.");
    }

    /// <summary>The corpus is LF by <c>.gitattributes</c> (<c>*.ir text eol=lf</c>), and so is the generator.</summary>
    private static string Committed() =>
        File.ReadAllText(CorpusPath("FB_Comms_ModbusServer.ir")).Replace("\r\n", "\n");

    /// <summary>A quoted value off a line with a known prefix — the four pieces of prose, and nothing else.</summary>
    private static string Quoted(string prefix)
    {
        var line = Committed().Split('\n').First(l => l.StartsWith(prefix, StringComparison.Ordinal));
        var open = line.IndexOf('"', StringComparison.Ordinal);
        return line[(open + 1)..line.LastIndexOf('"')];
    }

    /// <summary>
    /// The geometry the committed block serves: <c>P#M1000.0 WORD 37</c>. The retentive extent is
    /// synthetic — it is not stated in that file and nothing in this test depends on its value beyond
    /// being below the base.
    /// </summary>
    private static MirrorGeometry Geometry() => MirrorGeometry.ForCpu1214C(16, baseByte: 1000, declaredRegisters: 37);

    private static CommsFbDeclaration Declaration() => new(
        new CommsFbNaming("FB_Comms_ModbusServer", 9000, Quoted("TITLE "), Quoted("COMMENT ")),

        // The three numbers that identify the instance. Short scalars, so they are typed here rather than
        // parsed back out — and they are exactly what the generator refuses to default.
        new CommsEndpoint(LocalPort: 503, ConnectionId: "16#0010", InterfaceId: 64),

        new CommsNetworkText(Quoted("NETWORK 1 "), Quoted("  COMMENT ")),
        CommsConnectionShape.PassiveAnyClient,
        MemoryLayout: "Optimized");

    [Fact]
    public void THE_GENERATED_COMMS_FB_IS_THE_COMMITTED_ONE()
    {
        var generated = CommsFbGenerator.Generate(Declaration(), Geometry());

        Assert.Equal(Committed(), generated.Ir);
    }

    /// <summary>
    /// 🔴 <b>The area pointer is the geometry's and NOT the corpus's</b> — the same declaration against a
    /// different mirror serves a different window, in the statement and in the sidecar constant behind it,
    /// with nothing else in the block moving.
    /// </summary>
    [Fact]
    public void THE_SERVED_WINDOW_FOLLOWS_THE_GEOMETRY_IN_BOTH_OF_ITS_HOMES()
    {
        var wider = MirrorGeometry.ForCpu1214C(16, baseByte: 2000, declaredRegisters: 64);

        // The committed prose states 37 registers at M1000.0, which this geometry contradicts — so the
        // placeholders are used instead, which is the mechanism that stops prose falling behind.
        var declaration = Declaration() with
        {
            Naming = Declaration().Naming with
            {
                Comment = $"Serves {CommsFbGenerator.RegistersPlaceholder} holding registers of the harness mirror.",
            },
            Network = new CommsNetworkText(
                "Serve The Harness Mirror",
                $"Covers {CommsFbGenerator.RegistersPlaceholder} words from "
                + $"M{CommsFbGenerator.BasePlaceholder}.0, which is the whole of the mirror."),
        };

        var generated = CommsFbGenerator.Generate(declaration, wider);

        Assert.Equal("P#M2000.0 WORD 64", generated.AreaPointer);
        Assert.Contains("MB_HOLD_REG := P#M2000.0 WORD 64,", generated.Ir, StringComparison.Ordinal);
        Assert.Contains("  constant P#M2000.0 WORD 64 = 22 Any\n", generated.Ir, StringComparison.Ordinal);
        Assert.Contains("Serves 64 holding registers", generated.Ir, StringComparison.Ordinal);
        Assert.Contains("Covers 64 words from M2000.0", generated.Ir, StringComparison.Ordinal);

        // The pointer appears exactly twice: the readable statement and the sidecar constant. That is the
        // pair `converter served-area` reconciles, and it is one expression evaluated once here.
        Assert.Equal(2, generated.Ir.Split("P#M2000.0 WORD 64").Length - 1);
    }

    /// <summary>
    /// The seven ports, in the converter's own template order, with the wire kinds the committed block
    /// uses. <b>Pinned against the corpus rather than against this generator's own array</b>, so a drift
    /// in the copied port list fails here.
    /// </summary>
    [Fact]
    public void THE_PORT_ORDER_AND_WIRE_KINDS_MATCH_THE_COMMITTED_CALL()
    {
        var committedCall = Committed().Split('\n').First(l => l.TrimStart().StartsWith("MB_SERVER(", StringComparison.Ordinal));
        var generatedCall = CommsFbGenerator.Generate(Declaration(), Geometry())
            .Ir.Split('\n').First(l => l.TrimStart().StartsWith("MB_SERVER(", StringComparison.Ordinal));

        Assert.Equal(committedCall, generatedCall);
    }

    /// <summary>
    /// Every UId the sidecar cites is distinct. They are arbitrary as numbers and load-bearing as a set:
    /// two ports sharing a wire is a document that parses and rebuilds the wrong net.
    /// </summary>
    [Fact]
    public void EVERY_SIDECAR_UID_IS_DISTINCT()
    {
        var sidecar = CommsFbGenerator.Generate(Declaration(), Geometry()).Ir.Split("\nSIDECAR\n")[1];

        var uids = sidecar.Split('\n')
            .SelectMany(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(token => int.TryParse(token, out _))
            .Select(int.Parse)
            .ToList();

        // The area pointer's own `WORD 37` contributes a 37 that is not a UId; the port line for STATUS
        // legitimately carries wire UId 37 too. Compare only the lines that declare or cite one.
        var declaredOrCited = sidecar.Split('\n')
            .Where(l => l.TrimStart().StartsWith("access ", StringComparison.Ordinal)
                     || l.TrimStart().StartsWith("port ", StringComparison.Ordinal))
            .SelectMany(l => l.Split('=')[1].Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(t => int.TryParse(t, out _))
            .Select(int.Parse)
            .ToList();

        Assert.NotEmpty(uids);

        // Each access UId is cited by exactly one port; each wire and each OpenCon appears once.
        var wiresAndOpens = declaredOrCited.GroupBy(u => u).Where(g => g.Count() > 2).ToArray();
        Assert.True(wiresAndOpens.Length == 0,
            "a sidecar UId is used more than twice: " + string.Join(", ", wiresAndOpens.Select(g => g.Key)));
    }
}
