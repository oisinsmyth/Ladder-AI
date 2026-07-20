using System;
using System.Linq;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// TIA rejects a network's <c>&lt;Parts&gt;</c> on import unless its <c>&lt;Access&gt;</c> elements are
/// UId-ascending ("the elements must be sorted according to the current flow"). Tag-accesses and
/// constants form ONE Access group; the synthesizer can mint a constant's UId interleaved among the
/// tag-access UIds (a TON PT or comparison literal), so <see cref="FlgNetWriter"/> must merge-sort them
/// rather than emit tag-accesses then constants. Surfaced 2026-07-20 on <c>MotorStarter</c> NW3 (a
/// TypedConstant UId 31 emitted after tag-accesses 37..57). The <c>Normalizer</c> sorts parts before
/// comparing, so it masks this — hence a raw-order regression guard here, not a Normalizer-based one.
/// </summary>
public class FlgNetWriterPartOrderTests
{
    [Fact]
    public void AccessGroup_IsUIdAscending_WhenAConstantInterleavesTagAccessUIds()
    {
        // The MotorStarter NW3 shape: tag-accesses 27 and 37 with a TypedConstant UId 31 between them.
        // Pre-fix, FlgNetWriter emitted the whole AccessNodes group then the Constants group -> 27, 37, 31.
        var network = new FlgNetwork(
            new[]
            {
                new AccessNode(27, "LocalVariable", new[] { "A" }),
                new AccessNode(37, "LocalVariable", new[] { "B" }),
            },
            Array.Empty<PartNode>(),
            Array.Empty<WireNode>(),
            new[] { new ConstantAccessNode(31, "T#5S") });

        var flgNet = FlgNetWriter.Write(network);

        var accessUIds = flgNet.Elements().First(e => e.Name.LocalName == "Parts")
            .Elements().Where(e => e.Name.LocalName == "Access")
            .Select(e => int.Parse(e.Attribute("UId")!.Value))
            .ToList();

        Assert.Equal(new[] { 27, 31, 37 }, accessUIds);
    }

    [Fact]
    public void InstructionParts_EmittedInWireFlowOrder_GroupingAConsumerWithItsProducer()
    {
        // The MotorStarter/MotorVSDSystem NW3 shape (Gap I): a TON (UId 30) whose `.Q` feeds a reset-coil
        // (UId 56), plus an INDEPENDENT coil (UId 52). Raw UId order is 30, 52, 56 — which separates the
        // TON from the consumer it feeds by the independent rung, and TIA import rejects that ("must be
        // sorted according to the current flow"). Flow order (DFS from the rail) groups the TON with its
        // downstream consumer before the independent rung: 30, 56, 52. The Normalizer sorts parts before
        // comparing, so only a raw-order assertion like this catches a regression.
        var network = new FlgNetwork(
            Array.Empty<AccessNode>(),
            new[]
            {
                new PartNode(30, "Ton"),
                new PartNode(56, "RCoil"),
                new PartNode(52, "Coil"),
            },
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

        var flgNet = FlgNetWriter.Write(network);

        var partUIds = flgNet.Elements().First(e => e.Name.LocalName == "Parts")
            .Elements().Where(e => e.Name.LocalName == "Part")
            .Select(e => int.Parse(e.Attribute("UId")!.Value))
            .ToList();

        Assert.Equal(new[] { 30, 56, 52 }, partUIds);
    }
}
