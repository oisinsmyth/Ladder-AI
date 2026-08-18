using Converter.Ir;
using Converter.SimaticMl;

namespace Converter.Review;

// Orchestrates running all Phase-1 rules against one or more .ir files. Reuses the exact
// BLOCK/DB /TYPE /TAGTABLE prefix dispatch Program.ConvertToXml already uses for .ir text -
// deliberately not a new dispatch mechanism.
public static class ReviewRunner
{
    // *** THE AUTHORITATIVE LIST OF MECHANIZED RULES. A RULE NOT IN HERE DOES NOT EXIST. ***
    // Public and consumed by the tests on purpose (2026-08-18): it was private and referenced by
    // nothing, while ReviewRunnerTests carried its own hand-copied duplicate that had already
    // drifted to 12 of the 18 — so the invariant "every rule gets a status on every content kind"
    // was being asserted against a stale subset, and a rule could be added, never wired into a
    // content-kind branch, and pass. Counting mentions of `C-nnn` anywhere else (docs, comments)
    // over-counts and is never the answer to "which rules run".
    public static readonly string[] AllRuleIds = { "C-001", "C-003", "C-005", "C-103", "C-118", "C-119", "C-120", "C-121", "C-122", "C-125", "C-201", "C-301", "C-501", "C-406", "C-408", "C-410", "C-102", "C-401", "C-404" };

    // udtIndex (optional) resolves cross-file references — today only C-118's interface-UDT Step
    // (FI-09), built from `--project` when supplied. Null means the caller ran `review` without
    // `--project`: C-118 can't resolve the enclosing UDT and is recorded NotApplicable per file.
    // *** THE TWO RULES THE HARNESS SCOPE MAY TOUCH, AND NO OTHERS. *** Owner ruling 2026-08-13
    // scopes the harness exemption to doc 06's NAMING conventions. C-103 was named explicitly as
    // staying a finding — it is behaviour, not naming, and it was recorded rather than silenced. A
    // future rule joins this set only by being added here on purpose.
    private static readonly string[] HarnessScopedRuleIds = { "C-001", "C-201" };

