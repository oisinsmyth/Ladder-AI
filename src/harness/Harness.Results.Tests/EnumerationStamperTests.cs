using Harness.Results;

namespace Harness.Results.Tests;

/// <summary>
/// §3.4's stamping step.
///
/// <para><b>Most of these are negative</b>, because the stamper's whole risk profile is that it would be
/// helpful: preserve an ID whose text changed, stamp 18 of 19 and report success, or approximate a text
/// it could not read. Each of those is a silent survival, and each is refused here.</para>
/// </summary>
public class EnumerationStamperTests
{
    /// <summary>
    /// A minimal enumeration in the shape the enumerator emits. <paramref name="assertions"/> are
    /// (clause, ordinal, form, text, existingIdOrNull) so a test can construct the re-stamp case.
    /// </summary>
    private static string File(int declared, params (string Clause, string Ordinal, string Form, string Text, string? Id)[] assertions)
    {
        var lines = new List<string>
        {
            "enumerator: assertion-enumerator",
            "",
            "denominator:",
            "  clauses: " + assertions.Select(a => a.Clause).Distinct(StringComparer.Ordinal).Count(),
            "  assertions: " + declared,
            "",
            "clauses:",
        };

        foreach (var clause in assertions.GroupBy(a => a.Clause, StringComparer.Ordinal))
        {
            lines.Add("  " + clause.Key + ":");
            lines.Add("    instances: []");
            lines.Add("    assertions:");

            foreach (var a in clause)
            {
                lines.Add("      - ordinal: " + a.Ordinal);
                lines.Add("        form: " + a.Form);
                lines.Add("        text: \"" + a.Text + "\"");
                lines.Add("        normalised_text: \"" + a.Text + "\"");
                if (a.Id is not null)
                    lines.Add("        id: " + a.Id);
                lines.Add("        response_signal: Something");
            }
        }

        return string.Join("\n", lines) + "\n";
    }

    private static (string Clause, string Ordinal, string Form, string Text, string? Id) A(
        string clause = "REQ-1", string ordinal = "A1", string form = "When",
        string text = "WHEN a THEN b", string? id = null) => (clause, ordinal, form, text, id);

    // ---- THE HAPPY PATH ----------------------------------------------------------------------------

    [Fact]
    public void An_unstamped_enumeration_gets_an_id_line_under_every_normalised_text()
    {
        var result = EnumerationStamper.Stamp(File(2, A(ordinal: "A1", text: "WHEN a THEN b"), A(ordinal: "A2", text: "WHEN c THEN d")));

        Assert.True(result.Stamped);
        Assert.Equal(2, result.Assertions.Count);
        Assert.All(result.Assertions, a => Assert.Equal(AssertionId.Compute(a.ClauseId, a.NormalisedText), a.Id));
        Assert.Contains("        id: " + result.Assertions[0].Id, result.StampedText!, StringComparison.Ordinal);
        Assert.Empty(result.Dangling);
    }

    [Fact]
    public void Stamping_is_idempotent()
    {
        var once = EnumerationStamper.Stamp(File(1, A())).StampedText!;
        var twice = EnumerationStamper.Stamp(once);

        Assert.True(twice.Stamped);
        Assert.Equal(once, twice.StampedText);
        Assert.Empty(twice.Dangling);
    }

    [Fact]
    public void CRLF_input_stays_CRLF_and_LF_input_stays_LF()
    {
        var lf = File(1, A());
        Assert.DoesNotContain("\r\n", EnumerationStamper.Stamp(lf).StampedText!, StringComparison.Ordinal);

        var crlf = lf.Replace("\n", "\r\n");
        var stamped = EnumerationStamper.Stamp(crlf).StampedText!;

        // Every newline is a CRLF and none is a bare LF: this repo's tracked text is CRLF, and a stamper
        // that quietly rewrote a whole file to LF would show in `git diff --stat` as a two-line edit.
        Assert.Contains("\r\n", stamped, StringComparison.Ordinal);
        Assert.Equal(stamped.Count(c => c == '\n'), stamped.Split("\r\n").Length - 1);
    }

    // ---- 🔴 THE ONE THAT MATTERS -------------------------------------------------------------------

