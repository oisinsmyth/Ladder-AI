using Harness.Map;

namespace Harness.Map.Tests;

/// <summary>
/// 🔴 <b>THE COMMS FB'S REFUSALS. The block is mechanism end to end EXCEPT for three numbers that
/// identify one server on one CPU, and for its prose — and that is the whole of what it will not
/// originate.</b>
///
/// <para>Every name here is invented. The mirror is a widget rig's.</para>
/// </summary>
public class CommsFbGeneratorTests
{
    private static MirrorGeometry Geometry() => MirrorGeometry.ForCpu1214C(16, baseByte: 2400, declaredRegisters: 41);

    private static CommsFbNaming Naming() => new(
        "FB_Comms_WidgetMirrorServer", 9411, "Widget Mirror Server",
        "Serves the widget rig's mirror to a Modbus TCP client. Synthetic test material.");

    private static CommsNetworkText Network() =>
        new("Serve The Widget Mirror", "Runs the server every scan. Synthetic test material.");

    private static CommsFbDeclaration Declaration() =>
        new(Naming(), new CommsEndpoint(1502, "16#0021", 64), Network());

    /// <summary>
    /// 🔴 <b>THE CENTRAL REFUSAL, and the measured hazard behind it is recorded from the other side in
    /// <see cref="InstanceDbGenerator"/>: a listening port and a connection ID each IDENTIFY the instance,
    /// and a default hands the second server the first one's.</b> The failure is not a compile error.
    /// </summary>
    [Fact]
    public void NO_ENDPOINT_IS_A_REFUSAL_NAMING_ALL_THREE_NUMBERS()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            CommsFbGenerator.Generate(Declaration() with { Endpoint = null }, Geometry()));

        Assert.Contains("listening port", error.Message, StringComparison.Ordinal);
        Assert.Contains("connection ID", error.Message, StringComparison.Ordinal);
        Assert.Contains("hardware interface ID", error.Message, StringComparison.Ordinal);
        Assert.Contains("NONE of the three is defaulted", error.Message, StringComparison.Ordinal);
        Assert.Contains("not a compile error", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, "16#0021", 64)]
    [InlineData(70000, "16#0021", 64)]
    [InlineData(1502, "not-a-literal", 64)]
    [InlineData(1502, "16#0021", 0)]
    public void AN_UNUSABLE_ENDPOINT_VALUE_IS_REFUSED(int port, string connectionId, int interfaceId)
    {
        Assert.Throws<ArgumentException>(() =>
            CommsFbGenerator.Generate(
                Declaration() with { Endpoint = new CommsEndpoint(port, connectionId, interfaceId) }, Geometry()));
    }

    /// <summary>
    /// 🔴 <b>PROSE THAT STATES A WIDTH THE BLOCK DOES NOT SERVE IS REFUSED, AND THE NUMBER IS NOT
    /// CORRECTED.</b> The window has four homes in this block and <c>converter served-area</c> reconciles
    /// only two of them; which of the two the author meant is not something to assume.
    /// </summary>
    [Theory]
    [InlineData("Serves 37 holding registers of the widget mirror.")]
    [InlineData("Exposes WORD 37 from the mirror base.")]
    [InlineData("Covers 37 words of the mirror.")]
    [InlineData("Serves the area starting at M1000.0.")]
    public void A_COMMENT_THAT_DISAGREES_WITH_THE_SERVED_WINDOW_IS_REFUSED(string comment)
    {
        var error = Assert.Throws<ArgumentException>(() =>
            CommsFbGenerator.Generate(Declaration() with { Naming = Naming() with { Comment = comment } }, Geometry()));

        Assert.Contains("FOUR homes", error.Message, StringComparison.Ordinal);
        Assert.Contains(CommsFbGenerator.RegistersPlaceholder, error.Message, StringComparison.Ordinal);
    }

    /// <summary>A comment stating the truth is left alone; the check is on disagreement, not on mentioning.</summary>
    [Fact]
    public void A_COMMENT_THAT_AGREES_WITH_THE_SERVED_WINDOW_PASSES_THROUGH_UNCHANGED()
    {
        var comment = "Serves 41 holding registers from M2400.0 — the whole of the widget mirror.";
        var generated = CommsFbGenerator.Generate(
            Declaration() with { Naming = Naming() with { Comment = comment } }, Geometry());

        Assert.Contains(comment, generated.Ir, StringComparison.Ordinal);
    }

    /// <summary>
    /// The geometry's own refusals are passed through verbatim rather than restated. A second copy of
    /// "the base must be word-aligned" here would be a second rule free to disagree with the one the
    /// allocator actually runs.
    /// </summary>
    [Fact]
    public void A_GEOMETRY_ITS_OWN_TYPE_REFUSES_IS_REFUSED_HERE_IN_ITS_OWN_WORDS()
    {
        var odd = MirrorGeometry.ForCpu1214C(16, baseByte: 2401, declaredRegisters: 41);
        Assert.NotEmpty(odd.Refusals);

        var error = Assert.Throws<ArgumentException>(() => CommsFbGenerator.Generate(Declaration(), odd));

        Assert.Contains("refused by MirrorGeometry itself", error.Message, StringComparison.Ordinal);
        Assert.Contains(odd.Refusals[0], error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EMPTY_PROSE_AND_A_MISSING_BLOCK_NUMBER_ARE_REFUSED()
    {
        Assert.Throws<ArgumentException>(() =>
            CommsFbGenerator.Generate(Declaration() with { Naming = Naming() with { Comment = "  " } }, Geometry()));

        Assert.Throws<ArgumentException>(() =>
            CommsFbGenerator.Generate(Declaration() with { Naming = Naming() with { Title = "" } }, Geometry()));

        var error = Assert.Throws<ArgumentException>(() =>
            CommsFbGenerator.Generate(Declaration() with { Naming = Naming() with { BlockNumber = 0 } }, Geometry()));

        Assert.Contains("--floor 9000", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// MEMORYLAYOUT absent means NOT DECLARED and is reported on a positive line — not a claim that the
    /// block has no layout.
    /// </summary>
    [Fact]
    public void AN_UNDECLARED_MEMORY_LAYOUT_IS_OMITTED_AND_REPORTED()
    {
        var generated = CommsFbGenerator.Generate(Declaration(), Geometry());

        Assert.DoesNotContain("MEMORYLAYOUT", generated.Ir, StringComparison.Ordinal);
        Assert.Contains(generated.Obligations, o => o.Contains("NO MEMORYLAYOUT WAS DECLARED", StringComparison.Ordinal));

        var declared = CommsFbGenerator.Generate(Declaration() with { MemoryLayout = "Standard" }, Geometry());

        Assert.Contains("MEMORYLAYOUT Standard\n", declared.Ir, StringComparison.Ordinal);
        Assert.Contains(declared.Obligations, o => o.Contains("MEMORYLAYOUT WAS DECLARED", StringComparison.Ordinal));
    }

    /// <summary>The obligations name the instance-DB shape and the call-site requirement, both measured.</summary>
    [Fact]
    public void THE_OBLIGATIONS_NAME_WHAT_THE_GENERATOR_CANNOT_CHECK()
    {
        var generated = CommsFbGenerator.Generate(Declaration(), Geometry());

        Assert.Contains(generated.Obligations, o => o.Contains("NOTHING CHECKED THAT AGAINST THE CONTROLLER", StringComparison.Ordinal));
        Assert.Contains(generated.Obligations, o => o.Contains("LeftToTia", StringComparison.Ordinal));
        Assert.Contains(generated.Obligations, o => o.Contains("MUST BE CALLED", StringComparison.Ordinal));
    }
}
