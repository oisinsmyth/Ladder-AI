using System.Xml.Linq;
using Converter;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// `Modbus_Master` — Modbus RTU/TCP master-request instruction (Phase 2 Tier 4, 2026-07-14).
/// Grounded against a real export sweep of `JOB9002`'s full block inventory (`FC ModbusComs`):
/// `&lt;Part Name="Modbus_Master" Version="6.0" UId="N"&gt;&lt;Instance Scope="GlobalVariable"
/// UId="M"&gt;&lt;Component Name="Modbus_Master_DB" /&gt;&lt;/Instance&gt;&lt;/Part&gt;` —
/// Instance-DB-backed like TON/Call, no `DisabledENO`, no `TemplateValue` children. `REQ` is
/// genuinely different from every other operand this converter has resolved: confirmed real fed
/// by a full Contact chain's own `out` (`Contact -> Contact -> REQ` in the real export), not a
/// plain tag — resolved via `TraceChain`, the same mechanism a Coil's own condition uses.
/// `MB_ADDR`/`MODE`/`DATA_ADDR`/`DATA_LEN`/`DATA_PTR` are ordinary tag-or-literal inputs.
/// `DONE`/`BUSY`/`ERROR`/`STATUS` are four separate plain-tag writes — the second instruction this
/// converter models with more than one destination, after `MOVE_BLK_VARIANT`.
/// </summary>
public class ModbusMasterTests
{
    private static FlgNetwork LoadFixture(string name) =>
        FlgNetParser.Parse(XElement.Load(Path.Combine("Fixtures", name)));

    [Fact]
    public void Parse_ModbusMasterFedByRail_ProducesModbusMasterPart()
    {
        var network = LoadFixture("ModbusMasterFedByRail.xml");

        var part = Assert.Single(network.Parts, p => p.Name == "Modbus_Master");
        Assert.Equal(34, part.UId);
        Assert.Equal("6.0", part.Version);
        Assert.NotNull(part.Instance);
        Assert.Equal(new[] { "Modbus_Master_DB" }, part.Instance!.ComponentPath);
    }

    [Fact]
    public void Reduce_ModbusMasterFedByRail_ReqIsChainAndAllOperandsResolve()
    {
        var network = LoadFixture("ModbusMasterFedByRail.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Modbus request", compileUnitUId: "62");

        var statement = Assert.Single(reduced.Network.ModbusMasters);
        var en = Assert.IsType<EnSource.Condition>(statement.En);
        Assert.Empty(Assert.IsType<Expr.And>(en.Value).Operands);

        // Req is a two-Contact AND chain (GateBit AND TriggerBit), not a plain tag reference.
        var reqAnd = Assert.IsType<Expr.And>(statement.Req);
        Assert.Equal(2, reqAnd.Operands.Count);
        Assert.Equal("GateBit", Assert.IsType<Expr.TagRef>(reqAnd.Operands[0]).Path);
        Assert.Equal("TriggerBit", Assert.IsType<Expr.TagRef>(reqAnd.Operands[1]).Path);

        Assert.Equal("StationAddr", Assert.IsType<Expr.TagRef>(statement.MbAddr).Path);
        Assert.Equal("RequestMode", Assert.IsType<Expr.TagRef>(statement.Mode).Path);
        Assert.Equal("DataAddress", Assert.IsType<Expr.TagRef>(statement.DataAddr).Path);
        Assert.Equal("DataLength", Assert.IsType<Expr.TagRef>(statement.DataLen).Path);
        Assert.Equal("DataBuffer", Assert.IsType<Expr.TagRef>(statement.DataPtr).Path);

        Assert.Equal("Modbus_Master_DB", statement.InstancePath);
        Assert.Equal("RequestDone", statement.DoneTag);
        Assert.Equal("RequestBusy", statement.BusyTag);
        Assert.Equal("RequestError", statement.ErrorTag);
        Assert.Equal("RequestStatus", statement.StatusTag);
    }

    [Fact]
    public void Reduce_ModbusMasterFedByRail_SidecarRecordsVersionAndInstance()
    {
        var network = LoadFixture("ModbusMasterFedByRail.xml");

        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Modbus request", compileUnitUId: "62");

        var sidecar = Assert.Single(reduced.Sidecar.ModbusMasters);
        Assert.Equal("6.0", sidecar.Version);
        Assert.Equal("GlobalVariable", sidecar.InstanceScope);
        Assert.Equal(new[] { "Modbus_Master_DB" }, sidecar.InstanceComponentPath);
        Assert.Equal(2, sidecar.ReqSteps.Count);
    }

