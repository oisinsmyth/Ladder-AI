using Converter.Ir;
using Converter.SimaticMl;

namespace Converter.Review;

// Orchestrates running all Phase-1 rules against one or more .ir files. Reuses the exact
// BLOCK/DB /TYPE /TAGTABLE prefix dispatch Program.ConvertToXml already uses for .ir text -
// deliberately not a new dispatch mechanism.
public static class ReviewRunner
{
    private static readonly string[] AllRuleIds = { "C-001", "C-003", "C-005", "C-103", "C-118", "C-119", "C-120", "C-121", "C-122", "C-201", "C-301", "C-501", "C-406", "C-408", "C-102", "C-401", "C-404" };

    // udtIndex (optional) resolves cross-file references — today only C-118's interface-UDT Step
    // (FI-09), built from `--project` when supplied. Null means the caller ran `review` without
    // `--project`: C-118 can't resolve the enclosing UDT and is recorded NotApplicable per file.
    public static ReviewReport ReviewFiles(IReadOnlyList<string> paths, bool ignoreErrors, TagTypeRegistry? udtIndex = null)
    {
        var results = new List<FileReviewResult>();

        foreach (var path in paths)
        {
            try
            {
                results.Add(ReviewFile(path, udtIndex));
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

    private static FileReviewResult ReviewFile(string path, TagTypeRegistry? udtIndex)
    {
        var text = File.ReadAllText(path);

        if (text.StartsWith("DB ", StringComparison.Ordinal))
        {
            return ReviewDb(path, DbIrParser.ParseDb(text));
        }

        if (text.StartsWith("TYPE ", StringComparison.Ordinal))
        {
            return ReviewType(path, TypeIrParser.ParseType(text));
        }

        if (text.StartsWith("TAGTABLE ", StringComparison.Ordinal))
        {
            // Tag-table entries are physical-IO tags, which keep their underscores by design (C-001)
            // - so no member-naming check applies, and no other Phase-1 rule inspects tag tables.
            var statuses = AllRuleIds.Select(id => new RuleStatusEntry(id, RuleCheckStatus.NotApplicable, 0, "TAGTABLE rule support not implemented in Phase 1")).ToList();
            return new FileReviewResult(path, null, Array.Empty<Finding>(), statuses, null);
        }

        // A block .ir may be sidecar-less (e.g. a hand-authored OB committed without one) - review
        // its logic anyway, the same branch preflight/ProjectIndex use. The sidecar carries
        // wire/UID layout, not the logic the rules inspect, so its absence doesn't affect findings.
        var block = IrParser.HasSidecarSection(text)
            ? IrParser.ParseBlock(text).Block
            : IrParser.ParseBlockWithoutSidecar(text);
        return ReviewBlock(path, block, udtIndex);
    }

    private static FileReviewResult ReviewBlock(string path, IrBlock block, TagTypeRegistry? udtIndex)
    {
        var findings = new List<Finding>();
        var statuses = new List<RuleStatusEntry>();

        var blockMembers = (block.InputMembers ?? Array.Empty<DbMember>())
            .Concat(block.OutputMembers ?? Array.Empty<DbMember>())
            .Concat(block.InOutMembers ?? Array.Empty<DbMember>())
            .Concat(block.StaticMembers ?? Array.Empty<DbMember>())
            .Concat(block.TempMembers ?? Array.Empty<DbMember>())
            .Concat(block.ConstantMembers ?? Array.Empty<DbMember>())
            .ToList();
        Record(statuses, findings, "C-001", RuleCheckStatus.Checked, Rules.CheckC001MemberNames(block.Name, blockMembers));

        Record(statuses, findings, "C-003", RuleCheckStatus.Checked, Rules.CheckC003BlockPrefix(block));
        Record(statuses, findings, "C-005", RuleCheckStatus.Checked, Rules.CheckC005Charset(block));

        Record(statuses, findings, "C-103", RuleCheckStatus.Checked, Rules.CheckC103SetResetPairing(block));

        // C-118 is cross-file (FI-09): it resolves the block's interface UDT from the --project
        // index. Without one it can't run — recorded NotApplicable so it's never silently absent.
        if (udtIndex is not null)
        {
            Record(statuses, findings, "C-118", RuleCheckStatus.Checked, Rules.CheckC118StepInterfaceUdt(block, udtIndex));
        }
        else
        {
            statuses.Add(new RuleStatusEntry("C-118", RuleCheckStatus.NotApplicable, 0, "C-118 needs --project to resolve the block's interface UDT (cross-file)"));
        }

        Record(statuses, findings, "C-121", RuleCheckStatus.Checked, Rules.CheckC121StepTransition(block));

        // C-119/C-120 are single-file (step-number census) — always run; they yield nothing when the
        // block has no step logic.
        Record(statuses, findings, "C-119", RuleCheckStatus.Checked, Rules.CheckC119IdleIsStepZero(block));
        Record(statuses, findings, "C-120", RuleCheckStatus.Checked, Rules.CheckC120StepsMultipleOfTen(block));

        // C-122's PT-home check is cross-file (FI-09) — like C-118 it needs the --project index to
        // resolve the block's interface UDT. Without one the whole rule is recorded NotApplicable so
        // it's never silently absent.
        if (udtIndex is not null)
        {
            Record(statuses, findings, "C-122", RuleCheckStatus.Checked, Rules.CheckC122DwellTimerShape(block, udtIndex));
        }
        else
        {
            statuses.Add(new RuleStatusEntry("C-122", RuleCheckStatus.NotApplicable, 0, "C-122 PT-home check needs --project to resolve the block's interface UDT (cross-file)"));
        }

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
            .Concat(Rules.CheckC406TimerDeclarations(block.Name, block.TempMembers ?? Array.Empty<DbMember>())).ToList();
        Record(statuses, findings, "C-406", RuleCheckStatus.Checked, usageFindings.Concat(declFindings));

        Record(statuses, findings, "C-408", RuleCheckStatus.Checked, Rules.CheckC408EtComparison(block));

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
        Record(statuses, findings, "C-001", RuleCheckStatus.Checked, Rules.CheckC001MemberNames(db.Name, allMembers));
        Record(statuses, findings, "C-005", RuleCheckStatus.Checked, Rules.CheckC005CharsetDbMembers(db.Name, allMembers));

        Record(statuses, findings, "C-201", RuleCheckStatus.Checked, Rules.CheckC201HeaderComment(db.Name, db.Comment));

        // No networks in a DB file - nothing for these to inspect.
        statuses.Add(new RuleStatusEntry("C-103", RuleCheckStatus.NotApplicable, 0, "DB-kind file has no networks/coils"));
        statuses.Add(new RuleStatusEntry("C-118", RuleCheckStatus.NotApplicable, 0, "DB-kind file has no interface UDT / step logic"));
        statuses.Add(new RuleStatusEntry("C-119", RuleCheckStatus.NotApplicable, 0, "DB-kind file has no step logic"));
        statuses.Add(new RuleStatusEntry("C-120", RuleCheckStatus.NotApplicable, 0, "DB-kind file has no step logic"));
        statuses.Add(new RuleStatusEntry("C-121", RuleCheckStatus.NotApplicable, 0, "DB-kind file has no networks"));
        statuses.Add(new RuleStatusEntry("C-122", RuleCheckStatus.NotApplicable, 0, "DB-kind file has no networks/timers"));
        statuses.Add(new RuleStatusEntry("C-301", RuleCheckStatus.NotApplicable, 0, "DB-kind file has no networks"));
        statuses.Add(new RuleStatusEntry("C-501", RuleCheckStatus.NotApplicable, 0, "DB-kind file has no networks"));

        Record(statuses, findings, "C-406", RuleCheckStatus.Checked, Rules.CheckC406TimerDeclarations(db.Name, allMembers));

        statuses.Add(new RuleStatusEntry("C-408", RuleCheckStatus.NotApplicable, 0, "DB-kind file has no networks/instructions"));
        statuses.Add(new RuleStatusEntry("C-102", RuleCheckStatus.NotApplicable, 0, "DB-kind file has no networks/instructions"));
        statuses.Add(new RuleStatusEntry("C-401", RuleCheckStatus.NotApplicable, 0, "DB-kind file has no networks/instructions"));
        statuses.Add(new RuleStatusEntry("C-404", RuleCheckStatus.NotApplicable, 0, "DB-kind file has no networks/instructions"));

        return new FileReviewResult(path, db.Name, findings, statuses, null);
    }

    private static FileReviewResult ReviewType(string path, PlcTypeSource type)
    {
        var findings = new List<Finding>();
        var statuses = new List<RuleStatusEntry>();

        // A UDT is a member container with no networks: only C-001 (member naming) applies. Every
        // other Phase-1 rule inspects networks/instructions/prefixes a type doesn't have.
        Record(statuses, findings, "C-001", RuleCheckStatus.Checked, Rules.CheckC001MemberNames(type.Name, type.Members));

        foreach (var id in AllRuleIds.Where(id => id != "C-001"))
        {
            statuses.Add(new RuleStatusEntry(id, RuleCheckStatus.NotApplicable, 0, "TYPE rule support: only C-001 (member naming) checked in Phase 1"));
        }

        return new FileReviewResult(path, type.Name, findings, statuses, null);
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
