namespace Harness.Cleanup;

/// <summary>
/// <c>harness-cleanup</c> — DB-7's cleanup stage, given an entry point.
///
/// <para><b>Why it exists.</b> <c>Harness.Results/Cleanup.cs</c> implements DB-7 in full and had ZERO
/// references anywhere in <c>src/</c> outside its own file and its own tests. No executable reached it;
/// no production code called it. A library island passes every test it has and has never been asked a
/// question it did not already know the answer to — which is why the first run against the real corpus
/// found something no unit test could: X-J's number-range ownership rule cannot reach a TYPE or a
/// TAGTABLE at all, so two genuinely harness-owned objects are outside DB-7's scope in both
/// directions.</para>
///
/// <para>🔴 <b>THIS BINARY PLANS AND CANNOT DELETE.</b> <c>--yes</c>, <c>--force</c> and
/// <c>--confirm</c> are REFUSED BY NAME rather than ignored: silently accepting a confirmation flag
/// would let a caller believe a deletion happened. The delete verb lives in <c>openness-cli delete</c>,
/// behind Portal and its own fences, and a second path to the same destructive act would be a second
/// fence to keep correct. The plan emits the exact command instead.</para>
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            Usage(Console.Out);
            return (int)CleanupExit.Usage;
        }

        // *** REFUSED BY NAME, NOT IGNORED. *** `download-plan`'s shape, for `download-plan`'s reason.
        foreach (var confirm in new[] { "--yes", "--force", "--confirm", "--delete", "--execute" })
        {
            if (!args.Contains(confirm)) continue;

            Console.Error.WriteLine(
                $"refused: {confirm} is not accepted. harness-cleanup PLANS a cleanup batch and cannot perform one - "
                + "there is no confirmed form, and accepting the flag silently would let you believe a deletion "
                + "happened. Deletion is `openness-cli delete`, and this tool prints the exact command per removal.");
            return (int)CleanupExit.ConfirmationRefused;
        }

        var project = Option(args, "--project");
        if (string.IsNullOrWhiteSpace(project))
            return Refuse("--project <ir-dir> is required. There is no default corpus: a cleanup planned against a directory nobody named is one whose denominator means nothing.");

        var authority = Option(args, "--authority");
        if (string.IsNullOrWhiteSpace(authority))
            return Refuse("--authority is required. DB-7's third rule is that every removal records what, why AND on whose authority; with none recorded, Cleanup.Plan refuses every candidate and the run could not remove anything anyway.");

        var options = new CleanupOptions(
            ProjectDir: project,
            CrossCheckPath: Option(args, "--cross-check"),
            TestArtifactPaths: Options(args, "--test-artifact"),
            DrainReportPath: Option(args, "--drain-report"),
            Authority: authority,
            ClaimsJsonPath: Option(args, "--claims-json"),
            DeclaredModels: Options(args, "--model"));

        var outcome = CleanupRun.Execute(options);
        Console.Out.Write(outcome.Text);
        return (int)outcome.Exit;
    }

    private static int Refuse(string message)
    {
        Console.Error.WriteLine($"usage error: {message}");
        Console.Error.WriteLine("  Nothing was examined, nothing was deleted, and no claim was released.");
        return (int)CleanupExit.Usage;
    }

    private static void Usage(TextWriter output)
    {
        output.WriteLine("harness-cleanup - DB-7's cleanup stage. PLANS a batch; CANNOT delete.");
        output.WriteLine();
        output.WriteLine("  --project <ir-dir>          required. The corpus. Its object count is the denominator.");
        output.WriteLine("  --authority <text>          required. DB-7 rule 3: on whose authority. No default.");
        output.WriteLine("  --test-artifact <file>      required, repeatable. A vector/binding artifact of an ADMITTED test.");
        output.WriteLine("                              With none, every model and block reads as referenced by nothing.");
        output.WriteLine("  --cross-check <file.json>   `converter cross-check --project <ir> --json`. DB-7 rule 2's");
        output.WriteLine("                              evidence. Omitted => Cleanup.Plan refuses (GraphNotAvailable).");
        output.WriteLine("  --drain-report <file>       DB-7 rule 1's in-flight set. Omitted => Cleanup.Plan refuses");
        output.WriteLine("                              (DrainStateUnknown). Unknown is not drained.");
        output.WriteLine("  --claims-json <file>        `converter claims --project <ir> --claims <ROOT> --json`.");
        output.WriteLine("                              Omitted => claim disposition is UNKNOWN, not 'nothing held'.");
        output.WriteLine("  --model <name>              repeatable. Declares a harness block to be a MODEL, which changes");
        output.WriteLine("                              what a removal is RECORDED as. A name no object declares is a hard");
        output.WriteLine("                              error, never a silent no-op.");
        output.WriteLine();
        output.WriteLine("  --yes / --force / --confirm / --delete / --execute   REFUSED BY NAME (exit 5).");
        output.WriteLine();
        output.WriteLine("drain report format - its own count and terminator, so a truncated write cannot read as drained:");
        output.WriteLine("  format=1");
        output.WriteLine("  computed-by=<who established this>");
        output.WriteLine("  computed-at=<ISO-8601 UTC>");
        output.WriteLine("  in-flight=<n>          must equal the number of test= lines");
        output.WriteLine("  test=<id>              zero or more");
        output.WriteLine("  end");
        output.WriteLine();
        output.WriteLine("exit codes: 0 planned (may be zero removals - read the SCOPE denominator) | 1 usage");
        output.WriteLine("            2 refused, a required input was absent | 3 NOTHING EXAMINED (not a pass)");
        output.WriteLine("            4 tests not drained | 5 a confirmation flag was passed | 6 an input was unreadable");
    }

    private static string? Option(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static IReadOnlyList<string> Options(string[] args, string name)
    {
        var values = new List<string>();
        for (var i = 0; i < args.Length - 1; i++)
            if (args[i] == name)
                values.Add(args[i + 1]);

        return values;
    }
}
