using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Harness.Map;

namespace Harness.Batch;

/// <summary>
/// One object a generator is handing to a manifest: what it is called, and the file it was written to.
///
/// <para><b>Two fields and no origin and no role, deliberately.</b> Those are what
/// <see cref="LaneManifest.Derive"/> DERIVES — §3.1, a field that can be derived must never be typed —
/// and a caller able to state them here could state them wrongly.</para>
/// </summary>
public sealed record EmittedObject(string Name, string Path);

/// <summary>
/// 🔴 <b>Whether a manifest describes the same program the build stamp hashed.</b>
///
/// <para><see cref="Detail"/> is printed on agreement TOO, and it always carries both counts.
/// <i>"offenders.Count == 0 is true of nothing found and of nothing looked at"</i>: a bare AGREES over a
/// manifest of zero objects and a stamp of zero objects is the shape of a check that compared nothing.</para>
/// </summary>
public sealed record StampAgreement(bool Agrees, string Detail);

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
    /// 🔴 <b>THE PRODUCER. The type opens by saying the manifest is "EMITTED BY WHATEVER BUILT THE LANE —
    /// not typed on a command line", and until this existed NOTHING EMITTED ONE.</b>
    ///
    /// <para>*** MEASURED: a nine-object set was deployed on 2026-08-22 and ran green, and it exists only
    /// as a hand-typed <c>--program</c> list. *** The next lane does not inherit it, and
    /// <c>BatchCli.Enqueue</c> prints the weaker DECLARED wording for exactly that case. Every guard this
    /// file already carried — the refusal to pick a winner in <see cref="Disagreement"/>, the roles, the
    /// origins — was reachable only from a document a person had written by hand, which is the document
    /// they were written to replace.</para>
    ///
    /// <para><b>The stamp is an INPUT, and it is what makes this a derivation rather than a second
    /// opinion.</b> <c>BuildStamp.Derive</c> records the objects it hashed in the same loop that feeds the
    /// hash; this method is handed that record and REFUSES to emit a manifest that disagrees with it. So a
    /// manifest cannot come into existence describing a program other than the one stamped — which is the
    /// pre-fix <c>Main</c> failure, closed from the construction side.</para>
    ///
    /// <para>🔴 <b>The copy layer is listed FIRST so a name collision resolves toward the harness's own
    /// output.</b> A caller who passes the emit directory as <c>--program</c> hands its own copy layer back
    /// in; recording it twice under two roles would make <see cref="BlockUnderTest"/> and the stamp
    /// comparison both read a set that does not exist.</para>
    /// </summary>
    /// <param name="programUnderTest">
    /// What was loaded from <c>--program</c>, with the file each object came from. <b>Origin is
    /// <see cref="ObjectOrigin.Unstated"/> for every one of them, and that is the honest value</b>: the
    /// harness reads an <c>.ir</c> file off disk and has no way to know whether a person or a generator
    /// wrote it. Claiming Authored here would answer "how much of this lane is still hand-built" with a
    /// guess.
    /// </param>
    /// <param name="generatedCopyLayer">
    /// The objects the copy-layer generator produced, and where they were written. These are the ONLY
    /// objects the build stamp legitimately does not hash — see <c>BuildStamp</c>'s opening invariant, that
    /// the copy layer cannot be one of its own inputs.
    /// </param>
    /// <param name="blockUnderTest">
    /// The subject, by name, or null. <b>If named it MUST be among <paramref name="programUnderTest"/>.</b>
    ///
    /// <para>The gap this closes is Phase 10's, recorded in <c>docs/18-project-workbench.md</c> under
    /// <b>§5, "Phase 10 — Wave time"</b> — search the phrase <i>"appears only as its instance DB"</i>. A
    /// deployed program set carried the unit's instance DB and <b>no block at all</b>, so the stamp said
    /// nothing whatever about the logic being tested. A manifest that named a subject the stamp had not
    /// hashed would put that gap in writing and still not close it.</para>
    ///
    /// <para><b>Measured both ways in <c>Harness.Map.Tests.BlockUnderTestEntersTheStampTests</c></b>, which
    /// is the citation to follow if the prose above has drifted: it pins the stamp before and after the
    /// block enters the set, and holds the two-programs-one-stamp case as the negative half.</para>
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The subject is not in the program set, or the manifest and the stamp describe different programs.
    /// </exception>
    public static LaneManifest Derive(
        string laneName,
        IReadOnlyList<EmittedObject> programUnderTest,
        IReadOnlyList<EmittedObject> generatedCopyLayer,
        string? blockUnderTest,
        ProgramManifest stamp,
        IReadOnlyList<string>? obligations = null)
    {
        ArgumentNullException.ThrowIfNull(programUnderTest);
        ArgumentNullException.ThrowIfNull(generatedCopyLayer);
        ArgumentNullException.ThrowIfNull(stamp);

        if (string.IsNullOrWhiteSpace(laneName))
            throw new ArgumentException("a manifest with no lane name cannot be matched to the lane it describes.", nameof(laneName));

        var objects = new List<ManifestObject>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var generated in generatedCopyLayer)
        {
            if (seen.Add(generated.Name))
                objects.Add(new ManifestObject(generated.Name, generated.Path, ObjectOrigin.Generated, ObjectRole.CopyLayer));
        }

        foreach (var supplied in programUnderTest)
        {
            if (!seen.Add(supplied.Name))
                continue;

            var role = blockUnderTest is not null && string.Equals(supplied.Name, blockUnderTest, StringComparison.OrdinalIgnoreCase)
                ? ObjectRole.BlockUnderTest
                : ObjectRole.Unstated;

            objects.Add(new ManifestObject(supplied.Name, supplied.Path, ObjectOrigin.Unstated, role));
        }

        // 🔴 TRACK 3. The subject must be IN the set, not merely mentioned beside it.
        if (blockUnderTest is not null && !objects.Any(o => o.Role == ObjectRole.BlockUnderTest))
        {
            // 🔴 THIS MESSAGE STANDS ON ITS OWN. Whoever reads it has just hit the error and is the least
            // able to go and check a reference, so it says what is wrong and why before it points anywhere
            // — and the pointer at the end is a section and a test name, never a line number.
            throw new InvalidOperationException(
                $"'{blockUnderTest}' was named as the block under test and is NOT among the {programUnderTest.Count} object(s) supplied as "
                + "the program under test, so the build stamp did not hash it. THE STAMP IS THE CLAIM THAT A PARTICULAR PROGRAM IS "
                + "EXECUTING, and it can only be a claim about objects that went into it: with the subject outside the set you can edit "
                + "the block, redeploy, and the stamp will not move — two materially different programs carrying one stamp, which is the "
                + "single thing the version register exists to prevent. Supply the block's own .ir in --program, or drop "
                + "--block-under-test and accept that no per-block check can be pointed at this lane. "
                + $"Supplied: {string.Join(", ", objects.Select(o => o.Name))}. "
                + "(Background: docs/18-project-workbench.md §5 \"Phase 10 — Wave time\", the finding that the subject "
                + "\"appears only as its instance DB\"; measured both ways in BlockUnderTestEntersTheStampTests.)");
        }

        var manifest = new LaneManifest(laneName, objects, obligations ?? Array.Empty<string>());

        // 🔴 THE TIE. A manifest is only worth having if it names what was actually hashed, and it is
        // refused at BIRTH rather than checked later by something that might not run.
        var agreement = manifest.AgreesWithStamp(stamp);
        if (!agreement.Agrees)
            throw new InvalidOperationException(agreement.Detail);

        return manifest;
    }

    /// <summary>
    /// 🔴 <b>Does this manifest name the same program the build stamp was computed over?</b> Both
    /// directions, and a MISSING object fails.
    ///
    /// <para><b>Direction one — every object the stamp hashed must be named here.</b> That is the guard for
    /// the failure this type's own summary describes: a lane pointed at a pre-fix <c>Main</c> by hand, with
    /// the stamp following it. A manifest that omits an object the stamp hashed is describing a different
    /// program from the one the version register will confirm.</para>
    ///
    /// <para><b>Direction two — every object named here that is not the copy layer must have been
    /// hashed.</b> The copy layer is the ONE legitimate exclusion and it is excluded by ROLE, never by
    /// origin: a program under test may perfectly well have been generated, and keying on that would let a
    /// real deliverable slip out of the stamp under the label that describes who wrote it. Everything else
    /// a lane deploys executes on the controller and belongs in the hash — which is Track 3's finding
    /// stated as a rule rather than as an incident.</para>
    ///
    /// <para><b><see cref="StampAgreement.Detail"/> carries both counts on agreement as well as on
    /// refusal.</b> A green with no denominator is true of a comparison that examined nothing.</para>
    /// </summary>
    public StampAgreement AgreesWithStamp(ProgramManifest stamp)
    {
        ArgumentNullException.ThrowIfNull(stamp);

        var hashed = new HashSet<string>(stamp.Objects.Select(o => o.Name), StringComparer.OrdinalIgnoreCase);

        // `ExcludedAsSelfReferential` rows are `Kind:Name`. They are the harness's own output, refused BY
        // NAME by the stamp, so an object here is accounted for without having been hashed.
        var excluded = new HashSet<string>(
            stamp.ExcludedAsSelfReferential.Select(e => e[(e.IndexOf(':') + 1)..]),
            StringComparer.OrdinalIgnoreCase);

        var mine = new HashSet<string>(Objects.Select(o => o.Name), StringComparer.OrdinalIgnoreCase);

        var missing = stamp.Objects
            .Select(o => o.Name)
            .Where(n => !mine.Contains(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var unstamped = Objects
            .Where(o => o.Role != ObjectRole.CopyLayer)
            .Select(o => o.Name)
            .Where(n => !hashed.Contains(n) && !excluded.Contains(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var copyLayer = Objects.Count(o => o.Role == ObjectRole.CopyLayer);
        var denominator =
            $"lane '{LaneName}': {Objects.Count} manifest object(s) — {copyLayer} copy-layer, {Objects.Count - copyLayer} deployed-and-hashable "
            + $"— compared against {hashed.Count} object(s) the stamp hashed and {excluded.Count} it excluded as the harness's own output.";

        if (missing.Length == 0 && unstamped.Length == 0)
        {
            return new StampAgreement(true,
                "manifest AGREES with the build stamp. " + denominator
                + " Every hashed object is named, and every named object that is not the copy layer was hashed.");
        }

        var detail = new StringBuilder("manifest DISAGREES with the build stamp, so it would describe a program other than the one stamped. ")
            .Append(denominator);

        if (missing.Length > 0)
        {
            detail.Append($" HASHED BUT NOT NAMED ({missing.Length}): {string.Join(", ", missing)} — the stamp claims these are executing and "
                        + "the manifest does not know about them, which is how a lane gets pointed at a pre-fix Main and the stamp goes with it.");
        }

        if (unstamped.Length > 0)
        {
            detail.Append($" NAMED BUT NOT HASHED ({unstamped.Length}): {string.Join(", ", unstamped)} — these are deployed and the stamp does "
                        + "not cover them, so changing one changes the controller without changing the stamp and the verifying gateway, whose "
                        + "whole job is to refuse a program that is not the one described, would not notice.");
        }

        return new StampAgreement(false, detail.ToString());
    }

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
