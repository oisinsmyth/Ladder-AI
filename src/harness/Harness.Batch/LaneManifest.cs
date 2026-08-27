using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Harness.Map;

namespace Harness.Batch;

/// <summary>
/// One object a generator is handing to a manifest: what it is called, what it IS, and the file it was
/// written to.
///
/// <para><b>No origin and no role, deliberately.</b> Those are what <see cref="LaneManifest.Derive"/>
/// DERIVES — §3.1, a field that can be derived must never be typed — and a caller able to state them here
/// could state them wrongly.</para>
///
/// <para>🔴 <b><paramref name="Kind"/> is the opposite case and that is why it IS here.</b> A kind cannot
/// be derived by this type at all: <c>ProgramUnderTest</c> establishes it from the IR's own header line
/// and <c>LoadedProgramObject</c> carries it, and dropping it on the way in is what let
/// <c>--block-under-test</c> accept an instance DB — the verbatim shape of the Phase 10 defect the flag
/// was built to close. Required rather than defaulted: a default would be this record guessing.</para>
/// </summary>
public sealed record EmittedObject(string Name, string Path, HarnessObjectKind Kind);

/// <summary>
/// 🔴 <b>An object a GENERATOR produced, with the role the generator itself knows.</b>
///
/// <para><see cref="EmittedObject"/> carries no role because <see cref="LaneManifest.Derive"/> derives it —
/// §3.1, a field that can be derived must never be typed. This type is the case where the derivation
/// happened one layer UP: <c>SlotFcGenerator</c> is handed both the head and the block under test, so by
/// the time an FC exists its role is settled and re-deriving it here would be a second opinion.</para>
///
/// <para><b>It is NOT for the copy layer</b>, which has its own parameter and its own exclusion from the
/// build stamp. Everything here EXECUTES and is expected to have been hashed — which is why the role is
/// carried and the origin is not: the origin of an object on this list is <see cref="ObjectOrigin.Generated"/>
/// by construction, and a parameter for it would be a caller able to state it wrongly.</para>
/// </summary>
public sealed record GeneratedObject(string Name, string Path, HarnessObjectKind Kind, ObjectRole Role);

/// <summary>Which comparison inside <see cref="LaneManifest.AgreesWithStamp"/> refused.</summary>
/// <remarks>
/// 🔴 <b>Flags, because more than one can be true and collapsing them loses the diagnosis.</b> A rename
/// moves the NAMES and the STAMP; an edit that keeps the name moves the CONTENT and the STAMP; a changed
/// binding moves only the STAMP. Three different remedies, and a caller told only "it disagrees" has to
/// go and work out which.
/// </remarks>
[Flags]
public enum StampArm
{
    /// <summary>Nothing refused.</summary>
    None = 0,

    /// <summary>The two sets do not name the same objects.</summary>
    Names = 1,

    /// <summary>🔴 The same names, and at least one object's IR is not the IR that was hashed.</summary>
    Content = 2,

    /// <summary>The stamp value recorded in the manifest is not the one re-derived beside it.</summary>
    StampValue = 4,
}

/// <summary>
/// 🔴 <b>Whether a manifest describes the same program the build stamp hashed.</b>
///
/// <para><see cref="Detail"/> is printed on agreement TOO, and it always carries both counts.
/// <i>"offenders.Count == 0 is true of nothing found and of nothing looked at"</i>: a bare AGREES over a
/// manifest of zero objects and a stamp of zero objects is the shape of a check that compared nothing.</para>
///
/// <para>🔴 <b><see cref="Fired"/> says WHICH comparison refused</b>, because until 2026-08-24 there was
/// only one — names — and a block whose logic was inverted <i>without being renamed</i> passed
/// <c>manifest --check</c> with the stamp visibly moved on the line above. Names and content are two
/// distinct refusals with two distinct remedies and they are reported as two.</para>
/// </summary>
public sealed record StampAgreement(bool Agrees, string Detail, StampArm Fired = StampArm.None);

