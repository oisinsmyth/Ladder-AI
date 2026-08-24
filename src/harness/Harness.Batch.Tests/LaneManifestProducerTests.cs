using Harness.Batch;
using Harness.Map;

namespace Harness.Batch.Tests;

/// <summary>
/// 🔴 <b><c>LaneManifest</c> HAD NO PRODUCER.</b> Its own summary says the program set is <i>"EMITTED BY
/// WHATEVER BUILT THE LANE — not typed on a command line"</i>, and until <see cref="LaneManifest.Derive"/>
/// existed the only thing that constructed one was a test. Every manifest in the world was hand-authored,
/// which is the document the type exists to replace.
///
/// <para><b>The tie that makes it worth having:</b> a manifest is refused at BIRTH unless it names exactly
/// what the build stamp hashed. A MISSING object fails — that is the guard for the failure the class
/// docstring records, <i>"a lane was pointed at a pre-fix Main by hand and the stamp went with it"</i> —
/// and a NAMED-BUT-UNHASHED object fails too, which is Track 3's finding stated as a rule.</para>
///
/// <para><b>Every refusal here is paired with a control that must still pass.</b> A check that refuses
/// everything is not a check, and the copy-layer exemption is the case most likely to be over-tightened
/// into one: the copy layer is legitimately absent from the stamp, and a rule that could not say so would
/// refuse every lane.</para>
/// </summary>
public sealed class LaneManifestProducerTests
{
    private const string CopyLayerBlock = "FC_HarnessCopyLayer";
    private const string MirrorTable = "HarnessMirror";

    /// <summary>
    /// A supplied object. <b>The kind is explicit</b> — <c>ProgramUnderTest</c> reads it off the IR header
    /// and a fixture that guessed would be guessing about the very thing the subject guard checks.
    /// </summary>
    private static EmittedObject Emitted(string name, HarnessObjectKind kind = HarnessObjectKind.Block) =>
        new(name, $@"C:\ir\{name}.ir", kind);

    private static IReadOnlyList<EmittedObject> CopyLayer() =>
        new[] { Emitted(CopyLayerBlock), Emitted(MirrorTable, HarnessObjectKind.TagTable) };

    /// <summary>A stamp record naming <paramref name="hashed"/>, with nothing excluded unless asked.</summary>
    private static ProgramManifest Stamp(IEnumerable<string> hashed, params string[] excluded) =>
        new(
            hashed.Select(n => new ProgramManifestEntry("Block", n, Sha(n))).ToArray(),
            excluded,
            0xDEADBEEF);

    /// <summary>
    /// A distinct, stable content hash per object name. <b>Distinct matters</b>: a fixture reusing one
    /// constant across every object would let a content comparison that matched the WRONG entry still pass.
    /// </summary>
    private static string Sha(string name) => ProgramManifestEntry.HashOf("ir-of-" + name);

    // ---------------------------------------------------------------------------------------------
    // The producer
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// THE CONTROL FOR EVERYTHING BELOW: the ordinary lane. Two generated copy-layer objects, three
    /// objects under test, one of them the subject — derived, agreeing, and round-tripping.
    /// </summary>
    [Fact]
    public void A_derived_manifest_names_the_stamped_set_with_its_roles_and_origins()
    {
        var program = new[] { Emitted("FB_Unit"), Emitted("DB_UnitInstance"), Emitted("DB_Params") };

        var manifest = LaneManifest.Derive(
            "valve", program, CopyLayer(), "FB_Unit",
            Stamp(new[] { "FB_Unit", "DB_UnitInstance", "DB_Params" }));

        Assert.Equal("valve", manifest.LaneName);
        Assert.Equal(5, manifest.Objects.Count);
        Assert.Equal("FB_Unit", manifest.BlockUnderTest);

        // The copy layer is the ONLY thing labelled Generated: the harness knows it produced those two and
        // knows nothing about who wrote the three it read off disk.
        Assert.Equal(2, manifest.Objects.Count(o => o.Origin == ObjectOrigin.Generated));
        Assert.Equal(2, manifest.Objects.Count(o => o.Role == ObjectRole.CopyLayer));
        Assert.Equal(3, manifest.Objects.Count(o => o.Origin == ObjectOrigin.Unstated));
        Assert.Empty(manifest.Objects.Where(o => o.Origin == ObjectOrigin.Authored));
    }

