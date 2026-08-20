using System;
using System.Linq;
using OpennessCli.Openness;
using Xunit;

namespace OpennessCli.Tests;

/// <summary>
/// 🔴 A DEFECT THAT COULD NOT BE DIAGNOSED, AND THE CHEAPEST CHANGE THAT WOULD HAVE DIAGNOSED IT.
///
/// <para>
/// A <c>hmi-delete-screen</c> carrying eighteen <c>--name</c> flags reported the FIRST name as not
/// found. The same name alone, in the very next invocation, deleted fine. Two hypotheses were ruled
/// out by reading the code — the parser accumulates names in argv order with no overwrite, and
/// <c>DeleteScreens</c> resolves every name before the first delete, re-walking from scratch each
/// time — so the cause was recorded as unexplained rather than guessed at.
/// </para>
/// <para>
/// What remained was that <c>--name</c> is taken verbatim (no trim, no Unicode normalisation) and
/// matched <c>Ordinal</c>. A trailing space or a non-breaking space in one element of a long
/// generated command line is invisible to an echo, invisible in the output, and would produce
/// exactly the observed signature. The old message printed only the name, so there was no way to
/// see it.
/// </para>
/// <para>
/// These tests pin the diagnostic, not the cause. If the next reproduction is a whitespace
/// artefact, the message now says so outright; if it is not, the near-match line stays silent and
/// that is evidence too.
/// </para>
/// </summary>
public class ScreenNotFoundDiagnosticTests
{
    private static string Message(string requested, params string[] present) =>
        new ScreenNotFoundException(requested, present).Message;

    [Fact]
    public void The_present_set_is_named_so_absence_can_be_told_from_a_failed_walk()
    {
        var message = Message("09 Parameters", "01 Plant Overview", "09 Param Weighing");

        Assert.Contains("PRESENT:", message, StringComparison.Ordinal);
        Assert.Contains("01 Plant Overview", message, StringComparison.Ordinal);
        Assert.Contains("09 Param Weighing", message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Empty is not clean here either: "no screens at all" and "your name is not among these
    /// sixty-six" are different findings and must not print identically.
    /// </summary>
    [Fact]
    public void A_project_with_no_classic_screens_says_so_rather_than_printing_an_empty_list()
    {
        var message = Message("09 Parameters");

        Assert.Contains("none", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no classic screens at all", message, StringComparison.Ordinal);
    }

    /// <summary>The signature the whole diagnostic exists for, in each of its invisible forms.</summary>
    [Theory]
    [InlineData("09 Parameters ", "trailing space")]
    [InlineData(" 09 Parameters", "leading space")]
    [InlineData("09 Parameters", "non-breaking space")]
    [InlineData("09 Parameters​", "zero-width space")]
    [InlineData("09  Parameters", "doubled space")]
    [InlineData("09 parameters", "case")]
    public void A_name_differing_only_invisibly_is_called_out(string requested, string why)
    {
        var message = Message(requested, "09 Parameters", "01 Plant Overview");

        Assert.True(message.IndexOf("NEAR MATCH", StringComparison.Ordinal) >= 0,
            $"a name differing by {why} should be reported as a near match. Got: {message}");
        Assert.Contains("'09 Parameters'", message, StringComparison.Ordinal);

        // The code points are the half a person cannot get any other way - an echo shows nothing.
        Assert.Contains("REQUESTED, code point by code point:", message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The negative case, and it is the one that keeps the diagnostic honest. A genuinely absent
    /// screen must NOT be dressed up as a near miss, or the message starts inventing a cause.
    /// </summary>
    [Fact]
    public void A_genuinely_different_name_is_not_reported_as_a_near_match()
    {
        var message = Message("11 Simulation", "09 Parameters", "01 Plant Overview");

        Assert.DoesNotContain("NEAR MATCH", message, StringComparison.Ordinal);
        Assert.DoesNotContain("code point", message, StringComparison.Ordinal);
    }

    /// <summary>An exact match is not a near match — it would never have thrown in the first place.</summary>
    [Fact]
    public void An_exactly_present_name_is_not_reported_as_a_near_match()
    {
        var message = Message("09 Parameters", "09 Parameters");

        Assert.DoesNotContain("NEAR MATCH", message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_code_point_dump_renders_the_invisible_character_and_leaves_ascii_alone()
    {
        var message = Message("A B", "A B");

        Assert.Contains("U+00A0", message, StringComparison.Ordinal);
        Assert.Contains("A U+00A0 B", message, StringComparison.Ordinal);
    }
}
