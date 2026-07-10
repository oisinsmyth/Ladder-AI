using OpennessCli.Openness;
using Xunit;

namespace OpennessCli.Tests;

public class SafetyClassifierTests
{
    [Theory]
    [InlineData("F_LAD")]
    [InlineData("F_FBD")]
    [InlineData("F_DB")]
    [InlineData("F_STL")]
    [InlineData("F_LAD_LIB")]
    [InlineData("F_FBD_LIB")]
    [InlineData("F_CALL")]
    [InlineData("F_SomeFutureVariantWeHaventSeenYet")] // prefix match must catch unseen F_ variants too
    public void IsSafety_FlagsAnyFPrefixedLanguage(string language)
    {
        Assert.True(SafetyClassifier.IsSafety(language));
    }

    [Theory]
    [InlineData("LAD")]
    [InlineData("FBD")]
    [InlineData("STL")]
    [InlineData("SCL")]
    [InlineData("GRAPH")]
    [InlineData("DB")]
    [InlineData("CPU_DB")]
    [InlineData("Undef")]
    public void IsSafety_DoesNotFlagKnownNonSafetyLanguages(string language)
    {
        Assert.False(SafetyClassifier.IsSafety(language));
    }

    [Theory]
    [InlineData("bogus")]
    [InlineData("")]
    [InlineData("UDT")]
    public void IsSafety_ThrowsOnUnrecognizedLanguage(string language)
    {
        Assert.Throws<UnrecognizedProgrammingLanguageException>(() => SafetyClassifier.IsSafety(language));
    }

    [Fact]
    public void IsSafety_ThrowsOnNull()
    {
        Assert.Throws<System.ArgumentNullException>(() => SafetyClassifier.IsSafety(null!));
    }
}