    /// <summary>
    /// <b>Unstated is the honest origin for a program read off disk, and it is NOT "authored".</b> The
    /// enum's own docstring says the two have different failure modes and different remedies; a producer
    /// that guessed would answer "how much of this lane is still hand-built" with a number nothing
    /// established.
    /// </summary>
    [Fact]
    public void The_program_under_test_is_recorded_with_an_unstated_origin_rather_than_a_guessed_one()
    {
        var manifest = LaneManifest.Derive(
            "valve", new[] { Emitted("FB_Unit") }, CopyLayer(), null, Stamp(new[] { "FB_Unit" }));

        Assert.Equal(ObjectOrigin.Unstated, Assert.Single(manifest.Objects.Where(o => o.Name == "FB_Unit")).Origin);
    }

    /// <summary>The obligation travels: a generated FC nothing calls is deployed, loaded, healthy and never runs.</summary>
    [Fact]
    public void Obligations_are_carried_through_and_an_empty_list_is_a_real_answer()
    {
        var withOne = LaneManifest.Derive(
            "valve", new[] { Emitted("FB_Unit") }, CopyLayer(), null, Stamp(new[] { "FB_Unit" }),
            new[] { "call FC_HarnessCopyLayer from the cyclic OB" });

        var withNone = LaneManifest.Derive(
            "valve", new[] { Emitted("FB_Unit") }, CopyLayer(), null, Stamp(new[] { "FB_Unit" }));

        Assert.Single(withOne.Obligations);
        Assert.Empty(withNone.Obligations);
    }

    /// <summary>
    /// A copy-layer object handed BACK in via <c>--program</c> — what happens when a caller points
    /// <c>--program</c> at the emit directory. It is recorded ONCE, keeping the copy-layer role: two rows
    /// under two roles would make <see cref="LaneManifest.BlockUnderTest"/> and the stamp comparison both
    /// read a set that does not exist.
    /// </summary>
    [Fact]
    public void A_copy_layer_object_also_supplied_as_program_is_recorded_once_as_the_copy_layer()
    {
        var manifest = LaneManifest.Derive(
            "valve",
            new[] { Emitted("FB_Unit"), Emitted(CopyLayerBlock) },
            CopyLayer(),
            null,
            Stamp(new[] { "FB_Unit" }, $"Block:{CopyLayerBlock}"));

        Assert.Equal(3, manifest.Objects.Count);
        Assert.Equal(ObjectRole.CopyLayer, Assert.Single(manifest.Objects.Where(o => o.Name == CopyLayerBlock)).Role);
    }

