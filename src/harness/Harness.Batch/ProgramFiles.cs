namespace Harness.Batch;

/// <summary>
/// What one requested program path actually contributed.
/// </summary>
/// <param name="Requested">The path as the lane gave it, so a refusal can quote what was typed.</param>
/// <param name="Files">The <c>.ir</c> files it resolved to. <b>Empty is the finding</b>, not a detail.</param>
/// <param name="Kind">Which of the three things the path turned out to be — see <see cref="ProgramPathKind"/>.</param>
public sealed record ProgramPathContribution(string Requested, IReadOnlyList<string> Files, ProgramPathKind Kind)
{
    public bool ContributedNothing => Files.Count == 0;
}

/// <summary>
/// 🔴 <b>Three outcomes, and the third is the one that used to be invisible.</b> A path that is neither a
/// file nor a directory returned an empty sequence indistinguishable from a directory that genuinely holds
/// no <c>.ir</c> — and both were indistinguishable from having asked for nothing at all.
/// </summary>
public enum ProgramPathKind
{
    /// <summary>A single <c>.ir</c> file, named directly.</summary>
    File = 1,

    /// <summary>A directory that was walked. It may still have contributed zero files.</summary>
    Directory = 2,

    /// <summary>🔴 <b>Neither exists.</b> A typo, a moved file, or a path from another machine.</summary>
    Missing = 3,
}

/// <summary>
/// 🔴 <b>THE ONE ENUMERATION OF A LANE'S PROGRAM FILES — and until now there were two.</b>
///
/// <para><c>BatchPlanner.FilesUnder</c> and <c>BatchCli.UnionFiles</c> were byte-for-byte the same rule
/// written twice, in two files, with no test holding them together. CLAUDE.md names this exact class:
/// <i>"when you write a SECOND derivation of an existing rule, the pinning test belongs in the same commit
/// as the copy"</i> — <c>GateParityTests</c> exists because two derivations once disagreed on twelve gate
/// inputs, and a batch planner then re-derived slot width and was short by the latch registers. These two
/// had not diverged yet. They were one edit away from it, and the edit was about to be made: fixing the
/// silent-empty defect in one and not the other would have left the planner and the drift check disagreeing
/// about which files the batch was even about.</para>
///
/// <para>🔴 <b>AND THE SHARED DEFECT: A PATH THAT IS NEITHER A FILE NOR A DIRECTORY RETURNED AN EMPTY
/// SEQUENCE, SILENTLY.</b> A typo'd or moved <c>--program</c> entry therefore shrank the union with no
/// line anywhere — and because the SAME set feeds the reachability check and the drift check, it shrank
/// both, in the same direction, at once. The build stamp is computed over this set and means <i>"what is
/// executing"</i>, so a set that is quietly short produces a stamp that describes a program nobody
/// deployed. That is <b>FI-44 one level in</b>: not a check that examined nothing, but a check whose
/// DENOMINATOR was wrong and which reported confidently over it.</para>
/// </summary>
public static class ProgramFiles
{
    /// <summary>
    /// Resolve every requested path, <b>keeping the ones that contributed nothing</b> rather than
    /// filtering them out. The caller decides what to do about them; this refuses to make them disappear.
    /// </summary>
    public static IReadOnlyList<ProgramPathContribution> Resolve(IEnumerable<string> requested)
    {
        ArgumentNullException.ThrowIfNull(requested);

        var contributions = new List<ProgramPathContribution>();

        foreach (var path in requested)
        {
            if (File.Exists(path))
            {
                contributions.Add(new ProgramPathContribution(path, new[] { path }, ProgramPathKind.File));
                continue;
            }

            if (Directory.Exists(path))
            {
                // TopDirectoryOnly, matching what both derivations did. Deliberately unchanged here: this
                // commit unifies the rule, it does not alter it. Changing depth at the same time would
                // make any resulting difference unattributable.
                var files = Directory.EnumerateFiles(path, "*.ir", SearchOption.TopDirectoryOnly)
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                contributions.Add(new ProgramPathContribution(path, files, ProgramPathKind.Directory));
                continue;
            }

            contributions.Add(new ProgramPathContribution(path, Array.Empty<string>(), ProgramPathKind.Missing));
        }

        return contributions;
    }

    /// <summary>Every file, deduplicated by full path, in the order the paths were given.</summary>
    public static IReadOnlyList<string> Under(IEnumerable<string> requested) =>
        Resolve(requested).SelectMany(c => c.Files).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    /// <summary>
    /// The refusals a caller should raise for paths that contributed nothing — <b>one per path, naming
    /// it</b>, because "the union is short" is not actionable and "this path names nothing" is.
    /// </summary>
    public static IReadOnlyList<string> Refusals(IReadOnlyList<ProgramPathContribution> contributions)
    {
        ArgumentNullException.ThrowIfNull(contributions);

        return contributions
            .Where(c => c.ContributedNothing)
            .Select(c => c.Kind == ProgramPathKind.Missing
                ? $"the program path '{c.Requested}' is neither a file nor a directory, so it contributed NOTHING to the union. "
                  + "The build stamp is computed over this set and means \"what is executing\", so a set that is quietly short "
                  + "stamps a program nobody deployed — and the same set feeds the reachability check, which would then have "
                  + "verified a program with a hole in it."
                : $"the program directory '{c.Requested}' holds no .ir files, so it contributed NOTHING to the union. "
                  + "An empty contribution is refused rather than ignored: a lane whose program is partly missing produces a "
                  + "stamp and a reachability verdict over the part that is left, and neither says so.")
            .ToArray();
    }

    /// <summary>
    /// The denominator line, printed on <b>every</b> run — the passing one included. Every other number in
    /// a batch report is a reason something did not happen; this one says how much there was.
    /// </summary>
    public static string Summary(IReadOnlyList<ProgramPathContribution> contributions)
    {
        ArgumentNullException.ThrowIfNull(contributions);

        var files = contributions.Sum(c => c.Files.Count);
        var empty = contributions.Count(c => c.ContributedNothing);

        return $"{files} program file(s) from {contributions.Count} requested path(s)"
             + (empty > 0 ? $"; {empty} path(s) contributed NOTHING" : string.Empty);
    }
}
