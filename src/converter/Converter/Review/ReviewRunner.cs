using Converter.Ir;
using Converter.SimaticMl;

namespace Converter.Review;

// Orchestrates running all Phase-1 rules against one or more .ir files. Reuses the exact
// BLOCK/DB /TYPE /TAGTABLE prefix dispatch Program.ConvertToXml already uses for .ir text -
// deliberately not a new dispatch mechanism.
public static class ReviewRunner
{
    private static readonly string[] AllRuleIds = { "C-003", "C-005", "C-201", "C-301", "C-501", "C-406", "C-102", "C-401", "C-404" };

    public static ReviewReport ReviewFiles(IReadOnlyList<string> paths, bool ignoreErrors)
    {
        var results = new List<FileReviewResult>();

        foreach (var path in paths)
        {
            try
            {
                results.Add(ReviewFile(path));
            }
            catch (Exception ex) when (ex is SimaticMlFormatException or UnsupportedConstructException or NonReducibleNetworkException or IrFormatException)
            {
                if (!ignoreErrors)
                {
                    throw new ReviewFileException($"{path}: {ex.GetType().Name}: {ex.Message}");
                }

                results.Add(new FileReviewResult(path, null, Array.Empty<Finding>(), Array.Empty<RuleStatusEntry>(), $"{ex.GetType().Name}: {ex.Message}"));
            }
        }

        return new ReviewReport(results);
    }

    private static FileReviewResult ReviewFile(string path)
    {
        var text = File.ReadAllText(path);

        if (text.StartsWith("DB ", StringComparison.Ordinal))
        {
            return ReviewDb(path, DbIrParser.ParseDb(text));
        }

        if (text.StartsWith("TYPE ", StringComparison.Ordinal) || text.StartsWith("TAGTABLE ", StringComparison.Ordinal))
        {
            // Phase 1 pilot corpus has no TYPE/TAGTABLE files - handled honestly (every rule
            // reported NotApplicable with a stated reason) rather than silently mis-checked or
            // left to throw.
            var statuses = AllRuleIds.Select(id => new RuleStatusEntry(id, RuleCheckStatus.NotApplicable, 0, "TYPE/TAGTABLE rule support not implemented in Phase 1")).ToList();
            return new FileReviewResult(path, null, Array.Empty<Finding>(), statuses, null);
        }

        var (block, _) = IrParser.ParseBlock(text);
        return ReviewBlock(path, block);
    }

    private static FileReviewResult ReviewBlock(string path, IrBlock block)
    {
        var findings = new List<Finding>();
        var statuses = new List<RuleStatusEntry>();

        Record(statuses, findings, "C-003", RuleCheckStatus.Checked, Rules.CheckC003BlockPrefix(block));
        Record(statuses, findings, "C-005", RuleCheckStatus.Checked, Rules.CheckC005Charset(block));

        var headerFindings = Rules.CheckC201HeaderComment(block.Name, block.Comment).ToList();
        var titleFindings = Rules.CheckC201NetworkTitles(block).ToList();
        Record(statuses, findings, "C-201", RuleCheckStatus.Checked, headerFindings.Concat(titleFindings));

        Record(statuses, findings, "C-301", RuleCheckStatus.Checked, Rules.CheckC301AbsoluteAddressing(block));
        // C-501 findings are emitted alongside C-301's own by CheckC301AbsoluteAddressing (a
        // location failing the alarm-word exception implicates both rules at once) - status
        // recorded here too so it's never silently absent from the report.
        statuses.Add(new RuleStatusEntry("C-501", RuleCheckStatus.Checked, findings.Count(f => f.RuleId == "C-501"), null));

        var usageFindings = Rules.CheckC406TimerUsage(block).ToList();
        var declFindings = Rules.CheckC406TimerDeclarations(block.Name, block.StaticMembers ?? Array.Empty<DbMember>())
            .Concat(Rules.CheckC406TimerDeclarations(block.Name, block.TempMembers)).ToList();
        Record(statuses, findings, "C-406", RuleCheckStatus.Checked, usageFindings.Concat(declFindings));

        Record(statuses, findings, "C-102", RuleCheckStatus.CheckedVacuous, Rules.CheckC102NoJumps(block));
        Record(statuses, findings, "C-401", RuleCheckStatus.CheckedVacuous, Rules.CheckC401NoCounters(block));
        Record(statuses, findings, "C-404", RuleCheckStatus.CheckedVacuous, Rules.CheckC404NoBuiltInEdgeInstructions(block));

        return new FileReviewResult(path, block.Name, findings, statuses, null);
    }

    private static FileReviewResult ReviewDb(string path, DbSource db)
    {
        var findings = new List<Finding>();
        var statuses = new List<RuleStatusEntry>();

        Record(statuses, findings, "C-003", RuleCheckStatus.Checked, Rules.CheckC003DbPrefix(db));

        var allMembers = db.Members.Concat(db.InputMembers ?? Array.Empty<DbMember>())
            .Concat(db.OutputMembers ?? Array.Empty<DbMember>())
            .Concat(db.InOutMembers)
            .ToList();
        Record(statuses, findings, "C-005", RuleCheckStatus.Checked, Rules.CheckC005CharsetDbMembers(db.Name, allMembers));

        Record(statuses, findings, "C-201", RuleCheckStatus.Checked, Rules.CheckC201HeaderComment(db.Name, db.Comment));

        // No networks in a DB file - nothing for these to inspect.
        statuses.Add(new RuleStatusEntry("C-301", RuleCheckStatus.NotApplicable, 0, "DB-kind file has no networks"));
        statuses.Add(new RuleStatusEntry("C-501", RuleCheckStatus.NotApplicable, 0, "DB-kind file has no networks"));

        Record(statuses, findings, "C-406", RuleCheckStatus.Checked, Rules.CheckC406TimerDeclarations(db.Name, allMembers));

        statuses.Add(new RuleStatusEntry("C-102", RuleCheckStatus.NotApplicable, 0, "DB-kind file has no networks/instructions"));
        statuses.Add(new RuleStatusEntry("C-401", RuleCheckStatus.NotApplicable, 0, "DB-kind file has no networks/instructions"));
        statuses.Add(new RuleStatusEntry("C-404", RuleCheckStatus.NotApplicable, 0, "DB-kind file has no networks/instructions"));

        return new FileReviewResult(path, db.Name, findings, statuses, null);
    }

    private static void Record(List<RuleStatusEntry> statuses, List<Finding> findings, string ruleId, RuleCheckStatus status, IEnumerable<Finding> ruleFindings)
    {
        var list = ruleFindings.ToList();
        findings.AddRange(list);
        // Count only this rule's own findings, not everything the check returned: a check may
        // co-emit another rule's findings (CheckC301AbsoluteAddressing also yields C-501), and the
        // status line must match the findings actually printed under this rule ID (F-1 fix).
        statuses.Add(new RuleStatusEntry(ruleId, status, list.Count(f => f.RuleId == ruleId), null));
    }
}
