using Harness.Map;
using Xunit;

namespace Harness.Map.Tests;

/// <summary>
/// The symbolic encoder, and the two-tag form it makes possible.
///
/// <para><b>Every table in here is INVENTED FOR THE TEST</b>, deliberately: this class is an
/// interpreter and knows nothing about hoppers. A test written against the deliverable's own decade
/// table would be evidence that one table works, and the property under test is that the TABLE is data.</para>
/// </summary>
public class ValueEncodingTests
{
    private static ValueEncoding Encoding(
        NumericFallback whenNumeric = NumericFallback.Refuse,
        long? literal = null,
        string? source = "md:100-110",
        (string Cited, long Value)[]? values = null) =>
        new((values ?? Array.Empty<(string Cited, long Value)>()).ToDictionary(v => v.Cited, v => v.Value, StringComparer.Ordinal),
            whenNumeric, literal, source);

    [Fact]
    public void NamedValue_EncodesToTheStatedInteger()
    {
        var result = Encoding(values: new[] { ("ALPHA", 10L) }).Encode("Sig", "ALPHA");

        Assert.True(result.Encoded);
        Assert.Equal(10, result.Value);

        // The derivation carries the CITATION, on the success path. A transcription that can only say
        // where it came from when it fails is one nobody can audit when it succeeds.
        Assert.Contains("md:100-110", result.Derivation);
    }

    [Fact]
    public void WhitespaceIsTrimmedBeforeTheTableIsConsulted()
    {
        Assert.True(Encoding(values: new[] { ("ALPHA", 10L) }).Encode("Sig", "  ALPHA  ").Encoded);
    }

    [Fact]
    public void MatchingIsOrdinal_NotCaseInsensitive()
    {
        // A decade table that also answers `alpha` is one that will one day answer something nobody
        // wrote. Refusing is the whole contract: an unnamed value gets no code.
        var result = Encoding(values: new[] { ("ALPHA", 10L) }).Encode("Sig", "alpha");

        Assert.False(result.Encoded);
        Assert.Contains("does not name it", result.Refusal);
    }

    [Fact]
    public void UnnamedValue_IsRefusedByName_AndTheRefusalListsWhatIsAdmissible()
    {
        var result = Encoding(values: new[] { ("ALPHA", 10L), ("BETA", 20L) }).Encode("Sig", "GAMMA");

        Assert.False(result.Encoded);
        Assert.Contains("'Sig'", result.Refusal);
        Assert.Contains("'GAMMA'", result.Refusal);
        Assert.Contains("'ALPHA'", result.Refusal);
        Assert.Contains("'BETA'", result.Refusal);

        // *** NEVER AN INVENTED CODE. *** The value must not have been silently encoded as anything.
        Assert.Equal(0, result.Value);
    }

    [Fact]
    public void Passthrough_WritesTheNumberItself()
    {
        var result = Encoding(NumericFallback.Passthrough).Encode("Sig", "90000");

        Assert.True(result.Encoded);
        Assert.Equal(90000, result.Value);
    }

    [Fact]
    public void Literal_DiscardsTheNumberAndWritesTheFixedValue()
    {
        // md:128's shape: `a number, e.g. 90000` classifies as mode 1, and the NUMBER is carried by the
        // other member of the two-tag pair.
        var result = Encoding(NumericFallback.Literal, literal: 1).Encode("Sig", "90000");

        Assert.True(result.Encoded);
        Assert.Equal(1, result.Value);
    }

    [Fact]
    public void TheTableWinsOverTheNumberTest()
    {
        // *** LOAD-BEARING ORDERING. *** `-1` is a SENTINEL meaning "never", not a duration, and it also
        // parses as an integer. Test the number first and the sentinel is silently written as -1.
        var result = Encoding(NumericFallback.Passthrough, values: new[] { ("-1", 0L) }).Encode("Sig", "-1");

        Assert.True(result.Encoded);
        Assert.Equal(0, result.Value);
    }

    [Fact]
    public void NumberUnderRefuse_IsRefused()
    {
        var result = Encoding(values: new[] { ("ALPHA", 10L) }).Encode("Sig", "10");

        Assert.False(result.Encoded);
        Assert.Contains("REFUSED by this encoding too", result.Refusal);
    }

    [Fact]
    public void EmptyValue_IsRefused_AndIsNotZero()
    {
        var result = Encoding(NumericFallback.Passthrough).Encode("Sig", "   ");

        Assert.False(result.Encoded);
        Assert.Contains("An absent value is not a zero one", result.Refusal);
    }

    // ---- THE ENCODING ITSELF, CHECKED BEFORE ANY VALUE GOES THROUGH IT ---------------------------

    [Fact]
    public void NoSource_IsARefusal_NotAMissingComment()
    {
        // The citation is the difference between a transcription and a number somebody invented, so it
        // is a required field rather than a convention. Held as a convention it is one somebody drops.
        var result = Encoding(source: null, values: new[] { ("ALPHA", 10L) }).Encode("Sig", "ALPHA");

        Assert.False(result.Encoded);
        Assert.Contains("states no `source`", result.Refusal);
    }

    [Fact]
    public void LiteralWithoutItsValue_IsARefusal()
    {
        var result = Encoding(NumericFallback.Literal, literal: null, values: new[] { ("ALPHA", 10L) }).Encode("Sig", "ALPHA");

        Assert.False(result.Encoded);
        Assert.Contains("no `numericLiteral` is stated", result.Refusal);
    }

