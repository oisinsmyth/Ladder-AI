using Harness.Results;

namespace Harness.Gate;

/// <summary>Exit codes for <c>harness-gate stamp</c>. Distinct from <see cref="GateExit"/>: different question.</summary>
public static class StampExit
{
    /// <summary>The file was stamped. Every ID in it recomputes from its own <c>normalised_text</c>.</summary>
    public const int Stamped = 0;

    /// <summary>The enumeration was refused. <b>Nothing was written</b>, so the file on disk is unchanged.</summary>
    public const int Refused = 1;

    /// <summary>Usage, or a file that could not be read. Nothing was examined.</summary>
    public const int NothingExamined = 2;

    /// <summary>
    /// Stamped, and <b>at least one ID moved because its text changed</b>. Its own code because it is not a
    /// failure and must not read like one: it is §3.2's dangling-ID EVENT, and every prior citation to a
    /// superseded ID is now STALE and has to be re-read against the new words. A script that treated it as
    /// success would let exactly the silent survival this scheme exists to prevent happen at the CI level.
    /// </summary>
    public const int StampedWithDanglingIds = 3;
}

/// <summary>
/// <c>harness-gate stamp &lt;enumeration.yaml&gt; [--in-place] [--out &lt;path&gt;]</c> — §3.4's stamping step.
///
/// <para>It exists because <c>assertion-enumerator</c> is denied <c>Bash</c> on purpose: that fence stops
/// the implementation contaminating a spec-side denominator, and it also removes every way to compute a
/// SHA-256. <b>The agent responsible for the denominator cannot produce the identifiers it is cited
/// by.</b> This does that, and needs no independence because the gate recomputes.</para>
/// </summary>
public static class StampCli
{
    public static int Run(
        IReadOnlyList<string> args,
        TextWriter output,
        Func<string, string> readFile,
        Action<string, string> writeFile)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(readFile);
        ArgumentNullException.ThrowIfNull(writeFile);

        if (args.Count < 2 || !string.Equals(args[0], "stamp", StringComparison.Ordinal))
        {
            Usage(output);
            return StampExit.NothingExamined;
        }

        var path = args[1];
        var inPlace = args.Contains("--in-place");
        var outIndex = args.ToList().IndexOf("--out");
        var outPath = outIndex >= 0 && outIndex + 1 < args.Count ? args[outIndex + 1] : null;

        if (inPlace && outPath is not null)
        {
            output.WriteLine("--in-place and --out name two different destinations. Pick one; writing both would leave two files claiming to be the citable enumeration.");
            return StampExit.NothingExamined;
        }

        string text;
        try
        {
            text = readFile(path);
        }
        catch (Exception ex)
        {
            output.WriteLine($"NOTHING EXAMINED — could not read '{path}': {ex.GetType().Name}: {ex.Message}");
            return StampExit.NothingExamined;
        }

        var result = EnumerationStamper.Stamp(text);

        if (!result.Stamped)
        {
            output.WriteLine("REFUSED — the enumeration was not stamped, and the file on disk is UNCHANGED.");
            output.WriteLine();
            foreach (var error in result.Errors)
                output.WriteLine("  - " + error);

            output.WriteLine();
            output.WriteLine("*** AN UNSTAMPED ENUMERATION HAS NOTHING CITABLE IN IT. *** A vector citing into one is a hard");
            output.WriteLine("error, never a lookup miss (assertion-enumeration.md section 3.4).");
            return StampExit.Refused;
        }

        Report(result, output);

        var destination = outPath ?? (inPlace ? path : null);
        if (destination is null)
        {
            output.WriteLine();
            output.WriteLine("DRY RUN — nothing was written. Pass --in-place, or --out <path>, to publish the stamped file.");
        }
        else
        {
            writeFile(destination, result.StampedText!);
            output.WriteLine();
            output.WriteLine($"WRITTEN: {destination}");
        }

        return result.Dangling.Count > 0 ? StampExit.StampedWithDanglingIds : StampExit.Stamped;
    }

    private static void Report(StampResult result, TextWriter output)
    {
        output.WriteLine("STAMPED");
        output.WriteLine("  " + result.Summary);
        output.WriteLine();

        var clause = string.Empty;
        foreach (var assertion in result.Assertions)
        {
            if (!string.Equals(clause, assertion.ClauseId, StringComparison.Ordinal))
            {
                clause = assertion.ClauseId;
                output.WriteLine($"  {clause}");
            }

            var note = assertion.ReStamped
                ? $"   *** RE-STAMPED, was {assertion.PreviousId} — EVERY CITATION TO THAT ID IS NOW STALE ***"
                : assertion.PreviousId is null ? string.Empty : "   (unchanged)";

            output.WriteLine($"    {assertion.Ordinal,-4} {assertion.Id,-24} {assertion.Form}{note}");
        }

        if (result.Dangling.Count > 0)
        {
            output.WriteLine();
            output.WriteLine("*** DANGLING IDs — THIS IS AN EVENT, NOT A WARNING ***");
            output.WriteLine("Each of these assertions was REWORDED, so its ID moved. A citation to the old ID does not");
            output.WriteLine("survive: the vector was written against the old words and nobody has re-read it.");
            foreach (var assertion in result.Dangling)
                output.WriteLine($"  {assertion.PreviousId}  ->  {assertion.Id}   ({assertion.Display})");
        }

        output.WriteLine();
        output.WriteLine("The display ordinals above are DISPLAY ONLY and are NEVER citable (section 3.3): a Basis in");
        output.WriteLine("`REQ-nnn.An` form is rejected mechanically, by shape.");
    }

    private static void Usage(TextWriter output)
    {
        output.WriteLine("usage: harness-gate stamp <enumeration.yaml> [--in-place | --out <path>]");
        output.WriteLine();
        output.WriteLine("Computes the assertion IDs (assertion-enumeration.md section 3.4) and writes them into the");
        output.WriteLine("enumeration. The enumerator issues `normalised_text:` and no `id:` — it is denied Bash on");
        output.WriteLine("purpose, so it cannot produce a SHA-256. This is that step.");
        output.WriteLine();
        output.WriteLine("IT NEVER PRESERVES AN ID WHOSE TEXT HAS CHANGED. A reworded assertion gets a NEW id, the old");
        output.WriteLine("one dangles, a `supersedes:` link is written and the run exits 3.");
        output.WriteLine();
        output.WriteLine("Without --in-place or --out it is a DRY RUN and writes nothing.");
        output.WriteLine();
        output.WriteLine($"exit {StampExit.Stamped} = stamped, no ID moved");
        output.WriteLine($"exit {StampExit.Refused} = REFUSED, nothing written, the file is unchanged");
        output.WriteLine($"exit {StampExit.NothingExamined} = nothing examined (usage, or the file could not be read)");
        output.WriteLine($"exit {StampExit.StampedWithDanglingIds} = stamped AND at least one ID moved — every citation to a superseded ID is STALE");
    }
}
