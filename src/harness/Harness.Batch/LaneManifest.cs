using System.Text.Json;
using System.Text.Json.Serialization;

namespace Harness.Batch;

/// <summary>One object a lane contributes to the deployed program.</summary>
/// <param name="Name">The object's name in the project — what TIA matches an import on.</param>
/// <param name="Path">The <c>.ir</c> file (or directory) it comes from.</param>
/// <param name="Origin">Who produced it: a generator, or a person. See <see cref="ObjectOrigin"/>.</param>
/// <param name="Role">
/// What this object IS in the lane. 🔴 <b>Present so a check can NAME the subject.</b>
/// <c>undriven-scan --fb &lt;name&gt;</c> needs the block under test, and a manifest that lists objects
/// without saying which one is the subject makes that check uncomposable — which is exactly why it had
/// not been composed. <b>Derived, never authored:</b> <c>SlotFcGenerator</c> is handed both the head and
/// the block under test and used to discard them (§3.1 — a field that can be derived must never be typed).
/// </param>
public sealed record ManifestObject(string Name, string Path, ObjectOrigin Origin, ObjectRole Role = ObjectRole.Unstated);

/// <summary>What an object is FOR in a conformance lane.</summary>
public enum ObjectRole
{
    /// <summary>Not recorded. Legitimate for a supporting object, and it means no check can key on it.</summary>
    Unstated = 0,

    /// <summary>The generated mirror copy layer.</summary>
    CopyLayer = 1,

    /// <summary>The two-CALL slot FC.</summary>
    SlotFc = 2,

    /// <summary>The stimulus head that drives the plant model.</summary>
    StimulusHead = 3,

    /// <summary>🔴 <b>The block being tested — the subject every per-block check has to be pointed at.</b></summary>
    BlockUnderTest = 4,
}

/// <summary>
/// 🔴 <b>Recorded because the two have different failure modes and different remedies.</b> A generated
/// object is wrong only if its generator is wrong, and the fix is one edit for every lane. An authored
/// one is wrong on its own, and the fix is a review. A manifest that flattened the distinction would make
/// "how much of this lane is still hand-built" unanswerable — which is the exact question Phase 4 exists
/// to move.
/// </summary>
public enum ObjectOrigin
{
    Unstated = 0,
    Generated = 1,
    Authored = 2,
}