    /// <summary>
    /// *** RE-STAMPING AFTER A TEXT EDIT MUST NOT PRESERVE THE OLD ID. ***
    ///
    /// <para>§3.2's silent-survival failure, and the single most likely helpful-implementation defect
    /// here: a stamper that "kept" an existing ID would let a citation written against the OLD words
    /// survive a rewording nobody re-read.</para>
    /// </summary>
    [Fact]
    public void An_ID_whose_text_CHANGED_is_replaced_and_never_preserved()
    {
        var stale = AssertionId.Compute("REQ-1", "WHEN the level is high THEN the alarm is asserted");
        var edited = File(1, A(text: "WHEN the level has been high for the persistence time THEN the alarm is asserted", id: stale));

        var result = EnumerationStamper.Stamp(edited);

        Assert.True(result.Stamped);
        var assertion = Assert.Single(result.Assertions);

        Assert.NotEqual(stale, assertion.Id);
        Assert.Equal(stale, assertion.PreviousId);
        Assert.True(assertion.ReStamped);
        Assert.Equal(AssertionId.Compute("REQ-1", assertion.NormalisedText), assertion.Id);

        // The stale ID is GONE from the published file — a citation to it must dangle, not resolve.
        Assert.DoesNotContain("        id: " + stale, result.StampedText!, StringComparison.Ordinal);
        Assert.Contains("        id: " + assertion.Id, result.StampedText!, StringComparison.Ordinal);
    }

