using System.Text;
using Harness.Results;

// This project's NAMESPACE is Harness.Cleanup and the library's static class is Harness.Results.Cleanup,
// so a bare `Cleanup.Plan` binds to the namespace and does not compile. The alias is the fix; renaming
// either would cost more than it buys.
using CleanupStage = Harness.Results.Cleanup;

namespace Harness.Cleanup;

/// <summary>
/// <b>DB-7's cleanup stage, driven end to end against a real corpus.</b>
///
/// <para>The stage's judgement lives in <see cref="Harness.Results.Cleanup"/> and is not duplicated
/// here: this reads the inputs, hands <see cref="Harness.Results.Cleanup.Plan"/> a candidate set, a
/// drain state and a graph, and renders what came back. Anything that decides ELIGIBILITY belongs
/// there — a second implementation of the rule would be a check that shares its subject's blind spot.
/// </para>
///
/// <para><b>The refusals are ROUTED, not shadowed.</b> When <c>--drain-report</c> or
/// <c>--cross-check</c> is not given at all, this passes <c>null</c> through to <c>Plan</c> and reports
/// the refusal IT produced, rather than refusing first and leaving the library's guard permanently
/// unexercised on the live path. A flag that names a file which does not EXIST is a different fact — a
/// typo, not an omission — and is refused here with the path.</para>
///
/// <para><b>What this file adds is scope, never judgement:</b> which objects the stage OWNS (X-J's
/// reserved number range), which objects X-J's rule cannot reach at all, a denominator on each of
/// those, and the §16.12c claim disposition per removal.</para>
/// </summary>
public static class CleanupRun
{
    /// <summary>The exit code and the text, so a caller can assert on both without a process.</summary>
    public sealed record Outcome(CleanupExit Exit, string Text);

