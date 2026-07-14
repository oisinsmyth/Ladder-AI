using System.Xml.Linq;
using Converter;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// `Modbus_Comm_Load` — Modbus RTU/TCP port-configuration instruction (Phase 2 Tier 4,
/// 2026-07-14). Grounded against a real export sweep of `JOB9002`'s full block inventory
/// (`FC ModbusComs`): `&lt;Part Name="Modbus_Comm_Load" Version="5.0" UId="N"&gt;&lt;Instance
/// Scope="GlobalVariable" UId="M"&gt;&lt;Component Name="MB_Master_Comm" /&gt;&lt;/Instance&gt;
/// &lt;/Part&gt;` — Instance-DB-backed like `Modbus_Master`, no `DisabledENO`. `REQ`/`PORT`/
/// `BAUD`/`PARITY`/`RESP_TO`/`MB_DB` are ordinary tag-or-literal inputs (unlike `Modbus_Master`'s
/// own `REQ`, this one is a plain `IdentCon`-fed tag in every real instance seen).
/// `FLOW_CTRL`/`RTS_ON_DLY`/`RTS_OFF_DLY` are real ports always left wired to `&lt;OpenCon&gt;`
/// (deliberately unconnected) — the same shape TON's own `ET` already established
/// (`ResolveOptionalOpenPort`/`OpenConnectionSidecar`, generalized once this second real
/// instruction confirmed the shape isn't TON-`ET`-specific), so they never appear on the readable
/// form, sidecar-only. `DONE`/`ERROR`/`STATUS` are three separate plain-tag writes.
/// </summary>
public class ModbusCommLoadTests
{
    private static FlgNetwork LoadFixture(string name) =>
        FlgNetParser.Parse(XElement.Load(Path.Combine("Fixtures", name)));

    [Fact]
    public void Parse_ModbusCommLoadFedByRail_ProducesModbusCommLoadPart()
    {
        var network = LoadFixture("ModbusCommLoadFedByRail.xml");

        var part = Assert.Single(network.Parts, p => p.Name == "Modbus_Comm_Load");
        Assert.Equal(30, part.UId);
        Assert.Equal("5.0", part.Version);
        Assert.NotNull(part.Instance);
        Assert.Equal(new[] { "MB_Master_Comm" }, part.Instance!.ComponentPath);
    }

    [Fact]
    public void Reduce_ModbusCommLoadFedByRail_EnIsRailAndAllOperandsResolve()
    {
        var network = LoadFixture("ModbusCommLoadFedByRail.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Modbus port setup", compileUnitUId: "62");

        var statement = Assert.Single(reduced.Network.ModbusCommLoads);
        var en = Assert.IsType<EnSource.Condition>(statement.En);
        Assert.Empty(Assert.IsType<Expr.And>(en.Value).Operands);

        Assert.Equal("LoadRequest", Assert.IsType<Expr.TagRef>(statement.Req).Path);
        Assert.Equal("PortNumber", Assert.IsType<Expr.TagRef>(statement.Port).Path);
        Assert.Equal("BaudRate", Assert.IsType<Expr.TagRef>(statement.Baud).Path);
        Assert.Equal("ParitySetting", Assert.IsType<Expr.TagRef>(statement.Parity).Path);
        Assert.Equal("ResponseTimeout", Assert.IsType<Expr.TagRef>(statement.RespTo).Path);
        Assert.Equal("Modbus_Master_DB", Assert.IsType<Expr.TagRef>(statement.MbDb).Path);

        Assert.Equal("MB_Master_Comm", statement.InstancePath);
        Assert.Equal("LoadDone", statement.DoneTag);
        Assert.Equal("LoadError", statement.ErrorTag);
        Assert.Equal("LoadStatus", statement.StatusTag);
    }

    [Fact]
    public void Reduce_ModbusCommLoadFedByRail_SidecarRecordsThreeOpenConnections()
    {
        var network = LoadFixture("ModbusCommLoadFedByRail.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Modbus port setup", compileUnitUId: "62");

        var sidecar = Assert.Single(reduced.Sidecar.ModbusCommLoads);
        Assert.Equal("5.0", sidecar.Version);
        Assert.NotNull(sidecar.FlowCtrl);
        Assert.NotNull(sidecar.RtsOnDly);
        Assert.NotNull(sidecar.RtsOffDly);
        Assert.Equal(40, sidecar.FlowCtrl!.OpenConUId);
        Assert.Equal(41, sidecar.RtsOnDly!.OpenConUId);
        Assert.Equal(42, sidecar.RtsOffDly!.OpenConUId);
    }

