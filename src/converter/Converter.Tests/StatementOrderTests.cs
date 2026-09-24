using System.IO;
using System.Linq;
using System.Xml.Linq;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

// A network's statement order IS its rung order (ir/SPEC.md, "Statement order within one network").
//
// Until this existed the readable IR was grouped by kind — every TON first, then every coil, ... — so it
// could not say that a timer rung sits BETWEEN two coil rungs. The sidecar's source UIds kept the order on
// a sidecar round trip, but a sidecar-less to-xml synthesizes UIds in statement order, and FlgNetBuilder
// orders the rebuilt parts by UId: the engineer's network came back with the timer rung hoisted to the
// top. Logic identical, drawing rearranged, promotion blocked. Rung order is also scan order, so it is not
// cosmetic either — which is why it has to be in the readable IR (ADR-0010), not only in the sidecar.
//
// These tests replace FI-69's out-of-order DIAGNOSTIC tests: an interleaved network is no longer an error
// to be explained, it is the representation. The one positional rule left, a network COMMENT directly under
// the header, keeps a message that names it rather than blaming the header.
public class StatementOrderTests
{
    private static FlgNetwork LoadFixture(string name) =>
        FlgNetParser.Parse(XElement.Load(Path.Combine("Fixtures", name)));

    private const string InterleavedIr =
        "NETWORK 1 \"Rungs\"\n" +
        "  COIL DemoOut.X := DemoIn.A\n" +
        "  TON(DemoTimer, IN := DemoIn.B, PT := T#2S)\n" +
        "  COIL DemoOut.Y := DemoTimer.Q\n" +
        "  COIL DemoOut.Z := DemoIn.C\n";

    private static string[] PartNamesInWrittenOrder(XElement flgNet) =>
        flgNet.Elements().First(e => e.Name.LocalName == "Parts")
            .Elements().Where(e => e.Name.LocalName == "Part")
            .Select(e => e.Attribute("Name")!.Value)
            .ToArray();

    [Fact]
    public void ToIr_ATimerRungBetweenCoilRungs_IsListedWhereTheSourceHasIt()
    {
        var reduced = GraphReducer.Reduce(LoadFixture("RungOrderTimerBetweenCoils.xml"), networkNumber: 1, title: "Rungs", compileUnitUId: "3");

        Assert.Equal(InterleavedIr, IrSerializer.SerializeNetworkOnly(reduced.Network));
    }

    // The regression the live run hit: to-ir, then to-xml WITHOUT a sidecar (the path an edited block
    // takes). Before, the TON rung came out first.
    [Fact]
    public void SidecarlessToXml_KeepsTheTimerRungBetweenTheCoilRungs()
    {
        var reduced = GraphReducer.Reduce(LoadFixture("RungOrderTimerBetweenCoils.xml"), networkNumber: 1, title: "Rungs", compileUnitUId: "3");
        var network = IrParser.ParseNetworkOnly(IrSerializer.SerializeNetworkOnly(reduced.Network));

        var flgNet = FlgNetWriter.Write(FlgNetBuilder.Build(network, SidecarSynthesizer.Synthesize(network)));

        Assert.Equal(new[] { "Contact", "Coil", "Contact", "TON", "Coil", "Contact", "Coil" }, PartNamesInWrittenOrder(flgNet));
    }

    [Fact]
    public void SidecarToXml_KeepsTheSourcePartOrder()
    {
        var source = LoadFixture("RungOrderTimerBetweenCoils.xml");
        var reduced = GraphReducer.Reduce(source, networkNumber: 1, title: "Rungs", compileUnitUId: "3");

        var flgNet = FlgNetWriter.Write(FlgNetBuilder.Build(reduced.Network, reduced.Sidecar));

        var writtenUIds = flgNet.Elements().First(e => e.Name.LocalName == "Parts")
            .Elements().Where(e => e.Name.LocalName == "Part")
            .Select(e => int.Parse(e.Attribute("UId")!.Value));
        Assert.Equal(source.Parts.Select(p => p.UId), writtenUIds);
    }

    [Fact]
    public void AnInterleavedNetwork_ParsesAndReserializesByteIdentically()
    {
        var network = IrParser.ParseNetworkOnly(InterleavedIr);

        Assert.Equal(
            new[]
            {
                new IrStatementRef(IrStatementKind.Assignment, 0),
                new IrStatementRef(IrStatementKind.Timer, 0),
                new IrStatementRef(IrStatementKind.Assignment, 1),
                new IrStatementRef(IrStatementKind.Assignment, 2),
            },
            network.OrderedStatements());
        Assert.Equal(InterleavedIr, IrSerializer.SerializeNetworkOnly(network));
    }

    // Two runs of one kind split by another — FI-69's "second run of a closed kind" — is now simply
    // three rungs in that order.
    [Fact]
    public void ASecondRunOfAKind_IsKeptInPlace()
    {
        const string text =
            "NETWORK 1 \"T\"\n" +
            "  MOVE(EN := A, IN := 1) => Status.Code\n" +
            "  CALL PumpControl(Pump1_DB, EN := TRUE)\n" +
            "  MOVE(EN := B, IN := 2) => Status.Word\n";

        var network = IrParser.ParseNetworkOnly(text);

        Assert.Equal(2, network.Moves.Count);
        Assert.Single(network.Calls);
        Assert.Equal(text, IrSerializer.SerializeNetworkOnly(network));
    }

    // A kind-ordered network keeps its long-standing representation: no explicit order at all, so every
    // grouped IR file already on disk parses, serializes and synthesizes exactly as before.
    [Fact]
    public void AKindOrderedNetwork_CarriesNoExplicitOrder()
    {
        const string text =
            "NETWORK 7 \"Mixed\"\n" +
            "  COMMENT \"Everything, in kind order\"\n" +
            "  TON(RunDelay, IN := Sensor1.Ok, PT := T#100MS)\n" +
            "  COIL Status.Valid := RunPermit\n" +
            "  RCOIL Status.Fault := ResetCmd\n" +
            "  MOVE(EN := RunPermit, IN := SourceValue) => Status.Code\n" +
            "  CALL PumpControl(Pump1_DB, EN := TRUE)\n" +
            "  CONVERT(EN := TRUE, IN := RawValue) => Scaled\n";

        var network = IrParser.ParseNetworkOnly(text);

        Assert.Null(network.StatementOrder);
        Assert.Equal("Everything, in kind order", network.Comment);
        Assert.Equal(text, IrSerializer.SerializeNetworkOnly(network));
    }

    [Fact]
    public void AMisplacedComment_IsNamedRatherThanReportedAsABadHeader()
    {
        var ex = Assert.Throws<IrFormatException>(() => IrParser.ParseNetworkOnly(
            "NETWORK 4 \"T\"\n" +
            "  COIL Status.Valid := RunPermit\n" +
            "  COMMENT \"Late\"\n"));

        Assert.Contains("Network 4, line 3", ex.Message);
        Assert.Contains("COMMENT must come directly under the NETWORK header", ex.Message);
        Assert.DoesNotContain("Expected 'NETWORK", ex.Message);
    }

    // An explicit order that no longer matches the statement lists is refused, never patched: a guessed
    // rung order is a different program that still compiles.
    [Fact]
    public void AStaleExplicitOrder_IsRefused()
    {
        var network = IrParser.ParseNetworkOnly(InterleavedIr);
        var stale = network with { Assignments = network.Assignments.Take(2).ToList() };

        Assert.Throws<IrFormatException>(() => stale.OrderedStatements());
    }
}
