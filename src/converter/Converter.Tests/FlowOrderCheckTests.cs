using Converter.Preflight;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// FI-27 (2026-07-20): the `preflight` flow-order check. Validates that a synthesized network's
/// instruction <c>&lt;Part&gt;</c>/<c>&lt;Call&gt;</c> elements are emitted in TIA's required DFS-from-rail
/// wire-graph order — the class the Normalizer masks from every equivalence oracle. Uses the MotorVSDSystem
/// NW3 shape (TON whose .Q feeds an RCoil, plus an independent coil) from FlgNetWriterPartOrderTests:
/// raw UId order 30,52,56 is TIA-rejected; flow order is 30,56,52.
/// </summary>
public class FlowOrderCheckTests
{
    // TON(30) -> its .Q feeds RCoil(56); Coil(52) is an independent rail-fed rung.
    private static FlgNetwork MotorVsdShape() => new(
        Array.Empty<AccessNode>(),
        new[] { new PartNode(30, "Ton"), new PartNode(56, "RCoil"), new PartNode(52, "Coil") },
        new[]
        {
            new WireNode(1, new[]
            {
                new WireEndpoint(EndpointKind.Powerrail, null, null),
                new WireEndpoint(EndpointKind.NameCon, 30, "in"),
            }),
            new WireNode(2, new[]
            {
                new WireEndpoint(EndpointKind.NameCon, 30, "Q"),
                new WireEndpoint(EndpointKind.NameCon, 56, "in"),
            }),
            new WireNode(3, new[]
            {
                new WireEndpoint(EndpointKind.Powerrail, null, null),
                new WireEndpoint(EndpointKind.NameCon, 52, "in"),
            }),
        });

    [Fact]
    public void FlowOrderedPartUIds_GroupsProducerWithConsumer_NotRawUIdOrder()
    {
        var order = FlgNetWriter.FlowOrderedPartUIds(MotorVsdShape());
        Assert.Equal(new[] { 30, 56, 52 }, order);
    }

    [Fact]
    public void ReadEmittedPartUIds_ReadsPartAndCallInDocumentOrder_ExcludingAccess()
    {
        var flgNet = FlgNetWriter.Write(MotorVsdShape());
        var emitted = FlowOrderCheck.ReadEmittedPartUIds(flgNet);
        Assert.Equal(new[] { 30, 56, 52 }, emitted);
    }

    [Fact]
    public void Validate_CorrectlyOrderedWriterOutput_NoFinding()
    {
        var network = MotorVsdShape();
        var emitted = FlowOrderCheck.ReadEmittedPartUIds(FlgNetWriter.Write(network));

        Assert.Null(FlowOrderCheck.Validate(3, network, emitted));
    }

    [Fact]
    public void Validate_ScrambledOrder_ProducesFlowOrderFinding()
    {
        var network = MotorVsdShape();
        // Raw UId order — topologically valid but the exact order TIA rejected (56 separated from its
        // producer 30 by the independent coil 52). The check must catch it.
        var finding = FlowOrderCheck.Validate(3, network, new[] { 30, 52, 56 });

        Assert.NotNull(finding);
        Assert.Equal("flow-order", finding!.Check);
        Assert.Contains("must be sorted according to the current flow", finding.Description);
    }
}