    [Fact]
    public void RoundTrip_ModbusMasterFedByRail_RebuildsIdenticalTopology()
    {
        var original = LoadFixture("ModbusMasterFedByRail.xml");
        var reduced = GraphReducer.Reduce(original, networkNumber: 1, title: "Modbus request", compileUnitUId: "62");

        var rebuilt = FlgNetBuilder.Build(reduced.Network, reduced.Sidecar);
        var xml = FlgNetWriter.Write(rebuilt);
        var reparsed = FlgNetParser.Parse(xml);

        Assert.Equal(original.Parts.Count, reparsed.Parts.Count);
        Assert.Equal(original.Wires.Count, reparsed.Wires.Count);

        var part = Assert.Single(reparsed.Parts, p => p.Name == "Modbus_Master");
        Assert.Equal("6.0", part.Version);

        var reqWire = Assert.Single(reparsed.Wires, w => w.UId == 40);
        Assert.Contains(reqWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 33 && e.PortName == "out");
        Assert.Contains(reqWire.Endpoints, e => e.Kind == EndpointKind.NameCon && e.UId == 34 && e.PortName == "REQ");
    }

    [Fact]
    public void SerializeNetworkOnly_ModbusMasterFedByRail_ProducesReadableSyntax()
    {
        var network = LoadFixture("ModbusMasterFedByRail.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Modbus request", compileUnitUId: "62");

        var text = IrSerializer.SerializeNetworkOnly(reduced.Network);

        Assert.Contains(
            "  MODBUS_MASTER(Modbus_Master_DB, EN := TRUE, REQ := GateBit AND TriggerBit, MB_ADDR := StationAddr, " +
            "MODE := RequestMode, DATA_ADDR := DataAddress, DATA_LEN := DataLength, DATA_PTR := DataBuffer, " +
            "DONE => RequestDone, BUSY => RequestBusy, ERROR => RequestError, STATUS => RequestStatus)\n", text);
    }

    [Fact]
    public void FullBlock_ModbusMasterFedByRail_ParseThenSerialize_IsByteIdentical()
    {
        var network = LoadFixture("ModbusMasterFedByRail.xml");
        var reduced = GraphReducer.Reduce(network, networkNumber: 1, title: "Modbus request", compileUnitUId: "62");

        var block = new IrBlock("0", "FC", "TestBlock", 1, "LAD", "A test block", new[] { reduced.Network });
        var text = IrSerializer.SerializeBlock(block, new[] { reduced.Sidecar });

        var (parsedBlock, parsedSidecars) = IrParser.ParseBlock(text);
        var reserialized = IrSerializer.SerializeBlock(parsedBlock, parsedSidecars);

        Assert.Equal(text, reserialized);
    }

    [Fact]
    public void Parse_ModbusMasterWithDisabledEno_ThrowsUnsupportedConstruct()
    {
        var xml = """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Part Name="Modbus_Master" Version="6.0" UId="1" DisabledENO="true">
                  <Instance Scope="GlobalVariable" UId="2">
                    <Component Name="Modbus_Master_DB" />
                  </Instance>
                </Part>
                <Part Name="Coil" UId="3" />
              </Parts>
              <Wires />
            </FlgNet>
            """;

        var element = XElement.Parse(xml);

        var ex = Assert.Throws<UnsupportedConstructException>(() => FlgNetParser.Parse(element));
        Assert.Contains("DisabledENO", ex.Message);
    }

    [Fact]
    public void Parse_ModbusMasterMissingInstance_ThrowsSimaticMlFormatException()
    {
        var xml = """
            <FlgNet xmlns="http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v5">
              <Parts>
                <Part Name="Modbus_Master" Version="6.0" UId="1" />
                <Part Name="Coil" UId="2" />
              </Parts>
              <Wires />
            </FlgNet>
            """;

        var element = XElement.Parse(xml);

        var ex = Assert.Throws<SimaticMlFormatException>(() => FlgNetParser.Parse(element));
        Assert.Contains("Instance", ex.Message);
    }
}
