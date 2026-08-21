using Harness.Gate;
using Harness.Results;
using Xunit;

namespace Harness.Results.Tests;

/// <summary>
/// 🔴 <b><c>harness-gate derive</c> — the tool that makes gate 0c satisfiable, and the only thing that
/// writes a provenance record.</b>
///
/// <para><b>The load-bearing test is <see cref="A_DERIVED_SUBMISSION_IS_ADMISSIBLE_END_TO_END"/>.</b>
/// Everything else here checks a refusal, and a tool that refuses everything passes every refusal test
/// ever written. That one runs the real deriver and then the real gate over its real output, which is
/// the only evidence that the pair actually composes.</para>
/// </summary>
public class DeriveCliTests
{
    // ---------------------------------------------------------------------------------------------
    // the pair, end to end
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_DERIVED_SUBMISSION_IS_ADMISSIBLE_END_TO_END()
    {
        // Every derivable field that TAKES an artifact, whether or not this submission carries it — an
        // artifact for an absent field is simply unused. Enumerating the registry rather than listing
        // fields by hand means a NEW derivable field joins this test the day it is added, instead of
        // quietly being the one thing the end-to-end proof does not cover.
        //
        // Fields whose producer emits NO artifact are excluded, and supplying one for them is its own
        // refusal — see the by-rule tests below.
        var artifacts = DerivableField.All
            .Where(f => DerivationProducer.KindFor(DeriveCli.DefaultProducerFor(f)) != ArtifactKind.None)
            .SelectMany(f => new[] { "--artifact", $"{f}={ArtifactFor(f)}" })
            .ToArray();

        var (exit, output, written) = Derive(
            new[] { "--submission", "sub.json", "--out", "derived.json" }.Concat(artifacts).ToArray());

        // The tool's own output is the failure message. A bare Assert.Equal here says only "0 != 1",
        // which for a tool whose entire job is explaining WHY it refused is a wasted diagnostic.
        Assert.True(exit == DeriveExit.Derived, output);
        Assert.Contains("EXAMINED:", output, StringComparison.Ordinal);
        Assert.Contains("DERIVED:", output, StringComparison.Ordinal);

        // The strong claims, named: the map was RECOMPUTED from the binding and matched, and the
        // by-rule field was settled without an artifact. Asserting only "exit 0" would pass just as
        // happily if every field had been merely cited.
        Assert.Contains("COMPUTED   " + DerivableField.Map, output, StringComparison.Ordinal);
        Assert.Contains("BY RULE    " + DerivableField.RuntimeCompression, output, StringComparison.Ordinal);
        Assert.Contains("recomputed and matched", output, StringComparison.Ordinal);

        // The gate, over the deriver's own output. No hand-written provenance anywhere in this path.
        // 🔴 The gate must be handed the SAME artifacts the deriver hashed. Serving one file for every
        // path made every record read as STALE - which is the hash check doing exactly its job, on a
        // fixture that was lying to it.
        var writer = new StringWriter();
        var gateExit = GateCli.Run(new[] { "check", "derived.json", "--binding", "binding.json" }, writer,
            path => path switch
            {
                "derived.json" => written["derived.json"],
                "tags.json" => GateCliTests.TagMap,
                "reachable.json" => ReachableState,
                "conflicts.json" => ConflictGraph,
                "deploy.json" => DeployResult,
                _ => GateCliTests.Binding,
            });

        Assert.True(gateExit == GateExit.AdmissibleSubjectToJudgement, writer.ToString());
        Assert.Contains("0c derived fields", writer.ToString(), StringComparison.Ordinal);

        // And the gate's own denominator must show the strong claim, not just a count of records.
        Assert.Contains("recomputed and matched", writer.ToString(), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // empty is not clean
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_SUBMISSION_WITH_NO_DERIVABLE_FIELD_IS_NOTHING_DERIVED_AND_NOT_EXIT_ZERO()
    {
        // *** A RUN THAT STAMPED NOTHING MUST NOT REPORT SUCCESS. *** Exit 0 here would read to a caller
        // as "the provenance is complete", which is the exact shape of green this project keeps having to
        // retract — a check that examined nothing and said so in a way nobody noticed.
        var (exit, output, written) = DeriveOver(
            """{ "blockAuthor": "agent-a", "vectors": [] }""",
            "--submission", "sub.json", "--out", "derived.json");

        Assert.Equal(DeriveExit.NothingDerived, exit);
        Assert.Contains("NOTHING DERIVED - this is not a pass.", output, StringComparison.Ordinal);
        Assert.Empty(written);
    }

    // ---------------------------------------------------------------------------------------------
    // refusals
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_FIELD_WITH_NO_ARTIFACT_IS_REFUSED_AND_NOTHING_IS_WRITTEN()
    {
        // Partial provenance is the dangerous middle: the submission would pass gate 0c for whatever the
        // run happened to cover and be refused only on the rest, so writing it at all invites a re-run
        // that looks like progress. Nothing is written.
        var (exit, output, written) = Derive(
            "--submission", "sub.json", "--out", "derived.json",
            "--artifact", $"{DerivableField.Map}=binding.json");

        Assert.Equal(DeriveExit.Refused, exit);
        Assert.Contains("REFUSED    " + DerivableField.Deployment, output, StringComparison.Ordinal);
        Assert.Contains("Nothing was written", output, StringComparison.Ordinal);
        Assert.Empty(written);
    }

    [Fact]
    public void AN_ARTIFACT_THAT_CANNOT_BE_READ_IS_A_REFUSAL_RATHER_THAN_A_STAMPED_HASH()
    {
        // The deriver refuses where the gate only reports: the gate finds a record that already exists
        // and says it could not verify it; here the record is being CREATED, and stamping a hash for a
        // file we could not open would be manufacturing the evidence.
        var (exit, output, _) = Derive(
            "--submission", "sub.json", "--out", "derived.json",
            "--artifact", $"{DerivableField.Map}=missing.json");

        Assert.Equal(DeriveExit.Refused, exit);
        Assert.Contains("REFUSED    " + DerivableField.Map, output, StringComparison.Ordinal);
    }

    [Fact]
    public void AN_ARTIFACT_FLAG_FOR_A_FIELD_THAT_IS_NOT_DERIVABLE_IS_REFUSED_BY_NAME()
    {
        var (exit, output, _) = Derive(
            "--submission", "sub.json", "--out", "derived.json",
            "--artifact", "vectors=binding.json");

        Assert.Equal(DeriveExit.Refused, exit);
        Assert.Contains("'vectors' is not a derivable field", output, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // withholding
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void WITHHOLDING_REMOVES_THE_FIELD_NAMES_IT_AND_EXITS_THREE()
    {
        var (exit, output, written) = Derive(
            "--submission", "sub.json", "--out", "derived.json", "--withhold-unattributable");

        Assert.Equal(DeriveExit.WithheldSomething, exit);
        Assert.Contains("WITHHELD   " + DerivableField.Map, output, StringComparison.Ordinal);
        Assert.Contains("WITHHELD BY NAME", output, StringComparison.Ordinal);

        // Withheld means GONE, not quietly emptied: the gates that consumed it now report NOT CHECKED,
        // which is the true state of affairs and is loud.
        var derived = SubmissionDocument.Read(written["derived.json"]);
        Assert.DoesNotContain(DerivableField.Map, derived.DerivableFieldsPresent(), StringComparer.Ordinal);
    }

    [Fact]
    public void RUNTIME_COMPRESSION_IS_SETTLED_BY_RULE_AND_NEVER_ENTERS_WITHHOLDING()
    {
        // 🔴 *** IT HAS A DEFAULT, SO IT CANNOT BE WITHHELD AT ALL: *** removing the key leaves the value
        // 1 in place, which would not withhold anything - it would forge the quieter claim that the run
        // was uncompressed. The protection is now STRUCTURAL rather than a guard: no artifact produces
        // this value, so it is settled by rule before the withholding path is ever reached.
        var (exit, output, written) = Derive(
            "--submission", "sub.json", "--out", "derived.json", "--withhold-unattributable");

        Assert.Equal(DeriveExit.WithheldSomething, exit);
        Assert.Contains("BY RULE    " + DerivableField.RuntimeCompression, output, StringComparison.Ordinal);
        Assert.DoesNotContain("WITHHELD   " + DerivableField.RuntimeCompression, output, StringComparison.Ordinal);

        // And it survives into the output, because withholding it was never the answer.
        var derived = SubmissionDocument.Read(written["derived.json"]);
        Assert.Contains(DerivableField.RuntimeCompression, derived.DerivableFieldsPresent(), StringComparer.Ordinal);
    }

    [Fact]
    public void AN_ARTIFACT_FOR_A_BY_RULE_FIELD_IS_REFUSED_because_no_file_can_be_its_source()
    {
        var (exit, output, written) = Derive(
            "--submission", "sub.json", "--out", "derived.json",
            "--artifact", $"{DerivableField.RuntimeCompression}=binding.json");

        Assert.Equal(DeriveExit.Refused, exit);
        Assert.Contains("BAD SOURCE " + DerivableField.RuntimeCompression, output, StringComparison.Ordinal);
        Assert.Contains("produces no artifact", output, StringComparison.Ordinal);
        Assert.Empty(written);
    }

    // ---------------------------------------------------------------------------------------------
    // fixtures
    // ---------------------------------------------------------------------------------------------

    // ---------------------------------------------------------------------------------------------
    // artifacts, one per KIND
    // ---------------------------------------------------------------------------------------------
    //
    // 🔴 Every artifact used to be `binding.json`, which passed while the deriver only hashed. It does
    // not any more, and that is the kind check working: a reachable-state field pointed at a binding is
    // WRONG KIND. One file per kind is now the minimum a derived submission needs.

    /// <summary>The closure the submission's declared storage path must appear in.</summary>
    private const string ReachableState = """
    { "blocks": [ { "block": "DemoUnit", "reachableState": [ "Demo_Count", "Demo_Done" ] } ] }
    """;

    /// <summary>A graph that RAN and found nothing — the earned claim the submission's empty list makes.</summary>
    private const string ConflictGraph = """
    { "edges": [] }
    """;

    /// <summary>A deployment that actually happened. Anything but <c>Ran</c> is a failure report.</summary>
    private const string DeployResult = """
    { "outcome": "Ran", "detail": "the wave ran." }
    """;

    private static string ArtifactFor(string field) => DerivationProducer.KindFor(DeriveCli.DefaultProducerFor(field)) switch
    {
        ArtifactKind.Binding => "binding.json",
        ArtifactKind.ReachableState => "reachable.json",
        ArtifactKind.ConflictGraph => "conflicts.json",
        ArtifactKind.DeployResult => "deploy.json",
        ArtifactKind.TagMap => "tags.json",
        _ => "binding.json",
    };

    private static (int Exit, string Output, Dictionary<string, string> Written) Derive(
        params string[] args) => DeriveOver(GateCliTests.Good, args);

    /// <summary>
    /// <b>Named differently from <see cref="Derive(string[])"/> rather than overloaded.</b> A
    /// <c>params</c> overload pair binds the first string to the non-params parameter, so
    /// <c>Derive("--submission", …)</c> silently passed the FLAG as the submission and every test failed
    /// on an unrecognised argument.
    /// </summary>
    private static (int Exit, string Output, Dictionary<string, string> Written) DeriveOver(
        string submission, params string[] args)
    {
        var writer = new StringWriter();
        var written = new Dictionary<string, string>(StringComparer.Ordinal);

        var exit = DeriveCli.Run(
            new[] { "derive" }.Concat(args).ToArray(),
            writer,
            path => path switch
            {
                "sub.json" => submission,
                "binding.json" => GateCliTests.Binding,
                "tags.json" => GateCliTests.TagMap,
                "reachable.json" => ReachableState,
                "conflicts.json" => ConflictGraph,
                "deploy.json" => DeployResult,
                _ => throw new FileNotFoundException(path),
            },
            (path, content) => written[path] = content);

        return (exit, writer.ToString(), written);
    }
}
