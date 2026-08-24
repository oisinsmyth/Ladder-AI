using Harness.Batch;

namespace Harness.Batch.Tests;

/// <summary>
/// 🔴 <b><c>harness-batch manifest</c> — the producer, end to end, and the DERIVED wording it makes
/// reachable.</b>
///
/// <para><c>BatchCli.Enqueue</c> has printed <i>"program set DECLARED by the caller … nothing emitted this
/// list, so nothing checks it against what the lane actually built"</i> for every lane ever enqueued,
/// because nothing emitted a manifest. These tests are the other branch actually being taken: generate,
/// write the manifest, enqueue from it, read DERIVED.</para>
///
/// <para><b><c>--check</c> is the arm that reaches the MISSING guard with a real input.</b> A manifest and
/// the stamp derived over that manifest's own paths agree by construction — <i>unless the files changed
/// underneath</i>, which is exactly how a lane came to be pointed at a pre-fix <c>Main</c>. The stale case
/// is driven below by editing an <c>.ir</c> file after the manifest was written.</para>
/// </summary>
public sealed class ManifestCommandTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "manifest-cmd-" + Guid.NewGuid().ToString("N"));
    private readonly string _binding;
    private readonly string _submission;
    private readonly string _ir;
    private readonly string _emit;
    private readonly string _out;

    public ManifestCommandTests()
    {
        Directory.CreateDirectory(_root);

        _binding = Path.Combine(_root, "lane.json");
        _submission = Path.Combine(_root, "sub.json");
        _ir = Path.Combine(_root, "ir");
        _emit = Path.Combine(_root, "emit");
        _out = Path.Combine(_root, "lane-manifest.json");

        File.WriteAllText(_binding,
            "{ \"blockName\": \"FC_HarnessCopyLayer\", \"blockNumber\": 9001, \"tagTableName\": \"HarnessMirror\", "
            + "\"tagPrefix\": \"HX_\", \"baseByte\": 1000, \"retentiveBytes\": 256, \"declaredRegisters\": 576, "
            + "\"slots\": [{ \"slotId\": \"S0\", \"startCondition\": \"Go\", "
            + "\"vectorTargets\": [{ \"tag\": \"In\", \"specName\": \"In\", \"type\": \"Int\" }], "
            + "\"resultSources\": [{ \"tag\": \"Out\", \"specName\": \"Out\", \"type\": \"Int\" }] }] }");

        File.WriteAllText(_submission, "{}");

        Directory.CreateDirectory(_ir);
        File.WriteAllText(Path.Combine(_ir, "FB_Unit.ir"), "BLOCK FB FB_Unit\nNETWORK 1 \"drive\"\n  COIL Out := Go\n");
        File.WriteAllText(Path.Combine(_ir, "DB_UnitInstance.ir"), "DB DB_UnitInstance\n  In : Int := 0\n  Out : Int := 0\n");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private (int Exit, string Output) Run(params string[] args)
    {
        var writer = new StringWriter();
        var exit = BatchCli.Run(args, writer, File.ReadAllText, File.WriteAllText, readBytes: File.ReadAllBytes);
        return (exit, writer.ToString());
    }

    private (int Exit, string Output) Produce(params string[] extra) => Run(new[]
    {
        "manifest",
        "--lane", "valve",
        "--binding", _binding,
        "--submission", _submission,
        "--program", _ir,
        "--emit", _emit,
        "--out", _out,
    }.Concat(extra).ToArray());

    // ---------------------------------------------------------------------------------------------
    // The producer, and the DERIVED wording it makes reachable
    // ---------------------------------------------------------------------------------------------

    /// <summary>THE CONTROL. A manifest is produced, it agrees with the stamp, and the file exists.</summary>
    [Fact]
    public void The_command_writes_a_manifest_that_agrees_with_the_stamp_it_was_derived_beside()
    {
        var (exit, output) = Produce("--block-under-test", "FB_Unit");

        Assert.Equal(BatchExit.Ok, exit);
        Assert.True(File.Exists(_out));
        Assert.Contains("manifest  DERIVED:", output, StringComparison.Ordinal);
        Assert.Contains("manifest AGREES with the build stamp", output, StringComparison.Ordinal);
        Assert.Contains("BLOCK UNDER TEST: FB_Unit", output, StringComparison.Ordinal);

        // The copy layer was WRITTEN, not merely named: a manifest whose paths do not exist turns into a
        // --program list that contributes nothing three steps later.
        var manifest = LaneManifest.Read(_out, File.ReadAllText);
        Assert.All(manifest.Objects, o => Assert.True(File.Exists(o.Path) || Directory.Exists(o.Path), o.Path));
    }

    /// <summary>
    /// 🔴 <b>THE BLOCK UNDER TEST IS IN THE STAMPED SET, and the stamp says so by moving.</b> Same lane,
    /// same binding, same instance DB — the only difference is whether the block's own <c>.ir</c> is in
    /// <c>--program</c>. Before Track 3 this pair produced ONE stamp.
    /// </summary>
    [Fact]
    public void The_stamp_reported_by_the_command_changes_when_the_block_under_test_is_in_the_program_set()
    {
        var dbOnly = Path.Combine(_root, "db-only");
        Directory.CreateDirectory(dbOnly);
        File.Copy(Path.Combine(_ir, "DB_UnitInstance.ir"), Path.Combine(dbOnly, "DB_UnitInstance.ir"));

        var without = Run("manifest", "--lane", "valve", "--binding", _binding, "--submission", _submission,
            "--program", dbOnly, "--emit", Path.Combine(_root, "e1"), "--out", Path.Combine(_root, "m1.json"));

        var with = Produce("--block-under-test", "FB_Unit");

        Assert.Equal(BatchExit.Ok, without.Exit);
        Assert.Equal(BatchExit.Ok, with.Exit);

        Assert.NotEqual(StampOf(without.Output), StampOf(with.Output));

        // The denominator, so "the stamps differ" cannot be true of two runs that hashed nothing.
        Assert.Contains("over 1 object(s)", without.Output, StringComparison.Ordinal);
        Assert.Contains("over 2 object(s)", with.Output, StringComparison.Ordinal);
    }

    private static string StampOf(string output) =>
        output.Split('\n').First(l => l.Contains("stamp ", StringComparison.Ordinal)).Trim();

    /// <summary>
    /// 🔴 <b>THE POINT OF THE WHOLE TRACK: <c>enqueue</c> now reaches its DERIVED branch.</b> The weaker
    /// DECLARED wording is asserted beside it — making the honest case unreachable while wiring the happy
    /// one would convert a loud absence into a silent assumption.
    /// </summary>
    [Fact]
    public void Enqueue_reports_DERIVED_from_a_produced_manifest_and_DECLARED_without_one()
    {
        Assert.Equal(BatchExit.Ok, Produce("--block-under-test", "FB_Unit").Exit);

        var derived = Run("enqueue", "--queue", Path.Combine(_root, "q1"), "--lane", "valve",
            "--binding", _binding, "--submission", _submission, "--manifest", _out);

        var declared = Run("enqueue", "--queue", Path.Combine(_root, "q2"), "--lane", "valve",
            "--binding", _binding, "--submission", _submission, "--program", _ir);

        Assert.Equal(BatchExit.Ok, derived.Exit);
        Assert.Contains("program set DERIVED from the manifest", derived.Output, StringComparison.Ordinal);
        Assert.Contains("block under test DERIVED from the manifest: FB_Unit", derived.Output, StringComparison.Ordinal);
        Assert.Contains("origin unstated", derived.Output, StringComparison.Ordinal);

        Assert.Equal(BatchExit.Ok, declared.Exit);
        Assert.Contains("program set DECLARED by the caller", declared.Output, StringComparison.Ordinal);
        Assert.Contains("staged corpus NONE", declared.Output, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 <b>A lane that states no subject still enqueues — owner's ruling 2026-08-24, REPORT DO NOT GATE
    /// — and the absence is a BANNER rather than a clause.</b>
    ///
    /// <para><c>LaneManifest.BlockUnderTest</c> is consumed by nothing: its intended consumer,
    /// <c>converter undriven-scan --fb</c>, is not wired, and outside tests the only readers are the two
    /// report lines this test and <see cref="A_produced_manifest_with_no_subject_says_so_in_a_banner"/>
    /// cover. So the report is the entire mechanism, and a subjectless lane must be impossible to mistake
    /// for one with a verified subject.</para>
    /// </summary>
    [Fact]
    public void A_manifest_with_no_subject_enqueues_and_says_so()
    {
        Assert.Equal(BatchExit.Ok, Produce().Exit);

        var enqueued = Run("enqueue", "--queue", Path.Combine(_root, "q3"), "--lane", "valve",
            "--binding", _binding, "--submission", _submission, "--manifest", _out);

        Assert.Equal(BatchExit.Ok, enqueued.Exit);
        Assert.Contains("BLOCK UNDER TEST: none stated.", enqueued.Output, StringComparison.Ordinal);
        Assert.Contains("SO NO PER-BLOCK CHECK APPLIES", enqueued.Output, StringComparison.Ordinal);

        // THE PAIRED CONTROL: a lane that DOES name one must not carry the banner, or the banner means
        // nothing.
        Assert.Equal(BatchExit.Ok, Produce("--block-under-test", "FB_Unit").Exit);

        var named = Run("enqueue", "--queue", Path.Combine(_root, "q3b"), "--lane", "valve",
            "--binding", _binding, "--submission", _submission, "--manifest", _out);

        Assert.Contains("block under test DERIVED from the manifest: FB_Unit", named.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("SO NO PER-BLOCK CHECK APPLIES", named.Output, StringComparison.Ordinal);
    }

    /// <summary>The producer says the same thing in the same words, so the two reports cannot drift apart.</summary>
    [Fact]
    public void A_produced_manifest_with_no_subject_says_so_in_a_banner()
    {
        var (exit, output) = Produce();

        Assert.Equal(BatchExit.Ok, exit);
        Assert.Contains("BLOCK UNDER TEST: none stated.", output, StringComparison.Ordinal);
        Assert.Contains("SO NO PER-BLOCK CHECK APPLIES: the stamp moves if ANY object changes", output, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // TRACK 3's refusal, through the CLI
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// The measured shape: the instance DB is in <c>--program</c> and the block itself is not, and the
    /// caller names the block as the subject anyway.
    /// </summary>
    [Fact]
    public void Naming_a_subject_that_is_not_in_the_program_set_is_refused_by_the_command()
    {
        var dbOnly = Path.Combine(_root, "db-only");
        Directory.CreateDirectory(dbOnly);
        File.Copy(Path.Combine(_ir, "DB_UnitInstance.ir"), Path.Combine(dbOnly, "DB_UnitInstance.ir"));

        var (exit, output) = Run("manifest", "--lane", "valve", "--binding", _binding, "--submission", _submission,
            "--program", dbOnly, "--emit", Path.Combine(_root, "e2"), "--out", Path.Combine(_root, "m2.json"),
            "--block-under-test", "FB_Unit");

        Assert.Equal(BatchExit.Refused, exit);
        Assert.Contains("REFUSED", output, StringComparison.Ordinal);
        Assert.Contains("did not hash it", output, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(_root, "m2.json")));
    }

    /// <summary>
    /// 🔴 <b>A NON-BLOCK NAMED AS THE SUBJECT IS REFUSED, AND THE REFUSAL NAMES THE KIND.</b>
    ///
    /// <para>*** MEASURED BY AN ADVERSARIAL AUDIT 2026-08-24: *** a directory holding only
    /// <c>DB_UnitInstance.ir</c>, with <c>--block-under-test DB_UnitInstance</c>, produced
    /// <c>stamp … over 1 object(s): DataBlock:DB_UnitInstance</c> and then <i>"BLOCK UNDER TEST:
    /// DB_UnitInstance — IN the stamped set, so changing it changes the stamp"</i>, at exit 0. <b>That is
    /// the documented Phase 10 defect verbatim</b> — a set carrying the unit's instance DB and no block —
    /// passing the guard built to close it, with a sentence of reassurance attached. The in-set guard
    /// matched by NAME and <c>EmittedObject</c> had dropped the kind on the way in.</para>
    /// </summary>
    [Fact]
    public void Naming_an_instance_DB_as_the_block_under_test_is_refused_and_the_kind_is_named()
    {
        var dbOnly = Path.Combine(_root, "db-subject");
        Directory.CreateDirectory(dbOnly);
        File.Copy(Path.Combine(_ir, "DB_UnitInstance.ir"), Path.Combine(dbOnly, "DB_UnitInstance.ir"));

        var (exit, output) = Run("manifest", "--lane", "valve", "--binding", _binding, "--submission", _submission,
            "--program", dbOnly, "--emit", Path.Combine(_root, "e3"), "--out", Path.Combine(_root, "m3.json"),
            "--block-under-test", "DB_UnitInstance");

        Assert.Equal(BatchExit.Refused, exit);
        Assert.Contains("it is a DataBlock, not a Block", output, StringComparison.Ordinal);
        Assert.Contains("DB_UnitInstance", output, StringComparison.Ordinal);

        // Nothing is written on a refusal, or the gap goes into a file that outlives the message.
        Assert.False(File.Exists(Path.Combine(_root, "m3.json")));
    }

    /// <summary>
    /// THE PAIRED CONTROL: the same lane with the BLOCK named as the subject derives cleanly. A kind guard
    /// that refused every subject would satisfy the test above.
    /// </summary>
    [Fact]
    public void Naming_the_block_as_the_subject_is_accepted_and_the_report_says_it_is_a_Block()
    {
        var (exit, output) = Produce("--block-under-test", "FB_Unit");

        Assert.Equal(BatchExit.Ok, exit);
        Assert.Contains("BLOCK UNDER TEST: FB_Unit — a Block, IN the stamped set", output, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // A denominator of zero — exit 2, "examined nothing", never a pass
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>AGREES OVER A ZERO DENOMINATOR IS NOT A PASS — it is exit 2, "examined nothing".</b>
    ///
    /// <para>Reachable through the emit-directory case <c>LaneManifest.Derive</c>'s own docstring
    /// contemplates: point <c>--program</c> at an earlier run's <c>--emit</c> directory and every object in
    /// it is the harness's own output, excluded from the hash BY NAME. *** MEASURED 2026-08-24: ***
    /// <c>stamp … over 0 object(s)</c>, then <i>"manifest AGREES with the build stamp … 0 object(s) the
    /// stamp hashed"</i>, exit 0. <c>StampAgreement</c>'s docstring names this exact shape as the disease —
    /// <i>"a bare AGREES over a manifest of zero objects and a stamp of zero objects is the shape of a
    /// check that compared nothing"</i> — and the mitigation chosen was to PRINT the counts.
    /// <c>ProgramManifest.HashedNothing</c> existed to tell the case apart and nothing consulted it.</para>
    ///
    /// <para><b>2 matches the converter's mechanical floor</b>, where <c>candidate-scan</c>,
    /// <c>undriven-scan</c>, <c>reuse-scan</c> and the rest all read exit 2 as "examined nothing".</para>
    /// </summary>
    [Fact]
    public void Pointing_the_program_at_an_earlier_emit_directory_examines_nothing_and_exits_2()
    {
        Assert.Equal(BatchExit.Ok, Produce().Exit);

        var (exit, output) = Run("manifest", "--lane", "valve", "--binding", _binding, "--submission", _submission,
            "--program", _emit, "--emit", Path.Combine(_root, "e4"), "--out", Path.Combine(_root, "m4.json"));

        Assert.Equal(BatchExit.Unusable, exit);   // 2 — examined nothing
        Assert.Contains("the build stamp hashed NOTHING", output, StringComparison.Ordinal);
        Assert.Contains("EXAMINED NOTHING", output, StringComparison.Ordinal);
        Assert.DoesNotContain("manifest AGREES", output, StringComparison.Ordinal);

        // Nothing written: a refused invocation must not leave a manifest describing a program of zero.
        Assert.False(File.Exists(Path.Combine(_root, "m4.json")));
    }

    /// <summary>
    /// THE PAIRED CONTROL for the zero-denominator refusal: a real lane hashes two objects and passes. A
    /// guard keyed on something wider — the presence of excluded objects, say — would refuse this too, and
    /// the ordinary lane DOES hand its copy layer back in on the <c>--check</c> path.
    /// </summary>
    [Fact]
    public void A_lane_with_a_real_program_is_not_caught_by_the_zero_denominator_refusal()
    {
        Assert.Equal(BatchExit.Ok, Produce("--block-under-test", "FB_Unit").Exit);

        var (exit, output) = Check(_out);

        Assert.Equal(BatchExit.Ok, exit);
        Assert.Contains("2 object(s) the stamp hashed", output, StringComparison.Ordinal);

        // …and the copy layer WAS handed back in and excluded, which is the shape the guard must tolerate.
        Assert.Contains("EXCLUDED as the harness's own output (2)", output, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // --check: the stale manifest
    // ---------------------------------------------------------------------------------------------

    private (int Exit, string Output) Check(string manifestPath) => Run(
        "manifest", "--lane", "valve", "--binding", _binding, "--submission", _submission, "--check", manifestPath);

    /// <summary>THE CONTROL: a manifest checked immediately after it was produced agrees.</summary>
    [Fact]
    public void A_freshly_produced_manifest_passes_its_own_check()
    {
        Assert.Equal(BatchExit.Ok, Produce("--block-under-test", "FB_Unit").Exit);

        var (exit, output) = Check(_out);

        Assert.Equal(BatchExit.Ok, exit);
        Assert.Contains("OK       manifest AGREES", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 <b>A RENAME under the manifest's own paths: both NAME arms fire.</b> The manifest still names
    /// <c>FB_Unit</c>; the file at that path now declares <c>FB_Unit_v2</c>. The stamp hashes what is on
    /// disk, so it names an object the manifest has never heard of AND the manifest names one the stamp did
    /// not hash.
    ///
    /// <para>⚠️ <b>CORRECTED DOCSTRING 2026-08-24. This test used to be labelled <i>"THE PRE-FIX
    /// <c>Main</c> CASE, reproduced"</i> and it is not that case.</b> A pre-fix <c>Main</c> and a post-fix
    /// <c>Main</c> HAVE THE SAME NAME — renaming the object is what makes this one catchable by a name-set
    /// comparison, and the founding incident had no rename in it. The test asserted the right thing under
    /// the wrong claim, which left the actual case uncovered while reading as covered; see
    /// <see cref="A_manifest_whose_block_changed_without_being_renamed_is_refused_as_content_drift"/> for
    /// the case the name promised.</para>
    /// </summary>
    [Fact]
    public void A_manifest_whose_object_was_RENAMED_under_its_own_paths_is_refused_and_both_name_arms_fire()
    {
        Assert.Equal(BatchExit.Ok, Produce("--block-under-test", "FB_Unit").Exit);

        File.WriteAllText(Path.Combine(_ir, "FB_Unit.ir"), "BLOCK FB FB_Unit_v2\nNETWORK 1 \"drive\"\n  COIL Out := NOT Go\n");

        var (exit, output) = Check(_out);

        Assert.Equal(BatchExit.Refused, exit);
        Assert.Contains("HASHED BUT NOT NAMED (1): FB_Unit_v2", output, StringComparison.Ordinal);
        Assert.Contains("NAMED BUT NOT HASHED (1): FB_Unit", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 <b>THE PRE-FIX <c>Main</c> CASE, ACTUALLY REPRODUCED: the block's logic is inverted and its NAME
    /// IS UNCHANGED.</b>
    ///
    /// <para>*** MEASURED BY AN ADVERSARIAL AUDIT 2026-08-24, and this is what it found: ***
    /// <code>
    /// before:  stamp 16#D78FEE7C  ... OK  manifest AGREES ...  exit 0
    /// after :  stamp 16#74E8F976  ... OK  manifest AGREES ...  exit 0
    /// </code>
    /// The stamp moved on the line directly above the verdict and <c>--check</c> still said OK, because
    /// <c>AgreesWithStamp</c> compared NAME SETS and both files declare <c>FB_Unit</c>. The founding
    /// incident this command cites — a lane pointed at a pre-fix <c>Main</c>, with the stamp following it —
    /// would not have been caught by the arm written to catch it.</para>
    ///
    /// <para><b>Three things are asserted, and the third is the one that makes the other two mean
    /// something:</b> the stamp moved, the verdict is REFUSED, and the refusal is CONTENT DRIFT rather than
    /// a name finding — the name arms must stay silent here or the diagnosis is wrong even when the exit
    /// code is right.</para>
    /// </summary>
    [Fact]
    public void A_manifest_whose_block_changed_without_being_renamed_is_refused_as_content_drift()
    {
        Assert.Equal(BatchExit.Ok, Produce("--block-under-test", "FB_Unit").Exit);

        var before = Check(_out);
        Assert.Equal(BatchExit.Ok, before.Exit);

        // The ONLY edit: the coil's condition is inverted. Same header, same object name.
        File.WriteAllText(Path.Combine(_ir, "FB_Unit.ir"), "BLOCK FB FB_Unit\nNETWORK 1 \"drive\"\n  COIL Out := NOT Go\n");

        var after = Check(_out);

        // The precondition the defect turned on: the stamp DID move, so a check that passed was passing
        // over a visibly different program.
        Assert.NotEqual(StampOf(before.Output), StampOf(after.Output));

        Assert.Equal(BatchExit.Refused, after.Exit);
        Assert.Contains("CONTENT DRIFT (1): FB_Unit", after.Output, StringComparison.Ordinal);
        Assert.Contains("STAMP MOVED", after.Output, StringComparison.Ordinal);

        // 🔴 THE DIAGNOSIS, not just the verdict. Nothing was renamed, so neither name arm may fire.
        Assert.DoesNotContain("HASHED BUT NOT NAMED", after.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("NAMED BUT NOT HASHED", after.Output, StringComparison.Ordinal);

        // THE DENOMINATOR: two objects were compared by hash, so "1 drifted" is out of a real total.
        Assert.Contains("CONTENT: 2 object(s) compared by SHA-256", after.Output, StringComparison.Ordinal);
    }

    /// <summary>
    /// THE PAIRED CONTROL for the arm above: an object that did NOT change still passes, and the pass says
    /// how many objects it compared. A content arm that refused everything would satisfy the test above.
    /// </summary>
    [Fact]
    public void An_unchanged_lane_passes_the_content_arm_and_states_what_it_compared()
    {
        Assert.Equal(BatchExit.Ok, Produce("--block-under-test", "FB_Unit").Exit);

        // Rewritten with identical bytes — the file's timestamp moves and its content does not.
        var path = Path.Combine(_ir, "FB_Unit.ir");
        File.WriteAllText(path, File.ReadAllText(path));

        var (exit, output) = Check(_out);

        Assert.Equal(BatchExit.Ok, exit);
        Assert.Contains("CONTENT: 2 object(s) compared by SHA-256", output, StringComparison.Ordinal);
        Assert.Contains("still hashes to it", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// The other half of stale: an object DELETED from the lane after the manifest was written.
    ///
    /// <para><b>It is refused one step EARLIER than the stamp comparison, and that is why the manifest
    /// records a FILE per object rather than the directory it sat in.</b> A directory would simply
    /// enumerate one fewer file and the stamp would be quietly short — the silent case this whole coverage
    /// item exists for. A file path that no longer exists cannot be read, and the refusal names it.</para>
    /// </summary>
    [Fact]
    public void An_object_removed_from_the_lane_after_the_manifest_was_written_is_refused_by_name()
    {
        Assert.Equal(BatchExit.Ok, Produce("--block-under-test", "FB_Unit").Exit);

        File.Delete(Path.Combine(_ir, "DB_UnitInstance.ir"));

        var (exit, output) = Check(_out);

        Assert.Equal(BatchExit.Unusable, exit);
        Assert.Contains("REFUSED", output, StringComparison.Ordinal);
        Assert.Contains("DB_UnitInstance.ir", output, StringComparison.Ordinal);

        // 🔴 THE PROPERTY BEHIND IT, asserted rather than trusted: every object in a produced manifest
        // names a FILE. If one ever named a directory, deleting a member of it would go unnoticed here.
        Assert.All(
            LaneManifest.Read(_out, File.ReadAllText).Objects,
            o => Assert.EndsWith(".ir", o.Path, StringComparison.OrdinalIgnoreCase));
    }

    // ---------------------------------------------------------------------------------------------
    // Argument handling — a command that decided nothing must say so
    // ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("--lane")]
    [InlineData("--binding")]
    [InlineData("--submission")]
    public void The_two_documents_and_the_lane_name_are_required(string flag)
    {
        var args = new[]
        {
            "manifest", "--lane", "valve", "--binding", _binding, "--submission", _submission,
            "--program", _ir, "--emit", _emit, "--out", _out,
        };

        var index = Array.IndexOf(args, flag);
        var (exit, output) = Run(args.Take(index).Concat(args.Skip(index + 2)).ToArray());

        Assert.Equal(BatchExit.Unusable, exit);
        Assert.Contains("manifest needs --lane", output, StringComparison.Ordinal);
    }

    /// <summary><c>--emit</c> is not optional: without it the generated objects have no path to record.</summary>
    [Fact]
    public void Producing_without_an_emit_directory_or_an_out_path_is_refused()
    {
        var (exit, output) = Run("manifest", "--lane", "valve", "--binding", _binding, "--submission", _submission,
            "--program", _ir, "--out", _out);

        Assert.Equal(BatchExit.Unusable, exit);
        Assert.Contains("--emit is not optional", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>--check</c> reports on a manifest and does not build one. Accepting both would let one command
    /// report on one document and write another.
    /// </summary>
    [Fact]
    public void Checking_and_producing_in_one_invocation_is_refused()
    {
        Assert.Equal(BatchExit.Ok, Produce("--block-under-test", "FB_Unit").Exit);

        var (exit, output) = Run("manifest", "--lane", "valve", "--binding", _binding, "--submission", _submission,
            "--check", _out, "--program", _ir, "--emit", _emit, "--out", _out);

        Assert.Equal(BatchExit.Unusable, exit);
        Assert.Contains("does not build one", output, StringComparison.Ordinal);
    }

    /// <summary><c>manifest</c> takes no queue — it runs at build time, before the lane exists.</summary>
    [Fact]
    public void The_command_needs_no_queue()
    {
        Assert.Equal(BatchExit.Ok, Produce().Exit);
    }

    /// <summary>An unreadable program set is a refusal naming the file, and nothing is written.</summary>
    [Fact]
    public void An_unclassifiable_program_file_is_refused_and_no_manifest_is_written()
    {
        File.WriteAllText(Path.Combine(_ir, "junk.ir"), "this is not IR\n");

        var (exit, output) = Produce();

        Assert.Equal(BatchExit.Unusable, exit);
        Assert.Contains("REFUSED", output, StringComparison.Ordinal);
        Assert.False(File.Exists(_out));
    }

    /// <summary>The verb is listed where an unknown one is reported, or nobody finds it.</summary>
    [Fact]
    public void The_verb_is_named_in_the_unknown_subcommand_message()
    {
        var (exit, output) = Run("manifesto");

        Assert.Equal(BatchExit.Unusable, exit);
        Assert.Contains("enqueue, manifest, plan, list, dequeue, run", output, StringComparison.Ordinal);
    }
}