/// <summary>
/// 🔴 <b>THE PROGRAM SET, EMITTED BY WHATEVER BUILT THE LANE — not typed on a command line.</b>
///
/// <para><c>Lane.ProgramPaths</c> is author-declared at enqueue, and the BUILD STAMP is computed over it
/// and means <i>"what is executing"</i>. So a mistyped, stale or short <c>--program</c> list produces a
/// stamp describing a program nobody deployed — and the same list feeds the reachability check and the
/// drift check, so one mistake weakens three things in the same direction. That is not a hypothetical:
/// a lane was pointed at a pre-fix <c>Main</c> by hand and the stamp went with it.</para>
///
/// <para><b>A manifest replaces the typing with an emission.</b> Whatever generated the lane knows exactly
/// which objects it produced, so it writes them down; <c>enqueue</c> then takes the program set from the
/// manifest instead of from a flag. Where no manifest exists the old path still works and <b>the report
/// says the set was DECLARED rather than DERIVED</b> — the weaker case stays visible instead of being
/// indistinguishable from the stronger one.</para>
///
/// <para><b>The call-site obligation travels with it</b> (<c>Harness.Map.SlotFcGenerator</c> emits one).
/// A generated FC nothing calls is deployed, loaded, healthy in every artifact, and never runs.</para>
/// </summary>
public sealed record LaneManifest(
    string LaneName,
    IReadOnlyList<ManifestObject> Objects,

    /// <summary>
    /// What a person must still do for this lane to actually execute, in their own words — chiefly the
    /// call sites a generator cannot create for itself. Empty is a legitimate answer and is written as
    /// an empty list rather than omitted, so "nothing is owed" is distinguishable from "nobody said".
    /// </summary>
    IReadOnlyList<string> Obligations)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Distinct program paths, in manifest order — what a lane's <c>ProgramPaths</c> becomes.</summary>
    public IReadOnlyList<string> ProgramPaths =>
        Objects.Select(o => o.Path).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    /// <summary>
    /// 🔴 <b>The block under test, or null.</b> Null is a real answer — an older manifest states no roles
    /// — and it means a per-block check <b>cannot be pointed at anything</b> and must say so rather than
    /// picking an object. <b>Two objects claiming the role is null too</b>: a lane tests one block, and
    /// guessing which would be worse than declining.
    /// </summary>
    public string? BlockUnderTest
    {
        get
        {
            var named = Objects.Where(o => o.Role == ObjectRole.BlockUnderTest).ToArray();
            return named.Length == 1 ? named[0].Name : null;
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    /// <summary>
    /// Reads a manifest, <b>refusing anything it cannot use rather than returning a thin one</b>. A
    /// manifest that parsed to zero objects would hand <c>enqueue</c> an empty program set, which is the
    /// silence this type exists to remove.
    /// </summary>
    public static LaneManifest Read(string path, Func<string, string> readFile)
    {
        ArgumentNullException.ThrowIfNull(readFile);

        LaneManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<LaneManifest>(readFile(path), Json);
        }
        catch (JsonException error)
        {
            throw new InvalidOperationException($"the lane manifest at {path} is not readable JSON: {error.Message}", error);
        }

        if (manifest is null)
            throw new InvalidOperationException($"the lane manifest at {path} parsed to nothing.");

        if (manifest.Objects is null || manifest.Objects.Count == 0)
        {
            throw new InvalidOperationException(
                $"the lane manifest at {path} names NO objects. The program set is what the build stamp is computed over, "
                + "so an empty manifest would stamp a lane that deploys nothing — refused rather than treated as a lane "
                + "with nothing to say.");
        }

        var unnamed = manifest.Objects.Where(o => string.IsNullOrWhiteSpace(o.Name) || string.IsNullOrWhiteSpace(o.Path)).ToArray();
        if (unnamed.Length > 0)
        {
            throw new InvalidOperationException(
                $"the lane manifest at {path} has {unnamed.Length} object(s) missing a name or a path. TIA matches an import "
                + "by NAME, so an unnamed object cannot be reasoned about at all.");
        }

        return manifest with { Obligations = manifest.Obligations ?? Array.Empty<string>() };
    }

    /// <summary>
    /// 🔴 <b>Does a hand-supplied <c>--program</c> agree with what the lane actually emitted?</b> Returns
    /// the disagreement, naming BOTH sides — never picks a winner. The manifest is the better source, but
    /// a caller who passed something else meant something by it, and silently overriding them would swap
    /// one unexamined program set for another.
    /// </summary>
    public string? Disagreement(IReadOnlyList<string> declaredPaths)
    {
        ArgumentNullException.ThrowIfNull(declaredPaths);

        if (declaredPaths.Count == 0)
            return null;

        var mine = ProgramPaths.Select(Canonical).OrderBy(p => p, StringComparer.Ordinal).ToArray();
        var theirs = declaredPaths.Select(Canonical).Distinct().OrderBy(p => p, StringComparer.Ordinal).ToArray();

        if (mine.SequenceEqual(theirs, StringComparer.Ordinal))
            return null;

        return $"lane '{LaneName}': --program disagrees with the manifest, and the batch will not choose between them. "
             + $"MANIFEST ({mine.Length}): {string.Join(", ", mine)}. "
             + $"--program ({theirs.Length}): {string.Join(", ", theirs)}. "
             + "The build stamp is computed over this set and claims it is what executes, so the two must be the same set. "
             + "Drop --program to use the manifest, or fix whichever one is stale.";
    }

    private static string Canonical(string path)
    {
        try
        {
            return System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(path))
                .ToUpperInvariant();
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // An unusable path still has to compare as ITSELF: returning a constant here would make two
            // different bad paths look equal and suppress the very disagreement being looked for.
            return path.ToUpperInvariant();
        }
    }
}
