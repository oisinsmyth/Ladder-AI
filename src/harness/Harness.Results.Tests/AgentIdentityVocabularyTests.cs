using Harness.Results;

namespace Harness.Results.Tests;

/// <summary>
/// 🔴 <b>THE FORM-DETECTION RULE, ITS STATED LIMITS, AND THE THIRD OUTCOME THE OLD <c>bool</c> HAD NO
/// ROOM FOR.</b>
///
/// <para>The defect these exist for, measured 2026-08-24: <c>AgentIdentity.SameAs</c> was
/// <c>Trim()</c> + <c>OrdinalIgnoreCase</c> exact match, and the repo carries two identity
/// vocabularies at once. A binding stamped <c>&lt;session-id&gt;/lad-coder</c> and a submission
/// stamped <c>lad-coder</c> are the same party twice over — and the comparison said <c>false</c>, so
/// every D6 gate passed on a difference of formatting. <b>Every limit the rule has is asserted here
/// rather than described in a comment nobody re-reads.</b></para>
/// </summary>
public class AgentIdentityVocabularyTests
{
    private const string Session = "session_015D8Gn9KXogXP6UxXZzHeFj";

    private static AgentIdentity Id(string value) => new(value);

    // ---------------------------------------------------------------------------------------------
    // The rule
    // ---------------------------------------------------------------------------------------------

    [Theory]
    // Every role label this repo actually carries, read out of gen/, src/ and docs/ on 2026-08-24.
    [InlineData("lad-coder")]
    [InlineData("vector-author-5.2")]
    [InlineData("vector-author-b-5.2")]
    [InlineData("model-fidelity-declarer-1")]
    [InlineData("assertion-enumerator")]
    [InlineData("agent-a")]
    public void A_STRING_WITH_NO_SEPARATOR_IS_A_ROLE_LABEL(string value)
    {
        Assert.Equal(IdentityForm.Role, Id(value).Form);
    }

    [Theory]
    [InlineData(Session + "/lad-coder")]
    [InlineData(Session + "/assertion-enumerator")]
    [InlineData("a/b")]
    public void EXACTLY_ONE_SEPARATOR_WITH_BOTH_SIDES_PRESENT_IS_AN_INSTANCE_LABEL(string value)
    {
        Assert.Equal(IdentityForm.Instance, Id(value).Form);
    }

    [Theory]
    [InlineData("/lad-coder")]              // no session
    [InlineData(Session + "/")]             // no agent type
    [InlineData(Session + "//lad-coder")]   // doubled
    [InlineData("a/b/c")]                   // three parts: neither vocabulary defines one
    [InlineData(Session + "/ ")]            // whitespace is not an agent type
    [InlineData("")]                        // unrecorded
    [InlineData("   ")]
    public void ANYTHING_ELSE_TOUCHING_THE_SEPARATOR_IS_INDETERMINATE_and_compares_against_nothing(string value)
    {
        // *** THE FAIL-SAFE SINK. *** A shape that fits neither vocabulary is not rounded into the nearer
        // one, because rounding is how a role label would end up compared against an instance label.
        Assert.Equal(IdentityForm.Indeterminate, Id(value).Form);
        Assert.False(Id(value).CanCompareWith(Id(Session + "/lad-coder")));
        Assert.False(Id(value).CanCompareWith(Id("lad-coder")));
    }

    [Fact]
    public void TWO_INDETERMINATES_ARE_STILL_NOT_COMPARABLE_because_both_unclassifiable_is_not_a_vocabulary()
    {
        Assert.Equal(IdentityRelation.NotComparable, Id("a/b/c").SameAs(Id("a/b/c")));
        Assert.Equal(IdentityRelation.NotComparable, Id("a/b/c").SameAs(Id("x/y/z")));
    }

    [Fact]
    public void THE_DEFAULTS_ARE_THE_FAIL_SAFE_VALUES()
    {
        // Both enums put the not-a-verdict member at 0, so a value that arrives from anywhere without
        // being classified compares against nothing rather than joining a vocabulary by accident.
        Assert.Equal(IdentityForm.Indeterminate, default(IdentityForm));
        Assert.Equal(IdentityRelation.NotComparable, default(IdentityRelation));
        Assert.Equal(IdentityForm.Indeterminate, default(AgentIdentity).Form);
    }

    // ---------------------------------------------------------------------------------------------
    // The three outcomes
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void THE_VACUOUS_GREEN_AN_INSTANCE_LABEL_AGAINST_A_ROLE_LABEL_IS_NOT_COMPARABLE()
    {
        // 🔴 *** THE MEASURED CASE. *** Before this guard both directions returned `false`, which every
        // caller read as "two different parties" — the pass this whole change exists to remove.
        Assert.Equal(IdentityRelation.NotComparable, Id(Session + "/lad-coder").SameAs(Id("lad-coder")));
        Assert.Equal(IdentityRelation.NotComparable, Id("lad-coder").SameAs(Id(Session + "/lad-coder")));
    }

    [Fact]
    public void WITHIN_ONE_VOCABULARY_BOTH_REAL_VERDICTS_SURVIVE()
    {
        // The other direction, and it is half the deliverable: the guard must not silence what worked.
        Assert.Equal(IdentityRelation.DifferentParties, Id("agent-a").SameAs(Id("agent-b")));
        Assert.Equal(IdentityRelation.SameParty, Id("agent-a").SameAs(Id("agent-a")));
        Assert.Equal(IdentityRelation.DifferentParties, Id(Session + "/lad-coder").SameAs(Id(Session + "/assertion-enumerator")));
        Assert.Equal(IdentityRelation.SameParty, Id(Session + "/lad-coder").SameAs(Id(Session + "/lad-coder")));
    }

