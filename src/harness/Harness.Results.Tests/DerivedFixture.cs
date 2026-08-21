using System.Text.Json;
using Harness.Gate;
using Harness.Results;

namespace Harness.Results.Tests;

/// <summary>
/// Turns an authored submission fixture into a DERIVED one, the way the real tool would.
///
/// <para>🔴 <b>WHY THIS EXISTS RATHER THAN A HAND-WRITTEN <c>derivation</c> BLOCK IN EACH FIXTURE.</b>
/// A fixture that hand-writes provenance is asserting what the deriver emits, and the two can drift —
/// at which point every gate-0c test passes against a format the tool does not produce. The producer
/// names and the hash come from the production path (<see cref="DeriveCli.DefaultProducerFor"/>,
/// <see cref="DerivationHash"/>), so a change to either breaks these fixtures, which is the point.</para>
///
/// <para><b>It does not round-trip the document.</b> The derivable set is read by deserialising, but the
/// JSON handed to the gate is the ORIGINAL TEXT with one key inserted. Re-serialising would quietly
/// normalise the very fixtures whose exact text other gates are being tested against — annotations,
/// explicit nulls, key presence — and a fixture that changed under the test is not the fixture the test
/// names.</para>
/// </summary>
internal static class DerivedFixture
{
    /// <summary>
    /// Add a provenance record for every derivable field <paramref name="json"/> carries, all attributed
    /// to one artifact whose content is <paramref name="artifactContent"/>.
    /// </summary>
    public static string WithDerivation(string json, string artifactPath, string artifactContent)
    {
        // *** A FIXTURE THAT IS NOT A SUBMISSION IS RETURNED UNTOUCHED, AND THAT IS NOT A SILENT SKIP. ***
        // Several tests here feed the CLI something deliberately unreadable to prove it exits 2 rather
        // than treating it as admissible. There is genuinely nothing to attribute in a document that does
        // not parse, and stamping one would destroy the very input the test is named after.
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