    // harnessScope (optional) supplies the DERIVED classification of reviewed content as
    // harness-generated or plant. *** ITS DEFAULT IS THE CONSERVATIVE ONE AND THAT IS DELIBERATE: ***
    // with no scope, blocks and DBs still classify from their own NUMBER (which is in the file being
    // reviewed), and no TAG can be classified at all — so a caller that forgets to build one gets the
    // full pre-2026-08-13 finding set, never a bypass. See HarnessScope for why there is no
    // `--harness` flag and why a tag table's NAME is never consulted.
    public static ReviewReport ReviewFiles(IReadOnlyList<string> paths, bool ignoreErrors, TagTypeRegistry? udtIndex = null, HarnessScope? harnessScope = null)
    {
        var scope = harnessScope ?? HarnessScope.Empty;
        var results = new List<FileReviewResult>();

        foreach (var path in paths)
        {
            try
            {
                results.Add(ReviewFile(path, udtIndex, scope));
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

    private static FileReviewResult ReviewFile(string path, TagTypeRegistry? udtIndex, HarnessScope scope)
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
            return ReviewTagTable(path, TagTableIrParser.ParseTagTable(text), scope);
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
        // index. Without one it can't run — and WHICH non-run this is depends on the block. A block
        // that never touches a Step register has nothing for C-118 to place, index or no index: that
        // is NotApplicable and a zero is meaningful. A block that DOES use one has a subject that
        // went unjudged: that is Skipped, and `converter review` fails closed on it (exit 2) rather
        // than printing a line a reader will file next to seventeen clean ones. Both used to read
        // "not applicable" (2026-08-13).
        if (udtIndex is not null)
        {
            Record(statuses, findings, "C-118", RuleCheckStatus.Checked, Rules.CheckC118StepInterfaceUdt(block, udtIndex));
        }
        else if (Rules.UsesStepRegister(block))
        {
            statuses.Add(new RuleStatusEntry("C-118", RuleCheckStatus.Skipped, 0, "this block USES a Step register but no --project index was supplied, so its interface UDT could not be resolved — the rule had a subject and was not judged"));
        }
        else
        {
            statuses.Add(new RuleStatusEntry("C-118", RuleCheckStatus.NotApplicable, 0, "this block references no Step register, so there is no stepped sequence for C-118 to place"));
        }

        Record(statuses, findings, "C-121", RuleCheckStatus.Checked, Rules.CheckC121StepTransition(block));

        // C-119/C-120 are single-file (step-number census) — always run; they yield nothing when the
        // block has no step logic.
        Record(statuses, findings, "C-119", RuleCheckStatus.Checked, Rules.CheckC119IdleIsStepZero(block));
        Record(statuses, findings, "C-120", RuleCheckStatus.Checked, Rules.CheckC120StepsMultipleOfTen(block));

        // C-122's PT-home check is cross-file (FI-09) — like C-118 it needs the --project index to
        // resolve the block's interface UDT. Same subject-bearing split: its subject is a step-gated
        // dwell timer, so a block with none is genuinely NotApplicable and a block with one that
        // went unjudged is Skipped.
        if (udtIndex is not null)
        {
            Record(statuses, findings, "C-122", RuleCheckStatus.Checked, Rules.CheckC122DwellTimerShape(block, udtIndex));
        }
        else if (Rules.HasStepGatedTimer(block))
        {
            statuses.Add(new RuleStatusEntry("C-122", RuleCheckStatus.Skipped, 0, "this block HAS a step-gated dwell timer but no --project index was supplied, so its PT home could not be resolved — the rule had a subject and was not judged"));
        }
        else
        {
            statuses.Add(new RuleStatusEntry("C-122", RuleCheckStatus.NotApplicable, 0, "this block has no step-gated dwell timer, which is C-122's only subject"));
        }

        // C-125's fault-bit-home check is cross-file (FI-09) — like C-118/C-122 it needs the
        // --project index to resolve the block's interface UDT. It shares C-122's subject test (a
        // timeout-fault bit only exists downstream of a step-gated dwell timer).
        if (udtIndex is not null)
        {
            Record(statuses, findings, "C-125", RuleCheckStatus.Checked, Rules.CheckC125TimeoutFaultInInterfaceUdt(block, udtIndex));
        }
        else if (Rules.HasStepGatedTimer(block))
        {
            statuses.Add(new RuleStatusEntry("C-125", RuleCheckStatus.Skipped, 0, "this block HAS a step-gated dwell timer whose timeout-fault bit could have a home to check, but no --project index was supplied — the rule had a subject and was not judged"));
        }
        else
        {
            statuses.Add(new RuleStatusEntry("C-125", RuleCheckStatus.NotApplicable, 0, "this block has no step-gated dwell timer, so it has no C-122 timeout-fault bit for C-125 to place"));
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

        Record(statuses, findings, "C-410", RuleCheckStatus.Checked, Rules.CheckC410SelfRestartingTimer(block));

        Record(statuses, findings, "C-102", RuleCheckStatus.CheckedVacuous, Rules.CheckC102NoJumps(block));
        Record(statuses, findings, "C-401", RuleCheckStatus.CheckedVacuous, Rules.CheckC401NoCounters(block));
        Record(statuses, findings, "C-404", RuleCheckStatus.CheckedVacuous, Rules.CheckC404NoBuiltInEdgeInstructions(block));

        return Partition(path, block.Name, findings, statuses, HarnessScope.ClassifyBlock(block));
    }

    // Split a file's findings into the ones that GATE and the ones reported under the harness bucket.
    //
    // *** THIS IS THE ONLY PLACE ANYTHING IS EXEMPTED, AND IT DROPS NOTHING. *** Every finding the
    // rules produced still exists, still printed, still counted — the harness ones simply live in a
    // list that ReviewOutcome does not read. Two properties worth keeping true:
    //   • ONLY a Harness verdict moves anything. Plant and Unclassified are identical here, on
    //     purpose: "I could not tell" must behave exactly like "plant", and differ only in the
    //     report's words (HarnessVerdict.Basis).
    //   • ONLY HarnessScopedRuleIds move. A rule outside that set is untouched no matter what the
    //     verdict says, so a classifier gone wrong cannot silence C-103 or anything else.
    private static FileReviewResult Partition(
        string path,
        string? name,
        IReadOnlyList<Finding> findings,
        IReadOnlyList<RuleStatusEntry> statuses,
        HarnessVerdict verdict)
    {
        if (verdict.Class != HarnessClass.Harness)
        {
            return new FileReviewResult(path, name, findings, statuses, null, verdict);
        }

        var scoped = findings.Where(f => HarnessScopedRuleIds.Contains(f.RuleId)).ToList();
        var gating = findings.Where(f => !HarnessScopedRuleIds.Contains(f.RuleId)).ToList();
        // *** FindingCount KEEPS ITS ONE MEANING EVERYWHERE: THE FINDINGS THAT GATE. *** The exempted
        // count goes in Reason. Overloading the same field with two meanings depending on status is
        // how a reader — and ReviewFiles_EveryCheckedStatusCountEqualsItsOwnPrintedFindings — would
        // start disagreeing with the list printed beneath it.
        var restated = statuses
            .Select(s => HarnessScopedRuleIds.Contains(s.RuleId) && s.Status == RuleCheckStatus.Checked
                ? s with
                {
                    Status = RuleCheckStatus.CheckedHarnessScope,
                    FindingCount = gating.Count(f => f.RuleId == s.RuleId),
                    Reason = HarnessScopeReason(scoped.Count(f => f.RuleId == s.RuleId)),
                }
                : s)
            .ToList();

        return new FileReviewResult(path, name, gating, restated, null, verdict, scoped);
    }

    private static string HarnessScopeReason(int exempted) =>
        $"{exempted} further finding(s) reported under HARNESS-SCOPE and NOT gated - this content is harness-generated, and doc 06's naming conventions govern content authored for the plant";

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
        statuses.Add(new RuleStatusEntry("C-125", RuleCheckStatus.NotApplicable, 0, "DB-kind file has no networks/timers/fault coils"));
        statuses.Add(new RuleStatusEntry("C-301", RuleCheckStatus.NotApplicable, 0, "DB-kind file has no networks"));
        statuses.Add(new RuleStatusEntry("C-501", RuleCheckStatus.NotApplicable, 0, "DB-kind file has no networks"));

        Record(statuses, findings, "C-406", RuleCheckStatus.Checked, Rules.CheckC406TimerDeclarations(db.Name, allMembers));

        statuses.Add(new RuleStatusEntry("C-408", RuleCheckStatus.NotApplicable, 0, "DB-kind file has no networks/instructions"));
        statuses.Add(new RuleStatusEntry("C-410", RuleCheckStatus.NotApplicable, 0, "DB-kind file declares timer instances but makes no timer CALL, so no IN expression exists here to read a timer's own Q back"));
        statuses.Add(new RuleStatusEntry("C-102", RuleCheckStatus.NotApplicable, 0, "DB-kind file has no networks/instructions"));
        statuses.Add(new RuleStatusEntry("C-401", RuleCheckStatus.NotApplicable, 0, "DB-kind file has no networks/instructions"));
        statuses.Add(new RuleStatusEntry("C-404", RuleCheckStatus.NotApplicable, 0, "DB-kind file has no networks/instructions"));

        return Partition(path, db.Name, findings, statuses, HarnessScope.ClassifyDb(db));
    }

    private static FileReviewResult ReviewType(string path, PlcTypeSource type)
    {
        var findings = new List<Finding>();
        var statuses = new List<RuleStatusEntry>();

        // A UDT is a named, commented member container with no networks. Until 2026-08-13 this
        // branch checked C-001 and stamped the other 17 rules "TYPE rule support: only C-001 …
        // checked in Phase 1" — the same defect the TAGTABLE branch had, collapsing "this rule has
        // nothing to inspect here" together with "nobody implemented it". Four of those 17 were
        // implementable against a type all along, and three were REAL: C-003 names `UDT_` in the same
        // breath as `FB_`/`FC_`/`DB_`, C-005's charset applies to a member name wherever it lives,
        // and C-201's header-comment half applies to any content kind carrying its own Comment (the
        // reasoning already written into CheckC201HeaderComment, and already applied to DBs).
        Record(statuses, findings, "C-001", RuleCheckStatus.Checked, Rules.CheckC001MemberNames(type.Name, type.Members));
        Record(statuses, findings, "C-003", RuleCheckStatus.Checked, Rules.CheckC003TypePrefix(type));
        Record(statuses, findings, "C-005", RuleCheckStatus.Checked, Rules.CheckC005CharsetDbMembers(type.Name, type.Members));
        Record(statuses, findings, "C-201", RuleCheckStatus.Checked, Rules.CheckC201HeaderComment(type.Name, type.Comment));
        Record(statuses, findings, "C-406", RuleCheckStatus.Checked, Rules.CheckC406TimerDeclarations(type.Name, type.Members));

        // The rest genuinely have no subject in a TYPE file — each with its own reason, so a reader
        // can check the claim instead of taking a blanket phrase on trust.
        statuses.Add(new RuleStatusEntry("C-102", RuleCheckStatus.NotApplicable, 0, "a TYPE file has no networks/instructions"));
        statuses.Add(new RuleStatusEntry("C-103", RuleCheckStatus.NotApplicable, 0, "a TYPE file has no coils"));
        statuses.Add(new RuleStatusEntry("C-118", RuleCheckStatus.NotApplicable, 0, "C-118 places a Step register relative to a BLOCK's interface UDT; a TYPE file is that UDT, it has no interface of its own"));
        statuses.Add(new RuleStatusEntry("C-119", RuleCheckStatus.NotApplicable, 0, "a TYPE file has no step logic"));
        statuses.Add(new RuleStatusEntry("C-120", RuleCheckStatus.NotApplicable, 0, "a TYPE file has no step logic"));
        statuses.Add(new RuleStatusEntry("C-121", RuleCheckStatus.NotApplicable, 0, "a TYPE file has no networks"));
        statuses.Add(new RuleStatusEntry("C-122", RuleCheckStatus.NotApplicable, 0, "a TYPE file has no timer calls"));
        statuses.Add(new RuleStatusEntry("C-125", RuleCheckStatus.NotApplicable, 0, "a TYPE file has no timer calls or coils"));
        statuses.Add(new RuleStatusEntry("C-301", RuleCheckStatus.NotApplicable, 0, "a TYPE file has no networks"));
        statuses.Add(new RuleStatusEntry("C-401", RuleCheckStatus.NotApplicable, 0, "a TYPE file has no networks/instructions"));
        statuses.Add(new RuleStatusEntry("C-404", RuleCheckStatus.NotApplicable, 0, "a TYPE file has no networks/instructions"));
        statuses.Add(new RuleStatusEntry("C-408", RuleCheckStatus.NotApplicable, 0, "a TYPE file has no comparisons"));
        statuses.Add(new RuleStatusEntry("C-410", RuleCheckStatus.NotApplicable, 0, "a TYPE file declares members only — it calls no timer, so nothing here can wire a timer's IN to its own Q"));
        statuses.Add(new RuleStatusEntry("C-501", RuleCheckStatus.NotApplicable, 0, "a TYPE file has no networks writing alarm-word bits"));

        // Always Unclassified — a UDT has no number and no referrer relation. Reported rather than
        // left blank, so a reader sees that the question was asked and answered "cannot tell".
        return Partition(path, type.Name, findings, statuses, HarnessScope.ClassifyType(type));
    }

    // A TAG TABLE is where tag NAMES live, which makes it the file kind C-001 applies to MOST, not
    // least. Until 2026-08-13 this branch reported all 18 rules "not applicable — TAGTABLE rule
    // support not implemented in Phase 1" and the run exited 0: a tag table was effectively
    // unreviewed while the summary read exactly like a clean review. The three-way split the old
    // wording collapsed, restored explicitly here:
    //   CHECKED       — C-001 (physical-IO format + address cross-check, and the variables layer for
    //                   flag tags), C-005 (charset, table name included), C-406 (declaration form).
    //   NOT APPLICABLE— the network/instruction/step/prefix rules, each with its OWN reason.
    //   SKIPPED       — nothing, now. The status exists and `converter review` fails closed on it
    //                   (exit 2), so the next unimplemented (kind, rule) pair cannot exit 0 quietly.
    private static FileReviewResult ReviewTagTable(string path, PlcTagTableSource table, HarnessScope scope)
    {
        var findings = new List<Finding>();
        var harnessScoped = new List<Finding>();
        var statuses = new List<RuleStatusEntry>();

        // *** C-001 IS CLASSIFIED PER TAG, NOT PER TABLE — AND THE TABLE'S NAME IS NEVER READ. ***
        // A tag table carries no number, so the only table-level property available is its NAME, and
        // a name is exactly what anything can be renamed into: were the exemption decided at table
        // level, "rename a plant tag table to HarnessMirror" would be a route out of review. Each tag
        // is instead classified by the blocks that reference it (HarnessScope), so laundering a plant
        // tag means moving every reference to it into blocks numbered inside the reserved band —
        // which breaks the plant program, where a rename costs nothing. A tag nobody references, or
        // one reviewed without its referrers in the corpus, is UNCLASSIFIED and its findings gate.
        var tagStatus = RuleCheckStatus.Checked;
        foreach (var tag in table.Tags)
        {
            var tagFindings = Rules.CheckC001TagName(table.Name, tag).ToList();
            if (scope.ClassifyTag(tag.Name).Class == HarnessClass.Harness)
            {
                harnessScoped.AddRange(tagFindings);
                if (tagFindings.Count > 0)
                {
                    tagStatus = RuleCheckStatus.CheckedHarnessScope;
                }
            }
            else
            {
                findings.AddRange(tagFindings);
            }
        }

        // A MIXED table reports CheckedHarnessScope with the GATING count, exactly as a block does:
        // the status line names the findings that gate, its reason names the ones that do not, and
        // the bucket below prints them. Neither number is hidden.
        statuses.Add(new RuleStatusEntry(
            "C-001",
            tagStatus,
            findings.Count(f => f.RuleId == "C-001"),
            tagStatus == RuleCheckStatus.CheckedHarnessScope
                ? HarnessScopeReason(harnessScoped.Count(f => f.RuleId == "C-001"))
                : null));

        Record(statuses, findings, "C-005", RuleCheckStatus.Checked, Rules.CheckC005TagTableCharset(table));
        Record(statuses, findings, "C-406", RuleCheckStatus.Checked, Rules.CheckC406TagDataTypes(table));

        // C-003 prescribes FB_/FC_/DB_/UDT_ and the iDB_ instance form. It names no tag-table
        // prefix — this is a rule that genuinely does not reach this content kind, not one nobody
        // got to.
        statuses.Add(new RuleStatusEntry("C-003", RuleCheckStatus.NotApplicable, 0, "C-003 prescribes block/DB/UDT name prefixes and names none for a tag table"));

        // C-301 is the interesting one: a tag table is FULL of absolute addresses, and that is
        // exactly what it is for. C-301 governs LOGIC reaching past the symbol to the address; the
        // symbol-to-address mapping is the mechanism that makes symbolic access possible, and there
        // is no logic in this file at all.
        statuses.Add(new RuleStatusEntry("C-301", RuleCheckStatus.NotApplicable, 0, "a tag table is the symbol-to-address mapping itself, not logic addressing absolutely — and it has no networks"));

        statuses.Add(new RuleStatusEntry("C-102", RuleCheckStatus.NotApplicable, 0, "a tag table has no networks/instructions"));
        statuses.Add(new RuleStatusEntry("C-103", RuleCheckStatus.NotApplicable, 0, "a tag table has no coils"));
        statuses.Add(new RuleStatusEntry("C-118", RuleCheckStatus.NotApplicable, 0, "a tag table has no step logic and no interface UDT"));
        statuses.Add(new RuleStatusEntry("C-119", RuleCheckStatus.NotApplicable, 0, "a tag table has no step logic"));
        statuses.Add(new RuleStatusEntry("C-120", RuleCheckStatus.NotApplicable, 0, "a tag table has no step logic"));
        statuses.Add(new RuleStatusEntry("C-121", RuleCheckStatus.NotApplicable, 0, "a tag table has no networks"));
        statuses.Add(new RuleStatusEntry("C-122", RuleCheckStatus.NotApplicable, 0, "a tag table has no timer calls"));
        statuses.Add(new RuleStatusEntry("C-125", RuleCheckStatus.NotApplicable, 0, "a tag table has no timer calls or coils"));
        statuses.Add(new RuleStatusEntry("C-201", RuleCheckStatus.NotApplicable, 0, "a tag table has no networks to title, and SW.Tags.PlcTagTable carries no header-comment field at all (per-tag comments are not C-201's subject)"));
        statuses.Add(new RuleStatusEntry("C-401", RuleCheckStatus.NotApplicable, 0, "a tag table has no networks/instructions"));
        statuses.Add(new RuleStatusEntry("C-404", RuleCheckStatus.NotApplicable, 0, "a tag table has no networks/instructions"));
        statuses.Add(new RuleStatusEntry("C-408", RuleCheckStatus.NotApplicable, 0, "a tag table has no comparisons"));
        statuses.Add(new RuleStatusEntry("C-410", RuleCheckStatus.NotApplicable, 0, "a tag table maps symbols to addresses and calls no timer — a self-restarting IN is a property of a timer call"));
        statuses.Add(new RuleStatusEntry("C-501", RuleCheckStatus.NotApplicable, 0, "a tag table has no networks writing alarm-word bits"));

        // NOT routed through Partition: the partition already happened per tag above, and re-running
        // it at file level on a mixed table would move findings that were deliberately kept gating.
        return new FileReviewResult(path, table.Name, findings, statuses, null, scope.ClassifyTagTable(table), harnessScoped);
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