    [Fact]
    public void NORMALISATION_IS_UNCHANGED_INSIDE_A_VOCABULARY_trim_and_case_still_collide()
    {
        // §1.1: loose is safe for a refusal. The form check is a PRE-condition on that comparison and
        // never a replacement for it, so a keystroke variant is still one party.
        Assert.Equal(IdentityRelation.SameParty, Id("AGENT-B ").SameAs(Id("agent-b")));
        Assert.Equal(IdentityRelation.SameParty, Id("  " + Session + "/LAD-CODER").SameAs(Id(Session + "/lad-coder")));
    }

    // ---------------------------------------------------------------------------------------------
    // The API's own guard
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void IsSamePartyAs_THROWS_ON_A_CROSS_FORM_PAIR_rather_than_answering_FALSE()
    {
        // *** `false` IS THE VACUOUS PASS. *** A caller that forgets `CanCompareWith` gets a loud stop on
        // the first mixed submission instead of a green on every one of them.
        var ex = Assert.Throws<InvalidOperationException>(
            () => Id(Session + "/lad-coder").IsSamePartyAs(Id("lad-coder")));

        Assert.Contains("not comparable", ex.Message, StringComparison.Ordinal);
        Assert.Contains("CanCompareWith", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void IsSamePartyAs_ANSWERS_NORMALLY_INSIDE_ONE_VOCABULARY()
    {
        Assert.True(Id("agent-a").IsSamePartyAs(Id("agent-a")));
        Assert.False(Id("agent-a").IsSamePartyAs(Id("agent-b")));
    }

    [Fact]
    public void THERE_IS_NO_BOOL_RETURNING_SameAs_LEFT_TO_REACH_FOR()
    {
        // The signature change is the forcing function — it broke all eight call sites at compile time.
        // If a bool overload came back, a new call site could reintroduce the hole without a compiler
        // complaint and this suite would not notice.
        var sameAs = typeof(AgentIdentity).GetMethod(nameof(AgentIdentity.SameAs), new[] { typeof(AgentIdentity) });

        Assert.NotNull(sameAs);
        Assert.Equal(typeof(IdentityRelation), sameAs!.ReturnType);
        Assert.DoesNotContain(
            typeof(AgentIdentity).GetMethods(),
            m => m.Name == nameof(AgentIdentity.SameAs) && m.ReturnType == typeof(bool));
    }

    // ---------------------------------------------------------------------------------------------
    // The stated limits, asserted so nobody has to take the comment's word for it
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void THE_DECIDED_AMBIGUITY_a_ROLE_LABEL_CONTAINING_ONE_SLASH_READS_AS_AN_INSTANCE()
    {
        // 🔴 Nothing mechanical separates `a/b` from `<session>/lad-coder`, and this is where it goes.
        // The rule is safe TODAY because the committed role vocabulary contains no slash; if one ever
        // enters, this test is the tripwire and IdentityVocabulary's comment is where to start.
        Assert.Equal(IdentityForm.Instance, Id("a/b").Form);
        Assert.Equal(IdentityRelation.NotComparable, Id("a/b").SameAs(Id("lad-coder")));
    }

    [Fact]
    public void THE_OTHER_LIMIT_an_instance_label_that_lost_its_session_prefix_reads_as_a_ROLE()
    {
        // Under the convention `--agent` is DERIVED, never invented, so a bare agent type is a convention
        // violation and not an instance label. It costs a NOT CHECKED against real instance labels, which
        // is the quiet direction rather than the green one.
        Assert.Equal(IdentityForm.Role, Id("lad-coder").Form);
        Assert.Equal(IdentityRelation.NotComparable, Id("lad-coder").SameAs(Id(Session + "/lad-coder")));
    }

    [Fact]
    public void THE_RULE_DOES_NOT_PRETEND_TO_VALIDATE_A_SESSION_ID()
    {
        // It answers one question — WHICH VOCABULARY — and no other. Keying on the shape of the left-hand
        // side would mean inventing a session-id grammar nobody has published, and the first id that did
        // not match it would silence a gate on exactly the new material the convention exists for.
        Assert.Equal(IdentityForm.Instance, Id("not-a-session-id/lad-coder").Form);
        Assert.Equal(IdentityForm.Instance, Id("7/x").Form);
    }

    [Fact]
    public void THE_NOT_CHECKED_TEXT_TELLS_A_READER_WHICH_IS_WHICH_AND_WHAT_TO_DO()
    {
        var detail = IdentityVocabulary.CrossFormDetail(
            "the map's declarer against the block's author", Id(Session + "/lad-coder"), Id("lad-coder"));

        Assert.Contains("an INSTANCE label (<session-id>/<agent-type>)", detail, StringComparison.Ordinal);
        Assert.Contains("a ROLE label", detail, StringComparison.Ordinal);
        Assert.Contains("This is a RULING and not a bug", detail, StringComparison.Ordinal);
        Assert.Contains("committed artifacts are NOT retrofitted", detail, StringComparison.Ordinal);
        Assert.Contains("The repair is that both sides be produced under the convention", detail, StringComparison.Ordinal);
        Assert.Contains("docs/notes/test-environment-contract.md §1.1", detail, StringComparison.Ordinal);
        Assert.Contains("Unknown is not independent.", detail, StringComparison.Ordinal);

        // Short enough to finish reading. The per-vector paragraph that was emitted 27 times is the
        // precedent: a refusal nobody finishes is a refusal nobody acts on.
        Assert.True(detail.Length < 900, $"the cross-form text is {detail.Length} characters");
    }
}
