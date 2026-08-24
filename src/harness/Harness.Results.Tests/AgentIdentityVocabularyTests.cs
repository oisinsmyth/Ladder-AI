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

    // =============================================================================================
    // THE OWNER FORM — ruled 2026-08-24. `owner:<handle>`, a THIRD vocabulary, and the first one
    // that does not name an agent.
    //
    // The gap it closes: the convention had no form for a human. An owner-authored artifact stamped
    // with a bare handle carries no separator, so it read as a ROLE and every comparison against a
    // convention-stamped agent went NotComparable — permanently NOT CHECKED on precisely the
    // artifacts with the best provenance in the system, reported identically to an unattributed one.
    // =============================================================================================

    /// <summary>The owner's handle, under the convention. The spelling and capitalisation are the owner's own.</summary>
    private const string Owner = "owner:MaTRiXz";

    [Theory]
    [InlineData("owner:MaTRiXz")]
    [InlineData("OWNER:MaTRiXz")]      // the prefix is matched case-insensitively
    [InlineData("  owner:MaTRiXz  ")]  // Normalised trims first
    [InlineData("owner:someone-else")]
    public void THE_OWNER_PREFIX_IS_ITS_OWN_VOCABULARY(string value)
    {
        Assert.Equal(IdentityForm.Owner, Id(value).Form);
    }

    [Fact]
    public void THE_TRAP_THE_SHAPE_WAS_CHOSEN_TO_AVOID_owner_SLASH_handle_IS_AN_AGENT_AND_NOT_THE_OWNER()
    {
        // 🔴 *** THIS IS WHY THE SEPARATOR IS A COLON. *** `owner/MaTRiXz` has exactly one slash with both
        // sides non-empty, so the INSTANCE rule claims it: session id `owner`, agent type `MaTRiXz`. The
        // owner would be silently filed as an agent — the one reading the ruling exists to forbid, and it
        // would produce a VERDICT rather than a refusal, which is the dangerous direction.
        //
        // The test is here so that "tidying" the prefix to a slash later fails loudly instead of quietly.
        Assert.Equal(IdentityForm.Instance, Id("owner/MaTRiXz").Form);
        Assert.NotEqual(IdentityForm.Owner, Id("owner/MaTRiXz").Form);

        // And the consequence, spelled out: under the slash spelling the owner would compare against a
        // real agent instance as two agents, on a string difference.
        Assert.Equal(IdentityRelation.DifferentParties, Id("owner/MaTRiXz").SameAs(Id(Session + "/lad-coder")));
    }

    [Theory]
    [InlineData("owner:")]        // no handle
    [InlineData("owner:   ")]     // whitespace is not a handle
    [InlineData("owner:a:b")]     // a second colon owns no defined meaning
    [InlineData("owner:a/b")]     // the agent separator, smuggled inside a handle
    public void A_MALFORMED_OWNER_STRING_GOES_TO_THE_FAIL_SAFE_SINK_and_is_not_rounded_into_a_vocabulary(string value)
    {
        Assert.Equal(IdentityForm.Indeterminate, Id(value).Form);
        Assert.False(Id(value).CanCompareWith(Id(Session + "/lad-coder")));
        Assert.False(Id(value).CanCompareWith(Id(Owner)));
    }

    [Fact]
    public void THE_OWNER_FORMS_OWN_LIMIT_A_BARE_HANDLE_STILL_READS_AS_A_ROLE()
    {
        // ⚠️ Stated rather than implied away. `MaTRiXz` has no separator and nothing can tell it from a
        // role label. That is today's behaviour unchanged — the form did not exist — and it is exactly
        // why Owner-versus-Role is NotComparable rather than DifferentParties: see the test below.
        Assert.Equal(IdentityForm.Role, Id("MaTRiXz").Form);

        // `owner` on its own is a role label too: no colon, no prefix, nothing to key on.
        Assert.Equal(IdentityForm.Role, Id("owner").Form);
    }

    [Fact]
    public void THE_DECIDED_CASE_ON_THE_OTHER_SIDE_a_NON_OWNER_COLON_STAYS_A_ROLE()
    {
        // Same trade as the slash paragraph, resolved the same way: no committed identity string contains
        // a colon (measured across gen/ on 2026-08-24), so sending `foo:bar` to Indeterminate would
        // silence a real comparison to guard a hypothetical one. If a colon ever enters a role label this
        // test is the tripwire.
        Assert.Equal(IdentityForm.Role, Id("foo:bar").Form);
        Assert.Equal(IdentityRelation.DifferentParties, Id("foo:bar").SameAs(Id("lad-coder")));
    }

    // ---------------------------------------------------------------------------------------------
    // The relation table
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void OWNER_AGAINST_AN_AGENT_INSTANCE_IS_A_REAL_VERDICT_and_this_is_the_case_worth_getting_right()
    {
        // 🔴 *** THE PAIRING THE WHOLE FORM EXISTS FOR. *** A human and an agent are different parties by
        // construction — no string comparison is needed and none could help. This is the one that turns a
        // permanent NOT CHECKED into a genuine pass.
        Assert.Equal(IdentityRelation.DifferentParties, Id(Owner).SameAs(Id(Session + "/lad-coder")));
        Assert.Equal(IdentityRelation.DifferentParties, Id(Session + "/lad-coder").SameAs(Id(Owner)));

        // And it is comparable, which is what lets a gate get past its cross-form sweep at all.
        Assert.True(Id(Owner).CanCompareWith(Id(Session + "/lad-coder")));
    }

    [Fact]
    public void OWNER_AGAINST_A_ROLE_LABEL_IS_NOT_COMPARABLE_and_the_weaker_answer_is_the_deliberate_one()
    {
        // 🔴 The construction argument — a role names an agent's job, a human is not an agent, therefore
        // different parties — LEAKS, and this assertion is the leak. The role vocabulary is the UNDEFINED
        // one: every string with no separator. So it CONTAINS strings that denote the owner, and under
        // `DifferentParties` the line below would be the same human written twice, passing a self-check.
        Assert.Equal(IdentityForm.Role, Id("MaTRiXz").Form);
        Assert.Equal(IdentityRelation.NotComparable, Id(Owner).SameAs(Id("MaTRiXz")));

        // Not hypothetical: owner-questions.md D1 records a committed binding whose prose names an
        // "owner as a role". Any role label, not just the handle-shaped one.
        Assert.Equal(IdentityRelation.NotComparable, Id(Owner).SameAs(Id("lad-coder")));
        Assert.Equal(IdentityRelation.NotComparable, Id("lad-coder").SameAs(Id(Owner)));
    }

    [Fact]
    public void TWO_OWNER_HANDLES_COMPARE_AS_STRINGS_because_two_handles_CAN_collide()
    {
        Assert.Equal(IdentityRelation.SameParty, Id(Owner).SameAs(Id(Owner)));
        Assert.Equal(IdentityRelation.DifferentParties, Id(Owner).SameAs(Id("owner:someone-else")));

        // Normalised, like every other same-vocabulary comparison: loose is safe for a refusal, so a case
        // or whitespace variant must not buy a second party.
        Assert.Equal(IdentityRelation.SameParty, Id("  OWNER:matrixz ").SameAs(Id(Owner)));
    }

    [Fact]
    public void THE_FULL_RELATION_MATRIX_ALL_SIXTEEN_CELLS_and_the_denominator_that_keeps_it_complete()
    {
        // *** THE DENOMINATOR ASSERTION. *** Four forms, so sixteen ordered pairs, every one named. A
        // fifth form cannot be added without this test failing, which is the point: the table is the
        // specification and a new member that nobody routed would otherwise default into silence.
        Assert.Equal(4, Enum.GetValues(typeof(IdentityForm)).Length);
        Assert.Equal(3, Enum.GetValues(typeof(IdentityRelation)).Length);

        var representative = new Dictionary<IdentityForm, string>
        {
            [IdentityForm.Indeterminate] = "a/b/c",
            [IdentityForm.Role] = "lad-coder",
            [IdentityForm.Instance] = Session + "/lad-coder",
            [IdentityForm.Owner] = Owner,
        };

        Assert.Equal(4, representative.Count);
        foreach (var (form, value) in representative)
            Assert.Equal(form, Id(value).Form);

        const IdentityRelation no = IdentityRelation.NotComparable;
        const IdentityRelation diff = IdentityRelation.DifferentParties;
        const IdentityRelation same = IdentityRelation.SameParty;

        var expected = new Dictionary<(IdentityForm Left, IdentityForm Right), IdentityRelation>
        {
            // Indeterminate compares against nothing, including itself.
            [(IdentityForm.Indeterminate, IdentityForm.Indeterminate)] = no,
            [(IdentityForm.Indeterminate, IdentityForm.Role)] = no,
            [(IdentityForm.Indeterminate, IdentityForm.Instance)] = no,
            [(IdentityForm.Indeterminate, IdentityForm.Owner)] = no,

            // The two AGENT vocabularies never compare across. 5c99bd3's guard, and it must not weaken.
            [(IdentityForm.Role, IdentityForm.Indeterminate)] = no,
            [(IdentityForm.Role, IdentityForm.Role)] = same,
            [(IdentityForm.Role, IdentityForm.Instance)] = no,
            [(IdentityForm.Role, IdentityForm.Owner)] = no,

            [(IdentityForm.Instance, IdentityForm.Indeterminate)] = no,
            [(IdentityForm.Instance, IdentityForm.Role)] = no,
            [(IdentityForm.Instance, IdentityForm.Instance)] = same,
            [(IdentityForm.Instance, IdentityForm.Owner)] = diff,   // by construction

            [(IdentityForm.Owner, IdentityForm.Indeterminate)] = no,
            [(IdentityForm.Owner, IdentityForm.Role)] = no,         // the undefined vocabulary
            [(IdentityForm.Owner, IdentityForm.Instance)] = diff,   // by construction
            [(IdentityForm.Owner, IdentityForm.Owner)] = same,
        };

        Assert.Equal(16, expected.Count);

        foreach (var ((left, right), want) in expected)
        {
            // Same representative on both sides where the forms match, so the diagonal reads SameParty.
            var got = Id(representative[left]).SameAs(Id(representative[right]));
            Assert.True(want == got, $"{left} vs {right}: expected {want}, got {got}");
        }
    }

    [Fact]
    public void THE_RELATION_IS_SYMMETRIC_for_every_pair_of_representative_strings()
    {
        // An asymmetric relation would mean a gate's verdict depended on which identity the caller
        // happened to put first — and the four gates do not all order them the same way.
        var values = new[]
        {
            "a/b/c", "lad-coder", "agent-b", Session + "/lad-coder", Session + "/assertion-enumerator",
            Owner, "owner:someone-else", "owner:", "MaTRiXz",
        };

        Assert.Equal(9, values.Length);

        var pairs = 0;
        foreach (var left in values)
        {
            foreach (var right in values)
            {
                Assert.True(Id(left).SameAs(Id(right)) == Id(right).SameAs(Id(left)),
                    $"'{left}' vs '{right}' is not symmetric");
                pairs++;
            }
        }

        Assert.Equal(81, pairs);
    }

    [Fact]
    public void THE_PRESERVED_GUARD_the_two_AGENT_vocabularies_STILL_do_not_compare()
    {
        // 🔴 Landed 2026-08-24 in 5c99bd3, hours before the owner form. Adding a third vocabulary must not
        // orphan it: a role label and an instance label can never collide, so a verdict across them would
        // report the string formats. Both directions, because that is what the guard claims.
        Assert.Equal(IdentityRelation.NotComparable, Id(Session + "/lad-coder").SameAs(Id("lad-coder")));
        Assert.Equal(IdentityRelation.NotComparable, Id("lad-coder").SameAs(Id(Session + "/lad-coder")));
        Assert.False(Id("agent-a").CanCompareWith(Id(Session + "/agent-a")));
    }

    [Fact]
    public void IsSamePartyAs_ANSWERS_FOR_AN_OWNER_AGAINST_AN_INSTANCE_and_still_THROWS_against_a_ROLE()
    {
        // The partial function must stay partial in exactly the same places the relation says
        // NotComparable — no wider, no narrower.
        Assert.False(Id(Owner).IsSamePartyAs(Id(Session + "/lad-coder")));
        Assert.True(Id(Owner).IsSamePartyAs(Id("owner:matrixz")));

        var ex = Assert.Throws<InvalidOperationException>(() => Id(Owner).IsSamePartyAs(Id("lad-coder")));
        Assert.Contains("CanCompareWith", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void THE_OWNER_NOT_CHECKED_TEXT_NEVER_CALLS_A_NAMED_HUMAN_UNKNOWN()
    {
        // 🔴 *** "Unknown is not independent" IS THE WRONG SENTENCE FOR A NAMED HUMAN, *** and the generic
        // text is wrong twice over here: the owner side is the best-attributed artifact in the system,
        // and the generic repair — "both sides produced under the convention" — is unreachable, because
        // the owner will never hold a session id.
        var detail = IdentityVocabulary.CrossFormDetail(
            "the map's declarer against the block's author", Id(Owner), Id("lad-coder"));

        Assert.DoesNotContain("Unknown is not independent", detail, StringComparison.Ordinal);
        Assert.DoesNotContain("committed artifacts are NOT retrofitted", detail, StringComparison.Ordinal);

        Assert.Contains("MaTRiXz", detail, StringComparison.Ordinal);
        Assert.Contains("a named human, not an agent", detail, StringComparison.Ordinal);
        Assert.Contains("THIS ARTIFACT IS ATTRIBUTED", detail, StringComparison.Ordinal);
        Assert.Contains("THE REPAIR IS ON THE COUNTERPARTY, NEVER THE OWNER", detail, StringComparison.Ordinal);
        Assert.Contains("docs/notes/test-environment-contract.md §1.1", detail, StringComparison.Ordinal);

        // Same budget as the generic branch, for the same reason.
        Assert.True(detail.Length < 900, $"the owner cross-form text is {detail.Length} characters");
    }

    [Fact]
    public void THE_OWNER_TEXT_NAMES_THE_OWNER_FIRST_WHICHEVER_SIDE_THE_CALLER_PUT_THEM_ON()
    {
        // The four gates do not all order the pair the same way, and a message that opened with the agent
        // when the owner was the subject would read as an accusation against the artifact that is fine.
        var ownerLeft = IdentityVocabulary.CrossFormDetail("x", Id(Owner), Id("lad-coder"));
        var ownerRight = IdentityVocabulary.CrossFormDetail("x", Id("lad-coder"), Id(Owner));

        Assert.Equal(ownerLeft, ownerRight);
        Assert.Contains($"'{Owner}' is the OWNER'S OWN HANDLE", ownerLeft, StringComparison.Ordinal);
    }
}
