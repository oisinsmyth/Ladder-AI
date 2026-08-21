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
        // Every derivable field is attributed, whether or not this submission carries it — an artifact
        // for an absent field is simply unused. Enumerating the registry rather than listing the fields
        // by hand means a NEW derivable field joins this test the day it is added, instead of quietly
        // being the one thing the end-to-end proof does not cover.
        var artifacts = DerivableField.All
            .SelectMany(f => new[] { "--artifact", $"{f}=binding.json" })
            .ToArray();

        var (exit, output, written) = Derive(
            new[] { "--submission", "sub.json", "--out", "derived.json" }.Concat(artifacts).ToArray());

        Assert.Equal(DeriveExit.Derived, exit);
        Assert.Contains("EXAMINED:", output, StringComparison.Ordinal);
        Assert.Contains("DERIVED:", output, StringComparison.Ordinal);

        // The gate, over the deriver's own output. No hand-written provenance anywhere in this path.
        var writer = new StringWriter();
        var gateExit = GateCli.Run(new[] { "check", "derived.json", "--binding", "binding.json" }, writer,
            path => path switch
            {
                "derived.json" => written["derived.json"],
                "tags.json" => GateCliTests.TagMap,
                _ => GateCliTests.Binding,
            });

        Assert.Equal(GateExit.AdmissibleSubjectToJudgement, gateExit);
        Assert.Contains("0c derived fields", writer.ToString(), StringComparison.Ordinal);
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
            "--submission", "sub.json", "--out", "derived.json", "--withhold-unattributable",
            "--artifact", $"{DerivableField.RuntimeCompression}=binding.json");

        Assert.Equal(DeriveExit.WithheldSomething, exit);
        Assert.Contains("WITHHELD   " + DerivableField.Map, output, StringComparison.Ordinal);
        Assert.Contains("WITHHELD BY NAME", output, StringComparison.Ordinal);

        // Withheld means GONE, not quietly emptied: the gates that consumed it now report NOT CHECKED,
        // which is the true state of affairs and is loud.
        var derived = SubmissionDocument.Read(written["derived.json"]);
        Assert.DoesNotContain(DerivableField.Map, derived.DerivableFieldsPresent(), StringComparer.Ordinal);
    }

    [Fact]
    public void RUNTIME_COMPRESSION_CANNOT_BE_WITHHELD_AND_THE_REFUSAL_EXPLAINS_WHY()
    {
        // 🔴 It has a DEFAULT. Removing the key leaves the value 1 in place, so "withholding" it would not
        // withhold anything — it would forge the quieter claim that the run was uncompressed.
        var (exit, output, written) = Derive(
            "--submission", "sub.json", "--out", "derived.json", "--withhold-unattributable",
            "--artifact", $"{DerivableField.Map}=binding.json");

        Assert.Equal(DeriveExit.Refused, exit);
        Assert.Contains("runtimeCompression cannot be withheld", output, StringComparison.Ordinal);
        Assert.Empty(written);
    }

    // ---------------------------------------------------------------------------------------------
    // fixtures
    // ---------------------------------------------------------------------------------------------

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
                _ => throw new FileNotFoundException(path),
            },
            (path, content) => written[path] = content);

        return (exit, writer.ToString(), written);
    }
}
