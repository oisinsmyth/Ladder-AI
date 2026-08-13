using Harness.Results;

namespace Harness.Results.Tests;

/// <summary>
/// The ID scheme (`assertion-enumeration.md` §3.1–§3.3).
///
/// <para><b>The known-answer vectors at the foot are the load-bearing part.</b> Every other test here
/// checks this implementation against itself, which demonstrates self-consistency and nothing else. The
/// pinned IDs were computed by a SECOND, INDEPENDENTLY WRITTEN implementation — Python 3.12,
/// <c>hashlib</c>, its own normalisation — over the live enumeration, and agreed on all nineteen. They
/// are also what <c>Ladder.Wave</c>'s parallel copy of this scheme can be checked against, since the two
/// implementations are not consolidated (different lane).</para>
/// </summary>
public class AssertionIdTests
{
    // ---- NORMALISATION -----------------------------------------------------------------------------

    [Theory]
    [InlineData("  WHEN a THEN b  ", "WHEN a THEN b")]
    [InlineData("WHEN a   THEN    b", "WHEN a THEN b")]
    [InlineData("WHEN a\tTHEN\nb", "WHEN a THEN b")]
    [InlineData("WHEN a THEN b.", "WHEN a THEN b")]
    [InlineData("WHEN a THEN b;", "WHEN a THEN b")]
    public void Normalisation_trims_collapses_and_strips_one_trailing_terminator(string raw, string expected)
    {
        Assert.Equal(expected, AssertionId.Normalise(raw));
    }

    [Fact]
    public void Only_ONE_trailing_terminator_is_stripped()
    {
        // Two is a typo the enumerator can see. Stripping both would make two visibly different texts
        // collide, which is the opposite of what the scheme is for.
        Assert.Equal("WHEN a THEN b.", AssertionId.Normalise("WHEN a THEN b.."));
    }

    /// <summary>
    /// *** CASE IS PRESERVED DELIBERATELY. *** Folding it risks merging two distinct signal names, and
    /// two assertions differing only by the case of a tag are two assertions.
    /// </summary>
    [Fact]
    public void Case_is_preserved_so_two_signal_names_differing_only_in_case_do_not_merge()
    {
        Assert.Equal("WHEN Motor_Run THEN x", AssertionId.Normalise("WHEN Motor_Run THEN x"));

        Assert.NotEqual(
            AssertionId.Compute("REQ-1", "WHEN Motor_Run THEN x"),
            AssertionId.Compute("REQ-1", "WHEN MOTOR_RUN THEN x"));
    }

    [Fact]
    public void Whitespace_and_terminator_variants_of_one_sentence_share_an_ID()
    {
        var canonical = AssertionId.Compute("REQ-1", "WHEN a THEN b");

        Assert.Equal(canonical, AssertionId.Compute("REQ-1", "  WHEN   a  THEN b.  "));
        Assert.Equal(canonical, AssertionId.Compute("REQ-1", "WHEN a THEN b;"));
    }

    // ---- NOTHING POSITIONAL (§3.2) ------------------------------------------------------------------

    [Fact]
    public void An_ID_depends_on_the_clause_and_the_content_and_on_NOTHING_ELSE()
    {
        // The same sentence in two clauses is two different assertions; the same clause and sentence is
        // the same assertion however many siblings sit around it, in whatever order.
        Assert.NotEqual(AssertionId.Compute("REQ-1", "WHEN a THEN b"), AssertionId.Compute("REQ-2", "WHEN a THEN b"));
        Assert.Equal(AssertionId.Compute("REQ-1", "WHEN a THEN b"), AssertionId.Compute("REQ-1", "WHEN a THEN b"));
    }

