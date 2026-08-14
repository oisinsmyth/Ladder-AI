namespace Harness.MirrorView.Tests;

/// <summary>
/// A small map, built the way the real one is — <b>through the parser, from text</b>, never by
/// constructing <c>MirrorTag</c>s by hand.
///
/// <para>That is deliberate: a fixture that bypassed the parser would let the view tests pass against a
/// map shape the parser can no longer produce, and the two halves would drift apart while both stayed
/// green. Six registers, carrying one of each thing the page has to render.</para>
/// </summary>
internal static class Fixtures
{
    public const int Registers = 6;

    private const string TagText = """
TAGTABLE Small
  ROOTID 0
  TAGS
    HX_ProgramVersion 1 : DWord @ %MD1000 ACCESSIBLE VISIBLE WRITABLE COMMENT "Build stamp of the downloaded IR set."
    HX_ScanCount 4 : DInt @ %MD1004 ACCESSIBLE VISIBLE WRITABLE COMMENT "Free-running scan counter."
    HX_V000 7 : Int @ %MW1008 ACCESSIBLE VISIBLE WRITABLE COMMENT "Vector register 0."
    HX_R000 A : Bool @ %M1011.0 ACCESSIBLE VISIBLE WRITABLE COMMENT "Result register 0."
""";

    private const string AreaText =
        "  MB_SERVER(MbServer, EN := TRUE, MB_HOLD_REG := P#M1000.0 WORD 6, CONNECT := Comms)";

    public static MirrorMap Map()
    {
        var load = MirrorMapParser.Parse(TagText, AreaText, "small.ir", "small-area.ir");
        Assert.True(load.Ok, string.Join(" | ", load.Refusals));
        return load.Map!;
    }

    public static MirrorViewOptions Options(int pollMs = 1000, int staleMs = 3000, string address = "10.10.10.10") =>
        new(address, 503, 1, "allowlist.json", Registers, pollMs, staleMs, 8137);

    public static readonly DateTimeOffset T0 = new(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);
}
