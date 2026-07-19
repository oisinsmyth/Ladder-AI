using System;
using System.Collections.Generic;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// Comparison SrcType from the tag registry (2026-07-19). A tag-vs-tag comparison has no literal to infer a
/// type from, so it used to default `SrcType` to `Int` — which overflows at import for a wide comparison. It
/// now resolves the operands' declared types from the registry, defaulting to Int only when neither resolves.
/// </summary>
public class ComparisonTagTypeSynthesisTests
{
    private const string Network = "NETWORK 1 \"C\"\n  COIL Out := HrsA >= HrsB\n";

    private static ChainStepSidecar.CompareStep SynthesizeCompare(TagTypeRegistry? tagTypes)
    {
        var network = IrParser.ParseNetworkOnly(Network);
        var sidecar = SidecarSynthesizer.Synthesize(
            network, new HashSet<string>(StringComparer.Ordinal), callees: null, tagTypes: tagTypes);
        return Assert.IsType<ChainStepSidecar.CompareStep>(Assert.Single(sidecar.Assignments[0].Steps));
    }

    [Fact]
    public void WideTagVsTagComparison_TypesFromTheRegistry()
    {
        var tagTypes = TagTypeRegistry.FromSources(
            Array.Empty<DbSource>(), Array.Empty<PlcTypeSource>(),
            new[]
            {
                new PlcTagSource("1", "HrsA", "UDInt", "%MD0", true, true, true, null),
                new PlcTagSource("2", "HrsB", "UDInt", "%MD4", true, true, true, null),
            });

        Assert.Equal("UDInt", SynthesizeCompare(tagTypes).SrcType);
    }

    [Fact]
    public void TagVsTagComparison_NoResolvableType_FallsBackToInt()
    {
        // No registry → neither operand resolves → the historical Int default holds (unchanged behaviour).
        Assert.Equal("Int", SynthesizeCompare(null).SrcType);
    }
}
