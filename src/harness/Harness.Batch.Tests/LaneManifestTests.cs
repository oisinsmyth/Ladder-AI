using Harness.Batch;

namespace Harness.Batch.Tests;

/// <summary>
/// 🔴 <b>The program set, EMITTED by what built the lane rather than typed on a command line.</b>
///
/// <para>The build stamp is computed over that set and means <i>"what is executing"</i>, so a mistyped or
/// stale <c>--program</c> stamps a program nobody deployed — measured, when a lane was pointed at a
/// pre-fix <c>Main</c> by hand. The manifest closes it from the construction side.</para>
/// </summary>
public sealed class LaneManifestTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lane-manifest-" + Guid.NewGuid().ToString("N"));

    public LaneManifestTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private string Write(string name, string json)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, json);
        return path;
    }

    private LaneManifest Sample() => new(
        "valve",
        new[]
        {
            new ManifestObject("FC_HarnessSlot", Path.Combine(_root, "slot.ir"), ObjectOrigin.Generated),
            new ManifestObject("FB_DemoStim", Path.Combine(_root, "stim.ir"), ObjectOrigin.Generated),
            new ManifestObject("FB_DemoUnderTest", Path.Combine(_root, "uut.ir"), ObjectOrigin.Authored),
        },
        new[] { "'FC_HarnessSlot' MUST be called from the cyclic OB, ahead of the copy layer." });

    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_manifest_round_trips_through_JSON_with_its_origins_intact()
    {
        var path = Write("m.json", Sample().ToJson());
        var read = LaneManifest.Read(path, File.ReadAllText);

        Assert.Equal("valve", read.LaneName);
        Assert.Equal(3, read.Objects.Count);
        Assert.Equal(2, read.Objects.Count(o => o.Origin == ObjectOrigin.Generated));
        Assert.Single(read.Objects.Where(o => o.Origin == ObjectOrigin.Authored));
        Assert.Single(read.Obligations);
    }

    /// <summary>Three objects, three distinct files — the program set is the distinct paths.</summary>
    [Fact]
    public void The_program_set_is_the_distinct_paths_in_manifest_order()
    {
        Assert.Equal(3, Sample().ProgramPaths.Count);
    }

    /// <summary>
    /// 🔴 <b>An empty manifest is REFUSED, not treated as a lane with nothing to say.</b> It would hand
    /// enqueue an empty program set, and the stamp over an empty set is the exact silence this type exists
    /// to remove — a correct download reporting <c>Stale</c>, measured once already.
    /// </summary>
    [Fact]
    public void A_manifest_naming_no_objects_is_refused()
    {
        var path = Write("empty.json", "{ \"laneName\": \"valve\", \"objects\": [], \"obligations\": [] }");

        var error = Assert.Throws<InvalidOperationException>(() => LaneManifest.Read(path, File.ReadAllText));
        Assert.Contains("NO objects", error.Message);
    }

    /// <summary>TIA matches an import by NAME, so an object without one cannot be reasoned about.</summary>
    [Fact]
    public void An_object_missing_its_name_or_path_is_refused()
    {
        var path = Write("unnamed.json",
            "{ \"laneName\": \"valve\", \"objects\": [ { \"name\": \"\", \"path\": \"x.ir\", \"origin\": \"Generated\" } ] }");

        var error = Assert.Throws<InvalidOperationException>(() => LaneManifest.Read(path, File.ReadAllText));
        Assert.Contains("missing a name or a path", error.Message);
    }

    [Fact]
    public void Unreadable_JSON_is_a_named_refusal_not_a_crash()
    {
        var path = Write("bad.json", "{ this is not json");

        var error = Assert.Throws<InvalidOperationException>(() => LaneManifest.Read(path, File.ReadAllText));
        Assert.Contains("not readable JSON", error.Message);
    }

    /// <summary>Absent obligations read back as an empty list — "nothing owed", not a null to trip over.</summary>
    [Fact]
    public void A_manifest_with_no_obligations_reads_back_as_an_empty_list()
    {
        var path = Write("noobs.json",
            "{ \"laneName\": \"v\", \"objects\": [ { \"name\": \"A\", \"path\": \"a.ir\", \"origin\": \"Authored\" } ] }");

        Assert.Empty(LaneManifest.Read(path, File.ReadAllText).Obligations);
    }

    // --- the disagreement, which is the point of having both ---------------------------------------

    /// <summary>No <c>--program</c> at all: the manifest simply supplies the set.</summary>
    [Fact]
    public void No_declared_paths_is_not_a_disagreement()
    {
        Assert.Null(Sample().Disagreement(Array.Empty<string>()));
    }

    /// <summary>The same set written differently — order, duplication, casing — is not a disagreement.</summary>
    [Fact]
    public void The_same_set_in_a_different_order_or_case_agrees()
    {
        var declared = Sample().ProgramPaths.Reverse().Select(p => p.ToUpperInvariant()).ToList();
        declared.Add(declared[0]);

        Assert.Null(Sample().Disagreement(declared));
    }

    /// <summary>
    /// 🔴 <b>A real disagreement is REFUSED, naming BOTH sides — the batch never picks a winner.</b> The
    /// manifest is the better source, but a caller who passed <c>--program</c> meant something by it, and
    /// silently overriding them swaps one unexamined program set for another.
    /// </summary>
    [Fact]
    public void A_genuine_disagreement_names_both_sides_and_chooses_neither()
    {
        var declared = new[] { Path.Combine(_root, "somewhere-else.ir") };

        var message = Sample().Disagreement(declared);

        Assert.NotNull(message);
        Assert.Contains("MANIFEST (3)", message);
        Assert.Contains("--program (1)", message);
        Assert.Contains("SOMEWHERE-ELSE.IR", message);
        Assert.Contains("will not choose between them", message);
    }

    /// <summary>A subset is still a disagreement — three paths and two paths are different program sets.</summary>
    [Fact]
    public void A_SUBSET_of_the_manifest_is_still_a_disagreement()
    {
        Assert.NotNull(Sample().Disagreement(Sample().ProgramPaths.Take(2).ToArray()));
    }

    // ---------------------------------------------------------------------------------------------
    // THE ROLE. A manifest that lists objects without saying which is the SUBJECT makes every per-block
    // check uncomposable — which is why `undriven-scan` had never been composed over the union.
    // ---------------------------------------------------------------------------------------------

    private static LaneManifest WithRoles(params ObjectRole[] roles) => new(
        "valve",
        roles.Select((r, i) => new ManifestObject($"Obj{i}", $"obj{i}.ir", ObjectOrigin.Generated, r)).ToArray(),
        Array.Empty<string>());

    [Fact]
    public void Exactly_one_block_under_test_is_named()
    {
        var manifest = WithRoles(ObjectRole.CopyLayer, ObjectRole.SlotFc, ObjectRole.StimulusHead, ObjectRole.BlockUnderTest);

        Assert.Equal("Obj3", manifest.BlockUnderTest);
    }

    /// <summary>
    /// 🔴 <b>No role stated is NULL, not a guess.</b> An older manifest carries no roles at all, and a
    /// per-block check pointed at an object nobody nominated is a confident answer about the wrong thing.
    /// </summary>
    [Fact]
    public void A_manifest_that_states_no_roles_names_no_block_under_test()
    {
        Assert.Null(Sample().BlockUnderTest);
    }

    /// <summary>
    /// 🔴 <b>TWO claimants is also null.</b> A lane tests one block; two is a defect in the manifest, and
    /// picking one would hide it behind a check that appeared to run.
    /// </summary>
    [Fact]
    public void TWO_objects_claiming_the_role_names_neither()
    {
        Assert.Null(WithRoles(ObjectRole.BlockUnderTest, ObjectRole.BlockUnderTest).BlockUnderTest);
    }

    /// <summary>The role survives the JSON round trip, or it would be derived once and lost immediately.</summary>
    [Fact]
    public void The_role_survives_a_JSON_round_trip()
    {
        var path = Write("roles.json", WithRoles(ObjectRole.SlotFc, ObjectRole.BlockUnderTest).ToJson());

        Assert.Equal("Obj1", LaneManifest.Read(path, File.ReadAllText).BlockUnderTest);
    }

    // ---------------------------------------------------------------------------------------------
    // THROUGH THE CLI. The point of the feature is which of the two paths a run took, and that has to be
    // legible in the report rather than inferable from the absence of a flag.
    // ---------------------------------------------------------------------------------------------

    private (int Exit, string Output) Enqueue(params string[] extra)
    {
        var binding = Path.Combine(_root, "b.json");
        var submission = Path.Combine(_root, "s.json");
        File.WriteAllText(binding, "{}");
        File.WriteAllText(submission, "{}");

        var args = new[]
        {
            "enqueue",
            "--queue", Path.Combine(_root, "queue"),
            "--lane", "valve",
            "--binding", binding,
            "--submission", submission,
        }.Concat(extra).ToArray();

        var writer = new StringWriter();
        var exit = BatchCli.Run(args, writer, File.ReadAllText, File.WriteAllText);
        return (exit, writer.ToString());
    }

    /// <summary>
    /// 🔴 <b>With a manifest, the set is DERIVED and the report says so — with the generated/authored
    /// split, which is how "how much of this lane is still hand-built" stays answerable.</b>
    ///
    /// <para><b>THREE buckets, not two.</b> The line counted Generated and called everything else
    /// "authored", which folds <see cref="ObjectOrigin.Unstated"/> — what <c>LaneManifest.Derive</c>
    /// honestly records for a program it read off disk — into "a person wrote it". This hand-authored
    /// sample states all three, so it pins the split rather than the sum.</para>
    /// </summary>
    [Fact]
    public void Enqueue_with_a_manifest_reports_the_set_as_DERIVED()
    {
        var manifest = Write("m.json", Sample().ToJson());
        var (exit, output) = Enqueue("--manifest", manifest);

        Assert.Equal(0, exit);
        Assert.Contains("DERIVED from the manifest: 3 object(s) (2 generated, 1 authored, 0 origin unstated)", output);
        Assert.Contains("OBLIGATION: 'FC_HarnessSlot' MUST be called from the cyclic OB", output);
    }

    /// <summary>
    /// 🔴 <b>THE NEGATIVE CONTROL, and the honest half.</b> Without a manifest the old path still works —
    /// and the report says the set was DECLARED, so a reader can tell a lane that emitted its program set
    /// from one where somebody typed it. Silence would make the two identical.
    /// </summary>
    [Fact]
    public void Enqueue_without_a_manifest_still_works_and_reports_the_set_as_DECLARED()
    {
        var (exit, output) = Enqueue("--program", Path.Combine(_root, "slot.ir"));

        Assert.Equal(0, exit);
        Assert.Contains("DECLARED by the caller: 1 path(s)", output);
        Assert.Contains("nothing checks it", output);
    }

    /// <summary>A manifest and a contradicting <c>--program</c> is refused, and NOTHING is queued.</summary>
    [Fact]
    public void Enqueue_refuses_a_manifest_that_disagrees_with_program_and_queues_nothing()
    {
        var manifest = Write("m.json", Sample().ToJson());
        var (exit, output) = Enqueue("--manifest", manifest, "--program", Path.Combine(_root, "elsewhere.ir"));

        Assert.NotEqual(0, exit);
        Assert.Contains("disagrees with the manifest", output);
        Assert.Empty(new LaneQueue(Path.Combine(_root, "queue")).All());
    }
}