    // ---------------------------------------------------------------------------------------------
    // TRACK 3 — the subject must be IN the set the stamp hashed
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>THE GUARD. Naming a block under test that is not in the program set is a refusal.</b>
    /// <c>docs/18-project-workbench.md</c>, <b>§5 "Phase 10 — Wave time"</b>: a deployed program set carried
    /// the unit's instance DB and no block, so the subject <i>"appears only as its instance DB"</i> and the
    /// stamp said nothing about it. A manifest that named it anyway would put that gap in writing and leave
    /// it open. Stamp arithmetic for the same fact:
    /// <c>Harness.Map.Tests.BlockUnderTestEntersTheStampTests</c>.
    /// </summary>
    [Fact]
    public void Naming_a_block_under_test_that_is_not_in_the_program_set_is_refused()
    {
        var error = Assert.Throws<InvalidOperationException>(() => LaneManifest.Derive(
            "valve",
            new[] { Emitted("DB_UnitInstance") },       // only the instance DB — the measured case
            CopyLayer(),
            "FB_Unit",
            Stamp(new[] { "DB_UnitInstance" })));

        Assert.Contains("FB_Unit", error.Message, StringComparison.Ordinal);
        Assert.Contains("did not hash it", error.Message, StringComparison.Ordinal);
        Assert.Contains("DB_UnitInstance", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// THE PAIRED CONTROL. The same lane with the block actually supplied derives cleanly and the subject
    /// is readable. Without this, a producer that refused every lane would satisfy the test above.
    /// </summary>
    [Fact]
    public void The_same_lane_with_the_block_supplied_derives_and_names_its_subject()
    {
        var manifest = LaneManifest.Derive(
            "valve",
            new[] { Emitted("DB_UnitInstance"), Emitted("FB_Unit") },
            CopyLayer(),
            "FB_Unit",
            Stamp(new[] { "DB_UnitInstance", "FB_Unit" }));

        Assert.Equal("FB_Unit", manifest.BlockUnderTest);
    }

    /// <summary>Stating no subject stays legal — an older lane, and a null <c>BlockUnderTest</c> that says so.</summary>
    [Fact]
    public void Stating_no_block_under_test_is_still_allowed_and_reads_as_null()
    {
        var manifest = LaneManifest.Derive(
            "valve", new[] { Emitted("FB_Unit") }, CopyLayer(), null, Stamp(new[] { "FB_Unit" }));

        Assert.Null(manifest.BlockUnderTest);
    }

    // ---------------------------------------------------------------------------------------------
    // AND THE SUBJECT MUST BE A BLOCK — the second half of the same guard
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>AN INSTANCE DB NAMED AS THE SUBJECT IS REFUSED, AND THE KIND IS IN THE MESSAGE.</b>
    ///
    /// <para>The guard above matches by NAME, so a set holding only <c>DB_UnitInstance</c> with
    /// <c>--block-under-test DB_UnitInstance</c> satisfied it and the command reported the subject as
    /// <i>"IN the stamped set"</i> at exit 0 — <b>the Phase 10 defect verbatim, passing the guard built to
    /// close it.</b> An instance DB is DATA: the logic under test can be rewritten end to end while its
    /// iDB stays byte-identical.</para>
    /// </summary>
    [Theory]
    [InlineData(HarnessObjectKind.DataBlock, "DataBlock")]
    [InlineData(HarnessObjectKind.DataType, "DataType")]
    [InlineData(HarnessObjectKind.TagTable, "TagTable")]
    public void A_subject_that_is_not_a_code_block_is_refused_and_the_kind_is_named(HarnessObjectKind kind, string rendered)
    {
        var error = Assert.Throws<InvalidOperationException>(() => LaneManifest.Derive(
            "valve",
            new[] { Emitted("Subject", kind) },
            CopyLayer(),
            "Subject",
            Stamp(new[] { "Subject" })));

        Assert.Contains($"it is a {rendered}, not a Block", error.Message, StringComparison.Ordinal);
        Assert.Contains("Subject", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// THE PAIRED CONTROL. The identical lane whose subject IS a block derives, so the guard is not simply
    /// refusing every subject. Both halves in one test because the two differ by one argument.
    /// </summary>
    [Fact]
    public void A_subject_that_is_a_code_block_is_accepted()
    {
        var manifest = LaneManifest.Derive(
            "valve",
            new[] { Emitted("Subject", HarnessObjectKind.Block) },
            CopyLayer(),
            "Subject",
            Stamp(new[] { "Subject" }));

        Assert.Equal("Subject", manifest.BlockUnderTest);
    }

    /// <summary>
    /// The measured lane in full: the instance DB IS legitimately in the program set, and only naming it
    /// as the SUBJECT is refused. A guard that had refused the DB's presence would have refused every real
    /// lane, since a unit's iDB belongs in the stamp.
    /// </summary>
    [Fact]
    public void The_instance_DB_may_be_in_the_set_and_only_naming_it_as_the_subject_is_refused()
    {
        var program = new[] { Emitted("FB_Unit"), Emitted("DB_UnitInstance", HarnessObjectKind.DataBlock) };
        var stamp = Stamp(new[] { "FB_Unit", "DB_UnitInstance" });

        var ok = LaneManifest.Derive("valve", program, CopyLayer(), "FB_Unit", stamp);
        Assert.Equal("FB_Unit", ok.BlockUnderTest);
        Assert.Equal(2, ok.Objects.Count(o => o.Role != ObjectRole.CopyLayer));

        Assert.Throws<InvalidOperationException>(() =>
            LaneManifest.Derive("valve", program, CopyLayer(), "DB_UnitInstance", stamp));
    }

    // ---------------------------------------------------------------------------------------------
    // THE CONTENT ARM — the same names carrying different IR
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>THE HASHES ARE RECORDED, one per stamped object, and the copy layer's is null.</b> Without
    /// them there is nothing for the content arm to compare, and this is the field that was read from
    /// <c>ProgramManifestEntry</c> and thrown away.
    /// </summary>
    [Fact]
    public void A_derived_manifest_records_the_stamps_own_hash_per_object_and_the_stamp_value()
    {
        var manifest = LaneManifest.Derive(
            "valve", new[] { Emitted("FB_Unit") }, CopyLayer(), "FB_Unit", Stamp(new[] { "FB_Unit" }));

        Assert.Equal(0xDEADBEEFu, manifest.Stamp);
        Assert.Equal(Sha("FB_Unit"), Assert.Single(manifest.Objects.Where(o => o.Name == "FB_Unit")).Sha256);

        // The copy layer is not hashed by the stamp, so it carries no hash — null, and counted as such
        // rather than folded into the verified total.
        Assert.All(manifest.Objects.Where(o => o.Role == ObjectRole.CopyLayer), o => Assert.Null(o.Sha256));
    }

    /// <summary>
    /// 🔴 <b>SAME NAMES, DIFFERENT IR: the content arm fires and the NAME arms do not.</b> This is the
    /// pre-fix <c>Main</c> shape — the incident every docstring in this file cites — and a name-set
    /// comparison is blind to it by construction.
    /// </summary>
    [Fact]
    public void A_manifest_whose_object_hashes_differently_disagrees_on_CONTENT_and_not_on_names()
    {
        var manifest = LaneManifest.Derive(
            "valve", new[] { Emitted("FB_Unit") }, CopyLayer(), "FB_Unit", Stamp(new[] { "FB_Unit" }));

        // The SAME name, a different hash — one object edited in place.
        var edited = new ProgramManifest(
            new[] { new ProgramManifestEntry("Block", "FB_Unit", ProgramManifestEntry.HashOf("ir-of-FB_Unit, inverted")) },
            Array.Empty<string>(),
            0xDEADBEEF);

        var verdict = manifest.AgreesWithStamp(edited);

        Assert.False(verdict.Agrees);
        Assert.Equal(StampArm.Content, verdict.Fired);
        Assert.Contains("CONTENT DRIFT (1): FB_Unit", verdict.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("HASHED BUT NOT NAMED", verdict.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("NAMED BUT NOT HASHED", verdict.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>A RENAME still fires BOTH name arms</b> — the older behaviour is not traded away for the new one.
    /// It also fires nothing else, so the two diagnoses stay separable.
    /// </summary>
    [Fact]
    public void A_rename_still_fires_both_name_arms()
    {
        var manifest = LaneManifest.Derive(
            "valve", new[] { Emitted("FB_Unit") }, CopyLayer(), "FB_Unit", Stamp(new[] { "FB_Unit" }));

        var verdict = manifest.AgreesWithStamp(Stamp(new[] { "FB_Unit_v2" }));

        Assert.False(verdict.Agrees);
        Assert.Equal(StampArm.Names, verdict.Fired);
        Assert.Contains("HASHED BUT NOT NAMED (1): FB_Unit_v2", verdict.Detail, StringComparison.Ordinal);
        Assert.Contains("NAMED BUT NOT HASHED (1): FB_Unit", verdict.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>THE STAMP VALUE IS ITS OWN ARM, and it is the only one that can fire alone.</b> Every object is
    /// present and unchanged and the stamp still moved — a changed binding, map or naming. Reported as
    /// such, because the remedy is not in the program set at all.
    /// </summary>
    [Fact]
    public void A_stamp_that_moved_with_no_object_changing_fires_only_the_stamp_arm_and_says_so()
    {
        var manifest = LaneManifest.Derive(
            "valve", new[] { Emitted("FB_Unit") }, CopyLayer(), "FB_Unit", Stamp(new[] { "FB_Unit" }));

        var sameObjectsNewStamp = new ProgramManifest(
            new[] { new ProgramManifestEntry("Block", "FB_Unit", Sha("FB_Unit")) },
            Array.Empty<string>(),
            0x0BADF00D);

        var verdict = manifest.AgreesWithStamp(sameObjectsNewStamp);

        Assert.False(verdict.Agrees);
        Assert.Equal(StampArm.StampValue, verdict.Fired);
        Assert.Contains("STAMP MOVED: the manifest was derived beside 16#DEADBEEF", verdict.Detail, StringComparison.Ordinal);
        Assert.Contains("NO OBJECT CHANGED", verdict.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// THE PAIRED CONTROL for all three arms: the manifest checked against the stamp it was derived beside
    /// agrees, <b>and the agreement states how many objects it compared by hash.</b> A green with no
    /// content denominator is true of a comparison that compared nothing.
    /// </summary>
    [Fact]
    public void The_manifest_agrees_with_its_own_stamp_and_says_how_many_it_compared_by_hash()
    {
        var stamp = Stamp(new[] { "FB_Unit", "DB_UnitInstance" });
        var manifest = LaneManifest.Derive(
            "valve",
            new[] { Emitted("FB_Unit"), Emitted("DB_UnitInstance", HarnessObjectKind.DataBlock) },
            CopyLayer(), "FB_Unit", stamp);

        var verdict = manifest.AgreesWithStamp(stamp);

        Assert.True(verdict.Agrees);
        Assert.Equal(StampArm.None, verdict.Fired);
        Assert.Contains("CONTENT: 2 object(s) compared by SHA-256, 2 carrying no recorded hash", verdict.Detail, StringComparison.Ordinal);
        Assert.Contains("STAMP: recorded 16#DEADBEEF, re-derived 16#DEADBEEF", verdict.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 <b>A MANIFEST THAT RECORDS NO HASHES SAYS SO IN THE PASS MESSAGE, rather than reading as
    /// verified.</b> An older document carries none, and "0 compared" is the number a reader needs to see
    /// before treating an AGREES as evidence about content.
    /// </summary>
    [Fact]
    public void A_manifest_carrying_no_recorded_hashes_agrees_and_states_that_it_compared_none()
    {
        var older = new LaneManifest(
            "valve",
            new[] { new ManifestObject("FB_Unit", @"C:\ir\FB_Unit.ir", ObjectOrigin.Unstated) },
            Array.Empty<string>());

        var verdict = older.AgreesWithStamp(Stamp(new[] { "FB_Unit" }));

        Assert.True(verdict.Agrees);
        Assert.Contains("CONTENT: 0 object(s) compared by SHA-256, 1 carrying no recorded hash", verdict.Detail, StringComparison.Ordinal);
        Assert.Contains("the manifest records none of its own", verdict.Detail, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // ContentStillMatches — the arm `enqueue` can take without a copy layer
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>Re-reading the files: unchanged verifies, edited drifts, missing drifts.</b> All three in one
    /// test because the three buckets are one denominator and asserting them apart would let their sum
    /// disagree with the object count.
    /// </summary>
    [Fact]
    public void ContentStillMatches_verifies_unchanged_files_and_calls_edited_and_missing_ones_drift()
    {
        var manifest = LaneManifest.Derive(
            "valve",
            new[] { Emitted("FB_Same"), Emitted("FB_Edited"), Emitted("FB_Gone") },
            CopyLayer(), null,
            Stamp(new[] { "FB_Same", "FB_Edited", "FB_Gone" }));

        var check = manifest.ContentStillMatches(path => Path.GetFileNameWithoutExtension(path) switch
        {
            "FB_Same" => "ir-of-FB_Same",                     // exactly what Stamp() hashed
            "FB_Edited" => "ir-of-FB_Edited, but changed",
            "FB_Gone" => throw new FileNotFoundException(path),
            _ => throw new FileNotFoundException(path),
        });

        Assert.False(check.Holds);
        Assert.Equal(new[] { "FB_Same" }, check.Verified);
        Assert.Equal(2, check.Drifted.Count);
        Assert.Contains(check.Drifted, d => d.StartsWith("FB_Edited", StringComparison.Ordinal));
        Assert.Contains(check.Drifted, d => d.Contains("could not be read", StringComparison.Ordinal));

        // The copy layer carries no hash, so it is neither verified nor drifted — and the three buckets
        // still account for every row.
        Assert.Equal(2, check.NotRecorded.Count);
        Assert.Equal(manifest.Objects.Count, check.Verified.Count + check.Drifted.Count + check.NotRecorded.Count);
    }

    /// <summary>THE PAIRED CONTROL: every file unchanged holds, and the detail states the denominator.</summary>
    [Fact]
    public void ContentStillMatches_holds_when_nothing_changed_and_states_what_it_compared()
    {
        var manifest = LaneManifest.Derive(
            "valve", new[] { Emitted("FB_Unit") }, CopyLayer(), "FB_Unit", Stamp(new[] { "FB_Unit" }));

        var check = manifest.ContentStillMatches(path => "ir-of-" + Path.GetFileNameWithoutExtension(path));

        Assert.True(check.Holds);
        Assert.Contains("1 object(s) re-hashed and unchanged, 0 drifted or unreadable", check.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// A subject whose name collides with the copy layer's is refused rather than silently re-roled. The
    /// dedupe above resolves toward the harness's own output, so the role would otherwise vanish and
    /// <see cref="LaneManifest.BlockUnderTest"/> would quietly return null on a lane that named one.
    /// </summary>
    [Fact]
    public void A_subject_colliding_with_the_copy_layer_name_is_refused_rather_than_silently_dropped()
    {
        Assert.Throws<InvalidOperationException>(() => LaneManifest.Derive(
            "valve",
            new[] { Emitted(CopyLayerBlock) },
            CopyLayer(),
            CopyLayerBlock,
            Stamp(Array.Empty<string>(), $"Block:{CopyLayerBlock}")));
    }

    // ---------------------------------------------------------------------------------------------
    // THE TIE — the manifest and the stamp must describe one program
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>MISSING FAILS. The stamp hashed something the manifest does not name.</b> This is the pre-fix
    /// <c>Main</c> shape: the stamp claims a program is executing and the document meant to record it has
    /// never heard of part of that program.
    /// </summary>
    [Fact]
    public void A_manifest_missing_an_object_the_stamp_hashed_disagrees()
    {
        var manifest = new LaneManifest(
            "valve",
            new[] { new ManifestObject("FB_Unit", @"C:\ir\FB_Unit.ir", ObjectOrigin.Unstated) },
            Array.Empty<string>());

        var verdict = manifest.AgreesWithStamp(Stamp(new[] { "FB_Unit", "Main" }));

        Assert.False(verdict.Agrees);
        Assert.Contains("HASHED BUT NOT NAMED (1): Main", verdict.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 <b>NAMED-BUT-UNHASHED FAILS, and it is Track 3's finding as a rule.</b> A deployed object the
    /// stamp does not cover means changing it changes the controller without changing the stamp.
    /// </summary>
    [Fact]
    public void A_manifest_naming_an_object_the_stamp_did_not_hash_disagrees()
    {
        var manifest = new LaneManifest(
            "valve",
            new[]
            {
                new ManifestObject("FB_Unit", @"C:\ir\FB_Unit.ir", ObjectOrigin.Unstated, ObjectRole.BlockUnderTest),
                new ManifestObject("DB_Params", @"C:\ir\DB_Params.ir", ObjectOrigin.Unstated),
            },
            Array.Empty<string>());

        var verdict = manifest.AgreesWithStamp(Stamp(new[] { "FB_Unit" }));

        Assert.False(verdict.Agrees);
        Assert.Contains("NAMED BUT NOT HASHED (1): DB_Params", verdict.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 <b>THE CONTROL THAT KEEPS THE RULE USABLE: the copy layer is named and NOT hashed, and that
    /// agrees.</b> <c>BuildStamp</c> opens by stating the copy layer cannot be one of its own inputs, so a
    /// tie that could not express the exemption would refuse every lane that has ever existed.
    /// </summary>
    [Fact]
    public void The_copy_layer_named_but_not_hashed_agrees()
    {
        var manifest = new LaneManifest(
            "valve",
            new[]
            {
                new ManifestObject(CopyLayerBlock, @"C:\ir\cl.ir", ObjectOrigin.Generated, ObjectRole.CopyLayer),
                new ManifestObject(MirrorTable, @"C:\ir\mt.ir", ObjectOrigin.Generated, ObjectRole.CopyLayer),
                new ManifestObject("FB_Unit", @"C:\ir\FB_Unit.ir", ObjectOrigin.Unstated, ObjectRole.BlockUnderTest),
            },
            Array.Empty<string>());

        Assert.True(manifest.AgreesWithStamp(Stamp(new[] { "FB_Unit" })).Agrees);
    }

    /// <summary>
    /// <b>The exemption is keyed on ROLE, never on origin.</b> A program under test may perfectly well have
    /// been generated — the whole pipeline generates blocks — and keying on <see cref="ObjectOrigin"/>
    /// would let a real deliverable out of the stamp under a label describing who wrote it.
    /// </summary>
    [Fact]
    public void A_generated_object_that_is_not_the_copy_layer_still_has_to_be_hashed()
    {
        var manifest = new LaneManifest(
            "valve",
            new[] { new ManifestObject("FB_GeneratedByThePipeline", @"C:\ir\g.ir", ObjectOrigin.Generated, ObjectRole.SlotFc) },
            Array.Empty<string>());

        Assert.False(manifest.AgreesWithStamp(Stamp(Array.Empty<string>())).Agrees);
    }

    /// <summary>
    /// An object the stamp refused BY NAME as its own output is accounted for without being hashed —
    /// otherwise pointing <c>--program</c> at the emit directory would be permanently unrepresentable.
    /// </summary>
    [Fact]
    public void An_object_excluded_as_self_referential_is_accounted_for()
    {
        var manifest = new LaneManifest(
            "valve",
            new[] { new ManifestObject(CopyLayerBlock, @"C:\ir\cl.ir", ObjectOrigin.Generated, ObjectRole.SlotFc) },
            Array.Empty<string>());

        Assert.True(manifest.AgreesWithStamp(Stamp(Array.Empty<string>(), $"Block:{CopyLayerBlock}")).Agrees);
    }

    /// <summary>
    /// 🔴 <b>THE DENOMINATOR IS IN THE PASS MESSAGE.</b> <i>"offenders.Count == 0 is true of nothing found
    /// and of nothing looked at"</i> — an AGREES with no counts beside it is the shape of a comparison that
    /// examined nothing, and this pair of manifests is exactly that: one real, one empty.
    /// </summary>
    [Fact]
    public void Agreement_states_both_counts_so_an_empty_comparison_cannot_read_as_a_full_one()
    {
        var real = LaneManifest.Derive(
            "valve",
            new[] { Emitted("FB_Unit"), Emitted("DB_UnitInstance") },
            CopyLayer(), "FB_Unit",
            Stamp(new[] { "FB_Unit", "DB_UnitInstance" }));

        var detail = real.AgreesWithStamp(Stamp(new[] { "FB_Unit", "DB_UnitInstance" })).Detail;

        Assert.Contains("4 manifest object(s)", detail, StringComparison.Ordinal);
        Assert.Contains("2 copy-layer", detail, StringComparison.Ordinal);
        Assert.Contains("2 deployed-and-hashable", detail, StringComparison.Ordinal);
        Assert.Contains("2 object(s) the stamp hashed", detail, StringComparison.Ordinal);

        // The vacuous case reads DIFFERENTLY, in numbers, rather than sharing one word with the real one.
        var empty = new LaneManifest("valve", new[] { new ManifestObject(CopyLayerBlock, "x.ir", ObjectOrigin.Generated, ObjectRole.CopyLayer) }, Array.Empty<string>());
        var emptyDetail = empty.AgreesWithStamp(Stamp(Array.Empty<string>())).Detail;

        Assert.True(empty.AgreesWithStamp(Stamp(Array.Empty<string>())).Agrees);
        Assert.Contains("0 object(s) the stamp hashed", emptyDetail, StringComparison.Ordinal);
        Assert.Contains("0 deployed-and-hashable", emptyDetail, StringComparison.Ordinal);
    }

    /// <summary>
    /// <see cref="LaneManifest.Derive"/> refuses at birth rather than emitting a document that disagrees
    /// with the stamp beside it — the disagreement above, reached through the producer.
    /// </summary>
    [Fact]
    public void Derive_refuses_to_emit_a_manifest_that_disagrees_with_its_stamp()
    {
        var error = Assert.Throws<InvalidOperationException>(() => LaneManifest.Derive(
            "valve",
            new[] { Emitted("FB_Unit"), Emitted("DB_Params") },
            CopyLayer(),
            "FB_Unit",
            Stamp(new[] { "FB_Unit" })));         // DB_Params was supplied and NOT hashed

        Assert.Contains("NAMED BUT NOT HASHED", error.Message, StringComparison.Ordinal);
        Assert.Contains("DB_Params", error.Message, StringComparison.Ordinal);
    }

    /// <summary>TIA resolves names case-insensitively, so the comparison must too — or it reports phantom gaps.</summary>
    [Fact]
    public void Names_are_compared_the_way_TIA_resolves_them()
    {
        var manifest = new LaneManifest(
            "valve",
            new[] { new ManifestObject("fb_unit", @"C:\ir\FB_Unit.ir", ObjectOrigin.Unstated) },
            Array.Empty<string>());

        Assert.True(manifest.AgreesWithStamp(Stamp(new[] { "FB_Unit" })).Agrees);
    }

    /// <summary>A manifest with no lane name cannot be matched to the lane it describes.</summary>
    [Fact]
    public void A_manifest_with_no_lane_name_is_refused()
    {
        Assert.Throws<ArgumentException>(() => LaneManifest.Derive(
            "  ", new[] { Emitted("FB_Unit") }, CopyLayer(), null, Stamp(new[] { "FB_Unit" })));
    }

    /// <summary>Derived, serialised, read back — the roles and origins survive the file the enqueue reads.</summary>
    [Fact]
    public void A_derived_manifest_round_trips_through_the_file_enqueue_reads()
    {
        var json = LaneManifest.Derive(
            "valve",
            new[] { Emitted("FB_Unit"), Emitted("DB_UnitInstance") },
            CopyLayer(), "FB_Unit",
            Stamp(new[] { "FB_Unit", "DB_UnitInstance" })).ToJson();

        var read = LaneManifest.Read("in-memory.json", _ => json);

        Assert.Equal("FB_Unit", read.BlockUnderTest);
        Assert.Equal(2, read.Objects.Count(o => o.Role == ObjectRole.CopyLayer));
        Assert.Equal(4, read.ProgramPaths.Count);
    }
}
