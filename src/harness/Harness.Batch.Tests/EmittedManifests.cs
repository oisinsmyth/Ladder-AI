using Harness.Batch;
using Harness.Map;

namespace Harness.Batch.Tests;

/// <summary>
/// 🔴 <b>A manifest that was EMITTED, for tests that need one — written because a fixture that hand-built
/// a <see cref="LaneManifest"/> was proving the wrong thing.</b>
///
/// <para><c>enqueue --manifest</c> used to accept any well-formed document and report the program set as
/// <c>DERIVED</c>, because the word was keyed on which FLAG was passed. Two test fixtures leaned on that:
/// they constructed a <c>LaneManifest</c> by hand, over paths that did not exist, and asserted the
/// <c>DERIVED</c> wording came back. Both passed, and neither exercised a manifest anything had emitted.</para>
///
/// <para><b>So this writes real <c>.ir</c> files and derives the manifest against a stamp record carrying
/// their real hashes</b> — the same tie <c>BatchCli.Manifest</c> makes. The SHA-256 comes from
/// <see cref="ProgramManifestEntry.HashOf"/>, the production rule, so a fixture cannot agree with a
/// production hash that has drifted.</para>
/// </summary>
internal static class EmittedManifests
{
    /// <summary>One object to write and stamp: what it is called, what it is, and its IR text.</summary>
    internal sealed record Object(string Name, HarnessObjectKind Kind, string Ir);

    /// <summary>
    /// Writes each object to <c>&lt;directory&gt;/&lt;Name&gt;.ir</c> and returns a manifest derived over
    /// them, tied to a stamp record that hashed exactly those objects.
    /// </summary>
    /// <param name="blockUnderTest">The subject, or null. It must be one of <paramref name="objects"/> and a Block.</param>
    internal static LaneManifest Derive(
        string lane, string directory, string? blockUnderTest, params Object[] objects)
    {
        Directory.CreateDirectory(directory);

        var emitted = new List<EmittedObject>();
        var hashed = new List<ProgramManifestEntry>();

        foreach (var obj in objects)
        {
            var path = Path.Combine(directory, obj.Name + ".ir");
            File.WriteAllText(path, obj.Ir);

            emitted.Add(new EmittedObject(obj.Name, path, obj.Kind));
            hashed.Add(new ProgramManifestEntry(obj.Kind.ToString(), obj.Name, ProgramManifestEntry.HashOf(obj.Ir)));
        }

        // No copy layer: these fixtures are about the program set and the tie, and an unhashed copy-layer
        // row would only add rows to every count they assert.
        return LaneManifest.Derive(
            lane, emitted, Array.Empty<EmittedObject>(), blockUnderTest,
            new ProgramManifest(hashed, Array.Empty<string>(), 0x1234ABCD));
    }

    /// <summary>The same, written to a file, returning the path an <c>enqueue --manifest</c> takes.</summary>
    internal static string Write(
        string lane, string directory, string manifestPath, string? blockUnderTest, params Object[] objects)
    {
        File.WriteAllText(manifestPath, Derive(lane, directory, blockUnderTest, objects).ToJson());
        return manifestPath;
    }
}
