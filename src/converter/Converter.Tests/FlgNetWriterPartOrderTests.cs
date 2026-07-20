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
}
