using System.Text.Json;
using Harness.Gate;
using Harness.Results;

namespace Harness.Loop.Tests;

/// <summary>
/// Turns an authored submission fixture into a DERIVED one, the way the real tool would.
///
/// <para>🔴 <b>WHY THIS EXISTS RATHER THAN A HAND-WRITTEN <c>derivation</c> BLOCK IN EACH FIXTURE.</b>
/// A fixture that hand-writes provenance is asserting what the deriver emits, and the two can drift —
/// at which point every gate-0c test passes against a format the tool does not produce. The producer
/// names and the hash come from the production path (<see cref="DeriveCli.DefaultProducerFor"/>,
/// <see cref="DerivationHash"/>), so a change to either breaks these fixtures, which is the point.</para>
///
/// <para><b>It does not round-trip the document.</b> The derivable set is read by parsing, but the JSON
/// handed on is the ORIGINAL TEXT with one key inserted. Re-serialising would quietly normalise the very
/// fixtures whose exact text these tests are written against — key presence, explicit nulls, the
/// settling field spliced in by the caller — and a fixture that changed under the test is not the
/// fixture the test names.</para>
///
/// <para><b>Deliberately a second copy of the one in <c>Harness.Results.Tests</c>.</b> The two assemblies
/// share no test-utility project, and inventing one to hold eight lines would be a bigger change than
/// the duplication. Both call the same production methods, so neither can drift from the tool — only
/// from each other, which no test depends on.</para>
/// </summary>
internal static class DerivedFixture
{
    /// <summary>The artifact every fixture record points at. One name, so the reader below is trivial.</summary>
    public const string ArtifactPath = "derivation-artifact.json";

    /// <summary>
    /// A reader that serves the derivation artifact and nothing else.
    ///
    /// <para><b>It answers every path with the same content on purpose.</b> These fixtures name no tag
    /// map, so the artifact is the only file anything asks for; a switch would add a branch no test
    /// exercises. If a fixture ever does name a second file, this returns the wrong bytes and gate 0c's
    /// hash check is what fails — loudly, which is the correct direction.</para>
    /// </summary>
    public static Func<string, string> ReaderFor(string artifactContent) => _ => artifactContent;

    /// <summary>
    /// Add a provenance record for every derivable field <paramref name="json"/> carries, all attributed
    /// to one artifact whose content is <paramref name="artifactContent"/>.
    /// </summary>
    public static string WithDerivation(string json, string artifactPath, string artifactContent)
    {
        // A fixture that is not a submission is returned untouched: several tests feed the CLI something
        // deliberately unreadable to prove it refuses, and there is nothing to attribute in a document
        // that does not parse.
        SubmissionDocument document;
        try
        {
            document = SubmissionDocument.Read(json);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            return json;
        }

        var hash = DerivationHash.Of(artifactContent);

        var entries = document.DerivableFieldsPresent().Select(field =>
            $$"""
              {"field":"{{field}}","producer":"{{DeriveCli.DefaultProducerFor(field)}}","artifact":"{{artifactPath}}","artifactSha256":"{{hash}}"}
              """.Trim());

        var trimmed = json.TrimStart();
        var brace = trimmed.IndexOf('{');
        if (brace < 0)
            return json;

        return trimmed.Insert(brace + 1, $"\"derivation\":[{string.Join(",", entries)}],");
    }
}