    /// <summary>
    /// *** THE PROPERTY THE WHOLE SCHEME EXISTS FOR. *** Editing an assertion's text changes its ID, so a
    /// citation cannot silently survive a rewording of what it cites.
    /// </summary>
    [Fact]
    public void Editing_the_text_MOVES_the_ID()
    {
        var before = AssertionId.Compute("REQ-1", "WHEN the level is high THEN the alarm is asserted");
        var after = AssertionId.Compute("REQ-1", "WHEN the level has been high for the persistence time THEN the alarm is asserted");

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void A_clause_ID_is_required_because_the_IDs_inherit_its_stability()
    {
        Assert.Throws<ArgumentException>(() => AssertionId.Compute("", "WHEN a THEN b"));
        Assert.Throws<ArgumentException>(() => AssertionId.Compute("   ", "WHEN a THEN b"));
    }

    [Fact]
    public void An_empty_assertion_text_is_refused_rather_than_hashed()
    {
        // Every empty assertion would otherwise share one ID and collide silently.
        Assert.Throws<ArgumentException>(() => AssertionId.Compute("REQ-1", "   "));
    }

    // ---- SHAPE --------------------------------------------------------------------------------------

    [Theory]
    [InlineData("REQ-014:3f9a1c", true)]
    [InlineData("REQ-HBA-001:ab68c1", true)]
    [InlineData("REQ-014:3F9A1C", false)]   // uppercase hex is not the form
    [InlineData("REQ-014:3f9a1", false)]    // five characters
    [InlineData("REQ-014:3f9a1cd", false)]  // seven
    [InlineData("REQ-014", false)]
    [InlineData("REQ-014:", false)]
    [InlineData("REQ-014:zzzzzz", false)]
    public void TryParse_accepts_only_the_canonical_shape(string id, bool expected)
    {
        Assert.Equal(expected, AssertionId.TryParse(id, out _, out _));
    }

    [Theory]
    [InlineData("REQ-014.A2", true)]
    [InlineData("REQ-HBA-001.A12", true)]
    [InlineData("REQ-014.a2", true)]
    [InlineData("REQ-014:3f9a1c", false)]
    [InlineData("REQ-14.b", false)]
    [InlineData("REQ-14.invented", false)]
    [InlineData("REQ-014.A", false)]
    public void The_display_ordinal_form_is_recognisable_by_shape(string citation, bool expected)
    {
        Assert.Equal(expected, AssertionId.IsDisplayOrdinalForm(citation));
    }

    [Theory]
    [InlineData("WHEN a THEN b", AssertionForm.When, true)]
    [InlineData("NEVER x happens", AssertionForm.Never, true)]
    [InlineData("NEVER x THEN y", AssertionForm.Never, false)]
    [InlineData("WHEN a happens", AssertionForm.When, false)]
    [InlineData("WHEN a THEN b", AssertionForm.Unstated, false)]
    public void TextMatchesForm_holds_the_text_to_the_template(string text, AssertionForm form, bool expected)
    {
        Assert.Equal(expected, AssertionId.TextMatchesForm(text, form));
    }

    // ---- KNOWN-ANSWER VECTORS ----------------------------------------------------------------------

    /// <summary>
    /// The live enumeration's own IDs, pinned.
    ///
    /// <para><b>Independently derived:</b> a separate Python 3.12 implementation (its own normalisation,
    /// <c>hashlib.sha256</c>) read the same YAML and produced these nineteen, character for character.
    /// That is the only step in this file with an authority outside this assembly.</para>
    /// </summary>
    [Theory]
    [InlineData("REQ-HBA-001", "WHEN Hopper_Level_High has been continuously true and the plant has run continuously for the persistence threshold time THEN the hopper-blockage alarm is asserted", "REQ-HBA-001:ab68c1")]
    [InlineData("REQ-HBA-001", "WHEN Hopper_Level_High has been continuously true and the plant has run continuously for the persistence threshold time THEN the inhibit/stop demand output is asserted", "REQ-HBA-001:8ea7f6")]
    [InlineData("REQ-HBA-004", "NEVER the hopper-blockage alarm de-asserts unless FaultReset has been asserted since the alarm was raised", "REQ-HBA-004:14c9b6")]
    [InlineData("REQ-HBA-005", "NEVER the hopper-blockage alarm is held de-asserted by a continuously-true FaultReset while the accumulated persistence time is at or past the threshold", "REQ-HBA-005:6e1e8d")]
    [InlineData("REQ-HBA-006", "WHEN Hopper_Level_High returns false while the hopper-blockage alarm is latched and FaultReset is not asserted THEN the hopper-blockage alarm remains asserted", "REQ-HBA-006:469954")]
    public void Known_answer_vectors_from_an_independent_implementation(string clause, string text, string expected)
    {
        Assert.Equal(expected, AssertionId.Compute(clause, text));
    }

    /// <summary>
    /// The two assertions of REQ-HBA-001 that share a trigger and differ only in their response are two
    /// DIFFERENT IDs. If a normalisation ever merged them the denominator would silently lose one.
    /// </summary>
    [Fact]
    public void Two_assertions_sharing_a_trigger_and_differing_in_response_do_not_collide()
    {
        Assert.NotEqual("REQ-HBA-001:ab68c1", "REQ-HBA-001:8ea7f6");
    }
}