    public static Outcome Execute(CleanupOptions options)
    {
        var output = new StringBuilder();
        void Line(string s = "") => output.Append(s).Append('\n');

        Line("harness-cleanup - DB-7 cleanup PLAN.");
        Line("*** DRY RUN, AND THERE IS NO OTHER MODE: this binary cannot delete. It emits the command; a");
        Line("    party holding the Portal token runs it. `--yes` is refused by name. ***");
        Line();

        if (!Directory.Exists(options.ProjectDir))
            return Refuse(output, CleanupExit.Refused, $"--project '{options.ProjectDir}' is not a directory. Nothing was examined.");

        if (options.TestArtifactPaths.Count == 0)
        {
            // DB-7 removes "models no VECTOR still references" and "blocks not in any ADMITTED TEST".
            // With no test artifact at all, both of those read as "nothing references it" — the input
            // whose absence produces a WRONG conclusion rather than a smaller one.
            return Refuse(output, CleanupExit.Refused,
                "at least one --test-artifact is required. DB-7 removes 'models no vector still references' and "
                + "'blocks not in any admitted test'; with no test artifact supplied, every model and every block "
                + "reads as referenced by nothing. That is not a smaller answer, it is a wrong one.");
        }

        CorpusScan.Result corpus;
        var artifacts = new List<(string Label, string Text)>();
        ClaimLedger? claims = null;
        DrainReport? drain = null;
        string? crossCheck = null;

        try
        {
            corpus = CorpusScan.Read(options.ProjectDir);

            foreach (var path in options.TestArtifactPaths)
                // The label is the path AS GIVEN, not its basename. Two of this project's real test
                // artifacts are both named harness-binding.json in different directories, and a
                // basename label would report the same provenance for two different documents — the
                // pairing-by-filename defect drift-check was repaired for on 2026-08-14.
                artifacts.Add((path, Required(path, "--test-artifact")));

            if (options.CrossCheckPath is { } xc) crossCheck = Required(xc, "--cross-check");
            if (options.DrainReportPath is { } dr) drain = DrainReport.Parse(Required(dr, "--drain-report"), Path.GetFileName(dr));
            if (options.ClaimsJsonPath is { } cj) claims = ClaimLedger.Parse(Required(cj, "--claims-json"), Path.GetFileName(cj));
        }
        catch (FileNotFoundException ex)
        {
            return Refuse(output, CleanupExit.Refused, ex.Message);
        }
        catch (CleanupInputException ex)
        {
            return Refuse(output, CleanupExit.InputUnreadable, ex.Message);
        }
        catch (IOException ex)
        {
            return Refuse(output, CleanupExit.InputUnreadable, ex.Message);
        }

        ReferenceGraphBuilder.Result? graph = null;
        if (crossCheck is not null)
        {
            try
            {
                graph = ReferenceGraphBuilder.Build(crossCheck, corpus.Objects, artifacts);
            }
            catch (CleanupInputException ex)
            {
                return Refuse(output, CleanupExit.InputUnreadable, ex.Message);
            }
        }

        var owned = corpus.Objects.Where(o => o.IsHarnessOwned).OrderBy(o => o.Name, StringComparer.Ordinal).ToArray();
        var unaddressable = corpus.Objects.Where(o => o.IsUnaddressableByNumber).OrderBy(o => o.Name, StringComparer.Ordinal).ToArray();

        var unknownModels = options.DeclaredModels
            .Where(m => !corpus.Objects.Any(o => string.Equals(o.Name, m, StringComparison.Ordinal)))
            .ToArray();
        if (unknownModels.Length > 0)
            return Refuse(output, CleanupExit.Usage,
                $"--model named {string.Join(", ", unknownModels)}, which no object in the corpus declares. "
                + "A declaration that lands on nothing is a silent no-op, and this one would silently change what a "
                + "removal is RECORDED as.");

        Line("INPUTS");
        Line($"  corpus            {options.ProjectDir}");
        Line($"                    {corpus.FilesSeen} file(s) -> {corpus.Objects.Count} object(s), {corpus.Unreadable.Count} unreadable");
        foreach (var u in corpus.Unreadable) Line($"                    UNREADABLE {u}");
        Line($"  reference graph   {options.CrossCheckPath ?? "NOT SUPPLIED"}");
        if (graph is not null)
            Line($"                    edges: {graph.CallEdges} call, {graph.InstanceDbRootEdges} instance-DB, {graph.PathEdges} path, {graph.InstanceOfEdges} INSTANCEOF, {graph.VectorEdges} test-artifact");
        Line($"  test artifacts    {artifacts.Count} file(s): {string.Join(", ", artifacts.Select(a => a.Label))}");
        Line($"  drain report      {options.DrainReportPath ?? "NOT SUPPLIED"}");
        Line($"  claims            {(claims is null ? "NOT SUPPLIED - claim disposition is UNKNOWN for every removal below" : $"store={claims.StorePath}  {claims.Claims.Count} claim(s) held")}");
        Line($"  authority         {(string.IsNullOrWhiteSpace(options.Authority) ? "NONE STATED" : options.Authority)}");
        Line();

        Line("SCOPE - the denominator. A cleanup that looked at nothing is not one that found nothing to do.");
        Line($"  objects examined                          {corpus.Objects.Count}");
        Line($"  harness-owned (X-J, numbers {IrObject.HarnessNumberFloor}-{IrObject.HarnessNumberCeiling})   {owned.Length}   <- the candidate set");
        Line($"  outside the reserved range                {corpus.Objects.Count - owned.Length}   deliverable content; never a cleanup candidate");
        Line($"  self-edges dropped                        {graph?.SelfEdgesDropped.Count.ToString() ?? "n/a"}   {(graph is null ? string.Empty : string.Join(", ", graph.SelfEdgesDropped))}");
        Line();
        Line($"  !! UNADDRESSABLE BY X-J'S OWNERSHIP RULE   {unaddressable.Length}");
        Line("    A TYPE and a TAGTABLE have NO NUMBER SPACE, so a reserved NUMBER RANGE cannot say whether the");
        Line("    harness owns one. These are neither claimed nor excluded - they are outside the reach of the");
        Line("    rule in BOTH directions, and are listed so the gap is visible rather than absent. Closing it");
        Line("    needs a declarer that is not a number; section 16.10 named none.");
        foreach (var u in unaddressable)
            Line($"      {u.Kind,-10} {u.Name}");
        Line();

        var models = options.DeclaredModels.ToHashSet(StringComparer.Ordinal);
        var candidates = owned.Select(o => new CleanupCandidate(
            o.Name,
            KindOf(o, models),
            WhyOf(o, models),
            options.Authority)).ToArray();

        var report = CleanupStage.Plan(candidates, drain?.TestsInFlight, graph?.Graph);

        Line("DRAIN - DB-7 rule 1. Unknown is not drained.");
        if (drain is null)
        {
            Line("  NO DRAIN REPORT SUPPLIED. The in-flight set was passed to Cleanup.Plan as null and it refused;");
            Line("  the verdict below is the library's, not this shim's.");
        }
        else
        {
            Line($"  declared by       {drain.ComputedBy}");
            Line($"  declared at       {drain.ComputedAt}");
            Line($"  tests in flight   {drain.TestsInFlight.Count}{(drain.IsDrained ? "  (an EMPTY set - the drain ran and nothing is outstanding)" : string.Empty)}");
            foreach (var t in drain.TestsInFlight) Line($"                    IN FLIGHT {t}");
            Line("  *** THIS IS A DECLARATION, NOT A MEASUREMENT. Nothing in this repository computes the in-flight");
            Line("     set. This reader can demand an author, demand a timestamp and detect a truncated file; it");
            Line("     cannot make a false declaration true.");
        }

        Line();
        Line("PLAN");
        Line("  " + report.Render().Replace("\n", "\n  "));
        Line();

        if (report.Dispositions.Count > 0)
        {
            Line("DISPOSITIONS - one line per candidate.");
            foreach (var d in report.Dispositions)
                Line($"  {d.Eligibility,-21} {d.Candidate.Kind,-11} {d.Candidate.Name}");
            Line();
            Line("  WHY EACH RETAINED OBJECT IS RETAINED - graph-proven, never inferred from a test finishing:");
            foreach (var d in report.Dispositions.Where(x => !x.Removable))
            {
                Line($"    {d.Candidate.Name}");
                var edges = graph is not null && graph.Edges.TryGetValue(d.Candidate.Name, out var e)
                    ? e
                    : Array.Empty<ReferenceGraphBuilder.Edge>();
                if (edges.Count == 0) Line($"      {d.Detail}");
                foreach (var edge in edges) Line($"      <- {edge.Detail}");
            }

            Line();
        }

        Line("CLAIM RELEASE - spec section 16.12c: every removal path must release its claim, or numbers leak.");
        if (report.Removals.Count == 0)
            Line("  No removal is planned, so no claim is stranded by this run.");

        foreach (var d in report.Removals)
        {
            var o = owned.Single(x => x.Name == d.Candidate.Name);
            var held = claims?.For(o) ?? Array.Empty<HeldClaim>();
            var disposition = claims is null ? ClaimDisposition.NotChecked
                : held.Count > 0 ? ClaimDisposition.Held
                : ClaimDisposition.NoClaimHeld;

            Line($"  {d.Candidate.Name}  [{disposition}]");
            foreach (var c in held)
                Line($"    HELD  {c.Kind} {c.Value} by {c.Agent} since {c.Created} - {c.Purpose}");
            Line(disposition switch
            {
                ClaimDisposition.NotChecked =>
                    "    No claims document was supplied. NOT the same as 'no claim held': this run did not look.",
                ClaimDisposition.NoClaimHeld =>
                    "    The store was read and holds no claim on this object. Nothing leaks here - but a number\n"
                    + "    allocated BEFORE the registry existed also holds no claim, and the two are indistinguishable\n"
                    + "    from this side.",
                _ => "    RELEASE REQUIRED. The command is emitted below; this binary does not run it.",
            });
        }

        if (report.Removals.Count > 0)
        {
            Line();
            Line("  COMMANDS, for the party that executes the deletion. NOT RUN HERE, and the ORDER MATTERS:");
            Line("  delete first, release second. A release issued before the block is gone hands the number to the");
            Line("  next allocator while the block is still in the project - the leak is slow, an early release is an");
            Line("  immediate collision.");
            foreach (var d in report.Removals)
            {
                var o = owned.Single(x => x.Name == d.Candidate.Name);
                Line($"    openness-cli delete <project> --block {o.Name} --yes");
                foreach (var c in claims?.For(o) ?? Array.Empty<HeldClaim>())
                    Line($"    converter claims --project {options.ProjectDir} --claims <STORE ROOT> --release --agent {c.Agent} --kind {c.Kind} --value {c.Value}");
            }
        }

        var exit = report.Outcome switch
        {
            CleanupOutcome.Planned => CleanupExit.Planned,
            CleanupOutcome.TestsNotDrained => CleanupExit.TestsNotDrained,
            CleanupOutcome.NothingExamined => CleanupExit.NothingExamined,
            CleanupOutcome.DrainStateUnknown => CleanupExit.Refused,
            CleanupOutcome.GraphNotAvailable => CleanupExit.Refused,
            _ => CleanupExit.InputUnreadable,
        };

        if (exit == CleanupExit.NothingExamined)
        {
            Line();
            Line($"  NOTHING EXAMINED - this is not a pass. {corpus.Objects.Count} object(s) were read and none is");
            Line("  harness-owned, so DB-7's cleanup owns nothing here and this run proved nothing about it.");
        }

        Line();
        Line("WHAT THIS RUN DOES NOT ESTABLISH");
        Line("  - The drain state was DECLARED, not computed. See DRAIN above.");
        Line("  - A TYPE or a TAGTABLE cannot be owned by X-J's number rule, so this stage can neither propose");
        Line("    nor exclude one. Listed under SCOPE.");
        Line("  - The test-artifact scan is a whole-word text match. It over-references by design, so it can");
        Line("    retain something genuinely dead; it cannot release something that is live.");
        Line("  - Nothing was deleted, and no claim was released.");
        Line($"EXIT {(int)exit} {exit}");

        return new Outcome(exit, output.ToString());
    }