    [Fact]
    public void A_re_stamp_writes_a_supersedes_link_so_the_change_is_a_visible_event()
    {
        var stale = AssertionId.Compute("REQ-1", "WHEN the level is high THEN the alarm is asserted");
        var result = EnumerationStamper.Stamp(File(1, A(text: "WHEN the level has been high THEN the alarm is asserted", id: stale)));

        Assert.Contains("        supersedes: " + stale, result.StampedText!, StringComparison.Ordinal);
        Assert.Single(result.Dangling);
        Assert.Contains("RE-STAMPED", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void A_re_stamp_does_not_stack_duplicate_supersedes_lines()
    {
        var first = EnumerationStamper.Stamp(File(1, A(text: "WHEN a THEN b", id: "REQ-1:000000"))).StampedText!;
        var second = EnumerationStamper.Stamp(first);

        Assert.True(second.Stamped);
        Assert.Equal(1, second.StampedText!.Split("supersedes:").Length - 1);
    }

    [Fact]
    public void An_ID_whose_text_did_NOT_change_is_left_exactly_as_it_was()
    {
        var correct = AssertionId.Compute("REQ-1", "WHEN a THEN b");
        var result = EnumerationStamper.Stamp(File(1, A(text: "WHEN a THEN b", id: correct)));

        var assertion = Assert.Single(result.Assertions);
        Assert.False(assertion.ReStamped);
        Assert.DoesNotContain("supersedes:", result.StampedText!, StringComparison.Ordinal);
    }

    // ---- REFUSALS ----------------------------------------------------------------------------------

    /// <summary>
    /// *** THE PARSE'S ONLY CROSS-CHECK ON ITSELF. *** A line-oriented reader that saw fewer assertions
    /// than the file has would stamp a subset and report success, which is the shape of every silent-loss
    /// defect this project has found.
    /// </summary>
    [Fact]
    public void A_declared_count_that_disagrees_with_what_was_parsed_stamps_NOTHING()
    {
        var result = EnumerationStamper.Stamp(File(declared: 3, A(ordinal: "A1"), A(ordinal: "A2")));

        Assert.False(result.Stamped);
        Assert.Contains(result.Errors, e => e.Contains("declares 3", StringComparison.Ordinal));
    }

    [Fact]
    public void A_file_with_no_declared_count_is_refused_because_the_parse_could_not_be_checked()
    {
        var text = File(1, A()).Replace("  assertions: 1", "  # nothing here");

        var result = EnumerationStamper.Stamp(text);

        Assert.False(result.Stamped);
        Assert.Contains(result.Errors, e => e.Contains("ONLY CROSS-CHECK", StringComparison.Ordinal));
    }

    /// <summary>Two assertions in one clause that normalise identically are a DUPLICATE, not a collision (§3.1).</summary>
    [Fact]
    public void Two_identical_assertions_in_one_clause_are_a_duplicate_and_a_refusal()
    {
        var result = EnumerationStamper.Stamp(File(2,
            A(ordinal: "A1", text: "WHEN a THEN b"),
            A(ordinal: "A2", text: "WHEN a THEN b")));

        Assert.False(result.Stamped);
        Assert.Contains(result.Errors, e => e.Contains("DUPLICATE", StringComparison.Ordinal));
    }

    [Fact]
    public void The_same_text_in_two_DIFFERENT_clauses_is_two_assertions_and_is_fine()
    {
        var result = EnumerationStamper.Stamp(File(2,
            A(clause: "REQ-1", text: "WHEN a THEN b"),
            A(clause: "REQ-2", text: "WHEN a THEN b")));

        Assert.True(result.Stamped);
        Assert.NotEqual(result.Assertions[0].Id, result.Assertions[1].Id);
    }

    [Fact]
    public void A_normalised_text_that_is_not_normalised_is_refused_rather_than_normalised_here()
    {
        // Normalising it silently would change the ID that gets published, and the enumerator would
        // never know its own artifact had been edited.
        var text = File(1, A()).Replace("normalised_text: \"WHEN a THEN b\"", "normalised_text: \"WHEN a  THEN b.\"");

        var result = EnumerationStamper.Stamp(text);

        Assert.False(result.Stamped);
        Assert.Contains(result.Errors, e => e.Contains("not itself normalised", StringComparison.Ordinal));
    }

    [Fact]
    public void A_normalised_text_that_is_not_the_normalisation_of_its_own_text_is_refused()
    {
        var text = File(1, A()).Replace("normalised_text: \"WHEN a THEN b\"", "normalised_text: \"WHEN c THEN d\"");

        var result = EnumerationStamper.Stamp(text);

        Assert.False(result.Stamped);
        Assert.Contains(result.Errors, e => e.Contains("does not normalise to", StringComparison.Ordinal));
    }

    [Fact]
    public void A_block_scalar_normalised_text_is_refused_rather_than_approximated()
    {
        var text = File(1, A()).Replace("normalised_text: \"WHEN a THEN b\"", "normalised_text: >-");

        var result = EnumerationStamper.Stamp(text);

        Assert.False(result.Stamped);
        Assert.Contains(result.Errors, e => e.Contains("block scalar", StringComparison.Ordinal));
    }

    [Fact]
    public void A_text_that_does_not_wear_its_declared_form_is_refused()
    {
        var result = EnumerationStamper.Stamp(File(1, A(form: "Never", text: "WHEN a THEN b")));

        Assert.False(result.Stamped);
        Assert.Contains(result.Errors, e => e.Contains("does not wear its declared form", StringComparison.Ordinal));
    }

    [Fact]
    public void An_unknown_form_is_refused_rather_than_defaulted()
    {
        var result = EnumerationStamper.Stamp(File(1, A(form: "Whenever")));

        Assert.False(result.Stamped);
        Assert.Contains(result.Errors, e => e.Contains("neither When nor Never", StringComparison.Ordinal));
    }

    [Fact]
    public void An_empty_file_is_refused_and_names_the_unstamped_consequence()
    {
        var result = EnumerationStamper.Stamp("");

        Assert.False(result.Stamped);
        Assert.Contains("nothing citable", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void A_file_with_no_clauses_key_is_refused_rather_than_matched_loosely()
    {
        var result = EnumerationStamper.Stamp("enumerator: someone\ndenominator:\n  assertions: 1\n");

        Assert.False(result.Stamped);
        Assert.Contains(result.Errors, e => e.Contains("`clauses:`", StringComparison.Ordinal));
    }

    [Fact]
    public void A_refusal_leaves_NO_stamped_text_at_all()
    {
        // The CLI writes StampedText; a refusal that still produced one would publish a file the stamper
        // had already said it could not vouch for.
        var result = EnumerationStamper.Stamp(File(declared: 5, A()));

        Assert.False(result.Stamped);
        Assert.Null(result.StampedText);
        Assert.Empty(result.Assertions);
    }
}
