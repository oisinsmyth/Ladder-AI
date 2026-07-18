using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// DataHandling's box family (2026-07-18): WAND/CALC/T_SUB/T_CONV/MOVE_BLK_VARIANT synthesis.
/// End-to-end parity is proven by the harness (DataHandling flips green → 14/14); these check the
/// pieces the plan called out as non-trivial.
/// </summary>
public class DataHandlingSynthesisTests
{
    private static TagTypeRegistry Types(params (string Name, string Type)[] tags) =>
        TagTypeRegistry.FromSources(
            System.Array.Empty<DbSource>(), System.Array.Empty<PlcTypeSource>(),
            System.Linq.Enumerable.Select(tags, t => new PlcTagSource("1", t.Name, t.Type, "%M0", true, true, true, null)));

    private static NetworkSidecar Synthesize(string network, TagTypeRegistry types) =>
        SidecarSynthesizer.Synthesize(
            IrParser.ParseNetworkOnly(network), new HashSet<string>(System.StringComparer.Ordinal), callees: null, tagTypes: types);

    [Fact]
    public void Wand_SrcTypeFromTag_LiteralMaskTakesThatType()
    {
        var sidecar = Synthesize(
            "NETWORK 1 \"W\"\n  WAND(EN := TRUE, IN1 := StatusWord, IN2 := 16#89) => CommandWord\n",
            Types(("StatusWord", "Word"), ("CommandWord", "Word")));

        Assert.Equal("Word", Assert.Single(sidecar.WordAnds).SrcType);
        // The `16#89` mask is a Word, not the Int its digits suggest.
        Assert.Equal("Word", Assert.Single(sidecar.ConstantUIds).ConstantType);
    }

    [Fact]
    public void Calc_CarriesEquationAndSrcType()
    {
        var sidecar = Synthesize(
            "NETWORK 1 \"C\"\n  CALC(EN := TRUE, IN1 := Speed, IN2 := Ratio) => Out \"IN1*IN2\"\n",
            Types(("Speed", "Real"), ("Ratio", "Real"), ("Out", "Real")));

        var calc = Assert.Single(sidecar.Calcs);
        Assert.Equal("IN1*IN2", calc.Equation);
        Assert.Equal("Real", calc.SrcType);
    }

    [Fact]
    public void TSubThenTConv_ChainsEnoAndCarriesVersionAndTypes()
    {
        var sidecar = Synthesize(
            "NETWORK 1 \"T\"\n" +
            "  T_SUB(EN := TRUE, IN1 := StartTime, IN2 := EndTime) => Elapsed\n" +
            "  T_CONV(EN := ENO, IN := StartTime) => Millis\n",
            Types(("StartTime", "Time"), ("EndTime", "Time"), ("Elapsed", "Time"), ("Millis", "UDInt")));

        var tsub = Assert.Single(sidecar.TSubs);
        var tconv = Assert.Single(sidecar.TConvs);
        Assert.Equal("1.2", tsub.Version);
        Assert.Equal("Time", tsub.DateType);
        Assert.Equal("1.2", tconv.Version);
        Assert.Equal("Time", tconv.SrcType);
        Assert.Equal("UDInt", tconv.DestType);

        var eno = Assert.IsType<EnSourceSidecar.PrecedingEnoSidecar>(tconv.En);
        Assert.Equal(tsub.TSubPartUId, eno.PrecedingPartUId);
    }

    [Fact]
    public void MoveBlkVariant_HasVersionAndTwoDistinctOutputs()
    {
        var sidecar = Synthesize(
            "NETWORK 1 \"M\"\n  MOVE_BLK_VARIANT(EN := TRUE, SRC := Src, COUNT := Cnt, SRC_INDEX := Si, DEST_INDEX := Di, Ret_Val => Rv, DEST => Dst)\n",
            Types());

        var mbv = Assert.Single(sidecar.MoveBlkVariants);
        Assert.Equal("1.2", mbv.Version);
        Assert.NotEqual(mbv.RetValAccessUId, mbv.DestAccessUId);
    }
}
