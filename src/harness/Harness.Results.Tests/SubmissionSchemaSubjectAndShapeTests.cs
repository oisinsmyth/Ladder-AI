using Harness.Gate;
using Harness.Results;

namespace Harness.Results.Tests;

/// <summary>
/// The two document-schema additions of 2026-08-18, and the property they share: <b>a field the parser
/// does not know is REFUSED by gate 0b, so mapping it is what makes it writable at all.</b>
///
/// <para>Both were blocked on exactly that. <c>enumerations</c> could not be expressed, so a two-subject
/// campaign had to merge its denominators; <c>temporalShape</c> could not be expressed, so a vector using
/// it was refused along with the whole submission it travelled in.</para>
/// </summary>
public class SubmissionSchemaSubjectAndShapeTests
{
    private const string ClauseId = "REQ-014";
    private const string AssertionText = "WHEN the step is applied THEN the count reaches the limit";
    private static readonly string AssertionIdValue = AssertionId.Compute(ClauseId, AssertionText);

    // ---------------------------------------------------------------------------------------------
    // temporalShape — the observation-window model's last two hops
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>BEFORE THIS MAPPING, A VECTOR CARRYING <c>temporalShape</c> WAS NOT IGNORED — IT WAS REFUSED,
    /// AND THE WHOLE SUBMISSION WITH IT.</b>
    ///
    /// <para>Gate 0b names any field the schema does not read, because <i>"a silently-ignored field is
    /// worse than a rejected one, since it reads as accepted"</i>. That is why
    /// <c>docs/notes/observation-window-shapes.md §7</c> fixes the sequencing: the parser learns the field
    /// before any author may write it.</para>
    /// </summary>
    [Fact]
    public void A_VECTOR_DECLARING_A_TEMPORAL_SHAPE_IS_NOT_AN_UNKNOWN_FIELD()
    {
        var document = SubmissionDocument.Read(Submission("\"temporalShape\": \"throughout\","));

        Assert.Empty(document.UnknownFieldPaths());
    }

    /// <summary>
    /// <b>And it reaches the checked type</b> — mapped and then carried, because a field the parser knows
    /// and the projection drops is the silently-ignored field one layer in.
    /// </summary>
    [Theory]
    [InlineData("throughout", TemporalShape.Throughout)]
    [InlineData("atSomePoint", TemporalShape.AtSomePoint)]
    [InlineData("becomesAndHolds", TemporalShape.BecomesAndHolds)]
    [InlineData("atEnd", TemporalShape.AtEnd)]
    [InlineData("atNoPoint", TemporalShape.AtNoPoint)]
    public void EVERY_DECLARED_SHAPE_REACHES_THE_OBSERVABILITY_DECLARATION(string written, TemporalShape expected)
    {
        var document = SubmissionDocument.Read(Submission($"\"temporalShape\": \"{written}\","));
        var vector = GateCli.ToSubmissionVector(document.Vectors![0]);

        Assert.Equal(expected, Assert.Single(vector.Expectations).Shape);
    }

    /// <summary>
    /// 🔴 <b>ABSENT IS <c>Unstated</c>, WHICH REPRODUCES THE PREVIOUS FOLD EXACTLY.</b> Every vector
    /// written before the field existed carries this, including vectors already run against a real job — a
    /// silent change of verdict here would rewrite the meaning of results already recorded.
    /// </summary>
    [Fact]
    public void AN_EXPECTATION_THAT_DECLARES_NO_SHAPE_IS_UNSTATED()
    {
        var document = SubmissionDocument.Read(Submission());
        var vector = GateCli.ToSubmissionVector(document.Vectors![0]);

        Assert.Equal(TemporalShape.Unstated, Assert.Single(vector.Expectations).Shape);
    }

    /// <summary>
    /// ⚠️ <b>A MISSPELT SHAPE IS A PARSE REFUSAL, NOT A SILENT <c>Unstated</c>.</b>
    ///
    /// <para>The design note's §7 reads "unrecognised ⇒ Unstated", which is right about the EVALUATOR and
    /// would be wrong at the parser: the very next paragraph requires that <i>"a dropped field must fail
    /// the same comparison a wrong one does"</i>, and coercing a typo to the default would hand the author
    /// who misspelt it the one shape that never accuses. <c>nature</c> and <c>mode</c> have always been
    /// refused this way; this is the same treatment, not a new one.</para>
    /// </summary>
    [Fact]
    public void A_MISSPELT_SHAPE_IS_REFUSED_RATHER_THAN_QUIETLY_DEFAULTED()
    {
        Assert.ThrowsAny<Exception>(() => SubmissionDocument.Read(Submission("\"temporalShape\": \"throughoutt\",")));
    }

