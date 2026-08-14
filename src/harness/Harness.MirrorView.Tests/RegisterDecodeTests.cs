namespace Harness.MirrorView.Tests;

/// <summary>
/// Decoding, against values that are real.
///
/// <para>The build stamp <c>16#F52ECEAD</c> is the one deployed to this rig, and its two halves are
/// DISTINGUISHABLE — which is the whole reason it can settle word order. A fixture of
/// <c>16#AAAAAAAA</c> would agree with both orders and prove nothing.</para>
/// </summary>
public class RegisterDecodeTests
{
    private static MirrorTag Tag(string type, MirrorWidth width, int bit = 0) =>
        new("T", type, "%MW1000", 0, width, bit, "", 1);

    // ---- 32-bit, high word first ------------------------------------------------------------------

    [Fact]
    public void TheDeployedBuildStamp_DecodesHighWordFirst()
    {
        var decoded = RegisterDecode.Decode(Tag("DWord", MirrorWidth.DoubleWord), new ushort[] { 0xF52E, 0xCEAD });

        Assert.NotNull(decoded);
        Assert.Equal("16#F52ECEAD", decoded!.Text);
    }

    /// <summary>
    /// The negative half of the same measurement, stated separately because it is the half that could
    /// fail silently: under the OTHER order the same registers read <c>16#CEADF52E</c>, and a decoder
    /// that had drifted would produce exactly that while still looking like a hex number.
    /// </summary>
    [Fact]
    public void TheDeployedBuildStamp_IsNotTheHalvesSwappedValue()
    {
        var decoded = RegisterDecode.Decode(Tag("DWord", MirrorWidth.DoubleWord), new ushort[] { 0xF52E, 0xCEAD });

        Assert.NotEqual("16#CEADF52E", decoded!.Text);
    }

    [Fact]
    public void ADWordDecode_NamesTheWordOrderItUsed()
    {
        var decoded = RegisterDecode.Decode(Tag("DWord", MirrorWidth.DoubleWord), new ushort[] { 0xF52E, 0xCEAD });

        Assert.Contains("HIGH-WORD-FIRST", decoded!.Basis, StringComparison.Ordinal);
        Assert.Contains("16#F52E", decoded.Basis, StringComparison.Ordinal);
        Assert.Contains("16#CEAD", decoded.Basis, StringComparison.Ordinal);
    }

    // ---- Time -------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0x0000, 0x01F4, "T#500ms")]
    [InlineData(0x0000, 0x03E8, "T#1s")]
    [InlineData(0x0001, 0x5F90, "T#1m30s")]
    [InlineData(0x0000, 0x0000, "T#0ms")]
    public void ATimeSpansTwoRegisters_AndInheritsTheWordOrder(ushort high, ushort low, string expected)
    {
        var decoded = RegisterDecode.Decode(Tag("Time", MirrorWidth.DoubleWord), new[] { high, low });

        Assert.Equal(expected, decoded!.Text);
    }

    [Fact]
    public void ATimeCarriesItsMillisecondCount_SoTheFormattingIsNeverTheOnlyEvidence()
    {
        var decoded = RegisterDecode.Decode(Tag("Time", MirrorWidth.DoubleWord), new ushort[] { 0x0001, 0x5F90 });

        Assert.Contains("90000", decoded!.Basis, StringComparison.Ordinal);
    }

    [Fact]
    public void ANegativeTime_KeepsItsSign_RatherThanBeingClamped() =>
        Assert.Equal("T#-1s", RegisterDecode.FormatIecTime(-1000));

    /// <summary>int.MinValue has no positive counterpart; the decomposition must not overflow on it.</summary>
    [Fact]
    public void TheMostNegativeTime_DoesNotOverflowTheDecomposition() =>
        Assert.StartsWith("T#-24d", RegisterDecode.FormatIecTime(int.MinValue), StringComparison.Ordinal);

    // ---- Bool -------------------------------------------------------------------------------------

    [Theory]
    [InlineData((ushort)0x0000, 0, "FALSE")]
    [InlineData((ushort)0x0001, 0, "TRUE")]
    [InlineData((ushort)0xFFFE, 0, "FALSE")]
    [InlineData((ushort)0x0100, 8, "TRUE")]
    [InlineData((ushort)0x0001, 8, "FALSE")]
    public void ABoolIsOneBitOfItsRegister(ushort word, int bit, string expected)
    {
        var decoded = RegisterDecode.Decode(Tag("Bool", MirrorWidth.Bit, bit), new[] { word });

        Assert.Equal(expected, decoded!.Text);
    }

    /// <summary>
    /// The bit POSITION rests on an inference <c>Harness.Map.MirrorGeometry</c> itself marks <c>[I]</c>.
    /// The decode says so, and prints the whole raw word — a reader is never asked to take it on trust.
    /// </summary>
    [Fact]
    public void ABoolDecode_AdmitsThatItsBitPositionIsInferred()
    {
        var decoded = RegisterDecode.Decode(Tag("Bool", MirrorWidth.Bit), new ushort[] { 0x0001 });

        Assert.Contains("inferred", decoded!.Basis, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("16#0001", decoded.Basis, StringComparison.Ordinal);
    }

    // ---- Int, and the gap --------------------------------------------------------------------------

    [Theory]
    [InlineData((ushort)0x0000, "0")]
    [InlineData((ushort)0x0064, "100")]
    [InlineData((ushort)0xFFFF, "-1")]
    [InlineData((ushort)0x8000, "-32768")]
    public void AnIntIsSigned16Bit(ushort word, string expected) =>
        Assert.Equal(expected, RegisterDecode.Decode(Tag("Int", MirrorWidth.Word), new[] { word })!.Text);

    /// <summary>
    /// *** A TYPE WITH NO DECODE PRODUCES NO VALUE, NOT A PLAUSIBLE ONE. *** The raw words still show;
    /// what must never happen is a number appearing under an encoding this tool does not know.
    /// </summary>
    [Fact]
    public void AnUnknownType_HasNoDecodeAtAll() =>
        Assert.Null(RegisterDecode.Decode(Tag("LReal", MirrorWidth.Word), new ushort[] { 0x1234 }));

    /// <summary>A slice that is not the element's width is not decoded — it would be decoding the wrong bytes.</summary>
    [Fact]
    public void AWrongWidthSlice_IsNotDecoded() =>
        Assert.Null(RegisterDecode.Decode(Tag("DWord", MirrorWidth.DoubleWord), new ushort[] { 0xF52E }));
}