    [Fact]
    public void NumericLiteralUnderAModeThatNeverReadsIt_IsARefusal()
    {
        // A field read in one mode and ignored in another reads as ACCEPTED in both — which is the
        // dropped-field defect this repo keeps finding, one level down.
        var result = Encoding(NumericFallback.Passthrough, literal: 7).Encode("Sig", "5");

        Assert.False(result.Encoded);
        Assert.Contains("never reads it", result.Refusal);
    }

    [Fact]
    public void AnEncodingThatAdmitsNothing_IsARefusal()
    {
        var result = Encoding().Encode("Sig", "ALPHA");

        Assert.False(result.Encoded);
        Assert.Contains("names no values and refuses every number", result.Refusal);
    }

    // ---- THE WIDTH RULE STILL APPLIES TO THE ENCODED VALUE ---------------------------------------

    [Fact]
    public void AnEncodedValueIsStillRangeChecked()
    {
        // *** AN ENCODING MUST NOT BE A WAY PAST THE WIDTH REFUSAL. *** The encoding decides WHICH
        // integer; the element decides whether that integer survives the register.
        var encoding = new ValueEncoding(
            new Dictionary<string, long>(StringComparer.Ordinal) { ["HUGE"] = 70000 },
            NumericFallback.Refuse, null, "md:1");

        var fit = MirrorValueFit.Check("Sig", MirrorValueType.Int, "HUGE", encoding);

        Assert.False(fit.Fits);
        Assert.Contains("does not fit", fit.Refusal);
    }

    [Fact]
    public void AnEncodedValueThatFits_Passes()
    {
        var encoding = new ValueEncoding(
            new Dictionary<string, long>(StringComparer.Ordinal) { ["OK"] = 30 },
            NumericFallback.Refuse, null, "md:1");

        var fit = MirrorValueFit.Check("Sig", MirrorValueType.Int, "OK", encoding);

        Assert.True(fit.Fits);
        Assert.Equal(30, fit.Value);
    }

    [Fact]
    public void WithoutAnEncoding_ASymbolicValueIsStillRefused()
    {
        // The unaffected case: a null encoding is the ABSENCE of one, not an identity rule nobody wrote.
        var fit = MirrorValueFit.Check("Sig", MirrorValueType.Int, "ALPHA", encoding: null);

        Assert.False(fit.Fits);
        Assert.Contains("is not an integer", fit.Refusal);
    }

    // ---- THE TWO-TAG FORM ------------------------------------------------------------------------

    [Fact]
    public void TwoTargetsMayShareOneSpecName_AndEachAppliesItsOwnRule()
    {
        var mode = new MirroredSignal("DB.ResetMode", MirrorValueType.Int, "Spec.ResetAtMs",
            Encoding: new ValueEncoding(
                new Dictionary<string, long>(StringComparer.Ordinal) { ["-1"] = 0, ["HOLD"] = 2 },
                NumericFallback.Literal, 1, "md:123-130"));

        var at = new MirroredSignal("DB.ResetAt", MirrorValueType.Time, "Spec.ResetAtMs",
            Encoding: new ValueEncoding(
                new Dictionary<string, long>(StringComparer.Ordinal) { ["-1"] = 0, ["HOLD"] = 0 },
                NumericFallback.Passthrough, null, "md:123-130"));

        var inputs = new Dictionary<string, string>(StringComparer.Ordinal) { ["Spec.ResetAtMs"] = "90000" };

        // Both consume the one cited value, under different rules, and neither is an orphan.
        Assert.Empty(MirrorValueFit.CheckAll(new[] { mode, at }, inputs));

        Assert.Equal(1, MirrorValueFit.Check(mode.JoinKey, mode.Type, "90000", mode.Encoding).Value);
        Assert.Equal(90000, MirrorValueFit.Check(at.JoinKey, at.Type, "90000", at.Encoding).Value);
    }

    [Fact]
    public void TheOrphanRefusalStillFires()
    {
        // *** KEEP IT WORKING. *** An input no bound target consumes leaves its register at zero, and
        // zero is a legal value for every element the mirror carries — so the block runs a scenario
        // nobody asked for and the result is reported as though the commanded one had been applied.
        var target = new MirroredSignal("DB.Profile", MirrorValueType.Int, "Spec.Profile");

        var inputs = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Spec.Profile"] = "1",
            ["Spec.Unclaimed"] = "5",
        };

        var refusals = MirrorValueFit.CheckAll(new[] { target }, inputs);

        Assert.Single(refusals);
        Assert.Contains("'Spec.Unclaimed'", refusals[0]);
        Assert.Contains("NO BOUND TARGET CONSUMES IT", refusals[0]);
    }

    [Fact]
    public void TheJoinKeyIsTheSpecName_AndTheTagIsNotAlsoTried()
    {
        var target = new MirroredSignal("DB.Profile", MirrorValueType.Int, "Spec.Profile");

        // Keyed by the TAG: not consumed, and reported as an orphan rather than matched by accident.
        var refusals = MirrorValueFit.CheckAll(
            new[] { target },
            new Dictionary<string, string>(StringComparer.Ordinal) { ["DB.Profile"] = "1" });

        Assert.Single(refusals);
        Assert.Contains("'DB.Profile'", refusals[0]);
    }

    [Fact]
    public void WithNoSpecName_TheTagIsTheJoinKey()
    {
        var target = new MirroredSignal("DB.Profile", MirrorValueType.Int);

        Assert.Equal("DB.Profile", target.JoinKey);
        Assert.Empty(MirrorValueFit.CheckAll(
            new[] { target },
            new Dictionary<string, string>(StringComparer.Ordinal) { ["DB.Profile"] = "1" }));
    }
}