    // ---------------------------------------------------------------------------------------------
    // enumerations — plural, each with a subject
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_SUBMISSION_CAN_CARRY_TWO_ENUMERATIONS_WITH_DISTINCT_SUBJECTS()
    {
        var set = GateCli.ToEnumerationSet(SubmissionDocument.Read(TwoEnumerations()));

        Assert.Equal(2, set.Enumerations.Count);
        Assert.Equal(new[] { "PUMP", "TANK" }, set.Subjects);
        Assert.Equal(new[] { "SHARED-7" }, set.SharedClauses());
    }

    /// <summary>
    /// <b>An unknown field in the SECOND enumeration is named, not dropped.</b> It is exactly as silently
    /// ignored as one in the first, and it is the one nobody would go looking for.
    /// </summary>
    [Fact]
    public void AN_UNKNOWN_FIELD_IN_THE_SECOND_ENUMERATION_IS_NAMED()
    {
        var json = TwoEnumerations(secondExtra: "\"enumeratorr\": \"agent-c\",");
        var paths = SubmissionDocument.Read(json).UnknownFieldPaths();

        Assert.Contains("enumerations[1].enumeratorr", paths);
    }

    /// <summary>
    /// 🔴 <b>BOTH KEYS AT ONCE IS TWO ANSWERS TO ONE QUESTION, AND NEITHER CAN BE PREFERRED.</b>
    ///
    /// <para>Choosing one would silently discard a coverage denominator somebody wrote down; merging them
    /// would produce a denominator belonging to no subject, which is the defect subjects exist to remove.
    /// Both CLIs wrap this read and report NOTHING EXAMINED naming the contradiction.</para>
    /// </summary>
    [Fact]
    public void SUPPLYING_BOTH_ENUMERATION_AND_ENUMERATIONS_IS_REFUSED_BY_NAME()
    {
        var json = TwoEnumerations(alsoSingular: true);
        var document = SubmissionDocument.Read(json);

        var thrown = Assert.Throws<InvalidDataException>(() => GateCli.ToEnumerationSet(document));

        Assert.Contains("enumeration", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("enumerations", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NEITHER_KEY_IS_AN_EMPTY_SET_RATHER_THAN_AN_EMPTY_ENUMERATION()
    {
        var document = SubmissionDocument.Read("{ \"blockAuthor\": \"agent-a\" }");
        var set = GateCli.ToEnumerationSet(document);

        // Different facts: "nobody supplied one" (gate 3j, NOTHING EXAMINED) versus "one was supplied and
        // it is empty" (gate 3, EnumerationEmpty). The first is the one that is true here.
        Assert.True(set.IsEmpty);
        Assert.Empty(set.Enumerations);
    }

    /// <summary>
    /// <b>A vector's <c>subject</c> reaches its <c>Basis</c></b>, and a vector that declares none carries
    /// none — which is what makes an unqualified citation resolvable exactly as it was.
    /// </summary>
    [Fact]
    public void A_VECTORS_SUBJECT_REACHES_ITS_BASIS()
    {
        var qualified = SubmissionDocument.Read(Submission(vectorSubject: "\"subject\": \"TANK\","));
        Assert.Equal("TANK", GateCli.ToSubmissionVector(qualified.Vectors![0]).Basis!.Subject);

        var plain = SubmissionDocument.Read(Submission());
        Assert.Null(GateCli.ToSubmissionVector(plain.Vectors![0]).Basis!.Subject);
    }

    // ---------------------------------------------------------------------------------------------

    private static string Submission(string? expectationExtra = null, string? vectorSubject = null) => $$"""
    {
      "blockAuthor": "agent-a",
      "enumeration": { "clauses": ["{{ClauseId}}"], "assertions": ["{{AssertionIdValue}}"], "enumerator": "agent-c" },
      "vectors": [{
        "id": "V-1",
        "slot": "S0",
        "author": "agent-b",
        "clause": "{{ClauseId}}",
        "assertion": "{{AssertionIdValue}}",
        {{vectorSubject ?? string.Empty}}
        "startBool": "Demo_Start",
        "expectations": [
          { "signal": "Count", "nature": "PersistentState", "mode": "Sampled", "windowScans": 20, "expected": "10", {{expectationExtra ?? string.Empty}} "_note": "the shape is the field under test" }
        ],
        "completionSignal": "Done"
      }]
    }
    """;

    private static string TwoEnumerations(string? secondExtra = null, bool alsoSingular = false) => $$"""
    {
      "blockAuthor": "agent-a",
      {{(alsoSingular ? "\"enumeration\": { \"clauses\": [\"REQ-201\"], \"assertions\": [\"REQ-201:aaaaaa\"] }," : string.Empty)}}
      "enumerations": [
        { "subject": "PUMP", "clauses": ["REQ-201", "SHARED-7"], "assertions": ["REQ-201:aaaaaa"], "enumerator": "agent-c" },
        { "subject": "TANK", "clauses": ["REQ-305", "SHARED-7"], "assertions": ["REQ-305:bbbbbb"], {{secondExtra ?? string.Empty}} "enumerator": "agent-c" }
      ],
      "vectors": []
    }
    """;
}