/// <summary>
/// 🔴 <b>Whether the files a manifest names still hold the IR the stamp hashed — asked WITHOUT re-deriving
/// the stamp.</b>
///
/// <para><c>harness-batch enqueue</c> is the caller. It has a lane's manifest and no copy layer, so it
/// cannot recompute the stamp; what it CAN do is re-read each recorded path and re-hash it. That is enough
/// to tell an emitted manifest from a hand-typed one and a current one from a stale one, which is the whole
/// of what <c>DERIVED</c> was claiming on the strength of a flag having been passed.</para>
/// </summary>
/// <param name="Verified">Objects whose recorded hash was compared against the file and matched.</param>
/// <param name="Drifted">
/// Objects whose file no longer hashes to what was recorded, or could not be read at all. <b>Unreadable
/// counts as drifted, never as skipped</b> — a manifest naming a file that is gone is describing a program
/// that cannot be deployed.
/// </param>
/// <param name="NotRecorded">
/// Objects carrying no recorded hash. The copy layer legitimately carries none — the stamp does not hash
/// it — and so does any manifest written before the hash was recorded. <b>It is a count, not a pass</b>,
/// and it is printed.
/// </param>
public sealed record ManifestContentCheck(
    IReadOnlyList<string> Verified,
    IReadOnlyList<string> Drifted,
    IReadOnlyList<string> NotRecorded)
{
    /// <summary>True when nothing drifted. <b>Also true when nothing was compared</b> — read
    /// <see cref="Verified"/> before believing it, and <see cref="Detail"/> states both counts.</summary>
    public bool Holds => Drifted.Count == 0;

    /// <summary>The denominator, on the passing path as well as the failing one.</summary>
    public string Detail =>
        $"{Verified.Count} object(s) re-hashed and unchanged, {Drifted.Count} drifted or unreadable, "
        + $"{NotRecorded.Count} carrying no recorded hash (the copy layer, which the stamp does not hash, is always in this bucket)"
        + (Drifted.Count > 0 ? ". DRIFTED: " + string.Join(", ", Drifted) : string.Empty);
}

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
/// <param name="Sha256">
/// 🔴 <b>THE HASH OF THE IR THE BUILD STAMP ACTUALLY HASHED — and until 2026-08-24 it was read from
/// <c>ProgramManifestEntry</c> and thrown away.</b>
///
/// <para>*** MEASURED: derive a manifest, then invert a block's logic WITHOUT renaming the block. The
/// stamp moved — <c>16#D78FEE7C</c> → <c>16#74E8F976</c> on the reproduction — and
/// <c>harness-batch manifest --check</c> printed <c>OK … manifest AGREES</c> and exited 0. *** The
/// comparison was over NAME SETS, and a pre-fix <c>Main</c> and a post-fix <c>Main</c> have the same name,
/// so the founding incident this whole type cites would not have been caught by the check written to
/// catch it.</para>
///
/// <para><b>Null is a real value and is not "unchanged".</b> The copy layer carries none because the stamp
/// does not hash it, and a manifest written before this field existed carries none either. Both are
/// counted and printed rather than folded into the verified total.</para>
/// </param>
/// <param name="Kind">
/// What the object is, as <c>ProgramUnderTest</c> classified it from the IR header. <b>Recorded so a
/// reader of the file can tell a block from its instance DB</b> — see <see cref="LaneManifest.Derive"/>'s
/// subject guard. Null on a manifest written before the field existed.
/// </param>
public sealed record ManifestObject(
    string Name,
    string Path,
    ObjectOrigin Origin,
    ObjectRole Role = ObjectRole.Unstated,
    string? Sha256 = null,
    HarnessObjectKind? Kind = null);

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
    IReadOnlyList<string> Obligations,

    /// <summary>
    /// 🔴 <b>THE BUILD STAMP THIS MANIFEST WAS DERIVED BESIDE — the value, not a promise about it.</b>
    ///
    /// <para><b>Null means NOTHING EMITTED THIS DOCUMENT.</b> <see cref="Derive"/> always records one, so a
    /// manifest without a stamp was typed by a person. That distinction had no representation at all until
    /// 2026-08-24, and <c>enqueue</c>'s <c>DERIVED</c>/<c>DECLARED</c> split was therefore keyed on WHICH
    /// FLAG WAS PASSED: a hand-written file naming two nonexistent objects with fabricated origins reported
    /// as <i>"program set DERIVED from the manifest"</i> and as the <i>"DENOMINATOR every wave's build
    /// stamp will be reported against"</i>. Null is read as "not derived", never as "derived and the stamp
    /// happened to be zero" — a stamp of zero is refused at the source (<c>BuildStamp</c>: <i>"zero is not
    /// a stamp"</i>).</para>
    /// </summary>
    uint? Stamp = null)
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
    ///
    /// <para>🔴 <b>NOTHING CONSUMES THIS AS A GATE, AND THAT IS THE CURRENT STATE RATHER THAN AN OVERSIGHT
    /// TO BE ASSUMED CLOSED.</b> The intended consumer named above — <c>converter undriven-scan --fb
    /// &lt;name&gt;</c> — is <b>not wired</b>: outside this type's own tests the only readers are the two
    /// report lines in <c>BatchCli</c> (<c>Manifest</c> and <c>Enqueue</c>), both of which PRINT the value
    /// and neither of which requires one. <b>Owner's ruling 2026-08-24: report, do not gate.</b> A lane
    /// naming no subject still runs — so the report must make a subjectless lane impossible to mistake for
    /// a lane with a verified one, which is what the banner in those two call sites is for. Wiring
    /// <c>undriven-scan</c> is a separate job; do not read the presence of this property as evidence that
    /// it happened.</para>
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
    /// The subject, by name, or null. <b>If named it MUST be among <paramref name="programUnderTest"/> AND
    /// it MUST be a <see cref="HarnessObjectKind.Block"/>.</b>
    ///
    /// <para>🔴 <b>THE KIND GUARD IS THE SECOND HALF AND IT WAS MISSING UNTIL 2026-08-24.</b> The in-set
    /// guard below matches by NAME, and a set holding only <c>DB_UnitInstance</c> with
    /// <c>--block-under-test DB_UnitInstance</c> satisfied it: the command reported <i>"BLOCK UNDER TEST:
    /// DB_UnitInstance — IN the stamped set, so changing it changes the stamp"</i> and exited 0. That is
    /// the Phase 10 defect verbatim — a set carrying the unit's instance DB and no block — passing the
    /// guard built to close it, with a reassuring sentence attached. An instance DB is DATA; the logic
    /// under test can be rewritten end to end without its iDB changing a byte.</para>
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
        IReadOnlyList<string>? obligations = null,

        // 🔴 *** THE OTHER GENERATED OBJECTS — THE ONES THAT EXECUTE. *** The copy layer above is the ONE
        // legitimate exclusion from the stamp, and it is excluded by ROLE. Everything on this list is a
        // deployed, hashed object that a GENERATOR produced rather than a person, and recording it as
        // Unstated — which is what happens to anything read off disk — would answer "how much of this lane
        // is still hand-built" with a guess in the pessimistic direction.
        //
        // Listed after the copy layer and BEFORE the program under test for the same collision reason: a
        // caller who passes the emit directory as --program hands its own generated objects back in, and
        // the generated row must win.
        IReadOnlyList<GeneratedObject>? generated = null,

        // 🔴 THE STIMULUS HEAD, DERIVED RATHER THAN TYPED. `SlotFcGenerator` is handed the head and the
        // block under test and used to discard them; naming the head here is that derivation arriving,
        // so `ObjectRole.StimulusHead` stops being a value nothing ever sets. Null is "no generator knew",
        // which is every lane that declares no generation.
        string? stimulusHead = null)
    {
        ArgumentNullException.ThrowIfNull(programUnderTest);
        ArgumentNullException.ThrowIfNull(generatedCopyLayer);
        ArgumentNullException.ThrowIfNull(stamp);

        if (string.IsNullOrWhiteSpace(laneName))
            throw new ArgumentException("a manifest with no lane name cannot be matched to the lane it describes.", nameof(laneName));

        var objects = new List<ManifestObject>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 🔴 THE HASHES THE STAMP RECORDED, BY NAME. Read from the stamp rather than recomputed: the whole
        // point of `BuildStamp.Derive` building its manifest IN the hashing loop is that no second walk can
        // disagree with it, and re-hashing here would be exactly that second walk.
        var hashedByName = new Dictionary<string, ProgramManifestEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in stamp.Objects)
            hashedByName[entry.Name] = entry;

        string? HashOf(string name) => hashedByName.TryGetValue(name, out var entry) ? entry.Sha256 : null;

        foreach (var mirror in generatedCopyLayer)
        {
            if (seen.Add(mirror.Name))
            {
                objects.Add(new ManifestObject(
                    mirror.Name, mirror.Path, ObjectOrigin.Generated, ObjectRole.CopyLayer,
                    HashOf(mirror.Name), mirror.Kind));
            }
        }

        foreach (var produced in generated ?? Array.Empty<GeneratedObject>())
        {
            if (seen.Add(produced.Name))
            {
                objects.Add(new ManifestObject(
                    produced.Name, produced.Path, ObjectOrigin.Generated, produced.Role,
                    HashOf(produced.Name), produced.Kind));
            }
        }

        foreach (var supplied in programUnderTest)
        {
            if (!seen.Add(supplied.Name))
                continue;

            // 🔴 THE ROLES A GENERATOR ALREADY KNEW, ARRIVING RATHER THAN BEING RE-DERIVED. The block under
            // test is named by the caller; the stimulus head is named by `SlotFcGenerator`, which was handed
            // both and discarded them. Checked in that order because a lane whose head IS its subject is a
            // block driving itself, which the FC generator refuses at source — so the two cannot both match.
            var role =
                blockUnderTest is not null && string.Equals(supplied.Name, blockUnderTest, StringComparison.OrdinalIgnoreCase)
                    ? ObjectRole.BlockUnderTest
                    : stimulusHead is not null && string.Equals(supplied.Name, stimulusHead, StringComparison.OrdinalIgnoreCase)
                        ? ObjectRole.StimulusHead
                        : ObjectRole.Unstated;

            objects.Add(new ManifestObject(
                supplied.Name, supplied.Path, ObjectOrigin.Unstated, role,
                HashOf(supplied.Name), supplied.Kind));
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

        // 🔴 AND IT MUST BE A BLOCK. Matching by name got the subject INTO the set; it did not make the
        // subject a subject. Taken after the in-set guard on purpose: an object outside the set has no
        // kind this method can quote, so the two refusals cannot both be the right first thing to say.
        if (blockUnderTest is not null)
        {
            var subject = objects.Single(o => o.Role == ObjectRole.BlockUnderTest);

            if (subject.Kind is not HarnessObjectKind.Block)
            {
                throw new InvalidOperationException(
                    $"'{blockUnderTest}' was named as the block under test and it is a {subject.Kind}, not a Block. THE SUBJECT OF A "
                    + "PER-BLOCK CHECK IS CODE. An instance DB, a UDT and a tag table are all DATA: the logic under test can be rewritten "
                    + "end to end while its instance DB stays byte-identical, so a lane naming one as its subject reports a block as "
                    + "covered by a stamp that moves for something else. That is the Phase 10 finding exactly — a deployed program set "
                    + "that carried the unit's instance DB and no block at all — and naming the DB is the same gap with a sentence of "
                    + "reassurance on top of it. Supply the block's own .ir in --program and name THAT, or drop --block-under-test and "
                    + "accept that no per-block check can be pointed at this lane. "
                    + $"Supplied: {string.Join(", ", objects.Select(o => $"{o.Kind?.ToString() ?? "kind unstated"}:{o.Name}"))}. "
                    + "(Background: docs/18-project-workbench.md §5 \"Phase 10 — Wave time\", the finding that the subject "
                    + "\"appears only as its instance DB\".)");
            }
        }

        var manifest = new LaneManifest(laneName, objects, obligations ?? Array.Empty<string>(), stamp.Stamp);

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
    /// <para>🔴 <b>DIRECTION THREE — AND IT IS THE ONE THE CHECK EXISTS FOR. Every object named here that
    /// carries a recorded hash must still hash to it.</b></para>
    ///
    /// <para>*** MEASURED 2026-08-24, BY REPRODUCTION: *** derive a manifest, then invert the logic inside
    /// <c>FB_Unit.ir</c> <b>without renaming it</b>. The stamp printed on the line above moved —
    /// <c>16#D78FEE7C</c> → <c>16#74E8F976</c> — and this method returned <c>AGREES</c>, because it
    /// compared NAME SETS and both files declare <c>FB_Unit</c>. The founding incident quoted throughout
    /// this file is <i>a lane pointed at a pre-fix <c>Main</c></i>, and a pre-fix <c>Main</c> and a
    /// post-fix <c>Main</c> have exactly the same name: the check could not have caught the thing it was
    /// written for. The hashes were already in hand — <c>ProgramManifestEntry.Sha256</c>, one per object,
    /// recorded in the stamp's own hashing loop — and were read and discarded.</para>
    ///
    /// <para><b>Names stay FIRST, and content is a SECOND and DISTINCT refusal.</b> A rename fires both
    /// name arms and is reported that way; an in-place edit fires the content arm alone; a changed binding
    /// fires only the stamp-value arm and moves no object at all. <see cref="StampAgreement.Fired"/> says
    /// which, because the remedy differs in each case.</para>
    ///
    /// <para><b><see cref="StampAgreement.Detail"/> carries every count on agreement as well as on
    /// refusal</b>, the number of objects actually compared by hash included. A green with no denominator
    /// is true of a comparison that examined nothing.</para>
    /// </summary>
    public StampAgreement AgreesWithStamp(ProgramManifest stamp)
    {
        ArgumentNullException.ThrowIfNull(stamp);

        var hashedByName = new Dictionary<string, ProgramManifestEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in stamp.Objects)
            hashedByName[entry.Name] = entry;

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
            .Where(n => !hashedByName.ContainsKey(n) && !excluded.Contains(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // 🔴 THE CONTENT ARM. Only objects present on BOTH sides are comparable — one that is not in the
        // stamp at all is the NAMES arm's finding and reporting it twice would double-count one defect.
        var drifted = new List<string>();
        var compared = 0;
        var unhashedRows = 0;

        foreach (var obj in Objects)
        {
            if (obj.Sha256 is not { Length: > 0 } recorded)
            {
                unhashedRows++;
                continue;
            }

            if (!hashedByName.TryGetValue(obj.Name, out var entry))
                continue;

            compared++;

            if (!string.Equals(entry.Sha256, recorded, StringComparison.OrdinalIgnoreCase))
                drifted.Add($"{obj.Name} (recorded {Short(recorded)}, now {Short(entry.Sha256)})");
        }

        // The stamp VALUE. It moves for anything in the canonical form — the map, the binding, the naming,
        // the excision set — so it catches the case where no object changed and the program still did.
        var stampMoved = Stamp is { } recordedStamp && recordedStamp != stamp.Stamp;

        var copyLayer = Objects.Count(o => o.Role == ObjectRole.CopyLayer);
        var denominator =
            $"lane '{LaneName}': {Objects.Count} manifest object(s) — {copyLayer} copy-layer, {Objects.Count - copyLayer} deployed-and-hashable "
            + $"— compared against {hashedByName.Count} object(s) the stamp hashed and {excluded.Count} it excluded as the harness's own output. "
            + $"CONTENT: {compared} object(s) compared by SHA-256, {unhashedRows} carrying no recorded hash. "
            + (Stamp is { } s
                ? $"STAMP: recorded 16#{s:X8}, re-derived 16#{stamp.Stamp:X8}."
                : "STAMP: the manifest records none of its own, so the stamp VALUE was not compared — nothing emitted this document.");

        var fired = StampArm.None;
        if (missing.Length > 0 || unstamped.Length > 0) fired |= StampArm.Names;
        if (drifted.Count > 0) fired |= StampArm.Content;
        if (stampMoved) fired |= StampArm.StampValue;

        if (fired == StampArm.None)
        {
            return new StampAgreement(true,
                "manifest AGREES with the build stamp. " + denominator
                + " Every hashed object is named, every named object that is not the copy layer was hashed, and every object carrying a "
                + "recorded hash still hashes to it.");
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

        if (drifted.Count > 0)
        {
            detail.Append($" CONTENT DRIFT ({drifted.Count}): {string.Join(", ", drifted)} — the NAMES still match and the IR does not. This is "
                        + "the same object under the same name carrying different logic, which is precisely the pre-fix Main shape: a rename "
                        + "would have shown up in the two arms above and an in-place edit shows up only here.");
        }

        if (stampMoved)
        {
            detail.Append($" STAMP MOVED: the manifest was derived beside 16#{Stamp!.Value:X8} and this derivation produced 16#{stamp.Stamp:X8}."
                        + (fired == StampArm.StampValue
                            ? " NO OBJECT CHANGED, so the difference is in the rest of the canonical form — the map, the binding, the naming or "
                            + "the excision set. The program under test is intact and the copy layer around it is not the one recorded."
                            : string.Empty));
        }

        return new StampAgreement(false, detail.ToString(), fired);
    }

    /// <summary>First eight hex digits — enough to tell two hashes apart in a message without printing 64.</summary>
    private static string Short(string sha) => sha.Length <= 8 ? sha : sha[..8] + "…";

    /// <summary>
    /// 🔴 <b>Re-read every file this manifest names and re-hash it, WITHOUT re-deriving the stamp.</b>
    ///
    /// <para><c>harness-batch enqueue</c> holds a manifest and no copy layer, so it cannot recompute a
    /// stamp — and it was therefore believing the document outright. It printed <i>"program set DERIVED
    /// from the manifest"</i> and <i>"the DENOMINATOR every wave's build stamp will be reported against"</i>
    /// for a file a person had typed, naming two objects that did not exist. The <c>DERIVED</c>/
    /// <c>DECLARED</c> split was keyed on WHICH FLAG WAS PASSED and on nothing else.</para>
    ///
    /// <para><b>The hash rule is <c>ProgramManifestEntry.HashOf</c>, the one the stamp itself uses.</b> A
    /// second derivation of it here would be comparing two different numbers and calling them equal.</para>
    /// </summary>
    /// <param name="readFile">
    /// How to read a recorded path. <b>A throw is DRIFT, not an exception to propagate</b>: a manifest
    /// naming a file that has been moved or deleted describes a program that cannot be deployed, and that
    /// is an answer about the world rather than a failure of this method.
    /// </param>
    public ManifestContentCheck ContentStillMatches(Func<string, string> readFile)
    {
        ArgumentNullException.ThrowIfNull(readFile);

        var verified = new List<string>();
        var drifted = new List<string>();
        var notRecorded = new List<string>();

        foreach (var obj in Objects)
        {
            if (obj.Sha256 is not { Length: > 0 } recorded)
            {
                notRecorded.Add(obj.Name);
                continue;
            }

            string actual;
            try
            {
                actual = ProgramManifestEntry.HashOf(readFile(obj.Path));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                drifted.Add($"{obj.Name} at '{obj.Path}' could not be read ({error.GetType().Name})");
                continue;
            }

            if (string.Equals(actual, recorded, StringComparison.OrdinalIgnoreCase))
                verified.Add(obj.Name);
            else
                drifted.Add($"{obj.Name} at '{obj.Path}' (recorded {Short(recorded)}, now {Short(actual)})");
        }

        return new ManifestContentCheck(verified, drifted, notRecorded);
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
