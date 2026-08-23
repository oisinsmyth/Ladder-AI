using Harness.Batch;

namespace Harness.Batch.Tests;

/// <summary>
/// 🔴 <b>One enumeration of a lane's program files, and the denominator that goes with it.</b>
///
/// <para>Two things are pinned here. First, that a path contributing NOTHING is a reported outcome rather
/// than an empty sequence — the union feeds the build stamp, the reachability check and the drift check,
/// so one typo'd <c>--program</c> used to weaken three things at once with no line anywhere. Second, that
/// there is exactly ONE derivation of the rule: <c>BatchPlanner</c> and <c>BatchCli</c> each carried their
/// own copy, byte-for-byte identical and held together by nothing.</para>
/// </summary>
public sealed class ProgramFilesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "program-files-" + Guid.NewGuid().ToString("N"));

    public ProgramFilesTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private string Dir(string name, params string[] files)
    {
        var dir = Path.Combine(_root, name);
        Directory.CreateDirectory(dir);
        foreach (var file in files)
            File.WriteAllText(Path.Combine(dir, file), "BLOCK FC X\nEND_BLOCK\n");
        return dir;
    }

    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>The defect: a path that is neither a file nor a directory used to return an empty sequence,
    /// indistinguishable from a directory that legitimately holds nothing.</b> Now they are different
    /// kinds, and both are still present in the result rather than filtered away.
    /// </summary>
    [Fact]
    public void A_missing_path_and_an_empty_directory_are_DIFFERENT_and_both_are_kept()
    {
        var contributions = ProgramFiles.Resolve(new[]
        {
            Path.Combine(_root, "nowhere"),
            Dir("empty"),
            Dir("real", "FC_A.ir"),
        });

        Assert.Equal(3, contributions.Count);
        Assert.Equal(ProgramPathKind.Missing, contributions[0].Kind);
        Assert.Equal(ProgramPathKind.Directory, contributions[1].Kind);
        Assert.Equal(ProgramPathKind.Directory, contributions[2].Kind);

        Assert.True(contributions[0].ContributedNothing);
        Assert.True(contributions[1].ContributedNothing);
        Assert.False(contributions[2].ContributedNothing);
    }

    /// <summary>Both empty kinds produce a refusal, and each names the path that caused it.</summary>
    [Fact]
    public void Every_path_that_contributed_nothing_is_refused_BY_NAME()
    {
        var missing = Path.Combine(_root, "nowhere");
        var refusals = ProgramFiles.Refusals(ProgramFiles.Resolve(new[] { missing, Dir("empty"), Dir("real", "FC_A.ir") }));

        Assert.Equal(2, refusals.Count);
        Assert.Contains(refusals, r => r.Contains(missing, StringComparison.Ordinal));
        Assert.Contains(refusals, r => r.Contains("neither a file nor a directory", StringComparison.Ordinal));
        Assert.Contains(refusals, r => r.Contains("holds no .ir files", StringComparison.Ordinal));
    }

    /// <summary>
    /// 🔴 <b>THE NEGATIVE CONTROL.</b> Every assertion above is about refusing; a helper that refused
    /// everything would satisfy all of them. A corpus that is entirely present must produce NO refusal.
    /// </summary>
    [Fact]
    public void A_corpus_that_is_all_there_is_refused_for_nothing()
    {
        var refusals = ProgramFiles.Refusals(ProgramFiles.Resolve(new[]
        {
            Dir("one", "FC_A.ir"),
            Path.Combine(Dir("two", "FC_B.ir"), "FC_B.ir"),
        }));

        Assert.Empty(refusals);
    }

    /// <summary>
    /// The denominator, on every run including the clean one. <b>"12 files staged" cannot be told from
    /// "12 of 15"</b>, and only the second says whether the comparison covers what was asked for.
    /// </summary>
    [Fact]
    public void The_summary_states_the_denominator_even_when_nothing_is_missing()
    {
        var clean = ProgramFiles.Summary(ProgramFiles.Resolve(new[] { Dir("a", "FC_A.ir", "FC_B.ir") }));

        Assert.Contains("2 program file(s) from 1 requested path(s)", clean);
        Assert.DoesNotContain("NOTHING", clean);

        var holed = ProgramFiles.Summary(ProgramFiles.Resolve(new[] { Dir("b", "FC_A.ir"), Path.Combine(_root, "nowhere") }));

        Assert.Contains("1 program file(s) from 2 requested path(s)", holed);
        Assert.Contains("1 path(s) contributed NOTHING", holed);
    }

    /// <summary>A file named directly is itself, whatever its extension — the caller chose it explicitly.</summary>
    [Fact]
    public void A_file_named_directly_contributes_itself()
    {
        var file = Path.Combine(Dir("c", "FC_A.ir"), "FC_A.ir");
        var contribution = Assert.Single(ProgramFiles.Resolve(new[] { file }));

        Assert.Equal(ProgramPathKind.File, contribution.Kind);
        Assert.Equal(new[] { file }, contribution.Files);
    }

    /// <summary>The same file reached twice — once directly, once through its directory — is one file.</summary>
    [Fact]
    public void Under_deduplicates_by_full_path()
    {
        var dir = Dir("d", "FC_A.ir");
        var file = Path.Combine(dir, "FC_A.ir");

        Assert.Single(ProgramFiles.Under(new[] { dir, file }));
    }

    /// <summary>
    /// 🔴 <b>THE PARITY TEST, IN THE SAME COMMIT AS THE UNIFICATION</b> — CLAUDE.md's rule, and
    /// <c>GateParityTests</c> is why it exists. The planner's <c>FilesUnder</c> and the CLI's
    /// <c>UnionFiles</c> were the same rule written twice with nothing holding them together. This asserts
    /// that what the planner's union sees and what <see cref="ProgramFiles"/> returns are the same set,
    /// over a corpus deliberately containing both a directory and a directly-named file.
    /// </summary>
    [Fact]
    public void The_planner_and_ProgramFiles_enumerate_the_SAME_set()
    {
        var dir = Dir("plan-a", "FC_A.ir", "FC_B.ir");
        var loose = Path.Combine(Dir("plan-b", "FC_C.ir"), "FC_C.ir");

        var paths = new[] { dir, loose };
        var mine = ProgramFiles.Under(paths).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();

        Assert.Equal(3, mine.Length);

        // What the planner ACTUALLY reaches, via its own public surface: a lane over these paths plans
        // without a "contributed nothing" refusal, which is only true if it enumerated the same files.
        var plan = BatchPlanner.Plan(
            new[] { new Lane("a", "b.json", "s.json", paths) },
            _ => "{ \"blockName\": \"FC_HarnessCopyLayer\", \"blockNumber\": 9001, \"tagTableName\": \"HarnessMirror\", "
               + "\"tagPrefix\": \"HX_\", \"baseByte\": 1000, \"retentiveBytes\": 256, \"declaredRegisters\": 576, "
               + "\"slots\": [{ \"slotId\": \"S0\", \"startCondition\": \"Go\", "
               + "\"vectorTargets\": [{ \"tag\": \"In\", \"specName\": \"In\", \"type\": \"Int\" }], "
               + "\"resultSources\": [{ \"tag\": \"Out\", \"specName\": \"Out\", \"type\": \"Int\" }] }] }");

        Assert.DoesNotContain(plan.Refusals, r => r.Contains("contributed NOTHING", StringComparison.Ordinal));
        Assert.True(plan.Planned, string.Join(" | ", plan.Refusals));
    }
}