    [Fact]
    public void RoundTrip_ModbusCommLoadFedByRail_RebuildsIdenticalTopology()
    {
        var original = LoadFixture("ModbusCommLoadFedByRail.xml");
        var reduced = GraphReducer.Reduce(original, networkNumber: 1, title: "Modbus port setup", compileUnitUId: "62");

        var rebuilt = FlgNetBuilder.Build(reduced.Network, reduced.Sidecar);
        var xml = FlgNetWriter.Write(rebuilt);
        var reparsed = FlgNetParser.Parse(xml);

        Assert.Equal(original.Parts.Count, reparsed.Parts.Count);
        Assert.Equal(original.Wires.Count, reparsed.Wires.Count);

        var part = Assert.Single(reparsed.Parts, p => p.Name == "Modbus_Comm_Load");
        Assert.Equal("5.0", part.Version);

        var flowCtrlWire = Assert.Single(reparsed.Wires, w => w.UId == 37);
        Assert.Contains(flowCtrlWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 30 && e.PortName == "FLOW_CTRL");
        Assert.Contains(flowCtrlWire.Endpoints, e => e.Kind == EndpointKind.OpenCon);
    }

    [Fact]
    public void SerializeNetworkOnly_ModbusCommLoadFedByRail_ProducesReadableSyntaxWithoutOpenPorts()
    {
        var network = LoadFixture("ModbusCommLoadFedByRail.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Modbus port setup", compileUnitUId: "62");

        var text = IrSerializer.SerializeNetworkOnly(reduced.Network);

        Assert.Contains(
            "  MODBUS_COMM_LOAD(MB_Master_Comm, EN := TRUE, REQ := LoadRequest, PORT := PortNumber, BAUD := BaudRate, " +
            "PARITY := ParitySetting, RESP_TO := ResponseTimeout, MB_DB := Modbus_Master_DB, DONE => LoadDone, " +
            "ERROR => LoadError, STATUS => LoadStatus)\n", text);
        Assert.DoesNotContain("FLOW_CTRL", text);
        Assert.DoesNotContain("RTS_ON_DLY", text);
    }

    [Fact]
    public void FullBlock_ModbusCommLoadFedByRail_ParseThenSerialize_IsByteIdentical()
    {
        var network = LoadFixture("ModbusCommLoadFedByRail.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Modbus port setup", compileUnitUId: "62");

        var block = new IrBlock("0", "FC", "TestBlock", 1, "LAD", "A test block", new[] { reduced.Network });
        var text = IrSerializer.SerializeBlock(block, new[] { reduced.Sidecar });

        var (parsedBlock, parsedSidecars) = IrParser.ParseBlock(text);
        var reserialized = IrSerializer.SerializeBlock(parsedBlock, parsedSidecars);

        Assert.Equal(text, reserialized);
    }

    [Fact]
    public void Parse_ModbusCommLoadWithConnectedFlowCtrl_ThrowsUnsupportedConstruct()
    {
        // Based on the full ModbusCommLoadFedByRail.xml shape (every other required port still
        // wired) with FLOW_CTRL's own OpenCon swapped for a real tag connection — isolates the
        // one thing under test rather than risking an unrelated "missing wire" error firing first.
        var element = XElement.Load(Path.Combine("Fixtures", "ModbusCommLoadFedByRail.xml"));
        var ns = FlgNetParser.Ns;
        var flowCtrlOpenCon = element.Descendants(ns + "OpenCon").Single(e => (string?)e.Attribute("UId") == "40");
        flowCtrlOpenCon.ReplaceWith(new XElement(ns + "IdentCon", new XAttribute("UId", "21")));

        var network = FlgNetParser.Parse(element);

        var ex = Assert.Throws<UnsupportedConstructException>(
            () => GraphReducer.Reduce(network, networkNumber: 1, title: "t", compileUnitUId: "1"));
        Assert.Contains("FLOW_CTRL", ex.Message);
    }
}
