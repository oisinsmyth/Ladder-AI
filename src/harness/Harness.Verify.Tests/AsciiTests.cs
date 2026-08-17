namespace Harness.Verify.Tests;

/// <summary>
/// <see cref="Ascii"/> — the boundary that stops another component's prose mangling this tool's report.
///
/// <para><b>Why this needed to exist at all, measured.</b> Holding this tool's OWN literals to ASCII was
/// not enough: <c>VersionCheck</c>'s <c>Stale</c> detail, <c>MirrorClient</c>'s refusal and
/// <c>GuardDecision</c>'s message all carry em dashes, and all three are quoted verbatim into the report.
/// The first pass fixed every literal in this assembly and the live output was still mangled — in the one
/// paragraph a reader most needs, the version check's own explanation of what it found.</para>
/// </summary>
public class AsciiTests
{
    [Theory]
    [InlineData("plain ascii stays exactly as it is", "plain ascii stays exactly as it is")]
    [InlineData("", "")]
    [InlineData("an excision changes the stamp too — a stale-looking register",
                "an excision changes the stamp too - a stale-looking register")]
    [InlineData("§9's version register", "section 9's version register")]
    [InlineData("don’t and “quoted”", "don't and \"quoted\"")]
    [InlineData("a → b", "a -> b")]
    [InlineData("and so on…", "and so on...")]
    public void Known_characters_are_mapped_to_their_obvious_equivalent(string input, string expected) =>
        Assert.Equal(expected, Ascii.Of(input));

    /// <summary>
    /// <b>An unmapped character is shown, not silently substituted.</b> A question mark would turn "this
    /// text had something here" into "this text is like that" — the same silent degradation the class
    /// exists to stop, one level down.
    /// </summary>
    [Fact]
    public void An_unknown_character_is_printed_as_its_code_point()
    {
        Assert.Equal("a \\u4E2D b", Ascii.Of("a 中 b"));

        // Outside the BMP the conversion is per-CHAR, so a surrogate pair prints as its two halves.
        // Stated rather than glossed: it is still visible and still reversible, which is the property
        // that matters, and claiming a code-point conversion this implementation does not make would
        // be a claim in a test file that the code does not honour.
        Assert.Equal("\\uD83D\\uDD34", Ascii.Of("\U0001F534"));
    }

    /// <summary>Null is an empty string, never the word "null" — which is a value a reader would try to interpret.</summary>
    [Fact]
    public void Null_becomes_empty_rather_than_the_word()
    {
        Assert.Equal(string.Empty, Ascii.Of(null));
    }

    /// <summary>
    /// <b>The converse control.</b> A converter that mangled ordinary text would satisfy every assertion
    /// above that only checks the interesting characters — so an unremarkable string must come back
    /// byte-identical, including the punctuation this class is easily over-eager about.
    /// </summary>
    [Fact]
    public void Ordinary_report_text_is_returned_unchanged()
    {
        const string text = "the version register reads 16#F52ECEAD and this client's map was derived for "
                            + "16#33434A68. A download landed (DB-6); every address it holds is now a guess. 0..36 [x] {y} \\z\\";

        Assert.Same(text, Ascii.Of(text));
    }

    /// <summary>Everything it returns is ASCII, over an input that mixes mapped, unmapped and plain characters.</summary>
    [Fact]
    public void The_output_is_always_ascii()
    {
        var mixed = "a—b§c中d’e f";

        Assert.All(Ascii.Of(mixed), c => Assert.True(c <= '\u007F', $"U+{(int)c:X4} survived the conversion."));
    }
}