    /// <summary>
    /// DB-7 names THREE kinds, and a harness-owned GLOBAL DB is none of them — so it maps to
    /// <see cref="RemovalKind.Unstated"/> and <c>Cleanup.Plan</c> refuses it by that route. Fail-closed
    /// and reported: quietly filing it as a <see cref="RemovalKind.Block"/> would have the stage removing
    /// a class of object its own rule never sanctioned.
    /// </summary>
    public static RemovalKind KindOf(IrObject o, IReadOnlySet<string> declaredModels) =>
        o.Kind switch
        {
            IrObjectKind.InstanceDb => RemovalKind.InstanceDb,
            IrObjectKind.Block when declaredModels.Contains(o.Name) => RemovalKind.Model,
            IrObjectKind.Block => RemovalKind.Block,
            _ => RemovalKind.Unstated,
        };

    /// <summary>
    /// The <i>why</i> DB-7 requires, DERIVED rather than typed. A free-text reason supplied per candidate
    /// would be the author narrating their own removal; this states the facts the decision rested on and
    /// lets the graph evidence carry the rest.
    /// </summary>
    public static string WhyOf(IrObject o, IReadOnlySet<string> declaredModels)
    {
        var what = o.Kind switch
        {
            IrObjectKind.InstanceDb => $"a per-test instance DB of {o.InstanceOf}",
            IrObjectKind.Block when declaredModels.Contains(o.Name) => "a declared model",
            IrObjectKind.Block => $"a harness {o.SubKind}",
            _ => $"a harness-owned {o.Kind}",
        };

        return $"{what} at number {o.Number}, inside X-J's reserved harness range "
             + $"{IrObject.HarnessNumberFloor}-{IrObject.HarnessNumberCeiling}";
    }

    private static string Required(string path, string flag)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"{flag} '{path}' does not exist. It was NAMED, so this is a wrong path rather than an omission; "
                + "an absent input read as an empty one would report every object as unreferenced.");

        return File.ReadAllText(path);
    }

    private static Outcome Refuse(StringBuilder output, CleanupExit exit, string message)
    {
        output.Append("REFUSED: ").Append(message).Append('\n');
        output.Append("Nothing was planned, nothing was deleted, and no claim was released.\n");
        output.Append($"EXIT {(int)exit} {exit}\n");
        return new Outcome(exit, output.ToString());
    }
}
